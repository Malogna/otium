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
    /// <param name="gpus">The cards that mine this coin.</param>
    /// <param name="allGpus">Every card in the computer (defaults to <paramref name="gpus"/>): the rest are left out.</param>
    /// <param name="deviceIds">SRBMiner's own card numbers by PCI address (<see cref="ParseDeviceList"/>), needed only
    /// when some but not all cards of one vendor mine.</param>
    public static IReadOnlyList<string> Build(
        GpuAlgorithm algorithm,
        PoolLogin login,
        IReadOnlyList<GpuInfo> gpus,
        bool lowPriority = true,
        IReadOnlyList<GpuInfo>? allGpus = null,
        IReadOnlyDictionary<string, int>? deviceIds = null)
    {
        var all = allGpus ?? gpus;
        var args = new List<string>
        {
            "--algorithm", algorithm.MinerName,
            "--pool", login.Pool.ToString(),
            "--wallet", login.Wallet,
            "--password", login.Password,
            "--disable-cpu", // the CPU belongs to the XMRig provider
            // No --api-enable: SRBMiner's statistics API always listens on every network interface (3.7.6 has no
            // bind option; "127.0.0.1:port" is read as port 127), answers any website (Access-Control-Allow-Origin: *)
            // and shows the payout address. Otium reads the hashrate and shares from the console instead.
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
        // Some cards of a vendor mine and others don't (a card the coin doesn't accept, or one short of memory): name
        // the ones that do by SRBMiner's numbers, matched by PCI address since SRBMiner's order isn't NVML's.
        if (!WholeVendors(gpus, all) && Ids(gpus, deviceIds) is { } ids)
            args.AddRange(["--gpu-id", string.Join(",", ids)]);
        return args;
    }

    /// <summary>Whether SRBMiner can be told to mine with exactly these of <paramref name="all"/> cards.</summary>
    public static bool CanSelect(IReadOnlyList<GpuInfo> gpus, IReadOnlyList<GpuInfo> all, IReadOnlyDictionary<string, int>? deviceIds) =>
        gpus.Count > 0 && (WholeVendors(gpus, all) || Ids(gpus, deviceIds) is not null);

    /// <summary>The chosen cards are every card of their vendors, so the vendor switches alone select them.</summary>
    private static bool WholeVendors(IReadOnlyList<GpuInfo> gpus, IReadOnlyList<GpuInfo> all) =>
        all.Where(g => gpus.Any(c => c.Vendor == g.Vendor)).All(g => gpus.Contains(g));

    private static IReadOnlyList<int>? Ids(IReadOnlyList<GpuInfo> gpus, IReadOnlyDictionary<string, int>? deviceIds)
    {
        if (deviceIds is null)
            return null;
        var ids = new List<int>();
        foreach (var gpu in gpus)
        {
            if (gpu.PciBus is not { } bus || !deviceIds.TryGetValue(bus, out var id))
                return null;
            ids.Add(id);
        }
        return ids;
    }

    /// <summary>
    /// SRBMiner's card numbers by PCI address, from <c>--list-devices</c>, whose lines read like
    /// "GPU0  [CUDA][0] [0000:01:00.0] : nvidia_geforce_rtx_3080 …".
    /// </summary>
    public static IReadOnlyDictionary<string, int> ParseDeviceList(string output)
    {
        var ids = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(output,
                     @"^\s*GPU(\d+)\b.*?\[([0-9a-fA-F]{1,8}:[0-9a-fA-F]{1,2}:[0-9a-fA-F]{1,2}\.[0-7])\]", System.Text.RegularExpressions.RegexOptions.Multiline))
        {
            if (GpuInfo.NormalizePciBus(m.Groups[2].Value) is { } bus)
                ids.TryAdd(bus, int.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture));
        }
        return ids;
    }

    internal static string SanitizeWorker(string worker) => PayoutRouting.SanitizeWorker(worker);
}
