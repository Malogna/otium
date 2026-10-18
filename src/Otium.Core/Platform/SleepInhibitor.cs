using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Otium.Core.Platform;

/// <summary>
/// Keeps the computer from going to sleep while Otium earns. Mining starts a few minutes after the user walks away,
/// and Windows' usual power plan puts the PC to sleep 15–30 minutes later (laptops sooner), which would stop the
/// miners for the rest of the night. Only system sleep is held off: the screen still turns off as usual.
/// </summary>
public interface ISleepInhibitor : IDisposable
{
    bool IsHeld { get; }

    /// <summary>Asks the system to stay awake until <see cref="Release"/>. Safe to call while already held.</summary>
    void Hold();

    void Release();
}

public static class SleepInhibitor
{
    public const string Reason = "Otium is earning while you're away";

    public static ISleepInhibitor ForThisPlatform() =>
        OperatingSystem.IsWindows() ? new WindowsSleepInhibitor()
        : OperatingSystem.IsLinux() ? new ProcessSleepInhibitor("systemd-inhibit", ["--what=sleep:idle", "--who=Otium", $"--why={Reason}", "--mode=block", "sleep", "infinity"])
        : OperatingSystem.IsMacOS() ? new ProcessSleepInhibitor("caffeinate", ["-i"])
        : new NoSleepInhibitor();
}

/// <summary>
/// A Windows power request (PowerCreateRequest/PowerSetRequest with PowerRequestSystemRequired): the documented way for
/// an app to keep the system awake for a reason, listed by "powercfg /requests". Not tied to a thread, unlike
/// SetThreadExecutionState, and released automatically if Otium exits.
/// </summary>
internal sealed class WindowsSleepInhibitor : ISleepInhibitor
{
    private const uint PowerRequestContextVersion = 0;
    private const uint PowerRequestContextSimpleString = 0x1;
    private const int PowerRequestSystemRequired = 1;

    private readonly object _gate = new();
    private IntPtr _request;
    private IntPtr _reason;

    public bool IsHeld { get; private set; }

    public void Hold()
    {
        lock (_gate)
        {
            if (IsHeld)
                return;
            if (_request == IntPtr.Zero)
            {
                _reason = Marshal.StringToHGlobalUni(SleepInhibitor.Reason);
                var context = new ReasonContext { Version = PowerRequestContextVersion, Flags = PowerRequestContextSimpleString, SimpleReasonString = _reason };
                _request = PowerCreateRequest(ref context);
                if (_request == IntPtr.Zero || _request == new IntPtr(-1))
                {
                    _request = IntPtr.Zero;
                    return; // not fatal: the PC may sleep, as before
                }
            }
            IsHeld = PowerSetRequest(_request, PowerRequestSystemRequired);
        }
    }

    public void Release()
    {
        lock (_gate)
        {
            if (IsHeld && _request != IntPtr.Zero)
                PowerClearRequest(_request, PowerRequestSystemRequired);
            IsHeld = false;
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            Release();
            if (_request != IntPtr.Zero)
                CloseHandle(_request);
            _request = IntPtr.Zero;
            if (_reason != IntPtr.Zero)
                Marshal.FreeHGlobal(_reason);
            _reason = IntPtr.Zero;
        }
    }

    /// <summary>REASON_CONTEXT with the simple-string member of its union, padded to the union's full size.</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct ReasonContext
    {
        public uint Version;
        public uint Flags;
        public IntPtr SimpleReasonString;
        private uint _padding1;
        private uint _padding2;
        private IntPtr _padding3;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr PowerCreateRequest(ref ReasonContext context);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PowerSetRequest(IntPtr powerRequest, int requestType);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PowerClearRequest(IntPtr powerRequest, int requestType);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);
}

/// <summary>Linux (systemd-inhibit) and macOS (caffeinate): a helper process holds the lock for as long as it runs.</summary>
internal sealed class ProcessSleepInhibitor(string command, string[] arguments) : ISleepInhibitor
{
    private readonly object _gate = new();
    private Process? _process;

    public bool IsHeld => _process is { HasExited: false };

    public void Hold()
    {
        lock (_gate)
        {
            if (IsHeld)
                return;
            try
            {
                var start = new ProcessStartInfo(command) { UseShellExecute = false, CreateNoWindow = true };
                foreach (var argument in arguments)
                    start.ArgumentList.Add(argument);
                _process = Process.Start(start);
            }
            catch (System.ComponentModel.Win32Exception)
            {
                _process = null; // the helper isn't installed: the computer may sleep, as before
            }
        }
    }

    public void Release()
    {
        lock (_gate)
        {
            try
            {
                if (_process is { HasExited: false })
                    _process.Kill();
            }
            catch (InvalidOperationException)
            {
            }
            _process?.Dispose();
            _process = null;
        }
    }

    public void Dispose() => Release();
}

internal sealed class NoSleepInhibitor : ISleepInhibitor
{
    public bool IsHeld => false;

    public void Hold()
    {
    }

    public void Release()
    {
    }

    public void Dispose()
    {
    }
}

/// <summary>Whether the computer runs on battery, so Otium doesn't keep a laptop awake and draining.</summary>
public static class PowerSource
{
    public static bool OnBattery()
    {
        try
        {
            if (OperatingSystem.IsWindows())
                return GetSystemPowerStatus(out var status) && status.AcLineStatus == 0;
            if (OperatingSystem.IsLinux())
            {
                // Mains supplies report online=1 when plugged in; a desktop without any is on mains.
                var mains = Directory.Exists("/sys/class/power_supply")
                    ? Directory.GetDirectories("/sys/class/power_supply")
                        .Where(d => File.Exists(Path.Combine(d, "type")) && File.ReadAllText(Path.Combine(d, "type")).Trim() == "Mains")
                        .ToList()
                    : [];
                return mains.Count > 0 && mains.All(d => File.Exists(Path.Combine(d, "online")) && File.ReadAllText(Path.Combine(d, "online")).Trim() == "0");
            }
            if (OperatingSystem.IsMacOS())
            {
                using var pmset = Process.Start(new ProcessStartInfo("pmset", "-g batt") { RedirectStandardOutput = true, UseShellExecute = false });
                var output = pmset?.StandardOutput.ReadToEnd() ?? "";
                pmset?.WaitForExit(2000);
                return output.Contains("'Battery Power'", StringComparison.Ordinal);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
        }
        return false;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SystemPowerStatus
    {
        public byte AcLineStatus;
        public byte BatteryFlag;
        public byte BatteryLifePercent;
        public byte SystemStatusFlag;
        public uint BatteryLifeTime;
        public uint BatteryFullLifeTime;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemPowerStatus(out SystemPowerStatus status);
}

/// <summary>When Otium keeps the computer awake.</summary>
public static class KeepAwakePolicy
{
    /// <summary>
    /// While a miner is being set up, starting or earning, on mains power, and the user hasn't switched it off. Not while
    /// paused, waiting for the user to leave, stopped by an error or turned off: then the usual sleep settings apply.
    /// </summary>
    public static bool ShouldHold(bool enabled, IEnumerable<Engine.EngineState> engineStates, bool onBattery) =>
        enabled && !onBattery && engineStates.Any(s => s is Engine.EngineState.Installing or Engine.EngineState.Starting or Engine.EngineState.Earning);
}
