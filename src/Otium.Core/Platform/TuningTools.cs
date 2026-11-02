using System.Diagnostics;
using Otium.Core.Settings;

namespace Otium.Core.Platform;

/// <summary>A graphics card tuning program (overclocking, fan curves, power limits) that would disturb the miner or its tests.</summary>
/// <param name="Name">What users call it.</param>
/// <param name="ProcessNames">Its process names without ".exe", as Windows lists them.</param>
public sealed record TuningTool(string Name, IReadOnlyList<string> ProcessNames);

/// <summary>A tuning program that is running right now.</summary>
/// <param name="ExePath">Where it started from; null when Windows won't say (the program runs as administrator and Otium doesn't).</param>
public sealed record RunningTool(string Name, string ProcessName, int ProcessId, string? ExePath);

/// <summary>A tuning program Otium closed so it could earn, to start again when it stops.</summary>
public sealed record ClosedTool(string Name, string ExePath, DateTimeOffset ClosedAt);

/// <summary>What Otium closed, kept in a file so the programs come back even if Otium crashes or restarts meanwhile.</summary>
public sealed class TuningToolsState
{
    public List<ClosedTool> Closed { get; set; } = [];
}

/// <summary>What happened when Otium asked the running tuning programs to step aside.</summary>
/// <param name="Closed">The programs that closed.</param>
/// <param name="CouldNot">The programs still running: Windows wouldn't tell where they start from (so they couldn't be started again), or they didn't close.</param>
public sealed record ToolsPauseResult(IReadOnlyList<string> Closed, IReadOnlyList<string> CouldNot);

/// <summary>Finds, closes and starts tuning programs (a seam so the rules can be tested without real programs).</summary>
public interface IToolProcesses
{
    IReadOnlyList<RunningTool> FindRunning();

    /// <summary>Asks the program to close and, if it doesn't within <paramref name="wait"/>, ends it. True once it is gone.</summary>
    Task<bool> CloseAsync(RunningTool tool, TimeSpan wait, CancellationToken ct);

    /// <summary>Starts the program again, minimized; false when it couldn't be started.</summary>
    bool Start(string exePath);
}

public static class TuningToolCatalog
{
    /// <summary>
    /// The graphics card tuning programs Otium steps aside for. Only these exact programs are ever touched: they set clocks,
    /// voltage, fan speeds and power limits on their own, which fights Otium's tests (a fan curve that stops at 90 °C
    /// slowed a test to a third on an RTX 3080, November 2026) and its power limit.
    /// </summary>
    public static readonly IReadOnlyList<TuningTool> Known =
    [
        new("MSI Afterburner", ["MSIAfterburner"]),
        new("ASUS GPU Tweak", ["GPUTweakII", "GPUTweakIII"]),
        new("EVGA Precision X1", ["PrecisionX_x64", "PrecisionX1", "EVGAPrecisionX1"]),
        new("ZOTAC FireStorm", ["FireStorm"]),
        new("AORUS Engine", ["AORUS_ENGINE", "AORUSENGINE"]),
        new("Gigabyte Xtreme Engine", ["XtremeEngine"]),
        new("Sapphire TriXX", ["TriXX"]),
        new("Palit ThunderMaster", ["ThunderMaster"]),
        new("NVIDIA Inspector", ["nvidiaInspector"]),
    ];

    public static TuningTool? ByProcess(string processName) =>
        Known.FirstOrDefault(t => t.ProcessNames.Contains(processName, StringComparer.OrdinalIgnoreCase));
}

/// <summary>
/// Closes the user's tuning programs while Otium earns and brings them back when it stops, so a user who overclocks
/// their card gets their settings back for games. What was closed is remembered in a file (see <see cref="TuningToolsState"/>)
/// so it survives a restart of Otium.
/// </summary>
public sealed class TuningToolsGuard
{
    private static readonly TimeSpan CloseWait = TimeSpan.FromSeconds(6);

    private readonly IToolProcesses _processes;
    private readonly JsonFileStore<TuningToolsState> _store;
    private readonly TimeProvider _time;
    private readonly Lock _lock = new();
    private TuningToolsState _state;

    public TuningToolsGuard(IToolProcesses processes, JsonFileStore<TuningToolsState> store, TimeProvider? time = null)
    {
        _processes = processes;
        _store = store;
        _time = time ?? TimeProvider.System;
        _state = store.Load();
    }

    /// <summary>What is closed now because Otium closed it.</summary>
    public IReadOnlyList<ClosedTool> Closed
    {
        get
        {
            lock (_lock)
            {
                _state = _store.Load(); // the elevated helper writes the file from another process
                return [.. _state.Closed];
            }
        }
    }

