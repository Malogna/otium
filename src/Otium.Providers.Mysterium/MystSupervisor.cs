using System.ComponentModel;
using System.Diagnostics;

namespace Otium.Providers.Mysterium;

/// <summary>
/// Mysterium's network service ("supervisor"), which test versions of Otium (0.1.2-rc.1 to rc.3, and the earlier
/// Windows-only sharing) installed with the system's administrator prompt. The node now runs in userspace mode and needs
/// none, so Otium only finds and removes the one it installed (Settings and the uninstaller); a service another app
/// installed is left alone.
/// </summary>
public static class MystSupervisors
{
    /// <summary>Whether a supervisor service Otium installed (from its engines folder or its protected copy) is present.</summary>
    public static bool OursInstalled(string enginesRoot) =>
        OperatingSystem.IsWindows() ? WindowsMystSupervisor.OurServiceBinary(enginesRoot) is not null
        : OperatingSystem.IsMacOS() ? MacMystSupervisor.IsOurs()
        : OperatingSystem.IsLinux() && LinuxMystSupervisor.IsOurs();

    /// <summary>
    /// Removes the supervisor service Otium installed, and its protected copy, with the administrator prompt. Returns null
    /// when it's gone (or there was nothing to do), otherwise a reason (declined, timed out, failed).
    /// </summary>
    public static string? RemoveOurs(string enginesRoot, TimeSpan timeout) =>
        OperatingSystem.IsWindows() ? WindowsMystSupervisor.RemoveIfOurs(enginesRoot, timeout)
        : OperatingSystem.IsMacOS() ? MacMystSupervisor.RemoveIfOurs(timeout)
        : OperatingSystem.IsLinux() ? LinuxMystSupervisor.RemoveIfOurs(timeout)
        : null;

    /// <summary>A POSIX shell single-quoted word: the only character to escape is the quote itself.</summary>
    internal static string ShellQuote(string value) => "'" + value.Replace("'", "'\\''", StringComparison.Ordinal) + "'";

    /// <summary>Runs a command to the end; the exit code, and what it wrote to its error stream.</summary>
    internal static (int ExitCode, string Error) Run(string file, IEnumerable<string> args, TimeSpan timeout)
    {
        var start = new ProcessStartInfo(file) { UseShellExecute = false, RedirectStandardError = true, RedirectStandardOutput = true, CreateNoWindow = true };
        foreach (var arg in args)
            start.ArgumentList.Add(arg);
        using var process = Process.Start(start) ?? throw new InvalidOperationException($"Couldn't start {file}.");
        var error = process.StandardError.ReadToEndAsync();
        process.StandardOutput.ReadToEnd();
        if (!process.WaitForExit((int)timeout.TotalMilliseconds))
            return (-1, "timed out");
        return (process.ExitCode, error.Result);
    }
}

public static class WindowsMystSupervisor
{
    public const string ServiceName = "MysteriumVPNSupervisor";
    private const int ErrorCancelled = 1223;

    /// <summary>Where test versions of Otium put the service's files (with WireGuard's Wintun driver, from rc.3).</summary>
    public static string ProtectedDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Otium", "Mysterium");

    /// <summary>
    /// Removes the supervisor service, but only if Otium installed it (from its engines folder or its Program Files
    /// copy), and that folder. Windows asks for administrator permission. Returns null when there was nothing to do or it
    /// was removed, otherwise a reason.
    /// </summary>
    public static string? RemoveIfOurs(string enginesRoot, TimeSpan timeout)
    {
        if (OurServiceBinary(enginesRoot) is not { } binary)
            return null;

        // The supervisor removes its own service and event log source; without its file, Windows' sc does it.
        var remove = File.Exists(binary) ? $"\"{binary}\" -uninstall" : $"sc delete {ServiceName}";
        var start = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "cmd.exe"),
            $"/d /s /c \"net stop {ServiceName} >nul 2>&1 & {remove} & rmdir /s /q \"{ProtectedDirectory}\" 2>nul & exit /b 0\"")
        {
            UseShellExecute = true,
            Verb = "runas",
            WindowStyle = ProcessWindowStyle.Hidden,
        };
        try
        {
            using var process = Process.Start(start);
            if (process is null)
                return "Couldn't start removing Mysterium's network service.";
            if (!process.WaitForExit((int)timeout.TotalMilliseconds))
                return "Removing Mysterium's network service took too long.";
            return ServiceBinaryPath() is null ? null : "Removing Mysterium's network service failed.";
        }
        catch (Win32Exception e) when (e.NativeErrorCode == ErrorCancelled)
        {
            return "Mysterium's network service was left installed, because the Windows permission prompt was cancelled.";
        }
    }

    /// <summary>The supervisor service's executable when Otium installed it (under <paramref name="enginesRoot"/> or its protected folder), else null.</summary>
    public static string? OurServiceBinary(string enginesRoot) =>
        ServiceBinaryPath() is { } binary && (IsUnder(binary, enginesRoot) || IsUnder(binary, ProtectedDirectory)) ? binary : null;

    /// <summary>The installed supervisor service's executable, whoever installed it, or null when there's none.</summary>
    private static string? ServiceBinaryPath()
    {
        using var sc = Process.Start(new ProcessStartInfo("sc.exe", $"qc {ServiceName}")
        {
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
        });
        if (sc is null)
            return null;
        var output = sc.StandardOutput.ReadToEnd();
        sc.WaitForExit(5000);
        // "sc qc" exits with 1060 when the service doesn't exist.
        return sc.ExitCode == 0 ? ServiceBinary(output) : null;
    }

    /// <summary>The executable in <c>sc qc</c>'s BINARY_PATH_NAME line, without quotes or arguments.</summary>
    internal static string? ServiceBinary(string scQcOutput)
    {
        var line = scQcOutput.Split('\n').FirstOrDefault(l => l.TrimStart().StartsWith("BINARY_PATH_NAME", StringComparison.OrdinalIgnoreCase));
        var colon = line?.IndexOf(':') ?? -1;
        if (line is null || colon < 0)
            return null;
        var value = line[(colon + 1)..].Trim();
        if (value.StartsWith('"'))
        {
            var end = value.IndexOf('"', 1);
            return end > 1 ? value[1..end] : null;
        }
        var exe = value.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
        return exe > 0 ? value[..(exe + 4)] : value.Split(' ')[0];
    }

    internal static bool IsUnder(string path, string folder) => Core.Platform.WindowsPaths.IsUnder(path, folder);
}

