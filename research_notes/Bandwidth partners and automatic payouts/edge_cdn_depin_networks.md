# Edge, CDN and DePIN bandwidth networks (not residential-proxy SDKs): which pay real money in October 2026, and can an operator run them for many users?

Research date: 2026-10-16. Budget-limited pass (~23 tool calls). Many DePIN sources are SEO, airdrop or exchange-blog content. Each finding below is tagged with its source date where known. Anything dated before 2026 is marked **[older]**.

## 1. Edge/CDN/DePIN bandwidth networks in 2026: payout type, amounts, OS/headless, ports, multi-user terms, KYC, earnings API, reputation

### Takeaway
Very few networks pay a liquid token for consumer bandwidth today without a proxy component. These are:
- **AIOZ** (liquid token on Coinbase and Kraken; headless CLI; CLI reward query).
- **Theta** (TFUEL; real payouts are mainly for GPU work).
- **Mysterium** (kept from earlier research).

Most other names fall into one of two groups:
- **Points or pre-TGE:** Teneo, Dawn, Titan (TGE unconfirmed), and Pipe for PoP rewards beyond staking.
- **Residential-proxy or data-scraping services under another name:** GagaNode/Meson, Nodepay, Grass, UpRock, Salad's bandwidth feature. These are out of scope and carry the same abuse risk.

I found no network that publishes per-node USD earnings for plain bandwidth with dated evidence.

### Cited Findings

