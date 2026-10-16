using Otium.Core.Gpu;
using Otium.Core.Pools;
using Otium.Core.Power;
using Otium.Core.Providers;
using Otium.Core.Switching;
using Otium.Core.Wallets;

namespace Otium.Providers.SrbMiner;

/// <summary>
/// GPU mining with SRBMiner-Multi, following the Otium server's GPU policy: which algorithms to mine, on which pool
/// (MoneroOcean, which converts to XMR, or Kryptex Pool for coins like Pearl and Quantus) and what each pays now.
/// Without a policy (server unreachable) it falls back to MoneroOcean's own per-algorithm payouts.
///
/// On first run each usable algorithm is benchmarked for real (the hashes still count for the user). After that,
/// it mines whichever algorithm pays the most after miner and pool fees and measured electricity, and re-checks every
/// <see cref="ReevaluateEvery"/>. Algorithms that usually lose money run only when electricity is free, and anything
/// the server disables (the kill switch) is dropped at the next re-check.
/// </summary>
public sealed class SrbMinerGpuProvider : IComputeProvider
{
    public static readonly TimeSpan BenchmarkWarmup = TimeSpan.FromSeconds(60);
    public static readonly TimeSpan BenchmarkDuration = TimeSpan.FromSeconds(150);
    public static readonly TimeSpan ReevaluateEvery = TimeSpan.FromMinutes(10);
    public const int MinimumSamples = 3;

    private readonly IMoneroPoolApi _pool;
    private readonly IReadOnlyList<GpuInfo> _gpus;
    private readonly IPowerMeter _meter;
    private readonly Func<double> _pricePerKwh;
    private readonly IMinerProcessFactory _factory;
    private readonly TimeProvider _time;
    private readonly GpuPoolEndpoint _endpoint;
    private readonly ProfitSwitcher _switcher;
    private readonly BenchmarkStore _store;
    private readonly EngineInstaller _installer;
    private readonly SrbMinerAsset? _asset;
    private readonly Func<GpuPolicy?> _policy;
    private readonly List<(double Hashrate, double? Watts)> _samples = [];

    private IMinerProcess? _process;
    private ProviderRunOptions? _options;
    private GpuAlgorithm? _current;
    private DateTimeOffset _currentSince;
    private DateTimeOffset _launchedAt;
    private DateTimeOffset _lastEvaluation;
    private bool _benchmarking;
    private int _benchmarkIndex;
    private int _benchmarkTotal;
    private string? _switchNote;

    public SrbMinerGpuProvider(
        HttpClient http,
        IMoneroPoolApi pool,
        string enginesRoot,
        string benchmarkPath,
        IReadOnlyList<GpuInfo> gpus,
        IPowerMeter meter,
        Func<double> pricePerKwh,
        IMinerProcessFactory? factory = null,
        TimeProvider? time = null,
        GpuPoolEndpoint? endpoint = null,
        ProfitSwitcher? switcher = null,
        SrbMinerAsset? asset = null,
        Func<GpuPolicy?>? policy = null)
    {
        _policy = policy ?? (() => null);
        _pool = pool;
        _gpus = gpus.Where(g => g.Vendor is GpuVendor.Nvidia or GpuVendor.Amd).ToList();
        _meter = meter;
        _pricePerKwh = pricePerKwh;
        _factory = factory ?? new SrbMinerProcessFactory(http);
        _time = time ?? TimeProvider.System;
        _endpoint = endpoint ?? GpuPoolEndpoint.MoneroOcean;
        _switcher = switcher ?? new ProfitSwitcher();
        _store = new BenchmarkStore(benchmarkPath, _gpus);
        _asset = asset ?? SrbMinerRelease.ForCurrentPlatform();
        _installer = new EngineInstaller(http, Path.Combine(enginesRoot, $"srbminer-{SrbMinerRelease.Version}"), SrbMinerRelease.BinaryName);
    }

    public string Id => "srbminer-gpu";

    public string DisplayName => "GPU mining (best paying coin)";

    public DeviceKind Device => DeviceKind.Gpu;

    public string PayoutCurrency => "XMR";

    public bool IsSupportedOnThisPlatform => _asset is not null && _gpus.Count > 0;

    public bool IsInstalled => _installer.IsInstalled;

    public string EngineDirectory => _installer.EngineDirectory;

    public IReadOnlyList<GpuInfo> Gpus => _gpus;

    public IReadOnlyList<BenchmarkResult> Benchmarks => _store.Results;

