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
    int DeviceCount = 1);

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
public sealed record BalanceInfo(
    decimal AvailableUsd,
    decimal PendingUsd,
    decimal LifetimeUsd,
    decimal ReferralUsd,
    decimal MinPayPalUsd,
    decimal MinCryptoUsd,
    double FeePercent,
    double ReferralPercent,
    IReadOnlyList<CashoutInfo> RecentCashouts);

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
}

public sealed record CashoutInfo(
    string Id,
    string Method,
    string Destination,
    decimal AmountUsd,
    string Status,
    DateTimeOffset CreatedAt,
    string? Note);

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
}
