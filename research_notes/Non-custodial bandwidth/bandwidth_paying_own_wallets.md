# Bandwidth / DePIN networks that pay each user's own wallet (as of 17 Oct 2026)

The test: a desktop app (Windows first) onboards a user automatically, with no vendor account, email or KYC, through a headless node, CLI or SDK. The network then pays liquid crypto straight to the user's own address, with no manual claim. Points and airdrops count as zero. No software was downloaded or run, and nothing was signed up for.

## Verdict
Only Mysterium fully fits, and only if Otium pays each node's registration. That costs about $0.01 per node today. Sentinel passes on mechanics: no deposit, and it pays the node's own key every hour. In practice a new home node earns roughly nothing, it is Linux-only and it needs a public IP. Everything else fails on at least one hard rule: it needs a vendor account, a stake or lock, hardware, or a manual claim, or it pays points.

## Ranked shortlist

### 1. Mysterium (myst node, beneficiary = user's Polygon address) with an Otium-sponsored registration
- Registration costs 0.1297 MYST (from `GET /transactor/fees` on a real node). It is free only with a referral token or when the node is "eligible". Otherwise the fee comes from MYST sent to the identity's channel address. The beneficiary is set at registration, so earnings settle to the user's address. Source: Otium spike, docs/non-custodial-plan.md, line 76.
- MYST is $0.0788, 24h volume about $14k, market cap $2.56M ([CoinGecko](https://www.coingecko.com/en/coins/mysterium), 17 Oct 2026). Registration is therefore about **$0.010 per node**, plus Polygon gas. 1,000 users cost about $10.
- Earnings settle automatically at 5 MYST, with a 20% network fee plus gas ([Mysterium FAQ, via prior notes](https://docs.mysterium.network/faq); [MystNodes help](https://help.mystnodes.com/en/articles/8005144-where-do-i-check-and-withdraw-my-earnings)). 5 MYST is about $0.39, so payouts are frequent. The vendor estimates $8-25/month for an always-on node. An independent estimate is about $1.20/month ([MystNodes blog](https://www.mystnodes.com/blog/how-much-can-you-earn-sharing-bandwidth); [DePIN Scan](https://depinscan.io/projects/mysterium)).
- Legal: use B2B-only service types. Mysterium itself advises against Public traffic in the US, UK, CA, DE, IT, AU and IN ([Node UI management](https://help.mystnodes.com/en/articles/8006125-node-ui-management)).
- Is Otium still non-custodial? Yes. Otium gives each node about 1 cent of MYST and never receives or holds user earnings. The other route is to ask Mysterium for referral tokens, which make registration free.

### 2. Sentinel dVPN node (`sentinel-dvpnx`): fits on mechanics, fails on earnings and platform
- Deposit: the live chain param is **`deposit: 0 udvpn`** ([LCD /sentinel/node/v3/params](https://api.sentinel.mathnodes.com/sentinel/node/v3/params)). The node only needs gas. Docs recommend "at least 50 P2P in your node account" for the periodic on-chain status transactions ([Sentinel setup](https://docs.sentinel.co/dvpn-nodes/setup)). At $0.0000755 per P2P, that is about **$0.004**.
- Payouts are "paid out every hour". 80% goes to the node host and 20% to P2P stakers ([Sentinel earnings](https://docs.sentinel.co/dvpn-nodes/earnings); [subscription params staking_share 0.2](https://api.sentinel.mathnodes.com/sentinel/subscription/v3/params)). Earnings go to the node's own key on the PC. The app would sweep them to the user's address, or show the key to the user.
- Earnings: network revenue was 59.09M P2P over 30 days, about $4.5k, across 2,947 active nodes. That averages about **$1.5 per node per month**, but **99.7% is lease payments** from plan owners who pick the nodes. Only 61 nodes (2.1%) are residential ([SentNodes](https://sentnodes.com/), 17 Oct 2026). A new unleased home node gets session payments only, which is close to zero. Agents can pay nodes directly at a median of about 40 P2P/GB ([x402.sentinel.co](https://x402.sentinel.co/)).
- Platform: "Operative System: Linux (preferably Debian or Ubuntu)", and a "Public IP address required" ([requirements](https://docs.sentinel.co/dvpn-nodes/setup/requirements)). Windows would need WSL2 or Docker plus port forwarding, and CGNAT users are excluded.
- Liquidity: 24h volume about **$8.9k** (KuCoin 47%, Osmosis) ([CoinGecko P2P](https://www.coingecko.com/en/coins/sentinel)).
- Legal: this is an anonymous VPN exit with no B2B-only mode, which makes it riskier than Mysterium's B2B mode.

## Near-misses (one blocker each)
| Network | Blocker |
|---|---|
| Anyone Protocol relay | Needs a 100 ANYONE lock per relay for 180 days (about $18; [lock requirement](https://docs.anyone.io/tokenomics/relay-rewards/lock-requirement), [CoinGecko](https://www.coingecko.com/en/coins/anyone-protocol)). The wallet goes in `anonrc`, but rewards are claimed manually in a dashboard ([register](https://docs.anyone.io/dashboard/register)). There is no CGNAT workaround ([FAQ](https://docs.anyone.io/welcome/faq)). |
| Orchid (OXT) | Providers must stake OXT, and only "a curated list of verified providers" gets traffic ([orchid.com/how](https://orchid.com/how/)). |
| Bitping | Requires an account ("be sure to have an account created"; `bitpingd login` with email and password) ([Bitping](https://bitping.com/help/nodes/installing-the-node)). It pays SOL ([blog](https://bitping.com/blog/bitpings-solana-transition)). |
| Traffmonetizer | Account token from a dashboard. Pays USDT-TRC20 with a $10 minimum, on request ([traffmonetizer.com](https://traffmonetizer.com/)). |
| Repocket / EarnFM / ProxyLite / PacketShare | Each needs an account plus an API key or token, and payout is on request ([Repocket](https://raw.githubusercontent.com/GeiserX/CashPilot/HEAD/docs/guides/repocket.md), [EarnFM Docker](https://hub.docker.com/r/earnfm/earnfm-client), [ProxyLite](https://raw.githubusercontent.com/GeiserX/CashPilot/HEAD/docs/guides/proxylite.md), [PacketShare: PayPal or wire, $10 minimum](https://alternativeto.net/software/packetshare/about)). Spide: no reliable 2026 data, $20 threshold in 2024 ([Proxyway](https://proxyway.com/?p=11484)). |
| Grass (Wynd) | Account required. Season 2 was a one-off of about $3M USDC for 20 months, with a manual claim ([hokanews, Jul 2026](https://www.hokanews.com/2026/07/grass-airdrop-season-2-3m-usdc-claims.html)). |
| Nodepay / Dawn / Teneo / Gradient | Points-only, or the token is not live ([Dawn](https://blog.mexc.com/id/news/dawn-network-dawn-airdrop-guide-2026-complete-node-setup-points-system-mid-2026-tge-timeline/), [Teneo](https://usethebitcoin.com/airdrop/teneo-protocol-airdrop/), [Gradient](https://zipmex.com/gradient-airdrop/), [Nodepay](https://www.kucoin.com/airdrop/nodecoin)). |
| Titan | Account plus device ID, and in March 2026 the dashboard could not generate new device IDs ([CashPilot Titan](https://raw.githubusercontent.com/GeiserX/CashPilot/HEAD/docs/guides/titan.md)). |
| UpRock | Email sign-up and an in-app wallet ([depinhub](https://depinhub.io/academy/depin-revolution/day-3/uprock-turn-spare-bandwidth-into-big-benefits-with-the-best-rewards-program/)). |
| Theta Edge Node | Needs at least 16 GB RAM, ROI is "not measurable", and meaningful jobs need an Elite stake of 10k+ TFUEL ([gamefi-roi](https://gamefi-roi-web.onrender.com/opportunities/theta-edge-node), [stake guide](https://jantheta.notion.site/Stake-Unstake-TFUEL-upon-request-6f9f8a302a8147ca87759d2c904adb75)). TFUEL volume is about $229k ([CoinGecko](https://www.coingecko.com/en/coins/theta-fuel)). |
| Presearch | Registration code from an account dashboard, plus a PRE stake ([setup](https://docs.presearch.io/nodes/setup)). It is search, not bandwidth. |
| Nym | Bond required, and the docs lean towards a dedicated VPS ([bonding](https://nym.com/docs/operators/nodes/nym-node/bonding)). |
| Deeper / NATIX / Andrena | Need hardware: Deeper Connect ([depinhub](https://depinhub.io/projects/deeper/)), a camera or car app ([NATIX](https://tynmagazine.com/natix-network-expands-depin-ecosystem-with-vx360-and-drive-app-growth/)), or rooftop radios (Andrena). |
| Meson / GaGaNode, Koii | No 2026 evidence of account-free, wallet-direct payouts ([depinhub Meson](https://depinhub.io/projects/meson), [Koii](https://depinhub.io/projects/koii)). Unverified. |
| Salad | Pays Salad Balance to an account, not crypto to a wallet ([Salad support](https://support.salad.com/article/356-how-does-my-machine-earn-salad-balance)). |
| Pawns.app, Peer2Profit | Pawns moved to a GPT/tasks model, and Peer2Profit announced it was discontinuing (user report: [Trustpilot](https://se.trustpilot.com/review/peer2profit.io)). Unverified. |

## Compromises if Mysterium sponsorship is rejected
1. Partner SDKs (MystNodes SDK client, Honeygain and similar) pay the publisher, so Otium would hold and redistribute money. That makes Otium custodial.
2. Generate a local wallet key on the user's PC (Sentinel, Theta, Anyone) and sweep it to the user's address. This is still non-custodial, but adds key-management UX. For Anyone, the claim step stays manual.
