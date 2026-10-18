using System.Text.Json;
using System.Text.Json.Nodes;
using Otium.Core.Pools;
using Otium.Core.Providers;

namespace Otium.Providers.XMRig;

public sealed record PoolEndpoint(string Host, int Port, bool Tls)
{
    /// <summary>MoneroOcean pays straight to the user's own address (non-custodial). Port 20128 is its TLS port.</summary>
    public static readonly PoolEndpoint MoneroOcean = new("gulf.moneroocean.stream", 20128, Tls: true);

    /// <summary>unMineable's RandomX endpoint, which pays every coin, Monero included outside the US (TLS only on 443).</summary>
    public static readonly PoolEndpoint Unmineable = new(PayoutRouting.UnmineableCpuHost, PayoutRouting.UnmineableTlsPort, Tls: true);

    public override string ToString() => $"{Host}:{Port}";
}

internal static class XmrigConfig
{
    /// <summary>XMRig's minimum donation level for official builds; disclosed to the user alongside the app fee.</summary>
    public const int DonateLevelPercent = 1;

    /// <param name="pool">The MoneroOcean endpoint (or a simulated pool in dev mode) for US users' Monero.</param>
    /// <param name="unmineable">The unMineable endpoint for everything else; defaults to <see cref="PoolEndpoint.Unmineable"/>.</param>
    /// <param name="unitedStates">The user is in the US, where Monero mines on MoneroOcean.</param>
    public static string Build(
        ProviderRunOptions options,
        PoolEndpoint pool,
        int httpPort,
        string accessToken,
        string? referralCode = null,
        PoolEndpoint? unmineable = null,
        bool unitedStates = false)
    {
        // The server's route (balance accounts) decides pool and login. Otherwise the slice's payout target does: every
        // coin mines on unMineable as "COIN:address.worker#referral" (Monero too, paid in XMR), except US users'
        // Monero, which mines on MoneroOcean to the bare address with the worker name as the password.
        var target = options.Target.WalletAddress;
        var route = options.Pool;
        string url, user, pass;
        bool tls;
        if (route is not null)
            (url, user, pass, tls) = ($"{route.Host}:{route.Port}", route.User, route.Password, route.Tls);
        else if (PayoutRouting.PoolFor(target, unitedStates) == PayoutPool.Unmineable)
        {
            var endpoint = unmineable ?? PoolEndpoint.Unmineable;
            (url, user, pass, tls) = (endpoint.ToString(), PayoutRouting.UnmineableLogin(target, options.WorkerName, referralCode), "x", endpoint.Tls);
        }
        else
            (url, user, pass, tls) = (pool.ToString(), PayoutRouting.MoneroOceanWallet(target), SanitizeWorker(options.WorkerName), pool.Tls);

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
                // Unrestricted only for pause and resume; the API listens on loopback and needs the per-run token.
                ["restricted"] = false,
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

    internal static string SanitizeWorker(string worker) => PayoutRouting.SanitizeWorker(worker);
}
