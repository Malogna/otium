using Otium.Core.Gpu;

namespace Otium.Providers.SrbMiner;

public sealed record GpuPoolEndpoint(string Host, int Port)
{
    /// <summary>
    /// MoneroOcean's plain stratum port. GPU algorithms must be pinned with "worker~algo" in the password;
    /// the TLS port uses a self-signed certificate, which not every miner accepts, so GPU mining uses plain TCP.
    /// </summary>
    public static readonly GpuPoolEndpoint MoneroOcean = new("gulf.moneroocean.stream", 10128);

    public override string ToString() => $"{Host}:{Port}";
}

/// <summary>How the miner logs in to a pool.</summary>
public sealed record PoolLogin(GpuPoolEndpoint Pool, string Wallet, string Password)
{
    /// <summary>MoneroOcean: the account's wallet, with the worker and algorithm pinned in the password.</summary>
    public static PoolLogin MoneroOcean(GpuPoolEndpoint pool, string wallet, string worker, GpuAlgorithm algorithm) =>
        new(pool, wallet, $"{SrbMinerArgs.SanitizeWorker(worker)}~{algorithm.PoolName}");

    /// <summary>Kryptex: "account.worker", so the pool reports this worker's hashrate separately for crediting.</summary>
    public static PoolLogin Kryptex(GpuPoolEndpoint pool, string account, string worker) =>
        new(pool, $"{account}.{SrbMinerArgs.SanitizeWorker(worker)}", "x");
}

internal static class SrbMinerArgs
{
    public static IReadOnlyList<string> Build(
        GpuAlgorithm algorithm,
        PoolLogin login,
        int apiPort,
        IReadOnlyList<GpuInfo> gpus)
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
        };

        if (gpus.All(g => g.Vendor != GpuVendor.Nvidia))
            args.Add("--disable-gpu-nvidia");
        if (gpus.All(g => g.Vendor != GpuVendor.Amd))
            args.Add("--disable-gpu-amd");
        args.Add("--disable-gpu-intel");
        return args;
    }

    internal static string SanitizeWorker(string worker)
    {
        var cleaned = new string(worker.Where(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_').ToArray());
        return cleaned.Length == 0 ? "otium" : cleaned[..Math.Min(cleaned.Length, 32)];
    }
}
