# How Otium works

The technical side of Otium: the app in detail, mining and fees, the server and its settings, internet sharing, data,
building, dev mode and releasing. The short version for users is the [README](../README.md).

## The app, step by step

1. Install and open Otium. The welcome screen offers **Quick start** (processor, graphics card, internet sharing, keeping
   the miner ready and sharing earnings figures all on; just say where to pay you) or **Customize**: four short screens
   that show what this computer could earn per month (from its processor and graphics card, today's payouts and your
   electricity price), let you choose each part, ask where to pay you, and summarise what will happen.
2. **Where should we pay you?** Pick a coin (USDT on TRON by default) and paste your own address for it. It's checked as
   you type, including the network, and the screen says which network to pick in your wallet and when the pool pays
   (unMineable: daily once you reach its minimum, e.g. 1.5 USDT). US users are paid in Monero by MoneroOcean, because
   unMineable doesn't serve US residents.
3. Press **Start earning**. Right away, a two-minute test runs the miners while you watch and measures what this PC
   really earns (on its best coin, after Otium's fee and electricity), so the overview shows your own numbers instead
   of a hardware table's; it earns for real, to your address, and can be run again from the overview. If the graphics card earns at least ten times what the processor would (an i7-8700K next to an RTX 3080: $0.09 against $3.23 a day), the first test switches the processor off and the result says so; it can be turned back on in Settings, and later tests don't switch it off again. From then on Otium starts when you sign in and earns only while you're away from the computer, after 3 minutes without keyboard or mouse input. While it earns on mains power it keeps the computer awake, so Windows' usual sleep timer doesn't stop it for the night; the screen still turns off, and laptops on battery are left alone (Settings can turn this off). It pauses the moment you come back, and also when electricity would cost more than it earns (the price is estimated from your country; in Norway, Sweden, Denmark and Finland it follows live hourly spot prices, from hvakosterstrommen.no, elprisetjustnu.se, elprisenligenu.dk and sahkotin.fi).
4. The pool pays your address by itself. The overview shows how far you are from its next payout ("0.84 USDT at
   unMineable · paid out at 1.5 USDT", with an estimate of when) and warns you if the pool won't pay on its own, for
   example when unMineable's "Auto pay" switch is off for your address (new TRC-20 addresses start with it off), with a
   link to your address page there.
5. Share your invite code. You and the friend both get 30 days without Otium's fee, and for 6 months 25% of Otium's fee
   on their mining is mined to your address ("Earned from friends" on the Invites page). The Invites page
   has ready-made messages (short, explained, for gamers) that you can edit, one-click sharing to WhatsApp, Discord and
   email, and a monthly leaderboard of top inviters, counted by friends who actually started earning. A friend who opens
   your link (otium.lol/r/CODE) and installs Otium on the same internet connection within a week finds your code already
   filled in on the welcome screen ("Filled in from your friend's invite link"), so they can't forget it.
6. Optionally, earn from your internet connection too: the overview suggests it once, explains in plain words what
   sharing unused bandwidth means, and turns it on only when you agree. Proxybase's client then earns per GB, and Otium has
   it converted and paid to your own payout address, in the coin you chose, like your mining earnings.

When you come back after a stretch of earning, the overview says what it brought in ("While you were away: $0.84 in
7 h 40 min of earning"), and in the first days of a month it sums up the last one. Each can be copied as a message
with your invite code or saved as a picture to post. If you answer yes to "Share analytics" at the end
of the welcome screens (or turn it on later in Settings), the app sends a few times a day what each device earned, with the card model and processor name and never your
address; otium.lol then shows real per-card medians from computers of at least three accounts that are a week old or
more (each account counting once), next to the total the pools have paid Otium users (read from unMineable's and
MoneroOcean's public APIs, counting each address once and only addresses mined by Otium alone). A report that overlaps
one the same computer already sent is refused.

If you tick "Keep the processor miner ready when I come back" (welcome screen or Settings; off by default), the
processor miner pauses rather than stops when you come back, keeping its 2 GB dataset and pool connection for up to 10
minutes, so earning resumes the instant you leave again instead of after another warm-up.

The overview shows, at a glance: what you've earned and progress to the pool's next payout (with an ETA), what each device is
doing right now in plain words ("Mining Monero", "Waiting for you to step away · starts in 1:42"), what you can expect
today, this week and this month, and an hour-by-hour chart of the last 24 hours. Projections use how many hours a day
this computer actually earns once there is a day of history.

Everything technical lives under **Advanced**:
- Details per device, including GPU algorithm and speed, power draw, and profit per day.
- CPU usage, the idle delay, your electricity price, and switching devices on or off.
- "Efficiency mode for my graphics card" (Windows, NVIDIA): while the card mines, its power limit drops to 70% of the
  default (NVML), and it's back at full power the moment mining stops, so games aren't affected. One administrator
  prompt creates two scheduled tasks (`\Otium\GPU power cap` / `restore`, run with highest privileges, never on a
  schedule) that Otium starts without further prompts; uninstalling removes them.
- "Let my graphics card earn while I browse" (Windows, opt-in): the card also mines while the user does ordinary
  things at the computer. Windows' own full-screen signal (SHQueryUserNotificationState) stops it the moment a game or
  full-screen app starts, and it never runs on battery. The processor isn't affected.
- Desktop notifications (on by default, one switch): "While you were away: $0.84", a payout the pool sent, internet
  sharing paid out, last month's report, and a pool that something on the computer blocks (at most twice a day).
- "Speed up processor mining" (Windows): one administrator prompt gives your account the "Lock pages in memory" right,
  so XMRig can use large memory pages (XMRig reports up to 50% faster RandomX, depending on the processor). It applies
  from your next sign-in.
- Internet sharing (Proxybase): on/off, only while away, and its status and payouts.
- Your payout coin and address.
- Updates: a downloaded update installs itself once you've been away 10 minutes with the window closed ("Install
  updates while I'm away", on by default), or with "Restart to update".
- Help: "Send a problem report", "Join our Discord" and contact@otium.lol (Settings → About; the overview's footer
  links them too).

## How it works

```
 desktop app (Avalonia, .NET 10)                              Otium server (ASP.NET Core + SQLite)
 ┌────────────────────────────────┐   register, invites       ┌──────────────────────────────────┐
 │ CPU: XMRig (RandomX)           │ ────────────────────────▶ │ accounts, invite codes, referrals │
 │ GPU: SRBMiner, best algorithm  │ ◀──────────────────────── │ GPU policy (which coins, where)   │
 │ fee/referral time slices       │   GPU policy, referrer    └──────────────────────────────────┘
 └───────────────┬────────────────┘
                 │ login "USDT:T….worker#ztjq-jr7s"    (Monero: wallet + worker on MoneroOcean)
                 ▼
     unMineable (or MoneroOcean) ── pays the coin straight to ──▶ the user's own address
```

Otium is **non-custodial by default**: it never receives or holds users' money, so there is no balance, ledger,
treasury or cash-out queue to run. The server only keeps accounts, invite codes and referrals, and publishes the GPU
policy.

**Mining**
- **Everything mines on unMineable**, which mines and converts, and pays the address in its own coin once a day after
  it reaches the coin's minimum; Monero too (paid in XMR from 0.03 XMR). Processor, graphics card and every fee slice
  then share one pool, so Otium needs a single fee address (USDT). Only US users' Monero mines on MoneroOcean, to the
  bare address with the worker name as password, because unMineable's terms exclude US persons. The login is `COIN:address.worker#ztjq-jr7s`, and unMineable picks the network
  from the address (a TRON address gets USDT on TRC-20). `ztjq-jr7s` is Otium's unMineable referral code: users pay
  unMineable 0.75% instead of 1%, and unMineable pays Otium 0.25–0.50% of their rewards.
- CPU: XMRig on `rx.unmineable.com:443` with TLS (unMineable's only RandomX port that works). GPU: SRBMiner on the
  routes in the server's GPU policy: unMineable for **Pearl** (the best-paying coin for NVIDIA cards in October 2026),
  plus Xelis (`xelishashv3`), which only runs on free power since it loses money at normal prices; all
  with `--tls true`, priced from WhatToMine minus unMineable's 0.75% (FishHash was dropped in October 2026: Pearl
  pays more on every card, and it never started hashing on an RTX 3080). US users' graphics cards stay on
  MoneroOcean, whose GPU algorithms are "free power only". Without the server's policy, unMineable users' GPUs wait
  (unMineable's prices come from the server) and US Monero users fall back to MoneroOcean's own rates.
- **One wallet only.** Everything is paid to the single payout address the user gives. Quantus, which Kryptex pays
  only to a separate Quantus wallet, is no longer mined (the code for Kryptex's address login stays, unused, and
  `Kryptex:AddressMining` is off by default).
- **AMD cards mine Pearl too.** SRBMiner 3.7.6 mines `pearlhash` on AMD (WhatToMine benchmarks the RX 9070 XT at about
  70 TH/s, $2.33 a day, and the RX 7900 XTX at $1.33, October 2026), so the server no longer limits unMineable's Pearl to
  NVIDIA, although unMineable's support article still says NVIDIA only. If unMineable rejects an AMD card's shares, the
  share-rejection failover sets Pearl aside for that card and it mines the next coin. Still to confirm on a real AMD
  card that unMineable credits the work.
- **Pearl fork switch.** Pearl's planned FP8 fork (PIP-3) only accepts NVIDIA Hopper and Blackwell cards. The server
  watches PIP-3 for an activation height and Pearl's chain height on Kryptex; once the chain reaches it, the policy
  limits `pearlhash` to those cards (`GpuPolicy:PearlFork:SupportedGpus`), so every other card moves to its next best
  coin at the next refresh. Apps also drop any coin whose shares the pool mostly rejects (5 or more, at least 80%),
  which covers a fork the server hasn't seen yet. The admin summary shows the fork's state.
- **Fee and referrals are mining time.** 2% of mining time is mined to Otium's own address on the user's pool (one
  slice of about 29 minutes per 24 hours of mining), and for 6 months a quarter of it goes to whoever invited the user,
  mined to the referrer's own address when the same pool pays it. New users with an invite code get 30 fee-free days;
  each verified friend gives the referrer another 30 (capped at 90). The server sends these terms with every account
  summary (`Fees:*`), so they change without an app release; apps cap the fee at 10% and use the built-in defaults
  until a server answers. Release builds only lower the fee, or add fee-free days or an inviter's slice, within the
  built-in terms and from a valid signed fee grant (see "Keeping the fee" below). The miner keeps its pool and algorithm; only the login
  changes for a slice. Builds set Otium's fee address with `-FeeWallet 'USDT:T…'`: one USDT address covers every user
  outside the US. Adding `;XMR:4…` also takes the fee from US users, whose Monero mines on MoneroOcean; without a fee
  address for a pool, the slice is mined for the user.
- **Referral checks.** An invited friend counts as verified once their pool shows real mining by one of their own
  Otium computers (a worker named "ot" + 8 hex digits of one of their device ids): MoneroOcean hashes and worker
  history for Monero addresses, or unMineable having credited their address in two daily accountings (or paid it), so
  at least a day of mining, with such a worker in its list. The flip to verified is one conditional `UPDATE`, so check-ins
  arriving together reward the inviter once; at most 10 friends' pools are asked per check-in, and friends invited
  under the code of an account merged into this one (email sign-in) count too. Wallets are compared by address alone,
  so the same address under another coin (TRX/USDT, BNB/ETH) or typed differently can't invite itself. A code is
  bound only once the account has a payout address (without custody). Each payout address and each computer joins
  through an invite once, ever: `referral_redemptions` keeps HMAC hashes of the address and of the app's hardware id
  (`HardwareId`, a SHA-256 of the machine id, sent even when the install uses its own device id), and keeps them when
  the account is deleted. A referred account that later moves to an address that already joined through another invite
  (re-registering, or a computer joining it by email sign-in) loses what's left of its 30 fee-free days. Nobody can
  invite themselves from a second computer: an invite doesn't count when the new computer is on an internet
  connection one of the inviter's computers used in the last 30 days (kept as HMAC hashes, `account_networks`), a
  friend isn't verified while their computers and the inviter's used the same connection in the last 30 days, and a
  friend can't later switch to the inviter's payout address (nor the inviter to a friend's). Invite links
  (`/r/CODE`) are remembered only for page loads (`Sec-Fetch-Dest: document`, `Sec-Fetch-Mode: navigate` when the
  browser sends them), the first link on a connection wins for the week, and the app only shows the code on the
  welcome screen to keep or clear, never using it unseen. Invited friends' apps report the share of Otium's fee they
  mined for the inviter (`/referral-share`, at most $0.50 a day each, ignored after the 6 months); the inviter's
  summary carries the total for verified friends (`ReferralEarnedUsd`, "Earned from friends").
- The **kill switch** still works: `GpuPolicy:Disabled` turns an algorithm off for every app at the next refresh, and a
  failed, lagging or more than 2-hour-old feed disables a coin automatically.
- **SRBMiner's statistics API stays off.** It listens on every network interface (3.7.6 has no bind option), answers
  any website (`Access-Control-Allow-Origin: *`) and shows the payout address, so hashrate and shares come from the
  console's statistics instead (its table's "Hashrate" row in 3.7.6, or an older "Total" line). XMRig's API is bound to `127.0.0.1`.
- **Guards against a wrong server answer.** Own-wallet graphics cards only connect to the pools' own hosts
  (`*.unmineable.com`, `*.kryptex.network`, `*.moneroocean.stream`; `GpuPolicy.OnlyTrustedHosts`), the referrer's
  share is capped at 1% of mining time, and internet-sharing payouts only go to a well-formed 0x deposit address.
- **Keeping the fee.** Every copy from otium.lol mines Otium's fee; only someone who builds Otium from the source
  can change that.
  - Release builds (the production server baked in, `AppIdentity.IsReleaseBuild`) ignore every developer override:
    `OTIUM_SERVER_URL`, `OTIUM_FEE_WALLET`, `OTIUM_UNMINEABLE_REFERRAL`, `OTIUM_UPDATE_URL`, `OTIUM_SIMULATE`, the pool
    API overrides, `OTIUM_DEVICE_SEED`, `OTIUM_HOME` and `OTIUM_HONEYGAIN_KEY`. There's no server setting in the app either.
  - Every account summary carries a **fee grant** for the computer asking (`Fees.FeeGrant`): the fee, the inviter's
    share and address, the fee-free date, and for custodial balance accounts the pool wallet. It's signed by the
    server (`Fees:GrantSigningKey`, ECDSA P-256) and valid for 7 days. Release builds take those values only from a
    grant that verifies against `AppIdentity.FeeGrantKeys`, is for their own device and hasn't expired. Without one
    (an edited settings.json, a blocked otium.lol for over a week) they mine the full built-in fee with no fee-free
    days and no inviter's slice.
  - The built-in terms are the limits (`FeePolicy.WithinBuiltInLimits`): a grant can't make the fee lower than 2%,
    nor fee-free time or the inviter's share more generous; fee-free dates are capped at 90 days ahead.
  - **Mining time, which places the slice, can't be rolled back.** It's never negative in `ledger.json` and only
    grows. The app reports each engine's total with its account check-in (`?cpuMined=&gpuMined=`, seconds); the server
    keeps the most per device (`devices.cpu_mined_seconds`/`gpu_mined_seconds`, growing no faster than the time between
    reports) and signs it back as the grant's `CpuMiningFloor`/`GpuMiningFloor`, which release builds raise their
    ledgers to, so a deleted or edited ledger is undone at the next check-in. Ticks are measured on the monotonic clock
    and capped at 4 tick intervals (setting the system clock back does nothing), and only count while the miner reports
    a hashrate (warming up or a suspended miner isn't mining time).
  - **The miner runs Otium's settings.** XMRig's config has `"watch": false` (an edited config.json isn't reloaded),
    and every minute Otium reads `GET /1/config` from XMRig's API: a pool or login other than Otium's, a second pool,
    or an API that refuses Otium restarts XMRig with Otium's config. XMRig and SRBMiner keep their pinned download, and
    the binary is checked against it before every launch (hashes cached per size and modification time); a changed
    binary or download makes the app install the engine again.
  - **Fee slices are checked from the server.** The payout checker records which Otium workers (`ot` + 8 hex digits
    of the device id) the pools show working on users' own addresses, and `FeeSliceWatcher` does the same every hour
    for Otium's fee address (`Unmineable:OtiumTarget`). The admin summary's `feeSlicesMissing` lists computers seen
    mining over the last 7 days (first seen at least 2 days ago) with no fee slice in that time and no fee-free days:
    most likely a modified app. Nothing is done to them automatically.

Spike results behind these choices (logins, TLS ports, the referral code on workers, auto-pay, the worker API) are in
[docs/non-custodial-plan.md](non-custodial-plan.md).

### Custodial mode (`Custody:Enabled`, off by default)

The original design is still in the code and tested: the server holds a USD balance per account, mining goes to the
operator's pools, and users cash out from a Bitcoin treasury. It only runs with `Custody:Enabled=true`; otherwise its
background jobs don't start and its routes (`/cashouts`, `/bandwidth`, admin cash-outs) return 404. Holding users'
earnings has legal consequences (see [Before launch](#before-launch)).

**Mining**
- The CPU runs XMRig (RandomX). For balance accounts the **server decides the pool** and sends it with the account
  summary, so moving users needs no app release: MoneroOcean (pays XMR to the operator's wallet) by default, or
  Monero on **Kryptex Pool** under the operator's Kryptex account (`account.worker`, auto-exchanged to BTC, PPS+ 1%).
  `CpuMining:KryptexPercent` moves a stable share of accounts (a canary, by a hash of the worker id) and 100 moves
  everyone; raising it only ever adds accounts. Kryptex CPU work is credited at the same Monero network value as
  MoneroOcean work (block reward ÷ difficulty × price, minus the pool fee), so a move doesn't change what a user earns.
  Users paid at their own address mine as described above.
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
- Kryptex work (GPU coins, and CPU Monero when routed there) is credited from Kryptex's public worker list
  (`/{coin}/api/v3/miner/workers/{account}`), because per-worker history (`/miner/chart`) returns 403 for "krx"
  accounts. Each 10-minute run takes every worker's 30-minute average × the time since the last run (sampled every
  10 minutes, that adds up to the real total: each hash sits in three overlapping windows at a third each; longer gaps
  use the 3-hour average, for at most 3 hours). Hashes are valued at the policy's USD rate (Monero: the network rate),
  minus Kryptex's fee, then Otium's fee and referral split.
- The operator carries the pool's luck and the price until Kryptex's auto-exchange. `GET /admin/summary` compares what was
  credited (including `kryptexCreditedToUsersUsd`) against what the pools actually paid.

**Accounts and sign-in** (`src/Otium.Server/Accounts`)
- The first time a computer connects it gets its own account: no sign-up step.
- An account can have several computers. Each computer authenticates with its own random secret, and the balance, invite
  code and pool worker id belong to the account.
- **Adding an email** (a 6-digit code, valid 15 minutes, 5 tries, stored only as a salted hash; at most 5 codes an hour
  and 10 a day to one address; after 10 wrong codes in a day from one computer that computer is locked out of that
  address until the next day, and after 50 from all computers together every computer is, with no new code emails)
  secures the account. Counting per computer means someone guessing at another person's address locks out only
  themselves. Signing in with the same email on another computer attaches that computer to the account. If that computer
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
    announced native Monero support goes live. The operator's research notes cover moving all
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


## Running the server

To run it in production (Docker Compose, HTTPS through Caddy, backups and restore), see
[docs/deploying.md](deploying.md).

Configure `src/Otium.Server/appsettings.json` (or environment variables such as `Earnings__PoolWallets__0=...`):

| Setting | Purpose |
|---|---|
| `ConnectionStrings:Otium` | SQLite database path |
| `Fees:FeePercent`, `ReferrerShareOfFee`, `ReferrerRewardDays` | The disclosed fee as a share of mining time (default 2, apps cap it at 10) and the referrer's part of it (default a quarter, for 180 days; apps cap the referrer's part at 1% of mining time, since the referrer's address comes from the server); apps pick changes up at their next sync |
| `Fees:RefereeFeeFreeDays`, `ReferrerFeeFreeDaysPerFriend`, `MaxFeeFreeDays` | Fee-free days for invited users (30), per verified friend for the referrer (30), and the most that can stack up (90) |
| `Fees:GrantSigningKey` | Base64 PKCS#8 ECDSA P-256 key that signs fee grants. Its public half must be in `AppIdentity.FeeGrantKeys`, or release builds ignore the grants (and charge the full fee with no fee-free days). Production's lives in `.env`, with a copy in the developer's private keys folder |
| `App:MinimumVersion` | Off by default. Apps older than this (they send `X-Otium-Version` from 0.1.1; older ones send nothing) get 426 "please update" on everything except account export and deletion, email sign-in and problem reports; the update feed stays open. Set it (e.g. `0.1.1`) once a release that fixes something the fee or invites depend on has reached most users: 0.1.0 trusted its settings file for the fee |
| `Custody:Enabled` | Off (default): non-custodial, accounts and referrals only. On: balances, crediting, cash-outs, Kryptex and internet sharing; the settings marked "custodial" below apply only then |
| `Unmineable:OtiumTarget` | Otium's fee address on unMineable (e.g. `USDT:T…`), shown with its fee and referral earnings on the admin dashboard |
| `Unmineable:Enabled`, `FeePercent`, `Algorithms:{algorithm}` | unMineable GPU routes in the policy (default on, 0.75%; Pearl and Xelis with their WhatToMine ids and hosts) |
| `Referral:VerifyUnmineableByRewards` | Count an invited friend paid by unMineable as verified once unMineable has rewarded their address (default on) |
| `Earnings:PoolWallets` | Custodial: operator XMR addresses (subaddresses of one wallet). Balance mode is off until at least one is set or the wallet RPC is configured. |
| `Earnings:FeePercent`, `ReferralPercent`, `HoldPeriod` | Business terms; defaults 10%, 5%, 48 h |
| `Cashouts:Methods`, `MinUsd:{method}` | Custodial: payout methods offered (default `btc`, `eth`, `bnb`, `sol`) and their minimums |
| `Cashouts:*` | Automatic-approval limit, daily budget, minimum account age |
| `BitcoinWallet:RpcUrl`, `Username`, `Password`, `Wallet`, `ConfTarget` | Custodial: the Bitcoin treasury: bitcoind's JSON-RPC (pruned is fine) on localhost or a private network. Crypto payouts are paused until it's set. |
| `Thorchain:ApiBase` | THORNode API used for prices, quotes and swap status (default: Liquify's public gateway) |
| `Sanctions:ListUrls:{list}`, `RefreshInterval` | Sanctioned-address lists (btc, evm, bsc, sol) and how often to reload them |
| `PayPal:*`, `MoneroWallet:*` | Older payout methods, off by default (see above). The Monero wallet RPC is still used to create pool addresses. |
| `Admin:Token` | Enables `/admin/*`; send it as `X-Admin-Token` |
| `ReverseProxy:Enabled`, `KnownProxies`, `KnownNetworks` | Behind an HTTPS proxy: take client IPs from `X-Forwarded-For`, trusted only from these proxy addresses or CIDR networks (localhost if neither is set) |
| `Email:SmtpHost`, `Port`, `Username`, `Password`, `From` | SMTP service for sign-in codes and cash-out receipts (Postmark, SES, Mailgun, ...) |
| `Referral:PublicBaseUrl`, `DownloadUrl` | Invite links, and where the website's Download buttons go while no release can be read from GitHub (otherwise otium.lol/download, which starts the installer for the visitor's system from the newest release, or the newest release candidate before the first stable one) |
| `Site:PagesDirectory` | Folder with the reviewed `privacy.html` and `terms.html`, served at `/privacy` and `/terms` once present (default `/data/site`) |
| `Kryptex:Enabled`, `Account` | Custodial: GPU coins on Kryptex Pool (off by default). Set up auto-exchange on that Kryptex account. |
| `Kryptex:Coins:{algorithm}` | Kryptex coin slug, WhatToMine id and stratum host per algorithm (defaults for `pearlhash` and `quantus`) |
| `Kryptex:Cpu:Coin`, `Host`, `Port`, `Tls` | Kryptex's Monero pool for CPUs (default `xmr.kryptex.network:8029`, TLS) |
| `CpuMining:KryptexPercent` | Share of balance accounts (0–100) whose CPU mines on Kryptex instead of MoneroOcean; default 0 |
| `GpuPolicy:Disabled`, `Tiers`, `RefreshInterval` | Kill switch, tier overrides and refresh interval for the GPU policy |
| `GpuPolicy:PearlFork:Mode`, `ActivationHeight`, `SupportedGpus`, `CheckInterval` | Pearl's FP8 fork: `auto` (default) follows PIP-3 and Pearl's chain height, `active` limits Pearl to the supported cards now, `off` never; an operator-set height overrides PIP-3 |
| `Kryptex:AddressMining`, `AddressCoins:{algorithm}` | Quantus on Kryptex's address login, paid to users' own Quantus wallets (default **off**: Otium pays only the one payout address; `qtc.kryptex.network:8049` with TLS) |
| `Mysterium:Enabled`, `SponsorPrivateKey`, `PolygonRpcUrl` | Internet sharing through Mysterium nodes: Otium pays each new node's registration from this Polygon wallet (a secret; keep only a few days of MYST and some POL in it) |
| `Mysterium:AmountMyst`, `DailyBudgetMyst`, `MaxPerAccount`, `MaxPerIpPerDay` | Fee sent per node (default 0.15 MYST) and the limits on sponsoring (15 MYST a day, 5 per account, 3 per connection a day) |
| `Bandwidth:Enabled`, `PartnerName` | Custodial: internet sharing through Honeygain (off by default); the partner's name appears on the consent screen |
| `Bandwidth:UsdPerSharingHour`, `UsdPerGb` | Gross rates credited from usage reports, before the fee. Set them from the partner agreement (Honeygain pays per active device, so per hour). |
| `Bandwidth:DailyLimitBytes`, `MaxBytesPerSecond`, `MaxReportPeriod`, `SharedConnectionWindow` | Limits on self-reported usage: daily cap, speed cap, longest report, one paid computer per connection |

Run it locally:

```bash
dotnet run --project src/Otium.Server
```

**Operator dashboard:** open `/admin` on the server (e.g. `https://your-server/admin`) and enter the `Admin:Token`.
Non-custodial, it shows what Otium itself earns on unMineable (fee slices and referral rewards on
`Unmineable:OtiumTarget`, its auto-pay state and problems, how many miners use the referral code), accounts by payout
coin, computers seen this week, and invites and verified friends. Custodial, it shows what was credited against what
was paid out and what is still owed, a MoneroOcean check (pool earned vs. credited), and the cash-out review queue with
Approve, Reject (the note is shown to the user) and Mark paid buttons, plus the other cash-out statuses. The page holds no data itself: the token stays in that browser tab's session storage and is sent only
as a header to the API below, under a strict content security policy. It's served only when `Admin:Token` is set, and
the admin API is rate-limited per IP. Serve it over HTTPS only.

Operator endpoints:

| Endpoint | Purpose |
|---|---|
| `GET /admin/summary` | Non-custodial: `custodial: false`, Otium's unMineable earnings, accounts by coin, referrals. Custodial: credited vs. pool-earned totals, money owed to users (balances and unpaid cash-outs), pool address load, bandwidth credited (GB and USD) to check against the partner's statements. Ledger totals still load when the pool API is down (`poolError`). |
| `GET /admin/cashouts?status=review` | The review queue (or `queued`, `sending`, `paid`, `failed`), with the account's email, age, lifetime earnings, payouts and how many other accounts used the destination |
| `POST /admin/cashouts/{id}/approve` | Release a reviewed cash-out for automatic sending |
| `POST /admin/cashouts/{id}/reject` | Reject it; the amount goes back to the balance (`{"note": "..."}`) |
| `POST /admin/cashouts/{id}/mark-paid` | Record a payout you made by hand (`{"note": "PayPal txn ..."}`) |

**Limits.** Requests are capped at 1 MB. Per internet connection (the IPv4 address or the IPv6 /64, counted in memory
only, never written down): 30 new installs a day, 10 sign-in code emails an hour and 30 a day, 20 problem reports
a day and 300 new sharing payout routes a day (`ConnectionQuotas`). Problem reports are also limited to 5 a day per
computer and per account, and stored ones stay under about 200 MB in all: past it the oldest are deleted. The per-minute rate limiter also
counts IPv6 per /64.

**Problem reports** (Settings → Send a problem report) carry the app version, settings switches, what each miner is
doing and its recent messages, GPU test results, sharing status, the pool balance and the antivirus name. Before
sending, the app removes payout and email addresses, keys, the device secret, saved cash-out addresses, and the
computer and user names, including in file paths (`Otium.Core.Support.ReportScrubber`). The server answers with a
reference; the operator reads it at `/admin/problem-reports/<reference>`.

The app finds the server through `AppIdentity.DefaultServerUrl`; development builds can override it with
`OTIUM_SERVER_URL`, release builds can't.

### Internet sharing (bandwidth)

Users can also opt in to sharing unused internet bandwidth. Non-custodial, this runs **Proxybase's seller client**
(proxybase.xyz; the open-source `proxybase-cli`, MIT/Apache-2.0, `src/Otium.Providers.Proxybase`). Proxybase sells
SOCKS5 access, mostly to AI agents and scraping/ad-verification jobs, and pays sellers per GB. It needs no account:
the client makes a wallet key on the computer and signs in with it. Proxybase pays only USDC on the Tempo network, so
each payout goes **through a one-time conversion to the user's own payout address and coin** (USDT on TRON, BTC, SOL…),
and users never see Tempo. Otium's cut is paid separately, never from the conversion, and the server checks on Tempo
that it arrived.

Next to Proxybase, users can also share through a **Mysterium node** (`src/Otium.Providers.Mysterium`, the
open-source `myst` 1.39.7 pinned for Windows x64, macOS and Linux x64), with its own switch, consent screen, daily
limit and row on the overview. It runs Mysterium's business (B2B) services only: scraping, data transfer and business
VPN. The node settles MYST on Polygon straight to the user's own 0x address at 5 MYST, minus Mysterium's 20% fee;
Otium takes nothing, and the server pays each node's one-time registration from a small wallet of its own
(`Mysterium:*` below). Otium can't convert MYST without holding it: Relay can swap it (SideShift and ChangeNOW don't
list MYST), and the app links there. The app shows earnings in dollars from the MYST price the server sends
(`MystUsd` in the account summary, from CoinGecko, refreshed hourly). The node runs in Mysterium's userspace mode
(`--userspace`): WireGuard and the forwarding of buyers' traffic run inside the node, so it needs no administrator
rights, network service (supervisor), tunnel driver or system NAT settings. (Its other mode needs Windows connection
sharing, iptables or pfctl to forward traffic, which a node running as the user can't set up: every buyer session
failed that way in October 2026.) Test versions 0.1.2-rc.1 to rc.3 installed Mysterium's supervisor service; Advanced
settings and the Windows uninstaller remove it. Microsoft Defender flags the Windows node (`Trojan:Win32/Pomal!rfn`, a
reputation detection); the app's "Allow in Windows Security" fix covers it. Share analytics report it as
`sharing-mysterium`, kept apart from Proxybase's `sharing`.

Users can also share disk space and upload bandwidth through an **AIOZ DePIN node** (`src/Otium.Providers.Aioz`,
AIOZ's official `aioz-depin-cli`, Apache-2.0, pinned to 1.2.5 for Windows x64 and Linux x64), with its own switch,
consent screen, storage limit and row. AIOZ's network stores pieces of files on the node and has it deliver them
(storage and CDN), and credits AIOZ tokens to the node's own key. The app reads `GET /stats` and
`GET /worker/balance` from the node's local API and, once 5 AIOZ is waiting, runs `reward withdraw` (at most daily) to
the user's own 0x address on the AIOZ network; Otium takes nothing. That address is the payout address when it's an
0x one, else the one given for Mysterium or for AIOZ in Settings; with none, the AIOZ waits in the node's key, so
turning AIOZ on never asks for an address. It can't be routed to the payout coin like Proxybase's earnings: Relay and
LI.FI don't support the AIOZ chain, and ChangeNOW and SideShift take only AIOZ's Ethereum and BNB Chain tokens
(October 2026). Dollar figures use `AiozUsd` in the account
summary (CoinGecko `aioz-network`, hourly). The node runs as the user and listens on `127.0.0.1` on a free port of
its own (`--laddr`; AIOZ's default 1317 is often reserved on Windows), with `--home` in `engines/aioz-data` so its
key (`privkey.json`, created without a password; the recovery words it prints are discarded), files and helper
programs stay there (without `--home` it uses `~/.aiozworker`), and `--no-auto-update`. 1.2.6 doesn't start (its
issue #10) and 1.2.5 has no storage limit of its own, so the app measures `home/data` every ten minutes and stops the
node at the user's limit (20 GB by default). macOS isn't offered: 1.2.5's two Mac archives are labelled `amd64` and
`x86_64`. Demand is thin (a test node in October 2026 got two storage tasks in ten minutes, both aborted with "client
credit is not enough"), and the consent screen says earnings are uncertain. Share analytics report it as
`sharing-aioz`.

- **Consent first.** Sharing is off until the user agrees: Quick start lists it with the rest, Customize and the
  overview explain it on its own (who the buyers are, what websites see, paid in their coin, Proxybase's terms). Users
  can share only while away.
  Proxybase reports no byte counts Otium can see, so there's no daily GB limit for it; the consent screen says so.
- **The client** is the pinned `proxybase-cli` 0.1.60, checked against GitHub's SHA-256 for each asset (its builds aren't
  signed): Windows x64, Linux x64/arm64 and Apple-silicon Macs (no Intel Mac build). It runs as
  `seller start --foreground` in direct mode only (this connection, no resold proxies; `seller_config.json` says so), dies
  with Otium, and keeps its wallet key, session and config in `engines/proxybase-data` (`PROXYBASE_DIR`). The key never
  leaves the computer and Otium never shows or stores its recovery phrase; only unpaid credits (under $1) depend on it.
  The client gets only an allow-list of Otium's environment (system folders, `PATH`, temp, language, proxy settings
  and its own data folder), not tokens or `PROXYBASE_*` overrides. Otium never replaces an existing wallet: when a
  wallet file is there but can't be read, sharing pauses with an explanation instead of creating a new one.
- **Payouts.** Every 10 minutes the app reads `buyer balance` (`seller_available`, in microcredits; 1,000,000 = $1).
  From $1 it asks the server (`POST /api/v1/devices/{id}/sharing/route`), which opens a one-time conversion to the
  account's **registered** payout address (the app can't redirect it) and answers "pay" with a Tempo deposit address, or
  "wait" while the network fee would be a large share. Each account gets at most one new route every 5 minutes (asking
  again inside that gets the same answer, so a modified app can't burn Relay, SideShift or ChangeNOW quotas), and the
  user's own IP address (IPv6 as it is) goes to SideShift as `x-user-ip`. The app then runs `seller payout create --amount …
  --tempo-address <deposit address>`; the conversion delivers the user's coin and refunds a failure to the client's own
  wallet on the computer. Payouts are kept in `otium-payouts.json`, so the overview shows what was paid.
- **Routes** (`src/Otium.Server/Sharing`): [Relay](https://docs.relay.link) deposit addresses (open, from Tempo USDC)
  deliver USDT on TRON, TRX, BTC, SOL, BNB and ETH directly. LTC goes Relay → USDC on Base → a SideShift variable shift;
  XMR goes Relay → USDC on Base → a ChangeNOW exchange (minimum about 12 USDC). Payouts wait while the network part of
  the cost is above `Sharing:MaxNetworkCostPercent` (5%), up to `Sharing:PayAnywayUsd` ($25): TRON's delivery fee
  (about $0.75 in October 2026) means USDT-on-TRON payouts of about $16.
- **Otium's cut:** `Sharing:FeeBps` (default: the mining fee, 2% = 200, at most 10%) of each payout, kept back at
  Proxybase (`otium-fee.json`) and paid by the app from the user's sharing earnings to `Sharing:FeeRecipient` (Otium's
  0x address, on Tempo). Only the rest is converted, with no app fee at Relay and SideShift's commission set to 0, so
  Otium earns nothing from exchanging coins. ChangeNOW's partner commission (from 0.4%) still applies on XMR.
- **The server checks the fee arrived** (`SharingFees`), since paying it is otherwise up to the app. A fee payment may
  come from Proxybase's own wallet, so the sender says nothing about whose it is; instead it's recognised by its
  **exact amount**. The app tells the server what it keeps back (`reservedFeeMicrocredits` in the route request); from
  $1 the server answers with `feePaymentMicrocredits`, the fee moved by a few microcredits (+1, −1, +2…, at most 500,
  never under $1) so that no other payment still on its way has the same amount, records it, and the app pays exactly
  that. Every 3 minutes the server reads Tempo (`Sharing:TempoRpcUrl`, default `https://rpc.tempo.xyz`, chain 4217) with
  `eth_getLogs` for the token's ERC-20 `Transfer` events (TIP-20 tokens emit them for every movement) to Otium's
  address and to recent routes' deposit addresses, from the last block read, `Sharing:TempoBlocksPerQuery` (10,000)
  blocks per request. A transfer of exactly a payment's amount settles it; a transfer into a deposit address makes
  that route's fee owed (so payouts that never happened aren't counted). No new route opens while a payment is more
  than `Sharing:FeeGraceHours` (2) overdue, or while the account owes more than $1 beyond the payout at hand; then the
  answer is "wait" ("Otium's share of your last sharing payouts hasn't arrived yet…") with the payment to make, out of
  what's waiting if nothing is kept back. A payment unseen for 3 days lapses and its fee counts as owed again. When
  Tempo hasn't been read in the last 30 minutes nothing is held: users don't lose payouts to Otium's outage, at the
  price of a modified app going unchecked meanwhile. Apps from before don't send what they keep back and aren't
  checked; `Sharing:RequireFeeCheck=true` tells them to update instead (turn it on once the app release that pays
  this way is out).
- **Server secrets:** `Sharing:RelayApiKey` (Relay requires one for quotes from October 2026), `Sharing:SideShiftSecret`
  and `Sharing:SideShiftAffiliateId` for LTC, `Sharing:ChangeNowApiKey` for XMR. Without the last two, LTC and XMR users
  are told to pick another coin for sharing.
- **Operator switch.** On by default; `Proxybase:Enabled=false` on the server hides it in every app (the account summary
  sends `proxybaseDisabled`).
- **Still to verify on a real run:** a node relaying traffic; which token Proxybase actually pays (Relay expects
  Stargate's USDC on Tempo, `0x20c0…8b50`); a first payout through Relay arriving as USDT on TRON; and that Relay's app
  fee is charged on open deposit addresses (`/requests/v3` shows `appFees.actual`); and that Proxybase's fee payouts
  arrive at `Sharing:FeeRecipient` as single transfers of exactly the amount asked for, in that token.
- **AV and code signing.** Proxyware is a common antivirus "potentially unwanted" category. Keep the sharing state
  visible in the app, and sign the Windows build with an EV certificate.

**When antivirus removes an engine**, the app says which file Microsoft Defender removed and when (from
`Get-MpThreatDetection`, readable without admin rights) and checks which antivirus is active (Windows Security
Center). With Defender, "Allow automatically" adds an exclusion for the engines folder after a UAC prompt; with another
antivirus, whose settings Otium can't change, it names the product and gives the folder to add as an exception.

**Custodial servers** can instead use the [Honeygain SDK](https://developers.honeygain.com/docs/), with Otium as the
publisher: Honeygain pays Otium per active device, and the server credits each user's balance from their computer's
usage reports. Builds with a Honeygain key (`OTIUM_HONEYGAIN_KEY`) use it instead of Proxybase.

Honeygain details:

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
     `OTIUM_HONEYGAIN_KEY` environment variable at runtime (development builds only).
  4. On the server, set `Bandwidth:Enabled=true`, `PartnerName` and the rates.
  
  Without the library or key, the option stays hidden.
- **Before launch, confirm with Honeygain:**
  - that rewarding users in cash for opting in is allowed,
  - whether they can supply per-device reports (the SDK's device id is available as `PartnerDeviceId`),
  - how they treat a connection that also runs their own consumer app.

## Your data: what Otium stores, export and deletion

Many users are in the EU and the Nordics, so the server supports the GDPR rights of access, portability and erasure.
The draft privacy policy is in [docs/privacy.md](privacy.md) and **still needs legal review**.

**What the server stores** (SQLite, `src/Otium.Server/Data/OtiumStore.cs`)

| Data | Why |
|---|---|
| Account: random id, pool worker id, invite code, email (if added), payout address with its coin, who invited it and when, fee-free period | Referrals (mining the referrer's share to their address, checking friends with their pool), sign-in; custodial: crediting mining |
| Computers: random device id (a hash of the machine id), a SHA-256 hash of its secret, first and last seen | Authenticating each computer |
| Invite links opened (7 days): the code and an HMAC-SHA256 of the visitor's IPv4 address or IPv6 /64, keyed with a random secret kept in the database (`InviteLinks`) | Filling in the code for Otium installed on the same connection (`GET /api/v1/invite-link`) |
| Custodial only. Ledger: every earning, referral reward, cash-out, refund and adjustment, in micro-dollars, with dates and a reference to the pool record | The balance, and bookkeeping |
| Custodial only. Cash-outs: method, destination address, amount, status, provider reference, review notes | Paying out, fraud checks, bookkeeping |
| Custodial only. Bandwidth reports: computer, public IP address, period, bytes, credited amount | Paying for internet sharing; one paid computer per connection |
| Email codes: salted hash, expiry, attempts | Sign-in; useless after 15 minutes |

The desktop app keeps its settings, a cached copy of the account summary and local earnings stats in its data folder
(Advanced → Settings → About → Open data folder). Mining pools (unMineable, MoneroOcean, and Kryptex in custodial mode), the email provider and the
bandwidth partner see what they need to do their part; [docs/privacy.md](privacy.md) lists them.

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

**Is the work paid?** After 90 minutes on a coin, the graphics card asks unMineable's public API whether its worker
has a counted ("calculated") hashrate; if not, the coin is set aside like any failure and the card mines the next one.
This is what confirms AMD cards on Pearl on real hardware.

**Blocked pools.** When a miner can't reach its pool for a couple of minutes, Otium opens a TCP connection to the pool
and to api.github.com. Pool unreachable but the internet fine means something on the computer blocks the pool (antivirus
web protection: Malwarebytes flags unmineable.com and moneroocean.stream), so the app names the active antivirus
(Windows Security Center) and says to allow the pool's site, instead of failing the coin over.

## Building the desktop app

You need the .NET 10 SDK. On this machine it's installed per-user, so add it to `PATH` for the session first:

```powershell
$env:PATH = "$env:LOCALAPPDATA\Microsoft\dotnet;$env:PATH"
```

Run the tests (covering payout addresses (TRON, Litecoin and the other coins' checksums) and unMineable logins, fee slices and the non-custodial server, accounts and sign-in, data export and deletion, crediting on MoneroOcean and Kryptex, CPU pool routing, crypto payouts (addresses, the Bitcoin treasury, THORChain swaps, sanctions screening) and receipts, the operator dashboard, the reverse proxy, invites and the leaderboard, fraud rules, GPU profit switching, policy and stall failover, internet sharing, live electricity prices, signed updates and the update feed, problem-report scrubbing, the simulated pool, wallet checksums and the engine):

```bash
dotnet test
```

`.github/workflows/ci.yml` runs them on Windows and Linux for every push to `main` and every pull request.

### Dev mode: the whole loop on one PC

```bash
pwsh build/dev.ps1
```

This starts everything locally, with no real mining and no real money:
- **Otium.DevPool**, a simulated MoneroOcean, unMineable (v5 API, auto-pay off for USDT like a real new TRC-20 address),
  Kryptex Pool and WhatToMine. Earnings are sped up 3000× so balances move in minutes; `-Speedup 1` shows real-world
  numbers.
- **Otium.Server**, non-custodial like production. With `-Custodial` it holds balances with dev timings: credits every
  20 s, a 1-minute hold, no new-account review, and fake payouts that are marked paid 15 s later.
- **The app** with simulated CPU and GPU miners.

Sign-in codes are printed in the console. Closing the app window stops everything.

| Option | Effect |
|---|---|
| `-Computers 2` | Two simulated PCs, to try signing in on a second computer |
| `-Reset` | Wipe dev data (`artifacts/dev`) first |
| `-Speedup N` | Change the earnings speed-up |
| `-Custodial` | Run the server with custody on (balances, cash-outs, Kryptex, internet sharing) |
| `-KryptexCpu` | With `-Custodial`: route every balance account's CPU to the simulated Kryptex Monero pool instead of MoneroOcean |

Operator dashboard while it runs: http://localhost:5080/admin (token `dev`). Or the raw summary:
`curl -H "X-Admin-Token: dev" http://localhost:5080/admin/summary`. Custodial, it compares what users were credited
against what the simulated pool paid, which is a quick check that crediting is right.

### Screenshots without opening a window

`dotnet run --project tools/Otium.Screenshots -- <folder>` (the README's pictures come from it) draws the welcome steps, Home (with every result card) and
Settings in light and dark, plus the share picture, into PNGs (Avalonia headless with Skia). It uses a temporary data
folder (`OTIUM_HOME`) and simulated miners, so it never touches a real install.

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

**Releasing:** push a tag such as `v0.1.0`. A tag with a suffix (`v0.1.0-rc.1`) is published as a GitHub
pre-release, which only release-candidate installs follow; see [release-candidate.md](release-candidate.md). `.github/workflows/release.yml` checks the release settings, tests, builds
every platform and publishes one GitHub release. Installed copies read it through the server's `/updates/{stable|pre}/`
feed, with GitHub itself (`AppIdentity.UpdateUrl`) as the fallback, and the website's `/download` page offers its
installers. Then sign the release's update feeds on the developer's PC (below); until then installed copies stay on
the previous signed release. The production server URL is baked in at build time from the `OTIUM_SERVER_URL`
repository variable (locally: `pwsh build/package.ps1 -ServerUrl https://...`); the workflow won't release without
it. Step by step, including the first release: [docs/releasing.md](releasing.md). Code signing (Authenticode,
Developer ID): [docs/code-signing.md](code-signing.md).

**Signed updates.** Every release's update feeds (`releases.<rid>.json`, which list each package with its SHA-256) are
signed on the developer's PC with an ECDSA P-256 key that never goes to GitHub or the server
(`tools/Otium.UpdateSigner`, see [release-candidate.md](release-candidate.md)). The app (`SignedUpdateSource`, over
`Otium.Core.Updates.SignedUpdates`) only uses a feed whose `.sig` checks out against `AppIdentity.UpdateSigningKeys`,
from otium.lol or GitHub alike, and only downloads packages that feed lists, keeping each one only if its SHA-256
matches. The server's `/updates/` feed offers only the newest release with a valid signature
(`UpdateFeed.SignedFeedAsync`) and redirects only to packages of signed releases.

## Project layout

```
src/Otium.Core               engine, provider interface, fees, power, GPU detection (NVML), profit switcher,
                             wallet checksums, account contracts and client
src/Otium.Providers.XMRig    CPU miner provider
src/Otium.Providers.SrbMiner GPU miner provider with benchmarking and profit switching
src/Otium.Providers.Proxybase internet sharing through Proxybase's client, converted to the user's own coin
src/Otium.Providers.Mysterium internet sharing as a Mysterium node, next to Proxybase (B2B only, paid in MYST)
src/Otium.Providers.Aioz     storage and upload sharing as an AIOZ DePIN node (paid in AIOZ, withdrawn to the user)
src/Otium.Providers.Honeygain internet sharing through the Honeygain SDK (custodial servers; loaded at runtime)
src/Otium.App                desktop app (Welcome → Home ⇄ Advanced), Velopack updates
src/Otium.Server             accounts and email sign-in, referrals, GPU policy, website (front page, /download,
                             invite pages), signed update feed, admin API; custodial crediting and cash-outs
src/Otium.DevPool            simulated MoneroOcean, unMineable, Kryptex Pool and WhatToMine for dev mode
tests/Otium.Tests            unit and API tests
tools/Otium.UpdateSigner     signs each release's update feeds on the developer's PC (keygen, sign, verify, check)
tools/Otium.Screenshots      draws the README's pictures without opening a window
build/                       dev.ps1 (dev mode), package.ps1 (installers), icon generator, icons
```

## Before launch

- **Relay API key** on the server (`Sharing__RelayApiKey`), so internet sharing payouts reach users' wallets, and one
  real sharing payout seen arriving. Then the release-candidate checklist: [release-candidate.md](release-candidate.md).
- **Legal review (required).**
  - Non-custodial (the default), pools pay users directly and Otium earns from its own fee slices and unMineable's
    referral rewards, so it doesn't receive or transfer users' money. Counsel should still confirm that this keeps
    Otium outside money-transmission (US) and crypto-asset-service (MiCA) rules, and review the fee and referral
    disclosures.
  - unMineable's terms exclude US persons; the app sends US users to MoneroOcean. Confirm that region check is enough.
  - Custodial mode holds users' earnings and pays them out: hosting balances can make the operator a money transmitter
    (US) and MiCA may apply (EU). Don't turn it on without counsel; collect tax details (W-9/W-8) before anyone passes
    the yearly reporting threshold.
  - Privacy: have [docs/privacy.md](privacy.md) reviewed (controller details, legal bases, retention periods,
    processor agreements with the hosting, email, payout and bandwidth providers, transfers outside the EU), then
    publish it and link it from the welcome screen and the landing page.
- **unMineable:** check Otium's own address has "Auto pay" on, and ask unMineable whether an app may put one referral
  code on its whole install base (and about a custom code with a higher tier).
- **Deploy the server** behind HTTPS with backups of the SQLite database. Set the `OTIUM_SERVER_URL` repository
  variable so release builds use it.
- **Custodial only: fund the Bitcoin treasury** (bitcoind) and keep only a few weeks of payouts in it; sweep the Kryptex
  account's BTC into it weekly. The admin dashboard shows its balance and warns when it's below the open cash-outs.
- **Code signing** for Windows and macOS: see [docs/code-signing.md](code-signing.md) (Azure Artifact Signing or
  a cloud-HSM certificate for Windows, Developer ID and notarization for macOS).
- **Real-machine run:** mostly done; see [docs/real-mining-run.md](real-mining-run.md). SRBMiner's console "Total"
  line (its stats API is no longer used), H/s units (including `pearlhash`/`quantus` on Kryptex), NVML power, stall
  failover and miners dying with the app are confirmed. On an RTX 3080 the Kryptex coins earn through Otium; MoneroOcean's GPU algorithms fail in SRBMiner's OpenCL
  path there. Still open: other NVIDIA machines, and the Kryptex account setting on the deployed server.
- **Kryptex (custodial only):** confirm in writing that one operator account may aggregate an app's whole install base, and ask how Pearl's
  August 2026 "rank-penalty" softfork treats a large pooled account. Kryptex runs a competing consumer app, so keep
  MoneroOcean as the fallback route.
