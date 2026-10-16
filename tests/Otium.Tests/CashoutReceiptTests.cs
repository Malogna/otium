using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Otium.Core.Accounts;
using Otium.Server.Accounts;
using Otium.Server.Data;
using Otium.Server.Payouts;

namespace Otium.Tests;

public sealed class CashoutReceiptTests : MoneyTestBase
{
    private const string TxHash = "a3f1c2d4e5b6a7980112233445566778899aabbccddeeff00112233445566778";

    private readonly FakeProvider _paypal = new(CashoutMethods.PayPal);

    private CashoutService Service => NewCashoutService([_paypal]);

    private CashoutProcessor Processor => new(Store, [_paypal], Receipts, Time, NullLogger<CashoutProcessor>.Instance);

    [Fact]
    public async Task A_paid_cashout_emails_one_receipt_to_the_account()
    {
        var account = NewAccount();
        Fund(account, 12.34m);
        var info = Service.Request(account, new CashoutRequest(CashoutMethods.PayPal, "alex@example.com"));

        await Processor.ProcessOnceAsync(CancellationToken.None);
        await Processor.ProcessOnceAsync(CancellationToken.None);

        var receipt = Assert.Single(Outbox.Sent);
        Assert.Equal(account.Email, receipt.To);
        Assert.Equal("Your Otium cash-out of $12.34 was sent", receipt.Subject);
        Assert.Contains("PayPal", receipt.Body);
        Assert.Contains("al••@example.com", receipt.Body);
        Assert.DoesNotContain("alex@example.com", receipt.Body);
        Assert.Contains(info.Id, receipt.Body);
        Assert.Contains("16 October 2026, 12:00 UTC", receipt.Body);
    }

    [Fact]
    public async Task The_receipt_waits_until_the_money_has_actually_arrived()
    {
        var account = NewAccount();
        Fund(account, 8m);
        _paypal.NextStatus = CashoutStatuses.Sending; // PayPal accepted the batch...
        _paypal.CheckStatus = null;                   // ...but hasn't delivered it yet
        var info = Service.Request(account, new CashoutRequest(CashoutMethods.PayPal, "me@example.com"));

        await Processor.ProcessOnceAsync(CancellationToken.None);
        Assert.Equal(CashoutStatuses.Sending, Store.FindCashout(info.Id)!.Status);
        Assert.Empty(Outbox.Sent);

        _paypal.CheckStatus = CashoutStatuses.Paid;
        Time.Advance(TimeSpan.FromMinutes(1));
        await Processor.ProcessOnceAsync(CancellationToken.None);
        await Processor.ProcessOnceAsync(CancellationToken.None);

        Assert.Contains("12:01 UTC", Assert.Single(Outbox.Sent).Body);
    }

    [Fact]
    public async Task Failed_payouts_send_no_receipt()
    {
        var account = NewAccount();
        Fund(account, 6m);
        _paypal.NextStatus = CashoutStatuses.Failed;
        Service.Request(account, new CashoutRequest(CashoutMethods.PayPal, "me@example.com"));

        await Processor.ProcessOnceAsync(CancellationToken.None);

        Assert.Empty(Outbox.Sent);
    }

    [Fact]
    public async Task Receipts_go_to_the_account_a_cashout_was_merged_into()
    {
        var old = NewAccount();
        var survivor = NewAccount();
        Fund(old, 7m);
        _paypal.IsConfigured = false; // hold it until after the merge
        var info = Service.Request(old, new CashoutRequest(CashoutMethods.PayPal, "me@example.com"));
        Store.MergeAccount(old.AccountId, survivor.AccountId);

        await Receipts.SendPaidAsync(Store.FindCashout(info.Id)!, Time.GetUtcNow(), CancellationToken.None);

        Assert.Equal(survivor.Email, Assert.Single(Outbox.Sent).To);
    }

