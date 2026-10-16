namespace Otium.Core.Accounts;

// Wire contracts shared by the desktop app and the Otium server.

/// <param name="PayoutAddress">The user's own XMR wallet, only for <c>PayoutMode.OwnWallet</c>; null for balance accounts.</param>
public sealed record RegisterDeviceRequest(string DeviceId, string DeviceSecret, string? PayoutAddress, string? ReferralCode);

/// <param name="PoolWallet">Operator wallet balance accounts mine to (with <paramref name="WorkerId"/> as the worker name).</param>
public sealed record AccountSummary(
    string WorkerId,
    string? PoolWallet,
    string Code,
    string ShareUrl,
    string? ReferrerWallet,
    DateTimeOffset? ReferredAt,
    DateTimeOffset? FeeFreeUntil,
    int InvitedCount,
    int VerifiedCount,
    string? ReferralCodeError,
    BalanceInfo Balance,
    string? Email = null,
    int DeviceCount = 1,
    BandwidthInfo? Bandwidth = null);

/// <summary>Terms for sharing unused internet bandwidth, as the server currently pays them.</summary>
/// <param name="Available">False while the operator has no bandwidth partner set up; the app then hides the option.</param>
/// <param name="UserUsdPerGb">What the user receives per GB shared, after Otium's fee.</param>
/// <param name="UserUsdPerSharingHour">What the user receives per hour of being available to share, after the fee.</param>
/// <param name="DailyLimitBytes">Most a computer is credited for per day (UTC).</param>
/// <param name="PartnerName">The company whose customers' traffic is routed, shown on the consent screen.</param>
public sealed record BandwidthInfo(
    bool Available,
    double UserUsdPerGb,
    double UserUsdPerSharingHour,
    long DailyLimitBytes,
    string? PartnerName);

/// <summary>
/// Usage a computer reports for one sharing period (a few minutes). <paramref name="Sequence"/> increases by one per
/// report, so a retried report is never credited twice.
/// </summary>
/// <param name="SharingSeconds">How long within the period the computer was online and available to share.</param>
/// <param name="Provider">Id of the bandwidth provider that measured the bytes, e.g. "simulated".</param>
public sealed record BandwidthReport(
    long Sequence,
    DateTimeOffset PeriodStart,
    DateTimeOffset PeriodEnd,
    long Bytes,
    double SharingSeconds,
    string Provider);

public static class BandwidthReportStatuses
{
    public const string Credited = "credited";

    /// <summary>Another computer on the same internet connection is already sharing; only one per connection earns.</summary>
    public const string SharedConnection = "shared-connection";

    /// <summary>The daily limit is reached; usage beyond it isn't paid.</summary>
    public const string DailyLimit = "daily-limit";
}

/// <param name="Note">Plain-language explanation when less than everything was credited.</param>
public sealed record BandwidthReportResult(string Status, long CreditedBytes, decimal CreditedUsd, string? Note);

/// <summary>Asks the server to email a 6-digit sign-in code.</summary>
public sealed record EmailCodeRequest(string Email);

/// <summary>
/// Confirms the code. If the email is new it secures this computer's account; if it already belongs to an account,
/// this computer joins that account (and any earnings it had on its own are moved over).
/// </summary>
public sealed record EmailVerifyRequest(string Email, string Code);

public static class EmailAddresses
{
    public static string Normalize(string? email) => (email ?? "").Trim().ToLowerInvariant();

    public static bool IsValid(string? email)
    {
        var value = (email ?? "").Trim();
        return value.Length <= 254
               && System.Net.Mail.MailAddress.TryCreate(value, out var parsed)
               && parsed.Address == value
               && value.Contains('.', StringComparison.Ordinal);
    }
}

