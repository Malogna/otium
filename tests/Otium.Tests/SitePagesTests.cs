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
    private readonly FakeReleases _releases = new();
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
                services.RemoveAll<Otium.Server.IReleaseFeed>();
                services.AddSingleton<Otium.Server.IReleaseFeed>(_releases);
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
        Assert.Contains("""<a class="button" href="/download">""", html); // straight to the installer
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
    public async Task The_first_thing_on_the_page_is_what_a_typical_pc_earns_a_month()
    {
        var html = (await Get("/")).Html;

        // RTX 3060, the typical gaming PC: Pearl 41 TH/s × 3.4e-14 $/H/s/day = $1.39, less SRBMiner 2%, unMineable 0.75%
        // and Otium 3% = $1.32, less 100 W for 24 h at $0.15/kWh ($0.36) = $0.96 a day of mining; left alone 12 hours a
        // day, $0.48 a day and $14 a month. 1.5 USDT takes a little over 3 days.
        Assert.Contains("""<span class="est-amount" id="est-month">$14</span>""", html);
        Assert.Contains("$0.48 a day with an RTX 3060, mining Pearl (paid in your coin)", html);
        Assert.Contains("First payout of 1.5 USDT to your wallet in about 4 days, then daily.", html);
        Assert.True(html.IndexOf("est-month", StringComparison.Ordinal) < html.IndexOf("id=\"night-shift\"", StringComparison.Ordinal));
        Assert.Contains("""data-card="rtx3060" aria-pressed="true">""", html);
        Assert.Contains("""data-card="rx6700xt" aria-pressed="false">""", html);
        Assert.Contains("""<script type="application/json" id="otium-data">{"cards":[{"id":"rtx3060","name":"RTX 3060",""", html);
        Assert.Contains("\"payoutUsdt\":1.5", html);
        Assert.Contains("Today's coin prices from WhatToMine (18 October 2026, 15:00 UTC)", html);
    }

    [Fact]
    public void A_card_that_costs_more_in_power_than_it_earns_shows_nothing_earned()
    {
        // AMD can't mine Pearl on unMineable; Quantus on an RX 6700 XT earns $0.19 a day against $0.32 of power.
        var rx = new Otium.Server.CardEarnings("rx6700xt", "RX 6700 XT", "AMD · 12 GB", [new("Quantus", 0.1908, 90, "with a Quantus wallet")]);
        var estimate = Otium.Server.SitePages.Estimate(rx, 12, 0.15)!.Value;
        Assert.Equal(0, estimate.Day);
        Assert.Equal(0, estimate.Month);
        // With free power it pays: 12 hours of $0.19.
        Assert.Equal(0.0954, Otium.Server.SitePages.Estimate(rx, 12, 0)!.Value.Day, 4);
    }

    [Fact]
    public async Task Without_coin_prices_the_page_says_so_instead_of_inventing_numbers()
    {
        _feed.Fail = true;
        var html = (await Get("/")).Html;
        Assert.Contains("Today's coin prices aren't available right now.", html);
        Assert.DoesNotContain("data-card=", html);
        Assert.DoesNotContain("est-month", html);
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

    [Theory]
    [InlineData("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/130.0 Safari/537.36", "windows")]
    [InlineData("Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 Safari/605.1.15", "macos")]
    [InlineData("Mozilla/5.0 (X11; Linux x86_64; rv:131.0) Gecko/20100101 Firefox/131.0", "linux")]
    [InlineData("Mozilla/5.0 (Linux; Android 14; Pixel 8) AppleWebKit/537.36 Chrome/130.0 Mobile Safari/537.36", null)]
    [InlineData("Mozilla/5.0 (iPhone; CPU iPhone OS 18_0 like Mac OS X) AppleWebKit/605.1.15 Mobile/15E148", null)]
    [InlineData(null, null)]
    public void The_visitors_system_is_read_from_the_browser(string? userAgent, string? os) =>
        Assert.Equal(os, Otium.Server.ReleaseDownloads.DetectOs(userAgent));

    [Fact]
    public async Task Download_goes_straight_to_the_installer_for_the_visitors_computer()
    {
        _releases.Assets =
        [
            new("Otium.Desktop-win-x64-Setup.exe", "https://dl.example/win-setup.exe"),
            new("Otium.Desktop-win-x64-Portable.zip", "https://dl.example/win.zip"),
            new("Otium.Desktop-osx-arm64-Setup.pkg", "https://dl.example/mac-arm.pkg"),
            new("Otium.Desktop-osx-x64-Setup.pkg", "https://dl.example/mac-intel.pkg"),
            new("Otium.Desktop-linux-x64.AppImage", "https://dl.example/linux.AppImage"),
            new("RELEASES-win-x64", "https://dl.example/RELEASES"),
        ];
        using var noRedirect = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        async Task<string?> Where(string path, string? userAgent)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, path);
            if (userAgent is not null)
                request.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            using var response = await noRedirect.SendAsync(request);
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            return response.Headers.Location?.ToString();
        }

        Assert.Equal("https://dl.example/win-setup.exe", await Where("/download", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)"));
        Assert.Equal("https://dl.example/mac-arm.pkg", await Where("/download", "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7)"));
        Assert.Equal("https://dl.example/linux.AppImage", await Where("/download", "Mozilla/5.0 (X11; Linux x86_64)"));
        Assert.Equal("https://dl.example/mac-intel.pkg", await Where("/download?os=macos-intel", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)"));
        // Phones can't install it: the releases page explains what's there.
        Assert.Equal("https://example.com/download?a=1&b=2", await Where("/download", "Mozilla/5.0 (iPhone; CPU iPhone OS 18_0 like Mac OS X) Mobile"));
    }

    [Fact]
    public async Task Before_the_first_release_download_goes_to_the_releases_page()
    {
        using var noRedirect = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var request = new HttpRequestMessage(HttpMethod.Get, "/download");
        request.Headers.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
        using var response = await noRedirect.SendAsync(request);
        Assert.Equal("https://example.com/download?a=1&b=2", response.Headers.Location?.ToString());
    }

    private sealed class FakeReleases : Otium.Server.IReleaseFeed
    {
        public IReadOnlyList<Otium.Server.ReleaseAsset> Assets { get; set; } = [];

        public Task<IReadOnlyList<Otium.Server.ReleaseAsset>> GetLatestAsync(CancellationToken ct) => Task.FromResult(Assets);
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
