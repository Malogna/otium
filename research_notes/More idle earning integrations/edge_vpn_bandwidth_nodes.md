# Edge / CDN / VPN-exit / Bandwidth Node Networks for Otium (status as of October 2026)

Context: Otium is a C#/.NET 10 Avalonia desktop app (Windows, Linux, macOS). The server keeps users' USD balance and pays out via PayPal or crypto. It already embeds the Honeygain bandwidth SDK. These notes look at which other networks could run inside the app, pay the operator, and be credited to users.

Research date: 2026-10-16. Many crypto/DePIN sources are third-party, affiliate or vendor marketing pages. Each item is flagged where it is old, from a vendor, or unverified.

---

## 1. Mysterium Network / MystNodes: earnings, payouts, multi-node operation, SDK, OS packaging, NAT, legal risk, ToS

### Takeaway
Of everything reviewed, Mysterium is the most viable addition for an operator. A Windows desktop app already embeds MystNodes and credits its users: Kryptex does this and converts MYST to BTC at 5 MYST. MystNodes also ships a partner "SDK client" that is token-authenticated and issued by the MystNodes team. Earnings are small, about $2-25 per month per home device by Mysterium's own figures. They are paid in volatile MYST, with a 20% settlement fee. Exit-node legal risk is real, which is why Mysterium itself recommends "B2B-only" traffic and advises against "Public" traffic in the US, UK, Canada, Germany, Italy, Australia and India.

