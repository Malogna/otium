using System.Diagnostics;
using Otium.Core.Providers;

namespace Otium.Providers.XMRig;

/// <summary>One running XMRig. Abstracted so the provider can be tested without starting a real miner.</summary>
internal interface IXmrigProcess : IAsyncDisposable
{
    bool HasExited { get; }

    int ExitCode { get; }
}

/// <summary>The real XMRig process: its output goes to the provider's log, and it dies with Otium.</summary>
internal sealed class XmrigProcess : IXmrigProcess
{
    private readonly Process _process;

    private XmrigProcess(Process process, Action<string?> log)
    {
        _process = process;
        _process.OutputDataReceived += (_, e) => log(e.Data);
        _process.ErrorDataReceived += (_, e) => log(e.Data);
        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();
    }

    public static IXmrigProcess Start(string binary, IReadOnlyList<string> arguments, Action<string?> log)
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

        var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Could not start the mining engine.");
        MinerLifetime.Adopt(process); // dies with Otium, even if Otium crashes
        return new XmrigProcess(process, log);
    }

    public bool HasExited => _process.HasExited;

    public int ExitCode => _process.ExitCode;

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
            // Already exited, or didn't within the timeout.
        }
        finally
        {
            _process.Dispose();
        }
    }
}
