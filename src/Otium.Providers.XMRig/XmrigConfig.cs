using System.Text.Json;
using System.Text.Json.Nodes;
using Otium.Core.Providers;

namespace Otium.Providers.XMRig;

public sealed record PoolEndpoint(string Host, int Port, bool Tls)
{
    /// <summary>MoneroOcean pays straight to the user's own address (non-custodial). Port 20128 is its TLS port.</summary>
    public static readonly PoolEndpoint MoneroOcean = new("gulf.moneroocean.stream", 20128, Tls: true);

    public override string ToString() => $"{Host}:{Port}";
}

internal static class XmrigConfig
{
    /// <summary>XMRig's minimum donation level for official builds; disclosed to the user alongside the app fee.</summary>
    public const int DonateLevelPercent = 1;

    public static string Build(ProviderRunOptions options, PoolEndpoint pool, int httpPort, string accessToken)
    {
        // The server's route (balance accounts) decides pool and login; otherwise mine to the target wallet on the
        // default pool with the worker name as the password, as MoneroOcean expects.
        var route = options.Pool;
        var url = route is null ? pool.ToString() : $"{route.Host}:{route.Port}";
        var user = route?.User ?? options.Target.WalletAddress;
        var pass = route?.Password ?? SanitizeWorker(options.WorkerName);
        var tls = route?.Tls ?? pool.Tls;

        var config = new JsonObject
        {
            ["autosave"] = false,
            ["background"] = false,
            ["colors"] = false,
            ["donate-level"] = DonateLevelPercent,
            ["donate-over-proxy"] = 0,
            ["print-time"] = 60,
            ["http"] = new JsonObject
            {
                ["enabled"] = true,
                ["host"] = "127.0.0.1",
                ["port"] = httpPort,
                ["access-token"] = accessToken,
                ["restricted"] = true,
            },
            ["cpu"] = new JsonObject
            {
                ["enabled"] = true,
                ["huge-pages"] = true,
                ["yield"] = true,
                // Lowest scheduling priority so games and work always win.
                ["priority"] = 0,
                ["max-threads-hint"] = Math.Clamp(options.UsagePercent, 1, 100),
            },
            ["opencl"] = false,
            ["cuda"] = false,
            ["pools"] = new JsonArray
            {
                new JsonObject
                {
                    ["url"] = url,
                    ["user"] = user,
                    ["pass"] = pass,
                    ["keepalive"] = true,
                    ["tls"] = tls,
                },
            },
        };
        return config.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    internal static string SanitizeWorker(string worker)
    {
        var cleaned = new string(worker.Where(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_').ToArray());
        return cleaned.Length == 0 ? "otium" : cleaned[..Math.Min(cleaned.Length, 32)];
    }
}
