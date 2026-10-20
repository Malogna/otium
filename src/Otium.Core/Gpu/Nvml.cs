using System.Runtime.InteropServices;
using System.Text;

namespace Otium.Core.Gpu;

/// <summary>
/// Minimal binding to NVIDIA's management library (ships with the driver: nvml.dll on Windows,
/// libnvidia-ml.so.1 on Linux). Gives GPU names, memory and live board power without admin rights.
/// </summary>
public sealed class Nvml : IDisposable
{
    private const int Success = 0;

    private readonly IntPtr _library;
    private readonly InitDelegate _init;
    private readonly ShutdownDelegate _shutdown;
    private readonly GetCountDelegate _getCount;
    private readonly GetHandleDelegate _getHandle;
    private readonly GetNameDelegate _getName;
    private readonly GetPciInfoDelegate? _getPciInfo;
    private readonly GetTemperatureDelegate? _getTemperature;
    private readonly GetUtilizationDelegate? _getUtilization;
    private readonly GetFanSpeedDelegate? _getFanSpeed;
    private readonly GetClockDelegate? _getClock;
    private readonly GetMemoryDelegate _getMemory;
    private readonly GetPowerDelegate _getPower;
    private readonly GetPowerDelegate? _getDefaultLimit;
    private readonly GetConstraintsDelegate? _getConstraints;
    private readonly SetLimitDelegate? _setLimit;
    private readonly Lock _lock = new();
    private bool _disposed;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int InitDelegate();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int ShutdownDelegate();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int GetCountDelegate(out uint count);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int GetHandleDelegate(uint index, out IntPtr device);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int GetNameDelegate(IntPtr device, byte[] name, uint length);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int GetPciInfoDelegate(IntPtr device, byte[] pciInfo);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int GetMemoryDelegate(IntPtr device, out MemoryInfo memory);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int GetPowerDelegate(IntPtr device, out uint milliwatts);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int GetTemperatureDelegate(IntPtr device, int sensor, out uint celsius);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int GetUtilizationDelegate(IntPtr device, out Utilization rates);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int GetFanSpeedDelegate(IntPtr device, out uint percent);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int GetClockDelegate(IntPtr device, int clockType, out uint mhz);

    [StructLayout(LayoutKind.Sequential)]
    private struct Utilization
    {
        public uint Gpu;
        public uint Memory;
    }
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int GetConstraintsDelegate(IntPtr device, out uint minMilliwatts, out uint maxMilliwatts);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int SetLimitDelegate(IntPtr device, uint milliwatts);

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryInfo
    {
        public ulong Total;
        public ulong Free;
        public ulong Used;
    }

    private Nvml(IntPtr library)
    {
        _library = library;
        _init = Load<InitDelegate>("nvmlInit_v2");
        _shutdown = Load<ShutdownDelegate>("nvmlShutdown");
        _getCount = Load<GetCountDelegate>("nvmlDeviceGetCount_v2");
        _getHandle = Load<GetHandleDelegate>("nvmlDeviceGetHandleByIndex_v2");
        _getName = Load<GetNameDelegate>("nvmlDeviceGetName");
        _getPciInfo = TryLoad<GetPciInfoDelegate>("nvmlDeviceGetPciInfo_v3");
        _getTemperature = TryLoad<GetTemperatureDelegate>("nvmlDeviceGetTemperature");
        _getUtilization = TryLoad<GetUtilizationDelegate>("nvmlDeviceGetUtilizationRates");
        _getFanSpeed = TryLoad<GetFanSpeedDelegate>("nvmlDeviceGetFanSpeed");
        _getClock = TryLoad<GetClockDelegate>("nvmlDeviceGetClockInfo");
        _getMemory = Load<GetMemoryDelegate>("nvmlDeviceGetMemoryInfo");
        _getPower = Load<GetPowerDelegate>("nvmlDeviceGetPowerUsage");
        // Power limits: in every current driver, but optional so an old one still gives names and power.
        _getDefaultLimit = TryLoad<GetPowerDelegate>("nvmlDeviceGetPowerManagementDefaultLimit");
        _getConstraints = TryLoad<GetConstraintsDelegate>("nvmlDeviceGetPowerManagementLimitConstraints");
        _setLimit = TryLoad<SetLimitDelegate>("nvmlDeviceSetPowerManagementLimit");
        if (_init() != Success)
            throw new InvalidOperationException("nvmlInit failed");
    }

    /// <summary>Returns null when no NVIDIA driver is installed.</summary>
    public static Nvml? TryOpen()
    {
        var names = OperatingSystem.IsWindows()
            ? new[] { "nvml.dll", Path.Combine(Environment.SystemDirectory, "nvml.dll") }
            : new[] { "libnvidia-ml.so.1", "libnvidia-ml.so" };

        foreach (var name in names)
        {
            if (!NativeLibrary.TryLoad(name, out var handle))
                continue;
            try
            {
                return new Nvml(handle);
            }
            catch (Exception e) when (e is InvalidOperationException or EntryPointNotFoundException)
            {
                NativeLibrary.Free(handle);
            }
        }
        return null;
    }

