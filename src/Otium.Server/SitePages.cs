using System.Globalization;
using System.Net;
using System.Text.Json;
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

public sealed record CardEarnings(string Id, string Name, string Detail, IReadOnlyList<CardCoin> Coins);

/// <summary>
/// The public website: the front page that ads and invite links lead to, and the operator's legal pages. The front page
/// tells the night-shift story (the computer works while you're away), with live per-card earnings from the server's
/// GPU policy and the same cost breakdown the app shows, generated from the server's fee terms so the two can't
/// disagree. Styles, script, logo and fonts are embedded (<see cref="SiteAssets"/>); nothing loads from other hosts.
/// </summary>
internal static class SitePages
{
    public static readonly string[] LegalPages = ["privacy", "terms"];

    /// <summary>The electricity price the page opens with, in $/kWh (the slider changes it).</summary>
    public const double DefaultPricePerKwh = 0.15;

    /// <summary>Hours a day the estimate assumes the computer is left alone (nights plus some of the day).</summary>
    public const int DefaultHoursAway = 12;

    /// <summary>The card the estimate opens with: a typical gaming PC's.</summary>
    public const string TypicalCard = "rtx3060";

    /// <summary>A card's best coin at a price and hours away: per day and per 30-day month, after fees and electricity.</summary>
    public readonly record struct CardEstimate(string Card, string Coin, string? Note, double Day, double Month);

    public static CardEstimate? Estimate(CardEarnings card, double hoursAway, double pricePerKwh)
    {
        var best = card.Coins.Select(c => (c, Net: (c.UsdPerDay - c.Watts * 24 / 1000 * pricePerKwh) * hoursAway / 24)).MaxBy(x => x.Net);
        return best.c is null ? null : new CardEstimate(card.Name, best.c.Coin, best.c.Note, Math.Max(0, best.Net), Math.Max(0, best.Net) * 30);
    }

    private static string EstimateLine(CardEstimate e) => e.Day <= 0
        ? $"At this electricity price an {e.Card} would cost more than it earns, so Otium would wait."
        : $"{Usd(e.Day)} a day with an {e.Card}, mining {e.Coin} ({e.Note}), after every fee and the power it uses.";

    private static string PayoutLine(double perDay)
    {
        if (perDay <= 0)
            return "";
        var days = (double)Core.Wallets.PayoutCoins.Usdt.Threshold / perDay;
        var when = days < 1 ? "within a day" : $"in about {Math.Ceiling(days):0} day{(Math.Ceiling(days) == 1 ? "" : "s")}";
        return $"First payout of 1.5 USDT to your wallet {when}, then daily.";
    }

    private static string UsdWhole(double value) => "$" + Math.Round(value).ToString("0", CultureInfo.InvariantCulture);

    /// <summary>
    /// Typical tuned speeds and power from WhatToMine's card pages (October 2026): Pearl in TH/s, Quantus in MH/s, watts.
    /// AMD cards can't mine Pearl on unMineable, and sources disagree by about 2× on AMD's Quantus speed, so it takes a
    /// middle figure.
    /// </summary>
    private static readonly (string Id, string Name, string Detail, double PearlTh, double PearlW, double QuantusMh, double QuantusW)[] Cards =
    [
        ("rtx3060", "RTX 3060", "NVIDIA · 12 GB", 41, 100, 155, 70),
        ("rtx3080", "RTX 3080", "NVIDIA · 10 GB", 100, 240, 380, 160),
        ("rtx4070", "RTX 4070", "NVIDIA · 12 GB", 97, 150, 410, 140),
        ("rtx4090", "RTX 4090", "NVIDIA · 24 GB", 310, 400, 880, 270),
        ("rx6700xt", "RX 6700 XT", "AMD · 12 GB", 0, 0, 130, 90),
    ];

