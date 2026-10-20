using System.Globalization;
using System.Text.Json.Nodes;
using Otium.Core.Fees;

namespace Otium.Server;

/// <summary>
/// "What each graphics card earns mining": a page per card that mines Pearl (/mining/rtx-4090) and one comparing them all
/// (/mining), from the same live earnings and typical speeds as the front page's estimate. These answer what people
/// search for before they'd ever hear of Otium ("rtx 3080 mining profitability"), so they lead with the numbers and keep
/// to what the data says: before electricity unless a price is named, after every fee.
/// </summary>
internal static partial class SitePages
{
    /// <summary>Electricity prices the card pages work their earnings out at, in $/kWh.</summary>
    private static readonly double[] TablePrices = [0.05, 0.10, 0.15, 0.20, 0.30, 0.40];

    /// <summary>A card with a mining page: NVIDIA cards with enough graphics memory for Pearl.</summary>
    private sealed record MiningCardInfo(string Slug, string Id, string Name, string Group, int MemoryGb, double PearlTh, double Watts);

    private static readonly MiningCardInfo[] MiningCardList = Cards
        .Where(c => c.PearlTh > 0 && c.MemoryGb >= PearlMinMemoryGb && c.Id.StartsWith("rtx", StringComparison.Ordinal))
        .Select(c => new MiningCardInfo(c.Name.ToLowerInvariant().Replace(' ', '-'), c.Id, c.Name, c.Group, c.MemoryGb, c.PearlTh, c.PearlW))
        .ToArray();

    /// <summary>Every mining page's path, for the sitemap.</summary>
    public static IEnumerable<string> MiningPaths => MiningCardList.Select(c => $"/mining/{c.Slug}").Prepend("/mining");

    /// <summary>The card's page address ("rtx-4090") for an address or card id ("rtx4090"), or null for no such card.</summary>
    public static string? MiningSlug(string requested) =>
        MiningCardList.FirstOrDefault(c => c.Slug == requested.ToLowerInvariant() || c.Id == requested.ToLowerInvariant())?.Slug;

    /// <summary>A card's Pearl earnings per day today (after fees, before electricity), or null when it isn't mined or prices are unknown.</summary>
    private static double? PearlPerDay(MiningCardInfo card, IReadOnlyList<CardEarnings> earnings) =>
        earnings.FirstOrDefault(e => e.Id == card.Id)?.Coins.FirstOrDefault(c => c.Coin == "Pearl")?.UsdPerDay;

    /// <summary>The electricity price at which a day's mining around the clock earns exactly its power.</summary>
    private static double BreakEven(double perDay, double watts) => perDay / (watts * 24 / 1000);

    private static string Kwh(double price) => "$" + price.ToString("0.00", CultureInfo.InvariantCulture) + "/kWh";

    private static string SignedUsd(double value) => value < 0 ? "−" + Usd(-value) : Usd(value);

    private static string SignedUsdWhole(double value) => Math.Round(value) < 0 ? "−" + UsdWhole(-value) : UsdWhole(value);

    private static string Th(double th) => th.ToString("0", CultureInfo.InvariantCulture) + " TH/s";

    /// <summary>"Speed and power": where the figures come from, the same for every mining page.</summary>
    private static string Method(string cardName, string fee) => $"""
        <p>Speed and power are typical tuned figures from WhatToMine for {cardName}. Earnings use today's Pearl price and
        network difficulty, which Otium's server checks every few minutes, less the fees of the mining software, the pool
        and Otium ({fee} of mining time). Per-day figures are before electricity unless a price is named. The app measures your own
        card's speed and power before you start, so your numbers may differ a little.</p>
        """;

