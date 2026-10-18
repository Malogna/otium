using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Otium.App.Services;

/// <summary>
/// Short desktop notifications ("While you were away: $0.84"), which reach the user while Otium sits in the tray.
/// Windows: a notification-area balloon from a helper icon (Windows 10 and 11 show it as a regular notification);
/// Linux: notify-send; macOS: osascript. Failures are silent: a missed notification costs nothing.
/// </summary>
public static class DesktopNotifier
{
    public static void Show(string title, string message)
    {
        try
        {
            if (OperatingSystem.IsWindows())
                WindowsBalloon.Show(title, message);
            else if (OperatingSystem.IsMacOS())
                Run("osascript", ["-e", $"display notification \"{Escape(message)}\" with title \"{Escape(title)}\""]);
            else
                Run("notify-send", ["--app-name=Otium", title, message]);
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException or ExternalException)
        {
        }
    }

    private static string Escape(string s) => s.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);

    private static void Run(string command, string[] arguments)
    {
        var start = new ProcessStartInfo(command) { UseShellExecute = false, CreateNoWindow = true };
        foreach (var argument in arguments)
            start.ArgumentList.Add(argument);
        using var process = Process.Start(start);
    }

    /// <summary>
    /// A hidden message-only window owns a second notification-area icon just long enough to show the balloon, so
    /// Avalonia's own tray icon is left alone.
    /// </summary>
    [SupportedOSPlatform("windows")]
    private static class WindowsBalloon
    {
        private const int NimAdd = 0, NimModify = 1, NimDelete = 2, NimSetVersion = 4;
        private const int NifIcon = 0x2, NifTip = 0x4, NifInfo = 0x10;
        private const int NiifInfo = 0x1;
        private const int NotifyIconVersion4 = 4;
        private static readonly IntPtr HwndMessage = new(-3);
        private static readonly Lock Gate = new();

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct NotifyIconData
        {
            public int CbSize;
            public IntPtr HWnd;
            public int UId;
            public int UFlags;
            public int UCallbackMessage;
            public IntPtr HIcon;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string SzTip;
            public int DwState;
            public int DwStateMask;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string SzInfo;
            public int UVersion;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string SzInfoTitle;
            public int DwInfoFlags;
            public Guid GuidItem;
            public IntPtr HBalloonIcon;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool Shell_NotifyIcon(int message, ref NotifyIconData data);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr ExtractIcon(IntPtr instance, string exeFileName, int iconIndex);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr CreateWindowEx(int exStyle, string className, string windowName, int style, int x, int y, int width, int height,
            IntPtr parent, IntPtr menu, IntPtr instance, IntPtr param);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DestroyWindow(IntPtr window);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DestroyIcon(IntPtr icon);

        public static void Show(string title, string message)
        {
            // Its own thread: the window and icon live a few seconds, and nothing waits for them.
            var thread = new Thread(() => ShowNow(title, message)) { IsBackground = true, Name = "Otium notification" };
            thread.Start();
        }

        private static void ShowNow(string title, string message)
        {
            lock (Gate)
            {
                var window = CreateWindowEx(0, "STATIC", "Otium notification", 0, 0, 0, 0, 0, HwndMessage, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
                if (window == IntPtr.Zero)
                    return;
                var icon = Environment.ProcessPath is { } exe ? ExtractIcon(IntPtr.Zero, exe, 0) : IntPtr.Zero;
                var data = new NotifyIconData
                {
                    CbSize = Marshal.SizeOf<NotifyIconData>(),
                    HWnd = window,
                    UId = 1,
                    UFlags = NifIcon | NifTip,
                    HIcon = icon,
                    SzTip = "Otium",
                    SzInfo = "",
                    SzInfoTitle = "",
                };
                try
                {
                    if (!Shell_NotifyIcon(NimAdd, ref data))
                        return;
                    data.UVersion = NotifyIconVersion4;
                    Shell_NotifyIcon(NimSetVersion, ref data);
                    data.UFlags = NifInfo;
                    data.SzInfoTitle = Truncate(title, 63);
                    data.SzInfo = Truncate(message, 255);
                    data.DwInfoFlags = NiifInfo;
                    Shell_NotifyIcon(NimModify, ref data);
                    Thread.Sleep(TimeSpan.FromSeconds(8)); // long enough for Windows to hand it to the notification center
                }
                finally
                {
                    Shell_NotifyIcon(NimDelete, ref data);
                    if (icon != IntPtr.Zero)
                        DestroyIcon(icon);
                    DestroyWindow(window);
                }
            }
        }

        private static string Truncate(string s, int max) => s.Length <= max ? s : s[..(max - 1)] + "…";
    }
}
