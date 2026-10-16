# Storage Networks for Otium (embedding storage nodes on users' PCs, operator collects and credits users) — status as of October 2026

Research date: 2026-10-16. About 17 tool calls. Primary sources are Storj's official docs and terms, Swarm Foundation reports, and Sia docs. Community evidence comes mostly from forum.storj.io. Older data is flagged inline.

## 1. Storj storage node: rates, held amount, requirements, OS, multi-node rules, ToS, earnings, payout and KYC

### Takeaway
Storj is a mature, well-documented network, but it is a poor fit for Otium. The payout rates were cut again on Sep 1, 2026, to $1.35/TB-month for storage and $1.00/TB for egress. Payouts are now USDC on Ethereum L1 only, because zkSync was sunset. The node needs an inbound port and 99.3% uptime. Most decisively, the Node Operator Terms forbid letting nodes "be shared or used by anyone other than Node Operator" and require the operator to "own or control" the device. A Storj-compliant integration would therefore have each user be their own node operator with their own wallet, not Otium operating nodes on users' PCs.

### Cited Findings
**Payout rates (current)**
- As of Sep 1, 2026, for Storj Labs-operated satellites: storage $1.35/TB-month, egress $1.00/TB, audit/repair egress $1.00/TB. Storage is metered in GB-hours. — [Storj docs: Payouts](https://storj.dev/node/payouts)
- The previous rates were $1.50/TB-month storage, $2.00/TB egress and $2.00/TB repair/audit, effective Dec 1, 2023. Before that, egress was $20/TB and repair $10/TB. That makes the 2026 cut the second major reduction. — [WebSearch summary of storj.dev payouts and forum](https://forum.storj.io/t/payout-per-terabyte/10910); [Storj forum "Time to cut operator payments again"](https://forum.storj.io/t/time-to-cut-operator-payments-again/31120)
- A forum post noted that Storj said in Dec 2023 that it did "not anticipate further payout rate adjustments for the foreseeable future". The Sep 2026 table shows they were adjusted anyway. — [Storj forum economic model thread](https://forum.storj.io/t/lets-talk-about-the-elephant-in-the-room-the-storj-economic-model-node-operator-payout-model/20127/844)
- An unverified forum claim says storage is computed in TiB, not TB, which would mean about 10% less pay. — [Storj forum](https://forum.storj.io/t/are-payouts-calculated-by-tib-or-tb/26062)

**Payout currency, zkSync and threshold**
- Payouts are in USDC on Ethereum at 1 USDC per USD. Storj "no longer pays in STORJ tokens". zkSync Layer 2 payouts were sunset on Sep 1, 2026, and opted-in wallets now get L1 payouts when the balance clears the threshold. — [Storj docs: Payouts](https://storj.dev/node/payouts)
- The threshold is set per wallet. No transaction is sent if the fee is more than 25% of the payout, using the average USD fee over the prior 12 hours. The docs' examples are a $12.50 fee giving a $50 threshold and a $17.14 L1 fee giving a $68.56 threshold. Balances below the threshold roll over. — [Storj docs: Payouts](https://storj.dev/node/payouts)
- Supplier Terms §8.1 say fees are paid "in USD Coin". §8.4.1 requires an ERC-20 Ethereum address, and Storj "will not send payments on any other blockchain or network." — [Storj Supplier Terms & Conditions](https://www.storj.io/legal/supplier-terms-conditions)
- Older forum and FAQ material says payouts are in STORJ token and arrive between the 3rd and 15th of the following month. This is superseded for currency (now USDC), but the timing language may still apply. — [SNO Payment mega-FAQ](https://forum.storj.io/t/sno-payment-mega-faq/16228)

**Held amount and vetting**
- Held back: 75% of fees in months 1–3, 50% in months 4–6, 25% in months 7–9, and 100% paid from month 10. 50% of the held amount is released after 15 consecutive months, and the rest is released on Graceful Exit. Forfeited holds are "retained permanently by Company". — [Storj Supplier Terms §8.5](https://www.storj.io/legal/supplier-terms-conditions)
- Disqualification is at Storj's "sole and absolute discretion". It forfeits the held amount and requires a new NodeID. Containment longer than 7 days triggers disqualification. — [Storj Supplier Terms §8.6, §7.2](https://www.storj.io/legal/supplier-terms-conditions)
- Vetting: new nodes on a shared /24 vet proportionally slower because they split the small initial vetting traffic. — [Storj forum: multiple nodes behind same /24](https://forum.storj.io/t/multi-nodes-behind-same-24-when-one-fills-will-the-data-to-the-others-increase/17528)

**Requirements**
- Minimum 500 GB per node process (2 TB recommended). This is lower than the 550 GB figure in the brief. — [Storj docs: Prerequisites](https://storj.dev/node/get-started/prerequisites)
- One CPU core per node process. One dedicated HDD per node with no SMR, and no RAID unless the controller is in passthrough mode. LAN is recommended over Wi-Fi. — [Storj docs: Prerequisites](https://storj.dev/node/get-started/prerequisites)
- Bandwidth minimums: 1.5 TB/month of transit per TB of capacity, 1 Mbps up and 3 Mbps down per TB. Recommended: 3 Mbps up and 5 Mbps down per TB, with unlimited transit. — [Storj docs: Prerequisites](https://storj.dev/node/get-started/prerequisites)
- Uptime: 99.3% per month minimum (at most 5 hours of downtime per month), 99.5% recommended. Also stated in Terms §4.1.4. — [Storj docs: Prerequisites](https://storj.dev/node/get-started/prerequisites); [Supplier Terms](https://www.storj.io/legal/supplier-terms-conditions)
- OS: Linux is preferred (Debian 9+, Ubuntu 18.04+, Fedora 28+, CentOS). Windows and macOS are listed without detail. The Windows GUI installer and Docker image are the common install paths. — [Storj docs: Prerequisites](https://storj.dev/node/get-started/prerequisites)

**Port forwarding and NAT**
- Inbound port forwarding (default 28967 TCP+UDP) is required. A forum thread says there is "no way to skip port forwarding" behind NAT. IPv6 is only a partial alternative because not all satellites support it. CGNAT users need a public IP from the ISP, a VPN with port forwarding (PIA, AirVPN), or a WireGuard tunnel to a VPS. Cloudflared TCP tunnels reportedly do not work. — [Storj forum: CGNAT](https://forum.storj.io/t/configuring-node-behind-cg-nat/24774); [Cloudflared tunnel problems](https://forum.storj.io/t/problems-accessing-node-via-tcp-tunnel-cloudflared/27617); [portmap.io tutorial](https://forum.storj.io/t/tutorial-tunneling-through-cgnat-with-portmap-io-ssh/14512); [WireGuard DNAT blog](https://blog.arrogantrabbit.com/vpn/net/Wireguard-DNAT-and-IPTABLES/)

**Multiple nodes and IP/subnet rules**
- Terms §5.1 prohibit operating more than one node using different payment addresses for each node, and prohibit running more than one node behind the same IP. As summarized from the terms page, this reads as one wallet across your nodes. — [Storj Supplier Terms §5.1](https://www.storj.io/legal/supplier-terms-conditions)
- All nodes behind one /24 are treated as a single node for uploads and downloads, at most one piece per /24 per segment. They are counted separately for audits and uptime. A Storj team member stated that bypassing the /24 rule is against the Terms. — [Storj forum: Enterprise SNO / /24 discussion](https://forum.storj.io/t/enterprise-sno-members/9211); [Ingress with multiple nodes on same IP](https://forum.storj.io/t/ingress-with-multiple-nodes-on-the-same-ip/7953)

**ToS on third parties running nodes on others' machines (critical for Otium)**
- §6.1: "You own or control your Device(s), and have the right to install the Storage Node Software." — [Storj Supplier Terms](https://www.storj.io/legal/supplier-terms-conditions)
- §5.1.1(e) bars allowing "Storage Nodes to be shared or used by anyone other than Node Operator". §5.1.1(a) bars renting, reselling or timesharing the Storage Service. — [Storj Supplier Terms](https://www.storj.io/legal/supplier-terms-conditions)
- §4.1.3 forbids modifying the node software. §4.1.1 requires compliance with your ISP agreement. — [Storj Supplier Terms](https://www.storj.io/legal/supplier-terms-conditions)

**KYC and eligibility**
- No KYC or identity-verification process is described. The operator must be 18 or older, not on U.S. denied-party lists, and not in embargoed regions (§15). W-8 is required before payment for certain payees (§8.4.3), Storj may request "all tax forms required by Company" (§8.4.2), and Storj may withhold taxes (§8.8). — [Storj Supplier Terms](https://www.storj.io/legal/supplier-terms-conditions)

**Realistic earnings (mostly pre-Sep-2026 rates, so treat them as optimistic now)**
- Two home nodes with 6.5 TB stored: about $5.30 per node per month. — [Storj forum "Share your monthly payout"](https://forum.storj.io/t/share-your-monthly-payout/30642)
- Six drives with about 8 TB stored: $10.63/month (estimate). — [Storj forum 3-month test](https://forum.storj.io/t/is-earning-with-storj-worth-it-my-3-month-test-results-and-insights/28720)
- Raspberry Pi with an 18 TB drive: about $18/month, down from about $25 after the earlier cut. — [Storj forum](https://forum.storj.io/t/is-earning-with-storj-worth-it-my-3-month-test-results-and-insights/28720?page=2)
- One operator said a 1-year-old 800 GB node earned about $7/TB. A European operator reported inconsistent payouts versus the stated rates. — [Storj forum payout per TB](https://forum.storj.io/t/payout-per-terabyte/10910)
- The gamefi-roi tracker models a negative net ROI for a Storj node on existing hardware, but its data is marked stale. — [gamefi-roi Storj page](https://gamefi-roi-web.onrender.com/opportunities/storj-storage-node)
- Drive power alone was estimated at about €3/month. — [Storj forum](https://forum.storj.io/t/is-earning-with-storj-worth-it-my-3-month-test-results-and-insights/28720)

### Inferences
- At the new rates, a typical home node holding 1–3 TB after a year might earn about $1.35–$4/month from storage, plus small egress. Nodes fill slowly (months to years), so the first-year gross is likely only $0–$3/month, and 75%/50%/25% of that is held back in months 1–9. This is my calculation from the cited rates and fill reports, not a measured 2026 figure.
- The L1-only USDC payout with a roughly $50–70 threshold means a small node may go many months or years without an on-chain payout. Per-user wallets would almost never cross the threshold. If Otium pooled payouts to one wallet, that pooling is what the Terms appear to forbid (one operator's nodes behind different IPs is allowed, but nodes shared with or used by others is not).
- Otium operating nodes on users' devices conflicts with §6.1 (Otium does not own or control the device) and §5.1.1(e). The compliant model is a guided "become your own Storj operator" flow where the user owns the identity and wallet. Otium could not take a cut of earnings or credit them, but could still show the user's own earnings by reading their node's local dashboard API.
- A 99.3% uptime requirement is unrealistic for typical desktops and laptops that sleep or shut down. Repeated downtime leads to suspension or disqualification and forfeiture of the held amount.
- Headless automation is technically feasible: the Docker image or Linux binary takes config flags, and the node exposes a local web dashboard and API (default port 14002) with per-node earnings estimates. This comes from my background knowledge; I did not verify it with a source in this session.

### Gaps
- No explicit official statement found on a third-party company running nodes on its users' machines beyond the general clauses cited. A written ruling from Storj would be needed.
- No 2026 earnings reports after the Sep 1, 2026 cut were found. storjnet.info was not fetched.
- Current vetting length (historically about 100 audits per satellite, roughly 1–2 months) was not re-verified from an official 2026 source.
- macOS support status: the docs list macOS with no detail. Historically there is no native macOS installer (Docker only). Not verified.
- The 550 GB versus 500 GB minimum discrepancy: the docs and Terms both say 500 GB.

## 2. Sia hostd: status, earnings, requirements, collateral, automation

### Takeaway
Sia is alive (hostd v0.40.0, v2 hard fork in June 2025, about 525 active hosts). Demand is thin, though: about 2.08 PB used out of 7.30 PB advertised, roughly 30% utilization. Hosts must lock their own Siacoin as collateral (default 2x the storage price) and forward ports. Sia is a capital-at-risk, always-online host business, not a consumer idle-disk app.

### Cited Findings
- hostd v0.40.0 is the current downloadable host daemon, with a 4 TB HDD recommended for stored data. — [sia.tech/wallet (downloads)](https://sia.tech/wallet)
- Network as of early Sep 2026: about 2.08 PB used versus about 7.30 PB advertised, about 525 active hosts, and HostScore utilization in the low 30% range. — [yellow.com Siacoin page](https://yellow.com/asset/siacoin)
- The June 2025 v2 hard fork changed the renter-host protocol. 2026 releases added S3-compatible tooling. — [securities.io](https://www.securities.io/investing-in-siacoin-sc-everything-you-need-to-know/)
- OS support: Linux (apt package or binary, "recommended for a dedicated host"), Docker, macOS via Homebrew, and a Windows desktop app. hostd "earns Siacoin (SC)". — [Sia docs: Setting up hostd](https://docs.sia.tech/provide-storage/setting-up-hostd)
- Collateral is the SC a host locks per TB per month of storage, with a default multiplier of 2 (2x the storage price) and a configurable max collateral per contract. Hosts need SC to form contracts, post collateral, and pay for storage proofs. — [Sia docs: Configuring your host](https://docs.sia.tech/provide-storage/configuring-your-host); [legacy siad Linux guide](https://docs.sia.tech/legacy/hosting/siad-linux)
- Ports: 9981/TCP (consensus) and 9984/TCP (RHP4/SiaMux) to forward in current docs. Older guides say 9981–9984. Never expose API port 9980. — [Sia docs: Configuring your host](https://docs.sia.tech/provide-storage/configuring-your-host); [Sia UI legacy guide](https://docs.sia.tech/legacy/hosting/sia-ui)
- An IP change requires re-announcing the host. — [Sia UI legacy guide](https://docs.sia.tech/legacy/hosting/sia-ui)
- Sia's hosting best practices: hosts are "entirely responsible" for their systems, should not access renter data beyond what is needed, and should route court orders to legal counsel. The Foundation is not a party to contracts. — [Sia hosting best practices](https://sia.tech/hosting-best-practices)

### Inferences
- Collateral means the host takes on financial risk: SC is burned if contracts fail, which happens when a PC goes offline during proof windows. Otium would have to pre-fund SC per user node and absorb the losses.
- hostd has a full HTTP API (the Go package and Docker template suggest it), so automation is feasible. Otium would still be operating a hosting business with pricing, collateral and announcement management for each user.
- About 70% of advertised capacity is idle, so a new small host behind a residential IP will win few contracts.

### Gaps
- No 2026 per-host earnings data found. HostScore and Siascan were not fetched directly.
- hostd UPnP support is unconfirmed. Legacy siad promised UPnP "in future versions", so assume manual port forwarding.
- No hostd terms found on third-party or white-label operation. The Foundation positions itself as not party to contracts, so ToS risk appears lower than Storj's, but this is unverified.

## 3. Other networks: Swarm (Bee), Filecoin/Saturn, Arweave/AO, Internxt, Akash, Bittensor, Theta EdgeStore, Crust, BTFS — alive and consumer-feasible?

### Takeaway
None is a clean consumer idle-disk earner in October 2026. Swarm is alive, but rewards are falling, it needs a non-refundable 10 xBZZ stake per node and an SSD, and payouts are in a volatile token. Filecoin Saturn, the lightweight role, shut down in 2024 in favor of Storacha. The Internxt node app was archived in 2022. Bittensor storage subnets (for example Hippius, SN75) are datacenter-grade mining. I found no current, reliable 2026 data for Crust, BTFS, Theta EdgeStore, Arweave/AO or Akash storage.

### Cited Findings
**Swarm (Bee)**
- April 2026 network rewards were 12,265 xBZZ (March: 16,450; Nov 2025: 39,109). Average earnings per active staking node were 6.78 xBZZ/month (March: 9.08; Nov 2025: 19.40). About 1,808 active staking nodes and 4,098 reachable full nodes. — [Swarm State of the Network, April 2026](https://blog.ethswarm.org/foundation/2026/swarm-state-of-the-network-april-2026)
- Bee v2.8.0 (May 2026) was a mandatory security release, followed by v2.8.2 security hardening. — [Bee 2.8.2 release](https://blog.ethswarm.org/foundation/2026/bee-2-8-2-release/); [May 2026 dev update](https://blog.ethswarm.org/foundation/2026/monthly-development-update-may-2026/)
- Earning storage incentives requires a 10 xBZZ stake plus about 0.1 xDAI for gas on Gnosis Chain. Staked BZZ is non-refundable. A SWAP chequebook transaction is needed for light and full nodes. — [Gnosis docs: Swarm quickstart](https://docs.gnosischain.com/node/participate-validator/swarm/a-quickstart-swarm); [bee-js staking docs](https://bee-js.ethswarm.org/docs/staking/); [Swarm shell install](https://docs.ethswarm.org/docs/bee/installation/shell-script-install)
- Full-node hardware: 2 GHz dual-core, 8 GB RAM, 30 GB SSD (HDD not recommended). Test performance with /rchash before staking. — [Swarm Getting Started](https://docs.ethswarm.org/docs/bee/installation/getting-started)
- P2P port 1634 must be forwarded. The FAQ advises against UPnP for security reasons. The `isReachable` field in the API shows status. API port 1633 should be bound to localhost. — [Bee FAQ](https://docs.ethswarm.org/docs/bee/bee-faq); [Bee API](https://docs.ethswarm.org/docs/bee/working-with-bee/bee-api)
- More nodes in a neighborhood lowers each node's chance of winning rewards. — [Gnosis Dappnode guide](https://docs.gnosischain.com/node/participate-validator/swarm/c-dappnode-swarm)

**Filecoin / Saturn**
- The Saturn team "transitioned its focus" on June 1, 2024, toward fast storage and retrievals, and promotes Storacha. No lightweight paid retrieval-node role for consumers was found in 2026. — [saturn.tech](https://saturn.tech/); [Filecoin docs: Saturn](https://docs.filecoin.io/basics/how-retrieval-works/saturn)

**Internxt**
- The Internxt X Core desktop app, which paid users for sharing spare disk, was archived May 18, 2022. The current Internxt CLI is for users' own files, not hosting. — [GitHub internxt/core-gui](https://github.com/internxt/core-gui); [Internxt CLI](https://github.com/internxt/cli)

**Bittensor**
- Storage subnets exist in 2026, for example Hippius on SN75 (S3-compatible and IPFS-based). About 128 active subnets as of April 2026. — [CoinGecko: top Bittensor subnets](https://www.coingecko.com/learn/top-bittensor-subnets-dtao); [Tao.media 2026 guide](https://www.tao.media/the-ultimate-guide-to-bittensor-2026/)
- Subnet mining involves registration costs and competitive hardware. Some sources are investment-promotional. — [misar.blog subnet mining 2026](https://www.misar.blog/@synor/articles/bittensor-subnet-mining)

**BTFS, Crust, Theta EdgeStore**
- BTFS still has an active repo, and BitTorrent announced a local node dashboard at localhost:5001/dashboard (Jan 2026 X post). No 2026 earnings data was found. — [go-btfs GitHub](https://github.com/bittorrent/go-btfs); [BitTorrent on X](https://x.com/BitTorrent/status/2011731834808213859)
- Crust: only the member-node wiki setup guide was found, with no 2026 earnings or activity data. — [Crust Wiki: Member Node](https://wiki.crust.network/docs/en/memberNode)
- Theta EdgeStore: searches returned only unrelated projects named "EdgeStore". No 2026 status was found.

### Inferences
- Swarm: 6.78 xBZZ/month per staking node (April 2026) works out to only a few dollars a month at typical xBZZ prices (fiat price not sourced). Getting there requires a sunk 10 xBZZ stake, an always-on SSD, and a forwarded port, so net ROI for an average home PC is likely near zero or negative. Bee has a full HTTP API and a wallet per node, so automation is technically possible, but the stake per node is capital Otium would have to front.
- Arweave/AO and Akash storage target server-class providers with staking or collateral. They are not consumer idle-disk products (background knowledge, not sourced here).

### Gaps
- Swarm May–Oct 2026 reward figures were not retrieved.
- No reliable 2026 sources for Crust, BTFS earnings, Theta EdgeStore, Arweave/AO mining economics, Akash storage providers, or "Eden". These should be treated as unverified or likely unsuitable.
- Bittensor storage subnet miner requirements and economics were not verified from primary subnet docs.

## 4. NAT traversal: which work behind home routers without port forwarding?

### Takeaway
None of the paying storage networks researched work well without inbound reachability. Storj, Sia hostd and Swarm full nodes all need a forwarded port. UPnP is not officially supported or recommended: Swarm advises against it, and Sia's UPnP support is unconfirmed. CGNAT users need tunnels or a VPS, which adds latency and cost.

### Cited Findings
- Storj: "no way to skip port forwarding" behind NAT. IPv6 is only partially supported by satellites. CGNAT workarounds are a VPN with port forwarding, a VPS with WireGuard, or portmap.io. Cloudflared TCP tunnels fail. — [Storj forum CGNAT](https://forum.storj.io/t/configuring-node-behind-cg-nat/24774); [Cloudflared thread](https://forum.storj.io/t/problems-accessing-node-via-tcp-tunnel-cloudflared/27617)
- Swarm: forward port 1634, UPnP not recommended. — [Bee FAQ](https://docs.ethswarm.org/docs/bee/bee-faq)
- Sia: forward 9981 and 9984. Legacy siad said UPnP was a future feature. — [Sia docs: Configuring your host](https://docs.sia.tech/provide-storage/configuring-your-host); [Sia UI legacy](https://docs.sia.tech/legacy/hosting/sia-ui)

### Inferences
- An Otium storage integration would only work for the subset of users who can open ports, which means non-CGNAT users willing to touch router settings. That is likely a minority of a consumer install base. Relaying through Otium's own VPS would add bandwidth cost that exceeds the per-TB revenue ($1/TB egress on Storj).

### Gaps
- The share of consumer connections behind CGNAT in 2026 was not researched.
- Whether Storj's node supports UPnP auto-mapping was not verified from official docs.

## 5. Headless automation and per-node earnings attribution

### Takeaway
All three main candidates (Storj, Sia hostd, Swarm Bee) can run headlessly with a Linux binary or Docker, config files or flags, and a local HTTP API. The local API also exposes per-node earnings, so attribution is easy technically. The blockers are ToS, payout mechanics and economics, not automation.

### Cited Findings
- Bee: shell-script and Docker installs, HTTP API on 1633 (`/peers`, `isReachable`, staking endpoints), and bee-js SDK staking APIs. — [Swarm shell install](https://docs.ethswarm.org/docs/bee/installation/shell-script-install); [Bee API](https://docs.ethswarm.org/docs/bee/working-with-bee/bee-api); [bee-js staking](https://bee-js.ethswarm.org/docs/staking/)
- hostd: Linux binary or apt, Docker template, a Windows desktop app, macOS Homebrew, and a Go package with an API. — [Sia docs: Setting up hostd](https://docs.sia.tech/provide-storage/setting-up-hostd); [hostd Go pkg](https://beta.pkg.go.dev/go.sia.tech/hostd); [hostd Docker](https://docs.sia.tech/hosting/setting-up-hostd/docker)
- Storj: Docker and Linux binary, and the Windows installer (from the prerequisites OS list). — [Storj docs: Prerequisites](https://storj.dev/node/get-started/prerequisites)

### Inferences
- A .NET Avalonia app could supervise a sidecar binary on each OS and poll localhost APIs for earnings. Each node's identity and wallet would be per-user, though. Under Storj Terms Otium cannot be the operator of record, and under Swarm and Sia each node needs its own funded wallet or stake.

### Gaps
- Storj node dashboard API endpoints (port 14002, `/api/sno/estimated-payout`) are from background knowledge only and were not verified in this session.

## 6. Risks: disk wear, bandwidth caps, legal liability, AV flags

### Takeaway
The risks are significant relative to the cents-to-dollars of monthly revenue: constant HDD I/O and power, bandwidth consumption that can exceed ISP caps (Storj budgets 1.5 TB/month of transit per TB), ToS disqualification with forfeited holds, and storage of encrypted third-party data the user cannot inspect.

### Cited Findings
- Storj transit budget: 1.5 TB/month per TB of capacity. — [Storj docs: Prerequisites](https://storj.dev/node/get-started/prerequisites)
- Users must comply with ISP agreements (Storj Terms §4.1.1). One forum thread title reports an ISP cutting service ("ISP pulls the plug"). — [Storj Supplier Terms](https://www.storj.io/legal/supplier-terms-conditions); [Storj forum "ISP pulls the plug"](https://forum.storj.io/t/isp-pulls-the-plug/14382)
- HDD power cost of about €3/month was cited by an operator. — [Storj forum](https://forum.storj.io/t/is-earning-with-storj-worth-it-my-3-month-test-results-and-insights/28720)
- Sia: hosts should not access renter data and should send legal requests to counsel. The Foundation will not act for hosts. — [Sia hosting best practices](https://sia.tech/hosting-best-practices)
- Storj disqualification forfeits the held amount. — [Storj Supplier Terms §8.6](https://www.storj.io/legal/supplier-terms-conditions)

### Inferences
- Content liability: Storj, Sia and Swarm pieces are encrypted or erasure-coded, which lowers the practical risk. Hosting obligations and takedown handling would still sit with the user, or with Otium if Otium is the operator.
- Antivirus: no source was found on AV flags for storagenode, hostd or bee binaries. A bundled crypto-paying daemon that opens inbound ports is likely to draw heuristics or user suspicion (speculative).
- Overall recommendation for the report: do not embed storage nodes as an Otium-credited earner. At most, offer an optional link or guide for advanced users to run their own Storj or Sia node with their own wallet. Storj Terms explicitly forbid Otium-operated nodes on users' devices. The others need per-node crypto stakes or collateral and port forwarding, for a few dollars a month at best.

### Gaps
- No sources found on AV or SmartScreen detections for these node binaries.
- No legal analysis found on the liability of an intermediary company operating storage nodes on consumer devices.
