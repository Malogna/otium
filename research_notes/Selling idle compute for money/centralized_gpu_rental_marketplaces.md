# Centralized and Semi-Centralized GPU/CPU Rental Marketplaces for Consumer Hosts (as of Oct 2026)

Scope note: Research done 2026-10-15. Many primary pages are JavaScript dashboards (cloud.vast.ai/host/setup, cloud.vast.ai/host/agreement, salad.com/earning-rates) that did not render for the fetch tool, so several numbers could not be extracted and are listed as gaps. Fully decentralized/token networks (io.net, Akash, Nosana, Golem, Render) are only touched on where they affect the comparison.

## Q1. Current status in 2026 (active, closed, invite-only, pivoted) and recent changes

### Takeaway
Only Vast.ai, Salad, and Clore.ai still take ordinary individuals as self-serve hosts in 2026. RunPod Community Cloud is closed to new hosts. TensorDock is data-center-only and owned by Voltage Park. Hyperbolic, Prime Intellect, Cudo and Shadeform are supply-partner or aggregator businesses, not consumer-PC hosting programs.

### Cited Findings
**Vast.ai: active, self-serve**
- The April 2026 product update says the Host Setup page was revised with clearer instructions for new hosts. It also lists a fix for a "Host CLI schedule maintenance command issue". — [Vast.ai April 2026 Product Update](https://vast.ai/article/april-2026-product-update)
- On May 18, 2026 Vast published a host earnings article, which shows it is still actively recruiting hosts. — [Vast.ai: How Much Money Can You Earn](https://vast.ai/article/how-much-money-can-you-earn-renting-out-your-gpu-on-vast-ai)
- Verification is fully automated with no manual review. It depends on reliability, infrastructure, DLPerf score, and supply and demand. "Datacenter" status needs a minimum server count plus third-party certification such as ISO 27001. — [Vast hosting overview](https://docs.vast.ai/documentation/host/hosting-overview)

**Salad (Salad desktop app / SaladCloud): active**
- The download page shows app version 1.9.27, 2026 copyright, and "earning rates as of September 12, 2026". — [Salad download-high-end](https://salad.com/download-high-end)
- The SaladCloud (SCE) docs (updated July 2026) describe a two-sided marketplace connecting PC owners ("chefs") to businesses. Chefs sell idle time, averaging about 22 hours per day. — [SaladCloud architectural overview](https://docs.salad.com/products/sce/getting-started/architectural-overview)
- Salad also sells a separate contracted "Salad Dedicated" product (reserved bare metal and VMs in data centers), distinct from the consumer pool. — [salad.com/products](https://salad.com/products)
- The get-started page advertises "60,000+ GPUs Online Now" (marketing figure). — [salad.com/get-started](https://salad.com/get-started)

**RunPod Community Cloud: closed to new hosts**
- RunPod docs state the Community Cloud host program, which let individual compute providers contribute GPUs, is closed to new participants. — [RunPod docs: hosting](https://docs.runpod.io/hosting) (via search snippet; the fetched overview page did not show this text)
- RunPod's blog post "An update on the community cloud hosting application process" says no new hosts will be onboarded. RunPod attributes this to a large expansion of Secure Cloud capacity across T3/T4 data centers. It points would-be suppliers to Secure Cloud, whose bar is "much higher". A search aggregator dates this to about February 2026. — [RunPod blog](https://www.runpod.io/blog/an-update-on-the-community-cloud-hosting-application-process); [Plushcap RunPod Feb 2026 summaries](https://www.plushcap.com/companies/runpod/blog/summaries/2026/02). Caveat: when I fetched the blog URL it redirected to an unrelated Clusters product page, so I could not read the post directly.
- Secure Cloud partners are described as T3/T4 data centers meeting SOC 2, ISO 27001 and PCI DSS. — [RunPod docs overview](https://docs.runpod.io/hosting/overview)
- Sacra describes a separate "Become a Host" federated program with regional partners. Its eligibility and status are unknown. — [Sacra RunPod](https://sacra.com/c/runpod/)

**TensorDock: active, but data-center hosts only; acquired**
- Voltage Park acquired TensorDock in March 2025 and said it will "remain one of many participants on the TensorDock marketplace" and keep sourcing hardware suppliers. — [Voltage Park blog](https://voltagepark.com/blog/tensordock-joins-voltage-park-a-new-chapter-begins)
- The host page requires data-center placement with redundant power and internet, plus strict vetting. — [TensorDock host page](https://www.tensordock.com/host.html)

**Clore.ai: active, self-serve**
- The docs carry current installer channels (Stable/Beta/Legacy) and recommend the NVIDIA R580/R590 drivers, including RTX 50-series guidance, which points to maintenance in 2025-2026. — [Clore install on HiveOS](https://docs.clore.ai/clore.ai/clore.ai-eng-hi/for-hosts/installing-clore-hosting/installing-clore-hosting-software)

**Hyperbolic: supply sourced from businesses**
- A Dec 12, 2024 post says Hyperbolic aggregates "underutilized GPUs from data centers, mining farms, and personal machines". It advertises "5-minute onboarding" with suppliers setting their own prices. — [Hyperbolic blog](https://www.hyperbolic.ai/blog/deep-dive-into-hyperbolic-gpu-marketplace)
- An April 28, 2026 Head of Supply posting emphasizes onboarding "NeoCloud providers". — [Variant job posting](https://jobs.variant.fund/companies/hyperbolic-2/jobs/76450999-head-of-supply)
- On June 10, 2026 Hyperbolic launched "Forge", a layer that provisions, hardens, monitors and sanitizes machines from fragmented global supply. — [Principal VC Substack](https://principalvc.substack.com/p/hyperbolic-launches-forge-an-infrastructure)

**Prime Intellect: no public self-serve provider program**
- The protocol post says workers can only be launched through the Prime Intellect platform, with "permissionless contributions soon" (undated). — [Prime Intellect protocol blog](https://primeintellect.ai/blog/protocol)
- A Feb 2026 Compute Partnerships Lead posting describes securing GPU capacity from cloud providers, data centers and hardware vendors, i.e. B2B supply. — [Protocol Labs jobs](https://jobs.protocol.ai/companies/prime-intellect/jobs/67934436-compute-partnerships-lead)

**Shadeform: aggregator with no individual hosting**
- Shadeform is a unified API and console across 20-30+ GPU clouds (Lambda, Nebius, Crusoe, DigitalOcean, etc.). Supply comes from cloud providers. — [AIMultiple](https://aimultiple.com/gpu-marketplace); [Cerebral Valley](https://cerebralvalley.beehiiv.com/p/gpu-clouds-everywhere-are-sold-out-shadeform-is-building-the-solution)
- In March 2025 it partnered with WhiteFiber (Bit Digital) for B200s. I found no evidence of an acquisition. — [TipRanks](https://tipranks.com/news/the-fly/bit-digitals-whitefiber-partners-with-shadeform-to-deliver-nvidia-b200-gpus)

**Cudo Compute: data-center and service-provider suppliers**
- Cudo says it is "currently focusing on targeting data center vendors and service providers" and asks suppliers to register interest. — [Cudo supplier docs](https://docs.cudocompute.com/suppliers.md)
- Its 2023 one-pager also mentions mining farms and USB-installed auto-provisioning software. — [Cudo 2023 supplier 1-pager](https://www.cudocompute.com/resources/2023-cudo-compute-supplier-1-pager.pdf) (stale, 2023)

**Spheron: token network whose consumer path is uncertain**
- "Fizz Nodes" are lightweight nodes for personal computers and laptops. Rewards require at least 50% uptime per 24h ERA. — [Spheron docs: Fizz](https://docs.spheron.network/core-concepts/fizz)
- The 2025 roadmap and a migration note drop many GTX 10, RTX 20 and entry RTX 30/40 GPUs. — [Spheron roadmap 2025](https://spheron-roadmap.super.site/knowledge-gallery/roadmap-2025)
- The SPON token reportedly launched July 2025. — [Coinranking](https://coinranking.com/coin/wra4uV6iN+spheronnetwork-spon/about) (third-party)
- Spheron's 2026 marketing now centers on a GPU marketplace of data-center supply. — [spheron.network](https://spheron.network)

**Kryptex: no GPU rental marketplace for individual sellers found**
- The only rental page is a "Kryptex GPU Network" built on mining farms, with a price comparison dated July 23, 2019 (stale). — [Kryptex rent-cloud-gpus](https://www.kryptex.com/rent-cloud-gpus)
- Kryptex's pool runs a guide on mining via rented Clore.ai rigs (March 2025), which makes it a buyer-side partner, not a host program. — [Kryptex pool article](https://pool.kryptex.com/articles/kryptex-clore-mining-en)

**Other significant platform (decentralized, for completeness): io.net**
- io.net has official worker install guides for macOS (Apple M3/M4, Docker Desktop) and Windows. — [io.net macOS guide](https://io.net/docs/guides/workers/install-on-macos); [io.net Windows guide](https://io.net/docs/guides/workers/install-on-windows)
- A support article warned that adding new workers was "currently disabled". Its date is unknown. — [io.net support](https://support.io.net/en/support/solutions/articles/156000014581-how-to-add-a-new-worker-macos)

### Inferences
- For a consumer one-click app, the realistic integration targets are Salad (consumer-native, Windows), Vast.ai (Linux-only, prosumer) and Clore.ai (Linux/HiveOS, crypto-heavy).
- RunPod, TensorDock, Cudo, Hyperbolic, Prime Intellect and Shadeform are effectively closed to consumer PCs. Shadeform is a buyer-side aggregator with no host path at all.
- The 2025-2026 trend is consolidation toward data-center supply: RunPod closing Community Cloud, TensorDock's data-center rules, Spheron dropping low-end GPUs.

### Gaps
- I could not read the RunPod blog post text directly, so the exact closure date and the treatment of existing hosts are unconfirmed.
- No 2026 primary source on whether Spheron Fizz nodes still accept new registrations or pay out.
- No public self-serve supplier sign-up URL found for Hyperbolic. Its "personal machines" claim is from Dec 2024.
- No current status found for a Kryptex GPU-rental service.

## Q2. Supported host OS, Docker/WSL2, dedicated machine requirements

### Takeaway
Salad is the only significant platform that runs on a normal Windows gaming PC. It uses WSL2/Hyper-V for container jobs and has a Linux beta for dedicated machines; there is no macOS support. Vast.ai, Clore.ai and TensorDock all need Ubuntu (or HiveOS for Clore) installed on the machine itself. No major centralized marketplace supports macOS / Apple Silicon hosting; io.net is the exception.

### Cited Findings
**Salad**
- Supports recent Windows 10 and 11, has a Linux beta "for Chefs who have dedicated Linux boxes", and says "We have no plans to support MacOS". — [Salad support: macOS/Linux](https://support.salad.com/faq/compatibility/do-you-support-macos-and-linux)
- The Linux build is "early beta". Access is granted to specific users after verification, and large operators are invited to sign a contract. Only a few distros are supported, and no other containerd/Docker packages may be installed, because Salad uses the official containerd.io package. — [Salad Linux onboarding](https://support.salad.com/guides/getting-started/linux-onboarding)
- Container jobs require WSL2. Workloads run in an OCI container on a Linux VM partitioned by a hypervisor. They need Windows 11 or Windows 10 1903 (build 18362)+, and BIOS virtualization may need to be enabled. — [Salad community: container workloads](https://community.salad.com/container-workloads-and-your-pc/); [Salad security](https://salad.com/security)

**Vast.ai**
- Hosts install Ubuntu themselves and handle NVIDIA drivers, disk partitioning and router port configuration. They should disable auto-updates. Windows/WSL is mentioned only for running the CLI, not for hosting. — [Vast hosting overview](https://docs.vast.ai/documentation/host/hosting-overview)
- The older official FAQ references Ubuntu 16.04 (stale). — [Vast FAQ](https://vast.ai/faq)
- A third-party guide lists Ubuntu 22.04/24.04, NVIDIA driver 535, NVIDIA Container Toolkit, Docker, and the "Kaalia" host daemon. — [AG-Sec4 VastAI-GPU-Host-Guide (GitHub)](https://github.com/AG-Sec4/VastAI-GPU-Host-Guide) (unofficial)
- Client workloads run as Docker images, and Vast also offers Linux VM instances. — [Vast using-your-own-machine](https://docs.vast.ai/host/using-your-own-machine.md); [Vast Linux VMs](https://docs.vast.ai/linux-virtual-machines)

**Clore.ai**
- Runs on HiveOS or standard Ubuntu server with the same install command. Windows is not mentioned. A working NVIDIA driver is required before install, because the installer does not install drivers. — [Clore install guide](https://docs.clore.ai/clore.ai/clore.ai-eng-hi/for-hosts/installing-clore-hosting/installing-clore-hosting-software)

**TensorDock**
- Ubuntu 22.04 LTS with a libvirt (KVM) hypervisor. GPU passthrough needs a separate display device. — [TensorDock host](https://www.tensordock.com/host.html)

**io.net (decentralized)**
- macOS (M3/M4) and Windows workers via Docker Desktop. — [io.net macOS](https://io.net/docs/guides/workers/install-on-macos)

### Inferences
- A cross-platform one-click app could wrap Salad on Windows only, and only as a launcher; it cannot replicate Salad's agent.
- For Vast and Clore, "one-click" would mean an OS reinstall or dual-boot to Ubuntu/HiveOS. Neither supports a WSL2-hosted node on a gaming PC. This is an inference from the docs; neither explicitly forbids WSL2, but neither supports it.
- No viable macOS path exists among the centralized marketplaces.

### Gaps
- The official current Ubuntu versions for Vast (the cloud.vast.ai/host/setup page did not render).
- The distros supported by the Salad Linux beta.
- Whether Vast tolerates dual-boot or non-dedicated machines. Reliability penalties for downtime suggest it is discouraged.

## Q3. Hardware requirements (GPU, CPU, RAM, disk, network, uptime, IP/ports)

### Takeaway
Salad's bar is lowest. Vast and Clore accept consumer NVIDIA cards but need open ports and high uptime. TensorDock requires data-center-class infrastructure.

### Cited Findings
**Salad**
- Windows 10/11 64-bit, NVIDIA and AMD GPUs. An RTX 3060 or later gets the best job access, and older GPUs can do CPU or bandwidth workloads. Recommended: 4-core CPU, 12 GB RAM, stable broadband, PC awake. — [Salad download-high-end](https://salad.com/download-high-end)
- Container jobs need at least 10 Mbps download. — [Salad support: internet speed](https://support.salad.com/faq/compatibility/what-is-the-minimum-recommended-internet-speed-for-salad)
- CPU-only chopping is possible when there is no compatible GPU or a low-end one (GT 1030, GTX 1050). — [Salad support: GPU vs CPU](https://support.salad.io/article/247-should-i-enable-my-gpu-my-cpu-or-both)
- "Star Chef" status (more than 50 h/week) gives priority for higher-paying jobs. — [Salad earning rates (search snippet)](https://salad.com/earning-rates)

**Vast.ai**
- The FAQ says the software "will generally accept recent/decent Nvidia GPUs (GTX 10XX series)". Revenue depends on real workload performance, and deep learning also needs IO and CPU per GPU. — [Vast FAQ](https://vast.ai/faq) (older)
- Clients need open ports. The unofficial guide cites the 16384–32768 range. — [Vast hosting overview](https://docs.vast.ai/documentation/host/hosting-overview); [AG-Sec4 guide](https://github.com/AG-Sec4/VastAI-GPU-Host-Guide)
- Restarting during an active rental lowers the reliability score. — [Vast hosting overview](https://docs.vast.ai/documentation/host/hosting-overview)
- A third-party guide claims machines below 90% reliability get only spot traffic. — [MiningBoard](https://miningboard.com/guides/vast-ai-bid-pricing) (unverified)

**Clore.ai**
- NVIDIA only, with AMD not supported. Minimums: 32 GB disk (SSD preferred), 8 GB RAM (16 GB recommended), Celeron-class CPU (i7-6700 suggested). Overclocks and power limits must be disabled. Outbound access is needed to Docker, NVIDIA, GitHub, GitLab, Docker Hub and api.clore.ai. — [Clore install guide](https://docs.clore.ai/clore.ai/clore.ai-eng-hi/for-hosts/installing-clore-hosting/installing-clore-hosting-software)

**TensorDock**
- Per GPU: 16 GB+ DDR4/5, 8+ vCPU at ≥3.0 GHz, 1 TB NVMe (TLC, DRAM cache), and PCIe ≥16 GB/s.
- Network: 1 Gbps (shared across up to 40 GPUs), static IP, plus 100 forwarded ports or a dedicated IP.
- Placement and uptime: data center with redundant power and internet, 24/7 uptime, maintenance announced 2 weeks ahead.
- [TensorDock host](https://www.tensordock.com/host.html)

**Spheron Fizz**
- At least 50% uptime per 24h. Many GTX 10, RTX 20 and entry RTX 30/40 cards are delisted. — [Spheron Fizz docs](https://docs.spheron.network/core-concepts/fizz); [Spheron roadmap](https://spheron-roadmap.super.site/knowledge-gallery/roadmap-2025)

### Inferences
- Port forwarding and UPnP for Vast is a major friction point for consumer users behind CGNAT. Salad avoids it because it uses an outbound-only network.

### Gaps
- Official Vast minimums for RAM, disk and bandwidth are not published on the pages I could read.
- Clore's network/ports page was not retrieved.

## Q4. Payout currency, method, minimum, fees

### Takeaway
Vast pays USD via Wise, PayPal or Stripe with a $20 minimum, and listed prices are about 25% above host earnings. Salad pays in "Salad Balance" redeemable for PayPal cash or gift cards. Clore pays in CLORE, BTC or stablecoins, with a hefty extra fee unless you hold or lock CLORE. TensorDock takes 25% and pays via Stripe.

### Cited Findings
**Vast.ai**
- Payouts go only through Wise, PayPal or Stripe, with no ACH, wire or SWIFT. Minimum is $20 (lower balances roll forward). Invoices are generated weekly on Fridays and paid the following Friday, and the first payout takes up to 2 weeks. Hosts must be able to receive B2B payments. — [Vast payment docs](https://docs.vast.ai/host/payment.md)
- In June 2024 Vast removed explicit "hosting fees" and replaced them with an internal surcharge, so "prices hosts set will be the prices they earn". — [Vast June 2024 update](https://vast.ai/article/june-2024-product-update)
- Vast's May 2026 article says live GPU prices are about 25% higher than what hosts earn. — [Vast earnings article](https://vast.ai/article/how-much-money-can-you-earn-renting-out-your-gpu-on-vast-ai)
- Third-party claims conflict: "host keeps 85%" from MiningBoard versus a $25 minimum from CashPilot. Both are unverified. — [MiningBoard](https://miningboard.com/rental-markets/vast-ai); [CashPilot guide](https://raw.githubusercontent.com/GeiserX/CashPilot/HEAD/docs/guides/vast-ai.md)

**Salad**
- Earnings accrue as Salad Balance and are redeemed in the Salad Storefront. PayPal redemption cashes out to the user's bank in local currency, takes up to 24h, and is available only in PayPal-supported countries. — [Salad: redeem PayPal](https://support.salad.com/rewards/redeeming-your-rewards/how-to-redeem-paypal); [Salad PayPal FAQ](https://support.salad.com/article/228-faq-on-paypal-rewards)
- The Storefront also offers gift cards and games. — [Salad basics](https://support.salad.com/article/135-getting-started-with-salad-the-basics)

**Clore.ai**
- Base fee is 10% on on-demand and 2.5% on spot, split 50/50 between renter and host. — [Clore fee structure](https://docs.clore.ai/for-renters/fee-structure.md)
- When paid in BTC, USDT or USDC, an extra 15% host fee applies; it is 0% for CLORE. The extra fee can be reduced by locking CLORE in "MFP" tiers, and Proof-of-Holding cuts the base fee by up to 50% at 2M CLORE.
- Worked example on a 100 CLORE listing with no PoH or MFP: the host keeps 95 if paid in CLORE, or 80 if paid in BTC/USDT.
- [Clore host fees](https://docs.clore.ai/clore.ai-eng-de/fur-hosts/host-fees)
- The default auto-price is $5 per server per day. — [Clore install guide](https://docs.clore.ai/clore.ai/clore.ai-eng-hi/for-hosts/installing-clore-hosting/installing-clore-hosting-software)

**TensorDock**
- 25% revenue share, payouts via Stripe, and TensorDock covers fees on withdrawals above $10K. — [TensorDock host](https://www.tensordock.com/host.html)

**Spheron**
- Most leasing fees go to operators after network fees, and rewards are in the SPON token. — [Spheron Fizz docs](https://docs.spheron.network/core-concepts/fizz); [Coinranking](https://coinranking.com/coin/wra4uV6iN+spheronnetwork-spon/about)

### Inferences
- Vast's B2B payout requirement could make it awkward for minors or for countries without Wise, PayPal or Stripe business receiving.
- Clore's fee design pushes hosts into holding CLORE, which is token-price exposure a consumer app should disclose.

### Gaps
- Salad's minimum PayPal redemption amount and Salad's margin over chef pay.
- Clore's withdrawal minimum: third-party snapshots disagree between "$50" and none.
- Hyperbolic, Cudo and Prime Intellect supplier payout terms are not public.

## Q5. Typical earnings for consumer hardware and utilization

### Takeaway
Credible 2026 numbers are scarce.
- Vast's official 2026 range is $0.30–0.60 per GPU-hour for consumer cards (RTX 5090 cited), before utilization.
- Salad's undated marketing figure for an RTX 4090 is $130–150/month, roughly $4.30–5.00/day.
- Real utilization is the dominant unknown.

### Cited Findings
**Vast.ai**
- May 18, 2026: consumer GPUs (RTX 5090) earn $0.30–$0.60/GPU-hr depending on rental type and reliability. H100/H200 earn $2.15–$4.00+. Example: a 4×RTX 5090 rig at 80% utilization earns $700–$1,400/month. Interruptible clients pay 50–80% less than on-demand. — [Vast earnings article](https://vast.ai/article/how-much-money-can-you-earn-renting-out-your-gpu-on-vast-ai)
- Third-party 2026 claims on 4090 renter prices conflict: $0.169/h, $0.22–0.35/h, and $0.39–0.59/h. One unsourced guide claims 65–85% utilization for a well-priced 4090. — [MiningBoard](https://miningboard.com/guides/rent-gpu-for-ai-earnings); [deploybase](https://deploybase.ai/articles/vastai-review-2026-pricing-performance-pros-cons); [fast.io](https://fast.io/resources/vast-ai-review-2026/) (all unverified, affiliate-style)

**Salad**
- RTX 4090 at $130–150/month with 16–64 GB RAM. The figure is undated; the page notes rates depend on demand. — [Salad download-high-end (search snippet)](https://salad.com/download-high-end)
- The live earning-rates page reports per-chopping-hour averages, a "most machines" range and a "top chefs" figure over the last 7 days. It counts only machines averaging at least 4 h/day of chopping. — [Salad earning rates](https://salad.com/earning-rates)
- Renter price for a 4090 on SaladCloud is about $0.16/hr according to a tracker. Chef earnings are lower. — [computeprices.com](https://computeprices.com/providers/packet/gpus/rtx4090)

**Clore.ai**
- No reliable 2025-2026 earnings data found. Pricing is set relative to mining profitability or AI demand. — [Clore install overview](https://docs.clore.ai/for-hosts/installing-clore-hosting.md)

### Inferences
- If a 4090 earns $0.20–0.35/hr net at 50–80% utilization, that is roughly $2.40–6.70/day gross before electricity. This is my arithmetic from the ranges above and is unverified.
- CPU-only earnings on Salad are likely cents per day. No source was found.

### Gaps
- Per-GPU figures for 3060/3080/4070 on Salad's live page could not be extracted (JS-rendered).
- No 2025-2026 Reddit host-earnings threads surfaced in search.
- No verified CPU-only earnings for any platform.

## Q6. Automation surface (CLI, API, daemon, install script)

### Takeaway
Vast has the richest host automation:
- an official `vastai` CLI and Python SDK with host commands (list machine, set min-bid, show earnings, schedule maintenance)
- a host daemon installed by script

Clore has a scriptable one-line installer with token registration. Salad has no documented public chef-side API; its automation is the Windows desktop app itself.

### Cited Findings
**Vast.ai**
- The CLI reference includes host commands such as `list machine`/`list machines`, `show earnings` ("machine earning history reports") and `set min-bid`.
- The Python SDK exposes `show_earnings(start_date, end_date, machine_id)` and `set_min_bid(id, price)`.
- [Vast CLI commands](https://docs.vast.ai/cli/commands); [vastai PyPI](https://pypi.org/project/vastai); [Vast Python SDK](https://vast.ai/docs/cli/python-sdk); [set-min-bid](https://docs.vast.ai/host/cli/set-min-bid.md)
- The `list machines` command takes `-g` (GPU price), `-s` (disk price) and `-u` (upload price). — [list-machines](https://docs.vast.ai/host/cli/list-machines.md)
- Hosts can run their own "default jobs" (Docker images) when the machine is idle. — [using-your-own-machine](https://docs.vast.ai/host/using-your-own-machine.md)
- An official uninstall script is published at s3.amazonaws.com/vast.ai/uninstall. — [Vast hosting overview](https://docs.vast.ai/documentation/host/hosting-overview)

**Clore.ai**
- Install: `bash <(curl -s https://gitlab.com/cloreai-public/hosting-agent-installer/-/raw/main/install.sh)` with `--init-token <token>`.
- Re-register with `/opt/clore-hosting/clore.sh --init-token`; reset with `--reset`.
- There are HiveOS flight-sheet integration and a Telegram bot for notifications.
- [Clore install guide](https://docs.clore.ai/clore.ai/clore.ai-eng-hi/for-hosts/installing-clore-hosting/installing-clore-hosting-software); [Clore HiveOS](https://docs.clore.ai/clore.ai/clore.ai-eng-hi/for-hosts/advanced/hiveos-integration)

**TensorDock**
- After vetting, hosts install a libvirt-based hypervisor package. — [TensorDock host](https://www.tensordock.com/host.html)

**Salad**
- Public docs (docs.salad.com) cover the SaladCloud renter API, not a chef-side API. — [docs.salad.com](https://docs.salad.com/)

### Inferences
- A third-party app could drive Vast end to end on Linux: run the install script, call `vastai` with the host API key for pricing and earnings, and use the uninstall script.
- Clore can be driven via its installer and token.
- With Salad, the realistic option is to bundle or launch the Salad app plus a referral link; earnings read-back is not available.

### Gaps
- Whether Clore exposes a host-side REST API for earnings (only renter API docs were seen).
- Whether Salad's app has any CLI or headless mode on Windows. The Linux beta implies a headless agent but is contract-gated.

## Q7. ToS: third-party wrappers, resellers, referral/affiliate programs

### Takeaway
Referral or affiliate programs exist at Salad. I found no explicit permission or prohibition for third-party host wrappers on any platform, and this needs direct confirmation.

### Cited Findings
- Salad referral codes can only be entered at signup. The referee gets a 2× earning bonus until $4 earned. — [Salad: referral code](https://support.salad.com/hc/en-us/articles/360042398312)
- Salad runs an application-based affiliate program with tiered rewards. Tier amounts are not public. — [Salad Affiliate](https://salad.com/affiliate)
- RunPod's ToS prohibits hosts from inspecting pod or worker data or analyzing usage patterns, with removal as the penalty. — [RunPod docs overview](https://docs.runpod.io/hosting/overview)
- TensorDock hosts sign a data protection policy and may not contact customers directly. — [TensorDock search snippet](https://marketplace.tensordock.com/host.html)
- Vast's hosting agreement requires client data to be "isolated and protected according to the data protection policy". — [Vast hosting overview](https://docs.vast.ai/documentation/host/hosting-overview)

### Inferences
- Vast's separate host account plus API key model makes a wrapper technically straightforward. The ToS stance on a third party acting as agent for many hosts is unknown.

### Gaps
- Full text of the Vast hosting agreement (the page is a JS console that did not render), Salad's chef ToS, and Clore's host terms on wrappers.
- Referral payout terms for Vast and Clore.

## Q8. KYC, identity, tax, age, country restrictions

### Takeaway
Explicit KYC is confirmed only for RunPod hosts (now closed). Vast pushes compliance onto the payout provider. Salad's tax and age requirements were not found.

### Cited Findings
- RunPod required all hosts to complete KYC. Larger providers also signed a Provider Agreement and SLA. — [RunPod docs hosting (search snippet)](https://docs.runpod.io/hosting)
- Vast requires a fully verified payout account with whatever identity, tax or business verification Wise, PayPal or Stripe requires. Hosts can add a company name and tax ID to invoices. W-9, W-8BEN and 1099 are not mentioned. — [Vast payment docs](https://docs.vast.ai/host/payment.md)
- Salad PayPal redemption is limited to PayPal-supported countries. — [Salad: redeem PayPal](https://support.salad.com/rewards/redeeming-your-rewards/how-to-redeem-paypal)

### Gaps
- Salad's age minimum, W-9/1099 practice, and country list.
- Clore's KYC (likely none for crypto payouts, but unverified).
- Vast's age requirement.

## Q9. Host security model and known risks

### Takeaway
Salad isolates workloads with a Hyper-V/WSL2 Linux VM and containers. Vast runs client Docker containers (plus optional VMs) directly on the host's Ubuntu. TensorDock uses full KVM VMs. Every platform asks hosts to avoid accessing renter data.

### Cited Findings
- Salad nodes are partitioned by a hypervisor, and workloads run in an OCI container on a Linux VM isolated from Windows. Salad claims the protection runs both ways.
- Salad notes residual risks: destination IPs are visible, and reverse DNS may reveal the workload type.
- [Salad security](https://salad.com/security)
- Docker's own docs say WSL2 "provides reduced security isolation" compared with Hyper-V. — [Docker ECI limitations](https://docs.docker.com/enterprise/security/hardened-desktop/enhanced-container-isolation/limitations.md)
- Vast client jobs are Docker images, with Linux VM instances also offered. The host-side isolation model is not documented on the pages read. — [Vast using-your-own-machine](https://docs.vast.ai/host/using-your-own-machine.md); [Vast Linux VMs](https://docs.vast.ai/linux-virtual-machines)
- RunPod runs each pod in its own container in a multi-tenant environment. — [RunPod docs overview](https://docs.runpod.io/hosting/overview)
- TensorDock uses a libvirt/KVM hypervisor with GPU passthrough. — [TensorDock host](https://www.tensordock.com/host.html)
- Spheron warns that workloads should not contain sensitive data or secrets, because consumer nodes are untrusted. — [Spheron Fizz docs](https://docs.spheron.network/core-concepts/fizz)

### Inferences
- Docker-on-host models (Vast, Clore) put the host OS at greater risk from container escapes than VM models (Salad, TensorDock). Abuse-complaint and legal exposure from renters' network traffic out of a residential IP is a risk on all platforms. That last point is my inference; no platform document quantifies it.

### Gaps
- No documented security incidents affecting hosts on any of these platforms were found in this pass.
