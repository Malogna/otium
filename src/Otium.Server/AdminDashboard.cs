namespace Otium.Server;

/// <summary>
/// The operator dashboard at <c>/admin</c>: a static page that asks for the admin token and then reads and acts on the
/// token-protected <c>/admin/*</c> API from the browser. The page itself holds no data, so serving it without the token
/// reveals nothing; the token stays in the tab's session storage and travels only as the <c>X-Admin-Token</c> header.
/// Data is written with <c>textContent</c> only, and a strict content security policy allows no inline script.
/// </summary>
internal static class AdminDashboard
{
    /// <summary>No inline script or style, no framing, nothing loaded from elsewhere.</summary>
    public const string ContentSecurityPolicy =
        "default-src 'none'; script-src 'self'; style-src 'self'; connect-src 'self'; img-src 'self' data:; " +
        "base-uri 'none'; form-action 'none'; frame-ancestors 'none'";

    public static void AddSecurityHeaders(HttpResponse response)
    {
        response.Headers.ContentSecurityPolicy = ContentSecurityPolicy;
        response.Headers.XFrameOptions = "DENY";
        response.Headers.XContentTypeOptions = "nosniff";
        response.Headers["Referrer-Policy"] = "no-referrer";
        response.Headers["X-Robots-Tag"] = "noindex, nofollow";
        response.Headers.CacheControl = "no-store";
    }

