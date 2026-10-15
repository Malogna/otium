using System.Text.Json;

namespace Otium.Providers.XMRig;

internal sealed record XmrigSummary(double HashrateHps, bool PoolConnected, long AcceptedShares)
{
    /// <summary>Parses XMRig's /2/summary. Prefers the 60s average, falling back to 10s while it warms up.</summary>
    public static XmrigSummary Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        double hashrate = 0;
        if (root.TryGetProperty("hashrate", out var hr) && hr.TryGetProperty("total", out var total) && total.ValueKind == JsonValueKind.Array)
        {
            var values = total.EnumerateArray().Select(v => v.ValueKind == JsonValueKind.Number ? v.GetDouble() : (double?)null).ToArray();
            hashrate = (values.ElementAtOrDefault(1) ?? values.ElementAtOrDefault(0)) ?? 0;
        }

        var connected = root.TryGetProperty("connection", out var conn)
                        && conn.TryGetProperty("uptime", out var uptime)
                        && uptime.ValueKind == JsonValueKind.Number
                        && uptime.GetInt64() > 0;

        long accepted = 0;
        if (root.TryGetProperty("results", out var results) && results.TryGetProperty("shares_good", out var good) && good.ValueKind == JsonValueKind.Number)
            accepted = good.GetInt64();

        return new XmrigSummary(hashrate, connected, accepted);
    }
}
