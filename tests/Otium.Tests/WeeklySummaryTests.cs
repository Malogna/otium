using Otium.Core.Engine;
using Otium.Core.Projections;

namespace Otium.Tests;

public class WeeklySummaryTests
{
    // Wednesday 11 November 2026 12:00, so last week is Monday 2 to Sunday 8 November.
    private static readonly DateTimeOffset Wednesday = new(2026, 11, 11, 12, 0, 0, TimeSpan.Zero);

    private static EarningsHistory With(params (string Day, double Usd, double Power, double Seconds)[] days)
    {
        var history = new EarningsHistory();
        foreach (var d in days)
            history.Days.Add(new DayBucket { Day = d.Day, EarnedUsd = d.Usd, ElectricityUsd = d.Power, EarningSeconds = d.Seconds });
        history.FirstSampleAt = Wednesday.AddDays(-30);
        return history;
    }

    [Fact]
    public void Last_weeks_days_are_added_up_early_in_the_new_week()
    {
        var history = With(("2026-11-01", 5, 0, 3600), ("2026-11-02", 0.40, 0.10, 7200), ("2026-11-08", 0.60, 0.20, 3600), ("2026-11-09", 9, 0, 3600));

        var week = WeeklySummary.Due(history, Wednesday, null);

        Assert.NotNull(week);
        Assert.Equal("2026-W45", week.Week);
        Assert.Equal(1.00, week.EarnedUsd, 9); // only the Monday and the Sunday are last week
        Assert.Equal(0.30, week.ElectricityUsd, 9);
        Assert.Equal(3, week.EarningHours, 9);
    }

    [Fact]
    public void A_week_is_announced_once()
    {
        var history = With(("2026-11-03", 1, 0, 3600));

        Assert.Null(WeeklySummary.Due(history, Wednesday, "2026-W45"));
        Assert.NotNull(WeeklySummary.Due(history, Wednesday, "2026-W44"));
    }

    [Fact]
    public void Later_in_the_week_the_summary_is_stale()
    {
        var history = With(("2026-11-03", 1, 0, 3600));

        Assert.NotNull(WeeklySummary.Due(history, Wednesday.AddDays(1), null)); // Thursday
        Assert.Null(WeeklySummary.Due(history, Wednesday.AddDays(2), null)); // Friday
    }

    [Fact]
    public void A_week_with_nothing_earned_says_nothing()
    {
        Assert.Null(WeeklySummary.Due(With(("2026-11-03", 0.004, 0, 60)), Wednesday, null));
        Assert.Null(WeeklySummary.Due(new EarningsHistory(), Wednesday, null));
    }

    [Fact]
    public void Weeks_follow_iso_numbering_across_the_new_year()
    {
        Assert.Equal("2026-W53", WeeklySummary.KeyFor(new DateTime(2026, 12, 28)));
        Assert.Equal("2027-W01", WeeklySummary.KeyFor(new DateTime(2027, 1, 4)));
        var history = With(("2026-12-29", 2, 0, 3600));
        Assert.Equal("2026-W53", WeeklySummary.Due(history, new DateTimeOffset(2027, 1, 5, 9, 0, 0, TimeSpan.Zero), null)?.Week);
    }

    [Theory]
    [InlineData(false, false, 1, true)]
    [InlineData(false, true, 0, true)]
    [InlineData(false, true, 1, false)]
    [InlineData(true, false, 0, false)]
    [InlineData(true, true, 1, false)]
    public void Nothing_running_is_explained_only_when_the_user_can_fix_it(bool cpu, bool gpu, int gpus, bool explained) =>
        Assert.Equal(explained, NothingRunningReason.Explain(cpu, gpu, gpus) is not null);
}
