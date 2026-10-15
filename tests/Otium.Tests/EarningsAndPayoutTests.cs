using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Otium.Core.Accounts;
using Otium.Core.Pools;
using Otium.Server;
using Otium.Server.Accounts;
using Otium.Server.Data;
using Otium.Server.Earnings;
using Otium.Server.Payouts;

namespace Otium.Tests;

/// <summary>Shared fakes and a real SQLite store for the money-handling tests.</summary>
public abstract class MoneyTestBase : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"otium-money-{Guid.NewGuid():N}.db");

    protected MoneyTestBase()
    {
        Store = new OtiumStore($"Data Source={_dbPath};Pooling=False");
        Store.AddPoolWallet(PoolAddress, Time.GetUtcNow());
    }

    protected ManualTime Time { get; } = new(new DateTimeOffset(2026, 10, 16, 12, 0, 0, TimeSpan.Zero));

    protected OtiumStore Store { get; }

    protected FakePool Pool { get; } = new();

    protected FakeWallet Wallet { get; } = new();

    protected string PoolAddress { get; } = TestAddresses.Generate();

    protected EarningsOptions Earnings { get; } = new() { FeePercent = 10, ReferralPercent = 5, HoldPeriod = TimeSpan.FromHours(48) };

    protected CashoutOptions Cashouts { get; } = new();

    protected AccountRecord NewAccount(string? referredBy = null, DateTimeOffset? feeFreeUntil = null, TimeSpan? age = null, string? email = "default")
    {
        var id = Guid.NewGuid().ToString("N");
        var account = new AccountRecord(id, "ot" + id[..12], PoolAddress, null, id[..7].ToUpperInvariant(),
            email == "default" ? $"{id[..8]}@example.com" : email,
            referredBy, referredBy is null ? null : Time.GetUtcNow(), feeFreeUntil, false, null, null, Time.GetUtcNow() - (age ?? TimeSpan.FromDays(30)));
        Store.Upsert(account);
        return account;
    }

    /// <summary>Gives an account spendable money directly (as if credited and past the hold period).</summary>
    protected void Fund(AccountRecord device, decimal usd) =>
        Store.AddLedger(device.AccountId, LedgerKinds.Earning, AccountService.ToMicros(usd), Time.GetUtcNow(), Time.GetUtcNow(), "test");

    protected decimal Available(AccountRecord device) => AccountService.ToUsd(Store.Balance(device.AccountId, Time.GetUtcNow()).AvailableMicros);

    public void Dispose()
    {
        try
        {
            File.Delete(_dbPath);
        }
        catch (IOException)
        {
        }
    }

    protected sealed class FakePool : IMoneroPoolApi
    {
        // 1 XMR per 1e12 hashes and $500/XMR: one normalized hash is worth $5e-10.
        public MoneroNetworkStats Network { get; set; } = new(1e12, 1, 500, DateTimeOffset.UnixEpoch);

        public Dictionary<string, Dictionary<string, IReadOnlyList<HashratePoint>>> History { get; } = [];

        public Task<MoneroNetworkStats> GetNetworkStatsAsync(CancellationToken ct) => Task.FromResult(Network);

        public Task<PoolMinerStats?> GetMinerStatsAsync(string walletAddress, CancellationToken ct) => Task.FromResult<PoolMinerStats?>(null);

        public Task<AlgoProfits> GetAlgoProfitsAsync(CancellationToken ct) => throw new NotSupportedException();

        public Task<IReadOnlyDictionary<string, IReadOnlyList<HashratePoint>>> GetWorkerHistoryAsync(string walletAddress, CancellationToken ct) =>
            Task.FromResult<IReadOnlyDictionary<string, IReadOnlyList<HashratePoint>>>(
                History.TryGetValue(walletAddress, out var workers) ? workers : new Dictionary<string, IReadOnlyList<HashratePoint>>());
    }

    protected sealed class FakeWallet : IMoneroWallet
    {
        public bool IsConfigured { get; set; } = true;
        public int Created { get; private set; }
        public List<IReadOnlyList<(string Address, long AmountAtomic)>> Transfers { get; } = [];
        public string? FailWith { get; set; }

        public Task<string> CreateAddressAsync(string label, CancellationToken ct)
        {
            Created++;
            return Task.FromResult(TestAddresses.Generate(tag: 42));
        }

        public Task<MoneroTransferResult> TransferAsync(IReadOnlyList<(string Address, long AmountAtomic)> destinations, CancellationToken ct)
        {
            if (FailWith is not null)
                throw new MoneroWalletException(FailWith);
            Transfers.Add(destinations);
            return Task.FromResult(new MoneroTransferResult("txhash123", "txkey456", destinations.Sum(d => d.AmountAtomic), 1000));
        }
    }

    protected sealed class FakeProvider(string method) : IPayoutProvider
    {
        public string Method => method;
        public bool IsConfigured { get; set; } = true;
        public int BatchSize => 10;
        public string? NextStatus { get; set; } = CashoutStatuses.Paid;
        public List<string> Sent { get; } = [];

        public Task<IReadOnlyList<PayoutOutcome>> SendAsync(IReadOnlyList<CashoutRecord> batch, CancellationToken ct)
        {
            Sent.AddRange(batch.Select(c => c.Id));
            return Task.FromResult<IReadOnlyList<PayoutOutcome>>(batch.Select(c => new PayoutOutcome(c.Id, NextStatus, "ref-" + c.Id, NextStatus == CashoutStatuses.Failed ? "bounced" : null)).ToList());
        }

        public Task<PayoutOutcome> CheckAsync(CashoutRecord sending, CancellationToken ct) => Task.FromResult(new PayoutOutcome(sending.Id, CashoutStatuses.Paid));
    }
}

