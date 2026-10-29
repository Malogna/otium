using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Otium.Core.Bandwidth;
using Otium.Core.Fees;
using Otium.Core.Pools;
using Otium.Core.Switching;

namespace Otium.Server;

/// <summary>Where the operator keeps the site's legal pages, which only appear once their files exist.</summary>
public sealed class SiteOptions
{
    public const string Section = "Site";

    /// <summary>
    /// Folder with <c>privacy.html</c> and <c>terms.html</c>: the body of each page (HTML, no &lt;html&gt; or &lt;head&gt;),
    /// shown at /privacy and /terms inside the site's layout. Pages whose file is missing return 404 and aren't linked.
    /// Put only reviewed legal text here (docs/privacy.md is a draft that must not be published as it is).
    /// </summary>
    public string PagesDirectory { get; set; } = "/data/site";

    /// <summary>GitHub's "latest release" API for the app, whose files the Download buttons link to directly.</summary>
    public string LatestReleaseApi { get; set; } = "https://api.github.com/repos/Malogna/otium/releases/latest";
}

/// <summary>One graphics card's earnings with one coin today, after every fee but before electricity.</summary>
/// <param name="UsdPerDay">Gross from the live GPU policy, less the miner's, the pool's and Otium's fees.</param>
public sealed record CardCoin(string Coin, double UsdPerDay, double Watts, string? Note);

/// <param name="Group">The card's series, for grouping the list ("RTX 40 series").</param>
public sealed record CardEarnings(string Id, string Name, string Detail, IReadOnlyList<CardCoin> Coins, string Group = "");

/// <summary>
/// The public website: the front page that ads and invite links lead to, the download page, what each graphics card
/// earns (SitePages.Mining.cs), and the operator's legal pages. The front page
/// tells the night-shift story (the computer works while you're away), with live per-card earnings from the server's
/// GPU policy and the same cost breakdown the app shows, generated from the server's fee terms so the two can't
/// disagree. Styles, script, logo and fonts are embedded (<see cref="SiteAssets"/>); nothing loads from other hosts.
/// </summary>
internal static partial class SitePages
{
    public static readonly string[] LegalPages = ["privacy", "terms"];

    /// <summary>Where visitors and users ask for help (the app's Settings links the same page).</summary>
    private const string HelpUrl = Core.AppIdentity.HelpUrl;

    /// <summary>
    /// The footer's contact link. The email_off comments keep Cloudflare's Email Obfuscation from swapping the address
    /// for a script, which the pages' Content-Security-Policy would block.
    /// </summary>
    private const string ContactLink =
        $"""<!--email_off--><a href="mailto:{Core.AppIdentity.ContactEmail}">{Core.AppIdentity.ContactEmail}</a><!--/email_off-->""";

    /// <summary>The electricity price the page opens with, in $/kWh (the slider changes it).</summary>
    public const double DefaultPricePerKwh = 0.15;

    /// <summary>Hours a day the estimate assumes the computer is left alone: nights and a working day.</summary>
    public const int DefaultHoursAway = 16;

    /// <summary>The card the estimate opens with.</summary>
    public const string TypicalCard = "rtx4090";

    /// <summary>
    /// The electricity price the page opens with for a visitor: the typical household price in their country (the same
    /// table the app starts from), from Cloudflare's CF-IPCountry header, or <see cref="DefaultPricePerKwh"/> without one.
    /// </summary>
    public static (double PricePerKwh, string? Country) PriceFor(string? countryCode)
    {
        var code = countryCode?.Trim().ToUpperInvariant();
        if (code is not { Length: 2 } || code is "XX" or "T1")
            return (DefaultPricePerKwh, null);
        string name;
        try
        {
            name = new RegionInfo(code).EnglishName;
        }
        catch (ArgumentException)
        {
            name = code;
        }
        return (Core.Power.ElectricityPrices.ForCountry(code), name);
    }

    /// <summary>A card's best coin at a price and hours away: per day and per 30-day month, after fees and electricity.</summary>
    public readonly record struct CardEstimate(string Card, string Coin, string? Note, double Day, double Month);

    public static CardEstimate? Estimate(CardEarnings card, double hoursAway, double pricePerKwh)
    {
        var best = card.Coins.Select(c => (c, Net: (c.UsdPerDay - c.Watts * 24 / 1000 * pricePerKwh) * hoursAway / 24)).MaxBy(x => x.Net);
        return best.c is null ? null : new CardEstimate(card.Name, best.c.Coin, best.c.Note, Math.Max(0, best.Net), Math.Max(0, best.Net) * 30);
    }

    /// <summary>What the headline figure assumes, in one sentence (the details are one click away).</summary>
    internal static string SummaryLine(CardEstimate e, int hoursAway, SharingFigure s) => e.Day <= 0
        ? $"An {e.Card} would mostly wait at this electricity price{(s.PerDay > 0 ? "; internet sharing still earns" : "")}."
        : $"An {e.Card} mining while you're away ({hoursAway} h a day){(s.PerDay > 0 ? ", plus internet sharing" : "")}.";

    private static string EstimateLine(CardEstimate e) => e.Day <= 0
        ? $"Mining: waits at this power price on an {e.Card}."
        : $"Mining: {Usd(e.Day)}/day on an {e.Card} ({e.Coin}).";

    /// <summary>
    /// The card's best coin over a 30-day month mined around the clock, before electricity (after fees): how mining
    /// calculators and comparison sites count, so visitors can compare like for like. Otium's own estimate counts only the
    /// hours the computer is left alone, less its electricity.
    /// </summary>
    internal static string AroundTheClockLine(CardEarnings card) =>
        card.Coins.Count == 0
            ? ""
            : $"Mining around the clock, before electricity: {UsdWhole(card.Coins.Max(c => c.UsdPerDay) * 30)} a month, the way most comparison sites count it.";

    private static string PayoutLine(double perDay)
    {
        if (perDay <= 0)
            return "";
        var days = (double)Core.Wallets.PayoutCoins.Usdt.Threshold / perDay;
        var when = days < 1 ? "within a day" : $"in about {Math.Ceiling(days):0} day{(Math.Ceiling(days) == 1 ? "" : "s")}";
        return $"First payout {when}.";
    }

    /// <summary>What internet sharing pays a visitor from this country per day: Otium users' real median when there is one.</summary>
    /// <param name="Short">Where the figure comes from, for the hero's line.</param>
    internal sealed record SharingFigure(string CountryName, double PerDay, bool IsReal, int Computers, double RatePerGb, bool ListedMarket, string Short);

    internal static SharingFigure SharingFor(string? visitorCountry, FeePolicy fees, RealEarnings.RealEarningsSummary? real)
    {
        var code = visitorCountry?.Trim().ToUpperInvariant();
        var market = ProxybaseMarkets.For(code is { Length: 2 } and not ("XX" or "T1") ? code : null);
        if (real?.SharingByCountry.FirstOrDefault(s => s.Country == code) is { } measured)
            return new SharingFigure(market.CountryName, measured.UsdPerMonth / 30, true, measured.Computers, market.SellerUsdPerGb, market.Listed,
                $"median of {measured.Computers} Otium computers in {market.CountryName}");
        var estimate = ProxybaseMarkets.EstimatedUserUsdPerDay(market, fees.FeePercent / 100);
        return new SharingFigure(market.CountryName, estimate, false, 0, market.SellerUsdPerGb, market.Listed, "estimate");
    }

    private static string SharingLine(SharingFigure s) =>
        $"Internet sharing: +{Usd(s.PerDay)}/day ({E(s.Short)}).";

