using Otium.Core.Platform;
using Otium.Core.Providers;

namespace Otium.App.Services;

/// <summary>
/// Runs from the Velopack uninstall hook (Windows only, must finish within 30 seconds, no UI).
/// Removes what an uninstaller is expected to remove: the downloaded engines, Mysterium's network service (if
/// internet sharing installed it; Windows asks for permission), and the start-at-sign-in entry. Settings and earnings
/// history are left in place in case the user reinstalls.
/// </summary>
public static class UninstallCleanup
{
    public static void Run()
    {
        try
        {
            AutoStart.Set(false);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
        }

        var engines = AppPaths.Default.EnginesDir;
        try
        {
            // The service runs from the engines folder and holds its file, so it goes first. The hook has 30 seconds.
            Otium.Providers.Mysterium.WindowsMystSupervisor.RemoveIfOurs(engines, TimeSpan.FromSeconds(20));
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException or IOException)
        {
            // Left behind: the user can remove it with "sc delete MysteriumVPNSupervisor".
        }

        try
        {
            MinerLifetime.StopOrphans(engines, Path.Combine(AppPaths.Default.Root, "otium.lock")); // or their files can't be deleted
            if (Directory.Exists(engines))
                Directory.Delete(engines, recursive: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // A running miner may still hold a file; Velopack stops the app before uninstalling, so this is rare.
        }
    }
}
