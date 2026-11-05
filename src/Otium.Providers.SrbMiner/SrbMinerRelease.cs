using System.Runtime.InteropServices;
using Otium.Core.Gpu;
using Otium.Core.Switching;

namespace Otium.Providers.SrbMiner;

public sealed record SrbMinerAsset(string FileName, string Sha256)
{
    public Uri DownloadUrl => new($"https://github.com/doktor83/SRBMiner-Multi/releases/download/{SrbMinerRelease.Version}/{FileName}");

    public bool IsZip => FileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Pinned SRBMiner-Multi build (closed source, free to download; we fetch it from the official GitHub release at
/// runtime rather than redistributing it). Hashes are GitHub's published SHA-256 digests for the release assets.
/// </summary>
public static class SrbMinerRelease
{
    public const string Version = "3.8.3";

    private static readonly SrbMinerAsset WindowsX64 = new("SRBMiner-Multi-3-8-3-win64.zip", "92af8d2db5bc96df62825487e7f384b8ab04e5db26a634e29c2cd86dcbb87d0e");
    private static readonly SrbMinerAsset LinuxX64 = new("SRBMiner-Multi-3-8-3-Linux.tar.gz", "c9e10baae24d1811c04d30a416e237774e9e16110276c864a4221395f9f55e8b");

    public static SrbMinerAsset? ForCurrentPlatform() => For(
        OperatingSystem.IsWindows() ? OSPlatform.Windows : OperatingSystem.IsLinux() ? OSPlatform.Linux : OSPlatform.OSX,
        RuntimeInformation.ProcessArchitecture);

    /// <summary>SRBMiner ships Windows and Linux x64 builds only; macOS GPUs (Metal) aren't supported.</summary>
    public static SrbMinerAsset? For(OSPlatform os, Architecture arch)
    {
        if (arch != Architecture.X64)
            return null;
        if (os == OSPlatform.Windows)
            return WindowsX64;
        if (os == OSPlatform.Linux)
            return LinuxX64;
        return null;
    }

    public static string BinaryName => OperatingSystem.IsWindows() ? "SRBMiner-MULTI.exe" : "SRBMiner-MULTI";
}

/// <param name="PoolName">The algorithm's name in the GPU policy and on MoneroOcean (its "worker~algo" password pin and stats).</param>
/// <param name="MinerName">SRBMiner's --algorithm value.</param>
/// <param name="DevFeePercent">SRBMiner's developer fee for this algorithm.</param>
/// <param name="MinMemoryGb">Approximate VRAM needed (DAG sizes grow over time); cards below it are skipped.</param>
/// <param name="DefaultTier">Tier used when the server's policy can't be fetched (see <see cref="GpuTiers"/>).</param>
/// <param name="DefaultRoute">Pool kind used without a policy; Kryptex and unMineable coins need the policy's route and
/// price, so they wait for it.</param>
/// <param name="WorkingMemoryGb">
/// Graphics memory the miner itself takes for this algorithm, when that's far below <paramref name="MinMemoryGb"/> (a
/// card size, not an allocation); what must be free to start it. Without it, <paramref name="MinMemoryGb"/>.
/// </param>
public sealed record GpuAlgorithm(
    string PoolName,
    string MinerName,
    double DevFeePercent,
    double MinMemoryGb,
    IReadOnlySet<GpuVendor> Vendors,
    string DefaultTier = GpuTiers.FreePowerOnly,
    string DefaultRoute = GpuRoutes.MoneroOcean,
    double? WorkingMemoryGb = null)
{
    /// <summary>Free graphics memory a card needs to start this algorithm (other apps hold the rest).</summary>
    public double FreeMemoryNeededGb => WorkingMemoryGb ?? MinMemoryGb;

    /// <summary>At least one of these cards can mine it (the others are left out of that coin's miner).</summary>
    public bool Supports(IReadOnlyList<GpuInfo> gpus) => gpus.Any(Fits);

    /// <summary>This card can mine it: a vendor the algorithm runs on, and enough graphics memory (unknown counts).</summary>
    public bool Fits(GpuInfo gpu) => Vendors.Contains(gpu.Vendor) && (gpu.MemoryGb <= 0 || gpu.MemoryGb + 0.25 >= MinMemoryGb);
}

/// <summary>
/// GPU algorithms SRBMiner can mine. The server's GPU policy decides which are mined and where. Pearl and Quantus are
/// primary on unMineable, paid to the user's own address in their chosen coin: Quantus is what cards that can't mine
/// Pearl mine. The rest (Xelis on unMineable, the MoneroOcean algorithms for US users) lose money at
/// normal electricity prices as of October 2026, so they run on free power, or as the last resort of a card no primary
/// coin works on, judged on its own measured earnings against the user's electricity. Quantus to the user's own
/// Quantus wallet (Kryptex's address login) is no longer offered (AppHost.QuantusTarget). FishHash stays out: on an RTX
/// 3080 it connects to unMineable but never starts hashing (October 2026, twice). Dev fees are SRBMiner's
/// (README, October 2026), except Quantus: Kryptex's and NurseryPool's guides say 2.5%, not the README's 2%.
/// </summary>
public static class GpuAlgorithms
{
    private static readonly IReadOnlySet<GpuVendor> NvidiaAmd = new HashSet<GpuVendor> { GpuVendor.Nvidia, GpuVendor.Amd };

    public static readonly IReadOnlyList<GpuAlgorithm> All =
    [
        // Pearl is offered on 6 GB cards and up (hashrate.no lists the 6 GB RTX 2060 on it; a card that can't hash it
        // fails over to the next coin), but SRBMiner 3.7.6 takes only about 0.45 GB of graphics memory for it (an RTX
        // 3080, October 2026). Asking for the card's full size free kept every 8 GB card from ever mining it, since
        // Windows and open apps always hold some.
        new("pearlhash", "pearlhash", 2.0, 6, NvidiaAmd, GpuTiers.Primary, GpuRoutes.Unmineable, WorkingMemoryGb: 1),
        // Quantus and Xelis are offered from 2 GB cards, but a 2 GB card never has 2 GB free (Windows holds some: a Quadro P600
        // showed 1.6 GB free with nothing else open, November 2026), so asking for the card's full size kept every such card
        // from mining either. The working memory is an estimate, not measured: a card that is short of it fails the start
        // and the next coin is tried, as for any coin that doesn't run on a card.
        new("quantus", "quantus", 2.5, 2, NvidiaAmd, GpuTiers.Primary, GpuRoutes.Kryptex, WorkingMemoryGb: 1),
        new("xelishash", "xelishashv3", 1.5, 2, NvidiaAmd, GpuTiers.FreePowerOnly, GpuRoutes.Unmineable, WorkingMemoryGb: 1),
        new("kawpow", "kawpow", 0.85, 6, NvidiaAmd),
        new("autolykos2", "autolykos2", 1.0, 4, NvidiaAmd),
        new("etchash", "etchash", 0.65, 4, NvidiaAmd),
        new("cn/gpu", "cryptonight_gpu", 0.85, 2, NvidiaAmd),
    ];

    public static GpuAlgorithm? Find(string poolName) =>
        All.FirstOrDefault(a => string.Equals(a.PoolName, poolName, StringComparison.OrdinalIgnoreCase));
}
