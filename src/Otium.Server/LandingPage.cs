using System.Net;

namespace Otium.Server;

/// <summary>The page a shared invite link opens. This is the free-advertising surface, so it sells the app plainly and honestly.</summary>
internal static class LandingPage
{
    public static string Render(string code, string? downloadUrl, bool codeExists)
    {
        var safeCode = WebUtility.HtmlEncode(code);
        var safeDownload = WebUtility.HtmlEncode(downloadUrl ?? "#");
        var invite = codeExists
            ? $"""<p class="lead">A friend invited you. Enter code <span class="code">{safeCode}</span> during setup and pay <strong>no app fee for 30 days</strong>.</p>"""
            : """<p class="lead">This invite code isn't valid any more, but you can still join.</p>""";

        return $$"""
            <!doctype html>
            <html lang="en">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width, initial-scale=1">
              <title>Otium invite</title>
              <meta property="og:title" content="Get paid for your PC's spare power">
              <meta property="og:description" content="Otium puts your idle CPU to work and pays you directly. Join with my code {{safeCode}} for 30 days fee-free.">
              <style>
                :root { --bg:#f6f7f9; --fg:#14171c; --muted:#5b6472; --card:#fff; --accent:#1f8a5b; --border:#e3e6eb; }
                @media (prefers-color-scheme: dark) { :root { --bg:#0f1216; --fg:#e8ebef; --muted:#9aa4b2; --card:#171b21; --accent:#3fbf87; --border:#262c35; } }
                * { box-sizing:border-box; }
                body { margin:0; font:16px/1.55 system-ui,-apple-system,"Segoe UI",sans-serif; background:var(--bg); color:var(--fg); }
                main { max-width:640px; margin:0 auto; padding:48px 16px; }
                .card { background:var(--card); border:1px solid var(--border); border-radius:14px; padding:28px; }
                h1 { font-size:28px; line-height:1.2; margin:0 0 12px; }
                .lead { font-size:18px; }
                .code { font-family:ui-monospace,Consolas,monospace; font-weight:700; letter-spacing:.08em; background:var(--bg); padding:2px 8px; border-radius:6px; }
                a.button { display:inline-block; margin-top:8px; background:var(--accent); color:#fff; text-decoration:none; padding:12px 22px; border-radius:10px; font-weight:600; }
                ul { padding-left:20px; color:var(--muted); }
                .fine { color:var(--muted); font-size:14px; margin-top:24px; }
              </style>
            </head>
            <body>
              <main>
                <div class="card">
                  <h1>Get paid for your computer's spare power</h1>
                  {{invite}}
                  <a class="button" href="{{safeDownload}}">Download Otium</a>
                  <ul>
                    <li>One-click setup on Windows, macOS and Linux.</li>
                    <li>Pays straight to your own wallet: we never hold your money.</li>
                    <li>Pauses automatically when electricity would cost more than you earn.</li>
                  </ul>
                  <p class="fine">Earnings depend on your hardware and electricity price and are often small, typically cents to a few dollars a day. Otium runs the open-source XMRig miner, which some antivirus tools flag.</p>
                </div>
              </main>
            </body>
            </html>
            """;
    }
}