    public string? CurrentAlgorithm => _current?.PoolName;

    public IReadOnlyList<string> RecentLog => _process?.RecentLog ?? [];

    public string? ValidatePayoutAddress(string address) =>
        MoneroAddress.TryValidate(address, out _, out var error) ? null : error;

    public Task InstallAsync(IProgress<InstallProgress>? progress, CancellationToken ct)
    {
        if (_asset is null)
            throw new PlatformNotSupportedException("SRBMiner has no build for this operating system.");
        return _installer.InstallAsync($"SRBMiner {SrbMinerRelease.Version}", _asset.DownloadUrl, _asset.FileName, _asset.Sha256, progress, ct);
    }

    /// <summary>Forget all measurements so every algorithm is tested again on the next start.</summary>
    public void ResetBenchmarks() => _store.Clear();

    public async Task StartAsync(ProviderRunOptions options, CancellationToken ct)
    {
        _options = options;
        var eligible = await EligibleAsync(ct);
        if (eligible.Count == 0)
            throw new InvalidOperationException("No coin your graphics card can mine pays more than its electricity right now.");

        var pending = eligible.Where(a => !_store.Has(a.PoolName)).ToList();
        if (pending.Count > 0)
        {
            _benchmarking = true;
            _benchmarkTotal = eligible.Count;
            _benchmarkIndex = eligible.Count - pending.Count;
            await LaunchAsync(pending[0], ct);
            return;
        }

        _benchmarking = false;
        var choice = await ChooseAsync(ct)
                     ?? throw new InvalidOperationException("None of the GPU algorithms worked on your graphics card. Use \"Test GPU again\" in Settings to retry.");
        await LaunchAsync(choice, ct);
    }

    public async Task StopAsync(CancellationToken ct)
    {
        // Keep _current and _currentSince: after a fee-slice restart the same algorithm continues without a re-pick.
        await KillAsync();
    }

    public async Task<ProviderStatus> GetStatusAsync(CancellationToken ct)
    {
        if (_process is null || _current is null)
            return ProviderStatus.Stopped(IsInstalled);

        var now = _time.GetUtcNow();
        if (_process.HasExited)
        {
            var reason = $"exited with code {_process.ExitCode}: {string.Join(" ", _process.RecentLog.TakeLast(2))}".Trim();
            if (!_benchmarking)
                return new ProviderStatus(ProviderState.Error, 0, $"The GPU miner {reason}");

            _store.Record(new BenchmarkResult(_current.PoolName, 0, null, now, Failure: reason));
            return await AdvanceBenchmarkAsync(ct);
        }

        var hashrate = await _process.ReadHashrateAsync(ct) ?? 0;

        if (_benchmarking)
        {
            var elapsed = now - _launchedAt;
            if (elapsed >= BenchmarkWarmup && hashrate > 0)
            {
                var power = _meter.Read(100);
                _samples.Add((hashrate, power.IsMeasured ? power.ExtraWatts : null));
            }

            if (elapsed >= BenchmarkDuration)
            {
                _store.Record(_samples.Count >= MinimumSamples
                    ? new BenchmarkResult(_current.PoolName, Median(_samples.Select(s => s.Hashrate)), AverageWatts(), now)
                    : new BenchmarkResult(_current.PoolName, 0, null, now, Failure: "no stable hashrate"));
                return await AdvanceBenchmarkAsync(ct);
            }

            var testing = $"Testing {_current.PoolName} on your graphics card ({_benchmarkIndex + 1} of {_benchmarkTotal})";
            return new ProviderStatus(hashrate > 0 ? ProviderState.Running : ProviderState.Starting, hashrate, testing + "…", testing, IsCalibrating: true);
        }

        if (now - _lastEvaluation >= ReevaluateEvery)
        {
            _lastEvaluation = now;
            try
            {
                var choice = await ChooseAsync(ct);
                if (choice is null)
                {
                    // Everything usable was switched off (e.g. the server disabled this coin): stop instead of mining on.
                    var stopped = _current.PoolName;
                    await KillAsync();
                    return new ProviderStatus(ProviderState.Error, 0, $"Stopped mining {stopped}: it isn't paying right now. Otium checks again shortly.");
                }
                if (choice != _current)
                {
                    var from = _current.PoolName;
                    await LaunchAsync(choice, ct);
                    _switchNote = $"switched from {from}";
                    return new ProviderStatus(ProviderState.Starting, 0, $"Switching to {choice.PoolName}, which pays more right now…", choice.PoolName);
                }
            }
            catch (HttpRequestException)
            {
                // Pool stats unavailable: keep mining the current algorithm and try again next time.
            }
        }

        var detail = _switchNote is null ? _current.PoolName : $"{_current.PoolName} ({_switchNote})";
        return new ProviderStatus(
            hashrate > 0 ? ProviderState.Running : ProviderState.Starting,
            hashrate,
            hashrate > 0 ? null : $"Starting {_current.PoolName} (preparing the GPU can take a minute)…",
            detail);
    }

