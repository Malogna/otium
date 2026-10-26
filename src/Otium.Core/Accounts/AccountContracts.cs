namespace Otium.Core.Accounts;

// Wire contracts shared by the desktop app and the Otium server.

/// <param name="PayoutAddress">
/// The user's own payout target ("USDT:T…", or a bare Monero address), only for <c>PayoutMode.OwnWallet</c>; null for
/// balance accounts.
/// </param>
/// <param name="HardwareId">
/// A hash of the computer's own machine id (see <c>DeviceIdentity.HardwareId</c>), the same for every install on it, so
/// an invite code can be used only once per computer; null when the computer has none (and from older apps).
/// </param>
public sealed record RegisterDeviceRequest(string DeviceId, string DeviceSecret, string? PayoutAddress, string? ReferralCode, string? HardwareId = null);

/// <summary>The invite code of a friend's link opened on the caller's internet connection in the last week.</summary>
public sealed record InviteLinkResponse(string Code);

/// <param name="PoolWallet">Operator wallet balance accounts mine to (with <paramref name="WorkerId"/> as the worker name).</param>
/// <param name="ReferrerWallet">The referrer's payout target, which own-wallet apps mine the referral slice to.</param>
/// <param name="MysteriumSponsored">Otium pays new Mysterium nodes' registration, so the app can offer internet sharing.</param>
/// <param name="ProxybaseDisabled">
/// The operator switched internet sharing through Proxybase off (it's on unless they do, so servers that don't send this
/// field leave it on).
/// </param>
/// <param name="Fees">The server's current fee and invite terms; null from servers that don't send them.</param>
/// <param name="MystUsd">Today's MYST price in US dollars, so the app can show Mysterium sharing earnings in dollars; null
/// when the server has none.</param>
/// <param name="AiozUsd">Today's AIOZ price in US dollars, for AIOZ sharing earnings; null when the server has none.</param>
/// <param name="Custodial">
/// False when the server holds no balances (non-custodial): the app then offers only own-address payouts, and
/// <paramref name="Balance"/> is empty.
/// </param>
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
    BandwidthInfo? Bandwidth = null,
    CpuPoolRoute? CpuPool = null,
    bool Custodial = true,
    bool MysteriumSponsored = false,
    FeeTerms? Fees = null,
    bool ProxybaseDisabled = false,
    double ReferralEarnedUsd = 0,
    string? FeeGrant = null,
    string? FeeGrantSignature = null,
    double? MystUsd = null,
    double? AiozUsd = null);

/// <summary>
/// A referred friend's app: how much of Otium's fee it has mined for the person who invited them so far (an estimate
/// from its own records), so the inviter can see what invites earn.
/// </summary>
public sealed record ReferralShareReport(double EstimatedUsd);

/// <summary>
/// The disclosed fee and invite terms, set by the server so they can change without an app release. Apps clamp them
/// (fee at most 10%) and fall back to their built-in defaults when a server doesn't send them.
/// </summary>
/// <param name="FeePercent">Share of mining time mined for Otium (and the referrer), in percent.</param>
/// <param name="ReferrerShareOfFee">Fraction of that fee mined for whoever invited the user, for <paramref name="ReferrerRewardDays"/>.</param>
/// <param name="RefereeFeeFreeDays">Fee-free days for someone who joins with an invite code.</param>
/// <param name="ReferrerFeeFreeDaysPerFriend">Fee-free days a referrer gets per invited friend who really starts mining.</param>
/// <param name="MaxFeeFreeDays">Most fee-free time that can stack up at once.</param>
public sealed record FeeTerms(
    double FeePercent,
    double ReferrerShareOfFee,
    double ReferrerRewardDays,
    double RefereeFeeFreeDays,
    double ReferrerFeeFreeDaysPerFriend,
    double MaxFeeFreeDays);

/// <summary>
/// Where a balance account's CPU mines, decided by the server so the pool can change (or fail over) without an app
/// release. Null for own-wallet accounts, which mine to their own address on MoneroOcean.
/// </summary>
/// <param name="Kind">"moneroocean" or "kryptex", for display and diagnostics.</param>
/// <param name="User">The stratum login: the pool wallet (MoneroOcean) or "account.worker" (Kryptex).</param>
/// <param name="Password">The stratum password: the worker id (MoneroOcean) or "x" (Kryptex).</param>
public sealed record CpuPoolRoute(string Kind, string Host, int Port, bool Tls, string User, string Password)
{
    public const string MoneroOcean = "moneroocean";
    public const string Kryptex = "kryptex";
}

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

/// <summary>
/// What this computer earned since its last report, sent only when the user agreed to share it ("Share
/// analytics"). It feeds the real-earnings table on otium.lol, where only totals over several computers appear.
/// <paramref name="Sequence"/> increases by one per report, so a retried report is stored once.
/// </summary>
public sealed record EarningsReport(long Sequence, DateTimeOffset PeriodStart, DateTimeOffset PeriodEnd, IReadOnlyList<DeviceEarnings> Devices);

