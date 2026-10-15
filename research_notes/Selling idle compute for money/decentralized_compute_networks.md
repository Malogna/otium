# Decentralized / Token-Based (DePIN) Compute Networks for Consumer CPU/GPU Contribution (as of Oct 2026)

Research date: 2026-10-15. Sources are a mix of official docs (io.net, Golem, Nosana, Akash, Livepeer, Flux, Render KB) and secondary aggregators (BlockEden blog, yellow.com, gamefi-roi, securities.io, Binance Square, KuCoin explainers). Secondary figures are flagged. Many network docs carry no publication date; "(c) 2026" footers are noted where seen. Some networks (Aethir checker, Spheron Fizz, Gaimin, Exabits, NodeShift, Theta) had little or no primary 2026 documentation surfaced in this session.

## Current status in 2026 (active / permissioned / pivots / recent changes)

### Takeaway
The "big" compute DePINs (Akash, Render, io.net, Aethir) are alive and increasingly chasing data-center/enterprise GPUs (H100/H200), with tokenomics shifting from emissions toward revenue-linked models (Akash BME, Mar 2026; Nosana NNP-0001). Consumer-PC-friendly options are a shrinking subset: io.net worker (incl. Apple Silicon M3+), Nosana (Linux-only now), Golem (Linux-only CPU), Theta Edge Node, Spheron Fizz, and the non-crypto/semi-crypto Salad/Vast.ai/Clore marketplaces. Gaimin, Exabits and NodeShift are either unclear in status or not consumer-supply networks.

