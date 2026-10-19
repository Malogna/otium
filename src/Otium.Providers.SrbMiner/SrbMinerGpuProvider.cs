using Otium.Core.Gpu;
using Otium.Core.Pools;
using Otium.Core.Power;
using Otium.Core.Providers;
using Otium.Core.Switching;
using Otium.Core.Wallets;

namespace Otium.Providers.SrbMiner;

/// <summary>
/// GPU mining with SRBMiner-Multi, following the Otium server's GPU policy: which algorithms to mine, on which pool
/// (MoneroOcean, which converts to XMR; unMineable, which pays the user's chosen coin; or Kryptex Pool for balance
/// accounts) and what each pays now.
/// Without a policy (server unreachable) it falls back to MoneroOcean's own per-algorithm payouts.
///
/// On first run each usable algorithm is benchmarked for real (the hashes still count for the user). After that,
/// it mines whichever algorithm pays the most after miner and pool fees and measured electricity, and re-checks every
/// <see cref="ReevaluateEvery"/>. Algorithms that usually lose money run only when electricity is free, and anything
/// the server disables (the kill switch) is dropped at the next re-check.
///
/// A miner that runs but doesn't hash for <see cref="StallTimeout"/> (or exits) has failed: that algorithm is set
/// aside for a while (longer each time it fails again) and the next best one is tried, so a card that can't mine a
/// coin, for example because another app holds its memory, doesn't sit at "Starting…" or retry the same coin in a loop.
/// So has one whose shares the pool mostly rejects (<see cref="RejectedSharesToFail"/>): after a coin's fork (Pearl's
/// PIP-3 drops consumer cards) the card hashes fine, but none of its work counts.
/// </summary>
public sealed class SrbMinerGpuProvider : IComputeProvider
{
    public static readonly TimeSpan BenchmarkWarmup = TimeSpan.FromSeconds(60);
    public static readonly TimeSpan BenchmarkDuration = TimeSpan.FromSeconds(150);
    public static readonly TimeSpan ReevaluateEvery = TimeSpan.FromMinutes(10);
    public const int MinimumSamples = 3;

    /// <summary>A benchmark only counts if the card is still hashing this close to its end.</summary>
    public static readonly TimeSpan StillHashingWithin = TimeSpan.FromSeconds(30);

    /// <summary>
    /// How long SRBMiner's first dev-fee round is treated as running after it's announced (about 45 s in 3.7.6). Its
    /// hashes go to the developer's pool, so they're neither earnings nor proof the card works for the user's pool.
    /// </summary>
    public static readonly TimeSpan DevFeeRound = TimeSpan.FromSeconds(60);

    /// <summary>A running miner with no hashrate for this long (after launch or since it last hashed) has failed.</summary>
    public static readonly TimeSpan StallTimeout = TimeSpan.FromMinutes(3);

    /// <summary>How long a failed algorithm is set aside; doubles with each failure in a row, up to <see cref="MaxStallCooldown"/>.</summary>
    public static readonly TimeSpan StallCooldown = TimeSpan.FromHours(1);

    public static readonly TimeSpan MaxStallCooldown = TimeSpan.FromHours(24);

    /// <summary>Set aside for less time when another app holds graphics memory: that usually ends when the app closes.</summary>
    public static readonly TimeSpan LowMemoryCooldown = TimeSpan.FromMinutes(10);

    /// <summary>
    /// This many rejected shares, with at least <see cref="RejectedShareFraction"/> of all shares rejected, means the
    /// pool isn't counting the card's work: that algorithm fails over like a stalled one.
    /// </summary>
    public const int RejectedSharesToFail = 5;

    public const double RejectedShareFraction = 0.8;

    /// <summary>
    /// How long a coin mines on unMineable before Otium asks unMineable whether it credited the work. unMineable's
    /// "calculated" hashrate is an hour's average of accepted shares, so this leaves it time to fill.
    /// </summary>
    public static readonly TimeSpan CreditGrace = TimeSpan.FromMinutes(90);