    /// <summary>"Get paid for internet bandwidth you aren't using": the visitor's country, real figures when there are, else the estimate.</summary>
    private static string SharingSection(SharingFigure s, string fee)
    {
        var rate = Usd(s.RatePerGb);
        var explain = s.IsReal
            ? $"a month: the median of {s.Computers} Otium computers in {E(s.CountryName)} whose owners shared their numbers, after Otium's {fee}."
            : (s.ListedMarket
                  ? $"a month, estimated for {E(s.CountryName)}: Proxybase pays {rate} per GB there"
                  : $"a month, estimated: Proxybase pays {rate} per GB where it doesn't list a country on its own")
              + $", and says a home connection carries 1–5 GB a day on average. This assumes 1 GB, after Otium's {fee}. What you really earn depends on how much Proxybase's customers use your connection; the app shows your real numbers after the first hour.";
        return $"""
                <section id="sharing" aria-labelledby="sharing-title">
                  <div class="wrap power">
                    <div class="reveal">
                      <span class="label">Internet sharing</span>
                      <h2 id="sharing-title" class="mt-18">Get paid for internet bandwidth you aren't using.</h2>
                      <p class="muted mt-20">Optional, and off until you tick it. Proxybase's customers, mostly AI agents and businesses that collect public web pages, use some of the internet bandwidth you aren't using. It's paid to your wallet in your coin, like mining.</p>
                      <p class="fine mt-14">The same tick adds two more networks, with no fee from Otium: Mysterium carries traffic for its business customers and pays its MYST token straight to your own address, and AIOZ uses some spare disk space to store and deliver files, paying its AIOZ token. Untick any you don't want.</p>
                    </div>
                    <div class="panel reveal">
                      <p class="keep" data-count>{(s.IsReal ? "" : "≈ ")}{UsdWhole(s.PerDay * 30)}<small>{explain}</small></p>
                    </div>
                  </div>
                </section>
            """;
    }

    /// <summary>
    /// "Real numbers": what the pools have paid Otium users, and what real computers earned per graphics card (only from
    /// users who chose to share, and only cards with several computers). Nothing at all until enough users have earned
    /// enough (<see cref="RealEarnings.RealEarningsOptions.MinimumEarnedUsd"/>, <see cref="RealEarnings.RealEarningsOptions.MinimumUsers"/>).
    /// </summary>
    internal static string RealSection(RealEarnings.RealEarningsSummary? real)
    {
        if (real is null || real.IsEmpty || !real.ShowSection)
            return "";
        var stats = new List<string>();
        if (real.PaidUsd >= 1)
            stats.Add($"""<div class="real-stat"><span class="real-num">{UsdWhole(real.PaidUsd)}</span><span>paid by the pools straight to {real.PaidUsers.ToString("N0", CultureInfo.InvariantCulture)} Otium {(real.PaidUsers == 1 ? "user's wallet" : "users' wallets")} so far</span></div>""");
        if (real.Rewarded30dUsd >= 1)
            stats.Add($"""<div class="real-stat"><span class="real-num">{UsdWhole(real.Rewarded30dUsd)}</span><span>earned by Otium users in the last 30 days</span></div>""");
        var table = real.Cards.Count == 0 ? "" : $"""
                      <div class="panel real-cards">
                        <table>
                          <caption>Real computers running Otium, last {real.WindowDays} days</caption>
                          <thead><tr><th scope="col">Graphics card</th><th scope="col">Computers</th><th scope="col">Per day of earning</th><th scope="col">Hours a day</th><th scope="col">Per day</th></tr></thead>
                          <tbody>{string.Concat(real.Cards.Select(c => $"<tr><th scope=\"row\">{E(CardName(c.Model))}</th><td>{c.Computers}</td><td>{Usd(c.UsdPerEarningDay)}</td><td>{c.HoursPerDay.ToString("0.#", CultureInfo.InvariantCulture)} h</td><td>{Usd(c.UsdPerDay)}</td></tr>"))}</tbody>
                        </table>
                        <p class="footnote">Medians across computers whose owners chose to share their numbers. Measured hash rates at each hour's prices, after every fee, before electricity. "Per day" is what they actually earned on an average day, at the hours they were away.</p>
                      </div>
            """;
        var sharingTable = real.SharingByCountry.Count == 0 ? "" : $"""
                      <div class="panel real-cards">
                        <table>
                          <caption>Internet sharing on real computers running Otium, last {real.WindowDays} days</caption>
                          <thead><tr><th scope="col">Country</th><th scope="col">Computers</th><th scope="col">Per day of sharing</th><th scope="col">Per month</th></tr></thead>
                          <tbody>{string.Concat(real.SharingByCountry.Select(s => $"<tr><th scope=\"row\">{E(ProxybaseMarkets.For(s.Country).CountryName)}</th><td>{s.Computers}</td><td>{Usd(s.UsdPerSharingDay)}</td><td>{Usd(s.UsdPerMonth)}</td></tr>"))}</tbody>
                        </table>
                        <p class="footnote">Medians across computers whose owners chose to share their numbers, after Otium's fee. "Per month" is what they actually earned, at the hours they shared.</p>
                      </div>
            """;
        return $"""
                <section id="real" aria-labelledby="real-title">
                  <div class="wrap">
                    <div class="section-head reveal">
                      <div><span class="label">Real numbers</span><h2 id="real-title">Not a calculator. What Otium users actually earn.</h2></div>
                      <p>Payouts are what unMineable and MoneroOcean report as paid to the addresses of Otium users, counted only where all the mining on an address is Otium's.</p>
                    </div>
                    <div class="real reveal">
                      {(stats.Count == 0 ? "" : $"<div class=\"real-stats\">{string.Concat(stats)}</div>")}
                      {table}
                      {sharingTable}
                    </div>
                  </div>
                </section>
            """;
    }

    /// <summary>"rtx4070tisuper" → "RTX 4070 Ti Super", from the card list when it's there.</summary>
    internal static string CardName(string model)
    {
        if (Cards.FirstOrDefault(c => c.Id == model) is { Name: { } name })
            return name;
        var m = System.Text.RegularExpressions.Regex.Match(model, @"^(rtx|gtx|rx)(\d{3,4})(tisuper|ti|super|xtx|xt|gre)?(laptop)?$");
        if (!m.Success)
            return model;
        var suffix = m.Groups[3].Value switch { "tisuper" => " Ti Super", "ti" => " Ti", "super" => " Super", "" => "", var s => " " + s.ToUpperInvariant() };
        return $"{m.Groups[1].Value.ToUpperInvariant()} {m.Groups[2].Value}{suffix}{(m.Groups[4].Success ? " Laptop" : "")}";
    }

    private static string UsdWhole(double value) => "$" + Math.Round(value).ToString("0", CultureInfo.InvariantCulture);

