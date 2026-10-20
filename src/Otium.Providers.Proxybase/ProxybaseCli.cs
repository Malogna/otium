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
        try
        {
            // Above the miners, which run at normal priority on every thread while the user is away: Proxybase times its
            // test requests through this computer and stops sending buyers to slow ones. The client itself uses little CPU.
            process.PriorityClass = ProcessPriorityClass.AboveNormal;
        }
        catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception or PlatformNotSupportedException)
        {
            // already gone, or not allowed here; it still runs at normal priority
        }
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
        LimitEnvironment(start.Environment, dataDir);
        return start;
    }

    /// <summary>
    /// What the client may see of Otium's environment, by name: what any program needs to run, find its folders and
    /// reach the internet through the user's proxy. Not the rest, which can hold tokens (the client sends
    /// GITHUB_TOKEN/GH_TOKEN with its update checks) or settings that change it (PROXYBASE_PASSWORD,
    /// PROXYBASE_UPDATE_REPO, PROXYBASE_BACKEND…).
    /// </summary>
    private static readonly HashSet<string> PassedThrough = new(StringComparer.OrdinalIgnoreCase)
    {
        "SystemRoot", "windir", "SystemDrive", "ComSpec", "PATH", "PATHEXT", "TEMP", "TMP", "TMPDIR",
        "USERPROFILE", "HOME", "HOMEDRIVE", "HOMEPATH", "APPDATA", "LOCALAPPDATA", "ProgramData",
        "ProgramFiles", "ProgramFiles(x86)", "ProgramW6432", "CommonProgramFiles", "NUMBER_OF_PROCESSORS",
        "PROCESSOR_ARCHITECTURE", "OS", "LANG", "LC_ALL", "XDG_RUNTIME_DIR", "SSL_CERT_FILE", "SSL_CERT_DIR",
        "HTTP_PROXY", "HTTPS_PROXY", "ALL_PROXY", "NO_PROXY",
    };

    /// <summary>Leaves only <see cref="PassedThrough"/>, then points the client at its own folder.</summary>
    public static void LimitEnvironment(IDictionary<string, string?> environment, string dataDir)
    {
        foreach (var name in environment.Keys.Where(k => !PassedThrough.Contains(k)).ToList())
            environment.Remove(name);
        environment["PROXYBASE_DIR"] = dataDir;
        environment["NO_COLOR"] = "1";
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

    /// <summary>This computer as Proxybase sees it, from <c>seller status</c>; null when the backend didn't answer.</summary>
    public static ProxybaseNode? Node(string output)
    {
        if (Json(output) is not { ValueKind: JsonValueKind.Object } json || !json.TryGetProperty("active_streams", out var streams))
            return null;
        static double? Number(JsonElement e, string name) =>
            e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : null;
        static string? Text(JsonElement e, string name) =>
            e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
        var pending = json.TryGetProperty("earnings", out var earnings) && Number(earnings, "seller_pending") is { } p ? (long)p : 0;
        return new ProxybaseNode(
            streams.ValueKind == JsonValueKind.Number ? streams.GetInt32() : 0,
            (int)(Number(json, "max_streams") ?? 0),
            Text(json, "country"),
            Text(json, "proxy_category"),
            Number(json, "qos_latency_ms"),
            Number(json, "qos_uptime"),
            pending,
            Text(json, "pool"));
    }

    /// <summary>Buyer sessions and sellers across Proxybase, from <c>health</c>; null when the backend didn't answer.</summary>
    public static ProxybaseNetwork? Network(string output) =>
        Json(output) is { ValueKind: JsonValueKind.Object } json
        && json.TryGetProperty("active_sessions", out var sessions) && sessions.TryGetInt32(out var buyers)
        && json.TryGetProperty("connected_sellers", out var connected) && connected.TryGetInt32(out var sellers)
            ? new ProxybaseNetwork(buyers, sellers)
            : null;

    /// <summary>The payout id from <c>seller payout create</c>, or null when it wasn't created.</summary>
    public static string? PayoutId(string output) =>
        Json(output) is { ValueKind: JsonValueKind.Object } json && json.TryGetProperty("payout_id", out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString()
            : null;

    /// <summary>A 0x address (Tempo is EVM-compatible), checked before Otium pays anything to one.</summary>
    public static bool IsEvmAddress(string? value) => value is not null && EvmAddress().IsMatch(value);

    [GeneratedRegex(@"Address:\s*(0x[0-9a-fA-F]{40})\b")]
    private static partial Regex AddressLine();

    [GeneratedRegex(@"^0x[0-9a-fA-F]{40}$")]
    private static partial Regex EvmAddress();
}
