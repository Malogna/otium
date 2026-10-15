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
    private readonly GetMemoryDelegate _getMemory;
    private readonly GetPowerDelegate _getPower;
    private readonly Lock _lock = new();
    private bool _disposed;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int InitDelegate();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int ShutdownDelegate();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int GetCountDelegate(out uint count);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int GetHandleDelegate(uint index, out IntPtr device);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int GetNameDelegate(IntPtr device, byte[] name, uint length);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int GetMemoryDelegate(IntPtr device, out MemoryInfo memory);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int GetPowerDelegate(IntPtr device, out uint milliwatts);

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
        _getMemory = Load<GetMemoryDelegate>("nvmlDeviceGetMemoryInfo");
        _getPower = Load<GetPowerDelegate>("nvmlDeviceGetPowerUsage");
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
                result.Add(new GpuInfo(GpuVendor.Nvidia, (int)i, name, memoryGb));
            }
            return result;
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
