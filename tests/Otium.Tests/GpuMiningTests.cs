using System.Runtime.InteropServices;
using System.Text.Json;
using Otium.Core.Gpu;
using Otium.Core.Pools;
using Otium.Core.Power;
using Otium.Core.Providers;
using Otium.Core.Switching;
using Otium.Providers.SrbMiner;

namespace Otium.Tests;

public class ProfitSwitcherTests
{
    private static readonly DateTimeOffset T = new(2026, 10, 15, 12, 0, 0, TimeSpan.Zero);
    private readonly ProfitSwitcher _switcher = new();

    private static AlgorithmQuote Quote(string algo, double perHash, double fee = 1) => new(algo, perHash, fee);

    [Fact]
    public void Scores_by_net_profit_after_fees_and_electricity()
    {
        var scored = _switcher.Score(
            [new BenchmarkResult("kawpow", 30e6, 220, T), new BenchmarkResult("autolykos2", 200e6, 160, T)],
            [Quote("kawpow", 7.6e-12), Quote("autolykos2", 2.43e-12)],
            coinPriceUsd: 545,
            pricePerKwh: 0.15,
            fallbackWatts: 180);

        Assert.Equal("autolykos2", scored[0].Algorithm);
        var kawpow = scored.Single(o => o.Algorithm == "kawpow");
        // 30 MH/s * 7.6e-12 XMR/(H/s·day) * 0.99 dev * 0.99 pool * $545
        Assert.Equal(30e6 * 7.6e-12 * 0.99 * 0.99 * 545, kawpow.GrossUsdPerDay, precision: 9);
        Assert.Equal(220 * 24 / 1000.0 * 0.15, kawpow.ElectricityUsdPerDay, precision: 9);
    }

    [Fact]
    public void Expensive_electricity_can_change_the_winner()
    {
        // Hungry etchash earns ~$0.27/day at 300 W; efficient cn/gpu ~$0.21/day at 120 W.
        BenchmarkResult[] benches = [new("etchash", 100e6, 300, T), new("cn/gpu", 4000, 120, T)];
        AlgorithmQuote[] quotes = [Quote("etchash", 5e-12), Quote("cn/gpu", 1e-7)];

        var cheap = _switcher.Score(benches, quotes, 545, pricePerKwh: 0.0, fallbackWatts: 0);
        var pricey = _switcher.Score(benches, quotes, 545, pricePerKwh: 0.30, fallbackWatts: 0);
        Assert.Equal("etchash", cheap[0].Algorithm);
        Assert.Equal("cn/gpu", pricey[0].Algorithm);
    }

    [Fact]
    public void Failed_benchmarks_and_unpaid_algorithms_are_ignored()
    {
        var scored = _switcher.Score(
            [new BenchmarkResult("kawpow", 0, null, T, "out of memory"), new BenchmarkResult("etchash", 50e6, 200, T), new BenchmarkResult("pearlhash", 1e14, 300, T)],
            [Quote("kawpow", 7e-12), Quote("etchash", 1e-12), Quote("pearlhash", 0)],
            545, 0.15, 180);
        Assert.Equal(["etchash"], scored.Select(s => s.Algorithm));
    }

    [Fact]
    public void Missing_power_measurement_uses_the_fallback_watts()
    {
        var scored = _switcher.Score([new BenchmarkResult("kawpow", 30e6, null, T)], [Quote("kawpow", 7e-12)], 545, 0.15, fallbackWatts: 180);
        Assert.Equal(180 * 24 / 1000.0 * 0.15, scored[0].ElectricityUsdPerDay, precision: 9);
        Assert.Null(scored[0].ExtraWatts);
    }

    [Fact]
    public void Hysteresis_and_minimum_hold_prevent_flapping()
    {
        ScoredOption[] scored = [new("autolykos2", 1, 1.03, 0.30, 150), new("kawpow", 1, 1.00, 0.30, 150)];

        Assert.Equal("autolykos2", _switcher.Choose(scored, current: null, TimeSpan.Zero));
        // 4% better isn't enough to leave kawpow (needs 5%)
        Assert.Equal("kawpow", _switcher.Choose(scored, "kawpow", TimeSpan.FromHours(1)));

        ScoredOption[] clearlyBetter = [new("autolykos2", 1, 1.50, 0.30, 150), new("kawpow", 1, 1.00, 0.30, 150)];
        // clearly better, but held for less than the minimum
        Assert.Equal("kawpow", _switcher.Choose(clearlyBetter, "kawpow", TimeSpan.FromMinutes(5)));
        Assert.Equal("autolykos2", _switcher.Choose(clearlyBetter, "kawpow", TimeSpan.FromMinutes(20)));
    }

    [Fact]
    public void Leaves_an_algorithm_that_stopped_paying()
    {
        ScoredOption[] scored = [new("etchash", 1, 0.8, 0.3, 100)];
        Assert.Equal("etchash", _switcher.Choose(scored, "kawpow", TimeSpan.FromMinutes(1)));
        Assert.Null(_switcher.Choose([], "kawpow", TimeSpan.FromHours(1)));
    }
}

public class MoneroOceanAlgoProfitTests
{
    [Fact]
    public void Takes_the_best_active_coin_per_algorithm()
    {
        // Trimmed from MoneroOcean's /pool/stats on 2026-10-15.
        const string json = """
            {"pool_statistics":{
              "price":{"usd":544.59},
              "activePorts":[18081,8766,19001,9053,17767],
              "portCoinAlgo":{"18081":"rx/0","8766":"kawpow","19001":"kawpow","9053":"autolykos2","17767":"rx/0","9998":"ghostrider"},
              "coinProfit":{"18081":7.683e-08,"8766":6.675e-12,"19001":7.636e-12,"9053":2.426e-12,"17767":4.48e-08,"9998":5.2e-08}
            }}
            """;
        using var doc = JsonDocument.Parse(json);
        var profits = MoneroOceanClient.ParseAlgoProfits(doc.RootElement, DateTimeOffset.UnixEpoch);

        Assert.Equal(544.59, profits.XmrPriceUsd);
        Assert.Equal(7.636e-12, profits.For("kawpow")); // XNA beats RVN
        Assert.Equal(7.683e-08, profits.For("rx/0"));
        Assert.Equal(2.426e-12, profits.For("autolykos2"));
        Assert.Equal(0, profits.For("ghostrider")); // port not active
        Assert.Equal(0, profits.For("etchash"));
    }
}

public class SrbMinerTests
{
    private static readonly GpuInfo Rtx3080 = new(GpuVendor.Nvidia, 0, "NVIDIA GeForce RTX 3080", 12);

