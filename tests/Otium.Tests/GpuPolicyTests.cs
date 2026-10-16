using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Otium.Core.Accounts;
using Otium.Core.Pools;
using Otium.Core.Switching;
using Otium.Server;
using Otium.Server.Gpu;

namespace Otium.Tests;

/// <summary>The server's GPU policy (live rates, tiers, kill switch) and the WhatToMine feed math.</summary>
public sealed class GpuPolicyTests
{
    private readonly ManualTime _time = new(new DateTimeOffset(2026, 10, 16, 12, 0, 0, TimeSpan.Zero));
    private readonly FakeFeed _feed = new();
    private readonly FakeKryptex _kryptex = new();
    private readonly GpuPolicyOptions _policyOptions = new();
    private readonly KryptexOptions _kryptexOptions = new() { Enabled = true, Account = "otium" };

    internal sealed class FakeFeed : IProfitFeed
    {
        public Dictionary<int, CoinRevenue?> Coins { get; } = [];

        public bool Fail { get; set; }

        public Task<CoinRevenue?> GetAsync(int whatToMineId, CancellationToken ct) =>
            Fail ? throw new HttpRequestException("down") : Task.FromResult(Coins.GetValueOrDefault(whatToMineId));
    }

    internal sealed class FakeKryptex : IKryptexApi
    {
        public Dictionary<string, List<KryptexPoint>> Charts { get; } = [];

        public Task<KryptexPoolInfo> GetPoolInfoAsync(string coin, CancellationToken ct) => Task.FromResult(new KryptexPoolInfo(3));