    /// <summary>What each example card earns per coin today, from the live policy; empty when prices are unavailable.</summary>
    public static IReadOnlyList<CardEarnings> Earnings(GpuPolicy? policy, FeePolicy fees)
    {
        if (policy is null)
            return [];
        var pearl = Usable(policy, "pearlhash");
        var quantus = Usable(policy, "quantus");
        var otium = 1 - fees.FeePercent / 100;
        var list = new List<CardEarnings>();
        foreach (var c in Cards)
        {
            var coins = new List<CardCoin>();
            if (pearl is { } p && c.PearlTh > 0 && p.Allows([$"NVIDIA GeForce {c.Name}"]))
                coins.Add(new("Pearl", c.PearlTh * 1e12 * p.UsdPerHashPerSecondDay * 0.98 * (1 - p.PoolFeePercent / 100) * otium, c.PearlW, "paid in your coin"));
            if (quantus is { } q && c.QuantusMh > 0)
                coins.Add(new("Quantus", c.QuantusMh * 1e6 * q.UsdPerHashPerSecondDay * 0.975 * (1 - q.PoolFeePercent / 100) * otium, c.QuantusW, "with a Quantus wallet"));
            if (coins.Count > 0)
                list.Add(new(c.Id, c.Name, c.Detail, coins));
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
              <nav aria-label="Site">{{string.Concat(legalLinks.Select(p => $"""<a href="/{p}">{Capitalize(p)}</a>"""))}}<a href="https://github.com/Malogna/otium">Source code</a><a href="https://github.com/Malogna/otium/releases">Releases</a></nav>
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
        IReadOnlyList<CardEarnings>? earnings = null, DateTimeOffset? pricesAt = null)
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
            ("TRX", "TRON"), ("ETH", "Ethereum"), ("XMR", "Monero"), ("QTC", "graphics card"),
        }.Select(c => $"""<span class="coin">{c.Item1} <small>{c.Item2}</small></span>"""));

        var data = JsonSerializer.Serialize(new
        {
            cards = earnings.Select(c => new
            {
                id = c.Id,
                name = c.Name,
                coins = c.Coins.Select(x => new { coin = x.Coin, usdPerDay = Math.Round(x.UsdPerDay, 4), watts = x.Watts, note = x.Note }),
            }),
            payoutUsdt = (double)Core.Wallets.PayoutCoins.Usdt.Threshold,
        }).Replace("</", "<\\/", StringComparison.Ordinal);
        var asOf = pricesAt is { } at ? at.UtcDateTime.ToString("d MMMM yyyy, HH:mm 'UTC'", CultureInfo.InvariantCulture) : null;
        var typical = earnings.FirstOrDefault(c => c.Id == TypicalCard) ?? earnings.FirstOrDefault();
        var estimate = typical is null ? null : Estimate(typical, DefaultHoursAway, DefaultPricePerKwh);
        var chips = string.Concat(earnings.Select(c =>
            $"""<button type="button" class="chip" data-card="{c.Id}" aria-pressed="{(c == typical ? "true" : "false")}">{E(c.Name)}<small>{E(c.Detail)}</small></button>"""));
        var estimator = typical is null || estimate is null
            ? """
                      <div class="est-head"><span class="label">What your PC could earn</span></div>
                      <p class="est-sub">Today's coin prices aren't available right now. The app always shows your own computer's estimate before you start.</p>
              """
            : $"""
                      <div class="est-head"><span class="label">What your PC could earn</span><span class="live"><i></i>Today's prices</span></div>
                      <div class="est-figure"><span class="est-amount" id="est-month">{UsdWhole(estimate.Value.Month)}</span><span class="est-per">a month</span></div>
                      <p class="est-sub" id="est-sub">{EstimateLine(estimate.Value)}</p>
                      <p class="est-payout" id="est-payout">{PayoutLine(estimate.Value.Day)}</p>
                      <div class="chips" role="group" aria-label="Graphics card">{chips}</div>
                      <div class="est-sliders">
                        <label for="hours">Away from the computer</label>
                        <input id="hours" type="range" min="2" max="24" step="1" value="{DefaultHoursAway}">
                        <output id="hours-out" for="hours">{DefaultHoursAway} h a day</output>
                        <label for="price">Electricity price</label>
                        <input id="price" type="range" min="0" max="0.40" step="0.01" value="{DefaultPricePerKwh.ToString("0.00", CultureInfo.InvariantCulture)}">
                        <output id="price-out" for="price">${DefaultPricePerKwh.ToString("0.00", CultureInfo.InvariantCulture)} / kWh</output>
                      </div>
                      <p class="footnote">Today's coin prices from WhatToMine{(asOf is null ? "" : $" ({asOf})")} and typical card speeds. They change daily; the app measures your own computer before you start. The processor adds a few cents a day.</p>
                      <script type="application/json" id="otium-data">{data}</script>
              """;

        // An example day of hourly prices (southern Norway in autumn): Otium mines below break-even, waits above it.
        double[] day = [0.07, 0.065, 0.06, 0.06, 0.062, 0.07, 0.11, 0.19, 0.24, 0.2, 0.15, 0.13, 0.12, 0.12, 0.13, 0.15, 0.2, 0.27, 0.29, 0.23, 0.17, 0.12, 0.09, 0.075];
        const double breakEven = 0.16, top = 0.3;
        var priceBars = string.Concat(day.Select((p, i) =>
            $"""<span class="{(p > breakEven ? "off" : "")}" style="height:{(p / top * 100).ToString("0.#", CultureInfo.InvariantCulture)}%;--i:{i}" title="{i:00}:00 · ${p.ToString("0.000", CultureInfo.InvariantCulture)}/kWh · {(p > breakEven ? "waits" : "mines")}"></span>"""));

        string Seg(string cls, double pct) => $"""<span class="{cls}" style="width:{pct.ToString("0.##", CultureInfo.InvariantCulture)}%"></span>""";

        var body = $"""
                <div class="glow" aria-hidden="true"></div>
                <section class="hero" aria-labelledby="hero-title">
                  <div class="wrap">
                    <div class="hero-copy">
                      <h1 id="hero-title">Your computer works the night shift. <span class="shift">You get paid.</span></h1>
                      <p class="lede">Otium mines with your idle processor and graphics card while you're away, and the pool pays your own wallet directly. It stops the moment you're back, and waits when electricity costs more than it earns.</p>
                      <div class="actions">
                        <a class="button" href="{download}"><svg viewBox="0 0 24 24" aria-hidden="true"><path fill="currentColor" d="M11 3h2v10.2l3.6-3.6 1.4 1.4-6 6-6-6 1.4-1.4 3.6 3.6z"/><path fill="currentColor" d="M5 19h14v2H5z"/></svg><span data-download-label>Download Otium</span></a>
                        <a class="button ghost" href="#night-shift">How it works</a>
                      </div>
                      <ul class="facts">
                        <li>Pays your wallet, never holds your money</li>
                        <li>Open source</li>
                        <li>Windows, macOS and Linux</li>
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
                      <div class="moment"><time>23:00</time><h3>Walk away</h3><p>A few minutes after you leave the keyboard, your processor and graphics card start mining at the lowest priority. Touch the mouse and they stop.</p></div>
                      <div class="moment"><time>09:00</time><h3>The pool pays you</h3><p>unMineable sends your earnings straight to your address once a day after the minimum. Otium never holds your money and can't touch it.</p></div>
                    </div>
                  </div>
                </section>

                <section aria-labelledby="power-title">
                  <div class="wrap power">
                    <div class="reveal">
                      <span class="label">Never at a loss</span>
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
                      <details><summary>Will it slow my computer down?</summary><p>By default it only runs while you're away, at the lowest priority, and you choose how much of the processor it may use. Games and work always come first.</p></details>
                      <details><summary>I live in the United States</summary><p>unMineable doesn't serve US residents, so Otium pays you in Monero through MoneroOcean instead.</p></details>
                      <details><summary>What about AMD graphics cards?</summary><p>They can't mine Pearl, the best coin for NVIDIA cards. Add a free Quantus wallet in Settings and your card mines Quantus, paid hourly by Kryptex Pool.</p></details>
                      <details><summary>Which download do I need?</summary><p>The Download button picks the installer for the computer you're on. To choose one yourself: <a href="/download?os=windows">Windows</a>, <a href="/download?os=macos">Mac with Apple silicon</a>, <a href="/download?os=macos-intel">Mac with Intel</a> or <a href="/download?os=linux">Linux (AppImage)</a>.</p></details>
                      <details><summary>Can I also share my internet connection?</summary><p>On Windows, optionally. A Mysterium node shares a little of your unused bandwidth with businesses and pays you in MYST at your own Polygon address. It's off unless you turn it on.</p></details>
                    </div>
                  </div>
                </section>

                <section class="closing" aria-labelledby="closing-title">
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
