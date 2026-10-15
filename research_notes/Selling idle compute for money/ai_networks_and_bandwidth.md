# AI Inference/Training Compute Networks and Bandwidth-Sharing Apps (status as of Oct 2026)

Scope note: Researched 2026-10-15. Many official docs (Inference.net operator docs, Psyche docs, Fortytwo node docs) returned 404 / connection errors during fetch, so several items rely on secondary sources and are flagged. "Points" = unconfirmed, non-cash rewards; "real payout" = fiat/stablecoin or a liquid token actually paid out.

## Q1. Status in 2026 and whether payouts are real money, tokens, or points

### Takeaway
Only a handful of AI networks pay consumer hardware real money in 2026: Darkbloom (Eigen Labs; Apple Silicon Macs; USD via Stripe), Salad (consumer GPUs; Salad Balance cashable to PayPal/gift cards), Hyperbolic (GPU rental income, 10% fee, but datacenter-oriented), Nosana (NOS token), and Bittensor (alpha/TAO tokens, but miners need datacenter-class setups for the inference subnets). Most others (Gensyn RL Swarm, Inference.net devnet, Fortytwo, Dria, Lilypad, Pluralis, Psyche, Prime Intellect) are paused, points-only, whitelisted, or pay nothing; Exo and Petals are free/volunteer software with no payments.

### Cited Findings

