namespace Otium.Server;

/// <summary>One file of a release.</summary>
public sealed record ReleaseAsset(string Name, string Url);

/// <summary>One system's installer, as the download page lists it.</summary>
/// <param name="Os">"windows", "macos" (Apple silicon), "macos-intel" or "linux": the /download/{os} address.</param>
public sealed record Installer(string Os, string Label, string FileName, string Url);

/// <summary>The release the website offers: its version ("0.1.0-rc.13") and installers.</summary>
public sealed record LatestRelease(string Version, IReadOnlyList<Installer> Installers);

/// <summary>
/// What the website's Download buttons offer: the installers of the newest release, from the same cached GitHub release
/// list as the app's update feed. GitHub's own "latest release" leaves out pre-releases, and so far every Otium release
/// is a release candidate, so the buttons went to GitHub's releases page instead of an installer. The newest stable
/// release wins once there is one; until then the newest release candidate.
/// </summary>
public sealed class ReleaseDownloads(UpdateFeed feed)
{
    /// <summary>Every system the page offers, in its order.</summary>
    private static readonly (string Os, string Label, Func<ReleaseAsset, bool> Matches)[] Systems =
    [
        ("windows", "Windows", a => Has(a, "win-x64", "Setup.exe")),
        ("macos", "macOS (Apple silicon)", a => Has(a, "osx-arm64", ".pkg")),
        ("macos-intel", "macOS (Intel)", a => Has(a, "osx-x64", ".pkg")),
        ("linux", "Linux", a => Has(a, ".AppImage")),
    ];

    /// <summary>The newest stable release with installers, else the newest release candidate with them, or null.</summary>
    public async Task<LatestRelease?> LatestAsync(CancellationToken ct)
    {
        var releases = (await feed.ReleasesAsync(ct)).Where(r => Installers(r).Count > 0).ToList();
        var release = releases.FirstOrDefault(r => !r.Prerelease) ?? releases.FirstOrDefault();
        return release is null ? null : new LatestRelease(release.Tag.TrimStart('v'), Installers(release));
    }

    /// <summary>The installer's address for this system ("windows", "macos", "macos-intel", "linux"), or null.</summary>
    public async Task<string?> InstallerUrlAsync(string? os, CancellationToken ct) =>
        (await LatestAsync(ct))?.Installers.FirstOrDefault(i => i.Os == os)?.Url;

    internal static IReadOnlyList<Installer> Installers(PublishedRelease release) =>
        Systems.Select(s => release.Assets.FirstOrDefault(s.Matches) is { } a ? new Installer(s.Os, s.Label, a.Name, a.Url) : null)
            .OfType<Installer>()
            .ToList();

    private static bool Has(ReleaseAsset a, params string[] parts) => parts.All(p => a.Name.Contains(p, StringComparison.OrdinalIgnoreCase));

    /// <summary>The visitor's system from their browser's User-Agent, or null for phones and anything unknown.</summary>
    public static string? DetectOs(string? userAgent)
    {
        var ua = userAgent ?? "";
        bool Has(string s) => ua.Contains(s, StringComparison.OrdinalIgnoreCase);
        if (Has("Android") || Has("iPhone") || Has("iPad") || Has("Mobile"))
            return null;
        if (Has("Windows"))
            return "windows";
        // Browsers report "Intel Mac OS X" on Apple Silicon too, so every Mac gets the Apple Silicon build by default.
        if (Has("Macintosh") || Has("Mac OS X"))
            return "macos";
        if (Has("Linux") || Has("X11"))
            return "linux";
        return null;
    }
}