    /// <summary>One card's mining page, for a slug from <see cref="MiningSlug"/>.</summary>
    public static string MiningCard(string slug, FeePolicy fees, IReadOnlyList<CardEarnings> earnings, DateTimeOffset? pricesAt,
        string? visitorCountry, IEnumerable<string> legalLinks, string baseUrl)
    {
        var card = MiningCardList.First(c => c.Slug == slug);
        var name = card.Name;
        var fee = Percent(fees.FeePercent);
        var perDay = PearlPerDay(card, earnings);
        var kwhPerDay = card.Watts * 24 / 1000;
        var (price, country) = PriceFor(visitorCountry);
        var asOf = pricesAt?.UtcDateTime.ToString("d MMMM yyyy, HH:mm 'UTC'", CultureInfo.InvariantCulture);

        string lead, facts = "", table = "";
        var questions = new List<(string Question, string AnswerHtml)>();
        if (perDay is { } day)
        {
            var breakEven = BreakEven(day, card.Watts);
            var local = day - kwhPerDay * price;
            var otiumMonth = Math.Max(0, local) * DefaultHoursAway / 24 * 30;
            lead = $"""About <strong>{Usd(day)} a day</strong> mining Pearl at today's prices, around the clock and before electricity: <strong>{UsdWhole(day * 30)} a month</strong>, after the mining software's, the pool's and Otium's fees.""";
            facts = $"""
                      <dl class="card-facts">
                        <div><dt>Speed</dt><dd>{Th(card.PearlTh)}</dd></div>
                        <div><dt>Power</dt><dd>{card.Watts:0} W</dd></div>
                        <div><dt>Breaks even at</dt><dd>{Kwh(breakEven)}</dd></div>
                        <div><dt>At {Kwh(price)}{(country is null ? "" : $" <small>typical in {E(country)}</small>")}</dt><dd>{(local > 0 ? $"{UsdWhole(otiumMonth)} a month" : "Otium waits")}<small>{(local > 0 ? $"mining while you're away, {DefaultHoursAway} h a day" : "power would cost more than it earns")}</small></dd></div>
                      </dl>
                """;
            var rows = string.Concat(TablePrices.Select(p =>
            {
                var net = day - kwhPerDay * p;
                var otium = net > 0 ? UsdWhole(net * DefaultHoursAway / 24 * 30) : "waits";
                return $"<tr><th scope=\"row\">{Kwh(p)}</th><td>{SignedUsd(net)}</td><td>{SignedUsdWhole(net * 30)}</td><td>{otium}</td></tr>";
            }));
            table = $"""
                    <div class="panel real-cards">
                      <table>
                        <caption>{E(name)} after electricity</caption>
                        <thead><tr><th scope="col">Electricity</th><th scope="col">Per day, 24 h</th><th scope="col">Per month, 24 h</th><th scope="col">With Otium, {DefaultHoursAway} h a day</th></tr></thead>
                        <tbody>{rows}</tbody>
                      </table>
                      <p class="footnote">Today's Pearl price{(asOf is null ? "" : $" ({asOf})")}, {card.Watts:0} W for every hour mined. Otium never mines at a loss: it waits whenever power would cost more than mining earns, and counts only the hours you're away.</p>
                    </div>
                """;
            questions.Add(($"Is mining with an {name} worth it?",
                $"At today's prices an {name} earns about {Usd(day)} a day mining Pearl, before electricity. It uses about {kwhPerDay.ToString("0.0", CultureInfo.InvariantCulture)} kWh over 24 hours, so it earns more than its power costs as long as electricity is under {Kwh(breakEven)}. Otium checks that for you and waits whenever power would cost more than mining earns."));
        }
        else
        {
            lead = earnings.Count == 0
                ? "Today's coin prices aren't available right now, so there's no figure for today. The typical speed and power are below, and the app always measures your own card before you start."
                : $"Otium isn't mining Pearl on the {E(name)} at the moment, so there's no figure for today.";
            facts = $"""
                      <dl class="card-facts">
                        <div><dt>Speed</dt><dd>{Th(card.PearlTh)}</dd></div>
                        <div><dt>Power</dt><dd>{card.Watts:0} W</dd></div>
                        <div><dt>Graphics memory</dt><dd>{card.MemoryGb} GB</dd></div>
                      </dl>
                """;
        }
        questions.Add(($"How much power does an {name} use mining?",
            $"About {card.Watts:0} W tuned, as WhatToMine lists it: {kwhPerDay.ToString("0.0", CultureInfo.InvariantCulture)} kWh over 24 hours. Otium measures your own card's speed and power before you start."));
        questions.Add(($"Can I still game on my {name}?",
            "Yes. By default Otium only mines while you're away, and stops within a second when you touch the mouse or keyboard, so games and work get the whole card."));

        var siblings = MiningCardList.Where(c => c.Group == card.Group && c != card).ToList();
        var others = siblings.Count == 0 ? "" : $"""
                    <div class="panel">
                      <h2>Other {E(card.Group)} cards</h2>
                      <ul class="card-links">{string.Concat(siblings.Select(c => $"""<li><a href="/mining/{c.Slug}">{E(c.Name)}</a>{(PearlPerDay(c, earnings) is { } d ? $" <span>{Usd(d)} a day</span>" : "")}</li>"""))}</ul>
                      <p><a href="/mining">Compare every card</a></p>
                    </div>
            """;

        var body = $"""
                <div class="glow-frame" aria-hidden="true"><div class="glow"></div></div>
                <section class="page mining">
                  <div class="wrap">
                    <nav class="crumbs" aria-label="Breadcrumb"><a href="/">Otium</a><span aria-hidden="true">/</span><a href="/mining">GPU mining</a><span aria-hidden="true">/</span><span aria-current="page">{E(name)}</span></nav>
                    <div class="panel">
                      <span class="label">{E(card.Group)} · NVIDIA · {card.MemoryGb} GB</span>
                      <h1 style="margin-top:14px">What an {E(name)} earns mining today</h1>
                      <p class="lead">{lead}</p>
            {facts}
                      <div class="actions"><a class="button" href="/download"><span data-download-label>Download Otium</span></a><a class="button ghost" href="/mining">Compare every card</a></div>
                    </div>
            {table}
                    <div class="panel">
                      <h2>Why Pearl</h2>
                      <p>Pearl is a proof-of-useful-work coin: mining it means multiplying large matrices, the operation AI models are built from, which is why NVIDIA cards with tensor cores mine it best. Otium mines it while you're away and has it paid to your own wallet in the coin you choose.</p>
                      {Method($"the {E(name)}", fee)}
                    </div>
                    <div class="panel">
                      <h2>Questions</h2>
                      <div class="faq">{string.Concat(questions.Select(q => $"<details><summary>{E(q.Question)}</summary><p>{E(q.AnswerHtml)}</p></details>"))}</div>
                    </div>
            {others}
                  </div>
                </section>
            """;

        var title = $"{name} mining profitability today | Otium";
        var description = perDay is { } d1
            ? $"An {name} earns about {Usd(d1)} a day mining Pearl at today's prices, before electricity ({Th(card.PearlTh)}, {card.Watts:0} W), and breaks even at {Kwh(BreakEven(d1, card.Watts))}. Live figures, after every fee."
            : $"What an {name} earns mining Pearl: typical speed ({Th(card.PearlTh)}) and power ({card.Watts:0} W), with live earnings after every fee.";
        var root = baseUrl.TrimEnd('/');
        var head = JsonLd(
            Breadcrumbs(root, ("Otium", "/"), ("GPU mining", "/mining"), (name, $"/mining/{card.Slug}")),
            FaqPage(questions));
        return Layout(title, description, body, legalLinks, "/download", baseUrl, $"/mining/{card.Slug}", head: head);
    }

