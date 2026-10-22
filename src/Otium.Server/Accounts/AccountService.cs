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
    IOptions<FeeOptions> feeOptions,
    IOptions<ProxybaseOptions> proxybaseOptions,
    ConnectionHashes connections,
    ReferralRedemptions redemptions)
{
    /// <summary>
    /// How long a connection one of an account's computers used still counts as theirs: an invite from a computer on it
    /// doesn't count (one person inviting themselves from a second computer and wallet would collect both rewards).
    /// </summary>
    public static readonly TimeSpan ConnectionMemory = TimeSpan.FromDays(30);

    /// <summary>
    /// Most a friend's computer can plausibly mine for its inviter in a day (a top graphics card mining around the clock
    /// makes about $0.05 of it); higher reports are cut to this, so they can't inflate what the inviter sees.
    /// </summary>
    public const double MaxReferralShareUsdPerDay = 0.5;

    /// <summary>
    /// Most invited friends whose pool is asked about per check-in (the rest wait for the next ones), so an inviter with
    /// many made-up friends can't make every check-in send a crowd of requests to the pools.
    /// </summary>
    public const int MaxPoolLookupsPerRefresh = 10;

    public static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(15);
    public const int MaxCodeAttempts = 5;
    public const int MaxCodesPerEmailPerHour = 5;
    public const int MaxCodesPerDevicePerHour = 10;

    /// <summary>
    /// Codes emailed to one address in a day, whoever asks: connections and devices are easy to come by, so without it
    /// an address could be sent over a hundred codes a day.
    /// </summary>
    public const int MaxCodesPerEmailPerDay = 10;

    /// <summary>
    /// Wrong codes one computer may enter for one email address in a day. Counted per computer, so someone guessing at
    /// another person's address locks out only their own computer, not the owner's.
    /// </summary>
    public const int MaxWrongCodesPerEmailPerDevicePerDay = 10;

    /// <summary>
    /// Wrong codes allowed for one email address in a day over all computers, so guessing spread over many computers
    /// still stops. Generous, so it takes many computers' worth of wrong codes before the owner is affected; with at most
    /// <see cref="MaxCodesPerEmailPerDay"/> codes of 5 tries a day there can't be more anyway, and guessing a 6-digit code
    /// at 50 tries a day takes about 27 years on average.
    /// </summary>
    public const int MaxWrongCodesPerEmailPerDay = 50;

    /// <summary>The fee and invite terms apps are told about; fee-free periods are granted by the same terms.</summary>
    private FeePolicy Policy => FeePolicy.FromTerms(feeOptions.Value.ToTerms());

    /// <summary>The fee-grant signing key, or null when the server has none (development, tests).</summary>
    private readonly Lazy<System.Security.Cryptography.ECDsa?> _grantKey = new(() =>
    {
        if (string.IsNullOrWhiteSpace(feeOptions.Value.GrantSigningKey))
            return null;
        var key = System.Security.Cryptography.ECDsa.Create();
        key.ImportPkcs8PrivateKey(Convert.FromBase64String(feeOptions.Value.GrantSigningKey.Trim()), out _);
        return key;
    });
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

    public async Task<AccountSummary> RegisterAsync(RegisterDeviceRequest request, string publicBaseUrl, CancellationToken ct, System.Net.IPAddress? from = null)
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
        if (request.HardwareId is { Length: > 128 })
            throw new OtiumException(400, "Invalid hardware id.");
        var hardwareId = string.IsNullOrWhiteSpace(request.HardwareId) ? null : request.HardwareId.Trim();

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
            RefuseInvitersWallet(account, ownWallet);
            var previousWallet = account.OwnWallet;
            account = account with { OwnWallet = ownWallet };
            if (!ReferralRedemptions.SameAddress(previousWallet, ownWallet))
                account = CheckRefereeWallet(account, ownWallet, now);
            device = device with { AccountId = account.AccountId, LastSeenAt = now };
        }

        // Balance accounts need a pool address to mine to; keep it if switching back and forth between modes. Without
        // custody there are no balance accounts, only ones that haven't chosen their payout address yet.
        if (_custodial && account.IsBalanceAccount && account.PoolWallet is null)
            account = account with { PoolWallet = await allocator.AssignAsync(ct) };

        string? codeError = null;
        var network = connections.Of(from);
        // ReferredAt stays set when the referrer deleted their account, so the invite can't be swapped for another one.
        if (!string.IsNullOrWhiteSpace(request.ReferralCode) && account.ReferredByCode is null && account.ReferredAt is null)
        {
            codeError = TryBind(ref account, ReferralCodes.Normalize(request.ReferralCode), now, network, hardwareId);
            if (codeError is not null)
                logger.LogInformation("Referral code rejected for {Account}: {Reason}", account.AccountId, codeError);
        }

        store.Upsert(account);
        store.Upsert(device);
        if (network is not null)
        {
            store.DeleteAccountNetworksBefore(now - ConnectionMemory);
            store.RecordAccountNetwork(account.AccountId, network, now);
        }
        return Summarize(account, publicBaseUrl, codeError, device.DeviceId);
    }

    /// <summary>
    /// A friend can't switch their payout address to the inviter's, nor an inviter to a friend's: that's one person
    /// inviting themselves, which the checks when the code was entered would have refused.
    /// </summary>
    private void RefuseInvitersWallet(AccountRecord account, string? ownWallet)
    {
        if (ownWallet is null)
            return;
        // By address alone: the same address under another coin (TRX and USDT, BNB and ETH) is the same person's.
        if (account.ReferredByCode is { } code && store.Resolve(store.FindAccountByCode(code)) is { } inviter
            && ReferralRedemptions.SameAddress(inviter.OwnWallet, ownWallet))
            throw new OtiumException(400, "That's the payout address of the friend who invited you. Use an address of your own.");
        if (RefereesOf(account).Any(f => ReferralRedemptions.SameAddress(f.OwnWallet, ownWallet)))
            throw new OtiumException(400, "That's the payout address of a friend you invited. Use an address of your own.");
    }

    /// <summary>
    /// A referred account's payout address changed (re-registered with another one, or a computer with its own address
    /// joined it by email sign-in). An address that already joined through another invite ends the fee-free days this
    /// invite gave, else joining with a fresh address and switching back would get round "one invite per address"; a
    /// new address is recorded as this invite's.
    /// </summary>
    private AccountRecord CheckRefereeWallet(AccountRecord account, string? wallet, DateTimeOffset now)
    {
        if (wallet is null || account.ReferredAt is not { } referredAt)
            return account;
        if (store.ClaimRedemption(redemptions.OfWallet(wallet), account.AccountId, now))
            return account;
        logger.LogInformation("Referred account {Account} moved to an address that already joined through an invite", account.AccountId);
        return account.FeeFreeUntil is { } until && until > now && now - referredAt <= Policy.RefereeFeeHoliday
            ? account with { FeeFreeUntil = now }
            : account;
    }

    /// <summary>
    /// Friends invited with this account's code, or with the code of an account merged into it (they keep counting
    /// for the account they were merged into).
    /// </summary>
    private List<AccountRecord> RefereesOf(AccountRecord account) => InviteCodesOf(account).SelectMany(store.FindReferees).ToList();

    private List<string> InviteCodesOf(AccountRecord account) => [account.Code, .. store.MergedAccounts(account.AccountId).Select(a => a.Code)];

    /// <summary>A referred friend's computer reports what it has mined for its inviter so far; see <see cref="MaxReferralShareUsdPerDay"/>.</summary>
    public void ReportReferralShare(Session session, ReferralShareReport report)
    {
        var account = session.Account;
        if (account.ReferredByCode is null || account.ReferredAt is not { } since || !double.IsFinite(report.EstimatedUsd))
            return;
        var now = time.GetUtcNow();
        // The inviter's share ends after 6 months, so nothing mined later is for them.
        if (now - since > Policy.ReferrerRewardPeriod)
            return;
        var cap = MaxReferralShareUsdPerDay * Math.Max(1, (now - since).TotalDays);
        store.SetReferralShare(session.Device.DeviceId, account.AccountId, Math.Clamp(report.EstimatedUsd, 0, cap), now);
    }

    /// <summary>"0.1.1" from "0.1.1+abc123"; null when it isn't an Otium version number.</summary>
    internal static string? CleanVersion(string? version) =>
        version is { Length: <= 64 } && Otium.Core.AppVersions.Parse(version) is not null ? version.Trim().TrimStart('v').Split('+')[0] : null;

    public Session Authenticate(string deviceId, string secret)
    {
        var device = store.FindDevice(deviceId) ?? throw new OtiumException(404, "Unknown device.");
        RequireSecret(device, secret);
        var account = store.Resolve(store.FindAccount(device.AccountId)) ?? throw new OtiumException(404, "Account not found.");
        return new Session(device, account);
    }

    /// <param name="cpuMinedSeconds">How long the computer's processor has mined in all, as its app reports (null from older apps).</param>
    /// <param name="gpuMinedSeconds">The same for its graphics card.</param>
    /// <param name="appVersion">The <see cref="Otium.Core.AppVersions.Header"/> the app sent (none before 0.1.1).</param>
    public async Task<AccountSummary> GetSummaryAsync(string deviceId, string secret, string publicBaseUrl, CancellationToken ct, System.Net.IPAddress? from = null,
        double? cpuMinedSeconds = null, double? gpuMinedSeconds = null, string? appVersion = null)
    {
        var session = Authenticate(deviceId, secret);
        // The app's regular check-in (every minute): what the admin dashboard's "online now" and "seen in 24 h" count.
        store.TouchDevice(deviceId, time.GetUtcNow(), TimeSpan.FromMinutes(5), CleanVersion(appVersion));
        // Kept so the signed fee grant can give it back (see FeeGrant.CpuMiningFloor).
        store.RecordMiningTime(deviceId, cpuMinedSeconds, gpuMinedSeconds, time.GetUtcNow());
        if (connections.Of(from) is { } network)
            store.RecordAccountNetwork(session.Account.AccountId, network, time.GetUtcNow());
        await RefreshVerificationsAsync(session.Account, ct);
        return Summarize(store.FindAccount(session.Account.AccountId)!, publicBaseUrl, null, deviceId);
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
        RequireNotLocked(address, session.Device.DeviceId, now);
        if (store.EmailCodesSince(address, now - TimeSpan.FromHours(1)) >= MaxCodesPerEmailPerHour
            || store.DeviceEmailCodesSince(session.Device.DeviceId, now - TimeSpan.FromHours(1)) >= MaxCodesPerDevicePerHour)
            throw new OtiumException(429, "Too many codes requested. Please wait a while and try again.");
        if (store.EmailCodesSince(address, now - TimeSpan.FromDays(1)) >= MaxCodesPerEmailPerDay)
            throw new OtiumException(429, "Too many codes were sent to this email address today. Please try again tomorrow.");

        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6", System.Globalization.CultureInfo.InvariantCulture);
        var salt = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16));
        store.AddEmailCode(address, session.Device.DeviceId, HashCode(code, salt), salt, now + CodeLifetime, now);

        if (deleting)
        {
            await SendCodeEmailAsync(
                request.Email.Trim(),
                $"Your code to delete your Otium account: {code}",
                $"Your code to permanently delete your Otium account is:\n\n    {code}\n\nIt expires in {CodeLifetime.TotalMinutes:0} minutes. " +
                "If you didn't ask for it, don't share it with anyone; your account stays as it is.",
                ct);
            return;
        }

        var existing = store.FindAccountByEmail(address);
        var purpose = existing is null || existing.AccountId == session.Account.AccountId
            ? "add this email to your Otium account"
            : "sign in to your Otium account on this computer";
        await SendCodeEmailAsync(
            request.Email.Trim(),
            $"Your Otium code: {code}",
            $"Your code to {purpose} is:\n\n    {code}\n\nIt expires in {CodeLifetime.TotalMinutes:0} minutes. If you didn't ask for it, you can ignore this email.",
            ct);
    }

    /// <summary>
    /// Sends a code email. When the email service refuses or can't be reached, the app gets a plain message to show
    /// (and the log the reason) instead of a bare server error.
    /// </summary>
    private async Task SendCodeEmailAsync(string to, string subject, string body, CancellationToken ct)
    {
        try
        {
            await email.SendAsync(to, subject, body, ct);
        }
        catch (Exception e) when (e is System.Net.Mail.SmtpException or IOException or InvalidOperationException && !ct.IsCancellationRequested)
        {
            logger.LogWarning(e, "Couldn't send a code email");
            throw new OtiumException(StatusCodes.Status503ServiceUnavailable,
                "Otium couldn't send the email just now. Check the address and try again in a few minutes.");
        }
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
            return Summarize(current, publicBaseUrl, null, deviceId);
        }

        if (owner.AccountId == current.AccountId)
            return Summarize(current, publicBaseUrl, null, deviceId);

        // This computer will mine for the signed-in account to its own payout address: one that already joined through
        // another invite can't also have this account's invite.
        if (!ReferralRedemptions.SameAddress(current.OwnWallet, owner.OwnWallet)
            && CheckRefereeWallet(owner, current.OwnWallet, now) is { } checkedOwner && checkedOwner.FeeFreeUntil != owner.FeeFreeUntil)
            store.SetFeeFreeUntil(owner.AccountId, checkedOwner.FeeFreeUntil);

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

        return Summarize(store.FindAccount(owner.AccountId)!, publicBaseUrl, null, deviceId);
    }

    /// <summary>
    /// Checks the newest code emailed to <paramref name="address"/> at this computer's request and marks it used, so
    /// each code works once. Throws with a user-facing reason when it's missing, expired, locked or wrong.
    /// </summary>
    public void ConsumeEmailCode(string address, string deviceId, string? code)
    {
        RequireNotLocked(address, deviceId, time.GetUtcNow());
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

    /// <summary>
    /// Refuses codes for an email address on a computer that entered <see cref="MaxWrongCodesPerEmailPerDevicePerDay"/>
    /// wrong ones for it in the last day, or on any computer once <see cref="MaxWrongCodesPerEmailPerDay"/> were entered.
    /// </summary>
    private void RequireNotLocked(string address, string deviceId, DateTimeOffset now)
    {
        var dayAgo = now - TimeSpan.FromDays(1);
        if (store.EmailCodeAttemptsSince(address, deviceId, dayAgo) >= MaxWrongCodesPerEmailPerDevicePerDay
            || store.EmailCodeAttemptsSince(address, dayAgo) >= MaxWrongCodesPerEmailPerDay)
            throw new OtiumException(429, "Too many wrong codes for this email address today. Please try again tomorrow.");
    }

    // ---------- referrals ----------

    private string? TryBind(ref AccountRecord account, string code, DateTimeOffset now, string? network, string? hardwareId)
    {
        if (!ReferralCodes.IsWellFormed(code))
            return "That invite code doesn't look right.";
        if (now - account.CreatedAt > _referral.RedeemWindow)
            return "Invite codes can only be used in your first week.";
        // Without custody every account mines to its own address. Bound before there is one, the checks on the address
        // below would be skipped, and an address that already joined through an invite could be added afterwards.
        if (account.OwnWallet is null && !_custodial)
            return "Add your payout address to use an invite code.";

        var referrer = store.Resolve(store.FindAccountByCode(code));
        if (referrer is null)
            return "We couldn't find that invite code.";
        if (referrer.AccountId == account.AccountId || referrer.Code == account.Code)
            return "You can't use your own invite code.";
        if (account.OwnWallet is not null && ReferralRedemptions.SameAddress(referrer.OwnWallet, account.OwnWallet))
            return "You can't invite a device that pays out to your own wallet.";
        if (referrer.ReferredByCode == account.Code)
            return "Two people can't invite each other.";
        // One person inviting themselves from a second computer (with a second wallet) would collect both rewards.
        if (network is not null && store.AccountUsedNetwork(referrer.AccountId, network, now - ConnectionMemory))
            return "Invites only count for friends on a different internet connection.";
        if (account.OwnWallet is not null && store.WalletAlreadyReferred(ReferralRedemptions.Address(account.OwnWallet), account.AccountId))
            return "This wallet has already joined through an invite.";

        // Each address and computer joins through one invite, ever: these stay recorded after an account is deleted, so
        // deleting it and joining again (or two households re-inviting each other) doesn't repeat the rewards.
        var walletKey = account.OwnWallet is null ? null : redemptions.OfWallet(account.OwnWallet);
        var hardwareKey = hardwareId is null ? null : redemptions.OfHardware(hardwareId);
        if (hardwareKey is not null && store.RedemptionHolder(hardwareKey) is { } holder && holder != account.AccountId)
            return "This computer has already joined through an invite.";
        if (walletKey is not null && !store.ClaimRedemption(walletKey, account.AccountId, now))
            return "This wallet has already joined through an invite.";
        if (hardwareKey is not null && !store.ClaimRedemption(hardwareKey, account.AccountId, now))
        {
            if (walletKey is not null)
                store.ReleaseRedemption(walletKey, account.AccountId); // the same computer, a moment ago, with another address
            return "This computer has already joined through an invite.";
        }

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
    /// Monero addresses: hashes reported by MoneroOcean; other coins: rewards from unMineable; in both cases mined by one
    /// of the friend's own Otium computers) and gives the referrer fee-free time.
    /// </summary>
    private async Task RefreshVerificationsAsync(AccountRecord referrer, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var due = RefereesOf(referrer)
            .Where(r => !r.Verified && (r.LastCheckedAt is not { } checkedAt || now - checkedAt >= _referral.VerificationRecheck))
            .OrderBy(r => r.LastCheckedAt ?? DateTimeOffset.MinValue); // the longest waiting first, when there are many
        var lookups = 0;
        foreach (var referee in due)
        {
            bool verified;
            if (store.AccountsSharedNetwork(referee.AccountId, referrer.AccountId, now - ConnectionMemory))
            {
                // The friend's and the inviter's computers used the same internet connection lately: most likely one
                // person with two wallets, who joined from elsewhere. Asked again later, once that's a month ago.
                verified = false;
            }
            else if (referee.IsBalanceAccount)
            {
                // Without custody this is an account that hasn't set a payout address, so it hasn't mined anything.
                verified = _custodial && store.LifetimeEarningMicros(referee.AccountId) >= ToMicros(_referral.VerificationUsd);
            }
            else
            {
                if (++lookups > MaxPoolLookupsPerRefresh)
                    continue;
                try
                {
                    verified = await PoolConfirmsMiningAsync(referee, ct);
                }
                catch (Exception e) when (e is HttpRequestException or TaskCanceledException or InvalidOperationException or System.Text.Json.JsonException && !ct.IsCancellationRequested)
                {
                    logger.LogWarning(e, "Pool lookup failed for referee {Account}", referee.AccountId);
                    continue;
                }
            }

            // Only the call that marks the friend verified rewards the inviter: check-ins arriving together (several
            // computers, the Invites page) each see the friend unverified and would otherwise each add 30 days.
            if (!verified)
                store.MarkReferralChecked(referee.AccountId, now);
            else if (store.MarkReferralVerified(referee.AccountId, now))
            {
                var current = store.FindAccount(referrer.AccountId)!;
                store.SetFeeFreeUntil(referrer.AccountId, ExtendHoliday(current.FeeFreeUntil, Policy.ReferrerFeeHolidayPerVerifiedFriend, now));
            }
        }
    }

    /// <summary>
    /// Whether the pool that pays the friend's address shows real mining by one of the friend's own Otium computers
    /// (workers named "ot" + 8 hex digits of a device id of theirs): any address with some pool history isn't enough,
    /// nor one mined by someone else's computers.
    /// </summary>
    private async Task<bool> PoolConfirmsMiningAsync(AccountRecord friend, CancellationToken ct)
    {
        var wallet = friend.OwnWallet!;
        var computers = store.DevicesFor(friend.AccountId)
            .Where(d => d.DeviceId.Length >= 8)
            .Select(d => d.DeviceId[..8])
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        bool FriendsWorker(string name) => RealEarnings.PoolPayoutChecker.IsOtiumWorker(name) && computers.Contains(name[2..10]);

        var target = PayoutTarget.ParseOrNull(wallet);
        if (target is null || target.Coin == PayoutCoins.Monero)
        {
            var address = target?.Address ?? wallet;
            var stats = await pool.GetMinerStatsAsync(address, ct);
            if (stats is not null && stats.TotalHashes >= _referral.VerificationHashes
                && (await pool.GetWorkerHistoryAsync(address, ct)).Keys.Any(FriendsWorker))
                return true;
            // Outside the US a Monero user's graphics card mines on unMineable, paid in XMR there.
            if (target is null)
                return false;
        }
        if (!_referral.VerifyUnmineableByRewards)
            return false;
        var account = await unmineable.GetAddressAsync(target, ct);
        if (account.Uuid is not { } uuid)
            return false;
        var mined = await unmineable.GetStatsAsync(uuid, ct);
        // Credited in two of unMineable's daily accountings (or paid out): at least a day of real mining, not a few
        // minutes on a spare wallet to unlock the inviter's fee-free days.
        var credited = mined.Paid > 0
                       || (mined.Rewarded24h > 0 && mined.Rewarded7d > mined.Rewarded24h)
                       || (mined.Rewarded7d > 0 && mined.Rewarded30d > mined.Rewarded7d);
        return credited && (await unmineable.GetWorkersAsync(uuid, ct)).Any(w => FriendsWorker(w.Name));
    }

    // ---------- summary ----------

    /// <param name="deviceId">The computer asking: its summary carries a fee grant signed for it.</param>
    public AccountSummary Summarize(AccountRecord account, string publicBaseUrl, string? codeError, string? deviceId = null)
    {
        var now = time.GetUtcNow();
        var referrer = account.ReferredByCode is null ? null : store.Resolve(store.FindAccountByCode(account.ReferredByCode));
        var codes = InviteCodesOf(account);
        var referees = codes.SelectMany(store.FindReferees).ToList();
        var totals = store.Balance(account.AccountId, now);
        var cashouts = store.CashoutsFor(account.AccountId).Select(ToInfo).ToList();
        // Without custody nothing can be cashed out, so no methods are offered (the app hides the balance UI).
        var methods = _custodial ? cashoutMethods.Current : [];

        var summary = new AccountSummary(
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
            Policy.ToTerms(),
            !proxybaseOptions.Value.Enabled,
            store.ReferralShareTotal(codes));
        return deviceId is null ? summary : WithFeeGrant(summary, deviceId, now);
    }

    /// <summary>
    /// Signs what this computer's fee depends on (see <see cref="FeeGrant"/>), for a week: release builds of the app
    /// take fee-free days, the inviter's slice and the fee only from a grant like this.
    /// </summary>
    private AccountSummary WithFeeGrant(AccountSummary summary, string deviceId, DateTimeOffset now)
    {
        if (_grantKey.Value is not { } key)
            return summary;
        var policy = Policy;
        var (cpuMined, gpuMined) = store.MiningTime(deviceId);
        var grant = new FeeGrant(deviceId, policy.FeePercent, policy.ReferrerShareOfFee, summary.ReferrerWallet, summary.ReferredAt,
            summary.FeeFreeUntil, summary.Custodial && summary.PoolWallet is not null, summary.PoolWallet, now, now + FeeGrant.Lifetime,
            cpuMined, gpuMined);
        return summary with { FeeGrant = grant.Payload(), FeeGrantSignature = FeeGrants.Sign(grant, key) };
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