public sealed class EarningsCreditorTests : MoneyTestBase
{
    private EarningsCreditor Creditor => new(Store, Pool, Options.Create(Earnings), Time, NullLogger<EarningsCreditor>.Instance);

    private static IReadOnlyList<HashratePoint> Points(DateTimeOffset start, int count, double hs2) =>
        Enumerable.Range(0, count).Select(i => new HashratePoint(start + TimeSpan.FromMinutes(2 * i), hs2 * 3, hs2)).ToList();

    private void History(AccountRecord device, IReadOnlyList<HashratePoint> points) =>
        Pool.History[PoolAddress] = new Dictionary<string, IReadOnlyList<HashratePoint>> { [device.WorkerId] = points };

    [Fact]
    public async Task Credits_the_value_of_normalized_hashes_minus_the_fee_and_holds_it()
    {
        var device = NewAccount();
        // 6 points × 120 s × 10,000 H/s = 7.2e6 hashes → $0.0036 gross → $0.00324 after the 10% fee.
        History(device, Points(Time.GetUtcNow() - TimeSpan.FromMinutes(12), 6, 10_000));

        var run = await Creditor.CreditOnceAsync(CancellationToken.None);

        Assert.Equal(1, run.WorkersCredited);
        var balance = Store.Balance(device.AccountId, Time.GetUtcNow());
        Assert.Equal(3240, balance.PendingMicros);
        Assert.Equal(0, balance.AvailableMicros);
        Assert.Equal(3240, Store.Balance(device.AccountId, Time.GetUtcNow() + TimeSpan.FromHours(49)).AvailableMicros);
    }

    [Fact]
    public async Task Never_credits_the_same_points_twice()
    {
        var device = NewAccount();
        var start = Time.GetUtcNow() - TimeSpan.FromMinutes(12);
        History(device, Points(start, 6, 10_000));
        await Creditor.CreditOnceAsync(CancellationToken.None);
        await Creditor.CreditOnceAsync(CancellationToken.None);
        Assert.Equal(3240, Store.Balance(device.AccountId, Time.GetUtcNow()).PendingMicros);

        // Two new points later: only those are added (first one spans the 2-minute gap since the last point).
        History(device, Points(start, 8, 10_000));
        await Creditor.CreditOnceAsync(CancellationToken.None);
        Assert.Equal(3240 + 1080, Store.Balance(device.AccountId, Time.GetUtcNow()).PendingMicros);
    }