### Cited Findings
**Akash Network**
- Community approved Burn-Mint Equilibrium (BME, AEP-76) in March 2026 (roadmap completion date 3/23/2026): every on-chain compute payment market-buys AKT and burns it, issuing a non-transferable settlement credit to providers; BME testnet launched Feb 17, 2026 — [Akash Roadmap 2026](https://akash.network/roadmap/2026/); [Crypto Community News substack](https://cryptocommunitynews.substack.com/p/akash-networks-bme-testnet-begins)
- Independent (non-official) analysis dated Aug 2026: net burn ~5,950 AKT/day at launch rising to ~11,105 AKT/day by late July 2026; claims 70% of every block reward diverted to the community pool — [DEV Community, AKT inflation analysis July 2026](https://dev.to/mrnasdog/akt-inflation-analysis-july-2026-a-lower-mint-still-ahead-of-the-burn-1fk6) (single blog source, unverified)
- Provider Incentives Pilot (PIP) targeted high-density GPUs (A100s): requested $5M, expected >1,000 A100s, ~120 days; marked final. "Starcluster" described only as "on deck" in 2025 TIP01 proposal (anticipated start Aug 11, 2025) — [Akash AEP-26 PIP](https://akash.network/roadmap/aep-26/); status of Starcluster in 2026 not found
- Secondary: record $5M compute spend Q1 2026; utilization >80%; usage +428% YoY; H100 at $1.20–1.80/hr; Reserved Instances scheduled Aug 2026; AkashML 1.7B tokens/day on OpenRouter — [BlockEden, Apr 12 2026](https://blockeden.xyz/blog/2026/04/12/depin-revenue-pivot-token-subsidies-ai-compute-akash-render-ionet/) (secondary; self-reported utilization)
- New lease creation +42% QoQ in Q3 2025 (19k → 27k leases) — [BlockEden, Mar 12 2026](https://blockeden.xyz/blog/2026/03/12/depin-compute-revenue-pivot-akash-ionet-aethir/)

**io.net**
- Active; docs list macOS, Windows, Linux (Ubuntu) and HiveOS install paths — [io.net quick-start](https://io.net/docs/guides/workers/quick-start-guide)
- A Freshdesk support article states "Adding new workers to the IO network is currently disabled" (undated; may be stale; the current docs show no such notice) — [io.net support: Add worker macOS](https://support.io.net/en/support/solutions/articles/156000014581-how-to-add-a-new-worker-macos)
- Block rewards began June 25, 2024 12:00 UTC; Ryzen Threadripper eligibility removed (July 5, year unstated); IOG Foundation "may cap eligible devices" in the future — [io.net Block Rewards doc](https://io.net/docs/guides/block-rewards/block-rewards.md)
- Secondary: ATH utilization for AI training in March 2026; path to ~$20M annualized revenue; ~139,000 GPUs on network; >$20M cumulative verifiable on-chain revenue; Q1 2026 focus on high-end GPU inventory — [BlockEden Apr 2026](https://blockeden.xyz/blog/2026/04/12/depin-revenue-pivot-token-subsidies-ai-compute-akash-render-ionet/); [BlockEden Mar 2026](https://blockeden.xyz/blog/2026/03/12/depin-compute-revenue-pivot-akash-ionet-aethir/)

**Render Network**
- Onboarding is still a sign-up form + onboarding queue; nodes inactive ≥3 months are expired and must reapply — [Render KB: How to get started](https://know.rendernetwork.com/general-render-network/what-role-am-i/how-to-get-started-1)
- Render Compute Network (AI) has begun onboarding US-based node operators for a testing phase with US customers; availability rewards start from first connection — [Render Foundation monthly report (Medium)](https://rendernetwork.medium.com/render-network-foundation-monthly-report-24e14ea50e13)
- Secondary: Dec 2025 launch of Dispersed.com AI compute subnet (H100/H200 at $1.75/hr); "~$38M monthly revenue" Jan 2026 and 5,600 active GPU nodes; Hollywood studios 35% of 2025 output — [BlockEden Apr 2026](https://blockeden.xyz/blog/2026/04/12/depin-revenue-pivot-token-subsidies-ai-compute-akash-render-ionet/). The $38M/month figure looks implausibly high versus peers and is unverified. A price-news piece claims 5,600 node operators at 85–95% utilization — [MEXC news](https://www.mexc.com/news/460591) (unverified)
- No evidence found of a product called "RenderOS".

**Nosana**
- Active on Solana mainnet; docs carry "(c) 2026". Windows (incl. WSL2) is being deprecated for GPU hosts; only native Linux officially supported — [Nosana docs: grid-windows](https://docs.nosana.com/hosts/grid-windows); [Nosana: Running the Host](https://learn.nosana.com/hosts/grid-run)
- NNP-0001 (2025 tokenomics proposal): ends idle ~20% APR staking; passive pool capped at 720,000 NOS over 12 months, passive staking ends Nov 28, 2026 unless renewed; introduces minimum stake for "cost-indexed hosts", host reputation scores, non-transferable dNOS rebates burned on redemption — [yellow.com NOS asset page](https://yellow.com/it/asset/nos) (aggregator; whether fully implemented unconfirmed)

**Golem Network**
- Active; provider Docker image `golemfactory/provider` example version 0.17.9 with a status date of 2026-07-31 — [Golem provider installation](https://docs.golem.network/docs/providers/provider-installation)
- 2025 narrowing of focus to AI inference and GPU provisioning; GLM described as ~$0.15–0.35 (undated) and minority market share vs Akash/io.net in 2026 — [cryptoactu.com](https://cryptoactu.com/quest-ce-que-golem-gnt/); [OneKey GLM report](https://onekey.so/blog/ecosystem/glm-deep-research-report-token-utility-roadmap-and-price-outlook) (secondary)
- GPU provider program ("GPU Golem Live") is a bootable image flashed to an SSD — [Golem GPU docs](https://docs.golem.network/docs/providers/gpu/gpu-golem-live)

**Aethir**
- Secondary: led DePIN 2025 revenue at $127.8M from enterprise customers (94 countries); $147M ARR by Q3 2025 — [BlockEden Mar 2026](https://blockeden.xyz/blog/2026/03/12/depin-compute-revenue-pivot-akash-ionet-aethir/)
- 2026: eATH program (one-year lock-up, keeps earning ATH, redemption window 30 days after June 13, 2026; Pendle liquidity); Checker node buyback prices move with market — [ChainCatcher](https://www.chaincatcher.com/en/article/2183396)
- Supply side is enterprise GPU clusters ("Aethir Cloud hosts"); consumer participation is via Checker Node licenses (sold 2024, 100,000 nodes, tiered pricing on Arbitrum, transferable after 1 year) and the "Aethir Edge" consumer device — [Decrypt, Feb 2024](https://decrypt.co/218993/aethir-unveils-its-first-decentralized-ai-node-sale); [DEXTools KO guide 2026](https://www.dextools.io/tutorials/what-is-aethir-ath-decentralized-gpu-cloud-depin-guide-2026-ko)

**Fluence**
- Pivoted from CPU cloud to GPU compute (GPU containers launched Oct 3, 2025; VMs/bare metal planned); partnership with Spheron to expand provider supply — [Decrypt, Oct 2025](https://decrypt.co/342785/fluence-launches-global-and-affordable-gpu-compute-for-ai)
- 2026: Fluence Rollup sunset; FLT/USDC migrated to Ethereum L1; pFLT→FLT 1:1; migration claim window open until April 1, 2027; FLT staking now on Ethereum — [Fluence Telegram](https://t.me/s/fluence_official/501)
- Roadmap: GPU node onboarding Q3 2025; Guardian verifiers/inference routing Q2 2026 (delivery unverified) — [Fluence Telegram](https://t.me/s/fluence_official/351)

**Flux**
- ArcaneOS (released 2025) is now the only setup eligible for Progressive Node Rewards (FluxCloud app revenue split 80% ArcaneOS operators / 20% hosts); legacy nodes keep only block rewards — [Flux docs: What are FluxNodes](https://docs.cloud.runonflux.com/flux/fluxnodes); [Flux legacy node docs](https://docs.runonflux.com/fluxnodes/legacy-fluxnode/)
- PoUW v2 replaced mining pools; 30-second blocks; 14 FLUX per block (9 Stratus / 3.5 Nimbus / 1 Cumulus) — [runonflux.com token explainer](https://runonflux.com/fueling-the-network-flux-token-explained/)
- "Titan Node" option: stake ≥50 FLUX, InFlux Technologies runs the hardware — [Flux docs](https://docs.cloud.runonflux.com/flux/fluxnodes)

**Livepeer**
- Active; docs last modified ~March 2026, referencing LIP-89 (March 2026: 10% of each round's new LPT to treasury) — [Livepeer docs: staking LPT](https://docs.livepeer.org/v2/orchestrators/advanced-setup/staking-LPT)
- AI job routing uses capability, price and latency rather than stake weight; AI inference is a path for operators with limited stake, alongside joining a pool as a worker — [Livepeer docs: operator rationale](https://docs.livepeer.org/v2/orchestrators/guides/operator-considerations/operator-rationale); [Livepeer earning model](https://docs.livepeer.org/v2/orchestrators/guides/staking-and-rewards/earning-model)

**Theta EdgeCloud / Edge Node**
- Edge Node reportedly moved from beta to full release; EdgeCloud Hybrid beta (June 2025) lets contributors supply GPU and earn TFUEL — [DePIN Hub, Jun 25 2025](https://9o1hbtdj3x.depinhub.io/news/theta-launches-edge-cloud-hybrid-beta-for-high-performance-computing-23342); [Binance Square](https://www.binance.com/square/hashtag/THETAUSAT) (user-generated)
- 30M THETA treasury unstaked to fund 2026–2027 growth incl. EdgeCloud compute; Aug 2026 AI agents able to discover/deploy GPUs (Binance Square, unverified) — [Binance Square](https://www.binance.com/square/hashtag/THETAUSAT)

**Spheron (Fizz Nodes)**
- Fizz Nodes launched Oct 2024 as lightweight provider nodes for PCs/laptops; >20,000 nodes (Dec, year unstated) and >25,000 by early 2025; SPON token TGE July 2025 on Base (single aggregator) — [Investing.com](https://investing.com/news/cryptocurrency-news/spheron-introduces-fizz-node-for-flexible-ai-compute-resources-3642904); [Coinranking](https://coinranking.com/coin/wra4uV6iN+spheronnetwork-spon/about); [TechFlow](https://www.techflowpost.com/en-US/article/22350)
- No 2026-specific Fizz updates found.

**Gaimin**
- No 2026 primary information found. MEXC pages tag GMRX "delisted"/"not listed" (~Aug 2026); Finary (June 16, 2026) still lists markets on Gate, KuCoin, PancakeSwap V3 (BSC), circulating supply ~55.09B GMRX — [MEXC GMRX](https://www.mexc.com/ro-RO/price/gaimin); [Finary](https://finary.com/fr/crypto/coins/gaimin)
- Historic model: app uses idle gaming GPU, returns "up to 90%" of rewards, paid in GMRX; promotional "up to 70 GMRX/day" for 20 hrs connected — [Investing News (2022–23)](https://investingnews.com/gaimin-s-early-access-event-opens-its-platform-and-monetization-app-to-gamers/) (old, promotional)

**NodeShift / Exabits**
- NodeShift: Abu Dhabi-based decentralized cloud reselling spare capacity from independent data centers and Web3 networks (A100, RTX 4090); latest funding $1M seed Feb 24, 2026; total ~$4.26M — [CB Insights](https://www.cbinsights.com/company/nodeshift/financials); [yespress NodeShift](https://yespress.io/nodeshift.md)
- Exabits: San Mateo GPU cloud that owns/operates thousands of GPUs (GB200/B200/H200/H100/A100/RTX 5090/4090); $15M seed Dec 2024 (Hack VC), "tokenize GPUs"; ~$12M ARR (company-reported) — [TechBullion](https://techbullion.com/exabits-powers-enterprise-ai-with-cutting-edge-gpu-cloud-and-proven-revenue-growth/); [Techleap](https://finder.techleap.nl/news/feed/exabits-raises-15m-to-tokenize-gpus)

**Other significant consumer-relevant networks found**
- Salad (SaladCloud): download app, sign in, click Start (~5 minute setup); 20–25% platform cut; RTX 4090 ~$0.30–0.60/hr to host (one guide) vs $0.50–1.20/hr (another) — [gpunex blog](https://www.gpunex.com/blog/sell-computing-power-best-platforms); [gpunex DE](https://www.gpunex.com/de/blog/sell-computing-power-best-platforms) (conflicting secondary)
- Vast.ai: 10–15% fee, host sets price, works best on Linux; RTX 4090 $0.55–1.50/hr gross — [gpunex blog](https://www.gpunex.com/blog/sell-computing-power-best-platforms)
- Clore.ai: RTX 4090 ~$0.139/hr across 192 offers, paid in CLORE — [miningboard RTX 4090](https://miningboard.com/de/rigs/nvidia-rtx-4090/ai-rental)

**Sector context**
- ~$10B circulating DePIN market cap produced ~$72M on-chain revenue in FY2025; leading networks ~$150M on-chain revenue in Jan 2026; fewer than 20 of 650+ DePIN projects generated meaningful non-token revenue (late 2024) — [BlockEden Mar 21 2026](https://blockeden.xyz/blog/2026/03/21/depin-march-2026-reality-check-650-projects-19b-market-cap-revenue/); [BlockEden Apr 12 2026](https://blockeden.xyz/blog/2026/04/12/depin-revenue-pivot-token-subsidies-ai-compute-akash-render-ionet/) (secondary, inconsistent methodology)

### Inferences
- Summary matrix (consumer one-click viability):

| Network | Consumer PC viable? | Windows | macOS | Linux | Gate |
|---|---|---|---|---|---|
| io.net worker | Yes (best fit among crypto networks) | Yes (NVIDIA) | Yes, Apple M3/M4 via Docker Desktop | Yes | Stake per device for block rewards |
| Nosana | Partial (Linux NVIDIA only) | Deprecated | No | Ubuntu 20.04+ | Benchmark; stake for some host classes (NNP-0001) |
| Golem | Partial (Linux x86-64 + KVM, CPU) | No (WSL2 not supported) | No | Yes | None |
| Theta Edge Node | Yes (low pay) | Yes | Yes | Reported | None found |
| Spheron Fizz | Likely yes (status 2026 unclear) | ? | ? | ? | 50% daily uptime |
| Render | No for one-click (queue, high spec, OctaneRender) | Yes | ? | ? | Application queue |
| Akash provider | No (K8s cluster, public IP, RPC node, 4-host recommended) | No | No | Ubuntu 22.04 | 5 AKT per bid deposit |
| Livepeer | Only as pool worker / AI orchestrator (technical) | — | — | Linux | LPT stake for video active set |
| Flux | No (dedicated AMD64 TPM2.0 box, 1,000+ FLUX collateral) | No | No | ArcaneOS | Collateral |
| Fluence | No (data-center GPU providers, FLT staking) | No | No | — | Stake |
| Aethir | No compute for consumers; checker licence = node-sale product | — | — | Linux CLI | Licence purchase |
| Gaimin | Historically yes; 2026 status doubtful | Yes | ? | ? | — |
| NodeShift / Exabits | No (B2B clouds, not open supply) | — | — | — | — |

- The general direction (BME, NNP-0001, Flux ArcaneOS revenue sharing) is that rewards are moving from emissions to paid demand, which will compress returns for low-end consumer hardware.

### Gaps
- No official 2026 confirmation of whether io.net worker onboarding is open or paused (conflicting support article).
- Aethir Edge device pricing/specs and current Checker licence economics not found from primary sources.
- Spheron Fizz 2026 status, OS support and payout data not found.
- Gaimin 2026 operating status unconfirmed; only exchange-listing signals.
- Render $38M/month revenue claim unverified and likely erroneous.

## Supported OS and platform requirements (Windows / Linux / macOS, Docker/WSL, dedicated machine)

### Takeaway
Only io.net (Win/Linux/macOS M3+) and Theta Edge Node (Win/macOS, Linux reported) natively cover all three desktop OSes. Golem and Nosana are now Linux-only (Golem explicitly excludes WSL2 and macOS; Nosana is deprecating Windows/WSL2). Akash and Flux require dedicated Linux machines/clusters.

### Cited Findings
- io.net: separate install guides for macOS, Windows, Linux (Ubuntu), HiveOS — [io.net quick-start](https://io.net/docs/guides/workers/quick-start-guide)
- io.net macOS: supports "Apple chip processors (M3, M4)"; Mac qualifies if chip is M3 or higher; Docker Desktop required; ~20GB container; disable sleep/power saving; capping Docker RAM at ≤8GB hurts PoW (most common on Macs) — [io.net install on macOS](https://io.net/docs/guides/workers/install-on-macos.md). An older support article lists M1/M2/M3 and 60GB free disk — [io.net support](https://support.io.net/en/support/solutions/articles/156000014581-how-to-add-a-new-worker-macos) (conflict; docs page is newer)
- io.net block rewards: M3 devices with 8GB can be configured; minimum configuration 16GB — [io.net Block Rewards](https://io.net/docs/guides/block-rewards/block-rewards.md)
- io.net Windows: install worker + NVIDIA drivers — [io.net quick-start](https://io.net/docs/guides/workers/quick-start-guide)
- Golem: "x86-64 Linux is the only supported platform"; ARM/Apple Silicon and macOS unsupported; requires nested virtualization / `/dev/kvm`; Docker Desktop for Windows (WSL 2) not supported — [Golem provider installation](https://docs.golem.network/docs/providers/provider-installation)
- Nosana: Windows (incl. WSL2) being deprecated for GPU hosts; only native Linux officially supported; Ubuntu 20.04+; Docker (rootless config) + NVIDIA Container Toolkit — [Nosana grid-windows](https://docs.nosana.com/hosts/grid-windows); [Nosana Running the Host](https://learn.nosana.com/hosts/grid-run)
- Akash: providers tested on Ubuntu 22.04; full Kubernetes cluster reachable from the internet; recommended ≥4 hosts for production — [Akash K8s cluster for providers](https://akash.network/docs/providers/build-a-cloud-provider/kubernetes-cluster-for-akash-providers/kubernetes-cluster-for-akash-providers/); [Akash Build a Cloud Provider](https://akash.network/docs/deployments/praetor-app/akash-provider/build-cloud-provider)
- Flux ArcaneOS: AMD64, TPM 2.0, UEFI; Secure Boot constraints for virtualized installs — [securities.io Flux](https://www.securities.io/ru/investing-flux-network/) (secondary)
- Theta Edge Node: official setup page offers Windows and macOS installers; other guides list Linux too; a third party reports a native Apple Silicon build (4 cores, 16GB RAM, 64GB disk; unverified) — [RedCap Digital Medium](https://redcapdigital.medium.com/earn-tfuel-40-using-your-spare-laptop-and-sharing-bandwidth-f4fa11d7de42); [CashPilot guide](https://raw.githubusercontent.com/GeiserX/CashPilot/HEAD/docs/guides/theta-edge.md); [Rosetta Check](https://rosettacheck.com/apps/2tfukg44)
- Aethir Checker: Linux CLI package run under `screen` on a VPS (community guide, v1.0.2.0) or delegated via NodeOps — [Beehiiv guide](https://selected-investments.beehiiv.com/p/aethir-cloud-setup-checker-node-vps); [NodeOps docs](https://docs.nodeops.network/Guides/Console/aethir)
- Spheron Fizz: designed for "personal computers or laptops" — [Investing.com](https://investing.com/news/cryptocurrency-news/spheron-introduces-fizz-node-for-flexible-ai-compute-resources-3642904)
- Vast.ai "works best on Linux with a stable connection" — [gpunex blog](https://www.gpunex.com/blog/sell-computing-power-best-platforms)

### Inferences
- A cross-platform app would realistically need: (a) a Windows path via io.net / Salad-style native or Docker Desktop; (b) a macOS path almost solely via io.net (M3+) or Theta; (c) a Linux path covering io.net, Nosana, Golem (CPU), and Vast.ai/Clore. Nosana and Golem would require the app to manage Linux VMs or dual-boot on Windows/macOS, which is fragile (Golem explicitly rejects WSL2; nested KVM inside a Windows VM is non-trivial).

### Gaps
- Spheron Fizz, Gaimin current OS support not found.
- Theta Linux installer not confirmed on official page.
- io.net Windows exact requirements (Win10/11, WSL2/Docker Desktop) not extracted.

## Hardware requirements, uptime, connectivity and staking/collateral

### Takeaway
Consumer GPUs (8GB+ NVIDIA) meet the floor for io.net, Nosana, Render, Golem GPU; but stake/collateral gates are common (io.net device-level stake for block rewards, Akash 5 AKT per bid, Flux 1,000–40,000 FLUX, Livepeer LPT for video, Nosana stake for some host classes, Aethir licence purchase).

### Cited Findings
- io.net block-reward nomination: uptime green for past 5 hours; device meets minimum stake (amount not stated on page, linked to IO Staking guide); valid Solana wallet; hardware multiplier > 0; connectivity tier > 0; uptime and PoW rechecked at block close — [io.net Block Rewards](https://io.net/docs/guides/block-rewards/block-rewards.md)
- Akash: control plane min 8 CPU/16GB/50GB (rec. 16/32/100); worker min 4 CPU/8GB/100GB; 5 AKT deposit per bid won (refundable); must run own RPC node (strict); outbound internet + reachable from internet — [Akash Build a Cloud Provider](https://akash.network/docs/deployments/praetor-app/akash-provider/build-cloud-provider); [Akash setup & installation](https://akash.network/docs/providers/setup-and-installation)
- Nosana: benchmark at registration; aggregator lists compatible NVIDIA GPU, ≥12GB RAM, ≥256GB SSD — [Nosana Running the Host](https://learn.nosana.com/hosts/grid-run); [gamefi-roi Nosana](https://gamefi-roi-web.onrender.com/opportunities/nosana-gpu-host) (secondary). 2023 Test Grid post: 4GB RAM, RTX 4090/4080 examples (outdated) — [Nosana blog Dec 2023](https://nosana.io/blog/test-grid-phase-1-accelerating-the-ai-and-gpu-computing-revolution)
- Render: CUDA NVIDIA GPU, CUDA ≥10.1, driver ≥419; VRAM min 6GB (8GB+ preferred); 32GB+ RAM; 100GB free disk; strong network; avoid concurrent heavy GPU workloads (hurts reputation score) — [Render KB](https://know.rendernetwork.com/general-render-network/what-role-am-i/how-to-get-started-1); [Render KB multiple programs](https://know.rendernetwork.com/general-render-network/what-role-am-i/how-to-get-started-1/running-multiple-programs-or-tasks)
- Golem GPU: IOMMU support, NVIDIA 30xx+ with ≥8GB VRAM — [Golem GPU docs](https://docs.golem.network/docs/providers/gpu/gpu-golem-live). Golem CPU provider image defaults: 2 cores, 1GiB RAM, 20GiB disk (configurable) — [Golem provider installation](https://docs.golem.network/docs/providers/provider-installation)
- Flux collateral: Cumulus 1,000 FLUX, Nimbus 12,500, Stratus 40,000; protocol does not lock collateral (operator can delete node and sell); Cumulus 8GB RAM/220GB SSD up to Stratus 64GB/880GB; public network setup, benchmark — [runonflux.com](https://runonflux.com/?p=5799); [securities.io](https://www.securities.io/ru/investing-flux-network/)
- Livepeer: solo video orchestrators must be in the top 100 by bonded stake; borrowing LPT not supported; AI selection by capability match, latency/uptime history, price vs gateway max, warm models — [Livepeer earning model](https://docs.livepeer.org/v2/orchestrators/guides/staking-and-rewards/earning-model); [Livepeer staking](https://docs.livepeer.org/v2/orchestrators/advanced-setup/staking-LPT)
- Spheron Fizz: liveness rewards require ≥50% uptime in a 24h window — [TechFlow](https://www.techflowpost.com/en-US/article/22350)
- Aethir Checker: requires a Checker Node Key License (purchased NFT), EVM wallet, ETH on Arbitrum for gas; up to 100 licences per burner wallet — [NodeOps docs](https://docs.nodeops.network/Guides/Console/aethir); [Blast API guide](https://docs.blastapi.io/blast-documentation/tutorials-and-guides/naas-app/delegate-license-on-aethir)
- Fluence: stake scales with capacity (CPU era: "$170M stake in FLT" to onboard planned hardware) — [Fluence blog: FLT staking](https://www.fluence.network/blog/flt-staking-for-compute-resources/) (older, CPU-era)
- Salad Star Chef: 3,000 min (~50 hrs)/week of chopping gives priority for high-paying jobs — [Salad support](https://support.salad.com/article/337-star-chef-qualifications-and-benefits)

### Inferences
- For a one-click app, any network requiring the user to buy tokens up front (io.net stake for block rewards, Flux, Aethir licence, Livepeer LPT) becomes a financial-product decision rather than a "donate idle compute" flow, adding regulatory and UX friction.

### Gaps
- io.net exact per-device IO stake amount not found.
- Nosana minimum stake amount under NNP-0001 not found.
- Fluence GPU-era stake per GPU not found.

## Payout mechanics (token, wallet, fiat conversion, fees)

### Takeaway
All crypto networks pay in their native token to a self-custodied wallet (Solana for io.net/Nosana, EVM/Polygon for Golem, Arbitrum for Aethir/Livepeer, Theta chain for TFUEL); none provides native fiat off-ramp. Salad is the outlier (in-app balance redeemable for gift cards/etc., not crypto).

### Cited Findings
- io.net: block rewards distributed hourly in $IO; 95% of emission to GPUs, 5% to CPUs (ratio adjustable); block rewards incur no fees; worker (job) earnings incur 0.25% fee; rewards potentially slashed before distribution; self-custodial wallet required, exchange deposit addresses unsupported because claims are smart-contract interactions — [io.net Block Rewards](https://io.net/docs/guides/block-rewards/block-rewards.md); [io.net Rewards & Wallets](https://io.net/docs/guides/workers/rewards-wallets)
- io.net job pay: jobs finishing within a day paid immediately; multi-day paid daily; unavailable time unpaid; nothing paid if a worker stops within its first hour — [io.net Earnings & Rewards](https://io.net/docs/guides/workers/earnings-rewards.md)
- Nosana: earnings in $NOS; node's Solana keypair stored at `~/.nosana/nosana_key.json`; view at host.nosana.com — [Nosana Running the Host](https://learn.nosana.com/hosts/grid-run)
- Golem: GLM to an ERC-20 address (`YA_ACCOUNT`) with Polygon payments; image defaults to testnet; set `YA_PAYMENT_NETWORK_GROUP=mainnet`; default price 0.1 GLM/thread-hour — [Golem provider installation](https://docs.golem.network/docs/providers/provider-installation)
- Akash post-BME: providers receive a non-transferable settlement credit, with AKT bought and burned on each payment — [Akash Roadmap 2026](https://akash.network/roadmap/2026/)
- Theta: TFUEL; older guide says create wallet at wallet.thetatoken.org — [RedCap Medium](https://redcapdigital.medium.com/earn-tfuel-40-using-your-spare-laptop-and-sharing-bandwidth-f4fa11d7de42)
- Clore.ai pays in CLORE — [miningboard](https://miningboard.com/de/rigs/nvidia-rtx-4090/ai-rental)
- Salad balance starts appearing after ~15 minutes — [Salad support](https://support.salad.com/article/127-how-long-salad-takes-to-start-earning)

### Inferences
- An aggregator app would need embedded wallet management for multiple chains (Solana, EVM/Polygon, Arbitrum, Theta) or require users to bring their own wallets; generating/holding user keys makes the app a custodian, which raises money-transmission/regulatory exposure.

### Gaps
- Minimum withdrawal thresholds for most networks not found.
- Salad redemption options/fees not re-verified for 2026.

## Realistic earnings and utilization (paid demand vs emissions)

### Takeaway
No reliable 2026 per-device earnings data exists for io.net, Nosana, Golem, Theta, Spheron or Gaimin; aggregators explicitly call ROI "not measurable". Paid demand is concentrated on data-center GPUs (H100/H200). For an RTX 4090 on open marketplaces, realistic net is roughly $30–200/month depending on utilization and power cost; CPU-only and Mac contributions are minor (io.net gives CPUs only 5% of emissions).

### Cited Findings
- RTX 4090 Vast.ai: $0.55–1.50/hr gross (10–15% fee); Salad $0.30–0.60/hr after 20–25% cut (~$100–300/month less than Vast on same hardware) — [gpunex DE](https://www.gpunex.com/de/blog/sell-computing-power-best-platforms); [gpunex EN](https://www.gpunex.com/blog/sell-computing-power-best-platforms)
- Sept 2026 analysis: 4090 at 30% utilization and ~18.3¢/kWh nets ~+$30/month; other 2026 write-ups $50–85/month — [qwe.edu.pl GPU rental income](https://www.qwe.edu.pl/tutorial/gpu-rental-income-real-net/)
- miningboard: RTX 4090 $6.68/day at median $0.32/hr, 328W (~$200/month); Clore 4090 ~$0.139/hr — [miningboard](https://miningboard.com/de/rigs/nvidia-rtx-4090/ai-rental)
- Cheapest RTX 4090 renter price tracked: $0.160/hr on SaladCloud (renter price, not host) — [computeprices.com](https://computeprices.com/providers/vast/gpus/rtx4090)
- io.net worker rated "not measurable yet" by aggregator — [gamefi-roi io.net](https://gamefi-roi-web.onrender.com/opportunities/io-net-worker); Nosana ROI "unavailable" — [gamefi-roi Nosana](https://gamefi-roi-web.onrender.com/opportunities/nosana-gpu-host); Theta Edge Node estimated $0–10/month — [gamefi-roi Theta](https://gamefi-roi-web.onrender.com/opportunities/theta-edge-node)
- Gaimin promotional claims up to 70 GMRX/day (old) — [Investing News](https://investingnews.com/gaimin-s-early-access-event-opens-its-platform-and-monetization-app-to-gamers/)
- Golem: no 2026 per-GPU earnings data; check stats.golem.network by node name — [Golem GPU docs](https://docs.golem.network/docs/providers/gpu/gpu-golem-live)
- Aethir revenue ($127.8M 2025) reflects enterprise GPU payments, not consumer nodes — [BlockEden Mar 2026](https://blockeden.xyz/blog/2026/03/12/depin-compute-revenue-pivot-akash-ionet-aethir/)
- Nosana's NNP-0001 rationale: previous staking paid ~20% APR on idle balances that supplied no compute — [yellow.com NOS](https://yellow.com/it/asset/nos)

### Inferences
- io.net block rewards (emissions) are likely the dominant income for consumer io.net workers versus paid jobs; per-device stake requirement and token price determine whether it's net positive. This should be validated with live dashboard data before integration.
- Mac M-series and CPU-only contributions appear economically marginal across all networks surveyed.

### Gaps
- No verifiable 2025–2026 Reddit/Discord earnings reports were retrieved for io.net, Nosana, Golem, Theta, Spheron.
- Token prices (IO, NOS, GLM, TFUEL, AKT, RENDER, FLUX, ATH) as of Oct 2026 were not collected.

## Automation surface (CLI, daemon, API, Docker, install scripts)

### Takeaway
io.net, Nosana and Golem all expose scriptable, headless installs that a wrapper app could drive; Akash/Flux/Livepeer are infrastructure operator tooling; Theta/Salad are GUI apps.

### Cited Findings
- io.net: launch binaries from GitHub `ionet-official/io_launch_binaries` (e.g., `io_net_launch_binary_mac`), flags `--disable_sleep_mode=true` and `--token <value>` to skip interactive login; device auth via code at auth0.io.solutions/activate within ~3 minutes; modifying/running custom code in io.net containers may disqualify the device — [io.net install on macOS](https://io.net/docs/guides/workers/install-on-macos.md)
- Nosana: `bash <(wget -qO- https://nosana.com/start.sh)` (no sudo); node runs as `docker.io/nosana/nosana-node:latest` inside Podman (`nosana/podman:v1.1.0`) in Docker with `--gpus=all --privileged`; `--network mainnet|devnet`; logs `docker logs -f nosana-node`; registration requires email, accept T&Cs, benchmark — [Nosana Running the Host](https://learn.nosana.com/hosts/grid-run)
- Golem: Docker image `golemfactory/provider` with docker-compose (`restart: unless-stopped`), env vars `YA_RT_CORES`, `YA_RT_MEM`, `YA_RT_STORAGE`, `YA_ACCOUNT`, `YA_PAYMENT_NETWORK_GROUP`; or `curl -sSf https://join.golem.network/as-provider | bash -`; `golemsp run --no-interactive`, `golemsp status`, `golemsp settings set --cpu-per-hour ...` — [Golem provider installation](https://docs.golem.network/docs/providers/provider-installation)
- Akash: Ansible playbooks (~1 hr), Kubespray + Helm (K8s 1.33.5 / Kubespray 2.29), or Provider Console — [Akash setup & installation](https://akash.network/docs/providers/setup-and-installation); [Akash Helm build](https://akash.network/docs/providers/build-a-cloud-provider/akash-cloud-provider-build-with-helm-charts)
- Aethir Checker: Linux CLI tarball + install script (community guide); delegation via NodeOps/Blast API node-as-a-service — [Beehiiv guide](https://selected-investments.beehiiv.com/p/aethir-cloud-setup-checker-node-vps)

### Inferences
- io.net's `--token` flag suggests headless provisioning is feasible, but the token is tied to the user's io.net account; the wrapper would still need each user to have an io.net account (auth0) and Solana wallet.

### Gaps
- No public APIs for querying per-device earnings found for io.net/Nosana beyond dashboards.
- Theta, Spheron Fizz, Gaimin CLI/headless options not found.

## ToS / third-party wrappers, aggregators, referral programs

### Takeaway
No network's ToS on third-party wrappers/aggregators was found. Indirect signals: io.net disqualifies modified containers; Nosana requires each host to accept T&Cs at registration; Aethir explicitly supports delegated node-operator services (NodeOps, Blast API).

### Cited Findings
- io.net: modifying code or running custom code in io.net Docker containers "may disqualify" a device from block rewards or hiring — [io.net install on macOS](https://io.net/docs/guides/workers/install-on-macos.md)
- Nosana: host registration requires accepting terms and conditions (email + optional Discord/X) — [Nosana Running the Host](https://learn.nosana.com/hosts/grid-run)
- Aethir: partnered node-operator services let licence holders delegate operation; NodeOps console and Blast API provide this — [Bitrue Aethir checker guide](https://www.bitrue.com/blog/aethir-checker-nodes-guide); [NodeOps](https://docs.nodeops.network/Guides/Console/aethir)
- Flux Titan Nodes: InFlux Technologies runs hardware on behalf of stakers — [Flux docs](https://docs.cloud.runonflux.com/flux/fluxnodes)
- Livepeer: operators with limited stake can join a pool as a worker (pool operators aggregate GPUs) — [Livepeer operator rationale](https://docs.livepeer.org/v2/orchestrators/guides/operator-considerations/operator-rationale)
- Existing open-source aggregator precedent: GeiserX/CashPilot GitHub project includes a Theta Edge guide — [CashPilot](https://raw.githubusercontent.com/GeiserX/CashPilot/HEAD/docs/guides/theta-edge.md)

### Inferences
- Livepeer orchestrator pools and Aethir/Flux delegated-operator models demonstrate that "aggregator" roles are tolerated in some ecosystems; but explicit legal ToS review is required per network before shipping.

### Gaps
- Actual ToS text for io.net, Nosana, Golem, Salad, Vast.ai regarding resellers/wrappers not retrieved.
- Referral programs (io.net, Salad referral bonuses) not verified for 2026.

## KYC, geographic restrictions, regulatory and tax

### Takeaway
Little primary data found. Render's AI compute network currently onboards US-based operators specifically; Nosana registration needs only email; io.net uses an auth0 account; no KYC requirements were surfaced for consumer workers. Token-reward tax treatment was not researched from primary sources in this session.

### Cited Findings
- Render Compute Network onboarding US-based node operators for US-customer testing phase — [Render Foundation report](https://rendernetwork.medium.com/render-network-foundation-monthly-report-24e14ea50e13)
- Nosana registration: email, optional Discord/Twitter handles, accept T&Cs — [Nosana Running the Host](https://learn.nosana.com/hosts/grid-run)
- io.net device authorization via auth0 — [io.net install on macOS](https://io.net/docs/guides/workers/install-on-macos.md)
- Salad bandwidth sharing availability depends on region — [Salad support](https://support.salad.com/guides/using-salad/star-chef-qualifications-and-benefits)
- Aethir eATH programme involves lock-ups and Pendle liquidity (financial-product-like features) — [ChainCatcher](https://www.chaincatcher.com/en/article/2183396)

### Inferences
- (Background knowledge, not verified this session) In the US, IRS Rev. Rul. 2023-14 treats staking rewards as ordinary income at fair market value when received; token rewards for providing compute are generally treated as ordinary/self-employment income at receipt. An app paying users in tokens will likely trigger 1099/DAC8-style reporting questions in the US/EU. Requires dedicated tax/legal research.
- Node-licence sales (Aethir), required staking (io.net, Flux, Livepeer), and yield-like programs raise securities-law questions that a consumer app facilitating token purchases would inherit.

### Gaps
- No KYC/geo-restriction terms found for io.net, Golem, Theta, Spheron, Gaimin.
- No 2025–2026 regulatory actions against these specific networks found.

## Host security and isolation model

### Takeaway
Isolation strength varies: Golem uses KVM VMs (strongest); io.net and Nosana run containers (Nosana runs Podman nested in a privileged Docker container); Akash runs Kubernetes pods; Render runs vendor rendering software directly on the host.

### Cited Findings
- Golem: tasks run inside VMs (VM runtime) requiring `/dev/kvm` — [Golem provider installation](https://docs.golem.network/docs/providers/provider-installation)
- Nosana: Podman inside Docker with `--gpus=all` and `--privileged`; jobs run in containers — [Nosana Running the Host](https://learn.nosana.com/hosts/grid-run)
- io.net: workloads in Docker; docs state nothing inside the container can access your filesystem — [io.net install on macOS](https://io.net/docs/guides/workers/install-on-macos.md)
- Akash: leases run as Kubernetes pods — [Akash K8s for providers](https://akash.network/docs/providers/build-a-cloud-provider/kubernetes-cluster-for-akash-providers/kubernetes-cluster-for-akash-providers/)
- Flux ArcaneOS requires TPM 2.0/UEFI/Secure Boot (hardened dedicated OS) — [securities.io](https://www.securities.io/ru/investing-flux-network/)
- Render: avoid other heavy GPU workloads; failures affect reputation — [Render KB](https://know.rendernetwork.com/general-render-network/what-role-am-i/how-to-get-started-1/running-multiple-programs-or-tasks)

### Inferences
- A `--privileged` container (Nosana) gives near-root host access; a consumer app should warn users or isolate via a dedicated VM. Container GPU passthrough (io.net/Nosana) leaves the host kernel/driver as attack surface for arbitrary third-party workloads.

### Gaps
- Theta, Spheron Fizz, Gaimin, Salad isolation models not found.
- No documented security incidents for these networks were researched.
