# Bandwidth and idle-compute options with no user sign-up that let Otium take a fee (as of 17 Oct 2026)

The question: which ways to earn from idle bandwidth or non-mining idle compute need **no per-user registration** with a third party **and** let Otium take a fee? This builds on earlier notes in `Non-custodial bandwidth/`, `Bandwidth partners and automatic payouts/`, `More idle earning integrations/` and `Selling idle compute for money/`. Nothing was signed up for, downloaded or run. The only code reads were public GitHub sources fetched as text.

Prices are from the CoinGecko simple-price API, 17 Oct 2026:

| Token | Price | 24h volume |
|---|---|---|
| MYST | $0.0784 | $15k |
| GLM | $0.114 | $1.29M |
| TFUEL | $0.0088 | $208k |
| KOII | $0.00002 | $1.3k (market cap $22k) |
| GRC | $0.0068 | $1.3k |
| STORJ | $0.0257 | |
| AKT | $0.70 | |
| FLT (Fluence) | $0.0054 | |
| TAO | $272 | |
| NOS | $0.50 | |
| XTM (Tari) | $0.0014 | $21k |

## Verdict

- Only **Mysterium** meets all three conditions at once: no user sign-up, Windows and headless support, and a fee mechanism that keeps Otium non-custodial.
- The fee has to be built **on-chain by Otium**. Mysterium has no published partner revenue share. The 20% Hermes fee goes to Mysterium. The referral bonus needs MystNodes accounts.
- The take is tiny, because home nodes earn little. Independent estimates put a node at about $1.2/month, so a 2% fee is about $0.02/PC/month.
- The **publisher SDKs** (Honeygain, Massive, Decodo, Pawns, Infatica, Bright) also need no user sign-up, and Otium would hold the revenue. They pay $0.05–0.60 per user/month in fiat (PayPal or bank), and they make Otium custodial.
- Every compute network found fails on at least one point: Linux/KVM only, a stake or collateral, an operator application, an account, or a dead token.

## 1. Mysterium: can Otium take a fee?

