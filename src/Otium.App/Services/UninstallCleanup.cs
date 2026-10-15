using Otium.Core.Platform;

namespace Otium.App.Services;

/// <summary>
/// Runs from the Velopack uninstall hook (Windows only, must finish within 30 seconds, no UI).
/// Removes what an uninstaller is expected to remove: the downloaded miner binaries and the
/// start-at-sign-in entry. Settings and earnings history are left in place in case the user reinstalls.
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

        try
        {
            var engines = AppPaths.Default.EnginesDir;
            if (Directory.Exists(engines))
                Directory.Delete(engines, recursive: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // A running miner may still hold a file; Velopack stops the app before uninstalling, so this is rare.
        }
    }
}
