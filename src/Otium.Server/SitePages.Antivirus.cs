namespace Otium.Server;

/// <summary>
/// /antivirus: why antivirus tools flag Otium's miners and how to allow its one folder, for each common antivirus. People
/// search for this ("trojan:win32/coinminer", "allow xmrig in windows defender") whether or not they know Otium, and it's
/// the main hurdle for new users. Only allowing the folder is covered: restoring quarantined files isn't needed, because
/// Otium's Try again downloads fresh copies and checks them again.
/// </summary>
internal static partial class SitePages
{
    /// <summary>
    /// How to add a folder exclusion in each antivirus, from the vendors' own help pages (checked 20 October 2026). Steps
    /// are trusted HTML. McAfee's help only documents excluding single files from real-time scanning, so it gets a
    /// pointer to its help rather than steps that might not work.
    /// </summary>
    private static readonly (string Id, string Name, string StepsHtml, string HelpUrl)[] AntivirusSteps =
    [
        ("avast", "Avast", """
            <li>Open Avast and go to <strong>☰ Menu</strong>, then <strong>Settings</strong>.</li>
            <li>Choose <strong>General</strong>, then <strong>Exceptions</strong>, and press <strong>Add exception</strong>.</li>
            <li>Press <strong>Next</strong>, then <strong>I understand risks</strong>.</li>
            <li>On the <strong>File / Folder</strong> tab, pick Otium's engines folder, press <strong>OK</strong>, then <strong>Add</strong>.</li>
            """, "https://support.avast.com/en-us/article/antivirus-scan-exclusions/"),
        ("avg", "AVG", """
            <li>Open AVG and go to <strong>☰ Menu</strong>, then <strong>Settings</strong>.</li>
            <li>Choose <strong>General</strong>, then <strong>Exceptions</strong>, and press <strong>Add exception</strong>.</li>
            <li>Press <strong>Next</strong>, then <strong>I understand risks</strong>.</li>
            <li>On the <strong>File / Folder</strong> tab, pick Otium's engines folder, then press <strong>Add</strong>.</li>
            """, "https://support.avg.com/SupportArticleView?l=en&urlName=avg-antivirus-scan-exclusions"),
        ("norton", "Norton 360", """
            <li>Open Norton 360 and choose <strong>Security</strong> on the left.</li>
            <li>Open the <strong>Scans</strong> tile, then the <strong>Exclusions</strong> tab.</li>
            <li>Press <strong>Add</strong>, pick Otium's engines folder, press <strong>OK</strong>, then <strong>Add</strong>. This covers Auto-Protect too.</li>
            """, "https://support.norton.com/sp/en/us/norton-360/home/solutions/v3672136"),
        ("bitdefender", "Bitdefender", """
            <li>Open Bitdefender and choose <strong>Protection</strong> (under <strong>Services</strong> in the newest app).</li>
            <li>In the <strong>Antivirus</strong> panel, press <strong>Open</strong>, then go to the <strong>Settings</strong> tab.</li>
            <li>Press <strong>Manage exceptions</strong>, then <strong>Add an exception</strong>, pick Otium's engines folder and save.</li>
            <li>If you turned on Bitdefender's Cryptomining Protection, it has its own exceptions list: add the folder there too.</li>
            """, "https://www.bitdefender.com/consumer/support/answer/125476/"),
        ("kaspersky", "Kaspersky", """
            <li>Open Kaspersky, press the settings (gear) button and choose <strong>Security settings</strong>.</li>
            <li>Choose <strong>Exclusions and actions on object detection</strong>, then <strong>Manage exclusions</strong>, then <strong>Add</strong>.</li>
            <li>Press <strong>Browse</strong>, pick Otium's engines folder and press <strong>Select</strong>.</li>
            <li>Leave the threat type empty, make sure it's <strong>Active</strong>, and press <strong>Add</strong>.</li>
            """, "https://support.kaspersky.com/help/Kaspersky/Win21.20/en-US/227390.htm"),
        ("eset", "ESET", """
            <li>Open ESET and press <strong>F5</strong> for Advanced setup.</li>
            <li>Choose <strong>Scan</strong>, then <strong>Edit</strong> next to <strong>Performance exclusions</strong>.</li>
            <li>Press <strong>Add</strong>, then <strong>…</strong>, pick Otium's engines folder and press <strong>Open</strong>.</li>
            <li>Press <strong>OK</strong> until the windows close.</li>
            """, "https://support.eset.com/en/kb2769"),
        ("malwarebytes", "Malwarebytes", """
            <li>Open Malwarebytes and choose <strong>Detection History</strong>.</li>
            <li>Open the <strong>Allow list</strong> tab and press <strong>Add</strong>.</li>
            <li>Choose to allow a folder, pick Otium's engines folder, and confirm. The labels may differ slightly in the newest version.</li>
            """, "https://help.malwarebytes.com/hc/en-us/articles/31589453538075"),
        ("mcafee", "McAfee", """
            <li>McAfee's help only describes excluding single files from real-time scanning, not a whole folder. Follow McAfee's own help, or ask its support how to allow a folder.</li>
            """, "https://smarthelp.mcafee.com/help/mcafee/13.6/total%20protection/en-us/GUID-7A7B8997-88DB-46C3-9A64-1240B3A98E6A.html"),
    ];

