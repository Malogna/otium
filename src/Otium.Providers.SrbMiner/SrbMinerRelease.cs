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
    public const string Version = "3.7.6";

    private static readonly SrbMinerAsset WindowsX64 = new("SRBMiner-Multi-3-7-6-win64.zip", "fb0ed55977aad70b6efb45e14c2c41263c557f1df64fb02a1396c40c99ebae3b");
    private static readonly SrbMinerAsset LinuxX64 = new("SRBMiner-Multi-3-7-6-Linux.tar.gz", "5d3f5a30f09b17c828fe9881141ca3b2fce8471422e5d88b074ab654dc456674");

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
public sealed record GpuAlgorithm(
    string PoolName,
    string MinerName,
    double DevFeePercent,
    double MinMemoryGb,
    IReadOnlySet<GpuVendor> Vendors,
    string DefaultTier = GpuTiers.FreePowerOnly,
    string DefaultRoute = GpuRoutes.MoneroOcean)
{
    public bool Supports(IReadOnlyList<GpuInfo> gpus) =>
        gpus.Count > 0 && gpus.All(g => Vendors.Contains(g.Vendor) && (g.MemoryGb <= 0 || g.MemoryGb + 0.25 >= MinMemoryGb));
}

/// <summary>
/// GPU algorithms SRBMiner can mine. The server's GPU policy decides which are mined and where; as of October 2026
/// the MoneroOcean algorithms lose money at normal electricity prices, so they only run on free power. Pearl, Xelis
/// and FishHash (Iron Fish) are mined on unMineable, paid to the user's own address in their chosen coin; balance
/// accounts mine Pearl and Quantus on Kryptex Pool instead. Dev fees are SRBMiner's (README, October 2026).
/// </summary>
public static class GpuAlgorithms
{
    private static readonly IReadOnlySet<GpuVendor> NvidiaAmd = new HashSet<GpuVendor> { GpuVendor.Nvidia, GpuVendor.Amd };

    public static readonly IReadOnlyList<GpuAlgorithm> All =
    [
        new("pearlhash", "pearlhash", 2.0, 8, NvidiaAmd, GpuTiers.Primary, GpuRoutes.Unmineable),
        new("quantus", "quantus", 2.0, 2, NvidiaAmd, GpuTiers.Primary, GpuRoutes.Kryptex),
        new("xelishash", "xelishashv3", 1.5, 2, NvidiaAmd, GpuTiers.FreePowerOnly, GpuRoutes.Unmineable),
        new("fishhash", "fishhash", 0.85, 4, NvidiaAmd, GpuTiers.FreePowerOnly, GpuRoutes.Unmineable),
        new("kawpow", "kawpow", 0.85, 6, NvidiaAmd),
        new("autolykos2", "autolykos2", 1.0, 4, NvidiaAmd),
        new("etchash", "etchash", 0.65, 4, NvidiaAmd),
        new("cn/gpu", "cryptonight_gpu", 0.85, 2, NvidiaAmd),
    ];

    public static GpuAlgorithm? Find(string poolName) =>
        All.FirstOrDefault(a => string.Equals(a.PoolName, poolName, StringComparison.OrdinalIgnoreCase));
}
