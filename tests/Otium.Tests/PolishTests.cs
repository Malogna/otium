using Otium.Core.Gpu;
using Otium.Core.Platform;

namespace Otium.Tests;

/// <summary>"Earn while I browse" and graphics card efficiency mode.</summary>
public sealed class PolishTests
{
    private sealed class Idle(TimeSpan? value) : IIdleDetector
    {
        public TimeSpan? GetIdleTime() => value;
    }

    private sealed class Foreground(ForegroundState state) : IForegroundMonitor
    {
        public ForegroundState Current() => state;
    }

    [Fact]
    public void Browsing_counts_as_away_only_when_switched_on_calm_and_on_mains_power()
    {
        var atDesk = new Idle(TimeSpan.FromSeconds(4));
        BrowsingIdleDetector Detector(bool on, ForegroundState state, bool battery = false) => new(atDesk, () => on, new Foreground(state), () => battery);

        Assert.Equal(BrowsingIdleDetector.AsIfAway, Detector(true, ForegroundState.Calm).GetIdleTime());
        Assert.Equal(TimeSpan.FromSeconds(4), Detector(false, ForegroundState.Calm).GetIdleTime()); // off by default
        Assert.Equal(TimeSpan.FromSeconds(4), Detector(true, ForegroundState.Busy).GetIdleTime()); // a game or full-screen app
        Assert.Equal(TimeSpan.FromSeconds(4), Detector(true, ForegroundState.Unknown).GetIdleTime());
        Assert.Equal(TimeSpan.FromSeconds(4), Detector(true, ForegroundState.Calm, battery: true).GetIdleTime());
    }

    [Theory]
    [InlineData(new[] { "--gpu-power", "cap", "70" }, true)]
    [InlineData(new[] { "--gpu-power", "restore" }, true)]
    [InlineData(new[] { "--gpu-power-setup" }, true)]
    [InlineData(new[] { "--gpu-power-remove" }, true)]
    [InlineData(new[] { "--grant-large-pages", "S-1-5-21" }, false)]
    [InlineData(new string[0], false)]
    public void Efficiency_mode_helpers_are_recognised_by_their_arguments(string[] args, bool helper) =>
        Assert.Equal(helper, GpuEfficiency.IsHelper(args));

    [Fact]
    public void An_unknown_helper_argument_does_nothing()
    {
        Assert.Equal(2, GpuEfficiency.RunHelper(["--gpu-power", "melt"], "Otium.exe"));
        Assert.Equal(70, GpuEfficiency.CapPercent);
    }
}