    [Fact]
    public async Task Fee_free_period_credits_the_full_value()
    {
        var device = NewAccount(feeFreeUntil: Time.GetUtcNow() + TimeSpan.FromDays(10));
        History(device, Points(Time.GetUtcNow() - TimeSpan.FromMinutes(12), 6, 10_000));
        await Creditor.CreditOnceAsync(CancellationToken.None);
        Assert.Equal(3600, Store.Balance(device.AccountId, Time.GetUtcNow()).PendingMicros);
    }

    [Fact]
    public async Task Referrer_earns_five_percent_of_the_friends_gross_earnings()
    {
        var alice = NewAccount();
        var bob = NewAccount(referredBy: alice.Code);
        History(bob, Points(Time.GetUtcNow() - TimeSpan.FromMinutes(12), 6, 10_000));
        await Creditor.CreditOnceAsync(CancellationToken.None);

        Assert.Equal(3240, Store.Balance(bob.AccountId, Time.GetUtcNow()).PendingMicros);
        var aliceBalance = Store.Balance(alice.AccountId, Time.GetUtcNow() + TimeSpan.FromDays(3));
        Assert.Equal(180, aliceBalance.AvailableMicros); // 5% of $0.0036
        Assert.Equal(180, aliceBalance.ReferralMicros);
    }

    [Fact]
    public async Task Unknown_workers_and_long_gaps_are_not_overcredited()
    {
        var device = NewAccount();
        var now = Time.GetUtcNow();
        Pool.History[PoolAddress] = new Dictionary<string, IReadOnlyList<HashratePoint>>
        {
            ["all_other_workers"] = Points(now - TimeSpan.FromHours(1), 10, 1e6),
            // Two points three hours apart: the gap counts as at most 10 minutes.
            [device.WorkerId] = [new HashratePoint(now - TimeSpan.FromHours(3), 0, 1000), new HashratePoint(now, 0, 1000)],
        };
        var run = await Creditor.CreditOnceAsync(CancellationToken.None);
        Assert.Equal(1, run.WorkersCredited);
        // (120 s + 600 s) × 1000 H/s = 720,000 hashes → $0.00036 gross → 324 micros after fee
        Assert.Equal(324, Store.Balance(device.AccountId, now).PendingMicros);
    }

    [Fact]
    public void Worker_history_parser_reads_hs2_and_skips_global()
    {
        const string json = """
            {"global":[{"ts":1792018232000,"hs":5,"hs2":5}],
             "otabc":[{"ts":1792018352000,"hs":5466210435072,"hs2":4266.67},{"ts":1792018232000,"hs":10,"hs2":12.5}]}
            """;
        using var doc = JsonDocument.Parse(json);
        var parsed = MoneroOceanClient.ParseWorkerHistory(doc.RootElement);
        Assert.False(parsed.ContainsKey("global"));
        var points = parsed["otabc"];
        Assert.Equal(2, points.Count);
        Assert.Equal(12.5, points[0].NormalizedHps); // sorted by time
        Assert.Equal(4266.67, points[1].NormalizedHps);
    }
}

public sealed class PoolWalletAllocatorTests : MoneyTestBase
{
    [Fact]
    public async Task Creates_a_new_subaddress_when_every_address_is_full()
    {
        var allocator = new PoolWalletAllocator(Store, Wallet, Options.Create(new EarningsOptions { MaxAccountsPerPoolWallet = 2 }), Time,
            NullLogger<PoolWalletAllocator>.Instance);

        Assert.Equal(PoolAddress, await allocator.AssignAsync(CancellationToken.None));
        NewAccount();
        NewAccount(); // PoolAddress now carries 2 accounts

        var next = await allocator.AssignAsync(CancellationToken.None);
        Assert.NotEqual(PoolAddress, next);
        Assert.Equal(1, Wallet.Created);
    }

    [Fact]
    public async Task Without_any_pool_address_or_wallet_balance_mode_is_unavailable()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"otium-empty-{Guid.NewGuid():N}.db");
        var emptyStore = new OtiumStore($"Data Source={dbPath};Pooling=False");
        var allocator = new PoolWalletAllocator(emptyStore, new FakeWallet { IsConfigured = false }, Options.Create(new EarningsOptions()), Time,
            NullLogger<PoolWalletAllocator>.Instance);
        Assert.Null(await allocator.AssignAsync(CancellationToken.None));
    }
}

