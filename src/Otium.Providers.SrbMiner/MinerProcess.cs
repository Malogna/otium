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

    /// <summary>The intensity the miner picked by itself (its startup line), or null when it didn't say or tunes itself.</summary>
    int? AutoIntensity => null;
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
        // SRBMiner holds its console output back when it is piped (all of it arrives at once when it exits), so a
        // miner that prints a line every few seconds looks silent for minutes. Its log file is written as it goes.
        var logFile = Path.Combine(Path.GetDirectoryName(binary)!, $"otium-{Guid.NewGuid():N}.log");
        startInfo.ArgumentList.Add("--log-file");
        startInfo.ArgumentList.Add(logFile);

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
            return new SrbMinerProcess(process, logFile);
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
    private readonly string? _logFile;
    private readonly CancellationTokenSource _stopTailing = new();
    private readonly Task _tail = Task.CompletedTask;

    /// <summary>Timestamped lines already taken, from the console or the log file, whichever came first (both carry them).</summary>
    private readonly HashSet<string> _seen = [];

    /// <param name="logFile">The file SRBMiner was told to log to, read as it grows; null to use the console only.</param>
    public SrbMinerProcess(Process process, string? logFile = null)
    {
        _process = process;
        _logFile = logFile;
        _process.OutputDataReceived += (_, e) => Append(e.Data);
        _process.ErrorDataReceived += (_, e) => Append(e.Data);
        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();
        if (logFile is not null)
            _tail = Task.Run(() => TailAsync(logFile, _stopTailing.Token));
    }

    /// <summary>Follows the log file, handing each new line to <see cref="Append"/>, until stopped.</summary>
    private async Task TailAsync(string path, CancellationToken ct)
    {
        var pending = "";
        long position = 0;
        while (!ct.IsCancellationRequested)
        {
            try
            {
                if (File.Exists(path))
                {
                    using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                    if (stream.Length < position)
                        position = 0; // truncated
                    stream.Seek(position, SeekOrigin.Begin);
                    using var reader = new StreamReader(stream, System.Text.Encoding.UTF8);
                    var text = await reader.ReadToEndAsync(ct);
                    position = stream.Position;
                    pending += text;
                    int newline;
                    while ((newline = pending.IndexOf('\n')) >= 0)
                    {
                        Append(pending[..newline].TrimEnd('\r'));
                        pending = pending[(newline + 1)..];
                    }
                }
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // Being written or just removed: next round.
            }
            catch (OperationCanceledException)
            {
                return;
            }

            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(500), ct);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    public bool HasExited => _process.HasExited;

    public int? ExitCode => _process.HasExited ? _process.ExitCode : null;

    public IReadOnlyList<string> RecentLog => _log.ToArray();

    public ShareCounts? Shares { get; private set; }

    public int? AutoIntensity { get; private set; }

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
        // A line the console and the log file both carry (they begin with a timestamp) is kept once.
        if (_logFile is not null && line.StartsWith('['))
        {
            lock (_seen)
            {
                if (_seen.Count > 2000)
                    _seen.Clear();
                if (!_seen.Add(line))
                    return;
            }
        }
        _log.Enqueue(line);
        if (SrbMinerStats.ParseIntensity(line, out _) is { } intensity)
            AutoIntensity ??= intensity; // the first card's (Otium sets one value for all of them)
        while (_log.Count > 300 && _log.TryDequeue(out _))
        {
        }
    }

    private async Task StopTailingAsync()
    {
        _stopTailing.Cancel();
        try
        {
            await _tail.WaitAsync(TimeSpan.FromSeconds(5));
        }
        catch (Exception e) when (e is TimeoutException or OperationCanceledException)
        {
        }
        _stopTailing.Dispose();
        if (_logFile is not null)
        {
            try
            {
                File.Delete(_logFile); // it holds the wallet address; nothing needs it afterwards
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
            }
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
            await StopTailingAsync();
        }
    }
}
