using System.Runtime.InteropServices;
using System.Text.Json;
using Otium.Core.Bandwidth;
using Otium.Core.Platform;
using Otium.Core.Providers;
using Otium.Providers.Mysterium;

namespace Otium.Tests;

/// <summary>
/// Sharing through Mysterium on Windows, macOS and Linux, next to Proxybase: the pinned node per platform, run in
/// userspace mode (no administrator rights), and its MYST earnings shown in dollars.
/// </summary>
public class MysteriumPlatformTests
{
    [Theory]
    [InlineData("WINDOWS", Architecture.X64, "myst_windows_amd64.zip")]
    [InlineData("OSX", Architecture.Arm64, "myst_darwin_arm64.tar.gz")]
    [InlineData("OSX", Architecture.X64, "myst_darwin_amd64.tar.gz")]
    [InlineData("LINUX", Architecture.X64, "myst_linux_amd64.tar.gz")]
    [InlineData("LINUX", Architecture.Arm64, null)] // only .deb packages for ARM Linux
    [InlineData("WINDOWS", Architecture.Arm64, null)]
    public void Each_platform_gets_its_own_pinned_node(string os, Architecture arch, string? file)
    {
        var asset = MystRelease.For(OSPlatform.Create(os), arch);
        Assert.Equal(file, asset?.FileName);
        if (asset is not null)
        {
            Assert.Matches("^[0-9a-f]{64}$", asset.Sha256);
            Assert.Equal($"https://github.com/mysteriumnetwork/node/releases/download/{MystRelease.Version}/{file}", asset.DownloadUrl.ToString());
        }
    }

    [Fact]
    public void The_node_runs_in_userspace_so_it_needs_no_administrator_rights()
    {
        // In "--usermode" every buyer session failed on a home PC: forwarding (Windows connection sharing, iptables,
        // pfctl) needs administrator rights the node doesn't have. "--userspace" runs WireGuard and forwarding inside it.
        var args = MysteriumBandwidthProvider.DaemonArguments("/data", 41234, "secret");
        Assert.Contains("--userspace", args);
        Assert.DoesNotContain("--usermode", args);
    }

    [Fact]
    public void Removing_the_old_service_on_macos_asks_for_the_password()
    {
        var script = MacMystSupervisor.AppleScript("echo \"hi\"");
        Assert.StartsWith("do shell script \"echo \\\"hi\\\"\"", script);
        Assert.EndsWith("with administrator privileges", script);
    }

    [Fact]
    public void Paths_with_quotes_stay_one_shell_word()
    {
        Assert.Equal("'/Users/o'\\''brien/x'", MystSupervisors.ShellQuote("/Users/o'brien/x"));
    }

    [Fact]
    public void Earnings_turn_into_dollars_after_mysteriums_fee()
    {
        var start = new DateTimeOffset(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);
        var earned = MystEarned.Empty.With(start, 1.0m, null);
        Assert.Null(earned.UserUsd); // no price yet

        earned = earned.With(start.AddHours(12), 2.0m, 0.08);
        Assert.Equal(2.0 * 0.8 * 0.08, earned.UserUsd!.Value, 9);
        Assert.Equal(1.0 * 0.8 * 0.08 / 12 * 24, earned.UserUsdPerDay(start.AddHours(12))!.Value, 9);

        // The server had no price this time: the last one stays.
        var later = earned.With(start.AddHours(13), 2.5m, null);
        Assert.Equal(0.08, later.MystUsd);
        Assert.Same(later.Samples, later.With(start.AddHours(13.5), 2.5m, null).Samples); // nothing new within the hour

        var restored = JsonSerializer.Deserialize<MystEarned>(JsonSerializer.Serialize(later))!;
        Assert.Equal(later.UserUsd, restored.UserUsd);
    }

    [Fact]
    public async Task An_antivirus_block_is_flagged_so_the_overview_offers_to_allow_it()
    {
        var time = new ManualTime(new DateTimeOffset(2026, 10, 25, 12, 0, 0, TimeSpan.Zero));
        var config = new BandwidthConfig(true, false, TimeSpan.FromMinutes(3), 1024L * 1024 * 1024, 0, 0, "");
        await using var sharer = new BandwidthSharer(new BlockedProvider(), new AlwaysAway(), () => config, report: null, new BandwidthLedger(), _ => { }, time);
        sharer.Enable();
        sharer.Start();
        for (var i = 0; i < 50 && !sharer.Snapshot.EngineBlocked; i++)
            await Task.Delay(100);

        Assert.True(sharer.Snapshot.EngineBlocked);
        Assert.Equal(BandwidthState.Error, sharer.Snapshot.State);
    }

    private sealed class BlockedProvider : IBandwidthProvider
    {
        public string Id => "mysterium";

        public string DisplayName => "Blocked partner";

        public string? UnavailableReason => null;

        public bool IsInstalled => false;

        public Task InstallAsync(IProgress<InstallProgress>? progress, CancellationToken ct) =>
            throw new EngineBlockedException("Mysterium node 1.39.7 was removed or blocked by antivirus software.", "engines/myst-1.39.7");

        public Task StartAsync(BandwidthRunOptions options, CancellationToken ct) => Task.CompletedTask;

        public Task StopAsync(CancellationToken ct) => Task.CompletedTask;

        public Task<BandwidthStatus> GetStatusAsync(CancellationToken ct) => Task.FromResult(new BandwidthStatus(BandwidthProviderState.Stopped, 0));

        public Task OptInAsync(CancellationToken ct) => Task.CompletedTask;

        public Task OptOutAsync(CancellationToken ct) => Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class AlwaysAway : IIdleDetector
    {
        public TimeSpan? GetIdleTime() => TimeSpan.FromHours(1);
    }
}
