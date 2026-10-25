using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Otium.Core.Providers;

namespace Otium.Providers.Mysterium;

/// <summary>
/// The Mysterium node runs as the user and asks a small system service (its "supervisor") to set up its network
/// tunnels. Installing that service is the one step that needs administrator rights, once per computer. The service runs
/// with full rights, so Otium installs it from a copy in a folder only administrators can change, never from its own
/// engines folder, which any program the user runs could rewrite.
/// </summary>
public interface IMystSupervisor
{
    /// <summary>
    /// Whether the supervisor service is installed from Otium's protected copy. One an earlier Otium installed straight
    /// from the engines folder counts as not installed, so it's moved on the next start.
    /// </summary>
    bool IsInstalled { get; }

    /// <summary>Installs the service, showing the system's administrator prompt. Throws a plain-words problem if declined.</summary>
    Task InstallAsync(string supervisorPath, CancellationToken ct);
}

/// <summary>The supervisor for this operating system, and removing the one Otium installed (Settings and uninstall).</summary>
public static class MystSupervisors
{
    public static IMystSupervisor? ForCurrentPlatform() =>
        OperatingSystem.IsWindows() ? new WindowsMystSupervisor()
        : OperatingSystem.IsMacOS() ? new MacMystSupervisor()
        : OperatingSystem.IsLinux() ? new LinuxMystSupervisor()
        : null;

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

    [DllImport("libc", EntryPoint = "getuid")]
    private static extern uint GetUid();

    /// <summary>The user's numeric id (macOS and Linux), which the supervisor gives its socket to.</summary>
    internal static uint CurrentUid() => GetUid();
}

public sealed class WindowsMystSupervisor : IMystSupervisor
{
    public const string ServiceName = "MysteriumVPNSupervisor";
    private const int ErrorCancelled = 1223;

    /// <summary>Where the service's executable lives: under Program Files, which only administrators can change.</summary>
    public static string ProtectedDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Otium", "Mysterium");

    public bool IsInstalled => ServiceBinaryPath() is { } binary && IsUnder(binary, ProtectedDirectory);

    /// <summary>
    /// The elevated command: stop an earlier service (it may hold the file), copy the supervisor into the protected
    /// folder, and install the service from there for this user.
    /// </summary>
    internal static string InstallCommand(string supervisorPath, string protectedDirectory, string user)
    {
        var target = protectedDirectory.TrimEnd('\\') + @"\myst_supervisor.exe"; // Windows paths, whatever the test machine
        return $"/d /s /c \"net stop {ServiceName} >nul 2>&1 & (if not exist \"{protectedDirectory}\" mkdir \"{protectedDirectory}\")"
               + $" && copy /y \"{supervisorPath}\" \"{target}\" >nul && \"{target}\" -install -uid \"{user}\"\"";
    }

    public async Task InstallAsync(string supervisorPath, CancellationToken ct)
    {
        try
        {
            using var install = Process.Start(new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "cmd.exe"),
                InstallCommand(supervisorPath, ProtectedDirectory, Environment.UserName))
            {
                UseShellExecute = true,
                Verb = "runas", // the Windows administrator prompt
                WindowStyle = ProcessWindowStyle.Hidden,
            }) ?? throw new ProviderProblemException("Couldn't start the Mysterium network setup.");
            await install.WaitForExitAsync(ct);
            if (install.ExitCode != 0 || !IsInstalled)
                throw new ProviderProblemException(
                    $"The Mysterium network setup failed (code {install.ExitCode}). If Windows Security blocked it, allow Otium in Windows Security, then turn Mysterium sharing off and on again.");
        }
        catch (Win32Exception e) when (e.NativeErrorCode == ErrorCancelled)
        {
            throw new ProviderProblemException(
                "Sharing through Mysterium needs a one-time administrator permission to set up its network service. Turn it off and on again to be asked once more.");
        }
    }

    /// <summary>
    /// For Otium's uninstaller and Settings: removes the supervisor service, but only if Otium installed it (from its
    /// engines folder or its protected copy); a Mysterium node installed by another app keeps its service. Windows asks
    /// for administrator permission. Returns null when there was nothing to do or it was removed, otherwise a reason.
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

/// <summary>macOS: the supervisor installs itself as a launchd daemon; the password prompt comes from AppleScript.</summary>
public sealed class MacMystSupervisor : IMystSupervisor
{
    public const string PlistFile = "/Library/LaunchDaemons/network.mysterium.myst_supervisor.plist";

