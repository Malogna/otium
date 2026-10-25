using System.Runtime.InteropServices;
using System.Text.Json;
using Otium.Core.Bandwidth;
using Otium.Core.Platform;
using Otium.Core.Providers;
using Otium.Providers.Mysterium;

namespace Otium.Tests;

/// <summary>
/// Sharing through Mysterium on Windows, macOS and Linux, next to Proxybase: the pinned node per platform, its network
/// supervisor installed from a copy only administrators can change, and its MYST earnings shown in dollars.
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
    public void On_windows_the_supervisor_is_copied_under_program_files_before_installing()
    {
        // It runs as SYSTEM, so never from Otium's engines folder, which any program the user runs could rewrite.
        var command = WindowsMystSupervisor.InstallCommand(
            @"C:\Users\pat\AppData\Local\Otium\engines\myst-1.39.7\myst_supervisor.exe", @"C:\Program Files\Otium\Mysterium", "pat");

        Assert.StartsWith("/d /s /c \"", command);
        Assert.Contains(@"copy /y ""C:\Users\pat\AppData\Local\Otium\engines\myst-1.39.7\myst_supervisor.exe"" ""C:\Program Files\Otium\Mysterium\", command);
        Assert.Contains(@"\Otium\Mysterium\myst_supervisor.exe"" -install -uid ""pat""", command);
        Assert.True(command.IndexOf("copy", StringComparison.Ordinal) < command.IndexOf("-install", StringComparison.Ordinal));
    }

    [Fact]
    public void On_windows_wintun_is_copied_next_to_the_supervisor_too()
    {
        // Mysterium's node zip has no wintun.dll: without it every buyer session failed ("Error loading wintun.dll").
        var command = WindowsMystSupervisor.InstallCommand(
            @"C:\Users\pat\AppData\Local\Otium\engines\myst-1.39.7\myst_supervisor.exe", @"C:\Program Files\Otium\Mysterium", "pat");

        Assert.Contains(@"copy /y ""C:\Users\pat\AppData\Local\Otium\engines\myst-1.39.7\wintun.dll"" ""C:\Program Files\Otium\Mysterium\wintun.dll""", command);
        Assert.True(command.IndexOf("wintun.dll", StringComparison.Ordinal) < command.IndexOf("-install", StringComparison.Ordinal));
    }

    [Fact]
    public void Wintun_is_pinned_to_wireguards_release()
    {
        var w = MystRelease.Wintun;
        Assert.Equal("https://www.wintun.net/builds/wintun-0.14.1.zip", w.DownloadUrl.ToString());
        Assert.Equal("07c256185d6ee3652e09fa55c0b673e2624b565e02c4b9091c79ca7d2f24ef51", w.Sha256);
        Assert.Equal("e5da8447dc2c320edc0fc52fa01885c103de8c118481f683643cacc3220dafce", w.DllSha256);
        Assert.EndsWith(Path.Combine("wintun", "bin", "amd64", "wintun.dll"), WintunAsset.AmdDll("x"));
    }

    [Theory]
    [InlineData(@"C:\Windows\System32;C:\Program Files\PowerShell\7\", @"C:\Windows\System32;C:\Program Files\PowerShell\7\;C:\Windows\System32\WindowsPowerShell\v1.0")]
    [InlineData(@"C:\Windows\System32\WindowsPowerShell\v1.0\;C:\Windows", @"C:\Windows\System32\WindowsPowerShell\v1.0\;C:\Windows")] // already there
    [InlineData(@"C:\Windows;", @"C:\Windows;C:\Windows\System32\WindowsPowerShell\v1.0")]
    [InlineData(null, @"C:\Windows\System32\WindowsPowerShell\v1.0")]
    public void The_node_finds_windows_powershell(string? path, string expected) =>
        Assert.Equal(expected, MysteriumBandwidthProvider.WithWindowsPowerShell(path, @"C:\Windows\System32\WindowsPowerShell\v1.0"));

    [Fact]
    public void On_linux_the_supervisor_is_copied_to_a_root_owned_folder_through_pkexec()
    {
        var script = LinuxMystSupervisor.InstallScript("/home/pat/.local/share/Otium/engines/myst-1.39.7/myst_supervisor", 1000);
        var target = "'/usr/local/lib/otium-mysterium/myst_supervisor'";

        Assert.StartsWith("set -e; install -d -m 755 '/usr/local/lib/otium-mysterium'; ", script);
        Assert.Contains($"install -m 755 '/home/pat/.local/share/Otium/engines/myst-1.39.7/myst_supervisor' {target}", script);
        Assert.EndsWith($"{target} -install -uid 1000", script);
    }

    [Fact]
    public void On_macos_the_supervisor_is_copied_and_owned_by_root_with_the_password_prompt()
    {
        var script = MacMystSupervisor.InstallScript("/Users/pat/Library/Application Support/Otium/engines/myst-1.39.7/myst_supervisor", 501);
        var target = "'/Library/Application Support/Otium/Mysterium/myst_supervisor'";

        Assert.Contains($"/usr/sbin/chown root:wheel {target}", script);
        Assert.EndsWith($"{target} -install -uid 501", script);

        var apple = MacMystSupervisor.AppleScript("echo \"hi\"");
        Assert.StartsWith("do shell script \"echo \\\"hi\\\"\"", apple);
        Assert.EndsWith("with administrator privileges", apple);
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
