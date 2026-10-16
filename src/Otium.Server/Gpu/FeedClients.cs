using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Otium.Server.Gpu;

/// <summary>Live revenue for one coin: USD per day earned by 1 H/s, from network data and the market price.</summary>
public sealed record CoinRevenue(double UsdPerHashPerSecondDay, bool Lagging, DateTimeOffset UpdatedAt);

public interface IProfitFeed
{
    Task<CoinRevenue?> GetAsync(int whatToMineId, CancellationToken ct);
}

/// <summary>
/// WhatToMine's coin JSON (https://whattomine.com/coins/{id}.json). Revenue per hash follows from the network itself:
/// block reward × blocks per day ÷ network hashrate, priced via its BTC exchange rate. The BTC price is implied by its
/// own "revenue" and "btc_revenue" fields, so no second price source is needed.
/// </summary>
public sealed class WhatToMineClient(HttpClient http, IOptions<KryptexOptions> options) : IProfitFeed
{
    public async Task<CoinRevenue?> GetAsync(int whatToMineId, CancellationToken ct)
    {
        var url = new Uri(new Uri(options.Value.WhatToMineBase.TrimEnd('/') + "/"), $"coins/{whatToMineId}.json");
        using var response = await http.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(ct));
        return Parse(doc.RootElement);
    }

    internal static CoinRevenue? Parse(JsonElement coin)
    {
        var reward = Number(coin, "block_reward24") ?? Number(coin, "block_reward");
        var blockTime = Number(coin, "block_time");
        var nethash = Number(coin, "nethash");
        var rateBtc = Number(coin, "exchange_rate24") ?? Number(coin, "exchange_rate");
        var revenueUsd = Number(coin, "revenue");
        var revenueBtc = Number(coin, "btc_revenue");
        if (reward is not > 0 || blockTime is not > 0 || nethash is not > 0 || rateBtc is not > 0 || revenueUsd is not > 0 || revenueBtc is not > 0)
            return null;

        var btcUsd = revenueUsd.Value / revenueBtc.Value;
        var coinsPerHashDay = reward.Value * 86_400 / blockTime.Value / nethash.Value;
        var lagging = coin.TryGetProperty("lagging", out var l) && l.ValueKind == JsonValueKind.True;
        var updated = Number(coin, "timestamp") is { } ts ? DateTimeOffset.FromUnixTimeSeconds((long)ts) : DateTimeOffset.UtcNow;
        return new CoinRevenue(coinsPerHashDay * rateBtc.Value * btcUsd, lagging, updated);
    }

    /// <summary>WhatToMine mixes JSON numbers and strings like "177.0" or "$6.86".</summary>
    private static double? Number(JsonElement e, string name)
    {
        if (!e.TryGetProperty(name, out var v))
            return null;
        if (v.ValueKind == JsonValueKind.Number)
            return v.GetDouble();
        if (v.ValueKind == JsonValueKind.String
            && double.TryParse(v.GetString()!.Replace("$", "", StringComparison.Ordinal).Replace(",", "", StringComparison.Ordinal),
                NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
            return d;
        return null;
    }
}

public sealed record KryptexPoolInfo(double FeePercent);

public sealed record KryptexWorker(string Name, double Hashrate);

public sealed record KryptexPoint(DateTimeOffset Time, double Hashrate);

public interface IKryptexApi
{
    Task<KryptexPoolInfo> GetPoolInfoAsync(string coin, CancellationToken ct);

    Task<IReadOnlyList<KryptexWorker>> GetWorkersAsync(string coin, string account, CancellationToken ct);

    /// <summary>Hashrate history for one worker (H/s), oldest first.</summary>
    Task<IReadOnlyList<KryptexPoint>> GetWorkerChartAsync(string coin, string account, string worker, CancellationToken ct);
}

/// <summary>Kryptex Pool's public API (https://pool.kryptex.com/openapi.yaml).</summary>
public sealed class KryptexClient(HttpClient http, IOptions<KryptexOptions> options) : IKryptexApi
{
    private Uri Base => new(options.Value.ApiBase.TrimEnd('/') + "/");

    public async Task<KryptexPoolInfo> GetPoolInfoAsync(string coin, CancellationToken ct)
    {
        using var doc = await GetAsync($"{Esc(coin)}/api/v1/pool/info", ct);
        var fee = doc.RootElement.TryGetProperty("fee", out var f) && f.ValueKind == JsonValueKind.Number ? f.GetDouble() : 0.01;
        return new KryptexPoolInfo(fee * 100);
    }

    public async Task<IReadOnlyList<KryptexWorker>> GetWorkersAsync(string coin, string account, CancellationToken ct)
    {
        using var doc = await GetAsync($"{Esc(coin)}/api/v3/miner/workers/{Esc(account)}", ct);
        return ParseWorkers(doc.RootElement);
    }

    /// <summary>
    /// The live API (October 2026) sends no "hashrate" per worker, despite its OpenAPI file, but 30-minute, 3-hour and
    /// 24-hour averages as strings ("avg_hashrate_30m": "250199979298360.89"). Accept both.
    /// </summary>
    internal static IReadOnlyList<KryptexWorker> ParseWorkers(JsonElement root)
    {
        var list = new List<KryptexWorker>();
        if (root.TryGetProperty("results", out var results) && results.ValueKind == JsonValueKind.Array)
        {
            foreach (var w in results.EnumerateArray())
            {
                if (w.TryGetProperty("worker", out var name) && name.ValueKind == JsonValueKind.String)
                    list.Add(new KryptexWorker(name.GetString()!, Number(w, "hashrate") ?? Number(w, "avg_hashrate_30m") ?? 0));
            }
        }
        return list;
    }

    private static double? Number(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value))
            return null;
        if (value.ValueKind == JsonValueKind.Number)
            return value.GetDouble();
        return value.ValueKind == JsonValueKind.String
               && double.TryParse(value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    public async Task<IReadOnlyList<KryptexPoint>> GetWorkerChartAsync(string coin, string account, string worker, CancellationToken ct)
    {
        using var doc = await GetAsync($"{Esc(coin)}/api/v1/miner/chart/{Esc(account)}/{Esc(worker)}?days=1", ct);
        return ParseChart(doc.RootElement);
    }

    internal static IReadOnlyList<KryptexPoint> ParseChart(JsonElement root)
    {
        var points = new List<KryptexPoint>();
        if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var p in root.EnumerateArray())
            {
                // Numbers or numeric strings: the workers endpoint already sends strings, so don't let a format change
                // here silently credit nothing.
                if (ParseTime(p) is { } time && Number(p, "hashrate") is { } hashrate)
                    points.Add(new KryptexPoint(time, hashrate));
            }
        }
        return points.OrderBy(p => p.Time).ToList();
    }

    /// <summary>Unix seconds, Unix milliseconds or an ISO date, whichever the API sends.</summary>
    internal static DateTimeOffset? ParseTime(JsonElement point)
    {
        if (!point.TryGetProperty("timestamp", out var t))
            return null;
        if (t.ValueKind == JsonValueKind.Number)
        {
            var value = t.GetDouble();
            return value > 1e12 ? DateTimeOffset.FromUnixTimeMilliseconds((long)value) : DateTimeOffset.FromUnixTimeSeconds((long)value);
        }
        return t.ValueKind == JsonValueKind.String
               && DateTimeOffset.TryParse(t.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed)
            ? parsed
            : null;
    }

    private async Task<JsonDocument> GetAsync(string path, CancellationToken ct)
    {
        using var response = await http.GetAsync(new Uri(Base, path), ct);
        response.EnsureSuccessStatusCode();
        return await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
    }

    private static string Esc(string value) => Uri.EscapeDataString(value);
}