/// <summary>macOS: test versions installed it as a launchd daemon; removing it asks for the password through AppleScript.</summary>
public static class MacMystSupervisor
{
    public const string PlistFile = "/Library/LaunchDaemons/network.mysterium.myst_supervisor.plist";
    public const string ProtectedDirectory = "/Library/Application Support/Otium/Mysterium";
    public const string ProtectedBinary = ProtectedDirectory + "/myst_supervisor";

    private const string Prompt = "Otium needs your password to remove Mysterium's network service.";

    internal static bool IsOurs() => File.Exists(PlistFile) && File.ReadAllText(PlistFile).Contains(ProtectedDirectory, StringComparison.Ordinal);

    /// <summary>AppleScript that runs <paramref name="script"/> with the system's administrator password prompt.</summary>
    internal static string AppleScript(string script) =>
        $"do shell script \"{Escape(script)}\" with prompt \"{Escape(Prompt)}\" with administrator privileges";

    private static string Escape(string text) => text.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);

    internal static string? RemoveIfOurs(TimeSpan timeout)
    {
        if (!IsOurs())
            return null;
        var target = MystSupervisors.ShellQuote(ProtectedBinary);
        var (code, error) = MystSupervisors.Run("/usr/bin/osascript",
            ["-e", AppleScript($"{target} -uninstall; /bin/rm -f {MystSupervisors.ShellQuote(PlistFile)}; /bin/rm -rf {MystSupervisors.ShellQuote(ProtectedDirectory)}")], timeout);
        if (code != 0 && error.Contains("-128", StringComparison.Ordinal))
            return "Mysterium's network service was left installed, because the password prompt was cancelled.";
        return IsOurs() ? "Removing Mysterium's network service failed." : null;
    }
}

/// <summary>Linux: test versions installed it as a systemd service; removing it asks for the password through PolicyKit (pkexec).</summary>
public static class LinuxMystSupervisor
{
    public const string UnitFile = "/etc/systemd/system/myst_supervisor.service";
    public const string ProtectedDirectory = "/usr/local/lib/otium-mysterium";
    public const string ProtectedBinary = ProtectedDirectory + "/myst_supervisor";

    /// <summary>pkexec's exit codes when the password prompt is dismissed or the user isn't allowed.</summary>
    private static readonly int[] Declined = [126, 127];

    internal static bool IsOurs() => File.Exists(UnitFile) && File.ReadAllText(UnitFile).Contains(ProtectedDirectory, StringComparison.Ordinal);

    internal static string? RemoveIfOurs(TimeSpan timeout)
    {
        if (!IsOurs())
            return null;
        var target = MystSupervisors.ShellQuote(ProtectedBinary);
        try
        {
            var (code, _) = MystSupervisors.Run("pkexec",
                ["/bin/sh", "-c", $"{target} -uninstall; rm -f {MystSupervisors.ShellQuote(UnitFile)}; systemctl daemon-reload; rm -rf {MystSupervisors.ShellQuote(ProtectedDirectory)}"], timeout);
            if (Declined.Contains(code))
                return "Mysterium's network service was left installed, because the password prompt was cancelled.";
        }
        catch (Win32Exception)
        {
            return $"Couldn't ask for your password (pkexec is missing). Remove it with: sudo {ProtectedBinary} -uninstall";
        }
        return IsOurs() ? "Removing Mysterium's network service failed." : null;
    }
}
