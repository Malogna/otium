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

        try
        {
            // Graphics card efficiency mode: its two tasks were created with administrator rights, so removing them (and
            // putting the power limit back) asks Windows once more.
            if (Otium.Core.Gpu.GpuEfficiency.IsSetUp() && Environment.ProcessPath is { } self)
            {
                using var helper = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(self, [Otium.Core.Gpu.GpuEfficiency.RemoveArgument])
                {
                    UseShellExecute = true,
                    Verb = "runas",
                    WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden,
                });
                helper?.WaitForExit(15_000);
            }
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // Declined: the tasks stay (they do nothing unless started), removable with "schtasks /Delete /TN \Otium\...".
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