### What Mysterium offers (none of it is a revenue share)
- **Partner SDK client.** `mysteriumnetwork/mystnodes-sdk-client` runs with `--user.token` "obtained from mystnodes team". No revenue-share terms are published, and `mystnodes.com/sdk` and `/terms` return 404 ([Docker Hub](https://hub.docker.com/r/mysteriumnetwork/mystnodes-sdk-client)). The homepage "For Business" button only leads to account registration ([mystnodes.com](https://www.mystnodes.com/)).
- **Affiliator free registration.** Partners log in to sentinel, call `free-registration/partner`, and register with a beneficiary per node. This saves the 0.13 MYST fee but pays nothing ([node-partner-guide](https://github.com/mysteriumnetwork/node-partner-guide); see the earlier note `Non-custodial bandwidth/mysterium_automatic_registration.md`).
- **`--vendor.id`.** The flag help says it "Marks vendor (distributor) of the node for collecting statistics". It is statistics only, not payment ([flags_node.go](https://github.com/mysteriumnetwork/node/blob/master/config/flags_node.go)).
- **Referral.** The help article says 5 MYST to the referrer and 5 MYST to the friend, once, after the friend's first node reaches 5 MYST. The friend must be new, must have *claimed the node on MystNodes.com*, and must be on a residential IP. The referrer must add a wallet to their MystNodes.com account ([help](https://help.mystnodes.com/en/articles/8836815-how-does-the-referral-system-work)). The homepage instead says "earn 5% of their rewards every month" ([mystnodes.com](https://www.mystnodes.com/)), which conflicts with the article. The exit-node Terms §15 treat "creation of multiple accounts" as fraud ([TERMS_EXIT_NODE.md](https://github.com/mysteriumnetwork/terms/blob/master/documents/TERMS_EXIT_NODE.md)). **Verdict: not usable without per-user accounts.**
- **MystNodes Rewards System.** Points come from data shared, uptime (at least 14 h/day) and holding 100+ MYST. They convert monthly from a pool of unpublished size and must be claimed manually within 30 days, otherwise they are forfeited. Rewards are tracked via a "connected wallet" ([help](https://help.mystnodes.com/en/articles/13925332-mystnodes-rewards-system); [blog](https://www.mystnodes.com/blog/mystnodes-launches-rewards-system)).
  - Nodes can be claimed headlessly into one account with `--mmn.api-key` ([flags_mmn.go](https://github.com/mysteriumnetwork/node/blob/master/config/flags_mmn.go)). In principle, Otium could claim users' nodes into Otium's account and collect the pool share without touching users' bandwidth earnings.
  - **Unverified:** whether CLI nodes count, the pool size, and the anti-abuse rules.
  - Claiming also gives Otium's dashboard control over users' nodes, including "Update Wallet", which is a trust issue.
  - Treat this as something to ask Mysterium about, not as a plan.
- **Hivello precedent.** Hivello, a DePIN aggregator that runs MystNodes, reportedly keeps 10–50% of tokens mined through its app. Holding its HVLO token raises the user's share from 50% to 90% ([Solana Compass](https://solanacompass.com/projects/hivello)). Hivello is custodial and needs an account, but it shows that an aggregator fee is accepted in this market.

### Fee mechanisms Otium can build itself (non-custodial)
Hermes contract facts ([HermesImplementation.sol](https://github.com/mysteriumnetwork/payments-smart-contracts/blob/master/contracts/HermesImplementation.sol)):
- `settlePromise` pays `registry.getBeneficiary(identity)`. There is one beneficiary per identity, and **no native split**.
- `settleWithBeneficiary` sets a new beneficiary and pays that settlement to it.
- `payAndSettle` pays an identity-signed arbitrary beneficiary. The node uses it for `Withdraw` from channel balance, and each withdrawal must exceed the transactor fee ([hermes_promise_settler.go](https://github.com/mysteriumnetwork/node/blob/master/session/pingpong/hermes_promise_settler.go)).

**A. Split contract as beneficiary (recommended).**
- At registration, set the beneficiary to a per-user immutable splitter on Polygon, paying 98% to the user and 2% to Otium.
- MYST arrives through a plain `token.transfer`, so a contract can receive it. Anyone, Otium included, calls `distribute` and pays Polygon gas of a few cents. Batch these calls monthly.
- 0xSplits has been deployed on Polygon ([0xSplits Polygon](https://0xsplits.substack.com/p/contracts-are-live-on-polygon)). v2 supports immutable splits with "no owner-based recovery" ([Split v2 docs](https://splits.org/protocol/docs/core/split-v2/)). The v2 Polygon address is not confirmed, so as an alternative Otium could deploy its own audited splitter of about 40 lines.
- Pros: exact percentage, transparent on-chain, Otium never holds user funds, and it survives the app being uninstalled.
- Cons: the user's wallet no longer shows MYST arriving directly, so the UI must show the splitter. Distribution costs gas. Changing the fee needs a beneficiary change, which rides on a settlement (about 0.06 MYST settle fee).

**B. Probabilistic settlement redirect.**
- Before a settlement, the app sets the local beneficiary to Otium with probability *f*, then back to the user. The node then calls `settle_with_beneficiary`.
- This costs nothing extra, because settlements happen at 5 MYST anyway.
- Risk: if the app dies between the two settlements, the on-chain beneficiary stays Otium's until the next settlement. That needs a watchdog plus disclosure.
- The expected value is exact, but per-user variance is high: about 3 settlements a month, so with f = 2% one user pays Otium about once every 1.4 years.

**C. Second identity time-slice: not recommended.**
- It needs a second registration of 0.13 MYST.
- The new identity starts with no quality history, and consumers pick nodes by quality.
- Switching identities restarts the service and drops sessions.

**Earnings.** The vendor gives $2–8/month for a casual laptop and $8–25 for an always-on node ([MystNodes blog](https://www.mystnodes.com/blog/how-much-can-you-earn-sharing-bandwidth)). An independent estimate is about $1.2/month ([DePIN Scan](https://depinscan.io/projects/mysterium)). Settlement carries the 20% Hermes fee plus gas ([FAQ](https://docs.mysterium.network/faq)).

| Otium fee | Take per PC per month (at $1.2) | Take per PC per month (at $8) |
|---|---|---|
| 2% | $0.024 | $0.16 |
| 10% | $0.12 | $0.80 |

**Legal.** Run B2B-only service types. Mysterium advises against Public traffic in the US, UK, CA, DE, IT, AU and IN ([Node UI](https://help.mystnodes.com/en/articles/8006125-node-ui-management)). The exit-node Terms do not restrict beneficiary choice or distributor fees. They do forbid sublicensing the user's rights (§16) ([Terms](https://github.com/mysteriumnetwork/terms/blob/master/documents/TERMS_EXIT_NODE.md)). Disclose the split in Otium's own terms.

## 2. Publisher SDKs (no user sign-up; Otium is paid, so custodial)

| SDK | Rate | Payout | Windows | Notes |
|---|---|---|---|---|
| Honeygain SDK | Proxyway estimates $0.10–0.40 per DAU/month in top regions | Bank or PayPal, **no crypto** | Yes | Users are offered "premium features, bonuses and rewards", and cash rewards to users are not addressed ([FAQ](https://sdk.honeygain.com/faq/); [Proxyway](https://proxyway.com/research/internet-sharing-sdks-app-monetization)) |
| Massive | $0.05–0.20/user/month | PayPal monthly | Win 7+ x64 and macOS | Self-serve ([FAQ](https://www.joinmassive.com/faq)) |
| Decodo | Up to $0.20 per DAU/month | Not stated | Win/mac | Requires users to be "compensated" and to have an easy opt-out ([Decodo](https://decodo.com/monetize)) |
| Pawns SDK | $0.20/GB | Not stated | Win/mac/Linux | Approval needed ([Pawns](https://business.pawns.app/sdk/)) |
| Infatica | Up to $0.60/MAU | Not stated | Win/mac | 50k+ MAU minimum ([infatica-sdk.io](https://infatica-sdk.io/)) |
| Bright SDK / EarnApp | $0.10–0.35 per IP/month | Not stated | Win | 2026 scrutiny over consent ([Proxyway](https://proxyway.com/research/internet-sharing-sdks-app-monetization)) |
| PacketStream, Repocket | — | — | — | No publisher SDK found (consumer apps only). PacketSDK is IPIDEA-linked and was disrupted by Google in Jan 2026: avoid |

- Fee mechanism: Otium keeps a margin and credits users from its own uptime metering.
- Blockers: custody and money-transmission questions, fiat-only inflows that Otium must convert to crypto for users, vendor approval of cash pass-through (ask each vendor), and antivirus PUA noise. See the earlier notes for details.

## 3. Idle-compute networks

| Network | No sign-up? | Fee possible? | Blocker |
|---|---|---|---|
| Golem provider | Yes. `YA_ACCOUNT` is any Ethereum address, or a local wallet is created ([docs](https://docs.golem.network/docs/providers/provider-installation)) | Yes, set `YA_ACCOUNT` to a split contract | "x86-64 Linux is the only supported platform". Docker Desktop/WSL2 is "not supported" and needs `/dev/kvm`. ROI is "not measurable" ([gamefi-roi](https://gamefi-roi-web.onrender.com/opportunities/golem-provider)) |
| Koii | Local wallet | Sweep split | KOII market cap $22k and volume $1.3k, so effectively illiquid (CoinGecko) |
| Fluence | — | — | FLT $0.0054. Provider tier is data-center grade (no home-PC path found, unverified) |
| Akash | Wallet | Yes | Full Kubernetes cluster (4+ hosts recommended), public IPv4, own RPC node, 5 AKT deposit per bid ([Akash docs](https://akash.network/docs/providers/setup-and-installation/kubespray/provider-installation-prep/)) |
| Nosana / io.net | No | — | Nosana requires registered and verified hosts on Linux. io.net requires a 200 IO stake per GPU (earlier notes) |
| BOINC + Gridcoin | No | Pool-style | Each BOINC project needs an account. A solo beacon costs 0.5 GRC and must be renewed every 6 months ([wiki](https://gridcoin.us/wiki/beacons.html)). GRC volume is $1.3k/day |
| Theta Edge | Local wallet | Hard | ROI "not measurable". Meaningful jobs need an Elite stake. TFUEL is $0.0088 ([gamefi-roi](https://gamefi-roi-web.onrender.com/opportunities/theta-edge-node)) |
| Salad | No | No | Account required; multiple accounts are banned (earlier notes) |
| Render | No | — | Onboarding queue or interest form ([Render KB](https://know.rendernetwork.com/general-render-network/what-role-am-i/how-to-get-started-1)) |
| Flux | Wallet | Yes | 1,000 FLUX collateral per Cumulus node ([Flux docs](https://docs.runonflux.com/fluxnodes/what-are-fluxnodes/)) |

## 4. Other options
- **Tari merge-mining:** about $0.02/day uplift per 21 kH/s, roughly 2% ([WhatToMine CPU](https://whattomine.com/cpu), in earlier notes). It is still mining, and the take is the same as the existing 2% slice.
- **Bittensor:** needs a registration burn and ML engineering, and below-threshold miners are deregistered (earlier notes).
- **Storj:** needs a single-use, email-tied auth token per node ([forum](https://forum.storj.io/t/authorization-token-request/17445)). The terms forbid operator-run nodes on others' devices. Payout is USDC on Ethereum L1 with a $50+ threshold (earlier notes).
- **Sentinel dVPN:** pays the node's local key hourly, so a sweep could split 98/2. But it is Linux-only, needs a public IP, and earns about $0 for unleased home nodes (earlier notes).
- **Helium-style networks:** need hardware.

## Recommendation
1. Ship Mysterium with **a per-user immutable split contract as beneficiary**. Use mechanism B only if contract deployment is not wanted.
   - Keep the fee consistent with mining (2%) or justify a higher bandwidth fee (5–10%), since Otium pays registration.
   - Expect about $0.02–0.16 per PC per month at 2%.
2. In parallel, email MystNodes for partner status. Ask about free registrations, any integrator revenue share, whether CLI nodes claimed via `--mmn.api-key` earn Rewards-pool points, and pool size.
3. Keep the publisher SDKs (Honeygain first) as an optional custodial tier, to revisit once Otium has more than 1k DAU and a fiat-to-crypto payout pipeline.
4. Do not add compute networks now. None runs headless on Windows without a stake, account or approval, with liquid payouts.