    public const string Html = """
        <!doctype html>
        <html lang="en">
        <head>
          <meta charset="utf-8">
          <meta name="viewport" content="width=device-width, initial-scale=1">
          <meta name="robots" content="noindex, nofollow">
          <title>Otium operator</title>
          <link rel="stylesheet" href="/admin/dashboard.css">
          <script src="/admin/dashboard.js" defer></script>
        </head>
        <body>
          <main>
            <header>
              <h1>Otium operator</h1>
              <div class="actions" id="session" hidden>
                <span class="muted" id="updated"></span>
                <label class="muted toggle"><input type="checkbox" id="auto" checked> every minute</label>
                <button type="button" id="refresh">Refresh</button>
                <button type="button" id="signout" class="quiet">Sign out</button>
              </div>
            </header>

            <section class="card" id="signin">
              <h2>Sign in</h2>
              <p class="muted">Enter the server's <code>Admin:Token</code>. It's kept only in this tab and sent as a header.</p>
              <div class="row">
                <input type="password" id="token" autocomplete="current-password" placeholder="Admin token" aria-label="Admin token">
                <button type="button" id="signin-button" class="primary">Sign in</button>
              </div>
              <p class="error" id="signin-error" role="alert"></p>
            </section>

            <div id="dashboard" hidden>
              <p class="error" id="error" role="alert"></p>

              <div id="noncustodial" hidden>
                <section>
                  <div class="section-head">
                    <h2>Users and computers</h2>
                    <span class="muted">a user is an account; a computer is one install of the app</span>
                  </div>
                  <div class="tiles">
                    <div class="tile"><span class="label">Users</span><strong id="uc-users"></strong><span class="muted" id="uc-users-detail"></span></div>
                    <div class="tile"><span class="label">Active users, 24 hours</span><strong id="uc-active"></strong><span class="muted" id="uc-active-detail"></span></div>
                    <div class="tile"><span class="label">Computers online now</span><strong id="act-now"></strong><span class="muted" id="act-now-detail"></span></div>
                    <div class="tile"><span class="label">Computers mining now</span><strong id="uc-mining"></strong><span class="muted" id="uc-mining-detail"></span></div>
                    <div class="tile"><span class="label">Still using Otium</span><strong id="uc-retention"></strong><span class="muted" id="uc-retention-detail"></span></div>
                    <div class="tile"><span class="label">Computers per user</span><strong id="uc-per-user"></strong><span class="muted" id="uc-per-user-detail"></span></div>
                  </div>
                  <h3>Last 30 days (UTC), with today's number</h3>
                  <div class="charts" id="uc-charts"></div>
                  <p class="muted" id="uc-counting"></p>
                  <h3>App versions on computers seen in the last 7 days</h3>
                  <ul class="addresses" id="uc-versions"></ul>
                </section>

                <section>
                  <h2>What users earned</h2>
                  <div class="tiles">
                    <div class="tile"><span class="label">Paid to users by pools</span><strong id="pp-paid"></strong><span class="muted" id="pp-paid-detail"></span></div>
                    <div class="tile"><span class="label">Users earned, 30 days</span><strong id="pp-rewarded"></strong><span class="muted">at their pools, before payout</span></div>
                  </div>
                </section>

                <section>
                  <h2>Otium on unMineable</h2>
                  <p class="muted">Non-custodial: pools pay users directly. Otium earns its fee slices and unMineable's referral share, both paid to <span class="mono" id="nc-target"></span>.</p>
                  <div class="tiles">
                    <div class="tile"><span class="label">At unMineable</span><strong id="nc-balance"></strong><span class="muted" id="nc-balance-detail"></span></div>
                    <div class="tile"><span class="label">Rewarded</span><strong id="nc-rewarded"></strong><span class="muted" id="nc-rewarded-detail"></span></div>
                    <div class="tile"><span class="label">Paid to Otium</span><strong id="nc-paid"></strong><span class="muted" id="nc-paid-detail"></span></div>
                    <div class="tile"><span class="label">Referral code</span><strong id="nc-code"></strong><span class="muted" id="nc-code-detail"></span></div>
                  </div>
                  <p class="error" id="nc-error"></p>
                  <p><a id="nc-link" target="_blank" rel="noopener noreferrer">Open on unMineable</a></p>
                </section>
                <section>
                  <h2>Share analytics, last 7 days</h2>
                  <p class="muted" id="an-summary"></p>
                  <div class="table-wrap">
                    <table id="an-table" hidden>
                      <thead><tr><th>Part</th><th>Computers</th><th>Hours</th><th>Earned by users</th><th>Electricity</th><th>Per hour</th></tr></thead>
                      <tbody id="an-rows"></tbody>
                    </table>
                  </div>
                </section>

                <section>
                  <h2>Invites and payout coins</h2>
                  <div class="tiles">
                    <div class="tile"><span class="label">Joined by invite</span><strong id="nc-referred"></strong><span class="muted" id="nc-verified"></span></div>
                  </div>
                  <h3>Payout coins</h3>
                  <ul class="addresses" id="nc-coins"></ul>
                </section>

                <section>
                  <h2>Pearl</h2>
                  <p id="pearl"></p>
                  <p class="muted">Pearl's FP8 fork (PIP-3) limits Pearl to Hopper and Blackwell cards once active; the GPU policy then stops offering it to other cards.</p>
                </section>

                <section id="myst" hidden>
                  <h2>Mysterium sponsoring</h2>
                  <p id="myst-detail"></p>
                </section>
              </div>

              <div id="custodial">

              <section>
                <h2>Money</h2>
                <div class="tiles">
                  <div class="tile"><span class="label">Credited to users</span><strong id="credited"></strong><span class="muted">earnings after the fee, all sources</span></div>
                  <div class="tile"><span class="label">Referral rewards</span><strong id="referral"></strong><span class="muted">paid out of the fee</span></div>
                  <div class="tile"><span class="label">Paid out</span><strong id="paid"></strong><span class="muted">cash-outs marked paid</span></div>
                  <div class="tile"><span class="label">Owed to users</span><strong id="owed"></strong><span class="muted" id="owed-detail">balances, held or available</span></div>
                </div>
                <div class="bar" aria-hidden="true"><div id="paid-bar"></div></div>
                <p class="muted" id="paid-share"></p>
              </section>

              <section>
                <h2>Payouts</h2>
                <div class="tiles">
                  <div class="tile"><span class="label">Bitcoin treasury</span><strong id="treasury"></strong><span class="muted" id="treasury-detail"></span></div>
                  <div class="tile"><span class="label">Sanctions screening</span><strong id="sanctions"></strong><span class="muted">OFAC addresses loaded</span></div>
                </div>
                <ul class="addresses" id="methods"></ul>
              </section>

              <section>
                <h2>Check against the pools</h2>
                <table class="compare">
                  <thead><tr><th>Source</th><th>Pool or partner says</th><th>Credited to users</th><th>Difference</th></tr></thead>
                  <tbody>
                    <tr><td>MoneroOcean (CPU and GPU)</td><td id="pool-earned"></td><td id="mo-credited"></td><td id="mo-margin"></td></tr>
                    <tr><td>Kryptex (GPU coins)</td><td class="muted">see the Kryptex dashboard</td><td id="kx-credited"></td><td></td></tr>
                    <tr><td>Internet sharing</td><td class="muted">see the partner's statement</td><td id="bw-credited"></td><td></td></tr>
                  </tbody>
                </table>
                <p class="muted">Pool figures are gross (paid plus pending) at today's XMR price; credits are after Otium's fee, so a healthy margin is roughly the fee.</p>
              </section>

              <section>
                <div class="section-head">
                  <h2 id="queue-title">Cash-outs waiting for review</h2>
                  <label class="muted">Show
                    <select id="status">
                      <option value="review">Waiting for review</option>
                      <option value="queued">Queued</option>
                      <option value="sending">Sending</option>
                      <option value="paid">Paid</option>
                      <option value="failed">Failed or rejected</option>
                    </select>
                  </label>
                </div>
                <p class="muted" id="queue-empty" hidden>Nothing here.</p>
                <div class="table-wrap">
                  <table id="queue" hidden>
                    <thead><tr><th>Requested</th><th>Amount</th><th>Method and destination</th><th>Account</th><th id="note-head">Why it's held</th><th></th></tr></thead>
                    <tbody id="queue-rows"></tbody>
                  </table>
                </div>
              </section>

              <section>
                <h2>Pool addresses</h2>
                <ul class="addresses" id="addresses"></ul>
              </section>
              </div>

              <section>
                <div class="section-head">
                  <h2 id="pr-title">Problem reports</h2>
                  <span class="muted">newest first; open one to read what the app sent</span>
                </div>
                <p class="muted" id="pr-empty" hidden>No problem reports yet.</p>
                <div class="table-wrap">
                  <table id="pr-table" hidden>
                    <thead><tr><th>Sent</th><th>Reference</th><th>Version</th><th>System</th><th>What happened</th><th></th></tr></thead>
                    <tbody id="pr-rows"></tbody>
                  </table>
                </div>
              </section>
            </div>
          </main>
        </body>
        </html>
        """;

