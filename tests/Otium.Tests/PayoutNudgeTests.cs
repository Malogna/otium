using Otium.Core.Engine;
using Otium.Core.Projections;

namespace Otium.Tests;

public class PayoutNudgeTests
{
    [Theory]
    [InlineData(0.3, 1.5, false, PayoutNudge.Action.None)] // early: nothing to say
    [InlineData(1.19, 1.5, false, PayoutNudge.Action.None)] // just under four fifths
    [InlineData(1.2, 1.5, false, PayoutNudge.Action.Notify)]
    [InlineData(1.45, 1.5, false, PayoutNudge.Action.Notify)]
    [InlineData(1.5, 1.5, false, PayoutNudge.Action.None)] // reached: the payout notification takes over
    [InlineData(1.3, 1.5, true, PayoutNudge.Action.None)] // told once already
    [InlineData(0.9, 1.5, true, PayoutNudge.Action.None)] // still above half
    [InlineData(0.2, 1.5, true, PayoutNudge.Action.Reset)] // paid out: ready for the next one
    [InlineData(1, 0, false, PayoutNudge.Action.None)] // no known threshold
    public void Close_to_a_payout_is_announced_once_per_payout(double pending, double threshold, bool notified, PayoutNudge.Action expected) =>
        Assert.Equal(expected, PayoutNudge.Check(pending, threshold, notified));

    [Fact]
    public void An_error_is_reported_only_once_it_has_gone_on_for_a_while()
    {
        var grace = new ErrorGrace();
        var t = new DateTimeOffset(2026, 11, 6, 12, 0, 0, TimeSpan.Zero);

        // One stop (Windows ending the miner at shutdown): the next check-in doesn't call it an error.
        grace.Observe("cpu", "error", t);
        Assert.Equal("starting", grace.Report("cpu", "error", t.AddSeconds(50)));
        Assert.Equal("earning", grace.Report("cpu", "earning", t.AddSeconds(50)));

        // A miner that keeps failing (an error for a minute, starting again, an error again) is reported after the grace.
        for (var minute = 1; minute <= 6; minute++)
            grace.Observe("cpu", minute % 2 == 0 ? "starting" : "error", t.AddMinutes(minute));
        Assert.Equal("error", grace.Report("cpu", "error", t.AddMinutes(6)));
        Assert.Equal("error-memory", grace.Report("cpu", "error-memory", t.AddMinutes(6)));

        // Another device's errors don't count for this one, and a long quiet spell starts the count again.
        Assert.Equal("starting", grace.Report("gpu", "error", t.AddMinutes(6)));
        Assert.Equal("starting", grace.Report("cpu", "error", t.AddMinutes(30)));
        grace.Observe("cpu", "error", t.AddMinutes(30));
        Assert.Equal("starting", grace.Report("cpu", "error", t.AddMinutes(31)));
    }
}
