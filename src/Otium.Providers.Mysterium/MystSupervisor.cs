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
