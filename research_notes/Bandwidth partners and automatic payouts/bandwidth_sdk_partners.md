# Bandwidth-sharing SDK and publisher programs for a desktop operator (as of 16 Oct 2026)

Scope note: Honeygain SDK, Massive, Mysterium, Bright SDK, Pawns SDK, Castar, PacketSDK, IPIDEA and NetNut/Popa were covered in earlier research. Below they appear only where 2026 sources show something new. The search budget was limited to about 17 queries and fetches. Several candidates (Traffmonetizer, Repocket, PacketStream, Earn.fm, ProxyRack/Peer2Profit, SOAX, Oxylabs, Live Proxies, Rayobyte, Proxy-Cheap) had no primary SDK or publisher page I could find, and they are marked that way below. Vendor figures are marketing ("up to") unless stated otherwise.

## Which other publisher/SDK programs exist and are active in 2026, and how do they compare on the integration criteria?

### Takeaway
Two new programs have current public SDK pages that list Windows and macOS: Decodo (formerly Smartproxy) and Infatica. Both pay per active user per month and both require a sales contact. Neither lists Linux. Neither publishes a reporting API, payout method or minimum. Pawns/IPRoyal's SDK page now lists Linux and a flat $0.20/GB, which changes the earlier picture. For most other names on the candidate list (Traffmonetizer, Repocket, PacketStream, Earn.fm, ProxyRack/Peer2Profit, SOAX, Oxylabs, Live Proxies, Rayobyte, Proxy-Cheap), I found no developer-facing SDK program. They are consumer "earn" apps or proxy sellers, not embeddable publisher programs.

### Cited Findings

