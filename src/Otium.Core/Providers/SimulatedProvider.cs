using System.Net.Http.Json;
using Otium.Core.Pools;
using Otium.Core.Switching;
using Otium.Core.Wallets;

namespace Otium.Core.Providers;

/// <summary>
/// Development stand-in for a real miner: pretends to hash at a believable speed and reports it to the simulated
/// pool (Otium.DevPool), so the whole earn → credit → cash-out loop can run without mining software, antivirus
/// prompts or real money. Enabled with OTIUM_SIMULATE=1.
/// </summary>
public sealed class SimulatedProvider(
    HttpClient http,
    IMoneroPoolApi pool,
    DeviceKind device,
    string algo,
    double fullSpeedHps,
    double normalizedPerHash,
    TimeProvider? time = null,
    Func<GpuPolicy?>? policy = null,
    bool unitedStates = false) : IComputeProvider
{
    public static bool IsEnabled => Environment.GetEnvironmentVariable("OTIUM_SIMULATE") is "1" or "true";

    /// <summary>Simulated speeds (H/s) for Kryptex coins, like an RTX 3080.</summary>
    private static readonly Dictionary<string, double> KryptexSpeeds = new(StringComparer.OrdinalIgnoreCase)
    {
        ["pearlhash"] = 104e12,
        ["quantus"] = 1.0e9,
    };

    private readonly TimeProvider _time = time ?? TimeProvider.System;

    // One "connection" to the pool, like a real miner process; CPU, GPU and other PCs of an account are separate rigs.
    private readonly string _rig = $"{device}-{Guid.NewGuid():N}"[..16];
    private ProviderRunOptions? _running;
    private DateTimeOffset _startedAt;

    public string Id => $"simulated-{device.ToString().ToLowerInvariant()}";

    public string DisplayName => $"Simulated {(device == DeviceKind.Cpu ? "processor" : "graphics card")}";

    public DeviceKind Device => device;

    public string PayoutCurrency => "XMR";

    public bool IsSupportedOnThisPlatform => true;

    public bool IsInstalled => true;

    public Task InstallAsync(IProgress<InstallProgress>? progress, CancellationToken ct) => Task.CompletedTask;

    public Task StartAsync(ProviderRunOptions options, CancellationToken ct)
    {
        _running = options;
        _startedAt = _time.GetUtcNow();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken ct)
    {
        _running = null;
        return Task.CompletedTask;
    }

    public async Task<ProviderStatus> GetStatusAsync(CancellationToken ct)
    {
        if (_running is not { } options)
            return ProviderStatus.Stopped(installed: true);

        // A short warm-up like the real miners, then the configured speed with a little jitter.
        if (_time.GetUtcNow() - _startedAt < TimeSpan.FromSeconds(8))
            return new ProviderStatus(ProviderState.Starting, 0, "Warming up (simulated)…", $"simulated {algo}");

        var usage = device == DeviceKind.Cpu ? Math.Clamp(options.UsagePercent, 1, 100) / 100.0 : 1.0;
        var jitter = 0.95 + Random.Shared.NextDouble() * 0.1;

        // Like the real GPU miner, follow the server's policy: mine the best Kryptex coin when there is one.
        var kryptex = device == DeviceKind.Gpu ? BestKryptexAlgorithm() : null;
        var mined = kryptex?.Policy.Algorithm ?? algo;
        var hashrate = (kryptex is null ? fullSpeedHps * usage : KryptexSpeeds[kryptex.Policy.Algorithm]) * jitter;
        // Like XMRig, a CPU the server routes to Kryptex mines Monero there under "account.worker".
        var kryptexCpu = device == DeviceKind.Cpu && options.Pool is { Kind: Accounts.CpuPoolRoute.Kryptex } route ? route : null;
        object report = (kryptex, kryptexCpu) switch
        {
            ({ } k, _) => new { wallet = $"kryptex:{k.Coin}:{k.Route.User}", worker = options.WorkerName, algo = mined, hashrate, normalizedHashrate = 0.0, rig = _rig },
            (null, { } r) => new { wallet = $"kryptex:xmr:{r.User.Split('.')[0]}", worker = options.WorkerName, algo, hashrate, normalizedHashrate = 0.0, rig = _rig },
            // Monero is credited to the bare address, as MoneroOcean does; other coins under their "COIN:address".
            _ => new
            {
                wallet = PayoutRouting.PoolFor(options.Target.WalletAddress, unitedStates) == PayoutPool.Unmineable
                    ? options.Target.WalletAddress
                    : PayoutRouting.MoneroOceanWallet(options.Target.WalletAddress),
                worker = options.WorkerName, algo, hashrate, normalizedHashrate = hashrate * normalizedPerHash, rig = _rig,
            },
        };
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(3));
            await http.PostAsJsonAsync(new Uri(MoneroOceanClient.ApiBase, "dev/report"), report, timeout.Token);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            return new ProviderStatus(ProviderState.Running, hashrate, "Simulated pool not reachable", $"simulated {mined}");
        }
        return new ProviderStatus(ProviderState.Running, hashrate, null, $"simulated {mined}");
    }

    private sealed record KryptexChoice(GpuAlgorithmPolicy Policy, GpuPoolRoute Route, string Coin);

    private KryptexChoice? BestKryptexAlgorithm()
    {
        if (policy?.Invoke() is not { } current)
            return null;
        return current.Algorithms
            .Where(a => a.Tier == GpuTiers.Primary && a.UsdPerHashPerSecondDay > 0 && KryptexSpeeds.ContainsKey(a.Algorithm))
            .Select(a => (a, route: current.Route(a.Route)))
            .Where(x => x.route is { Kind: GpuRoutes.Kryptex, User.Length: > 0 })
            .OrderByDescending(x => x.a.UsdPerHashPerSecondDay * KryptexSpeeds[x.a.Algorithm])
            .Select(x => new KryptexChoice(x.a, x.route!, x.route!.Name[(GpuRoutes.Kryptex.Length + 1)..]))
            .FirstOrDefault();
    }

    public async Task<RevenueEstimate> EstimateRevenueAsync(double hashrateHps, CancellationToken ct)
    {
        if (device == DeviceKind.Gpu && BestKryptexAlgorithm() is { } k)
        {
            // 2% miner fee and the pool's fee, as with the real miner.
            var usd = hashrateHps * k.Policy.UsdPerHashPerSecondDay * 0.98 * (1 - k.Policy.PoolFeePercent / 100);
            return new RevenueEstimate(usd, usd, "USD");
        }
        var network = await pool.GetNetworkStatsAsync(ct);
        var xmr = MoneroProfit.XmrPerDay(hashrateHps * normalizedPerHash, network);
        return new RevenueEstimate(xmr, xmr * network.PriceUsd, PayoutCurrency);
    }

    public Task<ProfitOffer?> GetExpectedOfferAsync(CancellationToken ct) => Task.FromResult<ProfitOffer?>(null);

    public async Task<PayoutBalance?> GetPayoutBalanceAsync(string walletAddress, CancellationToken ct)
    {
        // The dev pool also answers unMineable's API (OTIUM_UNMINEABLE_API), for coins other than Monero.
        if (PayoutRouting.PoolFor(walletAddress, unitedStates) == PayoutPool.Unmineable)
            return await UnmineableClient.GetPayoutBalanceAsync(new UnmineableClient(http), PayoutTarget.ParseOrNull(walletAddress)!, ct);

        walletAddress = PayoutRouting.MoneroOceanWallet(walletAddress);
        var stats = await pool.GetMinerStatsAsync(walletAddress, ct);
        return stats is null ? null : new PayoutBalance(stats.PendingXmr, stats.PaidXmr, PayoutCurrency, MoneroOceanClient.MinimumPayoutXmr, null);
    }

    public string? ValidatePayoutAddress(string address) =>
        PayoutTarget.TryParse(address, out _, out var error) ? null : error;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