public sealed class CashoutTests : MoneyTestBase
{
    private readonly FakeProvider _paypal = new(CashoutMethods.PayPal);
    private readonly FakeProvider _monero = new(CashoutMethods.Monero);

    private CashoutService Service => new(Store, [_paypal, _monero], Options.Create(Cashouts), Time, NullLogger<CashoutService>.Instance);

    private CashoutProcessor Processor => new(Store, [_paypal, _monero], Time, NullLogger<CashoutProcessor>.Instance);

    [Fact]
    public void Below_the_minimum_is_refused_and_nothing_is_debited()
    {
        var device = NewAccount();
        Fund(device, 4.00m);
        var error = Assert.Throws<OtiumException>(() => Service.Request(device, new CashoutRequest(CashoutMethods.PayPal, "me@example.com")));
        Assert.Contains("at least $5.00", error.Message);
        Assert.Equal(4.00m, Available(device));
    }

    [Fact]
    public async Task Ordinary_cashout_is_sent_automatically_and_debits_the_balance()
    {
        var device = NewAccount();
        Fund(device, 12.34m);

        var info = Service.Request(device, new CashoutRequest(CashoutMethods.PayPal, "alex@example.com"));
        Assert.Equal(CashoutStatuses.Queued, info.Status);
        Assert.Equal(12.34m, info.AmountUsd);
        Assert.Equal("al••@example.com", info.Destination);
        Assert.Equal(0m, Available(device));

        await Processor.ProcessOnceAsync(CancellationToken.None);
        Assert.Single(_paypal.Sent);
        Assert.Equal(CashoutStatuses.Paid, Store.FindCashout(info.Id)!.Status);
    }

    [Fact]
    public async Task Failed_payout_returns_the_money_to_the_balance()
    {
        var device = NewAccount();
        Fund(device, 6m);
        _paypal.NextStatus = CashoutStatuses.Failed;
        var info = Service.Request(device, new CashoutRequest(CashoutMethods.PayPal, "me@example.com"));
        await Processor.ProcessOnceAsync(CancellationToken.None);

        Assert.Equal(CashoutStatuses.Failed, Store.FindCashout(info.Id)!.Status);
        Assert.Equal(6m, Available(device));
    }

    [Fact]
    public void Second_request_while_one_is_open_is_refused()
    {
        var device = NewAccount();
        Fund(device, 10m);
        Service.Request(device, new CashoutRequest(CashoutMethods.Monero, TestAddresses.Generate()));
        Fund(device, 10m);
        var error = Assert.Throws<OtiumException>(() => Service.Request(device, new CashoutRequest(CashoutMethods.Monero, TestAddresses.Generate())));
        Assert.Equal(409, error.StatusCode);
    }

    [Theory]
    [InlineData("new account")]
    [InlineData("amount above automatic limit")]
    [InlineData("destination used by another account")]
    [InlineData("provider not configured")]
    public async Task Unusual_cashouts_wait_for_review_and_are_not_sent(string reason)
    {
        var device = NewAccount(age: reason == "new account" ? TimeSpan.FromHours(3) : null);
        Fund(device, reason.StartsWith("amount") ? 75m : 10m);
        if (reason.StartsWith("destination"))
        {
            var other = NewAccount();
            Fund(other, 10m);
            Service.Request(other, new CashoutRequest(CashoutMethods.PayPal, "shared@example.com"));
        }
        if (reason.StartsWith("provider"))
            _paypal.IsConfigured = false;

        var info = Service.Request(device, new CashoutRequest(CashoutMethods.PayPal, "shared@example.com"));

        Assert.Equal(CashoutStatuses.Review, info.Status);
        Assert.Contains(reason, Store.FindCashout(info.Id)!.Note);
        _paypal.Sent.Clear();
        await Processor.ProcessOnceAsync(CancellationToken.None);
        Assert.DoesNotContain(info.Id, _paypal.Sent);
    }

    [Fact]
    public void Own_wallet_accounts_have_nothing_to_cash_out()
    {
        var device = NewAccount() with { OwnWallet = TestAddresses.Generate(), PoolWallet = null };
        Store.Upsert(device);
        var error = Assert.Throws<OtiumException>(() => Service.Request(device, new CashoutRequest(CashoutMethods.PayPal, "me@example.com")));
        Assert.Contains("own wallet", error.Message);
    }