    /// <summary>The comparison: every card's speed, power, earnings today and break-even price, best-paying first.</summary>
    public static string MiningIndex(FeePolicy fees, IReadOnlyList<CardEarnings> earnings, DateTimeOffset? pricesAt,
        IEnumerable<string> legalLinks, string baseUrl)
    {
        var fee = Percent(fees.FeePercent);
        var asOf = pricesAt?.UtcDateTime.ToString("d MMMM yyyy, HH:mm 'UTC'", CultureInfo.InvariantCulture);
        var cards = MiningCardList.Select(c => (Card: c, PerDay: PearlPerDay(c, earnings)))
            .OrderByDescending(c => c.PerDay ?? -1).ToList(); // a stable sort: without prices, the list's own order
        var rows = string.Concat(cards.Select(c => c.PerDay is { } day
            ? $"""<tr><th scope="row"><a href="/mining/{c.Card.Slug}">{E(c.Card.Name)}</a></th><td>{Th(c.Card.PearlTh)}</td><td>{c.Card.Watts:0} W</td><td>{Usd(day)}</td><td>{UsdWhole(day * 30)}</td><td>{Kwh(BreakEven(day, c.Card.Watts))}</td></tr>"""
            : $"""<tr><th scope="row"><a href="/mining/{c.Card.Slug}">{E(c.Card.Name)}</a></th><td>{Th(c.Card.PearlTh)}</td><td>{c.Card.Watts:0} W</td><td>–</td><td>–</td><td>–</td></tr>"""));
        var best = cards.FirstOrDefault(c => c.PerDay is not null);
        var lead = best.PerDay is { } top
            ? $"Today's earnings for {cards.Count} NVIDIA graphics cards mining Pearl, after every fee and before electricity. The {E(best.Card.Name)} leads at {Usd(top)} a day."
            : $"Typical speed and power for {cards.Count} NVIDIA graphics cards mining Pearl. Today's coin prices aren't available right now, so there are no earnings figures.";

        var body = $"""
                <div class="glow-frame" aria-hidden="true"><div class="glow"></div></div>
                <section class="page mining">
                  <div class="wrap">
                    <nav class="crumbs" aria-label="Breadcrumb"><a href="/">Otium</a><span aria-hidden="true">/</span><span aria-current="page">GPU mining</span></nav>
                    <div class="panel">
                      <span class="label">GPU mining</span>
                      <h1 style="margin-top:14px">What every graphics card earns mining today</h1>
                      <p class="lead">{lead}</p>
                      <div class="actions"><a class="button" href="/download"><span data-download-label>Download Otium</span></a><a class="button ghost" href="/#earnings">Estimate for your PC</a></div>
                    </div>
                    <div class="panel real-cards wide">
                      <table>
                        <caption>Pearl mining, around the clock</caption>
                        <thead><tr><th scope="col">Graphics card</th><th scope="col">Speed</th><th scope="col">Power</th><th scope="col">Per day</th><th scope="col">Per month</th><th scope="col">Breaks even at</th></tr></thead>
                        <tbody>{rows}</tbody>
                      </table>
                      <p class="footnote">{(asOf is null ? "" : $"Today's Pearl price ({asOf}). ")}"Breaks even at" is the electricity price where a day's mining earns exactly what its power costs: below it, the card earns more than it uses. Cards with 6 GB of graphics memory can't mine Pearl.</p>
                    </div>
                    <div class="panel">
                      <h2>How these numbers are worked out</h2>
                      {Method("each card", fee)}
                      <p>AMD cards: the mining software runs Pearl on recent ones too, and Otium tests it on yours. If the pool doesn't accept an AMD card's work, Otium moves it to the next coin that pays more than its power, so they aren't in this table.</p>
                    </div>
                  </div>
                </section>
            """;
        var description = best.PerDay is { } t
            ? $"GPU mining profitability today: what {cards.Count} NVIDIA cards earn mining Pearl per day and month, and the electricity price each breaks even at. The {best.Card.Name} leads at {Usd(t)} a day."
            : $"GPU mining profitability: typical speed and power for {cards.Count} NVIDIA cards mining Pearl, with live earnings after every fee.";
        var root = baseUrl.TrimEnd('/');
        return Layout("GPU mining profitability today: what every graphics card earns | Otium", description, body, legalLinks, "/download",
            baseUrl, "/mining", head: JsonLd(Breadcrumbs(root, ("Otium", "/"), ("GPU mining", "/mining"))));
    }