**AIOZ Network (dCDN / W3AI) — liquid token, headless-capable**
- The AIOZ node shares storage, bandwidth and compute and is rewarded in AIOZ. There is a GUI-less CLI (`aioznode start`) that can run as a Windows Service, systemd or launchd unit. Supported OS: Windows 10 22H2 x64+, Ubuntu 14.04 x64+, macOS 10.15+. `aioznode reward balance` and `aioznode stats` query a local endpoint on default port 1317. Minimum withdrawal is 1 AIOZ. [older, Dec 2023] — [AIOZ dCDN CLI Node Update](https://aioz.network/blog/aioz-dcdn-cli-node)
- Market data around 2026-10-16: AIOZ price ~$0.105, market cap ~$135M (rank ~#228), 24h volume ~$5.5M. It trades on Coinbase (AIOZ/USD), Kraken (AIOZ/USD), Gate, Bybit and KuCoin, across 39 markets. — [CoinGecko AIOZ](https://www.coingecko.com/en/coins/aioz-network)
- Rewards scale with bandwidth and storage served. AIOZ Node v3 AI tasks support NVIDIA GPUs only [older, ~2024]. — [AIOZ Node v3 blog](https://aioz.network/blog/release-aioz-node-v3)
- A 2026 Bitget guide claims "12%–25%" annual yields and minimum specs of 4 cores, 8 GB RAM and 500 GB storage. It gives no source; treat as unreliable. — [Bitget Academy](https://www.bitget.com/academy/aioz-network-token)
- Not found: per-node monthly earnings for 2025–2026, terms on one operator running many nodes, KYC policy, port-forwarding needs.

**Theta Edge Node / EdgeCloud — TFUEL**
- The EdgeCloud client guide was last updated 2026-08-05. Node rewards are paid in TFUEL monthly, between the 1st and 5th. GPU-node and on-demand inference rewards are calculated in USD and converted to TFUEL at the market rate at distribution. The client targets "anyone with idle GPUs", has Linux and Windows installation guides and a CLI guide, and the page says nothing about KYC, multi-node limits or an earnings API. — [Theta EdgeCloud Client Guide](https://docs.thetatoken.org/docs/theta-edgecloud-client-guide)
- Secondary 2026 explainers say edge nodes still earn TFUEL for relaying video, with no THETA stake required. Elite Edge Node staking starts at 10,000 TFUEL; one reward calculator shows ~7.9% APY. TFUEL was ~$0.012–0.014 in late March 2026. — [DEXTools guide 2026](https://www.dextools.io/tutorials/what-is-theta-network-theta-tfuel-video-streaming-guide-2026); [Theta Simplified](https://thetasimplified.com/earn)
- I found no official evidence that bandwidth relay rewards were discontinued, and no current figure for relay-only earnings. — [search summary incl. KuCoin explainer](https://www.kucoin.com/knowledge-base/Concepts/what-is-theta-network-theta)
- Older forum estimates of $80–120 per year assumed TFUEL at $0.10. That is roughly 7–8x the 2026 price, so the figures are obsolete. [older] — [Theta Forums](https://www.thetaforums.com/forum/theta-network-forum-aa/425-how-much-can-you-earn-running-a-theta-edge-node)

**GagaNode (Meson Network) — a residential IP/proxy marketplace, so out of scope**
- Crunchbase describes GagaNode as a Meson Network sub-organization: a "Decentralized Residential IP + Bandwidth marketplace". — [Crunchbase](https://www.crunchbase.com/organization/gaganode)
- A third-party blog published 2026-10-04 says nodes earn "Gaga Credits" while serving proxy requests, convertible to USDT, USDC or Meson tokens. Its estimates are $0.15–0.60 per day, about $10.50–19.50 per month for US/UK/CA/AU and $6–12 per month for EU. It lists binaries for Windows, macOS, Linux and Android, says UPnP or port forwarding is recommended, and says strict NAT cuts output by 50–80%. It also states that ISP terms often prohibit commercial proxy use. The blog is internally inconsistent: thresholds of 10–20k credits versus $2–5, and multi-node figures of $30–75 versus $45–97.50. It has no methodology. — [finanstry.com](https://www.finanstry.com/gaganode-payout-rates-real-bandwidth-earnings-token-rates-setup-guide/)
- The official site (gaganode.com) returned no readable content to my fetch, so I could not check the terms.

**Titan Network — claims dollar earnings, token status unclear**
- The official earn page (© 2026, newest news ~May 2026) says:
  - "Most users earn $5–$30 monthly per device", paid in $TNT, withdrawable to wallets.
  - Desktop apps for Windows, macOS and Linux, plus Android.
  - You may install on "as many devices as you own".
  - Electricity cost is estimated at $2–5 per month.
  - There is an SDK for partners to offer rewards to their own users. This is relevant to an operator model.
  - It also offers a Chrome extension to "lease IP addresses", which is a proxy-like component.
  - The page does not confirm that TNT is live or tradable.
  — [Titan Earn](https://www.titannet.io/earn)
- A May 2025 press release targeted mainnet and TGE in Q2 2025. I found no confirmation that it happened. [older] — [CryptoSlate PR](https://cryptoslate.com/press-releases/titan-network-races-toward-mainnet-and-tge-with-global-node-expansion-across-3-continents/)
- Testnet-era rules tied daily rewards to paid client usage, with 60% withdrawable, in TNT4 testnet tokens. [older] — [Titan docs v0.1](https://titannet.gitbook.io/titan-network-en/galileo-testnet/node-reward-rules-v0.3/node-reward-rules-v0.1)

**Pipe Network (Solana decentralized CDN, PoP nodes)**
- Mainnet was announced 2025-10-08 with ~290,000 PoP nodes, over 60 PB served and over 1 billion files. $PIPE was to be "listed for sale" from Oct 8. Early node operators are "entitled to rewards", with "further details to be disclosed post-launch". Ongoing node income is planned as Jito-backed stake delegation to PoP nodes by bandwidth and uptime. No stablecoin pay was mentioned. [older, Oct 2025] — [PR Newswire](https://www.prnewswire.com/news-releases/pipe-network-launches-mainnet-302577880.html)
- Guides say operators earn PIPE for verified bandwidth, uptime and latency, and that 25% of supply is allocated to node-operator incentives. — [Bitget Web3 Academy](https://web3.bitget.com/en/academy/what-is-pipe-network-pipe-the-depin-infrastructure-powering-decentralized-cdns); [CoinList deep dive](https://blog.coinlist.co/a-deep-dive-into-pipe-network/)
- Not found: 2026 per-node payouts, PIPE price or liquidity in Oct 2026, OS and port requirements, operator terms.

**Points / pre-TGE / collapsed-token projects (value at $0)**
- **Teneo:** points from a browser community node. CoinGecko shows TENEO as not tradable. The points window ended 2026-02-26 with distribution TBA. — [CoinGecko Teneo](https://www.coingecko.com/en/coins/teneo-protocol); [Coinlaunch](https://coinlaunch.space/events-contests/teneo-protocol-airdrop/)
- **Dawn (Andrena):** ~$48.5M raised. A 2026-01-09 AMA said mainnet "within six months", but no TGE or airdrop date has been confirmed. — [MEXC blog](https://blog.mexc.com/news/dawn-network-dawn-airdrop-guide-2026-complete-node-setup-points-system-mid-2026-tge-timeline/); [CoinGabbar](https://www.coingabbar.com/en/crypto-currency-news/dawn-network-airdrop-mainnet-tge-extension-update)
- **Nodepay (NC):** listed on KuCoin, Gate, MEXC, Bitget and OKX, but fell from an ATH of $0.33 to under $0.001, with FDV around $870k (2026 guide). This is effectively worthless as a payout asset. — [Zipmex](https://zipmex.com/blog/nodepay-airdrop/)
- **UpRock (UPT):** the only price data I found is ~858 days old, at ~$0.0017. — [CoinGecko UpRock](https://www.coingecko.com/en/coins/uprock)
- **Gradient, Bless:** no reliable 2026 payout or token data found.
- General caution: "healthy DePIN networks earn revenue from external customers"; weak ones pay from token issuance. — [Altrady](https://www.altrady.com/blog/cryptocurrency/depin-explained-crypto-traders-guide)

**2026 changes to previously covered networks**
- **Mysterium:** a pinned MystNodes post (Mar 4, year presumably 2026) introduced a new reward system. Nodes earn points during the month, which convert to MYST from a monthly reward pool. Claims stay open 30 days and the MYST amount is not fixed. The CLI help article dated 2026-07-20 covers payouts and node identities, which are on-chain Ethereum addresses. Older guides cite a 5 MYST settlement threshold and a 20% network fee; these may be superseded. — [MystNodes X](https://x.com/MystNodes_com); [Mysterium CLI help](https://help.mystnodes.com/en/articles/8006072-mysterium-cli)
- **Grass:** Stage 2 rewards for Epochs 1–19 (2024-10-14 to 2026-06-08) are paid in USDC. Claims opened 2026-07-22, with a deadline of 2027-01-22. A Season 2 token airdrop of ~170M GRASS is listed for H2 2026, with no confirmed date. — [HokaNews](https://www.hokanews.com/2026/07/grass-airdrop-season-2-claim-opens-july.html); [CoinMarketCap updates](https://coinmarketcap.com/fr/cmc-ai/grass/latest-updates/)

**Filecoin Saturn**
- Sources describe Saturn as a community CDN that paid node operators. It launched publicly in November 2022 and removed multi-noding in October 2023. L1 nodes needed a 10 Gbps (burstable) uplink, which makes them datacenter-class, not home PCs. I found no 2024–2026 status. [older] — [Filecoin TLDR](https://wp.filecointldr.io/en/?p=966); [Saturn L1 FAQ](https://github.com/filecoin-saturn/L1-node/blob/main/docs/faq.md); [Filecoin deprecated networks doc](https://docs.filecoin.io/networks/deprecated-networks)

**Reputation and antivirus**
- Talos and others document malware that monetizes victims' bandwidth through proxyware such as Honeygain and Nanowire, and trojanized installers posing as such apps. — [Duo Decipher](https://duo.com/decipher/cybercriminals-beginning-to-leverage-proxyware-for-malware-installation); [The Hacker News 2021](https://thehackernews.com/2021/09/cybercriminals-abusing-internet-sharing.html) [older]
- Trend Micro's exit-node traffic analysis found SQL-injection probing and bulk account registration. It notes that installers "have no means of controlling or even monitoring" exit traffic. [older, 2023] — [Trend Micro](https://trendmicro.com/en_us/research/23/b/hijacking-your-bandwidth-how-proxyware-apps-open-you-up-to-risk.html)

### Inferences
- **AIOZ** is the cleanest technical fit among non-proxy networks for an embedded, headless integration:
  - CLI and service mode on all three OSes.
  - Local reward query, so per-node crediting is possible by reading each node's balance.
  - A liquid token with USD pairs on Coinbase and Kraken.
  - The unknowns are actual per-node yield (likely small), operator/ToS terms, and wallet-per-node handling. Each node withdraws to its own wallet, so the operator would have to hold many keys or set withdrawal addresses to its own wallet.
- **Theta:** its USD-denominated payout is attractive, but meaningful money appears to go to GPU EdgeCloud nodes. That makes it a GPU-compute integration rather than a bandwidth one. Relay-only income is unquantified and likely tiny at a ~$0.013 TFUEL price.
- **GagaNode/Meson, Nodepay, Grass, UpRock and Salad's bandwidth feature** are proxy or scraping products by function. They carry the same legal and abuse risk as residential-proxy SDKs and should be grouped with them.
- **Titan** is the only project offering an explicit partner SDK for third-party apps to reward their own users. Its token liquidity is unverified, and its IP-leasing extension suggests proxy-like use. It is a "watch" candidate, not a "ship" one.
- No CDN/edge project in this set publishes a verifiable per-node payout ledger for consumer bandwidth. Expect most revenue for plain bandwidth to be token emissions, not customer fees.

### Gaps
- Official GagaNode terms (pay currency, operator or multi-user rules, SDK): the site fetch returned nothing.
- AIOZ 2026 per-node earnings, ToS on operators, KYC.
- Theta relay-only (non-GPU) TFUEL per node per month in 2026.
- Pipe Network 2026 PoP reward mechanics, OS/hardware, PIPE liquidity.
- Titan TNT tradability and mainnet/TGE status.
- Gradient, Bless, Blockcast, Huddle01, NKN, Sentinel dVPN, Livepeer: not researched for lack of budget. They are possible follow-ups (Livepeer pays ETH fees for GPU transcoding, which is not bandwidth).
- Commercial P2P CDNs (the old Peer5, now Microsoft eCDN; Streamroot, now Lumen Mesh Delivery; System73, SwarmCloud) appear to be viewer-side WebRTC meshes that never paid end users. I did not verify this with sources here.
- Antivirus detection status of specific DePIN node binaries (AIOZ, Theta, Titan, GagaNode) was not found.

## 2. Network-measurement or data-collection programs that pay for running a probe and allow embedding

### Takeaway
I found no measurement program that pays cash for hosting a probe. RIPE Atlas pays only measurement credits. Paid "web data collection" programs (Grass, UpRock, Teneo, Nodepay) are scraping/proxy services or points programs.

### Cited Findings
- RIPE Atlas hosts can run a hardware or software probe on a home connection "where you have permission to do so". They earn credits, not money, proportional to uptime, usable for their own measurements. — [RIPE Host a Probe](https://www.ripe.net/analyse/internet-measurements/ripe-atlas/host-a-probe/); [RIPE Atlas FAQ](https://atlas.ripe.net/docs/faq/probes-hosts-anchors-sponsors/)
- IPinfo launched ProbeNet in Q1 2026 as a measurement network customers can use: free, up to 25 measurements per day. The sources do not say it pays third-party hosts. — [IPinfo Q1 2026 updates](https://ipinfo.io/blog/q1-2026-product-updates)
- Teneo (social-data points) and Grass (web-data scraping, USDC Stage 2) are the "data collection" DePINs; see Section 1. — [CoinGecko Teneo](https://www.coingecko.com/en/coins/teneo-protocol); [HokaNews Grass](https://www.hokanews.com/2026/07/grass-airdrop-season-2-claim-opens-july.html)

### Inferences
- A measurement probe adds no revenue. At most it is a goodwill or credibility feature, such as bundling a RIPE Atlas software probe with an opt-in.

### Gaps
- I could not verify any commercial vantage-point network that pays hosts (for example IPinfo, Kentik, Catchpoint or ThousandEyes host programs). None surfaced in search.

## 3. Operator precedents: apps that bundle bandwidth nodes and how they pay users

### Takeaway
- **Kryptex + Mysterium** is the clean precedent: the app runs the node, converts MYST, and pays users in BTC.
- **Salad's bandwidth sharing** is the cautionary precedent: an opaque proxy partner, abuse traffic from users' IPs, and threatened class action (Oct 2025).
- **Titan** advertises a partner SDK for exactly this model.

### Cited Findings
- **Kryptex.** Users enable a "Network Card" switch in Kryptex PRO mode. The app runs a Mysterium node, and Kryptex converts MYST to Bitcoin once 5 MYST accrues, paying it out in the user's Kryptex balance. Launched around Dec 2023. [older but ongoing] — [Kryptex news](https://www.kryptex.com/en/news/kryptex_x_mysterium); [MystNodes blog](https://blog.mystnodes.com/blog/kryptex-launch); [Mysterium on X](https://x.com/MysteriumNet/status/1734238865387348347)
- **Salad (IBTimes, 2025-10-20).**
  - Salad's bandwidth-sharing nodes reportedly acted as open proxy exits for an unnamed VPN/proxy partner.
  - One user received a Netflix notice citing 30,000 failed logins from their IP.
  - Users reported IP bans, CAPTCHAs and legal warnings.
  - "Dozens" of users were reportedly preparing a class action.
  - Salad's response stressed that the feature was optional and opt-in.
  This is a single-outlet report and was not independently verified. — [IBTimes](https://www.ibtimes.com/salads-bandwidth-sharing-causing-community-uproar-amid-abuse-legal-threats-3787696)
- **Titan.** It offers an SDK for partners to offer rewards to their own users, and allows unlimited devices per owner. — [Titan Earn](https://www.titannet.io/earn)

### Inferences
- The Salad incident shows that unfiltered exit traffic is the main liability, even with consent. Edge/CDN nodes that serve cached content, such as AIOZ, Theta relay and Pipe PoP, do not route third-party requests out of the user's IP. That is a materially lower risk class than proxy/VPN exits.
- The Kryptex model handles the token-to-fiat problem: the operator converts tokens centrally and credits users in its own unit. It is the direct template for Otium.

### Gaps
- Exact Kryptex payout split and fees for Mysterium.
- Whether Salad's feature is still live in 2026, and which partner powered it.

## 4. Realistic net revenue per typical home connection (Europe/Nordics vs US), including token conversion

### Takeaway
I found no rigorous, dated per-node revenue data for non-proxy edge/CDN networks. The only explicit numbers are self-reported (Titan: $5–30 per device per month) or come from unreliable blogs (GagaNode, which is proxy: ~$6–12 per month EU, ~$10–20 per month US). Realistic expectations for non-proxy edge nodes on a home PC are low single-digit USD per month or less, before conversion costs.

### Cited Findings
- Titan claims $5–30 per month per device, with $2–5 per month electricity. This is a self-claim, and TNT liquidity is unverified. — [Titan Earn](https://www.titannet.io/earn)
- GagaNode estimates (blog, 2026-10-04): Tier 1 (US/UK/CA/AU) $10.50–19.50 per month; Tier 2 (EU/JP/KR) $6–12 per month; datacenter $0.60–1.50 per month; strict NAT cuts 50–80%. These are unverified and internally inconsistent. — [finanstry.com](https://www.finanstry.com/gaganode-payout-rates-real-bandwidth-earnings-token-rates-setup-guide/)
- Grass users report single-digit-dollar payouts after a year or more of running nodes (secondary reporting). — [search summary of MEXC/CoinMarketCap/Trustpilot items](https://coinmarketcap.com/fr/cmc-ai/grass/latest-updates/)
- Token liquidity for conversion:
  - AIOZ has USD pairs on Coinbase and Kraken, ~$5.5M in daily volume (Oct 2026). — [CoinGecko AIOZ](https://www.coingecko.com/en/coins/aioz-network)
  - TFUEL was ~$0.012–0.014 in March 2026. — [DEXTools](https://www.dextools.io/tutorials/what-is-theta-network-theta-tfuel-video-streaming-guide-2026)
  - Nodepay NC is below $0.001. — [Zipmex](https://zipmex.com/blog/nodepay-airdrop/)

### Inferences
- Conversion cost is mainly exchange fees plus withdrawal gas. For AIOZ, Coinbase or Kraken USD pairs make automated conversion feasible. Per-node withdrawals (min 1 AIOZ, ~$0.10) are trivial in size, so batching at the operator level matters more than fees.
- **Nordics and Europe:** edge/CDN networks typically weight by demand and location, so EU nodes likely earn less than US ones; the GagaNode tiering implies roughly 0.6x. Nordic fiber with symmetric upload may help on bandwidth-weighted networks. This is not quantified by any source.
- **Planning figure for Otium:** treat non-proxy edge/CDN income as about $0–3 per month per home PC. Upside depends on GPU (Theta EdgeCloud) or on proxy-class products (excluded). Mysterium remains the only bandwidth candidate with an existing operator precedent and liquid payouts.

### Gaps
- No independent ROI tracker data for 2025–2026 for AIOZ, Theta relay, Pipe or Titan nodes.
- No Nordic-specific figures for any network.
- Current TFUEL and PIPE prices for October 2026 were not retrieved.
