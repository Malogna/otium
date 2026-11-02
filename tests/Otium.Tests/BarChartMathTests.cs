using Otium.Core.Projections;

namespace Otium.Tests;

/// <summary>The arithmetic behind the earnings chart: morphing between ranges, picking a bar, spacing labels.</summary>
public class BarChartMathTests
{
    [Fact]
    public void Resampling_to_the_same_number_of_bars_changes_nothing()
    {
        var bars = new[] { 0.2, 0.9, 0.5 };

        Assert.Equal(bars, BarChartMath.Resample(bars, 3));
    }

    [Fact]
    public void Twenty_four_hourly_bars_squeeze_into_seven_by_position()
    {
        var hours = Enumerable.Range(0, 24).Select(i => i < 12 ? 0.0 : 1.0).ToArray(); // quiet morning, busy afternoon

        var week = BarChartMath.Resample(hours, 7);

        Assert.Equal(7, week.Length);
        Assert.Equal(0, week[0], 6);
        Assert.Equal(1, week[6], 6);
        Assert.True(week.Zip(week.Skip(1), (a, b) => b >= a).All(x => x)); // the shape is kept: it only rises
    }

    [Fact]
    public void Seven_bars_spread_out_to_thirty_and_stay_between_their_neighbours()
    {
        var week = new[] { 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 1.0 };

        var month = BarChartMath.Resample(week, 30);

        Assert.Equal(30, month.Length);
        Assert.All(month, v => Assert.InRange(v, 0, 1));
        Assert.Equal(1, month[^1], 6);
        Assert.Equal(0, month[0], 6);
    }

    [Fact]
    public void Nothing_to_morph_from_means_the_bars_start_at_zero()
    {
        Assert.Equal(new double[5], BarChartMath.Resample([], 5));
        Assert.Empty(BarChartMath.Resample([0.5], 0));
        Assert.Equal(new[] { 0.5, 0.5, 0.5 }, BarChartMath.Resample([0.5], 3));
    }

    [Fact]
    public void Bars_set_off_one_after_another_and_all_arrive_together()
    {
        const int count = 10;

        Assert.Equal(0, BarChartMath.GrowthOf(0, 0, count));
        Assert.Equal(0, BarChartMath.GrowthOf(0, count - 1, count));
        // Part way, the first bar is further along than the last.
        Assert.True(BarChartMath.GrowthOf(0.4, 0, count) > BarChartMath.GrowthOf(0.4, count - 1, count));
        for (var i = 0; i < count; i++)
            Assert.Equal(1, BarChartMath.GrowthOf(1, i, count), 9);
        Assert.Equal(1, BarChartMath.GrowthOf(1, 0, 1)); // a single bar has nothing to wait for
        Assert.Equal(0, BarChartMath.GrowthOf(-5, 3, count)); // progress is clamped
        Assert.Equal(1, BarChartMath.GrowthOf(5, 3, count));
    }

    [Fact]
    public void Labels_are_thinned_to_leave_room()
    {
        Assert.Equal(1, BarChartMath.LabelStep(600, 7));    // wide bars: every label fits
        Assert.True(BarChartMath.LabelStep(600, 24) > 1);   // narrow bars: every few
        Assert.True(BarChartMath.LabelStep(300, 30) > BarChartMath.LabelStep(600, 30));
        Assert.Equal(1, BarChartMath.LabelStep(600, 1));
    }

    [Fact]
    public void The_pointer_finds_the_bar_under_it_and_nothing_between_or_outside()
    {
        // 3 bars in 100 px: each (100 - 2 * 4) / 3 = 30.67 wide, 4 apart.
        Assert.Equal(0, BarChartMath.BarAt(10, 100, 3));
        Assert.Equal(1, BarChartMath.BarAt(50, 100, 3));
        Assert.Equal(2, BarChartMath.BarAt(95, 100, 3));
        Assert.Equal(-1, BarChartMath.BarAt(-1, 100, 3));
        Assert.Equal(-1, BarChartMath.BarAt(101, 100, 3));
        Assert.Equal(-1, BarChartMath.BarAt(10, 100, 0));
        Assert.Equal(-1, BarChartMath.BarAt(10, 0, 3));
    }
}
