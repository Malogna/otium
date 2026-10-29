using System.Globalization;
using Otium.Core.Fees;

namespace Otium.Server;

/// <summary>
/// The pages for what people search for before they know Otium: "sell unused bandwidth" (/sell-bandwidth) and "sell idle
/// processing power" (/sell-computing-power). Both lead with what it really pays, say plainly when that's small, and name
/// the risks, which the vendor blogs and listicles that rank for these searches leave out.
/// </summary>
internal static partial class SitePages
{
    private static readonly (string Question, string AnswerHtml)[] BandwidthFaq =
    [
        ("How much can I make selling bandwidth?", """It depends on where you are and how much of your connection buyers actually use. Proxybase pays per GB, more in some countries than others, and says a home connection carries 1–5 GB a day on average. For many people that's a few dollars a month, sometimes less. Otium shows your real numbers after the first hour."""),
        ("Is selling bandwidth safe?", """Your connection carries other people's web requests, so websites see them coming from your internet address. Proxybase's buyers are mostly AI agents and businesses collecting public web pages, and Otium only shares while you're away. If that isn't for you, leave sharing off: it's off until you tick it, and you can untick it any time."""),
        ("Does my internet provider allow it?", """Some providers' terms forbid reselling your connection. Check yours before turning sharing on."""),
        ("Will it slow down my internet or games?", """Proxybase sharing runs only while you're away from the computer and stops when you're back, so it doesn't compete with your own browsing or games. Mysterium has a daily limit of its own, and AIOZ a storage limit (20 GB by default)."""),
        ("Can I sell bandwidth for crypto?", """Yes. Otium converts what Proxybase pays into the coin you choose (USDT, BTC, LTC, BNB, SOL, TRX, ETH or XMR) and sends it to your own wallet. Mysterium pays its MYST token and AIOZ its AIOZ token, also straight to you."""),
        ("Can I sell bandwidth and processing power at the same time?", """Yes, that's what Otium does: while you're away it mines with your graphics card and processor, and shares your connection if you've ticked it. <a href="/sell-computing-power">Selling processing power</a>"""),
    ];

    private static readonly (string Question, string AnswerHtml)[] ComputeFaq =
    [
        ("Can you sell your computer's processing power?", """Yes. The common ways today are mining a cryptocurrency, renting your graphics card out on a GPU marketplace, or joining a network that pays for compute. Otium mines, because it needs no account and pays your own wallet, and it can stop the moment you're back."""),
        ("How much can I earn from idle CPU and GPU time?", """A recent NVIDIA gaming card earns a few dollars a day before electricity at today's prices; older and smaller cards earn less. A processor on its own earns only cents a day. <a href="/mining">See what each graphics card earns today</a>."""),
        ("Is it worth it after electricity?", """It depends on your power price. Every card page shows the price at which a day's mining earns exactly what its power costs, and Otium waits whenever power would cost more than mining earns."""),
        ("Why does my antivirus flag it?", """Antivirus tools flag every crypto miner, because criminals hide the same miners on other people's computers. Otium uses the official open-source releases and keeps them in one folder you can allow. <a href="/antivirus">How to allow it</a>"""),
        ("Will it wear out my graphics card?", """Mining keeps the card busy and warm, like a long gaming session. Otium stops when you're back, and on Windows with an NVIDIA card its efficiency mode caps the card at 70% power while it mines."""),
        ("Do I need an account?", """No. You paste a wallet address for the coin you want, and the pool pays it directly. Otium never holds your money."""),
    ];