    /// <summary>
    /// Typical tuned speeds and power from WhatToMine's card pages (18 October 2026): Pearl in TH/s and Quantus in MH/s,
    /// each with its watts; 0 where WhatToMine lists none. Pearl needs 8 GB of graphics memory in SRBMiner, so 6 GB cards
    /// mine only Quantus. AMD cards can't mine Pearl on unMineable and WhatToMine lists no Quantus speed for them; the
    /// RX 6700 XT takes the middle of Kryptex's and hashrate.no's figures, which differ by about 2×.
    /// </summary>
    private static readonly (string Id, string Name, string Group, int MemoryGb, double PearlTh, double PearlW, double QuantusMh, double QuantusW)[] Cards =
    [
        ("rtx5090", "RTX 5090", "RTX 50 series", 32, 370, 520, 0, 0),
        ("rtx5080", "RTX 5080", "RTX 50 series", 16, 195, 250, 760, 230),
        ("rtx5070ti", "RTX 5070 Ti", "RTX 50 series", 16, 165, 220, 615, 200),
        ("rtx5070", "RTX 5070", "RTX 50 series", 12, 110, 160, 430, 150),
        ("rtx5060ti", "RTX 5060 Ti", "RTX 50 series", 16, 90, 130, 300, 120),
        ("rtx5060", "RTX 5060", "RTX 50 series", 8, 66, 100, 230, 110),
        ("rtx4090", "RTX 4090", "RTX 40 series", 24, 310, 400, 880, 270),
        ("rtx4080super", "RTX 4080 Super", "RTX 40 series", 16, 175, 270, 715, 260),
        ("rtx4080", "RTX 4080", "RTX 40 series", 16, 168, 260, 690, 250),
        ("rtx4070tisuper", "RTX 4070 Ti Super", "RTX 40 series", 16, 144, 220, 590, 220),
        ("rtx4070ti", "RTX 4070 Ti", "RTX 40 series", 12, 124, 210, 540, 190),
        ("rtx4070super", "RTX 4070 Super", "RTX 40 series", 12, 122, 200, 450, 180),
        ("rtx4070", "RTX 4070", "RTX 40 series", 12, 97, 150, 410, 140),
        ("rtx4060ti", "RTX 4060 Ti", "RTX 40 series", 8, 74, 110, 300, 100),
        ("rtx4060", "RTX 4060", "RTX 40 series", 8, 52, 100, 210, 80),
        ("rtx3090ti", "RTX 3090 Ti", "RTX 30 series", 24, 135, 330, 0, 0),
        ("rtx3090", "RTX 3090", "RTX 30 series", 24, 130, 320, 460, 220),
        ("rtx3080ti", "RTX 3080 Ti", "RTX 30 series", 12, 119, 300, 450, 210),
        ("rtx3080", "RTX 3080", "RTX 30 series", 10, 100, 240, 380, 160),
        ("rtx3070ti", "RTX 3070 Ti", "RTX 30 series", 8, 76, 200, 260, 120),
        ("rtx3070", "RTX 3070", "RTX 30 series", 8, 68, 150, 255, 110),
        ("rtx3060ti", "RTX 3060 Ti", "RTX 30 series", 8, 59, 140, 190, 80),
        ("rtx3060", "RTX 3060", "RTX 30 series", 12, 41, 100, 155, 70),
        ("rtx3050", "RTX 3050", "RTX 30 series", 8, 22, 90, 95, 60),
        ("rtx2080ti", "RTX 2080 Ti", "RTX 20 series", 11, 90, 220, 340, 170),
        ("rtx2080super", "RTX 2080 Super", "RTX 20 series", 8, 70, 200, 230, 100),
        ("rtx2080", "RTX 2080", "RTX 20 series", 8, 65, 190, 230, 90),
        ("rtx2070super", "RTX 2070 Super", "RTX 20 series", 8, 60, 160, 0, 0),
        ("rtx2070", "RTX 2070", "RTX 20 series", 8, 51, 150, 180, 90),
        ("rtx2060super", "RTX 2060 Super", "RTX 20 series", 8, 38, 100, 170, 90),
        ("rtx2060", "RTX 2060", "RTX 20 series", 6, 37, 100, 140, 80),
        ("rx6700xt", "RX 6700 XT", "AMD Radeon", 12, 0, 0, 130, 90),
    ];

    /// <summary>SRBMiner's graphics memory floor for Pearl (GpuAlgorithms in the app).</summary>
    private const int PearlMinMemoryGb = 8;

    /// <summary>What each example card earns per coin today, from the live policy; empty when prices are unavailable.</summary>
    public static IReadOnlyList<CardEarnings> Earnings(GpuPolicy? policy, FeePolicy fees)
    {
        if (policy is null)
            return [];
        var pearl = Usable(policy, "pearlhash");
        var otium = 1 - fees.FeePercent / 100;
        var list = new List<CardEarnings>();
        foreach (var c in Cards)
        {
            var coins = new List<CardCoin>();
            if (pearl is { } p && c.PearlTh > 0 && c.MemoryGb >= PearlMinMemoryGb && p.Allows([$"NVIDIA GeForce {c.Name}"]))
                coins.Add(new("Pearl", c.PearlTh * 1e12 * p.UsdPerHashPerSecondDay * 0.98 * (1 - p.PoolFeePercent / 100) * otium, c.PearlW, "paid in your coin"));
            if (coins.Count > 0)
                list.Add(new(c.Id, c.Name, $"{(c.Id.StartsWith("rx", StringComparison.Ordinal) ? "AMD" : "NVIDIA")} · {c.MemoryGb} GB", coins, c.Group));
        }
        return list;

        static GpuAlgorithmPolicy? Usable(GpuPolicy policy, string algorithm) =>
            policy.For(algorithm) is { Tier: GpuTiers.Primary, UsdPerHashPerSecondDay: > 0 } a ? a : null;
    }

    /// <param name="baseUrl">The site's public address ("https://otium.lol"): search engines and link previews need full URLs.</param>
    /// <param name="path">This page's own path ("/", "/privacy", "/r/ABC123").</param>
    /// <param name="index">
    /// False keeps the page out of search results while links on it are still followed: invite pages, one per code, all
    /// saying the same thing. Indexed pages name their address without query strings as canonical.
    /// </param>
    /// <param name="head">Extra tags for the page's head (the front page's structured data).</param>
    /// <param name="image">The page's own 1200×630 link-preview picture (a /site/ address) and what it says, instead of Site/og.png.</param>
    public static string Layout(string title, string description, string body, IEnumerable<string> legalLinks, string? downloadUrl,
        string baseUrl, string path, bool index = true, string? head = null, (string Path, string Alt)? image = null)
    {
        var url = E(baseUrl.TrimEnd('/') + path);
        var imagePath = image?.Path ?? SiteAssets.Url("og.png");
        var imageUrl = E(baseUrl.TrimEnd('/') + imagePath);
        var imageType = imagePath.Split('?')[0].EndsWith(".jpg", StringComparison.Ordinal) ? "image/jpeg" : "image/png";
        var imageAlt = E(image?.Alt ?? PreviewImageAlt);
        return $$"""
        <!doctype html>
        <html lang="en">
        <head>
          <meta charset="utf-8">
          <meta name="viewport" content="width=device-width, initial-scale=1, viewport-fit=cover">
          <title>{{E(title)}}</title>
          <meta name="description" content="{{E(description)}}">
          {{(index ? $"""<link rel="canonical" href="{url}">""" : """<meta name="robots" content="noindex, follow">""")}}
          <meta name="theme-color" content="#08131a">
          <meta property="og:site_name" content="Otium">
          <meta property="og:title" content="{{E(title)}}">
          <meta property="og:description" content="{{E(description)}}">
          <meta property="og:type" content="website">
          <meta property="og:url" content="{{url}}">
          <meta property="og:locale" content="en_US">
          <meta property="og:image" content="{{imageUrl}}">
          <meta property="og:image:type" content="{{imageType}}">
          <meta property="og:image:width" content="1200">
          <meta property="og:image:height" content="630">
          <meta property="og:image:alt" content="{{imageAlt}}">
          <meta name="twitter:card" content="summary_large_image">
          <link rel="icon" href="{{SiteAssets.Url("otium.svg")}}" type="image/svg+xml">
          <link rel="apple-touch-icon" href="{{SiteAssets.Url("icon.png")}}">
          <link rel="preload" href="{{SiteAssets.Url("fonts/BricolageGrotesque.woff2")}}" as="font" type="font/woff2" crossorigin>
          <link rel="preload" href="{{SiteAssets.Url("fonts/HankenGrotesk.woff2")}}" as="font" type="font/woff2" crossorigin>
          <link rel="stylesheet" href="{{SiteAssets.Url("site.css")}}">
          {{head}}
        </head>
        <body>
          <canvas id="sky" aria-hidden="true"></canvas>
          <header class="nav">
            <div class="wrap">
              <a class="brand" href="/"><img src="{{SiteAssets.Url("otium.svg")}}" alt="" width="30" height="30">Otium</a>
              <nav class="nav-links" aria-label="Sections">
                <a href="/#night-shift">How it works</a>
                <a href="/#earnings">Earnings</a>
                <a href="/mining">Graphics cards</a>
                <a href="/#fees">Fees</a>
                <a href="/#questions">Questions</a>
              </nav>
              <a class="button small" href="{{E(downloadUrl ?? "#")}}">Download</a>
            </div>
          </header>
          <main>
        {{body}}
          </main>
          <footer>
            <div class="wrap">
              <span>Otium · <i>otium</i> is Latin for leisure</span>
              <nav aria-label="Site">{{string.Concat(legalLinks.Select(p => $"""<a href="/{p}">{Capitalize(p)}</a>"""))}}<a href="{{HelpUrl}}">Help</a><a href="/pearl">What is Pearl?</a><a href="/antivirus">Antivirus help</a><a href="/compare/nicehash">vs NiceHash</a><a href="/compare/honeygain">vs Honeygain</a><a href="{{Core.AppIdentity.DiscordUrl}}">Discord</a>{{ContactLink}}<a href="https://github.com/Malogna/otium">Source code</a><a href="https://github.com/Malogna/otium/releases">Releases</a></nav>
            </div>
          </footer>
          <script src="{{SiteAssets.Url("site.js")}}" defer></script>
        </body>
        </html>
        """;
    }

