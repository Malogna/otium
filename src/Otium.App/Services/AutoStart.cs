using System.Diagnostics;
using Microsoft.Win32;

namespace Otium.App.Services;

/// <summary>Per-user "start when I sign in" registration. Never needs admin rights.</summary>
public static class AutoStart
{
    private const string WindowsRunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string Name = "Otium";
    private const string MacLabel = "com.otium.app";

    // An AppImage runs from a temporary mount that changes every launch; $APPIMAGE is the real file to start.
    private static string Executable =>
        Environment.GetEnvironmentVariable("APPIMAGE") is { Length: > 0 } appImage
            ? appImage
            : Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName ?? "";

    public static bool IsEnabled()
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                using var key = Registry.CurrentUser.OpenSubKey(WindowsRunKey);
                return key?.GetValue(Name) is string;
            }
            return File.Exists(EntryPath());
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return false;
        }
    }

    public static void Set(bool enabled)
    {
        if (OperatingSystem.IsWindows())
        {
            using var key = Registry.CurrentUser.CreateSubKey(WindowsRunKey);
            if (enabled)
                key.SetValue(Name, $"\"{Executable}\" --minimized");
            else
                key.DeleteValue(Name, throwOnMissingValue: false);
            return;
        }

        var path = EntryPath();
        if (!enabled)
        {
            if (File.Exists(path))
                File.Delete(path);
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, OperatingSystem.IsMacOS() ? MacPlist() : LinuxDesktopEntry());
    }

    private static string EntryPath()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return OperatingSystem.IsMacOS()
            ? Path.Combine(home, "Library", "LaunchAgents", MacLabel + ".plist")
            : Path.Combine(Environment.GetEnvironmentVariable("XDG_CONFIG_HOME") ?? Path.Combine(home, ".config"), "autostart", "otium.desktop");
    }

    private static string MacPlist() => $"""
        <?xml version="1.0" encoding="UTF-8"?>
        <!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
        <plist version="1.0">
        <dict>
          <key>Label</key><string>{MacLabel}</string>
          <key>ProgramArguments</key>
          <array><string>{System.Security.SecurityElement.Escape(Executable)}</string><string>--minimized</string></array>
          <key>RunAtLoad</key><true/>
        </dict>
        </plist>
        """;

    private static string LinuxDesktopEntry() => $"""
        [Desktop Entry]
        Type=Application
        Name=Otium
        Exec="{Executable}" --minimized
        X-GNOME-Autostart-enabled=true
        """;
}