    [Fact]
    public void Arguments_pin_the_algorithm_in_the_password_and_leave_the_cpu_alone()
    {
        var cnGpu = GpuAlgorithms.Find("cn/gpu")!;
        var args = SrbMinerArgs.Build(cnGpu, PoolLogin.MoneroOcean(GpuPoolEndpoint.MoneroOcean, "WALLET", "sc12345678g", cnGpu), 40001, [Rtx3080]);
        string Value(string flag) => args[args.ToList().IndexOf(flag) + 1];

        Assert.Equal("cryptonight_gpu", Value("--algorithm"));
        Assert.Equal("gulf.moneroocean.stream:10128", Value("--pool"));
        Assert.Equal("WALLET", Value("--wallet"));
        Assert.Equal("sc12345678g~cn/gpu", Value("--password"));
        Assert.Equal("40001", Value("--api-port"));
        Assert.Contains("--disable-cpu", args);
        Assert.Contains("--disable-gpu-amd", args);
        Assert.Contains("--disable-gpu-intel", args);
        Assert.Equal("2", Value("--miner-priority")); // below normal: the user may be at the computer
        var away = SrbMinerArgs.Build(cnGpu, PoolLogin.MoneroOcean(GpuPoolEndpoint.MoneroOcean, "WALLET", "sc12345678g", cnGpu), 40001, [Rtx3080], lowPriority: false);
        Assert.Equal("3", away[away.ToList().IndexOf("--miner-priority") + 1]); // normal while they're away
        Assert.DoesNotContain("--disable-gpu-nvidia", args);
    }

    [Fact]
    public void Algorithms_respect_vendor_and_memory()
    {
        var small = new GpuInfo(GpuVendor.Nvidia, 0, "GTX 1650", 4);
        Assert.False(GpuAlgorithms.Find("kawpow")!.Supports([small]));
        Assert.True(GpuAlgorithms.Find("autolykos2")!.Supports([small]));
        Assert.True(GpuAlgorithms.Find("kawpow")!.Supports([Rtx3080]));
        Assert.False(GpuAlgorithms.Find("kawpow")!.Supports([new GpuInfo(GpuVendor.Intel, 0, "Arc", 16)]));
        // unknown VRAM (0) doesn't exclude anything
        Assert.True(GpuAlgorithms.Find("pearlhash")!.Supports([new GpuInfo(GpuVendor.Amd, 0, "Radeon", 0)]));
    }

    [Theory]
    [InlineData("""{"algorithms":[{"name":"kawpow","hashrate":{"gpu":{"gpu0":25000000.5,"total":25000000.5}}}]}""", 25000000.5)]
    [InlineData("""{"algorithms":[{"name":"kawpow","hashrate":{"total":"1234.5"}}]}""", 1234.5)]
    [InlineData("""{"algorithms":[{"name":"etchash","hashrate":61000000}]}""", 61000000)]
    [InlineData("""{"hashrate_total_now":4200}""", 4200)]
    public void Api_hashrate_is_read_from_known_shapes(string json, double expected)
    {
        Assert.Equal(expected, SrbMinerStats.ParseApiHashrate(json));
    }

    [Fact]
    public void Unknown_api_shape_returns_null()
    {
        Assert.Null(SrbMinerStats.ParseApiHashrate("""{"miner":"x"}"""));
    }

    [Fact]
    public void Real_srbminer_376_api_is_read_in_hps()
    {
        // Trimmed from SRBMiner-Multi 3.7.6 mining kawpow on an RTX 3080 (October 2026); the UI showed 33.54 MH/s.
        const string json = """
            {"rig_name":"otium","miner_version":"3.7.6","mining_time":68,"total_gpu_workers":1,
             "gpu_devices":[{"id":0,"device":"gpu0","vendor":"nvidia","model":"nvidia_geforce_rtx_3080","asic_power":266}],
             "algorithms":[{"id":0,"name":"kawpow",
               "pool":{"pool":"gulf.moneroocean.stream:10128","difficulty":0.987,"last_job_received":2},
               "shares":{"total":0,"accepted":0,"rejected":0,"avg_find_time":0},
               "hashrate":{"1min":33728089.38,"15min":33728089.38,"1hr":33728089.38,"6hr":33728089.38,
                           "cpu":{"total":0.0},"gpu":{"gpu0":33040828.85,"total":33040828.85}}}]}
            """;
        Assert.Equal(33040828.85, SrbMinerStats.ParseApiHashrate(json));
    }

    [Fact]
    public void Srbminer_api_before_the_first_job_has_no_hashrate()
    {
        // While SRBMiner can't reach the pool, 3.7.6 serves "algorithms": null.
        Assert.Null(SrbMinerStats.ParseApiHashrate("""{"rig_name":"otium","miner_version":"3.7.6","mining_time":5,"algorithms":null}"""));
    }

    [Fact]
    public void Real_srbminer_376_console_total_line_is_read()
    {
        string[] lines =
        [
            "[2026-10-16 14:58:41] GPU0 RTX 3080: 0.00 H/s [T:63C FAN:40% P:91.1W EFF:- CC:1230 MC:5001 A:0 R:0 HW:0]",
            "[2026-10-16 14:58:41] Total: 0.00 H/s [P:91.1W EFF: A:0 R:0 HW:0]",
            "[2026-10-16 14:58:41] Average hashrate: 1m 0.00 H/s | 15m 0.00 H/s | 1h 0.00 H/s | 6h 0.00 H/s",
        ];
        Assert.Equal(0, SrbMinerStats.ParseConsoleHashrate(lines));
    }

    [Fact]
    public void Console_hashrate_fallback_normalises_units()
    {
        string[] lines = ["[2026-10-15 22:00:01] GPU0 : 24.88 MH/s", "[2026-10-15 22:00:31] Total: 25.12 MH/s", "noise"];
        Assert.Equal(25.12e6, SrbMinerStats.ParseConsoleHashrate(lines)!.Value, precision: 3);
        Assert.Null(SrbMinerStats.ParseConsoleHashrate(["nothing here"]));
    }

    [Fact]
    public void A_stale_console_total_line_does_not_count_as_hashing()
    {
        // A frozen miner (seen on the Kryptex run) stops answering its API and stops printing; its last line is old.
        string[] lines = ["[2026-10-16 16:36:28] Total: 360.53 MH/s [P:349.2W EFF: A:2 R:1 HW:0]"];
        Assert.Equal(360.53e6, SrbMinerStats.ParseConsoleHashrate(lines, new DateTime(2026, 10, 16, 16, 36, 0))!.Value, precision: 0);
        Assert.Null(SrbMinerStats.ParseConsoleHashrate(lines, new DateTime(2026, 10, 16, 16, 38, 0)));
        Assert.Equal(25.12e6, SrbMinerStats.ParseConsoleHashrate(["Total: 25.12 MH/s"], DateTime.MaxValue)!.Value, precision: 0); // no timestamp
    }

