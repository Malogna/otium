using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Otium.Core.Platform;

public interface IIdleDetector
{
    /// <summary>Time since the last keyboard/mouse input, or null when the platform can't tell.</summary>
    TimeSpan? GetIdleTime();
}

public static class IdleDetector
{
    public static IIdleDetector ForThisPlatform() =>
        OperatingSystem.IsWindows() ? new WindowsIdleDetector()
        : OperatingSystem.IsMacOS() ? new MacIdleDetector()
        : new LinuxIdleDetector();
}

[SupportedOSPlatform("windows")]
internal sealed partial class WindowsIdleDetector : IIdleDetector
{
    [StructLayout(LayoutKind.Sequential)]
    private struct LastInputInfo
    {
        public uint CbSize;
        public uint DwTime;
    }

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetLastInputInfo(ref LastInputInfo info);

    public TimeSpan? GetIdleTime()
    {
        var info = new LastInputInfo { CbSize = (uint)Marshal.SizeOf<LastInputInfo>() };
        if (!GetLastInputInfo(ref info))
            return null;
        // Both values are 32-bit millisecond tick counts; unsigned subtraction handles wrap-around.
        var idleMs = unchecked((uint)Environment.TickCount - info.DwTime);
        return TimeSpan.FromMilliseconds(idleMs);
    }
}

internal sealed class MacIdleDetector : IIdleDetector
{
    public TimeSpan? GetIdleTime()
    {
        var output = ProcessRunner.TryRun("/usr/sbin/ioreg", "-c IOHIDSystem -d 4");
        if (output is null)
            return null;

        foreach (var line in output.Split('\n'))
        {
            var index = line.IndexOf("\"HIDIdleTime\" = ", StringComparison.Ordinal);
            if (index < 0)
                continue;
            var value = line[(index + "\"HIDIdleTime\" = ".Length)..].Trim();
            if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var nanoseconds))
                return TimeSpan.FromMilliseconds(nanoseconds / 1_000_000.0);
        }
        return null;
    }
}

internal sealed class LinuxIdleDetector : IIdleDetector
{
    public TimeSpan? GetIdleTime()
    {
        // xprintidle works on X11 (and XWayland sessions on many desktops). Without it idle time is unknown.
        var output = ProcessRunner.TryRun("xprintidle", "");
        return long.TryParse(output?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var ms)
            ? TimeSpan.FromMilliseconds(ms)
            : null;
    }
}

internal static class ProcessRunner
{
    public static string? TryRun(string fileName, string arguments, int timeoutMs = 3000)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo(fileName, arguments)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            });
            if (process is null)
                return null;
            var output = process.StandardOutput.ReadToEndAsync();
            if (!process.WaitForExit(timeoutMs))
            {
                process.Kill(entireProcessTree: true);
                return null;
            }
            return process.ExitCode == 0 ? output.Result : null;
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException or IOException)
        {
            return null;
        }
    }
}