        public Task<IReadOnlyList<KryptexWorker>> GetWorkersAsync(string coin, string account, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<KryptexWorker>>(Charts.Keys.Where(k => k.StartsWith(coin + ":", StringComparison.Ordinal))
                .Select(k => new KryptexWorker(k[(coin.Length + 1)..], 1)).ToList());

        public Task<IReadOnlyList<KryptexPoint>> GetWorkerChartAsync(string coin, string account, string worker, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<KryptexPoint>>(Charts.GetValueOrDefault($"{coin}:{worker}") ?? []);
    }

    internal sealed class FakePool : IMoneroPoolApi
    {
        public Task<MoneroNetworkStats> GetNetworkStatsAsync(CancellationToken ct) => Task.FromResult(new MoneroNetworkStats(1, 1, 1, DateTimeOffset.UtcNow));

        public Task<PoolMinerStats?> GetMinerStatsAsync(string walletAddress, CancellationToken ct) => Task.FromResult<PoolMinerStats?>(null);

        public Task<AlgoProfits> GetAlgoProfitsAsync(CancellationToken ct) =>
            Task.FromResult(new AlgoProfits(new Dictionary<string, double> { ["kawpow"] = 7.6e-12, ["autolykos2"] = 2.4e-12 }, 545, DateTimeOffset.UtcNow));

        public Task<IReadOnlyDictionary<string, IReadOnlyList<HashratePoint>>> GetWorkerHistoryAsync(string walletAddress, CancellationToken ct) =>
            Task.FromResult<IReadOnlyDictionary<string, IReadOnlyList<HashratePoint>>>(new Dictionary<string, IReadOnlyList<HashratePoint>>());
    }

    private GpuPolicyService Service() => new(new FakePool(), _feed, _kryptex, Options.Create(_policyOptions), Options.Create(_kryptexOptions),
        _time, NullLogger<GpuPolicyService>.Instance);

    private void FreshFeeds()
    {
        _feed.Coins[469] = new CoinRevenue(3.4e-14, false, _time.GetUtcNow());
        _feed.Coins[473] = new CoinRevenue(1.6e-9, false, _time.GetUtcNow());
    }

    [Fact]
    public async Task Kryptex_coins_are_primary_and_legacy_algorithms_need_free_power()
    {
        FreshFeeds();
        var policy = await Service().BuildAsync(CancellationToken.None);

        var pearl = policy.For("pearlhash")!;
        Assert.Equal(GpuTiers.Primary, pearl.Tier);
        Assert.Equal(3.4e-14, pearl.UsdPerHashPerSecondDay);
        Assert.Equal(3, pearl.PoolFeePercent);
        var route = policy.Route(pearl.Route)!;
        Assert.Equal(GpuRoutes.Kryptex, route.Kind);
        Assert.Equal("prl.kryptex.network", route.Host);
        Assert.Equal("otium", route.User);

        Assert.Equal(GpuTiers.FreePowerOnly, policy.For("kawpow")!.Tier);
        Assert.Equal(7.6e-12 * 545, policy.For("kawpow")!.UsdPerHashPerSecondDay, 15);
        Assert.Equal(GpuTiers.Disabled, policy.For("etchash")!.Tier); // MoneroOcean isn't paying for it
    }

    [Fact]
    public async Task The_kill_switch_and_tier_overrides_apply_last()
    {
        FreshFeeds();
        _policyOptions.Disabled = ["pearlhash"];
        _policyOptions.Tiers["kawpow"] = GpuTiers.Primary;

        var policy = await Service().BuildAsync(CancellationToken.None);

        Assert.Equal(GpuTiers.Disabled, policy.For("pearlhash")!.Tier);
        Assert.Equal("Turned off by Otium", policy.For("pearlhash")!.Note);
        Assert.True(policy.For("pearlhash")!.UsdPerHashPerSecondDay > 0); // still priced, so work already done is credited
        Assert.Equal(GpuTiers.Primary, policy.For("kawpow")!.Tier);
        Assert.Equal(GpuTiers.Primary, policy.For("quantus")!.Tier);
    }

    [Fact]
    public async Task A_failed_or_stale_feed_disables_the_coin()
    {
        _feed.Fail = true;
        Assert.Equal(GpuTiers.Disabled, (await Service().BuildAsync(CancellationToken.None)).For("pearlhash")!.Tier);

        _feed.Fail = false;
        _feed.Coins[469] = new CoinRevenue(3.4e-14, false, _time.GetUtcNow() - TimeSpan.FromHours(3));
        _feed.Coins[473] = new CoinRevenue(1.6e-9, Lagging: true, _time.GetUtcNow());
        var policy = await Service().BuildAsync(CancellationToken.None);
        Assert.Equal(GpuTiers.Disabled, policy.For("pearlhash")!.Tier);
        Assert.Equal(GpuTiers.Disabled, policy.For("quantus")!.Tier);
    }

    [Fact]
    public async Task Without_a_kryptex_account_only_moneroocean_is_offered()
    {
        FreshFeeds();
        _kryptexOptions.Account = null;

        var policy = await Service().BuildAsync(CancellationToken.None);

        Assert.Null(policy.For("pearlhash"));
        Assert.Single(policy.Routes);
    }

    [Fact]
    public async Task The_policy_is_cached_between_refreshes()
    {
        FreshFeeds();
        var service = Service();
        var first = await service.GetAsync(CancellationToken.None);

        _feed.Coins[469] = new CoinRevenue(9e-14, false, _time.GetUtcNow());
        Assert.Same(first, await service.GetAsync(CancellationToken.None));

        _time.Advance(_policyOptions.RefreshInterval + TimeSpan.FromSeconds(1));
        _feed.Coins[469] = new CoinRevenue(9e-14, false, _time.GetUtcNow());
        Assert.Equal(9e-14, (await service.GetAsync(CancellationToken.None)).For("pearlhash")!.UsdPerHashPerSecondDay);
    }

    [Fact]
    public void WhatToMine_revenue_per_hash_matches_its_own_dollar_figure()
    {
        // Pearl on 2026-10-16 (trimmed). WhatToMine itself put an RTX 4090 (~310 TH/s) at about $10.44/day.
        using var doc = JsonDocument.Parse("""
            {"id":469,"tag":"PRL","block_time":"177.0","block_reward":2252.15,"block_reward24":2253.5035898742476,
             "nethash":57388423882767740112,"exchange_rate":0.0000228306,"exchange_rate24":0.000021986250087198342,
             "revenue":"$6.86","btc_revenue":"0.00008330","lagging":false,"timestamp":1792112561}
            """);

        var revenue = WhatToMineClient.Parse(doc.RootElement)!;

        var expected = 2253.5035898742476 * 86_400 / 177 / 57388423882767740112d * 0.000021986250087198342 * (6.86 / 0.0000833);
        Assert.Equal(expected, revenue.UsdPerHashPerSecondDay, tolerance: expected * 1e-9);
        Assert.InRange(revenue.UsdPerHashPerSecondDay * 310e12, 8, 14);
        Assert.False(revenue.Lagging);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1792112561), revenue.UpdatedAt);
    }

