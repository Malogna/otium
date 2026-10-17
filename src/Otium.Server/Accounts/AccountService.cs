using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Otium.Core.Accounts;
using Otium.Core.Fees;
using Otium.Core.Pools;
using Otium.Core.Wallets;
using Otium.Server.Bandwidth;
using Otium.Server.Data;
using Otium.Server.Payouts;

namespace Otium.Server.Accounts;

/// <summary>An error with an HTTP status and a message that is safe to show to the user.</summary>
public sealed class OtiumException(int statusCode, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}

/// <summary>A signed-in computer and the account it belongs to (after following any merges).</summary>
public sealed record Session(DeviceRecord Device, AccountRecord Account);

/// <summary>
/// Accounts, computers, email sign-in and referrals. The first time a computer connects it gets its own account
/// (no sign-up needed). Adding an email secures that account; signing in with the same email on another computer
/// attaches that computer to the account, so a balance survives reinstalls and is shared across a user's PCs.
/// </summary>
public sealed class AccountService(
    OtiumStore store,
    PoolWalletAllocator allocator,
    IMoneroPoolApi pool,
    IUnmineableApi unmineable,
    IEmailSender email,
    IOptions<ReferralOptions> referralOptions,
    IOptions<EarningsOptions> earningsOptions,
    IOptions<CashoutOptions> cashoutOptions,
    CashoutMethodCatalog cashoutMethods,
    Gpu.CpuPoolRouter cpuRouter,
    BandwidthService bandwidth,
    TimeProvider time,
    ILogger<AccountService> logger,
    IOptions<CustodyOptions> custodyOptions,
    MystSponsorService mystSponsor,
    IOptions<FeeOptions> feeOptions)
{
    public static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(15);
    public const int MaxCodeAttempts = 5;
    public const int MaxCodesPerEmailPerHour = 5;
    public const int MaxCodesPerDevicePerHour = 10;

    /// <summary>The fee and invite terms apps are told about; fee-free periods are granted by the same terms.</summary>
    private FeePolicy Policy => FeePolicy.FromTerms(feeOptions.Value.ToTerms());
    private readonly ReferralOptions _referral = referralOptions.Value;
    private readonly EarningsOptions _earnings = earningsOptions.Value;
    private readonly CashoutOptions _cashouts = cashoutOptions.Value;
    private readonly bool _custodial = custodyOptions.Value.Enabled;

    /// <summary>
    /// How a payout target is stored and compared: "COIN:address" in canonical form, except Monero, which stays a bare
    /// address as it was stored before other coins existed (and as MoneroOcean expects it).
    /// </summary>
    public static string StoredWallet(PayoutTarget target) => target.Coin == PayoutCoins.Monero ? target.Address : target.ToString();

    // ---------- registration & authentication ----------

    public async Task<AccountSummary> RegisterAsync(RegisterDeviceRequest request, string publicBaseUrl, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.DeviceId) || request.DeviceId.Length > 128)
            throw new OtiumException(400, "Invalid device id.");
        if (string.IsNullOrWhiteSpace(request.DeviceSecret) || request.DeviceSecret.Length < 32)
            throw new OtiumException(400, "Invalid device secret.");

        string? ownWallet = null;
        if (!string.IsNullOrWhiteSpace(request.PayoutAddress))
        {
            if (!PayoutTarget.TryParse(request.PayoutAddress, out var target, out var addressError))
                throw new OtiumException(400, addressError!);
            ownWallet = StoredWallet(target);
        }

        var now = time.GetUtcNow();
        AccountRecord account;
        var device = store.FindDevice(request.DeviceId);
        if (device is null)
        {
            account = new AccountRecord(Guid.NewGuid().ToString("N"), NewWorkerId(), null, ownWallet, NewCode(), null,
                null, null, null, false, null, null, now);
            device = new DeviceRecord(request.DeviceId, HashSecret(request.DeviceSecret), account.AccountId, now, now);
        }
        else
        {
            RequireSecret(device, request.DeviceSecret);
            account = store.Resolve(store.FindAccount(device.AccountId)) ?? throw new OtiumException(404, "Account not found.");
            account = account with { OwnWallet = ownWallet };
            device = device with { AccountId = account.AccountId, LastSeenAt = now };
        }

        // Balance accounts need a pool address to mine to; keep it if switching back and forth between modes. Without
        // custody there are no balance accounts, only ones that haven't chosen their payout address yet.
        if (_custodial && account.IsBalanceAccount && account.PoolWallet is null)
            account = account with { PoolWallet = await allocator.AssignAsync(ct) };

        string? codeError = null;
        // ReferredAt stays set when the referrer deleted their account, so the invite can't be swapped for another one.
        if (!string.IsNullOrWhiteSpace(request.ReferralCode) && account.ReferredByCode is null && account.ReferredAt is null)
        {
            codeError = TryBind(ref account, ReferralCodes.Normalize(request.ReferralCode), now);
            if (codeError is not null)
                logger.LogInformation("Referral code rejected for {Account}: {Reason}", account.AccountId, codeError);
        }

        store.Upsert(account);
        store.Upsert(device);
        return Summarize(account, publicBaseUrl, codeError);
    }

    public Session Authenticate(string deviceId, string secret)
    {
        var device = store.FindDevice(deviceId) ?? throw new OtiumException(404, "Unknown device.");
        RequireSecret(device, secret);
        var account = store.Resolve(store.FindAccount(device.AccountId)) ?? throw new OtiumException(404, "Account not found.");
        return new Session(device, account);
    }

    public async Task<AccountSummary> GetSummaryAsync(string deviceId, string secret, string publicBaseUrl, CancellationToken ct)
    {
        var session = Authenticate(deviceId, secret);
        await RefreshVerificationsAsync(session.Account, ct);
        return Summarize(store.FindAccount(session.Account.AccountId)!, publicBaseUrl, null);
    }

    // ---------- invite leaderboard ----------

    public const int LeaderboardSize = 10;

    /// <summary>This month's top inviters by friends who started earning, with the caller's own place.</summary>
    public async Task<InviteLeaderboard> GetLeaderboardAsync(string deviceId, string secret, CancellationToken ct)
    {
        var session = Authenticate(deviceId, secret);
        await RefreshVerificationsAsync(session.Account, ct); // so the caller's own count is current
        var now = time.GetUtcNow();
        var since = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);

        // Codes of merged accounts keep crediting the surviving account, so count them there.
        var byAccount = new Dictionary<string, (string Code, int Friends)>(StringComparer.Ordinal);
        foreach (var (code, friends) in store.VerifiedReferralsSince(since))
        {
            if (store.Resolve(store.FindAccountByCode(code)) is not { } referrer)
                continue;
            var current = byAccount.GetValueOrDefault(referrer.AccountId, (Code: referrer.Code, Friends: 0));
            byAccount[referrer.AccountId] = (referrer.Code, current.Friends + (int)friends);
        }

        var ranked = byAccount
            .OrderByDescending(e => e.Value.Friends)
            .ThenBy(e => e.Value.Code, StringComparer.Ordinal)
            .Select(e => (AccountId: e.Key, e.Value.Code, e.Value.Friends))
            .ToList();
        var ranks = new int[ranked.Count];
        for (var i = 0; i < ranked.Count; i++)
            ranks[i] = i > 0 && ranked[i].Friends == ranked[i - 1].Friends ? ranks[i - 1] : i + 1;

        var me = ranked.FindIndex(e => e.AccountId == session.Account.AccountId);
        var top = ranked.Take(LeaderboardSize).Select((e, i) => new InviteLeaderboardEntry(
            ranks[i],
            e.AccountId == session.Account.AccountId ? "You" : ReferralCodes.Mask(e.Code),
            e.Friends,
            e.AccountId == session.Account.AccountId)).ToList();
        return new InviteLeaderboard(since, top, me >= 0 ? ranks[me] : null, me >= 0 ? ranked[me].Friends : 0);
    }

    // ---------- email sign-in ----------

    public async Task SendSignInCodeAsync(string deviceId, string secret, EmailCodeRequest request, CancellationToken ct)
    {
        var session = Authenticate(deviceId, secret);
        if (!EmailAddresses.IsValid(request.Email))
            throw new OtiumException(400, "That doesn't look like an email address.");

        var address = EmailAddresses.Normalize(request.Email);
        var deleting = request.Purpose == EmailCodePurposes.DeleteAccount;
        if (deleting && session.Account.Email != address)
            throw new OtiumException(400, "Use the email address this account is signed in with.");

        var now = time.GetUtcNow();
        if (store.EmailCodesSince(address, now - TimeSpan.FromHours(1)) >= MaxCodesPerEmailPerHour
            || store.DeviceEmailCodesSince(session.Device.DeviceId, now - TimeSpan.FromHours(1)) >= MaxCodesPerDevicePerHour)
            throw new OtiumException(429, "Too many codes requested. Please wait a while and try again.");

        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6", System.Globalization.CultureInfo.InvariantCulture);
        var salt = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16));
        store.AddEmailCode(address, session.Device.DeviceId, HashCode(code, salt), salt, now + CodeLifetime, now);

        if (deleting)
        {
            await email.SendAsync(
                request.Email.Trim(),
                $"Your code to delete your Otium account: {code}",
                $"Your code to permanently delete your Otium account is:\n\n    {code}\n\nIt expires in {CodeLifetime.TotalMinutes:0} minutes. " +
                "If you didn't ask for it, don't share it with anyone; your account stays as it is.",
                ct);
            return;
        }

        var existing = store.FindAccountByEmail(address);
        var purpose = existing is null || existing.AccountId == session.Account.AccountId
            ? "secure your Otium balance"
            : "sign in to your Otium account on this computer";
        await email.SendAsync(
            request.Email.Trim(),
            $"Your Otium code: {code}",
            $"Your code to {purpose} is:\n\n    {code}\n\nIt expires in {CodeLifetime.TotalMinutes:0} minutes. If you didn't ask for it, you can ignore this email.",
            ct);
    }

    public AccountSummary VerifySignInCode(string deviceId, string secret, EmailVerifyRequest request, string publicBaseUrl)
    {
        var session = Authenticate(deviceId, secret);
        var address = EmailAddresses.Normalize(request.Email);
        var now = time.GetUtcNow();
        ConsumeEmailCode(address, session.Device.DeviceId, request.Code);

        var current = session.Account;
        var owner = store.FindAccountByEmail(address);

        if (owner is null)
        {
            // New email: it now secures (or replaces the email of) this computer's account.
            current = current with { Email = address };
            store.Upsert(current);
            return Summarize(current, publicBaseUrl, null);
        }

        if (owner.AccountId == current.AccountId)
            return Summarize(current, publicBaseUrl, null);

        // The email belongs to another account: this computer joins it. If the computer had only an anonymous
        // account of its own, fold that into the signed-in account so nothing it earned is lost.
        if (current.Email is null && store.DeviceCount(current.AccountId) <= 1)
        {
            store.MergeAccount(current.AccountId, owner.AccountId);
            logger.LogInformation("Merged anonymous account {From} into {Into}", current.AccountId, owner.AccountId);
        }
        else
        {
            store.Upsert(session.Device with { AccountId = owner.AccountId, LastSeenAt = now });
        }

        return Summarize(store.FindAccount(owner.AccountId)!, publicBaseUrl, null);
    }

    /// <summary>
    /// Checks the newest code emailed to <paramref name="address"/> at this computer's request and marks it used, so
    /// each code works once. Throws with a user-facing reason when it's missing, expired, locked or wrong.
    /// </summary>
    public void ConsumeEmailCode(string address, string deviceId, string? code)
    {
        var stored = store.LatestEmailCode(address, deviceId, time.GetUtcNow())
                     ?? throw new OtiumException(400, "That code has expired. Request a new one.");
        if (stored.Attempts >= MaxCodeAttempts)
            throw new OtiumException(400, "Too many wrong tries. Request a new code.");

        var presented = Encoding.ASCII.GetBytes(HashCode((code ?? "").Trim(), stored.Salt));
        if (!CryptographicOperations.FixedTimeEquals(presented, Encoding.ASCII.GetBytes(stored.CodeHash)))
        {
            store.CountEmailCodeAttempt(stored.Id);
            throw new OtiumException(400, "That code isn't right. Check the email and try again.");
        }
        store.UseEmailCode(stored.Id);
    }

    // ---------- referrals ----------

    private string? TryBind(ref AccountRecord account, string code, DateTimeOffset now)
    {
        if (!ReferralCodes.IsWellFormed(code))
            return "That invite code doesn't look right.";
        if (now - account.CreatedAt > _referral.RedeemWindow)
            return "Invite codes can only be used in your first week.";

        var referrer = store.Resolve(store.FindAccountByCode(code));
        if (referrer is null)
            return "We couldn't find that invite code.";
        if (referrer.AccountId == account.AccountId || referrer.Code == account.Code)
            return "You can't use your own invite code.";
        if (account.OwnWallet is not null && string.Equals(referrer.OwnWallet, account.OwnWallet, StringComparison.Ordinal))
            return "You can't invite a device that pays out to your own wallet.";
        if (referrer.ReferredByCode == account.Code)
            return "Two people can't invite each other.";
        if (account.OwnWallet is not null && store.WalletAlreadyReferred(account.OwnWallet, account.AccountId))
            return "This wallet has already joined through an invite.";

        account = account with
        {
            ReferredByCode = referrer.Code,
            ReferredAt = now,
            FeeFreeUntil = ExtendHoliday(account.FeeFreeUntil, Policy.RefereeFeeHoliday, now),
        };
        return null;
    }

    /// <summary>
    /// Marks invited friends as verified once they have really contributed (balance accounts: credited earnings;
    /// Monero addresses: hashes reported by MoneroOcean; other coins: rewards from unMineable) and gives the referrer
    /// fee-free time.
    /// </summary>
    private async Task RefreshVerificationsAsync(AccountRecord referrer, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        foreach (var referee in store.FindReferees(referrer.Code).Where(r => !r.Verified))
        {
            if (referee.LastCheckedAt is { } checkedAt && now - checkedAt < _referral.VerificationRecheck)
                continue;

            bool verified;
            if (referee.IsBalanceAccount)
            {
                // Without custody this is an account that hasn't set a payout address, so it hasn't mined anything.
                verified = _custodial && store.LifetimeEarningMicros(referee.AccountId) >= ToMicros(_referral.VerificationUsd);
            }
            else
            {
                try
                {
                    verified = await PoolConfirmsMiningAsync(referee.OwnWallet!, ct);
                }
                catch (Exception e) when (e is HttpRequestException or TaskCanceledException or InvalidOperationException or System.Text.Json.JsonException && !ct.IsCancellationRequested)
                {
                    logger.LogWarning(e, "Pool lookup failed for referee {Account}", referee.AccountId);
                    continue;
                }
            }

            store.Upsert(referee with { Verified = verified, LastCheckedAt = now });
            if (verified)
            {
                var current = store.FindAccount(referrer.AccountId)!;
                store.Upsert(current with { FeeFreeUntil = ExtendHoliday(current.FeeFreeUntil, Policy.ReferrerFeeHolidayPerVerifiedFriend, now) });
            }
        }
    }

    /// <summary>Whether the pool that pays this stored wallet shows real mining by it.</summary>
    private async Task<bool> PoolConfirmsMiningAsync(string wallet, CancellationToken ct)
    {
        var target = PayoutTarget.ParseOrNull(wallet);
        if (target is null || target.Coin == PayoutCoins.Monero)
        {
            var stats = await pool.GetMinerStatsAsync(target?.Address ?? wallet, ct);
            return stats is not null && stats.TotalHashes >= _referral.VerificationHashes;
        }
        if (!_referral.VerifyUnmineableByRewards)
            return false;
        var address = await unmineable.GetAddressAsync(target, ct);
        if (address.Uuid is not { } uuid)
            return false;
        var mined = await unmineable.GetStatsAsync(uuid, ct);
        return mined.Rewarded7d > 0 || mined.Rewarded30d > 0 || mined.Paid > 0;
    }

    // ---------- summary ----------

    public AccountSummary Summarize(AccountRecord account, string publicBaseUrl, string? codeError)
    {
        var now = time.GetUtcNow();
        var referrer = account.ReferredByCode is null ? null : store.Resolve(store.FindAccountByCode(account.ReferredByCode));
        var referees = store.FindReferees(account.Code);
        var totals = store.Balance(account.AccountId, now);
        var cashouts = store.CashoutsFor(account.AccountId).Select(ToInfo).ToList();
        // Without custody nothing can be cashed out, so no methods are offered (the app hides the balance UI).
        var methods = _custodial ? cashoutMethods.Current : [];

        return new AccountSummary(
            account.WorkerId,
            _custodial && account.IsBalanceAccount ? account.PoolWallet : null,
            account.Code,
            $"{publicBaseUrl.TrimEnd('/')}/r/{account.Code}",
            // Own-wallet clients mine the referrer's slice to this wallet; balance accounts are rewarded server-side.
            _custodial && account.IsBalanceAccount ? null : referrer?.OwnWallet,
            account.ReferredAt,
            account.FeeFreeUntil,
            referees.Count,
            referees.Count(r => r.Verified),
            codeError,
            new BalanceInfo(
                ToUsd(Math.Max(0, totals.AvailableMicros)),
                ToUsd(totals.PendingMicros),
                ToUsd(totals.LifetimeMicros),
                ToUsd(totals.ReferralMicros),
                _cashouts.MinPayPalUsd,
                methods.Where(m => m.Method != CashoutMethods.PayPal).Select(m => m.MinUsd).DefaultIfEmpty(_cashouts.MinCryptoUsd).Min(),
                _earnings.FeePercent,
                _earnings.ReferralPercent,
                cashouts,
                ToUsd(store.PaidOutMicros(account.AccountId)),
                methods),
            account.Email,
            (int)store.DeviceCount(account.AccountId),
            _custodial ? bandwidth.Terms(account) : null,
            _custodial ? cpuRouter.For(account) : null,
            _custodial,
            mystSponsor.IsAvailable,
            Policy.ToTerms());
    }

    public static CashoutInfo ToInfo(CashoutRecord c) => new(
        c.Id, c.Method, MaskDestination(c.Method, c.Destination), ToUsd(c.Micros), c.Status, c.CreatedAt,
        UserNote(c), c.UpdatedAt, TransactionId(c));

    /// <summary>
    /// The part of a cash-out's note the user should see: why it failed, or that PayPal is waiting for them to claim
    /// it. Review reasons are internal (they describe fraud checks) and stay hidden.
    /// </summary>
    private static string? UserNote(CashoutRecord c) => c.Status switch
    {
        CashoutStatuses.Failed => c.Note,
        CashoutStatuses.Sending when c.Note == PayPalPayoutProvider.UnclaimedNote => c.Note,
        _ => null,
    };

    /// <summary>
    /// The transaction that delivered a paid crypto cash-out: the payout itself, or THORChain's outbound for a swap
    /// (never a Monero tx key, which the batch shares). Null for PayPal and for manual notes.
    /// </summary>
    public static string? TransactionId(CashoutRecord c)
    {
        if (c.Status != CashoutStatuses.Paid || c.ProviderReference is null || c.Method == CashoutMethods.PayPal)
            return null;
        var id = c.ProviderReference.Split(' ', 2)[0];
        var hex = id.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? id[2..] : id;
        return c.Method switch
        {
            CashoutMethods.Solana => id.Length is >= 80 and <= 90 && id.All(char.IsAsciiLetterOrDigit) ? id : null,
            _ => hex.Length == 64 && hex.All(char.IsAsciiHexDigit) ? id : null,
        };
    }

    public static decimal ToUsd(long micros) => decimal.Round(micros / (decimal)OtiumStore.MicrosPerDollar, 2, MidpointRounding.ToZero);

    public static long ToMicros(decimal usd) => (long)decimal.Round(usd * OtiumStore.MicrosPerDollar);

    public static string MaskDestination(string method, string destination)
    {
        if (method == CashoutMethods.PayPal)
        {
            var at = destination.IndexOf('@');
            return at > 2 ? destination[..2] + new string('•', at - 2) + destination[at..] : destination;
        }
        return destination.Length > 12 ? $"{destination[..6]}…{destination[^6..]}" : destination;
    }

    /// <summary>Adds fee-free time, never beyond the policy cap from now.</summary>
    private DateTimeOffset ExtendHoliday(DateTimeOffset? current, TimeSpan add, DateTimeOffset now)
    {
        var start = current is { } c && c > now ? c : now;
        var extended = start + add;
        var cap = now + Policy.MaxFeeHoliday;
        return extended > cap ? cap : extended;
    }

    private string NewCode()
    {
        while (true)
        {
            var code = RandomString(ReferralCodes.Alphabet, ReferralCodes.Length);
            if (!store.CodeExists(code))
                return code;
        }
    }

    /// <summary>
    /// The pool worker name. Short, lower-case, never an email (MoneroOcean treats an email password as the
    /// worker "email"), and unguessable so nobody can mine under someone else's id on purpose.
    /// </summary>
    private string NewWorkerId()
    {
        while (true)
        {
            var id = "ot" + RandomString("abcdefghijkmnpqrstuvwxyz23456789", 12);
            if (!store.WorkerExists(id))
                return id;
        }
    }

    private static string RandomString(string alphabet, int length)
    {
        var chars = new char[length];
        for (var i = 0; i < chars.Length; i++)
            chars[i] = alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];
        return new string(chars);
    }

    private static void RequireSecret(DeviceRecord device, string secret)
    {
        var presented = Encoding.ASCII.GetBytes(HashSecret(secret ?? ""));
        var stored = Encoding.ASCII.GetBytes(device.SecretHash);
        if (!CryptographicOperations.FixedTimeEquals(presented, stored))
            throw new OtiumException(401, "Device secret doesn't match.");
    }

    private static string HashSecret(string secret) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(secret)));

    private static string HashCode(string code, string salt) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes($"{salt}:{code}")));
}