**Darkbloom (Eigen Labs) — REAL USD, live public alpha (best Apple Silicon fit)**
- Eigen Labs launched Darkbloom, a distributed network letting Apple Silicon Mac owners earn money by providing idle compute for AI inference; it began as an experiment in April 2026 — [The Register, 2026-08-24](https://www.theregister.com/ai-and-ml/2026/08/24/conjure-cash-with-old-macs-by-linking-them-to-ai-inference-borg/5292002)
- Payouts via Stripe Connect (bank account or debit card), funds typically arrive in 1–3 business days; docs state "no platform fee deducted from provider earnings" — [Darkbloom docs: earnings](https://docs.darkbloom.dev/provider/earnings.md)
- Providers earn a share of consumer billing per token, e.g., Gemma 4 26B 8-bit $0.065/$0.20 per 1M input/output tokens; Qwen3.5 122B MoE $0.13/$1.04 — [Darkbloom docs: earnings](https://docs.darkbloom.dev/provider/earnings.md)
- Network stats: >900 providers registered, ~250 online at time of company post; $102K ARR and 4.5B tokens served claimed; became a paid provider on OpenRouter — [The Register](https://www.theregister.com/ai-and-ml/2026/08/24/conjure-cash-with-old-macs-by-linking-them-to-ai-inference-borg/5292002)
- By Sept 11, 2026: 1,186 nodes online, 107M total requests, 374B tokens served (per reviewer's dashboard read) — [miyagadget review](https://miyagadget.page/en/blog/2026/06/24/darkbloom-provider-mac-earnings-review-en/)
- Software is labelled "public alpha" (v0.6.20 in June, v0.9.1 by Sept 2026) — [miyagadget review](https://miyagadget.page/en/blog/2026/06/24/darkbloom-provider-mac-earnings-review-en/)

**Salad (SaladCloud consumer "chef" network) — REAL money (Salad Balance)**
- Balance accrues ~15 minutes after starting; cash out to gift cards, games/subscriptions, or PayPal — [Salad support](https://support.salad.com/article/127-how-long-salad-takes-to-start-earning); [Salad earning rates](https://salad.com/earning-rates)
- Salad raised 4090/5090 earning rates at High/Medium/Low priority on Sept 12 (2026) because demand outran supply — [Salad blog](https://blog.salad.com/?p=1534)

**Hyperbolic GPU marketplace — REAL rental income + points for idle time**
- Suppliers set price up to per-model caps (e.g., $0.50/hr RTX 4090, $3.20/hr H100 SXM); Hyperbolic takes 10% platform fee; idle listed machines earn points (e.g., 50 pts/hr for a 4090) of unstated value — [Hyperbolic docs: pricing](https://docs.hyperbolic.xyz/docs/gpu-marketplace-pricing)
- Spot client prices listed as low as $0.30/hr for RTX 4090 — [Hyperbolic docs: spot market](https://docs.hyperbolic.xyz/docs/gpu-marketplace)
- A 2024 report said GPU suppliers would get paid in a token; no evidence found that a token is live — [AOL/Yahoo Finance](https://www.aol.com/finance/ai-company-hyperbolic-raises-12-160000750.html)

**Nosana — TOKEN (NOS) payouts, live**
- Hosts "start earning $NOS by contributing GPU power"; host runs via a single script in a Docker container — [Nosana docs: Host a GPU](https://docs.nosana.com/hosts/grid-run)
- Third-party ROI tracker lists Nosana host earnings "not measurable yet"; NOS ~$0.60 / ~$60M mcap (snapshot date unclear) — [gamefi-roi tracker](https://gamefi-roi-web.onrender.com/opportunities/nosana-gpu-host); [yellow.com](https://yellow.com/de/asset/nos)

**Bittensor (dTAO) — TOKEN payouts (subnet alpha, convertible to TAO), live mainnet**
- Under dTAO (launched Feb 2025), each subnet has its own alpha token; miners on Chutes (SN64) earn the subnet's alpha — [sawinyh tokenomics](https://sawinyh.com/blog/bittensor-tao-tokenomics.md); [asymmetricjump SN64 research](https://asymmetricjump.substack.com/p/bittensor-subnet-research-chutes)
- Under "Taoflow," a subnet's emission share is set by net TAO staking inflows; subnets with negative flows receive zero emissions — [alearesearch](https://alearesearch.substack.com/p/bittensor-subnets-how-they-work-and)
- Templar (SN3) produced Covenant-72B, trained permissionlessly by >70 contributors on commodity internet hardware — [alearesearch](https://alearesearch.substack.com/p/bittensor-subnets-how-they-work-and)
- Targon (SN4, Manifold Labs) sells confidential GPU compute to enterprises; $10.4M revenue figure is a projection — [Pine Analytics bear case](https://pineanalytics.substack.com/p/the-bear-case-for-bittensor-tao)

**Gensyn — RL Swarm PAUSED; mainnet live for Delphi only; node runners earned leaderboard points**
- Gensyn testnet launched March 2025; official docs now say RL Swarm is paused (Gensyn-hosted nodes paused too), BlockAssist and CodeAssist are sunset; Delphi (prediction market) live on testnet and mainnet — [Gensyn docs: testnet](https://docs.gensyn.ai/testnet)
- RL Swarm repo: "There are no official swarms running right now"; participation earned "activity points" on a leaderboard; no token/money mentioned — [GitHub gensyn-ai/rl-swarm](https://github.com/gensyn-ai/rl-swarm)
- $AI token sale Dec 2025 (300M tokens, 3% of supply, English auction, $1M–$1B FDV range); testnet users got a bonus multiplier on purchases from a 2% reward pool (a sale bonus, not node payouts) — [PRNewswire via AAP](https://www.aap.com.au/aapreleases/cision20251215en47107)
- Mainnet with Delphi reported April 22, 2026 (secondary) — [CoinGabbar](https://www.coingabbar.com/en/crypto-currency-news/gensyn-ai-token-launch-delphi-mainnet-kraken-2026); Upbit listing June 30, 2026 — [CryptoBriefing](https://cryptobriefing.com/upbit-lists-gensyn-ai-krw-btc-usdt-june-30/)
- Binance HODLer airdrop of AIGENSYN (May 2026) was based on BNB holdings, not node running; US excluded — [OneKey blog](https://onekey.so/blog/ecosystem/binance-hodler-gensyn-aigensyn-20260521230318/)

**Inference.net (formerly Kuzco) — POINTS/devnet tokens; current paid status UNCONFIRMED**
- Devnet: off-chain $INT points + on-chain $INT-DEV tokens "with no monetary value"; $INT-DEV airdrop to eligible operators June 13, 2025 — [Inference.net devnet docs](https://docs.devnet.inference.net); [airdrop page](https://docs.devnet.inference.net/devnet-epoch-3/airdrop)
- Third-party coverage: devnet test tokens convert to mainnet $INT at a TGE "to be announced" — [Solana Compass](https://solanacompass.com/projects/inference)
- Operator documentation states operators "can earn USDC by contributing their compute power" (search snippet; page 404'd on fetch) — [operator.inference.net](https://operator.inference.net/)
- Solana Compass's /projects/kuzco URL resolves to the "Inference" project page (supports the Kuzco→Inference.net rebrand, not independently confirmed) — [Solana Compass](https://solanacompass.com/projects/kuzco)
- Inference.net is now shipping its own models (Schematron V2) on OpenRouter as of Sept 2026 — [OpenRouter](https://openrouter.ai/inference-net)

**Fortytwo — POINTS (FOR points), Monad-linked, gated**
- Node operators earn "FOR points"; app at node.fortytwo.network; CLI package @fortytwo-network/fortytwo-cli — [skills.sh listing](https://www.skills.sh/fortytwo-network/fortytwo-node-skills/fortytwo-node)
- Points described as "foundation for future token rewards" — [Gate.com explainer](https://www.gate.com/learn/articles/what-is-forty-two-network/9670)
- Access code required via Node Operator Form; fund node with ≥0.5 MON for fees (undated) — [CryptoRank drophunting](https://cryptorank.io/drophunting/fortytwo-ai-activity724)
- Swarm inference of small models on consumer hardware — [The Register, 2025-11-02](https://theregister.com/2025/11/02/fortytwo_dcentralized_ai)
- Fortytwo also sells an OpenAI-compatible API (api.fortytwo.network) — [Fortytwo docs](https://docs.fortytwo.network)

**Dria — POINTS ($DRIA points), Mac-friendly**
- Nodes earn $DRIA points per completed inference task; referral points also exist; no stated conversion — [Dria node guide: rewards](https://node-guide.dria.co/rewards.md)

**Pluralis (Agora / Node0) — participation in training runs; no payout mechanism found**
- Agora pretrained an 8B model from scratch on consumer GPUs across the internet; Pluralis-8B run used 330 contributor nodes — [Pluralis job listing](https://www.builtinla.com/job/research-scientist/10931803); [alphaxiv paper](https://www.alphaxiv.org/abs/2607.13332.md)
- Contributions tracked per HuggingFace account; interrupted spot runs "still earn a baseline" (unit not specified) — [Pluralis docs: requirements](https://pluralis.ai/docs/quick-start/requirements/)

**Nous Psyche — gated (screened) testnet; no payouts found**
- Testnet compute providers "screened in a centralized (trusted) environment," transitioning to permissionless over time — [Nous Research announcement](https://nousresearch.com/?p=2359)
- Hermes 4.3 partly trained on Psyche (24 nodes, 144K tok/s claim) — [threadreader/Nous](https://threadreaderapp.com/user/NousResearch)
- Forum "Compute Contributions" category active Feb 2026 — [Nous forum](https://forum.nousresearch.com/c/psyche-network/compute-contributions/27)

**Prime Intellect — protocol on testnet; INTELLECT-3 trained centrally; no token**
- INTELLECT-2 used a "permissionless" swarm; INTELLECT-3 ran on 512 H200s in a co-located cluster — [Implicator](https://www.implicator.ai/prime-intellects-intellect-3-open-source-ambition-meets-centralized-reality/)
- Protocol described as live on testnet (2025); no token launched — [Prime Intellect fundraise blog](https://www.primeintellect.ai/blog/fundraise); [Sacra](https://sacra.com/chat/h/30d4347f-20c4-4f6f-b07d-5a7b294f8814/)

**Ritual — airdrop-speculative**
- Third-party: 50M RITUAL (0.5%) airdrop to contributors incl. Infernet node operators, unlocks at mainnet; unverified against official sources — [usethebitcoin](https://usethebitcoin.com/airdrop/ritual/)
- Official FAQ: run an Infernet node; GPU not required — [Ritual FAQ](https://ritual.net/blog/faq)

**Lilypad — stale (2024 IncentiveNet points; mainnet "tbd")**
- Lilybit_ credits redeemable for LP token at mainnet; terms last updated June 24, 2024 — [Lilypad Notion](https://lilypadnetwork.notion.site/Leap-into-Lilypad-s-IncentiveNet-9e9b12936d4340ad9417d92dab8bd9d1); [Lilypad docs GitHub](https://github.com/Lilypad-Tech/lilypad-docs/blob/main/lilypad/lilypad-milky-way-testnet/overview.md)

**Atoma — stale**
- Launched on Sui mainnet Dec 2024 with TEE inference; no 2025-26 node-operator info found — [Blockeden](https://blockeden.xyz/blog/2025/08/23/sui-blockchain-engineering-the-future-of-ai-robotics-and-quantum-computing/)

**OpenGradient / Parasail — not consumer supply programs**
- OpenGradient: GPU + TEE node network with validators checking TEE/zkML proofs — [CB Insights](https://www.cbinsights.com/company/opengradient); [Phantom token page](https://phantom.com/tokens/base/0xfbc2051ae2265686a469421b2c5a2d5462fbf5eb)
- Parasail aggregates GPU cloud providers (H100/H200/A100/4090), $10M seed (2025) — [DatacenterDynamics](https://datacenterdynamics.com/en/news/ai-deployment-network-parasail-launches-claims-to-have-more-gpus-than-oracle)

**Exo Labs — NO payment**
- exo is open-source software to cluster Macs/workstations for local inference; no paid-contribution program found; local.ai referral leaderboard has no cash/tokens reported — [AI Engineer org page](https://www.ai.engineer/orgs/exo-labs); [RuntimeWire](https://runtimewire.com/article/exo-labs-pauses-local-ai-name-claims-prelaunch-discovery)

**Petals — NO payment (volunteer)**
- "Petals is a community-run system — we rely on people sharing their GPUs"; contributors hosting 10+ blocks can get name on swarm monitor — [Petals README mirror](https://git.blob42.xyz/Archives/petals/raw/branch/bump/README.md); [jimmysong.io](https://jimmysong.io/ai/petals)

### Inferences
- For a one-click consumer app selling compute for real money in Oct 2026, the realistic integration targets are Darkbloom (Mac), Salad (Windows GPU), and Nosana (Linux/Docker GPU, token). Bittensor inference subnets are not consumer-viable (see Q2). Everything else is points/airdrop speculation or not paying.
- Gensyn's pivot to Delphi and pausing of RL Swarm shows testnet "node" programs can end abruptly once a token launches; points programs should be treated as zero expected value.

### Gaps
- Could not fetch current Inference.net operator docs (404); whether USDC payouts are live in 2026 is unconfirmed.
- No 2026 status found for Atoma, Lilypad, Ritual mainnet, Psyche permissionless opening, Prime Intellect token, or Petals swarm size.
- "DeepInfra-like community supply programs" and "Ollama-based earn programs": no DeepInfra community supply program found; Dria (formerly Ollama-based) is the only Ollama-adjacent earn program found (points). GaiaNet: no 2026 info found.

## Q2. Supported OS, hardware, Apple Silicon support, staking/registration costs

### Takeaway
Darkbloom is macOS/Apple Silicon only (16GB+, MDM enrollment); Salad is Windows-centric consumer GPU; Nosana is Linux+Docker NVIDIA. Bittensor's Chutes now requires Intel TDX confidential-VM GPU nodes plus a k3s control plane, Bittensor mining is not supported on Windows, and registration burns TAO. Pluralis requires 24GB VRAM, 80GB RAM and North American location; Gensyn RL Swarm needed 32GB RAM and is paused.

### Cited Findings
- **Darkbloom:** Apple Silicon (M1–M4), macOS 14+, 16GB+ unified memory (32GB+ for larger models), CLI install via curl script; MDM enrollment required for hardware attestation that gates paid traffic — [miyagadget review](https://miyagadget.page/en/blog/2026/06/24/darkbloom-provider-mac-earnings-review-en/)
- Darkbloom attestation checks SIP, Secure Boot, FileVault via Apple MDM SecurityInfo API and Apple Managed Device Attestation; challenge every 5 minutes — [Darkbloom docs: attestation](https://docs.darkbloom.dev/security/attestation)
- Darkbloom uses Apple MLX on GPU; outbound WebSocket, no port forwarding; personally owned device required (no cloud arbitrage) — [The Register](https://www.theregister.com/ai-and-ml/2026/08/24/conjure-cash-with-old-macs-by-linking-them-to-ai-inference-borg/5292002); [mindstudio](https://www.mindstudio.ai/blog/dark-bloom-distributed-ai-inference)
- **Chutes (SN64):** all GPU workers must run inside Intel TDX confidential VMs; bare metal/VMs with static unique IPs; Runpod/Vast-style hosts unsupported; non-TEE/legacy GraVal nodes rejected; control plane ≥4 cores/32GB RAM running k3s; system RAM ≈ total VRAM — [GitHub rayonlabs/chutes-miner](https://github.com/rayonlabs/chutes-miner)
- **Bittensor general:** register hotkey per subnet with btcli, cost in TAO (burn/"recycle", sunk, unrecoverable); 256 UIDs/subnet (≤64 validators); immunity period; lowest-emission miner deregistered when subnet full — [Bittensor docs: miners](https://docs.learnbittensor.org/miners); [Taostats docs](https://docs.taostats.io/docs/node-registration)
- Bittensor mining "is not supported on Windows" — [Learn Bittensor guide](https://guides.learnbittensor.org/miners)
- Snapshot per-subnet miner registration costs (undated, ©2025 pages): SN58 0.94 TAO, SN35 1.81 TAO, SN45 1.72 TAO — [taostats SN45](https://taostats.io/subnets/45/registration); live SN64 page did not render data on fetch — [taostats SN64](https://taostats.io/subnets/64/registration)
- Subnet *creation* burn ~1,500 TAO in mid-2026 (different from miner registration) — [cryptoactu](https://cryptoactu.com/bittensor-cout-enregistrement-subnets-multiplie/); [TAO Times #44](https://taotimes.beehiiv.com/p/tao-times-44-the-halving-s-bull-case)
- **Inference.net:** min GPU ≥16GB VRAM, 16GB RAM, 30GB disk, 100Mbps+; NVIDIA and AMD GPUs (search snippet of official docs) — [operator.inference.net hardware](https://operator.inference.net/getting-started/hardware)
- **Gensyn RL Swarm:** arm64 or x86 with ≥32GB RAM; officially supported GPUs RTX 3090/4090/5090/A100/H100; Mac via CPU path; Windows via WSL "not very well tested"; Docker recommended — [GitHub rl-swarm](https://github.com/gensyn-ai/rl-swarm)
- **Pluralis Agora:** 24GB VRAM GPU (4090/5090/RTX 6000), 80GB RAM per GPU, 80GB disk, 200Mbps, North American region, <80ms RTT, inbound TCP 49200; Linux or Windows WSL2; macOS unsupported (no CUDA); max 2 peers per HF account — [Pluralis docs: requirements](https://pluralis.ai/docs/quick-start/requirements/); older Node0 repo listed 16GB+ VRAM, Ubuntu — [Spheron guide](https://docs.spheron.network/guide-to-deploy/nodes/pluralis-node-0)
- **Psyche:** heterogeneous hardware (4090, A100, H100) per secondary report — [aibase](https://news.aibase.com/zh/news/18116)
- **Dria:** macOS Intel + Apple Silicon (Metal acceleration), native llama.cpp (Ollama no longer needed), ~1GB–27GB RAM depending on model, outbound UDP 4001 — [Dria node guide FAQ](https://node-guide.dria.co/faq.md); [how to run](https://node-guide.dria.co/how-to-run)
- **Fortytwo:** access code + ≥0.5 MON for fees — [CryptoRank](https://cryptorank.io/drophunting/fortytwo-ai-activity724)
- **Nosana:** Docker-container host via single script — [Nosana docs](https://docs.nosana.com/hosts/grid-run)
- **Salad:** consumer desktop app, no Docker/CLI required (vendor-comparison blog) — [gpunex](https://www.gpunex.com/blog/sell-computing-power-best-platforms)
- **Ritual:** GPU not required to run Infernet node — [Ritual FAQ](https://ritual.net/blog/faq)

### Inferences
- Apple Silicon: Darkbloom (paid) and Dria (points) are the only Mac-native options found; Exo runs on Macs but pays nothing.
- Chutes' TDX requirement effectively excludes all consumer PCs; Bittensor integration would only make sense for a niche subnet, not a mass-market app.

### Gaps
- Live 2026 miner registration cost for SN64/SN4/SN3 not retrievable (taostats page did not render).
- Salad official OS list (Linux/Mac support) not confirmed in 2026 sources.
- Hyperbolic supplier OS/hardware requirements (Hyper-dOS) not confirmed.

## Q3. Realistic earnings for consumer hardware (dated)

### Takeaway
Real consumer earnings are small. Darkbloom marketing claims $120–200/month per Mac, but independent tests measured ~$0.60/day (~$16/month) in June 2026, mostly from a fixed "base reward," and network utilization was ~7%. Salad's 4090 client prices ($0.16–$0.33/hr Sept 2026) imply a few dollars/day gross at best. Bandwidth apps yield ~$2–25/month/device.

### Cited Findings
- Darkbloom company claim: $120–200/month/machine average; $192–$4,983/year/device range; top models Qwen 3.6 35B A3B $399/mo, GPT-OSS 20B $52/mo — [The Register, 2026-08-24](https://www.theregister.com/ai-and-ml/2026/08/24/conjure-cash-with-old-macs-by-linking-them-to-ai-inference-borg/5292002)
- Independent Darkbloom test (M1 Max 64GB, June 21–23, 2026): $0.60/24h (564 jobs), nearly all from base reward $0.002083 per 5 min; per-job $0.000001–$0.000024; ~15W avg, ~$0.07/day electricity; net ≈ +$15.9/month — [miyagadget](https://miyagadget.page/en/blog/2026/06/24/darkbloom-provider-mac-earnings-review-en/)
- Darkbloom base reward tiers/month by unified memory: 512GB $40; 128GB $26; 64GB $18; 48GB $16; 32GB $12; <24GB usage only; requires attestation, ≥90% uptime, fixed monthly budget — [miyagadget](https://miyagadget.page/en/blog/2026/06/24/darkbloom-provider-mac-earnings-review-en/)
- Darkbloom calculator changed from $75.21/mo (June, 80% utilization) to $12.51/mo (Sept, 5% duty cycle default); network utilization 7% (peak 9%) in June — [miyagadget](https://miyagadget.page/en/blog/2026/06/24/darkbloom-provider-mac-earnings-review-en/)
- X user reply reported ~$0.43 earned vs ~$76 platform-wide over 30 days (anecdotal) — [X post thread](https://x.com/Michaelzsguo/status/2067795772439982591?lang=en)
- Salad 4090 client prices: High priority $0.330/hr, Batch $0.160/hr as of Sept 12, 2026 (competitor blog) — [Spheron blog](https://www.spheron.network/blog/saladcloud-pricing-2026-gpu-cost-vs-spheron/); Salad estimate methodology assumes 24h consistent workload, counts machines averaging ≥4h/day — [Salad support](https://support.salad.com/article/225-how-predicted-earnings-works)
- Salad takes 20–25% platform fee; 4090 $0.30–0.60/hr gross (vendor comparison, unverified) — [gpunex](https://www.gpunex.com/blog/sell-computing-power-best-platforms)
- Hyperbolic: supplier example H100 at $2.50/hr keeps $2.25/hr; 4090 cap $0.50/hr — [Hyperbolic docs](https://docs.hyperbolic.xyz/docs/gpu-marketplace-pricing)
- Grass Season 2 (USDC payouts for Oct 2024–Jun 2026 activity): many long-time runners reported single-digit-dollar payouts — [HokaNews, July 2026](https://www.hokanews.com/2026/07/grass-airdrop-season-2-enters-final.html)

### Inferences
- A wrapper app's honest consumer pitch is "a few to low tens of dollars per month," not hundreds. Darkbloom's fixed base reward (budget-capped) is currently the main reliable component for Macs.
- Earnings are demand-limited (utilization single-digit %), so aggregating many more consumer suppliers via a wrapper would likely dilute per-device payouts further.

### Gaps
- No 2026 per-device earnings data found for Nosana, Bittensor consumer miners, Inference.net, or Hyperbolic consumer suppliers.

## Q4. Automation surface (CLI/Docker/API/headless) and ToS on wrappers/referrals

### Takeaway
Most networks are CLI/Docker-driven and headless-friendly (Darkbloom CLI, Nosana Docker script, Gensyn Docker, Pluralis CLI/Docker, Fortytwo CLI npm package, Dria binary, Bittensor btcli), which makes wrapping technically feasible. But ToS terms visible so far tie participation to the account holder's own device (Darkbloom), one account per person (Honeygain), and anti-cheat/anti-VPS rules; no network found offers an official white-label/partner program for compute supply. Bandwidth vendors (Honeygain, Bright Data, Pawns) do offer embeddable SDKs.

### Cited Findings
- Darkbloom: CLI commands `darkbloom login/start/status/earnings`; public `/v1/leaderboard` and `/v1/providers/attestation` endpoints; earnings untracked for withdrawal until device linked via `darkbloom login` — [Darkbloom docs: earnings](https://docs.darkbloom.dev/provider/earnings.md); [attestation](https://docs.darkbloom.dev/security/attestation); [miyagadget](https://miyagadget.page/en/blog/2026/06/24/darkbloom-provider-mac-earnings-review-en/)
- Darkbloom ToS: personally owned device; no cloud-provider rental arbitrage — [The Register](https://www.theregister.com/ai-and-ml/2026/08/24/conjure-cash-with-old-macs-by-linking-them-to-ai-inference-borg/5292002)
- Darkbloom idle behaviour: backend stops after 1h idle, 10–30s cold start; scheduling docs for idle hours exist — [miyagadget](https://miyagadget.page/en/blog/2026/06/24/darkbloom-provider-mac-earnings-review-en/); [Darkbloom docs](https://docs.darkbloom.dev/provider/earnings.md)
- Nosana: single script, Docker container — [Nosana docs](https://docs.nosana.com/hosts/grid-run)
- Gensyn RL Swarm: `docker-compose run --rm --build -Pit swarm-cpu|swarm-gpu` or `./run_rl_swarm.sh` — [GitHub rl-swarm](https://github.com/gensyn-ai/rl-swarm)
- Pluralis: CLI prompts for HF token, Docker recommended; ships a Claude Code skill `/agora-join`; keep private.key to retain contribution history — [Pluralis docs: run](https://pluralis.ai/docs/quick-start/run/)
- Fortytwo: npm `@fortytwo-network/fortytwo-cli` — [skills.sh](https://www.skills.sh/fortytwo-network/fortytwo-node-skills/fortytwo-node)
- Chutes: Ansible + k3s; `chutes-miner add-node` CLI — [GitHub chutes-miner](https://github.com/rayonlabs/chutes-miner)
- Bittensor: `btcli subnets register` safe mode prompts for price tolerance — [Taostats docs](https://docs.taostats.io/docs/node-registration)
- Inference.net delegation: delegators stake to operator pools; operators charge commission — [Inference.net delegator guide](https://docs.devnet.inference.net/devnet-epoch-3/delegator-guide)
- Third-party hosting templates exist (NodeOps, Spheron, OctaSpace) for Dria, Inference.net, Pluralis, Gensyn; NodeOps states incentives are issued solely by the network and not guaranteed — [NodeOps Dria template](https://docs.nodeops.network/Guides/Marketplace/Marketplace-Templates/dria-testnet-gpu); [OctaSpace Pluralis](https://docs.octa.space/pluralis-node)
- Dria has a referral program for additional points — [Dria rewards](https://node-guide.dria.co/rewards.md)

### Inferences
- Third-party one-click deploy templates (NodeOps/Spheron/OctaSpace) show networks tolerate wrappers in practice, but these are hosting templates where the end-user still owns the account; a wrapper that pools users under one operator account would likely violate "own device"/one-account rules.
- The cleanest model is "user installs our app, signs into their own network account (e.g., Darkbloom login, Salad account), payouts go directly to them"; revenue to the wrapper would need a referral program (none confirmed for Darkbloom/Salad in sources found).

### Gaps
- Could not find Darkbloom full ToS, Salad referral/affiliate terms, Nosana ToS on wrappers, or any network's formal partner/white-label program.

## Q5. KYC, legal and tax concerns

### Takeaway
Real-money networks route payouts through regulated processors (Stripe Connect for Darkbloom; PayPal for Salad/bandwidth apps), implying KYC/tax-info collection by the processor. Token networks raise securities/jurisdiction issues (Grass switched to USDC citing regulatory uncertainty; Binance's Gensyn airdrop excluded the US). Darkbloom requires MDM enrollment, which is a notable security/privacy ask for a consumer app.

### Cited Findings
- Darkbloom payouts via Stripe Connect onboarding (bank/debit card) — [Darkbloom docs](https://docs.darkbloom.dev/provider/earnings.md)
- Darkbloom requires MDM enrollment for attestation gating paid traffic — [miyagadget](https://miyagadget.page/en/blog/2026/06/24/darkbloom-provider-mac-earnings-review-en/)
- Grass Season 2 paid in USDC, citing "ongoing regulatory uncertainty" and wish to let users in more jurisdictions claim — [HokaNews](https://www.hokanews.com/2026/07/grass-airdrop-season-2-claim-opens-july.html)
- Binance HODLer Gensyn airdrop exclusion list included the United States — [OneKey](https://onekey.so/blog/ecosystem/binance-hodler-gensyn-aigensyn-20260521230318/)
- Fortytwo relay operator wallets "only permitted to participate if approved" — [The Register 2025](https://theregister.com/2025/11/02/fortytwo_dcentralized_ai)
- Honeygain terms prohibit sharing where ISP/mobile-provider agreements forbid sharing traffic/IP — [Honeygain Terms](https://honeygain.com/terms-of-use)
- Lilypad incentivized testnet had separate participation terms of use (June 24, 2024) — [Lilypad ToU](https://lilypadnetwork.notion.site/Lilypad-Network-Incenvised-Testnet-Program-Participation-Terms-of-Use-437aade2b22b4e3d989b723df6cfd1df)

### Inferences
- A wrapper that holds or forwards user funds would itself become a money transmitter/payout platform; passing users through to the network's own Stripe/PayPal onboarding avoids this.
- In the US, earnings (fiat or token at fair market value) are generally taxable income; processors like Stripe Connect typically issue 1099s — general knowledge, not sourced here; verify with tax counsel.

### Gaps
- No source found on Darkbloom KYC thresholds, supported countries, or minimum withdrawal; no Salad 2026 cash-out minimum found.

## Q6. Part B — Bandwidth and other resource-sharing apps (brief)

### Takeaway
Bandwidth apps pay real but tiny amounts ($2–25/month/device), mostly via PayPal/crypto with $2.50–$20 minimums. Honeygain, Bright Data (EarnApp's parent), and Pawns.app each offer an embeddable SDK for app developers. Reputational/legal risk is high and rising: Google's Jan 2026 IPIDEA takedown and June 2026 research on Bright SDK show proxy SDKs are actively targeted by platform enforcement and security researchers. Storj pays fiat-pegged rates for storage; Mysterium earnings are negligible.

### Cited Findings
**Payouts/minimums/earnings**
- Honeygain $20 minimum (PayPal via third party or JumpTask); EarnApp $2.50 minimum (2026 comparison); PacketStream $5 minimum PayPal; Repocket $20 PayPal; Pawns.app $5 min (PayPal, BTC, ACH, Venmo, gift cards) — [Proxyway research](https://proxyway.com/research/proxyware-passive-income-apps); [Geekflare](https://geekflare.com/finance/bandwidth-sharing-passive-income/); [panstag 2026](https://www.panstag.com/2026/07/new-passive-income-apps.html)
- Typical earnings $5–25/month/device (one 2026 review), or $2–10/month (another); Honeygain ~ $1 per 5GB vs $1 per 10GB (sources conflict); PacketStream ~$0.10/GB — [panstag](https://www.panstag.com/2026/07/new-passive-income-apps.html); [Geekflare](https://geekflare.com/finance/bandwidth-sharing-passive-income/)
- Honeygain and EarnApp have no iOS app — [Geekflare](https://geekflare.com/finance/bandwidth-sharing-passive-income/)
- Grass: desktop app/browser extension sharing bandwidth for points (Wynd Network, Solana); Season 1 airdrop Oct 28, 2024 (100M GRASS to 2.8M users); Season 2 paid in USDC, claim July 22, 2026, covering Oct 14, 2024–Jun 8, 2026 — [synthient](https://synthient.com/context/provider/GETGRASS); [HokaNews](https://www.hokanews.com/2026/07/grass-airdrop-season-2-enters-final.html)
- Storj node payouts (official, as of Dec 1, 2023): $1.50/TB-month storage, $2.00/TB egress, $2.00/TB audit/repair; operators debating further cuts — [Storj payouts doc](https://tardigrade.gitbooks.storj.io/node/payouts); [Storj forum](https://forum.storj.io/t/lets-talk-about-the-elephant-in-the-room-the-storj-economic-model-node-operator-payout-model/20127)
- Mysterium pays MYST per bandwidth; a third-party model (Aug 31, 2026) estimated net ≈ -$0.0068/day for a small setup (flagged stale) — [gamefi-roi tracker](https://gamefi-roi-web.onrender.com/opportunities/mysterium-network-node); [Kryptex](https://pool.kryptex.com/articles/mysterium-network-en)

**SDK / partner programs**
- Honeygain SDK lets developers monetize idle user bandwidth alongside ads; claims "up to $500 per 1,000 exposures" (directory listing, unverified) — [MarTech Series](https://martechseries.com/sales-marketing/programmatic-buying/honeygain-sdk-drives-hybrid-monetization-increasing-app-revenue-alongside-traditional-ads/); [IoT For All](https://marketplace.iotforall.com/companies/honeygain-sdk)
- Bright Data SDK (Bright Data owns EarnApp): opt-in, users can skip ads in exchange for joining network; claims independent audit — [Bright Data SDK](https://brightdata.com/products/bright-sdk); [Bright Data trust center](https://brightdata.com/trustcenter/bright-sdk-ethical-monetization-data-privacy)
- Pawns.app (formerly IPRoyal Pawns) SDK for developers, opt-in, claims "up to $200 CPM" — [Pawns SDK page mirror](https://backiee.wasmer.app/https_pawns_app/sdk); [Pawns blog](https://pawns.app/vi/blog/tot-iproyal-vua-tro-thanh-pawns-app/)
- PacketSDK exists but link to PacketStream unconfirmed — [DEV Community](https://dev.to/bronnnya/packetsdk-revolutionizing-app-monetization-models-1d1l)

**ToS on wrappers/automation**
- Honeygain: one account; max 10 devices/account, 1 device per IP; datacenter IPs/VPN/VPS/VMs/emulators not allowed; anti-cheat (HAC) auto-suspends (secondary sources) — [visu.network review](https://visu.network/blog/honeygain-review/); [codegenes guide](https://www.codegenes.net/blog/honeygain-download/)

**Reputational/legal risk**
- Google disrupted IPIDEA residential proxy network Jan 28, 2026; Play Protect removes apps with IPIDEA SDKs; >550 threat groups used IPIDEA exit nodes in one week; ~9M devices expected to be knocked off — [The Register](https://www.theregister.com/2026/01/29/google_ipidea_crime_network/); [Security Affairs](https://securityaffairs.com/187463/security/google-targets-ipidea-in-crackdown-on-global-residential-proxy-networks); [Proxyway](https://proxyway.com/news/google-disrupts-10-ipidea-related-chinese-proxy-providers)
- Proxy operators pay app developers to embed SDKs; many analysed apps failed to disclose enrollment — [The Register](https://www.theregister.com/2026/01/29/google_ipidea_crime_network/)
- June 2026 Include Security teardown of Bright SDK on iOS/smart TVs: vague consent, config allows up to 200GB/month per device, weak peer-channel auth, traffic can bypass configured VPN on iOS; Google, Amazon, Roku restricted background proxy SDKs — [The Hacker News](https://thehackernews.com/2026/06/free-apps-are-quietly-turning-smart-tvs.html); [SC World](https://www.scworld.com/brief/researcher-finds-ios-sdk-turns-smart-tvs-into-web-scraping-nodes); [Include Security](https://blog.includesecurity.com/2026/06/the-smart-tv-in-your-livingroom-is-a-node-in-the-aiscraping-economy/)
- Third-party traffic passes through user's connection; check ISP terms/data caps — [Geekflare](https://geekflare.com/finance/bandwidth-sharing-passive-income/)

### Inferences
- Embedding a proxy SDK in a consumer desktop app is technically supported by Honeygain/Bright/Pawns but carries antivirus flagging, platform-enforcement and press risk given 2026 enforcement trends; if included it should be strictly opt-in, separately toggled, and clearly disclosed.
- Storj is the most "legitimate" non-AI add-on (fiat-pegged, published rates) but requires large disks and long uptime; earnings per consumer node are small.

### Gaps
- Filecoin not researched in depth (requires large sealed storage and collateral; not consumer-suitable — unsourced here).
- Official Repocket, PacketStream, Grass ToS on VPN/datacenter/wrappers not found; Mysterium node official 2026 earnings not found; Storj 2026 rate card not found (latest official Dec 2023).
- Honeygain SDK revenue share and official partner terms not found.