    /// <summary>What Site/og.png, the picture shown when a link to the site is shared, says.</summary>
    private const string PreviewImageAlt = "Otium: Your computer works the night shift. You get paid. Paid to your own wallet, stops when you're back, for Windows, macOS and Linux.";

    /// <summary>
    /// For search engines: everything public may be crawled except the app's API and feeds, and the installer redirects
    /// under /download/ (the download page itself is one to find). Invite pages stay crawlable so their noindex is seen.
    /// The operator's dashboard and /dev/ aren't listed: naming them would only point scanners at them, and neither can
    /// be indexed anyway (/admin is behind Cloudflare Access, /dev/ answers 404 in production).
    /// </summary>
    public static string Robots(string baseUrl) => $"""
        User-agent: *
        Disallow: /api/
        Disallow: /updates/
        Disallow: /download/

        Sitemap: {baseUrl.TrimEnd('/')}/sitemap.xml

        """.ReplaceLineEndings("\n");

    /// <summary>The pages that show today's coin prices, so they change every day: the front page and the mining pages.</summary>
    public static IEnumerable<string> PricedPaths => MiningPaths.Prepend("/");

    /// <summary>The pages worth finding: the priced pages, the download, Pearl, help and comparison pages, and whichever legal pages exist.</summary>
    public static IEnumerable<string> SitemapPaths(IEnumerable<string> legalLinks) =>
        PricedPaths.Concat(["/download", "/pearl", "/antivirus"]).Concat(ComparisonSlugs.Select(s => "/compare/" + s))
            .Concat(legalLinks.Select(p => "/" + p));

