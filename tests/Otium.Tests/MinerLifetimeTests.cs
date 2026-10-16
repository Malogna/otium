using System.Diagnostics;
using Otium.Core.Providers;

namespace Otium.Tests;

/// <summary>
/// Uses a harmless stand-in (a copy of ping on Windows, sleep elsewhere) named like XMRig, so no real miner runs.
/// </summary>
public sealed class MinerLifetimeTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"sc-life-{Guid.NewGuid():N}");
    private readonly List<Process> _started = [];

    private string EnginesRoot => Path.Combine(_dir, "engines");

    private string LockFile => Path.Combine(_dir, "otium.lock");

    private Process StartStandIn(string folder)
    {
        Directory.CreateDirectory(folder);
        ProcessStartInfo start;
        if (OperatingSystem.IsWindows())
        {
            var exe = Path.Combine(folder, "xmrig.exe");
            File.Copy(Path.Combine(Environment.SystemDirectory, "PING.EXE"), exe);
            start = new ProcessStartInfo(exe, "-n 120 127.0.0.1");
        }
        else
        {
            var exe = Path.Combine(folder, "xmrig");
            File.Copy(File.Exists("/bin/sleep") ? "/bin/sleep" : "/usr/bin/sleep", exe);
            File.SetUnixFileMode(exe, UnixFileMode.UserRead | UnixFileMode.UserExecute | UnixFileMode.UserWrite);
            start = new ProcessStartInfo(exe, "120");
        }
        start.UseShellExecute = false;
        start.CreateNoWindow = true;
        start.RedirectStandardOutput = true;
        var process = Process.Start(start)!;
        _started.Add(process);
        return process;
    }

    [Fact]
    public void Miners_left_by_a_crashed_otium_are_stopped_but_others_are_left_alone()
    {
        var orphan = StartStandIn(Path.Combine(EnginesRoot, "xmrig-6.26.0"));
        var someoneElses = StartStandIn(Path.Combine(_dir, "elsewhere", "xmrig"));

        Assert.Equal(1, MinerLifetime.StopOrphans(EnginesRoot, LockFile));
        Assert.True(orphan.WaitForExit(TimeSpan.FromSeconds(10)));
        Assert.False(someoneElses.HasExited);
    }

    [Fact]
    public void Nothing_is_stopped_while_another_otium_uses_the_same_install()
    {
        var running = StartStandIn(Path.Combine(EnginesRoot, "xmrig-6.26.0"));
        Directory.CreateDirectory(_dir);
        using (new FileStream(LockFile, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
            Assert.Equal(0, MinerLifetime.StopOrphans(EnginesRoot, LockFile));
        Assert.False(running.HasExited);
    }

    [Fact]
    public void Adopted_miners_join_the_kill_on_close_job_and_are_killed_on_exit()
    {
        var miner = StartStandIn(Path.Combine(EnginesRoot, "xmrig-6.26.0"));
        MinerLifetime.Adopt(miner);
        if (OperatingSystem.IsWindows())
            Assert.True(MinerLifetime.IsInJob(miner)); // Windows kills it when Otium's process ends, however it ends

        MinerLifetime.KillAdopted(); // what the exit hooks run on Linux and macOS
        Assert.True(miner.WaitForExit(TimeSpan.FromSeconds(10)));
    }

    public void Dispose()
    {
        foreach (var process in _started)
        {
            try
            {
                if (!process.HasExited)
                    process.Kill();
                process.WaitForExit(5000);
            }
            catch (InvalidOperationException)
            {
            }
            process.Dispose();
        }
        MinerLifetime.ReleaseInstallLock(LockFile);
        try
        {
            Directory.Delete(_dir, recursive: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
        }
    }
}