    /// <summary>USD per day for the current algorithm (rates are in USD, so "coin" here is USD).</summary>
    public async Task<RevenueEstimate> EstimateRevenueAsync(double hashrateHps, CancellationToken ct)
    {
        if (_current is null || hashrateHps <= 0)
            return new RevenueEstimate(0, 0, "USD");

        var quote = (await QuotesAsync(ct)).FirstOrDefault(q => q.Algorithm == _current).Quote;
        if (quote is null)
            return new RevenueEstimate(0, 0, "USD");
        var usd = hashrateHps * quote.RevenuePerHashPerSecondDay
                  * (1 - quote.DevFeePercent / 100) * (1 - (quote.PoolFeePercent ?? _switcher.PoolFeePercent) / 100);
        return new RevenueEstimate(usd, usd, "USD");
    }

    public async Task<ProfitOffer?> GetExpectedOfferAsync(CancellationToken ct)
    {
        var eligible = await EligibleAsync(ct);
        if (eligible.Count == 0)
            return new ProfitOffer(0, null, "No coin pays right now"); // lets the engine pause instead of failing to start
        if (eligible.Any(a => !_store.Has(a.PoolName)))
            return null; // still needs benchmarking: run to find out

        var best = (await ScoreAsync(ct)).FirstOrDefault();
        return best is null ? null : new ProfitOffer(best.GrossUsdPerDay, best.ExtraWatts, best.Algorithm);
    }

    /// <summary>Every measured algorithm with today's payout, best first. Shown in Settings.</summary>
    public async Task<IReadOnlyList<ScoredOption>> ScoreAsync(CancellationToken ct)
    {
        var quotes = (await QuotesAsync(ct)).Select(q => q.Quote);
        var fallbackWatts = _meter.Read(100) is { IsMeasured: false } estimate ? estimate.ExtraWatts : GpuPowerMeter.DefaultEstimatedWatts * _gpus.Count;
        return _switcher.Score(_store.Results, quotes, 1.0, _pricePerKwh(), fallbackWatts);
    }

    /// <summary>
    /// What each usable algorithm pays now, in USD per H/s per day: from the server's policy, or straight from
    /// MoneroOcean when there is none. "Usable" means primary, or free-power-only while electricity costs nothing.
    /// </summary>
    internal async Task<IReadOnlyList<(GpuAlgorithm Algorithm, AlgorithmQuote Quote)>> QuotesAsync(CancellationToken ct)
    {
        var freePower = _pricePerKwh() <= 0;
        var supported = GpuAlgorithms.All.Where(a => a.Supports(_gpus));

        if (_policy() is { } policy)
        {
            var list = new List<(GpuAlgorithm, AlgorithmQuote)>();
            foreach (var a in supported)
            {
                if (policy.For(a.PoolName) is { UsdPerHashPerSecondDay: > 0 } p && Usable(p.Tier, freePower) && policy.Route(p.Route) is not null)
                    list.Add((a, new AlgorithmQuote(a.PoolName, p.UsdPerHashPerSecondDay, a.DevFeePercent, p.PoolFeePercent)));
            }
            return list;
        }

        var profits = await _pool.GetAlgoProfitsAsync(ct);
        return supported
            .Where(a => a.DefaultRoute == GpuRoutes.MoneroOcean && Usable(a.DefaultTier, freePower) && profits.For(a.PoolName) > 0)
            .Select(a => (a, new AlgorithmQuote(a.PoolName, profits.For(a.PoolName) * profits.XmrPriceUsd, a.DevFeePercent)))
            .ToList();
    }

    private static bool Usable(string tier, bool freePower) =>
        tier == GpuTiers.Primary || (tier == GpuTiers.FreePowerOnly && freePower);

    public async Task<PayoutBalance?> GetPayoutBalanceAsync(string walletAddress, CancellationToken ct)
    {
        var stats = await _pool.GetMinerStatsAsync(walletAddress, ct);
        return stats is null
            ? null
            : new PayoutBalance(stats.PendingXmr, stats.PaidXmr, PayoutCurrency, MoneroOceanClient.MinimumPayoutXmr, MoneroOceanClient.DashboardUrl(walletAddress));
    }

