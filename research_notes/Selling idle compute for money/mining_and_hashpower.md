# Crypto Mining & Hashpower Marketplaces for Consumer PCs (as of Oct 2026)

Research date: 2026-10-15. All profitability numbers are snapshots; each carries the date it was fetched or that the source page displays. Several calculator pages (WhatToMine /gpus, hashrate.no) do not print a timestamp. Those are marked "fetched 2026-10-15, page undated". They were live pages fetched on that day.

---

## 1. Current daily profitability for consumer hardware (USD/day, before/after electricity)

### Takeaway
As of 15 Oct 2026, GPU mining for consumers is profitable only because of a few new coins. The biggest is **Pearl (PRL)**, an "AI proof-of-useful-work" coin launched 27 Apr 2026. Others are Quantus (QPoW) and Epic Cash (ProgPow). The classic GPU coins (Ravencoin, Ergo) lose money at $0.10/kWh, and Kaspa is ASIC-only. Rough figures for an RTX 4090 are about $10/day revenue and about $9.5/day net at $0.10/kWh. An RTX 3060 makes about $1.4/day. AMD cards make well under $1–2/day. Pearl's yield is falling fast, and a planned upgrade may lock consumer GPUs out, so this is a fragile window. CPU mining on Monero/RandomX earns about **$0.041 per KH/s per day**. A Ryzen 9 7950X/9950X therefore grosses about $1.1/day. Mid-range CPUs make $0.25–0.6/day and Apple M1 about $0.08/day. CPU mining is near break-even at $0.15/kWh and loses money at $0.30/kWh, except on very efficient chips.

### Cited Findings

