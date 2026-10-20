namespace Otium.Server;

/// <summary>
/// /pearl: what Pearl is, how its proof of useful work works, what it pays, and its risks. Pearl is new, so this answers
/// searches ("pearl coin mining", "pearlhash") that few sites cover yet. Facts are from Pearl's own site, code and papers,
/// the mining software's notes and the pools, checked 20 October 2026 and listed under Sources; where sources disagree
/// or a claim is disputed, the page says so.
/// </summary>
internal static partial class SitePages
{
    private const string PearlChecked = "20 October 2026";

    private static readonly (string Question, string AnswerHtml)[] PearlFaq =
    [
        ("Can my graphics card mine Pearl?", """NVIDIA cards with 8 GB of graphics memory or more can, and NVIDIA cards with tensor cores mine it best. The mining software also runs it on recent AMD cards. <a href="/mining">See what each card earns today</a>."""),
        ("How am I paid for mining Pearl?", "Through unMineable, which converts the Pearl your card mines and pays it to your wallet in the coin you choose, such as USDT or Bitcoin. You never need a Pearl wallet."),
        ("Does mining Pearl run AI models on my computer?", "No. Through a pool, your card multiplies the matrices the network hands it. It doesn't run anyone's AI model or see their data."),
        ("What happens to my card if Pearl changes its rules?", "Pearl's planned FP8 upgrade would only let whitelisted GPUs mine, and its specification names NVIDIA's data-centre chips. If that happens, Otium moves your card to the next coin that pays it the most, by itself."),
    ];