    /// <summary>Owned by root, so only an administrator can change what the daemon runs.</summary>
    public const string ProtectedDirectory = "/Library/Application Support/Otium/Mysterium";

    public const string ProtectedBinary = ProtectedDirectory + "/myst_supervisor";

    private const string Prompt = "Otium needs your password once to set up Mysterium's network service for internet sharing.";

    public bool IsInstalled => IsOurs();

    internal static bool IsOurs() => File.Exists(PlistFile) && File.ReadAllText(PlistFile).Contains(ProtectedDirectory, StringComparison.Ordinal);

    /// <summary>The shell script run as root: copy the supervisor into the protected folder and install the daemon from there.</summary>
    internal static string InstallScript(string supervisorPath, uint uid)
    {
        var target = MystSupervisors.ShellQuote(ProtectedBinary);
        return $"/bin/mkdir -p {MystSupervisors.ShellQuote(ProtectedDirectory)} && /bin/cp -f {MystSupervisors.ShellQuote(supervisorPath)} {target}"
               + $" && /usr/sbin/chown root:wheel {target} && /bin/chmod 755 {target} && {target} -install -uid {uid}";
    }

    /// <summary>AppleScript that runs <paramref name="script"/> with the system's administrator password prompt.</summary>
    internal static string AppleScript(string script) =>
        $"do shell script \"{Escape(script)}\" with prompt \"{Escape(Prompt)}\" with administrator privileges";

    private static string Escape(string text) => text.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);

    public Task InstallAsync(string supervisorPath, CancellationToken ct) => Task.Run(() =>
    {
        var (code, error) = MystSupervisors.Run("/usr/bin/osascript", ["-e", AppleScript(InstallScript(supervisorPath, MystSupervisors.CurrentUid()))], TimeSpan.FromMinutes(5));
        if (code != 0 && error.Contains("-128", StringComparison.Ordinal)) // "User canceled."
            throw new ProviderProblemException(
                "Sharing through Mysterium needs your password once to set up its network service. Turn it off and on again to be asked once more.");
        if (code != 0 || !IsOurs())
            throw new ProviderProblemException($"The Mysterium network setup failed ({error.Trim()}). Try turning Mysterium sharing off and on again.");
    }, ct);

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

/// <summary>Linux: the supervisor installs itself as a systemd service; the password prompt comes from PolicyKit (pkexec).</summary>
public sealed class LinuxMystSupervisor : IMystSupervisor
{
    public const string UnitFile = "/etc/systemd/system/myst_supervisor.service";

    /// <summary>Owned by root, so only an administrator can change what the service runs.</summary>
    public const string ProtectedDirectory = "/usr/local/lib/otium-mysterium";

    public const string ProtectedBinary = ProtectedDirectory + "/myst_supervisor";

    /// <summary>pkexec's exit codes when the password prompt is dismissed or the user isn't allowed.</summary>
    private static readonly int[] Declined = [126, 127];

    public bool IsInstalled => IsOurs();

    internal static bool IsOurs() => File.Exists(UnitFile) && File.ReadAllText(UnitFile).Contains(ProtectedDirectory, StringComparison.Ordinal);

    /// <summary>The shell script run as root: copy the supervisor into the protected folder and install the service from there.</summary>
    internal static string InstallScript(string supervisorPath, uint uid)
    {
        var target = MystSupervisors.ShellQuote(ProtectedBinary);
        return $"set -e; install -d -m 755 {MystSupervisors.ShellQuote(ProtectedDirectory)}; install -m 755 {MystSupervisors.ShellQuote(supervisorPath)} {target}; {target} -install -uid {uid}";
    }

    public Task InstallAsync(string supervisorPath, CancellationToken ct) => Task.Run(() =>
    {
        int code;
        string error;
        try
        {
            (code, error) = MystSupervisors.Run("pkexec", ["/bin/sh", "-c", InstallScript(supervisorPath, MystSupervisors.CurrentUid())], TimeSpan.FromMinutes(5));
        }
        catch (Win32Exception)
        {
            throw new ProviderProblemException(
                "Sharing through Mysterium needs PolicyKit (pkexec) to ask for your password once. Install your desktop's PolicyKit package and try again.");
        }
        if (Declined.Contains(code))
            throw new ProviderProblemException(
                "Sharing through Mysterium needs your password once to set up its network service. Turn it off and on again to be asked once more.");
        if (code != 0 || !IsOurs())
            throw new ProviderProblemException($"The Mysterium network setup failed ({error.Trim()}). Try turning Mysterium sharing off and on again.");
    }, ct);

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
