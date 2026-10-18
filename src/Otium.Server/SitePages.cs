using System.Globalization;
using System.Net;
using Otium.Core.Fees;
using Otium.Core.Pools;

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
}

/// <summary>
/// The public website: the front page that ads and invite links lead to, and the operator's legal pages. It explains
/// Otium plainly, with honest earnings and the same cost breakdown the app shows, generated from the server's fee terms
/// so the site and the app can't disagree.
/// </summary>
internal static class SitePages
{
    public static readonly string[] LegalPages = ["privacy", "terms"];

    /// <summary>Colours and type shared by every page, light and dark.</summary>
    public const string Styles = """
        :root { --bg:#f6f7f9; --fg:#14171c; --muted:#5b6472; --card:#fff; --accent:#1f8a5b; --accent-fg:#fff; --border:#e3e6eb; }
        @media (prefers-color-scheme: dark) { :root { --bg:#0f1216; --fg:#e8ebef; --muted:#9aa4b2; --card:#171b21; --accent:#3fbf87; --accent-fg:#06110c; --border:#262c35; } }
        * { box-sizing:border-box; }
        body { margin:0; font:16px/1.55 system-ui,-apple-system,"Segoe UI",sans-serif; background:var(--bg); color:var(--fg); }
        main { max-width:760px; margin:0 auto; padding:48px 16px; }
        .card { background:var(--card); border:1px solid var(--border); border-radius:14px; padding:28px; margin-bottom:20px; }
        h1 { font-size:32px; line-height:1.15; margin:0 0 12px; letter-spacing:-.01em; }
        h2 { font-size:21px; margin:0 0 10px; }
        h3 { font-size:16px; margin:18px 0 4px; }
        p { margin:0 0 10px; }
        .lead { font-size:18px; }
        .muted, .fine { color:var(--muted); }
        .fine { font-size:14px; }
        .code { font-family:ui-monospace,Consolas,monospace; font-weight:700; letter-spacing:.08em; background:var(--bg); padding:2px 8px; border-radius:6px; }
        a { color:var(--accent); }
        a.button { display:inline-block; margin:8px 0; background:var(--accent); color:var(--accent-fg); text-decoration:none; padding:12px 22px; border-radius:10px; font-weight:600; }
        ul, ol { padding-left:20px; margin:0 0 10px; }
        li { margin-bottom:6px; }
        .steps { display:grid; grid-template-columns:repeat(auto-fit,minmax(200px,1fr)); gap:14px; }
        .step { border:1px solid var(--border); border-radius:12px; padding:16px; }
        .step b { display:block; margin-bottom:4px; }
        table { width:100%; border-collapse:collapse; }
        th, td { text-align:left; vertical-align:top; padding:8px 0; border-top:1px solid var(--border); }
        th { width:9.5em; padding-right:12px; font-weight:600; }
        footer { text-align:center; font-size:14px; color:var(--muted); padding:8px 0 24px; }
        footer a { color:var(--muted); margin:0 8px; }
        @media (max-width:520px) {
          main { padding:24px 16px; }
          .card { padding:20px; }
          h1 { font-size:27px; }
          th, td { display:block; width:auto; }
          th { padding:10px 0 0; }
          td { border-top:none; padding-top:2px; }
        }
        """;

    public static string Layout(string title, string description, string body, IEnumerable<string> legalLinks) =>
        $$"""
        <!doctype html>
        <html lang="en">
        <head>
          <meta charset="utf-8">
          <meta name="viewport" content="width=device-width, initial-scale=1">
          <title>{{WebUtility.HtmlEncode(title)}}</title>
          <meta name="description" content="{{WebUtility.HtmlEncode(description)}}">
          <meta property="og:title" content="{{WebUtility.HtmlEncode(title)}}">
          <meta property="og:description" content="{{WebUtility.HtmlEncode(description)}}">
          <style>{{Styles}}</style>
        </head>
        <body>
          <main>
        {{body}}
          </main>
          <footer>
            <a href="/">Otium</a>{{string.Concat(legalLinks.Select(p => $"""<a href="/{p}">{Capitalize(p)}</a>"""))}}<a href="https://github.com/Malogna/otium">Source code</a>
          </footer>
        </body>
        </html>
        """;

