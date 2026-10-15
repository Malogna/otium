using System.Net.Http.Json;
using Otium.Core.Pools;
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
    TimeProvider? time = null) : IComputeProvider
{
    public static bool IsEnabled => Environment.GetEnvironmentVariable("OTIUM_SIMULATE") is "1" or "true";

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
        var hashrate = fullSpeedHps * usage * (0.95 + Random.Shared.NextDouble() * 0.1);
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(3));
            await http.PostAsJsonAsync(new Uri(MoneroOceanClient.ApiBase, "dev/report"),
                new { wallet = options.Target.WalletAddress, worker = options.WorkerName, algo, hashrate, normalizedHashrate = hashrate * normalizedPerHash, rig = _rig },
                timeout.Token);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            return new ProviderStatus(ProviderState.Running, hashrate, "Simulated pool not reachable", $"simulated {algo}");
        }
        return new ProviderStatus(ProviderState.Running, hashrate, null, $"simulated {algo}");
    }

    public async Task<RevenueEstimate> EstimateRevenueAsync(double hashrateHps, CancellationToken ct)
    {
        var network = await pool.GetNetworkStatsAsync(ct);
        var xmr = MoneroProfit.XmrPerDay(hashrateHps * normalizedPerHash, network);
        return new RevenueEstimate(xmr, xmr * network.PriceUsd, PayoutCurrency);
    }

    public Task<ProfitOffer?> GetExpectedOfferAsync(CancellationToken ct) => Task.FromResult<ProfitOffer?>(null);

    public async Task<PayoutBalance?> GetPayoutBalanceAsync(string walletAddress, CancellationToken ct)
    {
        var stats = await pool.GetMinerStatsAsync(walletAddress, ct);
        return stats is null ? null : new PayoutBalance(stats.PendingXmr, stats.PaidXmr, PayoutCurrency, MoneroOceanClient.MinimumPayoutXmr, null);
    }

    public string? ValidatePayoutAddress(string address) =>
        MoneroAddress.TryValidate(address, out _, out var error) ? null : error;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
