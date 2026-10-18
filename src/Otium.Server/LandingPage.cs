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
            ? $"""<p class="lead">A friend invited you. Enter code <span class="code">{safeCode}</span> during setup and pay <strong>no app fee for 30 days</strong>.</p>"""
            : """<p class="lead">This invite code isn't valid any more, but you can still join.</p>""";

        var body = $"""
              <div class="card">
                <h1>Get paid for your computer's spare power</h1>
                {invite}
                <a class="button" href="{safeDownload}">Download Otium</a>
                <ul class="muted">
                  <li>One-click setup on Windows, macOS and Linux.</li>
                  <li>Pays straight to your own wallet: we never hold your money.</li>
                  <li>Pauses automatically when electricity would cost more than you earn.</li>
                </ul>
                <p class="fine">Earnings depend on your hardware and electricity price and are often small, typically cents to a few dollars a day. Otium runs the open-source XMRig miner, which some antivirus tools flag. <a href="/">How Otium works</a></p>
              </div>
            """;
        return SitePages.Layout("Otium invite",
            codeExists
                ? $"Otium puts your idle CPU to work and pays you directly. Join with my code {code} for 30 days fee-free."
                : "Otium puts your idle CPU to work and pays you directly.",
            body, legalLinks ?? []);
    }
}
