using System.ComponentModel;
using System.Diagnostics;

namespace Otium.App.Services;

/// <summary>
/// Lets the user allow Otium's engines folder in Microsoft Defender with one click. Windows shows its own
/// administrator prompt (UAC) first, so nothing changes without the user's explicit OK, and the exclusion covers only
/// the folder holding the downloaded miners, never the whole drive or Defender itself.
/// </summary>
public static class WindowsSecurity
{
    /// <summary>Windows' "The operation was canceled by the user" when the UAC prompt is declined.</summary>
    private const int ErrorCancelled = 1223;

    public static bool IsSupported => OperatingSystem.IsWindows();

    /// <summary>
    /// Adds a Defender exclusion for <paramref name="folder"/>. Returns null on success, or a short user-facing reason.
    /// </summary>
    public static async Task<string?> AllowFolderAsync(string folder)
    {
        if (!IsSupported)
            return "This is only needed on Windows.";

        Directory.CreateDirectory(folder);
        // PowerShell single-quoted string: the only character to escape is the quote itself.
        var path = Path.GetFullPath(folder).Replace("'", "''", StringComparison.Ordinal);
        var start = new ProcessStartInfo
        {
            FileName = Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe"),
            Arguments = $"-NoProfile -NonInteractive -WindowStyle Hidden -Command \"Add-MpPreference -ExclusionPath '{path}'\"",
            UseShellExecute = true,
            Verb = "runas", // shows the UAC prompt
            WindowStyle = ProcessWindowStyle.Hidden,
        };

        try
        {
            using var process = Process.Start(start);
            if (process is null)
                return "Windows Security couldn't be updated.";
            await process.WaitForExitAsync();
            return process.ExitCode == 0
                ? null
                : "Windows Security didn't accept the change. Your organisation may manage it; add the exclusion by hand instead.";
        }
        catch (Win32Exception e) when (e.NativeErrorCode == ErrorCancelled)
        {
            return "Nothing was changed, because the Windows permission prompt was cancelled.";
        }
        catch (Win32Exception e)
        {
            return $"Windows Security couldn't be updated: {e.Message}";
        }
    }
}
