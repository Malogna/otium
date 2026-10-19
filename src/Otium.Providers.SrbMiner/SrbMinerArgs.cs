using Otium.Core.Gpu;
using Otium.Core.Pools;

namespace Otium.Providers.SrbMiner;

public sealed record GpuPoolEndpoint(string Host, int Port, bool Tls = false)
{
    /// <summary>
    /// MoneroOcean's plain stratum port. GPU algorithms must be pinned with "worker~algo" in the password;
    /// the TLS port uses a self-signed certificate, which not every miner accepts, so GPU mining uses plain TCP.
    /// </summary>
    public static readonly GpuPoolEndpoint MoneroOcean = new("gulf.moneroocean.stream", 10128);

    public override string ToString() => $"{Host}:{Port}";
}

/// <summary>How the miner logs in to a pool.</summary>
/// <param name="Worker">Sent as SRBMiner's --worker when the pool takes the worker name separately (Kryptex's address login).</param>
public sealed record PoolLogin(GpuPoolEndpoint Pool, string Wallet, string Password, string? Worker = null)
{
    /// <summary>MoneroOcean: the account's wallet, with the worker and algorithm pinned in the password.</summary>
    public static PoolLogin MoneroOcean(GpuPoolEndpoint pool, string wallet, string worker, GpuAlgorithm algorithm) =>
        new(pool, wallet, $"{SrbMinerArgs.SanitizeWorker(worker)}~{algorithm.PoolName}");

    /// <summary>Kryptex: "account.worker", so the pool reports this worker's hashrate separately for crediting.</summary>
    public static PoolLogin Kryptex(GpuPoolEndpoint pool, string account, string worker) =>
        new(pool, $"{account}.{SrbMinerArgs.SanitizeWorker(worker)}", "x");

    /// <summary>
    /// Kryptex without an account: the user's own coin address as the wallet and the worker passed separately, as
    /// Kryptex's Quantus guide shows ("--wallet qz… --worker …"). Kryptex pays the address in the coin itself.
    /// </summary>
    public static PoolLogin KryptexAddress(GpuPoolEndpoint pool, string address, string worker) =>
        new(pool, address, "x", SrbMinerArgs.SanitizeWorker(worker));

    /// <summary>unMineable: "COIN:address.worker#referral", paid to the slice's own target in its coin.</summary>
    public static PoolLogin Unmineable(GpuPoolEndpoint pool, string target, string worker, string? referralCode) =>
        new(pool, PayoutRouting.UnmineableLogin(target, worker, referralCode), "x");
}

internal static class SrbMinerArgs
{
    public static IReadOnlyList<string> Build(
        GpuAlgorithm algorithm,
        PoolLogin login,
        int apiPort,
        IReadOnlyList<GpuInfo> gpus,
        bool lowPriority = true)
    {
        var args = new List<string>
        {
            "--algorithm", algorithm.MinerName,
            "--pool", login.Pool.ToString(),
            "--wallet", login.Wallet,
            "--password", login.Password,
            "--disable-cpu", // the CPU belongs to the XMRig provider
            "--api-enable",
            "--api-port", apiPort.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "--api-rig-name", "otium",
            // SRBMiner sets its own priority (and its watchdog's): below normal (2) while the user may be at the
            // computer, normal (3) while they're away. Without it, it was seen lowering itself while the user was away.
            "--miner-priority", lowPriority ? "2" : "3",
        };
        if (login.Pool.Tls)
            args.AddRange(["--tls", "true"]);
        if (login.Worker is { Length: > 0 } worker)
            args.AddRange(["--worker", worker]);

        if (gpus.All(g => g.Vendor != GpuVendor.Nvidia))
            args.Add("--disable-gpu-nvidia");
        if (gpus.All(g => g.Vendor != GpuVendor.Amd))
            args.Add("--disable-gpu-amd");
        args.Add("--disable-gpu-intel");
        return args;
    }

    internal static string SanitizeWorker(string worker) => PayoutRouting.SanitizeWorker(worker);
}