    [Fact]
    public void Real_srbminer_dev_fee_and_failure_lines_are_recognised()
    {
        Assert.True(SrbMinerStats.StartedFirstDevFeeRound(["[2026-10-16 15:01:37] Hold on, doing the first devfee mining round[0]"]));
        Assert.False(SrbMinerStats.StartedFirstDevFeeRound(["[2026-10-16 15:01:38] Connected to gulf.moneroocean.stream:10128 [0]"]));

        // Seen on the retest with 10.9 GB of VRAM free, so it mustn't be blamed on memory.
        Assert.Equal("the miner couldn't load this coin's dataset onto the graphics card (\"Couldn't set epoch 611 on GPU0\")",
            SrbMinerStats.DescribeProblem(["[2026-10-16 15:02:23] Couldn't set epoch 611 on GPU0 [0]", "[2026-10-16 15:02:32] Total: 0.00 H/s [P:108.4W EFF: A:0 R:0 HW:0]"]));
        Assert.StartsWith("it ran out of graphics memory", SrbMinerStats.DescribeProblem(["not enough GPU memory"]));
        Assert.Equal("the miner reported \"OCL Error code -52 [clEnqueueNDRangeKernel [2]]\"",
            SrbMinerStats.DescribeProblem(["[2026-10-16 14:52:10] OCL Error code -52 [clEnqueueNDRangeKernel [2]]"]));
        Assert.Null(SrbMinerStats.DescribeProblem(
        [
            "[2026-10-16 14:58:41] GPU0 RTX 3080: 0.00 H/s [T:63C FAN:40% P:91.1W EFF:- CC:1230 MC:5001 A:0 R:0 HW:0]",
            "[2026-10-16 14:58:41] Total: 0.00 H/s [P:91.1W EFF: A:0 R:0 HW:0]",
        ]));
    }

    [Fact]
    public void Release_assets_cover_windows_and_linux_x64_only()
    {
        Assert.Equal("SRBMiner-Multi-3-7-6-win64.zip", SrbMinerRelease.For(OSPlatform.Windows, Architecture.X64)!.FileName);
        Assert.Equal("SRBMiner-Multi-3-7-6-Linux.tar.gz", SrbMinerRelease.For(OSPlatform.Linux, Architecture.X64)!.FileName);
        Assert.Null(SrbMinerRelease.For(OSPlatform.OSX, Architecture.Arm64));
        Assert.Null(SrbMinerRelease.For(OSPlatform.Linux, Architecture.Arm64));
    }
}

