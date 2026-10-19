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
    public async Task The_first_thing_on_the_page_is_what_an_rtx_4090_earns_a_month_left_alone_16_hours_a_day()
    {
        var html = (await Get("/")).Html;

        // RTX 4090: Pearl 310 TH/s × 3.4e-14 $/H/s/day = $10.54, less SRBMiner 2%, unMineable 0.75% and Otium 3% = $9.94,
        // less 400 W for 24 h at $0.15/kWh ($1.44) = $8.50 a day of mining; left alone 16 hours a day, $5.67 a day and
        // $170 a month. Without a visitor country the price is $0.15/kWh. Internet sharing adds Proxybase's worldwide rate
        // for 1 GB a day ($1.50, less Otium's 3% = $1.455 a day, $43.65 a month): $214 in all.
        Assert.Contains("""<span class="est-amount" id="est-month">$214</span>""", html);
        Assert.Contains("$5.67 a day with an RTX 4090, mining Pearl (paid in your coin)", html);
        Assert.Contains("a day from sharing your internet (an estimate from Proxybase", html);
        Assert.Contains("\"sharingPerDay\":1.455", html);
        Assert.Contains("First payout of 1.5 USDT to your wallet within a day, then daily.", html);
        Assert.Contains("""<input id="hours" type="range" min="2" max="24" step="1" value="16">""", html);
        Assert.Contains("""<input id="price" type="range" min="0" max="0.60" step="0.01" value="0.15">""", html);
        Assert.DoesNotContain("typical in", html);
        Assert.True(html.IndexOf("est-month", StringComparison.Ordinal) < html.IndexOf("id=\"night-shift\"", StringComparison.Ordinal));
        // Quick chips for popular cards, and every card in the list grouped by series.
        Assert.Contains("""data-card="rtx4090" aria-pressed="true">""", html);
        Assert.Contains("""data-card="rtx3060" aria-pressed="false">""", html);
        Assert.Contains("""<optgroup label="RTX 50 series"><option value="rtx5090">RTX 5090 · NVIDIA &#183; 32 GB</option>""", html);
        Assert.Contains("""<option value="rtx4090" selected>RTX 4090 · NVIDIA &#183; 24 GB</option>""", html);
        Assert.DoesNotContain("AMD Radeon", html); // no card list entry without a coin it can mine to the user's wallet
        Assert.Equal(30, System.Text.RegularExpressions.Regex.Matches(html, "<option value=").Count);
        Assert.Contains("""<script type="application/json" id="otium-data">{"cards":[{"id":"rtx5090","name":"RTX 5090",""", html);
        Assert.Contains("\"payoutUsdt\":1.5", html);
        Assert.Contains("Today's coin prices from WhatToMine (18 October 2026, 15:00 UTC)", html);
    }

    [Fact]
    public void Internet_sharing_uses_the_visitors_country_and_real_figures_once_there_are_enough()
    {
        var fees = new Otium.Core.Fees.FeePolicy { FeePercent = 2 };
        var estimate = Otium.Server.SitePages.SharingFor("NO", fees, null);
        Assert.False(estimate.IsReal);
        Assert.Equal("Norway", estimate.CountryName);
        Assert.Equal(2.10 * 0.98, estimate.PerDay, precision: 6);

        var real = new Otium.Server.RealEarnings.RealEarningsSummary(0, 0, 0, [], 7,
            [new Otium.Server.RealEarnings.RealSharing("NO", 4, 3.0, 45)]);
        var measured = Otium.Server.SitePages.SharingFor("no", fees, real);
        Assert.True(measured.IsReal);
        Assert.Equal(1.5, measured.PerDay, precision: 6);
        Assert.Contains("4 Otium computers in Norway", measured.Short);
        Assert.False(Otium.Server.SitePages.SharingFor("SE", fees, real).IsReal); // other countries keep the estimate
    }

    [Fact]
    public async Task Pearls_useful_work_is_explained_without_claiming_to_run_ai_models()
    {
        var html = (await Get("/")).Html;
        Assert.Contains("Pearl mining runs the math AI runs on.", html);
        Assert.Contains("It doesn't run anyone's AI model or see their data.", html);
        Assert.Contains("""<a href="#useful-work">Mines Pearl, built on AI's matrix math</a>""", html);
        Assert.Equal(48, System.Text.RegularExpressions.Regex.Matches(html, "<span style=\"--r:").Count); // three 4×4 grids
    }

    [Fact]
    public async Task Cards_that_cant_mine_pearl_arent_listed_since_quantus_needs_a_second_wallet()
    {
        var html = (await Get("/")).Html;
        var data = html[html.IndexOf("""id="otium-data">""", StringComparison.Ordinal)..];
        Assert.DoesNotContain("\"id\":\"rtx2060\"", data); // 6 GB: Pearl needs 8
        Assert.DoesNotContain("\"id\":\"rx6700xt\"", data);
        Assert.DoesNotContain("Quantus", data);
    }

    [Theory]
    [InlineData("DE", "0.40", "Germany")]
    [InlineData("no", "0.15", "Norway")]
    [InlineData("US", "0.18", "United States")]
    public async Task The_electricity_price_starts_at_the_visitors_country(string country, string price, string name)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.Add("CF-IPCountry", country);
        using var response = await _http.SendAsync(request);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains($"""<input id="price" type="range" min="0" max="0.60" step="0.01" value="{price}">""", html);
        Assert.Contains($"<small>typical in {name}</small>", html);
    }

    [Theory]
    [InlineData("XX")]
    [InlineData("T1")]
    [InlineData("")]
    public void Unknown_or_tor_visitors_get_the_default_price(string country) =>
        Assert.Equal((0.15, (string?)null), Otium.Server.SitePages.PriceFor(country));

    [Fact]
    public async Task Every_response_carries_the_browser_hardening_headers()
    {
        foreach (var path in new[] { "/", "/api/v1/gpu-policy", "/site/site.css" })
        {
            using var response = await _http.GetAsync(path);
            Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
            Assert.Equal("strict-origin-when-cross-origin", response.Headers.GetValues("Referrer-Policy").Single());
            Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
            // Only the site's own scripts, plus Cloudflare's injected one through a fresh nonce.
            Assert.Matches("script-src 'self' 'nonce-[A-Za-z0-9+/]{22}==';", response.Headers.GetValues("Content-Security-Policy").Single());
            Assert.DoesNotContain("unsafe-inline';", response.Headers.GetValues("Content-Security-Policy").Single().Split("style-src")[0]);
            Assert.Contains("frame-ancestors 'none'", response.Headers.GetValues("Content-Security-Policy").Single());
        }
        // A new nonce on every response, or it would be no protection.
        using var first = await _http.GetAsync("/");
        using var second = await _http.GetAsync("/");
        Assert.NotEqual(first.Headers.GetValues("Content-Security-Policy").Single(), second.Headers.GetValues("Content-Security-Policy").Single());

        // No inline scripts on the site, so script-src 'self' blocks nothing it needs (the JSON data block doesn't run).
        var html = (await Get("/")).Html;
        Assert.DoesNotMatch("<script>", html);
        Assert.DoesNotMatch("<script(?![^>]*(src=|type=\"application/json\"))", html);
    }

    [Fact]
    public async Task Security_txt_points_researchers_to_private_reporting()
    {
        var (status, text) = await Get("/.well-known/security.txt");
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Contains("Contact: https://github.com/Malogna/otium/security/advisories/new\n", text);
        Assert.Contains("Expires: 2027-04-16T00:00:00Z\n", text); // 180 days from the test clock
        Assert.Contains("/.well-known/security.txt\n", text);
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
