using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Otium.Core.Accounts;
using Otium.Core.Fees;
using Otium.Core.Pools;
using Otium.Core.Wallets;
using Otium.Server.Data;

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
    IEmailSender email,
    IOptions<ReferralOptions> referralOptions,
    IOptions<EarningsOptions> earningsOptions,
    IOptions<CashoutOptions> cashoutOptions,
    TimeProvider time,
    ILogger<AccountService> logger)
{
    public static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(15);
    public const int MaxCodeAttempts = 5;
    public const int MaxCodesPerEmailPerHour = 5;
    public const int MaxCodesPerDevicePerHour = 10;

    private static readonly FeePolicy Policy = FeePolicy.Default;
    private readonly ReferralOptions _referral = referralOptions.Value;
    private readonly EarningsOptions _earnings = earningsOptions.Value;
    private readonly CashoutOptions _cashouts = cashoutOptions.Value;

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
            if (!MoneroAddress.TryValidate(request.PayoutAddress, out _, out var addressError))
                throw new OtiumException(400, addressError!);
            ownWallet = request.PayoutAddress.Trim();
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

        // Balance accounts need a pool address to mine to; keep it if switching back and forth between modes.
        if (account.IsBalanceAccount && account.PoolWallet is null)
            account = account with { PoolWallet = await allocator.AssignAsync(ct) };

        string? codeError = null;
        if (!string.IsNullOrWhiteSpace(request.ReferralCode) && account.ReferredByCode is null)
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

    // ---------- email sign-in ----------

    public async Task SendSignInCodeAsync(string deviceId, string secret, EmailCodeRequest request, CancellationToken ct)
    {
        var session = Authenticate(deviceId, secret);
        if (!EmailAddresses.IsValid(request.Email))
            throw new OtiumException(400, "That doesn't look like an email address.");

        var address = EmailAddresses.Normalize(request.Email);
        var now = time.GetUtcNow();
        if (store.EmailCodesSince(address, now - TimeSpan.FromHours(1)) >= MaxCodesPerEmailPerHour
            || store.DeviceEmailCodesSince(session.Device.DeviceId, now - TimeSpan.FromHours(1)) >= MaxCodesPerDevicePerHour)
            throw new OtiumException(429, "Too many codes requested. Please wait a while and try again.");

        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6", System.Globalization.CultureInfo.InvariantCulture);
        var salt = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16));
        store.AddEmailCode(address, session.Device.DeviceId, HashCode(code, salt), salt, now + CodeLifetime, now);

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

        var stored = store.LatestEmailCode(address, session.Device.DeviceId, now)
                     ?? throw new OtiumException(400, "That code has expired. Request a new one.");
        if (stored.Attempts >= MaxCodeAttempts)
            throw new OtiumException(400, "Too many wrong tries. Request a new code.");

        var presented = Encoding.ASCII.GetBytes(HashCode((request.Code ?? "").Trim(), stored.Salt));
        if (!CryptographicOperations.FixedTimeEquals(presented, Encoding.ASCII.GetBytes(stored.CodeHash)))
        {
            store.CountEmailCodeAttempt(stored.Id);
            throw new OtiumException(400, "That code isn't right. Check the email and try again.");
        }
        store.UseEmailCode(stored.Id);

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
    /// own-wallet accounts: hashes reported by the pool) and gives the referrer fee-free time.
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
                verified = store.LifetimeEarningMicros(referee.AccountId) >= ToMicros(_referral.VerificationUsd);
            }
            else
            {
                try
                {
                    var stats = await pool.GetMinerStatsAsync(referee.OwnWallet!, ct);
                    verified = stats is not null && stats.TotalHashes >= _referral.VerificationHashes;
                }
                catch (HttpRequestException e)
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

    // ---------- summary ----------

    public AccountSummary Summarize(AccountRecord account, string publicBaseUrl, string? codeError)
    {
        var now = time.GetUtcNow();
        var referrer = account.ReferredByCode is null ? null : store.Resolve(store.FindAccountByCode(account.ReferredByCode));
        var referees = store.FindReferees(account.Code);
        var totals = store.Balance(account.AccountId, now);
        var cashouts = store.CashoutsFor(account.AccountId).Select(ToInfo).ToList();

        return new AccountSummary(
            account.WorkerId,
            account.IsBalanceAccount ? account.PoolWallet : null,
            account.Code,
            $"{publicBaseUrl.TrimEnd('/')}/r/{account.Code}",
            // Own-wallet clients mine the referrer's slice to this wallet; balance accounts are rewarded server-side.
            account.IsBalanceAccount ? null : referrer?.OwnWallet,
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
                _cashouts.MinCryptoUsd,
                _earnings.FeePercent,
                _earnings.ReferralPercent,
                cashouts),
            account.Email,
            (int)store.DeviceCount(account.AccountId));
    }

    public static CashoutInfo ToInfo(CashoutRecord c) => new(
        c.Id, c.Method, MaskDestination(c.Method, c.Destination), ToUsd(c.Micros), c.Status, c.CreatedAt,
        c.Status == CashoutStatuses.Failed ? c.Note : null);

    public static decimal ToUsd(long micros) => decimal.Round(micros / (decimal)OtiumStore.MicrosPerDollar, 2, MidpointRounding.ToZero);

    public static long ToMicros(decimal usd) => (long)decimal.Round(usd * OtiumStore.MicrosPerDollar);

    private static string MaskDestination(string method, string destination)
    {
        if (method == CashoutMethods.PayPal)
        {
            var at = destination.IndexOf('@');
            return at > 2 ? destination[..2] + new string('•', at - 2) + destination[at..] : destination;
        }
        return destination.Length > 12 ? $"{destination[..6]}…{destination[^6..]}" : destination;
    }

    /// <summary>Adds fee-free time, never beyond the policy cap from now.</summary>
    private static DateTimeOffset ExtendHoliday(DateTimeOffset? current, TimeSpan add, DateTimeOffset now)
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