public sealed class SrbMinerProviderTests : IDisposable
{
    private static readonly GpuInfo Rtx3080 = new(GpuVendor.Nvidia, 0, "NVIDIA GeForce RTX 3080", 12);
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"sc-gpu-{Guid.NewGuid():N}");
    private readonly ManualTime _time = new(new DateTimeOffset(2026, 10, 15, 12, 0, 0, TimeSpan.Zero));
    private readonly FakePool _pool = new();
    private readonly FakeFactory _factory = new();
    private readonly FakeMeter _meter = new();
    private readonly SrbMinerGpuProvider _provider;
    private static readonly ProviderRunOptions Options = new(new MiningTarget(TestAddresses.Generate(), PayoutRole.User), "sc1234abcdg", 100);

    public SrbMinerProviderTests()
    {
        // A stand-in binary so the provider believes the engine is installed.
        var engineDir = Path.Combine(_dir, "engines", $"srbminer-{SrbMinerRelease.Version}");
        Directory.CreateDirectory(engineDir);
        File.WriteAllText(Path.Combine(engineDir, SrbMinerRelease.BinaryName), "x");
        File.WriteAllText(Path.Combine(engineDir, ".verified"), "x");

        // Free electricity, so MoneroOcean's "free power only" algorithms take part (these tests cover switching itself).
        _provider = CreateProvider(policy: null, pricePerKwh: 0, benchmarks: "bench.json");
    }

    private SrbMinerGpuProvider CreateProvider(Func<GpuPolicy?>? policy, double pricePerKwh, string? benchmarks = null, Func<GpuInfo, double?>? freeMemoryGb = null,
        IUnmineableApi? unmineable = null, Func<string, int, CancellationToken, Task<string?>>? poolBlocked = null) =>
        new(new HttpClient(), _pool, Path.Combine(_dir, "engines"), Path.Combine(_dir, benchmarks ?? $"bench-{Guid.NewGuid():N}.json"),
            [Rtx3080], _meter, () => pricePerKwh, _factory, _time,
            asset: SrbMinerRelease.For(OSPlatform.Windows, Architecture.X64), policy: policy, freeMemoryGb: freeMemoryGb, unmineable: unmineable,
            poolBlocked: poolBlocked);

    [Fact]
    public async Task A_pool_blocked_on_this_computer_is_explained_instead_of_failing_the_coin()
    {
        string? blocked = null;
        var provider = CreateProvider(() => UnmineablePearlPolicy(), pricePerKwh: 0,
            poolBlocked: (host, port, _) => Task.FromResult(host == "pearlpow.unmineable.com" ? blocked : null));
        var options = new ProviderRunOptions(new MiningTarget(TestAddresses.Usdt(8), PayoutRole.User), "ot1234abcdg", 100);
        await provider.StartAsync(options, CancellationToken.None);
        await RunForProvider(provider, SrbMinerGpuProvider.BenchmarkDuration * 2 + TimeSpan.FromSeconds(10));
        Assert.Equal("pearlhash", provider.CurrentAlgorithm);

        // The miner can't reach unMineable: Malwarebytes is blocking it.
        blocked = "something on this computer blocks the mining pool (unmineable.com)";
        _factory.Launches[^1].Process.Hashrate = 0;
        var status = await RunForProvider(provider, SrbMinerGpuProvider.StallTimeout + TimeSpan.FromSeconds(10));
        Assert.Equal("Graphics card isn't mining: something on this computer blocks the mining pool (unmineable.com).", status.Problem);
        Assert.Equal("pearlhash", provider.CurrentAlgorithm); // not failed over: Pearl isn't the problem
        Assert.Null(provider.SetAsideReason("pearlhash"));
    }

    /// <summary>unMineable's view of the address: one account, and whatever workers the test says.</summary>
    private sealed class CreditingUnmineable : IUnmineableApi
    {
        public List<UnmineableWorker> Workers { get; } = [];

        public Task<UnmineableAddress> GetAddressAsync(Otium.Core.Wallets.PayoutTarget target, CancellationToken ct) =>
            Task.FromResult(new UnmineableAddress("uuid-1", 0, 0, 1.5m, true, true, "TRX", [], null));

        public Task<UnmineableStats> GetStatsAsync(string uuid, CancellationToken ct) => throw new NotSupportedException();

        public Task<IReadOnlyList<UnmineableWorker>> GetWorkersAsync(string uuid, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<UnmineableWorker>>(Workers.ToList());

        public Task<IReadOnlyList<UnmineablePayment>> GetPaymentsAsync(string uuid, CancellationToken ct) => throw new NotSupportedException();

        public Task<UnmineableReferrals> GetReferralsAsync(string uuid, CancellationToken ct) => throw new NotSupportedException();
    }

    private static GpuPolicy UnmineablePearlPolicy() => new(
        DateTimeOffset.UtcNow,
        [
            new GpuAlgorithmPolicy("pearlhash", GpuTiers.Primary, "unmineable-pearl", 3.4e-14, 0.75),
            new GpuAlgorithmPolicy("autolykos2", GpuTiers.FreePowerOnly, GpuRoutes.MoneroOcean, 2.43e-12 * 545, 1),
        ],
        [
            new GpuPoolRoute("unmineable-pearl", GpuRoutes.Unmineable, "pearlpow.unmineable.com", 443, null, Tls: true),
            new GpuPoolRoute(GpuRoutes.MoneroOcean, GpuRoutes.MoneroOcean, "gulf.moneroocean.stream", 10128, null),
        ]);

    [Fact]
    public async Task Work_the_pool_doesnt_credit_is_noticed_and_the_card_moves_on()
    {
        var unmineable = new CreditingUnmineable();
        var provider = CreateProvider(() => UnmineablePearlPolicy(), pricePerKwh: 0, unmineable: unmineable);
        var options = new ProviderRunOptions(new MiningTarget(TestAddresses.Usdt(7), PayoutRole.User), "ot1234abcdg", 100);
        await provider.StartAsync(options, CancellationToken.None);
        await RunForProvider(provider, SrbMinerGpuProvider.BenchmarkDuration * 2 + TimeSpan.FromSeconds(10)); // both tested
        Assert.Equal("pearlhash", provider.CurrentAlgorithm);

        // Credited: the worker shows a calculated hashrate, so mining carries on past the check.
        unmineable.Workers.Add(new UnmineableWorker("pearlpow", "ot1234abcdg", true, 9.5e13, 1.04e14));
        var status = await RunForProvider(provider, SrbMinerGpuProvider.CreditGrace + TimeSpan.FromMinutes(1));
        Assert.Equal("pearlhash", provider.CurrentAlgorithm);
        Assert.Null(status.Problem);

        // Not credited (the worker is listed but nothing is counted): set aside, next coin, and the app says why.
        unmineable.Workers.Clear();
        unmineable.Workers.Add(new UnmineableWorker("pearlpow", "ot1234abcdg", true, 0, 1.04e14));
        status = await RunForProvider(provider, SrbMinerGpuProvider.CreditCheckEvery + TimeSpan.FromMinutes(1));
        Assert.Equal("autolykos2", provider.CurrentAlgorithm);
        Assert.NotNull(provider.SetAsideReason("pearlhash"));
        Assert.Contains("unMineable hasn't counted any of its work", provider.SetAsideReason("pearlhash"));
    }

    /// <summary>SRBMiner 3.7.6's own output from the real run (docs/real-mining-run.md) when kawpow's dataset didn't fit.</summary>
    private static readonly string[] RealDevFeeThenEpochFailure =
    [
        "[2026-10-16 15:01:32] HTTP API enabled on port 21987",
        "[2026-10-16 15:01:37] Hold on, doing the first devfee mining round[0]",
        "[2026-10-16 15:01:38] Connected to gulf.moneroocean.stream:10128 [0]",
        "[2026-10-16 15:02:23] Couldn't set epoch 611 on GPU0 [0]",
        "[2026-10-16 15:02:32] GPU0 RTX 3080: 0.00 H/s [T:64C FAN:52% P:108.4W EFF:- CC:1200 MC:5001 A:0 R:0 HW:0]",
        "[2026-10-16 15:02:32] Total: 0.00 H/s [P:108.4W EFF: A:0 R:0 HW:0]",
    ];

    private sealed class FakePool : IMoneroPoolApi
    {
        public Dictionary<string, double> Profits { get; } = new()
        {
            ["kawpow"] = 7.6e-12,
            ["autolykos2"] = 2.43e-12,
        };

        public Task<MoneroNetworkStats> GetNetworkStatsAsync(CancellationToken ct) => throw new NotSupportedException();

        public Task<PoolMinerStats?> GetMinerStatsAsync(string walletAddress, CancellationToken ct) => Task.FromResult<PoolMinerStats?>(null);

        public Task<AlgoProfits> GetAlgoProfitsAsync(CancellationToken ct) =>
            Task.FromResult(new AlgoProfits(new Dictionary<string, double>(Profits), 545, DateTimeOffset.UtcNow));

        public Task<IReadOnlyDictionary<string, IReadOnlyList<HashratePoint>>> GetWorkerHistoryAsync(string walletAddress, CancellationToken ct) =>
            throw new NotSupportedException();
    }

    private sealed class FakeMeter : IPowerMeter
    {
        public void CaptureBaseline()
        {
        }

        public PowerReading Read(int usagePercent) => new(200, true, "fake");
    }

    private sealed class FakeProcess(double hashrate) : IMinerProcess
    {
        public double Hashrate { get; set; } = hashrate;
        /// <summary>When set, decides the hashrate on each read instead (e.g. a miner that stops after a while).</summary>
        public Func<double>? Script { get; set; }
        public bool Exited { get; set; }
        public bool HasExited => Exited;
        public int? ExitCode => Exited ? 1 : null;
        public List<string> Log { get; } = ["not enough GPU memory"];
        public IReadOnlyList<string> RecentLog => Log.ToArray();
        public ShareCounts? Shares { get; set; }
        public bool Disposed { get; private set; }

        public Task<double?> ReadHashrateAsync(CancellationToken ct) => Task.FromResult<double?>(Script?.Invoke() ?? Hashrate);

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FakeFactory : IMinerProcessFactory
    {
        /// <summary>Hashrate the fake "card" reaches per algorithm; 0 means the miner crashes.</summary>
        public Dictionary<string, double> Speeds { get; } = new() { ["kawpow"] = 30e6, ["autolykos2"] = 200e6, ["pearlhash"] = 104e12 };
        /// <summary>Algorithms whose miner keeps running but never hashes (like kawpow on the real RTX 3080 run).</summary>
        public HashSet<string> Stalls { get; } = [];
        /// <summary>Lines every new miner prints first, e.g. SRBMiner's dev-fee round.</summary>
        public List<string> StartupLog { get; } = [];
        public Action<string, FakeProcess>? OnStart { get; set; }
        public List<(string Algorithm, string Password, FakeProcess Process, string Pool, string Wallet)> Launches { get; } = [];

        public IMinerProcess Start(string binary, IReadOnlyList<string> arguments, int apiPort, bool lowPriority)
        {
            string Arg(string flag) => arguments[arguments.ToList().IndexOf(flag) + 1];
            var algo = Arg("--algorithm");
            var speed = Speeds.GetValueOrDefault(algo);
            var stalls = Stalls.Contains(algo);
            var process = new FakeProcess(stalls ? 0 : speed) { Exited = speed == 0 && !stalls };
            process.Log.InsertRange(0, StartupLog);
            if (stalls)
                process.Log.AddRange(RealDevFeeThenEpochFailure);
            OnStart?.Invoke(algo, process);
            Launches.Add((algo, Arg("--password"), process, Arg("--pool"), Arg("--wallet")));
            return process;
        }
    }

    private async Task<ProviderStatus> RunFor(TimeSpan duration)
    {
        ProviderStatus status = ProviderStatus.Stopped(true);
        var end = _time.GetUtcNow() + duration;
        while (_time.GetUtcNow() < end)
        {
            _time.Advance(TimeSpan.FromSeconds(5));
            status = await _provider.GetStatusAsync(CancellationToken.None);
        }
        return status;
    }

    private static GpuPolicy Policy(string pearlTier = GpuTiers.Primary) => new(
        DateTimeOffset.UtcNow,
        [
            new GpuAlgorithmPolicy("pearlhash", pearlTier, "kryptex-prl", 3.4e-14, 3),
            new GpuAlgorithmPolicy("kawpow", GpuTiers.FreePowerOnly, GpuRoutes.MoneroOcean, 7.6e-12 * 545, 1),
            new GpuAlgorithmPolicy("autolykos2", GpuTiers.FreePowerOnly, GpuRoutes.MoneroOcean, 2.43e-12 * 545, 1),
        ],
        [
            new GpuPoolRoute(GpuRoutes.MoneroOcean, GpuRoutes.MoneroOcean, "gulf.moneroocean.stream", 10128, null),
            new GpuPoolRoute("kryptex-prl", GpuRoutes.Kryptex, "prl.kryptex.network", 7048, "otium"),
        ]);

    [Fact]
    public async Task Policy_sends_pearl_to_kryptex_and_skips_algorithms_that_lose_money()
    {
        var provider = CreateProvider(() => Policy(), pricePerKwh: 0.15);

        await provider.StartAsync(Options, CancellationToken.None);

        // Only Pearl is tested: the MoneroOcean algorithms are "free power only" and power costs $0.15/kWh.
        var launch = _factory.Launches.Single();
        Assert.Equal("pearlhash", launch.Algorithm);
        Assert.Equal("prl.kryptex.network:7048", launch.Pool);
        Assert.Equal("otium.sc1234abcdg", launch.Wallet);
        Assert.Contains("1 of 1", (await RunForProvider(provider, TimeSpan.FromSeconds(30))).Message);

        await RunForProvider(provider, SrbMinerGpuProvider.BenchmarkDuration);
        Assert.Equal("pearlhash", provider.CurrentAlgorithm);
        var estimate = await provider.EstimateRevenueAsync(104e12, CancellationToken.None);
        Assert.Equal(104e12 * 3.4e-14 * 0.98 * 0.97, estimate.UsdPerDay, precision: 6);
    }

    [Fact]
    public async Task The_kill_switch_stops_a_coin_at_the_next_check()
    {
        var policy = Policy();
        var provider = CreateProvider(() => policy, pricePerKwh: 0.15);
        await provider.StartAsync(Options, CancellationToken.None);
        await RunForProvider(provider, SrbMinerGpuProvider.BenchmarkDuration + TimeSpan.FromSeconds(10));
        Assert.Equal("pearlhash", provider.CurrentAlgorithm);

        policy = Policy(GpuTiers.Disabled);
        ProviderStatus? stopped = null;
        var deadline = _time.GetUtcNow() + SrbMinerGpuProvider.ReevaluateEvery + TimeSpan.FromSeconds(10);
        while (stopped is null && _time.GetUtcNow() < deadline)
        {
            _time.Advance(TimeSpan.FromSeconds(5));
            if (await provider.GetStatusAsync(CancellationToken.None) is { State: ProviderState.Error } s)
                stopped = s;
        }

        Assert.NotNull(stopped);
        Assert.Contains("pearlhash", stopped.Message);
        Assert.True(_factory.Launches[^1].Process.Disposed);
        var offer = await provider.GetExpectedOfferAsync(CancellationToken.None);
        Assert.Equal(0, offer!.GrossUsdPerDay); // the engine pauses instead of retrying a coin that's off
    }

    [Fact]
    public async Task Without_a_policy_only_moneroocean_runs_and_only_on_free_power()
    {
        var paid = CreateProvider(policy: null, pricePerKwh: 0.15);
        var offer = await paid.GetExpectedOfferAsync(CancellationToken.None);
        Assert.Equal(0, offer!.GrossUsdPerDay);
        await Assert.ThrowsAsync<ProviderProblemException>(() => paid.StartAsync(Options, CancellationToken.None)); // shown as is

        var free = CreateProvider(policy: null, pricePerKwh: 0);
        await free.StartAsync(Options, CancellationToken.None);
        Assert.Equal("kawpow", _factory.Launches[^1].Algorithm);
        Assert.DoesNotContain(_factory.Launches, l => l.Algorithm == "pearlhash"); // Kryptex coins need the policy's route
    }

    [Fact]
    public void Own_wallet_users_get_only_moneroocean_algorithms()
    {
        var own = Policy().WithoutOperatorPools();

        Assert.Null(own.For("pearlhash"));
        Assert.NotNull(own.For("kawpow"));
        Assert.Single(own.Routes);
    }

    /// <summary>Ticks until <paramref name="until"/> holds (returning that status) or <paramref name="max"/> passes.</summary>
    private async Task<ProviderStatus> RunUntil(Func<ProviderStatus, bool> until, TimeSpan max)
    {
        ProviderStatus status = ProviderStatus.Stopped(true);
        var end = _time.GetUtcNow() + max;
        while (_time.GetUtcNow() < end)
        {
            _time.Advance(TimeSpan.FromSeconds(5));
            status = await _provider.GetStatusAsync(CancellationToken.None);
            if (until(status))
                return status;
        }
        throw new Xunit.Sdk.XunitException($"Condition not reached in {max}; last status: {status}");
    }

    /// <summary>Benchmarks kawpow and autolykos2, then mines autolykos2 (the better payer on the fake card).</summary>
    private async Task MineAutolykosAfterBenchmarks()
    {
        await _provider.StartAsync(Options, CancellationToken.None);
        await RunFor(SrbMinerGpuProvider.BenchmarkDuration * 2 + TimeSpan.FromSeconds(10));
        Assert.Equal("autolykos2", _provider.CurrentAlgorithm);
        Assert.Equal(ProviderState.Running, (await RunFor(TimeSpan.FromMinutes(2))).State);
    }

    private void StallCurrentMiner(string algorithm)
    {
        var process = _factory.Launches[^1].Process;
        process.Hashrate = 0;
        process.Log.AddRange(RealDevFeeThenEpochFailure.Skip(3));
        _factory.Stalls.Add(algorithm);
    }

    [Fact]
    public async Task A_miner_stuck_at_zero_hashrate_is_failed_over_and_the_reason_shown()
    {
        await MineAutolykosAfterBenchmarks();
        StallCurrentMiner("autolykos2");
        var stuck = _factory.Launches[^1].Process;

        // Within the grace period it's just "starting"...
        var status = await RunFor(SrbMinerGpuProvider.StallTimeout - TimeSpan.FromSeconds(10));
        Assert.Equal(ProviderState.Starting, status.State);
        Assert.Equal("autolykos2", _provider.CurrentAlgorithm);

        // ...then it has failed: the next algorithm runs and the app says why, plainly.
        status = await RunUntil(s => s.Problem is not null, TimeSpan.FromSeconds(30));
        Assert.Equal(ProviderState.Starting, status.State);
        Assert.Equal("kawpow", _factory.Launches[^1].Algorithm);
        Assert.True(stuck.Disposed);
        Assert.StartsWith("Graphics card isn't mining autolykos2: the miner couldn't load this coin's dataset onto the graphics card (\"Couldn't set epoch 611 on GPU0\")", status.Problem);
        Assert.Contains("Trying kawpow instead", status.Problem);
        Assert.NotNull(_provider.SetAsideReason("autolykos2"));

        // kawpow hashes, so the notice clears; autolykos2 pays more but isn't retried while it's set aside.
        var autolykosLaunches = _factory.Launches.Count(l => l.Algorithm == "autolykos2");
        status = await RunFor(TimeSpan.FromMinutes(50));
        Assert.Equal(ProviderState.Running, status.State);
        Assert.Null(status.Problem);
        Assert.Contains("autolykos2 wasn't mining", status.Detail);
        Assert.Equal(autolykosLaunches, _factory.Launches.Count(l => l.Algorithm == "autolykos2"));

        // After the cool-down it gets another chance at the next re-check.
        await RunFor(SrbMinerGpuProvider.StallCooldown);
        Assert.Equal(autolykosLaunches + 1, _factory.Launches.Count(l => l.Algorithm == "autolykos2"));
    }

    [Fact]
    public async Task A_coin_whose_shares_the_pool_rejects_is_failed_over_even_though_the_card_hashes()
    {
        // After a fork (Pearl's PIP-3 drops consumer cards) the card keeps hashing, but the pool refuses its work.
        await MineAutolykosAfterBenchmarks();
        var rejected = _factory.Launches[^1].Process;

        rejected.Shares = new ShareCounts(Accepted: 3, Rejected: 4); // not yet: below the count and the 80% share
        var status = await RunFor(TimeSpan.FromSeconds(10));
        Assert.Equal("autolykos2", _provider.CurrentAlgorithm);
        Assert.Null(status.Problem);

        rejected.Shares = new ShareCounts(Accepted: 1, Rejected: 9);
        status = await RunUntil(s => s.Problem is not null, TimeSpan.FromSeconds(30));
        Assert.True(rejected.Disposed);
        Assert.Equal("kawpow", _factory.Launches[^1].Algorithm);
        Assert.StartsWith("Graphics card isn't mining autolykos2: the pool rejected 9 of its 10 shares", status.Problem);
        Assert.Contains("fork", status.Problem);
        Assert.NotNull(_provider.SetAsideReason("autolykos2"));
    }

    [Fact]
    public void Share_counts_are_read_from_the_api_and_the_console()
    {
        const string json = """
            {"algorithms":[{"name":"pearlhash","shares":{"total":12,"accepted":2,"rejected":10,"avg_find_time":9},
              "hashrate":{"gpu":{"gpu0":1.0e14,"total":1.0e14}}}]}
            """;
        Assert.Equal(new ShareCounts(2, 10), SrbMinerStats.ParseApiShares(json));
        Assert.Null(SrbMinerStats.ParseApiShares("""{"algorithms":null}"""));
        Assert.Equal(new ShareCounts(2, 1), SrbMinerStats.ParseConsoleShares(
            ["[2026-10-16 16:36:28] GPU0 RTX 3080: 360.53 MH/s [A:9 R:9 HW:0]", "[2026-10-16 16:36:28] Total: 360.53 MH/s [P:349.2W EFF: A:2 R:1 HW:0]"]));
        Assert.Null(SrbMinerStats.ParseConsoleShares(["nothing here"]));
    }

    [Fact]
    public async Task After_testing_it_mines_the_best_algorithm_not_the_last_one_tested()
    {
        // As on the Kryptex run: pearlhash (tested first) paid more than quantus (tested last), yet the 15-minute hold
        // for "the current algorithm" kept quantus. Here kawpow (first) beats autolykos2 (last).
        _factory.Speeds["kawpow"] = 100e6;
        await _provider.StartAsync(Options, CancellationToken.None);
        await RunFor(SrbMinerGpuProvider.BenchmarkDuration * 2 + TimeSpan.FromSeconds(10));

        Assert.Equal("kawpow", _provider.CurrentAlgorithm);
        Assert.Equal("kawpow", _factory.Launches[^1].Algorithm);
        Assert.Equal(3, _factory.Launches.Count); // two tests, then straight to the best
    }

    [Fact]
    public async Task A_miner_that_exits_while_mining_is_replaced_by_the_next_algorithm()
    {
        await MineAutolykosAfterBenchmarks();
        _factory.Launches[^1].Process.Exited = true;

        var status = await RunFor(TimeSpan.FromSeconds(5));
        Assert.Equal("kawpow", _factory.Launches[^1].Algorithm);
        Assert.StartsWith("Graphics card isn't mining autolykos2:", status.Problem);
    }

    [Fact]
    public async Task When_no_algorithm_mines_it_says_so_plainly_and_backs_off_instead_of_looping()
    {
        await MineAutolykosAfterBenchmarks();
        StallCurrentMiner("autolykos2");
        _factory.Stalls.Add("kawpow");

        var status = await RunUntil(s => s.State == ProviderState.Error, TimeSpan.FromMinutes(10));
        Assert.StartsWith("Graphics card isn't mining: the miner couldn't load this coin's dataset onto the graphics card", status.Problem);
        Assert.Contains("Otium tries again in", status.Problem);
        Assert.All(_factory.Launches, l => Assert.True(l.Process.Disposed));

        // The engine retries every minute; that mustn't relaunch a miner that just failed.
        var launches = _factory.Launches.Count;
        var refused = await Assert.ThrowsAsync<ProviderProblemException>(() => _provider.StartAsync(Options, CancellationToken.None));
        Assert.StartsWith("Graphics card isn't mining:", refused.Message);
        Assert.Equal(launches, _factory.Launches.Count);

        // After the cool-down both are tried again; failing again sets them aside for twice as long.
        _time.Advance(SrbMinerGpuProvider.StallCooldown + TimeSpan.FromMinutes(10));
        await _provider.StartAsync(Options, CancellationToken.None);
        Assert.True(_factory.Launches.Count > launches);
        status = await RunUntil(s => s.State == ProviderState.Error, TimeSpan.FromMinutes(10));
        Assert.Contains("Otium tries again in 2 hours", status.Problem);
    }

    [Fact]
    public async Task A_benchmark_taken_during_the_dev_fee_round_does_not_count()
    {
        // As on the real RTX 3080: SRBMiner first hashes for its developer, then can't build kawpow's dataset for the
        // user's pool, while its API still shows the old speed for a while. The old check took 3 samples and passed.
        _factory.OnStart = (algorithm, process) =>
        {
            if (algorithm != "kawpow")
                return;
            process.Log.Clear();
            process.Log.AddRange(RealDevFeeThenEpochFailure);
            var launched = _time.GetUtcNow();
            process.Script = () => _time.GetUtcNow() - launched < TimeSpan.FromSeconds(100) ? 33.5e6 : 0;
        };

        await _provider.StartAsync(Options, CancellationToken.None);
        await RunFor(SrbMinerGpuProvider.BenchmarkDuration + TimeSpan.FromSeconds(5));

        var kawpow = _provider.Benchmarks.Single(b => b.Algorithm == "kawpow");
        Assert.False(kawpow.Succeeded);
        Assert.StartsWith("the miner couldn't load this coin's dataset", kawpow.Failure);
        Assert.Equal("autolykos2", _factory.Launches[^1].Algorithm);
    }

    [Fact]
    public async Task Hashes_during_the_dev_fee_round_are_not_reported_as_the_users()
    {
        _factory.StartupLog.Add("[2026-10-16 15:01:37] Hold on, doing the first devfee mining round[0]");
        await _provider.StartAsync(Options, CancellationToken.None);

        var status = await RunFor(TimeSpan.FromSeconds(30));
        Assert.Equal(ProviderState.Starting, status.State);
        Assert.Equal(0, status.HashrateHps);
        status = await RunFor(TimeSpan.FromSeconds(40));
        Assert.Equal(30e6, status.HashrateHps);

        // A card that keeps hashing after the round still passes its benchmark.
        await RunUntil(s => !s.IsCalibrating, SrbMinerGpuProvider.BenchmarkDuration * 2);
        Assert.All(_provider.Benchmarks, b => Assert.True(b.Succeeded));
        status = await RunFor(TimeSpan.FromSeconds(10));
        Assert.Equal(0, status.HashrateHps);
        Assert.Contains("fee round", status.Message);
    }

    [Fact]
    public async Task Too_little_free_graphics_memory_skips_an_algorithm_and_says_why()
    {
        double freeGb = 5; // e.g. a game holds 7 GB of the 12 GB card
        var provider = CreateProvider(policy: null, pricePerKwh: 0, freeMemoryGb: _ => freeGb);

        await provider.StartAsync(Options, CancellationToken.None);
        Assert.Equal("autolykos2", _factory.Launches.Single().Algorithm); // kawpow needs about 6 GB
        Assert.Contains($"{5.0:0.0} GB of 12 GB free, kawpow needs about 6 GB", provider.SetAsideReason("kawpow")); // the user's number format
        await RunForProvider(provider, SrbMinerGpuProvider.BenchmarkDuration + TimeSpan.FromSeconds(10));
        Assert.DoesNotContain(provider.Benchmarks, b => b.Algorithm == "kawpow"); // not a failure: tested once memory frees up
        Assert.Equal("autolykos2", provider.CurrentAlgorithm);

        await provider.StopAsync(CancellationToken.None);
        freeGb = 1;
        var refused = await Assert.ThrowsAsync<ProviderProblemException>(() => provider.StartAsync(Options, CancellationToken.None));
        Assert.StartsWith($"Graphics card isn't mining: other apps are using most of the graphics card's memory ({1.0:0.0} GB of 12 GB free", refused.Message);

        freeGb = 11;
        _time.Advance(SrbMinerGpuProvider.LowMemoryCooldown);
        await provider.StartAsync(Options, CancellationToken.None);
        Assert.Equal("kawpow", _factory.Launches[^1].Algorithm);
        Assert.True((await provider.GetStatusAsync(CancellationToken.None)).IsCalibrating);
    }

    [Fact]
    public async Task Before_the_servers_prices_arrive_the_card_says_its_waiting_not_that_power_costs_too_much()
    {
        // Otium starts its miners before its first call to the server; unMineable's coins are priced only by the server.
        var provider = CreateProvider(policy: () => GpuPolicy.NotYetFetched.OnlyRoutes(GpuRoutes.Unmineable), pricePerKwh: 0);
        var refused = await Assert.ThrowsAsync<ProviderProblemException>(() => provider.StartAsync(Options, CancellationToken.None));
        Assert.Equal("Graphics card is waiting for today's coin prices from Otium's server; it starts as soon as they arrive.", refused.Message);
        Assert.Empty(_factory.Launches);
    }

    [Fact]
    public void The_last_policy_is_kept_on_disk_and_used_at_startup_for_a_day()
    {
        var policy = new GpuPolicy(DateTimeOffset.UnixEpoch,
            [new GpuAlgorithmPolicy("pearlhash", GpuTiers.Primary, "unmineable-pearlhash", 3.36e-14, 0.75, null)],
            [new GpuPoolRoute("unmineable-pearlhash", GpuRoutes.Unmineable, "pearlpow.unmineable.com", 443, null, true)]);
        var fetched = new DateTimeOffset(2026, 10, 19, 19, 44, 0, TimeSpan.Zero);
        var store = new Otium.Core.Settings.JsonFileStore<CachedGpuPolicy>(Path.Combine(_dir, "gpu-policy.json"));
        Assert.Null(store.Load().UsableAt(fetched)); // nothing kept yet

        store.Save(new CachedGpuPolicy { Policy = policy, FetchedAt = fetched });
        var kept = store.Load();
        var usable = kept.UsableAt(fetched + TimeSpan.FromHours(12))!;
        Assert.False(usable.IsNotYetFetched);
        Assert.Equal(3.36e-14, usable.For("pearlhash")!.UsdPerHashPerSecondDay);
        Assert.Equal("pearlpow.unmineable.com", usable.Route("unmineable-pearlhash")!.Host);
        Assert.Null(kept.UsableAt(fetched + CachedGpuPolicy.MaxAge + TimeSpan.FromMinutes(1))); // too old: wait for the server
    }

    private async Task<ProviderStatus> RunForProvider(SrbMinerGpuProvider provider, TimeSpan duration)
    {
        ProviderStatus status = ProviderStatus.Stopped(true);
        var end = _time.GetUtcNow() + duration;
        while (_time.GetUtcNow() < end)
        {
            _time.Advance(TimeSpan.FromSeconds(5));
            status = await provider.GetStatusAsync(CancellationToken.None);
        }
        return status;
    }

    [Fact]
    public async Task Benchmarks_every_paying_algorithm_then_mines_the_most_profitable()
    {
        Assert.Null(await _provider.GetExpectedOfferAsync(CancellationToken.None)); // nothing measured yet

        await _provider.StartAsync(Options, CancellationToken.None);
        Assert.Equal("kawpow", _factory.Launches[0].Algorithm);
        Assert.Equal("sc1234abcdg~kawpow", _factory.Launches[0].Password);

        var status = await RunFor(TimeSpan.FromSeconds(30));
        Assert.True(status.IsCalibrating);
        Assert.Contains("1 of 2", status.Message);

        await RunFor(SrbMinerGpuProvider.BenchmarkDuration);
        Assert.Equal("autolykos2", _factory.Launches[^1].Algorithm); // second benchmark
        Assert.True(_factory.Launches[0].Process.Disposed);

        status = await RunFor(SrbMinerGpuProvider.BenchmarkDuration + TimeSpan.FromSeconds(10));
        Assert.False(status.IsCalibrating);

        // 200 MH/s * 2.43e-12 beats 30 MH/s * 7.6e-12, so autolykos2 wins and keeps running.
        Assert.Equal("autolykos2", _provider.CurrentAlgorithm);
        Assert.Equal(2, _provider.Benchmarks.Count);
        Assert.Equal(200e6, _provider.Benchmarks.Single(b => b.Algorithm == "autolykos2").HashrateHps);
        Assert.Equal(200, _provider.Benchmarks.Single(b => b.Algorithm == "autolykos2").ExtraWatts);

        var offer = await _provider.GetExpectedOfferAsync(CancellationToken.None);
        Assert.NotNull(offer);
        Assert.Equal("autolykos2", offer.Description);
        Assert.Equal(200, offer.ExtraWatts);

        var estimate = await _provider.EstimateRevenueAsync(200e6, CancellationToken.None);
        Assert.Equal(200e6 * 2.43e-12 * 0.99 * 0.99 * 545, estimate.UsdPerDay, precision: 9);
    }

    [Fact]
    public async Task An_algorithm_that_crashes_is_recorded_and_skipped()
    {
        _factory.Speeds["kawpow"] = 0; // e.g. out of memory
        await _provider.StartAsync(Options, CancellationToken.None);
        await RunFor(TimeSpan.FromSeconds(10));

        var kawpow = _provider.Benchmarks.Single(b => b.Algorithm == "kawpow");
        Assert.False(kawpow.Succeeded);
        Assert.Contains("not enough GPU memory", kawpow.Failure);
        Assert.Equal("autolykos2", _factory.Launches[^1].Algorithm);
    }

    [Fact]
    public async Task Switches_when_payouts_change_but_not_before_the_hold_time()
    {
        await _provider.StartAsync(Options, CancellationToken.None);
        await RunFor(SrbMinerGpuProvider.BenchmarkDuration * 2 + TimeSpan.FromSeconds(10));
        Assert.Equal("autolykos2", _provider.CurrentAlgorithm);
        var launches = _factory.Launches.Count;

        // Kawpow suddenly pays 4x more.
        _pool.Profits["kawpow"] = 30e-12;
        await RunFor(TimeSpan.FromMinutes(5));
        Assert.Equal(launches, _factory.Launches.Count); // not re-evaluated yet

        var status = await RunFor(SrbMinerGpuProvider.ReevaluateEvery + TimeSpan.FromMinutes(5));
        Assert.Equal("kawpow", _provider.CurrentAlgorithm);
        Assert.Equal("kawpow", _factory.Launches[^1].Algorithm);
        Assert.Contains("switched from autolykos2", status.Detail);
    }

    [Fact]
    public async Task Restarting_for_a_fee_slice_keeps_the_algorithm_and_the_benchmarks()
    {
        await _provider.StartAsync(Options, CancellationToken.None);
        await RunFor(SrbMinerGpuProvider.BenchmarkDuration * 2 + TimeSpan.FromSeconds(10));
        await _provider.StopAsync(CancellationToken.None);

        var feeSlice = Options with { Target = new MiningTarget(TestAddresses.Generate(), PayoutRole.App) };
        await _provider.StartAsync(feeSlice, CancellationToken.None);
        Assert.Equal("autolykos2", _factory.Launches[^1].Algorithm);
        Assert.False((await _provider.GetStatusAsync(CancellationToken.None)).IsCalibrating);
    }

    [Fact]
    public async Task Benchmarks_survive_an_app_restart_and_can_be_reset()
    {
        await _provider.StartAsync(Options, CancellationToken.None);
        await RunFor(SrbMinerGpuProvider.BenchmarkDuration * 2 + TimeSpan.FromSeconds(10));
        await _provider.StopAsync(CancellationToken.None);

        var reopened = new SrbMinerGpuProvider(
            new HttpClient(), _pool, Path.Combine(_dir, "engines"), Path.Combine(_dir, "bench.json"),
            [Rtx3080], _meter, () => 0.15, _factory, _time, asset: SrbMinerRelease.For(OSPlatform.Windows, Architecture.X64));
        Assert.Equal(2, reopened.Benchmarks.Count);

        // A different card invalidates them.
        var otherCard = new SrbMinerGpuProvider(
            new HttpClient(), _pool, Path.Combine(_dir, "engines"), Path.Combine(_dir, "bench.json"),
            [new GpuInfo(GpuVendor.Nvidia, 0, "NVIDIA GeForce RTX 4090", 24)], _meter, () => 0.15, _factory, _time,
            asset: SrbMinerRelease.For(OSPlatform.Windows, Architecture.X64));
        Assert.Empty(otherCard.Benchmarks);

        reopened.ResetBenchmarks();
        Assert.Empty(reopened.Benchmarks);
    }

    [Fact]
    public void Unsupported_without_a_mining_capable_gpu()
    {
        var intelOnly = new SrbMinerGpuProvider(
            new HttpClient(), _pool, Path.Combine(_dir, "engines"), Path.Combine(_dir, "bench2.json"),
            [new GpuInfo(GpuVendor.Intel, 0, "Intel Arc", 8)], _meter, () => 0.15, _factory, _time,
            asset: SrbMinerRelease.For(OSPlatform.Windows, Architecture.X64));
        Assert.False(intelOnly.IsSupportedOnThisPlatform);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_dir, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    [Fact]
    public void A_failed_coin_test_is_tried_again_later_and_a_success_stands()
    {
        // On the RTX 3080 the first Pearl test failed ("no stable hashrate") while the same miner did 108 TH/s by hand
        // later: a failure can pass, so it mustn't rule the coin out for good.
        var time = new ManualTime(new DateTimeOffset(2026, 10, 19, 10, 31, 0, TimeSpan.Zero));
        var path = Path.Combine(Path.GetTempPath(), $"otium-bench-{Guid.NewGuid():N}.json");
        try
        {
            var store = new BenchmarkStore(path, [new GpuInfo(GpuVendor.Nvidia, 0, "RTX 3080", 12)], time);
            store.Record(new BenchmarkResult("pearlhash", 0, null, time.GetUtcNow(), "no stable hashrate"));
            store.Record(new BenchmarkResult("xelishash", 9e3, 229, time.GetUtcNow()));
            Assert.True(store.Has("pearlhash"));

            time.Advance(TimeSpan.FromHours(6));
            Assert.False(store.Has("pearlhash")); // due for another try
            Assert.True(store.Has("xelishash")); // a success stands

            store.Record(new BenchmarkResult("pearlhash", 0, null, time.GetUtcNow(), "no stable hashrate"));
            time.Advance(TimeSpan.FromHours(7));
            Assert.True(store.Has("pearlhash")); // failed twice: waits 12 hours
            time.Advance(TimeSpan.FromHours(5));
            Assert.False(store.Has("pearlhash"));

            store.Record(new BenchmarkResult("pearlhash", 1.08e14, 343, time.GetUtcNow()));
            time.Advance(TimeSpan.FromDays(30));
            Assert.True(store.Has("pearlhash"));
            Assert.Equal(0, store.Results.Single(r => r.Algorithm == "pearlhash").FailedTimes);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
