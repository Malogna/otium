using Otium.Core.Engine;

namespace Otium.Tests;

/// <summary>
/// The one word per device the app sends with its check-in, so the admin dashboard can count where computers get stuck:
/// the user's own switches come first, then setup, antivirus and the engine's state.
/// </summary>
public class EngineStateTagsTests
{
    [Theory]
    [InlineData(EngineState.Earning, false, "earning")]
    [InlineData(EngineState.WaitingForIdle, false, "waiting-for-idle")]
    [InlineData(EngineState.PausedUnprofitable, false, "paused-electricity")]
    [InlineData(EngineState.Installing, false, "installing")]
    [InlineData(EngineState.Starting, false, "starting")]
    [InlineData(EngineState.Off, false, "starting")]
    [InlineData(EngineState.Error, false, "error")]
    [InlineData(EngineState.Error, true, "antivirus")]
    public void Each_engine_state_has_its_word(EngineState state, bool blocked, string tag)
    {
        Assert.Equal(tag, EngineStateTags.For(new EngineSnapshot { State = state, EngineBlocked = blocked }, enabled: true, earningOn: true, needsSetup: false));
        Assert.Contains(tag, EngineStateTags.Known);
    }

    [Fact]
    public void The_users_own_switches_and_setup_come_first()
    {
        var earning = new EngineSnapshot { State = EngineState.Earning };
        Assert.Equal("none", EngineStateTags.For(null, true, true, false)); // no such device
        Assert.Equal("switched-off", EngineStateTags.For(earning, enabled: false, earningOn: true, needsSetup: false));
        Assert.Equal("paused-by-user", EngineStateTags.For(earning, enabled: true, earningOn: false, needsSetup: false));
        Assert.Equal("needs-setup", EngineStateTags.For(earning, enabled: true, earningOn: true, needsSetup: true));
    }

    [Theory]
    [InlineData(EngineState.Error, "memory", "error-memory")]
    [InlineData(EngineState.Error, "failed", "error-failed")]
    [InlineData(EngineState.Error, "no-prices", "error-no-prices")]
    [InlineData(EngineState.Error, "nothing-pays", "error-nothing-pays")]
    [InlineData(EngineState.Starting, "pool-blocked", "error-pool-blocked")] // it keeps trying, but isn't mining
    [InlineData(EngineState.Error, "something-new", "error")] // a kind the server doesn't know stays plain "error"
    [InlineData(EngineState.Earning, "memory", "earning")] // mining again: the old problem no longer counts
    public void Errors_say_why_when_the_provider_names_the_problem(EngineState state, string kind, string tag)
    {
        Assert.Equal(tag, EngineStateTags.For(new EngineSnapshot { State = state, ProblemKind = kind }, enabled: true, earningOn: true, needsSetup: false));
        Assert.Contains(tag, EngineStateTags.Known); // so the server keeps it
    }

    [Theory]
    [InlineData(@"Could not find file 'C:\Users\Anna Smith\AppData\Local\Otium\engines\srb\x.dll'.", "Could not find file '<path>'.")]
    [InlineData("Access denied to /home/anna/.local/share/Otium/engines/x", "Access denied to <path>")]
    [InlineData(@"Share \\nas\anna\otium is unreachable", "Share <path> is unreachable")]
    [InlineData("The   card\r\n  stopped", "The card stopped")]
    [InlineData("A 50/50 split of 1/2 a card", "A 50/50 split of 1/2 a card")] // not paths
    public void Error_reasons_lose_file_paths_and_extra_whitespace(string message, string expected) =>
        Assert.Equal(expected, EngineStateTags.Scrub(message));

    [Fact]
    public void Error_reasons_are_short_and_only_sent_with_an_error()
    {
        var cut = EngineStateTags.Scrub(new string('x', 500))!;
        Assert.Equal(EngineStateTags.ReasonMaxLength, cut.Length);
        Assert.Null(EngineStateTags.Scrub("  "));
        var failed = new EngineSnapshot { State = EngineState.Error, StatusText = "Something went wrong: boom", Problem = null };
        Assert.Equal("Something went wrong: boom", EngineStateTags.ReasonFor(failed, "error"));
        Assert.Equal("Graphics card isn't mining: x", EngineStateTags.ReasonFor(new EngineSnapshot { State = EngineState.Error, StatusText = "t", Problem = "Graphics card isn't mining: x" }, "error-failed"));
        Assert.Null(EngineStateTags.ReasonFor(failed, "earning"));
        Assert.Null(EngineStateTags.ReasonFor(null, "error"));
    }
}
