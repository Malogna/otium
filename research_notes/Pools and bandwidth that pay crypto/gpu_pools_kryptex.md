# GPU (and CPU) mining pools for Otium: can everything run through Kryptex Pool? (state as of 16 Oct 2026)

Method note: the live Kryptex data below was pulled from Kryptex's public API (`https://pool.kryptex.com/api/v1/index` and `/{coin}/api/v1/pool/info`) on 2026-10-16 at about 15:48 UTC. The endpoint list comes from Kryptex's published OpenAPI spec (`https://pool.kryptex.com/openapi.yaml`, linked from https://pool.kryptex.com/en/api). The unMineable data came from its public API (`api.unminable.com/v4/...`), queried the same day. Zergpool's site and API returned HTTP 522 (down or blocked) on that day, so Zergpool could not be checked. MiningDutch's API returned nothing parseable.

## Q1. Kryptex Pool in 2026: coins, fees, auto-exchange, payouts, API, terms, KYC, precedent, Kryptex app

### Takeaway
Kryptex Pool supports both of Otium's workloads. It has the GPU coins that are profitable today (Pearl, Quantus, Xelis, Iron Fish, Tari C29, Conflux), several weaker ones (Ergo, Ravencoin, Nexa, Neurai, Quai-KawPow), and CPU RandomX (Monero, Tari-RX, Zephyr). Account ("krx…" username) mining auto-converts every coin except the Quai variants into BTC in the Kryptex account. The public API gives per-worker hashrate charts. Two things are missing:
- **No documented automatic BTC withdrawal to an external address.** Withdrawals from the Kryptex account need email confirmation.
- **The terms say nothing either way about operators, aggregators or bundling.** They do let Kryptex end the service at any time, without notice.

### Cited Findings

**Coin list, fees and minimum payouts** (live API, 16 Oct 2026; `device_types` and `commission` fields of the index):

GPU coins:

| Coin | Algorithm | Fee | Min. payout | Workers |
|---|---|---|---|---|
| Pearl PRL | PearlHash | PPS+ 3%, SOLO 1% | 1 PRL | 83,172 |
| Quantus (Kryptex slug `qtc`) | Poseidon2 | PROP 3%, SOLO 3% | 0.1 | 18,501 |
| Xelis XEL | Xelishash | PROP 1%, SOLO 1% | 0.1 | 7,498 |
| Tari XTM | Cuckaroo29 | PROP 1%, SOLO 1% | 200 | 1,890 |
| Iron Fish IRON | FishHash | PPS+ 1%, SOLO 1% | 0.1 | 871 |
| Conflux CFX | Octopus | PPS+ 1%, SOLO 1% | 1 | 345 |
| Ravencoin RVN | KawPow | PPS+ 1% | 10 | 176 |
| Nexa | NexaPow | PPS+ 3%, SOLO 1% | 20,000 | 142 |
| Quai | KawPow | PROP 1% | 100 | 141 |
| Neurai XNA | KawPow | PROP 1%, SOLO 1% | 100 | 138 |
| Ergo ERG | Autolykos | PPS+ 1%, SOLO 1% | 1 | 63 |

CPU coins:

| Coin | Algorithm | Fee | Min. payout | Workers |
|---|---|---|---|---|
| Monero XMR | RandomX | PPS+ 1%, SOLO 1% | 0.01 | 30,332 |
| Tari XTM | RandomX | PROP 1%, SOLO 1% | 200 | 4,501 |
| Zephyr ZEPH | RandomX | PROP 1%, SOLO 1% | 0.1 | 409 |

ASIC-only coins (not usable by Otium): BTC (PPS+ 3%), BCH, BSV, FB, DGB, XEC, LTC, KAS, ZEC, ETC, ETHW, OCTA, ALPH, Tari SHA3x, Quai SHA256 and Quai Scrypt. Note that Kryptex now marks ETC/etchash as "asic".
  — [Kryptex Pool API index](https://pool.kryptex.com/api/v1/index); [pool.kryptex.com](https://pool.kryptex.com/)

**Auto-convert, account mining and fees**
- **Auto-convert flag.** The pool front page shows "Auto-convert: Yes" for every coin except the three Quai entries (Quai SHA256, Quai Scrypt and Quai KawPow are "No"). In account mining, Quai therefore cannot be turned into BTC. — [pool.kryptex.com](https://pool.kryptex.com/)
- **Pearl fee change.** Kryptex's 18 Jun 2026 article listed Pearl on Kryptex at a 1% fee with "PROP or SOLO". On 16 Oct 2026 the live API shows Pearl as PPS+ 3% / SOLO 1%. The scheme and fee have changed since June. — [Pearl Mining Pools article, 18/06/2026](https://pool.kryptex.com/articles/pearl-mining-pools-en); [live pool info](https://pool.kryptex.com/prl/api/v1/pool/info)
- **Comparison pages.** Kryptex's own comparison pages give pool fees as "1% for PROP and SOLO and 1–3% for PPS+" and say the pool covers transaction fees. I found no published spread or commission for the auto-exchange to BTC. — [Kryptex Pool vs 2Miners](https://pool.kryptex.com/pl/articles/kryptex-pool-vs-2miners-en); [Kryptex Pool vs ViaBTC](https://pool.kryptex.com/articles/kryptex-pool-vs-viabtc-en)
- **Account mining article** (dated 12/09/2026; the page is titled "Mine Anything — Get Bitcoin!"):
  - "All earnings will be credited to your kryptex.com account in Bitcoins."
  - The login format is `krxXXXXXX.Worker`. `krxXXXXXX/Worker1` and `email/worker` also work.
  - Worker names can be at most 32 characters, Latin letters and digits only.
  - Workers appear under "Pool Workers" within minutes.
  - Coins listed for account mining: BTC+FB, FB, XEC, DGB, BCH, BSV, ZEC, LTC+DOGE+BEL+PEP, ALPH, KAS, ETC, ETHW, OCTA, XTM-SHA3X, XMR, XTM-RX, ZEPH, CFX, XTM-C29, XEL, IRON FISH, RVN, PRL, QTC, XNA, ERGO, NEXA.
  - The page says not all coins exchange to BTC, but the table showing which ones is an image I could not read.

  — [Kryptex account mining article](https://pool.kryptex.com/articles/account-mining-en)
- **Plain wallet mining.** Kryptex says that mining to a plain wallet address (no account) is "a traditional mining pool without auto-conversion": payouts arrive in the mined coin. Auto-exchange to BTC happens only in account mining. — [Kryptex vs Kryptex Pool (payload)](https://pool.kryptex.com/articles/kryptex-vs-kryptex-pool-en/_payload.json); [Kryptex profitability/autoexchange article](https://www.kryptex.com/articles/profitability-kryptex-app-pool-autoexchange-en)

**Withdrawals from the Kryptex account**

Withdrawal options listed in the account-mining article: BTC (including Lightning), ETH, LTC, USDT (BEP20, TRC20), USDC (Solana), BNB (Smart Chain), TRX, Volet, Bitrefill and "PayPal US". — [account mining article](https://pool.kryptex.com/articles/account-mining-en)

Fees and limits from the fees page ("© Kryptex 2026", no effective date):

| Method | Fee | Min. payout | Max. payout |
|---|---|---|---|
| BTC on-chain | 0.00003 BTC | 0.00025 BTC | none listed |
| BTC Lightning | 2% | 0.00001 BTC | 0.005 BTC |
| ETH | 0.001 ETH | 0.03 ETH | none listed |
| LTC | 0.01 LTC | 0.015 LTC | none listed |
| BNB | 0.001 BNB | 0.002 BNB | none listed |
| TRX | 2 TRX | 4 TRX | none listed |
| USDT TRC20 | 5 USDT | 6 USDT | 3,000 USDT |
| USDT BEP20 | 0.5 USDT | 1.5 USDT | 3,000 USDT |
| USDC (Polygon) | 0.5 USDC | 1.5 USDC | 1,000 USDC |
| Volet USD/EUR | 1.95% | $1 / €1 | $1,000 / €1,000 |

— [Kryptex Fees](https://www.kryptex.com/en/fees)

- **Sources disagree on SOL and PayPal.** The fees page does not list SOL or PayPal, while the account-mining article lists USDC on Solana and PayPal US.
- **No auto-withdraw in the docs, and email confirmation is required.** Neither page mentions automatic withdrawal. The account article says "To withdraw funds from Kryptex, email confirmation is required." — [account mining article](https://pool.kryptex.com/articles/account-mining-en)
- **Withdrawing from the Kryptex app** (article dated 30 Apr 2026): BTC minimum 0.00025, Lightning 0.00001 BTC, LTC 0.015, USDT BEP20 1.5, Volet $1/€1 at 1.95%, and Bitrefill eGift cards (Amazon, eBay, Visa, Mastercard) paid in USDT/LTC/BTC. — [Withdrawing Money From Kryptex](https://www.kryptex.com/articles/payouts-en)
- **PayPal route.** A Kryptex guide (Sept 2025) says Kryptex has no direct PayPal payout. Users send LTC to PayPal's LTC address, ideally through an intermediate wallet. Kryptex's ideas board lists PayPal withdrawals as a user request. — [Kryptex PayPal article](https://www.kryptex.com/articles/kryptex-paypal-en); [kryptex.com/ideas](https://kryptex.com/ideas)

**Per-worker stats API** (OpenAPI 3.0.3, "Kryptex Pool API", server `https://pool.kryptex.com`):
- **Endpoints:**
  - `GET /{coin}/api/v1/miner/chart/{address}/{worker}?days=N` returns `[{timestamp, hashrate, shares}]`.
  - `GET /{coin}/api/v1/miner/chart/{address}/{worker}/{scheme}` (scheme = solo, pps or prop).
  - `GET /{coin}/api/v3/miner/workers/{address}` returns a paginated list of `{worker, scheme, hashrate, shares, last_share}`.
  - Also: `/miner/balance`, `/miner/payouts` (including CSV), `/miner/reward-chart` (31 days), `/miner/settings` (GET, and POST with `threshold` plus `ip_address`).
- **Caching:** the `/api/v1/index` response "is also cached at the HTTP layer for 3 minutes".
- **Not documented:** the spec gives no hashrate units, no chart point spacing, no authentication and no rate limits.

  — [Kryptex Pool API page](https://pool.kryptex.com/en/api); [openapi.yaml](https://pool.kryptex.com/openapi.yaml)
- **Otium already uses this API.** `src/Otium.Server/Gpu/FeedClients.cs` calls `{coin}/api/v1/pool/info`, `{coin}/api/v3/miner/workers/{account}` and `{coin}/api/v1/miner/chart/{account}/{worker}?days=1`. `KryptexCreditor.cs` integrates these points per worker. (Local repo, read 2026-10-16.)
- **Other pool-info fields.** `/prl/api/v1/pool/info` returns servers in 8 regions (Global, EU, NA, SA, SG, HK, RU, Middle East), with TCP port 7048 and SSL port 8048, plus `minpay`/`defpay`/`maxpay`. Monero runs on `xmr(-eu|-us).kryptex.network:7029` with modes pps/solo. — [PRL pool info](https://pool.kryptex.com/prl/api/v1/pool/info); [XMR pool info](https://pool.kryptex.com/xmr/api/v1/pool/info)

**Terms and Conditions** (no "last updated" date shown; "© Kryptex 2026"). Verbatim quotes:
- Section 02: "Kryptex.com is entitled to modify, improve or discontinue any of its services at its sole discretion and without notice to you even if it may result in you being prevented from accessing any information contained in it."
- Section 05: "The Terms of this agreement will continue to apply in perpetuity until terminated by either party without notice at any time for any reason."
- Section 06: "If your account remains inactive for a continuous period of six (6) months, Kryptex.com reserves the right to suspend your account … one (1) year … permanently delete your account and all associated data without prior notice."
- Section 08: Kryptex is not liable for losses from "any permanent or temporary cessation of such service."
- Section 12: terms may change "at our sole discretion and without any notice."
- Section 03 covers website *content* only ("personal, non-commercial use").
- **Not in the terms:** no clause on reselling hashrate, aggregators, operators, bundling Kryptex mining into third-party software, multiple accounts, KYC or sanctions.

  — [Kryptex Terms and Conditions](https://www.kryptex.com/terms-and-conditions)
- **Inactive pool wallets.** A pool help article says an inactive pool wallet's balance is kept for 90 days, then deleted. An older 2021 kryptex.com article said one year. — [How long does the pool keep coins](https://pool.kryptex.com/ko/articles/how-long-pool-keep-coins-en); [older Kryptex article](https://www.kryptex.com/ru/articles/how-long-kryptex-keep-balance-en)
- **KYC:** the account article mentions only email confirmation. No KYC is mentioned on the fees, payouts or terms pages. — [account mining article](https://pool.kryptex.com/articles/account-mining-en); [Fees](https://www.kryptex.com/en/fees)
- **Kryptex's referral program** pays the referrer "20% from every referred miner", described on the pool page as 20% of the pool's fee. — [pool.kryptex.com](https://pool.kryptex.com/); [Kryptex T&C page navigation](https://www.kryptex.com/terms-and-conditions)
- **Kryptex's own products** include "Krig — AMD & NVIDIA miner for Pearl". Kryptex now ships its own Pearl miner. — [Kryptex site navigation](https://www.kryptex.com/terms-and-conditions)
- **Kryptex's share of each network** (pool hashrate ÷ network hashrate, live API, 16 Oct 2026):

  | Coin | Pool hashrate | Network hashrate | Kryptex share |
  |---|---|---|---|
  | Pearl | 29.99 EH/s | 64.36 EH/s | about 47% |
  | Quantus | 19.05 TH/s | 33.90 TH/s | about 56% |
  | Xelis | 36.7 MH/s | 52.5 MH/s | about 70% |
  | Iron Fish | 61.7 GH/s | 238 GH/s | about 26% |
  | Monero | 1.02 GH/s | 6.56 GH/s | about 15.5% |

  — [live pool info endpoints](https://pool.kryptex.com/xel/api/v1/pool/info)

### Inferences
- **Coin coverage.** All of Otium's current and plausible GPU coins except EPIC, NOID (Parano1d), Qubitcoin (Qhash), Zano, Firo and Evrmore are on Kryptex. CPU XMR is on Kryptex at 1% PPS+, and XMRig connects with standard stratum, so CPU mining can leave MoneroOcean too. "All mining through Kryptex into BTC" is technically possible today, except for the Quai coins, which do not auto-convert.
- **The weak point is the BTC exit.** Auto-exchange puts BTC into the operator's Kryptex account. Moving it into Otium's Bitcoin treasury looks like a manual or email-confirmed withdrawal: there is no documented auto-withdraw and no withdrawal API. The `miner/settings` threshold and IP endpoint applies to wallet-address mining, not to account BTC balances. Expect a periodic manual sweep at 0.00003 BTC per withdrawal, or ask Kryptex support about auto-withdrawal or API keys. This is unverified.
- **Silence in the terms is permissive, but not a guarantee.** Kryptex can end the service "for any reason." One operator account carrying thousands of workers is unusual but not banned on paper. Kryptex's leaderboard (`is_kryptex` flag) and its own app show Kryptex itself aggregates many users' hashrate.
- **Network concentration is a real risk.** Kryptex holds about 47–70% of the Pearl, Quantus and Xelis networks. A Kryptex outage or policy change would hit Otium's most profitable coins at the same moment. Large shares of a network can also bring orphan or reputation issues for the coin.
- **Ticker clash.** Kryptex uses the ticker "qtc" for Quantus. On hashrate.no and WhatToMine, "QTC" is Qubitcoin (Qhash), a different coin. Code and docs must not mix them up.

### Gaps
- The auto-exchange spread or fee for each coin is not published. The table of which coins can be converted to BTC is an image I could not read.
- I found no limit on workers per account, no API rate limit, no hashrate units and no chart granularity documented. Otium's own integration has the empirical point spacing.
- I found no third-party app (as opposed to farms or HiveOS users) documented as mining many end users into one Kryptex account. No precedent was found either way.
- Whether Kryptex has an API or setting that auto-withdraws account BTC to an external address is unknown. Confirm with support@kryptex.com.
- Whether "PayPal US" and USDC (Solana) withdrawals are live is unclear: the fees page does not list them.

## Q2. Alternatives with BTC or multi-coin payout and per-worker APIs, and which miners each algorithm needs

### Takeaway
unMineable is the only checked alternative that both auto-pays BTC, ETH, BNB, SOL or USDT straight to an external wallet with no account and exposes a per-worker API. However:
- It pays to a wallet address, not a sub-account.
- It lacks Quantus.
- Its per-worker API did not list Pearl or Xelis.

zpool (and probably Zergpool) pays BTC but mostly carries CPU and older algorithms, with no Pearl, Quantus or Xelis. Single-coin pools (HeroMiners, WoolyPooly, 2Miners, LuckyPool) pay in the mined coin. Otium would then have to swap those coins to BTC itself.

### Cited Findings

**unMineable** (live API, 16 Oct 2026)
- **Payout coins:** 99 assets, including BTC, ETH (Ethereum, Arbitrum and BSC chains), BNB (BSC default), SOL (Solana default) and USDT (TRC20 default). — [unMineable coin API](https://api.unminable.com/v4/coin)
- **BTC terms:** payment_threshold 0.00075 BTC; `mining_fee: "1"` (1%). "Payments are sent automatically once a day with no network fees." — [unMineable BTC API](https://api.unminable.com/v4/coin/BTC); [unMineable BTC page](https://unmineable.com/coins/BTC)
- **Algorithms listed for earning BTC:** PearlPow (PRL), XelisHash (XEL), FishHash (IRON), Blake3 (ALPH), RandomX (XMR), NexaPow, Octopus (CFX), BeamHash, Autolykos (ERG), Etchash, Ethash, plus ASIC algorithms. Quantus is not listed. — [unMineable BTC page](https://unmineable.com/coins/BTC)
- **Per-worker API.**
  - `GET /v4/address/{addr}?coin=BTC` returns a `uuid`, `mining_fee`, threshold and balance.
  - `GET /v4/account/{uuid}/workers` returns, per algorithm, a `workers` list and `chart.reported`/`chart.calculated` hashrate series.
  - `GET /v4/account/{uuid}/stats` returns balance, rewards for the past 24h/7d/30d, and amount paid.
  - The workers endpoint's algorithm keys are ethash, etchash, randomx, kawpow, autolykos, beamhash, zelhash, firopow, zhash, scrypt, sha256, equihash, kheavyhash, blake3, ironfish, octopus, nexapow, karlsenhash and dynexsolve. There is no pearl or xelis key, which is inconsistent with the coin page.

  — [unMineable address API](https://api.unminable.com/v4/address/1A1zP1eP5QGefi2DMPTfTL5SLmv7DivfNa?coin=BTC) (queried with a public example address)
- **Proxy pool and double fee.** Kryptex (a competitor, so biased) calls unMineable a proxy pool where "miners pay double fee", with 1% for PPS. — [Kryptex "unMineable alternative"](https://www.kryptex.com/articles/unmineable-alternative-en)
- **Referral discount.** A forum user reports a 0.25% fee reduction from swapping referral codes, which would mean 0.75%. This is anecdotal and I could not verify it on unMineable's site. — [HardForum thread](https://hardforum.com/threads/unmineable.2015306/)
- **Email signup.** unMineable's homepage now says its app needs "only your email to start mining". Pool mining still works by wallet address. — [unmineable.com](https://unmineable.com/)

**zpool** (live API, 16 Oct 2026)
- 1% fee on every algorithm.
- Active algorithms are mostly CPU and old GPU: kawpow (77 workers), evrprogpow (30), firopow (2), meowpow (2), equihash variants, verthash (695), ghostrider (7,338), yescrypt/yespower variants, sha256, scrypt and others.
- There is no pearl, quantus, xelishash, randomx, fishhash or autolykos.

— [zpool API status](https://www.zpool.ca/api/status)

**Zergpool:** `zergpool.com` and `/api/status` returned HTTP 522 on 16 Oct 2026. It could not be evaluated. — [zergpool.com](https://zergpool.com/)

**Single-coin pools (pay in the mined coin, no BTC auto-exchange):**
- **WoolyPooly** API stats list: aeternity, raven, ethw, etc, cfx, ergo, vtc, firo, alph, rtm, kas, mewc, rxd, nexa, xna, octa, kls, wart, zano, xel, mwc, dcr and prl. That includes Pearl, Xelis and Zano, but not Quantus. — [WoolyPooly API](https://api.woolypooly.com/api/stats)
- **Pearl pools**, per a Kryptex article dated 18 Jun 2026:

  | Pool | Fee | Scheme | Pays in |
  |---|---|---|---|
  | HeroMiners | 1% | PPS+/SOLO | PRL |
  | LuckyPool | 1% | PROP/SOLO | PRL |
  | PearlFortune | 5% | not stated | PRL only |
  | PearlHash | 1–5% | not stated | PRL |

  K1Pool and Coin-miners are also mentioned. — [Pearl Mining Pools](https://pool.kryptex.com/articles/pearl-mining-pools-en)
- **Quantus pools:** SRBMiner's 3.6.4 notes (Sept 2026) say Quantus "can be mined on quantus.luckypool.io … quantus.suprnova.cc … or any other pool that supports the QUIC protocol". QUIC support was later removed in 3.7.3. — [SRBMiner releases](https://github.com/doktor83/SRBMiner-Multi/releases)
- **HeroMiners and 2Miners coin lists:** only aggregator data (MiningBoard) was found. It lists HeroMiners Xelis at a 0.9% fee, PROP/SOLO, and gives inconsistent coin lists. Treat as unreliable. — [MiningBoard HeroMiners Xelis](https://miningboard.com/pools/xelis.herominers.com)

**Miner software**
- **SRBMiner-MULTI 3.7.6** (14 Oct 2026): "Improvements on algorithm 'pearlhash' for NVIDIA 3000, 4000 series GPUs"; quantus improvements for NVIDIA and AMD; noid improvements.
- **3.7.3** (7 Oct 2026): made the new NVIDIA pearlhash kernels the default for Ampere, Ada and Blackwell, which "require different overclocking settings … Set memory clock to 810MHz". It also removed QUIC for Quantus.
- **3.6.7** (13 Sept 2026): "Lowered devfee on algorithms 'noid' and 'quantus' to 2%".
- **3.6.4:** added Quantus "for NVIDIA and AMD RDNA GPUS, fee 2.5%".
- **3.7.0:** added Intel Arc for Quantus. **3.6.8:** added AMD Ellesmere (RX 4xx/5xx) for Quantus.

  — [SRBMiner-Multi GitHub releases](https://github.com/doktor83/SRBMiner-Multi/releases)
- **SRBMiner 3.5.0** made a "mandatory update for algorithm 'pearlhash'" that removed AMD RDNA2 support for pearlhash. This conflicts with Kryptex's June 2026 article, which lists AMD RX 6000 RDNA2 as supported for Pearl. — [newreleases.io SRBMiner 3.5.0](https://newreleases.io/project/github/doktor83/SRBMiner-Multi/release/3.5.0); [Pearl Mining Pools](https://pool.kryptex.com/articles/pearl-mining-pools-en)
- **Pearl miners** named by Kryptex: SRBMiner-MULTI, BzMiner, PeakMiner, ARCMiner and lpminer, for NVIDIA, AMD RX 6000 and Intel Arc. Kryptex also ships its own "Krig" Pearl miner. — [Pearl Mining Pools](https://pool.kryptex.com/articles/pearl-mining-pools-en); [Kryptex nav](https://www.kryptex.com/terms-and-conditions)

### Inferences
- **No pool beats Kryptex on coverage plus BTC conversion for Otium's coins.** unMineable is the closest backup for BTC-to-external-wallet payouts. Its payout path (direct BTC to an address, daily, no network fee) is better than Kryptex's manual account withdrawal. But it lacks Quantus, and its per-worker API apparently lacks Pearl and Xelis. It also stacks a proxy fee on top of the upstream pool.
- **Using single-coin pools as backup** (HeroMiners or LuckyPool for Pearl, WoolyPooly for Xelis and Zano, LuckyPool or Suprnova for Quantus) means Otium would hold altcoins and need its own swap to BTC. THORChain does not list these coins, so that would mean an exchange account, which likely brings KYC. That breaks the no-KYC, BTC-only goal.
- **SRBMiner covers pearlhash, quantus, xelishash, fishhash, cuckaroo29 and randomx** (XMRig remains the better CPU miner). Otium can stay on one GPU miner binary for the Kryptex coin set. SRBMiner's own devfee (2% on quantus) comes on top of pool fees.

### Gaps
- Zergpool, MiningDutch and 2Miners coin lists and fees could not be retrieved (522 errors or non-JSON responses).
- unMineable's official fee and referral-discount policy and its API documentation were not found. The fee comes from the API field; the 0.75% referral figure is unverified.
- I did not get a per-algorithm NVIDIA vs AMD support matrix for lolMiner, BzMiner, Rigel or OneZeroMiner as of Oct 2026.
- The cause of SRBMiner failing on MoneroOcean's GPU algorithms in Otium's RTX 3080 test was not researched.

## Q3. Which GPU coins are profitable on consumer NVIDIA/AMD cards in October 2026, and which pools support them

### Takeaway
Pearl and Quantus dominate GPU mining, at roughly 3–8× the revenue of anything else on NVIDIA. Next come Tari-C29, EPIC, Evrmore, Qubitcoin, NOID, Zano, Iron Fish, Xelis and Conflux. KawPow, Autolykos, Etchash and CryptoNight-GPU (Otium's MoneroOcean set) sit near or below break-even at $0.10/kWh. Kryptex supports Pearl, Quantus, Tari-C29, Iron Fish, Xelis and Conflux, the core of the profitable list.

### Cited Findings

**WhatToMine coins.json**, timestamps about 2026-10-16 15:48 UTC. Ranked by WhatToMine's relative profitability index, with BTC revenue per day at WhatToMine's reference hashrate:

| Coin | Algorithm | Index | BTC/day |
|---|---|---|---|
| Pearl | — | 6165 | 0.00007828 |
| Quantus | QPoW | 5186 | 0.00006585 |
| Tari | Cuckaroo29 | 2333 | — |
| EPIC | ProgPow | 2162 | — |
| Evrmore | EvrProgPow | 2128 | — (data lagging) |
| Qubitcoin | Qhash | 1316 | — |
| Neoxa | KawPow | 1173 | — |
| Aeternity | — | 1099 | — |
| Zano | — | 1073 | — |
| Decred | Blake3 | 964 | — |
| Iron Fish | — | 960 | — |
| Firo | — | 882 | — |
| Karlsen | — | 842 | — |
| Xelis | — | 722 | — |
| Ryo | CryptoNightGPU | 719 | — |
| Quai | KawPow | 678 | — |
| Ergo | — | 586 | — |
| Ravencoin | — | 317 | — |
| Nexa | — | 312 | — |
| ETHW | — | 249 | — |
| ETC | — | 100 | — |

— [WhatToMine coins.json](https://whattomine.com/coins.json)

**hashrate.no, RTX 3080** ($0.10/kWh; the page shows no timestamp, fetched 16 Oct 2026). Pearl 106.93 TH/s at 159 W comes from the new SRBMiner kernels.

| Coin | Algorithm | Hashrate | Power | Revenue/day | Profit/day |
|---|---|---|---|---|---|
| Pearl | — | 106.93 TH/s | 159 W | $3.80 | $3.42 |
| Quantus | — | 417 MH/s | 143 W | $2.96 | $2.62 |
| NOID | — | — | — | $1.81 | $1.43 |
| EPIC | — | — | — | $1.73 | $1.02 |
| Qubitcoin | Qhash | — | — | $0.73 | $0.48 |
| Conflux | — | — | — | $0.78 | $0.16 |
| Xelis | — | — | — | $0.37 | $0.04 |

Zano, Iron Fish, Firo, Ergo ($0.25 revenue, −$0.09 profit), Tari C29, Quai-KawPow ($0.50 revenue, −$0.10 profit), Beam and Nexa are all at or below zero profit. — [hashrate.no RTX 3080](https://www.hashrate.no/gpus/3080)

**hashrate.no, RX 6800 XT** (AMD, same assumptions):

| Coin | Hashrate | Power | Profit/day |
|---|---|---|---|
| Quantus | 154 MH/s | 108 W | $0.91 |
| EPIC | — | — | $0.53 |
| Qubitcoin | — | — | $0.53 |
| Pearl | 23.64 TH/s | 156 W | $0.39 |
| Evrmore | — | — | $0.29 |
| Conflux | — | — | $0.17 |
| Meowcoin | — | — | $0.13 |
| Iron Fish | — | — | $0.09 |
| Zano | — | — | $0.08 |
| Karlsen | — | — | $0.06 |
| Firo | — | — | $0.03 |
| Quai-KawPow | — | — | $0.01 |
| Xelis | — | — | −$0.02 |
| Tari C29 | — | — | −$0.05 |

Pearl is far weaker on AMD than on NVIDIA. — [hashrate.no RX 6800 XT](https://www.hashrate.no/gpus/6800xt)

**hashrate.no, RTX 4090:** about $10.99/day revenue with Pearl on top at 308.21 TH/s. WhatToMine gives the 4090 on Pearl $10.19 revenue and $9.23 profit at 310 TH/s and 400 W. — [hashrate.no 4090](https://hashrate.no/gpus/4090); [WhatToMine 4090](https://whattomine.com/gpus/79-nvidia-geforce-rtx-4090)

**Pool support for the profitable coins:**
- **Kryptex:** PRL, Quantus, XTM-C29, IRON, XEL, CFX (also ERG, RVN, NEXA, XNA, Quai-KawPow).
- **unMineable:** PRL, XEL, IRON, CFX (no Quantus).
- **WoolyPooly:** PRL, XEL, CFX, ZANO, FIRO, MEWC, KLS, DCR.
- **zpool:** EVR (evrprogpow), FIRO, MEWC.
- **Not on Kryptex:** EPIC, NOID, Qubitcoin, Zano, Firo, Evrmore, Meowcoin and Karlsen.

— [Kryptex API index](https://pool.kryptex.com/api/v1/index); [unMineable BTC](https://unmineable.com/coins/BTC); [WoolyPooly API](https://api.woolypooly.com/api/stats); [zpool API](https://www.zpool.ca/api/status)

### Inferences
- **On NVIDIA,** mining Pearl (and Quantus as a fallback) on Kryptex captures most of the available revenue. The MoneroOcean GPU algorithms add almost nothing.
- **On AMD,** Quantus beats Pearl. EPIC and Qubitcoin, which Kryptex lacks, are close behind. Kryptex-only would leave some AMD revenue on the table, but not much in absolute dollars (under $0.50/day per card).
- **These rankings move fast.** Pearl and Quantus are new coins with rapidly changing difficulty and miner kernels: SRBMiner shipped six releases between 28 Sept and 14 Oct 2026. Otium's server-side GPU policy should keep pulling a profitability feed rather than hard-coding a coin.

### Gaps
- hashrate.no pages show no data timestamp. The WhatToMine timestamps are exact, but its index assumes reference hashrates rather than specific cards.
- No CPU (RandomX) profitability comparison was gathered here: XMR via Kryptex PPS+ vs MoneroOcean.

## Q4. Risks of putting everything on one pool, and mitigations

### Takeaway
Putting everything on Kryptex concentrates five risks on one company that can change fees and schemes without notice (Pearl went from 1% PROP to 3% PPS+ between June and October 2026) and can end service "for any reason":
- counterparty risk
- policy risk
- API risk
- payout risk
- network-concentration risk

The mitigations are a pre-configured failover pool, frequent BTC sweeps, and keeping crediting independent of any one pool's API.

### Cited Findings
- The terms allow Kryptex to "modify, improve or discontinue any of its services at its sole discretion and without notice". Either party may terminate "without notice at any time for any reason". Kryptex disclaims liability for "any permanent or temporary cessation of such service." — [Kryptex T&C](https://www.kryptex.com/terms-and-conditions)
- The Pearl fee and scheme changed from "1%, PROP or SOLO" (18 Jun 2026) to PPS+ 3% / SOLO 1% (live 16 Oct 2026). — [Pearl Mining Pools](https://pool.kryptex.com/articles/pearl-mining-pools-en); [PRL pool info](https://pool.kryptex.com/prl/api/v1/pool/info)
- Account balances depend on staying active: suspension after 6 months and deletion after 1 year of inactivity. Pool wallet balances are kept 90 days after mining stops. — [Kryptex T&C](https://www.kryptex.com/terms-and-conditions); [pool coin retention article](https://pool.kryptex.com/ko/articles/how-long-pool-keep-coins-en)
- Withdrawals need email confirmation. USDT and USDC withdrawals have per-payout maximums (3,000 USDT; 1,000 USDC Polygon), and Lightning tops out at 0.005 BTC. — [account mining article](https://pool.kryptex.com/articles/account-mining-en); [Fees](https://www.kryptex.com/en/fees)
- **Precedent from another pool:** ForkLog reported ViaBTC restricting accounts and asking some users, mainly in Russia, for documents. This shows pools can impose document checks after the fact. — [ForkLog on ViaBTC](https://forklog.com/en/news/viabtc-clarifies-account-access-restrictions)
- Kryptex holds about 47%, 56% and 70% of the Pearl, Quantus and Xelis network hashrate (computed from the live API above). — [Kryptex pool info](https://pool.kryptex.com/prl/api/v1/pool/info)

### Inferences
- **Failover pools.** Keep each coin's stratum list multi-region; Kryptex offers 8 regions for PRL. Pre-configure a fallback pool per algorithm in the GPU policy: HeroMiners or LuckyPool for Pearl, WoolyPooly for Xelis, unMineable-BTC for Pearl, Xelis, Iron Fish and Conflux. Switch automatically if the Kryptex API or stratum fails for more than N minutes. The catch is that BTC conversion is lost or degraded on most fallbacks.
- **Sweep frequently.** Withdraw BTC from the Kryptex account often. At 0.00003 BTC per withdrawal, a daily or weekly sweep above 0.00025 BTC keeps exposure small. Never let the account sit idle long enough to hit the inactivity rules.
- **Reconcile revenue.** Check Kryptex's reported BTC credited against Otium's per-worker hashrate integration. That catches unannounced fee or scheme changes like the Pearl change.
- **Ask Kryptex directly.** Request written confirmation from Kryptex support that one account with thousands of end-user workers from a third-party app is acceptable. Also ask about worker caps and API rate limits. The terms are silent on all three.
- **Keep CPU XMR on a second provider** (MoneroOcean, or unMineable-BTC on RandomX) to diversify, since CPU revenue is small anyway.

### Gaps
- I found no public reports of Kryptex freezing large accounts or operator-style accounts in 2025–2026, and none of disputes over auto-exchange. Absence of evidence is not evidence of safety.
- Kryptex's historical uptime and status-page incident history were not gathered. The site footer shows "All systems operational" only.
