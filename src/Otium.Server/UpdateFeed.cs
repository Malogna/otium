using System.Text.Json;
using Microsoft.Extensions.Options;
using Otium.Core.Updates;

namespace Otium.Server;

/// <summary>One published release: its tag, whether it's a pre-release (a release candidate), and its files.</summary>
public sealed record PublishedRelease(string Tag, bool Prerelease, IReadOnlyList<ReleaseAsset> Assets);

public interface IReleaseList
{
    /// <summary>The most recent published releases, pre-releases included, in any order (GitHub sorts them by tag name).</summary>
    Task<IReadOnlyList<PublishedRelease>> GetRecentAsync(CancellationToken ct);

    /// <summary>One release file (a Velopack feed or its signature), byte for byte, by its download address.</summary>
    Task<byte[]> GetBytesAsync(string url, CancellationToken ct);
}

/// <summary>GitHub's release list for the app (public API, no token: 60 requests an hour, so <see cref="UpdateFeed"/> caches).</summary>
public sealed class GitHubReleaseList(HttpClient http, IOptions<SiteOptions> options) : IReleaseList
{
    public async Task<IReadOnlyList<PublishedRelease>> GetRecentAsync(CancellationToken ct)
    {
        // The repository named by the configured "latest release" API address.
        var latest = options.Value.LatestReleaseApi;
        var list = (latest.EndsWith("/latest", StringComparison.Ordinal) ? latest[..^"/latest".Length] : latest) + "?per_page=100";
        using var request = new HttpRequestMessage(HttpMethod.Get, list);
        request.Headers.UserAgent.ParseAdd("Otium-Server");
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        using var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        return Parse(doc.RootElement);
    }

    public Task<byte[]> GetBytesAsync(string url, CancellationToken ct) => http.GetByteArrayAsync(url, ct);

    internal static IReadOnlyList<PublishedRelease> Parse(JsonElement releases) =>
        releases.ValueKind != JsonValueKind.Array
            ? []
            : releases.EnumerateArray()
                .Where(r => !(r.TryGetProperty("draft", out var d) && d.GetBoolean()) && r.TryGetProperty("tag_name", out _))
                .Select(r => new PublishedRelease(
                    r.GetProperty("tag_name").GetString()!,
                    r.TryGetProperty("prerelease", out var p) && p.GetBoolean(),
                    ParseAssets(r)))
                .ToList();

    private static IReadOnlyList<ReleaseAsset> ParseAssets(JsonElement release) =>
        release.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array
            ? assets.EnumerateArray()
                .Where(a => a.TryGetProperty("name", out _) && a.TryGetProperty("browser_download_url", out _))
                .Select(a => new ReleaseAsset(a.GetProperty("name").GetString()!, a.GetProperty("browser_download_url").GetString()!))
                .ToList()
            : [];
}

/// <summary>The keys update feeds must be signed with: <see cref="Otium.Core.AppIdentity.UpdateSigningKeys"/> (tests use their own).</summary>
public sealed record UpdateSigningKeys(IReadOnlyList<string> Keys);

/// <summary>
/// The app's update feed, served from here so that installed copies don't each ask GitHub's API (60 requests an hour
/// per internet address, which shared addresses run out of). Velopack asks for <c>releases.{channel}.json</c> and then
/// the packages it lists: the feed comes from the newest suitable release whose signature checks out
/// (<see cref="UpdateSignature"/>), and each package is a redirect to GitHub's download, which has no such limit.
/// Release candidates follow the "pre" track, releases the "stable" one.
/// </summary>
public sealed class UpdateFeed(IReleaseList releases, UpdateSigningKeys signingKeys, TimeProvider time, ILogger<UpdateFeed> logger)
{
    public static readonly TimeSpan CacheFor = TimeSpan.FromMinutes(10);

    private readonly SemaphoreSlim _lock = new(1, 1);
    private IReadOnlyList<PublishedRelease> _recent = [];
    private DateTimeOffset _checkedAt = DateTimeOffset.MinValue;
    // Release files never change once published, so each feed and signature is read from GitHub once.
    private readonly Dictionary<string, byte[]> _files = [];

