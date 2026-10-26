using System.Runtime.InteropServices;

namespace Otium.Providers.Aioz;

/// <param name="BinaryName">The node's executable inside the archive.</param>
public sealed record AiozAsset(string FileName, string Sha256, string BinaryName)
{
    public Uri DownloadUrl => new($"https://github.com/AIOZNetwork/aioz-depin-cli/releases/download/v{AiozRelease.Version}/{FileName}");
}

/// <summary>
/// The pinned AIOZ DePIN node (its official command-line node, github.com/AIOZNetwork/aioz-depin-cli, Apache-2.0),
/// checked against the SHA-256 of each asset. Its executables aren't Authenticode-signed or notarized, so the checksum
/// is what vouches for them.
///
/// Pinned to 1.2.5: 1.2.6 doesn't start ("unknown flag: --password", its issue #10, unanswered in October 2026). 1.2.5
/// has no storage limit of its own, so Otium enforces the user's (see <see cref="AiozBandwidthProvider"/>). macOS isn't
/// offered: 1.2.5's two Mac archives are labelled "amd64" and "x86_64", so which one is for Apple silicon is unclear.
/// </summary>
public static class AiozRelease
{
    public const string Version = "1.2.5";

    private static readonly AiozAsset WindowsX64 = new("aioz-depin-windows-amd64-1.2.5.zip",
        "bbc96d3a3b7db1823bd2c177b26a4d2a1e05ca759e8315bc00e0b95178d2cc15", "aioz-depin-cli-windows-amd64.exe");

    private static readonly AiozAsset LinuxX64 = new("aioz-depin-linux-amd64-1.2.5.tar.gz",
        "cad2f49e2ebb82cf3f9b725ba076d36be563ceddb6531aa9ed3edaf23e94477d", "aioz-depin-cli-linux-amd64");

    public static AiozAsset? ForCurrentPlatform() => For(
        OperatingSystem.IsWindows() ? OSPlatform.Windows : OperatingSystem.IsMacOS() ? OSPlatform.OSX : OSPlatform.Linux,
        RuntimeInformation.ProcessArchitecture);

    public static AiozAsset? For(OSPlatform os, Architecture arch) => (os, arch) switch
    {
        _ when os == OSPlatform.Windows && arch == Architecture.X64 => WindowsX64,
        _ when os == OSPlatform.Linux && arch == Architecture.X64 => LinuxX64,
        _ => null,
    };
}
