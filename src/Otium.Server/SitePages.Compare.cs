using System.Globalization;
using Otium.Core.Fees;

namespace Otium.Server;

/// <summary>
/// /compare/nicehash and /compare/honeygain: Otium next to the apps people already search for ("nicehash alternative").
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
        IReadOnlyList<(string Label, string Url)> Sources);

    /// <summary>When the other apps' pages were last read for these comparisons.</summary>
    private const string ComparisonsChecked = "20 October 2026";

    public static readonly string[] ComparisonSlugs = ["nicehash", "honeygain"];

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
                ]),
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
                ]),
            _ => throw new ArgumentOutOfRangeException(nameof(slug)),
        };
    }

    public static string Compare(string slug, FeePolicy fees, IEnumerable<string> legalLinks, string baseUrl)
    {
        var c = ComparisonFor(slug, fees);
        var name = E(c.Name);
        var rows = string.Concat(c.Rows.Select(r => $"<tr><th scope=\"row\">{E(r.Topic)}</th><td>{E(r.Otium)}</td><td>{E(r.Them)}</td></tr>"));
        var sources = string.Concat(c.Sources.Select(s => $"""<li><a href="{E(s.Url)}" rel="nofollow">{E(s.Label)}</a></li>"""));
        var others = string.Concat(ComparisonSlugs.Where(s => s != slug).Select(s => $"""<a class="button ghost" href="/compare/{s}">Otium vs {E(ComparisonFor(s, fees).Name)}</a>"""));
        var body = $"""
                <div class="glow-frame" aria-hidden="true"><div class="glow"></div></div>
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
                      <p class="footnote">{name}'s details are from its own pages, checked on {ComparisonsChecked}, and can change. See Sources below.</p>
                    </div>
                    <div class="panel">
                      <h2>Where {name} is the better choice</h2>
                      <p>{E(c.TheyDoBetter)}</p>
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
