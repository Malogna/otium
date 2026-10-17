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
                  <h2>Accounts</h2>
                  <div class="tiles">
                    <div class="tile"><span class="label">Computers seen</span><strong id="nc-devices"></strong><span class="muted">in the last 7 days</span></div>
                    <div class="tile"><span class="label">Joined by invite</span><strong id="nc-referred"></strong><span class="muted" id="nc-verified"></span></div>
                  </div>
                  <ul class="addresses" id="nc-coins"></ul>
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
            $("nc-devices").textContent = s.devicesSeenPast7Days;
            $("nc-referred").textContent = s.referredAccounts;
            $("nc-verified").textContent = `${s.verifiedReferrals} verified by their pool`;
            const coins = $("nc-coins");
            coins.replaceChildren(...s.accountsByCoin.map(c => el("li", `${c.coin === "none" ? "No payout address yet" : c.coin}: ${c.accounts}`)));
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
            if (token) { showSignedIn(true); refresh(); } else { showSignedIn(false); $("token").focus(); }
          });
        })();
        """;
}