    /// <param name="pricesAt">When today's coin prices were read: the priced pages' last change, or null to leave it out.</param>
    public static string Sitemap(string baseUrl, IEnumerable<string> legalLinks, DateTimeOffset? pricesAt = null)
    {
        var root = baseUrl.TrimEnd('/');
        var priced = PricedPaths.ToHashSet();
        var lastmod = pricesAt?.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
        var urls = SitemapPaths(legalLinks)
            .Select(p => $"  <url><loc>{System.Security.SecurityElement.Escape(root + p)}</loc>{(lastmod is not null && priced.Contains(p) ? $"<lastmod>{lastmod}</lastmod>" : "")}</url>\n");
        return $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
            {string.Concat(urls)}</urlset>

            """.ReplaceLineEndings("\n");
    }

    /// <param name="fees">The server's fee terms, as apps receive them.</param>
    /// <param name="earnings">Live per-card earnings (<see cref="Earnings"/>); empty hides the numbers.</param>
    /// <param name="pricesAt">When the GPU prices were last refreshed.</param>
    /// <param name="legalLinks">The legal pages that exist ("privacy", "terms").</param>
    /// <param name="baseUrl">The site's public address, for search engines and link previews.</param>
    public static string Home(FeePolicy fees, string? downloadUrl, IEnumerable<string> legalLinks, string baseUrl,
        IReadOnlyList<CardEarnings>? earnings = null, DateTimeOffset? pricesAt = null, string? visitorCountry = null,
        RealEarnings.RealEarningsSummary? real = null)
    {
        earnings ??= [];
        var download = E(downloadUrl ?? "#");
        var fee = Percent(fees.FeePercent);
        var feeMinutes = (fees.CycleLength.TotalMinutes * fees.FeePercent / 100).ToString("0", CultureInfo.InvariantCulture);
        var feeFreeDays = fees.RefereeFeeHoliday.TotalDays.ToString("0", CultureInfo.InvariantCulture);
        var referrerShare = Percent(fees.ReferrerShareOfFee * 100);
        var referrerMonths = (fees.ReferrerRewardPeriod.TotalDays / 30).ToString("0", CultureInfo.InvariantCulture);
        var perFriend = fees.ReferrerFeeHolidayPerVerifiedFriend.TotalDays.ToString("0", CultureInfo.InvariantCulture);
        var maxFeeFree = fees.MaxFeeHoliday.TotalDays.ToString("0", CultureInfo.InvariantCulture);
        var bothFeeFree = perFriend == feeFreeDays
            ? $"you both get {feeFreeDays} days without Otium's fee"
            : $"your friend gets {feeFreeDays} days without Otium's fee, and you get {perFriend}";

        // Of every $100 the processor mines, with Otium's referral code on unMineable: what each party keeps.
        var poolPct = CostBreakdown.PoolPercent(PayoutPool.Unmineable, referralCode: true);
        var keep = 100 - fees.FeePercent - CostBreakdown.CpuMinerPercent - poolPct;
        var costs = string.Concat(CostBreakdown.For(fees, PayoutPool.Unmineable, PayoutPool.Unmineable, referralCode: true)
            .Select(line => $"<div><dt>{E(line.Who)}</dt><dd>{E(line.What)}</dd></div>"));

        var coins = string.Concat(new[]
        {
            ("USDT", "TRC-20"), ("BTC", "Bitcoin"), ("LTC", "Litecoin"), ("BNB", "BEP-20"), ("SOL", "Solana"),
            ("TRX", "TRON"), ("ETH", "Ethereum"), ("XMR", "Monero"),
        }.Select(c => $"""<span class="coin">{c.Item1} <small>{c.Item2}</small></span>"""));

        var sharing = SharingFor(visitorCountry, fees, real);
        var data = JsonSerializer.Serialize(new
        {
            cards = earnings.Select(c => new
            {
                id = c.Id,
                name = c.Name,
                coins = c.Coins.Select(x => new { coin = x.Coin, usdPerDay = Math.Round(x.UsdPerDay, 4), watts = x.Watts, note = x.Note }),
            }),
            payoutUsdt = (double)Core.Wallets.PayoutCoins.Usdt.Threshold,
            sharingPerDay = Math.Round(sharing.PerDay, 4),
            sharingSource = sharing.Short,
        }).Replace("</", "<\\/", StringComparison.Ordinal);
        var asOf = pricesAt is { } at ? at.UtcDateTime.ToString("d MMMM yyyy, HH:mm 'UTC'", CultureInfo.InvariantCulture) : null;
        var typical = earnings.FirstOrDefault(c => c.Id == TypicalCard) ?? earnings.FirstOrDefault();
        var (price, country) = PriceFor(visitorCountry);
        var priceText = price.ToString("0.00", CultureInfo.InvariantCulture);
        var estimate = typical is null ? null : Estimate(typical, DefaultHoursAway, price);
        var options = string.Concat(earnings.GroupBy(c => c.Group).Select(g =>
            $"""<optgroup label="{E(g.Key)}">{string.Concat(g.Select(c => $"""<option value="{c.Id}"{(c == typical ? " selected" : "")}>{E(c.Name)} · {E(c.Detail)}</option>"""))}</optgroup>"""));
        var estimator = typical is null || estimate is null
            ? """
                      <div class="est-head"><span class="label">What your PC could earn</span></div>
                      <p class="est-sub">Today's coin prices aren't available right now. The app always shows your own computer's estimate before you start.</p>
              """
            : $"""
                      <div class="est-head"><span class="label">What your PC could earn</span><span class="live"><i></i>Today's prices</span></div>
                      <div class="est-figure"><span class="est-amount" id="est-month">{UsdWhole(estimate.Value.Month + sharing.PerDay * 30)}</span><span class="est-per">a month</span></div>
                      <p class="est-summary" id="est-summary">{SummaryLine(estimate.Value, DefaultHoursAway, sharing)}</p>
                      <label class="card-pick"><span>Your graphics card</span><select id="card">{options}</select></label>
                      <a class="est-all" href="/mining">What every graphics card earns</a>
                      <details class="est-more">
                        <summary>See the breakdown and adjust</summary>
                        <div class="est-more-body">
                          <p class="est-sub" id="est-sub">{EstimateLine(estimate.Value)}<br><span id="est-share">{SharingLine(sharing)}</span></p>
                          <p class="est-payout" id="est-payout">{PayoutLine(estimate.Value.Day)}</p>
                          <p class="est-compare" id="est-compare">{AroundTheClockLine(typical!)}</p>
                          <div class="est-sliders">
                            <label for="hours">Away from the computer</label>
                            <input id="hours" type="range" min="2" max="24" step="1" value="{DefaultHoursAway}">
                            <output id="hours-out" for="hours">{DefaultHoursAway} h a day</output>
                            <label for="price">Electricity price{(country is null ? "" : $"<small>typical in {E(country)}</small>")}</label>
                            <input id="price" type="range" min="0" max="0.60" step="0.01" value="{priceText}">
                            <output id="price-out" for="price">${priceText} / kWh</output>
                          </div>
                          <p class="footnote">Estimate from today's coin prices{(asOf is null ? "" : $" ({asOf})")} and Proxybase's rates. The app measures your own PC before you start.</p>
                        </div>
                      </details>
                      <script type="application/json" id="otium-data">{data}</script>
              """;

        // An example day of hourly prices (southern Norway in autumn): Otium mines below break-even, waits above it.
        double[] day = [0.07, 0.065, 0.06, 0.06, 0.062, 0.07, 0.11, 0.19, 0.24, 0.2, 0.15, 0.13, 0.12, 0.12, 0.13, 0.15, 0.2, 0.27, 0.29, 0.23, 0.17, 0.12, 0.09, 0.075];
        const double breakEven = 0.16, top = 0.3;
        var priceBars = string.Concat(day.Select((p, i) =>
            $"""<span class="{(p > breakEven ? "off" : "")}" data-h="{(p / top * 100).ToString("0.#", CultureInfo.InvariantCulture)}" title="{i:00}:00 · ${p.ToString("0.000", CultureInfo.InvariantCulture)}/kWh · {(p > breakEven ? "waits" : "mines")}"></span>"""));

        static string Grid(string cls) => $"""<div class="grid {cls}">{string.Concat(Enumerable.Range(0, 16).Select(i => $"""<span></span>"""))}</div>""";

        string Seg(string cls, double pct) => $"""<span class="{cls}" data-w="{pct.ToString("0.##", CultureInfo.InvariantCulture)}"></span>""";

        var body = $"""
                <div class="glow-frame" aria-hidden="true"><div class="glow"></div></div>
                <section class="hero" aria-labelledby="hero-title">
                  <div class="wrap">
                    <div class="hero-copy">
                      <h1 id="hero-title">Your computer works the night shift. <span class="shift">You get paid.</span></h1>
                      <p class="lede">Otium earns with your PC while you're away, and pays your own wallet directly.</p>
                      <div class="actions">
                        <a class="button" href="{download}"><svg viewBox="0 0 24 24" aria-hidden="true"><path fill="currentColor" d="M11 3h2v10.2l3.6-3.6 1.4 1.4-6 6-6-6 1.4-1.4 3.6 3.6z"/><path fill="currentColor" d="M5 19h14v2H5z"/></svg><span data-download-label>Download Otium</span></a>
                        <a class="button ghost" href="#night-shift">How it works</a>
                        <a class="button ghost discord" href="{Core.AppIdentity.DiscordUrl}" title="Join our Discord" aria-label="Join our Discord"><svg viewBox="0 0 24 24" aria-hidden="true"><path fill="currentColor" d="M20.317 4.37a19.79 19.79 0 0 0-4.885-1.515.074.074 0 0 0-.079.037c-.21.375-.444.865-.608 1.25a18.27 18.27 0 0 0-5.487 0 12.64 12.64 0 0 0-.617-1.25.077.077 0 0 0-.079-.037A19.74 19.74 0 0 0 3.677 4.37a.07.07 0 0 0-.032.027C.533 9.046-.32 13.58.099 18.057a.082.082 0 0 0 .031.057 19.9 19.9 0 0 0 5.993 3.03.078.078 0 0 0 .084-.028c.462-.63.874-1.295 1.226-1.994a.076.076 0 0 0-.041-.106 13.1 13.1 0 0 1-1.872-.892.077.077 0 0 1-.008-.128c.126-.094.252-.192.372-.291a.074.074 0 0 1 .078-.01c3.928 1.793 8.18 1.793 12.062 0a.074.074 0 0 1 .078.009c.12.099.246.198.373.292a.077.077 0 0 1-.006.127 12.3 12.3 0 0 1-1.873.892.077.077 0 0 0-.041.107c.36.698.772 1.362 1.225 1.993a.076.076 0 0 0 .084.028 19.84 19.84 0 0 0 6.002-3.03.077.077 0 0 0 .032-.054c.5-5.177-.838-9.674-3.549-13.66a.061.061 0 0 0-.031-.03zM8.02 15.33c-1.183 0-2.157-1.085-2.157-2.419 0-1.333.956-2.419 2.157-2.419 1.21 0 2.176 1.096 2.157 2.42 0 1.333-.956 2.418-2.157 2.418zm7.975 0c-1.183 0-2.157-1.085-2.157-2.419 0-1.333.955-2.419 2.157-2.419 1.21 0 2.176 1.096 2.157 2.42 0 1.333-.946 2.418-2.157 2.418z"/></svg>Discord</a>
                      </div>
                      <ul class="facts">
                        <li>Stops when you're back</li>
                        <li>Never holds your money</li>
                        <li>Windows, macOS, Linux</li>
                        <li><a href="https://github.com/Malogna/otium">Open source</a></li>
                      </ul>
                    </div>
                    <div class="panel estimator" id="earnings" aria-label="Earnings estimate">
            {estimator}
                    </div>
                  </div>
                </section>

                <div class="ribbon">
                  <div class="wrap">
                    <span class="label">Paid to your own wallet in</span>
                    <div class="marquee"><div class="marquee-track">{coins}<span aria-hidden="true" class="contents">{coins}</span></div></div>
                  </div>
                </div>
            {PopularCardsStrip(earnings)}
            {RealSection(real)}

                <section id="night-shift" aria-labelledby="shift-title">
                  <div class="wrap">
                    <div class="section-head reveal">
                      <div><span class="label">How it works</span><h2 id="shift-title">Set it up once. It works while you rest.</h2></div>
                      <p>Otium tests your hardware, picks the coin that pays your card the most, and switches as prices change. You don't touch it again.</p>
                    </div>
                    <div class="night reveal">
                      <div class="instrument" aria-label="An example day: the computer mines from 23:00 to 08:30 while you're away">
                        <div class="dial-box">
                          <svg id="dial" class="dial" viewBox="-30 -30 460 460" aria-hidden="true"></svg>
                          <div class="dial-center">
                            <div class="clock mono" id="dial-clock">03:00</div>
                            <div class="amount" id="dial-amount">1.1043</div>
                            <div class="unit">USDT earned tonight</div>
                            <div class="state" id="dial-state">Away: mining Pearl at 101.4 TH/s</div>
                          </div>
                        </div>
                        <ul class="legend">
                          <li class="mining"><i></i>Away: mining</li>
                          <li class="using"><i></i>You're using it</li>
                          <li class="paused"><i></i>Power too expensive</li>
                          <li class="fee"><i></i>Otium's {feeMinutes} min</li>
                        </ul>
                      </div>
                      <div class="terminal" aria-label="What the miners report during the example night">
                        <div class="terminal-bar"><i></i><i></i><i></i><span>otium · miner log · RTX 3080</span></div>
                        <div class="log" id="log"></div>
                      </div>
                    </div>
                    <div class="shift-line reveal">
                      <div class="moment"><time>18:00</time><h3>Choose where to be paid</h3><p>Pick a coin and paste your wallet address. The default is USDT on TRON, which is worth a dollar and pays out from <span class="mono">1.5 USDT</span>.</p></div>
                      <div class="moment"><time>23:00</time><h3>Walk away</h3><p>A few minutes after you leave the keyboard, your processor and graphics card start mining. Touch the mouse and they stop within a second.</p></div>
                      <div class="moment"><time>09:00</time><h3>The pool pays you</h3><p>unMineable sends your earnings straight to your address once a day after the minimum. Otium never holds your money and can't touch it. <a href="#payouts">How payouts work</a></p></div>
                    </div>
                  </div>
                </section>

                <section id="useful-work" aria-labelledby="ai-title">
                  <div class="wrap power">
                    <div class="reveal">
                      <span class="label">Proof of useful work</span>
                      <h2 id="ai-title" class="mt-18">Pearl mining runs the math AI runs on.</h2>
                      <p class="muted mt-20">Most coins burn power on hashes nobody needs. Pearl is a proof-of-useful-work coin: mining a block means multiplying large matrices, the same operation every AI model is built from, and its protocol is designed so that work can be real AI computation, such as serving a language model. That's why NVIDIA cards with tensor cores mine it best, and why it pays more than any other coin a gaming card can mine today.</p>
                      <p class="fine mt-14">Through a pool, your card multiplies the matrices the network hands it. It doesn't run anyone's AI model or see their data. <a href="/pearl">What is Pearl?</a></p>
                    </div>
                    <div class="panel matmul reveal" aria-hidden="true">
                      <div class="mm">{Grid("a")}<span class="op">×</span>{Grid("b")}<span class="op">=</span>{Grid("c")}</div>
                      <div class="threshold"><span>noised matrix product · a winning tile is a share</span><span>INT8 today, FP8 next</span></div>
                    </div>
                  </div>
                </section>

                <section aria-labelledby="power-title">
                  <div class="wrap power">
                    <div class="reveal">
                      <span class="label">Watches the power price</span>
                      <h2 id="power-title" class="mt-18">It watches the price of power by the hour.</h2>
                      <p class="muted mt-20">When electricity would cost more than mining earns, Otium waits. In Norway, Sweden, Denmark and Finland it follows the live hourly price for your area, so it mines through the cheap night hours and sits out the evening peak.</p>
                      <p class="fine mt-14">Electricity included in your rent, or paid by someone else? Say so when you set it up, and Otium earns whenever you're away.</p>
                    </div>
                    <div class="panel prices reveal">
                      <div class="threshold"><span>Example day, southern Norway</span><span>break-even ${breakEven.ToString("0.00", CultureInfo.InvariantCulture)}/kWh</span></div>
                      <div class="price-bars" id="price-bars">{priceBars}</div>
                      <div class="price-axis"><span>00:00</span><span>08:00</span><span>16:00</span><span>23:00</span></div>
                      <ul class="split-legend"><li><i class="sw-emerald"></i>Mines</li><li><i class="sw-waits"></i>Waits</li></ul>
                    </div>
                  </div>
                </section>

                {SharingSection(sharing, fee)}
                <section id="fees" aria-labelledby="fees-title">
                  <div class="wrap fees">
                    <div class="reveal">
                      <span class="label">Every fee, in full</span>
                      <p class="keep mt-18" data-count>${keep.ToString("0.##", CultureInfo.InvariantCulture)}<small>of every $100 your processor mines stays yours. The rest goes to the mining software ({Percent(CostBreakdown.CpuMinerPercent)}), the pool ({Percent(poolPct)}) and Otium ({fee} of mining time).</small></p>
                      <div class="split" role="img" aria-label="You keep {Percent(keep)}">{Seg("you", keep)}{Seg("otium", fees.FeePercent)}{Seg("miner", CostBreakdown.CpuMinerPercent)}{Seg("pool", poolPct)}</div>
                      <ul class="split-legend">
                        <li><i class="sw-emerald"></i>You</li><li><i class="sw-ink"></i>Otium</li><li><i class="sw-mist"></i>Miner</li><li><i class="sw-amber"></i>Pool</li>
                      </ul>
                      <p class="muted fee-why">Otium's {fee} is how it pays its way, with no ads and no data sold: the servers, payouts to your own wallet, and development and support. <a href="#why-fee">What that covers</a></p>
                    </div>
                    <div class="panel reveal">
                      <h3 id="fees-title" class="mb-18">What comes off, and who gets it</h3>
                      <dl class="costs">{costs}</dl>
                    </div>
                  </div>
                </section>

                <section id="payouts" aria-labelledby="payouts-title">
                  <div class="wrap">
                    <div class="section-head reveal">
                      <div><span class="label">Getting paid</span><h2 id="payouts-title">The pool pays your wallet. Otium never holds your money.</h2></div>
                      <p>Mining and internet sharing each pay your own address, in the coin you chose. This is the path the money takes.</p>
                    </div>
                    <div class="payouts">
                      <div class="panel reveal">
                        <h3>Mining</h3>
                        <ol class="pay-steps">
                          <li><strong>Your computer mines on unMineable, under your address.</strong> Your processor mines Monero and your graphics card the coin that pays it most.</li>
                          <li><strong>unMineable converts it into your coin.</strong> A few hours after the mining, it adds the value to your balance at the pool, in USDT, BTC or whichever coin you picked.</li>
                          <li><strong>Once a day it pays your wallet,</strong> when your balance has passed the pool's minimum (<span class="mono">1.5 USDT</span> for USDT on TRON). Otium shows how close you are.</li>
                        </ol>
                        <p class="fine mt-14">Check that "Auto pay" is on for your address at unMineable: new TRON addresses start with it off, and Otium warns you if it is. A balance with no mining for 6 months is lost at unMineable, so collect it if you stop. In the US, MoneroOcean pays Monero instead, from <span class="mono">0.003 XMR</span>.</p>
                      </div>
                      <div class="panel reveal">
                        <h3>Internet sharing</h3>
                        <ol class="pay-steps">
                          <li><strong>Proxybase pays for the bandwidth its buyers use,</strong> in dollars, into a small wallet Otium keeps on your computer.</li>
                          <li><strong>From $1, Otium has it converted and sent to your address</strong> in your coin. It waits while network fees would take more than 5% of the payout, so USDT on TRON arrives in payouts of about $16, and pays anyway from $25.</li>
                          <li><strong>The conversion goes through Relay</strong> (and SideShift for Litecoin, ChangeNOW for Monero). One that fails comes back to the wallet on your computer.</li>
                        </ol>
                        <p class="fine mt-14">Otium's {fee} of sharing earnings comes off; the rest reaches your address. Payouts only ever go to the address you registered, never anywhere the app or anyone else asks.</p>
                        <p class="fine mt-14">Mysterium and AIOZ skip the conversion: Mysterium pays MYST on Polygon straight to your address from 5 MYST, and Otium sends AIOZ to your 0x address from 5 AIOZ. Otium takes nothing from either.</p>
                      </div>
                    </div>
                  </div>
                </section>

                <section aria-labelledby="invite-title">
                  <div class="wrap">
                    <div class="panel invite reveal">
                      <div>
                        <span class="label">Invite friends</span>
                        <h2 id="invite-title" class="mt-14">Bring a friend, and {bothFeeFree}.</h2>
                        <p>Then, for {referrerMonths} months, {referrerShare} of Otium's fee on their mining is mined straight to your address.</p>
                        <p class="muted fs-sm">Your fee-free days start once your friend has mined for a day (up to {maxFeeFree} days at a time). Friends on your own internet connection don't count.</p>
                      </div>
                      <div class="ticket" aria-hidden="true">
                        <span class="label">Invite</span>
                        <div class="big">{feeFreeDays} days fee-free</div>
                        <p>{(perFriend == feeFreeDays ? "for you both" : "for your friend")}, plus {referrerShare} of Otium's fee to you</p>
                      </div>
                    </div>
                  </div>
                </section>

                <section id="questions" aria-labelledby="faq-title">
                  <div class="wrap">
                    <div class="section-head reveal"><div><span class="label">Questions</span><h2 id="faq-title">Before you install</h2></div></div>
                    <div class="faq reveal">
                      {string.Concat(Faq.Select(q => $"<details{(q.Id is null ? "" : $" id=\"{q.Id}\"")}><summary>{q.Question}</summary><p>{q.AnswerHtml}</p></details>"))}
                    </div>
                  </div>
                </section>

                <section class="closing" aria-labelledby="closing-title">
                  <pre class="ascii" aria-hidden="true"></pre>
                  <div class="wrap">
                    <h2 id="closing-title">Your computer sleeps eight hours a night. <span>It could be working.</span></h2>
                    <a class="button" href="{download}"><span data-download-label>Download Otium</span></a>
                  </div>
                </section>
            """;
        const string description = "Otium earns crypto with your idle PC and, if you choose, unused internet bandwidth while you're away, paid to your own wallet. Windows, macOS, Linux.";
        return Layout("Otium: get paid for your PC's spare power and bandwidth", description, body, legalLinks, downloadUrl, baseUrl, "/",
            head: StructuredData(baseUrl, description));
    }

