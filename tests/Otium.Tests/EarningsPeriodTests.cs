using System.Globalization;
using System.Text.Json;
using Otium.Core.Projections;

namespace Otium.Tests;

/// <summary>The day, week, month and year views of the earnings history on the Home screen.</summary>
public class EarningsPeriodTests
{
    // Thursday 15 October 2026.
    private static readonly DateTimeOffset Now = new(2026, 10, 15, 12, 30, 0, TimeSpan.Zero);

    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-US");

    /// <summary>Earns <paramref name="usd"/> at <paramref name="at"/> (a baseline sample first, so only the growth counts).</summary>
    private static void Earn(EarningsHistory h, DateTimeOffset at, double usd)
    {
        h.Record(at.AddMinutes(-1), h.LastCumulativeEarnedUsd < 0 ? 0 : h.LastCumulativeEarnedUsd, 0, TimeSpan.Zero);
        h.Record(at, h.LastCumulativeEarnedUsd + usd, 0, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void Each_day_gets_its_own_total()
    {
        var h = new EarningsHistory();
        Earn(h, Now.AddDays(-2), 1.00);
        Earn(h, Now.AddDays(-2).AddHours(1), 0.50);
        Earn(h, Now, 0.25);

        Assert.Equal(new[] { "2026-10-13", "2026-10-15" }, h.Days.Select(d => d.Day));
        Assert.Equal(new[] { 1.50, 0.25 }, h.Days.Select(d => Math.Round(d.EarnedUsd, 6)));
    }

    [Fact]
    public void History_from_before_daily_totals_is_filled_in_from_its_hours_once()
    {
        // A history file written by an older app: hours and months, no days.
        var json = JsonSerializer.Serialize(new
        {
            Hours = new[]
            {
                new { Hour = Now.AddDays(-1).Date.AddHours(9), EarnedUsd = 0.40, ElectricityUsd = 0.10, EarningSeconds = 3000.0 },
                new { Hour = Now.AddDays(-1).Date.AddHours(10), EarnedUsd = 0.20, ElectricityUsd = 0.05, EarningSeconds = 1000.0 },
                new { Hour = Now.Date.AddHours(8), EarnedUsd = 0.30, ElectricityUsd = 0.07, EarningSeconds = 2000.0 },
            },
        });
        var h = JsonSerializer.Deserialize<EarningsHistory>(json)!;
        Assert.Empty(h.Days);

        h.BackfillDays();
        h.BackfillDays(); // asking again must not double anything

        Assert.Equal(new[] { 0.60, 0.30 }, h.Days.Select(d => Math.Round(d.EarnedUsd, 6)));
        Assert.Equal(0.15, h.Days[0].ElectricityUsd, 6);
    }

    [Fact]
    public void Recording_on_an_old_history_keeps_the_days_it_already_had_in_its_hours()
    {
        var h = new EarningsHistory();
        h.Hours.Add(new HourBucket { Hour = Now.Date.AddHours(8), EarnedUsd = 0.30 });

        h.Record(Now, 0, 0, TimeSpan.Zero);

        Assert.Equal(0.30, Assert.Single(h.Days).EarnedUsd, 6);
    }

    [Fact]
    public void The_week_is_the_last_seven_days_ending_today()
    {
        var h = new EarningsHistory();
        Earn(h, Now.AddDays(-6), 1.0);
        Earn(h, Now.AddDays(-1), 2.0);
        Earn(h, Now, 3.0);

        var week = EarningsPeriods.Build(h, EarningsPeriod.Week, Now, English);

        Assert.Equal(7, week.Bars.Count);
        Assert.Equal(new[] { 1.0, 0, 0, 0, 0, 2.0, 3.0 }, week.Bars.Select(v => Math.Round(v, 6)));
        Assert.Equal(new[] { "Fri", "Sat", "Sun", "Mon", "Tue", "Wed", "Thu" }, week.Labels);
        Assert.Equal("Thursday 15 October", week.Names[^1]);
        Assert.Equal(6.0, week.TotalUsd, 6);
        Assert.Equal("in the last 7 days", week.Range);
    }

    [Fact]
    public void The_month_chart_shows_thirty_days_labelled_by_day_of_month()
    {
        var h = new EarningsHistory();
        Earn(h, Now.AddDays(-29), 4.0);
        Earn(h, Now.AddDays(-30), 9.0); // one day too old

        var month = EarningsPeriods.Build(h, EarningsPeriod.Month, Now, English);

        Assert.Equal(30, month.Bars.Count);
        Assert.Equal(4.0, month.Bars[0], 6);
        Assert.Equal(4.0, month.TotalUsd, 6);
        Assert.Equal("16", month.Labels[0]); // 29 days before 15 October
        Assert.Equal("15", month.Labels[^1]);
    }

    [Fact]
    public void The_year_chart_shows_twelve_months_ending_this_one()
    {
        var h = new EarningsHistory();
        h.Months.Add(new MonthBucket { Month = "2025-10", EarnedUsd = 7 }); // 13 months ago: left out
        h.Months.Add(new MonthBucket { Month = "2025-11", EarnedUsd = 2, ElectricityUsd = 0.5 });
        h.Months.Add(new MonthBucket { Month = "2026-10", EarnedUsd = 5, ElectricityUsd = 1 });

        var year = EarningsPeriods.Build(h, EarningsPeriod.Year, Now, English);

        Assert.Equal(12, year.Bars.Count);
        Assert.Equal("Nov", year.Labels[0]);
        Assert.Equal("Oct", year.Labels[^1]);
        Assert.Equal("November 2025", year.Names[0]);
        Assert.Equal(7.0, year.TotalUsd, 6);
        Assert.Equal(1.5, year.ElectricityUsd, 6);
    }

    [Fact]
    public void The_day_chart_is_the_last_24_hours()
    {
        var h = new EarningsHistory();
        Earn(h, Now, 0.5);

        var day = EarningsPeriods.Build(h, EarningsPeriod.Day, Now, English);

        Assert.Equal(24, day.Bars.Count);
        Assert.Equal(0.5, day.Bars[^1], 6);
        Assert.Equal("12h", day.Labels[^1]);
        Assert.Equal("13h", day.Labels[0]);
        Assert.Equal("Thu 12:00", day.Names[^1]);
    }

    [Theory]
    [InlineData(DayOfWeek.Monday, 3.0 + 2.0)]  // Mon 12 .. Thu 15
    [InlineData(DayOfWeek.Sunday, 3.0 + 2.0 + 1.0)] // Sun 11 .. Thu 15
    public void This_week_starts_on_the_cultures_first_day(DayOfWeek first, double expected)
    {
        var h = new EarningsHistory();
        Earn(h, Now.AddDays(-4), 1.0); // Sunday 11
        Earn(h, Now.AddDays(-1), 2.0); // Wednesday 14
        Earn(h, Now, 3.0);

        Assert.Equal(expected, EarningsPeriods.ToDate(h, Now, first).Week, 6);
    }

    [Fact]
    public void To_date_adds_up_today_this_month_and_this_year()
    {
        var h = new EarningsHistory();
        Earn(h, Now, 0.75);
        h.Months.Clear();
        h.Months.Add(new MonthBucket { Month = "2025-12", EarnedUsd = 100 }); // last year
        h.Months.Add(new MonthBucket { Month = "2026-01", EarnedUsd = 10 });
        h.Months.Add(new MonthBucket { Month = "2026-10", EarnedUsd = 5 });

        var total = EarningsPeriods.ToDate(h, Now, DayOfWeek.Monday);

        Assert.Equal(0.75, total.Today, 6);
        Assert.Equal(5, total.Month);
        Assert.Equal(15, total.Year);
    }

    [Fact]
    public void An_empty_history_gives_empty_charts_not_errors()
    {
        var h = new EarningsHistory();
        foreach (var period in Enum.GetValues<EarningsPeriod>())
        {
            var view = EarningsPeriods.Build(h, period, Now, English);
            Assert.All(view.Bars, v => Assert.Equal(0, v));
            Assert.Equal(view.Bars.Count, view.Labels.Count);
            Assert.Equal(view.Bars.Count, view.Names.Count);
            Assert.Equal(0, view.TotalUsd);
        }
    }

    [Fact]
    public void Daily_totals_are_kept_for_a_little_over_a_year()
    {
        var h = new EarningsHistory();
        for (var i = 0; i < EarningsHistory.DaysKept + 20; i++)
            h.Record(Now.AddDays(i), 0, 0, TimeSpan.Zero);

        Assert.Equal(EarningsHistory.DaysKept, h.Days.Count);
        Assert.Equal(DayBucket.KeyFor(Now.AddDays(EarningsHistory.DaysKept + 19)), h.Days[^1].Day);
    }
}
