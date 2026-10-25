using System.Runtime.InteropServices;

namespace Otium.Providers.Mysterium;

public sealed record MystAsset(string FileName, string Sha256)
{
    public Uri DownloadUrl => new($"https://github.com/mysteriumnetwork/node/releases/download/{MystRelease.Version}/{FileName}");
}

/// <param name="Sha256">Of the zip.</param>
/// <param name="DllSha256">Of the 64-bit wintun.dll inside it, checked again before it's copied next to the supervisor.</param>
public sealed record WintunAsset(string Version, string Sha256, string DllSha256)
{
    public string FileName => $"wintun-{Version}.zip";

    public Uri DownloadUrl => new($"https://www.wintun.net/builds/{FileName}");

    /// <summary>The 64-bit driver's path inside the unpacked zip.</summary>
    public static string AmdDll(string unpacked) => Path.Combine(unpacked, "wintun", "bin", "amd64", MystRelease.WintunDll);
}

/// <summary>
/// The pinned Mysterium node release (open source, github.com/mysteriumnetwork/node), checked against the SHA-256
/// GitHub publishes for each asset. Its executables aren't Authenticode-signed or notarized, so the checksum is what
/// vouches for them. Each archive holds the node (<c>myst</c>) and its network supervisor (<c>myst_supervisor</c>), which
/// Otium installs once with the system's administrator prompt: a Windows service, a launchd daemon on macOS or a systemd
/// service on Linux (tried on Windows and Ubuntu, October 2026). Linux on ARM has only .deb packages, so it isn't offered.
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

    /// <summary>
    /// WireGuard's Wintun driver (wintun.net, signed by WireGuard LLC), which the Windows supervisor needs to create the
    /// node's tunnel. Mysterium's own Windows app ships it, but the node's release zip doesn't (October 2026), so every
    /// buyer session failed with "Error loading wintun.dll". The zip's SHA-256 is the one WireGuard publishes.
    /// </summary>
    public static readonly WintunAsset Wintun = new(
        "0.14.1",
        "07c256185d6ee3652e09fa55c0b673e2624b565e02c4b9091c79ca7d2f24ef51",
        "e5da8447dc2c320edc0fc52fa01885c103de8c118481f683643cacc3220dafce");

    public const string WintunDll = "wintun.dll";

    public static string SupervisorName => OperatingSystem.IsWindows() ? "myst_supervisor.exe" : "myst_supervisor";
}