    public const string Css = """
        :root { --bg:#f3f5f8; --fg:#14171c; --muted:#5b6575; --card:#fff; --border:#e3e6eb; --accent:#16a672; --good:#138a5e; --bad:#b42318; --warn:#9a6200; --soft:#f0f3f6; }
        @media (prefers-color-scheme: dark) { :root { --bg:#0b0e13; --fg:#e8ebef; --muted:#8d98aa; --card:#141922; --border:#262c35; --accent:#3ddc97; --good:#46d394; --bad:#f47066; --warn:#f2b544; --soft:#1a202a; } }
        * { box-sizing:border-box; }
        [hidden] { display:none !important; }
        body { margin:0; font:15px/1.5 system-ui,-apple-system,"Segoe UI",sans-serif; background:var(--bg); color:var(--fg); }
        main { max-width:1120px; margin:0 auto; padding:24px 16px 64px; }
        header { display:flex; align-items:center; justify-content:space-between; gap:12px; flex-wrap:wrap; margin-bottom:16px; }
        h1 { font-size:22px; margin:0; }
        h2 { font-size:17px; margin:0 0 12px; }
        section { background:var(--card); border:1px solid var(--border); border-radius:14px; padding:20px; margin-bottom:16px; }
        .actions, .row, .section-head { display:flex; align-items:center; gap:8px; flex-wrap:wrap; }
        .section-head { justify-content:space-between; margin-bottom:8px; }
        .section-head h2 { margin:0; }
        .muted { color:var(--muted); font-size:13px; }
        .error { color:var(--bad); min-height:1em; margin:8px 0 0; }
        .error:empty { display:none; }
        code { font-family:ui-monospace,Consolas,monospace; background:var(--soft); padding:1px 5px; border-radius:5px; }
        input, select, button { font:inherit; color:inherit; }
        input, select { background:var(--soft); border:1px solid var(--border); border-radius:8px; padding:8px 10px; }
        input { min-width:260px; flex:1; }
        button { background:var(--soft); border:1px solid var(--border); border-radius:8px; padding:7px 14px; cursor:pointer; }
        button:hover { border-color:var(--muted); }
        button:disabled { opacity:.5; cursor:default; }
        button.primary, button.approve { background:var(--accent); border-color:var(--accent); color:#fff; font-weight:600; }
        button.reject { color:var(--bad); }
        button.quiet { background:transparent; }
        .tiles { display:grid; grid-template-columns:repeat(auto-fit,minmax(200px,1fr)); gap:12px; }
        .tile { background:var(--soft); border-radius:10px; padding:14px; display:flex; flex-direction:column; gap:2px; }
        .tile strong { font-size:24px; font-variant-numeric:tabular-nums; }
        .label { font-size:13px; font-weight:600; }
        .bar { height:8px; background:var(--soft); border-radius:99px; overflow:hidden; margin:16px 0 6px; }
        .bar div { height:100%; width:0; background:var(--accent); }
        .table-wrap { overflow-x:auto; }
        table { width:100%; border-collapse:collapse; font-size:14px; }
        th { text-align:left; font-size:12px; color:var(--muted); font-weight:600; padding:6px 8px; border-bottom:1px solid var(--border); }
        td { padding:10px 8px; border-bottom:1px solid var(--border); vertical-align:top; }
        td.num { font-variant-numeric:tabular-nums; white-space:nowrap; }
        .good { color:var(--good); }
        .bad { color:var(--bad); }
        .mono { font-family:ui-monospace,Consolas,monospace; font-size:12px; word-break:break-all; }
        .flag { display:inline-block; font-size:12px; color:var(--warn); }
        .buttons { display:flex; gap:6px; white-space:nowrap; }
        .addresses { margin:0; padding-left:18px; }
        h3 { font-size:13px; font-weight:600; color:var(--muted); margin:18px 0 8px; }
        .toggle { display:inline-flex; align-items:center; gap:6px; }
        .toggle input { min-width:0; flex:none; }
        .chart { display:flex; align-items:flex-end; gap:4px; height:72px; }
        .chart div { flex:1; min-height:2px; background:var(--accent); border-radius:3px 3px 0 0; opacity:.85; }
        .chart div.zero { background:var(--border); }
        .chart-axis { display:flex; justify-content:space-between; font-size:12px; color:var(--muted); margin-top:4px; }
        .charts { display:grid; grid-template-columns:repeat(auto-fit,minmax(280px,1fr)); gap:12px; }
        .chart-card { margin:0; background:var(--soft); border-radius:10px; padding:12px 14px; min-width:0; }
        .chart-card figcaption { display:flex; justify-content:space-between; align-items:baseline; gap:8px; font-size:13px; font-weight:600; margin-bottom:8px; }
        .chart-card figcaption strong { font-size:18px; font-variant-numeric:tabular-nums; }
        .chart-card .chart { gap:2px; height:64px; }
        tr.details td { background:var(--soft); }
        pre.report { margin:0; max-height:420px; overflow:auto; white-space:pre-wrap; word-break:break-word; font:12px/1.45 ui-monospace,Consolas,monospace; }
        @media (max-width:640px) { input { min-width:0; } }
        """;

