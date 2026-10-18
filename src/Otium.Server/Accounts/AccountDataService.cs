using System.Globalization;
using System.Text.Json;
using Otium.Core.Accounts;
using Otium.Server.Data;

namespace Otium.Server.Accounts;

/// <summary>
/// Users' data-protection rights (GDPR articles 15, 17 and 20): a copy of everything the server stores about an
/// account, and deleting the account. Deletion anonymizes rather than erases money records, because the operator has
/// to keep bookkeeping records for years; nothing left behind points to the person.
/// </summary>
public sealed class AccountDataService(OtiumStore store, AccountService accounts, TimeProvider time, ILogger<AccountDataService> logger)
{
    public const string ExportFormat = "otium-account-export/1";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    // ---------- export ----------

    public AccountExport Export(string deviceId, string secret)
    {
        var session = accounts.Authenticate(deviceId, secret);
        var account = session.Account;
        var now = time.GetUtcNow();
        var totals = store.Balance(account.AccountId, now);
        var merged = store.MergedAccounts(account.AccountId);

        return new AccountExport(
            ExportFormat,
            now,
            "A copy of the data the Otium server stores about your account. Amounts are in US dollars; micros are millionths " +
            "of a dollar. Sign-in codes are stored only as salted hashes and expire after 15 minutes, so they aren't included. " +
            "People you invited (or who invited you) are shown without anything that identifies them.",
            new ExportedAccount(account.AccountId, account.Email, account.Code, account.WorkerId,
                account.IsBalanceAccount ? account.PoolWallet : null, account.OwnWallet, account.CreatedAt, account.FeeFreeUntil),
            new ExportedBalance(AccountService.ToUsd(Math.Max(0, totals.AvailableMicros)), AccountService.ToUsd(totals.PendingMicros),
                AccountService.ToUsd(totals.LifetimeMicros), AccountService.ToUsd(totals.ReferralMicros)),
            store.DevicesFor(account.AccountId)
                .Select(d => new ExportedDevice(d.DeviceId, d.DeviceId == session.Device.DeviceId, d.CreatedAt, d.LastSeenAt))
                .ToList(),
            store.LedgerFor(account.AccountId)
                .Select(e => new ExportedLedgerEntry(e.Id, e.Kind, ExactUsd(e.Micros), e.Micros, e.CreatedAt, e.AvailableAt, ExportReference(e)))
                .ToList(),
            store.CashoutsFor(account.AccountId, int.MaxValue)
                .OrderBy(c => c.CreatedAt)
                .Select(c => new ExportedCashout(c.Id, c.Method, c.Destination, ExactUsd(c.Micros), c.Status, c.CreatedAt, c.UpdatedAt,
                    c.ProviderReference, c.Note))
                .ToList(),
            new ExportedReferrals(
                account.ReferredAt is not null,
                account.ReferredAt,
                account.Verified,
                store.FindReferees(account.Code).Select(r => new ExportedInvitedFriend(r.ReferredAt, r.Verified)).ToList()),
            new[] { account }.Concat(merged)
                .SelectMany(a => store.BandwidthReportsFor(a.AccountId))
                .OrderBy(r => r.PeriodStart)
                .Select(r => new ExportedBandwidthReport(r.DeviceId, r.Seq, r.Ip, r.PeriodStart, r.PeriodEnd, r.Bytes, r.Seconds,
                    r.CreditedBytes, ExactUsd(r.CreditedMicros), r.Status, r.CreatedAt))
                .ToList(),
            merged.Select(a => new ExportedMergedAccount(a.AccountId, a.WorkerId, a.Code, a.CreatedAt)).ToList(),
            new[] { account }.Concat(merged)
                .SelectMany(a => store.EarningsReportsFor(a.AccountId))
                .OrderBy(r => r.PeriodStart)
                .Select(r => new ExportedEarningsReport(r.Kind, r.Model, r.PeriodStart, r.PeriodEnd, r.Seconds, r.UserUsd, r.ElectricityUsd))
                .ToList());
    }

    public static byte[] Serialize(AccountExport export) => JsonSerializer.SerializeToUtf8Bytes(export, JsonOptions);

    /// <summary>Referral rewards reference the invited friend's earnings (and worker id); keep only that it was a referral.</summary>
    private static string? ExportReference(LedgerEntry entry) =>
        entry.Kind == LedgerKinds.Referral ? "invited friend's earnings" : entry.Reference;

    private static decimal ExactUsd(long micros) => micros / (decimal)OtiumStore.MicrosPerDollar;

    // ---------- deletion ----------

