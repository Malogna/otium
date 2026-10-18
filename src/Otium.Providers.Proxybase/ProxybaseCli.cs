using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Otium.Core.Providers;

namespace Otium.Providers.Proxybase;

public sealed record CliResult(int ExitCode, string Output)
{
    public bool Succeeded => ExitCode == 0;
}

/// <summary>The long-running seller relay.</summary>
public interface ISellerProcess : IDisposable
{
    bool HasExited { get; }

    /// <summary>Its last lines of output, for explaining why it stopped.</summary>
    string RecentOutput { get; }

    Task StopAsync(CancellationToken ct);
}

/// <summary>Runs the Proxybase client with Otium's own data folder (its wallet key, session and settings live there).</summary>
public interface IProxybaseCli
{
    Task<CliResult> RunAsync(IReadOnlyList<string> arguments, CancellationToken ct);

    ISellerProcess StartSeller();
}

/// <summary>The real client: the pinned binary, with PROXYBASE_DIR pointing at Otium's folder for it.</summary>
public sealed class ProxybaseCli(Func<string?> binary, string dataDir) : IProxybaseCli
{
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(45);

    public async Task<CliResult> RunAsync(IReadOnlyList<string> arguments, CancellationToken ct)
    {
        using var process = Process.Start(StartInfo(arguments)) ?? throw new InvalidOperationException("Could not start the Proxybase client.");
        var output = new StringBuilder();
        process.OutputDataReceived += (_, e) => Append(output, e.Data);
        process.ErrorDataReceived += (_, e) => Append(output, e.Data);
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(CommandTimeout);
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            process.Kill(entireProcessTree: true);
            return new CliResult(-1, "The Proxybase client didn't answer in time.");
        }
        process.WaitForExit(); // flushes the redirected output
        lock (output)
            return new CliResult(process.ExitCode, output.ToString());
    }

    public ISellerProcess StartSeller()
    {
        // Direct mode with the saved config (no upstream proxies), kept in the foreground so Otium owns the process.
        var process = Process.Start(StartInfo(["seller", "start", "--foreground"])) ?? throw new InvalidOperationException("Could not start the Proxybase client.");
        MinerLifetime.Adopt(process); // ends with Otium, even if Otium crashes
        return new SellerProcess(process);
    }

    private ProcessStartInfo StartInfo(IReadOnlyList<string> arguments)
    {
        var path = binary() ?? throw new InvalidOperationException("The Proxybase client isn't installed.");
        var start = new ProcessStartInfo(path)
        {
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = dataDir,
        };
        foreach (var argument in arguments)
            start.ArgumentList.Add(argument);
        start.Environment["PROXYBASE_DIR"] = dataDir;
        start.Environment["NO_COLOR"] = "1";
        return start;
    }

    private static void Append(StringBuilder output, string? line)
    {
        if (line is null)
            return;
        lock (output)
            output.AppendLine(line);
    }

    private sealed class SellerProcess : ISellerProcess
    {
        private readonly Process _process;
        private readonly ConcurrentQueue<string> _lines = new();

        public SellerProcess(Process process)
        {
            _process = process;
            _process.OutputDataReceived += (_, e) => Keep(e.Data);
            _process.ErrorDataReceived += (_, e) => Keep(e.Data);
            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();
        }

        public bool HasExited => _process.HasExited;

        public string RecentOutput => string.Join(" ", _lines.TakeLast(3));

        private void Keep(string? line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return;
            _lines.Enqueue(line.Trim());
            while (_lines.Count > 50 && _lines.TryDequeue(out _))
            {
            }
        }

        public async Task StopAsync(CancellationToken ct)
        {
            try
            {
                if (!_process.HasExited)
                {
                    _process.Kill(entireProcessTree: true);
                    await _process.WaitForExitAsync(ct);
                }
            }
            catch (InvalidOperationException)
            {
                // Already gone.
            }
        }

        public void Dispose() => _process.Dispose();
    }
}

/// <summary>Reading the client's output, which is plain text with pretty-printed JSON for API answers.</summary>
public static partial class ProxybaseOutput
{
    /// <summary>"Address: 0x…" from <c>wallet create</c> or <c>wallet info</c>; null when there's no wallet.</summary>
    public static string? Address(string output) =>
        AddressLine().Match(output) is { Success: true } m ? m.Groups[1].Value.ToLowerInvariant() : null;

    /// <summary>The JSON document in an answer like <c>buyer balance</c>'s, which may follow other lines.</summary>
    public static JsonElement? Json(string output)
    {
        var start = output.IndexOfAny(['{', '[']);
        if (start < 0)
            return null;
        try
        {
            using var doc = JsonDocument.Parse(output[start..]);
            return doc.RootElement.Clone();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>The seller earnings available to pay out, in microcredits (1,000,000 = $1), from <c>buyer balance</c>.</summary>
    public static long? SellerAvailable(string output) =>
        Json(output) is { ValueKind: JsonValueKind.Object } json && json.TryGetProperty("seller_available", out var v) && v.TryGetInt64(out var micro)
            ? micro
            : null;

    /// <summary>The payout id from <c>seller payout create</c>, or null when it wasn't created.</summary>
    public static string? PayoutId(string output) =>
        Json(output) is { ValueKind: JsonValueKind.Object } json && json.TryGetProperty("payout_id", out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString()
            : null;

    [GeneratedRegex(@"Address:\s*(0x[0-9a-fA-F]{40})\b")]
    private static partial Regex AddressLine();
}