    /// <summary>Whether <paramref name="file"/> is a Velopack feed ("releases.win-x64.json").</summary>
    public static bool IsFeed(string file) =>
        file.StartsWith("releases.", StringComparison.Ordinal) && file.EndsWith(".json", StringComparison.Ordinal);

    /// <summary>Whether <paramref name="file"/> is a feed's signature ("releases.win-x64.json.sig").</summary>
    public static bool IsSignature(string file) =>
        file.EndsWith(".sig", StringComparison.Ordinal) && IsFeed(file[..^".sig".Length]);

    /// <summary>
    /// The feed file and its signature from the newest release on the track whose signature checks out, or null. A
    /// release that isn't signed yet, or is signed with a key Otium doesn't trust (say, one published by someone who got
    /// into the GitHub account), is skipped, so the last signed release is offered meanwhile. That protects copies too
    /// old to check signatures themselves.
    /// </summary>
    public async Task<(byte[] Feed, string Signature)?> SignedFeedAsync(string file, bool prereleases, CancellationToken ct)
    {
        foreach (var release in (await RecentAsync(ct)).Where(r => prereleases || !r.Prerelease))
        {
            if (await SignedAsync(release, file, ct) is { } signed)
                return signed;
        }
        return null;
    }

    /// <summary>This release's feed and its signature, when the signature checks out.</summary>
    private async Task<(byte[] Feed, string Signature)?> SignedAsync(PublishedRelease release, string file, CancellationToken ct)
    {
        var feedAsset = release.Assets.FirstOrDefault(a => a.Name == file);
        var signatureAsset = release.Assets.FirstOrDefault(a => a.Name == UpdateSignature.FileFor(file));
        if (feedAsset is null || signatureAsset is null)
            return null;
        var feed = await FileAsync(feedAsset.Url, ct);
        var signature = System.Text.Encoding.UTF8.GetString(await FileAsync(signatureAsset.Url, ct));
        if (UpdateSignature.Verify(file, feed, signature, signingKeys.Keys))
            return (feed, signature);
        logger.LogWarning("{Tag}: the signature on {File} doesn't check out; the release is skipped", release.Tag, file);
        return null;
    }

    private async Task<byte[]> FileAsync(string url, CancellationToken ct)
    {
        await _lock.WaitAsync(ct);
        try
        {
            if (!_files.TryGetValue(url, out var bytes))
                _files[url] = bytes = await releases.GetBytesAsync(url, ct);
            return bytes;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>Recent releases, newest version first (pre-releases included), for the website's Download buttons.</summary>
    public Task<IReadOnlyList<PublishedRelease>> ReleasesAsync(CancellationToken ct) => RecentAsync(ct);

    /// <summary>GitHub's download address for a package of a recent signed release (one with a feed that checks out), or null.</summary>
    public async Task<string?> DownloadUrlAsync(string file, CancellationToken ct)
    {
        foreach (var release in await RecentAsync(ct))
        {
            if (release.Assets.FirstOrDefault(a => a.Name == file) is not { } asset)
                continue;
            foreach (var feed in release.Assets.Where(a => IsFeed(a.Name)))
            {
                if (await SignedAsync(release, feed.Name, ct) is not null)
                    return asset.Url;
            }
        }
        return null;
    }

    private async Task<IReadOnlyList<PublishedRelease>> RecentAsync(CancellationToken ct)
    {
        if (time.GetUtcNow() - _checkedAt < CacheFor)
            return _recent;
        await _lock.WaitAsync(ct);
        try
        {
            if (time.GetUtcNow() - _checkedAt < CacheFor)
                return _recent;
            try
            {
                // Newest version first. GitHub lists releases by tag name, so "v0.1.0-rc.10" came after "rc.9" there.
                _recent = (await releases.GetRecentAsync(ct)).OrderByDescending(r => r.Tag, ReleaseVersion.Comparer).ToList();
            }
            catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException && !ct.IsCancellationRequested)
            {
                logger.LogWarning(e, "Couldn't read the release list; update checks get the last one meanwhile");
            }
            _checkedAt = time.GetUtcNow();
            return _recent;
        }
        finally
        {
            _lock.Release();
        }
    }
}
