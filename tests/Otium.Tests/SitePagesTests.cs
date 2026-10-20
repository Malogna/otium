using System.Net;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Otium.Core.Pools;
using Otium.Core.Updates;
using Otium.Server.Gpu;

namespace Otium.Tests;

/// <summary>The public website: the front page, its embedded files, the invite page, and legal pages that appear only once their files exist.</summary>
public sealed class SitePagesTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"otium-site-{Guid.NewGuid():N}.db");
    private readonly string _pages = Path.Combine(Path.GetTempPath(), $"otium-site-pages-{Guid.NewGuid():N}");
    private readonly ManualTime _time = new(new DateTimeOffset(2026, 10, 18, 15, 0, 0, TimeSpan.Zero));
    private readonly GpuPolicyTests.FakeFeed _feed = new();
    private readonly FakeReleaseList _releaseList = new();
    /// <summary>Signs these tests' update feeds; the server trusts it instead of Otium's real keys.</summary>
    private readonly ECDsa _signingKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
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
                services.RemoveAll<Otium.Server.IReleaseList>();
                services.AddSingleton<Otium.Server.IReleaseList>(_releaseList);
                services.RemoveAll<Otium.Server.UpdateSigningKeys>();
                services.AddSingleton(new Otium.Server.UpdateSigningKeys([Convert.ToBase64String(_signingKey.ExportSubjectPublicKeyInfo())]));
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
        Assert.Contains("Mining: $5.67/day on an RTX 4090 (Pearl).", html);
        Assert.Contains("Internet sharing: +$1.46/day (estimate).", html);
        Assert.Contains("\"sharingPerDay\":1.455", html);
        Assert.Contains("First payout within a day.", html);
        // Like for like with mining calculators and comparison sites: $9.94 a day around the clock, no electricity.
        Assert.Contains("""<p class="est-compare" id="est-compare">Mining around the clock, before electricity: $298 a month, the way most comparison sites count it.</p>""", html);
        Assert.Contains("""<input id="hours" type="range" min="2" max="24" step="1" value="16">""", html);
        Assert.Contains("""<input id="price" type="range" min="0" max="0.60" step="0.01" value="0.15">""", html);
        Assert.DoesNotContain("typical in", html);
        Assert.True(html.IndexOf("est-month", StringComparison.Ordinal) < html.IndexOf("id=\"night-shift\"", StringComparison.Ordinal));
        // At first sight: the figure, one sentence on what it assumes, and the card list; the breakdown and the sliders
        // wait behind a closed "See the breakdown and adjust".
        Assert.Contains("""<p class="est-summary" id="est-summary">An RTX 4090 mining while you're away (16 h a day), plus internet sharing.</p>""", html);
        Assert.Contains("""<details class="est-more">""", html); // closed: no "open"
        Assert.True(html.IndexOf("est-summary", StringComparison.Ordinal) < html.IndexOf("<details class=\"est-more\">", StringComparison.Ordinal));
        Assert.True(html.IndexOf("<details class=\"est-more\">", StringComparison.Ordinal) < html.IndexOf("id=\"est-sub\"", StringComparison.Ordinal));
        Assert.DoesNotContain("data-card=", html); // one card picker, not two
        // Every card in the list, grouped by series.
        Assert.Contains("""<optgroup label="RTX 50 series"><option value="rtx5090">RTX 5090 · NVIDIA &#183; 32 GB</option>""", html);
        Assert.Contains("""<option value="rtx4090" selected>RTX 4090 · NVIDIA &#183; 24 GB</option>""", html);
        Assert.DoesNotContain("AMD Radeon", html); // no card list entry without a coin it can mine to the user's wallet
        Assert.Equal(30, System.Text.RegularExpressions.Regex.Matches(html, "<option value=").Count);
        Assert.Contains("""<script type="application/json" id="otium-data">{"cards":[{"id":"rtx5090","name":"RTX 5090",""", html);
        Assert.Contains("\"payoutUsdt\":1.5", html);
        Assert.Contains("Estimate from today's coin prices (18 October 2026, 15:00 UTC)", html);
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

        // No inline scripts on the site, so script-src 'self' blocks nothing it needs (the JSON data blocks, the page's
        // own and the structured data for search engines, don't run).
        var html = (await Get("/")).Html;
        Assert.DoesNotMatch("<script>", html);
        Assert.DoesNotMatch("<script(?![^>]*(src=|type=\"application/(ld\\+)?json\"))", html);
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
    public async Task Each_graphics_card_has_a_page_with_what_it_earns_today()
    {
        var (status, html) = await Get("/mining/rtx-4090");

        Assert.Equal(HttpStatusCode.OK, status);
        // Same figures as the front page: $9.94 a day after every fee, before electricity; 400 W is 9.6 kWh a day, so it
        // breaks even at $1.04/kWh. At $0.15/kWh, left alone 16 hours a day: $170 a month.
        Assert.Contains("<title>RTX 4090 mining profitability today | Otium</title>", html);
        Assert.Contains("About <strong>$9.94 a day</strong> mining Pearl", html);
        Assert.Contains("<strong>$298 a month</strong>", html);
        Assert.Contains("<dt>Breaks even at</dt><dd>$1.04/kWh</dd>", html);
        Assert.Contains("$170 a month<small>mining while you're away, 16 h a day</small>", html);
        // After electricity: a loss around the clock shows as one; Otium's column waits instead.
        Assert.Contains("<tr><th scope=\"row\">$0.05/kWh</th><td>$9.46</td><td>$284</td><td>$189</td></tr>", html);
        Assert.Contains("""<link rel="canonical" href="http://localhost/mining/rtx-4090">""", html);
        Assert.Contains("""<a href="/mining/rtx-4080">RTX 4080</a>""", html); // the same series' other cards
        Assert.DoesNotContain("""<a href="/mining/rtx-4090">""", html.Split("Other RTX 40 series cards")[1]);

        // Breadcrumbs and the questions for search engines.
        var json = System.Text.RegularExpressions.Regex.Match(html, """<script type="application/ld\+json">(.*?)</script>""").Groups[1].Value;
        using var data = System.Text.Json.JsonDocument.Parse(json);
        var graph = data.RootElement.GetProperty("@graph").EnumerateArray().ToList();
        Assert.Equal(["BreadcrumbList", "FAQPage"], graph.Select(g => g.GetProperty("@type").GetString()));
        Assert.Equal("http://localhost/mining/rtx-4090", graph[0].GetProperty("itemListElement")[2].GetProperty("item").GetString());
        Assert.Equal("Is mining with an RTX 4090 worth it?", graph[1].GetProperty("mainEntity")[0].GetProperty("name").GetString());

        // A card's id moves to its page for good; unknown cards, and cards that can't mine Pearl, have none.
        using var noRedirects = _factory.CreateClient(new() { AllowAutoRedirect = false });
        using var moved = await noRedirects.GetAsync("/mining/rtx4090");
        Assert.Equal(HttpStatusCode.MovedPermanently, moved.StatusCode);
        Assert.Equal("/mining/rtx-4090", moved.Headers.Location?.OriginalString);
        Assert.Equal(HttpStatusCode.NotFound, (await Get("/mining/rtx-9999")).Status);
        Assert.Equal(HttpStatusCode.NotFound, (await Get("/mining/rtx-2060")).Status);
    }

    [Fact]
    public async Task The_mining_page_compares_every_card_best_paying_first()
    {
        var (status, html) = await Get("/mining");

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Contains("The RTX 5090 leads at", html);
        var rows = System.Text.RegularExpressions.Regex.Matches(html, """<tr><th scope="row"><a href="/mining/([a-z0-9-]+)">""")
            .Select(m => m.Groups[1].Value).ToList();
        Assert.Equal(30, rows.Count);
        Assert.Equal("rtx-5090", rows[0]);
        Assert.True(rows.IndexOf("rtx-4090") < rows.IndexOf("rtx-3060"));
        Assert.Contains("""<a href="/mining">Graphics cards</a>""", html); // in the nav, on every page
        Assert.Contains("""<a class="est-all" href="/mining">""", (await Get("/")).Html);
    }

    [Fact]
    public async Task The_antivirus_page_says_why_and_how_to_allow_only_the_engines_folder()
    {
        var (status, html) = await Get("/antivirus");

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Contains("""<link rel="canonical" href="http://localhost/antivirus">""", html);
        Assert.Contains(@"%LOCALAPPDATA%\Otium\engines", html); // the folder AppPaths.EnginesDir uses
        Assert.Contains("~/Library/Application Support/Otium/engines", html);
        Assert.Contains("Allow automatically", html); // the app's one-click Defender fix
        foreach (var vendor in new[] { "avast", "avg", "norton", "bitdefender", "kaspersky", "eset", "malwarebytes", "mcafee" })
            Assert.Contains($"""<details id="av-{vendor}">""", html);
        // Each vendor's steps end back in Otium, and link the vendor's own help.
        Assert.Equal(8, System.Text.RegularExpressions.Regex.Matches(html, "press <strong>Try again</strong>.</li></ol>").Count);
        Assert.Contains("""rel="nofollow">Norton 360's own help</a>""", html);

        var json = System.Text.RegularExpressions.Regex.Match(html, """<script type="application/ld\+json">(.*?)</script>""").Groups[1].Value;
        using var data = System.Text.Json.JsonDocument.Parse(json);
        var faq = data.RootElement.GetProperty("@graph")[1];
        Assert.Equal("FAQPage", faq.GetProperty("@type").GetString());
        Assert.Equal("Is Otium a virus?", faq.GetProperty("mainEntity")[0].GetProperty("name").GetString());

        // Found from the front page's antivirus question and every page's footer.
        var home = (await Get("/")).Html;
        Assert.Contains("""<a href="/antivirus">How to allow it in your antivirus</a>""", home);
        Assert.Contains("""<a href="/antivirus">Antivirus help</a>""", home);
    }

    [Theory]
    [InlineData("nicehash", "NiceHash", "https://www.nicehash.com/support/general-help/kyc/what-is-kyc")]
    [InlineData("honeygain", "Honeygain", "https://www.honeygain.com/terms-of-use/")]
    public async Task Comparisons_cite_the_other_apps_own_pages(string slug, string name, string source)
    {
        var (status, html) = await Get($"/compare/{slug}");

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Contains($"<title>{name} alternative: Otium vs {name} compared | Otium</title>", html);
        Assert.Contains($"""<link rel="canonical" href="http://localhost/compare/{slug}">""", html);
        Assert.Contains($"""<a href="{source}" rel="nofollow">""", html);
        Assert.Contains($"Where {name} is the better choice", html);
        Assert.Contains($"Otium isn't affiliated with {name}.", html);
        Assert.Contains("Otium&#39;s 3%", html); // the server's own fee, not a number written into the page
        Assert.Contains($"""<a href="/compare/{slug}">vs {name}</a>""", html);
    }

    [Theory]
    [InlineData("/", "text/html")]
    [InlineData("/sitemap.xml", "application/xml")]
    [InlineData("/robots.txt", "text/plain")]
    [InlineData("/mining/rtx-4090", "text/html")]
    public async Task Head_requests_get_the_pages_headers_without_a_body(string path, string type)
    {
        using var response = await _http.SendAsync(new HttpRequestMessage(HttpMethod.Head, path));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode); // not 405, which crawlers and link checkers read as broken
        Assert.Equal(type, response.Content.Headers.ContentType?.MediaType);
        Assert.Empty(await response.Content.ReadAsByteArrayAsync());
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
    }

    [Fact]
    public async Task Head_requests_for_missing_pages_still_say_not_found()
    {
        using var response = await _http.SendAsync(new HttpRequestMessage(HttpMethod.Head, "/compare/kryptex"));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task The_pearl_page_explains_the_coin_with_its_risks_and_sources()
    {
        var (status, html) = await Get("/pearl");

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Contains("""<link rel="canonical" href="http://localhost/pearl">""", html);
        Assert.Contains("""<a href="/mining/rtx-5090">RTX 5090</a> $""", html); // today's best cards, linked
        Assert.Contains("The risks, plainly", html);
        Assert.Contains("only let whitelisted GPUs mine", html); // the planned FP8 upgrade
        Assert.Contains("""<a href="https://arxiv.org/abs/2606.04819" rel="nofollow">""", html); // the study disputing its usefulness
        Assert.Contains("Otium isn't affiliated with Pearl Research Labs.", html);
        Assert.Contains("\"@type\":\"FAQPage\"", html);
        // Linked from the front page's Pearl section, every card page and the footer.
        Assert.Contains("""see their data. <a href="/pearl">What is Pearl?</a>""", (await Get("/")).Html);
        Assert.Contains("""<a href="/pearl">What is Pearl?</a>""", (await Get("/mining/rtx-3080")).Html);
        Assert.Contains("<loc>http://localhost/pearl</loc>", (await Get("/sitemap.xml")).Html);
    }

    [Fact]
    public async Task The_front_page_links_popular_cards_with_what_they_earn()
    {
        var html = (await Get("/")).Html;
        Assert.Contains("""<a href="/mining/rtx-4090">RTX 4090<span>$9.94/day</span></a>""", html);
        Assert.Contains("""<a class="all" href="/mining">Every card</a>""", html);
    }

    [Fact]
    public async Task Missing_pages_get_a_page_with_the_way_back_but_the_api_keeps_bare_404s()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/no-such-page");
        request.Headers.Accept.ParseAdd("text/html");
        using var page = await _http.SendAsync(request);
        Assert.Equal(HttpStatusCode.NotFound, page.StatusCode);
        var html = await page.Content.ReadAsStringAsync();
        Assert.Contains("This page doesn't exist", html);
        Assert.Contains("""<meta name="robots" content="noindex, follow">""", html);
        Assert.Contains("""<a href="/mining">What each graphics card earns</a>""", html);

        var api = new HttpRequestMessage(HttpMethod.Get, "/api/v1/no-such-thing");
        api.Headers.Accept.ParseAdd("text/html");
        using var bare = await _http.SendAsync(api);
        Assert.Equal(HttpStatusCode.NotFound, bare.StatusCode);
        Assert.Empty(await bare.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task The_sitemap_dates_the_priced_pages_by_todays_prices()
    {
        var sitemap = (await Get("/sitemap.xml")).Html;
        Assert.Contains("<url><loc>http://localhost/mining/rtx-4090</loc><lastmod>2026-10-18T15:00:00Z</lastmod></url>", sitemap);
        Assert.Contains("<url><loc>http://localhost/</loc><lastmod>2026-10-18T15:00:00Z</lastmod></url>", sitemap);
        Assert.Contains("<url><loc>http://localhost/antivirus</loc></url>", sitemap); // unchanged by prices: no date
    }

    [Fact]
    public async Task IndexNow_gets_the_pages_and_the_key_file_proves_the_site_sent_them()
    {
        Assert.Equal(Otium.Server.IndexNowService.Key, (await Get($"/{Otium.Server.IndexNowService.Key}.txt")).Html);

        var handler = new RecordingHandler();
        var service = new Otium.Server.IndexNowService(new HttpClient(handler),
            Microsoft.Extensions.Options.Options.Create(new Otium.Server.ReferralOptions { PublicBaseUrl = "https://otium.lol" }),
            Microsoft.Extensions.Options.Options.Create(new Otium.Server.SiteOptions { PagesDirectory = _pages }), _time,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<Otium.Server.IndexNowService>.Instance);
        await service.NotifyAsync("https://otium.lol/", ["/", "/mining/rtx-4090"], CancellationToken.None);

        Assert.Equal("https://api.indexnow.org/indexnow", handler.Uri?.ToString());
        using var notice = System.Text.Json.JsonDocument.Parse(handler.Body!);
        Assert.Equal("otium.lol", notice.RootElement.GetProperty("host").GetString());
        Assert.Equal($"https://otium.lol/{Otium.Server.IndexNowService.Key}.txt", notice.RootElement.GetProperty("keyLocation").GetString());
        Assert.Equal(["https://otium.lol/", "https://otium.lol/mining/rtx-4090"],
            notice.RootElement.GetProperty("urlList").EnumerateArray().Select(u => u.GetString()));
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public Uri? Uri { get; private set; }
        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Uri = request.RequestUri;
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.Accepted);
        }
    }

    [Fact]
    public async Task Every_mining_page_has_its_own_preview_picture()
    {
        // tools/render-site-previews.ps1 draws one per mining page; a card added without running it fails here.
        var sitemap = (await Get("/sitemap.xml")).Html;
        var paths = System.Text.RegularExpressions.Regex.Matches(sitemap, "<loc>http://localhost(/mining[^<]*)</loc>").Select(m => m.Groups[1].Value).ToList();
        Assert.Equal(31, paths.Count);
        foreach (var path in paths)
        {
            var html = (await Get(path)).Html;
            var name = path == "/mining" ? "mining" : path.Split('/')[^1];
            var image = System.Text.RegularExpressions.Regex.Match(html, $$"""<meta property="og:image" content="http://localhost(/site/previews/{{name}}\.jpg\?v=[0-9a-f]{10})">""");
            Assert.True(image.Success, path);
            Assert.Contains("""<meta property="og:image:type" content="image/jpeg">""", html);
            using var jpeg = await _http.GetAsync(image.Groups[1].Value);
            Assert.Equal("image/jpeg", jpeg.Content.Headers.ContentType?.MediaType);
        }
        Assert.Contains("""<meta property="og:image:alt" content="RTX 4090: 310 TH/s, 400 W. What it earns mining today, on otium.lol">""", (await Get("/mining/rtx-4090")).Html);
        // Other pages keep the site's own picture.
        Assert.Contains("/site/og.png?v=", (await Get("/pearl")).Html);

        // The pages the pictures are drawn from (development only).
        Assert.Contains("<h1>RTX 4090</h1>", (await Get("/dev/preview/mining/rtx-4090")).Html);
        Assert.Contains("""<h1 class="long">What every graphics card earns</h1>""", (await Get("/dev/preview/mining")).Html);
        Assert.Equal(HttpStatusCode.NotFound, (await Get("/dev/preview/mining/rtx-2060")).Status);
    }

    [Fact]
    public async Task Only_known_comparisons_exist()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await Get("/compare/kryptex")).Status);
        var sitemap = (await Get("/sitemap.xml")).Html;
        Assert.Contains("<loc>http://localhost/antivirus</loc>", sitemap);
        Assert.Contains("<loc>http://localhost/compare/nicehash</loc>", sitemap);
        Assert.Contains("<loc>http://localhost/compare/honeygain</loc>", sitemap);
    }

    [Fact]
    public async Task Without_coin_prices_the_card_pages_say_so_but_still_answer()
    {
        _feed.Fail = true;
        var (status, html) = await Get("/mining/rtx-3080");
        Assert.Equal(HttpStatusCode.OK, status); // not a 404 that would drop it from search results
        Assert.Contains("Today's coin prices aren't available right now", html);
        Assert.Contains("<dt>Speed</dt><dd>100 TH/s</dd>", html);
        Assert.DoesNotContain("Breaks even at", html);
        Assert.Contains("<td>–</td>", (await Get("/mining")).Html);
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
        var stylesheet = await versioned.Content.ReadAsStringAsync();
        Assert.Contains("--emerald", stylesheet);

        // The stylesheet loads its fonts by versioned address, the same one the page preloads, so they're cached for a
        // year too and downloaded once.
        var fontUrl = System.Text.RegularExpressions.Regex.Match(stylesheet, """url\("(/site/fonts/HankenGrotesk\.woff2\?v=[0-9a-f]{10})"\)""").Groups[1].Value;
        Assert.NotEmpty(fontUrl);
        Assert.Contains($"""<link rel="preload" href="{fontUrl}" as="font" """, html);
        using var font = await _http.GetAsync(fontUrl);
        Assert.Equal("font/woff2", font.Content.Headers.ContentType!.ToString());
        Assert.Contains("immutable", font.Headers.CacheControl!.ToString());
        using var plainFont = await _http.GetAsync("/site/fonts/HankenGrotesk.woff2");
        Assert.Equal("public, max-age=86400", plainFont.Headers.CacheControl!.ToString());
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
        Assert.Contains("<title>Otium privacy policy</title>", html);
        Assert.Contains("""<link rel="canonical" href="http://localhost/privacy">""", html);
        Assert.Contains("<loc>http://localhost/privacy</loc>", (await Get("/sitemap.xml")).Html);
        Assert.DoesNotContain("/terms", (await Get("/sitemap.xml")).Html);
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
        Assert.Contains("""<a href="https://discord.gg/4FtFDrz97t">Discord</a>""", html);
        // Outside Cloudflare's Email Obfuscation, whose decoding script the pages' CSP would block.
        Assert.Contains("""<!--email_off--><a href="mailto:contact@otium.lol">contact@otium.lol</a><!--/email_off-->""", html);
        // One page per code: kept out of search results, but shared links still get the full preview.
        Assert.Contains("""<meta name="robots" content="noindex, follow">""", html);
        Assert.DoesNotContain("rel=\"canonical\"", html);
        Assert.Contains("""<meta name="twitter:card" content="summary_large_image">""", html);
    }

    [Fact]
    public async Task Search_engines_and_link_previews_get_what_they_need()
    {
        // No public base URL is configured in tests, so addresses use the request's host.
        var (_, html) = await Get("/?utm_source=ad");
        Assert.Contains("""<link rel="canonical" href="http://localhost/">""", html); // without the ad's query string
        Assert.DoesNotContain("noindex", html);
        Assert.Contains("""<meta property="og:url" content="http://localhost/">""", html);
        var image = System.Text.RegularExpressions.Regex.Match(html, """<meta property="og:image" content="http://localhost(/site/og\.png\?v=[0-9a-f]{10})">""");
        Assert.True(image.Success);
        using (var png = await _http.GetAsync(image.Groups[1].Value))
        {
            Assert.Equal("image/png", png.Content.Headers.ContentType?.MediaType);
            var bytes = await png.Content.ReadAsByteArrayAsync();
            Assert.Equal(1200, System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(16))); // PNG header width…
            Assert.Equal(630, System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(20))); // …and height
        }

        // Structured data: the app, who makes it, and the same questions the page shows.
        var json = System.Text.RegularExpressions.Regex.Match(html, """<script type="application/ld\+json">(.*?)</script>""").Groups[1].Value;
        using var data = System.Text.Json.JsonDocument.Parse(json);
        var graph = data.RootElement.GetProperty("@graph").EnumerateArray().ToList();
        Assert.Equal(["Organization", "WebSite", "SoftwareApplication", "FAQPage"], graph.Select(g => g.GetProperty("@type").GetString()));
        Assert.Equal("Windows, macOS, Linux", graph[2].GetProperty("operatingSystem").GetString());
        var questions = graph[3].GetProperty("mainEntity").EnumerateArray().ToList();
        Assert.Equal(System.Text.RegularExpressions.Regex.Matches(html, "<details[^>]*><summary>").Count, questions.Count);
        Assert.Equal("Is my money safe?", questions[0].GetProperty("name").GetString());
        Assert.Contains("<summary>Is my money safe?</summary>", html);
        Assert.Contains("""<details id="why-fee"><summary>Why does Otium take 2%?</summary>""", html);
        Assert.Contains("href=\"#why-fee\"", html);

        var robots = (await Get("/robots.txt")).Html;
        Assert.DoesNotContain("/admin", robots); // listing it would only advertise it to scanners
        Assert.DoesNotContain("/dev/", robots);
        Assert.Contains("Disallow: /api/\n", robots);
        Assert.DoesNotContain("/r/", robots); // invite pages must stay crawlable for their noindex to be seen
        Assert.Contains("Sitemap: http://localhost/sitemap.xml\n", robots);
        Assert.Contains("Disallow: /download/\n", robots); // the installer redirects, not the download page
        Assert.DoesNotContain("Disallow: /download\n", robots);

        var sitemap = (await Get("/sitemap.xml")).Html;
        Assert.StartsWith("""<?xml version="1.0" encoding="UTF-8"?>""", sitemap);
        Assert.Contains("<loc>http://localhost/</loc>", sitemap);
        Assert.Contains("<loc>http://localhost/download</loc>", sitemap);
        Assert.Contains("<loc>http://localhost/mining</loc>", sitemap);
        Assert.Contains("<loc>http://localhost/mining/rtx-4070-ti-super</loc>", sitemap);
        Assert.DoesNotContain("rtx-2060<", sitemap); // 6 GB: can't mine Pearl
        Assert.DoesNotContain("/privacy", sitemap); // no legal pages yet
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

    /// <summary>A release with every installer the release build makes, as GitHub lists them.</summary>
    private static Otium.Server.PublishedRelease Release(string tag, bool prerelease)
    {
        var dl = $"https://github.com/Malogna/otium/releases/download/{tag}";
        return new(tag, prerelease,
        [
            new($"Otium.Desktop-{tag.TrimStart('v')}-win-x64-full.nupkg", $"{dl}/full.nupkg"),
            new("Otium.Desktop-win-x64-Setup.exe", $"{dl}/Otium.Desktop-win-x64-Setup.exe", SetupSha256),
            new("Otium.Desktop-win-x64-Portable.zip", $"{dl}/Otium.Desktop-win-x64-Portable.zip"),
            new("Otium.Desktop-osx-arm64-Setup.pkg", $"{dl}/Otium.Desktop-osx-arm64-Setup.pkg"),
            new("Otium.Desktop-osx-x64-Setup.pkg", $"{dl}/Otium.Desktop-osx-x64-Setup.pkg"),
            new("Otium.Desktop-linux-x64.AppImage", $"{dl}/Otium.Desktop-linux-x64.AppImage"),
            new("releases.win-x64.json", $"{dl}/releases.win-x64.json"),
        ]);
    }

    private async Task<HttpResponseMessage> Visit(string path, string? userAgent)
    {
        using var noRedirect = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        if (userAgent is not null)
            request.Headers.TryAddWithoutValidation("User-Agent", userAgent);
        return await noRedirect.SendAsync(request);
    }

    private const string WindowsBrowser = "Mozilla/5.0 (Windows NT 10.0; Win64; x64)";
    private const string SetupSha256 = "4799b1743ee2cb74bcf7339d39d5a2115892ac0d70818dd56aee738ce59ab8e6";

    [Fact]
    public async Task Download_shows_otiums_own_page_and_starts_the_installer_for_the_visitors_computer()
    {
        // Only release candidates so far: GitHub's "latest release" leaves them out, the download page doesn't.
        _releaseList.Releases = [Release("v0.1.0-rc.9", true), Release("v0.1.0-rc.13", true), Release("v0.1.0-rc.12", true)];

        using var windows = await Visit("/download", WindowsBrowser);
        Assert.Equal(HttpStatusCode.OK, windows.StatusCode);
        var html = await windows.Content.ReadAsStringAsync();
        Assert.Contains("""<meta http-equiv="refresh" content="1;url=/download/windows">""", html); // starts by itself
        Assert.Contains("Otium 0.1.0-rc.13 for Windows", html); // the newest version, not GitHub's tag-name order
        Assert.Contains("Run anyway", html); // what to do about the unsigned installer's warning
        Assert.Contains("""<details id="install-windows" open>""", html); // the visitor's own system's steps are open
        Assert.Contains("""<details id="install-macos">""", html);
        Assert.Contains("Smart App Control", html);
        Assert.Contains($"<code>{SetupSha256}</code>", html); // GitHub's checksum, and how to check it
        Assert.Contains(@"Get-FileHash .\Otium.Desktop-win-x64-Setup.exe", html);
        Assert.Contains("""href="/download/macos-intel">macOS (Intel)</a>""", html); // the other systems
        Assert.DoesNotContain("""href="/download/windows">Windows</a>""", html);

        using var mac = await Visit("/download", "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7)");
        Assert.Contains("Otium 0.1.0-rc.13 for macOS (Apple silicon)", await mac.Content.ReadAsStringAsync());
        using var linux = await Visit("/download?os=linux", WindowsBrowser);
        var linuxHtml = await linux.Content.ReadAsStringAsync();
        Assert.Contains("chmod +x", linuxHtml);
        Assert.Contains("""<details id="install-linux" open>""", linuxHtml);

        // Phones can't install it: the page says so, lists every system and starts nothing.
        using var phone = await Visit("/download", "Mozilla/5.0 (iPhone; CPU iPhone OS 18_0 like Mac OS X) Mobile");
        var phoneHtml = await phone.Content.ReadAsStringAsync();
        Assert.DoesNotContain("http-equiv=\"refresh\"", phoneHtml);
        Assert.Contains("On a phone? Open otium.lol on the computer", phoneHtml);
        Assert.Contains("""href="/download/windows">Windows</a>""", phoneHtml);

        // Each button is a direct link to that system's installer on GitHub.
        async Task<string?> Where(string path) => (await Visit(path, WindowsBrowser)).Headers.Location?.ToString();
        const string dl = "https://github.com/Malogna/otium/releases/download/v0.1.0-rc.13";
        Assert.Equal($"{dl}/Otium.Desktop-win-x64-Setup.exe", await Where("/download/windows"));
        Assert.Equal($"{dl}/Otium.Desktop-osx-arm64-Setup.pkg", await Where("/download/macos"));
        Assert.Equal($"{dl}/Otium.Desktop-osx-x64-Setup.pkg", await Where("/download/macos-intel"));
        Assert.Equal($"{dl}/Otium.Desktop-linux-x64.AppImage", await Where("/download/linux"));
        Assert.Equal("/download", await Where("/download/amiga"));
    }

    [Fact]
    public async Task Once_there_is_a_stable_release_the_page_offers_it_over_newer_release_candidates()
    {
        _releaseList.Releases = [Release("v0.1.1-rc.1", true), Release("v0.1.0", false), Release("v0.1.0-rc.13", true)];
        using var response = await Visit("/download", WindowsBrowser);
        Assert.Contains("Otium 0.1.0 for Windows", await response.Content.ReadAsStringAsync());
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

    [Fact]
    public async Task Installed_apps_get_their_update_feed_here_not_from_githubs_api()
    {
        const string gh = "https://github.com/Malogna/otium/releases/download";
        _releaseList.Releases =
        [
            Signed("v0.1.0-rc.9", true, "releases.win-x64.json", """{"Assets":[{"Version":"0.1.0-rc.9"}]}""",
                new("Otium.Desktop-0.1.0-rc.9-win-x64-full.nupkg", $"{gh}/v0.1.0-rc.9/Otium.Desktop-0.1.0-rc.9-win-x64-full.nupkg")),
            Signed("v0.0.9", false, "releases.win-x64.json", """{"Assets":[{"Version":"0.0.9"}]}"""),
        ];

        // Release candidates follow the newest pre-release; stable installs only see releases.
        var (preStatus, pre) = await Get("/updates/pre/releases.win-x64.json?localVersion=0.1.0-rc.8");
        Assert.Equal(HttpStatusCode.OK, preStatus);
        Assert.Contains("0.1.0-rc.9", pre);
        var (_, stable) = await Get("/updates/stable/releases.win-x64.json");
        Assert.Contains("0.0.9", stable);
        Assert.Equal(HttpStatusCode.NotFound, (await Get("/updates/pre/releases.osx-arm64.json")).Status);
        Assert.Equal(HttpStatusCode.NotFound, (await Get("/updates/nightly/releases.win-x64.json")).Status);

        // Packages are GitHub's own downloads, which have no hourly limit.
        using var noRedirect = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var response = await noRedirect.GetAsync("/updates/pre/Otium.Desktop-0.1.0-rc.9-win-x64-full.nupkg");
        Assert.Equal($"{gh}/v0.1.0-rc.9/Otium.Desktop-0.1.0-rc.9-win-x64-full.nupkg", response.Headers.Location?.ToString());
    }

    [Fact]
    public async Task The_feed_follows_the_newest_version_not_githubs_tag_name_order()
    {
        // GitHub lists releases by tag name, so rc.10 comes after rc.1 and rc.2 there; the feed must still pick it.
        const string gh = "https://github.com/Malogna/otium/releases/download";
        Otium.Server.PublishedRelease Rc(string tag) => Signed(tag, true, "releases.win-x64.json", $$"""{"Assets":[{"Version":"{{tag[1..]}}"}]}""");
        _releaseList.Releases = [Rc("v0.1.0-rc.9"), Rc("v0.1.0-rc.2"), Rc("v0.1.0-rc.10"), Rc("v0.1.0-rc.1")];

        var (_, feed) = await Get("/updates/pre/releases.win-x64.json");
        Assert.Contains("0.1.0-rc.10", feed);
    }

    [Theory]
    [InlineData("v0.1.0-rc.10", "v0.1.0-rc.9")]
    [InlineData("v0.1.0", "v0.1.0-rc.10")]
    [InlineData("v0.2.0-rc.1", "v0.1.9")]
    [InlineData("v1.0.0", "v0.10.0")]
    [InlineData("v0.1.0-rc.2", "v0.1.0-beta.9")]
    public void Release_tags_order_by_version(string newer, string older)
    {
        Assert.True(Otium.Core.Updates.ReleaseVersion.Compare(newer, older) > 0);
        Assert.True(Otium.Core.Updates.ReleaseVersion.Compare(older, newer) < 0);
        Assert.Equal(0, Otium.Core.Updates.ReleaseVersion.Compare(newer, newer));
    }

    [Fact]
    public void The_release_list_skips_drafts_and_marks_release_candidates()
    {
        using var doc = System.Text.Json.JsonDocument.Parse("""
            [
              {"tag_name":"v0.2.0","draft":true,"prerelease":false,"assets":[]},
              {"tag_name":"v0.1.0-rc.9","draft":false,"prerelease":true,"assets":[{"name":"a.nupkg","browser_download_url":"https://x/a.nupkg","digest":"sha256:4799B1743EE2CB74BCF7339D39D5A2115892AC0D70818DD56AEE738CE59AB8E6"},{"name":"b.exe","browser_download_url":"https://x/b.exe","digest":"md5:abc"}]},
              {"tag_name":"v0.0.9","draft":false,"prerelease":false,"assets":[]}
            ]
            """);
        var releases = Otium.Server.GitHubReleaseList.Parse(doc.RootElement);
        Assert.Equal(["v0.1.0-rc.9", "v0.0.9"], releases.Select(r => r.Tag));
        Assert.True(releases[0].Prerelease);
        Assert.Equal("https://x/a.nupkg", releases[0].Assets[0].Url);
        Assert.Equal(SetupSha256, releases[0].Assets[0].Sha256); // GitHub's digest, in lower case
        Assert.Null(releases[0].Assets[1].Sha256); // anything but a SHA-256 is left out
    }

    [Fact]
    public async Task Only_releases_signed_with_a_trusted_key_are_offered_to_installed_apps()
    {
        const string gh = "https://github.com/Malogna/otium/releases/download";
        using var stranger = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var rc14 = Signed("v0.1.0-rc.14", true, "releases.win-x64.json", """{"Assets":[{"Version":"0.1.0-rc.14"}]}""",
            new("Otium.Desktop-0.1.0-rc.14-win-x64-full.nupkg", $"{gh}/v0.1.0-rc.14/pkg"));
        // Published but not signed yet (or by someone who got into the GitHub account): skipped.
        var unsigned = new Otium.Server.PublishedRelease("v0.1.0-rc.16", true,
            [new("releases.win-x64.json", $"{gh}/v0.1.0-rc.16/releases.win-x64.json"), new("Otium.Desktop-0.1.0-rc.16-win-x64-full.nupkg", $"{gh}/v0.1.0-rc.16/pkg")]);
        _releaseList.Texts[$"{gh}/v0.1.0-rc.16/releases.win-x64.json"] = """{"Assets":[{"Version":"0.1.0-rc.16"}]}""";
        // Signed with a key Otium doesn't trust: skipped too.
        var forged = Signed("v0.1.0-rc.15", true, "releases.win-x64.json", """{"Assets":[{"Version":"0.1.0-rc.15"}]}""",
            new("Otium.Desktop-0.1.0-rc.15-win-x64-full.nupkg", $"{gh}/v0.1.0-rc.15/pkg"), key: stranger);
        _releaseList.Releases = [unsigned, forged, rc14];

        var (_, feed) = await Get("/updates/pre/releases.win-x64.json");
        Assert.Contains("0.1.0-rc.14", feed);
        var (status, signature) = await Get("/updates/pre/releases.win-x64.json.sig");
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.True(UpdateSignature.Verify("releases.win-x64.json", System.Text.Encoding.UTF8.GetBytes(feed), signature,
            [Convert.ToBase64String(_signingKey.ExportSubjectPublicKeyInfo())])); // the same release's signature, byte for byte

        // Packages only from signed releases.
        using var noRedirect = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using (var ok = await noRedirect.GetAsync("/updates/pre/Otium.Desktop-0.1.0-rc.14-win-x64-full.nupkg"))
            Assert.Equal($"{gh}/v0.1.0-rc.14/pkg", ok.Headers.Location?.ToString());
        foreach (var refused in new[] { "Otium.Desktop-0.1.0-rc.15-win-x64-full.nupkg", "Otium.Desktop-0.1.0-rc.16-win-x64-full.nupkg" })
        {
            using var response = await noRedirect.GetAsync($"/updates/pre/{refused}");
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        // With nothing signed at all, there's no feed.
        _releaseList.Releases = [unsigned];
        _time.Advance(Otium.Server.UpdateFeed.CacheFor);
        Assert.Equal(HttpStatusCode.NotFound, (await Get("/updates/pre/releases.win-x64.json")).Status);
    }

    /// <summary>A release with a feed signed by <paramref name="key"/> (the key the server trusts by default) and its other files.</summary>
    private Otium.Server.PublishedRelease Signed(string tag, bool prerelease, string feedFile, string feed, Otium.Server.ReleaseAsset? package = null, ECDsa? key = null)
    {
        var dl = $"https://github.com/Malogna/otium/releases/download/{tag}";
        _releaseList.Texts[$"{dl}/{feedFile}"] = feed;
        _releaseList.Texts[$"{dl}/{feedFile}.sig"] = UpdateSignature.Sign(key ?? _signingKey, feedFile, System.Text.Encoding.UTF8.GetBytes(feed));
        List<Otium.Server.ReleaseAsset> assets = [new(feedFile, $"{dl}/{feedFile}"), new($"{feedFile}.sig", $"{dl}/{feedFile}.sig")];
        if (package is not null)
            assets.Add(package);
        return new(tag, prerelease, assets);
    }

    private sealed class FakeReleaseList : Otium.Server.IReleaseList
    {
        public IReadOnlyList<Otium.Server.PublishedRelease> Releases { get; set; } = [];
        public Dictionary<string, string> Texts { get; } = [];

        public Task<IReadOnlyList<Otium.Server.PublishedRelease>> GetRecentAsync(CancellationToken ct) => Task.FromResult(Releases);

        public Task<byte[]> GetBytesAsync(string url, CancellationToken ct) => Task.FromResult(System.Text.Encoding.UTF8.GetBytes(Texts[url]));
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
