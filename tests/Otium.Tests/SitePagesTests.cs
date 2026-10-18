using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Otium.Core.Pools;
using Otium.Server.Gpu;

namespace Otium.Tests;

/// <summary>The public website: the front page, the invite page, and legal pages that appear only once their files exist.</summary>
public sealed class SitePagesTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"otium-site-{Guid.NewGuid():N}.db");
    private readonly string _pages = Path.Combine(Path.GetTempPath(), $"otium-site-pages-{Guid.NewGuid():N}");
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _http;

    public SitePagesTests()
    {
        Directory.CreateDirectory(_pages);
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Otium", $"Data Source={_dbPath};Pooling=False");
            builder.UseSetting("DisableBackgroundJobs", "true");
            builder.UseSetting("Fees:FeePercent", "3");
            builder.UseSetting("Referral:DownloadUrl", "https://example.com/download?a=1&b=2");
            builder.UseSetting("Site:PagesDirectory", _pages);
            builder.ConfigureServices(services =>
            {
                // No network in tests: the site never needs the pools, but keep the server's clients offline anyway.
                services.RemoveAll<IMoneroPoolApi>();
                services.AddSingleton<IMoneroPoolApi>(new GpuPolicyTests.FakePool());
                services.RemoveAll<IPearlPipFeed>();
                services.AddSingleton<IPearlPipFeed>(new GpuPolicyTests.FakePip());
            });
        });
        _http = _factory.CreateClient();
    }

    private async Task<(HttpStatusCode Status, string Html)> Get(string path)
    {
        using var response = await _http.GetAsync(path);
        return (response.StatusCode, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task The_front_page_explains_otium_with_the_servers_own_fee()
    {
        var (status, html) = await Get("/");

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Contains("<h1>Your computer earns while you're away</h1>", html);
        Assert.Contains("""href="https://example.com/download?a=1&amp;b=2">Download Otium</a>""", html);
        // The cost table comes from the same breakdown the app shows, with the server's 3% fee.
        Assert.Contains("<th>Otium</th><td>3% of mining time, about 43 minutes per day of mining", html);
        Assert.Contains("<th>Pool</th><td>unMineable keeps 0.75%", html);
        Assert.Contains("<th>Also</th><td>unMineable pays Otium 0.25–0.5%", html);
        Assert.Contains("Otium's fee is 3% of mining time", html);
        Assert.Contains("prefers-color-scheme: dark", html);
        // No legal pages yet: none are linked.
        Assert.DoesNotContain("""href="/privacy""", html);
        Assert.DoesNotContain("""href="/terms""", html);
    }

    [Fact]
    public async Task Legal_pages_appear_only_once_the_operator_adds_them()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await Get("/privacy")).Status);
        Assert.Equal(HttpStatusCode.NotFound, (await Get("/terms")).Status);
        Assert.Equal(HttpStatusCode.NotFound, (await Get("/secrets")).Status); // only the two known pages are served

        await File.WriteAllTextAsync(Path.Combine(_pages, "privacy.html"), "<h1>Privacy policy</h1><p>Reviewed text.</p>");

        var (status, html) = await Get("/privacy");
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Contains("<h1>Privacy policy</h1><p>Reviewed text.</p>", html);
        Assert.Contains("<title>Otium: Privacy</title>", html);
        Assert.Contains("""href="/privacy">Privacy</a>""", (await Get("/")).Html);
        Assert.DoesNotContain("""href="/terms""", (await Get("/")).Html);
        Assert.Equal(HttpStatusCode.NotFound, (await Get("/terms")).Status);
    }

    [Fact]
    public async Task The_invite_page_uses_the_same_layout_and_encodes_the_code()
    {
        var (status, html) = await Get("/r/<b>x");

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Contains("This invite code isn't valid any more", html);
        Assert.DoesNotContain("<b>x", html);
        Assert.Contains("""<a href="/">How Otium works</a>""", html);
        Assert.Contains("""<a href="https://github.com/Malogna/otium">Source code</a>""", html);
    }

    public void Dispose()
    {
        _http.Dispose();
        _factory.Dispose();
        try
        {
            File.Delete(_dbPath);
            Directory.Delete(_pages, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}
