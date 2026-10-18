using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Otium.Core.Pools;
using Otium.Server.Gpu;

namespace Otium.Tests;

/// <summary>The public website: the front page, its embedded files, the invite page, and legal pages that appear only once their files exist.</summary>
public sealed class SitePagesTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"otium-site-{Guid.NewGuid():N}.db");
    private readonly string _pages = Path.Combine(Path.GetTempPath(), $"otium-site-pages-{Guid.NewGuid():N}");
    private readonly ManualTime _time = new(new DateTimeOffset(2026, 10, 18, 15, 0, 0, TimeSpan.Zero));
    private readonly GpuPolicyTests.FakeFeed _feed = new();
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _http;

    public SitePagesTests()
    {
        Directory.CreateDirectory(_pages);
        _feed.Coins[469] = new Otium.Server.Gpu.CoinRevenue(3.4e-14, false, _time.GetUtcNow()); // Pearl
        _feed.Coins[473] = new Otium.Server.Gpu.CoinRevenue(1.6e-9, false, _time.GetUtcNow()); // Quantus
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Otium", $"Data Source={_dbPath};Pooling=False");
            builder.UseSetting("DisableBackgroundJobs", "true");
            builder.UseSetting("Fees:FeePercent", "3");
            builder.UseSetting("Referral:DownloadUrl", "https://example.com/download?a=1&b=2");
            builder.UseSetting("Site:PagesDirectory", _pages);
            builder.ConfigureServices(services =>
            {
                // No network in tests: fixed coin prices for the live earnings.
                services.RemoveAll<IMoneroPoolApi>();
                services.AddSingleton<IMoneroPoolApi>(new GpuPolicyTests.FakePool());
                services.RemoveAll<IProfitFeed>();
                services.AddSingleton<IProfitFeed>(_feed);
                services.RemoveAll<IKryptexApi>();
                services.AddSingleton<IKryptexApi>(new GpuPolicyTests.FakeKryptex());
                services.RemoveAll<IPearlPipFeed>();
                services.AddSingleton<IPearlPipFeed>(new GpuPolicyTests.FakePip());
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(_time);
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
    public async Task The_front_page_tells_the_story_with_the_servers_own_fee()
    {
        var (status, html) = await Get("/");

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Contains("Your computer works the night shift.", html);
        Assert.Contains("""href="https://example.com/download?a=1&amp;b=2">""", html);
        // The cost list comes from the same breakdown the app shows, with the server's 3% fee.
        Assert.Contains("<dt>Otium</dt><dd>3% of mining time, about 43 minutes per day of mining", html);
        Assert.Contains("<dt>Pool</dt><dd>unMineable keeps 0.75%", html);
        Assert.Contains("<dt>Also</dt><dd>unMineable pays Otium 0.25–0.5%", html);
        Assert.Contains("$95.25<small>of every $100 your processor mines stays yours", html); // 100 − 3 − 1 − 0.75
        Assert.Contains("Otium's 43 min", html); // the fee slice on the dial
        // No legal pages yet: none are linked.
        Assert.DoesNotContain("""href="/privacy""", html);
        Assert.DoesNotContain("""href="/terms""", html);
    }

    [Fact]
    public async Task Each_card_shows_its_best_coin_after_fees_and_electricity()
    {
        var html = (await Get("/")).Html;

        // RTX 3080: Pearl 100 TH/s × 3.4e-14 $/H/s/day = $3.40, less SRBMiner 2%, unMineable 0.75% and Otium 3% = $3.21,
        // less 240 W for 24 h at $0.15/kWh ($0.86) = $2.34. Quantus earns it less, so Pearl is shown.
        var row = html[html.IndexOf("""data-card="rtx3080""", StringComparison.Ordinal)..];
        Assert.Contains("""<span class="amount">$2.34</span>""", row[..800]);
        Assert.Contains("Pearl · paid in your coin", row[..800]);
        // AMD can't mine Pearl on unMineable: the RX 6700 XT only has Quantus, which loses money at $0.15/kWh.
        var amd = html[html.IndexOf("""data-card="rx6700xt""", StringComparison.Ordinal)..];
        Assert.Contains("stays paused", amd[..800]);
        // The slider's data for the script, with "</" escaped so it can't close the script tag.
        Assert.Contains("""<script type="application/json" id="otium-data">{"cards":[""", html);
        Assert.Contains("Coin prices from WhatToMine via Otium's server, 18 October 2026, 15:00 UTC", html);
    }

    [Fact]
    public async Task Without_coin_prices_the_page_says_so_instead_of_inventing_numbers()
    {
        _feed.Fail = true;
        var html = (await Get("/")).Html;
        Assert.Contains("Today's coin prices aren't available right now.", html);
        Assert.DoesNotContain("data-card=", html);
    }

    [Fact]
    public async Task The_sites_own_files_are_served_with_long_caching_when_versioned()
    {
        var html = (await Get("/")).Html;
        var css = System.Text.RegularExpressions.Regex.Match(html, """href="(/site/site\.css\?v=[0-9a-f]{10})""").Groups[1].Value;
        Assert.NotEmpty(css);

        using var versioned = await _http.GetAsync(css);
        Assert.Equal(HttpStatusCode.OK, versioned.StatusCode);
        Assert.Equal("text/css; charset=utf-8", versioned.Content.Headers.ContentType!.ToString());
        Assert.Contains("immutable", versioned.Headers.CacheControl!.ToString());
        Assert.Contains("--emerald", await versioned.Content.ReadAsStringAsync());

        using var font = await _http.GetAsync("/site/fonts/HankenGrotesk.woff2");
        Assert.Equal("font/woff2", font.Content.Headers.ContentType!.ToString());
        Assert.Equal("public, max-age=86400", font.Headers.CacheControl!.ToString());
        Assert.Equal(HttpStatusCode.OK, (await _http.GetAsync("/site/fonts/HankenGrotesk-OFL.txt")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _http.GetAsync("/site/../appsettings.json")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _http.GetAsync("/site/missing.js")).StatusCode);
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
