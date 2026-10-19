using System.Globalization;
using System.Net;
using System.Text.Json;
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
/// The public website: the front page that ads and invite links lead to, and the operator's legal pages. The front page
/// tells the night-shift story (the computer works while you're away), with live per-card earnings from the server's
/// GPU policy and the same cost breakdown the app shows, generated from the server's fee terms so the two can't
/// disagree. Styles, script, logo and fonts are embedded (<see cref="SiteAssets"/>); nothing loads from other hosts.
/// </summary>
internal static class SitePages
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

    /// <summary>Cards offered as one-click chips above the full list.</summary>
    public static readonly string[] QuickCards = ["rtx4090", "rtx5090", "rtx4070", "rtx3080", "rtx3060"];

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

    private static string EstimateLine(CardEstimate e) => e.Day <= 0
        ? $"Mining: waits at this power price on an {e.Card}."
        : $"Mining: {Usd(e.Day)}/day on an {e.Card} ({e.Coin}).";

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

    /// <summary>"Your unused internet can earn too": the visitor's country, real figures when there are, else the estimate.</summary>
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
                      <h2 id="sharing-title" style="margin-top:18px">Your unused internet can earn too.</h2>
                      <p style="margin-top:20px" class="muted">Optional, and off until you tick it. Proxybase's customers, mostly AI agents and businesses that collect public web pages, use some of the internet connection you aren't using. It's paid to your wallet in your coin, like mining.</p>
                    </div>
                    <div class="panel reveal">
                      <p class="keep">{(s.IsReal ? "" : "≈ ")}{UsdWhole(s.PerDay * 30)}<small>{explain}</small></p>
                    </div>
                  </div>
                </section>
            """;
    }

    /// <summary>
    /// "Real numbers": what the pools have paid Otium users, and what real computers earned per graphics card (only from
    /// users who chose to share, and only cards with several computers). Nothing at all until there is something real.
    /// </summary>
    internal static string RealSection(RealEarnings.RealEarningsSummary? real)
    {
        if (real is null || real.IsEmpty)
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

    public static string Layout(string title, string description, string body, IEnumerable<string> legalLinks, string? downloadUrl, string? head = null) =>
        $$"""
        <!doctype html>
        <html lang="en">
        <head>
          <meta charset="utf-8">
          <meta name="viewport" content="width=device-width, initial-scale=1, viewport-fit=cover">
          <title>{{E(title)}}</title>
          <meta name="description" content="{{E(description)}}">
          <meta name="theme-color" content="#08131a">
          <meta property="og:title" content="{{E(title)}}">
          <meta property="og:description" content="{{E(description)}}">
          <meta property="og:type" content="website">
          <link rel="icon" href="{{SiteAssets.Url("otium.svg")}}" type="image/svg+xml">
          <link rel="preload" href="/site/fonts/BricolageGrotesque.woff2" as="font" type="font/woff2" crossorigin>
          <link rel="preload" href="/site/fonts/HankenGrotesk.woff2" as="font" type="font/woff2" crossorigin>
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
              <nav aria-label="Site">{{string.Concat(legalLinks.Select(p => $"""<a href="/{p}">{Capitalize(p)}</a>"""))}}<a href="{{HelpUrl}}">Help</a>{{ContactLink}}<a href="https://github.com/Malogna/otium">Source code</a><a href="https://github.com/Malogna/otium/releases">Releases</a></nav>
            </div>
          </footer>
          <script src="{{SiteAssets.Url("site.js")}}" defer></script>
        </body>
        </html>
        """;

    /// <param name="fees">The server's fee terms, as apps receive them.</param>
    /// <param name="earnings">Live per-card earnings (<see cref="Earnings"/>); empty hides the numbers.</param>
    /// <param name="pricesAt">When the GPU prices were last refreshed.</param>
    /// <param name="legalLinks">The legal pages that exist ("privacy", "terms").</param>
    public static string Home(FeePolicy fees, string? downloadUrl, IEnumerable<string> legalLinks,
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
        var chips = string.Concat(QuickCards.Select(id => earnings.FirstOrDefault(c => c.Id == id)).OfType<CardEarnings>().Select(c =>
            $"""<button type="button" class="chip" data-card="{c.Id}" aria-pressed="{(c == typical ? "true" : "false")}">{E(c.Name)}<small>{E(c.Detail)}</small></button>"""));
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
                      <p class="est-sub" id="est-sub">{EstimateLine(estimate.Value)}<br><span id="est-share">{SharingLine(sharing)}</span></p>
                      <p class="est-payout" id="est-payout">{PayoutLine(estimate.Value.Day)}</p>
                      <div class="chips" role="group" aria-label="Popular graphics cards">{chips}</div>
                      <label class="card-pick"><span>Your graphics card</span><select id="card">{options}</select></label>
                      <div class="est-sliders">
                        <label for="hours">Away from the computer</label>
                        <input id="hours" type="range" min="2" max="24" step="1" value="{DefaultHoursAway}">
                        <output id="hours-out" for="hours">{DefaultHoursAway} h a day</output>
                        <label for="price">Electricity price{(country is null ? "" : $"<small>typical in {E(country)}</small>")}</label>
                        <input id="price" type="range" min="0" max="0.60" step="0.01" value="{priceText}">
                        <output id="price-out" for="price">${priceText} / kWh</output>
                      </div>
                      <p class="footnote">Estimate from today's coin prices{(asOf is null ? "" : $" ({asOf})")} and Proxybase's rates. The app measures your own PC before you start.</p>
                      <script type="application/json" id="otium-data">{data}</script>
              """;

        // An example day of hourly prices (southern Norway in autumn): Otium mines below break-even, waits above it.
        double[] day = [0.07, 0.065, 0.06, 0.06, 0.062, 0.07, 0.11, 0.19, 0.24, 0.2, 0.15, 0.13, 0.12, 0.12, 0.13, 0.15, 0.2, 0.27, 0.29, 0.23, 0.17, 0.12, 0.09, 0.075];
        const double breakEven = 0.16, top = 0.3;
        var priceBars = string.Concat(day.Select((p, i) =>
            $"""<span class="{(p > breakEven ? "off" : "")}" style="height:{(p / top * 100).ToString("0.#", CultureInfo.InvariantCulture)}%;--i:{i}" title="{i:00}:00 · ${p.ToString("0.000", CultureInfo.InvariantCulture)}/kWh · {(p > breakEven ? "waits" : "mines")}"></span>"""));

        static string Grid(string cls) => $"""<div class="grid {cls}">{string.Concat(Enumerable.Range(0, 16).Select(i => $"""<span style="--r:{i / 4};--c:{i % 4}"></span>"""))}</div>""";

        string Seg(string cls, double pct) => $"""<span class="{cls}" style="width:{pct.ToString("0.##", CultureInfo.InvariantCulture)}%"></span>""";

        var body = $"""
                <div class="glow" aria-hidden="true"></div>
                <section class="hero" aria-labelledby="hero-title">
                  <div class="wrap">
                    <div class="hero-copy">
                      <h1 id="hero-title">Your computer works the night shift. <span class="shift">You get paid.</span></h1>
                      <p class="lede">Otium earns with your PC while you're away, and pays your own wallet directly.</p>
                      <div class="actions">
                        <a class="button" href="{download}"><svg viewBox="0 0 24 24" aria-hidden="true"><path fill="currentColor" d="M11 3h2v10.2l3.6-3.6 1.4 1.4-6 6-6-6 1.4-1.4 3.6 3.6z"/><path fill="currentColor" d="M5 19h14v2H5z"/></svg><span data-download-label>Download Otium</span></a>
                        <a class="button ghost" href="#night-shift">How it works</a>
                      </div>
                      <ul class="facts">
                        <li>Stops when you're back</li>
                        <li>Never holds your money</li>
                        <li>Windows, macOS, Linux</li>
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
                    <div class="marquee"><div class="marquee-track">{coins}<span aria-hidden="true" style="display:contents">{coins}</span></div></div>
                  </div>
                </div>
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
                      <div class="moment"><time>09:00</time><h3>The pool pays you</h3><p>unMineable sends your earnings straight to your address once a day after the minimum. Otium never holds your money and can't touch it.</p></div>
                    </div>
                  </div>
                </section>

                <section id="useful-work" aria-labelledby="ai-title">
                  <div class="wrap power">
                    <div class="reveal">
                      <span class="label">Proof of useful work</span>
                      <h2 id="ai-title" style="margin-top:18px">Pearl mining runs the math AI runs on.</h2>
                      <p style="margin-top:20px" class="muted">Most coins burn power on hashes nobody needs. Pearl is a proof-of-useful-work coin: mining a block means multiplying large matrices, the same operation every AI model is built from, and its protocol lets that work be real AI computation, down to the mixture-of-experts layers of today's language models. That's why NVIDIA cards with tensor cores mine it best, and why it pays more than any other coin a gaming card can mine today.</p>
                      <p style="margin-top:14px" class="fine">Through a pool, your card multiplies the matrices the network hands it. It doesn't run anyone's AI model or see their data.</p>
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
                      <h2 id="power-title" style="margin-top:18px">It watches the price of power by the hour.</h2>
                      <p style="margin-top:20px" class="muted">When electricity would cost more than mining earns, Otium waits. In Norway, Sweden, Denmark and Finland it follows the live hourly price for your area, so it mines through the cheap night hours and sits out the evening peak.</p>
                    </div>
                    <div class="panel prices reveal">
                      <div class="threshold"><span>Example day, southern Norway</span><span>break-even ${breakEven.ToString("0.00", CultureInfo.InvariantCulture)}/kWh</span></div>
                      <div class="price-bars" id="price-bars">{priceBars}</div>
                      <div class="price-axis"><span>00:00</span><span>08:00</span><span>16:00</span><span>23:00</span></div>
                      <ul class="split-legend"><li><i style="background:var(--emerald)"></i>Mines</li><li><i style="background:repeating-linear-gradient(0deg,var(--amber) 0 3px,transparent 3px 6px)"></i>Waits</li></ul>
                    </div>
                  </div>
                </section>

                {SharingSection(sharing, fee)}
                <section id="fees" aria-labelledby="fees-title">
                  <div class="wrap fees">
                    <div class="reveal">
                      <span class="label">Every fee, in full</span>
                      <p class="keep" style="margin-top:18px">${keep.ToString("0.##", CultureInfo.InvariantCulture)}<small>of every $100 your processor mines stays yours. The rest goes to the mining software ({Percent(CostBreakdown.CpuMinerPercent)}), the pool ({Percent(poolPct)}) and Otium ({fee} of mining time).</small></p>
                      <div class="split" role="img" aria-label="You keep {Percent(keep)}">{Seg("you", keep)}{Seg("otium", fees.FeePercent)}{Seg("miner", CostBreakdown.CpuMinerPercent)}{Seg("pool", poolPct)}</div>
                      <ul class="split-legend">
                        <li><i style="background:var(--emerald)"></i>You</li><li><i style="background:var(--ink)"></i>Otium</li><li><i style="background:var(--mist)"></i>Miner</li><li><i style="background:var(--amber)"></i>Pool</li>
                      </ul>
                    </div>
                    <div class="panel reveal">
                      <h3 id="fees-title" style="margin-bottom:18px">What comes off, and who gets it</h3>
                      <dl class="costs">{costs}</dl>
                    </div>
                  </div>
                </section>

                <section aria-labelledby="invite-title">
                  <div class="wrap">
                    <div class="panel invite reveal">
                      <div>
                        <span class="label">Invite friends</span>
                        <h2 id="invite-title" style="margin-top:14px">Bring a friend, and part of Otium's fee on their mining goes to you.</h2>
                        <p>Friends who join with your invite code pay no Otium fee for their first {feeFreeDays} days. For {referrerMonths} months after that, {referrerShare} of Otium's fee on their mining is mined straight to your address, and each friend who starts mining gives you another {perFriend} fee-free days.</p>
                      </div>
                      <div class="ticket" aria-hidden="true">
                        <span class="label">Invite</span>
                        <div class="big">{feeFreeDays} days fee-free</div>
                        <p>for your friend, plus {referrerShare} of Otium's fee to you</p>
                      </div>
                    </div>
                  </div>
                </section>

                <section id="questions" aria-labelledby="faq-title">
                  <div class="wrap">
                    <div class="section-head reveal"><div><span class="label">Questions</span><h2 id="faq-title">Before you install</h2></div></div>
                    <div class="faq reveal">
                      <details><summary>Is my money safe?</summary><p>Otium never touches it. The pool pays your wallet directly, and you can check your balance on the pool's own page at any time.</p></details>
                      <details><summary>Why does my antivirus warn about it?</summary><p>Antivirus tools flag every mining program, because criminals hide miners on other people's computers. Otium downloads the official open-source XMRig and SRBMiner releases, checks their fingerprints, and keeps them in one folder you can allow. It only mines when you've set it up and turned it on.</p></details>
                      <details><summary>Will it slow my computer down?</summary><p>By default it only runs while you're away, and stops within a second when you're back. If you let it run while you work, it uses the lowest priority and only as much of the processor as you allow. Games and work always come first.</p></details>
                      <details><summary>I live in the United States</summary><p>unMineable doesn't serve US residents, so Otium pays you in Monero through MoneroOcean instead.</p></details>
                      <details><summary>What about AMD graphics cards?</summary><p>Otium tests Pearl on yours too: the mining software now runs it on recent AMD cards (an RX 9070 XT makes about $2 a day before power). If the pool doesn't accept an AMD card's work, Otium moves it to the next coin that pays your wallet more than the power it uses. Either way it's paid to your one wallet, and your processor earns too.</p></details>
                      <details><summary>Which download do I need?</summary><p>The Download button picks the installer for the computer you're on. To choose one yourself: <a href="/download?os=windows">Windows</a>, <a href="/download?os=macos">Mac with Apple silicon</a>, <a href="/download?os=macos-intel">Mac with Intel</a> or <a href="/download?os=linux">Linux (AppImage)</a>. The Mac and Linux versions are a beta in this first release: they work, but have been tested less than Windows.</p></details>
                      <details><summary>Something isn't working</summary><p>In Otium, open Advanced and press "Send a problem report". It sends what Otium knows about why it isn't earning, without your payout address or any keys, and gives you a reference. Then <a href="{HelpUrl}">ask for help</a> and mention it.</p></details>
                      <details><summary>Will my computer go to sleep?</summary><p>Not while it's earning: Otium keeps it awake so it can mine through the night, and the screen still turns off. When it stops earning (you're back, power is too expensive, or a laptop runs on battery) your usual sleep settings apply again. You can switch this off in Settings.</p></details>
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
        return Layout("Otium: get paid for your computer's spare power",
            "Otium mines with your idle processor and graphics card while you're away, and the pool pays your own wallet directly.",
            body, legalLinks, downloadUrl);
    }

    /// <summary>A legal page: the operator's reviewed HTML inside the site's layout.</summary>
    public static string Legal(string page, string bodyHtml, IEnumerable<string> legalLinks, string? downloadUrl) =>
        Layout($"Otium: {Capitalize(page)}", $"Otium's {page} page.",
            $"""<section class="page"><div class="wrap"><div class="panel legal">{bodyHtml}</div></div></section>""", legalLinks, downloadUrl);

    /// <summary>The legal pages whose files exist in <paramref name="directory"/>, in footer order.</summary>
    public static IReadOnlyList<string> AvailableLegalPages(string directory) =>
        LegalPages.Where(p => File.Exists(Path.Combine(directory, $"{p}.html"))).ToList();

    private static string E(string text) => WebUtility.HtmlEncode(text);

    private static string Usd(double value) => "$" + value.ToString("0.00", CultureInfo.InvariantCulture);

    private static string Percent(double value) => value.ToString("0.##", CultureInfo.InvariantCulture) + "%";

    private static string Capitalize(string word) => word.Length == 0 ? word : char.ToUpperInvariant(word[0]) + word[1..];
}
