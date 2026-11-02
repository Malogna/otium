using Otium.Core.Projections;

namespace Otium.Tests;

/// <summary>The hours a day the projection assumes: history, unless the computer is mining all the time right now.</summary>
public class ProjectedHoursTests
{
    [Fact]
    public void A_computer_that_mines_whenever_it_is_on_and_is_mining_now_is_not_held_to_a_history_of_barely_any_earning()
    {
        // 0.06 h/day measured over a week: mostly paused, then set to earn all the time (a real PC, November 2026).
        Assert.Null(Projection.HoursToProject(0.06, onlyWhenIdle: false, earningNow: true));

        var projection = Projection.Create(1.76, 0, Projection.HoursToProject(0.06, false, true), false, 0m, 5m);

        Assert.Equal(Projection.AlwaysOnHours, projection.HoursPerDay);
        Assert.Equal(1.76 * 16 / 24, projection.PerDay, 6);
        Assert.False(projection.HoursAreMeasured);
        Assert.Equal(projection.PerDay * 365, projection.PerYear, 6);
    }

    [Theory]
    [InlineData(20.0, false, true)]   // a long always-on history above the assumption keeps its own number
    [InlineData(0.06, true, true)]    // earns only while the user is away: that pattern is what is projected
    [InlineData(0.06, false, false)]  // not mining at this moment: the history is all there is to go on
    public void Everyone_else_keeps_the_hours_their_history_measured(double measured, bool onlyWhenIdle, bool earningNow) =>
        Assert.Equal(measured, Projection.HoursToProject(measured, onlyWhenIdle, earningNow));

    [Fact]
    public void Without_any_history_the_assumptions_apply()
    {
        Assert.Null(Projection.HoursToProject(null, onlyWhenIdle: false, earningNow: true));
        Assert.Equal(Projection.IdleOnlyHours, Projection.Create(1, 0, null, true, 0m, 5m).HoursPerDay);
        Assert.Equal(Projection.AlwaysOnHours, Projection.Create(1, 0, null, false, 0m, 5m).HoursPerDay);
    }
}
