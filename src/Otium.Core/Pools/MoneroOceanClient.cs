using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Otium.Core.Pools;

public sealed record MoneroNetworkStats(double Difficulty, double BlockRewardXmr, double PriceUsd, DateTimeOffset FetchedAt);

public sealed record PoolMinerStats(double TotalHashes, double PendingXmr, double PaidXmr, double CurrentHashrate);

/// <summary>
/// What MoneroOcean currently pays per algorithm. MoneroOcean mines other coins on GPU algorithms, exchanges
/// them and pays everything out in XMR to the same wallet.
/// </summary>
/// <param name="XmrPerHashPerSecondDay">
/// Best "coinProfit" among the pool's active coins for each algorithm: XMR per day for each 1 H/s of that
/// algorithm (for rx/0 this matches difficulty/reward maths to within ~1%).
/// </param>
public sealed record AlgoProfits(IReadOnlyDictionary<string, double> XmrPerHashPerSecondDay, double XmrPriceUsd, DateTimeOffset FetchedAt)
{
    public double For(string algo) => XmrPerHashPerSecondDay.TryGetValue(algo, out var v) ? v : 0;
}

public interface IMoneroPoolApi
{
    Task<MoneroNetworkStats> GetNetworkStatsAsync(CancellationToken ct);

    Task<PoolMinerStats?> GetMinerStatsAsync(string walletAddress, CancellationToken ct);

    Task<AlgoProfits> GetAlgoProfitsAsync(CancellationToken ct);

    /// <summary>
    /// Per-worker hashrate history for one wallet (2-minute points, about 72 hours kept).
    /// <see cref="HashratePoint.NormalizedHps"/> is MoneroOcean's "hs2": hashrate converted to its XMR-equivalent value,
    /// so GPU algorithms and CPU RandomX can be credited on the same scale.
    /// </summary>
    Task<IReadOnlyDictionary<string, IReadOnlyList<HashratePoint>>> GetWorkerHistoryAsync(string walletAddress, CancellationToken ct);
}

/// <param name="Hps">Raw hashrate in the worker's own algorithm ("hs").</param>
/// <param name="NormalizedHps">XMR-equivalent (rx/0) hashrate ("hs2"), which is what earnings are proportional to.</param>
public sealed record HashratePoint(DateTimeOffset Time, double Hps, double NormalizedHps);

/// <summary>Public MoneroOcean API. Network stats are cached because they only change every block (~2 min).</summary>
public sealed class MoneroOceanClient(HttpClient http, TimeProvider? time = null) : IMoneroPoolApi
{
    public static readonly Uri ApiBase = new("https://api.moneroocean.stream/");
    public const double AtomicUnitsPerXmr = 1e12;
    public const double MinimumPayoutXmr = 0.003;

    private static readonly TimeSpan CacheFor = TimeSpan.FromMinutes(5);
    private readonly TimeProvider _time = time ?? TimeProvider.System;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private MoneroNetworkStats? _cached;
    private AlgoProfits? _cachedProfits;

    public static Uri DashboardUrl(string walletAddress) => new($"https://moneroocean.stream/?addr={Uri.EscapeDataString(walletAddress)}");

