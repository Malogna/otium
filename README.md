# Otium

*Latin for "idle time".* Otium is a one-click desktop app for Windows, macOS and Linux that earns money from a computer's spare power while nobody is using it. People cash out with PayPal or crypto.

| First launch | What this PC could earn | Overview | Advanced |
|---|---|---|---|
| ![Welcome](docs/welcome.png) | ![Estimate](docs/estimate.png) | ![Overview](docs/home.png) | ![Advanced](docs/advanced.png) |

<sub>Dev mode at real-world speed: simulated i7-8700K and RTX 3080 with electricity at $0.15/kWh, where mining doesn't pay. Otium says so plainly instead of promising money.</sub>

## For users: one button

1. Install and open Otium. Three short screens explain it, show what this computer could earn per month (from its
   processor and graphics card, today's payouts and your electricity price), and summarise what will happen.
2. Press **Start earning**. No wallet and no settings are needed.
3. Otium starts when you sign in and earns only while you're away from the computer, after 3 minutes without keyboard or mouse input. It pauses the moment you come back, and also when electricity would cost more than it earns (the price is estimated from your country).
4. Your balance grows in dollars. Add your email when Otium asks; that keeps the balance safe if you reinstall, lets you
   sign in on your other PCs (they all earn into one balance), and is needed before the first cash-out.
5. Press **Cash out** to get paid by PayPal or crypto (Monero).
6. Share your invite code. Friends mine fee-free for 30 days, and you get 5% of everything they earn.

The overview shows, at a glance: the balance and progress to the first cash-out (with an ETA), what each device is
doing right now in plain words ("Mining Monero", "Waiting for you to step away · starts in 1:42"), what you can expect
today, this week and this month, and an hour-by-hour chart of the last 24 hours. Projections use how many hours a day
this computer actually earns once there is a day of history.

Everything technical lives under **Advanced**:
- Details per device, including GPU algorithm and speed, power draw, and profit per day.
- CPU usage, the idle delay, your electricity price, and switching devices on or off.
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
                                                              PayPal Payouts API / monero-wallet-rpc
```

**Mining**
- The CPU runs XMRig. The GPU runs SRBMiner on whichever algorithm pays most after fees and measured electricity (kawpow, autolykos2, etchash, cn/gpu or pearlhash). MoneroOcean converts everything to XMR.
- Each account mines under its own worker id to one of the operator's pool addresses.

**Crediting** (`src/Otium.Server/Earnings`)
- Every 10 minutes the server reads MoneroOcean's per-worker hashrate history. It uses `hs2`, the XMR-equivalent hashrate, so CPU and every GPU algorithm are valued on one scale.
- It credits the expected value: hashes × block reward ÷ difficulty × XMR price.
- Credits are reduced by the fee (10% by default; 0% during a referral fee-free period).
- Credits are held 48 hours before they can be cashed out.
- Referrers get 5% of a friend's gross earnings, paid out of the fee.
- The operator carries the pool's luck. `GET /admin/summary` compares what was credited against what the pool actually paid.

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
- **Sent automatically:**
  - Crypto through `monero-wallet-rpc`: up to 15 cash-outs share one transaction, and Otium pays the network fee.
  - PayPal through the PayPal Payouts API: one batch per cash-out, using the cash-out id as `sender_batch_id` so a retry can never pay twice.
- **Held for review:**
  - accounts younger than 2 days;
  - amounts over $50;
  - a destination already used by another account;
  - the daily automatic payout budget being used up;
  - a yearly total near the US tax-reporting threshold ($2,000; tax details are needed);
  - the method's provider not being set up yet.
- Failed or returned payouts go back to the balance.

**Own-wallet mode** (Advanced) works as before: the pool pays the user's own XMR wallet directly, a 2% fee is mined in short time slices, and referral rewards are mined to the referrer's wallet.

## Running the server

Configure `src/Otium.Server/appsettings.json` (or environment variables such as `Earnings__PoolWallets__0=...`):

| Setting | Purpose |
|---|---|
| `ConnectionStrings:Otium` | SQLite database path |
| `Earnings:PoolWallets` | Operator XMR addresses (subaddresses of one wallet). Balance mode is off until at least one is set or the wallet RPC is configured. |
| `Earnings:FeePercent`, `ReferralPercent`, `HoldPeriod` | Business terms; defaults 10%, 5%, 48 h |
| `Cashouts:*` | Minimums ($5 PayPal, $2 crypto), automatic-approval limit, daily budget, minimum account age |
| `PayPal:ClientId`, `ClientSecret`, `UseSandbox` | PayPal Payouts. Use the sandbox until PayPal approves the live account. |
| `MoneroWallet:RpcUrl`, `Username`, `Password` | `monero-wallet-rpc` with `--rpc-login`, on localhost or a private network |
| `Admin:Token` | Enables `/admin/*`; send it as `X-Admin-Token` |
| `Email:SmtpHost`, `Port`, `Username`, `Password`, `From` | SMTP service for sign-in codes (Postmark, SES, Mailgun, ...) |
| `Referral:PublicBaseUrl`, `DownloadUrl` | Invite links and the landing page |

Run it locally:

```bash
dotnet run --project src/Otium.Server
```

Operator endpoints:

| Endpoint | Purpose |
|---|---|
| `GET /admin/summary` | Credited vs. pool-earned totals, money owed to users, pool address load |
| `GET /admin/cashouts?status=review` | The review queue |
| `POST /admin/cashouts/{id}/approve` | Release a reviewed cash-out for automatic sending |
| `POST /admin/cashouts/{id}/reject` | Reject it; the amount goes back to the balance (`{"note": "..."}`) |
| `POST /admin/cashouts/{id}/mark-paid` | Record a payout you made by hand (`{"note": "PayPal txn ..."}`) |

The app finds the server through `AppIdentity.DefaultServerUrl`, which is overridable with `OTIUM_SERVER_URL`.

## Building the desktop app

You need the .NET 10 SDK. On this machine it's installed per-user, so add it to `PATH` for the session first:

```powershell
$env:PATH = "$env:LOCALAPPDATA\Microsoft\dotnet;$env:PATH"
```

Run the tests (119, covering accounts and sign-in, crediting, cash-outs, fraud rules, GPU profit switching, the simulated pool, wallet checksums and the engine):

```bash
dotnet test
```

### Dev mode: the whole loop on one PC

```bash
pwsh build/dev.ps1
```

This starts everything locally, with no real mining and no real money:
- **Otium.DevPool**, a simulated MoneroOcean (same API). Earnings are sped up 3000× so balances move in minutes.
- **Otium.Server** with dev timings: credits every 20 s, a 1-minute hold, no new-account review, and fake PayPal/Monero
  payouts that are marked paid 15 s later.
- **The app** with simulated CPU and GPU miners.

Sign-in codes are printed in the console. Closing the app window stops everything.

| Option | Effect |
|---|---|
| `-Computers 2` | Two simulated PCs, to try signing in on a second computer |
| `-Reset` | Wipe dev data (`artifacts/dev`) first |
| `-Speedup N` | Change the earnings speed-up |

Server admin summary while it runs: `curl -H "X-Admin-Token: dev" http://localhost:5080/admin/summary`. It compares what
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

**Releasing:** push a tag such as `v0.2.0`. `.github/workflows/release.yml` tests, builds every platform and publishes one GitHub release. To turn on auto-updates for installed copies, set `AppIdentity.UpdateUrl` to the repository URL.

## Project layout

```
src/Otium.Core               engine, provider interface, fees, power, GPU detection (NVML), profit switcher,
                             wallet checksums, account contracts and client
src/Otium.Providers.XMRig    CPU miner provider
src/Otium.Providers.SrbMiner GPU miner provider with benchmarking and profit switching
src/Otium.App                desktop app (Welcome → Home ⇄ Advanced), Velopack updates
src/Otium.Server             accounts and email sign-in, crediting, cash-outs, referrals, landing page, admin API
src/Otium.DevPool            simulated MoneroOcean for dev mode
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
- **PayPal approval.** Payouts must be enabled on a business account, and PayPal pre-approves crypto-related businesses. Describe the mining revenue source honestly. Until approved, PayPal cash-outs land in the review queue for manual payment.
- **Deploy the server** behind HTTPS with backups of the SQLite database. Set `AppIdentity.DefaultServerUrl`.
- **Fund the hot wallet** in `monero-wallet-rpc` and keep only what payouts need in it.
- **Code signing** for Windows and macOS: add the certificates as the CI secrets listed in the workflow.
- **Real-machine run:** do a real mining run on your RTX 3080 to confirm SRBMiner's stats format and the GPU speed units.
