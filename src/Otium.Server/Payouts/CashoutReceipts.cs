using System.Globalization;
using System.Text;
using Otium.Core.Accounts;
using Otium.Server.Accounts;
using Otium.Server.Data;

namespace Otium.Server.Payouts;

/// <summary>
/// Emails the account owner a receipt when a cash-out is paid, whether the processor sent it or the operator
/// marked a manual payment. A failed email never affects the payout itself; it is only logged.
/// </summary>
public sealed class CashoutReceipts(OtiumStore store, IEmailSender email, ILogger<CashoutReceipts> logger)
{
    public async Task SendPaidAsync(CashoutRecord cashout, DateTimeOffset paidAt, CancellationToken ct)
    {
        var account = store.Resolve(store.FindAccount(cashout.AccountId));
        if (account?.Email is not { } to)
        {
            logger.LogInformation("No email on file for cash-out {Id}; receipt not sent", cashout.Id);
            return;
        }

        try
        {
            var (subject, body) = Compose(cashout, paidAt);
            await email.SendAsync(to, subject, body, ct);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            logger.LogWarning(e, "Couldn't send the receipt for cash-out {Id}", cashout.Id);
        }
    }

    public static (string Subject, string Body) Compose(CashoutRecord cashout, DateTimeOffset paidAt)
    {
        var amount = "$" + AccountService.ToUsd(cashout.Micros).ToString("0.00", CultureInfo.InvariantCulture);
        var isPayPal = cashout.Method == CashoutMethods.PayPal;
        var transaction = AccountService.TransactionId(cashout);

        var body = new StringBuilder()
            .AppendLine($"Your cash-out of {amount} has been sent.")
            .AppendLine()
            .AppendLine($"  Amount:     {amount}")
            .AppendLine($"  Method:     {(isPayPal ? "PayPal" : "Monero (XMR)")}")
            .AppendLine($"  To:         {AccountService.MaskDestination(cashout.Method, cashout.Destination)}")
            .AppendLine(CultureInfo.InvariantCulture, $"  Requested:  {cashout.CreatedAt.UtcDateTime:d MMMM yyyy, HH:mm} UTC")
            .AppendLine(CultureInfo.InvariantCulture, $"  Sent:       {paidAt.UtcDateTime:d MMMM yyyy, HH:mm} UTC")
            .AppendLine($"  Reference:  {cashout.Id}");
        if (transaction is not null)
            body.AppendLine($"  Transaction: {transaction}");
        body.AppendLine()
            .AppendLine(isPayPal
                ? "It's in your PayPal account now. If you didn't have a PayPal account for this email yet, PayPal has sent you a link to claim it."
                : "Monero needs 10 network confirmations before you can spend it, which usually takes about 20 minutes.")
            .AppendLine()
            .AppendLine("You can see all your cash-outs in the Otium app. If you didn't ask for this cash-out, reply to this email.")
            .AppendLine()
            .AppendLine("Otium");

        return ($"Your Otium cash-out of {amount} was sent", body.ToString());
    }
}