    private static readonly (string Question, string AnswerHtml)[] AntivirusFaq =
    [
        ("Is Otium a virus?", "No. Otium is open source, and the miners it runs are the official releases of XMRig and SRBMiner, downloaded from their own GitHub pages and checked against a pinned fingerprint before they run. Antivirus tools flag them for what they are, crypto miners, because criminals hide the same miners on other people's computers."),
        ("Why does Windows Defender say Trojan:Win32/CoinMiner or PUA:Win32/CoinMiner?", "Those are Microsoft Defender's names for crypto mining software. It uses them for every miner, wanted or not, because it can't tell whether you installed it yourself. Allowing Otium's engines folder tells it you did."),
        ("Should I turn off my antivirus?", "No. Allow only Otium's engines folder and leave everything else on, including Windows' potentially unwanted app blocking."),
        ("Is allowing the folder safe?", "An exclusion means your antivirus doesn't scan that one folder. That's why Otium keeps only its downloaded miners there and checks each one against its fingerprint when it downloads it. Never allow your whole drive or your Downloads folder."),
        ("Do I need to restore the files my antivirus removed?", "No. Once the folder is allowed, press Try again in Otium: it downloads fresh copies of the miners and checks them again."),
    ];

    public static string Antivirus(IEnumerable<string> legalLinks, string baseUrl)
    {
        var vendors = string.Concat(AntivirusSteps.Select(v =>
            $"""<details id="av-{v.Id}"><summary>{E(v.Name)}</summary><div class="install-body"><ol>{v.StepsHtml}<li>Back in Otium, press <strong>Try again</strong>.</li></ol><p class="sum-intro"><a href="{E(v.HelpUrl)}" rel="nofollow">{E(v.Name)}'s own help</a></p></div></details>"""));
        var body = $"""
                <div class="glow-frame" aria-hidden="true"><div class="glow"></div></div>
                <section class="page stacked">
                  <div class="wrap">
                    <nav class="crumbs" aria-label="Breadcrumb"><a href="/">Otium</a><span aria-hidden="true">/</span><span aria-current="page">Antivirus help</span></nav>
                    <div class="panel">
                      <span class="label">Help</span>
                      <h1 class="mt-14">Your antivirus flagged the miner. Here's why, and the safe fix.</h1>
                      <p class="lead">Antivirus tools flag every crypto miner, often as <span class="code">Trojan:Win32/CoinMiner</span>, because criminals hide the same open-source miners on other people's computers. The fix is to allow one folder, not to turn your antivirus off.</p>
                    </div>
                    <div class="panel">
                      <h2>Why it happens</h2>
                      <p>Otium earns with two open-source miners: XMRig for your processor and SRBMiner for your graphics card. It downloads their official releases from GitHub, checks each file against its published SHA-256 fingerprint, keeps them in one folder, and only runs them once you've set Otium up and turned it on.</p>
                      <p>Antivirus tools can't tell a miner you chose from one hidden by malware, so they flag them all. The warning is about what the program is, not something found inside it.</p>
                    </div>
                    <div class="panel" id="folder">
                      <h2>The one folder to allow</h2>
                      <p>Otium keeps everything it downloads in its engines folder, so that's the only exception you need:</p>
                      <dl class="card-facts paths">
                        <div><dt>Windows</dt><dd class="path">%LOCALAPPDATA%\Otium\engines</dd></div>
                        <div><dt>macOS</dt><dd class="path">~/Library/Application Support/Otium/engines</dd></div>
                        <div><dt>Linux</dt><dd class="path">~/.local/share/Otium/engines</dd></div>
                      </dl>
                      <p>When your antivirus blocks a miner, Otium tells you and shows this folder's full path. On Windows, %LOCALAPPDATA% is <span class="code">C:\Users\<i>you</i>\AppData\Local</span>.</p>
                    </div>
                    <div class="panel" id="defender">
                      <h2>Microsoft Defender (Windows Security)</h2>
                      <p><strong>The quick way:</strong> in Otium, press <strong>Allow automatically</strong>. Windows asks for your permission, then Otium adds its engines folder, and nothing else, as a Defender exclusion and downloads fresh copies of the miners.</p>
                      <p><strong>By hand:</strong></p>
                      <div class="install-body"><ol>
                        <li>Open <strong>Windows Security</strong> and choose <strong>Virus &amp; threat protection</strong>.</li>
                        <li>Under Virus &amp; threat protection settings, choose <strong>Manage settings</strong>.</li>
                        <li>Under <strong>Exclusions</strong>, choose <strong>Add or remove exclusions</strong>, then <strong>Add an exclusion</strong> and <strong>Folder</strong>.</li>
                        <li>Pick Otium's engines folder. Windows asks for administrator permission.</li>
                        <li>Back in Otium, press <strong>Try again</strong>.</li>
                      </ol></div>
                      <p class="muted">Leave <strong>Potentially unwanted app blocking</strong> on: the folder exclusion is enough. If Windows Firewall asks about SRBMiner afterwards, choose Cancel: it only needs outgoing connections. <a href="https://support.microsoft.com/en-us/windows/add-an-exclusion-to-windows-security-811816c0-4dfd-af4a-47e4-c301afe13b26" rel="nofollow">Microsoft's own help</a></p>
                    </div>
                    <div class="panel" id="others">
                      <h2>Other antivirus</h2>
                      <p>Otium can't change other antivirus products' settings, so add the folder yourself. Each one calls it something different: exceptions, exclusions or an allow list.</p>
                      <div class="install">{vendors}</div>
                    </div>
                    <div class="panel">
                      <h2>Questions</h2>
                      <div class="faq">{string.Concat(AntivirusFaq.Select(q => $"<details><summary>{E(q.Question)}</summary><p>{q.AnswerHtml}</p></details>"))}</div>
                    </div>
                  </div>
                </section>
            """;
        var root = baseUrl.TrimEnd('/');
        return Layout("Antivirus flags Trojan:Win32/CoinMiner? How to allow Otium",
            "Why antivirus tools flag miners like XMRig as Trojan:Win32/CoinMiner, and how to allow just Otium's folder in Defender, Avast, Norton, ESET and more.",
            body, legalLinks, "/download", baseUrl, "/antivirus",
            head: JsonLd(Breadcrumbs(root, ("Otium", "/"), ("Antivirus help", "/antivirus")), FaqPage(AntivirusFaq)));
    }
}
