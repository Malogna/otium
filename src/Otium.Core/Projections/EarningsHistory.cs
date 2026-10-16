namespace Otium.Core.Projections;

/// <summary>One hour of history: what was earned (after fees) and how long Otium was actively earning.</summary>
public sealed class HourBucket
{
    public DateTimeOffset Hour { get; set; }

    public double EarnedUsd { get; set; }

    public double ElectricityUsd { get; set; }

    public double EarningSeconds { get; set; }
}

/// <summary>
/// Local hour-by-hour record of earnings and active time (kept 14 days). Powers the 24-hour chart and the
/// projections, which use how many hours a day this computer actually earns, not a guess.
/// </summary>
public sealed class EarningsHistory
{
    public static readonly TimeSpan Retention = TimeSpan.FromDays(14);

    public List<HourBucket> Hours { get; set; } = [];

    /// <summary>Last cumulative totals seen, so each sample adds only what changed.</summary>
    public double LastCumulativeEarnedUsd { get; set; } = -1;

    public double LastCumulativeElectricityUsd { get; set; } = -1;

    public DateTimeOffset? FirstSampleAt { get; set; }

    /// <summary>
    /// Adds a sample. <paramref name="cumulativeEarnedUsd"/> and <paramref name="cumulativeElectricityUsd"/> are running
    /// totals (they only grow); <paramref name="earningFor"/> is how long Otium was earning since the previous sample.
    /// </summary>
    public void Record(DateTimeOffset now, double cumulativeEarnedUsd, double cumulativeElectricityUsd, TimeSpan earningFor)
    {
        FirstSampleAt ??= now;
        var earned = LastCumulativeEarnedUsd < 0 ? 0 : Math.Max(0, cumulativeEarnedUsd - LastCumulativeEarnedUsd);
        var electricity = LastCumulativeElectricityUsd < 0 ? 0 : Math.Max(0, cumulativeElectricityUsd - LastCumulativeElectricityUsd);
        LastCumulativeEarnedUsd = cumulativeEarnedUsd;
        LastCumulativeElectricityUsd = cumulativeElectricityUsd;

        var hour = new DateTimeOffset(now.Year, now.Month, now.Day, now.Hour, 0, 0, now.Offset);
        var bucket = Hours.LastOrDefault();
        if (bucket is null || bucket.Hour != hour)
        {
            bucket = new HourBucket { Hour = hour };
            Hours.Add(bucket);
        }
        bucket.EarnedUsd += earned;
        bucket.ElectricityUsd += electricity;
        bucket.EarningSeconds += Math.Max(0, earningFor.TotalSeconds);

        Hours.RemoveAll(h => h.Hour < now - Retention);
    }

    /// <summary>Earnings for each of the last <paramref name="count"/> hours, oldest first (zeros for empty hours).</summary>
    public IReadOnlyList<double> LastHours(DateTimeOffset now, int count)
    {
        var current = new DateTimeOffset(now.Year, now.Month, now.Day, now.Hour, 0, 0, now.Offset);
        // Grouped by UTC hour, so a time-zone or DST change can't create duplicate keys.
        var byHour = Hours.GroupBy(h => h.Hour.ToUniversalTime()).ToDictionary(g => g.Key, g => g.Aggregate(new HourBucket(), (acc, b) =>
        {
            acc.EarnedUsd += b.EarnedUsd;
            return acc;
        }));
        return Enumerable.Range(0, count)
            .Select(i => current.AddHours(i - count + 1).ToUniversalTime())
            .Select(h => byHour.TryGetValue(h, out var b) ? b.EarnedUsd : 0)
            .ToList();
    }

    public double EarnedSince(DateTimeOffset since) => Hours.Where(h => h.Hour >= since).Sum(h => h.EarnedUsd);

    /// <summary>
    /// Average hours per day this computer was earning, over the recorded days. Null until there is a full day of
    /// history (before that, projections use a sensible default).
    /// </summary>
    public double? AverageEarningHoursPerDay(DateTimeOffset now)
    {
        if (FirstSampleAt is not { } first || now - first < TimeSpan.FromDays(1))
            return null;
        var days = Math.Min((now - first).TotalDays, Retention.TotalDays);
        var seconds = Hours.Where(h => h.Hour >= now - TimeSpan.FromDays(days)).Sum(h => h.EarningSeconds);
        return seconds / 3600 / days;
    }
}
