# CPU/GPU Mining Opportunities and Payout Routes for Otium (as of 16 October 2026)

Context: Otium already runs XMRig to MoneroOcean (operator wallet, per-user worker names) and SRBMiner-Multi with GPU profit switching (KawPow, Autolykos2, etchash...). Research date is 2026-10-16. WhatToMine pages fetched on 2026-10-16 mostly carry no explicit timestamp (only the CPU page did: 2026-10-16 00:54 UTC). Treat all WhatToMine GPU numbers as "live snapshot fetched 2026-10-16, $0.10/kWh".

## Pearl (PRL): profitability, miners, pools, the consumer-GPU upgrade, liquidity

### Takeaway
Pearl is by far the most profitable thing a consumer NVIDIA GPU can mine on 16 Oct 2026: about $9.4/day profit on an RTX 4090/5090, $2.8/day on an RTX 3080, $2.9/day on an RTX 4070 and $1.1/day on an RTX 3060. That is roughly 5-30x the legacy algorithms Otium switches between today. SRBMiner-Multi already supports it (`pearlhash`), so integration cost is low. The risks are serious, though. The developers have said an upcoming upgrade will make consumer GPUs "pretty much useless" for Pearl, and no date has been given. Spot liquidity is concentrated on one low-trust exchange (SafeTrade), and the price has swung from about $0.14 to $1.40-1.90 within three months.