    /// <summary>schema.org data for a page's head. JSON's default escaping turns &lt; into <, so nothing in it can end the script.</summary>
    private static string JsonLd(params JsonNode[] items) =>
        $"""<script type="application/ld+json">{new JsonObject { ["@context"] = "https://schema.org", ["@graph"] = new JsonArray(items) }.ToJsonString()}</script>""";

    private static JsonObject Breadcrumbs(string root, params (string Name, string Path)[] trail) => new()
    {
        ["@type"] = "BreadcrumbList",
        ["itemListElement"] = new JsonArray(trail.Select((t, i) => (JsonNode)new JsonObject
        {
            ["@type"] = "ListItem",
            ["position"] = i + 1,
            ["name"] = t.Name,
            ["item"] = root + t.Path,
        }).ToArray()),
    };

    /// <summary>Questions and answers as search engines read them; answers may hold trusted HTML.</summary>
    private static JsonObject FaqPage(IEnumerable<(string Question, string AnswerHtml)> questions) => new()
    {
        ["@type"] = "FAQPage",
        ["mainEntity"] = new JsonArray(questions.Select(q => (JsonNode)new JsonObject
        {
            ["@type"] = "Question",
            ["name"] = q.Question,
            ["acceptedAnswer"] = new JsonObject { ["@type"] = "Answer", ["text"] = q.AnswerHtml },
        }).ToArray()),
    };
}
