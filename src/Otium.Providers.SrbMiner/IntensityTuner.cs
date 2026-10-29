namespace Otium.Providers.SrbMiner;

/// <summary>
/// What one intensity did for one algorithm on these cards, measured over at least <see cref="IntensityTuner.TrialLength"/>
/// of steady mining: the average hash rate and, when the power meter measures it, the average extra watts.
/// </summary>
/// <param name="Intensity">The intensity passed to SRBMiner, or null for the one it picks by itself.</param>
/// <param name="Failed">The miner stalled, crashed or had its shares rejected at this intensity.</param>
/// <param name="MinerPick">For the miner's own setting: the intensity it printed (the steps are tried around it).</param>
public sealed record IntensityTrial(string Algorithm, int? Intensity, double HashrateHps, double? ExtraWatts, DateTimeOffset At, bool Failed = false,
    int? MinerPick = null);

/// <summary>
/// Picks SRBMiner's intensity per algorithm by measuring, without administrator rights (NiceHash-style clock and power
/// tuning needs them). The miner's own pick isn't reliably its best: on an RTX 3080 it chose 23 for Quantus, where 21
/// hashed about 1.8 times as fast for about 1.7 times the power (October 2026). So while it mines, Otium tries a few
/// intensities around the miner's own pick, one at a time and spread out, measures each for ten minutes, and keeps the
/// one that earns the user the most after electricity (on free power: the fastest).
/// </summary>
public static class IntensityTuner
{
    /// <summary>A setting is judged on this much steady mining.</summary>
    public static readonly TimeSpan TrialLength = TimeSpan.FromMinutes(10);

    /// <summary>Measuring starts this long after a launch (and after the miner's own fee round).</summary>
    public static readonly TimeSpan Warmup = TimeSpan.FromMinutes(2);

    /// <summary>The current setting mines this long before another one is tried.</summary>
    public static readonly TimeSpan SteadyBeforeTrial = TimeSpan.FromMinutes(30);

    /// <summary>Trials are at least this far apart, so tuning stays a small part of the night.</summary>
    public static readonly TimeSpan BetweenTrials = TimeSpan.FromHours(2);

    /// <summary>Results this old are measured again (prices and drivers change).</summary>
    public static readonly TimeSpan Expiry = TimeSpan.FromDays(30);

    /// <summary>For algorithms the miner tunes by itself (no printed intensity): fixed steps to compare against it.</summary>
    private static readonly Dictionary<string, int[]> FixedSteps = new(StringComparer.OrdinalIgnoreCase)
    {
        ["pearlhash"] = [12, 16, 20, 24],
    };

    /// <summary>
    /// The settings worth comparing: the miner's own pick (null), then a little below and above the intensity it printed
    /// (<paramref name="autoIntensity"/>), or fixed steps for an algorithm it tunes by itself. None but its own pick for
    /// the rest.
    /// </summary>
    public static IReadOnlyList<int?> Candidates(string algorithm, int? autoIntensity)
    {
        IEnumerable<int> steps = autoIntensity is { } n
            ? [n - 2, n - 4, n + 2]
            : FixedSteps.TryGetValue(algorithm, out var fixedSteps) ? fixedSteps : [];
        return steps.Where(s => s is >= 1 and <= 31 && s != autoIntensity).Distinct().Select(s => (int?)s).Prepend(null).ToList();
    }

    /// <summary>The next setting to try: the first candidate not measured yet, or null when all have been.</summary>
    public static int? NextTrial(IReadOnlyList<int?> candidates, IReadOnlyList<IntensityTrial> trials, out bool any)
    {
        foreach (var candidate in candidates)
        {
            if (!trials.Any(t => t.Intensity == candidate))
            {
                any = true;
                return candidate;
            }
        }
        any = false;
        return null;
    }

    /// <summary>
    /// The best measured setting by <paramref name="netUsdPerDay"/> (hash rate and watts to what the user keeps a day),
    /// or by hash rate when any trial lacks a measured wattage; null (the miner's own pick) when nothing is measured.
    /// </summary>
    public static int? Best(IReadOnlyList<IntensityTrial> trials, Func<double, double?, double> netUsdPerDay)
    {
        var working = trials.Where(t => !t.Failed && t.HashrateHps > 0).ToList();
        if (working.Count == 0)
            return null;
        var byWatts = working.All(t => t.ExtraWatts is not null);
        return working.MaxBy(t => byWatts ? netUsdPerDay(t.HashrateHps, t.ExtraWatts) : t.HashrateHps)!.Intensity;
    }
}
