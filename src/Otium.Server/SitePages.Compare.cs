using System.Globalization;
using Otium.Core.Fees;

namespace Otium.Server;

/// <summary>
/// /compare/{app}: Otium next to the apps people already search for ("nicehash alternative", "grass alternative").
/// Every claim about the other app comes from its own help pages, terms or fee pages, listed under Sources with the date
/// they were checked; anything those pages didn't confirm is left out. Each page also says plainly where the other app
/// is the better choice.
/// </summary>
internal static partial class SitePages
{
    private sealed record Comparison(
        string Slug,
        string Name,
        string Category,
        string Intro,
        IReadOnlyList<(string Topic, string Otium, string Them)> Rows,
        string TheyDoBetter,
        IReadOnlyList<(string Label, string Url)> Sources,
        string Checked); // when the other app's pages were last read for this comparison

    /// <summary>Every comparison, in the footer's order: the apps people search for instead of selling processing power
    /// (NiceHash, Salad) or bandwidth (Honeygain, PacketStream, Grass).</summary>
    private static readonly (string Slug, string Name, bool Bandwidth)[] Comparisons =
    [
        ("nicehash", "NiceHash", false), ("salad", "Salad", false),
        ("honeygain", "Honeygain", true), ("packetstream", "PacketStream", true), ("grass", "Grass", true),
    ];

    public static readonly string[] ComparisonSlugs = Comparisons.Select(c => c.Slug).ToArray();

    private static string ComparisonLinks => string.Concat(Comparisons.Select(c => $"""<a href="/compare/{c.Slug}">vs {E(c.Name)}</a>"""));

