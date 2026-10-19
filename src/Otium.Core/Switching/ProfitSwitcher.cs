namespace Otium.Core.Switching;

/// <summary>A measured option: how fast this hardware runs an algorithm and how much extra power it draws doing so.</summary>
/// <param name="FailedTimes">For a failed test: how many tests in a row have failed, which sets when it's tried again.</param>
public sealed record BenchmarkResult(string Algorithm, double HashrateHps, double? ExtraWatts, DateTimeOffset MeasuredAt, string? Failure = null,
    int FailedTimes = 0)
{
    public bool Succeeded => Failure is null && HashrateHps > 0;

    /// <summary>
    /// A test can fail for a reason that passes (the driver compiling a coin's code on first launch, a busy computer),
    /// so a failure is tried again after 6 hours, then 12, 24… up to a week. A success stands.
    /// </summary>
    public bool IsCurrent(DateTimeOffset now) =>
        Succeeded || now - MeasuredAt < TimeSpan.FromHours(6 * Math.Pow(2, Math.Clamp(FailedTimes - 1, 0, 5)));
}

/// <param name="RevenuePerHashPerSecondDay">Earned per day per 1 H/s, in the unit priced by the coin price passed to Score.</param>
/// <param name="DevFeePercent">Miner developer fee for this algorithm.</param>
/// <param name="PoolFeePercent">This algorithm's pool fee; null uses the switcher's default.</param>
public sealed record AlgorithmQuote(string Algorithm, double RevenuePerHashPerSecondDay, double DevFeePercent, double? PoolFeePercent = null);

public sealed record ScoredOption(string Algorithm, double HashrateHps, double GrossUsdPerDay, double ElectricityUsdPerDay, double? ExtraWatts)
{
    public double NetUsdPerDay => GrossUsdPerDay - ElectricityUsdPerDay;
}

/// <summary>
/// Picks the most profitable algorithm from benchmarks and live per-algorithm payouts, after miner dev fees,
/// pool fee and electricity. Hysteresis and a minimum hold time stop it flapping between near-equal options,
/// since every switch costs a restart (and DAG generation for some algorithms).
/// </summary>
public sealed class ProfitSwitcher
{
    public double SwitchMarginFraction { get; init; } = 0.05;

    public double SwitchMarginUsd { get; init; } = 0.01;

    public TimeSpan MinimumHold { get; init; } = TimeSpan.FromMinutes(15);

    public double PoolFeePercent { get; init; } = 1.0;

    public IReadOnlyList<ScoredOption> Score(
        IEnumerable<BenchmarkResult> benchmarks,
        IEnumerable<AlgorithmQuote> quotes,
        double coinPriceUsd,
        double pricePerKwh,
        double fallbackWatts)
    {
        var quoteByAlgo = quotes.ToDictionary(q => q.Algorithm, StringComparer.OrdinalIgnoreCase);
        var options = new List<ScoredOption>();
        foreach (var b in benchmarks.Where(b => b.Succeeded))
        {
            if (!quoteByAlgo.TryGetValue(b.Algorithm, out var quote) || quote.RevenuePerHashPerSecondDay <= 0)
                continue;
            var coins = b.HashrateHps * quote.RevenuePerHashPerSecondDay * (1 - quote.DevFeePercent / 100) * (1 - (quote.PoolFeePercent ?? PoolFeePercent) / 100);
            var watts = b.ExtraWatts ?? fallbackWatts;
            options.Add(new ScoredOption(b.Algorithm, b.HashrateHps, coins * coinPriceUsd, Power.Electricity.CostPerDay(watts, pricePerKwh), b.ExtraWatts));
        }
        return options.OrderByDescending(o => o.NetUsdPerDay).ToList();
    }

    /// <summary>
    /// Returns the algorithm to mine next. Keeps <paramref name="current"/> unless it has been held long enough
    /// and the best alternative beats it by the configured margin.
    /// </summary>
    public string? Choose(IReadOnlyList<ScoredOption> scored, string? current, TimeSpan heldFor)
    {
        if (scored.Count == 0)
            return null;

        var best = scored[0];
        var currentOption = scored.FirstOrDefault(o => string.Equals(o.Algorithm, current, StringComparison.OrdinalIgnoreCase));
        if (currentOption is null)
            return best.Algorithm;
        if (heldFor < MinimumHold || best.Algorithm == currentOption.Algorithm)
            return currentOption.Algorithm;

        var threshold = currentOption.NetUsdPerDay + Math.Max(SwitchMarginUsd, Math.Abs(currentOption.NetUsdPerDay) * SwitchMarginFraction);
        return best.NetUsdPerDay > threshold ? best.Algorithm : currentOption.Algorithm;
    }
}