    /// <summary>
    /// Deletes the account this computer is signed in to. Refused while a cash-out is open, while there is a balance
    /// the user hasn't agreed to give up, and (for accounts with an email) without a fresh code sent to that email.
    /// </summary>
    public DeleteAccountResult Delete(string deviceId, string secret, DeleteAccountRequest request)
    {
        var session = accounts.Authenticate(deviceId, secret);
        var account = session.Account;
        var now = time.GetUtcNow();

        if (store.HasOpenCashout(account.AccountId))
            throw new OtiumException(409, OpenCashoutMessage);

        var totals = store.Balance(account.AccountId, now);
        var remaining = Math.Max(0, totals.AvailableMicros) + Math.Max(0, totals.PendingMicros);
        if (remaining > 0 && !request.GiveUpBalance)
        {
            throw new OtiumException(409,
                $"You still have ${AccountService.ToUsd(remaining).ToString("0.00", CultureInfo.InvariantCulture)} in Otium. " +
                "Cash it out first, or confirm that you're giving it up.");
        }

        if (account.Email is not null)
        {
            if (string.IsNullOrWhiteSpace(request.Code))
                throw new OtiumException(400, $"Enter the code we emailed to {account.Email}.");
            accounts.ConsumeEmailCode(account.Email, session.Device.DeviceId, request.Code);
        }

        if (!store.AnonymizeAccount(account.AccountId, MaskForDeletion, now))
            throw new OtiumException(409, OpenCashoutMessage);

        logger.LogInformation("Account {Account} deleted at the user's request; ${GivenUp} balance given up",
            account.AccountId, AccountService.ToUsd(remaining));
        return new DeleteAccountResult(now, AccountService.ToUsd(remaining));
    }

    private const string OpenCashoutMessage =
        "A cash-out is still on its way. You can delete your account once it has arrived (or been returned to your balance).";

    /// <summary>
    /// What stays of a cash-out destination after deletion: enough to tell payouts apart, not enough to identify anyone.
    /// The provider's reference (PayPal batch, Monero transaction) stays, so a payout can still be traced at the
    /// provider if the law requires it.
    /// </summary>
    public static string MaskForDeletion(string method, string destination)
    {
        if (method == CashoutMethods.PayPal)
        {
            var dot = destination.LastIndexOf('.');
            return dot > destination.IndexOf('@') && destination.IndexOf('@') >= 0 ? "•••@•••" + destination[dot..] : "•••";
        }
        return destination.Length > 8 ? $"{destination[..4]}…{destination[^4..]}" : "•••";
    }
}

/// <summary>Everything stored about an account, as delivered to its owner.</summary>
public sealed record AccountExport(
    string Format,
    DateTimeOffset ExportedAt,
    string Notice,
    ExportedAccount Account,
    ExportedBalance Balance,
    IReadOnlyList<ExportedDevice> Devices,
    IReadOnlyList<ExportedLedgerEntry> Ledger,
    IReadOnlyList<ExportedCashout> Cashouts,
    ExportedReferrals Referrals,
    IReadOnlyList<ExportedBandwidthReport> BandwidthReports,
    IReadOnlyList<ExportedMergedAccount> MergedAccounts,
    IReadOnlyList<ExportedEarningsReport>? EarningsReports = null);

/// <summary>An earnings report this account's computers sent because the user chose to share them.</summary>
public sealed record ExportedEarningsReport(
    string Kind,
    string? Model,
    DateTimeOffset PeriodStart,
    DateTimeOffset PeriodEnd,
    double EarningSeconds,
    double UserUsd,
    double ElectricityUsd);

public sealed record ExportedAccount(
    string AccountId,
    string? Email,
    string ReferralCode,
    string WorkerId,
    string? PoolWallet,
    string? OwnWallet,
    DateTimeOffset CreatedAt,
    DateTimeOffset? FeeFreeUntil);

public sealed record ExportedBalance(decimal AvailableUsd, decimal PendingUsd, decimal LifetimeUsd, decimal ReferralUsd);

public sealed record ExportedDevice(string DeviceId, bool ThisComputer, DateTimeOffset CreatedAt, DateTimeOffset LastSeenAt);

public sealed record ExportedLedgerEntry(
    long Id,
    string Kind,
    decimal AmountUsd,
    long AmountMicros,
    DateTimeOffset CreatedAt,
    DateTimeOffset AvailableAt,
    string? Reference);

public sealed record ExportedCashout(
    string Id,
    string Method,
    string Destination,
    decimal AmountUsd,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string? ProviderReference,
    string? Note);

/// <param name="CountsAsVerifiedFriend">Whether this account has done enough to count as a verified invite for whoever invited it.</param>
/// <param name="InvitedFriends">People who joined with this account's code: only when, and whether they were verified.</param>
public sealed record ExportedReferrals(
    bool JoinedWithInvite,
    DateTimeOffset? JoinedWithInviteAt,
    bool CountsAsVerifiedFriend,
    IReadOnlyList<ExportedInvitedFriend> InvitedFriends);

public sealed record ExportedInvitedFriend(DateTimeOffset? JoinedAt, bool Verified);

public sealed record ExportedBandwidthReport(
    string DeviceId,
    long Sequence,
    string Ip,
    DateTimeOffset PeriodStart,
    DateTimeOffset PeriodEnd,
    long Bytes,
    double SharingSeconds,
    long CreditedBytes,
    decimal CreditedUsd,
    string Status,
    DateTimeOffset ReceivedAt);

/// <summary>An anonymous account from another computer that was folded into this one when it signed in.</summary>
public sealed record ExportedMergedAccount(string AccountId, string WorkerId, string ReferralCode, DateTimeOffset CreatedAt);