**GPU – WhatToMine /gpus (fetched 2026-10-15, page undated; $0.10/kWh; GPU-only power; best coin = Pearl except AMD = Epic Cash)** — [WhatToMine GPUs](https://whattomine.com/gpus)

| GPU | Best coin (algo) | Hashrate / power | Revenue/day | Profit @ $0.10 (source) | Break-even $/kWh (source) |
|---|---|---|---|---|---|
| RTX 3060 | PRL (Pearl) | 41 Th/s / 100 W | $1.39 | $1.15 | $0.58 |
| RTX 3080 | PRL | 100 Th/s / 240 W | $3.39 | $2.82 | $0.59 |
| RTX 4070 | PRL | 97 Th/s / 150 W | $3.29 | $2.93 | $0.91 |
| RTX 4090 | PRL | 310 Th/s / 400 W | $10.52 | $9.56 | $1.10 |
| RX 6600 | EPIC (ProgPow) | 14 Mh/s / 110 W | $0.51 | $0.24 | $0.19 |
| RX 7900 XTX* | EPIC (ProgPow) | 56 Mh/s / 320 W | $2.02 | $1.25 | $0.26 |

\*WhatToMine labels the 7900 XTX entry "Limited, unverified results". The other top coins on the same page were: 3060 QUANTUS $0.81 and EPIC $0.44; 4070 QUANTUS $2.25 and KRX $1.84; 4090 QUANTUS $4.91 and EPIC $1.51; RX 6600 XTM (Tari Cuckaroo29) $0.19; 7900 XTX PRL $0.81. — [WhatToMine GPUs](https://whattomine.com/gpus)

**Same GPU data at $0.15 and $0.30/kWh.** These are computed by me as revenue − (W × 24 h × price), GPU power only, no PC overhead:

| GPU | Net @ $0.10 | Net @ $0.15 | Net @ $0.30 |
|---|---|---|---|
| RTX 3060 (100 W) | $1.15 | $1.03 | $0.67 |
| RTX 3080 (240 W) | $2.81 | $2.53 | $1.66 |
| RTX 4070 (150 W) | $2.93 | $2.75 | $2.21 |
| RTX 4090 (400 W) | $9.56 | $9.08 | $7.64 |
| RX 6600 (110 W) | $0.25 | $0.11 | −$0.28 |
| RX 7900 XTX (320 W) | $1.25 | $0.87 | −$0.28 |

- **hashrate.no, RTX 4090 (fetched 2026-10-15, page undated, $0.10/kWh)**, 24 h revenue/profit: Pearl $10.99/$10.32 (308.21 Th/s at about 280 W, 6.13 PRL/day, "ROI 155 days"). Quantus $8.74/$8.02; Parano1d (NOID) $5.94/$5.24; Epic Cash $2.26/$1.54; Qubitcoin (QTC) $1.99/$1.37; Aeternity $1.26/$0.62; Conflux $1.03/$0.36; Tari C29 $0.56/−$0.01. — [hashrate.no RTX 4090](https://hashrate.no/gpus/4090)
- **WhatToMine /coins, timestamped "last updated 2026-10-15 20:58:09 UTC"**, default rig of 3× RTX 3070, $0.10/kWh. Rig figures: Pearl revenue $6.92, profit $5.84 (204 Th/s, 450 W). Quantus $4.89/$4.10. EPIC $2.95/$1.66. Qubitcoin $1.30/$0.65. **Ravencoin (KawPow) $0.36/−$0.94** (82.8 Mh/s, 540 W). **Ergo (Autolykos) $0.64/−$0.30**. Alephium, Kaspa, Zephyr and Dero were not in the GPU table. XMR was priced at $545 in the ticker. — [WhatToMine coins](https://whattomine.com/coins)
- An earlier hashrate.no snapshot cited in search results showed a 4090 at only $1.90/day revenue. That was before Pearl, which shows how much the figure depends on the coin. — [hashrate.no historical 4090](https://hashrate.no/gpus/4090/historical) (via search snippet, undated)
- **Pearl (PRL) context.** Mainnet launched 27 Apr 2026. Mining is matrix multiplication plus a ZK proof, so tensor-core throughput dominates. Tom's Hardware reported RTX 5090 revenue halving from about $33.80/day to $17.19/day after launch. The 3.6.8 release notes from a miner developer warn that an upcoming Pearl upgrade will make consumer GPUs "pretty much useless" for it. An FP8 spec from Sept 2026 had not yet been scheduled as a fork as of 2 Oct. — [Tom's Hardware](https://tomshardware.com/tech-industry/cryptomining/new-ai-compute-cryptocurrency-pearl-sparks-a-gpu-mining-rush-but-profitability-is-sliding); [SimeonOnSecurity Pearl guide](https://simeononsecurity.com/articles/pearl-prl-mining-guide-2026/); [Hashrate Index](https://hashrateindex.com/blog/pearl-prl-ai-compute-cryptocurrency/)
- Kryptex's changelog lists a June 2026 release that added the pearlhash algorithm via SRBMiner (NVIDIA and AMD GPUs; AMD later limited to RDNA2). One guide says Pearl is NVIDIA-only, so the sources conflict. — [Kryptex changelog](https://www.kryptex.com/ms/changelog); [SimeonOnSecurity](https://simeononsecurity.com/articles/pearl-prl-mining-guide-2026/)
- **Kaspa:** ASICs have dominated since 2023, and GPU mining of kHeavyHash is "hardly profitable". A vendor guide estimates a GPU earns cents a day while burning dollars of power. — [BTC-Echo](https://www.btc-echo.de/academy/bibliothek/kaspa-mining/); [MillionMiner 2026](https://millionminer.com/el/news/how-to-mine-kaspa-2026); [ViaBTC](https://www.viabtc.com/en/blog/Insights-best-asic-miner-for-kaspa-mining-a-practical-guide-609?category=0)

**CPU – Monero/RandomX economics (minerstat snapshot, network chart labelled "Oct 15", fetched 2026-10-15).** XMR $546.81, network hashrate 5.848 GH/s, difficulty 701.73 G, block reward 0.6111 XMR, 120 s blocks. — [minerstat XMR](https://minerstat.com/coin/xmr/profitability). CoinWarz showed XMR at $541.54 on 15 Oct 2026. — [CoinWarz](https://www.coinwarz.com/monero-mining-calculator)
- Derived figure (my calculation): 720 blocks × 0.6111 XMR ÷ 5,848,000 KH/s = 7.52e-5 XMR per KH/s per day, about **$0.041/KH/s/day** before pool fee.
- CPU RandomX hashrates are XMRig community benchmark averages. — [XMRig benchmark](https://xmrig.com/benchmark). The M1 figure is Kryptex's pool page (2.00 KH/s at about 35 W). — [Kryptex Apple M1](https://pool.kryptex.com/vi/device/cpu/apple/apple-m1); M1 Pro 3.51 KH/s at 30 W — [Kryptex M1 Pro](https://pool.kryptex.com/vi/device/cpu/Apple/apple-m1-pro)

| CPU | RandomX H/s (source) | Gross XMR revenue/day (computed) | Assumed incremental W* | Net @ $0.10 | Net @ $0.15 | Net @ $0.30 |
|---|---|---|---|---|---|---|
| Ryzen 9 9950X | 28,416 | $1.17 | 170 | $0.76 | $0.56 | −$0.05 |
| Ryzen 9 7950X | 26,960 | $1.11 | 170 | $0.70 | $0.50 | −$0.11 |
| Ryzen 9 5950X | 23,370 | $0.96 | 140 | $0.62 | $0.46 | −$0.05 |
| Ryzen 7 7700X | 13,658 | $0.56 | 100 | $0.32 | $0.20 | −$0.16 |
| Ryzen 7 5800X | 12,445 | $0.51 | 100 | $0.27 | $0.15 | −$0.21 |
| Ryzen 5 7600 | 10,387 | $0.43 | 75 | $0.25 | $0.16 | −$0.11 |
| Ryzen 5 5600X | 9,100 | $0.37 | 75 | $0.19 | $0.10 | −$0.17 |
| Intel i5-12400 | 6,238 | $0.26 | 65 | $0.10 | $0.03 | −$0.21 |
| Intel i5-13600K | 11,203 | $0.46 | 125 | $0.16 | $0.01 | −$0.44 |
| Intel i7-12700K | 10,020 | $0.41 | 125 | $0.11 | −$0.04 | −$0.49 |
| Intel i7-13700K | 14,579 | $0.60 | 150 | $0.24 | $0.06 | −$0.48 |
| Intel i7-14700K | 15,923 | $0.65 | 150 | $0.29 | $0.11 | −$0.43 |
| Apple M1 | 2,000 (Kryptex) | $0.08 | 35 (Kryptex) | ≈$0.00 | −$0.04 | −$0.17 |
| Apple M1 Pro | 3,510 (Kryptex) | $0.14 | 30 (Kryptex) | $0.07 | $0.04 | −$0.07 |
| Apple M3 Max | 7,582 | $0.31 | 50 | $0.19 | $0.13 | −$0.05 |
| Apple M4 Max | 8,585 | $0.35 | 50 | $0.23 | $0.17 | $0.00 |

\*The power figures are my assumptions, except for the M1/M1 Pro (Kryptex) and the 7950X, whose 170 W default TDP is cited by [Club386](https://www.club386.com/qubic-crypto-miners-target-amd-ryzen-9-7950x-cpus/). No source gave measured mining wattage for these CPUs. See the Inferences section.

- **hashrate.no CPUs (fetched 2026-10-15, page undated, best coin shown = Epic Cash).** 24 h revenue/profit: 9950X $0.91/$0.43; 7950X $0.96/$0.41; 5950X $0.79/$0.48; 7700X $0.57/$0.32; 5800X $0.49/$0.24; 7800X3D $0.45/$0.16; 7600 $0.36/$0.21; i9-13900K $0.59/$0.29; i7-12700 $0.23/$0.08; i7-10700 $0.27/$0.11; i5-11400 $0.21/$0.06. Apple chips were not listed. — [hashrate.no CPUs](https://hashrate.no/cpus). These are consistent with the XMR-based table above.
- **Qubic (CPU, AVX-512 favoured).** In 2024, calculators put a 7950X at about $3/day net at 170 W. No 2026 figure was found. — [Club386](https://www.club386.com/qubic-crypto-miners-target-amd-ryzen-9-7950x-cpus/); [Viperatech](https://viperatech.com/news-details/the-ryzen-reckoning-how-qubic-mining-sent-amd-stocks-into-a-frenzy). Qubic's own self-reported claim is "~3x profitability over Monero". — [Qubic Epoch 172 recap](https://qubic.org/blog-detail/epoch-172-recap-the-monero-experiment)
- **Verus (CPU/ARM):** one VerusHash reference page lists even an EPYC 9965 at −$1.08/day at $0.10/kWh (undated). — [MiningBoard VerusHash](https://miningboard.com/algorithms/VerusHash). hashrate.no only shows VRSC as the best coin for Threadripper PRO/EPYC parts. — [hashrate.no CPUs](https://hashrate.no/cpus)
- **RandomX ASICs exist.** minerstat lists the Bitmain Antminer X9 at $40.92/day revenue and the X5 at $8.67/day. EPYC 9654 is $3.00/day. — [minerstat XMR](https://minerstat.com/coin/xmr/profitability). Vendor guides say these units out-earn CPUs by a wide margin. — [MillionMiner](https://millionminer.com/news/monero-mining-guide-2026-antminer-x5-x9-pinecone-r1x-randomx)
- An April 2026 guide says a difficulty increase cut daily earnings by about 5–8% versus early March 2026 for the same hardware. — [changee.com](https://changee.com/xmr-mining-profitability-in-2026)
- Monero has a perpetual tail emission of 0.6 XMR per block. — [millionminer](https://millionminer.com/news/is-monero-mining-profitable)

### Inferences
- **Consumer GPU mining in 2026 depends on one fragile coin.** Without PRL/Quantus, an RTX 4090 falls to about $1.5–2/day gross (EPIC/QTC) and a 3060 to about $0.4–0.8. Pearl's revenue has already halved once, and a planned upgrade may exclude consumer GPUs. A product should not market current Pearl numbers as durable.
- **The NVIDIA vs AMD gap is large in 2026**, because Pearl rewards tensor cores. AMD cards earn roughly 5–10× less per watt than RTX 40-series on these snapshots.
- **CPU (XMR) income is about $0.04/KH/s/day.** Mainstream desktops make $0.25–0.65/day gross and a 16-core Ryzen about $1.1. At $0.30/kWh (much of the EU, California), almost all CPUs lose money. The incremental power assumptions matter a lot, so a wrapper should measure real watts (for example via RAPL/powermetrics) rather than use TDP.
- **Apple Silicon** base chips earn cents per day. M-series Max/Ultra chips are the only Macs that clear about $0.2/day at $0.10/kWh.
- CPU revenue estimates should subtract a pool fee (about 0–1%) and, on XMRig, the 1% donation.

### Gaps
- WhatToMine /gpus and hashrate.no pages do not print a data timestamp. They were fetched 2026-10-15.
- No 2026 NiceHash profitability calculator numbers were retrieved; the page is JS-driven. NiceHash pays per algorithm in BTC and typically runs below direct-pool revenue, but no 2026 figure was obtained.
- No source covered the RX 7900 (non-XTX), RTX 4070 on non-Pearl coins, or base M2/M3 RandomX hashrates (the XMRig benchmark page was truncated).
- No current data on Alephium, Zephyr, Dero or Ravencoin per-card profitability, beyond RVN/ERG being negative on a 3×3070 rig.
- No measured mining wattage for desktop CPUs. The table uses stated assumptions.

---

## 2. Platforms: NiceHash, unMineable, Kryptex, Cudo Miner, Salad — OS support, payouts, minimums, fees

### Takeaway
None of the "one-click" consumer miners supports macOS. NiceHash (QuickMiner, NiceHash Miner, Excavator), Kryptex and unMiner are Windows-only on desktop. Kryptex also has an Android miner. NiceHash pays BTC every 4 hours, has a 2% service charge and very low payout thresholds, and supports Lightning, but it now requires KYC. Kryptex pays BTC/USDT/other coins and gift cards with no KYC. unMineable charges 1% (0.75% with a referral code) and pays in 80+ coins with per-coin thresholds. On macOS and Linux, the realistic route is open/closed CLI miners (XMRig, SRBMiner, lolMiner) pointed at pools, NiceHash stratum or unMineable.

### Cited Findings
**NiceHash**
- Payouts happen every 4 hours, or daily at 2AM CEST. If a miner is disconnected and the unpaid balance is ≥1000 sat or 0.01 USDT, payment is every 8 h. The minimum threshold is 0.00001 BTC or 0.01 USDT, and it is user-adjustable. A **2% service charge** is deducted from mining payments. Lightning withdrawals can be automated. — [NiceHash: When and how do you get paid](https://www.nicehash.com/support/mining-help/earnings-and-payments/when-and-how-do-you-get-paid)
- Withdrawal fees from the official table:

| Option | Minimum | Fee |
|---|---|---|
| BTC on-chain | 0.0005 BTC | 0.0001 BTC+ (varies) |
| BTC Lightning | 200 sats | 0.2% + routing (min 150, max 10,000 sats); free with a direct channel to the NiceHash node |
| USDT / USDC | 10 | 1+ |
| "Virgin Bitcoin" (0/1/2 hops) | 0.002 BTC | 10% / 7% / 5% + network fee |

  A public fee API endpoint exists. — [NiceHash Withdrawal Fees](https://www.nicehash.com/support/general-help/service-fees/withdrawal-fees)
- Automated Lightning **mining payouts** have a minimum of 10 sats. There is no fee if the destination wallet has a channel with the NiceHash node; otherwise routing fees apply. — [NiceHash LN payouts](https://www.nicehash.com/support/mining-help/earnings-and-payments/nicehash-mining-lighting-network-payouts); [NiceHash blog](https://www.nicehash.com/blog/post/game-changer-nicehashs-instant-lightning-network-payouts)
- **KYC is mandatory.** A competitor (Kryptex) article says NiceHash announced mandatory KYC for all users worldwide: deposits and withdrawals are blocked and mining stops on accounts without KYC. The dates in the article are internally inconsistent ("announced Nov 29", "starting Nov 4", "Nov 12" mining stop, no year given). — [Kryptex: NiceHash alternative](https://www.kryptex.com/en/articles/nicehash-alternative-en). User reports corroborate KYC before withdrawing or continuing to mine. — [Trustpilot](https://ie.trustpilot.com/review/www.nicehash.com?page=6). There is also a 24 h withdrawal hold after setting up 2FA. — [r/NiceHash thread](https://pol1.lr.ggtyler.dev/r/NiceHash/comments/1gxme17/easiest_way_to_withdraw)
- NiceHash has a "60-day rule" compliance program for miners (pages exist; contents not retrieved). — [NiceHash 60-day rule](https://www.nicehash.com/support/general-help/60-day-rule/how-nicehash-helps-miners-stay-compliant-with-the-60-day-rule)
- **OS support:** NiceHash's consumer software is Windows-only. NiceHash Miner does not run on Linux (NHOS is the Linux-based rig OS), and community clients such as Nuxhash drive Excavator on Linux. — [Linux Handbook](https://linuxhandbook.com/nicehash-linux/); [BeInCrypto](https://beincrypto.com/learn/ethereum-nicehash-os-mining/). QuickMiner bundles Excavator, a watchdog and OCTune (overclock/fan tool). — [QuickMiner repo mirror](https://github.com/Gujjargit/NiceHashQuickMiner)

**unMineable**
- Mining fee is 1%, or 0.75% with a referral code. There are no payout fees beyond network fees. — [GitHub gist review](https://gist.github.com/svv3085/b4ee22f7f19d7807b261288fe8847541); [qgu48331/unMineable](https://github.com/qgu48331/unMineable) (both promotional). Referral split per those sources: 0.5% platform / 0.25% referrer / 0.25% back to miner. Other pages give conflicting splits (0.25% commission; "up to 0.5%"). — [unmineable.wordpress](https://unmineable.wordpress.com/2024/12/28/unmineable-referral-code/)
- Payouts in "80+ supported assets". The unMiner desktop app (v3.6.1, 91 MB) is listed for **Windows 10/11 x64 only**, and only an email is needed to start. — [unmineable.com](https://www.unmineable.com/)
- Example per-coin minimums from official coin pages (cached, may be stale): TRX 15 TRX; SOL 0.05; ETH 0.025; SHIB 2,000,000 (ERC-20) or 150,000 (BSC). — [unMineable TRX](https://unmineable.com/coins/TRX); [SOL](https://unmineable.com/coins/SOL); [ETH](https://unmineable.com/coins/ETH); [SHIB](https://unmineable.com/coins/SHIB)

**Kryptex**
- The desktop app is Windows-only; there is also an Android miner and a monitoring app. The app detects hardware, picks the most profitable algorithm and switches coins in real time. Withdrawals go in BTC, USDT, ETH, USDC, LTC, BNB or TRX, or as Volet and Amazon gift cards. **Minimum withdrawal is 0.00025 BTC.** Kryptex Pool (PPS+, PROP, SOLO; 30+ coins) is a separate product. — [kryptex.com](https://www.kryptex.com/en)
- Kryptex pool fees are 1% for PROP/SOLO and 1–3% for PPS+. Kryptex states it requires no KYC. — [Kryptex: NiceHash alternative](https://www.kryptex.com/en/articles/nicehash-alternative-en)

**Salad**
- Crypto mining is Salad's **fallback/base workload**: "if there are no Containers available to chop, Salad will default to these workloads". Balance refreshes every 15 min, and earnings depend on container demand, bandwidth demand and crypto performance. — [Salad support](https://support.salad.com/faq/jobs/how-often-do-i-earn-with-salad); [Salad "how much do earnings rely on market"](https://support.salad.com/faq/salad-app/how-much-do-my-earnings-rely-on-the-market)
- Workload types are container workloads, bandwidth sharing and GPU crypto mining. — [Salad: how does my machine earn](https://support.salad.com/faq/jobs/how-does-my-machine-earn-salad-balance)
- In January 2026 Salad partnered with Golem Network and said it is working toward crypto payments. — [Barchart/Chainwire](https://www.barchart.com/story/news/37019027/salad-com-and-golem-network-partner-to-integrate-web2-workloads-with-decentralized-infrastructure)
- History: in the Ethereum era, Tom's Hardware estimated Salad earned about $3.6M from mining on users' PCs but paid out about $0.5M in rewards. This is the writer's estimate. — [Tom's Hardware](https://tomshardware.com/news/salad-9th-fastest-supercomputer)

**Cudo Miner**
- CB Insights lists the company as "alive". No shutdown announcement was found. — [CB Insights](https://www.cbinsights.com/company/cudo-miner). cudominer.com returned 403 to fetch.

### Inferences
- The consumer-app incumbents (NiceHash, Kryptex, unMineable, Salad) leave **macOS and Linux unserved** by one-click GUIs. A cross-platform wrapper around XMRig, plus GPU miners on Windows/Linux, would be differentiated there. The economics on Macs are poor, though (section 1).
- NiceHash's KYC requirement and 2% fee add friction for "earn pocket money" users. Kryptex's no-KYC, gift-card model is the closer comparable for a consumer app.
- Custodial models (Kryptex, Salad, unMiner) let users accumulate tiny balances. With direct pool mining, the user must reach coin-specific minimums such as 0.003 XMR on MoneroOcean.

### Gaps
- NiceHash's official KYC announcement and date were not retrieved. Neither were NiceHash's minimum hashrate and payout rules for external BTC addresses versus a NiceHash wallet.
- unMineable's official FAQ returned 404, so fee and referral figures come from promotional sources.
- Cudo Miner's 2026 status, OS support and payouts are unverified (site blocked).
- Which miners and coins Salad uses in 2026 is not found. Salad's FAQ pages "Which Miners do Salad Actually Use?" exist, but their content was not retrieved.

---

## 3. Monero / XMRig ecosystem: RandomX, P2Pool, SupportXMR, MoneroOcean, Qubic

### Takeaway
XMRig (GPL-3.0) is the standard cross-platform CPU miner: Windows, Linux, macOS (including ARM64) and FreeBSD. It has a 1% default donation and an HTTP API. Pool choices are P2Pool (0% fee, about 0.00027 XMR minimum, but needs a local monerod and p2pool node), centralized PPLNS pools such as SupportXMR, and MoneroOcean, an algo-switching pool with its own XMRig fork, about 1% fee and a 0.003 XMR minimum. Structural risks in 2026: RandomX ASICs (Bitmain X5/X9), Qubic's "useful PoW" pool controlling a large share of Monero hashrate since Aug 2025, and FCMP++ still in stressnet testing.

### Cited Findings
- XMRig is GPL-3.0, with official binaries for Windows, Linux, macOS and FreeBSD. Algorithms: RandomX, KawPow, CryptoNight, GhostRider. Backends: CPU (x86/x64/ARMv7/ARMv8/RISC-V), OpenCL (AMD) and CUDA via a plugin. Default donation is **1% (1 minute per 100)**. It can be raised via `donate-level`, but lowering to 0 requires changing the source. The repo ships a `bin/WinRing0` folder (the MSR driver used for RandomX boost on Windows). — [XMRig GitHub](https://github.com/xmrig/xmrig)
- XMRig HTTP API: a built-in HTTP server to "retrieve various data from the miner, change config and state on fly". It is grouped into Summary, Backends, Config and JSON-RPC (`POST /json_rpc`). The official client is workers.xmrig.info. — [XMRig API docs](https://xmrig.com/docs/miner/api); [JSON-RPC stub](https://xmrig.com/docs/miner/api/json_rpc)
- Kryptex lists the Apple M1 at 2.00 KH/s and 35 W on RandomX, with XMR the most profitable. M1-minable coins include XMR, ZEPH (Zephyr), SAL (Salvium) and XTM-RX (Tari RandomX). XMRig is recommended. — [Kryptex Apple M1](https://pool.kryptex.com/vi/device/cpu/apple/apple-m1)
- **P2Pool** is a decentralized sidechain with PPLNS over up to 2160 shares (about 6 h) and **0% fee and 0 payout fee by design**. Miners are paid directly in the Monero coinbase. Minimum payout ≈ block reward/2160 ≈ 0.00027 XMR. The miner must run their own Monero node. — [P2Pool GitHub](https://github.com/SChernykh/p2pool); [getmonero P2Pool](https://web.getmonero.org/2021/10/05/p2pool-released.html). P2Pool "Mini" is suggested for under about 100 KH/s (community guide). — [arnoldnakamura P2Pool guide](https://arnoldnakamura.codeberg.page/monero-p2pool/). Gupax is an open-source GUI that bundles P2Pool and XMRig. — [Gupax GitHub](https://github.com/hinto-janai/gupax)
- **MoneroOcean:** an algo-switching pool (RandomX, plus KawPow and Autolykos2, etc.) using its own XMRig fork (github.com/MoneroOcean/xmrig) that benchmarks and switches algorithms. Minimum payout is 0.003 XMR. The fee is reported as 1% in most sources and 0% in one 2026 guide, so the sources conflict. — [forge.lthn.ai pool research](https://forge.lthn.ai/Snider/Mining/src/tag/v0.0.10/docs/pool-research.md); [MiningBoard](https://miningboard.com/zh/pools/moneroocean.stream); [arnoldnakamura best pool](https://arnoldnakamura.codeberg.page/best-monero-mining-pool/); [Gentoo xmrig-mo](https://gpo.zugaina.org/net-misc/xmrig-mo)
- **SupportXMR** is a PPLNS pool. Its homepage fetch did not show the fee or minimum. — [supportxmr.com](https://supportxmr.com/)
- **Qubic** said in Aug 2025 that it had reached 51% of Monero hashrate. This was disputed, and later Qubic material claims 58%. — [Cointelegraph](https://cointelegraph.com/news/monero-qubic-selfish-mining-51-percent-attack); [The Block](https://www.theblock.co/post/366535/monero-faces-chain-reorganization-fears-after-qubic-says-it-controls-51-of-hashrate); [Qubic](https://qubic.org/blog-detail/historic-takeover-complete-qubic-miners-now-secure-monero-network)
- **FCMP++:** a stressnet fork was set for 5 Oct 2026 (beta v0.19.0.0-beta.3.0, 25 Sep 2026), with no mainnet date. — [CryptoTicker FR](https://cryptoticker.io/fr/monero-fcmp-stressnet-fork-octobre-2026/). No source confirmed a "RandomX v2".

### Inferences
- For a wrapper, XMRig plus MoneroOcean (algo-switching, low minimum) or P2Pool (zero fee, but heavy: about 100 GB+ node) are the realistic XMR backends. P2Pool's node requirement makes it a poor fit for a "one-click" consumer app unless the app connects to a remote node.
- The rise of RandomX ASICs and Qubic's share of hashrate pushes difficulty up. The per-KH/s yield for consumer CPUs will likely keep declining.

### Gaps
- SupportXMR's fee and minimum (commonly cited as 0.6% / 0.003 XMR in community lore) were not confirmed from a primary page.
- The exact XMRig JSON-RPC methods (pause/resume) were not documented on the fetched pages (stub). Reported paths such as `/1/summary` and `/2/backends` are not confirmed from primary text here.
- No 2026 measurement of Qubic's share of Monero hashrate.

---

## 4. Open-source and closed miners usable by a wrapper: OS, licenses, dev fees, APIs

### Takeaway
XMRig is the only fully open-source (GPL-3.0) cross-platform miner with macOS/ARM support. Most profitable 2026 GPU algorithms (Pearl, Quantus, ProgPow, KawPow, Autolykos2) are served by **closed-source** miners with built-in dev fees: SRBMiner-MULTI (0.85–2%), lolMiner (0.7–2.5%), Rigel (about 1%), GMiner/T-Rex (about 1%, older data). None of these supports macOS. NiceHash's Excavator is proprietary, Windows-only, works only with NiceHash, and its EULA prohibits redistribution.

### Cited Findings

| Miner | Source/license | OS | Hardware | Dev fee | Automation |
|---|---|---|---|---|---|
| XMRig | GPL-3.0 open source | Win, Linux, macOS, FreeBSD | CPU (x86, ARMv7/v8, RISC-V), AMD OpenCL, NVIDIA CUDA plugin | 1% default `donate-level` | HTTP API (summary, backends, config, JSON-RPC); JSON config file — [GitHub](https://github.com/xmrig/xmrig); [API](https://xmrig.com/docs/miner/api) |
| XMRig-MO (MoneroOcean fork) | XMRig fork | per XMRig | CPU/GPU | (n/a found) | Built-in algo benchmarking/switching — [Gentoo](https://gpo.zugaina.org/net-misc/xmrig-mo) |
| SRBMiner-MULTI | Proprietary ("All rights reserved"; GPU binaries proprietary, may only be run as part of an official release; no reverse engineering) | not stated on README | CPU, AMD, NVIDIA, Intel GPUs | randomx 0.85%, verushash 0.85%, kawpow 0.85%, autolykos2 1.0%, pearlhash 2.0% | not stated on README — [GitHub](https://github.com/doktor83/SRBMiner-Multi) |
| lolMiner | Closed source (no license stated) | Windows, Linux (no macOS) | AMD/NVIDIA | Alephium 0.75%, Autolykos2 1.5%, Kaspa 0.75%, Karlsen 1.0%, Radiant 0.75%, Etchash 0.7%, Cuckoo/Cuckatoo 2.0%, Cuckaroo30 2.5%, Nexa 2.0%, Octopus 2.0%, Equihash 1.0%, TON 1.0%; latest listed 1.96a | `--apiport`, `--apihost`, web GUI at `http://host:port/gui`; `--json` config — [GitHub](https://github.com/Lolliedieb/lolMiner-releases) |
| Rigel | Closed | (NVIDIA) | NVIDIA | 1% on recently added algos (sha256ton, fishhash) | — [GitHub releases](https://github.com/rigelminer/rigel/releases?page=1) |
| T-Rex / GMiner | Closed | Win/Linux | NVIDIA (+AMD for GMiner) | about 1–2% (2021–22 forum data, dated) | — [HiveOS forum](https://forum.hiveos.farm/t/ergo-miners-comparative/43102); [Hiveon forum](https://hiveon.com/forum/t/lhr-unlock-miners/75911) |
| Excavator (NiceHash) | Proprietary, EULA prohibits redistribution | Windows 10 64-bit | NVIDIA GTX 10/RTX 20/30, ≥6 GB | No devfee (NiceHash service charge instead); NiceHash stratum only since 1.6.1c | TCP JSON API port 3456 (`-p`), optional HTTP API (`-wp/-wi/-wa`), JSON command file (`-c`); API can set algos, workers, clocks, fans — [GitHub](https://github.com/nicehash/excavator) |

- Pool share snapshot (Kryptex Ergo pool): Rigel 34.8%, lolMiner 20.8%, SRBMiner-MULTI 12.8%, with GMiner and T-Rex far behind. This suggests T-Rex/GMiner are legacy. — [Kryptex Ergo top miners](https://pool.kryptex.com/pl/erg/top-miners)
- An ARM-optimized ccminer exists for Verus on phones and SBCs (ARMv8 with AES required). — [Oink70 CCminer-ARM](https://github.com/Oink70/CCminer-ARM-optimized/releases); [SimeonOnSecurity Android Verus](https://simeononsecurity.com/other/mine-verus-on-android-guide)

### Inferences
- **macOS/Apple Silicon:** only CPU mining via XMRig (RandomX: XMR, Zephyr, Salvium, Tari-RX) or ARM ccminer (Verus) is practical. No mainstream GPU miner targets Metal.
- A wrapper's GPU path on Windows/Linux will in practice depend on SRBMiner/lolMiner/Rigel binaries, which carry their own non-removable dev fees (about 0.75–2%) on top of any pool fee.

### Gaps
- No primary data found for BzMiner or OneZeroMiner fees, OS support or licenses.
- Redistribution terms for lolMiner/Rigel/T-Rex/GMiner binaries in a commercial installer were not found. SRBMiner's README restricts binary use to "official release" form.
- Quantus (QPoW) and Epic (ProgPow) miner specifics were not researched.

---

## 5. Automation surface: launching, monitoring and reading earnings

### Takeaway
A wrapper can launch miners as child processes using CLI flags or JSON config files, then poll local APIs. XMRig exposes an HTTP API, lolMiner `--apiport`, and Excavator a TCP JSON API on port 3456. Earnings come from pool APIs or NiceHash's v2 REST API (api2.nicehash.com, signed API keys with "mining" permission). Profitability data comes from WhatToMine, hashrate.no or minerstat.

### Cited Findings
- XMRig: the HTTP API covers summary, backends, config and JSON-RPC (`POST /json_rpc`), and can change config and state at runtime. Some options can be changed via API without restart. — [XMRig API](https://xmrig.com/docs/miner/api); [XMRig GitHub](https://github.com/xmrig/xmrig)
- Excavator: TCP API on 3456 with newline-terminated JSON (for example `device.list`). It can launch and switch algorithms, assign workers, dual/triple mine, and set power/clock/fan. An HTTP API is optional, and a JSON command file can schedule commands. The API is designed for third-party apps. — [Excavator GitHub](https://github.com/nicehash/excavator)
- lolMiner: `--apiport` / `--apihost`, a web GUI at `/gui`, and a `--json` config file. — [lolMiner](https://github.com/Lolliedieb/lolMiner-releases)
- NiceHash API: production is at api2.nicehash.com and a test environment at api-test.nicehash.com. API keys need "mining rig permission". The legacy `?method=rigs2` endpoint is from the old API. The v2 spec has `POST /main/api/v2/accounting/withdrawal`. — [nicehash-python (PyPI)](https://pypi.org/project/nicehash-python); [Jentic NiceHash spec](https://jentic.com/apis/nicehash.com/nicehash.md); [publicapis.io](https://publicapis.io/nice-hash-api). NiceHash withdrawal fees are available via a public fee API. — [NiceHash fees](https://www.nicehash.com/support/general-help/service-fees/withdrawal-fees)

### Inferences
- A typical wrapper loop: (1) benchmark each algorithm once with the target miner; (2) fetch coin revenue per unit hashrate from a profitability source; (3) pick the best net of measured power; (4) spawn the miner with `-o pool -u wallet.worker -p x` and an API port; (5) poll the local API every N seconds for hashrate and shares, and the pool or NiceHash API for balance.
- The XMRig JSON-RPC pause/resume interface is suited to "mine only when idle" behaviour (for example pausing when the user is active or on battery). XMRig also has its own `pause-on-battery`/`pause-on-active` options per its docs, but these were not confirmed in the fetched text.

### Gaps
- docs.nicehash.com returned HTTP 500, so the exact v2 mining endpoints (rigs list/status/actions, unpaid balance) are not confirmed.
- WhatToMine/hashrate.no API terms (keys, rate limits, commercial use) were not retrieved.

---

## 6. Licensing: can a commercial wrapper bundle these, and can it take its own fee?

### Takeaway
Shipping XMRig (GPL-3.0) as a separate executable launched via exec/CLI/HTTP is generally treated as "mere aggregation". The wrapper can stay proprietary, but must distribute XMRig's license and offer its corresponding source. Under the GPL, a modified XMRig (for example with the donation changed or removed, or redirected to the app's wallet) is permitted if source is offered. Closed miners carry dev fees the wrapper cannot remove, and some prohibit redistribution (Excavator EULA) or modification (SRBMiner). The wrapper can take its own cut in several ways: time-sliced mining to its own wallet (XMRig donate-style), a referral code (unMineable 0.25%), or acting as the pool/custodian (the Kryptex/Salad model).

### Cited Findings
- GPL FAQ, as quoted: mere aggregation means "separate programs, not parts of a single program", and "if one of the programs is covered by the GPL, it has no effect on the other program". Whether it applies depends on the communication mechanism (exec, pipes, sockets versus shared address space) and the semantics. Calling separate GPL binaries via exec with documented CLI options is aggregation. Redistributing the GPL binary still triggers its obligations (source). — [OSI license-discuss 2007](https://lists.opensource.org/pipermail/license-discuss_lists.opensource.org/2007-June/012856.html); [OSI 2001](https://lists.opensource.org/pipermail/license-discuss_lists.opensource.org/2001-January/002711.html); [LWN comment](https://lwn.net/Articles/745253)
- XMRig donation is 1% by default; raising it is a config option, and "disable" requires source changes. — [XMRig GitHub](https://github.com/xmrig/xmrig)
- Excavator is "proprietary software by NiceHash" under an EULA that prohibits redistribution. — [Excavator GitHub](https://github.com/nicehash/excavator)
- SRBMiner: all rights reserved; GPU binaries are proprietary and may be run only as part of an official release; no extraction or reverse engineering without written permission. — [SRBMiner GitHub](https://github.com/doktor83/SRBMiner-Multi)
- unMineable referral: the referrer earns about 0.25% of referred miners' rewards (promotional sources, conflicting). — [gist](https://gist.github.com/svv3085/b4ee22f7f19d7807b261288fe8847541)
- MoneroOcean's server-side code is partly closed. A CCS proposal sought 20 XMR to buy it and release it under MIT (outcome unconfirmed). — [Monero CCS MR 239](https://repo.getmonero.org/monero-project/ccs-proposals/-/merge_requests/239)
- Kryptex and Salad run custodial models: users accrue a platform balance and withdraw in BTC, USDT, gift cards and so on. — [kryptex.com](https://www.kryptex.com/en); [Salad support](https://support.salad.com/faq/jobs/how-does-my-machine-earn-salad-balance)

### Inferences
- **Safest structure:** ship unmodified official XMRig binaries plus the GPL text and a source link (or build from source and host it). Keep the wrapper in a separate process talking over CLI and HTTP. To take a fee, either set `donate-level` higher (that pays XMRig's devs, not you), or fork XMRig and point the donation slice at your own pool or wallet, publishing that fork's source. Many commercial wrappers (for example NiceHash QuickMiner bundling third-party miners) follow a similar pattern.
- For closed GPU miners, the app should **download them from official release URLs at runtime** rather than redistribute them, unless written permission is obtained. Users pay the miner dev fee plus the app's fee.
- A custodial model (app pays users) adds money-transmitter and KYC/AML obligations in many jurisdictions. It is also how NiceHash came to require KYC. A non-custodial model (user's own wallet address) avoids this, but users then face pool minimums.
- This is not legal advice. The GPL "aggregation" boundary is legally untested (per the FSF, ultimately "judges will decide").

### Gaps
- No primary source on the redistribution licences of lolMiner, Rigel, T-Rex or GMiner.
- No case law or official FSF statement specifically on miner wrappers.

---

## 7. Antivirus / Windows Defender flagging and code signing

### Takeaway
Windows Defender and most AV products classify miners as PUA (for example `PUA:Win32/CoinMiner`, `Win32/Uwamson.A!ml`), including NiceHash, Kryptex and even the Monero GUI. Legitimate apps deal with this through user-facing guidance: whitelist specific files and disable PUA blocking for the miner. Kryptex's own docs conflict on folder versus file exclusions. Users are told to distinguish PUA labels from Trojan, Backdoor or Virus labels.

### Cited Findings
- Kryptex says PUA is a category (miners) that "can decrease computer performance". It tells users to turn off PUA blocking in Windows reputation-based protection and add exclusions. — [Kryptex: is Kryptex a virus?](https://kryptex.com/articles/not-a-virus-en). A separate Kryptex article warns attackers hide malware in excluded folders and advises excluding individual files only, which conflicts with the first. — [Kryptex: suspicious files](https://api.kryptex.com/articles/suspicious-files-en)
- `Win32/Uwamson.A!ml` is often flagged on legitimate miners such as NiceHash. — [GridinSoft](https://blog.gridinsoft.com/program-win32-uwamson-aml/)
- Defender raised `PUA:Win32/CoinMiner` on a verified Electrum download (false positive). — [Microsoft Answers](https://answers.microsoft.com/en-us/windows/forum/all/windows-defender-puawin32coinminer-active-false/e2f3213a-a8f6-4a0d-b357-9097bff65548)
- Defender ATP has detected crypto-miners as a class since 2018. — [Borncity](https://borncity.com/blog/2018/03/16/windows-defender-atp-erkennt-krypto-miner/)
- XMRig ships WinRing0 (a kernel driver for MSR tweaks) in its Windows package. — [XMRig GitHub](https://github.com/xmrig/xmrig)

### Inferences
- Code signing (OV/EV) improves SmartScreen reputation for the wrapper's own installer. It is unlikely to stop PUA classification of the miner binaries, because the detection is behavior/category-based. Expect onboarding friction: an exclusion prompt or Defender "allow" step. Clear consent and disclosure UI ("this app mines cryptocurrency") is essential to avoid being classed as malware.
- The bundled WinRing0 driver is a known-vulnerable driver blocked by Microsoft's vulnerable-driver blocklist on some systems. Shipping without it (accepting about 10–15% lower RandomX hashrate) may reduce AV and driver issues. This is inference; not verified in this research.

### Gaps
- No source found on how NiceHash or Salad specifically handle Defender (their support articles were not retrieved), or on code-signing practices and Microsoft submission/allow-listing processes for miner vendors.

---

## 8. Legal and tax issues; profit-switching approaches

### Takeaway
Mining is prohibited in China, and in Kuwait, Nepal and Angola (criminalized). Russia bans it regionally, including Moscow region from 15 Aug 2026 through 2032, with pool participation explicitly covered, and other regions until 2031. Uzbekistan restricts home mining. In the US, mined coins are ordinary income at fair market value when received, plus self-employment tax if the activity is a business. Selling them later triggers capital gains. Profit-switching is done either by the marketplace (NiceHash pays per algorithm) or client-side by benchmarking plus a revenue feed (Kryptex app, MoneroOcean's XMRig fork).

### Cited Findings
- **Russia:** Resolution No. 936 (signed 25 Jul 2026) bans mining, including participation in a mining pool, in Moscow, Moscow Region and parts of Kursk from 15 Aug 2026 to 31 Dec 2032. A December 2024 order covers ten regions until 15 Mar 2031. Buryatia and Zabaykalsky have a year-round ban from 1 Apr 2026. A bill would add fines up to 2.5M rubles and up to 5 years' prison for illegal mining. — [Cointelegraph](https://cointelegraph.com/news/russia-expands-crypto-mining-ban-moscow-2032); [The Block](https://theblock.co/post/410345/russian-decree-bans-crypto-mining-across-moscow-region-through-2032); [Interfax 3 Aug 2026](https://interfax.com/newsroom/top-stories/118586/); [Whale Alert](https://whale-alert.io/stories/acba01147acc45/Russia-backs-proposal-to-ban-crypto-mining-in-Moscow-and-other-regions-while-lawmakers-advance-criminal-penalties-for-illegal-mining)
- **Kuwait:** the Ministry of Interior declared mining illegal on 22 April. The year is not stated in the snippet; likely 2025, unverified. — [Unlock-bc](https://www.unlock-bc.com/en/kuwait-bans-bitcoin-mining-citing-power-strain-and-legal-violations)
- **China:** mining is prohibited, with provincial bans (Inner Mongolia, Xinjiang, Qinghai) since 2021. An Aug 2026 Asia guide lists China and Nepal as prohibited. Kazakhstan and Uzbekistan license mining, and Uzbekistan restricts home mining. Kosovo and Angola have restrictions; Angola criminalizes mining. — [Forkast](https://forkast.news/china-xinjiang-qinghai-regions-ban-crypto-mining/) and synthesis in search results (Asia guide URL not captured)
- **US tax:** under IRS Notice 2014-21, mining rewards are gross income at FMV on receipt, and self-employment tax applies if mining is a trade or business. Business miners report on Schedule C (and can deduct electricity and hardware); hobby miners report on Schedule 1 (no deductions). A later sale creates a capital gain or loss from that basis. There is no withholding, so quarterly estimated payments may be needed. — [Mondaq](https://www.mondaq.com/unitedstates/fin-tech/1154806/taxation-of-crypto-mining); [Green Leaf Trust](https://greenleaftrust.com/missives/taxation-of-crypto-mining-services); [KoinX](https://www.koinx.com/stats/us-crypto-mining-tax-statistics); [FileTax](https://filetax.com/tax-situation/bought-or-sold-crypto/mining-taxes); [CoinTracker](https://cointracker.io/blog/how-is-cryptocurrency-mining-taxed)
- **KYC:** NiceHash requires KYC (see section 2), while Kryptex advertises no KYC. — [Kryptex article](https://www.kryptex.com/en/articles/nicehash-alternative-en)
- **Profit-switching:** the Kryptex app "detects hardware, selects the most profitable algorithm and switches coins in real time". — [kryptex.com](https://www.kryptex.com/en). MoneroOcean's XMRig fork does algorithm benchmarking and switching against the pool. — [Gentoo xmrig-mo](https://gpo.zugaina.org/net-misc/xmrig-mo); [pool research](https://forge.lthn.ai/Snider/Mining/src/tag/v0.0.10/docs/pool-research.md). Excavator's API supports switching algorithms on the fly. — [Excavator](https://github.com/nicehash/excavator). unMineable converts whatever is mined into the chosen payout coin (80+ assets). — [unmineable.com](https://www.unmineable.com/)

### Inferences
- A consumer app must geofence or at least warn users in banned jurisdictions (China, Kuwait, Russian banned regions, and others). Russia's explicit inclusion of "participation in a mining pool" covers exactly what a consumer app does.
- The app should export an earnings ledger (timestamp, coin, amount, USD FMV at receipt) for users' tax reporting.
- Practical switching options for a wrapper: (a) delegate to NiceHash (simple, BTC payout, but 2% fee, KYC and Windows-centric); (b) delegate to MoneroOcean or unMineable (non-custodial, pays in the chosen coin); (c) self-manage using benchmark results × WhatToMine/hashrate.no revenue per hash, with hysteresis to avoid thrashing. Under (c), new-coin spikes (PRL) drive most of the 2026 upside, and also the risk.

### Gaps
- No authoritative 2026 global legality map was retrieved. EU/UK tax treatment, non-US KYC rules for small payouts, and whether micro-payouts trigger money-transmitter licensing for a custodial app were not researched.
- NiceHash's "60-day rule" details are unknown.
