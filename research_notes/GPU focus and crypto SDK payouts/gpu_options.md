# GPU options for Otium: what to mine non-custodially (state as of 17 Oct 2026)

Method note: these figures come from page fetches on 2026-10-17 of WhatToMine per-GPU pages (requested at `cost=0.15`), hashrate.no GPU pages ($0.10/kWh; I recomputed net at $0.15), Kryptex Pool device pages ("last updated 17/10/2026", annual figures ÷ 365) and the live Kryptex API. Pool, miner and unMineable pages were fetched the same day. Nothing was signed up for, downloaded or run.

How net is calculated: net = gross − W × 24 h × $0.15/kWh. Each source reports a different wattage. hashrate.no shows tuned and often lower figures, and AMD readings are chip-only, so they understate wall power. Where sources disagree I give a range.

---

## 1. Top-paying algorithms per card (gross $/day; net at $0.15/kWh)

**WhatToMine, $0.15/kWh, fetched 2026-10-17**

| Card | #1 | #2 | Next best |
|---|---|---|---|
| RTX 3060 | Pearl 41 TH/s, 100 W: $1.32 / $0.96 | Quantus 155 MH/s, 70 W: $0.97 / $0.72 | EPIC $0.62 / $0.12 |
| RTX 3070 | Pearl 68 TH/s, 150 W: $2.18 / $1.64 | Quantus 255 MH/s, 110 W: $1.59 / $1.20 | Keryx $1.33 / $0.87; Zclassic $1.18 / $0.53 |
| RTX 3080 | Pearl 100 TH/s, 240 W: $3.24 / $2.38 | Quantus 380 MH/s, 160 W: $2.37 / $1.80 | Zclassic $1.26 / $0.36; EPIC $1.12 / $0.22; Qubitcoin $0.72 / $0.22 |
| RTX 4060 | Pearl 52 TH/s, 100 W: $1.67 / $1.31 | Quantus 210 MH/s, 80 W: $1.31 / $1.02 | Keryx $1.13 / $0.70 |
| RTX 4070 | Pearl 97 TH/s, 150 W: $3.11 / $2.57 | Quantus 410 MH/s, 140 W: $2.56 / $2.05 | Keryx $1.87 / $1.18; Qubitcoin $0.81 / $0.42 |
| RTX 4090 | Pearl 310 TH/s, 400 W: $9.95 / $8.51 | Quantus 880 MH/s, 270 W: $5.49 / $4.52 | Qubitcoin $2.24 / $1.16; Parallax $1.69 / $0.72; Tari C29 $1.62 / $0.68 |
| RX 6600 | Qubitcoin 700 MH/s, 50 W: $0.31 / $0.13 | Tari C29 3.5 h/s, 60 W: $0.34 / $0.12 | Karlsen $0.28 / $0.06. Quantus and Pearl are not listed. |
| RX 6700 XT | Zclassic 30 h/s, 110 W: $0.60 / $0.21 | Qubitcoin 930 MH/s, 70 W: $0.41 / $0.16 | Tari C29 $0.49 / $0.09; EPIC $0.55 / $0.05. Quantus and Pearl are not listed. |
| RX 7900 XTX | Pearl 40 TH/s, 230 W: $1.30 / $0.47 (marked "*" = limited/unverified) | EPIC 56 MH/s, 320 W: $1.62 / $0.46 | Tari C29 $1.31 / $0.41; Qubitcoin $0.96 / $0.21 |

