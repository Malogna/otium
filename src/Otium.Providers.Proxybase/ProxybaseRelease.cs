using System.Runtime.InteropServices;

namespace Otium.Providers.Proxybase;

public sealed record ProxybaseAsset(string FileName, string Sha256)
{
    public Uri DownloadUrl => new($"https://github.com/proxybasehq/proxybase-cli/releases/download/proxybase-cli-v{ProxybaseRelease.Version}/{FileName}");
}

/// <summary>
/// The pinned Proxybase seller client (open source, github.com/proxybasehq/proxybase-cli, MIT/Apache-2.0), checked
/// against the SHA-256 GitHub publishes for each release asset. Its builds aren't code-signed, so the checksum is what
/// vouches for them. There's no build for Intel Macs.
/// </summary>
public static class ProxybaseRelease
{
    public const string Version = "0.1.60";

    private static readonly ProxybaseAsset WindowsX64 = new("proxybase-cli-x86_64-pc-windows-msvc.zip", "64cde2c8ef86c2b31684b005f6c34ba9684a0f8c73676665f7c9faa014fa8a0e");
    private static readonly ProxybaseAsset LinuxX64 = new("proxybase-cli-x86_64-unknown-linux-gnu.tar.gz", "796b7dfd6bb1e3a700fa4e7696f2c42c2533b413fc45f4356b77b9a10f8fd323");
    private static readonly ProxybaseAsset LinuxArm64 = new("proxybase-cli-aarch64-unknown-linux-gnu.tar.gz", "8b553f17b92f3a20b746a85214744e4ce29b7d87e5f23f0ec0e9d724805a2aa6");
    private static readonly ProxybaseAsset MacArm64 = new("proxybase-cli-aarch64-apple-darwin.tar.gz", "78e84d2f4c5d45a44d00499e3cdf0eeed0023004f6fe44465e2b243647924fdc");

    public static ProxybaseAsset? ForCurrentPlatform() => For(
        OperatingSystem.IsWindows() ? OSPlatform.Windows : OperatingSystem.IsMacOS() ? OSPlatform.OSX : OSPlatform.Linux,
        RuntimeInformation.ProcessArchitecture);

    public static ProxybaseAsset? For(OSPlatform os, Architecture arch) => (os, arch) switch
    {
        _ when os == OSPlatform.Windows && arch == Architecture.X64 => WindowsX64,
        _ when os == OSPlatform.Linux && arch == Architecture.X64 => LinuxX64,
        _ when os == OSPlatform.Linux && arch == Architecture.Arm64 => LinuxArm64,
        _ when os == OSPlatform.OSX && arch == Architecture.Arm64 => MacArm64,
        _ => null,
    };

    public static string BinaryName => OperatingSystem.IsWindows() ? "proxybase-cli.exe" : "proxybase-cli";
}
