using System.Runtime.InteropServices;

namespace Otium.Providers.Mysterium;

public sealed record MystAsset(string FileName, string Sha256)
{
    public Uri DownloadUrl => new($"https://github.com/mysteriumnetwork/node/releases/download/{MystRelease.Version}/{FileName}");
}

/// <summary>
/// The pinned Mysterium node release (open source, github.com/mysteriumnetwork/node), checked against the SHA-256
/// GitHub publishes for each asset. Its executables aren't Authenticode-signed or notarized, so the checksum is what
/// vouches for them. Otium runs only the node (<c>myst</c>), in userspace mode; the archive's network supervisor isn't
/// installed (earlier test versions did, see <see cref="MystSupervisors"/>). Linux on ARM has only .deb packages, so it
/// isn't offered.
/// </summary>
public static class MystRelease
{
    public const string Version = "1.39.7";

    private static readonly MystAsset WindowsX64 = new("myst_windows_amd64.zip", "1c1cced4486e24e207dd3bdec075b2b000ee55c47ed4ef86d823078faa95ef1c");
    private static readonly MystAsset MacArm64 = new("myst_darwin_arm64.tar.gz", "7de4bbef1975c18277ede6064a34bb3093af18c7a046f334be5eddbf4aae2061");
    private static readonly MystAsset MacX64 = new("myst_darwin_amd64.tar.gz", "a1709cf3e4acef11caf1c8e87a1cc7c8b36a59b6a7b8c61ddb4e79063a6bc8d7");
    private static readonly MystAsset LinuxX64 = new("myst_linux_amd64.tar.gz", "cbfbf23018bd4ee85c8d202ce89820169e74e908f6eac689bd80c97070e87d29");

    public static MystAsset? ForCurrentPlatform() => For(
        OperatingSystem.IsWindows() ? OSPlatform.Windows : OperatingSystem.IsMacOS() ? OSPlatform.OSX : OSPlatform.Linux,
        RuntimeInformation.ProcessArchitecture);

    public static MystAsset? For(OSPlatform os, Architecture arch) => (os, arch) switch
    {
        _ when os == OSPlatform.Windows && arch == Architecture.X64 => WindowsX64,
        _ when os == OSPlatform.OSX && arch == Architecture.Arm64 => MacArm64,
        _ when os == OSPlatform.OSX && arch == Architecture.X64 => MacX64,
        _ when os == OSPlatform.Linux && arch == Architecture.X64 => LinuxX64,
        _ => null,
    };

    public static string BinaryName => OperatingSystem.IsWindows() ? "myst.exe" : "myst";
}