    [Fact]
    public void Incomplete_feed_data_is_rejected()
    {
        using var doc = JsonDocument.Parse("""{"block_time":"177.0","block_reward":10,"nethash":0,"exchange_rate":1,"revenue":"$1","btc_revenue":"1"}""");
        Assert.Null(WhatToMineClient.Parse(doc.RootElement));
    }

    [Theory]
    [InlineData("""{"timestamp":1792112561}""", 1792112561L)]
    [InlineData("""{"timestamp":1792112561000}""", 1792112561L)]
    [InlineData("""{"timestamp":"2026-10-16T00:22:41Z"}""", 1792110161L)]
    public void Kryptex_timestamps_parse_in_any_form(string json, long seconds)
    {
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(seconds, KryptexClient.ParseTime(doc.RootElement)!.Value.ToUnixTimeSeconds());
    }

    [Fact]
    public void Kryptex_workers_parse_the_live_shape_and_the_documented_one()
    {
        // Trimmed from a real /prl/api/v3/miner/workers/{address} response (October 2026).
        using var live = JsonDocument.Parse("""
            {"results":[{"miner":"prl1p0e","worker":"5082","scheme":"pps","status":"online","valid":3209,"stale":6,"invalid":88,
              "avg_hashrate_24h":"332140472518574.08","avg_hashrate_3h":"290231975986098.63","avg_hashrate_30m":"250199979298360.8888888888889"},
              {"miner":"prl1p0e","worker":"5083","scheme":"pps","status":"offline"}]}
            """);
        var workers = KryptexClient.ParseWorkers(live.RootElement);
        Assert.Equal(["5082", "5083"], workers.Select(w => w.Name));
        Assert.Equal(250199979298360.89, workers[0].Hashrate, 0);
        Assert.Equal(0, workers[1].Hashrate);

        using var documented = JsonDocument.Parse("""{"count":1,"results":[{"worker":"a","hashrate":1234.5}]}""");
        Assert.Equal(1234.5, Assert.Single(KryptexClient.ParseWorkers(documented.RootElement)).Hashrate);
    }

    [Fact]
    public void Kryptex_history_integrates_only_new_points_and_caps_gaps()
    {
        var t0 = new DateTimeOffset(2026, 10, 16, 12, 0, 0, TimeSpan.Zero);
        var points = new List<KryptexPoint>
        {
            new(t0, 100), new(t0.AddMinutes(10), 100), new(t0.AddMinutes(20), 100),
            new(t0.AddHours(3), 100), // the worker was offline for 2h40m: counts as at most 20 minutes
        };

        var (all, newest) = KryptexCreditor.Integrate(points, null);
        Assert.Equal(100 * (600 + 600 + 600 + 1200), all, 6);
        Assert.Equal(t0.AddHours(3).ToUnixTimeMilliseconds(), newest);

        var (rest, _) = KryptexCreditor.Integrate(points, t0.AddMinutes(10).ToUnixTimeMilliseconds());
        Assert.Equal(100 * (600 + 1200), rest, 6);
    }
}