    /// <param name="sharing">Internet sharing's figure for the visitor's country (<see cref="SharingFor"/>).</param>
    public static string SellBandwidth(FeePolicy fees, SharingFigure sharing, IEnumerable<string> legalLinks, string baseUrl)
    {
        var fee = Percent(fees.FeePercent);
        var month = UsdWhole(sharing.PerDay * 30);
        var figure = sharing.IsReal
            ? $"Otium computers in {E(sharing.CountryName)} that shared their numbers made a median of <strong>{month} a month</strong> from Proxybase, after Otium's {fee}."
            : $"Proxybase pays {Usd(sharing.RatePerGb)} per GB {(sharing.ListedMarket ? $"in {E(sharing.CountryName)}" : "where it doesn't list a country on its own")}. At 1 GB a day, that's about <strong>{month} a month</strong> after Otium's {fee}. What you really earn depends on how much buyers use your connection.";
        var body = $"""
                <div class="glow-frame" aria-hidden="true"><div class="glow"></div></div>
                <section class="page stacked">
                  <div class="wrap">
                    <nav class="crumbs" aria-label="Breadcrumb"><a href="/">Otium</a><span aria-hidden="true">/</span><span aria-current="page">Sell your bandwidth</span></nav>
                    <div class="panel">
                      <span class="label">Sell unused bandwidth</span>
                      <h1 class="mt-14">Sell your unused internet bandwidth, paid to your own wallet</h1>
                      <p class="lead">Otium shares the internet connection you aren't using while you're away from the computer, and pays what it earns to your own crypto wallet. It's optional, off until you tick it, and runs next to idle mining in the same app.</p>
                      <p>{figure}</p>
                      <div class="actions"><a class="button" href="/download"><span data-download-label>Download Otium</span></a><a class="button ghost" href="/#sharing">How sharing fits in</a></div>
                    </div>
                    <div class="panel">
                      <h2>What selling bandwidth means</h2>
                      <p>Companies that need to reach websites from ordinary home connections pay for it, through networks like Proxybase. When you share, some of their web requests travel through your connection, so the websites they visit see your internet address.</p>
                      <p>Proxybase's buyers are mostly AI agents and businesses that collect public web pages. Otium runs Proxybase's open-source client in direct mode, for your own connection only, while you're away.</p>
                    </div>
                    <div class="panel">
                      <h2>Three networks, one tick</h2>
                      <dl class="card-facts">
                        <div><dt>Proxybase</dt><dd>Bandwidth<small>paid per GB, converted to your coin, Otium's {fee}</small></dd></div>
                        <div><dt>Mysterium</dt><dd>Bandwidth<small>paid in MYST to your address; Mysterium keeps 20%, Otium nothing</small></dd></div>
                        <div><dt>AIOZ</dt><dd>Spare disk space<small>stores and delivers files, paid in AIOZ, Otium's {fee}; demand is thin so far</small></dd></div>
                      </dl>
                      <p>Proxybase's earnings are converted to the coin you choose (USDT, BTC, LTC, BNB, SOL, TRX, ETH or XMR) and sent to your own address from $1, once network fees are a small part of the payout. Mysterium pays from 5 MYST, AIOZ from 5 AIOZ (Windows and Linux only). Untick any network you don't want.</p>
                    </div>
                    <div class="panel">
                      <h2>The honest part</h2>
                      <ul>
                        <li><strong>It's often small.</strong> A few dollars a month is common, and it can be less when buyers use your connection little.</li>
                        <li><strong>Other people's traffic uses your address.</strong> Websites see their requests coming from your connection. If that isn't for you, leave sharing off.</li>
                        <li><strong>Check your internet provider's terms.</strong> Some forbid reselling your connection.</li>
                        <li><strong>It counts towards data caps.</strong> Proxybase says a home connection carries 1–5 GB a day on average.</li>
                      </ul>
                      <p>Compared with apps that keep your earnings as points until you cash out, Otium pays your own wallet and needs no account. <a href="/compare/honeygain">Otium vs Honeygain</a></p>
                    </div>
                    <div class="panel">
                      <h2>Questions</h2>
                      <div class="faq">{string.Concat(BandwidthFaq.Select(q => $"<details><summary>{E(q.Question)}</summary><p>{q.AnswerHtml}</p></details>"))}</div>
                    </div>
                  </div>
                </section>
            """;
        var root = baseUrl.TrimEnd('/');
        return Layout("Sell your unused internet bandwidth for crypto | Otium",
            "Sell the internet bandwidth you aren't using while you're away, paid in crypto to your own wallet. What it really pays, the risks, and how Otium does it.",
            body, legalLinks, "/download", baseUrl, "/sell-bandwidth",
            head: JsonLd(Breadcrumbs(root, ("Otium", "/"), ("Sell your bandwidth", "/sell-bandwidth")), FaqPage(BandwidthFaq)));
    }

