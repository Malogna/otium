using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Otium.Core.Providers;

/// <summary>
/// Makes sure miners don't outlive Otium.
///
/// On Windows every miner joins one Job Object created with JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE. Only Otium holds its
/// handle, so when Otium's process ends, however it ends (Quit, a crash, Task Manager), Windows closes the handle and
/// kills the miners together with anything they started, such as SRBMiner's watchdog child.
///
/// Linux and macOS have no equivalent that works from .NET's Process.Start, so Otium kills its miners (with their
/// child processes) on a normal exit, SIGTERM, SIGHUP or an unhandled exception, and on the next start stops any
/// that a SIGKILL or power cut left behind (<see cref="StopOrphans"/>). That runs on Windows too, as a backstop.
/// </summary>
public static partial class MinerLifetime
{
    /// <summary>
    /// Process names of the engines Otium runs: the miners (SRBMiner's watchdog child is another SRBMiner-MULTI) and
    /// the Mysterium node for internet sharing.
    /// </summary>
    public static readonly IReadOnlyList<string> MinerProcessNames = ["xmrig", "SRBMiner-MULTI", "myst"];

    private const int JobObjectExtendedLimitInformation = 9;
    private const uint JobObjectLimitKillOnJobClose = 0x2000;

    private static readonly Lock Gate = new();
    private static readonly List<(int Pid, DateTime StartedAt)> Tracked = [];
    private static IntPtr _job;
    private static bool _hooked;
    private static readonly Dictionary<string, FileStream> InstallLocks = new(StringComparer.OrdinalIgnoreCase);
    private static PosixSignalRegistration? _sighup;

    /// <summary>Call right after starting a miner so it can't outlive Otium.</summary>
    public static void Adopt(Process process)
    {
        lock (Gate)
        {
            if (OperatingSystem.IsWindows())
                AssignToJob(process);

            try
            {
                Tracked.Add((process.Id, process.StartTime));
            }
            catch (Exception e) when (e is InvalidOperationException or Win32Exception)
            {
                // Already gone.
            }

            if (_hooked)
                return;
            _hooked = true;
            AppDomain.CurrentDomain.ProcessExit += (_, _) => KillAdopted();
            AppDomain.CurrentDomain.UnhandledException += (_, _) => KillAdopted();
            if (!OperatingSystem.IsWindows())
            {
                try
                {
                    // Closing the terminal Otium was started from. SIGTERM and SIGINT already raise ProcessExit.
                    _sighup = PosixSignalRegistration.Create(PosixSignal.SIGHUP, _ => KillAdopted());
                }
                catch (Exception e) when (e is PlatformNotSupportedException or IOException)
                {
                }
            }
        }
    }

    /// <summary>Kills every miner started this session that's still running, with its child processes.</summary>
    public static void KillAdopted()
    {
        List<(int Pid, DateTime StartedAt)> tracked;
        lock (Gate)
        {
            tracked = [.. Tracked];
            Tracked.Clear();
        }

        foreach (var (pid, startedAt) in tracked)
        {
            try
            {
                using var process = Process.GetProcessById(pid);
                if (process.StartTime == startedAt && !process.HasExited) // not a new process that reused the id
                    process.Kill(entireProcessTree: true);
            }
            catch (Exception e) when (e is ArgumentException or InvalidOperationException or Win32Exception or NotSupportedException)
            {
            }
        }
    }

