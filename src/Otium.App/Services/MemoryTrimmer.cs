using System.Diagnostics;
using System.Runtime;
using System.Runtime.InteropServices;

namespace Otium.App.Services;

/// <summary>
/// Gives memory back to the system while nobody is looking at Otium. Most of what the app holds then is garbage that has not
/// been collected yet, and pages it touched once while starting up; a full, compacting collection and (on Windows) emptying
/// the working set shrinks Task Manager's number from the 150 MB range to a fraction of it. What is needed again is paged
/// back in, a little at a time, when the window opens.
/// </summary>
public static class MemoryTrimmer
{
    /// <summary>How long after the window disappears before the first trim: the window's own teardown has settled by then.</summary>
    public static readonly TimeSpan AfterHidden = TimeSpan.FromSeconds(4);

    /// <summary>While the window stays hidden, trim again this often (the miners' output and the network calls grow the heap a little).</summary>
    public static readonly TimeSpan WhileHidden = TimeSpan.FromMinutes(10);

    public static void Trim()
    {
        try
        {
            GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
            GC.Collect(2, GCCollectionMode.Aggressive, blocking: true, compacting: true);
            GC.WaitForPendingFinalizers();
            GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
            if (OperatingSystem.IsWindows())
            {
                using var self = Process.GetCurrentProcess();
                EmptyWorkingSet(self.Handle);
            }
        }
        catch
        {
            // Only a courtesy to the rest of the computer: never a reason to fail.
        }
    }

    [DllImport("psapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EmptyWorkingSet(IntPtr process);
}
