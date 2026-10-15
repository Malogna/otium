using System.Runtime.InteropServices;

namespace Otium.Providers.XMRig;

public sealed record XmrigAsset(string FileName, string Sha256)
{
    public Uri DownloadUrl => new($"https://github.com/xmrig/xmrig/releases/download/v{XmrigRelease.Version}/{FileName}");

    public bool IsZip => FileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// The XMRig build this version of Otium ships with. Versions and hashes are pinned in code (copied from the
/// release's SHA256SUMS) so a compromised download can't be swapped in; upgrading XMRig means shipping an app update.
/// </summary>
public static class XmrigRelease
{
    public const string Version = "6.26.0";

    private static readonly XmrigAsset WindowsX64 = new("xmrig-6.26.0-windows-x64.zip", "bba8097cb37d9b458a1cb1137876b27cde6740d17fe4ccbc086ba07d87d9e147");
    private static readonly XmrigAsset WindowsArm64 = new("xmrig-6.26.0-windows-arm64.zip", "958952de131c392a4e1e9656a1d70c3916d09d5a1f5e3f8c67dc0e6f35dbd76a");
    private static readonly XmrigAsset MacArm64 = new("xmrig-6.26.0-macos-arm64.tar.gz", "6ae4eb4216e99a201ae9a3d2c3a7c275207c5165cfc25da1f3d735d6c4829c18");
    private static readonly XmrigAsset MacX64 = new("xmrig-6.26.0-macos-x64.tar.gz", "1da924b358c0089e361540c4a9e6f8b09538b29efeafa2379590e0f6db358ff4");
    private static readonly XmrigAsset LinuxX64 = new("xmrig-6.26.0-linux-static-x64.tar.gz", "fc6f8ae5f64e4f17481f7e3be29a1c56949f216a998414188003eae1db20c9e5");

    public static XmrigAsset? ForCurrentPlatform() => For(
        OperatingSystem.IsWindows() ? OSPlatform.Windows
        : OperatingSystem.IsMacOS() ? OSPlatform.OSX
        : OperatingSystem.IsLinux() ? OSPlatform.Linux
        : OSPlatform.FreeBSD,
        RuntimeInformation.ProcessArchitecture);

    public static XmrigAsset? For(OSPlatform os, Architecture arch)
    {
        if (os == OSPlatform.Windows)
            return arch switch { Architecture.X64 => WindowsX64, Architecture.Arm64 => WindowsArm64, _ => null };
        if (os == OSPlatform.OSX)
            return arch switch { Architecture.Arm64 => MacArm64, Architecture.X64 => MacX64, _ => null };
        if (os == OSPlatform.Linux)
            return arch == Architecture.X64 ? LinuxX64 : null; // no official Linux ARM build
        return null;
    }

    public static string BinaryName => OperatingSystem.IsWindows() ? "xmrig.exe" : "xmrig";
}