    public async Task<MoneroNetworkStats> GetNetworkStatsAsync(CancellationToken ct)
    {
        await _lock.WaitAsync(ct);
        try
        {
            if (_cached is { } c && _time.GetUtcNow() - c.FetchedAt < CacheFor)
                return c;

            var network = await http.GetFromJsonAsync<NetworkStatsDto>(new Uri(ApiBase, "network/stats"), ct)
                          ?? throw new InvalidOperationException("Empty network stats response.");
            var pool = await http.GetFromJsonAsync<PoolStatsDto>(new Uri(ApiBase, "pool/stats"), ct)
                       ?? throw new InvalidOperationException("Empty pool stats response.");

            _cached = new MoneroNetworkStats(
                network.Difficulty,
                network.Value / AtomicUnitsPerXmr,
                pool.PoolStatistics?.Price?.Usd ?? 0,
                _time.GetUtcNow());
            return _cached;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<AlgoProfits> GetAlgoProfitsAsync(CancellationToken ct)
    {
        await _lock.WaitAsync(ct);
        try
        {
            if (_cachedProfits is { } c && _time.GetUtcNow() - c.FetchedAt < CacheFor)
                return c;

            using var stream = await http.GetStreamAsync(new Uri(ApiBase, "pool/stats"), ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            _cachedProfits = ParseAlgoProfits(doc.RootElement, _time.GetUtcNow());
            return _cachedProfits;
        }
        finally
        {
            _lock.Release();
        }
    }

    internal static AlgoProfits ParseAlgoProfits(JsonElement root, DateTimeOffset now)
    {
        var stats = root.GetProperty("pool_statistics");
        var price = stats.TryGetProperty("price", out var p) && p.TryGetProperty("usd", out var usd) ? usd.GetDouble() : 0;
        var best = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        if (stats.TryGetProperty("portCoinAlgo", out var algos) && stats.TryGetProperty("coinProfit", out var profits))
        {
            var active = stats.TryGetProperty("activePorts", out var ports)
                ? ports.EnumerateArray().Select(x => x.ToString()).ToHashSet()
                : null;

            foreach (var entry in algos.EnumerateObject())
            {
                if (active is not null && !active.Contains(entry.Name))
                    continue;
                if (!profits.TryGetProperty(entry.Name, out var profitElement) || profitElement.ValueKind != JsonValueKind.Number)
                    continue;
                var algo = entry.Value.GetString();
                var profit = profitElement.GetDouble();
                if (string.IsNullOrEmpty(algo) || profit <= 0)
                    continue;
                if (!best.TryGetValue(algo, out var existing) || profit > existing)
                    best[algo] = profit;
            }
        }

        return new AlgoProfits(best, price, now);
    }

    public async Task<IReadOnlyDictionary<string, IReadOnlyList<HashratePoint>>> GetWorkerHistoryAsync(string walletAddress, CancellationToken ct)
    {
        using var stream = await http.GetStreamAsync(
            new Uri(ApiBase, $"miner/{Uri.EscapeDataString(walletAddress)}/chart/hashrate/allWorkers"), ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        return ParseWorkerHistory(doc.RootElement);
    }

    internal static IReadOnlyDictionary<string, IReadOnlyList<HashratePoint>> ParseWorkerHistory(JsonElement root)
    {
        var result = new Dictionary<string, IReadOnlyList<HashratePoint>>(StringComparer.Ordinal);
        if (root.ValueKind != JsonValueKind.Object)
            return result;

        foreach (var worker in root.EnumerateObject())
        {
            if (worker.Name == "global" || worker.Value.ValueKind != JsonValueKind.Array)
                continue;
            var points = new List<HashratePoint>();
            foreach (var point in worker.Value.EnumerateArray())
            {
                if (!point.TryGetProperty("ts", out var ts) || ts.ValueKind != JsonValueKind.Number)
                    continue;
                var hs = point.TryGetProperty("hs", out var h) && h.ValueKind == JsonValueKind.Number ? h.GetDouble() : 0;
                var hs2 = point.TryGetProperty("hs2", out var h2) && h2.ValueKind == JsonValueKind.Number ? h2.GetDouble() : hs;
                points.Add(new HashratePoint(DateTimeOffset.FromUnixTimeMilliseconds(ts.GetInt64()), hs, hs2));
            }
            result[worker.Name] = points.OrderBy(p => p.Time).ToList();
        }
        return result;
    }

    public async Task<PoolMinerStats?> GetMinerStatsAsync(string walletAddress, CancellationToken ct)
    {
        var dto = await http.GetFromJsonAsync<MinerStatsDto>(
            new Uri(ApiBase, $"miner/{Uri.EscapeDataString(walletAddress)}/stats"), ct);
        return dto is null
            ? null
            : new PoolMinerStats(dto.TotalHashes, dto.AmtDue / AtomicUnitsPerXmr, dto.AmtPaid / AtomicUnitsPerXmr, dto.Hash);
    }

    private sealed record NetworkStatsDto(
        [property: JsonPropertyName("difficulty")] double Difficulty,
        [property: JsonPropertyName("value")] double Value);

    private sealed record PoolStatsDto([property: JsonPropertyName("pool_statistics")] PoolStatisticsDto? PoolStatistics);

    private sealed record PoolStatisticsDto([property: JsonPropertyName("price")] PriceDto? Price);

    private sealed record PriceDto([property: JsonPropertyName("usd")] double Usd);

    private sealed record MinerStatsDto(
        [property: JsonPropertyName("hash")] double Hash,
        [property: JsonPropertyName("totalHashes")] double TotalHashes,
        [property: JsonPropertyName("amtPaid")] double AmtPaid,
        [property: JsonPropertyName("amtDue")] double AmtDue);
}
