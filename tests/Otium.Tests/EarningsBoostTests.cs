using Otium.Core.Engine;
using Otium.Core.Platform;

namespace Otium.Tests;

/// <summary>Keeping the computer awake while it earns, and large memory pages for the processor miner.</summary>
public sealed class EarningsBoostTests
{
    [Theory]
    [InlineData(EngineState.Earning, true)]
    [InlineData(EngineState.Starting, true)]
    [InlineData(EngineState.Installing, true)]
    [InlineData(EngineState.WaitingForIdle, false)] // the user is at the computer: their own sleep settings apply
    [InlineData(EngineState.PausedUnprofitable, false)]
    [InlineData(EngineState.Error, false)]
    [InlineData(EngineState.Off, false)]
    public void The_computer_stays_awake_only_while_a_miner_works(EngineState state, bool hold)
    {
        Assert.Equal(hold, KeepAwakePolicy.ShouldHold(enabled: true, [state, EngineState.Off], onBattery: false));
    }

    [Fact]
    public void Never_on_battery_or_when_switched_off()
    {
        Assert.False(KeepAwakePolicy.ShouldHold(enabled: true, [EngineState.Earning], onBattery: true));
        Assert.False(KeepAwakePolicy.ShouldHold(enabled: false, [EngineState.Earning], onBattery: false));
        Assert.True(KeepAwakePolicy.ShouldHold(enabled: true, [EngineState.WaitingForIdle, EngineState.Earning], onBattery: false));
    }

    [Fact]
    public void Keeping_awake_is_on_by_default() => Assert.True(new Otium.Core.Settings.AppSettings().KeepAwakeWhileEarning);

    [Fact]
    public void The_platform_inhibitor_holds_and_releases()
    {
        using var inhibitor = SleepInhibitor.ForThisPlatform();
        inhibitor.Hold();
        if (OperatingSystem.IsWindows())
            Assert.True(inhibitor.IsHeld); // a real power request, listed by "powercfg /requests"
        inhibitor.Hold(); // holding twice is harmless
        inhibitor.Release();
        Assert.False(inhibitor.IsHeld);
        inhibitor.Release();
    }

    [Fact]
    public void Large_pages_status_can_be_read_without_administrator_rights()
    {
        var exception = Record.Exception(() => LargePages.InEffect());
        Assert.Null(exception);
        Assert.Equal(OperatingSystem.IsWindows(), LargePages.IsSupported);
    }
}
