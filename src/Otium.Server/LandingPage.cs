using System.Net;

namespace Otium.Server;

/// <summary>The page a shared invite link opens. This is the free-advertising surface, so it sells the app plainly and honestly.</summary>
internal static class LandingPage
{
    /// <param name="baseUrl">The site's public address, for link previews.</param>
    public static string Render(string code, string? downloadUrl, bool codeExists, string baseUrl, IEnumerable<string>? legalLinks = null)
    {
        var safeCode = WebUtility.HtmlEncode(code);
        var safeDownload = WebUtility.HtmlEncode(downloadUrl ?? "#");
        var invite = codeExists
            ? $"""<p class="lead">A friend invited you: pay <strong>no Otium fee for 30 days</strong>. Download Otium on this computer and the invite is filled in for you; if setup doesn't show it, enter code <span class="code">{safeCode}</span>.</p>"""
            : """<p class="lead">This invite code isn't valid any more, but you can still join.</p>""";

        var body = $"""
                <div class="glow-frame" aria-hidden="true"><div class="glow"></div></div>
                <section class="page">
                  <div class="wrap">
                    <div class="panel">
                      <span class="label">Invite</span>
                      <h1 class="mt-14">Get paid for your computer's spare power</h1>
                      {invite}
                      <div class="actions"><a class="button" href="{safeDownload}"><span data-download-label>Download Otium</span></a></div>
                      <ul>
                        <li>One-click setup on Windows, macOS and Linux.</li>
                        <li>Pays straight to your own wallet: Otium never holds your money.</li>
                        <li>Waits when electricity would cost more than you earn.</li>
                      </ul>
                      <p class="muted mt-18">Earnings depend on your hardware and electricity price and are often small, typically cents to a few dollars a day. Otium runs the open-source XMRig miner, which some antivirus tools flag. <a href="/">How Otium works</a></p>
                    </div>
                  </div>
                </section>
            """;
        // One page per code, all saying the same thing: kept out of search results (the front page is the one to find),
        // but still with a full link preview, since this is the link people share.
        return SitePages.Layout(codeExists ? "You're invited to Otium" : "Otium invite",
            codeExists
                ? $"Otium puts your idle computer to work and pays you directly. Join with my code {code} for 30 days fee-free."
                : "Otium puts your idle computer to work and pays you directly.",
            body, legalLinks ?? [], downloadUrl, baseUrl, "/r/" + Uri.EscapeDataString(code), index: false);
    }
}
