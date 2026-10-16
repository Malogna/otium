# CPU mining pools and coins that pay in something other than XMR (state as of 16 Oct 2026)

Method note: several facts below came from live API calls I made on 2026-10-16 (marked "live API, 2026-10-16"). Where a page carried no date, I say so. Third-party blogs are flagged.

## 1. Auto-exchange / multi-payout pools that accept RandomX (or other CPU algos) and pay BTC or other coins

### Takeaway
Two working options remain for RandomX paid as BTC: **unMineable** (pays straight to a BTC or other-coin address, 1% fee, per-worker API) and **Kryptex** (username login auto-converts XMR, XTM or SAL to a Kryptex balance, withdrawn as BTC, USDT, LTC or ETH, with no KYC per Kryptex). Zergpool and ProHashing have shut down. zpool no longer offers RandomX. MiningDutch lists RandomX, but the API shows no hashrate on it. 2Miners has no XMR pool. NiceHash requires KYC. Kryptex's public per-worker API is the best one I found (10-minute buckets, H/s). But Kryptex withdrawals are user-initiated, not automatic, and I could not confirm that the per-worker API works for username (auto-convert) accounts.

### Cited Findings

**unMineable**
- RandomX (XMR, CPU) is one of the algorithms you can mine "to BTC". The minimum payout is 0.00075 BTC on the Bitcoin network, "no network fees", and payments are "sent automatically once a day". Stratum is `rx.unmineable.com:3333/13333/80` (TCP) or `:443` (SSL). There are also xmr-node-proxy-compatible ports, `:4445` and `:14445` (SSL), which are useful for an operator-side proxy. unMineable mines the coin, converts it, and credits the BTC balance. — [unMineable BTC coin page](https://unmineable.com/coins/BTC)
- Live API, 2026-10-16: `GET https://api.unminable.com/v4/address/{addr}?coin=BTC` returned `mining_fee:"1"`, `payment_threshold:"0.00075"`, `auto:true`, a `uuid`, `balance` and `balance_payable`, and a `chains` list (only Bitcoin was enabled for BTC). `GET https://api.unminable.com/v4/account/{uuid}/workers` returns an object per algorithm (`randomx`, `kawpow` and others). Each has a `workers` array plus `chart.reported` and `chart.calculated`, each with `{data[], timestamps[]}`. No authentication was needed. I could not see the per-worker record fields because the test address had no active workers. — [unMineable API (live)](https://api.unminable.com/v4/address/bc1qxy2kgdygjrsqtzq2n0yrf2493p83kkfjhx0wlh?coin=BTC)
- The site says it supports "80+ supported assets" as payout coins. The unMiner app needs "only your email" to start. The homepage says nothing about KYC. — [unmineable.com](https://unmineable.com/)
- Login format (third-party guide, not official docs): `-u COIN:ADDRESS.WORKER` with `-a rx -k`, for example `DOGE:D....PCLINUX`. A referral code (`#code` suffix) lowers the fee from 1% to 0.75% (Docker image READMEs and a Dec 2023 guide). — [Scribd guide](https://www.scribd.com/document/518995343/Mining); [Docker hub image](https://hub.docker.com/r/thechristech/unmineable); [Publish0x guide, Dec 2023](https://www.publish0x.com/cryptospace/how-to-mine-dollar-bonk-solana-token-with-cpu-and-gpu-guide-xyqegdn)
- unMineable's /faq and /terms URLs returned 404 on 2026-10-16. I could not read the ToS on aggregating devices, KYC or worker limits. — [unmineable.com/faq](https://unmineable.com/faq)

**Kryptex (pool.kryptex.com and the kryptex.com account)**
- XMR pool: PPS+ and SOLO, 1% fee each, minimum payout 0.01 XMR. Login is `wallet/worker` or `username/worker`. "Get payouts in Bitcoin instead of XMR" by using your Kryptex username: withdraw in BTC, USDT, Volet or gift cards, "instant withdrawal as soon as you reach $1". — [Kryptex XMR pool page](https://pool.kryptex.com/en/xmr)
- Live API, 2026-10-16 (`/xmr/api/v1/pool/info`): fee_type PPS+, fee 0.01, minpay 0.01, default payout 0.1, max 1.0 XMR. Pool hashrate is about 1.02 GH/s against a network of about 6.56 GH/s (roughly 15.5%), with 10,543 miners and 30,332 workers. Servers are in EU, US, BR, SG, HK, RU and AE; the EU server is `xmr-eu.kryptex.network:7029`, SSL on 8029. — [Kryptex pool info API](https://pool.kryptex.com/xmr/api/v1/pool/info)
- Comparison article dated 14/09/2026. On **kryptex.com**, altcoins are auto-converted to BTC. Withdrawals go out in BTC (including Lightning), ETH, LTC, USDT (BEP20), Volet or fiat, starting from $1. Payouts are "user-initiated and manually confirmed", with no automatic payouts, and "No KYC is required". The **pool** itself has no auto-conversion and pays automatically to a fixed address. The pool also deletes the data of a wallet inactive for 90 days, and its coins are lost. — [Kryptex: kryptex.com vs pool.kryptex.com](https://pool.kryptex.com/articles/kryptex-vs-kryptex-pool-en); [How long the pool keeps coins](https://pool.kryptex.com/articles/how-long-pool-keep-coins-en)
- Other RandomX coins with BTC auto-convert on Kryptex. **Tari (XTM-RX)** pool: PROP 1% or SOLO 1%, minimum 200 XTM, "Auto-convert: Yes" via a username. Rented hashrate is forced to SOLO. Pool hashrate is 71 MH/s (3,250 miners, 4,501 workers). XMRig is recommended with `-a rx/0`. **Salvium (SAL)**: "Receive Bitcoins for mining SAL" with the `email/worker` login (news item dated 9 Aug 2024). — [Kryptex XTM-RX pool](https://pool.kryptex.com/xtm-rx); [Kryptex SAL→BTC news](https://www.kryptex.com/en/news/sal-btc-en)
- Public OpenAPI spec (`/openapi.yaml`) endpoints:
  - `GET /{coin}/api/v3/miner/workers/{address}` returns a paginated list of workers.
  - `GET /{coin}/api/v1/miner/chart/{address}/{worker}?days=N` returns one worker's hashrate history.
  - `/miner/balance`, `/miner/payouts` and a payouts CSV export.
  - No authentication or rate limits are documented. — [Kryptex OpenAPI](https://pool.kryptex.com/openapi.yaml); [API page](https://pool.kryptex.com/en/api)
- Live API test, 2026-10-16, using the public Monero general-fund address:
  - The `/v3/miner/workers/{address}` fields are `worker`, `scheme`, `status`, `valid/stale/invalid` (share counts), `avg_hashrate_24h/3h/30m` (H/s, as strings), `last_share`/`last_active` (Unix ms), `agent` (for example "XMRig/6.26.0") and `country`.
  - The per-worker chart has **10-minute buckets** (600,000 ms apart). `days=1` returns 144 points and `days=7` returns 1,008. Each point has `hashrate` and `avg_hashrate_3h/30m` (H/s) and `valid/stale/invalid` share counts.
  - `/miner/balance` returns total, confirmed, threshold and reached_pct.
  - A query with a username (`kryptex`) instead of an address returned `{"detail":"You do not have permission to perform this action."}`. — [Kryptex workers API (live)](https://pool.kryptex.com/xmr/api/v3/miner/workers/48edfHu7V9Z84YzzMa6fUueoELZ9ZRXq9VetWzYGzKt52XU5xvqgzYnDK9URnRoJMk1j8nLwEVsaSWJ4fhdUyZijBGUicoD)
- Kryptex's own pages disagree on some fee and address details (search summary). — [Kryptex comparison](https://pool.kryptex.com/articles/kryptex-vs-kryptex-pool-en)

**Zergpool**
- zergpool.com returned HTTP 522 on 2026-10-16. Secondary sources say the infrastructure was retired, with pool services ending at the end of September 2025. Reddit posts from October 2025 report miners who never received their shutdown payouts. — [MEXC glossary](https://www.mexc.com/crypto-glossary/article/zergpool-135531); [r/zergpool mirror, Oct 2025](https://red.applefritter.com/r/zergpool)

**ProHashing**
- The homepage now reads "PROHASHING is now shutdown. We thank everyone for their years of support…". The API returns "The ProHashing API has been permanently retired." (fetched 2026-10-16). — [prohashing.com](https://prohashing.com/)

**zpool**
- Live `/api/status`, 2026-10-16: there is **no RandomX algorithm** listed. CPU algorithms that pay BTC (or other coins via auto-exchange) are still offered at a 1% fee: ghostrider (7,343 workers), verushash (148 workers), minotaurx, yespower variants and flex. — [zpool status API](https://zpool.ca/api/status)

**MiningDutch**
- Live `/api/status/`, 2026-10-16: `randomx` is listed with 1 coin, a 2% fee, hashrate 0 and 0 workers. Ghostrider and yespower are also listed at 2%. The API rate limit is 1 request per 10 s. Third-party reviews say payouts run every 15 minutes and the pool sells coins as soon as they are verified. — [MiningDutch status API](https://www.mining-dutch.nl/api/status/); [Slashdot listing](https://slashdot.org/software/p/Mining-Dutch/)

**2Miners**
- `https://xmr.2miners.com/api/stats` returned 404 on 2026-10-16, so there is no XMR pool. — [2Miners XMR API (404)](https://xmr.2miners.com/api/stats)

**HeroMiners**
- The Monero pool is live (`de.monero.herominers.com`, fee 0.9%, hashrate window 600 s). It is a plain XMR pool; I found no auto-exchange. — [HeroMiners Monero API](https://monero.herominers.com/api/stats)

**NiceHash**
- It pays only in BTC. Mandatory KYC for all users was announced in late 2024, with unverified accounts losing mining in November 2024. This comes from a competitor's (Kryptex) article. Trustpilot and Reddit users describe locked accounts and denied KYC. — [Kryptex: NiceHash alternative](https://www.kryptex.com/articles/nicehash-alternative-en); [Trustpilot](https://ie.trustpilot.com/review/www.nicehash.com?page=6); [Reddit Nov 2024](https://pol1.lr.ggtyler.dev/r/NiceHash/comments/1gxme17/easiest_way_to_withdraw)

**MoneroOcean (baseline)**
- Live `/pool/stats`, 2026-10-16: about 244 MH/s with 4,620 miners, PPLNS. Alt-chain blocks found show its algo-switching across RandomX-family coins (77,610 alt blocks total). — [MoneroOcean API](https://api.moneroocean.stream/pool/stats)

### Inferences
- Viable now:
  - **unMineable to BTC**: automatic daily on-chain BTC payout, with the operator never touching XMR. Costs are 1% (0.75% with referral) plus an undisclosed conversion spread.
  - **Kryptex username login**: its revenue is PPS+, so there is no luck variance. It is much larger than MoneroOcean (about 4x the hashrate) and has a better-documented per-worker API.
- Kryptex's catch for Otium is the payout model. Auto-convert lives on kryptex.com, where withdrawals are user-initiated and manually confirmed. The operator would have to withdraw by hand (or script it against an undocumented API) to refill the BTC treasury. Wallet-address mining keeps the full public API but pays in XMR, which brings back the original problem.
- Per-worker crediting with Kryptex username mining needs testing. The public worker API rejected a username (403), so worker stats for auto-convert accounts may be visible only in the logged-in dashboard. A test account should be tried before committing.
- The unMineable per-worker structure fits Otium's 10-minute crediting loop (per-algo workers list plus chart), but per-worker field names and history granularity still need to be checked against a live address with workers.
- Algo-switching pools that pay BTC for CPUs have mostly collapsed (Zergpool and ProHashing shut down, no RandomX on zpool, MiningDutch RandomX empty). Building on them is a counterparty risk; Zergpool's disorderly shutdown left balances unpaid.

### Gaps
- unMineable's ToS on aggregating many users' devices, KYC triggers, worker limits, the exchange-spread size and the exact per-worker JSON fields. Its FAQ and terms URLs returned 404, and I had no active address with which to inspect worker records.
- Kryptex's terms (the /en/terms URL returned 404). There is no statement on worker limits per account, aggregating third-party devices, or withdrawal fees by network. Whether username accounts have an authenticated API, or programmatic withdrawal, is also unknown.
- Whether NiceHash's KYC rules have changed since 2024; I found no primary NiceHash source.
- Uptime and reputation data for unMineable and Kryptex in 2025–2026 (beyond pool size). I found no independent incident reports.

## 2. CPU coins in 2026 that pay better than XMR on desktop CPUs

### Takeaway
On hashrate.no (page undated, fetched 2026-10-16), the only RandomX coins slightly above XMR for a Ryzen 9 7950X are **Epic Cash** (+17%) and **Tari XTM** (+6%). Zephyr, Salvium, QRL and VerusHash pay less. Qubic claimed 3x XMR in mid-2025, but those are self-reported, dated figures from a controversial project, and I found no 2026 per-CPU numbers. Tari via Kryptex's auto-convert to BTC is the most practical "beats XMR and pays BTC" option, but the margin is small and fluctuates.

### Cited Findings
- hashrate.no, Ryzen 9 7950X at 21.89 kH/s RandomX, 24h revenue (no date on the page, fetched 2026-10-16; profit at $0.10/kWh in brackets):
  - Epic Cash EPIC: $0.98 ($0.42)
  - Tari XTM: $0.89 ($0.34)
  - Monero XMR: $0.84 ($0.29), with "RandomX merged XMR" also $0.84
  - Xelis XEL (XelisHashv3): $0.73
  - Zephyr ZEPH: $0.51
  - QRL: $0.40
  - Salvium SAL: $0.40
  - Raptoreum RTM (GhostRider): $0.13
  - VerusCoin VRSC: $0.12

  Qubic and SCASH were not listed. — [hashrate.no 7950X](https://www.hashrate.no/cpus/7950X)
- hashrate.no's CPU overview now ranks desktop CPUs by Tari (XTM, RandomX) yield. For example, it shows a 7950X3D at $0.92/day and a 5950X at $0.74/day. — [hashrate.no CPUs](https://www.hashrate.no/cpus)
- Kryptex's CPU table, "Last updated: 16/10/2026", ranks all CPUs on XMR (RandomX). Example: Ryzen 9 7900 at 19 KH/s with revenue $300 and profit $277. The period is not labelled; it appears to be yearly (19 KH/s × $0.84/21.89 kH/s ≈ $0.73/day ≈ $266/yr). — [Kryptex CPU table](https://pool.kryptex.com/device/cpu)
- Qubic (self-reported, mid-2025): $3.13/day per 7950X via the Qli pool versus $0.64/day for Monero, and "approximately 3x profitability over Monero". Profitability dropped as the miner count doubled. Qubic's XMR earnings were converted to USDT to buy back and burn QUBIC. — [Qubic blog](https://qubic.org/blog-detail/why-qubic-is-now-the-top-profitable-coin-to-mine-in-2025); [Epoch 172 recap](https://qubic.org/blog-detail/epoch-172-recap-the-monero-experiment); [Epoch 171 recap](https://qubic.org/blog-detail/qubic-mining-recap-(epoch-171)-big-changes)
- Qubic claimed a 2.77 GH/s peak and 50% of Monero's hashrate. An independent paper found an average effective share of about 22%, with short intervals of 23–34%. Kraken paused XMR deposits during the August 2025 "51% attack" episode. — [arXiv 2512.01437](https://arxiv.org/pdf/2512.01437v2); [FXStreet Aug 2025](https://www.fxstreet.com/cryptocurrencies/news/monero-price-forecast-xmr-eyes-wedge-breakout-rally-amid-qubic-hashrate-dominance-202508180526)
- Qubic mining uses qubic.li's own client (`mine.qubic.li`, with an access token from the qubic.li panel). The guide I found is a HiveOS/GPU guide, not XMRig. — [snoby/hiveos](https://github.com/snoby/hiveos)
- XMRig v6.26.0 (2026-03-28) added "RandomX v2" support and VAES-512 for Zen 5. v6.25.0 (2025-12-23) added solo-mining support for the FCMP++ hardfork. GhostRider (Raptoreum) is supported. — [XMRig releases](https://github.com/xmrig/xmrig/releases)
- As of 12 Oct 2026, Monero had no confirmed mainnet hard-fork date. A non-binding roadmap points to week 16 of 2027 for FCMP++. The 5 Oct 2026 fork was a stressnet only. — [Bitrue](https://www.bitrue.com/blog/when-is-next-monero-hard-fork-xmr-testnet-upgrade); [CryptoTicker](https://cryptoticker.io/en/monero-fcmp-stressnet-fork-october-2026/)
- Blog-level 2026 guides say: "Monero is the standard CPU pick"; it is profitable below about $0.10–0.12/kWh on modern Ryzen; the 9950X does about 28–30 kH/s; and the network was about 5.83 GH/s in March 2026. — [MiningReturns](https://miningreturns.com/news/monero-cpu-mining-still-profitable-2026); [LaikaLabs](https://laikalabs.ai/market-intelligence/best-cpu-mining-coins)

### Inferences
- Nothing on standard open-source tooling clearly beats XMR by enough to justify the extra liquidity risk. Tari (+6%) is the best fit because XMRig mines it unmodified (`rx/0`), Kryptex auto-converts it to BTC, and the Kryptex pool has the same per-worker API. Epic Cash leads on paper but has thin liquidity, and I found no BTC-converting pool for it.
- MoneroOcean already algo-switches among RandomX-family coins, so its effective rate may already capture some of the XTM/ZEPH/SAL premium. A plain-XMR comparison understates it.
- Qubic is a reputational and technical mismatch for Otium. It uses a proprietary client, pays in QUBIC (needs a swap), is tied to the 51%-attack controversy, and its profitability figures are dated and self-reported.

### Gaps
- No dated WhatToMine snapshot (not fetched), and hashrate.no gives no date on its estimates.
- No 2026 per-CPU Qubic revenue, no Qubic pool API documentation, and no data on QUBIC→BTC liquidity or exchange KYC.
- Exchange listings and liquidity for XTM, EPIC, ZEPH and SAL to BTC on non-KYC venues were not researched.
- Whether Monero will adopt RandomX v2 on mainnet, and when, is unconfirmed. XMRig added support, but I found no fork date.

## 3. Kryptex's desktop app as precedent

### Takeaway
The Kryptex app (Windows) automatically picks the most profitable algorithm and pays users in BTC (or USDT and others) through a custodial kryptex.com balance with no KYC. For CPUs, it mines RandomX coins such as XMR and SAL, auto-converted to BTC. This is essentially the model Otium is building, with Kryptex acting as both pool and converter.

### Cited Findings
- The Kryptex app "selects the most profitable algorithm" and switches coins automatically. Kryptex says "mine any coin — get paid in BTC". The app is Windows-only and pitched mainly at GPUs. — [Kryptex Miner](https://www.kryptex.com/pro-app); [Kryptex features](https://api.kryptex.com/features)
- SAL (RandomX, CPU) support arrived in the Kryptex app beta 4.42.0 with BTC payout (Aug 2024). Withdrawals go out in BTC, ETH, USDT, Volet and others. — [Kryptex SAL news](https://www.kryptex.com/en/news/sal-btc-en)
- kryptex.com withdrawals start at $1, there is no KYC, payouts are manual and user-initiated, and the referral programme pays up to 2% of referrals' earnings. — [Kryptex comparison, 14/09/2026](https://pool.kryptex.com/articles/kryptex-vs-kryptex-pool-en)

### Inferences
- Kryptex shows that a no-KYC, BTC-paid CPU mining app is commercially viable at scale (30k workers on its XMR pool alone). It is also a direct competitor to Otium.

### Gaps
- I did not find the exact list of CPU coins the app currently mines, or its effective fee and spread to end users.

## 4. Pools that pay directly in BTC, ETH, BNB or SOL (skipping treasury swaps)

### Takeaway
unMineable is the only working RandomX pool I found that pays straight to a chosen-coin address: BTC and 80+ assets, which likely include ETH, BNB and SOL. Its address model is one payout address per coin, which works for an operator-level treasury but not for paying each user. Kryptex withdrawals cover BTC, ETH, LTC and USDT, but not BNB or SOL, and are manual.

### Cited Findings
- unMineable lists "80+ supported assets". The BTC page confirms RandomX→BTC with a daily automatic payout and a 0.00075 BTC minimum. Its address API returns a per-coin `chains` list with a `payment_threshold` per network. — [unmineable.com](https://unmineable.com/); [unMineable BTC](https://unmineable.com/coins/BTC); [API](https://api.unminable.com/v4/address/bc1qxy2kgdygjrsqtzq2n0yrf2493p83kkfjhx0wlh?coin=BTC)
- The worker login prefixes the coin (`COIN:ADDRESS.WORKER`), so stats and balances are tracked per payout address. — [Scribd guide](https://www.scribd.com/document/518995343/Mining)
- Kryptex withdrawal coins: BTC (including Lightning), ETH, LTC, USDT (BEP20), Volet and fiat. — [Kryptex comparison](https://pool.kryptex.com/articles/kryptex-vs-kryptex-pool-en)

### Inferences
- An operator could run parallel unMineable payout streams (for example `ETH:opAddr.worker`, `SOL:opAddr.worker`) to receive each payout coin directly. However, users' hashrate would then be split by coin preference, and each address has its own threshold. A simpler route is to mine everything to `BTC:opAddr.<userId>` and keep the existing treasury swaps (BTC→ETH/BNB/SOL via THORChain).
- Paying users per-address directly from the pool is impractical. Per-user minimum thresholds (0.00075 BTC) would trap small balances.

### Gaps
- I did not confirm that unMineable's current list includes BNB (BEP20) and SOL, or their thresholds; the /coins page for those was not fetched.
- I did not check whether unMineable allows thousands of distinct worker names under one address.