    /// <summary>"Questions" on the front page, also given to search engines as an FAQ. Answers are trusted HTML.</summary>
    private static readonly (string Question, string AnswerHtml, string? Id)[] Faq =
    [
        ("Is my money safe?", "Otium never touches it. The pool pays your wallet directly, and you can check your balance on the pool's own page at any time.", null),
        ("Why does Otium take 2%?", "It's how Otium pays its way, without ads or selling anyone's data. It pays for the servers behind the website, your account and invites, the update feed, and the price check that picks the best-paying coin for your graphics card every 10 minutes. It pays for setting up payouts to your own wallet: each miner set up for your coin and address, a check that the pool will actually pay you, and turning internet sharing's earnings into your coin. And it pays for development and support: keeping up as coins and pools change, signed updates, and help on Discord and by email. The fee is 2% of mining time, so you can see it happen, and it stops during fee-free days.", "why-fee"),
        ("When do I get paid?", "Mining: unMineable pays your address once a day after your balance passes its minimum (1.5 USDT for USDT on TRON, usually within a day or two of mining with a gaming graphics card). Internet sharing through Proxybase: from $1, once the network fees are a small part of the payout. Mysterium and AIOZ pay their own tokens from 5 MYST or 5 AIOZ. All of it goes straight to your own wallet. <a href=\"#payouts\">How payouts work</a>", null),
        ("Is Otium open source?", $"""Yes. The app and the server behind otium.lol are free software under the AGPL-3.0, and <a href="https://github.com/Malogna/otium">the code is on GitHub</a>, so anyone can check what Otium does on your computer. Every update is signed with a key that never leaves the developer's PC. Questions? <a href="{Core.AppIdentity.DiscordUrl}">Join our Discord</a>.""", null),
        ("Why does my antivirus warn about it?", """Antivirus tools flag every mining program, because criminals hide miners on other people's computers. Otium downloads the official open-source XMRig and SRBMiner releases, checks their fingerprints, and keeps them in one folder you can allow. It only mines when you've set it up and turned it on. <a href="/antivirus">How to allow it in your antivirus</a>""", null),
        ("Will it slow my computer down?", "By default it only runs while you're away, and stops within a second when you're back. If you let it run while you work, it uses the lowest priority and only as much of the processor as you allow. Games and work always come first.", null),
        ("I live in the United States", "unMineable doesn't serve US residents, so Otium pays you in Monero through MoneroOcean instead.", null),
        ("What about AMD graphics cards?", "Otium tests Pearl on yours too: the mining software now runs it on recent AMD cards (an RX 9070 XT makes about $2 a day before power). If the pool doesn't accept an AMD card's work, Otium moves it to the next coin that pays your wallet more than the power it uses. Either way it's paid to your one wallet, and your processor earns too.", null),
        ("Which download do I need?", """The Download button picks the installer for the computer you're on. To choose one yourself: <a href="/download?os=windows">Windows</a>, <a href="/download?os=macos">Mac with Apple silicon</a>, <a href="/download?os=macos-intel">Mac with Intel</a> or <a href="/download?os=linux">Linux (AppImage)</a>. The Mac and Linux versions are a beta in this first release: they work, but have been tested less than Windows.""", null),
        ("Something isn't working", $"""In Otium, open Advanced and press "Send a problem report". It sends what Otium knows about why it isn't earning, without your payout address or any keys, and gives you a reference. Then <a href="{HelpUrl}">ask for help</a> and mention it.""", null),
        ("Will my computer go to sleep?", "Not while it's earning: Otium keeps it awake so it can mine through the night, and the screen still turns off. When it stops earning (you're back, power is too expensive, or a laptop runs on battery) your usual sleep settings apply again. You can switch this off in Settings.", null),
    ];