    public static string SellComputingPower(FeePolicy fees, IReadOnlyList<CardEarnings> earnings, IEnumerable<string> legalLinks, string baseUrl)
    {
        var fee = Percent(fees.FeePercent);
        var cards = MiningCardList.Select(c => (Card: c, PerDay: PearlPerDay(c, earnings))).Where(c => c.PerDay is not null)
            .OrderByDescending(c => c.PerDay).ToList();
        var picks = new[] { "rtx5090", "rtx4090", "rtx4070", "rtx3080", "rtx3060" }
            .Select(id => cards.FirstOrDefault(c => c.Card.Id == id)).Where(c => c.Card is not null).ToList();
        var table = picks.Count == 0
            ? """<p>Today's coin prices aren't available right now. <a href="/mining">The graphics card list</a> shows typical speeds and power.</p>"""
            : $"""
                      <div class="real-cards">
                        <table>
                          <caption>What popular graphics cards earn today, around the clock</caption>
                          <thead><tr><th scope="col">Graphics card</th><th scope="col">Per day</th><th scope="col">Per month</th><th scope="col">Breaks even at</th></tr></thead>
                          <tbody>{string.Concat(picks.Select(c => $"""<tr><th scope="row"><a href="/mining/{c.Card.Slug}">{E(c.Card.Name)}</a></th><td>{Usd(c.PerDay!.Value)}</td><td>{UsdWhole(c.PerDay.Value * 30)}</td><td>{Kwh(BreakEven(c.PerDay.Value, c.Card.Watts))}</td></tr>"""))}</tbody>
                        </table>
                        <p class="footnote">After every fee, before electricity. "Breaks even at" is the power price where a day's mining earns exactly what it uses. <a href="/mining">Every card</a></p>
                      </div>
              """;
        var body = $"""
                <div class="glow-frame" aria-hidden="true"><div class="glow"></div></div>
                <section class="page stacked">
                  <div class="wrap">
                    <nav class="crumbs" aria-label="Breadcrumb"><a href="/">Otium</a><span aria-hidden="true">/</span><span aria-current="page">Sell processing power</span></nav>
                    <div class="panel">
                      <span class="label">Sell idle processing power</span>
                      <h1 class="mt-14">Sell your PC's idle processing power</h1>
                      <p class="lead">Your graphics card and processor sit idle most of the day. Otium puts them to work while you're away, mining whichever coin pays your hardware the most, and the pool pays your own wallet directly. It stops within a second when you're back.</p>
                      <div class="actions"><a class="button" href="/download"><span data-download-label>Download Otium</span></a><a class="button ghost" href="/mining">What your card earns</a></div>
                    </div>
                    <div class="panel wide">
                      <h2>What idle processing power earns</h2>
                      <p>The graphics card does almost all the earning. A recent NVIDIA card mining Pearl earns a few dollars a day before electricity; a processor on its own earns cents.</p>
            {table}
                    </div>
                    <div class="panel">
                      <h2>Ways to sell computing power in 2026</h2>
                      <ul>
                        <li><strong>Mining</strong>, which Otium does: your card works for a mining pool, and the pool pays your wallet for each share of work. No account, no customer to wait for, and it can stop the moment you need your PC.</li>
                        <li><strong>Renting out your graphics card</strong> on a GPU marketplace, where customers pay by the hour for your card. It usually means leaving the PC to a customer for as long as they rent it.</li>
                        <li><strong>Volunteer computing</strong> such as BOINC, which donates your idle time to science and doesn't pay.</li>
                      </ul>
                      <p>Many of the services that once paid for idle CPU time have closed. Mining is what still works on an ordinary PC.</p>
                    </div>
                    <div class="panel">
                      <h2>How Otium does it</h2>
                      <ul>
                        <li>It tests your hardware first and shows what your own PC earns after fees and electricity.</li>
                        <li>It mines only while you're away by default, and waits when power would cost more than mining earns.</li>
                        <li>The pool pays your own wallet in the coin you choose. Otium never holds your money.</li>
                        <li>Otium's fee is {fee} of mining time. The mining software's and pool's fees are listed on the front page.</li>
                        <li>It can also <a href="/sell-bandwidth">sell your unused bandwidth</a>, in the same app.</li>
                      </ul>
                      <p class="muted">Antivirus tools flag every miner, so you'll allow one folder. <a href="/antivirus">Why, and how</a></p>
                    </div>
                    <div class="panel">
                      <h2>Questions</h2>
                      <div class="faq">{string.Concat(ComputeFaq.Select(q => $"<details><summary>{E(q.Question)}</summary><p>{q.AnswerHtml}</p></details>"))}</div>
                    </div>
                  </div>
                </section>
            """;
        var root = baseUrl.TrimEnd('/');
        return Layout("Sell your PC's idle processing power (CPU and GPU) | Otium",
            "Sell your computer's idle CPU and GPU time while you're away, paid in crypto to your own wallet. What each graphics card earns today, after electricity.",
            body, legalLinks, "/download", baseUrl, "/sell-computing-power",
            head: JsonLd(Breadcrumbs(root, ("Otium", "/"), ("Sell processing power", "/sell-computing-power")), FaqPage(ComputeFaq)));
    }
}