    public async ValueTask DisposeAsync() => await KillAsync();

    private async Task<IReadOnlyList<GpuAlgorithm>> EligibleAsync(CancellationToken ct) =>
        (await QuotesAsync(ct)).Select(q => q.Algorithm).ToList();

    private async Task<GpuAlgorithm?> ChooseAsync(CancellationToken ct)
    {
        var scored = await ScoreAsync(ct);
        var name = _switcher.Choose(scored, _current?.PoolName, _time.GetUtcNow() - _currentSince);
        return name is null ? null : GpuAlgorithms.Find(name);
    }

    private async Task<ProviderStatus> AdvanceBenchmarkAsync(CancellationToken ct)
    {
        var eligible = await EligibleAsync(ct);
        var pending = eligible.Where(a => !_store.Has(a.PoolName)).ToList();
        if (pending.Count > 0)
        {
            _benchmarkTotal = eligible.Count;
            _benchmarkIndex = eligible.Count - pending.Count;
            await LaunchAsync(pending[0], ct);
            var next = $"Testing {pending[0].PoolName} on your graphics card ({_benchmarkIndex + 1} of {_benchmarkTotal})";
            return new ProviderStatus(ProviderState.Starting, 0, next + "…", next, IsCalibrating: true);
        }

        _benchmarking = false;
        var choice = await ChooseAsync(ct);
        if (choice is null)
        {
            await KillAsync();
            return new ProviderStatus(ProviderState.Error, 0, "None of the GPU algorithms worked on your graphics card. Use \"Test GPU again\" in Settings to retry.");
        }

        await LaunchAsync(choice, ct);
        return new ProviderStatus(ProviderState.Starting, 0, $"Testing done. Mining {choice.PoolName}, the best option for your card…", choice.PoolName);
    }

    private async Task LaunchAsync(GpuAlgorithm algorithm, CancellationToken ct)
    {
        await KillAsync();
        _meter.CaptureBaseline();

        var options = _options ?? throw new InvalidOperationException("StartAsync must be called first.");
        var binary = _installer.FindBinary()
                     ?? throw new EngineBlockedException("The GPU miner is missing. Antivirus software may have removed it.", _installer.EngineDirectory);

        var now = _time.GetUtcNow();
        if (_current != algorithm)
        {
            _current = algorithm;
            _currentSince = now;
        }
        _launchedAt = now;
        _lastEvaluation = now;
        _samples.Clear();

        var apiPort = SrbMinerProcessFactory.FreeLoopbackPort();
        var args = SrbMinerArgs.Build(algorithm, LoginFor(algorithm, options), apiPort, _gpus);
        ct.ThrowIfCancellationRequested();
        _process = _factory.Start(binary, args, apiPort);
    }

    /// <summary>Kryptex coins log in to the operator's account as "account.worker"; MoneroOcean uses the pool wallet.</summary>
    private PoolLogin LoginFor(GpuAlgorithm algorithm, ProviderRunOptions options)
    {
        var policy = _policy();
        var route = policy?.For(algorithm.PoolName) is { } p ? policy.Route(p.Route) : null;
        if (route is { Kind: GpuRoutes.Kryptex, User.Length: > 0 })
            return PoolLogin.Kryptex(new GpuPoolEndpoint(route.Host, route.Port), route.User!, options.WorkerName);
        if (algorithm.DefaultRoute != GpuRoutes.MoneroOcean)
            throw new InvalidOperationException($"{algorithm.PoolName} needs Otium's pool settings, which aren't available right now.");
        return PoolLogin.MoneroOcean(_endpoint, options.Target.WalletAddress, options.WorkerName, algorithm);
    }

    private async Task KillAsync()
    {
        var process = _process;
        _process = null;
        if (process is not null)
            await process.DisposeAsync();
    }

    private double? AverageWatts()
    {
        var measured = _samples.Where(s => s.Watts is not null).Select(s => s.Watts!.Value).ToList();
        return measured.Count > 0 ? measured.Average() : null;
    }

    private static double Median(IEnumerable<double> values)
    {
        var sorted = values.Order().ToArray();
        var mid = sorted.Length / 2;
        return sorted.Length % 2 == 1 ? sorted[mid] : (sorted[mid - 1] + sorted[mid]) / 2;
    }
}
