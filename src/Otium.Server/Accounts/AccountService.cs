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

/// <summary>
/// Accounts and referrals. An account is created the first time a device connects; balance accounts get a pool
/// address and worker id to mine under, own-wallet accounts mine straight to their wallet.
/// </summary>
public sealed class AccountService(
    OtiumStore store,
    PoolWalletAllocator allocator,
    IMoneroPoolApi pool,
    IOptions<ReferralOptions> referralOptions,
    IOptions<EarningsOptions> earningsOptions,
    IOptions<CashoutOptions> cashoutOptions,
    TimeProvider time,
    ILogger<AccountService> logger)
{
    private static readonly FeePolicy Policy = FeePolicy.Default;
    private readonly ReferralOptions _referral = referralOptions.Value;
    private readonly EarningsOptions _earnings = earningsOptions.Value;
    private readonly CashoutOptions _cashouts = cashoutOptions.Value;

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
        var device = store.FindByDevice(request.DeviceId);
        if (device is null)
        {
            device = new DeviceRecord(request.DeviceId, HashSecret(request.DeviceSecret), NewWorkerId(), null, ownWallet, NewCode(),
                null, null, null, false, null, now);
        }
        else
        {
            RequireSecret(device, request.DeviceSecret);
            device = device with { OwnWallet = ownWallet };
        }

        // Balance accounts need a pool address to mine to; keep it if switching back and forth between modes.
        if (device.IsBalanceAccount && device.PoolWallet is null)
            device = device with { PoolWallet = await allocator.AssignAsync(ct) };

        string? codeError = null;
        if (!string.IsNullOrWhiteSpace(request.ReferralCode) && device.ReferredByCode is null)
        {
            codeError = TryBind(ref device, ReferralCodes.Normalize(request.ReferralCode), now);
            if (codeError is not null)
                logger.LogInformation("Referral code rejected for {Device}: {Reason}", device.DeviceId, codeError);
        }

        store.Upsert(device);
        return Summarize(device, publicBaseUrl, codeError);
    }

    public DeviceRecord Authenticate(string deviceId, string secret)
    {
        var device = store.FindByDevice(deviceId) ?? throw new OtiumException(404, "Unknown device.");
        RequireSecret(device, secret);
        return device;
    }

    public async Task<AccountSummary> GetSummaryAsync(string deviceId, string secret, string publicBaseUrl, CancellationToken ct)
    {
        var device = Authenticate(deviceId, secret);
        await RefreshVerificationsAsync(device, ct);
        return Summarize(store.FindByDevice(deviceId)!, publicBaseUrl, null);
    }

    private string? TryBind(ref DeviceRecord device, string code, DateTimeOffset now)
    {
        if (!ReferralCodes.IsWellFormed(code))
            return "That invite code doesn't look right.";
        if (now - device.CreatedAt > _referral.RedeemWindow)
            return "Invite codes can only be used in your first week.";

        var referrer = store.FindByCode(code);
        if (referrer is null)
            return "We couldn't find that invite code.";
        if (referrer.DeviceId == device.DeviceId || referrer.Code == device.Code)
            return "You can't use your own invite code.";
        if (device.OwnWallet is not null && string.Equals(referrer.OwnWallet, device.OwnWallet, StringComparison.Ordinal))
            return "You can't invite a device that pays out to your own wallet.";
        if (referrer.ReferredByCode == device.Code)
            return "Two people can't invite each other.";
        if (device.OwnWallet is not null && store.WalletAlreadyReferred(device.OwnWallet, device.DeviceId))
            return "This wallet has already joined through an invite.";

        device = device with
        {
            ReferredByCode = referrer.Code,
            ReferredAt = now,
            FeeFreeUntil = ExtendHoliday(device.FeeFreeUntil, Policy.RefereeFeeHoliday, now),
        };
        return null;
    }

    /// <summary>
    /// Marks invited friends as verified once they have really contributed (balance accounts: credited earnings;
    /// own-wallet accounts: hashes reported by the pool) and gives the referrer fee-free time.
    /// </summary>
    private async Task RefreshVerificationsAsync(DeviceRecord referrer, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        foreach (var referee in store.FindReferees(referrer.Code).Where(r => !r.Verified))
        {
            if (referee.LastCheckedAt is { } checkedAt && now - checkedAt < _referral.VerificationRecheck)
                continue;

            bool verified;
            if (referee.IsBalanceAccount)
            {
                verified = store.LifetimeEarningMicros(referee.DeviceId) >= ToMicros(_referral.VerificationUsd);
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
                    logger.LogWarning(e, "Pool lookup failed for referee {Device}", referee.DeviceId);
                    continue;
                }
            }

            store.Upsert(referee with { Verified = verified, LastCheckedAt = now });
            if (verified)
            {
                var current = store.FindByDevice(referrer.DeviceId)!;
                store.Upsert(current with { FeeFreeUntil = ExtendHoliday(current.FeeFreeUntil, Policy.ReferrerFeeHolidayPerVerifiedFriend, now) });
            }
        }
    }

    public AccountSummary Summarize(DeviceRecord device, string publicBaseUrl, string? codeError)
    {
        var now = time.GetUtcNow();
        var referrer = device.ReferredByCode is null ? null : store.FindByCode(device.ReferredByCode);
        var referees = store.FindReferees(device.Code);
        var totals = store.Balance(device.DeviceId, now);
        var cashouts = store.CashoutsFor(device.DeviceId).Select(ToInfo).ToList();

        return new AccountSummary(
            device.WorkerId,
            device.IsBalanceAccount ? device.PoolWallet : null,
            device.Code,
            $"{publicBaseUrl.TrimEnd('/')}/r/{device.Code}",
            // Own-wallet clients mine the referrer's slice to this wallet; balance accounts are rewarded server-side.
            device.IsBalanceAccount ? null : referrer?.OwnWallet,
            device.ReferredAt,
            device.FeeFreeUntil,
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
                cashouts));
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
}
