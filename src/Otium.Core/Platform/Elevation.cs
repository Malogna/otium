using System.ComponentModel;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Otium.Core.Platform;

/// <summary>How a run with administrator rights ended.</summary>
/// <param name="ExitCode">The helper's exit code (0 = done); meaningful only when it ran.</param>
/// <param name="Cancelled">The user said no to Windows' permission prompt.</param>
/// <param name="Failure">Windows couldn't start it at all, in its own words; null when it ran or was cancelled.</param>
public sealed record ElevatedRun(int ExitCode, bool Cancelled = false, string? Failure = null)
{
    public bool Succeeded => !Cancelled && Failure is null && ExitCode == 0;
}

/// <summary>
/// Runs Otium again with administrator rights (Windows asks once) for one helper job: setting up the graphics card's
/// power tasks, removing them, or the large-pages right. It goes through Windows' own command prompt: on some Windows 11
/// PCs Windows refuses to elevate a program in the user's AppData directly ("The system cannot find the path
/// specified", error 3, before any prompt appears; an RTX 3080 PC, October 2026), while elevating cmd.exe to start the
/// same program works. Asking Otium to elevate itself that way used to crash it.
/// </summary>
public static partial class Elevation
{
    private const int ErrorCancelled = 1223;

    /// <param name="arguments">Otium's helper arguments; plain words only (letters, digits, '-', '.', ':'), so nothing
    /// in them means anything to the command prompt.</param>
    public static async Task<ElevatedRun> RunSelfAsync(IReadOnlyList<string> arguments)
    {
        if (Environment.ProcessPath is not { } self)
            return new ElevatedRun(-1, Failure: "Otium couldn't find its own program file.");
        try
        {
            using var process = Process.Start(StartInfo(self, arguments));
            if (process is null)
                return new ElevatedRun(-1, Failure: "Windows didn't start it.");
            await process.WaitForExitAsync();
            return new ElevatedRun(process.ExitCode);
        }
        catch (Win32Exception e) when (e.NativeErrorCode == ErrorCancelled)
        {
            return new ElevatedRun(-1, Cancelled: true);
        }
        catch (Exception e) when (e is Win32Exception or InvalidOperationException)
        {
            return new ElevatedRun(-1, Failure: e.Message);
        }
    }

    /// <summary>
    /// The start: the system's cmd.exe, elevated, running Otium with the arguments (AutoRun off, no window). A program
    /// path the command prompt would read specially (a quote or '%') is started directly instead.
    /// </summary>
    internal static ProcessStartInfo StartInfo(string self, IReadOnlyList<string> arguments)
    {
        if (arguments.Any(a => !PlainArgument().IsMatch(a)))
            throw new ArgumentException("Helper arguments must be plain words.", nameof(arguments));
        var start = self.IndexOfAny(['"', '%']) >= 0
            ? new ProcessStartInfo(self, arguments)
            : new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "cmd.exe"), $"/d /c \"\"{self}\" {string.Join(' ', arguments)}\"");
        start.UseShellExecute = true;
        start.Verb = "runas"; // the Windows administrator prompt
        start.WindowStyle = ProcessWindowStyle.Hidden;
        return start;
    }

    [GeneratedRegex(@"^[A-Za-z0-9\-\.:]+$")]
    private static partial Regex PlainArgument();
}