/// <param name="FeePercent">Otium's share of mining earnings for balance accounts, shown to the user.</param>
/// <param name="ReferralPercent">What a referrer earns, as a percent of each invited friend's earnings.</param>
/// <param name="RecentCashouts">The latest cash-outs, newest first.</param>
/// <param name="PaidOutUsd">Everything ever paid out to this account.</param>
public sealed record BalanceInfo(
    decimal AvailableUsd,
    decimal PendingUsd,
    decimal LifetimeUsd,
    decimal ReferralUsd,
    decimal MinPayPalUsd,
    decimal MinCryptoUsd,
    double FeePercent,
    double ReferralPercent,
    IReadOnlyList<CashoutInfo> RecentCashouts,
    decimal PaidOutUsd = 0);

public static class CashoutMethods
{
    public const string PayPal = "paypal";
    public const string Monero = "xmr";

    public static bool IsKnown(string method) => method is PayPal or Monero;
}

/// <param name="Method"><see cref="CashoutMethods.PayPal"/> or <see cref="CashoutMethods.Monero"/>.</param>
/// <param name="Destination">PayPal email address or XMR address.</param>
public sealed record CashoutRequest(string Method, string Destination);

public static class CashoutStatuses
{
    /// <summary>Accepted and queued for automatic sending.</summary>
    public const string Queued = "queued";

    /// <summary>Held for a manual check (unusual amount, new destination, ...).</summary>
    public const string Review = "review";

    public const string Sending = "sending";
    public const string Paid = "paid";

    /// <summary>Failed or rejected; the amount was returned to the balance.</summary>
    public const string Failed = "failed";

    /// <summary>The note on a "sending" PayPal cash-out to an email that has no PayPal account yet.</summary>
    public const string AwaitingClaimNote = "waiting for you to claim it in PayPal";
}

/// <param name="Destination">Masked, e.g. <c>al••@example.com</c>.</param>
/// <param name="Note">Why it failed, or what the user still needs to do (claim it in PayPal); otherwise null.</param>
/// <param name="UpdatedAt">When the status last changed; for a paid cash-out, when it was paid.</param>
/// <param name="TransactionId">Monero transaction hash once paid, so the user can look it up.</param>
public sealed record CashoutInfo(
    string Id,
    string Method,
    string Destination,
    decimal AmountUsd,
    string Status,
    DateTimeOffset CreatedAt,
    string? Note,
    DateTimeOffset? UpdatedAt = null,
    string? TransactionId = null);

/// <summary>
/// The month's top inviters, ranked by invited friends who actually started earning (so fake sign-ups don't count).
/// Other people appear only as a masked code; the caller's own row has <see cref="InviteLeaderboardEntry.IsYou"/> set.
/// </summary>
/// <param name="PeriodStart">Start of the period (the first of the month, UTC).</param>
/// <param name="YourRank">The caller's rank, or null when they have no counted friends this period yet.</param>
public sealed record InviteLeaderboard(
    DateTimeOffset PeriodStart,
    IReadOnlyList<InviteLeaderboardEntry> Top,
    int? YourRank,
    int YourFriends);

/// <param name="Rank">1-based; equal counts share a rank (1, 2, 2, 4).</param>
/// <param name="Name">A masked invite code such as <c>K7M••••</c>, or "You".</param>
public sealed record InviteLeaderboardEntry(int Rank, string Name, int Friends, bool IsYou);

public static class ReferralCodes
{
    // No 0/O, 1/I/L, so codes survive being read aloud or typed from a screenshot.
    public const string Alphabet = "23456789ABCDEFGHJKMNPQRSTUVWXYZ";
    public const int Length = 7;

    public static string Normalize(string? code) =>
        new string((code ?? "").Trim().ToUpperInvariant().Where(c => !char.IsWhiteSpace(c) && c != '-').ToArray());

    public static bool IsWellFormed(string? code)
    {
        var normalized = Normalize(code);
        return normalized.Length == Length && normalized.All(c => Alphabet.Contains(c));
    }

    /// <summary>Enough to recognise yourself, not enough to use: <c>K7M••••</c>.</summary>
    public static string Mask(string code) => code.Length <= 3 ? code : code[..3] + new string('•', code.Length - 3);
}
