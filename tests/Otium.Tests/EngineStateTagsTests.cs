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
}
