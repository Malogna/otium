using System.Globalization;

namespace Otium.Core.Projections;

/// <summary>Last month in numbers, shown for the first days of the new month and shareable with an invite.</summary>
public sealed record MonthlyReport(string Month, string MonthName, double EarnedUsd, double ElectricityUsd, double EarningHours)
{
    public double NetUsd => EarnedUsd - ElectricityUsd;

    /// <summary>Shown during the first days of a month, until dismissed.</summary>
    public const int ShownForDays = 10;

    /// <summary>
    /// Last month's report when it's worth showing: early in the new month, something was earned, and the user hasn't
    /// dismissed it. Null otherwise.
    /// </summary>
    public static MonthlyReport? Due(EarningsHistory history, DateTimeOffset now, string? dismissedMonth)
    {
        if (now.Day > ShownForDays)
            return null;
        var previous = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, now.Offset).AddMonths(-1);
        var key = MonthBucket.KeyFor(previous);
        if (key == dismissedMonth || history.Month(key) is not { EarnedUsd: >= 0.01 } month)
            return null;
        return new MonthlyReport(key, previous.ToString("MMMM", CultureInfo.InvariantCulture),
            month.EarnedUsd, month.ElectricityUsd, month.EarningSeconds / 3600);
    }
}
