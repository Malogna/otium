using System.Globalization;

namespace Otium.Core.Projections;

/// <summary>
/// Last week (Monday to Sunday) in numbers, for the one notification a week that shows earning is still going. It comes
/// during the first days of the new week and only once per week, whether or not the app was open on Monday.
/// </summary>
public sealed record WeeklySummary(string Week, double EarnedUsd, double ElectricityUsd, double EarningHours)
{
    /// <summary>A week's summary is announced up to this many days into the next week (Monday is day 0), then it's stale.</summary>
    public const int ShownForDays = 3;

    /// <summary>The week's key, "2026-W45" (ISO weeks, which start on Monday).</summary>
    public static string KeyFor(DateTime date) =>
        FormattableString.Invariant($"{ISOWeek.GetYear(date)}-W{ISOWeek.GetWeekOfYear(date):00}");

    /// <summary>
    /// Last week's summary when it's worth announcing: early in the new week, something was earned and it wasn't
    /// announced before. Null otherwise.
    /// </summary>
    public static WeeklySummary? Due(EarningsHistory history, DateTimeOffset now, string? notifiedWeek)
    {
        history.BackfillDays();
        var today = now.Date;
        var thisMonday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
        if ((today - thisMonday).Days > ShownForDays)
            return null;
        var lastMonday = thisMonday.AddDays(-7);
        var key = KeyFor(lastMonday);
        if (key == notifiedWeek)
            return null;

        var keys = Enumerable.Range(0, 7).Select(i => DayBucket.KeyFor(lastMonday.AddDays(i))).ToHashSet(StringComparer.Ordinal);
        var days = history.Days.Where(d => keys.Contains(d.Day)).ToList();
        var earned = days.Sum(d => d.EarnedUsd);
        return earned >= 0.01
            ? new WeeklySummary(key, earned, days.Sum(d => d.ElectricityUsd), days.Sum(d => d.EarningSeconds) / 3600)
            : null;
    }
}
