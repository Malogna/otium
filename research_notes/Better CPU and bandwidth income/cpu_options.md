# CPU coins that might pay more than RandomX/XMR on a home desktop (checked 17 Oct 2026)

Question: which CPU-mineable coins or algorithms pay more than RandomX (XMR via MoneroOcean or unMineable) on a typical home CPU (Ryzen 7 5800X, i7-8700K), and which of them Otium could actually use? "Usable" means all four of these hold:

1. Address login with no account.
2. The pool tolerates the 2% fee time slice (switching to Otium's address on the same worker and IP) and pays small balances automatically.
3. A well-known Windows miner with checksums.
4. Enough liquidity to sell, or the pool pays out in a major coin.

Nothing was signed up for, downloaded or run. All data came from public pages and APIs, fetched on 2026-10-17 unless marked otherwise. Earlier notes cover some of the same ground: `research_notes/Pools and bandwidth that pay crypto/cpu_pools_non_xmr.md` and `research_notes/More idle earning integrations/mining_opportunities.md`.

## Takeaway

On 17 Oct 2026, nothing beats RandomX/XMR on a home CPU by a dependable margin while also meeting constraints 1–3.

- **Tari standalone RandomX (XTM-RX).** This is the only option that is sometimes ahead. It ranges from +2% to +5% (hashrate.no) down to −7% (Kryptex) and −17% (WhatToMine). MoneroOcean already mines it, and pays it out in XMR.
- **Dagger (XDAG).** It tops WhatToMine at +31%, but the coin can't be sold: CoinGecko shows $1.44 of volume in 24 hours.
- **Everything else is well below XMR:** EPIC, Etica, QRL, Zephyr, Salvium, Xelis, Verus, GhostRider, Dero, the yespower coins and the micro-cap RandomX forks.
- **Qubic** has no verifiable 2026 per-CPU figures. It uses its own client, pool fees are 7–10%, and its algorithm changed again in August 2026.

Recommendation: keep RandomX. Don't add a second CPU coin. MoneroOcean's built-in switching (XMR, XTM-T, ZEPH, SAL, plus Tari merge mining, about +1.4%) already captures the small RandomX-family premiums. unMineable remains the route when Otium needs to be paid in BTC.

## Reference revenue (24h, before electricity)

### hashrate.no (no date on the pages; fetched 2026-10-17)

| Coin (algo) | 5800X @ 11.1 kH/s, 105 W | vs XMR | 8700K @ 5.79 kH/s, 95 W | vs XMR |
|---|---|---|---|---|
| Monero XMR (RandomX) | $0.41 | — | $0.21 | — |
| "RandomX merged (XMR)" | $0.41 | 0% | $0.21 | 0% |
| Tari XTM (RandomX) | $0.42 | +2% | $0.22 | +5% |
| Epic Cash EPIC | $0.34 | −17% | $0.18 | −14% |
| QRL | $0.31 | −24% | $0.16 | −24% |
| Zephyr ZEPH | $0.26 | −37% | $0.13 | −38% |
| Salvium SAL | $0.22 | −46% | $0.11 | −48% |
| Raptoreum RTM (GhostRider) | n/a | | $0.02 @ 778 H/s | −90% |

Sources: [hashrate.no 5800X](https://www.hashrate.no/cpus/5800X), [hashrate.no 8700K](https://www.hashrate.no/cpus/8700K).

The 5950X page gives a sense of scale for the non-RandomX algorithms. Against XMR at $0.67 (18 kH/s), VerusHash pays $0.09 at 47.08 MH/s (−87%) and GhostRider pays $0.11 at 4,000 H/s (−84%). Source: [hashrate.no 5950X](https://www.hashrate.no/cpus/5950x).

### WhatToMine CPU (snapshot 2026-10-17 20:26 UTC, generic CPU, 140 W, $0.10/kWh)

Revenue per day, with exchange and 24h volume:

| Coin | Hashrate assumed | Revenue | vs XMR | Exchange, 24h volume |
|---|---|---|---|---|
| Dagger XDAG (RandomX) | 21 kH/s | $1.10 | **+31%** | NonKYC, 0.00 BTC |
| Monero XMR | 21 kH/s | $0.84 | — | MEXC, 33.55 BTC |
| Etica ETI (RandomX) | 21 kH/s | $0.83 | −1% | NonKYC, 0.00 BTC |
| NiceHash-RandomX | — | $0.79 | −6% | |
| Tari-RandomX XTM | 21 kH/s | $0.70 | −17% | SafeTrade, 0.11 BTC |
| EPIC-RandomX | 21 kH/s | $0.67 | −20% | NonKYC, 0.49 BTC |
| QRL | 21 kH/s | $0.58 | −31% | |
| Zephyr | 21 kH/s | $0.49 | −42% | |
| Xelis (XelisHash v3) | 16 kH/s | $0.42 | −50% | |
| Raptoreum (GhostRider) | 4.9 kH/s | $0.13 | −85% | |
| Verus (VerusHash) | 55 MH/s | $0.12 | −86% | |
| Tari-Merged (MoneroRandomX) | 21 kH/s | $0.01 | +1.2% on top of XMR | |

Source: [WhatToMine CPU](https://whattomine.com/cpu). Scaling to a 5800X: 11.1/21 × $0.84 ≈ $0.44/day for XMR.

### Kryptex 5800X page (10 kH/s, "last updated 17/10/2026"; figures are annual)

| Coin | Hashrate | Revenue per year | Revenue per day | vs XMR |
|---|---|---|---|---|
| XMR | 10 kH/s | $150.48 | $0.41 | — |
| XTM-RX | 10 kH/s | $139.54 | | −7% |
| ZEPH | 10 kH/s | $85.77 | | −43% |
| SAL | 10 kH/s | $81.18 | | −46% |
| Xelis | 7 kH/s | $70.02 | | −53% |

Source: [Kryptex 5800X](https://pool.kryptex.com/device/cpu/amd/ryzen-7-5800x).

### Other cross-checks

- Minerstat 5800X: RandomX 9 kH/s (data from 2026-10-01). It names ETI as the "most profitable coin", at about $0.53/day gross. Source: [minerstat](https://minerstat.com/hardware/amd-ryzen-7-5800x).
- miningboard RandomX: $0.0395 per kH/s per day, so about $0.43/day for an 11 kH/s CPU. It also lists XDAG with a network of only **1.84 MH/s** and a "$0.00" price. Source: [miningboard RandomX](https://miningboard.com/algorithms/RandomX).

## MoneroOcean already switches across the RandomX family (live `/pool/stats`, 2026-10-17)

`coinProfit`, normalised per hash:

| Coin | coinProfit | vs XMR |
|---|---|---|
| XMR | 7.296e-8 | — |
| XTM-T (Tari standalone, rx/0) | 7.282e-8 | −0.2% |
| ZEPH | 4.09e-8 | −44% |
| SAL | 2.98e-8 | −59% |
| XTM (merge-mined on the XMR port) | 1.03e-9 | +1.4% on top of XMR |

Ports shown as inactive: RTM (GhostRider, "daemon error"), DERO, ARQ and WOW-family. Source: [MoneroOcean API](https://api.moneroocean.stream/pool/stats).

MoneroOcean's internal switching already picks whichever of these is best for each miner, and pays in XMR. Adding Tari ourselves would duplicate that.

## Auto-exchange pools with address login

**unMineable** ([BTC page](https://unmineable.com/coins/BTC))
- Only one CPU algorithm is listed now: RandomX. XelisHash appears but is marked GPU. GhostRider is gone.
- BTC payouts: 0.00075 BTC minimum, automatic daily, 1% fee (0.75% with a referral code).
- unMineable has no CPU coin that out-earns RandomX.

**zpool** ([zpool.ca](https://zpool.ca/), [status API](https://zpool.ca/api/status))
- Login is the wallet address as username, password `c=BTC`. No registration.
- BTC is paid daily above 0.00075. Other coins are paid every 4 h above 0.05. Fee 1%.
- There is still no RandomX. Converting the `actual_last24h` figures at about $84k/BTC (WhatToMine-implied):
  - GhostRider: 0.00065 mBTC per kH/s per day, so about $0.22/day on a 5950X at 4 kH/s (−67% vs $0.67 XMR).
  - VerusHash: 0.0292 mBTC per GH/s per day, so about $0.12 at 47 MH/s (−83%).
  - yespowerTIDE: 0.07 mBTC per MH/s per day, a few cents.
  - MinotaurX: 0.16 mBTC per MH/s per day. It would need about 50 kH/s on a 5950X (about 30 kH/s on a 5800X) to tie XMR. I found no CPU MinotaurX benchmark, so this is a gap.
  - yescrypt: 0.175 mBTC per MH/s per day. It would need about 45 kH/s on a 5950X; no benchmark was found.

**Kryptex**
- XMR pool: PPS+, 1%, minimum 0.01 XMR, `wallet/worker` login, no merge-mined XTM bonus mentioned ([Kryptex XMR](https://pool.kryptex.com/en/xmr)).
- XTM-RX pool: PROP 1%, minimum 200 XTM (about $0.28 at roughly $0.0014/XTM, implied by Kryptex and WhatToMine), `wallet/worker` login, XMRig `-a rx/0`, 33.3 MH/s, 3,063 miners ([Kryptex XTM-RX](https://pool.kryptex.com/xtm-rx)).
- Converting to BTC requires a Kryptex username, which is an account and so breaks constraint 1.

**Defunct or unsuitable:** Zergpool and ProHashing have shut down. MiningDutch shows RandomX empty. 2Miners has no XMR pool. HeroMiners lists only plain "randomx" ([herominers.com](https://herominers.com/)). See the earlier notes for details.

## Coin by coin

### Dagger (XDAG): on paper +31%, in practice unsellable

- CoinGecko shows $0.000717, a **24h volume of $1.44**, and one market (XT.COM, flagged inactive) ([CoinGecko XDAG](https://www.coingecko.com/en/coins/xdag)).
- The network is about 1.84 MH/s (miningboard). Emission is roughly 64 XDAG every 64 s, or about 86,400 XDAG/day, which is about $60/day for the whole network at the CoinGecko price. A few hundred Otium CPUs would take most of it.
- The pool software (XDagger/xdag-pool) uses `<address>.WorkerID` and its example config pays every 10 minutes with a 5% fee ([xdag-pool](https://github.com/XDagger/xdag-pool)). I found no confirmed live public pool: the miningboard scraper has never reached xdag.org. Mainline XMRig has no XDAG mode; the XDagger org offers its own miner and an XMRig2XDAG proxy.
- Verdict: near-miss. It fails constraint 4 and probably constraint 3.

### Etica (ETI): −1% (WTM) or "best" on minerstat

- Volume is 0.00 BTC on NonKYC (WhatToMine).
- The only tracked pool, etica.xdagreef.org, had 3 miners ([miningboard](https://miningboard.com/zh/pools/etica.xdagreef.org)).
- Verdict: near-miss on liquidity and pool depth.

### Epic Cash (EPIC): −14% to −20% today; +17% on the 7950X page on 16 Oct

- CoinGecko: $0.84 (up 10% in 24h), **$95.6k** of 24h volume, all on NonKYC.io, which is a no-KYC venue: EPIC/XMR 54%, EPIC/USDT 46% ([CoinGecko EPIC](https://www.coingecko.com/en/coins/epic-cash)).
- epicmine.io:
  - Login is "username.worker". It looks like an account, but this is unconfirmed.
  - It wants XMRig-Epic, a fork, rather than mainline XMRig.
  - SRBMiner-Multi has `randomepic` at a 0.85% fee.
  - Fees are 2.5% and "dynamic".
  - Mimblewimble payouts are interactive, which complicates paying to an address.
  - Sources: [epicmine get-started](https://epicmine.io/get-started), [miningboard epicmine](https://miningboard.com/en/pools/epicmine.io).
- Verdict: near-miss. It is volatile around parity, and it fails constraints 1 and 3 (fork miner, account-style login).

### Tari standalone (XTM-RX): −17% to +5%

- Address login works on Kryptex with a 200 XTM minimum.
- Liquidity is thin, about $40–90k/day depending on the source ([CoinSwitch](https://coinswitch.co/coins/tari), [Cryptorank](https://cryptorank.io/price/tarii/usd)).
- MoneroOcean's XTM-T port is −0.2% vs XMR.
- Verdict: not worth a separate pool path; MoneroOcean covers it.

### Qubic: no current numbers

- The algorithm changed to "BPP-9000" in August 2026, inside an "ant colony" framework ([Cointribune, 20 Sep 2026](https://www.cointribune.com/en/start-mining-qubic-the-complete-2026-guide/)).
- Pools listed there (pool.qubic.li, MinerLab, Jetski) show fees of about 7%. apool charges 10% PPLNS ([miningboard apool](https://miningboard.com/zh/pools/apool.io)).
- Payouts come weekly at the end of each epoch (Wednesday 12:00 UTC).
- A registerless mode exists: `qli-Client <QUBICID> <THREADS>` ([snoby/hiveos](https://github.com/snoby/hiveos)). The client is qubic.li's own (Qli), not XMRig or SRBMiner.
- Qubic said in April 2026 that XMR mining phases out in favour of DOGE on ASICs ([The Block PR](https://www.theblock.co/press-releases/396050/)).
- WhatToMine only lists "Qubic-old", with no exchange rate ([WTM Qubic-old](https://whattomine.com/coins/458-qubic-qubic)).
- The last per-CPU figure ($3.13/day on a 7950X) is self-reported and from mid-2025 ([Qubic blog](https://qubic.org/blog-detail/why-qubic-is-now-the-top-profitable-coin-to-mine-in-2025)).
- Verdict: near-miss. It fails constraint 3, has no verifiable revenue, and its algorithm and payout model keep changing.

### Xelis

- −50% (WTM) to −53% (Kryptex) on CPU. unMineable treats XelisHash as a GPU algorithm. SRBMiner's fee for it is 1.5%.

### Verus (VerusHash 2.2)

- −83% to −87%. hellminer 0.59.1 is closed-source with no checksums on its README. It quotes 41 MH/s on a 3950X ([hminer README](https://github.com/hellcatz/hminer/blob/main/README.md)).
- Pools (luckpool and others) do take `WALLET.RIG` address logins with automatic payouts, so the blocker is revenue, not mechanics.

### Raptoreum (GhostRider)

- −84% to −90%. MoneroOcean's RTM port reports "daemon error".

### Dero (AstroBWT v3)

- miningboard shows CPUs at a net loss; the top 9950X is −$0.41/day after power ([miningboard AstroBWT](https://miningboard.com/en/algorithms/AstroBWT)). MoneroOcean's DERO port is inactive.

### yespower and yescrypt family

- A few cents per day on zpool (see the zpool section above).

### Micro-cap RandomX forks (SRBMiner random* variants: C64, Juno, Virel, Scash and others)

- JunoCash: $187k market cap and $21.9k of 24h volume ([CoinDesk JUNO](https://www.coindesk.com/price/junocash)).
- Virel: thousands of dollars a day in January 2026 ([coinranking](https://coinranking.com/coin/HBkAMbDEA+virelprotocol-vrl/historical-data)).
- C64: data inconsistent, price shown as "$0.00" ([miningboard C64](https://miningboard.com/mining/c64chain/hardware)).
- Verdict: too thin. A few hundred Otium CPUs would crush both the price and the difficulty.

### NexaPow

- GPU-only on unMineable.

## Miners

- **XMRig v6.26.0** (2026-03-28). Each release ships SHA256SUMS plus a GPG signature. It added RandomX v2 support ([XMRig releases](https://github.com/xmrig/xmrig/releases)).
- **SRBMiner-Multi** CPU fees: 0.85% for randomx, randomepic, ghostrider, minotaurx, verushash, yescrypt and yespower. Xelis is 1.5%, yespowereqpay 2% ([SRBMiner-Multi](https://github.com/doktor83/SRBMiner-Multi)). I did not confirm whether checksums are published.
- **cpuminer-opt**: no dev fee.
- **hellminer**: closed source, no checksums on its README.

## Inferences

- Every "beats XMR" result for a home CPU in October 2026 is either within the noise between data sources (Tari, EPIC, Etica), or a coin too illiquid to sell what is mined (XDAG, Etica, the Juno-type forks). Revenue sites rank by nominal price, which hides whether anyone can actually sell the coins.
- The non-RandomX CPU algorithms (Verus, GhostRider, Xelis, Dero, yespower) all pay less than a third to half of RandomX on these CPUs.
- The fee-slice mechanics (switching addresses on the same worker) work on all the address-login pools checked: MoneroOcean, unMineable, Kryptex wallet mode, zpool and luckpool. Constraint 2 is never what blocks an option. Revenue and liquidity are.
- The cheapest worthwhile change: let Otium's profit check read MoneroOcean's `coinProfit` and WhatToMine's JSON. If a RandomX-family coin beats XMR by more than 10% for several days and has more than $250k of daily volume, flag it for review. Don't auto-switch.

## Gaps

- No CPU MinotaurX or yescrypt benchmarks were found, so the zpool break-even hashrates above are unverified.
- hashrate.no gives no date on its estimates. The +17% EPIC figure (16 Oct) vs −17% (17 Oct) shows how much they move day to day.
- Qubic has no 2026 per-CPU revenue figures or pool API docs. Whether BPP-9000 clients are open source is unconfirmed.
- Whether epicmine's "username" is a registered account or an epicbox address is unconfirmed.
- No dated Tari volume from CoinGecko (its Tari page returned 404).
