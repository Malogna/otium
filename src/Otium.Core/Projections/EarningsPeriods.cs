using System.Globalization;

namespace Otium.Core.Projections;

/// <summary>The range of the earnings chart on the Home screen.</summary>
public enum EarningsPeriod
{
    /// <summary>The last 24 hours, hour by hour.</summary>
    Day,

    /// <summary>The last 7 days.</summary>
    Week,

    /// <summary>The last 30 days.</summary>
    Month,

    /// <summary>The last 12 months.</summary>
    Year,
}

/// <summary>What the chart draws for one <see cref="EarningsPeriod"/>.</summary>
/// <param name="Bars">Earnings after fees per bar, oldest first (zeros where nothing was earned).</param>
/// <param name="Labels">A short label per bar ("14:00", "Tue", "12", "Oct"), same length as <paramref name="Bars"/>.</param>
/// <param name="Names">A longer name per bar for a tooltip ("Tuesday 13 October").</param>
/// <param name="TotalUsd">The sum of <paramref name="Bars"/>.</param>
/// <param name="ElectricityUsd">The electricity cost over the same range.</param>
public sealed record PeriodEarnings(
    EarningsPeriod Period,
    IReadOnlyList<double> Bars,
    IReadOnlyList<string> Labels,
    IReadOnlyList<string> Names,
    double TotalUsd,
    double ElectricityUsd)
{
    /// <summary>"in the last 24 hours" and the like, to follow the total.</summary>
    public string Range => Period switch
    {
        EarningsPeriod.Day => "in the last 24 hours",
        EarningsPeriod.Week => "in the last 7 days",
        EarningsPeriod.Month => "in the last 30 days",
        _ => "in the last 12 months",
    };

    /// <summary>"hour by hour" and the like, for the empty chart.</summary>
    public string Grain => Period switch
    {
        EarningsPeriod.Day => "hour by hour",
        EarningsPeriod.Week or EarningsPeriod.Month => "day by day",
        _ => "month by month",
    };
}

/// <summary>What was earned today, this week, this month and this year (each to date).</summary>
public sealed record EarningsToDate(double Today, double Week, double Month, double Year);

public static class EarningsPeriods
{
    public const int WeekDays = 7;

    public const int MonthDays = 30;

    public const int YearMonths = 12;

    /// <summary>The chart for <paramref name="period"/>, ending now. Labels follow <paramref name="culture"/>.</summary>
    public static PeriodEarnings Build(EarningsHistory history, EarningsPeriod period, DateTimeOffset now, CultureInfo culture)
    {
        history.BackfillDays();
        return period switch
        {
            EarningsPeriod.Day => Hours(history, now, culture),
            EarningsPeriod.Week => Days(history, now, WeekDays, EarningsPeriod.Week, culture),
            EarningsPeriod.Month => Days(history, now, MonthDays, EarningsPeriod.Month, culture),
            _ => Months(history, now, culture),
        };
    }

    /// <summary>
    /// Earned so far today, this week (from <paramref name="firstDayOfWeek"/>), this month and this year, in local time.
    /// The year comes from the monthly totals, which are kept for two years.
    /// </summary>
    public static EarningsToDate ToDate(EarningsHistory history, DateTimeOffset now, DayOfWeek firstDayOfWeek)
    {
        history.BackfillDays();
        var today = now.Date;
        var weekStart = today.AddDays(-(((int)today.DayOfWeek - (int)firstDayOfWeek + 7) % 7));
        var byDay = history.Days.ToDictionary(d => d.Day, d => d.EarnedUsd);
        double Day(DateTime date) => byDay.TryGetValue(DayBucket.KeyFor(date), out var v) ? v : 0;

        var week = 0.0;
        for (var d = weekStart; d <= today; d = d.AddDays(1))
            week += Day(d);
        var yearPrefix = now.ToString("yyyy", CultureInfo.InvariantCulture) + "-";
        var year = history.Months.Where(m => m.Month.StartsWith(yearPrefix, StringComparison.Ordinal)).Sum(m => m.EarnedUsd);
        return new EarningsToDate(Day(today), week, history.Month(MonthBucket.KeyFor(now))?.EarnedUsd ?? 0, year);
    }

    private static PeriodEarnings Hours(EarningsHistory history, DateTimeOffset now, CultureInfo culture)
    {
        const int count = 24;
        var bars = history.LastHours(now, count);
        var current = new DateTimeOffset(now.Year, now.Month, now.Day, now.Hour, 0, 0, now.Offset);
        var starts = Enumerable.Range(0, count).Select(i => current.AddHours(i - count + 1)).ToList();
        var cost = history.Hours.GroupBy(h => h.Hour.ToUniversalTime()).ToDictionary(g => g.Key, g => g.Sum(h => h.ElectricityUsd));
        return new PeriodEarnings(
            EarningsPeriod.Day,
            bars,
            starts.Select(s => s.ToString("HH", culture) + "h").ToList(),
            starts.Select(s => s.ToString("ddd HH':00'", culture)).ToList(),
            bars.Sum(),
            starts.Sum(s => cost.TryGetValue(s.ToUniversalTime(), out var c) ? c : 0));
    }

    private static PeriodEarnings Days(EarningsHistory history, DateTimeOffset now, int count, EarningsPeriod period, CultureInfo culture)
    {
        var earned = history.Days.ToDictionary(d => d.Day, d => d.EarnedUsd);
        var cost = history.Days.ToDictionary(d => d.Day, d => d.ElectricityUsd);
        var dates = Enumerable.Range(0, count).Select(i => now.Date.AddDays(i - count + 1)).ToList();
        var bars = dates.Select(d => earned.TryGetValue(DayBucket.KeyFor(d), out var v) ? v : 0).ToList();
        return new PeriodEarnings(
            period,
            bars,
            dates.Select(d => period == EarningsPeriod.Week ? d.ToString("ddd", culture) : d.ToString("%d", culture)).ToList(),
            dates.Select(d => d.ToString("dddd d MMMM", culture)).ToList(),
            bars.Sum(),
            dates.Sum(d => cost.TryGetValue(DayBucket.KeyFor(d), out var c) ? c : 0));
    }

    private static PeriodEarnings Months(EarningsHistory history, DateTimeOffset now, CultureInfo culture)
    {
        var first = new DateTime(now.Year, now.Month, 1);
        var months = Enumerable.Range(0, YearMonths).Select(i => first.AddMonths(i - YearMonths + 1)).ToList();
        var byMonth = history.Months.ToDictionary(m => m.Month);
        double Earned(DateTime m) => byMonth.TryGetValue(MonthBucket.KeyFor(new DateTimeOffset(m, TimeSpan.Zero)), out var b) ? b.EarnedUsd : 0;
        double Cost(DateTime m) => byMonth.TryGetValue(MonthBucket.KeyFor(new DateTimeOffset(m, TimeSpan.Zero)), out var b) ? b.ElectricityUsd : 0;
        var bars = months.Select(Earned).ToList();
        return new PeriodEarnings(
            EarningsPeriod.Year,
            bars,
            months.Select(m => m.ToString("MMM", culture)).ToList(),
            months.Select(m => m.ToString("MMMM yyyy", culture)).ToList(),
            bars.Sum(),
            months.Sum(Cost));
    }
}