    public static string Pearl(IReadOnlyList<CardEarnings> earnings, IEnumerable<string> legalLinks, string baseUrl)
    {
        var top = MiningCardList.Select(c => (Card: c, PerDay: PearlPerDay(c, earnings))).Where(c => c.PerDay is not null)
            .OrderByDescending(c => c.PerDay).Take(3).ToList();
        var today = top.Count == 0
            ? """<p>Today's coin prices aren't available right now. <a href="/mining">The graphics card list</a> shows typical speeds and power.</p>"""
            : $"""<p>At today's price, after every fee and before electricity: {string.Join(", ", top.Select(t => $"""<a href="/mining/{t.Card.Slug}">{E(t.Card.Name)}</a> {Usd(t.PerDay!.Value)} a day"""))}. <a href="/mining">Every card</a></p>""";
        (string Label, string Url)[] sources =
        [
            ("Pearl Research Labs: the network", "https://pearlresearch.ai/network"),
            ("Pearl's INT8 whitepaper", "https://pearlresearch.ai/research/int-whitepaper"),
            ("Pearl's FP8 whitepaper (planned upgrade)", "https://pearlresearch.ai/Pearl_Whitepaper.pdf"),
            ("Proofs of Useful Work from Arbitrary Matrix Multiplication (arXiv)", "https://arxiv.org/abs/2504.09971"),
            ("A measurement study disputing Pearl's useful work (arXiv)", "https://arxiv.org/abs/2606.04819"),
            ("Pearl's source code and chain parameters (GitHub)", "https://github.com/pearl-research-labs/pearl"),
            ("SRBMiner-Multi releases", "https://github.com/doktor83/SRBMiner-Multi/releases"),
            ("Kryptex: how to mine Pearl", "https://pool.kryptex.com/en/articles/how-to-mine-pearl-en"),
            ("unMineable", "https://unmineable.com/coins/BTC"),
            ("Mining pools for Pearl", "https://miningpoolstats.stream/pearl"),
        ];
        var body = $"""
                <div class="glow-frame" aria-hidden="true"><div class="glow"></div></div>
                <section class="page stacked">
                  <div class="wrap">
                    <nav class="crumbs" aria-label="Breadcrumb"><a href="/">Otium</a><span aria-hidden="true">/</span><span aria-current="page">What is Pearl?</span></nav>
                    <div class="panel">
                      <span class="label">Pearl (PRL)</span>
                      <h1 class="mt-14">What is Pearl, and why does it pay gaming cards so well?</h1>
                      <p class="lead">Pearl is a cryptocurrency whose mining is matrix multiplication, the same arithmetic AI models run on. That's why NVIDIA graphics cards with tensor cores mine it well, and why it's the best-paying coin for most of them in October 2026.</p>
                      {today}
                    </div>
                    <div class="panel">
                      <h2>How its proof of useful work works</h2>
                      <p>Bitcoin-style mining burns power on hashes nobody needs. Pearl, made by Pearl Research Labs, replaces them with multiplying large matrices of 8-bit numbers. Random noise is added to the input matrices before the miner multiplies them, then removed again, so the real product can be recovered and the work can't be faked.</p>
                      <p>The design lets the same work be useful: Pearl's official miner can run inside vLLM, so a data centre serving a language model can mine Pearl with that work, and Together AI sells an AI endpoint priced partly by the Pearl mined on it. Independent researchers dispute how much useful AI work everyday mining does, though: one 2026 study found that the main mining software does no AI work and the network accepts random matrices.</p>
                      <p class="muted">Through a pool, your card multiplies the matrices the network hands it. It doesn't run anyone's AI model or see their data.</p>
                    </div>
                    <div class="panel">
                      <h2>The coin itself</h2>
                      <dl class="card-facts">
                        <div><dt>Launched</dt><dd>27 April 2026</dd></div>
                        <div><dt>Most there will be</dt><dd>2.1 billion PRL</dd></div>
                        <div><dt>Block time</dt><dd>About 3 minutes</dd></div>
                        <div><dt>Halvings</dt><dd>None<small>the reward shrinks a little every block</small></dd></div>
                      </dl>
                      <p>Half of all Pearl is mined by block 650,226, about four years after launch. Its software is open source, a fork of the btcd Bitcoin node.</p>
                    </div>
                    <div class="panel">
                      <h2>The risks, plainly</h2>
                      <ul>
                        <li><strong>It's new and volatile.</strong> Pearl traded around $1.65 in May 2026 and $0.14 in July. What a card earns moves with it, and the graphics card pages show today's figure.</li>
                        <li><strong>It trades on small exchanges</strong> with little volume, and no major exchange lists it yet.</li>
                        <li><strong>One company steers it.</strong> Pearl Research Labs schedules its upgrades, and there were four in its first four months, one of them an emergency fix.</li>
                        <li><strong>Gaming cards may lose it.</strong> A planned FP8 upgrade would only let whitelisted GPUs mine, and the specification names NVIDIA's data-centre Hopper and Blackwell chips. Otium watches for it and moves your card to the next best-paying coin when it happens.</li>
                      </ul>
                      <p>With Otium you're paid in the coin you choose, not Pearl: unMineable converts what your card mines as it goes, so you never hold Pearl yourself.</p>
                    </div>
                    <div class="panel">
                      <h2>Questions</h2>
                      <div class="faq">{string.Concat(PearlFaq.Select(q => $"<details><summary>{E(q.Question)}</summary><p>{q.AnswerHtml}</p></details>"))}</div>
                    </div>
                    <div class="panel">
                      <h2>Sources</h2>
                      <ul>{string.Concat(sources.Select(s => $"""<li><a href="{E(s.Url)}" rel="nofollow">{E(s.Label)}</a></li>"""))}</ul>
                      <p class="muted">Checked on {PearlChecked}. Otium isn't affiliated with Pearl Research Labs.</p>
                    </div>
                  </div>
                </section>
            """;
        var root = baseUrl.TrimEnd('/');
        return Layout("What is Pearl (PRL)? Proof of useful work mining explained | Otium",
            "Pearl (PRL) is a proof-of-useful-work coin mined by matrix multiplication, which NVIDIA tensor cores do best. How it works, what it pays gaming cards today, its supply and its risks.",
            body, legalLinks, "/download", baseUrl, "/pearl",
            head: JsonLd(Breadcrumbs(root, ("Otium", "/"), ("What is Pearl?", "/pearl")), FaqPage(PearlFaq)));
    }
}