### Cited Findings
**Earnings and rates**
- MystNodes' own blog (last updated 22 Sep 2026) gives payouts of about $0.04-$0.30 per GB depending on traffic type: VPN, data streaming, public web access, or scraping. It gives no per-service breakdown. — [MystNodes blog](https://www.mystnodes.com/blog/how-much-can-you-earn-sharing-bandwidth)
- The same page gives illustrative, not guaranteed, monthly earnings: casual laptop $2-8, always-on home node $8-25, multi-node operator $25-100+. Network scale: 120+ countries, 45k+ participating IPs, "$568,019 paid out to date". These are vendor figures. — [MystNodes blog](https://www.mystnodes.com/blog/how-much-can-you-earn-sharing-bandwidth)
- A July 2026 help article says earnings depend on traffic volume, region, bandwidth and uptime. It recommends verified B2B services for "safer traffic". — [MystNodes help: maximise earnings](https://help.mystnodes.com/en/articles/8005135-how-can-i-maximise-my-earning-potential)
- Operators choose service types. "B2B VPN and data transfer" carries streaming and data-transfer traffic from B2B clients. "B2B Data Scraping" allows only scraping traffic. Choosing B2B-only means turning off the Public option. — [MystNodes help: Node UI management](https://help.mystnodes.com/en/articles/8006125-node-ui-management)
- Kryptex, which embeds MystNodes in its Windows app, says "predicting your income is not possible. We only display the actual earned reward." — [Kryptex x Mysterium](https://www.kryptex.com/en/news/kryptex_x_mysterium)

**Payout mechanics**
- Unsettled earnings settle to the balance automatically at 5 MYST, or manually via the SETTLE button in NodeUI. The network settlement fee is a fixed 20%, plus dynamic blockchain fees. Example: settling 5 MYST deducts 1 MYST plus gas. Polygon L2 fees are "cents". — [Mysterium docs FAQ](https://docs.mysterium.network/faq)
- The blog says there is no platform-enforced minimum but recommends at least 5 MYST before claiming, to cover gas. — [MystNodes blog](https://www.mystnodes.com/blog/how-much-can-you-earn-sharing-bandwidth)
- KYC is not mentioned in the node FAQ. — [Mysterium docs FAQ](https://docs.mysterium.network/faq)

**Operator / partner integration (many nodes under one operator)**
- An official Docker image, `mysteriumnetwork/mystnodes-sdk-client` (about 14 MB, updated around early Oct 2026), "connects your application or service to the Mysterium SDK so you can monetize through bandwidth sharing". Usage: `docker run --restart always mysteriumnetwork/mystnodes-sdk-client:latest --user.token=<token>`, where the token is "obtained from mystnodes team". This means partner onboarding goes through MystNodes, not self-serve. — [Docker Hub: mystnodes-sdk-client](https://hub.docker.com/r/mysteriumnetwork/mystnodes-sdk-client)
- The MystNodes business/SDK page (via search snippet) describes the SDK as free to integrate, "no costs to buy, maintain", and ad-free. I could not fetch the page directly: /sdk returned 404. — [search result summary; Docker Hub](https://hub.docker.com/r/mysteriumnetwork/mystnodes-sdk-client)
- Precedent: Kryptex, a Windows desktop crypto-mining app, integrated MystNodes. Users switch to PRO mode and activate a "Network Card" switch. Rewards arrive in MYST and Kryptex converts them to BTC at 5 MYST. The feature is not available in some regions. Kryptex's guide warns users that they act "as an exit node for others" and that legality depends on local law, ISP terms and allowed traffic types. Use is "at your own risk", "as is". — [Kryptex x Mysterium](https://www.kryptex.com/en/news/kryptex_x_mysterium); [Kryptex Mysterium articles](https://www.kryptex.com/es/articles/category/mysterium)
- Other integrators: Nebra and SenseCAP (IoT hardware), Hivello (a DePIN aggregator that runs Mysterium alongside other networks), and Dappnode and AVADO (node boxes). — [MystNodes x Nebra](https://blog.mystnodes.com/blog/mysterium-nebra-partnership); [SenseCAP launch](https://blog.mystnodes.com/blog/sensecap-launch); [Mysterium partners](https://www.mysterium.network/partners)
- Each node has its own node identity that must appear in the Discovery service. The FAQ does not say whether one wallet or beneficiary can be shared across nodes. — [Mysterium docs FAQ](https://docs.mysterium.network/faq)
- A mobile SDK exists for Android, as Maven packages with a mobile-node dependency. A JS SDK (`node-js`) controls a running node through its API. — [Mysterium mobile SDK docs](https://docs.mysterium.network/apis-sdks/mobile-sdk); [node-js GitHub](https://github.com/mysteriumnetwork/node-js)

**OS / packaging / automation**
- The FAQ lists supported platforms as Mac, Linux, Windows, DAppNode, AVADO and Raspberry Pi. — [Mysterium docs FAQ](https://docs.mysterium.network/faq)
- The node is open source (GPL-3.0, Go). The README documents a Debian/Ubuntu/Raspbian install script and Docker images. Docker needs `--cap-add NET_ADMIN --net host` and starts with `service --agreed-terms-and-conditions`. The node exposes a "Tequila API" (REST) for control, and auto-updates can be disabled via `MYST_UPDATER_ENABLED=false`. — [GitHub mysteriumnetwork/node](https://github.com/mysteriumnetwork/node)
- MystNodes apps are listed for Windows, macOS, Linux, Android, Raspberry Pi and as a Chrome extension (per Kryptex's summary). — [Kryptex Mysterium articles](https://www.kryptex.com/es/articles/category/mysterium)
- Community Prometheus monitoring exists (mystprom). — [GitHub Sch8ill/mystprom](https://github.com/Sch8ill/mystprom)

**NAT traversal**
- The FAQ does not cover NAT or port forwarding. It only notes that routers may block Mysterium traffic. The GitHub repo has `nat`, `p2p` and `docker-compose.e2e-traversal.yml` paths, which suggests built-in NAT hole-punching, but the README does not document it. — [Mysterium docs FAQ](https://docs.mysterium.network/faq); [GitHub node](https://github.com/mysteriumnetwork/node)

**Legal risk / ToS**
- The FAQ says B2B partners must follow local law and "take full responsibility" for anything illegal. It admits "we cannot say it's 100% safe" while calling illegal activity "incredibly rare". It advises node runners in Canada, the US, the UK, Italy, Australia, Germany and India not to enable "Public" traffic. — [Mysterium docs FAQ](https://docs.mysterium.network/faq)
- The terms live at mystnodes.com/terms (referenced by Kryptex). I did not retrieve them. — [Kryptex x Mysterium](https://www.kryptex.com/en/news/kryptex_x_mysterium)

### Inferences
- The Kryptex model maps almost directly onto Otium. The operator app ships the node, the user opts in, MYST accrues per node, and the operator converts to its own currency (BTC for Kryptex, USD balance for Otium). The partner SDK client with a `--user.token` strongly implies operator-level aggregation: one partner token, many devices. The exact per-device attribution API must come from the MystNodes partner team.
- Otium would likely have to bundle the native node binary (Go, multiplatform) or the SDK client binary, not Docker. Docker on consumer Windows and macOS is unrealistic. The Docker image needs NET_ADMIN and host networking, so a native binary needs equivalent privileges (TUN/WireGuard driver install on Windows and macOS). That raises installer complexity and AV-flag risk.
- At $0.04-0.30/GB with a 20% settlement fee and MYST price volatility, net per-device revenue is probably low single-digit USD per month for typical Otium users. Otium should restrict nodes to B2B-only traffic by default, especially for US/UK/DE/CA/AU/IT/IN users.

### Gaps
- Partner SDK commercial terms (revenue share, minimums, KYC, per-device reporting API, consent rules) are not public. The /sdk page was unreachable, so the MystNodes team must be contacted.
- I did not find independent 2026 operator earnings reports from Reddit or forums.
- MystNodes ToS clauses on running many nodes under one identity or wallet were not retrieved.
- I found no explicit documentation of the "WireGuard" service rate versus the scraping rate.

---

## 2. Theta Edge Node: status, TFUEL pay, OS support, automation

### Takeaway
Theta Edge Node still exists and pays TFUEL. No reliable 2026 earning rate exists, and TFUEL was about $0.012-0.014 in March 2026. Hardware requirements are heavy (4 cores, 16 GB RAM, 64 GB+ disk per one listing). It is a poor fit for embedding in a consumer app.

### Cited Findings
- A DePIN ROI tracker (reviewed 24 Aug 2026) rates Theta Edge Node "Earning rate unverified" / "ROI not measurable yet". It says task demand, eligibility, stake requirements and utilization are not reproducible from public evidence, and it does not model a payout route. — [gamefi-roi: Theta Edge Node](https://gamefi-roi-web.onrender.com/opportunities/theta-edge-node)
- An exchange explainer (KuCoin) claims users can still earn TFUEL by running a node in 2026, and that rewards for GPU contribution increased with EdgeCloud AI workloads. This is unverified marketing. — [KuCoin](https://www.kucoin.com/knowledge-base/Concepts/what-is-theta-network-theta)
- TFUEL traded at about $0.012-0.014 in late March 2026 (third-party snapshot). — [MEXC news](https://www.mexc.com/news/602850)
- A software inventory listing reports minimum requirements of 4 CPU cores, 16 GB RAM and 64 GB+ free disk, with a native Apple Silicon build. This is not an official source. — [Rosetta Check](https://rosettacheck.com/apps/2tfukg44)
- The official setup docs are at docs.thetatoken.org/docs/setup-theta-edge-node (not fetched). — [gamefi-roi references](https://gamefi-roi-web.onrender.com/opportunities/theta-edge-node)

### Inferences
- There is no partner/SDK model for operators. Each node is tied to a user wallet, so Otium would have to hold wallets per device or one shared wallet, which is unclear under Theta rules. The resource footprint and low TFUEL price make it unattractive.

### Gaps
- Official 2026 OS support matrix, CLI/headless mode, per-node TFUEL yield, and staking requirement were not verified.

---

## 3. Other edge/CDN/DePIN networks: which are alive, consumer-runnable, and pay real money

### Takeaway
Most "edge CDN" DePINs are dead, points-only, or hardware/server oriented. Filecoin Saturn is inactive and Meson/GaGaNode were wound down in 2026. Titan and Blockcast run points/airdrop programs. Grass is the largest and pays real tokens, and it paid USDC for Season 2, but it has no operator SDK. Sentinel and Nym require Linux/VPS-style nodes. Nodle is mobile only. Aethir Edge needs a purchased device. Livepeer is a GPU/server business, not consumer idle earnings. None is a clean "operator collects, credits users in USD" fit except Mysterium.

### Cited Findings
**Filecoin Saturn: shut down**
- Filecoin's own legacy-content docs describe Saturn in the past tense as a Web3 CDN "no longer active". No shutdown date was found. — [Filecoin docs: Legacy content](https://docs.filecoin.io/reference/general/legacy-content); repo: [filecoin-saturn/L1-node](https://github.com/filecoin-saturn/L1-node)

**Meson Network / GaGaNode: wound down in 2026**
- A 16 Mar 2026 GitHub discussion quotes a maintainer saying Meson Network "has been removed and gaganode will be removed soon". The MSN token is still active. — [engageub/InternetIncome #306](https://github.com/engageub/InternetIncome/discussions/306)
- PitchBook lists Meson as "Out of Business" (status change 1 Mar 2026). — [PitchBook](https://pitchbook.com/profiles/company/469158-49)
- A token-info site says meson.network has been offline since 2026-06-07. Crunchbase still says "Active", which looks stale. — [Messari](https://messari.io/project/meson-network); [Crunchbase Gaganode](https://www.crunchbase.com/organization/gaganode)

**Titan Network (Titan Edge)**
- The TitanEdge Desktop app is open source on GitHub for Windows, macOS and Linux. The latest release seen was v0.0.11, which added tunnel-network reward statistics. — [GitHub titanedge-desktop releases](https://github.com/Titannet-dao/titanedge-desktop/releases); [Titannet-dao GitHub](https://github.com/Titannet-dao)
- The basic reward formula accrues every 5 s: PC = M_R × M_X × M_O × 300/17280, phone 500/17280, other (VPS/IoT) 150/17280, where the coefficients are region, loyalty and online status. Download/upload rewards also scale with "number of nodes under the same IP". — [Titan docs: Basic Rewards](https://titannet.gitbook.io/titan-network-en/resource-network-test/operate-nodes/l2-edge-node/reward-rules/basic-rewards); [Titan reward rules](https://titannet.gitbook.io/titan-network-en/resource-network-test/operate-nodes/l2-edge-node/reward-rules)
- The docs sit under "resource-network-test". The Impact Score season 3 counts only residential IPs. A 2025 press release talks of pre-TGE "$TNT" rewards. I found no confirmation that TNT launched or is tradable. — [CryptoSlate PR](https://cryptoslate.com/press-releases/titan-network-races-toward-mainnet-and-tge-with-global-node-expansion-across-3-continents/); [DePINscan 2025](https://depinscan.io/news/2025-07-23/titan-network-monetizing-idle-resources-in-the-blockchain-ecosystem)
- No "Titan container SDK" documentation for partners was found.

**Blockcast (BEACON node)**
- BEACON is Docker-based (any OS with Docker), one instance per hardware. Rewards depend on uptime, uplink and hardware score. A first connectivity test runs after 6 h and rewards start after 24 h online. — [Blockcast docs: BEACON FAQ](https://docs.blockcast.network/main/getting-started/how-do-i-participate-in-the-network/beacon/beacon-common-questions); [Start running your BEACON](https://docs.blockcast.network/main/getting-started/how-do-i-participate-in-the-network/beacon/start-running-your-beacon-today)
- Rewards are currently points / NFT licenses in a Proof-of-Resources phase. An airdrop guide says 99.9% uptime is needed for the NFT license and that the phase was about 6 months long (guide about 8 months old, so possibly ended). — [CryptoRank drophunting](https://cryptorank.io/drophunting/blockcast-activity1087); [Incrypted](https://incrypted.com/en/blockcast-node/)

**Grass (bandwidth for AI data; the largest DePIN in this category)**
- Grass reported about $17M revenue in 2025 and about $18M in H1 2026, and projects about $65-75M for 2026 from AI training data. — [Grass July 7 2026 call recap](https://www.grass.io/learn/july-7-grass-token-holder-and-network-participant-call/); [Dealroom](https://dealroom.co/news/157794-grass-backed-by-multicoin-projects-75m-in-2026-data-revenue/)
- Android devices earn the most, then desktop, then Chrome extensions. — [Grass recap](https://www.grass.io/learn/july-7-grass-token-holder-and-network-participant-call/)
- Rewards are points converted via seasonal airdrops. Season 2 reportedly paid in USDC, with claims from 22 Jul 2026 (third-party). Earnings only scale with distinct public IPs. — [Bitget academy](https://www.bitget.com/academy/which-platforms-and-services-support-grass-node-and-apps-in-india-2026-comprehensive-guide); [ownyourmind guide](https://ownyourmind.ai/use/how-to-earn-with-grass/)
- No operator/partner SDK for third-party apps was found.

**Uprock**
- A desktop and smartphone app shares internet and earns $UPT tokens. Only an opinion blog was found, with no verifiable earnings. — [Publish0x Grass vs Uprock](https://www.publish0x.com/makeitreal/i-tested-two-depin-passive-income-apps-grass-vs-uprock-which-xvpzwoj)

**Sentinel dVPN node**
- Official docs: hosting a dVPN node costs server and electricity. The "Node Revenue System" offsets costs, and passing a Health Check is the prerequisite for rewards. — [Sentinel docs: dVPN nodes](https://docs.sentinel.co/dvpn-nodes)
- Setup is Linux/VPS based. SentNodes shows 30-day network revenue of about 32.4M P2P across about 1,316 nodes (roughly 800 P2P per node per month on average; P2P token USD value not checked). — [Node Addicts](https://www.nodeaddicts.com/sentinel.html); [SentNodes](https://sentnodes.com/)

**Nym node**
- Only nodes in the rewarded set (240 nodes per 60-min epoch) earn. Rewards depend on stake saturation and performance. An older blog post says average operator rewards rose from 1.4 to over 2.8 NYM per epoch, and a dated network page shows about 10% APY. This is a staked server-operator model, not consumer idle. — [Nym docs: mixnet rewards](https://nym.com/docs/operators/tokenomics/mixnet-rewards); [Nym blog](https://nym.com/blog/min-profit-margin-and-max-operator-cost); [Nym operator guide 2026](https://weisser-zwerg.dev/posts/digital-civil-rights-networking-nym-node-operator-guide-2026/)

**Orchid**
- Providers must stake OXT on Ethereum and are chosen by stake-weighted lottery. OXT price is "near record lows". There is no consumer idle-node model. — [Bitcoin Foundation: OXT](https://bitcoinfoundation.org/news/altcoins/what-is-oxt-crypto-why-orchid-price-remains-near-record-lows/)

**Aethir Edge**
- Requires buying a dedicated Aethir Edge device. The Foundation states daily Edge rewards continue at 20 ATH per device per day. A 100 ATH/day promo rate has ended. A third-party guide says US residents cannot claim. — [Aethir Foundation update](https://ecosystem.aethir.com/blog-posts/aethir-foundation-update-redirecting-season-3-airdrop-to-strengthen-the-aethir-digital-asset-treasury-dat-and-affirming-edge-tokenomics-v1-0-for-sustainable-growth); [myedge.io](https://www.myedge.io/); [ownyourmind Aethir](https://ownyourmind.ai/use/aethir-node-setup/)

**Nodle**
- Mobile only: needs the Nodle phone app with Bluetooth and location. October 2026 US boost is 10x, then 5x through Nov 30. There is no desktop node. — [Nodle docs](https://docs.nodle.com/nodle-app); [Nodle Hot Zones (25 Sep 2026)](https://paragraph.com/@nodle/2026-09-25-nodle-hot-zones-are-expanding)

**Livepeer (orchestrator / transcoding / AI)**
- Q1 2026 was a record 134.4M minutes (+71.9% QoQ), but revenue per 1,000 minutes fell 22% to $1.91. The network is shifting toward AI inference. Orchestrators earn ETH fees plus LPT and need stake and a public server. — [Messari Livepeer](https://messari.io/project/livepeer); [Livepeer docs: run the network](https://docs.livepeer.org/network); [CryptoAdventure review 2026](https://cryptoadventure.com/livepeer-review-2026-decentralized-video-compute-orchestrators-and-real-time-ai-pipelines/)
- I found no 2026 consumer-GPU earnings data. The best-known "$1,000/month" write-up is old. — [Medium (old)](https://medium.com/coinmonks/a-new-livepeer-video-mining-rig-is-born-46919de23eae)

### Inferences
- Titan's PC rate (300) is lower than phone (500), and it penalizes multiple nodes per IP. Rewards are points or pre-mainnet tokens, so Otium could not credit users USD with confidence.
- Blockcast's Docker requirement and points-only rewards rule it out for a consumer desktop app.
- Grass is the most commercially successful bandwidth DePIN, but integration would require a partnership that does not appear to exist publicly. Running the Grass client per user under the operator's account would likely breach its terms (not verified).
- Peer-to-peer video CDNs that once used viewers' bandwidth (Peer5, Streamroot) were acquired by Microsoft and Lumen respectively (author's background knowledge, not verified this session). They never paid hosts. I found no live 2026 P2P-CDN network that pays desktop hosts.

### Gaps
- Fleek (Fleek Network edge nodes), Edgevana, and "EdgeVerse" were not researched for lack of budget. No 2026 status is confirmed. Fleek reportedly pivoted away from its edge node network (unverified).
- No partner/operator SDK was found for Titan ("container SDK"), Grass, Uprock or Blockcast.
- Sentinel P2P and Nym/Titan token USD values were not checked.

---

## 4. Other bandwidth SDK vendors (Pawns, Infatica, Repocket, PacketStream, Traffmonetizer, EarnFM, Castar, PacketSDK, Bright, Cash Raven) vs Honeygain; running two SDKs at once

### Takeaway
Several vendors offer desktop SDKs. Honeygain, Bright SDK and Infatica support Windows/macOS (no Linux per Proxyway). Pawns supports Windows/macOS; it markets Linux but its terms list only Android/iOS/Windows/macOS. Cash Raven, Castar and PacketSDK are mostly mobile/DAU oriented. Most pay per DAU or per opted-in device (roughly $0.10-0.50 per device per month in top geos), while Pawns pays only for "actual Used Traffic". No published Honeygain or Pawns clause explicitly forbids a second proxy SDK. However, both residential IP pools would share the same IP and device, and any Honeygain contractual exclusivity must be confirmed in the signed partner agreement.

### Cited Findings
**Honeygain SDK (current integration, for comparison)**
- Proxyway (16 Dec 2025): Honeygain SDK pays by geography, about $100-400 per 1,000 DAU per month in top regions (estimate). Platforms include Windows, macOS, Android, iOS, FireOS, webOS, Tizen, Flutter and Unity, with no Linux listed. — [Proxyway SDK research](https://proxyway.com/research/internet-sharing-sdks-app-monetization)
- Honeygain markets "up to $500 per DAU each month" (marketing; likely per 1,000 DAU). — [Honeygain SDK](https://sdk.honeygain.com/bright-sdk-alternatives/)
- Partner terms: use the SDK "solely in connection with Honeygain Services". Do not circumvent security or "create any obstacles for proper operation of SDK". There is no explicit ban on other proxy SDKs in the visible text (truncated). Only apps approved through the Honeygain SDK Partnership Program join the Oxylabs pool. — [Honeygain SDK ToS for partners](https://sdk.honeygain.com/tos-for-partners/); [Honeygain SDK ToS](https://sdk.honeygain.com/tos/); [Statement on third-party misuse](https://sdk.honeygain.com/blog/third-party-misuse-statement/)

**Bright SDK (Bright Data)**
- A flat rate per opted-in user of about $100-350 per 1,000 IPs per month. Code is not public; a partnership is required. Windows, macOS, Android, iOS, webOS, Tizen, Huawei. — [Proxyway](https://proxyway.com/research/internet-sharing-sdks-app-monetization)

**Infatica SDK**
- Usage-based pay that varies by volume and region; rates not published. Windows, macOS, Android. Uses Bitdefender to filter outbound traffic. Criticized in 2021 for limited disclosure to browser-extension developers. — [Proxyway](https://proxyway.com/research/internet-sharing-sdks-app-monetization)

**Pawns.app SDK (IPRoyal)**
- Business site: "extra $200 CPM", with a revenue calculator keyed to monthly DAU. Platforms listed as Android, iOS, Windows, macOS, Linux. — [Pawns business SDK](https://business.pawns.app/sdk/)
- SDK Terms: payment "only for actual Used Traffic" with no guaranteed minimum (3.3-3.4), and rates are deferred to the Pawns ToU. Supported OS: "Android, iOS, Windows, and macOS" (3.5). The feature must be disabled by default and need explicit separate opt-in, easy withdrawal and 24-month consent records (3.6.5-3.6.10). Traffic from datacenter, VPN or idle devices without "active personal use" is invalid (3.10). There is optional third-party KYC (1.5, 3.13). The partner is "solely responsible" for users being legally permitted to share traffic and indemnifies Pawns (3.7, 6.11). There is no exclusivity clause; only benchmarking or building a competing product is barred (1.9). — [Pawns SDK Terms](https://business.pawns.app/sdk-terms/)
- In January 2026 a GitHub user reported that the Pawns Linux SDK was "not yet available", which conflicts with the marketing page. — [engageub/InternetIncome #350](https://github.com/engageub/InternetIncome/discussions/350)

**Cash Raven SDK**
- (Aug 25, 2026) Pays "up to $0.50 per user per month", accrued daily once a device shares 60 min in a UTC day. Hosting, datacenter and VPN IPs are excluded. Consent is enforced architecturally. — [Cash Raven blog](https://cashraven.io/blog/passive-income-sdks-compared)

**Castar SDK / PacketSDK**
- Castar (Hong Kong, founded 2024): "up to $500 per 1,000 exposures". PacketSDK: "up to $600 per 1,000 valid impressions", NET45, paid on the 15th, based on average consenting DAU. Both are vendor claims. — [SourceForge Castar](https://sourceforge.net/software/product/Castar-SDK/); [PacketSDK blog](https://www.packetsdk.com/blog/packet_sdk/a-new-era-of-passive-revenue--how-packetsdk-offers-app-developers-a-unique-way-to-earn-money-without-ads.html)
- One anecdotal, older developer post reports about $20 per 1,000 DAU via PacketSDK. — [Medium](https://medium.com/@fengfengjun1228/how-i-integrated-a-simple-sdk-that-now-earns-300-month-in-passive-income-3c7decf145b4)
- Proxyway also cites unattributed industry ranges: $1.50-$6.00 per 1,000 DAU, $10-$70 per 1,000 IPs per month, and $0.001-$0.03 per user per day. These conflict sharply with the vendor ceilings above. — [Proxyway](https://proxyway.com/research/internet-sharing-sdks-app-monetization)

**Consumer-side reference rates (not publisher rates)**
- Pawns about $0.20/GB, Repocket about $0.20/GB, Traffmonetizer about $0.10/GB (affiliate blogs). — [EarnIndex Repocket](https://earnindex.io/blog/is-repocket-legit); [assistouest guide](https://assistouest.fr/en/blog/web-en/make-passive-money-by-sharing-your-internet-connection/)
- Trustpilot reviews of Traffmonetizer include payout-delay complaints. — [Trustpilot](https://au.trustpilot.com/review/traffmonetizer.com)

### Inferences
- Pawns, being per-GB/traffic-based, would yield less on idle desktops than per-DAU SDKs. Its "active personal use" clause (3.10(g)) may conflict with an "idle earning" app whose whole premise is running while the user is away. That clause needs review with Pawns.
- Running Honeygain plus a second SDK (or plus Mysterium) on the same device is not explicitly prohibited in the public terms reviewed. Risks: (a) the private Honeygain partner agreement may have exclusivity; (b) the same residential IP shows up in two pools, raising IP reputation and abuse load; (c) doubled AV/PUA surface; (d) bandwidth contention. Get written confirmation from Honeygain before adding a second proxy SDK. Mysterium (a VPN/B2B exit) is arguably a different product category, but it still sells the same IP.

### Gaps
- Infatica, Repocket business/SDK, PacketStream, Traffmonetizer partner program, EarnFM, Spider/ProxyBase: no publisher rate cards or desktop-SDK terms were found within budget.
- Exact Honeygain partner-agreement exclusivity is unknown; the public terms were truncated.

---

## 5. Cross-cutting: legal/abuse exposure, AV/PUA classification, aggregation, and a summary comparison

### Takeaway
Regulatory and platform pressure on residential-proxy SDKs escalated sharply in 2026. Google, the FBI and Lumen took down IPIDEA (January 2026) and NetNut/Popa (July 2026), including SDK detection via Play Protect and domain seizures. Microsoft Defender already classifies Repocket as PUA, and Talos classifies proxyware generally as PUA. Otium needs explicit, separate, revocable opt-in, B2B-only traffic where possible, and code-signing and AV-vendor whitelisting before adding any further node.

### Cited Findings
- **IPIDEA takedown (Jan 2026):** Google sued, seized or sinkholed C2 domains, shared SDK intelligence with platforms and law enforcement, and made Play Protect block IPIDEA-laden apps. IPIDEA was linked to the BadBox 2.0, Aisuru and Kimwolf botnets, and its reseller brands (922 Proxy, ABC Proxy and others) were affected. — [The Hacker News](https://thehackernews.com/2026/01/google-disrupts-ipidea-one-of-worlds.html); [BleepingComputer](https://www.bleepingcomputer.com/news/security/google-disrupts-ipidea-residential-proxy-networks-fueled-by-malware/); [The Register](https://www.theregister.com/security/2026/01/29/google-cripples-ipidea-proxy-network-abused-by-crims/5050674)
- **NetNut / Popa takedown (2-3 Jul 2026):** Google GTIG with the FBI and Lumen. At least 2M compromised devices (mostly TVs and streaming boxes, some via apps with hidden proxy SDKs). The FBI seized hundreds of domains. Play Protect disables apps containing NetNut SDKs. 316 threat groups were tied to NetNut exit nodes in one June week. — [BleepingComputer](https://bleepingcomputer.com/news/security/netnut-proxy-network-disrupted-2-million-infected-devices-cut-off); [Expert Insights](https://expertinsights.com/news/google-fbi-operation-takes-down-netnut); [IT Brief UK](https://itbrief.co.uk/story/google-disrupts-netnut-proxy-network-in-fbi-operation)
- Spur found nearly half of LG Smart TV apps contain residential proxy SDKs (a measure of how pervasive the category is). — [Spur](https://spur.us/blog/smart-tv-apps-residential-proxy-sdks)
- **AV / PUA:** Microsoft Defender detects `PUA:Win32/RepocketProxyware`. — [Microsoft Security Intelligence](https://www.microsoft.com/en-us/wdsi/threats/malware-encyclopedia-description?Name=PUA%3AWin32%2FRepocketProxyware)
- Cisco Talos (2021, older) classified proxyware as potentially unwanted applications and documented malware dropping Honeygain/Nanowire clients. — [Talos: Attracting flies with Honey(gain)](https://blog.talosintelligence.com/proxyware-abuse/)
- AhnLab ASEC reports proxyware bundled in fake Notepad++ tools and YouTube-downloader sites, with Defender exclusions added. — [ASEC 92183](https://asec.ahnlab.com/en/92183/); [ASEC 89574](https://asec.ahnlab.com/en/89574/)
- Cash Raven notes antivirus flagging on Windows as an evaluation criterion for SDKs. — [Cash Raven](https://cashraven.io/blog/passive-income-sdks-compared)
- Infostealer-style risk: a 2026 report says a bandwidth-sharing app can turn employee devices into gateways to internal networks. — [CybersecurityNews](https://cybersecuritynews.com/bandwidth-sharing-app/)
- **Exit-node liability:** Mysterium says legality depends on country, ISP terms and traffic type, and advises against Public traffic in 7 countries. Pawns makes the partner "solely responsible" and indemnifying. — [Mysterium FAQ](https://docs.mysterium.network/faq); [Pawns SDK Terms](https://business.pawns.app/sdk-terms/)

### Summary comparison (from the cited findings above; "?" means not verified)

| Network / SDK | Alive Oct 2026 | Desktop OS | Automation | Payout | Operator aggregation | Realistic per home device |
|---|---|---|---|---|---|---|
| Honeygain SDK (existing) | Yes | Win, mac (Linux ?) | SDK | USD, per DAU | Yes (partner) | ~$0.10-0.40/DAU/mo top geos (Proxyway est.) |
| Mysterium / MystNodes SDK | Yes | Win, mac, Linux, Pi, Docker | Native binary + Tequila REST API; SDK client token | MYST, 5 MYST auto-settle, 20% fee | Yes, partner token (Kryptex precedent) | $2-25/mo (vendor) |
| Pawns SDK | Yes | Win, mac (Linux disputed) | SDK | Per used traffic; rates private | Yes (partner, KYC) | ? |
| Bright SDK | Yes | Win, mac | SDK (private) | $100-350 per 1k IPs/mo | Yes | ~$0.10-0.35/mo |
| Infatica SDK | Yes | Win, mac | SDK | Usage-based, private | Yes | ? |
| Cash Raven | Yes | Win (others ?) | SDK | ≤$0.50/user/mo | Yes | ≤$0.50/mo |
| Castar / PacketSDK | Yes | Mostly mobile (desktop ?) | SDK | Per 1k DAU/exposures; NET45 | Yes | ? (vendor ceilings only) |
| Theta Edge Node | Yes (unverified ROI) | Win/mac/Linux? (heavy HW) | App | TFUEL (~$0.013) | No partner model | Unverified |
| Titan Edge | Yes (testnet / points) | Win, mac, Linux | Desktop app, open source | Points / pre-TGE TNT | No partner SDK found | Unquantifiable |
| Blockcast BEACON | Points phase | Docker any OS | Docker | Points / NFT | One per hardware | Unquantifiable |
| Grass | Yes, large | Win, mac, Linux, Android | App | Points → GRASS / USDC airdrops | No partner SDK found | Variable |
| Meson / GaGaNode | Dead (2026) | — | — | — | — | — |
| Filecoin Saturn | Dead | — | — | — | — | — |
| Sentinel dVPN | Yes | Linux/VPS | CLI | P2P token | Per node | Server-scale |
| Nym node | Yes | Linux server | CLI | NYM, stake-weighted | Per node, staked | Server-scale |
| Orchid | Marginal | Server | — | OXT, staked | — | — |
| Aethir Edge | Yes | Dedicated device only | — | 20 ATH/day/device | — | N/A (hardware purchase) |
| Nodle | Yes | Mobile only | — | NODL | — | N/A desktop |
| Livepeer | Yes | GPU server | CLI | ETH fees + LPT | Staked orchestrator | Not consumer-viable |

### Inferences
- The strongest candidate for Otium is MystNodes via its partner SDK/token, restricted to B2B traffic, modeled on Kryptex. A secondary option is one more per-DAU desktop SDK (Bright SDK or Infatica), conditional on Honeygain partner-agreement review. Everything else (Titan, Grass, Blockcast, Theta) pays in points or volatile tokens without an operator program, so Otium cannot credit users in USD predictably.
- The 2026 takedowns show regulators and platforms now treat the SDK and its whole reseller chain as targets. Vendor choice (pool owner, consent quality, KYC on proxy buyers) is now a material risk factor for Otium's distribution and code-signing reputation.

### Gaps
- No vendor publishes AV-whitelisting status. Defender entries for Honeygain, Pawns or Mysterium were not found, but absence does not mean they are not flagged.
- Jurisdiction-specific case law on exit-node operator liability (e.g., EU, US) was not found; consult counsel.
- Per-device aggregation rules (one MYST beneficiary for many identities, Grass/Titan multi-account rules) were not confirmed from primary ToS.
