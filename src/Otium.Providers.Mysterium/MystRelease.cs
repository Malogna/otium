using System.Runtime.InteropServices;

namespace Otium.Providers.Mysterium;

public sealed record MystAsset(string FileName, string Sha256)
{
    public Uri DownloadUrl => new($"https://github.com/mysteriumnetwork/node/releases/download/{MystRelease.Version}/{FileName}");
}

/// <summary>
/// The pinned Mysterium node release (open source, github.com/mysteriumnetwork/node), checked against the SHA-256
/// GitHub publishes for each asset. Its executables aren't Authenticode-signed, so the checksum is what vouches for them.
/// Only Windows is enabled: the one-time admin install of its network supervisor has been tried there (October 2026);
/// macOS (launchd) and Linux (root or capabilities) need their own flows first.
/// </summary>
public static class MystRelease
{
    public const string Version = "1.39.7";

    private static readonly MystAsset WindowsX64 = new("myst_windows_amd64.zip", "1c1cced4486e24e207dd3bdec075b2b000ee55c47ed4ef86d823078faa95ef1c");

    public static MystAsset? ForCurrentPlatform() => For(
        OperatingSystem.IsWindows() ? OSPlatform.Windows : OperatingSystem.IsMacOS() ? OSPlatform.OSX : OSPlatform.Linux,
        RuntimeInformation.ProcessArchitecture);

    public static MystAsset? For(OSPlatform os, Architecture arch) =>
        os == OSPlatform.Windows && arch == Architecture.X64 ? WindowsX64 : null;

    public static string BinaryName => OperatingSystem.IsWindows() ? "myst.exe" : "myst";

    public static string SupervisorName => OperatingSystem.IsWindows() ? "myst_supervisor.exe" : "myst_supervisor";
}