Sources: [3060](https://whattomine.com/gpus/58-nvidia-geforce-rtx-3060?cost=0.15&cost_currency=USD&sort=revenue24_desc), [3070](https://whattomine.com/gpus/48-nvidia-geforce-rtx-3070?cost=0.15&cost_currency=USD&sort=revenue24_desc), [3080](https://whattomine.com/gpus/46-nvidia-geforce-rtx-3080?cost=0.15&cost_currency=USD&sort=revenue24_desc), [4060](https://whattomine.com/gpus/87-nvidia-geforce-rtx-4060?cost=0.15&cost_currency=USD&sort=revenue24_desc), [4070](https://whattomine.com/gpus/84-nvidia-geforce-rtx-4070?cost=0.15&cost_currency=USD&sort=revenue24_desc), [4090](https://whattomine.com/gpus/79-nvidia-geforce-rtx-4090?cost=0.15&cost_currency=USD&sort=revenue24_desc), [RX 6600](https://whattomine.com/gpus/68-amd-radeon-rx-6600?cost=0.15&cost_currency=USD&sort=revenue24_desc), [RX 6700 XT](https://whattomine.com/gpus/59-amd-radeon-rx-6700-xt?cost=0.15&cost_currency=USD&sort=revenue24_desc), [RX 7900 XTX](https://whattomine.com/gpus/81-amd-radeon-rx-7900-xtx?cost=0.15&cost_currency=USD&sort=revenue24_desc)

**Cross-checks for the two reference cards** (net recomputed at $0.15)

RTX 3080:

| Algorithm | hashrate.no | Kryptex |
|---|---|---|
| Pearl | 106.9 TH/s, 159 W, $3.49 gross → **$2.92 net** | 105 TH/s, 240 W, $3.46 → $2.60 |
| Quantus | 417 MH/s, 143 W, $2.93 → **$2.42** | 488 MH/s, 165 W, $2.74 → $2.15 |
| Parano1d (noid) | 60.9 MH/s, 158 W, $1.74 → $1.17 | — |
| Epic | $1.41 → $0.33 | — |
| Qubitcoin | $0.85 → $0.48 | — |
| Tari C29 | — | $1.15, 192 W → $0.46 |
| Conflux | — | $0.78 → −$0.12 |
| Iron Fish | — | $0.62, 250 W → −$0.28 |
| Ergo | — | $0.44 → −$0.35 |
| Quai-KawPow | — | $0.34 → −$0.56 |
| **Xelis** | — | **$0.30, 220 W → −$0.49** |
| RVN, Nexa, Neurai, ETHW | — | $0.12–0.19 gross, all clearly negative |

Sources: [hashrate.no 3080](https://hashrate.no/gpus/3080), [Kryptex 3080 page](https://pool.kryptex.com/en/device/gpu/NVIDIA/nvidia-geforce-rtx-3080)

RX 6700 XT:

| Algorithm | Kryptex | hashrate.no |
|---|---|---|
| Quantus | 186 MH/s, 100 W, $1.05 → **$0.69** | 83 MH/s, 48 W, $0.56 → $0.39. Older figure: SRBMiner 3.7.1–3.7.6 improved AMD Quantus. |
| Tari C29 | 5 h/s, 95 W, $0.61 → $0.27 | — |
| Pearl | 16.2 TH/s, 140 W, $0.53 → $0.03 | 12.5 TH/s, 73 W, $0.41 → $0.15 |
| Qubitcoin | — | $0.48 → $0.30 |
| Epic | — | $0.47 → $0.15 |
| Conflux | $0.34 → −$0.13 | — |
| Iron Fish | $0.26, 60 W → $0.04 | — |
| Xelis | $0.14, 100 W → −$0.22 | — |

Sources: [Kryptex 6700 XT page](https://pool.kryptex.com/en/device/gpu/AMD/amd-radeon-rx-6700-xt), [hashrate.no 6700 XT](https://hashrate.no/gpus/6700xt)

**Takeaways**

- On every NVIDIA card, Pearl comes first and Quantus second, with Quantus at about 70–80% of Pearl's revenue.
- On AMD RDNA2, Quantus is the only option that clearly pays at $0.15/kWh. Everything else nets $0.30/day or less.
- Xelis and FishHash lose money at $0.15/kWh on all of these cards. So do all the MoneroOcean GPU algorithms (KawPow, Autolykos, Etchash).

---

## 2. Pools, unMineable, miners, AMD

### Pearl (PRL, pearlhash)

**unMineable**
- PearlPow can pay out in any of unMineable's coins, including USDT, BTC, XEL and **XMR**. Payouts are automatic, once a day, with no network fee.
- Minimums: USDT TRC20/BEP20 1.5, BTC 0.00075, XMR 0.03. [USDT](https://unmineable.com/coins/USDT), [BTC](https://unmineable.com/coins/BTC), [XMR](https://unmineable.com/coins/XMR)
- **AMD is not supported.** unMineable's support article says AMD GPUs are "not compatible" with PearlPow. [unMineable support](https://unmineable.com/support/article/how-to-setup-lpminer-for-pearl-pow-mining)
- Referral discount: the fee falls to 0.75% with a referral code on your own worker. [unMineable referrals](https://unmineable.com/referrals)

**Kryptex**
- Login is `PRL_ADDRESS.worker` with no account, and payouts come in PRL. With no account there is no auto-exchange: auto-convert to BTC only works for `krx…` accounts, which need email.
- Live API: PPS+ 3% or SOLO 1%, minimum payout 1 PRL, 8 regions, port 7048 / SSL 8048. [Kryptex PRL pool info](https://pool.kryptex.com/prl/api/v1/pool/info)
- Kryptex holds about 47% of Pearl's network hashrate (see `../Pools and bandwidth that pay crypto/gpu_pools_kryptex.md`).

**HeroMiners**
- Login is the wallet address, with PPS+ or PROPX. The API shows `minPaymentThreshold` = 1 PRL (1e8 units) and payouts every 3600 s. [HeroMiners Pearl API](https://pearl.herominers.com/api/stats)

**Miners**
- SRBMiner-Multi `pearlhash`: 2% fee, AMD and NVIDIA. [SRBMiner README](https://github.com/doktor83/SRBMiner-Multi)
- Also BzMiner (NVIDIA, AMD and Arc), lpminer (NVIDIA, Windows) and PeakMiner (NVIDIA). [Kryptex Pearl miners, 18 Jun 2026](https://pool.kryptex.com/en/articles/pearl-miners-en)
- SRBMiner 3.7.6 (14 Oct) improved pearlhash on RTX 30/40. 3.7.3 changed the kernel defaults, so overclocks need re-tuning (810 MHz memory). [SRBMiner releases](https://github.com/doktor83/SRBMiner-Multi/releases)

**AMD figures**
- WhatToMine lists only RDNA3/RDNA4 Pearl estimates, all marked "*": RX 9070 XT $2.27 gross, RX 7900 XTX $1.30. RDNA2 is not listed. [WhatToMine Pearl GPUs](https://whattomine.com/coins/469-prl-pearl/gpus)

**Liquidity**
- The only listed spot market is SafeTrade, at about 0.0000229 BTC/PRL with 40.7 BTC of 24h volume. [WhatToMine Pearl](https://whattomine.com/coins/469-prl-pearl)

### Quantus (QUANTUS/"QTC", QPoW/Poseidon2), mainnet 9 Sep 2026

**unMineable:** not listed. The USDT, BTC, XMR and XEL algorithm lists contain no Quantus.

**Kryptex**
- No account is needed. You mine to a QTC wallet address (Quantus Wallet app, or a BigOne/SafeTrade deposit address). Payouts run hourly, and Kryptex pays the transaction fee. [Kryptex Quantus guide, 5 Oct 2026](https://pool.kryptex.com/en/articles/how-to-mine-quantus-en)
- The live API shows PROP 3% / SOLO 3% and a 0.1 QTC minimum. [Kryptex API index](https://pool.kryptex.com/api/v1/index) The guide still says 1%.
- Stratum port is 7049. Otium already uses `qtc.kryptex.network:7049` for balance accounts (`src/Otium.Server/ServerOptions.cs:322`).

**NurseryPool**
- Login `qz…address.rig`, minimum 0.01 QUAN, SRBMiner over TCP stratum on port 2255. The miner pays network fees. The fee after the 20 Sep promotion is not stated. [NurseryPool help](https://quan.nurserypool.com/help.html)

**Miners**
- SRBMiner `quantus`: README says 2%, while NurseryPool and Kryptex say 2.5%. Supports AMD, NVIDIA and Intel. 3.7.1–3.7.6 improved AMD and NVIDIA. Kryptex calls it "the only miner with AMD support."

**Liquidity and price**
- SafeTrade only on WhatToMine: 0.00134 BTC (about $110 at WhatToMine's BTC price on 16 Oct, $82,376), 2.38 BTC of 24h volume. Difficulty is 8.6% below its 7-day average. [WhatToMine Quantus](https://whattomine.com/coins/473)
- Kryptex also names BigOne.
- So the 0.1 QTC minimum is about $11, about 4 days of mining on a 3080 or 10 on a 6700 XT.

### Tari Cuckaroo29 (XTM-C29)
- **Kryptex:** login `WALLET/WORKER`, PROP 1%, minimum 200 XTM, hourly automatic payouts. [Kryptex Tari C29](https://pool.kryptex.com/xtm-c29/about-coin); [API index](https://pool.kryptex.com/api/v1/index)
- **Miner:** lolMiner, per Kryptex. The lolMiner README lists only CuckarooD29 and CuckarooM29 at 2%, so the exact C29 fee is unverified. [lolMiner](https://github.com/Lolliedieb/lolMiner-releases)
- **Price:** about 2.14 sat (about $0.0018), so the 200 XTM minimum is about $0.35. Volume is about 0.88 BTC/day on NonKYC, with CoinEx and SafeTrade at about 0.08 BTC each. [WhatToMine XTM-C29](https://whattomine.com/coins/451-xtm-cuckaroo29)
- **unMineable:** no.

### Others checked

| Coin | What I found | Verdict |
|---|---|---|
| Qubitcoin (Qhash) | SRBMiner 1.5% (AMD/NVIDIA/Intel). Pools: Rplant, Suprnova (older snapshot). Exchanges: CoinEx, SafeTrade. [WhatToMine Qubitcoin](https://whattomine.com/coins/450-qtc-qhash) | Small; pools not re-verified |
| Parano1d NOID (`noid`) | SRBMiner 2%, AMD and NVIDIA. hashrate.no ranks it #3 on a 3080. Only market found: SafeTrade NOID/USDT (~$198k/day, unverified). No pool found. [blockspot](https://blockspot.io/nl/coin/parano1d-noid/) | Not usable yet |
| Keryx (KeryxHashH6) | WhatToMine ranks it #3 on the 4070, 4060 and 3070. The "Keryx-old" listing shows no exchange and 0 volume. [WhatToMine KRX](https://whattomine.com/coins/470-krx-keryxhash) | Liquidity and miner unverified; skip |
| Zclassic (EquihashZero) | #1 on 6700 XT per WhatToMine ($0.21 net) | No pool or miner verified; skip |
| Epic Cash (ProgPow) | SRBMiner `progpow_epic` 0.85%. Pools such as epicmine.io (2.5%). Mimblewimble payouts; address-login auto-payout not verified. | Marginal; skip |
| Quai | GPU is only Quai-KawPow (Kryptex: $0.34 on 3080, no auto-convert) | Negative |
| Kaspa, Alephium, Radiant, Tari SHA3x, ETC | ASIC (Kryptex marks KAS, ALPH, XTM-SHA3X, ETC "asic") | Remove from consideration |
| Ravencoin, Ergo, Nexa, Neurai, Neoxa, Clore (KawPow), Conflux | Negative at $0.15 on the 3080 and 6700 XT | Negative |
| Karlsen | +$0.05 on 3080 (WhatToMine) | Effectively zero |
| Warthog | Not checked | — |

**Dual mining.** lolMiner pairs FishHash/KarlsenV2/Octopus with ALPH, Pyrin, TON/Gram or SHA3X, and Rigel similarly (NVIDIA only). The secondaries are ASIC-dominated (ALPH, SHA3X) or tiny, and the primaries are negative at $0.15. No combination found beats Pearl or Quantus. [lolMiner](https://github.com/Lolliedieb/lolMiner-releases); [Rigel](https://github.com/rigelminer/rigel)

---

## 3. GPU AI-compute networks (provider paid to an address, no sign-up?)

None qualifies. Details are in `../More idle earning integrations/ai_compute_networks.md` (16 Oct 2026).

- **Salad:** needs an account, and multiple accounts are banned. Payout via storefront or PayPal.
- **io.net:** stake of 200 IO per card with a 14-day cooldown, plus Linux/Docker.
- **Nosana:** registered and verified hosts, native Linux, 85% uptime.
- **Akash:** Kubernetes provider.
- **Clore.ai:** host account, a $50 minimum (aggregator figure), and a 15% fee on non-CLORE payouts. [Clore host fees](https://docs.clore.ai/clore.ai-eng-de/fur-hosts/host-fees)
- **Vast.ai:** host account. Its verification requirements were not checked this time.

---

## 4. Stability and risks

**Pearl**
- Mainnet launched 27 Apr 2026. Per-card revenue roughly halved by about June as difficulty rose (Tom's Hardware, cited in `mining_opportunities.md`). Difficulty is still above its 7-day average.
- Price: all-time high $2.10 on 14 Oct, about $1.67 on 17 Oct, and it was at $0.14 in July. [CoinStats](https://coinstats.app/ai/a/latest-news-for-pearl-2.md)
- **Fork risk.** PIP-3 (FP8 certificates) is Draft, created 30 Jul 2026, with activation height "TBD". It whitelists only Hopper and Blackwell. [PIP-3](https://github.com/pearl-research-labs/pips/blob/main/PIPS/pip-0003.md)
  - Kryptex (7 Oct) reads this as RTX 20/30/40 and all AMD unable to produce valid certificates after activation. The founders confirmed the RTX 5090 and RTX PRO 6000 keep mining. [Kryptex FP8 fork article](https://pool.kryptex.com/articles/pearl-fp8-fork-en)
  - SRBMiner 3.6.8 (19 Sep) warned that consumer GPUs will become "pretty much useless". [SRBMiner releases](https://github.com/doktor83/SRBMiner-Multi/releases)
- **ASIC risk:** low. It is not hashing, but the fork is a de facto datacenter-GPU whitelist.

**Quantus**
- Launched 9 Sep 2026, so only about 5 weeks of history.
- Liquidity is thin (SafeTrade, BigOne). Kryptex holds about 56% of network hashrate.
- Difficulty is easing (−8.6% vs its 7-day average).

**Tari C29:** memory-hard, 8 GB+, low and stable margins.

---

## 5. Ranked GPU lineup for Otium (net at $0.15/kWh)

| Rank | Coin / algo | Best cards | RTX 3080 gross / net | RX 6700 XT gross / net | Pool meeting constraints 1–2 | Miner (fee) | Main risk |
|---|---|---|---|---|---|---|---|
| 1 | Pearl / pearlhash | RTX 20–50 (tensor cores) | $3.24–3.49 / $2.38–2.92 | $0.41–0.53 / $0.03–0.15 (not on unMineable) | unMineable (any coin incl. XMR, daily); Kryptex or HeroMiners for PRL payouts (1 PRL minimum, hourly) | SRBMiner 2% | PIP-3 fork drops RTX 20/30/40 and AMD (no date); SafeTrade-only spot |
| 2 | Quantus / quantus | All RTX; AMD RDNA2+ | $2.37–2.93 / $1.80–2.42 | $0.56–1.05 / $0.39–0.69 | Kryptex address login (QTC, 0.1 minimum ≈ $11, hourly, 3%); NurseryPool (0.01 minimum) | SRBMiner 2–2.5% | 5 weeks old; QTC illiquid; not on unMineable |
| 3 | Tari C29 / cuckaroo29 | 8 GB+ cards, AMD ok | $0.86–1.15 / $0.07–0.46 | $0.49–0.61 / $0.09–0.27 | Kryptex address login (200 XTM ≈ $0.35, hourly) | lolMiner (~2%) | Thin margin; new miner to ship |
| 4 | Qubitcoin / qhash | Low-power AMD and NVIDIA | $0.72–0.85 / $0.22–0.48 | $0.41–0.48 / $0.16–0.30 | Not verified (Rplant, Suprnova) | SRBMiner 1.5% | Small pools and liquidity |
| — | Xelis, FishHash, KawPow, Autolykos, Etchash | — | Negative | ≤ $0.04 | — | — | Free-power-only tier |

---

## 6. Recommendations

1. **Keep Pearl on unMineable (NVIDIA).** Do not route it to a direct pool. unMineable converts PRL to the user's coin for about 1% (0.75% with referral) and pays daily with no network fee. The direct pools (Kryptex PPS+ 3%, HeroMiners) pay only in PRL, a SafeTrade-only coin, which breaks constraint 4. Fee slicing works the same as today.
2. **Route Monero-paid NVIDIA users' GPUs to unMineable PearlPow with `XMR:address`** instead of MoneroOcean. The minimum is 0.03 XMR, paid daily ([unMineable XMR](https://unmineable.com/coins/XMR)). That is about $2.4–2.9/day net on a 3080, against negative net for MoneroOcean's GPU algorithms. Keep MoneroOcean GPU only as the free-power tier.
3. **Add Quantus as a non-custodial, opt-in "paid in QTC" route on Kryptex using the address login.** It is the only paying option for AMD and NVIDIA's #2. The SRBMiner entry and Kryptex host already exist in the code. The user needs a QTC (Dilithium "qz…") wallet, and nobody converts QTC for them. Label it as illiquid. For users who want BTC/USDT, there is no non-custodial conversion: Kryptex auto-exchange requires a `krx` account with email.
4. **Demote Xelis to free-power-only**, as FishHash already is, because it is negative at $0.15/kWh.
5. **Do not add Pearl-on-AMD.** It nets only $0.03–0.47/day, pays only in illiquid PRL, and PIP-3 removes AMD.
6. **Optionally add Tari C29** (lolMiner, Kryptex address login) as a low-margin AMD fallback after Quantus. Skip Keryx, NOID and Zclassic until pools and liquidity are verified.
7. **Build a Pearl kill-switch.** When PIP-3 gets an activation height, Otium's server policy should drop pearlhash for non-Blackwell cards automatically. After that the fallback is Quantus (NVIDIA ~$1.8–2.4 net on a 3080).

## Gaps
- No primary unMineable statement on whether Quantus is "upcoming": the listings widget did not load.
- HeroMiners' Pearl fee percentage was not shown; the API's `fee` field is 0.
- lolMiner's C29 dev fee, Warthog, and Vast.ai host requirements were not checked.
- AMD Quantus hashrate sources disagree by about 2×.
