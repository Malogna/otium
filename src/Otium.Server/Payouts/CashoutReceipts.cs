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

    /// <summary>"Monero (XMR)", "Ether (ETH) on Ethereum", "BNB on BNB Smart Chain (BEP-20)", "PayPal".</summary>
    private static string MethodLabel(string method)
    {
        var network = CashoutMethods.Network(method);
        var label = CashoutMethods.Label(method);
        return network.Length > 0 && network != CashoutMethods.Name(method) ? $"{label} on {network}" : label;
    }

    public static (string Subject, string Body) Compose(CashoutRecord cashout, DateTimeOffset paidAt)
    {
        var amount = "$" + AccountService.ToUsd(cashout.Micros).ToString("0.00", CultureInfo.InvariantCulture);
        var transaction = AccountService.TransactionId(cashout);

        var body = new StringBuilder()
            .AppendLine($"Your cash-out of {amount} has been sent.")
            .AppendLine()
            .AppendLine($"  Amount:     {amount}")
            .AppendLine($"  Method:     {MethodLabel(cashout.Method)}")
            .AppendLine($"  To:         {AccountService.MaskDestination(cashout.Method, cashout.Destination)}")
            .AppendLine(CultureInfo.InvariantCulture, $"  Requested:  {cashout.CreatedAt.UtcDateTime:d MMMM yyyy, HH:mm} UTC")
            .AppendLine(CultureInfo.InvariantCulture, $"  Sent:       {paidAt.UtcDateTime:d MMMM yyyy, HH:mm} UTC")
            .AppendLine($"  Reference:  {cashout.Id}");
        if (transaction is not null)
            body.AppendLine($"  Transaction: {transaction}");
        if (CashoutMethods.ExplorerUrl(cashout.Method, transaction) is { } explorer)
            body.AppendLine($"  View it:    {explorer}");
        body.AppendLine()
            .AppendLine(cashout.Method switch
            {
                CashoutMethods.PayPal => "It's in your PayPal account now. If you didn't have a PayPal account for this email yet, PayPal has sent you a link to claim it.",
                CashoutMethods.Monero => "Monero needs 10 network confirmations before you can spend it, which usually takes about 20 minutes.",
                CashoutMethods.Bitcoin => "Your wallet shows it once the transaction confirms, usually within an hour.",
                _ => "It was swapped from Bitcoin through THORChain and sent to your address, so it's in your wallet now.",
            })
            .AppendLine()
            .AppendLine("You can see all your cash-outs in the Otium app. If you didn't ask for this cash-out, reply to this email.")
            .AppendLine()
            .AppendLine("Otium");

        return ($"Your Otium cash-out of {amount} was sent", body.ToString());
    }
}
