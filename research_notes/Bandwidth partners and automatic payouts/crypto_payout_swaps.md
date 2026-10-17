# Automatic crypto payouts via no-account / no-KYC swap services and on-chain protocols (as of 16 Oct 2026)

Reference prices used for USD conversions below: BTC = $81,516 (mempool.space price API, fetched 2026-10-16) — [mempool.space prices](https://mempool.space/api/v1/prices). Implied (derived, not independently sourced) prices from live quotes the same day: SOL ≈ $107 (SideShift BTC→SOL rate 764.8 SOL/BTC), BNB ≈ $725 and ETH ≈ $2,440 (back-calculated from THORChain quote outputs). Treat the derived prices as approximate.

## Q1. Which on-chain protocols can do XMR→X or BTC→X automatically, with no account (THORChain, Chainflip, Maya)?

### Takeaway
On 16 Oct 2026 there is **no live XMR pool on THORChain mainnet** (no XMR entry in the live pools or inbound_addresses endpoints), despite news that v3.20 (Aug 2026) "enabled" Monero; THORChain is fully usable today only for **BTC → ETH / BNB(BSC) / USDC / USDT / Base-ETH**, while **SOL trading is currently halted** on THORChain. Chainflip covers BTC→ETH/SOL/USDC (no XMR, no BNB); Maya has no XMR or SOL. So an XMR source must go through a centralized instant-swap service (or be converted to BTC first), whereas BTC→target can be done permissionlessly via THORChain (and Chainflip for SOL).

### Cited Findings
**THORChain — live state (fetched 2026-10-16 from the public Liquify gateway)**
- Live `/thorchain/pools` lists 44 pools; **no XMR pool exists**. Available: BTC.BTC, ETH.ETH, BSC.BNB, BASE.ETH, BASE.USDC, BSC.USDC, BSC.USDT, ETH.USDC, ETH.USDT, TRON.USDT, AVAX.USDC/USDT, SOL.SOL, ZEC.ZEC, etc. SOL.SOL and ZEC.ZEC show `trading_halted: true`. — [THORNode pools (Liquify gateway)](https://gateway.liquify.com/chain/thorchain_api/thorchain/pools)
- Live `/thorchain/inbound_addresses`: chains AVAX, BASE, BCH, BSC, BTC, DOGE, ETH, GAIA, LTC, SOL, TRON, XRP, ZEC; **SOL and ZEC are halted / chain trading paused; no XMR chain**. Outbound fees (1e8 units): BTC 1,007 sats; BSC 34,493 (=0.000345 BNB); ETH 10,230 (=0.000102 ETH); BASE 10,233; SOL 196,188. Dust thresholds: BTC 1,000 sats, ETH 1,000, BSC 10,000. — [THORNode inbound_addresses](https://gateway.liquify.com/chain/thorchain_api/thorchain/inbound_addresses)
- Monero timeline: THORChain blog (Apr 2026) "XMR is coming… in 1-2 months"; a May 2026 post says a developer demoed live Monero on THORChain with 7 nodes and real funds; on 30 Jul 2026 the blog said Monero code is "essentially ready" but churn is the blocker, ZEC likely goes first, Monero will be a labelled-beta "soft launch", new pools are typically seeded with ~$100k from treasury. — [THORChain blog: XMR is coming](https://blog.thorchain.org/xmr-is-coming-monero-on-thorchain-in-1-2-months); [THORChain blog: JP demos live Monero](https://blog.thorchain.org/jp-demos-live-monero-on-thorchain-7-nodes-real-funds-first-swap-works); [THORChain blog: Monero soft launch / v3.20 testing (30 Jul 2026)](https://blog.thorchain.org/blog/monero-gets-a-soft-launch-as-thorchains-v320-enters-testing-and-adr31-passes)
- Conflicting press: Crypto Briefing / CoinMarketCap stories claimed THORChain v3.20 (25 Aug 2026) "enables native Monero swaps"; but a THORWallet press release of 5 Oct 2026 says Monero on THORChain "is currently being tested and audited", and CoinMarketCap's THORChain roadmap still lists "Monero & Zcash Mainnet Integration (Q4 2026)". — [Crypto Briefing](https://cryptobriefing.com/thorchain-native-monero-swaps-xmr-boost/); [Cointelegraph PR: THORWallet integrates Monero](https://cointelegraph.com/press-releases/thorwallet-integrates-monero-bringing-native-xmr-support-to-the-app); [CoinMarketCap THORChain updates](https://coinmarketcap.com/cmc-ai/thorchain/latest-updates/). The live pool data above contradicts the "launched" claims.
- **How to call THORChain programmatically**: GET `/thorchain/quote/swap?from_asset=BTC.BTC&to_asset=ETH.ETH&amount=<1e8 units>&destination=<addr>` (optional `refund_address`, `streaming_interval`, `streaming_quantity`, `liquidity_tolerance_bps`, `affiliate`, `affiliate_bps` 0–1000). Response gives `inbound_address`, `memo`, `expiry` (~10 min), `fees` {liquidity, outbound, affiliate, total, total_bps, slippage_bps}, `recommended_min_amount_in`, `dust_threshold`, `total_swap_seconds`. Memo format `=:ASSET:dest:limit/interval/quantity`; custom refund `=:BTC.BTC:dest/refund_addr`. Do not cache inbound addresses; never send after expiry. Quote endpoint rate limit: 1 req/s per IP. No API key or account. — [THORChain dev docs: Quickstart](https://dev.thorchain.org/swap-guide/quickstart-guide.html)
- **Live quote example 1 (2026-10-16)**: 20,000 sats BTC (~$16.30) → BSC.BNB: expected out 0.02211352 BNB; fees outbound 0.00034484 BNB + liquidity 0.00004498 BNB = total 170 bps (~$0.28); `recommended_min_amount_in` 6,021 sats (~$4.91); `total_swap_seconds` 606; memo `=:s:<addr>` (short-code). — [THORChain quote BTC→BNB](https://gateway.liquify.com/chain/thorchain_api/thorchain/quote/swap?from_asset=BTC.BTC&to_asset=BSC.BNB&amount=20000&destination=0x0000000000000000000000000000000000000001)
- **Live quote example 2**: 10,000 sats (~$8.15) → ETH.ETH: out 0.00322798 ETH; outbound 0.00010227 ETH + liquidity 0.00000667 = total 316 bps (~$0.26); recommended min 6,021 sats; ~606 s. — [THORChain quote BTC→ETH](https://gateway.liquify.com/chain/thorchain_api/thorchain/quote/swap?from_asset=BTC.BTC&to_asset=ETH.ETH&amount=10000&destination=0x0000000000000000000000000000000000000001)
- BTC network fee to make the inbound deposit is currently trivial: mempool.space recommended fees on 2026-10-16 were 1 sat/vB for all tiers. — [mempool.space fees](https://mempool.space/api/v1/fees/recommended)

**Chainflip**
- Native swaps among BTC, ETH (+major ERC-20s) and SOL (+SPL tokens); integrator claims fees of ~1–10 bps (third-party, unverified). — [SwapKit: Chainflip](https://swapkit.dev/chainflip-2/)
- Broker API method for programmatic deposit channels: `broker_request_swap_deposit_address` (plus `broker_get_open_deposit_channels`, `broker_request_swap_parameter_encoding`); Vault-swap API covers Bitcoin, EVM, Solana, Tron. Registration as broker required for own broker; JS SDK exists. Docs index did not list XMR or BNB. — [Chainflip Broker API docs](https://docs.chainflip.io/brokers/broker-api)
- Protocol has `set_minimum_swap_amount` and `register_as_broker`; audit flagged lack of broker-fee limits. — [Zellic audit: cf-swapping](https://reports.zellic.io/publications/chainflip-backend/sections/3-cf-swapping-pallet-cf-swapping-title)
- LI.FI changelog notes Chainflip was temporarily disabled and fixed via SDK v2.1.1 in early 2026. — [LI.FI changelog](https://docs.li.fi/changelog/backend)
- Third-party "broker-as-a-service" MCP connectors exist for Chainflip (lists BTC, ETH, SOL, DOT, Tron, Arbitrum) — wrapper claim, not authoritative. — [Glama: Chainflip Broker as a Service](https://glama.ai/mcp/connectors/io.chainflip-broker/crypto-swap)

**Maya Protocol**
- Supports native swaps among BTC, ETH, BNB Smart Chain, Dash, Kujira, Arbitrum, Zcash, Radix (no XMR, no SOL listed). — [SwapKit: Maya](https://swapkit.dev/maya-protocol/)

### Inferences
- XMR→target via a decentralized protocol is not possible today; plan for a two-step route if decentralization is wanted: XMR→BTC via a centralized swap service (or sell XMR elsewhere), then BTC→target via THORChain. Monero on THORChain may arrive as a small-liquidity beta (~$10k–$100k pool) — fine for $2–$50 swaps once live, but expect halts.
- THORChain's outbound fee appears to be roughly ~$0.25 per payout regardless of chain (BNB 0.000345, ETH/Base 0.000102 at today's prices), plus liquidity fee ~0.2–0.3% at these sizes. For a $5 payout that's ~5–6%; for $16 ~1.7%; for $50 ≈ 0.7%. Below ~$5 (recommended_min ≈ 6,021 sats) THORChain will refuse.
- Each THORChain BTC swap costs one BTC on-chain transaction from the operator wallet and ~10 minutes; sending to the user's address directly as `destination` matches the preferred design. Using `refund_address` = operator wallet keeps refunds automatic.
- SOL halts on THORChain mean SOL payouts should use Chainflip or a centralized service as fallback.

### Gaps
- Could not confirm exact Chainflip minimum swap sizes, the 0.05%/broker fee schedule, or whether a public no-registration broker endpoint suitable for server use exists (docs pages 404'd in fetch).
- Maya's own docs not fetched; whether Maya supports USDC on cheap chains not confirmed.
- The THORChain `memo` `=:s:` / `=:e:` short codes are from the live quote; full asset short-code list not verified.

## Q2. Centralized instant-swap services & aggregators: API, auth, pairs, min/max, fees, quote validity, refunds

### Takeaway
SideShift.ai is the cleanest fit for server automation: an anonymous "account" (just a generated ID + private key, creatable via API, no KYC), documented REST v2 with fixed quotes locked 15 min, refund addresses, low minimums (BTC→SOL min ≈ $3 on 16 Oct 2026), integrator commission 0–2%; but it forbids proxying and requires the end user's IP in `x-user-ip`, and is geo-restricted. FixedFloat requires a registered account for API keys and has a documented "freeze until source of funds" policy. Trocador (aggregator over many exchanges with A–D KYC ratings) has the best no-KYC posture but its docs were unreachable during research. LetsExchange stopped new swaps on 1 Oct 2026.

### Cited Findings
**SideShift.ai**
- Auth: account Private Key in `x-sideshift-secret`; Account ID as `affiliateId`. "You need an account" but it can be created via `POST /v2/create-account` (no KYC mentioned); one account per integration; end users identified by `shiftId`. Flow: `POST /v2/quotes` → `POST /v2/shifts/fixed` (quote id + settle address) → returns `depositAddress`; poll `GET /v2/shifts/{shiftId}`. Shift ID is sensitive (holder can change refund address if none set). — [SideShift API docs: Getting started](https://docs.sideshift.ai/)
- Fixed-rate quotes lock rate and fees for 15 minutes; deposits after expiry are refunded. Variable shifts valid 7 days, rate set at deposit. `/v2/pair` with `amount` shows how network fees affect rate; `settleCoinNetworkFee`/`networkFeeUsd` fields. — [SideShift docs: Shift](https://docs.sideshift.ai/api-intro/shift)
- Rate limits (per IP): create shift 5/min (fixed+variable combined); create quote 20/min; HTTP 429 when exceeded. — [SideShift docs: Rate limits](https://docs.sideshift.ai/api-intro/rate-limits)
- Integrator commission default 0.5%, adjustable 0–2% via `commissionRate`, embedded in rate; payouts automatic at thresholds (examples $5,000/$20,000). — [SideShift docs: Monetization](https://docs.sideshift.ai/api-intro/monetization)
- "SideShift.ai is not available in certain jurisdictions"; integrators should call `/v2/permissions`; proxied requests not allowed — the server must set `x-user-ip` to the end user's IP; country list not on the page. — [SideShift docs: Permissions](https://docs.sideshift.ai/api-intro/permissions)
- Min/max are USD-denominated; min set after network fees; refunds automatic if `refundAddress` set; wrong-coin deposits usually recovered within 3 days. — [SideShift FAQ](https://docs.sideshift.ai/faq)
- Live (2026-10-16) `GET /v2/pair/btc-bitcoin/sol-solana`: min 0.00003671 BTC (≈ $2.99), max 0.73415454 BTC (≈ $59.8k), rate 764.835 SOL/BTC. — [SideShift pair BTC→SOL](https://sideshift.ai/api/v2/pair/btc-bitcoin/sol-solana)
- Live `GET /v2/permissions` from the research fetcher (US-hosted per tool description) returned `{"createShift":true}`. — [SideShift permissions](https://sideshift.ai/api/v2/permissions)
- XMR pair queries (`xmr-monero/eth-ethereum`, `xmr-monero/bnb-bsc`) returned HTTP 500 on 2026-10-16 — XMR availability on SideShift could not be confirmed. — [SideShift pair XMR→ETH](https://sideshift.ai/api/v2/pair/xmr-monero/eth-ethereum)

**FixedFloat (ff.io)**
- API requires an account: register, then generate API Key + Secret; headers `X-API-KEY`, `X-API-SIGN` (HMAC-SHA256 of JSON body). Rate limit 250 weight/min; `/api/v2/create` costs 50 (≈5 orders/min), others 1. Endpoints: `ccies`, `price`, `create`, `order`, `emergency` (EXCHANGE or REFUND choice), `setEmail`, `qr`; public `fixed.xml`/`float.xml` rate feeds with min/max. Fees: fixed 1%, float 0.5% (base service fee). Affiliate via `refcode`/`afftax`. — [FixedFloat API](https://ff.io/api)
- ToS (per KYCnot.me review, 3 Sep 2026): funds may be frozen "until information about the source and legality of the funds is received"; refunds only after verification and only to the sending address; US citizens/residents, Georgia, and UN-sanctioned countries restricted; company re-registered Marshall Islands → Georgia on 20 Jul 2026; HTX-origin funds suspended Jun–Aug 2026. KYC level 3/4 "Shotgun KYC". — [KYCnot.me: FixedFloat](https://kycnot.me/service/fixedfloat)

**Trocador (aggregator)**
- API: COINS method returns coins with ticker, network, min/max; NEW_RATE takes both tickers+networks+amount; NEW_TRADE takes rate ID, chosen exchange and rate type (fixed/floating) and returns the exchange's deposit address. Companies register for API/Widget access. — [Trocador affiliate registration](https://trocador.app/en/register/affiliate); [Trocador affiliate program](https://trocador.app/en/affiliate/)
- Optional `min_kycrating` (A/B/C/D) and `min_logpolicy` (A/B/C) filters and a `payment` (fixed-output) mode — documented on a mirror domain (troccador.app), not verified on official domain. — [Trocador docs mirror](https://www.troccador.app/en/docs/); AnonPay docs also list `min_kycrating` (A/B/C) — [Trocador AnonPay docs](https://trocador.app/anonpaydocumentation)
- KYCnot.me: level 0/4 ("Never KYC" per ToS) but "KYC depends on partners"; no registration; API available; onion URL; score 8/10; reviewed 29 Sep 2026. — [KYCnot.me: Trocador](https://kycnot.me/service/trocador)
- Third-party claim: 50% revenue share to affiliates paid in XMR (unverified). — [payyd.co Trocador review](https://payyd.co/gateways/trocador)
- Official docs page `trocador.app/en/docs/` returned HTTP 503 on 2026-10-16. — [Trocador docs](https://www.trocador.app/en/docs/)

**ChangeNOW**
- API uses `x-changenow-api-key` header with partner accounts; endpoints min-amount, estimated-amount, create exchange, status; fixed-rate `rateId` (details not retrieved). — [ChangeNOW API (Postman)](https://documenter.getpostman.com/view/8180765/SVfTPnM8)
- ToS: may freeze an exchange "for any period of time necessary to complete the investigation and AML/KYC procedure". — [ChangeNOW Terms](https://changenow.io/terms-of-use/changenow-terms); [ChangeNOW KYC/AML FAQ](https://changenow.io/faq/kyc-aml-procedure)
- KYCnot.me: level 3/4 "Shotgun KYC" (lowered from 4 on 22 Jul 2025); flagged swaps frozen until Sumsub verification; KYC reportedly enforced before swaps for EU, US, UK, Canada, Australia users (community-contributed, unverified); score 4/10. — [KYCnot.me: ChangeNOW](https://kycnot.me/service/changenow)
- A 2026 competitor review says ChangeNOW verifies above €2,000, on privacy-coin swaps, or "suspicious" flags; another says KYC hits ~0.5% of swaps (platform claim). Competitor sources — treat with caution. — [GoDEX comparison 2026](https://godex.io/blog/changenow-vs-godex-vs-stealthex-best-no-kyc-crypto-exchange-2026-test); [Coinpaprika review](https://coinpaprika.com/education/changenow-review-non-custodial-instant-crypto-exchange-explained/)

**Exolix**
- KYCnot.me level 3/4; flagged deposits may require source of funds, otherwise swap and refund withheld; free pre-deposit AML score check via support; **unresolved high-severity incident (May 2026): API flaw exposed ~355,000 swap records** (addresses, amounts); API available; reviewed 13 Sep 2026. — [KYCnot.me: Exolix](https://kycnot.me/service/exolix)

**StealthEX**
- KYCnot.me level 3/4; flagged deposits held, may demand ID, selfie, source of funds; for high-risk flags refusing verification means "funds will not be returned"; some countries restricted; API available; reviewed 13 Sep 2026. — [KYCnot.me: StealthEX](https://kycnot.me/service/stealthex)

**SimpleSwap**
- KYCnot.me (community, unverified) level 3/4, score 3/10; deposits screened, flagged ones may require KYC/SoF; Marshall Islands registration. — [KYCnot.me: SimpleSwap](https://kycnot.me/service/simpleswap)

**LetsExchange**
- Stopped accepting new swaps 1 Oct 2026; support until 30 Nov 2026, citing regulatory complexity. — [CryptoDaily](https://cryptodaily.co.uk/2026/10/letsexchange-has-shut-down-so-which-alternatives-offer-the-experience-its-users-valued); [KYCnot.me: LetsExchange (archived)](https://kycnot.me/service/letsexchange)

### Inferences
- For per-cash-out automation, SideShift's 5 shifts/min/IP limit is ample at Otium's scale, and its fixed-quote + `refundAddress` + status polling maps exactly onto "quote → send XMR/BTC → user receives coin". The `x-user-ip` requirement means the server must forward each user's IP (privacy-policy disclosure needed) and per-user geo-blocking will apply to Otium users in restricted countries — check `/v2/permissions` with the user's IP before offering it.
- Whether SideShift's ToS allows an operator paying its own funds to many third-party addresses (vs. user-initiated swaps) is unclear; the `x-user-ip` rule suggests SideShift expects the end user to be the economic actor. Risk of account/IP blocks if the pattern looks like a payout business — ask SideShift directly.
- FixedFloat, ChangeNOW, StealthEX, Exolix, SimpleSwap all reserve the right to freeze and demand KYC/SoF. Because Otium's operator would be the depositor every time, a single AML flag would hit the operator (not the user) — workable only if the operator is willing to pass KYC once. XMR deposits are the most likely to be flagged (privacy-coin rules mentioned for ChangeNOW).
- Trocador is attractive as a single integration that can filter to A-rated (no-KYC-ever) providers and fall back automatically, but its API docs availability (503) and reliance on partners mean adding a second provider is prudent.

### Gaps
- Exact current KYC rating definitions on Trocador (A = never KYC / D = may hold funds indefinitely, from prior knowledge) — **unverified**; docs unreachable.
- Trocador API auth header name (prior knowledge: `API-Key` header to `api.trocador.app`) — **unverified**.
- SideShift's restricted-country list (prior knowledge suggests US and sanctioned countries, possibly others) — **not found in sources**; SideShift terms URL 404'd. Whether Norway/EEA is restricted: no source found.
- SideShift KYC policy for flagged deposits: no primary source found; KYCnot.me SideShift page 404'd.
- XMR support and XMR minimums on SideShift, ChangeNOW, Exolix, StealthEX, SimpleSwap as of Oct 2026: not retrieved (SideShift XMR endpoints errored).
- Exolix, StealthEX, SimpleSwap, ChangeNOW API specifics (auth, min amounts, rate limits) not retrieved from primary docs.
- No primary-source 2025–2026 Reddit threads were retrieved on freeze frequency; evidence is from KYCnot.me listings and competitor blogs.

## Q3. Source-side alternative: pools paying mining output directly in BTC/ETH/BNB/SOL (unMineable)

### Takeaway
unMineable mines RandomX (XMR, CPU) and many GPU algorithms and pays out daily in 80+ coins including BTC, ETH (ETH/Arbitrum/BEP20), BNB (BSC), SOL (Solana/BEP20) and USDT (BEP20/ERC20/TRC20) — no USDC; SOL minimum is 0.05 SOL (~$5) and payouts claim "no network fees", but its pool fee is ~1% and it is a proxy pool. Its public stats API and multi-worker aggregation per address could not be confirmed from primary sources.

### Cited Findings
- Payout coins/networks: BTC (Bitcoin); ETH (Arbitrum, BEP20, ETH); BNB (BSC); SOL (BEP20, Solana); USDT (BEP20, ERC20, TRC20); USDC not listed. — [unMineable coins](https://www.unmineable.com/coins)
- SOL: minimum payout 0.05 SOL on both Solana and BEP20; "Payments are sent automatically once a day with no network fees"; supported algorithms include RandomX (XMR, CPU), Etchash, Ethash, Autolykos, KHeavyHash, SHA-256, Scrypt, Equihash, Octopus, Blake3, etc. — [unMineable SOL page](https://www.unmineable.com/coins/SOL)
- Competitor (Kryptex) states unMineable charges 1% PPS and is a proxy pool ("miners pay double fee"); minimum payouts 1.5 USDT and 0.00075 BTC, no Lightning. Biased source. — [Kryptex: unMineable alternative](https://www.kryptex.com/de/articles/unmineable-alternative-en)
- unMineable's own Monitor app supports attaching legacy addresses to an Account and shows active workers per address. — [unMineable Monitor app (App Store)](https://apps.apple.com/app/id6444838563)

### Inferences
- Using unMineable with one operator address per payout coin (e.g., `SOL:<operatorAddr>.<workerId>`) would let the operator receive SOL/BNB/BTC directly and then pay users from hot wallets — removing the swap step but adding a second 1% fee layer vs. MoneroOcean and no per-user direct payouts (users would still be paid from operator wallets).
- 0.00075 BTC min (~$61) means BTC accrual via unMineable is slow for small fleets; BNB/SOL thresholds are lower.

### Gaps
- unMineable referral discount (prior knowledge: 0.75% with referral code) and current minimums for BTC/ETH/BNB — **not verified**.
- Public per-worker stats API (prior knowledge: `api.unminable.com/v4/address/...`) — **not verified**; no docs found.
- Whether unMineable requires KYC or limits worker counts per address: no source found.

## Q4. Automated address sanctions screening (free/cheap) and chain coverage

### Takeaway
The Chainalysis on-chain sanctions oracle is now deprecated ("no longer supported or maintained", page updated 18 Mar 2026) though contracts remain deployed on Ethereum, BSC, Base and other EVM chains; the free Chainalysis sanctions API exists (launched 2022, key-based) but its current endpoint details weren't retrievable. The simplest free option for BTC/ETH/BSC/SOL is the nightly-generated OFAC SDN address list from the open-source 0xB10C repo.

### Cited Findings
- Chainalysis sanctions oracle: "no longer supported or maintained", not recommended for new or production use (page updated 18 Mar 2026). Contract `0x40C57923924B5c5c5455c48D93317139ADDaC8fb` on Ethereum, Polygon, BNB Smart Chain, Avalanche, Optimism, Arbitrum, Fantom, Celo, Blast; `0x3A91A31cB3dC49b4db9Ce721F50a9D076c8D739B` on Base. — [Chainalysis oracle docs](https://go.chainalysis.com/chainalysis-oracle-docs.html)
- Free Chainalysis sanctions API launched March 2022 alongside the oracle; users obtain an API key; covers addresses on US, EU and UN sanctions lists. — [FXStreet (Mar 2022)](https://www.fxstreet.com/cryptocurrencies/news/chainalysis-launches-free-sanctions-screening-tools-202203110428); [Dynamic docs: Chainalysis integration](https://www.dynamic.xyz/docs/overview/security/address-screening)
- The old public docs URL now redirects to Chainalysis support; Chainalysis's marketing page only offers paid Address Screening ("request a demo"). — [Chainalysis free tools page](https://www.chainalysis.com/free-cryptocurrency-sanctions-screening-tools/)
- 0xB10C/ofac-sanctioned-digital-currency-addresses: extracts SDN-listed addresses per asset incl. XBT, ETH, XMR, USDC, USDT, TRX, ARB, BSC, SOL; lists regenerated nightly at 0 UTC via GitHub Actions; TXT or JSON; MIT, no warranty. — [GitHub: 0xB10C OFAC list](https://github.com/0xB10C/ofac-sanctioned-digital-currency-addresses)

### Inferences
- Practical design: pull the 0xB10C `lists` branch daily, check BTC/ETH(EVM, shared across ETH/BSC/Base)/SOL payout addresses before every cash-out; optionally also call the free Chainalysis API if a key can still be obtained. EVM addresses are chain-agnostic, so the ETH list effectively covers BSC/Base as well.
- SDN-only screening does not catch "risk exposure" (mixers, hacks) that swap services' AML engines flag, so operator-side screening reduces but doesn't remove freeze risk at swap providers.

### Gaps
- Current Chainalysis free API endpoint, header, rate limits and whether new keys are still issued in 2026 — not retrieved.
- Whether the 0xB10C repo is still updated in Oct 2026 (last commit date not visible).

## Q5. Paying users directly from operator hot wallets: cheapest methods and .NET libraries

### Takeaway
Fees are low in Oct 2026: Bitcoin mempool at 1 sat/vB, and THORChain's own outbound fee settings imply ~$0.25 per payout on ETH L1, Base and BSC — Base/BSC are usually far cheaper in practice for direct wallet sends. Solana has a fixed base fee per signature. For .NET, Solnet (Solana) is still on NuGet (8.x) but its repo activity looks slow; SolSharp is an alternative.

### Cited Findings
- BTC recommended fees on 2026-10-16: fastest/halfHour/hour/economy/minimum all 1 sat/vB. — [mempool.space fees](https://mempool.space/api/v1/fees/recommended)
- THORChain's live gas rates used for its outbound sends: BTC 3 sats/byte, ETH 15 gwei, BSC 1 gwei, BASE 150 mwei, SOL 7,500 lamports (these are THORChain's conservative settings, not market minimums). — [THORNode inbound_addresses](https://gateway.liquify.com/chain/thorchain_api/thorchain/inbound_addresses)
- Solnet (.NET Solana) packages: Solana.Programs/Extensions 8.7.0, Solana.Rpc 8.3.0, maintained by Bifrost, target .NET 8; repo index shows last push 2025-03-19; SolSharp 3.0.0 is an alternative. — [NuGet Solana.Programs](https://www.nuget.org/packages/Solana.Programs); [NuGet Solana.Rpc](https://www.nuget.org/packages/solana.rpc); [awesome.ecosyste.ms: solnet](https://awesome.ecosyste.ms/projects/github.com%2Fbmresearch%2Fsolnet); [NuGet SolSharp 3.0.0](https://www.nuget.org/packages/SolSharp/3.0.0)

### Inferences
- At 1 sat/vB, a single-input/single-output native SegWit BTC payment (~140 vB) costs ~140 sats ≈ $0.11; batching many payouts into one tx (one output ~31–43 vB each) brings marginal cost to cents — batching daily/hourly is attractive for BTC. (Calculation, not sourced.)
- For ETH, offering Base (or Arbitrum) instead of L1 avoids L1 gas spikes; THORChain itself supports BASE.ETH and BASE.USDC pools, so a BTC→Base-ETH/USDC swap is available programmatically.
- Bulk-swap-then-hot-wallet design: periodic large swaps (e.g., weekly XMR→BTC/BNB/SOL/USDC) cut per-swap minimum/flat fees and AML-touchpoints, then user payouts are plain transfers costing cents on BSC/Base/Solana.

### Gaps
- Live ETH L1, BSC and Base gas prices for Oct 2026 not retrieved; Solana base fee (prior knowledge: 5,000 lamports per signature, plus optional priority fee) not verified from a source here.
- NBitcoin (BTC) and Nethereum (ETH/BSC/Base) maintenance status in 2026 not checked (both widely used per prior knowledge — unverified).