    /// <summary>
    /// Stops miners from this install (their program lies under <paramref name="enginesRoot"/>) left running by an
    /// earlier Otium that crashed or was killed. Miners elsewhere on the computer are never touched, and nothing is
    /// stopped while another Otium using the same data folder is running: <paramref name="lockFile"/> is held open
    /// for the rest of this process to tell. Returns how many processes were stopped.
    /// </summary>
    public static int StopOrphans(string enginesRoot, string lockFile)
    {
        lock (Gate)
        {
            var lockPath = Path.GetFullPath(lockFile);
            if (!InstallLocks.ContainsKey(lockPath))
            {
                try
                {
                    InstallLocks[lockPath] = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                }
                catch (IOException)
                {
                    return 0; // another Otium owns these engines; its miners aren't orphans
                }
                catch (UnauthorizedAccessException)
                {
                    return 0;
                }
            }
        }

        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(enginesRoot)) + Path.DirectorySeparatorChar;
        var comparison = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var stopped = 0;
        foreach (var name in MinerProcessNames)
        {
            foreach (var process in Process.GetProcessesByName(name))
            {
                using (process)
                {
                    try
                    {
                        if (process.Id == Environment.ProcessId || ExecutablePath(process) is not { } path
                            || !Path.GetFullPath(path).StartsWith(root, comparison))
                            continue;
                        process.Kill(entireProcessTree: true);
                        process.WaitForExit(TimeSpan.FromSeconds(5));
                        stopped++;
                    }
                    catch (Exception e) when (e is InvalidOperationException or Win32Exception or NotSupportedException or IOException or UnauthorizedAccessException)
                    {
                        // Exited meanwhile, or another user's process we can't inspect (so not ours).
                    }
                }
            }
        }
        return stopped;
    }

    /// <summary>Lets go of an install's lock (for tests; the app holds it until it exits).</summary>
    internal static void ReleaseInstallLock(string lockFile)
    {
        lock (Gate)
        {
            if (InstallLocks.Remove(Path.GetFullPath(lockFile), out var stream))
                stream.Dispose();
        }
    }

    private static string? ExecutablePath(Process process)
    {
        if (OperatingSystem.IsLinux())
            return new FileInfo($"/proc/{process.Id}/exe").LinkTarget;
        return process.MainModule?.FileName;
    }

    /// <summary>True if the process is in Otium's kill-on-close job (Windows only; for tests).</summary>
    internal static bool IsInJob(Process process)
    {
        if (!OperatingSystem.IsWindows())
            return false;
        lock (Gate)
            return _job != IntPtr.Zero && IsProcessInJob(process.Handle, _job, out var inJob) && inJob;
    }

    private static void AssignToJob(Process process)
    {
        try
        {
            if (_job == IntPtr.Zero)
            {
                var job = CreateJobObject(IntPtr.Zero, null);
                if (job == IntPtr.Zero)
                    return;
                var info = new ExtendedLimitInformation
                {
                    BasicLimitInformation = new BasicLimitInformation { LimitFlags = JobObjectLimitKillOnJobClose },
                };
                if (!SetInformationJobObject(job, JobObjectExtendedLimitInformation, ref info, (uint)Marshal.SizeOf<ExtendedLimitInformation>()))
                {
                    CloseHandle(job);
                    return;
                }
                _job = job; // never closed: Windows closes it when Otium exits, which is the point
            }

            // Children the miner starts from now on (SRBMiner's watchdog) join the job automatically.
            AssignProcessToJobObject(_job, process.Handle);
        }
        catch (Exception e) when (e is InvalidOperationException or Win32Exception)
        {
            // The miner already exited, or this Windows refuses (e.g. Otium itself runs in a job that forbids it):
            // the exit hooks and the next start's orphan check still apply.
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BasicLimitInformation
    {
        public long PerProcessUserTimeLimit;
        public long PerJobUserTimeLimit;
        public uint LimitFlags;
        public nuint MinimumWorkingSetSize;
        public nuint MaximumWorkingSetSize;
        public uint ActiveProcessLimit;
        public nuint Affinity;
        public uint PriorityClass;
        public uint SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IoCounters
    {
        public ulong ReadOperationCount;
        public ulong WriteOperationCount;
        public ulong OtherOperationCount;
        public ulong ReadTransferCount;
        public ulong WriteTransferCount;
        public ulong OtherTransferCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ExtendedLimitInformation
    {
        public BasicLimitInformation BasicLimitInformation;
        public IoCounters IoInfo;
        public nuint ProcessMemoryLimit;
        public nuint JobMemoryLimit;
        public nuint PeakProcessMemoryUsed;
        public nuint PeakJobMemoryUsed;
    }

    [LibraryImport("kernel32.dll", EntryPoint = "CreateJobObjectW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    private static partial IntPtr CreateJobObject(IntPtr jobAttributes, string? name);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetInformationJobObject(IntPtr job, int infoClass, ref ExtendedLimitInformation info, uint length);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AssignProcessToJobObject(IntPtr job, IntPtr process);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool IsProcessInJob(IntPtr process, IntPtr job, [MarshalAs(UnmanagedType.Bool)] out bool result);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CloseHandle(IntPtr handle);
}
