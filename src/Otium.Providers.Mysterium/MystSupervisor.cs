using System.ComponentModel;
using System.Diagnostics;
using Otium.Core.Providers;

namespace Otium.Providers.Mysterium;

/// <summary>
/// The Mysterium node runs as the user and asks a small system service ("MysteriumVPN Supervisor") to set up its
/// network tunnels. Installing that service is the one step that needs administrator rights, once per computer.
/// </summary>
public interface IMystSupervisor
{
    bool IsInstalled { get; }

    /// <summary>Installs the service, showing Windows' administrator prompt. Throws a plain-words problem if declined.</summary>
    Task InstallAsync(string supervisorPath, CancellationToken ct);
}

public sealed class WindowsMystSupervisor : IMystSupervisor
{
    public const string ServiceName = "MysteriumVPNSupervisor";
    private const int ErrorCancelled = 1223;

    public bool IsInstalled
    {
        get
        {
            // "sc query" exits with 1060 when the service doesn't exist.
            using var sc = Process.Start(new ProcessStartInfo("sc.exe", $"query {ServiceName}")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
            });
            if (sc is null)
                return false;
            sc.StandardOutput.ReadToEnd();
            sc.WaitForExit(5000);
            return sc.ExitCode == 0;
        }
    }

    /// <summary>
    /// For Otium's uninstaller: removes the supervisor service, but only if it runs from Otium's own engines folder (a
    /// Mysterium node installed by another app keeps its service). Windows asks for administrator permission. Returns
    /// null when there was nothing to do or it was removed, otherwise a reason (declined, timed out, failed).
    /// </summary>
    public static string? RemoveIfOurs(string enginesRoot, TimeSpan timeout)
    {
        if (OurServiceBinary(enginesRoot) is not { } binary)
            return null;

        // The supervisor removes its own service and event log source; without its file, Windows' sc does it.
        var start = File.Exists(binary)
            ? new ProcessStartInfo(binary, "-uninstall")
            : new ProcessStartInfo("sc.exe", $"delete {ServiceName}");
        start.UseShellExecute = true;
        start.Verb = "runas";
        start.WindowStyle = ProcessWindowStyle.Hidden;
        try
        {
            using var remove = Process.Start(start);
            if (remove is null)
                return "Couldn't start removing Mysterium's network service.";
            if (!remove.WaitForExit((int)timeout.TotalMilliseconds))
                return "Removing Mysterium's network service took too long.";
            return remove.ExitCode == 0 ? null : $"Removing Mysterium's network service failed (code {remove.ExitCode}).";
        }
        catch (Win32Exception e) when (e.NativeErrorCode == ErrorCancelled)
        {
            return "Mysterium's network service was left installed, because the Windows permission prompt was cancelled.";
        }
    }

    /// <summary>The supervisor service's executable when it's installed from under <paramref name="enginesRoot"/>, else null.</summary>
    public static string? OurServiceBinary(string enginesRoot)
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
        return sc.ExitCode == 0 && ServiceBinary(output) is { } binary && IsUnder(binary, enginesRoot) ? binary : null;
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

    public async Task InstallAsync(string supervisorPath, CancellationToken ct)
    {
        try
        {
            using var install = Process.Start(new ProcessStartInfo(supervisorPath, ["-install", "-uid", Environment.UserName])
            {
                UseShellExecute = true,
                Verb = "runas", // the Windows administrator prompt
                WindowStyle = ProcessWindowStyle.Hidden,
            }) ?? throw new ProviderProblemException("Couldn't start the Mysterium network setup.");
            await install.WaitForExitAsync(ct);
            if (install.ExitCode != 0)
                throw new ProviderProblemException($"The Mysterium network setup failed (code {install.ExitCode}). Try turning internet sharing off and on again.");
        }
        catch (Win32Exception e) when (e.NativeErrorCode == ErrorCancelled)
        {
            throw new ProviderProblemException(
                "Internet sharing needs a one-time administrator permission to set up Mysterium's network service. Turn sharing off and on again to be asked once more.");
        }
    }
}
