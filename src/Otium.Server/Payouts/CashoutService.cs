using System.Net.Mail;
using Microsoft.Extensions.Options;
using Otium.Core.Accounts;
using Otium.Core.Wallets;
using Otium.Server.Accounts;
using Otium.Server.Data;

namespace Otium.Server.Payouts;

/// <summary>
/// Accepts cash-out requests. The whole available balance is debited atomically and the request is either queued
/// for automatic sending or held for review when something looks unusual. Reviews protect against one person
/// farming many accounts, unusually large amounts, and a provider that isn't set up yet.
/// </summary>
public sealed class CashoutService(
    OtiumStore store,
    IEnumerable<IPayoutProvider> providers,
    IOptions<CashoutOptions> options,
    CashoutMethodCatalog catalog,
    SanctionsList sanctions,
    TimeProvider time,
    ILogger<CashoutService> logger)
{
    /// <summary>Above this yearly total (USD) a US payer must collect tax details; such cash-outs wait for review.</summary>
    public const decimal TaxDetailsThresholdUsd = 1900m;

    private readonly CashoutOptions _options = options.Value;

    public CashoutInfo Request(AccountRecord account, CashoutRequest request)
    {
        var method = (request.Method ?? "").Trim().ToLowerInvariant();
        var destination = (request.Destination ?? "").Trim();

        if (!account.IsBalanceAccount)
            throw new OtiumException(400, "Your earnings are paid by the pool straight to your own wallet, so there's nothing to cash out here.");
        if (account.Email is null)
            throw new OtiumException(403, "Add your email first, so your balance can never get lost.");
        if (!CashoutMethods.IsKnown(method) || !_options.OfferedMethods.Contains(method))
            throw new OtiumException(400, $"Choose one of: {string.Join(", ", catalog.Current.Select(m => m.Name))}.");
        if (method == CashoutMethods.PayPal)
        {
            if (!IsEmail(destination))
                throw new OtiumException(400, "That doesn't look like an email address.");
        }
        else if (!CryptoAddresses.TryValidate(method, destination, out var addressError))
        {
            throw new OtiumException(400, addressError!);
        }
        if (catalog.Find(method)?.UnavailableReason is { } unavailable)
            throw new OtiumException(409, $"{CashoutMethods.Name(method)} payouts are {unavailable}. Choose another coin or try again later.");
        if (sanctions.IsSanctioned(method, destination))
        {
            logger.LogWarning("Refused a cash-out by {Account} to a sanctioned {Method} address", account.AccountId, method);
            throw new OtiumException(403, "This address can't receive payouts from Otium.");
        }
        if (store.HasOpenCashout(account.AccountId))
            throw new OtiumException(409, "You already have a cash-out on its way. You can request the next one when it arrives.");

        var now = time.GetUtcNow();
        var minimum = _options.MinimumFor(method);
        var available = AccountService.ToUsd(store.Balance(account.AccountId, now).AvailableMicros);
        if (available < minimum)
            throw new OtiumException(400, $"You need at least ${Money(minimum)} available to cash out this way (you have ${Money(available)}).");

        var reviewReason = ReviewReason(account, method, destination, available, now);
        var status = reviewReason is null ? Core.Accounts.CashoutStatuses.Queued : Core.Accounts.CashoutStatuses.Review;
        var cashout = store.DebitAndCreateCashout(account.AccountId, method, destination, AccountService.ToMicros(minimum), status, now)
                      ?? throw new OtiumException(400, $"You need at least ${Money(minimum)} available to cash out.");

        if (reviewReason is not null)
        {
            store.UpdateCashout(cashout.Id, status, now, note: reviewReason);
            logger.LogInformation("Cash-out {Id} held for review: {Reason}", cashout.Id, reviewReason);
        }
        return AccountService.ToInfo(store.FindCashout(cashout.Id)!);
    }

    private string? ReviewReason(AccountRecord account, string method, string destination, decimal amount, DateTimeOffset now)
    {
        if (providers.FirstOrDefault(p => p.Method == method) is not { IsConfigured: true })
            return "provider not configured: pay manually, then mark paid";
        if (!sanctions.IsLoaded(method))
            return "sanctions list not loaded yet: check the address, then approve";
        if (amount > _options.AutoApproveMaxUsd)
            return $"amount above automatic limit (${Money(_options.AutoApproveMaxUsd)})";
        if (now - account.CreatedAt < _options.MinAccountAgeForAuto && !store.CashoutsFor(account.AccountId).Any(c => c.Status == Core.Accounts.CashoutStatuses.Paid))
            return "new account";
        if (store.OtherAccountsUsingDestination(destination, account.AccountId) > 0)
            return "destination used by another account";
        if (AccountService.ToUsd(store.AutoPaidMicrosSince(now - TimeSpan.FromDays(1))) + amount > _options.DailyAutoBudgetUsd)
            return "daily automatic payout budget reached";
        if (AccountService.ToUsd(store.PaidMicrosInYear(account.AccountId, now.Year)) + amount > TaxDetailsThresholdUsd)
            return "yearly total near tax-reporting threshold: collect tax details";
        return null;
    }

    private static string Money(decimal usd) => usd.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);

    private static bool IsEmail(string value) =>
        MailAddress.TryCreate(value, out var mail) && mail.Address == value && value.Contains('.');
}
