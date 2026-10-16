# Otium

*Latin for "idle time".* Otium is a desktop app for Windows, macOS and Linux that brings the different ways a computer can earn
while nobody is using it (CPU and GPU mining, internet sharing, and more to come) into one place, set up and switched
automatically, as close to one click as possible. It's for people with some tech knowledge: users cash out in crypto
(BTC, ETH, BNB or SOL) to their own wallet.

| First launch | What this PC could earn | Overview | Advanced |
|---|---|---|---|
| ![Welcome](docs/welcome.png) | ![Estimate](docs/estimate.png) | ![Overview](docs/home.png) | ![Advanced](docs/advanced.png) |

<sub>Dev mode at real-world speed: simulated i7-8700K and RTX 3080 with electricity at $0.15/kWh, where mining doesn't pay. Otium says so plainly instead of promising money.</sub>

## For users: one button

1. Install and open Otium. Three short screens explain it, show what this computer could earn per month (from its
   processor and graphics card, today's payouts and your electricity price), and summarise what will happen.
2. Press **Start earning**. No settings are needed to start; you only need a wallet address when you cash out.
3. Otium starts when you sign in and earns only while you're away from the computer, after 3 minutes without keyboard or mouse input. It pauses the moment you come back, and also when electricity would cost more than it earns (the price is estimated from your country; in Norway, Sweden, Denmark and Finland it follows live hourly spot prices, from hvakosterstrommen.no, elprisetjustnu.se, elprisenligenu.dk and sahkotin.fi).
4. Your balance grows in dollars. Add your email when Otium asks; that keeps the balance safe if you reinstall, lets you
   sign in on your other PCs (they all earn into one balance), and is needed before the first cash-out.
5. Press **Cash out**, pick BTC, ETH, BNB or SOL and paste your wallet address (checked as you type, including the
   network). The overview lists every cash-out with its status in plain words (sending, swapping, being checked, paid,
   returned to balance) and a link to the transaction, and an email receipt arrives when it's paid.
6. Share your invite code. Friends mine fee-free for 30 days, and you get 5% of everything they earn. The Invites page
   has ready-made messages (short, explained, for gamers) that you can edit, one-click sharing to WhatsApp, Discord and
   email, and a monthly leaderboard of top inviters, counted by friends who actually started earning.
7. Optionally, earn from your internet connection too: the overview suggests it once, explains in plain words what
   sharing unused bandwidth means, and turns it on only when you agree. It pays into the same balance.

The overview shows, at a glance: the balance and progress to the first cash-out (with an ETA), what each device is
doing right now in plain words ("Mining Monero", "Waiting for you to step away · starts in 1:42"), what you can expect
today, this week and this month, and an hour-by-hour chart of the last 24 hours. Projections use how many hours a day
this computer actually earns once there is a day of history.

Everything technical lives under **Advanced**:
- Details per device, including GPU algorithm and speed, power draw, and profit per day.
- CPU usage, the idle delay, your electricity price, and switching devices on or off.
- Internet sharing: on or off, only while away, and a daily data limit.
- Getting paid by the pool straight to your own Monero wallet instead of an Otium balance.
- Updates.

## How it works

```
 desktop app (Avalonia, .NET 10)                         Otium server (ASP.NET Core + SQLite)
 ┌──────────────────────────────┐   register / balance   ┌─────────────────────────────────────┐
 │ CPU: XMRig (RandomX)         │ ─────────────────────▶ │ accounts, worker ids, pool addresses │
 │ GPU: SRBMiner, best algorithm│ ◀───────────────────── │ ledger (USD), referrals              │
 │ idle detection, power meter  │   cash-out requests    │ earnings creditor  (every 10 min)    │
 └──────────────┬───────────────┘                        │ cash-out processor (every minute)    │
                │ hashes, worker = account id            └───────┬──────────────────┬───────────┘
                ▼                                                │ pool stats       │ payouts
        MoneroOcean pool  ── pays XMR ──▶ operator wallet ◀──────┘                  ▼
                                       Bitcoin treasury (bitcoind) ── BTC, or swapped via THORChain ──▶ users' wallets
```

**Mining**
- The CPU runs XMRig (RandomX). For balance accounts the **server decides the pool** and sends it with the account
  summary, so moving users needs no app release: MoneroOcean (pays XMR to the operator's wallet) by default, or
  Monero on **Kryptex Pool** under the operator's Kryptex account (`account.worker`, auto-exchanged to BTC, PPS+ 1%).
  `CpuMining:KryptexPercent` moves a stable share of accounts (a canary, by a hash of the worker id) and 100 moves
  everyone; raising it only ever adds accounts. Kryptex CPU work is credited at the same Monero network value as
  MoneroOcean work (block reward ÷ difficulty × price, minus the pool fee), so a move doesn't change what a user earns.
  Own-wallet users always mine to their own address on MoneroOcean.
- The GPU runs SRBMiner on whichever algorithm pays most after miner and pool fees and measured electricity. Which
  algorithms are allowed, on which pool and at what rate comes from the server's **GPU policy**
  (`GET /api/v1/gpu-policy`, `src/Otium.Server/Gpu`), rebuilt every 10 minutes from live feeds:
  - **Pearl and Quantus** (primary) are mined on **Kryptex Pool** into the operator's Kryptex account as
    `account.workerid`, with auto-exchange to BTC/USDT. They are priced from WhatToMine's network data and Kryptex's
    live pool fee (3% in October 2026). Own-wallet users don't get them, since that pool pays Otium's account.
  - **kawpow, autolykos2, etchash, cn/gpu** (MoneroOcean, paid in XMR) lose money at normal electricity prices as of
    October 2026, so they are "free power only": not tested or mined unless the electricity price is 0.
  - **Kill switch:** `GpuPolicy:Disabled` turns an algorithm off for every app at the next refresh (useful when Pearl's
    announced upgrade ends consumer-GPU mining). A failed, lagging or more than 2-hour-old feed disables a coin
    automatically. `GpuPolicy:Tiers` overrides tiers.
  - Without a policy (server unreachable) the app falls back to MoneroOcean's own rates, still free-power-only.
- Each account mines under its own worker id to one of the operator's pool addresses.

**Crediting** (`src/Otium.Server/Earnings`)
- Every 10 minutes the server reads MoneroOcean's per-worker hashrate history. It uses `hs2`, the XMR-equivalent hashrate, so CPU and every GPU algorithm are valued on one scale.
- It credits the expected value: hashes × block reward ÷ difficulty × XMR price.
- Credits are reduced by the fee (10% by default; 0% during a referral fee-free period).
- Credits are held 48 hours before they can be cashed out.
- Referrers get 5% of a friend's gross earnings, paid out of the fee.
- Kryptex coins are credited the same way from Kryptex's per-worker hashrate history (`/{coin}/api/v1/miner/chart/{account}/{worker}`):
  hashes × the policy's USD rate, minus Kryptex's fee, then Otium's fee and referral split.
- The operator carries the pool's luck and the price until Kryptex's auto-exchange. `GET /admin/summary` compares what was
  credited (including `kryptexCreditedToUsersUsd`) against what the pools actually paid.

**Accounts and sign-in** (`src/Otium.Server/Accounts`)
- The first time a computer connects it gets its own account: no sign-up step.
- An account can have several computers. Each computer authenticates with its own random secret, and the balance, invite
  code and pool worker id belong to the account.
- **Adding an email** (a 6-digit code, valid 15 minutes, 5 tries, rate-limited, stored only as a salted hash) secures the
  account. Signing in with the same email on another computer attaches that computer to the account. If that computer
  had been earning on its own anonymous account, those earnings are merged in, and its old worker id keeps crediting the
  right account.
- A verified email is required before cashing out.
- Without an SMTP server configured, codes are written to the server log and to `/dev/outbox` (development only).

**Pool addresses**
- MoneroOcean reports only about 50 simultaneously active workers per address separately; beyond that, everything is lumped together.
- The server therefore spreads accounts across subaddresses of the operator's wallet, 40 per address, creating new ones through `monero-wallet-rpc` when needed. All payouts still land in one wallet.

**Cash-outs** (`src/Otium.Server/Payouts`)
- The whole available balance is debited in one database transaction.
- **Sent automatically, in crypto only, from the operator's Bitcoin treasury** (a bitcoind wallet):
  - **BTC:** up to 25 cash-outs share one transaction (`sendmany`), and Otium pays the network fee.
  - **ETH, BNB (BNB Smart Chain) and SOL:** swapped through [THORChain](https://dev.thorchain.org/), a decentralised
    swap network with no account, API key or KYC. For each cash-out the server asks THORNode for a quote with the
    user's address as the destination, sends BTC to THORChain's vault with the quote's memo, and THORChain delivers
    the coin straight to the user (usually 10–30 minutes). THORChain's fees (about $0.25–0.50) come out of the amount,
    which is why the minimums are $5 for BTC and BNB and $8 for ETH and SOL. If a swap fails, THORChain returns the BTC
    to the treasury and the cash-out goes back to the user's balance. A coin THORChain has paused (SOL as of October
    2026) shows as paused in the app until it resumes.
  - Kryptex credits the GPU coins to the operator's Kryptex account in BTC (auto-exchange), but account withdrawals
    are started by hand and confirmed by email (no withdrawal API as of October 2026), so sweep that BTC into the
    treasury about once a week. MoneroOcean still pays XMR, which has to be swapped to BTC by hand until THORChain's
    announced native Monero support goes live. See reports/Pools and bandwidth that pay crypto.md for moving all
    mining to Kryptex.
  - PayPal and Monero payouts still exist in the code but are off; add `paypal` or `xmr` to `Cashouts:Methods` to
    offer them again.
- **Screened:** cash-outs to addresses on OFAC's sanctions list are refused (lists from
  [0xB10C/ofac-sanctioned-digital-currency-addresses](https://github.com/0xB10C/ofac-sanctioned-digital-currency-addresses),
  refreshed twice a day). Until the lists have loaded, crypto cash-outs wait for review instead of going out unscreened.
- **Held for review:**
  - accounts younger than 2 days;
  - amounts over $50;
  - a destination already used by another account;
  - the daily automatic payout budget being used up;
  - a yearly total near the US tax-reporting threshold ($2,000; tax details are needed);
  - the method's provider not being set up yet, or the sanctions lists not having loaded.
- Failed or returned payouts go back to the balance.
- When a cash-out is paid (automatically, or marked paid by the operator) the owner gets an email receipt: amount,
  coin and network, masked address, dates, the cash-out id and the transaction with a block-explorer link. A mail
  failure is logged and never affects the payout.

**Own-wallet mode** (Advanced) works as before: the pool pays the user's own XMR wallet directly, a 2% fee is mined in short time slices, and referral rewards are mined to the referrer's wallet.

## Running the server

To run it in production (Docker Compose, HTTPS through Caddy, backups and restore), see
[docs/deploying.md](docs/deploying.md).

Configure `src/Otium.Server/appsettings.json` (or environment variables such as `Earnings__PoolWallets__0=...`):

| Setting | Purpose |
|---|---|
| `ConnectionStrings:Otium` | SQLite database path |
| `Earnings:PoolWallets` | Operator XMR addresses (subaddresses of one wallet). Balance mode is off until at least one is set or the wallet RPC is configured. |
| `Earnings:FeePercent`, `ReferralPercent`, `HoldPeriod` | Business terms; defaults 10%, 5%, 48 h |
| `Cashouts:Methods`, `MinUsd:{method}` | Payout methods offered (default `btc`, `eth`, `bnb`, `sol`) and their minimums |
| `Cashouts:*` | Automatic-approval limit, daily budget, minimum account age |
| `BitcoinWallet:RpcUrl`, `Username`, `Password`, `Wallet`, `ConfTarget` | The Bitcoin treasury: bitcoind's JSON-RPC (pruned is fine) on localhost or a private network. Crypto payouts are paused until it's set. |
| `Thorchain:ApiBase` | THORNode API used for prices, quotes and swap status (default: Liquify's public gateway) |
| `Sanctions:ListUrls:{list}`, `RefreshInterval` | Sanctioned-address lists (btc, evm, bsc, sol) and how often to reload them |
| `PayPal:*`, `MoneroWallet:*` | Older payout methods, off by default (see above). The Monero wallet RPC is still used to create pool addresses. |
| `Admin:Token` | Enables `/admin/*`; send it as `X-Admin-Token` |
| `ReverseProxy:Enabled`, `KnownProxies`, `KnownNetworks` | Behind an HTTPS proxy: take client IPs from `X-Forwarded-For`, trusted only from these proxy addresses or CIDR networks (localhost if neither is set) |
| `Email:SmtpHost`, `Port`, `Username`, `Password`, `From` | SMTP service for sign-in codes and cash-out receipts (Postmark, SES, Mailgun, ...) |
| `Referral:PublicBaseUrl`, `DownloadUrl` | Invite links and the landing page |
| `Kryptex:Enabled`, `Account` | GPU coins on Kryptex Pool (off by default). Set up auto-exchange on that Kryptex account. |
| `Kryptex:Coins:{algorithm}` | Kryptex coin slug, WhatToMine id and stratum host per algorithm (defaults for `pearlhash` and `quantus`) |
| `Kryptex:Cpu:Coin`, `Host`, `Port`, `Tls` | Kryptex's Monero pool for CPUs (default `xmr.kryptex.network:8029`, TLS) |
| `CpuMining:KryptexPercent` | Share of balance accounts (0–100) whose CPU mines on Kryptex instead of MoneroOcean; default 0 |
| `GpuPolicy:Disabled`, `Tiers`, `RefreshInterval` | Kill switch, tier overrides and refresh interval for the GPU policy |
| `Bandwidth:Enabled`, `PartnerName` | Internet sharing (off by default); the partner's name appears on the consent screen |
| `Bandwidth:UsdPerSharingHour`, `UsdPerGb` | Gross rates credited from usage reports, before the fee. Set them from the partner agreement (Honeygain pays per active device, so per hour). |
| `Bandwidth:DailyLimitBytes`, `MaxBytesPerSecond`, `MaxReportPeriod`, `SharedConnectionWindow` | Limits on self-reported usage: daily cap, speed cap, longest report, one paid computer per connection |

Run it locally:

```bash
dotnet run --project src/Otium.Server
```

**Operator dashboard:** open `/admin` on the server (e.g. `https://your-server/admin`) and enter the `Admin:Token`. It shows
what was credited against what was paid out and what is still owed, a MoneroOcean check (pool earned vs. credited), and
the cash-out review queue with Approve, Reject (the note is shown to the user) and Mark paid buttons, plus the other
cash-out statuses. The page holds no data itself: the token stays in that browser tab's session storage and is sent only
as a header to the API below, under a strict content security policy. It's served only when `Admin:Token` is set, and
the admin API is rate-limited per IP. Serve it over HTTPS only.

Operator endpoints:

| Endpoint | Purpose |
|---|---|
| `GET /admin/summary` | Credited vs. pool-earned totals, money owed to users (balances and unpaid cash-outs), pool address load, bandwidth credited (GB and USD) to check against the partner's statements. Ledger totals still load when the pool API is down (`poolError`). |
| `GET /admin/cashouts?status=review` | The review queue (or `queued`, `sending`, `paid`, `failed`), with the account's email, age, lifetime earnings, payouts and how many other accounts used the destination |
| `POST /admin/cashouts/{id}/approve` | Release a reviewed cash-out for automatic sending |
| `POST /admin/cashouts/{id}/reject` | Reject it; the amount goes back to the balance (`{"note": "..."}`) |
| `POST /admin/cashouts/{id}/mark-paid` | Record a payout you made by hand (`{"note": "PayPal txn ..."}`) |

The app finds the server through `AppIdentity.DefaultServerUrl`, which is overridable with `OTIUM_SERVER_URL`.

### Internet sharing (bandwidth)

Users can also opt in to sharing unused internet bandwidth. It runs inside Otium through the
[Honeygain SDK](https://developers.honeygain.com/docs/), with Otium as the publisher: Honeygain pays Otium per
active device, and the server credits each user's balance from their computer's usage reports.

- **Consent first.** Sharing is off until the user reads Otium's plain-language explanation and presses "I agree"
  (on the overview or in Advanced). Consent is recorded, passed to the SDK (`hgsdk_opt_in`) and withdrawn when they
  switch it off (`hgsdk_opt_out`). Users can share only while away and set a daily GB limit.
- **Honest limits.** The server credits each report once, caps period length and speed, applies a daily limit, and pays
  only one computer per public IP at a time, as partners require. If the server runs behind a reverse proxy, set
  `ReverseProxy:Enabled` so it sees client IPs.
- **To ship it:**
  1. Sign the Honeygain publisher agreement and get an API key.
  2. Put the SDK libraries next to the app: `hgsdk.dll` (Windows), `libhgsdk.so` (Linux), `libhgsdk.dylib` (macOS).
  3. Set the key: the `OTIUM_HONEYGAIN_KEY` secret for release builds (baked in at build time), or the
     `OTIUM_HONEYGAIN_KEY` environment variable at runtime.
  4. On the server, set `Bandwidth:Enabled=true`, `PartnerName` and the rates.
  
  Without the library or key, the option stays hidden.
- **Before launch, confirm with Honeygain:**
  - that rewarding users in cash for opting in is allowed,
  - whether they can supply per-device reports (the SDK's device id is available as `PartnerDeviceId`),
  - how they treat a connection that also runs their own consumer app.
- **AV and code signing.** Proxyware is a common antivirus "potentially unwanted" category. Keep the sharing state
  visible in the app, and sign the Windows build with an EV certificate.

## Your data: what Otium stores, export and deletion

Many users are in the EU and the Nordics, so the server supports the GDPR rights of access, portability and erasure.
The draft privacy policy is in [docs/privacy.md](docs/privacy.md) and **still needs legal review**.

**What the server stores** (SQLite, `src/Otium.Server/Data/OtiumStore.cs`)

| Data | Why |
|---|---|
| Account: random id, pool worker id, invite code, email (if added), own XMR wallet (own-wallet mode), who invited it and when, fee-free period | Crediting mining to the right person, sign-in, referrals |
| Computers: random device id (a hash of the machine id), a SHA-256 hash of its secret, first and last seen | Authenticating each computer |
| Ledger: every earning, referral reward, cash-out, refund and adjustment, in micro-dollars, with dates and a reference to the pool record | The balance, and bookkeeping |
| Cash-outs: method, destination (PayPal email or XMR address), amount, status, provider reference, review notes | Paying out, fraud checks, bookkeeping |
| Bandwidth reports: computer, public IP address, period, bytes, credited amount | Paying for internet sharing; one paid computer per connection |
| Email codes: salted hash, expiry, attempts | Sign-in; useless after 15 minutes |

The desktop app keeps its settings, a cached copy of the account summary and local earnings stats in its data folder
(Advanced → About → Open data folder). Mining pools (MoneroOcean, Kryptex), PayPal, the email provider and the
bandwidth partner see what they need to do their part; [docs/privacy.md](docs/privacy.md) lists them.

**Export** (Advanced → Settings → Your data → Download my data)
- `GET /api/v1/devices/{deviceId}/account/export` (device-secret header like every device endpoint) returns a JSON file:
  the account, its computers, the whole ledger, every cash-out with its full destination, invites (without anything
  identifying the people on the other side: referral rewards say only "invited friend's earnings"), bandwidth reports
  with IP addresses, and any anonymous accounts that were merged in.

**Deletion** (Advanced → Settings → Your data → Delete my account)
- `POST /api/v1/devices/{deviceId}/account/delete` with `{"code": "123456", "giveUpBalance": true}`.
- Accounts with an email need a fresh code: the app requests one with `POST .../email/code` and
  `{"email": ..., "purpose": "delete-account"}` (sent only to the account's own email; same expiry, tries and rate
  limits as sign-in).
- Refused while a cash-out is open (409). A remaining available or pending balance must be given up explicitly (409
  until `giveUpBalance` is true); the app shows the amount and suggests cashing out first.
- **Deletion anonymizes rather than erases.** Ledger and cash-out rows stay, because the operator must keep bookkeeping
  records (typically 5 to 10 years depending on the country) and needs them to show what was paid. Totals in
  `/admin/summary` don't change; a given-up balance stays in `owedToUsersUsd` until the operator writes it off.
  What goes:
  - the email, pool address and own wallet; the invite code and pool worker id become `deleted-<account id>`;
  - every computer and its secret hash, and the account's sign-in codes;
  - the IP address and computer id of each bandwidth report;
  - full cash-out destinations (kept as e.g. `•••@•••.com`; the provider's reference stays so a payout can still be
    traced at PayPal or on the Monero wallet if the law requires it).
- `accounts.deleted_at` records when. Anonymous accounts merged into the deleted one are anonymized too.
- **Referrals are detached:** the deleted account's link to whoever invited it is dropped, and friends it invited no
  longer point to it but keep their fee-free time and history. Referral rewards already credited stay in the
  referrer's ledger, so their balance doesn't change.
- Ledger references for mining credits still contain the old worker id, which matches the pool's own records for
  reconciliation but no longer leads to any account.
- The app then stops earning and internet sharing, forgets the account, invite and device secret, turns off
  start-at-sign-in and shows the welcome screen. Starting again creates a new, empty account.

## Building the desktop app

You need the .NET 10 SDK. On this machine it's installed per-user, so add it to `PATH` for the session first:

```powershell
$env:PATH = "$env:LOCALAPPDATA\Microsoft\dotnet;$env:PATH"
```

Run the tests (227, covering accounts and sign-in, crediting, cash-outs and receipts, the operator dashboard, invites and the leaderboard, fraud rules, GPU profit switching and policy, Kryptex crediting, internet sharing, live electricity prices, the simulated pool, wallet checksums and the engine):

```bash
dotnet test
```

`.github/workflows/ci.yml` runs them on Windows and Linux for every push to `main` and every pull request.

### Dev mode: the whole loop on one PC

```bash
pwsh build/dev.ps1
```

This starts everything locally, with no real mining and no real money:
- **Otium.DevPool**, a simulated MoneroOcean, Kryptex Pool and WhatToMine (same APIs). Earnings are sped up 3000× so
  balances move in minutes; `-Speedup 1` shows real-world numbers.
- **Otium.Server** with dev timings: credits every 20 s, a 1-minute hold, no new-account review, and fake PayPal/Monero
  payouts that are marked paid 15 s later.
- **The app** with simulated CPU and GPU miners.

Sign-in codes are printed in the console. Closing the app window stops everything.

| Option | Effect |
|---|---|
| `-Computers 2` | Two simulated PCs, to try signing in on a second computer |
| `-Reset` | Wipe dev data (`artifacts/dev`) first |
| `-Speedup N` | Change the earnings speed-up |
| `-KryptexCpu` | Route every balance account's CPU to the simulated Kryptex Monero pool instead of MoneroOcean |

Operator dashboard while it runs: http://localhost:5080/admin (token `dev`). Or the raw summary:
`curl -H "X-Admin-Token: dev" http://localhost:5080/admin/summary`. It compares what
users were credited against what the simulated pool paid, which is a quick check that crediting is right.

### Running the app on its own

Run the app. `OTIUM_HOME` keeps your real data folder untouched:

```bash
dotnet run --project src/Otium.App
```

Build installers. Output goes to `artifacts/releases/<runtime>/`:

```bash
pwsh build/package.ps1
```

| Platform | Package | Notes |
|---|---|---|
| Windows x64 | `Otium.Desktop-win-x64-Setup.exe` + portable zip | Per-user install to `%LocalAppData%\Otium.Desktop`; data stays in `%LocalAppData%\Otium` |
| Linux x64 | `Otium.Desktop-linux-x64.AppImage` | Needs the standard X11 session libraries (`libice6 libsm6`) |
| macOS arm64 / x64 | `.pkg` + zip | Built on macOS only (CI) |

**Releasing:** push a tag such as `v0.1.0`. `.github/workflows/release.yml` checks the release settings, tests, builds
every platform and publishes one GitHub release, which is also the auto-update feed (`AppIdentity.UpdateUrl`). The
production server URL is baked in at build time from the `OTIUM_SERVER_URL` repository variable (locally:
`pwsh build/package.ps1 -ServerUrl https://...`); the workflow won't release without it. Step by step, including the
first release: [docs/releasing.md](docs/releasing.md). Signing: [docs/code-signing.md](docs/code-signing.md).

## Project layout

```
src/Otium.Core               engine, provider interface, fees, power, GPU detection (NVML), profit switcher,
                             wallet checksums, account contracts and client
src/Otium.Providers.XMRig    CPU miner provider
src/Otium.Providers.SrbMiner GPU miner provider with benchmarking and profit switching
src/Otium.Providers.Honeygain internet sharing through the Honeygain SDK (loaded at runtime)
src/Otium.App                desktop app (Welcome → Home ⇄ Advanced), Velopack updates
src/Otium.Server             accounts and email sign-in, crediting, cash-outs, referrals, landing page, admin API
src/Otium.DevPool            simulated MoneroOcean, Kryptex Pool and WhatToMine for dev mode
tests/Otium.Tests            unit and API tests
build/                       dev.ps1 (dev mode), package.ps1 (installers), icon generator, icons
```

## Before launch

- **Legal review (required).**
  - In balance mode the operator holds users' earnings and pays them out.
  - US: FinCEN treats pool payouts as non-transmission, but hosting balances for members can make the operator a money transmitter.
  - EU: MiCA may apply to holding or transferring crypto for users.
  - The design keeps balances in USD for compute bought from users and never offers a withdrawable crypto balance, but this needs counsel to confirm.
  - Collect tax details (W-9/W-8) before anyone passes the yearly reporting threshold.
  - Privacy: have [docs/privacy.md](docs/privacy.md) reviewed (controller details, legal bases, retention periods,
    processor agreements with the hosting, email, payout and bandwidth providers, transfers outside the EU), then
    publish it and link it from the welcome screen and the landing page.
- **Crypto payouts and regulation.** Paying users in crypto from balances Otium holds may make Otium a crypto-asset
  service provider (MiCA in the EU/EEA) or a money transmitter (US), with know-your-customer duties that the current
  no-KYC design doesn't meet. Get counsel's view before launch; the cash-out flow can add identity checks later
  without a redesign.
- **Deploy the server** behind HTTPS with backups of the SQLite database. Set the `OTIUM_SERVER_URL` repository
  variable so release builds use it.
- **Fund the Bitcoin treasury** (bitcoind) and keep only a few weeks of payouts in it; sweep the Kryptex account's BTC
  into it weekly. The admin dashboard shows its balance and warns when it's below the open cash-outs.
- **Code signing** for Windows and macOS: see [docs/code-signing.md](docs/code-signing.md) (Azure Artifact Signing or
  a cloud-HSM certificate for Windows, Developer ID and notarization for macOS).
- **Real-machine run:** do a real mining run on your RTX 3080 to confirm SRBMiner's stats format and the GPU speed units
  (including `pearlhash` and `quantus`, and that Kryptex's per-worker chart reports H/s).
- **Kryptex:** confirm in writing that one operator account may aggregate an app's whole install base, and ask how Pearl's
  August 2026 "rank-penalty" softfork treats a large pooled account. Kryptex runs a competing consumer app, so keep
  MoneroOcean as the fallback route.
