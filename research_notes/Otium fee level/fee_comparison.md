# Otium fee level: what comparable apps charge (checked 17 Oct 2026)

## Comparison

"Effective user cost" means the share of what the hardware earns that the user doesn't get. It excludes electricity. Miner dev fees (about 1%) apply to every app that wraps XMRig, SRBMiner, Excavator and similar miners, unless noted.

| App / service | Custodial? | Effective user cost | Source |
|---|---|---|---|
| **NiceHash QuickMiner / NHM** | Yes (NiceHash wallet) | 2% seller fee + miner dev fee + BTC withdrawal (0.00003 BTC on-chain; Lightning 0.2% between 0.00075 and 0.05 BTC) + 0.5% exchange fee if converting. **Roughly 3–4%+** | NiceHash public fee API: `sellFee` 0.02, `exchangeTaker` 0.005 https://api2.nicehash.com/main/api/v2/public/service/fee/info ; fee page last changed 05.05.2026 https://www.nicehash.com/support/general-help/nicehash-service/fees |
| **Kryptex app + Kryptex Pool** | Either: "Kryptex account" (custodial, auto-convert) or mine direct to your own wallet | Pool fee 1% (XMR PPS+; FAQ dated 30 Mar 2026), 1–3% PPS+ on other pools. Network fees covered. Conversion spread is not disclosed. **~2–4% + spread** | https://pool.kryptex.com/articles/about-pool-en ; https://pool.kryptex.com/xmr ; https://www.kryptex.com/articles/unmineable-alternative-en (14 Sep 2026) |
| **Cudo Miner** | Yes | Commission tiered by BTC mined in the last 30 days: 6.5% (<0.005 BTC), 5%, 4%, 3%, 2.5%, 2%, down to 1.5% (≥10 BTC). A single home PC sits in the **6.5%** tier, so about **7.5%** with the dev fee. Third-party figures; the official page returned HTTP 429 | https://us.fitgap.com/products/015202/cudo-miner (official: https://www.cudominer.com/fees/) |
| **Honeyminer** | Yes | Stated 2.5–8% "depending on hardware", plus pool and withdrawal fees. One user reported getting about $35 of $50 (around a 30% effective cut). Service appears defunct | https://cryptonews.com/exclusives/this-app-is-a-new-hit-among-wannabe-miners-2320/ ; https://uk.trustpilot.com/review/honeyminer.com ; https://originstamp.com/blog/what-was-the-honeyminer-cryptocurrency-mining-app |
| **Salad** | Yes (Salad balance, mostly redeemed for gift cards) | Revenue split not disclosed. In 2021 the rewards-store markup averaged **23%** (a $40 gift card cost $50 of balance). That sits on top of an undisclosed mining or container margin | https://getlatka.com/blog/gamers-contribute-gpu-to-this-saas-marketplace-raising-15m-at-70m-valuation-with-5m-in-revenue/ ; https://support.salad.com/article/210-salad-sounds-too-good-to-be-true-whate28099s-the-catch |
| **unMineable (pool/app)** | Semi: holds a balance until the payout threshold, then pays the user's own wallet in the chosen coin | 1% platform fee; 0.75% with a referral code. Conversion spread not disclosed. Kryptex calls it a proxy pool where miners "pay double fee" (competitor claim) | https://unmineable.com/referrals ; https://www.kryptex.com/articles/unmineable-alternative-en |
| **Otium (now)** | **No**: pool pays the user's wallet | 2% mining time + XMRig 1% / SRBMiner 0.85% + pool (unMineable 0.75% or MoneroOcean 0%). **≈3.0% (MoneroOcean) to ≈3.7% (unMineable)** | see below |
| Bandwidth: Honeygain / EarnApp / Pawns | Yes | Not disclosed. Users get $0.10–0.80/GB (average about $0.28/GB). The 2025 median retail price for residential proxies is about $4/GB, so users likely receive roughly **5–20% of retail** (my inference) | https://proxyway.com/research/proxyware-passive-income-apps ; https://plisio.net/de/cybersecurity/residential-proxies (cites Proxyway 2025) |

Not verified: MinerGate's fees and "Easy Miner". I found no current primary source for either.

## Pool and miner reference fees

- MoneroOcean: `pplns_fee: 0`, `dev_donation: 0`. Payout tx fee is at most 0.0004 XMR and drops to zero at a threshold of 4 XMR or more; minimum payout 0.003 XMR. Source: https://api.moneroocean.stream/config . Third-party sites say "1%", which looks out of date. Otium's estimates use 1% as a conservative figure (GpuPolicyService.MoneroOceanFeePercent).
- unMineable: 1% / 0.75% with a code. The referrer earns 0.25%, or up to 0.5% under the custom code programme (https://unmineable.com/referrals).
- Kryptex Pool: 1% PPS+ (XMR); 1–3% PPS+ elsewhere.
- 2Miners 1% PPLNS; HeroMiners 0.9% PPLNS (aggregator: https://miningboard.com/pools/two-miners , https://miningboard.com/pools/hero-miners).
- XMRig: `kDefaultDonateLevel = 1`, `kMinimumDonateLevel = 1` (https://raw.githubusercontent.com/xmrig/xmrig/master/src/donate.h).
- SRBMiner-MULTI: randomx 0.85%, kawpow 0.85%, ethash/etchash 0.65%, others up to 3% (https://github.com/doktor83/SRBMiner-Multi).
- GPU miner dev fees typically sit at 0.65–2% (T-Rex 1%, lolMiner 0.7–1.5%) (https://hiveon.com/forum/t/dev-fee/44447).

## Evidence on fee sensitivity

- NiceHash: reviews frame the 2% fee as making it "slightly less profitable than directly mining". A 2022 r/NiceHash comment put the total at about 3% + dev 1% + pool 1%. Users complain mostly about withdrawal minimums and fees, not the 2% (https://coinbureau.com/review/nicehash ; https://www.mexc.com/news/266793).
- Cudo: SaaSHub notes that its fees "eat into" profitability, "especially those with lower mining power". The tiers charge small users the most (https://www.saashub.com/compare-mining-pool-hub-vs-cudo-miner).
- Honeyminer: the effective cut users perceived (around 30%) plus payout problems led to a TrustScore of 2/5 and the service's decline.
- Kryptex vs unMineable: Kryptex markets against unMineable on stacked fees and spread ("double fee"). Competitors treat the total stack, not the headline fee, as the battleground.
- Dev fees: about 1% is the accepted norm; forum replies treat 0.65–1% as "the price of development".
- Inference: 1–2% layered fees are tolerated. Complaints start when the total exceeds about 5%, when the fee is opaque (spread, rewards-store markup), or when withdrawals are costly.

## Recommendation (summary)

- Range 1.5–2.5%. Keep **2%**. Don't go above 3%, because the total stack would then exceed NiceHash's.
- Keep the fee as mining time. It is non-custodial, auditable, needs no balance or conversion, matches XMRig's donate-level convention, and the user pays nothing while Otium is paused for electricity cost.
- Show the full stack in the welcome screen, the settings and the earnings view: Otium 2% (≈29 min/day) · miner 1% · pool 0–0.75% · total ≈3–3.7%. Disclose that unMineable pays Otium 0.25–0.5% out of its own fee, and that the user pays 0.75% instead of 1% as a result.
- Optional: a 1% tier for users who opt into bandwidth sharing, or a 1.5% floor for low-hashrate machines. Don't use Cudo-style tiers that charge small users more.