    [Fact]
    public async Task A_failing_mail_server_does_not_break_payouts()
    {
        var account = NewAccount();
        Fund(account, 9m);
        var info = Service.Request(account, new CashoutRequest(CashoutMethods.PayPal, "me@example.com"));
        var processor = new CashoutProcessor(Store, [_paypal],
            new CashoutReceipts(Store, new BrokenEmail(), NullLogger<CashoutReceipts>.Instance), Time, NullLogger<CashoutProcessor>.Instance);

        await processor.ProcessOnceAsync(CancellationToken.None);

        Assert.Equal(CashoutStatuses.Paid, Store.FindCashout(info.Id)!.Status);
    }

    [Fact]
    public void Monero_receipts_include_the_transaction_hash_but_never_the_tx_key()
    {
        var cashout = Paid(CashoutMethods.Monero, TestAddresses.Generate(), $"{TxHash} key:secretkey");

        var (subject, body) = CashoutReceipts.Compose(cashout, Time.GetUtcNow());

        Assert.Contains("$5.00", subject);
        Assert.Contains("Monero (XMR)", body);
        Assert.Contains(TxHash, body);
        Assert.DoesNotContain("secretkey", body);
        Assert.Contains("confirmations", body);
    }

    [Fact]
    public void The_app_sees_when_and_how_a_cashout_was_paid()
    {
        var info = AccountService.ToInfo(Paid(CashoutMethods.Monero, TestAddresses.Generate(), $"{TxHash} key:secretkey"));

        Assert.Equal(TxHash, info.TransactionId);
        Assert.Equal(Time.GetUtcNow(), info.UpdatedAt);
        Assert.Null(info.Note);
    }

    [Theory]
    [InlineData(CashoutStatuses.Review, "destination used by another account", null)] // fraud checks stay private
    [InlineData(CashoutStatuses.Failed, "PayPal: returned", "PayPal: returned")]
    [InlineData(CashoutStatuses.Sending, PayPalPayoutProvider.UnclaimedNote, PayPalPayoutProvider.UnclaimedNote)]
    [InlineData(CashoutStatuses.Sending, "approved: amount above automatic limit ($50.00)", null)]
    public void Only_notes_meant_for_the_user_reach_the_app(string status, string note, string? shown)
    {
        var record = new CashoutRecord("co_1", "acc", CashoutMethods.PayPal, "me@example.com", 5_000_000, status,
            Time.GetUtcNow(), Time.GetUtcNow(), null, note, 0);

        Assert.Equal(shown, AccountService.ToInfo(record).Note);
    }

    [Fact]
    public void Transaction_ids_are_only_given_for_paid_monero_hashes()
    {
        Assert.Null(AccountService.ToInfo(Paid(CashoutMethods.Monero, TestAddresses.Generate(), "paid by hand")).TransactionId);
        Assert.Null(AccountService.ToInfo(Paid(CashoutMethods.PayPal, "me@example.com", TxHash)).TransactionId);
    }

    [Fact]
    public async Task Paid_out_total_counts_only_money_that_arrived()
    {
        var account = NewAccount();
        Fund(account, 10m);
        Service.Request(account, new CashoutRequest(CashoutMethods.PayPal, "me@example.com"));
        await Processor.ProcessOnceAsync(CancellationToken.None);
        Fund(account, 6m);
        _paypal.NextStatus = CashoutStatuses.Failed;
        Service.Request(account, new CashoutRequest(CashoutMethods.PayPal, "me@example.com"));
        await Processor.ProcessOnceAsync(CancellationToken.None);

        Assert.Equal(10m, AccountService.ToUsd(Store.PaidOutMicros(account.AccountId)));
    }

    private CashoutRecord Paid(string method, string destination, string reference) =>
        new("co_test", "acc", method, destination, 5_000_000, CashoutStatuses.Paid, Time.GetUtcNow() - TimeSpan.FromMinutes(5),
            Time.GetUtcNow(), reference, null, 1);

    private sealed class BrokenEmail : IEmailSender
    {
        public Task SendAsync(string to, string subject, string body, CancellationToken ct) =>
            throw new System.Net.Mail.SmtpException("mail server down");
    }
}