    [Fact]
    public void Accounts_without_an_email_must_add_one_before_cashing_out()
    {
        var device = NewAccount(email: null);
        Fund(device, 10m);
        var error = Assert.Throws<OtiumException>(() => Service.Request(device, new CashoutRequest(CashoutMethods.PayPal, "me@example.com")));
        Assert.Equal(403, error.StatusCode);
        Assert.Equal(10m, Available(device));
    }

    [Fact]
    public void Bad_destinations_are_rejected()
    {
        var device = NewAccount();
        Fund(device, 10m);
        Assert.Throws<OtiumException>(() => Service.Request(device, new CashoutRequest(CashoutMethods.PayPal, "not-an-email")));
        Assert.Throws<OtiumException>(() => Service.Request(device, new CashoutRequest(CashoutMethods.Monero, "44AFFq5k")));
        Assert.Throws<OtiumException>(() => Service.Request(device, new CashoutRequest("venmo", "me@example.com")));
        Assert.Equal(10m, Available(device));
    }

    [Fact]
    public async Task Monero_provider_batches_cashouts_into_one_transaction_at_the_current_price()
    {
        var provider = new MoneroPayoutProvider(Wallet, Pool, NullLogger<MoneroPayoutProvider>.Instance);
        var processor = new CashoutProcessor(Store, [provider], Time, NullLogger<CashoutProcessor>.Instance);
        var service = new CashoutService(Store, [provider], Options.Create(Cashouts), Time, NullLogger<CashoutService>.Instance);

        var a = NewAccount();
        var b = NewAccount();
        Fund(a, 5m);
        Fund(b, 10m);
        var ca = service.Request(a, new CashoutRequest(CashoutMethods.Monero, TestAddresses.Generate()));
        var cb = service.Request(b, new CashoutRequest(CashoutMethods.Monero, TestAddresses.Generate()));

        await processor.ProcessOnceAsync(CancellationToken.None);

        var transfer = Assert.Single(Wallet.Transfers);
        Assert.Equal(2, transfer.Count);
        Assert.Equal(10_000_000_000, transfer[0].AmountAtomic); // $5 at $500/XMR = 0.01 XMR
        Assert.Equal(CashoutStatuses.Paid, Store.FindCashout(ca.Id)!.Status);
        Assert.Contains("txhash123", Store.FindCashout(cb.Id)!.ProviderReference);
    }

    [Fact]
    public async Task Monero_wallet_errors_leave_cashouts_queued_for_a_retry()
    {
        var provider = new MoneroPayoutProvider(Wallet, Pool, NullLogger<MoneroPayoutProvider>.Instance);
        var processor = new CashoutProcessor(Store, [provider], Time, NullLogger<CashoutProcessor>.Instance);
        var service = new CashoutService(Store, [provider], Options.Create(Cashouts), Time, NullLogger<CashoutService>.Instance);
        var device = NewAccount();
        Fund(device, 5m);
        var info = service.Request(device, new CashoutRequest(CashoutMethods.Monero, TestAddresses.Generate()));

        Wallet.FailWith = "not enough unlocked money";
        await processor.ProcessOnceAsync(CancellationToken.None);
        Assert.Equal(CashoutStatuses.Queued, Store.FindCashout(info.Id)!.Status);
        Assert.Equal(0m, Available(device)); // still reserved, not refunded

        Wallet.FailWith = null;
        await processor.ProcessOnceAsync(CancellationToken.None);
        Assert.Equal(CashoutStatuses.Paid, Store.FindCashout(info.Id)!.Status);
    }

    [Theory]
    [InlineData("SUCCESS", CashoutStatuses.Paid)]
    [InlineData("UNCLAIMED", CashoutStatuses.Sending)]
    [InlineData("RETURNED", CashoutStatuses.Failed)]
    [InlineData("BLOCKED", CashoutStatuses.Failed)]
    [InlineData("PENDING", null)]
    [InlineData("ONHOLD", null)]
    public void PayPal_item_statuses_map_to_cashout_statuses(string paypal, string? expected)
    {
        Assert.Equal(expected, PayPalPayoutProvider.MapItemStatus("co_1", paypal).Status);
    }
}
