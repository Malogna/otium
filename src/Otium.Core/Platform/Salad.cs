using System.Diagnostics;
using System.Text;

namespace Otium.Core.Platform;

/// <summary>
/// Salad (salad.com) rents consumer graphics cards to AI companies through its own Windows app and pays in Salad
/// Balance, which users redeem for PayPal or gift cards. It has no CLI or local API, and its terms forbid automated use,
/// so Otium only hands off: it links to Salad's download page, the user installs it and signs in in Salad's own app, and
/// Otium pauses its own graphics-card miner while Salad runs a job, so the two don't fight over the card.
///
/// How a job is spotted: Salad runs its container jobs in its own WSL2 distribution, which shows in
/// <c>wsl --list --running</c> while one runs (October 2026; the exact distribution name isn't documented, so any
/// running distribution with "salad" in its name counts).
/// </summary>
public static class Salad
{
    public const string DownloadUrl = "https://salad.com/download";

    public const string TermsUrl = "https://salad.com/terms/chefs/";

    /// <summary>The key Salad's installer writes its install location under (from its uninstaller, obliterator.nsi).</summary>
    private const string InstallKey = @"SOFTWARE\7a0ebc42-7f71-5caa-9738-b7dda7589c77";

    /// <summary>Whether Salad's app is installed on this Windows computer (always false elsewhere).</summary>
    public static bool IsInstalled()
    {
        if (!OperatingSystem.IsWindows())
            return false;
        try
        {
            foreach (var hive in new[] { Microsoft.Win32.Registry.CurrentUser, Microsoft.Win32.Registry.LocalMachine })
            {
                using var key = hive.OpenSubKey(InstallKey);
                if (key?.GetValue("InstallLocation") is string { Length: > 0 })
                    return true;
            }
            using var run = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
            if (run?.GetValue("Salad") is not null)
                return true;
        }
        catch (Exception e) when (e is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            // Can't look: fall back to the folders.
        }
        // An uninstall can leave the folder behind empty, so it counts only while it still holds a program.
        return new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Salad"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Salad"),
        }.Any(HoldsAProgram);
    }

    internal static bool HoldsAProgram(string folder)
    {
        try
        {
            return Directory.Exists(folder) && Directory.EnumerateFiles(folder, "*.exe", SearchOption.TopDirectoryOnly).Any();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>Whether <c>wsl --list --running --quiet</c>'s output names a Salad distribution.</summary>
    public static bool RunsJob(string? runningDistributions) =>
        runningDistributions is not null
        && runningDistributions.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Any(line => line.Contains("salad", StringComparison.OrdinalIgnoreCase));

    /// <summary>The WSL distributions running now, one per line; null when WSL isn't there or didn't answer.</summary>
    public static async Task<string?> RunningDistributionsAsync(CancellationToken ct)
    {
        if (!OperatingSystem.IsWindows())
            return null;
        var wsl = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "wsl.exe");
        if (!File.Exists(wsl))
            return null;
        var start = new ProcessStartInfo(wsl)
        {
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.Unicode, // wsl.exe writes UTF-16
        };
        foreach (var arg in new[] { "--list", "--running", "--quiet" })
            start.ArgumentList.Add(arg);
        try
        {
            using var process = Process.Start(start);
            if (process is null)
                return null;
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(10));
            var output = process.StandardOutput.ReadToEndAsync(timeout.Token);
            _ = process.StandardError.ReadToEndAsync(timeout.Token);
            await process.WaitForExitAsync(timeout.Token);
            return (await output).Replace("\0", "");
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException or OperationCanceledException && !ct.IsCancellationRequested)
        {
            return null;
        }
    }
}
