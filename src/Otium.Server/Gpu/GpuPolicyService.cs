using Microsoft.Extensions.Options;
using Otium.Core.Pools;
using Otium.Core.Switching;

namespace Otium.Server.Gpu;

/// <summary>
/// Builds the GPU policy apps follow: every algorithm with its live USD rate, pool route and tier. MoneroOcean's
/// legacy algorithms default to "free power only" because they lose money at normal electricity prices (Oct 2026);
/// Kryptex coins (custodial servers only) and unMineable coins are primary while their feed is fresh. Each algorithm
/// has one route: Kryptex wins over unMineable for the same algorithm, since apps of a custodial server mine Kryptex
/// for balance accounts. An algorithm whose feed fails or goes stale is disabled automatically, and the operator can
/// disable any algorithm with <see cref="GpuPolicyOptions.Disabled"/>.
/// </summary>
public sealed class GpuPolicyService(
    IMoneroPoolApi pool,
    IProfitFeed feed,
    IKryptexApi kryptex,
    IOptions<GpuPolicyOptions> policyOptions,
    IOptions<KryptexOptions> kryptexOptions,
    TimeProvider time,
    ILogger<GpuPolicyService> logger,
    IOptions<UnmineableOptions>? unmineableOptions = null,
    IOptions<CustodyOptions>? custodyOptions = null)
{
    /// <summary>Feed data older than this can't be trusted to price a fast-moving coin.</summary>
    public static readonly TimeSpan MaxFeedAge = TimeSpan.FromHours(2);

    public const double MoneroOceanFeePercent = 1.0;
    public const double DefaultKryptexFeePercent = 3.0;

    public static readonly GpuPoolRoute MoneroOceanRoute = new(GpuRoutes.MoneroOcean, GpuRoutes.MoneroOcean, "gulf.moneroocean.stream", 10128, null);

    /// <summary>GPU algorithms MoneroOcean pays for, converted to XMR.</summary>
    public static readonly IReadOnlyList<string> MoneroOceanAlgorithms = ["kawpow", "autolykos2", "etchash", "cn/gpu"];

    private readonly SemaphoreSlim _lock = new(1, 1);
    private GpuPolicy? _cached;

    public async Task<GpuPolicy> GetAsync(CancellationToken ct)
    {
        var now = time.GetUtcNow();
        if (_cached is { } fresh && now - fresh.GeneratedAt < policyOptions.Value.RefreshInterval)
            return fresh;

        await _lock.WaitAsync(ct);
        try
        {
            if (_cached is { } again && now - again.GeneratedAt < policyOptions.Value.RefreshInterval)
                return again;
            _cached = await BuildAsync(ct);
            return _cached;
        }
        finally
        {
            _lock.Release();
        }
    }

    internal async Task<GpuPolicy> BuildAsync(CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var algorithms = new List<GpuAlgorithmPolicy>();
        var routes = new List<GpuPoolRoute> { MoneroOceanRoute };

        AlgoProfits? profits = null;
        try
        {
            profits = await pool.GetAlgoProfitsAsync(ct);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            logger.LogWarning(e, "MoneroOcean algorithm profits unavailable");
        }
        foreach (var algo in MoneroOceanAlgorithms)
        {
            var rate = profits is null ? 0 : profits.For(algo) * profits.XmrPriceUsd;
            algorithms.Add(rate > 0
                ? new GpuAlgorithmPolicy(algo, GpuTiers.FreePowerOnly, GpuRoutes.MoneroOcean, rate, MoneroOceanFeePercent,
                    "Usually costs more in electricity than it earns")
                : new GpuAlgorithmPolicy(algo, GpuTiers.Disabled, GpuRoutes.MoneroOcean, 0, MoneroOceanFeePercent, "MoneroOcean isn't paying for it right now"));
        }

        // Kryptex mines to Otium's own account and is credited as balances, so only a custodial server offers it.
        // (Constructed without the custody option, as in older tests, the service keeps its custodial behaviour.)
        var kx = kryptexOptions.Value;
        if ((custodyOptions?.Value.Enabled ?? true) && kx.Enabled && !string.IsNullOrWhiteSpace(kx.Account))
        {
            foreach (var (algo, coin) in kx.Coins)
            {
                var route = $"{GpuRoutes.Kryptex}-{coin.Coin}";
                routes.Add(new GpuPoolRoute(route, GpuRoutes.Kryptex, coin.Host, coin.Port, kx.Account.Trim()));
                algorithms.Add(await KryptexAlgorithmAsync(algo, coin, route, now, ct));
            }
        }

        // unMineable pays users' own addresses; apps log in with their payout target, so the route has no account.
        if (unmineableOptions?.Value is { Enabled: true } um)
        {
            foreach (var (algo, coin) in um.Algorithms)
            {
                if (algorithms.Any(a => string.Equals(a.Algorithm, algo, StringComparison.OrdinalIgnoreCase)))
                    continue;
                var route = $"{GpuRoutes.Unmineable}-{algo}";
                routes.Add(new GpuPoolRoute(route, GpuRoutes.Unmineable, coin.Host, coin.Port, null, Tls: true));
                algorithms.Add(await FeedAlgorithmAsync(algo, coin.WhatToMineId, route, um.FeePercent,
                    coin.Tier == GpuTiers.FreePowerOnly ? GpuTiers.FreePowerOnly : GpuTiers.Primary, now, ct));
            }
        }

        // Operator overrides last: explicit tiers, then the kill switch.
        var options = policyOptions.Value;
        for (var i = 0; i < algorithms.Count; i++)
        {
            var a = algorithms[i];
            if (options.Tiers.TryGetValue(a.Algorithm, out var tier) && tier is GpuTiers.Primary or GpuTiers.FreePowerOnly or GpuTiers.Disabled
                && !(tier != GpuTiers.Disabled && a.UsdPerHashPerSecondDay <= 0))
                a = a with { Tier = tier, Note = "Set by Otium" };
            if (options.Disabled.Contains(a.Algorithm, StringComparer.OrdinalIgnoreCase))
                a = a with { Tier = GpuTiers.Disabled, Note = "Turned off by Otium" };
            algorithms[i] = a;
        }

        return new GpuPolicy(now, algorithms, routes);
    }

    /// <summary>An algorithm priced only from WhatToMine, with a fixed pool fee (unMineable).</summary>
    private async Task<GpuAlgorithmPolicy> FeedAlgorithmAsync(
        string algo, int whatToMineId, string route, double feePercent, string tier, DateTimeOffset now, CancellationToken ct)
    {
        CoinRevenue? revenue;
        try
        {
            revenue = await feed.GetAsync(whatToMineId, ct);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            logger.LogWarning(e, "Profitability feed for {Algorithm} unavailable; disabling it until it recovers", algo);
            return new GpuAlgorithmPolicy(algo, GpuTiers.Disabled, route, 0, feePercent, "Profitability data unavailable");
        }

        if (revenue is null || revenue.UsdPerHashPerSecondDay <= 0)
            return new GpuAlgorithmPolicy(algo, GpuTiers.Disabled, route, 0, feePercent, "Profitability data unavailable");
        if (revenue.Lagging || now - revenue.UpdatedAt > MaxFeedAge)
            return new GpuAlgorithmPolicy(algo, GpuTiers.Disabled, route, revenue.UsdPerHashPerSecondDay, feePercent, "Profitability data is out of date");
        return new GpuAlgorithmPolicy(algo, tier, route, revenue.UsdPerHashPerSecondDay, feePercent,
            tier == GpuTiers.FreePowerOnly ? "Usually costs more in electricity than it earns" : null);
    }

    private async Task<GpuAlgorithmPolicy> KryptexAlgorithmAsync(string algo, KryptexCoin coin, string route, DateTimeOffset now, CancellationToken ct)
    {
        CoinRevenue? revenue;
        double fee;
        try
        {
            revenue = await feed.GetAsync(coin.WhatToMineId, ct);
            fee = (await kryptex.GetPoolInfoAsync(coin.Coin, ct)).FeePercent;
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            logger.LogWarning(e, "Profitability feed for {Algorithm} unavailable; disabling it until it recovers", algo);
            return new GpuAlgorithmPolicy(algo, GpuTiers.Disabled, route, 0, DefaultKryptexFeePercent, "Profitability data unavailable");
        }

        if (revenue is null || revenue.UsdPerHashPerSecondDay <= 0)
            return new GpuAlgorithmPolicy(algo, GpuTiers.Disabled, route, 0, fee, "Profitability data unavailable");
        if (revenue.Lagging || now - revenue.UpdatedAt > MaxFeedAge)
            return new GpuAlgorithmPolicy(algo, GpuTiers.Disabled, route, revenue.UsdPerHashPerSecondDay, fee, "Profitability data is out of date");
        return new GpuAlgorithmPolicy(algo, GpuTiers.Primary, route, revenue.UsdPerHashPerSecondDay, fee);
    }
}