    /// <param name="fees">The server's fee terms, as apps receive them.</param>
    /// <param name="legalLinks">The legal pages that exist ("privacy", "terms").</param>
    public static string Home(FeePolicy fees, string? downloadUrl, IEnumerable<string> legalLinks)
    {
        var download = WebUtility.HtmlEncode(downloadUrl ?? "#");
        var fee = Percent(fees.FeePercent);
        var feeFreeDays = fees.RefereeFeeHoliday.TotalDays.ToString("0", CultureInfo.InvariantCulture);
        var referrerShare = Percent(fees.ReferrerShareOfFee * 100);
        var referrerMonths = (fees.ReferrerRewardPeriod.TotalDays / 30).ToString("0", CultureInfo.InvariantCulture);
        var costs = string.Concat(CostBreakdown.For(fees, PayoutPool.Unmineable, PayoutPool.Unmineable, referralCode: true)
            .Select(line => $"<tr><th>{WebUtility.HtmlEncode(line.Who)}</th><td>{WebUtility.HtmlEncode(line.What)}</td></tr>"));

        var body = $"""
              <div class="card">
                <h1>Your computer earns while you're away</h1>
                <p class="lead">Otium puts your computer's spare processor and graphics card to work while you're not using it, and the
                  mining pool pays your own wallet directly: in USDT, Bitcoin, Litecoin, BNB, Solana, TRON, Ether or Monero.</p>
                <a class="button" href="{download}">Download Otium</a>
                <p class="fine">Free for Windows, macOS and Linux. Otium's fee is {fee} of mining time, shown in full below.</p>
              </div>

              <div class="card">
                <h2>How it works</h2>
                <div class="steps">
                  <div class="step"><b>1. Choose where to be paid</b>Pick a coin and paste your wallet address. USDT on TRON is the default: it's worth a dollar and pays out from 1.5 USDT.</div>
                  <div class="step"><b>2. Leave it running</b>Otium mines only while you're away and stops the moment you're back. It pauses when electricity would cost more than it earns.</div>
                  <div class="step"><b>3. Get paid by the pool</b>unMineable sends your earnings to your address once a day after you reach its minimum. Otium never holds your money.</div>
                </div>
              </div>

              <div class="card">
                <h2>What you can expect</h2>
                <p>Honestly: it depends on your hardware and your electricity price, and it changes daily with coin prices.</p>
                <ul>
                  <li><b>Processor only:</b> a few cents a day.</li>
                  <li><b>Recent NVIDIA graphics card:</b> roughly $1–3 a day after electricity at $0.15 per kWh (October 2026 prices).
                    For example, an RTX 3080 mining Pearl.</li>
                  <li><b>Most AMD cards:</b> usually less. Adding a free Quantus wallet in Settings helps, since Quantus is the coin that
                    pays best on them.</li>
                </ul>
                <p>Otium tests your own computer when you install it and shows you its estimate before you start.</p>
              </div>

              <div class="card">
                <h2>Everything that comes off your earnings</h2>
                <table>{costs}</table>
              </div>

              <div class="card">
                <h2>Invite friends</h2>
                <p>Friends who join with your invite code pay no Otium fee for their first {feeFreeDays} days. For {referrerMonths} months
                  after that, {referrerShare} of Otium's fee on their mining goes to you, mined straight to your address. Every friend who starts mining
                  also earns you more fee-free days.</p>
              </div>

              <div class="card">
                <h2>Questions</h2>
                <h3>Is my money safe?</h3>
                <p>Otium never touches it. The pool pays your wallet directly, and you can check your balance on the pool's own page at any time.</p>
                <h3>Will it slow my computer down?</h3>
                <p>By default it only runs while you're away, at the lowest priority, and you choose how much of the processor it may use.</p>
                <h3>Why does my antivirus warn about it?</h3>
                <p>Antivirus tools flag every mining program, because criminals hide miners on other people's computers. Otium downloads
                  the official open-source XMRig and SRBMiner releases, checks their fingerprints, and keeps them in one folder you can
                  allow. It only mines on your computer when you've set it up and turned it on.</p>
                <h3>I live in the United States</h3>
                <p>unMineable doesn't serve US residents, so Otium pays you in Monero through MoneroOcean instead.</p>
                <h3>Can I also share my internet connection?</h3>
                <p>On Windows, optionally. A Mysterium node shares a little of your unused bandwidth with businesses and pays you in MYST
                  at your own Polygon address. It's off unless you turn it on and agree to Mysterium's terms.</p>
              </div>

              <div class="card" style="text-align:center">
                <a class="button" href="{download}">Download Otium</a>
              </div>
            """;
        return Layout("Otium: get paid for your computer's spare power",
            "Otium mines with your idle processor and graphics card while you're away, and the pool pays your own wallet directly.",
            body, legalLinks);
    }

    /// <summary>A legal page: the operator's reviewed HTML inside the site's layout.</summary>
    public static string Legal(string page, string bodyHtml, IEnumerable<string> legalLinks) =>
        Layout($"Otium: {Capitalize(page)}", $"Otium's {page} page.", $"""<div class="card">{bodyHtml}</div>""", legalLinks);

    /// <summary>The legal pages whose files exist in <paramref name="directory"/>, in footer order.</summary>
    public static IReadOnlyList<string> AvailableLegalPages(string directory) =>
        LegalPages.Where(p => File.Exists(Path.Combine(directory, $"{p}.html"))).ToList();

    private static string Percent(double value) => value.ToString("0.##", CultureInfo.InvariantCulture) + "%";

    private static string Capitalize(string word) => word.Length == 0 ? word : char.ToUpperInvariant(word[0]) + word[1..];
}