    /// <summary>How often the credit is checked after <see cref="CreditGrace"/>.</summary>
    public static readonly TimeSpan CreditCheckEvery = TimeSpan.FromMinutes(20);

    private readonly IMoneroPoolApi _pool;
    private readonly IReadOnlyList<GpuInfo> _gpus;
    private readonly IPowerMeter _meter;
    private readonly Func<double> _pricePerKwh;
    private readonly IMinerProcessFactory _factory;
    private readonly TimeProvider _time;
    private readonly GpuPoolEndpoint _endpoint;
    private readonly IUnmineableApi _unmineable;
    private readonly IKryptexAddressApi _kryptex;

    /// <summary>The user is in the US, where Monero mines on MoneroOcean instead of unMineable.</summary>
    private readonly bool _unitedStates;
    private readonly string? _referralCode;
    private readonly ProfitSwitcher _switcher;
    private readonly BenchmarkStore _store;
    private readonly EngineInstaller _installer;
    private readonly SrbMinerAsset? _asset;
    private readonly Func<GpuPolicy?> _policy;
    private readonly Func<GpuInfo, double?> _freeMemoryGb;
    private readonly Func<string, int, CancellationToken, Task<string?>> _poolBlocked;
    private readonly List<(double Hashrate, double? Watts)> _samples = [];
    private readonly Dictionary<string, SetAside> _setAside = new(StringComparer.OrdinalIgnoreCase);

    private IMinerProcess? _process;
    private ProviderRunOptions? _options;
    private GpuAlgorithm? _current;
    private GpuPoolEndpoint? _currentPool;
    private DateTimeOffset _currentSince;
    private DateTimeOffset _launchedAt;
    private DateTimeOffset _lastHashingAt;
    private DateTimeOffset? _devFeeSeenAt;
    private DateTimeOffset _lastEvaluation;
    private DateTimeOffset _nextCreditCheck;
    private bool _benchmarking;
    private int _benchmarkIndex;
    private int _benchmarkTotal;
    private string? _switchNote;
    private string? _notice;
    private string? _lastProblem;

    /// <summary>An algorithm that failed and isn't tried again until <paramref name="Until"/>.</summary>
    private sealed record SetAside(string Reason, DateTimeOffset Until, int Failures);