**Decodo SDK (formerly Smartproxy)**
- Platforms are Windows, macOS, Android and iOS. Linux is not listed. — [Decodo monetize page](https://decodo.com/monetize)
- Payment is per active user, with no per-GB model mentioned. The page contradicts itself on the rate: the hero text says "up to $0.10" per active user, and the how-it-works step says "up to $0.20/active user per month." — [Decodo](https://decodo.com/monetize)
- Advertised annual tiers ("up to"): 1K DAU $2,400, 1.5K DAU $3,600, 7.5K DAU $18,000, 15K DAU $36,000. These work out to $0.20 per DAU per month. — [Decodo](https://decodo.com/monetize)
- Earnings are paid monthly. The page does not state the payout method, minimum, or a reporting dashboard or API. — [Decodo](https://decodo.com/monetize)
- Onboarding is sales and support-led. You submit the app through a form, get personalised setup, send a test build, and then confirm consent, compensation and opt-out mechanisms with Decodo before publishing. — [Decodo](https://decodo.com/monetize)
- Consent rules: users must be fully informed, give clear and explicit consent to join the residential IP network, be compensated, and be able to disable the SDK easily at any time. Otium's opt-in, paid model fits this as written. — [Decodo](https://decodo.com/monetize)
- Size threshold: the program is described as "ideal for 15K+ active users/month," but apps with about 1K DAU "can also integrate." — [Decodo](https://decodo.com/monetize)

**Infatica SDK**
- Platforms are Windows and macOS software and games, plus Android and iOS. Linux is not mentioned. — [infatica-sdk.io](https://infatica-sdk.io/)
- Payment is per active user per month, "up to 60 cents per user." No per-GB model is mentioned. — [infatica-sdk.io](https://infatica-sdk.io/)
- Minimum size is "50,000+ monthly active users." Onboarding is a partnership form followed by "tailored partnership options," with no self-serve option. — [infatica-sdk.io](https://infatica-sdk.io/)
- Consent: developers must update the EULA to tell users they join the Infatica P2B network. No other UI requirement is described. — [infatica-sdk.io](https://infatica-sdk.io/)
- Payout method, schedule, minimum, and reporting dashboard or API are not stated. Infatica claims ISO 27001/27701/22301/20000-1 certification and a Bitdefender partnership for filtering malicious content. It makes no AppEsteem claim. — [infatica-sdk.io](https://infatica-sdk.io/)
- Proxyway (Dec 2025) says the Infatica SDK is not openly distributed (you have to apply), that rates are usage-based and vary by volume and region, and that there is "little detail" on dashboards or APIs. — [Proxyway, 16 Dec 2025](https://proxyway.com/research/internet-sharing-sdks-app-monetization)
- Proxyway's Proxy Market Research 2026 says Infatica openly acknowledges sourcing residential IPs through an SDK. — [Proxyway 2026](https://proxyway.com/research/proxy-market-research-2026)

**Pawns SDK (IPRoyal): change since the earlier research**
- The SDK page now says it is "available on Android, iOS, Windows, macOS, and Linux," described as "subject to eligibility and approval." — [business.pawns.app/sdk](https://business.pawns.app/sdk/)
- Payment is a fixed $0.20 per GB of shared traffic. The headline also says "up to $200 CPM," which the page does not reconcile with the per-GB rate. — [Pawns SDK](https://business.pawns.app/sdk/)
- Onboarding is sales-led ("Schedule a quick meet"), with a reply promised within 3 business days. The form asks for MAU in buckets (1k-10k up to 1M+). — [Pawns SDK](https://business.pawns.app/sdk/)
- Reporting is a partner dashboard with "real-time insights on your app revenue." Per-device stats and an API are not mentioned. You can add or remove IPs at any time with no minimum or maximum. — [Pawns SDK](https://business.pawns.app/sdk/)
- The current SDK page says nothing about restrictions on idle or background devices. The earlier research's finding that Pawns bans idle-device traffic could not be re-confirmed on this page, but that does not show the restriction was lifted. — [Pawns SDK](https://business.pawns.app/sdk/)

**Honeygain SDK and Bright SDK: third-party rate data (Dec 2025)**
- Proxyway lists Honeygain SDK platforms as Android, Windows, macOS, iOS, FireOS, LG WebOS, Flutter, Tizen and Unity, without Linux. It estimates $100–$400 per 1,000 DAU per month in top regions. The SDK is self-service with documentation after registration. Metrics cover opt-in rates, bandwidth, session duration and "estimated earnings per user." — [Proxyway, Dec 2025](https://proxyway.com/research/internet-sharing-sdks-app-monetization)
- Proxyway puts Bright SDK at a flat rate per opted-in user, $100–$350 per 1,000 IPs per month. It notes limited self-service analytics, with reporting "through direct partner communication," and onboarding through a partnership or enterprise agreement with KYC. — [Proxyway, Dec 2025](https://proxyway.com/research/internet-sharing-sdks-app-monetization)
- Proxyway also gives general industry ranges of $1.50–$6.00 per 1,000 DAU, or $10–$70 per 1,000 unique IPs per month. These are an order of magnitude below the vendor figures, so the "per 1,000" units in that article may be inconsistent. — [Proxyway, Dec 2025](https://proxyway.com/research/internet-sharing-sdks-app-monetization)

**Candidates with no verifiable SDK or publisher program found**
- Traffmonetizer: I found only consumer-app material. Third-party figures put earnings at about $0.06/GB or about $2 per node per IP per month. One review says payouts are now TRC20 USDT only, with wire for $1,000 and up. Trustpilot shows complaints about delayed or unapproved payouts. — [paidfromsurveys review](https://paidfromsurveys.com/traffmonetizer-review); [Trustpilot](https://au.trustpilot.com/review/traffmonetizer.com); [savingadvice forum](https://www.savingadvice.com/forums/forum/financial-chit-chat/ways-to-make-money/734114-my-traffmonetizer-review-low-payout-not-worth-the-install-time)
- Repocket: I found no SDK or partner program. The only result was a consumer guide giving a $20 minimum payout. — [simeononsecurity](https://simeononsecurity.com/other/install-repocket-easily-using-docker/)
- PacketStream: I found no developer SDK documentation. The consumer program pays by PayPal with a $5 minimum, on request. Login is CAPTCHA-protected, which blocks automated collection of earnings. "PacketSDK" is a different brand and is IPIDEA-linked (see the enforcement section). — [CashPilot PacketStream guide](https://raw.githubusercontent.com/GeiserX/CashPilot/HEAD/docs/guides/packetstream.md)
- Earn.fm: I found no developer or SDK terms. Proxyway 2026 says the provider Evomi sources IPs from Earn.fm, an SDK, and possibly other vendors. Trustpilot includes a complaint about an unpaid withdrawal. — [Proxyway 2026](https://proxyway.com/research/proxy-market-research-2026); [Trustpilot](https://ca.trustpilot.com/review/earn.fm)
- ProxyRack / Peer2Profit: Proxyway 2026 says ProxyRack sources IPs through "an SDK and proxyware," but I found no public developer program. Peer2Profit's proxy buyer is unclear, and a review warns it is exposed to sanctions disruption. — [Proxyway 2026](https://proxyway.com/research/proxy-market-research-2026); [simeononsecurity Peer2Profit](https://simeononsecurity.com/other/install-peer2profit-easily-using-docker/)
- Oxylabs: Proxyway 2026 says it sources IPs through the Honeygain and JumpTask apps "plus an SDK." I found no public third-party developer SDK program, and one source says Oxylabs no longer seems to offer its SDK to third parties. — [Proxyway 2026](https://proxyway.com/research/proxy-market-research-2026)
- Others named as SDK-sourced in 2026 without public developer terms found: DataImpulse (SDK plus an unnamed app) and Byteful (SDK). — [Proxyway 2026](https://proxyway.com/research/proxy-market-research-2026)
- A small Android-only SDK exists at proxies.sx ("Peer Network SDK"). It is irrelevant for desktop. — [proxies.sx/sdk](https://www.proxies.sx/sdk)

### Inferences
- On published terms, Decodo is the most plausible new desktop candidate after Massive and Honeygain. Its consent rules (explicit opt-in, compensation, easy opt-out) match Otium's model, and its stated DAU floor of about 1K is low. Infatica's 50K MAU floor probably excludes Otium for now.
- The Pawns SDK on Linux at $0.20/GB is the only program found that pays per GB and covers all three desktop OSes. Per-GB pay suits always-on idle machines if traffic demand is high. The idle-device restriction from the earlier research needs re-checking with IPRoyal directly.
- The per-DAU figures broadly agree: Decodo up to $0.20/month, Honeygain $0.10–0.40/month, Bright $0.10–0.35/month, Infatica up to $0.60/month. That puts realistic desktop income at roughly $0.10–0.30 per user per month in US and EU markets, the same order as Massive's $0.05–0.20 from the earlier research.
- None of the vendor pages say an operator cannot aggregate users. All of them are designed for one app publisher with many end users, which is Otium's model.

### Gaps
- No vendor page found states the payout method or minimum, KYC specifics, exclusivity or stacking rules, or the integration form (C API vs. process). Decodo, Infatica and Pawns all need these confirmed through sales.
- I found no SDK or publisher pages for SOAX, Live Proxies, Rayobyte or Proxy-Cheap. They may not run public programs. I could not tell, because no sources turned up.
- Country-specific desktop rates for EU and US were not available beyond the ranges above.

## What 2026 enforcement, lawsuits, Play Protect/Defender detections or consent scandals affect these vendors?

### Takeaway
2026 brought Google- and FBI-led takedowns of IPIDEA (January) and NetNut (July), plus the March takedown of the Aisuru/Kimwolf botnet, which ran through residential proxy networks. Separately, AhnLab documented malware in 2026 installing legitimate SDK DLLs from SOAX and IPRoyal, with attacker tokens, on victims' Windows PCs. The vendors appear to have been abused rather than complicit. Infatica carries a Sophos PUA detection and a history of bundling. I found no Microsoft Defender detection naming Decodo, Honeygain, Pawns or Massive.

### Cited Findings
- **IPIDEA (late Jan 2026):** Google GTIG, with partners, took down control domains and shared SDK intelligence. Play Protect removes and blocks apps carrying IPIDEA SDKs, and Cloudflare disrupted resolution. — [BleepingComputer](https://www.bleepingcomputer.com/news/security/google-disrupts-ipidea-residential-proxy-networks-fueled-by-malware/); [Help Net Security, 29 Jan 2026](https://www.helpnetsecurity.com/2026/01/29/ipidea-proxy-network-disrupted/); [Peakhour, 3 Aug 2026](https://www.peakhour.io/blog/ipidea-netnut-proxy-takedowns/)
- Google acted against 10+ Chinese brands it called IPIDEA storefronts, including 922Proxy and LunaProxy, though "over a dozen related brands" still operate. — [Proxyway 2026](https://proxyway.com/research/proxy-market-research-2026)
- Wikipedia says IPIDEA's SDKs were branded among others as "Earn SDK" and "Packet SDK," enrolling devices "typically without the user's knowledge or consent." This is a secondary source. — [Wikipedia: Ipidea](https://en.wikipedia.org/wiki/Ipidea)
- **NetNut/Alarum (early July 2026):** Google, the FBI, Lumen and IRS-CI acted. The FBI seized NetNut domains, and Play Protect disables apps bundling the NetNut SDK. On 3 July Alarum said the seizures disrupted services and paused traffic. On 13 July it reported a material impact, an external forensic review, and cuts to about one third of its workforce. Alarum disputes the "botnet" label. — [Peakhour](https://www.peakhour.io/blog/ipidea-netnut-proxy-takedowns/); [Cybersecurity News](https://cybersecuritynews.com/google-dismantles-netnut-residential-proxy/)
- **Aisuru/Kimwolf (March 2026):** Kimwolf, an Android variant of Aisuru, spread through residential proxy networks and reached 2M+ devices by Jan 2026. US, Canadian and German authorities disrupted it in March 2026. Its operators reportedly monetised infected devices through the **Byteconnect SDK**, which is linked to Plain Proxies. The FBI issued a bulletin on residential proxies. Google tightened Play Store requirements, and NetNut removed ASN/ISP targeting. — [Proxyway 2026](https://proxyway.com/research/proxy-market-research-2026)
- **AhnLab Larva-25012 (17 Sep 2026):** A threat actor used DPLoader-infected PCs to install DigitalPulse, **SOAX**, Appsalt and **IPRoyal** proxyware in 2026. For SOAX, Appsalt and IPRoyal, a PowerShell downloader dropped a loader and the vendor's **SDK DLL**, and the loader passed the attacker's token to the SDK. Persistence was a scheduled task (for example "EnterpriseMgmtServicesScheduler" for SOAX). The scripts also disabled or excluded Microsoft Defender. Honeygain and Infatica had been used in earlier waves. Honeygain told ASEC it was not involved and did not profit. AhnLab detects the components as Unwanted/Win.Proxyware.*. — [ASEC/AhnLab](https://asec.ahnlab.com/en/95516/)
- **Infatica:** Sophos issued a PUA detection for "Infatica P2B in Non-Standard Location." — [Sophos KBA-000043735](https://support.sophos.com/support/s/article/KBA-000043735?language=en_US)
- Gridinsoft documents Infatica arriving bundled in third-party software such as K-Lite Codec Pack. — [Gridinsoft](https://blog.gridinsoft.com/k-lite-codec-pack-infatica-removal/)
- Synthient rates InfaticaSDK as medium risk for consent practices. — [Synthient](https://synthient.com/context/provider/INFATICA)
- Proxyway notes 2021 criticism that Infatica approached browser-extension developers to include the SDK with limited disclosure. — [Proxyway, Dec 2025](https://proxyway.com/research/internet-sharing-sdks-app-monetization)
- Bright VPN is reported as installed through bundled installs that trigger security alerts. This is Bright Data's consumer VPN, not the SDK as such. — [Gridinsoft](https://blog.gridinsoft.com/is-bright-vpn-safe/)
- **Defender:** A GitHub discussion describes PUA:Win32/FRProxy as a heuristic Microsoft applies to proxy software generally. This is a user explanation, not an official Microsoft statement. — [hiddify discussion #2101](https://github.com/hiddify/hiddify-app/discussions/2101)

### Inferences
- The SOAX and IPRoyal SDK DLLs are being dropped by malware in 2026. Any app that ships them, Pawns included, may share file hashes with samples that AV vendors flag as Unwanted/Proxyware. That raises the risk of false positives for Otium's installer. Honeygain has the same exposure from earlier waves.
- Infatica has the weakest reputation of the new candidates: a Sophos PUA detection, bundling reports and a past consent controversy.
- I found no enforcement action against Decodo, Honeygain, Bright SDK, Pawns/IPRoyal as a company, or Massive in 2026.

### Gaps
- I could not get Microsoft Security Intelligence encyclopedia entries or AppEsteem certification status for any of these SDKs.
- I found no 2026 lawsuits against SDK vendors other than the NetNut/Alarum domain seizures.

## Which programs offer a reporting or earnings API good enough to automate crediting each user?

### Takeaway
None of the 2026 vendor pages reviewed (Decodo, Infatica, Pawns) publicly documents a per-device or per-user earnings API. Pawns mentions a real-time revenue dashboard, Honeygain's SDK reports "estimated earnings per user" metrics, and Bright reports through partner contact. Otium probably needs to keep crediting from its own measurements (device active-time and bytes from the SDK's local callbacks) reconciled against the monthly partner statement, unless a vendor confirms an API under NDA.

### Cited Findings
- Honeygain SDK metrics include opt-in rates, bandwidth contributed, session duration and estimated earnings per user, in a self-service portal. — [Proxyway, Dec 2025](https://proxyway.com/research/internet-sharing-sdks-app-monetization)
- Bright SDK has limited self-service analytics, with reporting handled through direct partner communication. — [Proxyway, Dec 2025](https://proxyway.com/research/internet-sharing-sdks-app-monetization)
- Pawns SDK has a partner dashboard with "real-time insights on your app revenue." No API or per-device stats are mentioned. — [Pawns SDK](https://business.pawns.app/sdk/)
- Decodo's page mentions no dashboard or API. Infatica's page mentions none either, and Proxyway notes little detail on its dashboards or APIs. — [Decodo](https://decodo.com/monetize); [infatica-sdk.io](https://infatica-sdk.io/); [Proxyway](https://proxyway.com/research/internet-sharing-sdks-app-monetization)
- Consumer-side programs such as PacketStream use CAPTCHA-protected logins, which makes automated earnings collection hard. Running them as a fleet under one account is not a substitute for a publisher API. — [CashPilot PacketStream guide](https://raw.githubusercontent.com/GeiserX/CashPilot/HEAD/docs/guides/packetstream.md)

### Inferences
- Under per-DAU programs (Decodo, Infatica, Bright, Honeygain), Otium can credit users deterministically: a user who was active that day gets the day's rate share, and a monthly true-up matches the statement. Precise per-user API data is not needed.
- Under per-GB programs (Pawns), each user's credit needs per-device byte counts. The SDK would have to expose a local traffic counter or the vendor would have to supply per-device reporting. This is a key question for IPRoyal before integrating.

### Gaps
- Whether Decodo, Infatica or Pawns offer per-device reporting or an API to partners under contract is unknown, because none is published. The same is unknown for payout methods (PayPal, wire, crypto), minimums, publisher KYC, and stacking or exclusivity rules.
