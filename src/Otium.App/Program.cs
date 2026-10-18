using Avalonia;
using Otium.App.Services;
using System;
using Velopack;

namespace Otium.App;

sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        // Elevated helper for "Speed up processor mining": grant the account the right to large pages, then exit.
        if (OperatingSystem.IsWindows() && args is [Otium.Core.Platform.LargePages.GrantArgument, var sid])
            Environment.Exit(Otium.Core.Platform.LargePages.Grant(sid));

        // Graphics card efficiency mode: setting up its tasks (elevated, once), or capping/restoring the power limit.
        if (OperatingSystem.IsWindows() && Otium.Core.Gpu.GpuEfficiency.IsHelper(args))
            Environment.Exit(Otium.Core.Gpu.GpuEfficiency.RunHelper(args, Environment.ProcessPath ?? "Otium.exe"));

        // Must run first: Velopack's install/update/uninstall hooks re-launch the app with special arguments
        // and expect it to exit quickly without showing UI.
        var velopack = VelopackApp.Build();
        if (OperatingSystem.IsWindows())
            velopack = velopack.OnBeforeUninstallFastCallback(_ => UninstallCleanup.Run());
        velopack.Run();

        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (DllNotFoundException e) when (OperatingSystem.IsLinux() && (e.Message.Contains("libICE") || e.Message.Contains("libSM")))
        {
            // Avalonia's X11 backend needs the X session libraries every desktop distro ships; minimal installs may not.
            Console.Error.WriteLine(
                "Otium needs the X11 session libraries libICE and libSM.\n" +
                "Install them with your package manager, e.g.:\n" +
                "  Debian/Ubuntu:  sudo apt install libice6 libsm6\n" +
                "  Fedora:         sudo dnf install libICE libSM\n" +
                "  Arch:           sudo pacman -S libice libsm");
            Environment.Exit(1);
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();
}
