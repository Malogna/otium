using System.Net;

namespace Otium.Server;

/// <summary>The page a shared invite link opens. This is the free-advertising surface, so it sells the app plainly and honestly.</summary>
internal static class LandingPage
{
    public static string Render(string code, string? downloadUrl, bool codeExists, IEnumerable<string>? legalLinks = null)
    {
        var safeCode = WebUtility.HtmlEncode(code);
        var safeDownload = WebUtility.HtmlEncode(downloadUrl ?? "#");
        var invite = codeExists
            ? $"""<p class="lead">A friend invited you. Enter code <span class="code">{safeCode}</span> during setup and pay <strong>no Otium fee for 30 days</strong>.</p>"""
            : """<p class="lead">This invite code isn't valid any more, but you can still join.</p>""";

        var body = $"""
                <div class="glow" aria-hidden="true"></div>
                <section class="page">
                  <div class="wrap">
                    <div class="panel">
                      <span class="label">Invite</span>
                      <h1 style="margin-top:14px">Get paid for your computer's spare power</h1>
                      {invite}
                      <div class="actions"><a class="button" href="{safeDownload}"><span data-download-label>Download Otium</span></a></div>
                      <ul>
                        <li>One-click setup on Windows, macOS and Linux.</li>
                        <li>Pays straight to your own wallet: Otium never holds your money.</li>
                        <li>Waits when electricity would cost more than you earn.</li>
                      </ul>
                      <p class="muted" style="margin-top:18px">Earnings depend on your hardware and electricity price and are often small, typically cents to a few dollars a day. Otium runs the open-source XMRig miner, which some antivirus tools flag. <a href="/">How Otium works</a></p>
                    </div>
                  </div>
                </section>
            """;
        return SitePages.Layout("Otium invite",
            codeExists
                ? $"Otium puts your idle computer to work and pays you directly. Join with my code {code} for 30 days fee-free."
                : "Otium puts your idle computer to work and pays you directly.",
            body, legalLinks ?? [], downloadUrl);
    }
}