    public IReadOnlyList<GpuInfo> ListDevices()
    {
        lock (_lock)
        {
            if (_getCount(out var count) != Success)
                return [];

            var result = new List<GpuInfo>();
            for (uint i = 0; i < count; i++)
            {
                if (_getHandle(i, out var device) != Success)
                    continue;
                var nameBuffer = new byte[96];
                var name = _getName(device, nameBuffer, (uint)nameBuffer.Length) == Success
                    ? Encoding.ASCII.GetString(nameBuffer).TrimEnd('\0')
                    : "NVIDIA GPU";
                var memoryGb = _getMemory(device, out var memory) == Success ? memory.Total / 1024.0 / 1024 / 1024 : 0;
                // nvmlPciInfo_t (v3): busIdLegacy[16], domain, bus, device, pciDeviceId, pciSubSystemId, busId[32].
                var pci = new byte[68];
                var bus = _getPciInfo is not null && _getPciInfo(device, pci) == Success
                    ? GpuInfo.NormalizePciBus(Encoding.ASCII.GetString(pci, 36, 32).TrimEnd('\0'))
                    : null;
                result.Add(new GpuInfo(GpuVendor.Nvidia, (int)i, name, memoryGb, bus));
            }
            return result;
        }
    }

    /// <summary>
    /// Graphics memory free right now on one GPU (by NVML index), in GB, or null if it can't be read. Other apps
    /// (games, 3D editors, browsers) can hold several GB, which decides whether a coin's dataset still fits.
    /// </summary>
    public double? ReadFreeMemoryGb(int index)
    {
        lock (_lock)
        {
            if (_disposed || index < 0 || _getHandle((uint)index, out var device) != Success)
                return null;
            return _getMemory(device, out var memory) == Success ? memory.Free / 1024.0 / 1024 / 1024 : null;
        }
    }

    /// <summary>Total board power of all NVIDIA GPUs in watts, or null if it can't be read.</summary>
    public double? ReadTotalWatts()
    {
        lock (_lock)
        {
            if (_disposed || _getCount(out var count) != Success)
                return null;
            double total = 0;
            var any = false;
            for (uint i = 0; i < count; i++)
            {
                if (_getHandle(i, out var device) == Success && _getPower(device, out var mw) == Success)
                {
                    total += mw / 1000.0;
                    any = true;
                }
            }
            return any ? total : null;
        }
    }

    /// <summary>
    /// Sets every NVIDIA card's power limit to <paramref name="percent"/>% of its default (never below the card's own
    /// minimum), or back to the default with 100. Needs administrator rights. Returns how many cards were set.
    /// </summary>
    public int SetPowerLimitPercent(int percent)
    {
        lock (_lock)
        {
            if (_disposed || _getDefaultLimit is null || _setLimit is null || _getCount(out var count) != Success)
                return 0;
            var set = 0;
            for (uint i = 0; i < count; i++)
            {
                if (_getHandle(i, out var device) != Success || _getDefaultLimit(device, out var defaultMw) != Success)
                    continue;
                var target = (uint)(defaultMw * Math.Clamp(percent, 1, 100) / 100.0);
                if (_getConstraints is not null && _getConstraints(device, out var min, out var max) == Success)
                    target = Math.Clamp(target, min, max);
                if (_setLimit(device, target) == Success)
                    set++;
            }
            return set;
        }
    }

    /// <summary>
    /// One card's temperature, load, fan, power, graphics clock and memory right now (by NVML index), each null where
    /// the driver doesn't report it (some cards have no fan reading, laptops often no power reading).
    /// </summary>
    public GpuStats? ReadStats(int index)
    {
        lock (_lock)
        {
            if (_disposed || _getHandle((uint)index, out var device) != Success)
                return null;
            double? Read(Func<(int Result, uint Value)> read) => read() is (Success, var v) ? v : null;
            var memory = _getMemory(device, out var m) == Success ? m : (MemoryInfo?)null;
            return new GpuStats(
                "",
                _getTemperature is null ? null : Read(() => (_getTemperature(device, 0, out var c), c)), // NVML_TEMPERATURE_GPU
                _getUtilization is null ? null : _getUtilization(device, out var u) == Success ? u.Gpu : null,
                _getFanSpeed is null ? null : Read(() => (_getFanSpeed(device, out var f), f)),
                Read(() => (_getPower(device, out var mw), mw)) / 1000,
                _getClock is null ? null : Read(() => (_getClock(device, 0, out var mhz), mhz)), // NVML_CLOCK_GRAPHICS
                memory?.Used / 1024.0 / 1024 / 1024,
                memory?.Total / 1024.0 / 1024 / 1024);
        }
    }

    private T? TryLoad<T>(string export) where T : Delegate =>
        NativeLibrary.TryGetExport(_library, export, out var address) ? Marshal.GetDelegateForFunctionPointer<T>(address) : null;

    private T Load<T>(string export) where T : Delegate =>
        Marshal.GetDelegateForFunctionPointer<T>(NativeLibrary.GetExport(_library, export));

    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed)
                return;
            _disposed = true;
            _shutdown();
            NativeLibrary.Free(_library);
        }
    }
}