    private static Comparison ComparisonFor(string slug, FeePolicy fees)
    {
        var fee = Percent(fees.FeePercent);
        var payout = Core.Wallets.PayoutCoins.Usdt.Threshold.ToString("0.##", CultureInfo.InvariantCulture);
        const string platforms = "Windows, macOS and Linux (macOS and Linux in beta)";
        return slug switch
        {
            "nicehash" => new Comparison("nicehash", "NiceHash", "GPU mining",
                "Both put your graphics card to work and pay you for it. The difference is where the money goes: NiceHash pays into a NiceHash wallet that you withdraw from, and asks every user to verify their identity. Otium has the mining pool pay your own wallet directly, with no account.",
                [
                    ("Where your earnings go", "Straight to your own wallet address, paid by the pool. Otium never holds your money.", "Into your NiceHash wallet, a custodial wallet NiceHash holds for you. You then withdraw to your own."),
                    ("What you're paid in", "Your choice: USDT, BTC, LTC, BNB, SOL, TRX, ETH or XMR.", "Bitcoin, or USDT."),
                    ("When you're paid", $"Daily by unMineable once you reach its minimum (e.g. {payout} USDT). In the US, by MoneroOcean in Monero.", "Every 4 hours by default, or daily, into the NiceHash wallet."),
                    ("Fees", $"Otium's {fee} of mining time, plus the mining software's and the pool's own fees, all listed on the front page.", "A 2% fee on mining payouts, then a withdrawal fee to your own wallet (for Bitcoin, at least 0.0001 BTC, with a 0.0005 BTC minimum)."),
                    ("Account and ID", "No account and no ID: you paste a wallet address. An email is optional.", "An account, and identity verification (KYC) for every user: an ID document, proof of address and a liveness check."),
                    ("Computers", platforms + ".", "NiceHash Miner and QuickMiner run on Windows only; NiceHash OS is for Linux mining rigs."),
                    ("When it mines", "Only while you're away, by default; it stops within a second when you're back, and waits when electricity would cost more than mining earns.", "Whenever you start it. NiceHash Miner has a setting to start when the computer is idle."),
                    ("Your processor and internet", "Mines with your processor too, and can share unused internet bandwidth if you choose.", "Graphics cards, and processors through XMRig."),
                ],
                "NiceHash pays several times a day, has Lightning withdrawals for small Bitcoin amounts, and its apps are signed. If you want Bitcoin and don't mind an account with ID checks, it's a solid choice.",
                [
                    ("NiceHash: when and how you get paid", "https://www.nicehash.com/support/mining-help/earnings-and-payments/when-and-how-do-you-get-paid"),
                    ("NiceHash: withdrawal fees", "https://www.nicehash.com/support/general-help/service-fees/withdrawal-fees"),
                    ("NiceHash: what is KYC", "https://www.nicehash.com/support/general-help/kyc/what-is-kyc"),
                    ("NiceHash: KYC tiers", "https://www.nicehash.com/support/general-help/kyc/kyc-tiers"),
                    ("NiceHash: terms", "https://www.nicehash.com/terms"),
                    ("NiceHash: download center", "https://www.nicehash.com/download-center"),
                    ("NiceHash Miner on GitHub", "https://github.com/nicehash/NiceHashMiner"),
                    ("NiceHash: QuickMiner error codes", "https://www.nicehash.com/support/mining-help/quickminer/quickminer-error-codes"),
                ],
                "20 October 2026"),
            "salad" => new Comparison("salad", "Salad", "idle computing",
                "Both put your idle computer to work and pay you for it. Salad credits reward points to a Salad account, which you spend in its store on PayPal cash, gift cards, games or crypto. Otium has the mining pool pay your own wallet directly, with no account.",
                [
                    ("Where your earnings go", "Straight to your own wallet address, paid by the pool. Otium never holds your money.", "Reward points in your Salad Balance. Salad's terms say they have no cash value and remain Salad's property until you spend them."),
                    ("What you're paid in", "Your choice: USDT, BTC, LTC, BNB, SOL, TRX, ETH or XMR.", "Whatever you pick in the Salad Storefront: PayPal cash (in supported countries), RENDER on Solana, prepaid cards, gift cards or games."),
                    ("When you're paid", $"Daily by unMineable once you reach its minimum (e.g. {payout} USDT). In the US, by MoneroOcean in Monero.", "Points should reach your balance within five business days. Unspent points may expire after 12 months without earning."),
                    ("Fees", $"Otium's {fee} of mining time, plus the mining software's and the pool's own fees, all listed on the front page.", "No fee is listed. Salad buys rewards at discount or wholesale prices, and its margin is in what you pay for them."),
                    ("Account and ID", "No account and no ID: you paste a wallet address. An email is optional.", "An account with an email, age 18 or over. Salad may ask you to verify your account or give more information before you redeem."),
                    ("Computers", platforms + ".", "Windows 10 or 11, 64-bit. Its support pages mention beta Linux support; no Mac."),
                    ("When it runs", "Only while you're away, by default; it stops within a second when you're back, and waits when electricity would cost more than mining earns.", "When you start it, or on its own after 10 minutes without mouse or keyboard use (you can change the delay)."),
                    ("What your computer does", "Mines with your graphics card and processor, and can share unused internet bandwidth if you choose.", "AI and other jobs for Salad's customers on your graphics card or processor, bandwidth sharing in 31 countries, and mining when there are no jobs."),
                ],
                "Salad pays in PayPal cash, gift cards or games if you'd rather not use crypto, sells your graphics card's time to AI companies, and publishes hourly earning rates for each card.",
                [
                    ("Salad: download and how it works", "https://salad.com/download/"),
                    ("Salad: terms for Chefs", "https://salad.com/terms/chefs"),
                    ("Salad: business model", "https://support.salad.com/faq/company/what-is-salads-business-model"),
                    ("Salad: redeeming RENDER", "https://support.salad.com/rewards/redeeming-your-rewards/how-to-redeem-render-on-salad"),
                    ("Salad: macOS and Linux", "https://support.salad.com/faq/compatibility/do-you-support-macos-and-linux"),
                    ("Salad: Auto Start", "https://support.salad.com/guides/getting-jobs/configure-auto-start"),
                    ("Salad: bandwidth sharing jobs", "https://support.salad.com/guides/getting-jobs/getting-bandwidth-sharing-jobs"),
                    ("Salad: crypto mining jobs", "https://support.salad.com/guides/getting-jobs/getting-cryptomining-jobs"),
                    ("Salad: earning rates", "https://salad.com/earning-rates/"),
                ],
                "2 November 2026"),
            "honeygain" => new Comparison("honeygain", "Honeygain", "internet sharing",
                "Both pay you for sharing internet you aren't using. Honeygain keeps your earnings as credits in a Honeygain account until you cash out. Otium has them converted and sent to your own wallet, shares only while you're away, and also mines with your computer.",
                [
                    ("Where your earnings go", "To your own wallet address in your coin, through a one-time conversion, once $1 has built up. Otium never holds your money.", "Credits in your Honeygain account (1,000 credits = $1) until you cash out."),
                    ("Cashing out", "Automatic. A payout waits while the network fee would be a large part of it: for USDT on TRON that's about $16.", "From $20 by PayPal, or smaller amounts through JumpTask. Credits expire after 6 months without sharing."),
                    ("Fees", $"Otium's {fee} of each payout.", "No Honeygain fee; PayPal may charge one."),
                    ("Account and ID", "No account and no ID: you paste a wallet address. An email is optional.", "An account with a confirmed email, age 18 or over, and identity verification may be needed for payouts."),
                    ("Devices", "Windows, Linux and Apple-silicon Macs.", "Windows, macOS, Android, and Linux through Docker. One device per internet connection."),
                    ("Also earns by", "Mining with your graphics card and processor while you're away.", "Content Delivery, a bonus on some Windows and Mac devices."),
                ],
                "Honeygain runs on Android phones, which Otium doesn't, and pays by PayPal if you'd rather not use crypto.",
                [
                    ("Honeygain: minimum payout", "https://support.honeygain.com/hc/en-us/articles/30688405604252"),
                    ("Honeygain: payout methods", "https://support.honeygain.com/hc/en-us/articles/30688374419228"),
                    ("Honeygain: payouts", "https://www.honeygain.com/honeygain-payout/"),
                    ("Honeygain: terms of use", "https://www.honeygain.com/terms-of-use/"),
                    ("Honeygain: supported platforms", "https://support.honeygain.com/hc/en-us/articles/30688438511900"),
                    ("Honeygain: Content Delivery", "https://support.honeygain.com/hc/en-us/articles/30688572550556"),
                ],
                "20 October 2026"),
            "packetstream" => new Comparison("packetstream", "PacketStream", "internet sharing",
                "Both pay you for sharing internet you aren't using. PacketStream keeps a dollar balance in your PacketStream account and pays it out by PayPal. Otium has your earnings converted and sent to your own crypto wallet, shares only while you're away, and also mines with your computer.",
                [
                    ("Where your earnings go", "To your own wallet address in your coin, through a one-time conversion, once $1 has built up. Otium never holds your money.", "A dollar balance in your PacketStream account: $0.10 for each GB shared, at today's rate."),
                    ("Cashing out", "Automatic. A payout waits while the network fee would be a large part of it: for USDT on TRON that's about $16.", "From $5, in US dollars by PayPal only, paid within a week."),
                    ("Fees", $"Otium's {fee} of each payout.", "A 3% cash-out fee; PayPal may charge its own."),
                    ("Account and ID", "No account and no ID: you paste a wallet address. An email is optional.", "An account with a username and email, age 18 or over."),
                    ("Devices", "Windows, Linux and Apple-silicon Macs.", "Windows 10 or newer, macOS 14 or newer, Linux through Docker, and Android 8 or newer (a direct download, not on Google Play). One device per internet address."),
                    ("When it shares", "Proxybase sharing runs only while you're away from the computer and stops when you're back.", "While the app runs; closing the app doesn't stop sharing, you stop it in the app."),
                    ("Also earns by", "Mining with your graphics card and processor while you're away.", "Referrals: 20% of what the people you refer earn from sharing, paid by PacketStream on top (a limited-time offer)."),
                ],
                "PacketStream pays US dollars by PayPal, so you don't need a crypto wallet, runs on Android, and has one flat rate per GB with a low $5 minimum.",
                [
                    ("PacketStream: share bandwidth", "https://packetstream.io/share-bandwidth/"),
                    ("PacketStream: FAQ", "https://packetstream.io/faq/"),
                    ("PacketStream: terms of service", "https://packetstream.io/terms-of-service/"),
                    ("PacketStream: privacy policy", "https://packetstream.io/privacy-policy/"),
                    ("PacketStream: referral terms", "https://packetstream.io/referral-terms/"),
                ],
                "2 November 2026"),
            "grass" => new Comparison("grass", "Grass", "internet sharing",
                "Both pay you for sharing internet you aren't using. Grass gives you points and decides later how they turn into GRASS tokens or USDC, paid in rounds you claim. Otium converts what you earn and sends it to your own wallet in the coin you choose as it builds up, shares only while you're away, and also mines with your computer.",
                [
                    ("What you earn", "Your coin, converted from what Proxybase pays per GB: USDT, BTC, LTC, BNB, SOL, TRX, ETH or XMR.", "Points, which have no money value of their own. Grass decides what rewards they bring, in GRASS tokens or USDC; the last round, for traffic until 8 June 2026, paid USDC."),
                    ("When you're paid", "Automatically, to your own wallet address, once $1 has built up and the network fee is a small part of the payout. Otium never holds your money.", "In rounds after each period ends, claimed on the Grass dashboard; unclaimed rewards are kept by Grass. Grass hasn't yet said how traffic after 8 June 2026 will be paid."),
                    ("Where it goes", "Your own wallet address, which you can change any time.", "A Solana wallet built into the Grass app, which you control, or a wallet you link, which you may not be able to change."),
                    ("Fees", $"Otium's {fee} of each payout.", "$0.15 per wallet transaction plus Solana's network fee, and MoonPay's own fees to cash out to a card or PayPal."),
                    ("Account and ID", "No account and no ID: you paste a wallet address. An email is optional.", "A verified account (email or Google), one per person. Not available in 25 countries and regions, including Russia, Iran and Venezuela, and Grass may verify your identity before you claim."),
                    ("Devices", "Windows, Linux and Apple-silicon Macs.", "A desktop app for Windows, macOS and Ubuntu, a Chrome extension, and Android. Rewards are capped per internet connection."),
                    ("Also earns by", "Mining with your graphics card and processor while you're away.", "Referrals, a share of the uptime points of people you refer, three levels deep; and staking GRASS."),
                ],
                "Grass runs as a Chrome extension and on Android phones, which Otium doesn't, and has its own token you can hold and stake if you want a stake in the network.",
                [
                    ("Grass: terms and conditions", "https://www.grass.io/terms-and-conditions/"),
                    ("Grass Foundation: rewards program terms", "https://www.grassfoundation.io/rewards-program-terms-and-conditions"),
                    ("Grass: how Stage 2 rewards work", "https://www.grass.io/learn/how-your-stage-2-rewards-allocation-works/"),
                    ("Grass: the points model", "https://www.grass.io/learn/an-update-to-the-grass-points-model/"),
                    ("Grass: how the Grass wallet works", "https://www.grass.io/learn/how-the-grass-wallet-works/"),
                    ("Grass: setting up on every device", "https://www.grass.io/learn/how-to-set-up-grass-on-every-device-desktop-extension-and-android/"),
                    ("Grass docs: points and referrals", "https://grass-foundation.gitbook.io/grass-docs/how-to-guide/grass-points"),
                ],
                "2 November 2026"),
            _ => throw new ArgumentOutOfRangeException(nameof(slug)),
        };
    }

