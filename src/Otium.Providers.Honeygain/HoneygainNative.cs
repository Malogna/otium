using System.Runtime.InteropServices;
using System.Text;

namespace Otium.Providers.Honeygain;

/// <summary>The Honeygain SDK's C interface (hgsdk). Every call returns 0 on success or a negative error code.</summary>
internal interface IHoneygainSdk : IDisposable
{
    int Init(string apiKey);

    int OptIn();

    int OptOut();

    int Start(out bool started);

    int Stop();

    int IsRunning(out bool running);

    /// <summary>The SDK's stable id for this device, used in Honeygain's publisher reporting.</summary>
    string? Identify();

    int Mute();
}

/// <summary>
/// Loads the Honeygain SDK library that ships next to Otium (hgsdk.dll, libhgsdk.so or libhgsdk.dylib) at runtime.
/// Loading by explicit path from the app folder, never by search path, keeps a planted DLL elsewhere from being picked up.
/// </summary>
internal sealed class HoneygainNative : IHoneygainSdk
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int InitFn([MarshalAs(UnmanagedType.LPUTF8Str)] string apiKey);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int VoidFn();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int StateFn(out int state);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int IdentifyFn(byte[]? data, ref nuint size);

    private readonly nint _library;
    private readonly InitFn _init;
    private readonly VoidFn _optIn;
    private readonly VoidFn _optOut;
    private readonly StateFn _start;
    private readonly VoidFn _stop;
    private readonly StateFn _isRunning;
    private readonly IdentifyFn _identify;
    private readonly VoidFn _mute;

    private HoneygainNative(nint library)
    {
        _library = library;
        _init = Export<InitFn>("hgsdk_init");
        _optIn = Export<VoidFn>("hgsdk_opt_in");
        _optOut = Export<VoidFn>("hgsdk_opt_out");
        _start = Export<StateFn>("hgsdk_start");
        _stop = Export<VoidFn>("hgsdk_stop");
        _isRunning = Export<StateFn>("hgsdk_is_running");
        _identify = Export<IdentifyFn>("hgsdk_identify");
        _mute = Export<VoidFn>("hgsdk_mute");
    }

    public static string LibraryFileName =>
        OperatingSystem.IsWindows() ? "hgsdk.dll"
        : OperatingSystem.IsMacOS() ? "libhgsdk.dylib"
        : "libhgsdk.so";

    /// <summary>The SDK bundled with this build, or null when it isn't (development builds, unsupported platforms).</summary>
    public static HoneygainNative? TryLoad(string directory)
    {
        var path = Path.Combine(directory, LibraryFileName);
        if (!File.Exists(path) || !NativeLibrary.TryLoad(path, out var handle))
            return null;
        try
        {
            return new HoneygainNative(handle);
        }
        catch (EntryPointNotFoundException)
        {
            NativeLibrary.Free(handle);
            return null;
        }
    }

    public int Init(string apiKey) => _init(apiKey);

    public int OptIn() => _optIn();

    public int OptOut() => _optOut();

    public int Start(out bool started)
    {
        var result = _start(out var state);
        started = state != 0;
        return result;
    }

    public int Stop() => _stop();

    public int IsRunning(out bool running)
    {
        var result = _isRunning(out var state);
        running = state != 0;
        return result;
    }

    public string? Identify()
    {
        nuint size = 0;
        if (_identify(null, ref size) != 0 || size == 0 || size > 4096)
            return null;
        var buffer = new byte[(int)size];
        if (_identify(buffer, ref size) != 0)
            return null;
        return Encoding.UTF8.GetString(buffer, 0, Math.Min(buffer.Length, (int)size)).TrimEnd('\0');
    }

    public int Mute() => _mute();

    public void Dispose() => NativeLibrary.Free(_library);

    private T Export<T>(string name) where T : Delegate =>
        Marshal.GetDelegateForFunctionPointer<T>(NativeLibrary.GetExport(_library, name));
}