    /// <summary>
    /// The front page's schema.org description for search engines: who makes Otium (with its GitHub and Discord), the
    /// app itself, and the questions.
    /// </summary>
    private static string StructuredData(string baseUrl, string description)
    {
        var root = baseUrl.TrimEnd('/');
        return JsonLd(
            new JsonObject
            {
                ["@type"] = "Organization",
                ["@id"] = $"{root}/#organization",
                ["name"] = "Otium",
                ["url"] = $"{root}/",
                ["logo"] = root + SiteAssets.Url("icon.png"),
                ["email"] = Core.AppIdentity.ContactEmail,
                ["sameAs"] = new JsonArray("https://github.com/Malogna/otium", Core.AppIdentity.DiscordUrl),
            },
            new JsonObject
            {
                ["@type"] = "WebSite",
                ["@id"] = $"{root}/#website",
                ["name"] = "Otium",
                ["url"] = $"{root}/",
                ["publisher"] = new JsonObject { ["@id"] = $"{root}/#organization" },
            },
            new JsonObject
            {
                ["@type"] = "SoftwareApplication",
                ["name"] = "Otium",
                ["description"] = description,
                ["url"] = $"{root}/",
                ["image"] = root + SiteAssets.Url("og.png"),
                ["applicationCategory"] = "UtilitiesApplication",
                ["operatingSystem"] = "Windows, macOS, Linux",
                ["downloadUrl"] = $"{root}/download",
                ["publisher"] = new JsonObject { ["@id"] = $"{root}/#organization" },
            },
            FaqPage(Faq.Select(q => (q.Question, q.AnswerHtml))));
    }

    private static readonly Dictionary<string, (string Title, string Description)> LegalMeta = new()
    {
        ["privacy"] = ("Otium privacy policy", "How Otium handles your data: what the app and website collect, why, who it goes to, and your choices."),
        ["terms"] = ("Otium terms of use", "The terms for using Otium, the app that earns with your computer's spare power while you're away."),
    };

    /// <summary>"Page not found", with the way back in. Kept out of search results.</summary>
    public static string NotFoundPage(IEnumerable<string> legalLinks, string baseUrl, string path) =>
        Layout("Page not found | Otium", "This page doesn't exist on otium.lol.", """
                <div class="glow-frame" aria-hidden="true"><div class="glow"></div></div>
                <section class="page stacked">
                  <div class="wrap">
                    <div class="panel">
                      <span class="label">404</span>
                      <h1 class="mt-14">This page doesn't exist</h1>
                      <p class="lead">The link may be old or mistyped. Here's where most people are headed:</p>
                      <ul class="card-links">
                        <li><a href="/">How Otium works</a></li>
                        <li><a href="/mining">What each graphics card earns</a></li>
                        <li><a href="/download">Download Otium</a></li>
                        <li><a href="/antivirus">Antivirus help</a></li>
                        <li><a href="/pearl">What is Pearl?</a></li>
                      </ul>
                    </div>
                  </div>
                </section>
            """, legalLinks, "/download", baseUrl, path, index: false);

