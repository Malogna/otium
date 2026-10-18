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

    /// <summary>Accepted and rejected shares summed over algorithms[].shares, or null when the API has none yet.</summary>
    public static ShareCounts? ParseApiShares(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("algorithms", out var algorithms) || algorithms.ValueKind != JsonValueKind.Array)
            return null;
        long accepted = 0, rejected = 0;
        var found = false;
        foreach (var algo in algorithms.EnumerateArray())
        {
            if (!algo.TryGetProperty("shares", out var shares) || shares.ValueKind != JsonValueKind.Object)
                continue;
            if (shares.TryGetProperty("accepted", out var a) && TryNumber(a, out var acceptedCount))
            {
                accepted += (long)acceptedCount;
                found = true;
            }
            if (shares.TryGetProperty("rejected", out var r) && TryNumber(r, out var rejectedCount))
            {
                rejected += (long)rejectedCount;
                found = true;
            }
        }
        return found ? new ShareCounts(accepted, rejected) : null;
    }

    /// <summary>The share counts on the last console "Total" line ("Total: … [P:349.2W EFF: A:2 R:1 HW:0]"), or null.</summary>
    public static ShareCounts? ParseConsoleShares(IEnumerable<string> lines)
    {
        foreach (var line in lines.Reverse())
        {
            if (!line.Contains("Total", StringComparison.OrdinalIgnoreCase))
                continue;
            var match = ConsoleSharesRegex().Match(line);
            if (match.Success)
                return new ShareCounts(long.Parse(match.Groups["a"].Value, CultureInfo.InvariantCulture), long.Parse(match.Groups["r"].Value, CultureInfo.InvariantCulture));
        }
        return null;
    }

    /// <summary>
    /// Last hashrate printed on a console line, normalised to H/s. With <paramref name="notBefore"/>, a line whose
    /// "[yyyy-MM-dd HH:mm:ss]" timestamp (local time) is older counts as stale and gives null: a frozen miner stops
    /// both its API and its console, and its last "Total" line mustn't keep it looking busy.
    /// </summary>
    public static double? ParseConsoleHashrate(IEnumerable<string> lines, DateTime? notBefore = null)
    {
        foreach (var line in lines.Reverse())
        {
            var match = HashrateRegex().Match(line);
            if (!match.Success || !double.TryParse(match.Groups["value"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                continue;
            if (notBefore is { } cutoff && TimestampRegex().Match(line.TrimStart()) is { Success: true } stamp
                && DateTime.TryParseExact(stamp.Value.Trim().Trim('[', ']'), "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var at)
                && at < cutoff)
                return null;
            return value * Multiplier(match.Groups["unit"].Value);
        }
        return null;
    }

    /// <summary>
    /// True once SRBMiner has started its first developer-fee round ("Hold on, doing the first devfee mining round"
    /// in 3.7.6). It runs right after every launch for about 45 s, mining for the developer on the developer's pool,
    /// so any hashrate seen meanwhile says nothing about whether the card works for the user's pool.
    /// SRBMiner prints no "round finished" line, so callers time-box the round.
    /// </summary>
    public static bool StartedFirstDevFeeRound(IEnumerable<string> lines) =>
        lines.Any(l => l.Contains("first devfee mining round", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// The most telling problem in SRBMiner's recent output, in plain words for the app, or null if none is known.
    /// "Couldn't set epoch" (3.7.6) means the coin's dataset (DAG) couldn't be loaded onto the card. It isn't
    /// necessarily a memory shortage: on the RTX 3080 retest it happened with 10.9 GB of 12 GB free, so the app says
    /// only what failed (low free memory is reported separately, from NVML, before launch).
    /// </summary>
    public static string? DescribeProblem(IEnumerable<string> lines)
    {
        string? generic = null;
        foreach (var raw in lines.Reverse())
        {
            var line = Clean(raw);
            if (MemoryProblemRegex().IsMatch(line))
                return "it ran out of graphics memory (other apps, such as games or 3D editors, may be using it)";
            if (EpochProblemRegex().IsMatch(line))
                return $"the miner couldn't load this coin's dataset onto the graphics card (\"{line}\")";
            if (generic is null && ErrorRegex().IsMatch(line))
                generic = line;
        }
        return generic is null ? null : $"the miner reported \"{generic}\"";
    }

    /// <summary>Strips SRBMiner's "[2026-10-16 15:02:23] " timestamp and trailing " [0]" device tag.</summary>
    private static string Clean(string line)
    {
        var cleaned = TimestampRegex().Replace(line.Trim(), "");
        cleaned = DeviceTagRegex().Replace(cleaned, "");
        return cleaned.Length > 120 ? cleaned[..120] + "…" : cleaned;
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

    [GeneratedRegex(@"(?i)out of memory|not enough (gpu |video |graphics )?memory|memory allocation fail|MEM_OBJECT_ALLOCATION_FAILURE")]
    private static partial Regex MemoryProblemRegex();

    [GeneratedRegex(@"(?i)couldn'?t set epoch|dag.*(fail|error|couldn)")]
    private static partial Regex EpochProblemRegex();

    [GeneratedRegex(@"(?i)\berror\b|\bfailed\b|couldn'?t|can'?t|unable to")]
    private static partial Regex ErrorRegex();

    [GeneratedRegex(@"^\[\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\]\s*")]
    private static partial Regex TimestampRegex();

    [GeneratedRegex(@"\s*\[\d+\]$")]
    private static partial Regex DeviceTagRegex();

    [GeneratedRegex(@"\bA:(?<a>\d+)\s+R:(?<r>\d+)\b")]
    private static partial Regex ConsoleSharesRegex();
}
