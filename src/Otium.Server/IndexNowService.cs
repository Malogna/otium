using System.Net.Http.Json;
using Microsoft.Extensions.Options;

namespace Otium.Server;

/// <summary>
/// IndexNow (indexnow.org): tells Bing, Yandex and the other engines that share it which pages changed, so they crawl
/// them within hours instead of at their next visit. Google doesn't take part; it reads the sitemap. After each start
/// (a deploy may have changed any page) it sends every page in the sitemap, then once a day the pages showing today's
/// prices. Only where the site has its public address configured, so development and tests never send anything.
/// </summary>
public sealed class IndexNowService(HttpClient http, IOptions<ReferralOptions> referral, IOptions<SiteOptions> site, TimeProvider time,
    ILogger<IndexNowService> logger) : BackgroundService
{
    /// <summary>Not a secret: IndexNow publishes it at /{Key}.txt, where the engines check that the notice came from this site.</summary>
    public const string Key = "a1d6e46e63749e605c845e5d48a238e8";

    private const string Endpoint = "https://api.indexnow.org/indexnow";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (referral.Value.PublicBaseUrl is not { Length: > 0 } baseUrl)
            return;
        // Let the server settle, so the engines' first visit finds it ready.
        await Task.Delay(TimeSpan.FromMinutes(2), time, stoppingToken);
        var paths = SitePages.SitemapPaths(SitePages.AvailableLegalPages(site.Value.PagesDirectory)).ToList();
        while (!stoppingToken.IsCancellationRequested)
        {
            await NotifyAsync(baseUrl, paths, stoppingToken);
            await Task.Delay(TimeSpan.FromDays(1), time, stoppingToken);
            paths = SitePages.PricedPaths.ToList();
        }
    }

    /// <summary>Sends one notice for <paramref name="paths"/>; failures are logged, never thrown (the next day tries again).</summary>
    internal async Task NotifyAsync(string baseUrl, IReadOnlyList<string> paths, CancellationToken ct)
    {
        var root = baseUrl.TrimEnd('/');
        var notice = new { host = new Uri(root).Host, key = Key, keyLocation = $"{root}/{Key}.txt", urlList = paths.Select(p => root + p) };
        try
        {
            using var response = await http.PostAsJsonAsync(Endpoint, notice, ct);
            if (response.IsSuccessStatusCode)
                logger.LogInformation("IndexNow told about {Count} pages", paths.Count);
            else
                logger.LogWarning("IndexNow answered {Status} for {Count} pages", (int)response.StatusCode, paths.Count);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            logger.LogWarning(e, "IndexNow unavailable");
        }
    }
}
