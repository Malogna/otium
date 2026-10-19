# Plan: make Otium non-custodial (users' own addresses, unMineable, Mysterium)

## Context

Otium currently holds users' earnings as USD balances on its server and pays them out from a Bitcoin treasury. That
makes Otium the party that receives and transfers users' money (regulated under MiCA / US money transmission rules)
and needs a treasury, swaps, sanctions screening, review queues and manual sweeps. The user decided to go
non-custodial: users paste their own payout address, the pool pays them directly in their chosen coin, Otium earns
through mining time slices plus a pool referral code, and internet sharing moves to Mysterium nodes whose payouts go
to the user's own wallet. The custodial code stays in the repo but is switched off.

Decisions (from the user):
- **Fee:** 2% of mining time to Otium's address (existing own-wallet slicing) + Otium's unMineable referral code on
  every worker (users pay 0.75% instead of 1%; Otium earns 0.25–0.50% of users' rewards).
- **Default payout coin:** USDT on TRON (threshold 1.5 USDT ≈ $1.50, daily). Also offered: BTC, LTC, ETH (BSC),
  BNB (BSC), SOL, TRX, XMR.
- **US users:** unMineable's terms exclude US persons, so US-region users mine on MoneroOcean paid in XMR (the
  existing own-wallet path), with a plain explanation in the app.

