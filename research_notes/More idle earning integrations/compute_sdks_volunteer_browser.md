# Idle-compute monetization SDKs, volunteer computing, and browser DePIN for a desktop app publisher (Otium), as of Oct 2026

Context: Otium (C#/.NET 10 Avalonia, Windows/Linux/macOS) already ships XMRig (CPU), SRBMiner (GPU) and the Honeygain bandwidth SDK. These notes cover other ways an operator could embed something that uses users' idle CPU/GPU, get paid as the operator, and credit users. Research date: 2026-10-16. About 23 searches and fetches. Many vendor pages give no dates or rates, and those gaps are flagged below.

## Charity Engine: is there an SDK or partner program for publishers?

### Takeaway
I found no evidence of a current Charity Engine SDK or publisher revenue-share program. In 2026 Charity Engine shows itself as two things: a volunteer BOINC app that pays only through prize draws plus donations to charities, and a commercial "Use the engine" compute, storage, web-crawling and blockchain marketplace sold to buyers. It does not look like a route for a third-party publisher to get paid.

### Cited Findings
- The charityengine.com homepage (2026 copyright) has two sections. "Use the engine" sells "affordable, green computing at global scale": Computing, Storage, Web Crawling and Blockchain through a marketplace, with no prices shown. "Join the engine" asks people to donate spare computing power. The page names no SDK, partner, publisher or OEM program. — [Charity Engine homepage](https://www.charityengine.com/)
- The volunteer page says "every CPU hour donated earns a draw entry" and that profits are shared with charities such as Oxfam and Care. It gives no prize amounts and no supported platforms. The footer reads "Powered by GridRepublic". There is no partner or bundling program and no mention of Gridcoin. — [Charity Engine volunteer page](https://www.charityengine.com/volunteer)
- Background: Charity Engine is a free PC app based on BOINC, run by The Worldwide Computer Company Ltd. It sells spare home compute to universities and corporations and splits the profits between partner charities and cash prize draws. When no buyers are present, it donates the compute to volunteer BOINC projects. — [Wikipedia: Charity Engine](https://en.wikipedia.org/wiki/Charity_Engine)
- Older coverage (2012–2015) says profits were split three ways, between the company, charities and prize winners, and that Wolfram Mathematica was a major compute client. — [Wikipedia](https://en.wikipedia.org/wiki/Charity_Engine); [Fast Company](https://www.fastcompany.com/2679116/charity-engine-the-ethical-supercomputer-that-can-win-you-10000); [UK Fundraising 2012](https://fundraising.co.uk/2012/05/10/charity-engine-donating-spare-computing-power-good/)

### Inferences
- The only operator role at Charity Engine is as a compute buyer, not a seller. Users who install Charity Engine are paid in lottery entries, not cash, so Otium cannot capture revenue per CPU-hour from it.
- Any OEM or bundling arrangement would be a private business-development deal. Nothing public shows one exists.

### Gaps
- No public SDK documentation, revenue rates, platform list (Linux/macOS), or partner terms. Confirming whether a private bundling or partner deal exists would mean contacting Charity Engine or GridRepublic directly, which I did not do because the brief excludes forms and sign-ups.

## Other compute monetization SDKs for publishers (Bright SDK, Massive, Kryptex, Salad, Golem, others)

### Takeaway
Every live, publisher-paying "idle resource" SDK I could verify is bandwidth or web-data based (Bright SDK, Massive, Infatica, PacketSDK, Honeygain). None is a pure idle-CPU/GPU compute SDK that pays the publisher. Massive's docs mention "running simulations" and Bright SDK checks CPU before running, but neither publishes a compute-only product or rate. Kryptex and Salad have no publisher SDK or white-label program I could find. For a desktop .NET publisher, the realistic additions are more bandwidth or web-indexing SDKs on Windows, not a new compute revenue stream.

### Cited Findings

**Bright SDK (Bright Data)**
- Bright Data pays the app vendor a monthly fee per opted-in user, with ads as the alternative for users who decline. The SDK runs only when three conditions hold: the device is idle, it is online, and it is on power or above 60% battery. Bright Data says it is "currently interested in working with Windows and Android Applications". macOS and Linux are not mentioned. The post was published 2019-02-14 and last modified 2026-05-24. No rates are given. — [Bright Data blog: Bright Data Network SDK](https://brightdata.com/blog/why-brightdata/bright-data-network-sdk)
- The bright-sdk.com page (2026) says publishers get a "fixed daily rate" on top of ad revenue and advertises an "Extra $200 CPM" headline. It says the SDK "only operates when there is enough CPU, memory and battery". It has a configurable, translated opt-in screen and claims to be GDPR/CCPA compliant. It claims approval in the Apple, LG, Huawei and Samsung stores, says it is "whitelisted by major antivirus vendors", shows an AppEsteem badge, and links a publisher dashboard. — [bright-sdk.com](https://bright-sdk.com/)
- A Unity-era press release says Bright SDK "indexes information that is publicly available on the web", sells that data to companies, and is pitched as raising ARPDAU. — [BusinessWire 2022](https://www.businesswire.com/news/home/20221122005420/en/)
- Bright Data's "idle monitor" documentation lists these limits: usage stops while the user is active, a minimum battery threshold, a charging-only preference, and daily and weekly bandwidth caps. — [Bright Data docs: protect end-user resources](https://docs.brightdata.com/general/privacy/protect-end-users-resources.md)

**Massive (joinmassive.com)**
- The partner page describes the SDK as using idle **bandwidth**, with "no CPU spikes". Supported platforms are Android, SmartTV, Fire TV and **Windows**; macOS appears only on the application form, and Linux is not listed. Payment scales "linearly with active opted-in users". 2025 partner examples: more than $1M/yr from a US Android utility app with over 100K US DAU; $500K/yr from a worldwide SmartTV app with 250K DAU; $250K/yr from a UK/US SmartTV app with 50K DAU. Users must opt in in exchange for a perk the partner defines. The SDK includes a drop-in consent UI with an audit trail. Earnings update hourly on a dashboard that shows opt-in rate and revenue per user. Payouts are monthly by PayPal or wire. Massive claims AppEsteem "Certified for quality & safety". — [Massive: Get Paid](https://www.joinmassive.com/getpaid)
- Massive's knowledge base says the SDK uses "small amounts of users' idle device resources (with consent) to perform tasks such as processing public web data and running simulations". It gives no compute-specific rates or platform details. — [Massive docs KB](https://docs.joinmassive.com/kb/sdk/how-does-the-massive-sdk-work-and-make-money-for-developers)
- History: in 2021 Massive raised an $11M seed and pitched renting spare compute to pay for apps, including mining, hosting blockchain infrastructure and future distributed computing. Its research page now describes running CV inference and LLMs on "thousands of distributed CPUs". — [TechCrunch 2021](https://techcrunch.com/?p=2240788); [Massive blog](https://joinmassive.com/blog/massive-raises-11m-for-a-new-business-model-for-the-internet); [Massive Research](https://joinmassive.com/research)

**Infatica / PacketSDK / others (bandwidth, listed for completeness)**
- Infatica says its SDK supports desktop apps (Windows and macOS), mobile apps and Chrome extensions. — [Indie Hackers post](https://www.indiehackers.com/post/app-monetization-with-infatica-sdk-chrome-windows-desktop-android-6a396a6993); [Infatica SDK blog](https://infatica-sdk.io/blog/app-monetization/monetization-model-for-apps-2025/)
- PacketSDK pays by daily active devices, and its traffic fetches public web data for AI training. This is a vendor claim reported by Proxyway. — [Proxyway research](https://proxyway.com/research/internet-sharing-sdks-app-monetization)

**Kryptex**
- I found no developer or publisher SDK. Kryptex is a consumer CPU/GPU mining app plus an Android miner, estimated at about $0.50–1.00/month per phone. An npm package `kryptex` is a placeholder that says "more details coming soon" and that dedicated stratum nodes are available on request (npm@kryptex.org). — [Kryptex Android Miner](https://www.kryptex.com/android-miner); [npm kryptex](https://npmjs.com/package/kryptex)

**Salad / Golem**
- I found no Salad white-label or publisher SDK program. In 2026 Salad announced it would become an exclusive subnet on the Render Network (RNP-023) and move its rewards for distributed compute and bandwidth sharing on-chain in RENDER. — [Salad blog (RNP-023)](https://blog.salad.com/tag/depin/)
- January 2026: Salad and Golem announced a test partnership to "mirror" part of Salad's commercial workloads on Golem's permissionless execution layer. — [Barchart/Chainwire, 2026-01-13](https://www.barchart.com/story/news/37019027/salad-com-and-golem-network-partner-to-integrate-web2-workloads-with-decentralized-infrastructure)

### Inferences
- **Compute-specific publisher SDKs that pay the operator appear not to exist publicly in Oct 2026.** The category has consolidated around bandwidth and web-data SDKs. Massive and Bright SDK hint at compute ("simulations", CPU checks) but publish no compute product, rate, or Linux/macOS desktop build.
- **Integration format for .NET:** none of these vendors publicly documents a .NET/NuGet package. Windows SDKs are typically native DLLs or a service the host app loads, so Otium would likely need P/Invoke or a sidecar process. This is unverified and needs vendor confirmation.
- **Aggregation:** all of these SDKs are built for the publisher to aggregate many users under one publisher account. That is the business model, so ToS aggregation risk is low there. The exception is stacking several bandwidth SDKs (Honeygain plus Bright SDK plus Massive) on the same device, which vendor contracts may forbid. Unverified.
- **Per-device attribution:** Massive offers "revenue per user" and opt-in rate on its dashboard. Whether it exposes per-device IDs that Otium could map to its own user balance was not confirmed for any vendor.
- **Salad/Golem as a compute route:** the realistic option is for Otium to act as a provider or "node operator" on a DePIN compute network (Golem provider, Render/Salad subnet) rather than embed an SDK. That would mean shipping a provider runtime (often Docker/VM-based on Linux, sometimes GPU-only) and pooling many users under one operator wallet, which the ToS likely restricts. Unverified.

### Gaps
- No public per-DAU, per-MAU or per-CPU-hour rates for Bright SDK (the "$200 CPM" headline is ambiguous) or Massive (only annual case studies).
- No vendor confirms macOS or Linux desktop SDKs (Massive and Bright: Windows only, as published).
- No public SDK for distributed AI inference that pays app publishers was found. My searches turned up nothing beyond Massive's research claims.
- Golem provider economics and Salad ToS on operator aggregation were not checked.

## BOINC + Gridcoin: can an operator earn GRC from users' machines?

### Takeaway
Technically yes. Gridcoin pools exist precisely so many machines crunch under one pool BOINC account, and an operator could run its own solo account or pool and attach users' BOINC clients to it. Economically, though, GRC is close to worthless and illiquid: about $0.003, with aggregator-reported 24h volume of about $0 in July 2026. Even large per-account GRC rewards convert to trivial USD and may not be sellable.

### Cited Findings
- Gridcoin pools: members use the pool's BOINC account(s), so the pool crunches as one user. When the pool stakes, it collects rewards and distributes them pro rata. Pools let users earn without holding GRC. Listed pools are grcpool.com and Arikado Pool (grc.arikado.ru, open source). The wiki gives no current status, fees or anti-aggregation rules. — [Gridcoin wiki: Pools](https://gridcoin.us/wiki/pools)
- The Arikado pool is described as "a solo-miner with a single BOINC account" that allows pool users' computers to attach to that account. The post is undated. — [Steemit: Arikado Gridcoin pool intro](https://steemit.com/utopianio/@sau412/project-introduction-for-arikado-gridcoin-pool)
- Gridcoin removed the requirement for crunchers to join a Gridcoin BOINC team, and asked BOINC teams to help test the change. — [BOINC forum thread](https://boinc.berkeley.edu/forum_thread.php?id=13089)
- Example account reward snapshots on gridcoinstats.eu: about 24 GRC/day for one CPID and 111.37 GRC/day for another. Both are grcpool accounts, the dates are unclear, and these are whole-pool accounts, not per device. — [gridcoinstats grcpool CPID](https://www.gridcoinstats.eu/cpid/7d0d73fe026d66fd4ab8d5d8da32a611); [grcpool-3 CPID](https://www.gridcoinstats.eu/cpid/163f049997e8a2dee054d69a7720bf05)
- Price and liquidity: a CoinLore snapshot dated 2026-07-24 shows GRC at about $0.00309, roughly 98.5% below its all-time high, with 24h volume of $0 (−99.97%) and rank #3779. — [CoinLore Gridcoin](https://coinlore.com/coin/gridcoin). Other trackers show about $0.0036 (2026-07-01) and $0.0038 (2026-04-18), and one undated tracker shows $0.0073. — [CoinGecko](https://www.coingecko.com/en/coins/gridcoin-research); [Coinpaprika](https://coinpaprika.com/coin/grc-gridcoin/). KuCoin shows GRC as "not officially listed". — [KuCoin GRC](https://www.kucoin.com/es/price/GRC)

### Inferences
- Rough order of magnitude: even at 111 GRC/day for a whole pool account, that is 111 × $0.003 ≈ $0.33/day, or about $10/month, for an entire pool's output. Per user device, Gridcoin revenue would be fractions of a cent per month, far below Otium's existing XMRig/SRBMiner revenue. With near-zero exchange volume, cashing out at any scale may be impossible.
- Since a single pool BOINC account crunching for many machines is the documented pool model, aggregation does not appear to be prohibited. BOINC project ToS and any per-account rules were not checked.
- The non-monetary angle may still have value: offering "donate idle time to science (BOINC/Folding@home)" as a user-chosen mode, credited with points but not cash, could help Otium's reputation and AV standing.

### Gaps
- No reliable 2026 per-CPU or per-GPU GRC/month figure was found, and I did not find current operational status for grcpool or Arikado in 2025–2026.
- Gridcoin beacon and staking mechanics for a solo operator account were not verified from a source.

## Folding@home and other volunteer computing with a payout route

### Takeaway
I found no live, verifiable payout route for Folding@home in 2025–2026. Curecoin and FoldingCoin, the historical "merge-folding" tokens, appear dormant on all evidence found (only 2014-era sources). Folding@home itself pays nothing.

### Cited Findings
- Curecoin rewarded Folding@home work with CURE tokens. Folding@home was "not affiliated" with it, and Curecoin's developers controlled which projects counted. — [Brave New Coin 2014](https://bravenewcoin.com/insights/curecoin-the-first-six-months); [Bitcoin Magazine 2014](https://bitcoinmagazine.com/articles/mine-cure-1408057211)
- In 2014 CureCoins traded on sites like Eobot for 4–50 cents. — [Euromoney](https://www.euromoney.com/article/b12kkm86mnlj5q/curecoin-a-cryptocurrency-aiming-to-beat-cancer)
- Charity Engine (see above) is the main BOINC-based option with a "payout", and that payout is prize-draw entries per CPU-hour. — [Charity Engine volunteer page](https://www.charityengine.com/volunteer)

### Inferences
- Volunteer computing is useful to Otium only as a non-revenue "good cause" mode, unless Otium pays users from its own margin.

### Gaps
- Current status of Curecoin and FoldingCoin, including block production, exchange listings and 2025–2026 payouts, was not verified. Every source found is about a decade old.

## Browser-based DePIN (Grass, Gradient, Nodepay, Bless, Teneo, Dawn): real money or embeddable nodes?

### Takeaway
These are mostly bandwidth or "compute" points programs run by consumers through browser extensions or desktop apps, paid in tokens after airdrops. I found no embeddable SDK or operator-aggregation program for any of them. Grass and Bless have tradeable tokens. Gradient, Nodepay, Teneo and Dawn are still largely points or low-information as of the sources found. Aggregating many users' devices under one operator account would very likely break their anti-Sybil rules. I did not verify their ToS directly.

### Cited Findings
- **Grass:** the token trades at roughly $0.65–0.79 in October 2026. Season 2 claims run from 2026-07-22 to 2027-01-22 for bandwidth contributed from October 2024 to June 2026 (USDC-denominated rewards per that source). Rewards split into Uptime Points and Network Points, the latter earned only when bandwidth is actually used. — [Coingabbar: Grass S2 claim](https://www.coingabbar.com/en/grass-airdrop-season-2-claim-deadline-grass-coin-price-today); [Blockchain Reporter guide](https://blockchainreporter.net/grass-airdrop-farming-guide-2026-how-to-position-before-the-h1-drop/). Nansen says Grass is moving toward a desktop app and mobile apps. I found no public SDK. — [Nansen research](https://research.nansen.ai/articles/1871). Grass terms effective 2026-07-01 include mandatory arbitration, per a third-party tracker summary. — [ToS Tracker: getgrass.io](https://tostracker.app/document/getgrassio-tos)
- **Bless Network:** mainnet and TGE launched 2025-09-23 with listings on Binance, Bybit, Bitget, Kraken, Gate and MEXC. Pre-TGE TIME points for idle CPU/GPU, earned through a browser extension or app, converted to BLESS. Payments are split 90% to node operators and 10% to the treasury. Sources conflict on the buyback detail. BLESS was about $0.0055 on 2026-02-16. — [Coinswitch](https://coinswitch.co/switch/crypto/bless-network-airdrop/); [MEXC price page](https://www.mexc.com/price-prediction/BLESS); [CoinGecko BLESS](https://www.coingecko.com/en/coins/bless)
- **Gradient:** a Solana-based edge network where users run a browser-extension node for rewards. I found no 2026 token or payout data. — [Defiports Substack](https://defiports.substack.com/p/3-depin-projects-with-airdrop-opportunities)
- **Teneo:** a roundup lists 33–35% of a 10B supply for user rewards. I found no node SDK or value data. — [Cryptopolitan](https://www.cryptopolitan.com/top-10-crypto-airdrops-that-actually-worth-your-time-right-now/)
- **Dawn, Nodepay:** I found no 2026 information.

### Inferences
- For Otium these are not "real money" integrations. Rewards are tokens with airdrop timing risk and volatile, often collapsing prices; BLESS was at a fraction of a cent within five months of TGE. They also need per-user wallets or accounts, and running many nodes under one operator would look like Sybil farming.
- Bless's idle CPU/GPU model is the closest thing to a compute DePIN, but I found no operator SDK.

### Gaps
- I did not read the ToS of Nodepay, Dawn, Gradient, Teneo or Bless on multi-device or operator aggregation. Current 2026 prices for Gradient, Teneo, Dawn and Nodepay were not found.

## Enforcement actions and malware classifications against compute or bandwidth monetization SDKs

### Takeaway
Microsoft's official criteria (updated September 2026) list "Cryptomining software (Enterprise only)" as a PUA category. Otium's existing XMRig/SRBMiner integration is therefore exposed to PUA detection on enterprise-managed devices by definition. For proxy/bandwidth SDKs, Defender uses heuristic PUA names such as PUA:Win32/FRProxy, and 2026 security research ties SDK-based proxyware to understated consent and to malware campaigns. I found no specific 2025–2026 enforcement action against Bright SDK or Massive by name.

### Cited Findings
- Microsoft's PUA categories include "**Cryptomining software (Enterprise only):** Software that uses your device resources to mine cryptocurrencies." Other categories are bundling software ("offers to install other software that isn't developed by the same entity"), marketing software, evasion software and poor industry reputation. Microsoft states that "PUAs aren't considered malware." The "unwanted software" criteria also require prominent notice, a clear indication when the software is active, consent before storing or transmitting data, and standard uninstall. The page was updated 2026-09-10. — [Microsoft Learn: How Microsoft identifies malware and PUA](https://learn.microsoft.com/en-us/unified-secops/criteria)
- Defender flags proxy software heuristically as PUA:Win32/FRProxy, per a Hiddify user discussion. That explanation comes from a user, not Microsoft. — [GitHub hiddify discussion #2101](https://github.com/hiddify/hiddify-app/discussions/2101)
- A Cloud Security Alliance paper (June 2026) says residential-proxy devices are enrolled through SDKs bundled in free apps, "typically with consent mechanisms" that "understated the scope of relay activity". — [CSA AI data supply chain / residential proxy risk v1.0](https://labs.cloudsecurityalliance.org/wp-content/uploads/2026/06/ai-data-supply-chain-residential-proxy-risk-v1.0-csa-styled.pdf)
- AhnLab ASEC (2026) describes campaign "Larva-25012", which installs SDK-based proxyware (it names DigitalPulse, SOAX, Appsalt and IPRoyal) alongside loader malware, including commands that disable Defender. — [ASEC: Larva-25012](https://asec.ahnlab.com/en/95516/)
- The Hacker News (June 2026) reported "free apps are quietly turning smart TVs" into proxy nodes, and a security write-up reverse-engineered Bright SDK's iOS build. I could not read the article (HTTP 403), so its details are unverified. — [The Hacker News, 2026-06](https://thehackernews.com/2026/06/free-apps-are-quietly-turning-smart-tvs.html?m=1)
- Cryptominers and proxyware are often deployed together by attackers (Mimo/Craft CMS 2025; Langflow CVE-2026-33017), which reinforces heuristic detection of both categories. — [The Hacker News 2025](https://thehackernews.com/2025/05/mimo-hackers-exploit-cve-2025-32432-in.html); [Trend Micro 2026](https://www.trendmicro.com/en/research/26/f/from-langflow-to-monero-inside-cve-2026-33017-cryptominer.html)
- Vendors counter with certifications: Massive and Bright SDK both claim AppEsteem certification, and Bright claims AV whitelisting. — [Massive](https://www.joinmassive.com/getpaid); [bright-sdk.com](https://bright-sdk.com/)

### Inferences
- Adding more bandwidth SDKs alongside XMRig and SRBMiner makes Otium look more like the "miner plus proxyware" bundle security vendors associate with malware, which raises AV/PUA risk. Prominent opt-in, a visible activity indicator and a kill switch, standard uninstall, and an AppEsteem-style certification for Otium itself are the mitigations implied by Microsoft's criteria.
- Under Microsoft's "bundling software" definition, embedding third-party SDKs that run independent services may also count, unless they are clearly part of Otium's own functionality.

### Gaps
- I found no named Defender, Google Play Protect or Chrome Web Store enforcement against Bright SDK, Massive, Honeygain or Infatica in 2025–2026. I did not check Google's Unwanted Software Policy or Play's "device and network abuse" policy text. Sekoia and Talos reports were not reached within the search budget.