    public static string Compare(string slug, FeePolicy fees, IEnumerable<string> legalLinks, string baseUrl)
    {
        var c = ComparisonFor(slug, fees);
        var name = E(c.Name);
        var rows = string.Concat(c.Rows.Select(r => $"<tr><th scope=\"row\">{E(r.Topic)}</th><td>{E(r.Otium)}</td><td>{E(r.Them)}</td></tr>"));
        var sources = string.Concat(c.Sources.Select(s => $"""<li><a href="{E(s.Url)}" rel="nofollow">{E(s.Label)}</a></li>"""));
        // The other apps of the same kind, and the page about selling that kind; the rest are in the footer.
        var bandwidth = Comparisons.Single(o => o.Slug == slug).Bandwidth;
        var others = string.Concat(Comparisons.Where(o => o.Bandwidth == bandwidth && o.Slug != slug)
            .Select(o => $"""<a class="button ghost" href="/compare/{o.Slug}">Otium vs {E(o.Name)}</a>"""));
        var topic = bandwidth
            ? """<a href="/sell-bandwidth">How selling your unused bandwidth works</a>"""
            : """<a href="/sell-computing-power">How selling your PC's idle processing power works</a>""";
        var body = $"""
                <section class="page stacked">
                  <div class="wrap">
                    <nav class="crumbs" aria-label="Breadcrumb"><a href="/">Otium</a><span aria-hidden="true">/</span><span aria-current="page">Otium vs {name}</span></nav>
                    <div class="panel">
                      <span class="label">Compare · {E(c.Category)}</span>
                      <h1 class="mt-14">A {name} alternative that pays your own wallet</h1>
                      <p class="lead">{E(c.Intro)}</p>
                      <div class="actions"><a class="button" href="/download"><span data-download-label>Download Otium</span></a>{others}</div>
                    </div>
                    <div class="panel real-cards wide compare">
                      <table>
                        <caption>Otium and {name} side by side</caption>
                        <thead><tr><th scope="col"></th><th scope="col">Otium</th><th scope="col">{name}</th></tr></thead>
                        <tbody>{rows}</tbody>
                      </table>
                      <p class="footnote">{name}'s details are from its own pages, checked on {E(c.Checked)}, and can change. See Sources below.</p>
                    </div>
                    <div class="panel">
                      <h2>Where {name} is the better choice</h2>
                      <p>{E(c.TheyDoBetter)}</p>
                      <p>{topic}</p>
                    </div>
                    <div class="panel">
                      <h2>Sources</h2>
                      <ul>{sources}</ul>
                      <p class="muted">Otium isn't affiliated with {name}. Spotted something out of date? Write to {ContactLink}.</p>
                    </div>
                  </div>
                </section>
            """;
        var root = baseUrl.TrimEnd('/');
        return Layout($"{c.Name} alternative: Otium vs {c.Name} compared | Otium",
            $"A {c.Name} alternative that pays your own wallet, with no account or ID. How Otium compares on payouts, fees and platforms, from {c.Name}'s own pages.",
            body, legalLinks, "/download", baseUrl, $"/compare/{slug}",
            head: JsonLd(Breadcrumbs(root, ("Otium", "/"), ($"Otium vs {c.Name}", $"/compare/{slug}"))));
    }
}
