using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
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
}

public interface IMinerProcessFactory
{
    IMinerProcess Start(string binary, IReadOnlyList<string> arguments, int apiPort);
}

internal sealed class SrbMinerProcessFactory(HttpClient http) : IMinerProcessFactory
{
    private const int ErrorVirusInfected = 225;
    private const int ErrorVirusDeleted = 226;

    public IMinerProcess Start(string binary, IReadOnlyList<string> arguments, int apiPort)
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
            try
            {
                process.PriorityClass = ProcessPriorityClass.BelowNormal;
            }
            catch (Exception e) when (e is InvalidOperationException or Win32Exception)
            {
            }
            return new SrbMinerProcess(process, http, apiPort);
        }
        catch (Win32Exception e) when (e.NativeErrorCode is ErrorVirusInfected or ErrorVirusDeleted)
        {
            throw new EngineBlockedException("Windows Security blocked the GPU miner from starting.", Path.GetDirectoryName(binary)!);
        }
    }

    public static int FreeLoopbackPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}

internal sealed class SrbMinerProcess : IMinerProcess
{
    private readonly Process _process;
    private readonly HttpClient _http;
    private readonly int _apiPort;
    private readonly ConcurrentQueue<string> _log = new();

    public SrbMinerProcess(Process process, HttpClient http, int apiPort)
    {
        _process = process;
        _http = http;
        _apiPort = apiPort;
        _process.OutputDataReceived += (_, e) => Append(e.Data);
        _process.ErrorDataReceived += (_, e) => Append(e.Data);
        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();
    }

    public bool HasExited => _process.HasExited;

    public int? ExitCode => _process.HasExited ? _process.ExitCode : null;

    public IReadOnlyList<string> RecentLog => _log.ToArray();

    public async Task<double?> ReadHashrateAsync(CancellationToken ct)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(3));
            var json = await _http.GetStringAsync($"http://127.0.0.1:{_apiPort}/", timeout.Token);
            if (SrbMinerStats.ParseApiHashrate(json) is { } hashrate)
                return hashrate;
        }
        catch (Exception e) when (e is HttpRequestException or OperationCanceledException or System.Text.Json.JsonException && !ct.IsCancellationRequested)
        {
        }
        return SrbMinerStats.ParseConsoleHashrate(RecentLog.TakeLast(40));
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
