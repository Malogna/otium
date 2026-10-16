# AI Inference and Training Networks That Pay for Consumer GPUs and Apple Silicon Macs (status as of mid-October 2026), and Whether an Operator App (Otium) Can Aggregate Users' Devices

Research date: 2026-10-16. All web sources were read through search snippets and page fetches. Most third-party sources are aggregators or crypto-media sites, and this is flagged where it matters. Points programs are valued at $0 unless they convert to money. No sign-ups or form submissions were made.

## Q1. Network-by-network: status, hardware/OS, payment, rates, aggregation, automation, KYC

### Takeaway
Only a few networks in this list pay real fiat-denominated money to consumer hardware owners today. They are Darkbloom (Apple Silicon only, Stripe payouts, public alpha), Salad (NVIDIA on Windows, posted USD per-hour rates, PayPal/gift-card storefront), and, through crypto tokens with USD-referenced accounting, io.net, Nosana, Render, Akash and Bittensor. Almost everything else (Inference.net/Kuzco, Dria, Lilypad, Spheron Fizz, Psyche, Fortytwo, Petals, Exo) pays in points, not yet-liquid tokens, or nothing. Gensyn has a live token, but no published per-node rates were found. Measured consumer earnings are small: the realistic range is roughly $0–$16/month for a Mac and about $50–$145/month gross for an RTX 4090 at full utilization.

### Cited Findings

