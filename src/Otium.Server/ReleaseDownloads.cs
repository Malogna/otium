using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Otium.Server;

/// <summary>One file of the latest release.</summary>
public sealed record ReleaseAsset(string Name, string Url);

public interface IReleaseFeed
{
    /// <summary>The latest published release's files, or none while there is no release.</summary>
    Task<IReadOnlyList<ReleaseAsset>> GetLatestAsync(CancellationToken ct);
}

/// <summary>GitHub's latest release of the app (public API, no token; 60 requests an hour, so callers cache).</summary>
public sealed class GitHubReleaseFeed(HttpClient http, IOptions<SiteOptions> options) : IReleaseFeed
{
    public async Task<IReadOnlyList<ReleaseAsset>> GetLatestAsync(CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, options.Value.LatestReleaseApi);
        request.Headers.UserAgent.ParseAdd("Otium-Server");
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        using var response = await http.SendAsync(request, ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return []; // nothing released yet
        response.EnsureSuccessStatusCode();
        using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        return Parse(doc.RootElement);
    }

    internal static IReadOnlyList<ReleaseAsset> Parse(JsonElement release) =>
        release.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array
            ? assets.EnumerateArray()
                .Where(a => a.TryGetProperty("name", out _) && a.TryGetProperty("browser_download_url", out _))
                .Select(a => new ReleaseAsset(a.GetProperty("name").GetString()!, a.GetProperty("browser_download_url").GetString()!))
                .ToList()
            : [];
}

/// <summary>
/// Sends the website's Download buttons straight to the installer for the visitor's computer, so nobody has to find
/// the right file on GitHub: the Windows setup, the macOS installer package, or the Linux AppImage of the latest
/// release. Phones, unknown systems and the time before the first release go to the releases page instead.
/// </summary>
public sealed class ReleaseDownloads(IReleaseFeed feed, TimeProvider time, ILogger<ReleaseDownloads> logger)
{
    public static readonly TimeSpan CacheFor = TimeSpan.FromMinutes(10);

    private readonly SemaphoreSlim _lock = new(1, 1);
    private IReadOnlyList<ReleaseAsset> _assets = [];
    private DateTimeOffset _checkedAt = DateTimeOffset.MinValue;

    /// <summary>The installer's address for this system ("windows", "macos", "macos-intel", "linux"), or null.</summary>
    public async Task<string?> InstallerUrlAsync(string? os, CancellationToken ct)
    {
        var assets = await AssetsAsync(ct);
        bool Has(ReleaseAsset a, params string[] parts) => parts.All(p => a.Name.Contains(p, StringComparison.OrdinalIgnoreCase));
        var pick = os switch
        {
            "windows" => assets.FirstOrDefault(a => Has(a, "win-x64", "Setup.exe")),
            "macos" => assets.FirstOrDefault(a => Has(a, "osx-arm64", ".pkg")) ?? assets.FirstOrDefault(a => Has(a, "osx", ".pkg")),
            "macos-intel" => assets.FirstOrDefault(a => Has(a, "osx-x64", ".pkg")),
            "linux" => assets.FirstOrDefault(a => Has(a, ".AppImage")),
            _ => null,
        };
        return pick?.Url;
    }

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

    private async Task<IReadOnlyList<ReleaseAsset>> AssetsAsync(CancellationToken ct)
    {
        if (time.GetUtcNow() - _checkedAt < CacheFor)
            return _assets;
        await _lock.WaitAsync(ct);
        try
        {
            if (time.GetUtcNow() - _checkedAt < CacheFor)
                return _assets;
            try
            {
                _assets = await feed.GetLatestAsync(ct);
            }
            catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException && !ct.IsCancellationRequested)
            {
                logger.LogWarning(e, "Couldn't read the latest release; Download buttons go to the releases page meanwhile");
            }
            _checkedAt = time.GetUtcNow();
            return _assets;
        }
        finally
        {
            _lock.Release();
        }
    }
}
