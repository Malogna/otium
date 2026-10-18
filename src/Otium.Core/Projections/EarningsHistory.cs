namespace Otium.Core.Projections;

/// <summary>One hour of history: what was earned (after fees) and how long Otium was actively earning.</summary>
public sealed class HourBucket
{
    public DateTimeOffset Hour { get; set; }

    public double EarnedUsd { get; set; }

    public double ElectricityUsd { get; set; }

    public double EarningSeconds { get; set; }
}

/// <summary>One calendar month (local time): what was earned and how long Otium was earning.</summary>
public sealed class MonthBucket
{
    /// <summary>"2026-10".</summary>
    public string Month { get; set; } = "";

    public double EarnedUsd { get; set; }

    public double ElectricityUsd { get; set; }

    public double EarningSeconds { get; set; }

    public static string KeyFor(DateTimeOffset localTime) =>
        localTime.ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture);
}

/// <summary>
/// Local hour-by-hour record of earnings and active time (kept 14 days), and month-by-month totals (kept two years).
/// Powers the 24-hour chart, the monthly report and the projections, which use how many hours a day this computer
/// actually earns, not a guess.
/// </summary>
public sealed class EarningsHistory
{
    public static readonly TimeSpan Retention = TimeSpan.FromDays(14);

    public const int MonthsKept = 24;

    public List<HourBucket> Hours { get; set; } = [];

    public List<MonthBucket> Months { get; set; } = [];

    public MonthBucket? Month(string key) => Months.FirstOrDefault(m => m.Month == key);

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

        var key = MonthBucket.KeyFor(now);
        var month = Month(key);
        if (month is null)
        {
            month = new MonthBucket { Month = key };
            Months.Add(month);
            Months.Sort((a, b) => string.CompareOrdinal(a.Month, b.Month));
        }
        month.EarnedUsd += earned;
        month.ElectricityUsd += electricity;
        month.EarningSeconds += Math.Max(0, earningFor.TotalSeconds);

        Hours.RemoveAll(h => h.Hour < now - Retention);
        if (Months.Count > MonthsKept)
            Months.RemoveRange(0, Months.Count - MonthsKept);
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
