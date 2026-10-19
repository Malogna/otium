using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using Otium.Core.Providers;

namespace Otium.Providers.SrbMiner;

/// <summary>One running miner instance. Abstracted so the switching logic can be tested without real GPUs.</summary>
public interface IMinerProcess : IAsyncDisposable
{
    bool HasExited { get; }

    int? ExitCode { get; }

    IReadOnlyList<string> RecentLog { get; }

    /// <summary>Current total hashrate in H/s, or null if the miner hasn't reported one yet.</summary>
    Task<double?> ReadHashrateAsync(CancellationToken ct);

    /// <summary>Shares the pool has accepted and rejected since launch, as of the last hashrate read; null if unknown.</summary>
    ShareCounts? Shares => null;
}

/// <summary>Shares sent to the pool since the miner started.</summary>
public sealed record ShareCounts(long Accepted, long Rejected)
{
    public long Total => Accepted + Rejected;
}

public interface IMinerProcessFactory
{
    /// <param name="lowPriority">Below normal priority, because the user may be at the computer (see ProviderRunOptions).</param>
    IMinerProcess Start(string binary, IReadOnlyList<string> arguments, bool lowPriority);
}

internal sealed class SrbMinerProcessFactory : IMinerProcessFactory
{
    private const int ErrorVirusInfected = 225;
    private const int ErrorVirusDeleted = 226;

    public IMinerProcess Start(string binary, IReadOnlyList<string> arguments, bool lowPriority)
    {
        var startInfo = new ProcessStartInfo(binary)
        {
            WorkingDirectory = Path.GetDirectoryName(binary)!,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);

        try
        {
            var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Could not start the GPU miner.");
            MinerLifetime.Adopt(process); // it and its watchdog child die with Otium, even if Otium crashes
            try
            {
                if (lowPriority)
                    process.PriorityClass = ProcessPriorityClass.BelowNormal;
            }
            catch (Exception e) when (e is InvalidOperationException or Win32Exception)
            {
            }
            return new SrbMinerProcess(process);
        }
        catch (Win32Exception e) when (e.NativeErrorCode is ErrorVirusInfected or ErrorVirusDeleted)
        {
            throw new EngineBlockedException("Windows Security blocked the GPU miner from starting.", Path.GetDirectoryName(binary)!);
        }
    }
}

internal sealed class SrbMinerProcess : IMinerProcess
{
    private readonly Process _process;
    private readonly ConcurrentQueue<string> _log = new();

    public SrbMinerProcess(Process process)
    {
        _process = process;
        _process.OutputDataReceived += (_, e) => Append(e.Data);
        _process.ErrorDataReceived += (_, e) => Append(e.Data);
        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();
    }

    public bool HasExited => _process.HasExited;

    public int? ExitCode => _process.HasExited ? _process.ExitCode : null;

    public IReadOnlyList<string> RecentLog => _log.ToArray();

    public ShareCounts? Shares { get; private set; }

    /// <summary>
    /// From the console: SRBMiner's statistics API can't be limited to this computer, so it isn't turned on (see
    /// SrbMinerArgs). SRBMiner prints a "Total" line every 30 s; an older one means the miner has stopped (e.g. frozen).
    /// </summary>
    public Task<double?> ReadHashrateAsync(CancellationToken ct)
    {
        var recent = RecentLog.TakeLast(40).ToList();
        Shares = SrbMinerStats.ParseConsoleShares(recent) ?? Shares;
        return Task.FromResult(SrbMinerStats.ParseConsoleHashrate(recent, DateTime.Now - TimeSpan.FromSeconds(90)));
    }

    private void Append(string? line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return;
        _log.Enqueue(line);
        while (_log.Count > 300 && _log.TryDequeue(out _))
        {
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                await _process.WaitForExitAsync(timeout.Token);
            }
        }
        catch (Exception e) when (e is InvalidOperationException or OperationCanceledException)
        {
        }
        finally
        {
            _process.Dispose();
        }
    }
}