/// <summary>Kryptex crediting and the policy endpoint, through the real server.</summary>
public sealed class KryptexCreditingTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"otium-kx-{Guid.NewGuid():N}.db");
    private readonly ManualTime _time = new(new DateTimeOffset(2026, 10, 16, 12, 0, 0, TimeSpan.Zero));
    private readonly GpuPolicyTests.FakeKryptex _kryptex = new();
    private readonly GpuPolicyTests.FakeFeed _feed = new();
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _http;

    public KryptexCreditingTests()
    {
        _feed.Coins[469] = new CoinRevenue(3.4e-14, false, _time.GetUtcNow());
        _feed.Coins[473] = new CoinRevenue(1.6e-9, false, _time.GetUtcNow());
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Otium", $"Data Source={_dbPath};Pooling=False");
            builder.UseSetting("DisableBackgroundJobs", "true");
            builder.UseSetting("Earnings:PoolWallets:0", TestAddresses.Generate());
            builder.UseSetting("Earnings:FeePercent", "10");
            builder.UseSetting("Kryptex:Enabled", "true");
            builder.UseSetting("Kryptex:Account", "otium");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IMoneroPoolApi>();
                services.AddSingleton<IMoneroPoolApi>(new GpuPolicyTests.FakePool());
                services.RemoveAll<IKryptexApi>();
                services.AddSingleton<IKryptexApi>(_kryptex);
                services.RemoveAll<IProfitFeed>();
                services.AddSingleton<IProfitFeed>(_feed);
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(_time);
            });
        });
        _http = _factory.CreateClient();
    }

    [Fact]
    public async Task Apps_can_fetch_the_policy()
    {
        var policy = await new AccountClient(_http).GetGpuPolicyAsync(CancellationToken.None);

        Assert.Equal(GpuTiers.Primary, policy.For("pearlhash")!.Tier);
        Assert.Equal("otium", policy.Route(policy.For("pearlhash")!.Route)!.User);
    }

    [Fact]
    public async Task Each_users_kryptex_worker_is_credited_once_after_fees()
    {
        var secret = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
        var device = Guid.NewGuid().ToString("N");
        var register = await _http.PostAsJsonAsync("/api/v1/devices", new RegisterDeviceRequest(device, secret, null, null));
        var worker = (await register.Content.ReadFromJsonAsync<AccountSummary>())!.WorkerId;

        // Two 10-minute points at 100 TH/s on Pearl, plus a worker that isn't ours.
        var t0 = _time.GetUtcNow().AddMinutes(-20);
        _kryptex.Charts[$"prl:{worker}"] = [new(t0, 100e12), new(t0.AddMinutes(10), 100e12)];
        _kryptex.Charts["prl:stranger"] = [new(t0, 1e15)];

        var creditor = _factory.Services.GetRequiredService<KryptexCreditor>();
        var first = await creditor.CreditOnceAsync(CancellationToken.None);
        var again = await creditor.CreditOnceAsync(CancellationToken.None);

        Assert.Equal(1, first.WorkersCredited);
        Assert.Equal(0, again.WorkersCredited);
        // 100 TH/s for 20 minutes, at $3.4e-14 per H/s-day, minus Kryptex's 3% and Otium's 10%.
        var expected = 100e12 * 1200 * 3.4e-14 / 86_400 * 0.97 * 0.9;
        var summary = await new AccountClient(_http).GetSummaryAsync(device, secret, CancellationToken.None);
        Assert.Equal(decimal.Round((decimal)expected, 2, MidpointRounding.ToZero), summary.Balance.PendingUsd);
    }

    public void Dispose()
    {
        _http.Dispose();
        _factory.Dispose();
        try
        {
            File.Delete(_dbPath);
        }
        catch (IOException)
        {
        }
    }
}