Key facts from research (October 2026, details in the agents' notes):
- unMineable stratum: `<algo>.unmineable.com` (+ `-us/-eu/-asia`). RandomX: `rx.unmineable.com`, SSL only on 443.
  GPU: `pearlpow`, `xelishash` (SRBMiner `xelishashv3`), `fishhash`, `etchash`, `autolykos` hosts, SSL 4444/443.
  KawPow is gone. Pearl is NVIDIA-only there. Quantus is not offered (Kryptex only), so it's lost.
- Login `COIN:ADDRESS.WORKER#REFCODE`, password `x`. Worker: letters, digits, `_`, `-`. No network in the login:
  the coin's default chain is used if the address fits it (USDT + `T…` address → TRC20).
- Public stats API (use **v5**): `/v5/address/{addr}?coin=SYM` (uuid, balance, payable, threshold, `auto`,
  `err_flags`, referral_code), `/v5/account/{uuid}/stats` (rewarded 24h/7d/30d, paid), `/workers`, `/payments`.
  Auto-pay can be off per address (`auto:false`) → the app must warn. Balances are forfeited after 6 months idle.
- Mysterium: the open-source `myst` node runs headless; the default services (`scraping,data_transfer,dvpn`) are
  already B2B-only; the local Tequila API (127.0.0.1:4050) can create/unlock an identity and register it with the
  **user's Polygon address as beneficiary**. Windows/macOS need a one-time admin install of `myst_supervisor`
  (WireGuard/Wintun). Earnings settle in MYST on Polygon at 5 MYST with a 20% network fee. Registration currently
  costs ~0.13 MYST and how a zero-balance identity pays it is unverified → needs a spike first.

## Approach

### Phase 0 — real-world spike (½ day, before writing integration code)
- Mine 10 minutes each with XMRig (RandomX) and SRBMiner (Pearl, NVIDIA) to unMineable with a test USDT-TRC20 address
  and Otium's referral code; confirm the login format, the `#code` attribution, and the exact v5 `workers`/`stats`
  JSON (field names, units, update delay) for an address with live workers. Save fixtures for tests.
- Run a `myst` node locally (Windows), register an identity with a beneficiary via Tequila, and note what happens
  with the registration fee, admin prompts and antivirus. Decide go/no-go for Phase 5 from this.

#### Phase 0 results (17 October 2026, 15:35–15:45 UTC)

Otium's own payout target is `USDT:TB2xNFAjGarYPykmt1dZQz8hJb2Rhry4wJ` (TRC-20) and its unMineable referral code is
**`ztjq-jr7s`**. The code comes from unMineable's public API without signing up anything:
`/v5/account/{uuid}/referrals` returns `code` for any address unMineable has seen.

| Check | Result |
|---|---|
| XMRig 6.26.0, `rx.unmineable.com:443` with TLS, login `USDT:T….otiumspikecpu`, password `x` | ✅ TLS 1.3, 7 of 7 shares accepted in 10 minutes at difficulty 100,001 (50% of an i7-8700K, about 1.75 kH/s). |
| SRBMiner 3.7.6, `--algorithm pearlhash`, `pearlpow.unmineable.com:443 --tls true`, same login form | ✅ RTX 3080 at about 112 TH/s and 340 W; 1 of 1 share accepted at difficulty 4,194,304 (one share every ~2.7 minutes on average, so 10 minutes is a thin sample). |
| Referral code on another address (`BNB:0x….otiumspikeref#ztjq-jr7s`, 8 minutes CPU) | ✅ The worker lists `"referral":"ztjq-jr7s"`, the referred address's `mining_fee` is `0.75` instead of `1`, and Otium's `/referrals` list shows one miner. |
| Balance | ✅ 0.000402 USDT on Otium's address after the run, shown as `balance_mining`. `rewarded.past_24h` stays 0 until the daily cut-off. |
| `/v5/account/{uuid}/workers` | ✅ Per algorithm: `workers[{uuid, online, name, last (ms), rhr, chr (strings, H/s), referral}]` and 5-minute `chart` points. `chr` is the last hour's accepted share difficulty ÷ 3600 s (7 × 100,001 ÷ 3600 = 194.4 H/s; Pearl: 1 × 4,194,304 × 2³² ÷ 3600 = 5.0 TH/s), so it lags real hashrate for the first hour. Fixtures are in `PayoutTargetTests.cs`. |
| Auto-pay | ⚠️ A new USDT (TRC-20) address starts with `auto:false`; the new BNB (BSC) address started with `auto:true`. With auto-pay off, unMineable never pays by itself: the owner must tick "Auto pay" (or press "Pay out") on the address page, which posts to `/v1/address/{uuid}/setting/auto_pay`. Otium must not change that for users, so Phase 4 shows a warning with a link to the address page whenever `auto` is false. |

Plan changes from these results:
- Phase 2: the `#code` suffix works as documented; build it into every unMineable login. Use TLS on 443 for both
  CPU and GPU (unMineable's guide shows plain 3333 for Pearl; 443 with TLS also works).
- Phase 4: the "At the pool" panel must say plainly when auto-pay is off and link to the address page.
- Hashrate shown from unMineable is an hourly average; the app should keep showing the miner's own hashrate and use
  unMineable's only to confirm the worker is seen.

#### Phase 0 results: Mysterium node (17 October 2026, 16:05–16:15 UTC)

`myst` 1.39.7 for Windows (`myst_windows_amd64.zip`, 24.5 MB, SHA-256 matches GitHub's `1c1cced4…ef1c`), run with
its own data folder, the web UI off and the local API (Tequila) on 127.0.0.1:4050 with a generated password.

| Check | Result |
|---|---|
| Binaries | ⚠️ `myst.exe` and `myst_supervisor.exe` are **not Authenticode-signed**. Expect SmartScreen and antivirus friction; Otium should verify the SHA-256 (as it does for XMRig/SRBMiner) and say so in the consent screen. |
| `myst daemon` without admin | ✅ The API is up in about 5 seconds. On start it runs PowerShell to switch off Windows Internet Connection Sharing; without admin that fails with a warning and nothing else breaks. |
| Identity | ✅ `PUT /identities/current {"passphrase":""}` creates and unlocks one (`0x8371…712d`, channel `0x4061…24E6`). The keystore lives in the node's data folder, so Otium must keep that folder (losing it loses unsettled earnings). |
| Registration | ❌ **Not free here.** `GET /transactor/fees`: registration 0.1297 MYST, settlement 0.0593 MYST, Hermes fee 20%. `GET /identities/{id}/eligibility` and `/identities/provider/eligibility` both say `false`. `POST /identities/{id}/register {beneficiary, fee, referral_token}` is free only with a referral token or when eligible; otherwise the fee comes out of MYST sent to the channel address. Not registered: no settlement ("Provider … not registered, skipping"). |
| Supervisor | ✅ `myst_supervisor -install -uid <user>` with one UAC prompt installs the auto-start SYSTEM service "MysteriumVPN Supervisor" (pipe `\\.\pipe\mystpipe`, open to the Users group). `-uid` is required but unused on Windows. |
| Services | ⚠️ `POST /services {"provider_id","type":"scraping"}` started `scraping` **and** `quic_scraping` at once, with **no terms accepted and no registration**, and announced this PC's residential IP (Oslo, Telia) with the `mysterium` (B2B) access policy at 0.000628 MYST/hour plus 1.13 MYST/GiB (`quic_scraping` 0.19 MYST/GiB). Stopped after about 10 seconds with no sessions. Otium must not start services before the user has agreed to Mysterium's provider terms (`POST /terms`) in its consent screen. |

Decision (17 October 2026): Otium pays each node's registration from a small Polygon wallet of its own (option 2
below). Follow-up research found no other bandwidth network that pays users' own wallets in a liquid coin without a
per-user sign-up, stake or special hardware (in the operator's research notes), and found that Mysterium's
registration can be funded by sending MYST to the identity's channel address before `POST /identities/{id}/register`
(the registry contract takes the fee from the channel; Mysterium's transactor pays gas). Built in Phase 5.

What this means for Phase 5:
- **Registration is the blocker.** Each user's node needs 0.13 MYST (or a referral token) before it can settle a
  single payout. Options, best first: (1) ask MystNodes for a partner arrangement where Otium's referral token
  sponsors registration; (2) Otium pays the fee by sending MYST to each new node's channel address (a small treasury
  of MYST on Polygon, about 0.13 MYST per user, with fraud limits); (3) the user claims the node on mystnodes.com
  (`GET /mmn/onboarding`), which needs their own MystNodes account. Option 3 is the only one that needs no money
  from Otium but breaks "one click".
- Keep the beneficiary: registration takes the user's Polygon address (`beneficiary`), so earnings still settle to
  the user's own wallet; Otium never holds them.
- Start services only after the terms step, with `--active-services` limited to the B2B types, and clean up the
  supervisor service on uninstall (`myst_supervisor -uninstall`, another UAC prompt).

### Phase 1 — payout address model (Core)
- New `PayoutAddress` value type in `src/Otium.Core/Wallets/` (e.g. `PayoutTarget(Coin, Address)`), parsed from and
  formatted as `COIN:address`, with a curated coin list (name, ticker, network, unMineable symbol, threshold text,
  address placeholder) and a canonical form (upper-case coin; lower-case 0x/bech32) for comparisons.
- Validation reuses `CryptoAddresses.TryValidate` (BTC, EVM for ETH/BNB on BSC, SOL, XMR via `MoneroAddress`) and adds
  TRON (base58check `T…`, version 0x41) and Litecoin (`ltc1…` bech32, `L…`/`M…` base58check).
- `AppSettings`: `PayoutMode` default becomes `OwnWallet`; `PayoutAddress` holds `COIN:address` (migrate a bare XMR
  address to `XMR:<addr>` on load). `AppIdentity.FeeWalletAddress` becomes `COIN:address` too (build-time setting
  `OtiumFeeWallet`, plus new `OtiumUnmineableReferral`).
- New `UnmineableClient` in `src/Otium.Core/Pools/` (v5 address/stats/workers/payments), mirroring `MoneroOceanClient`.

### Phase 2 — mining to unMineable with fee and referral slices
- **Fix the route/slice conflict:** today `CpuPoolRoute.User` overrides the slice target, so fee slices would mine to
  the user. Add a route kind whose login is built from `ProviderRunOptions.Target.WalletAddress` per slice.
- **CPU:** `XmrigConfig.Build` gets an unMineable branch: `rx.unmineable.com:443`, TLS, user
  `{COIN:addr}.{SanitizeWorker(worker)}#{ref}` (sanitize only the worker part), pass `x`. US region → existing
  MoneroOcean path (XMR address required). Region from `RegionInfo`, overridable in Advanced.
- **GPU:** `GpuRoutes.Unmineable` in `src/Otium.Core/Switching/GpuPolicy.cs`; `PoolLogin.Unmineable(endpoint,
  target, worker, ref)` in `SrbMinerArgs.cs`; `LoginFor` in `SrbMinerGpuProvider.cs` uses it (target from the slice);
  `GpuAlgorithms.All` gets `xelishash`(`xelishashv3`) and `fishhash`, Pearl's default route becomes unMineable;
  `WithoutOperatorPools()` becomes "drop routes with an operator `User`" so unMineable stays for own-wallet users.
- **Slices:** keep `FeeSchedule` (2% fee, half to the referrer for a year, fee-free holidays). Targets are `COIN:addr`
  strings: Otium's fee address and the referrer's own payout address. Normalize the user-vs-referrer comparison.
  Lengthen `FeePolicy.CycleLength` (3h → 24h) so each slice lasts ~29 minutes instead of ~2, cutting miner restarts
  and warm-up waste.
- Payout balance: providers' `GetPayoutBalanceAsync` read unMineable (or MoneroOcean for US/XMR) and return the coin,
  payable balance, threshold, auto-pay flag and errors; `Format` gets a generic coin amount formatter.
- Dev mode: the dev pool gets unMineable-shaped endpoints; `SimulatedProvider` reports `unmineable:{COIN}:{addr}`.

### Phase 3 — server: one "non-custodial" switch
- New option `Custody:Enabled` (default **false**). When false:
  - Skip the four custodial hosted services (earnings creditor, Kryptex creditor, cash-out processor, sanctions
    refresher) and map no `/cashouts`, `/bandwidth` or admin cash-out routes.
  - `AccountService.RegisterAsync` requires a valid `COIN:address` (400 otherwise) and assigns no pool wallet;
    `Summarize` returns `Custodial = false` (new `AccountSummary` field), an empty `BalanceInfo` (no methods), no CPU
    route, no bandwidth terms; `ReferrerWallet` is the referrer's payout `COIN:address`.
  - Referrals: `TryBind`/`WalletAlreadyReferred` compare canonical addresses. Referee verification uses
    `UnmineableClient` (`rewarded` > small amount, or MoneroOcean hashes for XMR users) instead of only MoneroOcean.
  - `GpuPolicyService` adds unMineable routes (Pearl, Xelis, FishHash primary when profitable; Etchash/Autolykos
    free-power-only) priced from WhatToMine minus 0.75%, and skips Kryptex routes.
  - Admin dashboard (non-custodial view): accounts, referrals, and Otium's own fee address and referral earnings read
    from unMineable, instead of treasury/queue panels.
- Custodial code stays compiled and tested with `Custody:Enabled=true` in its tests.

### Phase 4 — app UI
- **Onboarding** (`WelcomeView`/`WelcomeViewModel`): new step "Where should we pay you?": coin picker (USDT TRON
  default; US → XMR), address box with live validation and network hint, links to wallets that support the coin.
  Start is disabled until the address is valid.
- **Settings** (`SettingsView`): the payout section becomes coin + address (no Balance option when the server says
  `Custodial = false`); Save re-registers.
- **Home/Dashboard**: replace the dollar balance and cash-out with "At the pool: 0.84 USDT · paid out daily at 1.5 USDT
  (≈ in 2 days)", last payouts with explorer links, and warnings for `auto:false`, payment errors, and the 6-month
  idle forfeit. Cash-out panel, history and "protect your balance" stay hidden (already gated by balance mode).
- Copy: Welcome/Settings/Invite texts, README "For users", release notes.

### Phase 5 — Mysterium (only if the Phase 0 spike works)
- New `src/Otium.Providers.Mysterium` implementing `IBandwidthProvider`: download the pinned `myst` release with
  `EngineInstaller` (SHA-256), one-time elevated `myst_supervisor -install` (UAC prompt, explained in the consent
  screen), run headless (`--ui.enable=false --tequilapi.address=127.0.0.1 service --agreed-terms-and-conditions
  --active-services=scraping,data_transfer,dvpn`), create/unlock the identity and register it with the user's
  Polygon beneficiary via Tequila; orphan cleanup like the miners.
- Beneficiary: the user's EVM address if their payout coin is on BSC/ETH, otherwise a separate "Polygon address"
  field. Earnings shown from the local API (`/identities/{id}`, settle history); no server reports.
- `BandwidthSharer` gets an optional report delegate (none for Mysterium) and node-reported earnings; Honeygain stays
  available only when custody is on. No Otium fee on bandwidth in v1 (ask MystNodes about partner terms).

### Phase 6 — docs, privacy, tests
- README, `docs/deploying.md`, `deploy/.env.example` (Custody:Enabled, unMineable referral), `docs/privacy.md` (no
  balances, ledger or bandwidth IP logs in non-custodial mode), release notes.
- Tests: payout-address parsing/validation (TRON, LTC vectors), XMRig/SRBMiner unMineable logins per slice, fee
  schedule with `COIN:addr` targets, unMineable client against Phase 0 fixtures, server non-custodial registration,
  referrals and summary, GPU policy unMineable routes, region fallback, Mysterium Tequila client against fixtures.

## Critical files
- Core: `src/Otium.Core/Settings/AppSettings.cs`, `AppIdentity.cs`, `Wallets/CryptoAddresses.cs` (+ new payout
  address type), `Pools/` (new `UnmineableClient`), `Fees/FeePolicy.cs`, `Fees/FeeSchedule.cs`,
  `Providers/ProviderModels.cs`, `Providers/SimulatedProvider.cs`, `Switching/GpuPolicy.cs`,
  `Accounts/AccountContracts.cs`, `Bandwidth/BandwidthSharer.cs`.
- Providers: `src/Otium.Providers.XMRig/XmrigConfig.cs`, `XmrigProvider.cs`;
  `src/Otium.Providers.SrbMiner/SrbMinerArgs.cs`, `SrbMinerGpuProvider.cs`, `SrbMinerRelease.cs`;
  new `src/Otium.Providers.Mysterium/`.
- App: `src/Otium.App/Services/AppHost.cs` (BuildConfig, ValidWalletOrNull, MiningBlocker, EffectiveGpuPolicy,
  bandwidth wiring), `ViewModels/WelcomeViewModel.cs`, `SettingsViewModel.cs`, `HomeViewModel.cs`,
  `DashboardViewModel.cs`, `InternetSharingViewModel.cs`, matching views.
- Server: `src/Otium.Server/Program.cs`, `ServerOptions.cs`, `Accounts/AccountService.cs`, `Data/OtiumStore.cs`,
  `Gpu/GpuPolicyService.cs`, `AdminDashboard.cs`, `src/Otium.DevPool/Program.cs`.

Reused as-is: `FeeSchedule`/`EarningsEngine` slice switching, `CryptoAddresses` validators, `EngineInstaller`,
`MinerLifetime` orphan cleanup, `IBandwidthProvider`/`BandwidthSharer` loop and consent flow, `WhatToMineClient`.

## Verification
- `dotnet test` (all existing tests keep passing with custody on in custodial tests; new tests above).
- Dev mode (`pwsh build/dev.ps1`) with the simulated unMineable pool: onboarding asks for a USDT address, mining
  reports to `unmineable:USDT:<addr>`, fee slices report to Otium's address and the referrer's, the overview shows the
  pool balance and threshold; `Custody:Enabled=false` shows no cash-out UI and `/cashouts` returns 404.
- Real check: 15 minutes of CPU and GPU mining to a real USDT-TRC20 address with the referral code; confirm the
  worker, hashrate and referral appear in unMineable's v5 API and in the app; Otium's fee slice appears on Otium's
  address. Mysterium: register a node with a test beneficiary and see it online with B2B services only.
- Commit per phase on a new branch from the integration branch; push and PR only when asked.