**Darkbloom (Eigen Labs) — Apple Silicon Macs**
- Status: started as a research preview in April 2026. Bankless reported in late May 2026 that it had moved to public alpha. It is now a paid provider on OpenRouter — [Bankless](https://www.bankless.com/read/eigen-labs-is-turning-idle-macs-into-a-private-ai-network); [The Register, 24 Aug 2026](https://www.theregister.com/ai-and-ml/2026/08/24/conjure-cash-with-old-macs-by-linking-them-to-ai-inference-borg/5292002)
- Hardware: M1 or later, macOS 14 Sonoma+, 16 GB minimum. The docs say "To actually serve a model, your Mac needs at least 36 GB of unified memory." SIP must be enabled, and only outbound internet is needed (no port forwarding) — [Darkbloom provider overview](https://eigenlabs-f49d2e97.mintlify.app/provider/overview)
- Model RAM examples: Qwen3.5-27B 8-bit needs 27 GB, Gemma-4-26B-A4B 8-bit needs 28 GB, and Qwen3.5-122B-A10B needs a 128 GB Mac — [Darkbloom provider quickstart](https://docs.darkbloom.dev/quickstart-provider)
- Inference runs as a hardened Swift process on the GPU through MLX/vllm-mlx. The coordinator runs in an AMD SEV-SNP confidential VM, and debugger attach is blocked (PT_DENY_ATTACH) — [The Register](https://www.theregister.com/ai-and-ml/2026/08/24/conjure-cash-with-old-macs-by-linking-them-to-ai-inference-borg/5292002)
- Attestation: the Secure Enclave generates an attestation on connect, and the coordinator re-checks it every 5 minutes. Routing stops if the security posture changes — [Darkbloom provider overview](https://eigenlabs-f49d2e97.mintlify.app/provider/overview)
- MDM: an independent reviewer reports that hardware attestation requires enrolling the Mac in Darkbloom's device management, and warns against doing this on main, company-managed or school-managed Macs — [Miya AI Lab review, Jun 2026 (updated Sep 2026)](https://miyagadget.page/en/blog/2026/06/24/darkbloom-provider-mac-earnings-review-en/). The ToS confirms MDM enrollment ("you are enrolling a personally owned device and accept the risks associated with granting administrative control") and says Darkbloom requests "only the minimum MDM permissions necessary to verify your device's security posture" — [Darkbloom Terms (redirects to darkbloom.ai)](https://www.darkbloom.ai/terms.html)
- Payment: per token served, paid through Stripe Connect to a bank account or debit card once the balance reaches a threshold (amount not stated). Darkbloom advertises a 0% platform fee — [Darkbloom provider overview](https://eigenlabs-f49d2e97.mintlify.app/provider/overview). The ToS says identity/compliance checks may be required before provider payouts, and that Stripe and Privy may be used — [Darkbloom Terms](https://www.darkbloom.ai/terms.html)
- Rates: there is a base reward of $0.002083 per 5 minutes (about $0.60/day, or about $18/month). It requires attestation and at least 90% uptime and comes from a fixed budget. Individual jobs paid about $0.000001–$0.000024 each — [Miya AI Lab review](https://miyagadget.page/en/blog/2026/06/24/darkbloom-provider-mac-earnings-review-en/). The ToS says base rewards "may be modified, reduced, or discontinued at any time" and that providers should not treat the service as a "primary or stable source of income" — [Darkbloom Terms](https://www.darkbloom.ai/terms.html)
- Company earnings claims: $120–$200/month per Mac, and $192–$4,983/year per device after about $2/month of electricity. About 250 Macs are online, 4.5B tokens have been served, and ARR is $102K — [Eigen Labs on X](https://x.com/eigenlabs/status/2090937606620303675); [The Register](https://www.theregister.com/ai-and-ml/2026/08/24/conjure-cash-with-old-macs-by-linking-them-to-ai-inference-borg/5292002). A search-snippet summary noted that $102K ARR is about $280/day, which is inconsistent with 250 Macs earning $120–200 each (this is an informal critique, not an audit) — [search summary of social posts; see KuCoin/Dealroom coverage](https://app.dealroom.co/news/feed/old-mac-owners-can-earn-150-250-month-by-joining-ai-inference-network)
- Automation surface: install with `curl -fsSL https://api.darkbloom.dev/install.sh | bash`. Then `darkbloom login` uses a device-code flow (a URL plus a code, approved in a browser). Other commands are `darkbloom start` (background daemon), `status`, `earnings` (total plus per-session breakdown), `models list` and `doctor`. `provider.toml` supports availability windows. A SwiftUI menu-bar app with idle detection exists. No headless/API-token login or provider stats API is documented beyond the CLI — [Darkbloom quickstart](https://docs.darkbloom.dev/quickstart-provider). A public leaderboard endpoint `GET /v1/leaderboard` and attestation endpoint `GET /v1/providers/attestation` exist — [Darkbloom provider overview](https://eigenlabs-f49d2e97.mintlify.app/provider/overview)
- Aggregation/ToS: the ToS does not expressly prohibit or permit third-party wrappers, multiple devices per account, or aggregating others' devices. It requires providers to "operate only hardware, networks, and accounts you are authorized to use," to "keep credentials confidential," and not to "bypass, disable, spoof, tamper with, or interfere with attestation." It also says a separate written agreement overrides the standard terms — [Darkbloom Terms](https://www.darkbloom.ai/terms.html). The Register summarized the ToS as requiring a personally owned device and barring cloud-provider rental arbitrage — [The Register](https://www.theregister.com/ai-and-ml/2026/08/24/conjure-cash-with-old-macs-by-linking-them-to-ai-inference-borg/5292002)
- An unaffiliated third-party monitoring app, BloomGauge, shows up to ten Macs in one view — [AlternativeTo listing](https://alternativeto.net/software/bloomgauge/about/)
- Idle behavior: a MacBook sleeps when the lid closes regardless of pmset, and the backend stops after 1 hour of idle, which means a 10–30 s cold start — [Miya AI Lab review](https://miyagadget.page/en/blog/2026/06/24/darkbloom-provider-mac-earnings-review-en/)

**Salad (SaladCloud consumer "Chopping" app) — NVIDIA GPUs, Windows**
- Status: live. It pays posted USD per-hour rates by GPU and priority tier. Rates effective 12 Sep 2026 (High/Medium/Low/Lowest): RTX 5090 $0.300/$0.271/$0.233/$0.175; RTX 4090 $0.198/$0.178/$0.152/$0.112; RTX 4080 $0.138/$0.124/$0.105/$0.077; RTX 4070 Ti $0.120/$0.104/$0.084/$0.056; RTX 4070 $0.090/$0.080/$0.068/$0.049; RTX 4060 Ti 16GB $0.084/$0.078/$0.070/$0.056. RTX 3060/3070 are not in the chart ("not changing"). CPU work pays $0.004/vCPU-hour — [Salad blog, Sep 2026](https://blog.salad.com/earning-rate-changes-september-2026/)
- Rates went up for the 4090/5090 because demand exceeded supply, and down for the 4070/4080 class — [Salad blog](https://blog.salad.com/earning-rate-changes-september-2026/)
- Payout: Salad Balance is redeemed in a storefront, including a PayPal reward (requires a linked PayPal account and a supported country, arrives within about 24 h) and prepaid Mastercards. There is a $1,000/day redemption cap — [Salad PayPal guide](https://support.salad.com/rewards/redeeming-your-rewards/how-to-redeem-paypal); [Salad withdrawal limit](https://support.salad.com/article/202-maximum-daily-withdrawal)
- Aggregation: "Creating, or using, multiple Salad Accounts is a breach of the terms" and can get all accounts permanently banned — [Salad support](https://support.salad.com/article/46-can-i-create-multiple-accounts). No white-label or partner SDK for embedding Salad's supply side was found. The SaladCloud SDK is for customers deploying containers — [Salad docs](https://docs.salad.com/tutorials/high-performance-apps). Salad has protocol-level partnerships (Render subnet proposal, Golem), not app-developer white-labeling — [Salad blog](https://blog.salad.com/salad-integration-render-network/)
- Gaming coexistence: Salad pauses when it detects GPU-heavy games, other miners, Blender or high-res streaming. The user gets 60 s to close the program, otherwise the GPU is disabled for the session. There are "Pause Until Idle" and auto-start-on-idle options — [Salad Temporary Workload Block](https://support.salad.com/faq/salad-app/temporary-workload-block); [Salad mining states](https://support.salad.com/guides/using-salad/salads-different-mining-states)

**io.net (IO Worker)**
- Payment: hourly block rewards from a 300M IO emission pool plus per-job payments, in IO tokens. Since June 2026 the "Incentive Dynamic Engine" computes supplier income in dollar terms each epoch. Network-wide daily earnings average about $35–36K — [CoinDesk Research](https://www.coindesk.com/research/the-incentive-dynamic-engine-a-new-era-for-io-net-tokenomics); [Own Your Mind review](https://ownyourmind.ai/projects/io-net/)
- Staking: a minimum of 200 IO per card, scaled by the device earning multiplier (`200 × max(1, multiplier) × GPUs`), with a 14-day unstake cooldown — [io.net staking docs](https://io.net/docs/guides/staking/staking). Minimum hardware is 12 GB RAM and a 256 GB SSD, with a 12 h wait before a new device can be hired — [io.net device onboarding](https://io.net/docs/guides/workers/device-onboarding). At least 5 h/day uptime is required, and consumer cards mostly earn idle rewards — [Own Your Mind GPU income, Oct 2026](https://ownyourmind.ai/use/gpu-provider-income/)
- Credibility: Own Your Mind claims 30,000+ devices are marketed against about 2,447 on io.net's own explorer (unverified) — [Own Your Mind](https://ownyourmind.ai/projects/io-net/)

**Nosana — NVIDIA, native Linux**
- Live. In September 2026 it ran a campaign for RTX 4090/5090 hosts: an extra incentive equal to 20% daily utilization (4.8 paid hours/day) on top of customer jobs. Hosts must be registered and verified and keep at least 85% daily uptime. Demand for 4090/5090 exceeds supply. Hosting "runs on native Linux" (a dual-boot Ubuntu guide), and WSL2 is not mentioned. The payout currency is not stated in the post, and $NOS "coordinates incentives" — [Nosana Monthly Sep 2026](https://nosana.com/blog/nosana-monthly-september-2026/)
- Older data: a 4090 priced to customers at $0.32/h, and about 300 nodes (more than a year old) — [Messari](https://messari.io/report/decentralized-ai-platform-nosana-readies-for-mainnet)

**Inference.net (formerly Kuzco) — NVIDIA / Apple M-series, Solana**
- Workers earn $INT points proportional to throughput/uptime, which convert to $INT at a TGE. No TGE confirmation was found — [Solana Compass](https://solanacompass.com/projects/kuzco). The devnet docs said "there will be bugs, and periods of downtime" — [Inference devnet docs (search snippet)](https://docs.devnet.inference.net/getting-started/introduction). When fetched on 2026-10-16, that docs URL 301-redirected to x.com/inference_net, which suggests the devnet docs have been taken down (my observation; no announcement found). A third-party guide lists 16 GB+ VRAM — [Spheron guide](https://docs.spheron.network/guide-to-deploy/nodes/inference-devnet)
- The company now sells centralized models such as Schematron V2 (released 12 Sep 2026) through OpenRouter — [OpenRouter](https://openrouter.ai/inference-net)

**Gensyn — RL Swarm**
- The $AI token sale ran 15–20 Dec 2025. Mainnet plus the Delphi information market reportedly went live on 22 Apr 2026, and the token launched on 29 Apr 2026 (10B max supply, about 13% circulating by July 2026). Testnet rewards were 2% of supply — [PR Newswire](https://www.prnewswire.co.uk/news-releases/gensyn-launches-ai-token-sale-on-sonar-302642323.html); [CoinGabbar](https://www.coingabbar.com/en/crypto-currency-news/gensyn-ai-token-launch-delphi-mainnet-kraken-2026); one review called it "testnet only" in early May 2026 — [Own Your Mind](https://ownyourmind.ai/projects/gensyn/)
- RL Swarm officially supports CUDA on RTX 3090/4090/5090/A100/H100. On macOS it runs CPU-only by default, and the macOS docs ask for at least 32 GB RAM — [gensyn-ai/rl-swarm GitHub](https://github.com/gensyn-ai/rl-swarm); [Gensyn macOS docs](https://docs.gensyn.ai/testnet/rl-swarm/getting-started/macos)
- No published per-node mainnet reward rates were found (see Gaps).

**Prime Intellect**
- Its live product is a GPU marketplace that aggregates more than 50 data-center providers (4090s at about $0.35/h on the buyer side) — [API Evangelist profile](https://github.com/api-evangelist/prime-intellect); [ComputeComparison](https://computecomparison.com/provider/prime-intellect). The protocol (with contributor payouts) is on the Base Sepolia testnet. INTELLECT-3 was post-trained on a centralized 512-GPU cluster — [Implicator](https://www.implicator.ai/prime-intellects-intellect-3-open-source-ambition-meets-centralized-reality/). No paid consumer-contributor program was found.

**Nous Research Psyche**
- Functionally live on Solana. Training runs such as Hermes 4.3 and Consilience 40B have used it, but Nous does not use the word "mainnet." Contributor rewards are designed as points to be redeemed for a Nous token, and no token or TGE terms have been announced — [Own Your Mind review](https://ownyourmind.ai/projects/nous-research/); [Oak Research](https://oakresearch.io/en/analyses/innovations/nous-research-psyche-open-source-decentralized-ai-revolution). Contributors are screened, with 4090/A100/H100 named — [blocmates on X](https://x.com/563defi/status/1909976170990313594)

**Hyperbolic**
- Aggregates capacity from "vetted data center partners." No consumer supplier payout terms were found — [Thunder Compute comparison, Aug 2026](https://www.thundercompute.com/blog/thunder-compute-vs-hyperbolic-gpu-cloud); [CostBench](https://costbench.com/software/ai-gpu-cloud/hyperbolic/)

**Akash**
- Providers run a Kubernetes provider stack and are paid in ACT (a USD-pegged compute credit) that is redeemed to AKT — [Akash docs](https://akash.network/docs/providers/getting-started/should-i-run-a-provider/); [Own Your Mind](https://ownyourmind.ai/use/gpu-provider-income/). GPU utilization in Q1 2026 was 33.7% (84 of 334 GPUs) — [Own Your Mind](https://ownyourmind.ai/use/gpu-provider-income/). An "Akash Homenode" early-access program for household GPUs behind NAT uses a WireGuard ingress gateway — [Blockmedia (Korean)](https://www.blockmedia.co.kr/archives/1007051). The renter price for a 4090 on Akash is about $0.30/h — [ComputePrices](https://computeprices.com/providers/akash/gpus/rtx4090)

**Render Network / Dispersed**
- Pays RENDER 7–10 days after approval, with a 5% fee. It is best suited to OctaneRender-capable high-VRAM cards, and operator income is about $300–800/month per high-end GPU for rendering (Own Your Mind estimate) — [Own Your Mind](https://ownyourmind.ai/use/gpu-provider-income/). The Dispersed AI subnet (launched Dec 2025) targets H100/H200/MI300X — [BlockEden, Apr 2026](https://blockeden.xyz/blog/2026/04/30/rendercon-2026-render-network-dispersed-ai-subnet-blackwell-b200-depin-gpu/)

**Bittensor**
- Miners are paid in subnet alpha tokens, which are converted to TAO through pools. Below-threshold miners earn nothing and are deregistered, losing the registration burn. Startup cost is $500–$5,000, and median miners are about breakeven — [misar.blog guide](https://www.misar.blog/@synor/articles/bittensor-subnet-mining). The work "needs ML engineering" — [Own Your Mind](https://ownyourmind.ai/use/gpu-provider-income/). Annualized revenue across 24 subnets is $28–35M, concentrated in large providers such as Chutes — [Crypto Briefing](https://cryptobriefing.com/bittensor-subnets-annualized-revenue/)

**Fortytwo**
- Swarm inference with peer ranking on Monad. The top half of nodes each round earn. Small models (Qwen3-8B, Gemma-3-4B) are meant to suit consumer hardware. As of Nov 2025, crypto payouts were described as a future commercial phase — [The Register, Nov 2025](https://www.theregister.com/software/2025/11/02/fortytwo-has-the-answer-to-everything-decentralized-ai/418422); [arXiv paper](https://arxiv.org/html/2510.24801)

**Dria**
- No token. Nodes earn $DRIA points, plus an "Early Contributor Appreciation Program" paying USDC-equivalent monthly to the top 50% of nodes active in the last 30 days. Pool = eligible points × $0.000025. Claiming requires a wallet login — [Dria node guide, rewards](https://node-guide.dria.co/rewards)

**Spheron Fizz**
- Launched Oct 2024. It runs on Linux and macOS (Windows "coming soon") using Docker. Liveness rewards are in Spheron Points (50% uptime minimum), plus lease income — [Crypto Briefing](https://cryptobriefing.com/spheron-introduces-fizz-node-for-flexible-ai-compute-resources/)

**Lilypad, Distribute.ai**
- Only 2024 to early-2025 information was found. Lilypad paid Lilybit points (a 4090 earned about 2,500/day). Distribute.ai's $DIS had a market cap of about $304K roughly 500 days ago — [Lilypad blog](https://blog.lilypad.tech/incentivenet-lilybit-reward-calculations); [Distribute.ai Medium](https://medium.com/@distributeai/distribute-ai-dis-airdrop-claim-guide-future-plans-a5571784803e)

**Exo, Petals**
- Neither pays contributors. Exo is open-source local clustering software. Petals' "bloom points" were only ever a plan — [Exo review](https://aicoolies.com/reviews/exo-review); [RuntimeWire on Petals, 2026](https://runtimewire.com/article/petals-405b-model-volunteer-gpu-swarm)

**Kryptex**
- No AI-compute payout program was found. A June 2026 anecdote says Kryptex added the Pearl chain to its pool and an RTX 4070 earned about A$66/month, paid in BTC. Kryptex supports BTC/USDT/fiat payouts — [Medium anecdote](https://xylomining.medium.com/im-mining-crypto-while-my-gpu-does-ai-work-at-the-same-time-here-s-how-bdca68f54be9); [CoinSpot review](https://coinspot.io/en/reviews/kryptex-miner/)

**Ritual, Atoma**
- No consumer-hardware reward program was found for Ritual (its docs discuss heterogeneous providers only) — [Ritual docs](https://docs.ritualfoundation.org/). Nothing was found for Atoma.

**Vast.ai (benchmark, not in the original list)**
- Fiat payouts. Host-kept 4090 rates are $0.29–0.59/h at 20–50% typical utilization, which works out to about $50–126/month gross for a 4090. RunPod Community is closed to new hosts — [Own Your Mind, updated 9 Oct 2026](https://ownyourmind.ai/use/gpu-provider-income/)

### Inferences
- Rough monthly gross at 100% utilization from Salad's posted rates: RTX 4090 about $81–143 (Lowest to High × 720 h). RTX 4070 about $35–65. RTX 3060 is unknown (not in the chart, likely below the 4060 Ti's $0.056–0.084/h, i.e. at most about $40–60). Real utilization is lower, and electricity is not deducted.
- Mac (M1/M2/M3, 16–32 GB): Darkbloom can probably earn only the base reward (about $18/month if attested with ≥90% uptime), because most catalog models need ≥36 GB. The one independent measurement (an M1 Max 64 GB) netted about $16/month.
- Inference.net, Psyche, Dria points, Spheron points, Lilypad, Fortytwo and Gensyn testnet should be valued at $0 for Otium's purposes.
- Inference.net appears to have pivoted to selling its own models, and its devnet docs now redirect to X. Treat its worker program as inactive or unverified.

### Gaps
- Gensyn mainnet per-node reward rates, and whether consumer RL Swarm nodes earn liquid $AI today: no primary source was found.
- io.net Apple Silicon/Windows support in 2026: the supported-devices list was not retrieved.
- Hyperbolic consumer supplier terms, Atoma status, and Distribute.ai/Lilypad 2026 status: not found.
- Darkbloom payout threshold, KYC specifics, and the per-device cap per account: not documented.
- Salad RTX 3060/3070 current rates and minimum PayPal redemption: not shown.
- AV/antivirus false-positive risk for any of these clients: no 2026 sources were found.
- No genuinely new (2026) consumer-GPU paid-inference entrant besides Darkbloom was surfaced by searches.

## Q2. Can an operator app aggregate many users' devices under one account and credit users itself?

### Takeaway
No network found offers an official operator/white-label program for consumer devices. Salad explicitly bans multiple accounts. Darkbloom's ToS is silent on aggregation but requires MDM enrollment, Secure Enclave attestation per device, browser device-code login, and "hardware … you are authorized to use." That makes a headless, Otium-owned account across strangers' Macs legally grey, technically awkward, and dependent on Darkbloom's permission. The crypto networks (io.net, Nosana, Akash, Render, Bittensor) are technically wallet-based and could run many workers under one wallet, but they require Linux/Docker/Kubernetes, staking (io.net) or verification, which does not fit a consumer Windows/macOS app.

### Cited Findings
- Salad: using multiple accounts breaches the terms, risking a permanent ban of all accounts — [Salad support](https://support.salad.com/article/46-can-i-create-multiple-accounts). No partner/white-label SDK was found — [Salad docs](https://docs.salad.com/tutorials/high-performance-apps)
- Darkbloom: the ToS does not address wrappers or multi-device aggregation. It requires authorized hardware, confidential credentials, untampered attestation, and SIP plus Secure Boot. A separate written agreement can override the terms — [Darkbloom Terms](https://www.darkbloom.ai/terms.html). Login uses an interactive device-code flow, and no API-token login is documented — [Darkbloom quickstart](https://docs.darkbloom.dev/quickstart-provider). The Register says the ToS requires personally owned devices and bars cloud arbitrage — [The Register](https://www.theregister.com/ai-and-ml/2026/08/24/conjure-cash-with-old-macs-by-linking-them-to-ai-inference-borg/5292002)
- Darkbloom MDM enrollment grants "administrative control" — [Darkbloom Terms](https://www.darkbloom.ai/terms.html); [Miya AI Lab](https://miyagadget.page/en/blog/2026/06/24/darkbloom-provider-mac-earnings-review-en/)
- io.net: the stake scales per GPU (200 IO minimum per card) with a 14-day cooldown, so an operator would have to front stake for every user's GPU — [io.net staking](https://io.net/docs/guides/staking/staking)
- Nosana: hosts must be registered and verified, and hosting runs on native Linux — [Nosana Sep 2026](https://nosana.com/blog/nosana-monthly-september-2026/)
- Lilypad (older) disallowed multiple resource providers on one GPU and required the full server resources — [Lilypad blog](https://blog.lilypad.tech/incentivenet-lilybit-reward-calculations)
- Aethir requires KYC and staking and excludes US residents — [Own Your Mind](https://ownyourmind.ai/use/gpu-provider-income/)

### Inferences
- The most viable path is a written partnership with Eigen Labs (Darkbloom) for Mac users. It would need either per-user Darkbloom accounts and Stripe Connect (Otium only orchestrates, it does not get paid) or an operator agreement. The MDM step is a significant trust/UX hurdle for a third-party app.
- For Windows GPUs, there is no compliant aggregation route through Salad. Vast.ai and Akash need Linux hosts and are rental marketplaces, not background apps.
- Running many users' workers under Otium's single crypto wallet (io.net/Nosana) is technically possible but brings staking capital, Linux/Docker requirements, verification, token-price risk and possibly money-transmission/KYC exposure. It was not explicitly prohibited in the sources found, but no source was found permitting it either.

### Gaps
- No ToS text was retrieved for io.net, Nosana, Akash or Render on third-party operators aggregating consumer devices.
- It is unknown whether Eigen Labs offers partner/fleet programs.

## Q3. Which could run unattended in the background on a gaming PC without breaking games?

### Takeaway
Only Salad has a documented, consumer-grade game-detection/auto-pause system, and it is Salad's own app, not embeddable. Darkbloom's menu-bar app pauses when the Mac is in use, and its availability windows can be set in `provider.toml`. The Linux/Docker networks (Nosana, io.net, Akash) assume dedicated, always-on machines with uptime minimums (5 h/day to 90% uptime), which conflicts with preempting for games.

### Cited Findings
- Salad detects GPU-heavy games, miners, Blender and high-res streaming. It gives the user 60 s to close the program before disabling the GPU for the session. "Pause Until Idle" and auto-start are available — [Salad support](https://support.salad.com/faq/salad-app/temporary-workload-block); [Salad mining states](https://support.salad.com/guides/using-salad/salads-different-mining-states)
- Darkbloom's macOS app has idle detection that pauses serving when the user is active. `provider.toml` availability windows shut down the inference backend outside the windows — [Darkbloom quickstart](https://docs.darkbloom.dev/quickstart-provider). The backend stops after 1 h idle, with a 10–30 s cold start, and the base reward requires ≥90% uptime — [Miya AI Lab](https://miyagadget.page/en/blog/2026/06/24/darkbloom-provider-mac-earnings-review-en/)
- Uptime requirements: Nosana ≥85% daily for its incentive — [Nosana](https://nosana.com/blog/nosana-monthly-september-2026/). io.net ≥5 h/day — [Own Your Mind](https://ownyourmind.ai/use/gpu-provider-income/). Spheron Fizz ≥50% per 24 h — [Crypto Briefing](https://cryptobriefing.com/spheron-introduces-fizz-node-for-flexible-ai-compute-resources/). Lilypad (older) ≥4 h continuous — [Lilypad](https://blog.lilypad.tech/incentivenet-lilybit-reward-calculations)
- Gensyn RL Swarm on Macs is CPU-only and slow, and third-party guides report OOM on MacBooks — [Gensyn macOS docs](https://docs.gensyn.ai/testnet/rl-swarm/getting-started/macos)

### Inferences
- Darkbloom's 90%-uptime base-reward requirement conflicts with "pause when the user is active." On a daily-use Mac, users would lose most of the base reward, which is the bulk of real earnings.
- Inference-serving networks that hold a model resident in VRAM (Darkbloom, Inference.net, Nosana) will compete with games for VRAM unless the client fully unloads. Only Salad documents a release/disable behavior.

### Gaps
- No documentation was found on VRAM release or preemption latency for io.net, Nosana or Akash workers when a game launches.

## Q4. Independent earnings measurements in 2026

### Takeaway
The few independent 2026 measurements are far below vendor claims. Darkbloom's M1 Max (64 GB) measured about $0.60/day (about $16/month net), and later cumulative earnings were only about $2.91. Leaderboard-based observations show top Darkbloom providers under $6/day. For NVIDIA cards, the best-grounded figures are Salad's posted host rates and Own Your Mind's Vast.ai model: a 4090 earns about $50–126/month gross at 20–50% utilization.

### Cited Findings
- Darkbloom, M1 Max 64 GB, v0.6.20, 21–23 Jun 2026: $0.60 over 24 h (564 jobs), almost all from the base reward. Net about $0.53/day, about $16/month. The calculator claimed $75.21/month, assuming 80% utilization against an observed 7–9%. A September update showed $2.91 cumulative (12,275 jobs), and the calculator default had been cut to a 5% duty cycle, estimating about $12.51/month — [Miya AI Lab](https://miyagadget.page/en/blog/2026/06/24/darkbloom-provider-mac-earnings-review-en/)
- Darkbloom leaderboard observation: the top-ranked provider earns under $6/day and the 5th under $2/day, against company claims of $120–200/month — [search summary citing Safemode and others](https://www.safemode.in/blogs/darkbloom-idle-macs-ai-inference-network) (secondary). The Register reports top models averaging $26/$19/$15 per day (company stats) — [The Register](https://www.theregister.com/ai-and-ml/2026/08/24/conjure-cash-with-old-macs-by-linking-them-to-ai-inference-borg/5292002)
- A September 2026 review says Darkbloom "supply … has completely outrun demand" — [MindStudio/aggregated search summary](https://www.mindstudio.ai/blog/dark-bloom-distributed-ai-inference) (secondary)
- Vast.ai 4090 model (Own Your Mind, updated 9 Oct 2026, re-priced 4 Oct): at 20%, 30% and 50% utilization, gross is about $50, $76 and $126/month, and net before amortization is about $38, $59 and $97. A 3090 nets about $0–25/month after amortization — [Own Your Mind](https://ownyourmind.ai/use/gpu-provider-income/)
- io.net network-wide supplier earnings average about $35–36K/day (since March 2026) — [CoinDesk Research](https://www.coindesk.com/research/the-incentive-dynamic-engine-a-new-era-for-io-net-tokenomics)
- A Kryptex RTX 4070 on the Pearl chain earned A$66.56/month (personal anecdote, not AI compute) — [Medium](https://xylomining.medium.com/im-mining-crypto-while-my-gpu-does-ai-work-at-the-same-time-here-s-how-bdca68f54be9)
- Third-party Salad 4090 estimates are $90–250/month gross, and one claims a 20–25% platform cut (unverified) — [CloudEarn](https://www.cloudearn.net/salad/); [Earnifyhub](https://earnifyhub.com/learning-guides/make-money-renting-out-gpu-2026)

### Inferences
- For Otium planning: assume about $0–20/month per Mac (Darkbloom, ≥36 GB only for job income), about $30–100/month per RTX 4090, and about $15–40/month per RTX 4070 at realistic partial utilization, before any operator margin. The RTX 3060 is likely in the low tens of dollars per month at best.

### Gaps
- No independent 2026 measurements were found for io.net, Nosana, Akash, Gensyn or Bittensor on consumer cards, or for any network on an RTX 3060 or an M1/M2 base model.
- No gamefi-roi tracker data was found for these networks.
