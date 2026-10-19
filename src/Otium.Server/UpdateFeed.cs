using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Otium.Server;

/// <summary>One published release: its tag, whether it's a pre-release (a release candidate), and its files.</summary>
public sealed record PublishedRelease(string Tag, bool Prerelease, IReadOnlyList<ReleaseAsset> Assets);

public interface IReleaseList
{
    /// <summary>The most recent published releases, newest first, pre-releases included.</summary>
    Task<IReadOnlyList<PublishedRelease>> GetRecentAsync(CancellationToken ct);

    /// <summary>The text of one release file (a Velopack feed), by its download address.</summary>
    Task<string> GetTextAsync(string url, CancellationToken ct);
}

/// <summary>GitHub's release list for the app (public API, no token: 60 requests an hour, so <see cref="UpdateFeed"/> caches).</summary>
public sealed class GitHubReleaseList(HttpClient http, IOptions<SiteOptions> options) : IReleaseList
{
    public async Task<IReadOnlyList<PublishedRelease>> GetRecentAsync(CancellationToken ct)
    {
        // The same repository as the "latest release" API the Download buttons use.
        var latest = options.Value.LatestReleaseApi;
        var list = (latest.EndsWith("/latest", StringComparison.Ordinal) ? latest[..^"/latest".Length] : latest) + "?per_page=20";
        using var request = new HttpRequestMessage(HttpMethod.Get, list);
        request.Headers.UserAgent.ParseAdd("Otium-Server");
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        using var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        return Parse(doc.RootElement);
    }

    public Task<string> GetTextAsync(string url, CancellationToken ct) => http.GetStringAsync(url, ct);

    internal static IReadOnlyList<PublishedRelease> Parse(JsonElement releases) =>
        releases.ValueKind != JsonValueKind.Array
            ? []
            : releases.EnumerateArray()
                .Where(r => !(r.TryGetProperty("draft", out var d) && d.GetBoolean()) && r.TryGetProperty("tag_name", out _))
                .Select(r => new PublishedRelease(
                    r.GetProperty("tag_name").GetString()!,
                    r.TryGetProperty("prerelease", out var p) && p.GetBoolean(),
                    GitHubReleaseFeed.Parse(r)))
                .ToList();
}

/// <summary>
/// The app's update feed, served from here so that installed copies don't each ask GitHub's API (60 requests an hour
/// per internet address, which shared addresses run out of). Velopack asks for <c>releases.{channel}.json</c> and then
/// the packages it lists: the feed comes from the newest suitable release, and each package is a redirect to GitHub's
/// download, which has no such limit. Release candidates follow the "pre" track, releases the "stable" one.
/// </summary>
public sealed class UpdateFeed(IReleaseList releases, TimeProvider time, ILogger<UpdateFeed> logger)
{
    public static readonly TimeSpan CacheFor = TimeSpan.FromMinutes(10);

    private readonly SemaphoreSlim _lock = new(1, 1);
    private IReadOnlyList<PublishedRelease> _recent = [];
    private DateTimeOffset _checkedAt = DateTimeOffset.MinValue;
    // Release files never change once published, so each feed is read from GitHub once.
    private readonly Dictionary<string, string> _texts = [];

    /// <summary>Whether <paramref name="file"/> is a Velopack feed ("releases.win-x64.json").</summary>
    public static bool IsFeed(string file) =>
        file.StartsWith("releases.", StringComparison.Ordinal) && file.EndsWith(".json", StringComparison.Ordinal);

    /// <summary>The feed file from the newest release on the track that has it, or null.</summary>
    public async Task<string?> FeedAsync(string file, bool prereleases, CancellationToken ct)
    {
        var release = (await RecentAsync(ct)).FirstOrDefault(r => (prereleases || !r.Prerelease) && r.Assets.Any(a => a.Name == file));
        var asset = release?.Assets.First(a => a.Name == file);
        if (asset is null)
            return null;
        await _lock.WaitAsync(ct);
        try
        {
            if (!_texts.TryGetValue(asset.Url, out var text))
                _texts[asset.Url] = text = await releases.GetTextAsync(asset.Url, ct);
            return text;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>GitHub's download address for a package of a recent release, or null.</summary>
    public async Task<string?> DownloadUrlAsync(string file, CancellationToken ct) =>
        (await RecentAsync(ct)).SelectMany(r => r.Assets).FirstOrDefault(a => a.Name == file)?.Url;

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
                _recent = await releases.GetRecentAsync(ct);
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