/// <param name="Kind">"cpu" or "gpu".</param>
/// <param name="Model">The graphics card's id ("rtx4090", see <see cref="Gpu.CardIds"/>) or the processor's name.</param>
/// <param name="EarningSeconds">Time spent mining in the period, fee slices included.</param>
/// <param name="UserUsd">The user's share earned in the period (measured hash rate at that hour's prices, after Otium's fee).</param>
/// <param name="ElectricityUsd">The extra power used, at the user's electricity price.</param>
public sealed record DeviceEarnings(string Kind, string? Model, double EarningSeconds, double UserUsd, double ElectricityUsd);

/// <summary>
/// The app has internet sharing earnings waiting at Proxybase (paid only in USDC on the Tempo network) and asks where
/// to send them so they reach the user's own payout address in the coin they chose.
/// </summary>
/// <param name="AvailableMicrocredits">What's waiting (1,000,000 = $1).</param>
/// <param name="RefundAddress">
/// The sharing client's own wallet address on this computer (0x…), where a conversion that fails is refunded. The user's
/// money either way; never Otium's.
/// </param>
/// <param name="ReservedFeeMicrocredits">
/// Otium's fee kept back at Proxybase so far and not yet paid. Apps that pay Otium's fee the exact amount the server
/// names send it (0 included); apps from before leave it out.
/// </param>
public sealed record SharingRouteRequest(long AvailableMicrocredits, string? RefundAddress, long? ReservedFeeMicrocredits = null);

/// <summary>Where to have Proxybase pay, or why to wait.</summary>
/// <param name="Decision">One of <see cref="SharingRouteDecisions"/>.</param>
/// <param name="DepositAddress">With "pay": the Tempo address of a one-time conversion to the user's coin and address.</param>
/// <param name="AmountMicrocredits">With "pay": how much to send there (what's waiting, less Otium's fee).</param>
/// <param name="ExpectedOutput">With "pay": what the user should receive, e.g. "4.23 USDT (TRON)".</param>
/// <param name="CostPercent">The conversion's own network costs, as a share of the amount converted.</param>
/// <param name="Message">With "wait" or "unsupported": why, in plain words.</param>
/// <param name="FeeAddress">Otium's own address on Tempo, where the app pays Otium's fee itself.</param>
/// <param name="FeeMicrocredits">With "pay": Otium's fee on this payout (its mining share of the amount), kept back at
/// Proxybase and paid to <paramref name="FeeAddress"/> once at least $1 of it has built up. It never goes through the
/// conversion.</param>
/// <param name="FeePaymentMicrocredits">With any decision: pay exactly this much to <paramref name="FeeAddress"/> now,
/// out of the kept-back fee (or what's waiting, for a fee still owed beyond it). The amount is a few microcredits off
/// the fee and unique to this payment, which is how the server recognises it on Tempo; payouts stop while one is
/// overdue.</param>
public sealed record SharingRoute(
    string Decision,
    string? DepositAddress = null,
    long AmountMicrocredits = 0,
    string? ExpectedOutput = null,
    double CostPercent = 0,
    string? Message = null,
    string? RequestId = null,
    string? FeeAddress = null,
    long FeeMicrocredits = 0,
    long FeePaymentMicrocredits = 0);

public static class SharingRouteDecisions
{
    public const string Pay = "pay";

    /// <summary>Not enough waiting yet for the conversion's fixed costs to be small; ask again later.</summary>
    public const string Wait = "wait";

    /// <summary>The user's payout coin can't be reached from Proxybase's network (yet).</summary>
    public const string Unsupported = "unsupported";
}

/// <summary>
/// "Send a problem report": what the app knows about why it isn't earning (engine states and problems, the miners'
/// recent output, settings switches, the antivirus in use), with payout addresses and keys taken out before sending.
/// </summary>
/// <param name="Note">What the user wrote about the problem, if anything.</param>
public sealed record ProblemReport(string AppVersion, string Platform, string? Note, string Details);

/// <param name="Reference">A short code the user can quote when asking for help, e.g. "K7Q2M9XA".</param>
public sealed record ProblemReportResult(string Reference);

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

/// <summary>
/// A new Mysterium node asking Otium to pay its one-time registration fee: Otium sends a little MYST to the node's
/// channel address on Polygon, and the node then registers with the user's own wallet as beneficiary.
/// </summary>
public sealed record MystSponsorRequest(string Identity, string ChannelAddress);

/// <param name="Status">One of <see cref="MystSponsorStatuses"/>.</param>
/// <param name="TransactionId">The Polygon transaction that sent the fee, when one was sent.</param>
/// <param name="Message">Why it wasn't sent, in plain words, for the app to show.</param>
public sealed record MystSponsorResult(string Status, string? TransactionId, decimal AmountMyst, string? Message = null);

public static class MystSponsorStatuses
{
    /// <summary>The fee was sent now.</summary>
    public const string Sent = "sent";

    /// <summary>This identity was funded before; just register it.</summary>
    public const string AlreadySent = "already-sent";

    /// <summary>Otium can't pay it right now (no sponsor wallet, budget used up, limit reached).</summary>
    public const string Unavailable = "unavailable";
}