### Cited Findings
**Profitability (WhatToMine GPU list for Pearl, fetched 2026-10-16, $0.10/kWh, "pool reported hashrate"):**
- RTX 4090: 310 TH/s @ 400 W, $10.44/day revenue, $9.48/day profit; RTX 5090: 300 TH/s @ 420 W, $10.10 rev / $9.09 profit; RTX 5080: 195 TH/s @ 250 W, $6.56 / $5.96 — [WhatToMine Pearl GPUs](https://whattomine.com/coins/469-prl-pearl/gpus)
- RTX 4070: 97 TH/s @ 150 W, $3.27 / $2.91; RTX 4070 Super $4.11 / $3.63; RTX 3080: 100 TH/s @ 240 W, $3.37 / $2.79; RTX 3090 $4.38 / $3.61; RTX 3060: 41 TH/s @ 100 W, $1.38 / $1.14; RTX 3060 Ti $1.99 / $1.65; RTX 5060 Ti $3.03 / $2.72; RTX 4060 $1.75 / $1.51; RTX 2060 $1.25 / $1.01 — [WhatToMine Pearl GPUs](https://whattomine.com/coins/469-prl-pearl/gpus)
- AMD (marked * = likely estimated, unexplained on page): RX 9070 XT 70 TH/s @ 340 W $2.36 / $1.54; RX 9070 $1.99 / $1.39; RX 7900 XTX 40 TH/s $1.35 / $0.79; RX 9060 XT $1.18 / $0.70. GTX 16xx cards (no tensor cores) earn ~$0.02/day, which is unprofitable — [WhatToMine Pearl GPUs](https://whattomine.com/coins/469-prl-pearl/gpus)
- Pearl network stats (WhatToMine, 2026-10-16): network hashrate 58,079 PH/s, block reward ~2,252 PRL, block time 2m55s, difficulty ~36.1M, rate 0.00002294 BTC/PRL on SafeTrade (≈$1.89 at the page's BTC price of $82,376), SafeTrade 24h volume 72.186 BTC — [WhatToMine Pearl](https://whattomine.com/coins/469-prl-pearl)
- hashrate.no (fetched 2026-10-16, undated): implied price ~$1.91/PRL, network 55.34 EH/s, revenue $0.0365 per TH/s per day (−12.69%), 24h volume ~$5.69M — [hashrate.no PRL](https://hashrate.no/coins/PRL)
- Trend: Tom's Hardware reported RTX 5090 daily revenue had fallen to $17.19 (hashrate.no), about half of the ~$33.80 seen after the April launch, as the mining rush raised difficulty. Article date not visible, but the content places it around May-June 2026. Today's WhatToMine figure for the 5090 is ~$10.10/day — [Tom's Hardware](https://www.tomshardware.com/tech-industry/cryptomining/new-ai-compute-cryptocurrency-pearl-sparks-a-gpu-mining-rush-but-profitability-is-sliding); [HydraX guide](https://hydrax.gg/blog/how-to-mine-pearl-prl.html)
- An academic study (arXiv, June 2026) found Pearl mining unprofitable on every GPU tier at a PRL price of $0.21 (−54% to −72% ROI). That conclusion depends on price and is stale at today's ~$1.4-1.9 — [arXiv 2606.04819](https://arxiv.org/html/2606.04819v1)
- An Aug 6, 2026 "rank-penalty softfork" cut effective hashrate by 50-87.5% for miners above rank 128 (mechanics not fully described). This kind of protocol change can hit per-device yield directly — [SimeonOnSecurity, 2026-09-22](https://simeononsecurity.com/articles/pearl-prl-mining-guide-2026/)

**Miners:**
- SRBMiner-Multi supports `pearlhash` (dev fee for pearlhash not stated in release notes). Recent releases: 3.7.6 (14 Oct) pearlhash improvements for NVIDIA 3000/4000; 3.7.4 (9 Oct) pearlhash for NVIDIA 2000/5000; 3.7.3 (7 Oct) made new, "significantly more efficient" pearlhash kernels default on SM86/SM89/SM120 (recommends 810 MHz mem clock, re-tune `--gpu-plimit`/`--gpu-coffset`; errors E2001/E1027 = core offset too high) and removed QUIC; 3.6.9 (21 Sep) "huge" pearlhash efficiency gains — [SRBMiner-Multi releases](https://github.com/doktor83/SRBMiner-Multi/releases)
- Other Pearl miners: Kryptex KRig (0% fee for Pearl, CUDA for NVIDIA / ROCm for AMD, RDNA2-RDNA4, RTX 40/50, SSL-only) — [Kryptex KRig](https://www.kryptex.com/krig); BzMiner, lpminer (LuckyPool) — [Kryptex Pearl pools article (June 2026)](https://pool.kryptex.com/articles/pearl-mining-pools-en); Tw-pearl-miner v3.3.0, WildRig 0.49.8, official vLLM miner (`pearl-gateway` + `vllm-miner`, needs synced `pearld`, CUDA, Python 3.12, Linux) — [SimeonOnSecurity](https://simeononsecurity.com/articles/pearl-prl-mining-guide-2026/); ssr-miner (NVIDIA, HiveOS packages, Pearl + Quantus) — [ssr-miner GitHub](https://github.com/ssr-miner/ssr-miner)
- AMD support is contested. One guide says AMD and Intel cannot mine Pearl — [SimeonOnSecurity](https://simeononsecurity.com/articles/pearl-prl-mining-guide-2026/). Kryptex says KRig and SRBMiner support AMD RDNA2+ — [Kryptex](https://www.kryptex.com/krig), and WhatToMine lists AMD estimates.

**Pools (operator attribution):**
- Kryptex Pool: 1% flat fee (PROP or SOLO), payout in PRL **or auto-exchange to BTC/USDT/ETH/LTC** (the only Pearl pool in Kryptex's table with auto-exchange), no registration needed for PRL-wallet mining, `WALLET.WORKER` naming, stratum `prl.kryptex.network:7048` (SSL 8048, eu/us regional hosts), ready configs for SRBMiner-MULTI/BzMiner, 1 PRL minimum payout. For BTC auto-exchange the login is the Kryptex username — [Kryptex Pearl pools (June 2026)](https://pool.kryptex.com/articles/pearl-mining-pools-en)
- Other pools: HeroMiners (1%), PearlHash (1-5%), PearlFortune (5%), LuckyPool, K1Pool, Coin-miners — [Kryptex comparison](https://pool.kryptex.com/articles/pearl-mining-pools-en). LuckyPool `pearl.luckypool.io` ports 3360/3361/3362 by hashrate tier, `solo:` prefix, optional Nockchain merge mining via `+NOCK_ADDRESS` — [SimeonOnSecurity](https://simeononsecurity.com/articles/pearl-prl-mining-guide-2026/)
- Pearl's official pool reportedly accepts only datacenter H100/H200 — [search summary of datawallet/hashrateindex/alphamine guides](https://www.datawallet.com/crypto/pearl-explained)

**The consumer-GPU-ending upgrade:**
- SRBMiner-Multi 3.6.8 release notes (19 Sep): "Soon, Pearl will be getting an upgrade that will make consumer-grade GPUs pretty much useless" — [SRBMiner-Multi releases](https://github.com/doktor83/SRBMiner-Multi/releases)
- In mid-September Pearl announced a new floating-point PoUW solution that would "soon be deployed in production". No date was given — [KuCoin News, 2026-10-12](https://www.kucoin.com/news/flash/prl-surges-300-as-pearl-gains-attention-as-ai-bitcoin)
- Secondary sources describe it as an FP8 upgrade (from the current INT8) so mining aligns with production AI inference — [Pearl INT whitepaper / search summary](https://pearlresearch.ai/research/int-whitepaper); [datawallet](https://www.datawallet.com/crypto/pearl-explained). The FP8 detail is not confirmed in the SRBMiner notes.
- Criticism: a study claims the network (~320k RTX 3090-equivalents, ~112 MW) does no useful AI work and has pushed Vast.ai budget GPU rental prices up 38% — [Tom's Hardware](https://www.tomshardware.com/tech-industry/artificial-intelligence/ai-cryptomining-networks-320-000-rtx-3090-class-gpus-allegedly-burn-112-megawatts-of-power-on-zero-useful-ai-computation-pearls-gpus-are-doing-random-matrix-math-study-claims)

**Liquidity / price:**
- KuCoin News (2026-10-12): PRL rose ~300% in a month from $0.33 to $1.40, peaked at $1.70, and had earlier fallen to $0.23. Circulating market cap ~$470M at $1.40. Buy venues named: SafeTrade (spot), Aster (perpetuals launched Oct 10), pearl-otc (OTC). KuCoin itself does not list Pearl spot — [KuCoin News](https://www.kucoin.com/news/flash/prl-surges-300-as-pearl-gains-attention-as-ai-bitcoin)
- As of 2026-09-22, SafeTrade held 95% of reported volume ($738k of ~$775k/24h on PRL/USDT), reported order-book depth was $0.00, it had no CoinGecko trust score, and PRL's all-time low was $0.1399 (July 23) — [SimeonOnSecurity](https://simeononsecurity.com/articles/pearl-prl-mining-guide-2026/)
- **Ticker collision:** "PRL" on KuCoin/MEXC/Gate/Coinbase is *Perle*, a different token (~$0.18-0.27). Do not use it for Pearl pricing or conversion — [Coinranking Perle](https://coinranking.com/coin/urULyL_US+perle-prl/exchanges); [Altrady](https://www.altrady.com/cryptocurrencies/prl-price-usd)

### Inferences
- Pearl is a strong short-term opportunity. SRBMiner already handles it, so Otium can add `pearlhash` to its switching table. Kryptex with BTC/USDT auto-exchange removes the SafeTrade liquidity problem for the operator, since Kryptex carries the conversion risk.
- Treat Pearl as a time-limited, kill-switch algorithm. Gate it behind a remote config flag and fall back automatically when the floating-point/FP8 upgrade lands, because consumer cards may drop to near-zero overnight.
- Device fit: NVIDIA RTX 20-series and newer benefit most (tensor cores). AMD gains are smaller (RX 9070 XT ~$1.5/day). GTX 16xx and older should not mine Pearl.
- Price risk is extreme: a $1.40-1.90 price today against $0.14 in July. Credit users in USD at the moment of auto-exchange, not on PRL accrual.

### Gaps
- No official date for the Pearl floating-point/FP8 upgrade was found, and the SRBMiner notes do not say how it will be activated (hard fork height?).
- SRBMiner's pearlhash dev fee is not stated in the release notes I saw.
- Kryptex Pearl per-worker API endpoints were not verified (see the payout section).
- The PRL price differs across sources ($1.40 on Oct 12 per KuCoin, ~$1.89-1.91 implied by WhatToMine/hashrate.no on Oct 16). Volume figures on SafeTrade cannot be independently verified.

## Other GPU coins: Quantus, ProgPow variants, Epic, Zano, Kaspa, RVN, Ergo, Alephium, Nexa, Dynex

### Takeaway
After Pearl, the only clearly profitable new GPU algorithm in October 2026 is Quantus (QPoW/Poseidon2, mainnet Sep 9, 2026): about $5.2/day profit on an RTX 4090. It is supported by SRBMiner (`quantus`, 2% fee, NVIDIA/AMD/Intel Arc), but liquidity is very thin. Every legacy algorithm Otium currently switches between (KawPow/RVN, Autolykos/ERG, Etchash/ETC) is unprofitable at $0.10/kWh on an RTX 4090. Kaspa (kHeavyHash) and Alephium (Blake3) show $0.00/day revenue because ASICs dominate them.

### Cited Findings
- WhatToMine RTX 4090 ranking (fetched 2026-10-16, $0.10/kWh): 1) Pearl $10.38 rev / $9.42 profit; 2) Quantus (QPoW) 880 MH/s @ 270 W $5.83 / $5.18; 3) EPIC-ProgPow 64 MH/s @ 330 W $2.03 / $1.24; 4) Qubitcoin (QTC, Qhash) 5.1 GH/s $1.94 / $1.22; 5) Decred Blake3DCR $1.29 / $0.62; 6) Parallax (LAX, Xhash) $1.21 / $0.56; Evrmore EvrProgPow $1.14 / $0.35; Conflux Octopus $0.98 / $0.31; Tari-Cuckaroo29 (XTM) $0.73 / $0.11; Zano ProgPowZ $0.87 / $0.08; Neoxa KawPow $0.87 / $0.07 — [WhatToMine RTX 4090](https://whattomine.com/gpus/79-nvidia-geforce-rtx-4090)
- Negative on the RTX 4090 (same page): Ergo −$0.11 ($0.37 rev), Dynex −$0.43 ($0.02 rev), Ravencoin −$0.45 ($0.34 rev), Nexa −$0.52, Kaspa −$0.58 ($0.00 rev), ETC −$0.56 ($0.06 rev), Alephium −$0.84 ($0.00 rev) — [WhatToMine RTX 4090](https://whattomine.com/gpus/79-nvidia-geforce-rtx-4090)
- Low-end card (GTX 1050 Ti): only EPIC-ProgPow (+$0.01) and Zclassic (+$0.01) are profitable. Everything else is negative — [WhatToMine GTX 1050 Ti](https://whattomine.com/gpus/31-nvidia-geforce-rtx-3060) (note: this URL returned the 1050 Ti page, not the RTX 3060)
- Quantus: Substrate chain, QPoW = Poseidon2 over the Goldilocks field, 12 s blocks, 21M max supply, Dilithium (ML-DSA) accounts — [Suprnova Quantus](https://quantus.suprnova.cc/). Mainnet launched Sep 9, 2026. Per-GPU estimates (updated 2026-09-16): RTX 4090 ~1120 MH/s; RTX 4080 Super ~765 MH/s ~$3/day; RTX 4070 Ti ~600 MH/s ~$3/day; RTX 3070 218-225 MH/s @ 70-80 W ~$1.0-1.2/day; RTX 3060 ~190 MH/s; RTX 5090 1.2-1.5 GH/s ~$6/day; a 7950X3D CPU gets ~20 MH/s ~$0.09/day — [bytwork, 2026-09-16](https://bytwork.com/en/articles/quantus)
- Quantus miners: lpminerquantus (0%), Quanpool miner (5%), AriaMiner (3%), official quantus-miner (0%, much slower), SRBMiner-Multi (added in 3.6.4 at 2.5%, lowered to 2% in 3.6.7; AMD/Intel Arc/Ellesmere support added in 3.6.8-3.7.6). Pools: Kryptex (auto-exchange BTC/USDT/LTC), Quanpool, LuckyPool, Suprnova, NurseryPool. Only exchange at launch was SafeTrade QUANTUS/USDT, with $16-35k volume and thin books; deposits under 0.1 QTC are lost — [bytwork](https://bytwork.com/en/articles/quantus); [SRBMiner releases](https://github.com/doktor83/SRBMiner-Multi/releases); [NurseryPool](https://quan.nurserypool.com/help.html)
- SRBMiner 3.7.3 removed QUIC (some Quantus pools used QUIC) — [SRBMiner releases](https://github.com/doktor83/SRBMiner-Multi/releases)
- SRBMiner also added a `noid` algorithm (3.6.3+, 2% fee, NVIDIA + AMD, SM80 in 3.7.5). The coin behind it was not identified — [SRBMiner releases](https://github.com/doktor83/SRBMiner-Multi/releases)
- Ticker confusion: bytwork calls Quantus "QTC" while WhatToMine lists "Qubitcoin (QTC, Qhash)" as a separate coin and Quantus as "QUANTUS"; NurseryPool uses "QUAN" — [WhatToMine RTX 4090](https://whattomine.com/gpus/79-nvidia-geforce-rtx-4090); [NurseryPool](https://quan.nurserypool.com/help.html)
- Kaspa is described as a top GPU coin in generic "best coins 2026" listicles, but those pages are stale (one titled "October 2026" was 254 days old) — [cryptonews](https://cryptonews.com/cryptocurrency/best-cryptos-to-mine/). The live WhatToMine figure is $0.00/day for a 4090.

### Inferences
- Add `quantus` to Otium's SRBMiner switching table, preferably via Kryptex for BTC/USDT auto-exchange, given QTC's SafeTrade-only thin liquidity. Expect its profitability to fall as more miners arrive, as Pearl's did.
- Add EPIC-ProgPow (and possibly Evrmore/Zano/Neoxa) as fallback algorithms once Pearl ends. They pay $1-2/day revenue on a 4090 and beat KawPow/RVN, Autolykos and Etchash, which are now negative.
- Drop Kaspa, Alephium and Nexa as GPU targets. Ergo/RVN/ETC switching makes no sense at $0.10/kWh unless the user's power is free (revenue only $0.06-0.37/day per 4090).
- Otium's profit switcher should use a live source (WhatToMine/hashrate.no JSON) rather than static tables, since rankings changed drastically in six months.

### Gaps
- No October 2026 $/day data for Qubitcoin, Parallax, Clore, Dynex beyond the 4090 page. Zephyr has no GPU algorithm (it is RandomX).
- I did not fetch per-coin RTX 3060/3080/4070 lists for non-Pearl coins. Pearl figures for those cards are above.
- The identity of the coin behind `noid` is unknown.

## CPU mining: Monero vs Qubic, Tari merge mining, Zephyr, Epic, others

### Takeaway
On 16 Oct 2026, RandomX alternatives are within about 15% of XMR. EPIC-RandomX pays $0.99/day and XDAG $0.96, against XMR's $0.87 per 21 kH/s CPU, so MoneroOcean's XMR-denominated switching is still reasonable. Tari merge mining adds only about $0.02/day per 21 kH/s (~2%), while Tari-RandomX standalone ($0.77) pays less than XMR. Qubic announced in April 2026 that it is phasing out Monero mining in favour of DOGE on ASICs, so it is no longer a clear CPU payout route.

### Cited Findings
- WhatToMine CPU (snapshot 2026-10-16 00:54 UTC, 140 W, $0.10/kWh, 21 kH/s RandomX): EPIC-RandomX $0.99 rev / $0.65 profit; Dagger XDAG $0.96 / $0.62; Monero $0.87 / $0.53; NiceHash-RandomX $0.81 / $0.47; Tari-RandomX (XTM, standalone) $0.77 / $0.43; Etica $0.73 / $0.39; Zephyr $0.51 / $0.18; Xelis (16 kH/s) $0.43 / $0.09; QRL $0.37 / $0.04; Raptoreum −$0.21; Verus −$0.24; **Tari-Merged (MoneroRandomX) $0.02 revenue** — [WhatToMine CPU](https://whattomine.com/cpu)
- NiceHash-RandomX ($0.81) pays ~7% less than mining XMR directly ($0.87) on the same snapshot — [WhatToMine CPU](https://whattomine.com/cpu)
- Tari reward split is contested: a 50/50 split between RandomX merge mining and SHA3x — [Tari tokenomics](https://tari.substack.com/p/tari-tokenomics) — versus a four-algorithm split with a quarter each, including RandomX-Merge and Cuckaroo29 (GPU) — [Kryptex Tari article](https://pool.kryptex.com/articles/tari-en). WhatToMine lists separate Tari-RandomX, Tari-Merged and Tari-Cuckaroo29 entries, which fits the multi-algorithm model.
- P2Pool v4.6 added Tari merge-mining support, including donation tasks for nodes not merge-mining Tari themselves, and has a separate Tari gRPC command-line option — [Monero Observer](https://monero.observer/schernykh-releases-p2pool-v4.6-tari-merge-mining-donations/); [P2Pool GitHub](https://github.com/SChernykh/p2pool). Community Docker stacks bundle Monero node + P2Pool + Minotari node + XMRig-proxy. Enabling Tari currently halts mining until the Tari node syncs (open issue, early Oct 2026) — [p2pool-starter-stack issue #3094](https://github.com/p2pool-starter-stack/pithead/issues/3094)
- MoneroOcean: listed at 0% fee, with the pool earning margin from switching/conversion (Coin Bureau, data dated Jan 1, 2026) — [Coin Bureau](https://coinbureau.com/mining/best-monero-pools); 0-0.6% per Monerica — [Monerica](https://blog.monerica.com/articles/best-monero-mining-pool). MoneroOcean's mo-miner lists a TARI c29 (Cuckaroo) GPU algo — [MoneroOcean mo-miner](https://github.com/MoneroOcean/mo-miner). Note: "moneroocean.io/fees", which shows a per-KH/s "deposit period" price table, does not look like the genuine pool (the real domain is moneroocean.stream). Possible impostor; do not cite it — [moneroocean.io/fees](https://moneroocean.io/fees)
- Qubic: its own epoch 172 report (2025) said Qubic mined ~517 XMR + 6M XTM per epoch at a peak of 2.77 GH/s (~50% of Monero hashrate), and claimed per-CPU $3.13/day (Qubic) vs $0.64/day (Monero) and ~$1.65/day (Tari merge) — [Qubic Epoch 172](https://qubic.org/blog-detail/epoch-172-recap-the-monero-experiment). These are self-reported, promotional and dated 2025.
- An independent academic analysis found Qubic averaged a 23-34% hashrate share during its campaign, never sustained a majority, and earned below honest-mining revenue — [arXiv 2512.01437](https://arxiv.org/pdf/2512.01437v2)
- April 1, 2026 press release: Qubic now mines DOGE on separate ASIC hardware, sells DOGE to buy back QUBIC for miners, and **Monero mining phases out as DOGE scales** — [The Block press release](https://www.theblock.co/press-releases/396050/); [Decrypt](https://decrypt.co/363018/qubic-the-network-that-captured-51-of-monero-is-now-mining-dogecoin-on-its-ai-compute-infrastructure-live?amp=1)
- Home CPU XMR earnings: ~$0.15/day net for a Ryzen 9 7950X at $0.10/kWh and XMR ~$355 (April 2026, older data) — [miningreturns](https://miningreturns.com/news/monero-cpu-mining-still-profitable-2026)

### Inferences
- Keep MoneroOcean as the CPU default. EPIC-RandomX/XDAG pay ~10-14% more than XMR on paper, but liquidity and pool/API support for those are unverified, and MoneroOcean's switching may already capture some of that gain.
- Tari merge mining is not worth operating P2Pool + Minotari infrastructure for (~2% uplift). Switching to standalone Tari-RandomX would *lose* revenue against XMR today.
- Qubic is not recommended. Its CPU payouts are paid in QUBIC tokens, its XMR mining is being phased out, its profitability numbers are self-reported, and its custom client (Qli) is a black box.

### Gaps
- Whether MoneroOcean currently switches into EPIC/XDAG/Tari-RandomX and pays the user in XMR was not verified.
- No current (Oct 2026) Qubic CPU earnings data was found.
- No WhatToMine CPU data was found for Zano (GPU ProgPowZ only), and Qubic is not listed.

## Apple Silicon mining

### Takeaway
No credible Metal-based miner that beats XMRig on M1-M4 for profitable coins was found. XMRig (RandomX on CPU) remains the practical option, at about 6.8 kH/s for an M4 Pro.

### Cited Findings
- Kryptex lists the Apple M4 Pro at 6.82 kH/s RandomX @ 46 W, and its estimates are very small, with some coins at a loss — [Kryptex M4 Pro](https://pool.kryptex.com/device/cpu/apple/apple-m4-pro)
- The only "Metal miner" found was a paid Gumroad "MacMetal Miner" for SHA256d solo Bitcoin lottery mining, whose performance claims are unverified marketing. SHA256 on GPUs is uncompetitive with ASICs — [Gumroad listing](https://winnertakeall.gumroad.com/l/bitcoin)
- MoneroOcean's mo-miner is an open-source Node.js/SYCL miner based on XMRig hashing sources, with GPU algos such as Tari c29. Apple/Metal support is not confirmed — [mo-miner](https://github.com/MoneroOcean/mo-miner)
- Pearl and Quantus miners are CUDA/ROCm/OpenCL/Vulkan, Linux/Windows. None target Metal — [SimeonOnSecurity](https://simeononsecurity.com/articles/pearl-prl-mining-guide-2026/); [bytwork](https://bytwork.com/en/articles/quantus)

### Inferences
- At WhatToMine's XMR rate (~$0.87/day per 21 kH/s), an M4 Pro at 6.8 kH/s grosses roughly $0.28/day, which is a small amount. Keep XMRig on macOS, and don't spend engineering effort on Metal.

### Gaps
- No benchmarks for an M-series GPU running Quantus (Vulkan via MoltenVK?) were found.

## Payout routes, pools and per-worker APIs (NiceHash, Kryptex, unMineable, 2Miners, HeroMiners, auto-exchange, USD conversion)

### Takeaway
Kryptex Pool is the best operator fit for the new GPU algorithms: Pearl and Quantus at 1%, auto-exchange to BTC/USDT, `wallet.worker` naming and ready SRBMiner configs. NiceHash gives a clean BTC payout and a documented per-rig API, but its RandomX price is ~7% below direct XMR, and its Excavator miner cannot be redistributed. Per-worker hashrate-history APIs for unMineable and HeroMiners could not be verified from official docs. The 2Miners API is documented at apidoc.2miners.com.

### Cited Findings
- Kryptex: 1% fee, auto-exchange to BTC/USDT/ETH/LTC for Pearl (and BTC/USDT/LTC for Quantus), username-based login for auto-exchange, Telegram monitoring bot — [Kryptex Pearl pools](https://pool.kryptex.com/articles/pearl-mining-pools-en); [bytwork](https://bytwork.com/en/articles/quantus)
- NiceHash API: needs an API key + Organization ID (shown in the API Keys menu), with the "mining rig" permission for rig management. Client libraries expose per-rig accepted/rejected hashrate and profitability — [ha_nicehash](https://github.com/qcasey/ha_nicehash); [pynicehash](https://pypi.org/project/pynicehash)
- Excavator is proprietary with a special EULA that forbids redistribution. Integrators (e.g. HiveOS) download it from NiceHash's official release rather than bundling it. Its local API is separate from the cloud account API — [Excavator repo/notes](https://github.com/MiningSoftware/excavator); [HiveOS forum](https://forum.hiveos.farm/t/excavator-1-5-11-a-with-algo-autoswitch/6429)
- NiceHash-RandomX revenue $0.81 vs XMR $0.87 (2026-10-16) — [WhatToMine CPU](https://whattomine.com/cpu)
- unMineable: the referral code reportedly cuts the fee from 1.00% to 0.75% (community blogs; an older forum post says 0.25% off) — [publish0x](https://www.publish0x.com/the-tech-behind-crypto/mining-unmineable-coins-xyeqkyq); [hardforum](https://hardforum.com/threads/unmineable.2015306/). The official Monitor app shows active workers, payouts and referrals per account — [App Store](https://apps.apple.com/app/id6444838563). api.unminable.com returned 404, and no official API docs were found.
- 2Miners: official API docs at apidoc.2miners.com (JSON; account/miners/payments/stats). Community apps show per-worker history — [twominers Go wrapper](https://github.com/stdfox/twominers)
- HeroMiners: ~0.9% PPLNS per aggregator, framework reportedly shared with 2Miners. No official API docs found — [miningboard](https://miningboard.com/es/pools/hero-miners)

### Inferences
- Recommended architecture: route GPU Pearl/Quantus to Kryptex with BTC/USDT auto-exchange, keep CPU on MoneroOcean, and do user attribution from Otium's own client-reported hashrate (SRBMiner local API on port 21550 / XMRig HTTP API) reconciled against the pool's per-worker totals. Don't rely solely on pool per-worker history APIs, which are inconsistently documented.
- USD conversion for PayPal: auto-exchanged BTC/USDT goes to a KYC'd major exchange (operator account) and then to fiat. Avoid holding PRL/QTC on SafeTrade (low trust, no depth).

### Gaps
- Kryptex per-worker hashrate-history API endpoints, rate limits and terms for operators were not verified.
- NiceHash organization-account KYC requirements and the current EULA text were not retrieved.
- unMineable and HeroMiners per-worker APIs, K1Pool, and MoneroOcean's own API were not confirmed in this session.
- Exchange-specific KYC/fiat details for converting BTC/USDT to USD for PayPal funding were not researched.
