using System.Diagnostics;

namespace Otium.Core.Gpu;

/// <summary>
/// "Efficiency mode" for NVIDIA graphics cards (Windows): while the card mines, its power limit is lowered to
/// <see cref="CapPercent"/>% of its default. Mining speed drops much less than the power does, so the user keeps more
/// after their electricity bill; the limit goes back to the default the moment mining stops, so games aren't affected.
///
/// Setting a power limit needs administrator rights. So that Windows asks only once, turning the mode on (one
/// administrator prompt) creates two scheduled tasks that run Otium with highest privileges: one that caps the limit and
/// one that restores it. Otium then just starts the right task (no prompt) when mining starts and stops.
/// </summary>
public static class GpuEfficiency
{
    public const int CapPercent = 70;

    /// <summary>
    /// Quiet mode lowers the cap further, in steps, for coins whose miner setting doesn't change the card's load (Pearl
    /// on an RTX 3080 drew the same 348 W at every intensity, October 2026); never below this.
    /// </summary>
    public const int MinCapPercent = 50;

    /// <summary>
    /// The cap the tasks apply, written by Otium (not elevated) so quiet mode can change it without another Windows
    /// prompt: "cap 70" in a task created earlier means "the file's percentage, or 70 without one". Clamped to
    /// <see cref="MinCapPercent"/>–100 whatever the file says.
    /// </summary>
    public static string CapFile(string dataRoot) => Path.Combine(dataRoot, "gpu-power-cap.txt");

    /// <summary>The percentage the cap task applies: the file's (clamped), or <paramref name="fallback"/> without a valid one.</summary>
    public static int CapFrom(string? fileText, int fallback) =>
        int.TryParse(fileText?.Trim(), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var percent)
            ? Math.Clamp(percent, MinCapPercent, 100)
            : Math.Clamp(fallback, MinCapPercent, 100);

    /// <summary>Sets the percentage the next cap applies (see <see cref="CapFile"/>); true when it was written.</summary>
    public static bool WriteCap(string dataRoot, int percent)
    {
        try
        {
            File.WriteAllText(CapFile(dataRoot), Math.Clamp(percent, MinCapPercent, 100).ToString(System.Globalization.CultureInfo.InvariantCulture));
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>The argument the tasks run Otium with: "--gpu-power cap 70" or "--gpu-power restore".</summary>
    public const string PowerArgument = "--gpu-power";

    /// <summary>Run elevated once: creates the two tasks, then exits.</summary>
    public const string SetupArgument = "--gpu-power-setup";

    /// <summary>Run elevated: restores the limit and removes the tasks (turning the mode off, or uninstalling).</summary>
    public const string RemoveArgument = "--gpu-power-remove";

    public const string CapTask = @"\Otium\GPU power cap";
    public const string RestoreTask = @"\Otium\GPU power restore";

    public static bool IsSupported => OperatingSystem.IsWindows();

    /// <summary>What the elevated helper does for each argument; returns a process exit code (0 = done).</summary>
    public static int RunHelper(string[] args, string exePath)
    {
        switch (args)
        {
            case [PowerArgument, "cap", var percent] when int.TryParse(percent, out var p):
                return SetLimit(CapFrom(ReadCap(), p));
            case [PowerArgument, "restore"]:
                return SetLimit(100);
            case [SetupArgument]:
                var created = Schtasks(["/Create", "/TN", CapTask, "/TR", $"\"{exePath}\" {PowerArgument} cap {CapPercent}", "/SC", "ONCE", "/ST", "00:00", "/RL", "HIGHEST", "/IT", "/F"]) == 0
                              && Schtasks(["/Create", "/TN", RestoreTask, "/TR", $"\"{exePath}\" {PowerArgument} restore", "/SC", "ONCE", "/ST", "00:00", "/RL", "HIGHEST", "/IT", "/F"]) == 0;
                // ("ONCE at 00:00" today is already past, so the tasks never run by themselves: only when Otium starts them.)
                return created ? 0 : 1;
            case [RemoveArgument]:
                SetLimit(100);
                Schtasks(["/Delete", "/TN", CapTask, "/F"]);
                Schtasks(["/Delete", "/TN", RestoreTask, "/F"]);
                return 0;
            default:
                return 2;
        }
    }

    /// <summary>True when Otium was started as one of the helpers above (it does its job and exits, no window).</summary>
    public static bool IsHelper(string[] args) => args is [PowerArgument, ..] or [SetupArgument] or [RemoveArgument];

    /// <summary>Starts the task that caps (or restores) the limit. Needs no administrator prompt once set up.</summary>
    public static bool Trigger(bool cap) => Schtasks(["/Run", "/TN", cap ? CapTask : RestoreTask]) == 0;

    /// <summary>Whether the tasks exist (the mode was set up on this computer).</summary>
    public static bool IsSetUp() => IsSupported && Schtasks(["/Query", "/TN", CapTask]) == 0;

    /// <summary>The cap file's text, from Otium's data folder (the task runs as the same user, elevated); null without one.</summary>
    private static string? ReadCap()
    {
        try
        {
            var file = CapFile(Platform.AppPaths.Default.Root);
            return File.Exists(file) ? File.ReadAllText(file) : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static int SetLimit(int percent)
    {
        using var nvml = Nvml.TryOpen();
        return nvml is not null && nvml.SetPowerLimitPercent(percent) > 0 ? 0 : 3;
    }

    private static int Schtasks(string[] arguments)
    {
        if (!OperatingSystem.IsWindows())
            return -1;
        var start = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "schtasks.exe"))
        {
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var argument in arguments)
            start.ArgumentList.Add(argument);
        try
        {
            using var process = Process.Start(start);
            if (process is null)
                return -1;
            process.StandardOutput.ReadToEnd();
            process.StandardError.ReadToEnd();
            return process.WaitForExit(15_000) ? process.ExitCode : -1;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return -1;
        }
    }
}
