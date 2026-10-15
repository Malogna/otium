using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Otium.Providers.SrbMiner;

/// <summary>
/// Reads SRBMiner's statistics. SRBMiner doesn't document its JSON schema, so the parser accepts the shapes
/// community tools read (algorithms[].hashrate.gpu.total in H/s) plus a few fallbacks, and can also read the
/// hashrate from console output if the API is unavailable.
/// </summary>
internal static partial class SrbMinerStats
{
    public static double? ParseApiHashrate(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (root.TryGetProperty("algorithms", out var algorithms) && algorithms.ValueKind == JsonValueKind.Array)
        {
            double total = 0;
            var found = false;
            foreach (var algo in algorithms.EnumerateArray())
            {
                if (!algo.TryGetProperty("hashrate", out var hashrate))
                    continue;
                if (TryNumber(hashrate, out var direct))
                {
                    total += direct;
                    found = true;
                }
                else if (hashrate.TryGetProperty("gpu", out var gpu) && TryTotal(gpu, out var gpuTotal))
                {
                    total += gpuTotal;
                    found = true;
                }
                else if (TryTotal(hashrate, out var anyTotal))
                {
                    total += anyTotal;
                    found = true;
                }
            }
            if (found)
                return total;
        }

        foreach (var key in new[] { "hashrate_total_now", "total_hashrate", "hashrate" })
        {
            if (root.TryGetProperty(key, out var value) && (TryNumber(value, out var n) || TryTotal(value, out n)))
                return n;
        }

        return null;
    }

    /// <summary>Last hashrate printed on a console line, normalised to H/s.</summary>
    public static double? ParseConsoleHashrate(IEnumerable<string> lines)
    {
        foreach (var line in lines.Reverse())
        {
            var match = HashrateRegex().Match(line);
            if (match.Success && double.TryParse(match.Groups["value"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                return value * Multiplier(match.Groups["unit"].Value);
        }
        return null;
    }

    private static double Multiplier(string unit) => unit.ToUpperInvariant() switch
    {
        "K" => 1e3,
        "M" => 1e6,
        "G" => 1e9,
        "T" => 1e12,
        "P" => 1e15,
        _ => 1,
    };

    private static bool TryTotal(JsonElement element, out double value)
    {
        value = 0;
        return element.ValueKind == JsonValueKind.Object
               && element.TryGetProperty("total", out var total)
               && TryNumber(total, out value);
    }

    private static bool TryNumber(JsonElement element, out double value)
    {
        value = 0;
        if (element.ValueKind == JsonValueKind.Number)
        {
            value = element.GetDouble();
            return true;
        }
        return element.ValueKind == JsonValueKind.String
               && double.TryParse(element.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    [GeneratedRegex(@"(?i)total[^0-9]*(?<value>[0-9]+(?:\.[0-9]+)?)\s*(?<unit>[kMGTP]?)H/s")]
    private static partial Regex HashrateRegex();
}