    public const string Script = """
        "use strict";
        (() => {
          const $ = id => document.getElementById(id);
          const KEY = "otium-admin-token";
          let token = sessionStorage.getItem(KEY) || "";

          const usd = v => v == null ? "–" : "$" + Number(v).toLocaleString("en-US", { minimumFractionDigits: 2, maximumFractionDigits: 2 });
          const micros = m => usd(Number(m) / 1e6);
          const when = s => s ? new Date(s).toLocaleString(undefined, { dateStyle: "medium", timeStyle: "short" }) : "";
          const el = (tag, text, cls) => { const e = document.createElement(tag); if (text != null) e.textContent = text; if (cls) e.className = cls; return e; };

          async function api(path, options = {}) {
            const response = await fetch(path, {
              ...options,
              headers: { "X-Admin-Token": token, ...(options.body ? { "Content-Type": "application/json" } : {}) },
              cache: "no-store",
            });
            if (response.status === 401) { signOut("That token isn't right."); throw new Error("unauthorized"); }
            if (response.status === 429) throw new Error("Too many requests. Wait a minute and refresh.");
            if (!response.ok) throw new Error(`${path} failed (${response.status})`);
            const text = await response.text();
            return text ? JSON.parse(text) : null;
          }

          function showSignedIn(signedIn) {
            $("signin").hidden = signedIn;
            $("dashboard").hidden = !signedIn;
            $("session").hidden = !signedIn;
          }

          function signOut(message) {
            token = "";
            sessionStorage.removeItem(KEY);
            showSignedIn(false);
            $("signin-error").textContent = message || "";
            $("token").value = "";
            $("token").focus();
          }

          async function signIn() {
            token = $("token").value.trim();
            if (!token) return;
            try {
              await api("/admin/summary");
              sessionStorage.setItem(KEY, token);
              $("signin-error").textContent = "";
              showSignedIn(true);
              await refresh();
            } catch (e) {
              if (e.message !== "unauthorized") $("signin-error").textContent = e.message;
            }
          }

          function renderSummary(s) {
            $("credited").textContent = usd(s.creditedToUsersUsd);
            $("referral").textContent = usd(s.referralRewardsUsd);
            $("paid").textContent = usd(s.paidOutUsd);
            $("owed").textContent = usd(Number(s.owedToUsersUsd) + Number(s.inOpenCashoutsUsd || 0));
            $("owed-detail").textContent = Number(s.inOpenCashoutsUsd) > 0
              ? `${usd(s.owedToUsersUsd)} in balances and ${usd(s.inOpenCashoutsUsd)} in cash-outs not yet paid`
              : "balances, held or available";
            const total = Number(s.creditedToUsersUsd) + Number(s.referralRewardsUsd);
            const share = total > 0 ? Math.min(100, Number(s.paidOutUsd) / total * 100) : 0;
            $("paid-bar").style.width = share.toFixed(1) + "%";
            $("paid-share").textContent = total > 0
              ? `${share.toFixed(1)}% of everything credited (${usd(total)}, including referral rewards) has been paid out.`
              : "Nothing credited yet.";

            if (s.poolError) {
              $("pool-earned").textContent = "unavailable: " + s.poolError;
              $("pool-earned").className = "bad";
              $("mo-margin").textContent = "";
            } else {
              $("pool-earned").textContent = `${Number(s.poolEarnedXmr).toFixed(6)} XMR ≈ ${usd(s.poolEarnedUsdAtTodaysPrice)}`;
              $("pool-earned").className = "";
              const margin = Number(s.poolEarnedUsdAtTodaysPrice) - Number(s.moneroOceanCreditedToUsersUsd);
              $("mo-margin").textContent = (margin >= 0 ? "+" : "−") + usd(Math.abs(margin));
              $("mo-margin").className = margin >= 0 ? "num good" : "num bad";
            }
            if (!s.treasuryConfigured) {
              $("treasury").textContent = "not set up";
              $("treasury-detail").textContent = "set BitcoinWallet:RpcUrl to pay crypto automatically";
            } else if (s.treasuryError) {
              $("treasury").textContent = "unavailable";
              $("treasury-detail").textContent = s.treasuryError;
            } else {
              $("treasury").textContent = `${Number(s.treasuryBtc).toFixed(8)} BTC`;
              const open = Number(s.inOpenCashoutsUsd || 0);
              const low = s.treasuryUsd != null && Number(s.treasuryUsd) < open;
              $("treasury-detail").textContent = `≈ ${usd(s.treasuryUsd)}` + (low ? ` — less than the ${usd(open)} of open cash-outs: top it up` : "");
              $("treasury-detail").className = low ? "bad" : "muted";
            }
            $("sanctions").textContent = Number(s.sanctionedAddressesLoaded || 0).toLocaleString("en-US");
            $("methods").replaceChildren(...(s.payoutMethods || []).map(m => {
              const li = el("li");
              li.append(el("span", `${m.name}: `), el("span", m.available ? `on, from ${usd(m.minUsd)}` : `paused (${m.unavailableReason})`, m.available ? "good" : "bad"));
              return li;
            }));

            $("mo-credited").textContent = usd(s.moneroOceanCreditedToUsersUsd);
            $("kx-credited").textContent = usd(s.kryptexCreditedToUsersUsd);
            $("bw-credited").textContent = `${usd(s.bandwidthCreditedToUsersUsd)} for ${Number(s.bandwidthCreditedGb).toFixed(3)} GB`;

            const list = $("addresses");
            list.replaceChildren(...(s.poolAddresses || []).map(a => {
              const li = el("li");
              li.append(el("span", a.address, "mono"), el("span", ` · ${a.accounts} accounts`, "muted"));
              return li;
            }));
            if (!list.children.length) list.append(el("li", "No pool addresses yet.", "muted"));
          }

          function renderQueue(status, cashouts) {
            const titles = { review: "Cash-outs waiting for review", queued: "Queued cash-outs", sending: "Cash-outs being sent", paid: "Paid cash-outs", failed: "Failed or rejected cash-outs" };
            $("queue-title").textContent = `${titles[status] || "Cash-outs"} (${cashouts.length})`;
            $("note-head").textContent = status === "review" ? "Why it's held" : "Note";
            $("queue").hidden = cashouts.length === 0;
            $("queue-empty").hidden = cashouts.length !== 0;
            $("queue-rows").replaceChildren(...cashouts.map(c => row(status, c)));
          }

          function row(status, c) {
            const tr = el("tr");
            tr.append(el("td", when(c.createdAt)));
            tr.append(el("td", micros(c.micros), "num"));

            const where = el("td");
            const names = { btc: "Bitcoin", eth: "Ether", bnb: "BNB (BNB Smart Chain)", sol: "Solana", xmr: "Monero", paypal: "PayPal" };
            where.append(el("div", names[c.method] || c.method), el("div", c.destination, "mono"));
            if (c.otherAccountsUsingDestination > 0)
              where.append(el("div", `⚠ also used by ${c.otherAccountsUsingDestination} other account(s)`, "flag"));
            tr.append(where);

            const account = el("td");
            account.append(el("div", c.accountEmail || "no email"));
            account.append(el("div", `joined ${when(c.accountCreatedAt)}`, "muted"));
            account.append(el("div", `earned ${usd(c.lifetimeEarnedUsd)} · paid ${usd(c.paidOutUsd)}`, "muted"));
            account.append(el("div", c.accountId, "mono muted"));
            tr.append(account);

            const why = el("td", c.note || "");
            if (c.providerReference) why.append(el("div", c.providerReference, "mono muted"));
            tr.append(why);

            const actions = el("td");
            const buttons = el("div", null, "buttons");
            if (status === "review") buttons.append(button("Approve", "approve", () => act(c, "approve", null)));
            if (status === "review" || status === "queued") {
              buttons.append(button("Reject", "reject", () => {
                const note = prompt("Why is it rejected? The amount goes back to the user's balance and they see this note.", "rejected");
                if (note !== null) act(c, "reject", note);
              }));
            }
            if (status === "review" || status === "queued" || status === "sending") {
              buttons.append(button("Mark paid", "", () => {
                const note = prompt("Transaction id of the payment you made by hand. The user gets an email receipt.", "");
                if (note !== null) act(c, "mark-paid", note || "manual");
              }));
            }
            actions.append(buttons);
            tr.append(actions);
            return tr;
          }

          function button(text, cls, onClick) {
            const b = el("button", text, cls);
            b.type = "button";
            b.addEventListener("click", onClick);
            return b;
          }

          async function act(c, action, note) {
            const verb = { approve: "Approve", reject: "Reject", "mark-paid": "Mark as paid" }[action];
            if (action === "approve" && !confirm(`${verb} ${micros(c.micros)} to ${c.destination}? It will be sent automatically.`)) return;
            try {
              await api(`/admin/cashouts/${encodeURIComponent(c.id)}/${action}`, { method: "POST", body: JSON.stringify({ note }) });
              await refresh();
            } catch (e) {
              if (e.message !== "unauthorized") $("error").textContent = `${verb} failed: ${e.message}. It may have changed already; refreshing.`;
              await refresh().catch(() => {});
            }
          }

          function renderNonCustodial(s) {
            const o = s.otium;
            const coin = o ? " " + o.coin : "";
            const amount = v => v == null ? "–" : Number(v).toLocaleString("en-US", { maximumFractionDigits: 6 }) + coin;
            $("nc-target").textContent = o ? o.target : "(not set)";
            $("nc-balance").textContent = o ? amount(o.balance) : "–";
            $("nc-balance-detail").textContent = o
              ? `${amount(o.feeMiningBalance)} from fee slices, ${amount(o.referralBalance)} from referrals; paid at ${amount(o.threshold)}`
                + (o.autoPay ? "" : " (auto-pay is OFF: turn it on at unMineable)")
              : "";
            $("nc-rewarded").textContent = o ? amount(o.rewardedPast24h) : "–";
            $("nc-rewarded-detail").textContent = o ? `past 24 hours; ${amount(o.rewardedPast30d)} in 30 days` : "";
            $("nc-paid").textContent = o ? amount(o.paid) : "–";
            $("nc-paid-detail").textContent = o && o.problems.length ? o.problems.join(" ") : "to Otium's own address";
            $("nc-code").textContent = o ? o.referralCode : "–";
            $("nc-code-detail").textContent = o ? `${o.referredMiners} miners using it` : "";
            $("nc-error").textContent = s.otiumError || "";
            const link = $("nc-link");
            link.hidden = !o;
            if (o) link.href = o.dashboardUrl;
            const a = s.activity || {};
            const n = v => Number(v || 0).toLocaleString("en-US");
            $("uc-users").textContent = n(a.accounts);
            $("uc-users-detail").textContent = `+${n(a.accountsNew24h)} in 24 h, +${n(a.accountsNew7d)} in 7 days`;
            $("uc-active").textContent = n(a.accountsActivePast24h);
            $("uc-active-detail").textContent = `with a computer online; ${n(a.accountsActivePast7d)} in 7 days, ${n(a.accountsActivePast30d)} in 30 days`;
            $("act-now").textContent = n(a.devicesActiveNow);
            $("act-now-detail").textContent = `app running in the last 15 minutes; ${n(a.devicesSeenPast24h)} in 24 h, ${n(s.devicesSeenPast7Days)} in 7 days, ${n(a.devicesSeenPast30d)} in 30 days, ${n(a.devices)} ever`;
            const mn = a.miningNow || {}, m24 = a.miningPast24h || {};
            $("uc-mining").textContent = n(mn.any);
            $("uc-mining-detail").textContent = `processor ${n(mn.cpu)}, graphics card ${n(mn.gpu)}; ${n(m24.any)} mined in 24 h`;
            const ret = a.retention || {};
            $("uc-retention").textContent = Number(ret.joinedOverAWeekAgo) ? Math.round(ret.seenPast7d / ret.joinedOverAWeekAgo * 100) + "%" : "–";
            $("uc-retention-detail").textContent = `${n(ret.seenPast7d)} of ${n(ret.joinedOverAWeekAgo)} computers that joined over a week ago were online this week`;
            $("uc-per-user").textContent = Number(a.accounts) ? (Number(a.devices) / Number(a.accounts)).toLocaleString("en-US", { maximumFractionDigits: 2 }) : "–";
            $("uc-per-user-detail").textContent = `${n(a.devices)} computers on ${n(a.accounts)} accounts`;

            const daily = a.daily || [];
            const charts = [
              ["Computers active", d => d.devices],
              ["Users active", d => d.accounts],
              ["Processors mining", d => d.cpuMining],
              ["Graphics cards mining", d => d.gpuMining],
              ["New computers", d => d.newDevices],
              ["New users", d => d.newAccounts],
            ];
            $("uc-charts").replaceChildren(...charts.map(([title, value]) => chart(title, daily, value)));
            const counted = daily.findIndex(d => Number(d.devices) > 0);
            $("uc-counting").textContent = counted > 0
              ? `Active and mining counts start on ${daily[counted].day}, when the server began counting them; new computers and users go back further.`
              : "";
            const versions = a.appVersions || [];
            $("uc-versions").replaceChildren(...(versions.length
              ? versions.map(v => el("li", `${v.version}: ${n(v.devices)} computer(s)`))
              : [el("li", "No computer was online in the last 7 days.")]));

            const pp = s.poolPayouts || {};
            $("pp-paid").textContent = usd(pp.paidUsd);
            $("pp-paid-detail").textContent = `${n(pp.paidAccounts)} account(s) paid at least once`;
            $("pp-rewarded").textContent = usd(pp.rewarded30dUsd);

            const an = s.analytics || { devices: 0, byKind: [] };
            const names = { cpu: "Processor", gpu: "Graphics card", sharing: "Internet sharing" };
            $("an-summary").textContent = an.devices
              ? `${n(an.devices)} computer(s) shared their numbers this week.`
              : "No computer has shared its numbers this week (Share analytics is opt-in).";
            $("an-table").hidden = !an.byKind.length;
            $("an-rows").replaceChildren(...an.byKind.map(k => {
              const tr = el("tr");
              const perHour = Number(k.hours) > 0 ? usd(Number(k.userUsd) / Number(k.hours)) : "–";
              [names[k.kind] || k.kind, n(k.devices), Number(k.hours).toLocaleString("en-US"), usd(k.userUsd), usd(k.electricityUsd), perHour]
                .forEach((v, i) => tr.append(el("td", v, i ? "num" : "")));
              return tr;
            }));

            const pearl = s.pearlFork;
            $("pearl").textContent = !pearl ? "Not watched on this server."
              : pearl.active ? `Fork active since block ${pearl.activationHeight}: only Hopper and Blackwell cards can mine Pearl.`
              : `Not active yet` + (pearl.height ? ` (block ${pearl.height}` + (pearl.activationHeight ? ` of ${pearl.activationHeight})` : ")") : "") + (pearl.reason ? `. ${pearl.reason}` : ".");
            const myst = s.mysterium;
            $("myst").hidden = !(myst && (myst.sponsoring || Number(myst.nodesSponsored) > 0));
            if (myst) $("myst-detail").textContent = `${n(myst.nodesSponsored)} node(s) sponsored, ${myst.mystSpent} MYST spent (${myst.mystSpentPast24h} in 24 h)` +
              (myst.walletError ? `; wallet: ${myst.walletError}` : myst.walletBalanceMyst != null ? `; wallet holds ${myst.walletBalanceMyst} MYST` : "");

            $("nc-referred").textContent = s.referredAccounts;
            $("nc-verified").textContent = `${s.verifiedReferrals} verified by their pool`;
            const coins = $("nc-coins");
            coins.replaceChildren(...s.accountsByCoin.map(c => el("li", `${c.coin === "none" ? "No payout address yet" : c.coin}: ${c.accounts}`)));
          }

          /** One small bar chart: a bar per day, today's number in the corner, the first and last day underneath. */
          function chart(title, days, value) {
            const values = days.map(d => Number(value(d)) || 0);
            const most = Math.max(1, ...values);
            const figure = el("figure", null, "chart-card");
            const caption = el("figcaption");
            caption.append(el("span", title), el("strong", values.length ? values[values.length - 1].toLocaleString("en-US") : "–"));
            const bars = el("div", null, "chart");
            bars.setAttribute("role", "img");
            bars.setAttribute("aria-label", `${title} per day, last ${days.length} days`);
            bars.append(...days.map((d, i) => {
              const bar = el("div", null, values[i] ? "" : "zero");
              bar.style.height = Math.max(3, values[i] / most * 100) + "%";
              bar.title = `${d.day}: ${values[i].toLocaleString("en-US")}`;
              return bar;
            }));
            const axis = el("div", null, "chart-axis");
            axis.append(el("span", days.length ? days[0].day : ""), el("span", days.length ? "today" : ""));
            figure.append(caption, bars, axis);
            return figure;
          }

          function renderProblemReports(reports) {
            $("pr-title").textContent = `Problem reports (${reports.length}${reports.length >= 50 ? "+" : ""})`;
            $("pr-table").hidden = !reports.length;
            $("pr-empty").hidden = reports.length > 0;
            $("pr-rows").replaceChildren(...reports.map(r => {
              const tr = el("tr");
              tr.append(el("td", when(r.createdAt)));
              const ref = el("td", null, "mono");
              ref.append(el("span", r.reference));
              tr.append(ref);
              tr.append(el("td", r.appVersion, "mono"));
              tr.append(el("td", r.platform));
              tr.append(el("td", r.note || "(no note)", r.note ? "" : "muted"));
              const actions = el("td");
              const buttons = el("div", null, "buttons");
              let open = null;
              const show = button("Open", "", async () => {
                if (open) { open.remove(); open = null; show.textContent = "Open"; return; }
                try {
                  const full = await api(`/admin/problem-reports/${encodeURIComponent(r.reference)}`);
                  open = el("tr", null, "details");
                  const td = el("td");
                  td.colSpan = 6;
                  td.append(el("div", `Account ${full.accountId} · computer ${full.deviceId}`, "muted mono"), el("pre", full.details || "(empty)", "report"));
                  open.append(td);
                  tr.after(open);
                  show.textContent = "Close";
                } catch (e) {
                  if (e.message !== "unauthorized") $("error").textContent = e.message;
                }
              });
              const copy = button("Copy reference", "quiet", async () => {
                try { await navigator.clipboard.writeText(r.reference); copy.textContent = "Copied"; } catch { copy.textContent = "Copy failed"; }
                setTimeout(() => { copy.textContent = "Copy reference"; }, 1500);
              });
              buttons.append(show, copy);
              actions.append(buttons);
              tr.append(actions);
              return tr;
            }));
          }

          async function refresh() {
            const status = $("status").value;
            $("refresh").disabled = true;
            try {
              const summary = await api("/admin/summary");
              const custodial = summary.custodial !== false;
              $("custodial").hidden = !custodial;
              $("noncustodial").hidden = custodial;
              if (custodial) {
                renderSummary(summary);
                renderQueue(status, await api(`/admin/cashouts?status=${encodeURIComponent(status)}`));
              } else {
                renderNonCustodial(summary);
              }
              // Problem reports in both modes; an open report stays open until the next manual refresh.
              if (!document.querySelector("tr.details")) renderProblemReports(await api("/admin/problem-reports"));
              $("updated").textContent = "Updated " + new Date().toLocaleTimeString();
              $("error").textContent = "";
            } catch (e) {
              if (e.message !== "unauthorized") $("error").textContent = e.message;
            } finally {
              $("refresh").disabled = false;
            }
          }

          document.addEventListener("DOMContentLoaded", () => {
            $("signin-button").addEventListener("click", signIn);
            $("token").addEventListener("keydown", e => { if (e.key === "Enter") signIn(); });
            $("refresh").addEventListener("click", () => { $("error").textContent = ""; refresh(); });
            $("signout").addEventListener("click", () => signOut(""));
            $("status").addEventListener("change", refresh);
            // Every minute while the tab is visible and auto-refresh is on (the API is rate-limited per address).
            setInterval(() => { if (token && $("auto").checked && document.visibilityState === "visible") refresh(); }, 60000);
            if (token) { showSignedIn(true); refresh(); } else { showSignedIn(false); $("token").focus(); }
          });
        })();
        """;
}
