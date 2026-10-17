# Bandwidth / internet-presence programs that pay the operator in crypto (as of 16 Oct 2026)

Scope: options for Otium (desktop app; operator runs the integration on opted-in users' PCs, receives revenue, credits users in USD, pays out BTC/ETH/BNB/SOL from a BTC treasury). Preference: revenue arrives in crypto (ideally BTC, or a token/stablecoin swappable to BTC on-chain without KYC, e.g. THORChain), with per-device reporting. Points-only programs are valued at zero. Items marked UNVERIFIED come from prior knowledge or a single weak source.

## Mysterium (MystNodes) in depth: earnings, MYST liquidity and conversion, fees, partner SDK, B2B mode, OS support, and how Kryptex does it

### Takeaway
Mysterium is the only bandwidth network found that pays natively in a token, has an official app-integration SDK, and has a live precedent for Otium's model: Kryptex runs Mysterium nodes inside its desktop app, converts MYST to BTC at 5 MYST, and credits users in BTC. The big problem is that MYST is now very illiquid (about $2.6M market cap, about $26k per day volume, about $2.5k of ±2% depth on the best DEX pools, CoinGecko 16 Oct 2026). Per-node earnings are also small: the vendor claims $8–25 per month for an always-on home node, and independent estimates are about $1–2 per month.

### Cited Findings
**Earnings (dated)**
- MystNodes' own article (updated 22 Sep 2026) gives a base rate of $0.04–$0.30 per GB depending on service type (VPN, data streaming, public web access, scraping). It estimates a casual laptop at $2–8 per month, an always-on home node at $8–25 per month, and a multi-node operator at $25–100+ per month. It reports 120+ countries, 45k+ participating IPs, and only $568,019 paid out network-wide "to date". This is a vendor claim. Residential IPs and some countries earn more, but no country rates are given. — [MystNodes blog](https://www.mystnodes.com/blog/how-much-can-you-earn-sharing-bandwidth)
- DePIN Scan estimates about $0.04 per day (about $1.20 per month) for an average device (undated snapshot). An older review projected about $2.33 per month from the reviewer's own node. — [DePIN Scan](https://depinscan.io/projects/mysterium); [PaidFromSurveys review](https://paidfromsurveys.com/mysterium-node-review)
- Rewards system launched 4 Mar 2026: users earn points for node uptime and for holding ≥100 MYST. Points convert to MYST at month end from a monthly pool with no fixed size, and must be claimed within 30 days. The per-point value depends on pool size and participation. — [MystNodes help: Rewards System](https://help.mystnodes.com/en/articles/13925332-mystnodes-rewards-system); [MystNodes blog: rewards launch](https://www.mystnodes.com/blog/mystnodes-launches-rewards-system)
- 4 Mar 2026: the team said it is adapting tokenomics, including the MYST staking delegation pool, to increase rewards for holders and nodes. No figures were given. — [MystNodes help: staking](https://help.mystnodes.com/en/articles/8005345-staking-your-myst-tokens)
- Kryptex says income "cannot be predicted" and depends on IP location and connection stability. One independent Windows 11 user saw about €0.08–0.19 per day in Kryptex, but that figure is not isolated to Mysterium. — [Kryptex getting started](https://www.kryptex.com/en/articles/mysterium-getting-started-en); [emielburgman.nl blog](https://www.emielburgman.nl/blog/passive-income-through-bandwidth-sharing-on-my-windows-11-setup)

**Settlement and fees**
- Settlement is in MYST on Polygon. A 20% network fee plus Polygon gas applies, and balances auto-settle at 5 MYST (third-party page, verified 1 Aug 2026). — [Jivaro bandwidth comparison](https://jivaro.net/resources/share-internet-bandwidth-for-money/)
- The MystNodes help centre confirms auto-settlement at 5 MYST and manual settle in NodeUI. The vendor blog says there is "no minimum" and advises waiting for 5 MYST because of gas. These are consistent in practice. — [MystNodes help: withdraw](https://help.mystnodes.com/en/articles/8005144-where-do-i-check-and-withdraw-my-earnings); [MystNodes blog](https://www.mystnodes.com/blog/how-much-can-you-earn-sharing-bandwidth)

**MYST liquidity and conversion to BTC**
- CoinGecko (16 Oct 2026):
  - Price $0.0797, market cap about $2.59M (rank about #2153), 24h volume about $25.8k, circulating supply 32.43M.
  - Venues: MEXC MYST/USDT (75% of volume; +2% depth $1,565, −2% depth only $73); Quickswap Polygon MYST/USDC.e (22%; ±2% depth about $2.57k; about $5.8k per day); Uniswap V3 Ethereum MYST/WETH (±2% depth about $2.5k; about $519 per day); Uniswap V3 Polygon MYST/WPOL and MYST/USDC (negligible, and the USDC pool is inactive).
  - Contracts exist on Ethereum, Polygon and BSC, but no BSC market is listed.
  — [CoinGecko MYST](https://www.coingecko.com/en/coins/mysterium)
- Price trend: about $0.156 (MEXC page, 22 Mar 2026), about $0.11 (DePIN Scan, later and undated), $0.0797 (16 Oct 2026). That is roughly a 50% fall since March. — [MEXC price page](https://www.mexc.com/price-prediction/MYST); [DePIN Scan](https://depinscan.io/projects/mysterium); [CoinGecko](https://www.coingecko.com/en/coins/mysterium)
- THORChain's TRON integration post lists the chains THORChain swaps to: BTC, ETH, Base, BSC, AVAX, DOGE, GAIA, LTC, XRP and BCH. Polygon is not on the list, so MYST on Polygon cannot go straight into THORChain. — [THORChain blog: TRON integration](https://blog.thorchain.org/tron-integration-complete-native-trx-usdt-swaps-live-on-thorchain)

**Partner SDK and B2B mode**
- Official Docker image `mysteriumnetwork/mystnodes-sdk-client`: "connects applications to the Mysterium SDK… monetization through bandwidth sharing". It runs with `--user.token=<token obtained from mystnodes team>`. It advertises "Ad-Free Monetization" and Windows, Android, macOS and Linux support, and was updated in 2026. No revenue share or payout terms are published. — [Docker Hub: mystnodes-sdk-client](https://hub.docker.com/r/mysteriumnetwork/mystnodes-sdk-client)
- Hivello (a DePIN aggregator app) also integrated MystNodes as an option for its users. — [search summary of Hivello x Mysterium release; see QuickNode builders guide](https://www.quicknode.com/builders-guide/tools/mysterium-network-by-mysterium-foundation)
- Node traffic types can be chosen in the node Web UI:
  - "B2B Data Scraping, B2B VPN and Data Transfer": traffic from Mysterium's business partners, which Mysterium recommends as "clean and safe"
  - "VPN (Mysterium VPN)": card-paying users of Mysterium's VPN apps
  - "Public (Mysterium Dark)": anonymous users paying in MYST

  The node is an exit node, and Mysterium "will try to shield" node runners from liability but "can't guarantee" it. — [Kryptex getting started (7 May 2026)](https://www.kryptex.com/en/articles/mysterium-getting-started-en)

**How Kryptex integrates it**
- Announced 11 Dec 2023. Users switch the app to PRO mode and turn on a "Network Card — Mysterium" device. If the switch is missing, the user's region is not supported. — [Kryptex news](https://www.kryptex.com/en/news/kryptex_x_mysterium)
- The KB article (7 May 2026) says:
  - Windows 10/11 x64 only. Recommended: 4 cores, 8 GB RAM, ≥50 Mbps, wired connection.
  - The user reviews and accepts the Mysterium Network Terms, which are Mysterium's, not Kryptex's.
  - The user sets a node password for the local NodeUI. Node data lives in `%UserProfile%\.mysterium\`, so it is a full local myst node.
  - "Kryptex converts your MYST into Bitcoin once you accumulate 5 MYST", and the user then withdraws BTC as usual.
  - With several PCs behind one router, Kryptex advises running it on only one PC because they share an IP.
  - No conversion fee is disclosed.

  — [Kryptex getting started](https://www.kryptex.com/en/articles/mysterium-getting-started-en)

### Inferences
- Kryptex is a working template for Otium: bundle the myst node and point its payout (beneficiary) at an operator-controlled wallet. Then read per-node earnings from the local node, convert to BTC centrally, and credit users. The 5-MYST threshold matches the auto-settlement threshold, which suggests Kryptex settles per node identity to its own wallet. This is an inference; Kryptex does not publish its beneficiary setup.
- Selling MYST into BTC is the binding constraint. With only about $2.5k of ±2% depth on each main DEX pool and about $6k per day of on-chain volume, an operator selling more than a few hundred dollars per day would move the price noticeably. The practical no-KYC route is:
  - MYST (Polygon) → USDC.e on Quickswap → bridge to Ethereum/Base/BSC → THORChain → BTC, or
  - MYST (Ethereum) → WETH on Uniswap V3 → THORChain → BTC.

  At Otium's likely early scale (tens to hundreds of users × about $1–25 per month), this is workable if sales are split into small daily tranches.
- Because MYST fell about 50% in seven months, Otium should convert MYST frequently (daily or weekly) and credit users at the realized BTC/USD value rather than at the MYST spot price when earned. Otherwise the operator carries token price risk.
- Per-user crediting: each user's PC runs its own node identity, so per-identity earnings are naturally attributable. This is the cleanest per-device accounting of any option found.
- Setting the node to B2B-only traffic types lowers legal and antivirus exposure (no anonymous "Mysterium Dark" exit traffic), though it may reduce earnings.

### Gaps
- Official partner/SDK revenue share, contract terms, KYC/KYB requirements for partners, and whether the SDK settles to one partner wallet or per identity. Not published; the token must be requested from the MystNodes team. The mystnodes.com/sdk URL returned 404, and blog.mystnodes.com did not resolve.
- UNVERIFIED (prior knowledge, not re-checked): the Tequila API (local node REST API on port 4050) exposes per-identity earnings and settlement endpoints, and many node identities can share one beneficiary address. Confirm against current Mysterium docs.
- macOS/Linux support for the full node is documented elsewhere by Mysterium (Docker/Linux packages), but not re-verified this pass. Kryptex's integration is Windows-only.
- The exact country restriction list was not found; Kryptex only says some regions are unsupported.
- Kryptex's MYST→BTC conversion fee or spread is not disclosed.
- Current THORChain or Chainflip listing of MYST: none found (as expected). Not directly confirmed against THORChain's pool list.

## Other proxy/bandwidth/DePIN programs that pay in crypto: currency, operator aggregation, reporting, OS, earnings, KYC, risk

### Takeaway
Among mainstream proxyware apps, only Traffmonetizer (USDT on TRON, $10 minimum) and Packetshare (USDT, $20 minimum) are confirmed to pay in a stablecoin. Honeygain pays crypto only as the JMPT token via JumpTask. Bitping pays SOL. Pawns.app bandwidth sharing was discontinued in 2026. EarnApp, PacketStream and Proxyrack pay fiat only. No consumer app was confirmed to permit an operator running it on other people's PCs, and none offered a crypto-paying publisher SDK. Most DePIN "bandwidth" networks remain points or airdrop based and are valued at zero.

### Cited Findings
**Traffmonetizer**
- Pays USDT on TRON (TRC20), $10 minimum. Wire transfer is available above $1,000. There is no dependable public per-GB rate (verified 1 Aug 2026). — [Jivaro](https://jivaro.net/resources/share-internet-bandwidth-for-money/)
- An older review says Traffmonetizer dropped Payeer, WebMoney, Skrill and other crypto, leaving TRC20 USDT plus wire. A forum user claims about $0.06 per GB. A guide estimates about $2 per node per IP per month. Additional devices on the same IP earn less. — [PaidFromSurveys](https://paidfromsurveys.com/traffmonetizer-review); [SavingAdvice forum](https://www.savingadvice.com/forums/forum/financial-chit-chat/ways-to-make-money/734114-my-traffmonetizer-review-low-payout-not-worth-the-install-time)
- Trustpilot reviews are mixed. One user reported a withdrawal "not yet approved"; another reported withdrawing $1k since 2023. — [Trustpilot](https://au.trustpilot.com/review/traffmonetizer.com)
- It can run headless via Docker with an account token. — [simeononsecurity guide](https://simeononsecurity-com.onrender.com/other/install-traff-monetizer-easily-using-docker/)

**Honeygain / JumpTask**
- Crypto payout is via JMPT in JumpTask mode (0.5 JMPT minimum); the standard PayPal minimum is $20. — [Jivaro](https://jivaro.net/resources/share-internet-bandwidth-for-money/)
- 24 Aug 2026 payout update: gift cards are added via JumpTask/GiftQ alongside PayPal and crypto, and the earning process is unchanged. — [Honeygain blog: payouts update](https://www.honeygain.com/blog/honeygain-payouts-update/)
- No change to the publisher SDK payout currency was found.

**Pawns.app (IPRoyal)**
- Its official 2026 information page says bandwidth sharing was discontinued, and legacy pages conflict ($0.20 per GB, $5 minimum). This is a material change from the earlier research. — [Jivaro](https://jivaro.net/resources/share-internet-bandwidth-for-money/)

**Fiat-only apps**
- EarnApp: moved to pay-per-time on 20 Aug 2025 (up to $10 per IP per month in the US, $5 elsewhere). Pays via PayPal, Wise or Amazon only, with no crypto. — [Jivaro](https://jivaro.net/resources/share-internet-bandwidth-for-money/)
- PacketStream: $0.10 per GB, PayPal only, 3% fee, $5 minimum. — [Jivaro](https://jivaro.net/resources/share-internet-bandwidth-for-money/)
- Proxyrack Peer: $0.50 per GB residential, paid via Tremendous (PayPal, gift cards, wire), no crypto. — [Jivaro](https://jivaro.net/resources/share-internet-bandwidth-for-money/)
- Repocket: rates and payout terms are not clearly documented, and no crypto payout was confirmed. — [Jivaro](https://jivaro.net/resources/share-internet-bandwidth-for-money/); [Honeygain comparison blog](https://www.honeygain.com/blog/bandwidth-sharing-apps/)

**Packetshare**
- Pays $0.20 per GB with PayPal or USDT cashout and a $20 minimum. A promotional article claims the minimum is now $10. The USDT network is not stated. — [Honeygain ES comparison](https://www.honeygain.com/es/blog/app-para-compartir-ancho-de-banda/); [TechBullion (promotional)](https://techbullion.com/?p=478739)

**Bitping**
- Payouts were consolidated to Solana in Jan 2025, dropping BSV and BCH. The wallet needs a rent-exempt SOL minimum. Internal accounting is still in BSV, so earnings fluctuate. — [Bitping blog](https://bitping.com/blog/bitpings-solana-transition); [Bitping help: Solana payouts](https://bitping.com/help/nodes/troubleshooting-solana-payouts); [Bitping help: earnings](https://bitping.com/help/nodes/how-earnings-work)
- A third-party estimate puts earnings at $0–5 per month per device. — [CashPilot guide](https://raw.githubusercontent.com/GeiserX/CashPilot/HEAD/docs/guides/bitping.md)

**Sentinel dVPN (P2P token)**
- Hosts earn P2P for bandwidth used by dVPN apps, either through subscription plans (hourly rate for uptime) or pay-as-you-go (per GB or hour at host-set USD-pegged prices via an oracle). Payout is hourly, split 80% to the host and 20% to P2P stakers. Payments are "substantially subsidized". — [Sentinel docs: earnings](https://docs.sentinel.co/dvpn-nodes/earnings)
- The P2P token hit its all-time low on 4 Aug 2026. — [CoinMarketCap](https://coinmarketcap.com/currencies/sentinel/)

**Grass**
- Stage 2 rewards (Epochs 1–19, 14 Oct 2024 – 8 Jun 2026) are claimable from 22 Jul 2026 until 22 Jan 2027. — [Grass Stage 2 rewards guide](https://youmind.com/es-419/landing/x-viral-articles/grass-stage-2-rewards-guide)
- Community guides say devices on the same network split rewards. No official terms on operator or multi-user use were retrieved. — [aipure guide](https://aipure.ai/articles/how-to-use-grass-maximize-earnings-with-bandwidth-sharing)

**Security and antivirus context**
- Trend Micro observed 2022 exit-node traffic from Honeygain, Traffmonetizer, Peer2Profit, PacketStream and IPRoyal Pawns for nine months. It found suspicious activity such as SQL-injection probing and access to SMS-verification services. — [Trend Micro research](https://www.trendmicro.com/en_us/research/23/b/hijacking-your-bandwidth-how-proxyware-apps-open-you-up-to-risk.html)
- Cisco Talos documented malware silently installing proxyware such as Honeygain and Nanowire. — [TechRadar on Talos](https://www.techradar.com/news/this-devious-cyberattack-might-be-selling-off-your-internet-bandwidth)
- Recent coverage still flags "widely promoted proxyware platforms" as a user risk. — [CyberInsider](https://cyberinsider.com/widely-promoted-proxyware-platforms-put-users-at-risk/)

### Inferences
- Traffmonetizer is the cleanest crypto rail among proxyware apps: USDT-TRC20 swaps natively to BTC on THORChain (see next section). However, it is a consumer app, not a publisher SDK. Running one operator account across many users' PCs is likely to conflict with its terms, and per-device earnings would have to be read from its dashboard (an unofficial scrape) rather than an API. Treat it as risky and unverified for an operator model.
- Bitping SOL can be held directly for Otium's SOL payouts or swapped to BTC via Chainflip (UNVERIFIED that Chainflip's SOL→BTC route is live today; not checked this pass). The earnings are tiny.
- Sentinel and Mysterium both require running a dVPN exit node on the user's PC. Sentinel adds token-price risk (P2P at all-time lows) and probably needs open ports or a public IP (UNVERIFIED), which is hard on home NAT.
- All proxy-exit options (Traffmonetizer, Honeygain, Packetshare, Mysterium Public mode) carry the same antivirus and abuse reputation. CDN/relay-style networks avoid this, but per earlier research they mostly pay points.

### Gaps
- Not researched this pass, so their status is unknown (treat as points-only or $0 unless later shown otherwise): Nodepay, Gradient, Bless, Orchid (OXT; search returned nothing), Deeper, NKN, Huddle01, Blockcast, Pipe Network, Titan partner SDK, Repocket crypto, EarnFM, Peer2Profit.
- Traffmonetizer terms (multi-device, reselling, operator use), per-device API, KYC, and any SDK: the official FAQ URL returned 404 and no official source was retrieved.
- JMPT token liquidity and chain: not checked.
- Packetshare USDT network and terms: not found.
- Sentinel P2P liquidity and node setup requirements: not checked.
- No 2026 antivirus-detection data was found for specific SDKs.

## Which options can be swapped to BTC automatically (THORChain/Chainflip or liquid DEX)?

### Takeaway
USDT-TRC20 (Traffmonetizer, possibly Packetshare) is the easiest: THORChain has had native TRON.USDT pools since about Oct 2025, so it swaps straight to native BTC without KYC. MYST needs a DEX hop first (Polygon Quickswap to USDC.e, or Ethereum Uniswap to WETH) and then THORChain, and depth is only about $2.5k at ±2%. SOL (Bitping) is usable directly for SOL payouts.

### Cited Findings
- THORChain completed its TRON integration with native TRX and whitelisted TRC-20 including USDT-TRC20. Users swap from self-custody to BTC, ETH, Base, BSC, AVAX, DOGE, GAIA, LTC, XRP and BCH (Nine Realms/THORChain post, about 1 Oct 2025). — [THORChain blog](https://blog.thorchain.org/tron-integration-complete-native-trx-usdt-swaps-live-on-thorchain)
- A third-party snapshot (undated) lists TRON.TRX and TRON.USDT pools as "Available", with about $208k USDT pool depth and about $2.0M per day of USDT volume. Third-party pages describe the route as USDT→RUNE→BTC with about 0.3% fee. — [deving.zone THORChain TRON page](https://deving.zone/thorchain/blockchains/tron); [Butter Network guide](https://docs.butternetwork.io/butter-swap-user-guide/swap-tutorials/swap-usdt-trc20-to-btc)
- THORWallet added TRX and USDT-TRC20 cross-chain swaps on 16 Dec 2025, routed via THORChain and NEAR Intents. — [THORWallet blog](https://www.thorwallet.org/thorwallet-blog/thorwallet-adds-tron-cross-chain-swaps-powered-by-thorchain-and-near-intents)
- MYST markets: Quickswap Polygon MYST/USDC.e (±2% depth about $2.57k) and Uniswap V3 Ethereum MYST/WETH (±2% depth about $2.5k) on 16 Oct 2026. — [CoinGecko](https://www.coingecko.com/en/coins/mysterium)

### Inferences
- With a pool depth of about $208k, USDT-TRC20 → BTC on THORChain handles operator-sized amounts ($100s–$1,000s) at low slippage. Otium's existing THORChain tooling (from the crypto-payouts commit) could add a TRON.USDT inbound leg. Snapshot depth may have changed.
- MYST → BTC can be automated with a Polygon DEX router call (Quickswap USDC.e), a USDC bridge to Ethereum or Base, and then a THORChain swap. A simpler alternative is selling on MEXC (centralized, so KYC is likely), which defeats the no-KYC goal.
- GRASS, JMPT and P2P conversion paths were not checked. None are THORChain assets, so each would need a DEX hop.

### Gaps
- A live THORChain TRON.USDT pool depth figure as of Oct 2026.
- Whether Chainflip supports TRON or Polygon assets. Not checked.

## Overall recommendation: the 1–2 integrations that best fit a crypto-paid, automated, per-user-credited model

### Takeaway
1. **Primary: Mysterium.** Use the official MystNodes SDK partnership, or bundle the myst node Kryptex-style with an operator beneficiary wallet and B2B-only traffic. It is the only option with native crypto revenue, per-device (per-identity) attribution, official app-integration support, cross-platform claims, and a live precedent (Kryptex converts MYST to BTC and credits users). Accept small per-node income and illiquid MYST, and convert frequently in small tranches via a DEX then THORChain.
2. **Fallback: keep Honeygain SDK (fiat) and buy BTC.** A crypto-native proxyware alternative is Traffmonetizer USDT-TRC20 → THORChain BTC, but only if written permission for operator or multi-user deployment is obtained. It has no SDK or per-device API and carries an exit-proxy reputation.

### Cited Findings
- Kryptex precedent: MYST is converted to BTC at 5 MYST and users withdraw BTC, on Windows 10/11 x64. — [Kryptex getting started](https://www.kryptex.com/en/articles/mysterium-getting-started-en)
- Official SDK client exists and needs a partner token from the MystNodes team; Windows, macOS, Linux and Android are claimed. — [Docker Hub](https://hub.docker.com/r/mysteriumnetwork/mystnodes-sdk-client)
- MYST liquidity is thin (about $26k per day volume, about $2.5k at ±2% depth). — [CoinGecko](https://www.coingecko.com/en/coins/mysterium)
- Traffmonetizer pays USDT-TRC20 ($10 minimum), which THORChain supports natively. — [Jivaro](https://jivaro.net/resources/share-internet-bandwidth-for-money/); [THORChain blog](https://blog.thorchain.org/tron-integration-complete-native-trx-usdt-swaps-live-on-thorchain)

### Inferences
- Realistic Otium economics with Mysterium: about $1–25 per node per month gross (independent versus vendor figures), minus the 20% network fee, gas, and DEX/THORChain slippage. Users should be credited conservatively, after conversion.
- Applying to the MystNodes partner programme is the main next step. Ask about revenue share, single-wallet settlement for many identities, a per-identity earnings API, B2B-only enforcement, country allowlist, KYB, and macOS/Linux SDK builds.
- Running Mysterium alongside Honeygain on the same IP may split traffic. Kryptex already advises one node per IP.

### Gaps
- No primary source confirms partner revenue share or that partners avoid KYC/KYB.
- No verified operator-permitted, crypto-paying proxyware SDK was found. A dedicated pass on Nodepay, Gradient, Bless, Blockcast, Pipe, Titan SDK and Orchid would be needed to rule them in or out.
