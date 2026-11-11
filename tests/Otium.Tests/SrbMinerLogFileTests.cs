using System.Diagnostics;
using Otium.Providers.SrbMiner;

namespace Otium.Tests;

/// <summary>
/// SRBMiner holds its console output back when it is piped, so Otium reads the log file it is told to write instead:
/// lines show up as the file grows, once even when the console repeats them, and the file is removed afterwards.
/// </summary>
public class SrbMinerLogFileTests
{
    [Fact]
    public async Task Lines_of_the_log_file_reach_the_miners_log_while_the_console_is_silent()
    {
        var path = Path.Combine(Path.GetTempPath(), $"otium-test-{Guid.NewGuid():N}.log");
        // A process that stays quiet for a while, like a miner whose piped output is held back.
        var quiet = Process.Start(new ProcessStartInfo(Environment.ProcessPath!, "--help") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true })!;
        await using var process = new SrbMinerProcess(quiet, path);

        await File.AppendAllTextAsync(path, "[2026-11-11 13:09:02] GPU0 |A0|quantus             |INTENSITY:         24|\r\n");
        await File.AppendAllTextAsync(path, "[2026-11-11 13:09:34] Total 68.46 MH/s\r\n");
        await WaitFor(() => process.RecentLog.Contains("[2026-11-11 13:09:34] Total 68.46 MH/s"));

        Assert.Equal(24, process.AutoIntensity);
        Assert.Contains("[2026-11-11 13:09:34] Total 68.46 MH/s", process.RecentLog);

        // A half-written line waits for its end instead of being read in two pieces.
        await File.AppendAllTextAsync(path, "[2026-11-11 13:10:04] Rec");
        await Task.Delay(1200);
        Assert.DoesNotContain(process.RecentLog, l => l.EndsWith("Rec", StringComparison.Ordinal));
        await File.AppendAllTextAsync(path, "onnecting\r\n");
        await WaitFor(() => process.RecentLog.Contains("[2026-11-11 13:10:04] Reconnecting"));
    }

    [Fact]
    public async Task The_log_file_is_deleted_when_the_miner_is_disposed()
    {
        var path = Path.Combine(Path.GetTempPath(), $"otium-test-{Guid.NewGuid():N}.log");
        await File.WriteAllTextAsync(path, "[2026-11-11 13:09:02] hello\r\n");
        var quiet = Process.Start(new ProcessStartInfo(Environment.ProcessPath!, "--help") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true })!;
        var process = new SrbMinerProcess(quiet, path);
        await WaitFor(() => process.RecentLog.Contains("[2026-11-11 13:09:02] hello"));

        await process.DisposeAsync();

        Assert.False(File.Exists(path));
    }

    private static async Task WaitFor(Func<bool> condition)
    {
        for (var i = 0; i < 100 && !condition(); i++)
            await Task.Delay(100);
        Assert.True(condition());
    }
}