/// <summary>Asks the server to email a 6-digit code.</summary>
/// <param name="Purpose">One of <see cref="EmailCodePurposes"/>; null means signing in.</param>
public sealed record EmailCodeRequest(string Email, string? Purpose = null);

public static class EmailCodePurposes
{
    public const string SignIn = "sign-in";

    /// <summary>Confirms deleting the account; only sent to the email the account is signed in with.</summary>
    public const string DeleteAccount = "delete-account";
}

/// <summary>
/// Deletes the account. Money records are kept, anonymized, for bookkeeping; everything that identifies the user is removed.
/// </summary>
/// <param name="Code">The code emailed for <see cref="EmailCodePurposes.DeleteAccount"/>; required when the account has an email.</param>
/// <param name="GiveUpBalance">The user accepts losing a remaining balance (deletion is refused without it while there is one).</param>
public sealed record DeleteAccountRequest(string? Code, bool GiveUpBalance = false);

/// <param name="GivenUpUsd">Balance (available and pending) the user gave up by deleting.</param>
public sealed record DeleteAccountResult(DateTimeOffset DeletedAt, decimal GivenUpUsd);

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
/// <param name="Methods">The ways the server pays out right now, in display order; null from older servers.</param>
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
    decimal PaidOutUsd = 0,
    IReadOnlyList<CashoutMethodInfo>? Methods = null);

/// <summary>One way to cash out, as the server offers it right now.</summary>
/// <param name="Method">A <see cref="CashoutMethods"/> id, e.g. "btc".</param>
/// <param name="Name">Coin name for the UI, e.g. "Bitcoin".</param>
/// <param name="Network">Where it arrives, e.g. "BNB Smart Chain"; the user's address must be on this network.</param>
/// <param name="MinUsd">Smallest cash-out this way.</param>
/// <param name="FeeNote">What fees come out of the amount, in plain words, or null when Otium pays them.</param>
/// <param name="UnavailableReason">Why it's paused right now (e.g. the swap network halted the coin), or null.</param>
public sealed record CashoutMethodInfo(
    string Method,
    string Name,
    string Network,
    decimal MinUsd,
    string? FeeNote,
    string? UnavailableReason)
{
    public bool Available => UnavailableReason is null;
}

public static class CashoutMethods
{
    public const string Bitcoin = "btc";
    public const string Ethereum = "eth";
    public const string Bnb = "bnb";
    public const string Solana = "sol";

    // Older methods, kept so existing cash-outs still read correctly; servers can still enable them.
    public const string PayPal = "paypal";
    public const string Monero = "xmr";

    /// <summary>Crypto the user can choose, in display order.</summary>
    public static readonly IReadOnlyList<string> Crypto = [Bitcoin, Ethereum, Bnb, Solana];

    public static bool IsKnown(string method) => method is PayPal or Monero || Crypto.Contains(method);

    public static string Name(string method) => method switch
    {
        Bitcoin => "Bitcoin",
        Ethereum => "Ether",
        Bnb => "BNB",
        Solana => "Solana",
        Monero => "Monero",
        PayPal => "PayPal",
        _ => method.ToUpperInvariant(),
    };

    /// <summary>"Bitcoin (BTC)", or just "BNB" when the name is the ticker.</summary>
    public static string Label(string method) => Name(method) == Ticker(method) || method == PayPal ? Name(method) : $"{Name(method)} ({Ticker(method)})";

    /// <summary>Ticker shown next to amounts, e.g. "BTC".</summary>
    public static string Ticker(string method) => method switch
    {
        Bitcoin => "BTC",
        Ethereum => "ETH",
        Bnb => "BNB",
        Solana => "SOL",
        Monero => "XMR",
        _ => method.ToUpperInvariant(),
    };

    public static string Network(string method) => method switch
    {
        Bitcoin => "Bitcoin",
        Ethereum => "Ethereum",
        Bnb => "BNB Smart Chain (BEP-20)",
        Solana => "Solana",
        Monero => "Monero",
        _ => "",
    };

    /// <summary>A block explorer page for a payout transaction, or null when there's none to show.</summary>
    public static string? ExplorerUrl(string method, string? transactionId)
    {
        if (string.IsNullOrEmpty(transactionId))
            return null;
        var evm = transactionId.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? transactionId : "0x" + transactionId;
        return method switch
        {
            Bitcoin => $"https://mempool.space/tx/{transactionId.ToLowerInvariant()}",
            Ethereum => $"https://etherscan.io/tx/{evm.ToLowerInvariant()}",
            Bnb => $"https://bscscan.com/tx/{evm.ToLowerInvariant()}",
            Solana => $"https://solscan.io/tx/{transactionId}",
            _ => null,
        };
    }
}

/// <param name="Method">A <see cref="CashoutMethods"/> id the server offers, e.g. "btc".</param>
/// <param name="Destination">The user's address for that coin (or a PayPal email, where PayPal is enabled).</param>
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
/// <param name="TransactionId">The transaction that delivered the coins once paid, so the user can look it up.</param>
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