    /// <summary>A legal page: the operator's reviewed HTML inside the site's layout.</summary>
    public static string Legal(string page, string bodyHtml, IEnumerable<string> legalLinks, string? downloadUrl, string baseUrl)
    {
        var (title, description) = LegalMeta.GetValueOrDefault(page, ($"Otium: {Capitalize(page)}", $"Otium's {page} page."));
        return Layout(title, description,
            $"""<section class="page"><div class="wrap"><div class="panel legal">{bodyHtml}</div></div></section>""", legalLinks, downloadUrl,
            baseUrl, "/" + page);
    }

    /// <summary>
    /// The download page: starts the installer for the visitor's system by itself (a meta refresh, no script needed),
    /// offers the other systems' installers of the same release, and says what to expect while installing, including
    /// the warning an unsigned installer gets.
    /// </summary>
    public static string Download(LatestRelease release, string? os, IEnumerable<string> legalLinks, string baseUrl)
    {
        var mine = release.Installers.FirstOrDefault(i => i.Os == os);
        var version = E(release.Version);
        var intro = mine is not null
            ? $"""
              <h1 class="mt-14">Your download is starting</h1>
              <p class="lead">Otium {version} for {E(mine.Label)}. If nothing happens, use the button.</p>
              <div class="actions"><a class="button" href="/download/{E(mine.Os)}">Download for {E(mine.Label)}</a></div>
              """
            : $"""
              <h1 class="mt-14">Download Otium</h1>
              <p class="lead">Otium {version} runs on Windows, macOS and Linux computers. On a phone? Open otium.lol on the computer you want to earn with.</p>
              """;
        var installing = Installing(release, mine?.Os);
        var others = string.Join("", release.Installers.Where(i => i != mine)
            .Select(i => $"""<a class="button ghost" href="/download/{E(i.Os)}">{E(i.Label)}</a>"""));
        var body = $"""
                <div class="glow-frame" aria-hidden="true"><div class="glow"></div></div>
                <section class="page">
                  <div class="wrap">
                    <div class="panel">
                      <span class="label">Download</span>
                      {intro}
                      <p class="muted mt-18">{(mine is null ? "Choose your system:" : "Another system?")}</p>
                      <div class="actions">{others}</div>
                      <p class="muted mt-18">The macOS and Linux versions are in beta. Otium runs the open-source XMRig and SRBMiner miners, which some antivirus tools flag. <a href="/">How Otium works</a></p>
                    </div>
                    {installing}
                  </div>
                </section>
            """;
        var head = mine is null ? null : $"""<meta http-equiv="refresh" content="1;url=/download/{E(mine.Os)}">""";
        return Layout($"Download Otium {release.Version}", $"Download Otium {release.Version} for Windows, macOS and Linux.", body, legalLinks,
            "/download", baseUrl, "/download", head: head);
    }

    /// <summary>
    /// "Installing": what each system says about an installer that isn't code-signed and how to get past it, then each
    /// file's SHA-256 from GitHub with the command that checks it. The visitor's own system is open. Otium isn't signed
    /// because certificates cost money every year and the free programs for open-source projects don't take miners.
    /// </summary>
    private static string Installing(LatestRelease release, string? os)
    {
        string Sums(IReadOnlyList<Installer> files, Func<string, string> command)
        {
            var known = files.Where(f => f.Sha256 is not null).ToList();
            return known.Count == 0 ? "" : $"""
                <p class="sum-intro">To check that a file is the one we published, compare its SHA-256 with ours: <span class="cmd">{E(command(known[0].FileName))}</span></p>
                {string.Concat(known.Select(f => $"""<p class="sum"><span>{E(f.FileName)}</span><code>{E(f.Sha256!)}</code></p>"""))}
                """;
        }
        string Part(string id, string title, bool open, string steps, IReadOnlyList<Installer> files, Func<string, string> command) =>
            $"""<details id="install-{id}"{(open ? " open" : "")}><summary>{title}</summary><div class="install-body"><ol>{steps}</ol>{Sums(files, command)}</div></details>""";

        var windows = release.Installers.Where(i => i.Os == "windows").ToList();
        var macs = release.Installers.Where(i => i.Os.StartsWith("macos", StringComparison.Ordinal)).ToList();
        var linux = release.Installers.Where(i => i.Os == "linux").ToList();
        var parts = new List<string>();
        if (windows.Count > 0)
            parts.Add(Part("windows", "Windows", os == "windows", $"""
                <li>If your browser says the file isn't commonly downloaded, keep it. In Edge: open the download's <strong>…</strong> menu, choose <strong>Keep</strong>, then <strong>Keep anyway</strong> (behind the arrow next to Delete).</li>
                <li>Open <span class="code">{E(windows[0].FileName)}</span> from your downloads. If Windows says "Windows protected your PC" (Otium isn't code-signed yet), choose <strong>More info</strong>, then <strong>Run anyway</strong>.</li>
                <li>Setup takes a few seconds and opens Otium. Choose Quick start, and add the wallet address you want to be paid to.</li>
                <li>If your antivirus stops the miners, Otium tells you and shows the one folder to allow. Don't turn your antivirus off.</li>
                <li>If Windows says <strong>Smart App Control</strong> blocked it, Otium can't run on that PC yet: Smart App Control only lets through signed apps and ones Microsoft already knows. We don't suggest turning it off for Otium.</li>
                """, windows, f => $"Get-FileHash .\\{f}"));
        if (macs.Count > 0)
        {
            var intel = macs.Count > 1 ? $""", <span class="code">{E(macs[1].FileName)}</span> for Intel""" : "";
            parts.Add(Part("macos", "macOS", os is "macos" or "macos-intel", $"""
                <li>Open the installer (<span class="code">{E(macs[0].FileName)}</span> for Apple silicon{intel}). Otium isn't signed by Apple yet, so macOS says it can't verify it: choose <strong>Done</strong>.</li>
                <li>Open System Settings, then Privacy &amp; Security. Under Security, choose <strong>Open Anyway</strong> next to the message about Otium, and confirm with your password.</li>
                <li>Follow the installer, then open Otium from Applications (if macOS asks again, do step 2 once more). Choose Quick start, and add the wallet address you want to be paid to.</li>
                """, macs, f => $"shasum -a 256 ~/Downloads/{f}"));
        }
        if (linux.Count > 0)
            parts.Add(Part("linux", "Linux", os == "linux", $"""
                <li>Make <span class="code">{E(linux[0].FileName)}</span> executable (right-click, Properties, or <span class="code">chmod +x</span>) and run it.</li>
                <li>If it doesn't start, install FUSE 2 (<span class="code">libfuse2</span>, or <span class="code">libfuse2t64</span> on Ubuntu 24.04), which AppImages need.</li>
                <li>Choose Quick start, and add the wallet address you want to be paid to.</li>
                """, linux, f => $"sha256sum {f}"));
        return parts.Count == 0 ? "" : $"""
            <div class="panel install" id="installing">
              <h2>Installing</h2>
              <p class="muted">Otium isn't code-signed yet: a certificate costs hundreds of dollars a year, and the free ones for open-source projects don't take apps that mine. So your computer warns you once when you install it. Here's what you'll see and what to do.</p>
              {string.Concat(parts)}
            </div>
            """;
    }

    /// <summary>The legal pages whose files exist in <paramref name="directory"/>, in footer order.</summary>
    public static IReadOnlyList<string> AvailableLegalPages(string directory) =>
        LegalPages.Where(p => File.Exists(Path.Combine(directory, $"{p}.html"))).ToList();

    private static string E(string text) => WebUtility.HtmlEncode(text);

    private static string Usd(double value) => "$" + value.ToString("0.00", CultureInfo.InvariantCulture);

    private static string Percent(double value) => value.ToString("0.##", CultureInfo.InvariantCulture) + "%";

    private static string Capitalize(string word) => word.Length == 0 ? word : char.ToUpperInvariant(word[0]) + word[1..];
}