    /// <summary>The tuning programs running now.</summary>
    public IReadOnlyList<RunningTool> Running() => _processes.FindRunning();

    /// <summary>
    /// Closes every running tuning program whose start path is known, remembering each first (so a crash mid-way
    /// still brings it back). A program that won't say where it starts from isn't closed: it couldn't be started again.
    /// </summary>
    public async Task<ToolsPauseResult> PauseAsync(CancellationToken ct)
    {
        var closed = new List<string>();
        var couldNot = new List<string>();
        lock (_lock)
            _state = _store.Load();
        foreach (var tool in _processes.FindRunning())
        {
            if (tool.ExePath is not { Length: > 0 } path)
            {
                couldNot.Add(tool.Name);
                continue;
            }
            var entry = new ClosedTool(tool.Name, path, _time.GetUtcNow());
            lock (_lock)
            {
                if (!_state.Closed.Any(c => string.Equals(c.ExePath, path, StringComparison.OrdinalIgnoreCase)))
                {
                    _state.Closed.Add(entry);
                    _store.Save(_state);
                }
            }
            if (await _processes.CloseAsync(tool, CloseWait, ct))
            {
                if (!closed.Contains(tool.Name))
                    closed.Add(tool.Name);
            }
            else
            {
                Forget(path);
                if (!couldNot.Contains(tool.Name))
                    couldNot.Add(tool.Name);
            }
        }
        return new ToolsPauseResult(closed, couldNot);
    }

    /// <summary>Starts again every program Otium closed (the ones still gone), and forgets them. Returns their names.</summary>
    public IReadOnlyList<string> Restore()
    {
        List<ClosedTool> pending;
        lock (_lock)
        {
            _state = _store.Load();
            pending = [.. _state.Closed];
            _state.Closed.Clear();
            _store.Save(_state);
        }
        var running = _processes.FindRunning();
        var started = new List<string>();
        foreach (var tool in pending)
        {
            // Back already (the user started it themselves)?
            if (running.Any(r => string.Equals(r.ExePath, tool.ExePath, StringComparison.OrdinalIgnoreCase) || r.Name == tool.Name))
                continue;
            if (_processes.Start(tool.ExePath))
                started.Add(tool.Name);
        }
        return started;
    }

    private void Forget(string path)
    {
        lock (_lock)
        {
            if (_state.Closed.RemoveAll(c => string.Equals(c.ExePath, path, StringComparison.OrdinalIgnoreCase)) > 0)
                _store.Save(_state);
        }
    }
}

/// <summary>The real thing, on Windows: looks for the programs by name, closes them and starts them again.</summary>
public sealed class WindowsToolProcesses : IToolProcesses
{
    public IReadOnlyList<RunningTool> FindRunning()
    {
        var found = new List<RunningTool>();
        foreach (var tool in TuningToolCatalog.Known)
        {
            foreach (var name in tool.ProcessNames)
            {
                foreach (var process in Process.GetProcessesByName(name))
                {
                    using (process)
                        found.Add(new RunningTool(tool.Name, name, process.Id, PathOf(process)));
                }
            }
        }
        return found;
    }

    public async Task<bool> CloseAsync(RunningTool tool, TimeSpan wait, CancellationToken ct)
    {
        try
        {
            using var process = Process.GetProcessById(tool.ProcessId);
            if (process.HasExited)
                return true;
            process.CloseMainWindow();
            if (await ExitedWithinAsync(process, wait, ct))
                return true;
            // A program that lives in the tray ignores "close": end it. Its settings stay applied; it just stops fighting.
            process.Kill();
            return await ExitedWithinAsync(process, TimeSpan.FromSeconds(4), ct);
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            // Gone already, or Windows won't let a non-administrator end it.
            return !IsRunning(tool.ProcessId);
        }
    }

    public bool Start(string exePath)
    {
        try
        {
            if (!File.Exists(exePath))
                return false;
            using var process = Process.Start(new ProcessStartInfo(exePath)
            {
                UseShellExecute = true,
                WorkingDirectory = Path.GetDirectoryName(exePath) ?? "",
                WindowStyle = ProcessWindowStyle.Minimized,
            });
            return true;
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException or IOException)
        {
            return false;
        }
    }

    private static string? PathOf(Process process)
    {
        try
        {
            return process.MainModule?.FileName;
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException or NotSupportedException)
        {
            return null; // it runs as administrator
        }
    }

    private static async Task<bool> ExitedWithinAsync(Process process, TimeSpan wait, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(wait);
        try
        {
            await process.WaitForExitAsync(timeout.Token);
            return true;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return process.HasExited;
        }
    }

    private static bool IsRunning(int id)
    {
        try
        {
            using var process = Process.GetProcessById(id);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