    /// <param name="freeMemoryGb">Free graphics memory of a GPU right now, or null if unknown (NVML reads it for NVIDIA cards).</param>
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
        Func<GpuPolicy?>? policy = null,
        Func<GpuInfo, double?>? freeMemoryGb = null,
        IUnmineableApi? unmineable = null,
        string? referralCode = null,
        IKryptexAddressApi? kryptex = null,
        bool unitedStates = false,
        Func<string, int, CancellationToken, Task<string?>>? poolBlocked = null)
    {
        _poolBlocked = poolBlocked ?? ((_, _, _) => Task.FromResult<string?>(null));
        _unitedStates = unitedStates;
        _policy = policy ?? (() => null);
        _unmineable = unmineable ?? new UnmineableClient(http);
        _kryptex = kryptex ?? new KryptexAddressClient(http);
        _referralCode = referralCode;
        _freeMemoryGb = freeMemoryGb ?? (_ => null);
        _pool = pool;
        _gpus = gpus.Where(g => g.Vendor is GpuVendor.Nvidia or GpuVendor.Amd).ToList();
        _meter = meter;
        _pricePerKwh = pricePerKwh;
        _factory = factory ?? new SrbMinerProcessFactory(http);
        _time = time ?? TimeProvider.System;
        _endpoint = endpoint ?? GpuPoolEndpoint.MoneroOcean;
        _switcher = switcher ?? new ProfitSwitcher();
        _store = new BenchmarkStore(benchmarkPath, _gpus, _time);
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
        PayoutTarget.TryParse(address, out _, out var error) ? null : error;

    public Task InstallAsync(IProgress<InstallProgress>? progress, CancellationToken ct)
    {
        if (_asset is null)
            throw new PlatformNotSupportedException("SRBMiner has no build for this operating system.");
        return _installer.InstallAsync($"SRBMiner {SrbMinerRelease.Version}", _asset.DownloadUrl, _asset.FileName, _asset.Sha256, progress, ct);
    }

    /// <summary>Forget all measurements and failures so every algorithm is tested again on the next start.</summary>
    public void ResetBenchmarks()
    {
        _store.Clear();
        _setAside.Clear();
        _lastProblem = null;
    }

    public async Task StartAsync(ProviderRunOptions options, CancellationToken ct)
    {
        _options = options;
        // A restart (e.g. for a fee slice) resumes the coin being mined; after an interrupted test there's none to resume.
        var resume = !_benchmarking;
        var eligible = await EligibleAsync(ct);
        if (eligible.Count == 0 && options.Target.Role != PayoutRole.User)
            throw new ProviderProblemException("Graphics card is resting during Otium's short fee slice: none of the coins it can mine there pay on this card. It carries on afterwards.");
        if (eligible.Count == 0)
            throw new ProviderProblemException("Graphics card isn't mining: no coin it can mine pays more than its electricity right now.");

        var pending = eligible.Where(a => !_store.Has(a.PoolName)).ToList();
        if (pending.Count > 0)
        {
            _benchmarking = true;
            if (await LaunchFirstAsync(pending, ct) is not null)
            {
                CountBenchmark(eligible);
                return;
            }
        }

        _benchmarking = false;
        if (await LaunchFirstAsync(await MiningCandidatesAsync(resume, ct), ct) is null)
            throw NotMining();
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
            var exit = $"exited with code {_process.ExitCode}: {string.Join(" ", _process.RecentLog.TakeLast(2))}".Trim();
            if (!_benchmarking)
                return await FailOverAsync(SrbMinerStats.DescribeProblem(_process.RecentLog) ?? $"the miner {exit}", ct);

            _store.Record(new BenchmarkResult(_current.PoolName, 0, null, now, Failure: exit));
            return await AdvanceBenchmarkAsync(ct);
        }

        // During SRBMiner's first dev-fee round the hashes are the developer's, not the user's.
        if (_devFeeSeenAt is null && SrbMinerStats.StartedFirstDevFeeRound(_process.RecentLog))
            _devFeeSeenAt = now;
        var inDevFeeRound = _devFeeSeenAt is { } feeStart && now - feeStart < DevFeeRound;
        var hashrate = inDevFeeRound ? 0 : await _process.ReadHashrateAsync(ct) ?? 0;
        if (hashrate > 0)
        {
            _lastHashingAt = now;
            _notice = null;
            _setAside.Remove(_current.PoolName); // it works again, so a later failure starts the back-off afresh
        }

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
                // A card that hashed early (e.g. only during the dev-fee round) and then stopped hasn't proven anything.
                var stillHashing = _samples.Count > 0 && now - _lastHashingAt <= StillHashingWithin;
                _store.Record(_samples.Count >= MinimumSamples && stillHashing
                    ? new BenchmarkResult(_current.PoolName, Median(_samples.Select(s => s.Hashrate)), AverageWatts(), now)
                    : new BenchmarkResult(_current.PoolName, 0, null, now,
                        Failure: SrbMinerStats.DescribeProblem(_process.RecentLog)
                                 ?? (_samples.Count > 0 ? "stopped hashing during the test" : "no stable hashrate")));
                return await AdvanceBenchmarkAsync(ct);
            }

            var testing = $"Testing {_current.PoolName} on your graphics card ({_benchmarkIndex + 1} of {_benchmarkTotal})";
            return new ProviderStatus(hashrate > 0 ? ProviderState.Running : ProviderState.Starting, hashrate, testing + "…", testing, IsCalibrating: true, Problem: _notice);
        }

        if (_process.Shares is { } shares && shares.Rejected >= RejectedSharesToFail && shares.Rejected >= shares.Total * RejectedShareFraction)
        {
            return await FailOverAsync(
                $"the pool rejected {shares.Rejected} of its {shares.Total} shares, so its work wasn't being paid "
                + "(a change to the coin's rules, such as a fork, can cause this)", ct);
        }

        // The card hashes and the pool takes the shares, but is anything credited? (A pool can accept a card's shares
        // and still not count them, e.g. a coin it doesn't support on that card.) Asked once mining has settled.
        if (now >= _nextCreditCheck)
        {
            _nextCreditCheck = now + CreditCheckEvery;
            if (await UncreditedReasonAsync(ct) is { } uncredited)
                return await FailOverAsync(uncredited, ct);
        }

        if (now - _lastHashingAt >= StallTimeout)
        {
            // A pool something on this computer blocks (antivirus web protection) isn't the coin's fault: say so and
            // keep the miner trying, so it carries on by itself once the user allows the pool.
            if (_currentPool is { } pool && await _poolBlocked(pool.Host, pool.Port, ct) is { } blocked)
            {
                _notice = $"Graphics card isn't mining: {blocked}.";
                return new ProviderStatus(ProviderState.Starting, 0, _notice, _current.PoolName, Problem: _notice);
            }

            var reason = SrbMinerStats.DescribeProblem(_process.RecentLog)
                         ?? $"it showed no hashing for {StallTimeout.TotalMinutes:0} minutes";
            return await FailOverAsync(reason, ct);
        }

        if (now - _lastEvaluation >= ReevaluateEvery)
        {
            _lastEvaluation = now;
            try
            {
                var candidates = await MiningCandidatesAsync(keepCurrent: true, ct);
                if (candidates.Count == 0)
                {
                    // Everything usable was switched off (e.g. the server disabled this coin): stop instead of mining on.
                    var stopped = _current.PoolName;
                    await KillAsync();
                    return new ProviderStatus(ProviderState.Error, 0, $"Stopped mining {stopped}: it isn't paying right now. Otium checks again shortly.");
                }
                if (candidates[0] != _current)
                {
                    var from = _current;
                    if (await LaunchFirstAsync(candidates, ct) is { } next && next != from)
                    {
                        _switchNote = $"switched from {from.PoolName}";
                        return new ProviderStatus(ProviderState.Starting, 0, $"Switching to {next.PoolName}, which pays more right now…", next.PoolName);
                    }
                    if (_process is null)
                        return NotMiningStatus();
                }
            }
            catch (HttpRequestException)
            {
                // Pool stats unavailable: keep mining the current algorithm and try again next time.
            }
        }

        var detail = _switchNote is null ? _current.PoolName : $"{_current.PoolName} ({_switchNote})";
        var waiting = inDevFeeRound
            ? $"Starting {_current.PoolName} (the miner's own short fee round runs first)…"
            : $"Starting {_current.PoolName} (preparing the GPU can take a minute)…";
        return new ProviderStatus(
            hashrate > 0 ? ProviderState.Running : ProviderState.Starting,
            hashrate,
            hashrate > 0 ? null : _notice ?? waiting,
            detail,
            Problem: hashrate > 0 ? null : _notice);
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

    /// <summary>Why an algorithm is set aside right now (it failed recently), or null if it can be mined.</summary>
    public string? SetAsideReason(string algorithm) =>
        _setAside.TryGetValue(algorithm, out var s) && s.Until > _time.GetUtcNow() ? s.Reason : null;

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
            var names = _gpus.Select(g => g.Name).ToList();
            foreach (var a in supported)
            {
                // A card the coin no longer accepts (OnlyGpus, e.g. after Pearl's fork) skips it.
                // Quantus goes to a Quantus wallet, so it's left out of slices that have none (Otium's and the referrer's).
                if (policy.For(a.PoolName) is { UsdPerHashPerSecondDay: > 0 } p && Usable(p.Tier, freePower) && policy.Route(p.Route) is { } route
                    && p.Allows(names)
                    && !(route.Kind == GpuRoutes.KryptexAddress && _options is { Target.QuantusTarget: null }))
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
        if (PayoutRouting.PoolFor(walletAddress, _unitedStates) == PayoutPool.Unmineable)
            return await UnmineableClient.GetPayoutBalanceAsync(_unmineable, PayoutTarget.ParseOrNull(walletAddress)!, ct);
        if (PayoutRouting.PoolFor(walletAddress, _unitedStates) == PayoutPool.Kryptex)
            return await KryptexAddressClient.GetPayoutBalanceAsync(_kryptex, PayoutTarget.ParseAnyOrNull(walletAddress)!, ct);

        walletAddress = PayoutRouting.MoneroOceanWallet(walletAddress);
        var stats = await _pool.GetMinerStatsAsync(walletAddress, ct);
        return stats is null
            ? null
            : new PayoutBalance(stats.PendingXmr, stats.PaidXmr, PayoutCurrency, MoneroOceanClient.MinimumPayoutXmr, MoneroOceanClient.DashboardUrl(walletAddress));
    }

    public async ValueTask DisposeAsync() => await KillAsync();

    private async Task<IReadOnlyList<GpuAlgorithm>> EligibleAsync(CancellationToken ct) =>
        (await QuotesAsync(ct)).Select(q => q.Algorithm).ToList();

    /// <summary>
    /// Measured algorithms that pay and aren't set aside: the switcher's pick first, then the rest best first, as
    /// fallbacks if the pick can't run. With <paramref name="keepCurrent"/>, the pick keeps the algorithm being mined
    /// unless another clearly pays more; without it (right after testing, when "current" is merely the last one
    /// tested) the best one wins outright.
    /// </summary>
    private async Task<IReadOnlyList<GpuAlgorithm>> MiningCandidatesAsync(bool keepCurrent, CancellationToken ct)
    {
        var scored = (await ScoreAsync(ct)).Where(s => SetAsideReason(s.Algorithm) is null).ToList();
        var pick = _switcher.Choose(scored, keepCurrent ? _current?.PoolName : null, _time.GetUtcNow() - _currentSince);
        return scored
            .OrderByDescending(s => string.Equals(s.Algorithm, pick, StringComparison.OrdinalIgnoreCase))
            .Select(s => GpuAlgorithms.Find(s.Algorithm))
            .OfType<GpuAlgorithm>()
            .ToList();
    }

    /// <summary>
    /// For a coin mined on unMineable for the user: why unMineable isn't crediting this card's work (no hashrate
    /// counted for its worker over the last hour), or null when it is, when it can't tell, or for other pools.
    /// </summary>
    internal async Task<string?> UncreditedReasonAsync(CancellationToken ct)
    {
        if (_current is null || _options is not { Target.Role: PayoutRole.User } options || _benchmarking)
            return null;
        var policy = _policy();
        var route = policy?.For(_current.PoolName) is { } p ? policy.Route(p.Route) : null;
        if (route is not { Kind: GpuRoutes.Unmineable } || PayoutTarget.ParseOrNull(options.Target.WalletAddress) is not { } target)
            return null;
        // unMineable groups workers by its own algorithm name, which is its stratum host's first label ("pearlpow").
        var unmineableAlgorithm = route.Host.Split('.')[0];
        var worker = PayoutRouting.SanitizeWorker(options.WorkerName);
        try
        {
            var address = await _unmineable.GetAddressAsync(target, ct);
            if (address.Uuid is not { } uuid)
                return $"unMineable hasn't counted any of its work after {CreditGrace.TotalMinutes:0} minutes";
            var workers = await _unmineable.GetWorkersAsync(uuid, ct);
            var mine = workers.Where(w => string.Equals(w.Algorithm, unmineableAlgorithm, StringComparison.OrdinalIgnoreCase)).ToList();
            if (mine.Any(w => w.CalculatedHps > 0 && (string.Equals(w.Name, worker, StringComparison.OrdinalIgnoreCase) || mine.Count == 1)))
                return null;
            return $"unMineable hasn't counted any of its work after {CreditGrace.TotalMinutes:0} minutes, although the card was hashing";
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or InvalidOperationException or System.Text.Json.JsonException && !ct.IsCancellationRequested)
        {
            return null; // can't tell right now; ask again later
        }
    }

    /// <summary>The current algorithm failed: set it aside and try the next best, or report plainly that the card isn't mining.</summary>
    private async Task<ProviderStatus> FailOverAsync(string reason, CancellationToken ct)
    {
        var failed = _current!;
        await KillAsync();
        SetAsideFor(failed, reason, stalled: true);

        var next = await LaunchFirstAsync(await MiningCandidatesAsync(keepCurrent: false, ct), ct);
        if (next is null)
            return NotMiningStatus();

        _switchNote = $"{failed.PoolName} wasn't mining";
        _notice = $"Graphics card isn't mining {failed.PoolName}: {reason}. Trying {next.PoolName} instead…";
        return new ProviderStatus(ProviderState.Starting, 0, _notice, next.PoolName, Problem: _notice);
    }

    private async Task<ProviderStatus> AdvanceBenchmarkAsync(CancellationToken ct)
    {
        var eligible = await EligibleAsync(ct);
        var pending = eligible.Where(a => !_store.Has(a.PoolName)).ToList();
        if (pending.Count > 0 && await LaunchFirstAsync(pending, ct) is { } testing)
        {
            CountBenchmark(eligible);
            var next = $"Testing {testing.PoolName} on your graphics card ({_benchmarkIndex + 1} of {_benchmarkTotal})";
            return new ProviderStatus(ProviderState.Starting, 0, next + "…", next, IsCalibrating: true, Problem: _notice);
        }

        _benchmarking = false;
        var choice = await LaunchFirstAsync(await MiningCandidatesAsync(keepCurrent: false, ct), ct);
        if (choice is null)
            return NotMiningStatus();
        return new ProviderStatus(ProviderState.Starting, 0, $"Testing done. Mining {choice.PoolName}, the best option for your card…", choice.PoolName, Problem: _notice);
    }

    private void CountBenchmark(IReadOnlyList<GpuAlgorithm> eligible)
    {
        _benchmarkTotal = eligible.Count;
        _benchmarkIndex = eligible.Count(a => _store.Has(a.PoolName));
    }

    /// <summary>
    /// Launches the first candidate that isn't set aside and fits in the graphics memory that's free right now
    /// (other apps can hold a lot of it). Returns null, with no miner running, if none can start.
    /// </summary>
    private async Task<GpuAlgorithm?> LaunchFirstAsync(IReadOnlyList<GpuAlgorithm> candidates, CancellationToken ct)
    {
        await KillAsync(); // before reading free memory, so this miner's own use doesn't count against the next
        foreach (var algorithm in candidates)
        {
            if (SetAsideReason(algorithm.PoolName) is not null)
                continue;
            if (MemoryShortfall(algorithm) is { } shortfall)
            {
                SetAsideFor(algorithm, shortfall, stalled: false);
                continue;
            }
            await LaunchAsync(algorithm, ct);
            return algorithm;
        }
        return null;
    }

    /// <summary>Why the free graphics memory is too small for this algorithm, or null if it fits (or is unknown).</summary>
    private string? MemoryShortfall(GpuAlgorithm algorithm)
    {
        foreach (var gpu in _gpus)
        {
            if (_freeMemoryGb(gpu) is { } free && free + 0.25 < algorithm.MinMemoryGb)
                return $"other apps are using most of the graphics card's memory ({free:0.0} GB of {gpu.MemoryGb:0} GB free, "
                       + $"{algorithm.PoolName} needs about {algorithm.MinMemoryGb:0} GB). Closing games or 3D apps frees it";
        }
        return null;
    }

    private void SetAsideFor(GpuAlgorithm algorithm, string reason, bool stalled)
    {
        var now = _time.GetUtcNow();
        var failures = (_setAside.TryGetValue(algorithm.PoolName, out var earlier) ? earlier.Failures : 0) + 1;
        var cooldown = stalled
            ? TimeSpan.FromTicks(Math.Min(MaxStallCooldown.Ticks, StallCooldown.Ticks * (1L << Math.Min(failures - 1, 10))))
            : LowMemoryCooldown;
        _setAside[algorithm.PoolName] = new SetAside(reason, now + cooldown, failures);
        _lastProblem = reason;
    }

    private ProviderStatus NotMiningStatus()
    {
        var problem = NotMining().Message;
        _notice = null;
        return new ProviderStatus(ProviderState.Error, 0, problem, _current?.PoolName, Problem: problem);
    }

    /// <summary>Plain-words reason that no algorithm can run now, with when Otium tries again.</summary>
    private ProviderProblemException NotMining()
    {
        var now = _time.GetUtcNow();
        var waiting = _setAside.Values.Where(s => s.Until > now).ToList();
        if (waiting.Count == 0 || _lastProblem is null)
            return new ProviderProblemException("Graphics card isn't mining: none of the coins it can mine worked in testing. Use \"Test GPU again\" in Settings to retry.");

        var retryIn = waiting.Min(s => s.Until) - now;
        var when = retryIn.TotalMinutes < 90 ? $"{Math.Ceiling(retryIn.TotalMinutes):0} minutes" : $"{Math.Round(retryIn.TotalHours):0} hours";
        return new ProviderProblemException($"Graphics card isn't mining: {_lastProblem}. Otium tries again in {when}.");
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
        _lastHashingAt = now;
        _devFeeSeenAt = null;
        _lastEvaluation = now;
        _nextCreditCheck = now + CreditGrace;
        _samples.Clear();

        var apiPort = SrbMinerProcessFactory.FreeLoopbackPort();
        var login = LoginFor(algorithm, options);
        _currentPool = login.Pool;
        var args = SrbMinerArgs.Build(algorithm, login, apiPort, _gpus, options.LowPriority);
        ct.ThrowIfCancellationRequested();
        _process = _factory.Start(binary, args, apiPort, options.LowPriority);
    }

    /// <summary>
    /// Kryptex coins log in to the operator's account as "account.worker", or with the slice's own Quantus wallet;
    /// unMineable logs in with the slice's payout target and Otium's referral code; MoneroOcean uses the bare Monero
    /// wallet.
    /// </summary>
    internal PoolLogin LoginFor(GpuAlgorithm algorithm, ProviderRunOptions options)
    {
        var policy = _policy();
        var route = policy?.For(algorithm.PoolName) is { } p ? policy.Route(p.Route) : null;
        if (route is { Kind: GpuRoutes.Kryptex, User.Length: > 0 })
            return PoolLogin.Kryptex(new GpuPoolEndpoint(route.Host, route.Port, route.Tls), route.User!, options.WorkerName);
        if (route is { Kind: GpuRoutes.KryptexAddress })
        {
            if (PayoutTarget.ParseAnyOrNull(options.Target.QuantusTarget) is not { Coin.Symbol: "QTC" } quantus)
                throw new InvalidOperationException($"{algorithm.PoolName} is paid to your own Quantus wallet, and there's no Quantus address in Settings.");
            return PoolLogin.KryptexAddress(new GpuPoolEndpoint(route.Host, route.Port, route.Tls), quantus.Address, options.WorkerName);
        }
        if (route is { Kind: GpuRoutes.Unmineable })
        {
            // Any payout coin works there, Monero included (the app keeps US users off these routes).
            if (PayoutRouting.PoolFor(options.Target.WalletAddress, _unitedStates) is null)
                throw new InvalidOperationException($"{algorithm.PoolName} is paid by unMineable, which needs a payout address with its coin.");
            return PoolLogin.Unmineable(new GpuPoolEndpoint(route.Host, route.Port, route.Tls), options.Target.WalletAddress, options.WorkerName, _referralCode);
        }
        if (route is null ? algorithm.DefaultRoute != GpuRoutes.MoneroOcean : route.Kind != GpuRoutes.MoneroOcean)
            throw new InvalidOperationException($"{algorithm.PoolName} needs Otium's pool settings, which aren't available right now.");
        return PoolLogin.MoneroOcean(_endpoint, PayoutRouting.MoneroOceanWallet(options.Target.WalletAddress), options.WorkerName, algorithm);
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
