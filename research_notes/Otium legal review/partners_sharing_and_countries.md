# Otium legal review: third-party terms, internet sharing, country restrictions

Research notes as of 19 October 2026, written as input for counsel. They are not legal advice. A URL follows each claim.
**[Primary]** means the provider's own page, a licence, or a government source. **[Secondary]** means news, law-firm
or vendor commentary. **[Unverified]** means I could not confirm it against a primary source. **[Superseded]** marks
material that is out of date.

**How the sources were read.** On the research machine, Malwarebytes blocks unmineable.com and moneroocean.stream.
`http://unmineable.com/` redirects to `block.malwarebytes.com?...cat=PUP...&url=www.unmineable.com` (seen 18 Oct 2026).
That is a data point for section 6 in its own right. Pool pages were therefore read from Wayback Machine snapshots, and
each snapshot date is given. The Otium code facts below come from this repo
(`src/Otium.Providers.*`, `src/Otium.App/Services/WindowsSecurity.cs`, `InternetSharingViewModel.cs`).

---

## 1. Mining pools

### unMineable: "Terms and Conditions – Platform Usage"
Source [Primary]: https://unmineable.com/support/article/term-and-conditions-platform-usage. Read from the snapshot of
1 Sep 2026, http://web.archive.org/web/20260901144527/https://unmineable.com/support/article/term-and-conditions-platform-usage.
The page says "Last updated: 6 months ago", which puts the last update around March 2026. The separate "App usage" T&C
URL now redirects to the homepage, so it appears to have been merged into this document **[Superseded]**
(https://www.unmineable.com/support/article/term-and-conditions-app-usage).

- **Age and capacity.** You must have legal capacity and be "at least the age of legal majority in your jurisdiction"
  (§2.1). Users must decide for themselves whether use is lawful where they are (§2.2–2.3).
- **US persons are excluded, and "US person" is read broadly** (§2.4). It covers "any person located in the United
  States, any U.S. citizen, any U.S. resident, any entity organized ... under U.S. law, or any person otherwise subject
  to U.S. jurisdiction." It also bars use "on behalf of, for the benefit of, or under the direction of a U.S. person."
  → Two consequences for Otium. US citizens living abroad are excluded too, which Otium's Windows-region check
  (`AppHost.InUnitedStates`, region only) does not catch. And Otium itself must not be a US person.
- **Other restricted persons.** The terms name no list of countries. They exclude anyone in a jurisdiction where use
  "would be prohibited or restricted under laws ... applicable to you, to us, or to the Services" (§2.5), sanctioned
  persons (§2.6) and terrorism links (§2.7). unMineable may suspend for "legal, regulatory, sanctions, operational,
  financial, or reputational risk" (§2.8, §3.14, §9.4).
- **Third-party and automated software.** Nothing prohibits it. The terms say "You remain solely responsible for
  configuring your mining software, workers, identifiers" (§3.7). An account "may be used with multiple workers,
  devices, or miners" (§3.8). The support centre publishes setup guides for XMRig, SRBMiner and other third-party miners
  (https://unmineable.com/support/article/how-to-setup-srbminer-for-gpu-mining/,
  https://unmineable.com/support/article/getting-started/how-to-setup-xmrig-for-cpu-mining/).
- **Mining on behalf of others.** The terms have no botnet clause and no "own hardware only" clause. The only "behalf"
  language is the US-person clause (§2.4). §10.4 bars interfering with or abusing the Services.
- **Referral code on every user's worker.** No separate referral-programme terms are published. The CDX listing of
  unmineable.com/support/* (Wayback Machine) has no referral article. The T&C says only: "We reserve the right to modify
  referral rules, calculations, eligibility, attribution logic ... at any time" (§3.10). unMineable's web app
  (https://unmineable.com/referrals; strings in the site's JS bundle, Oct 2026) shows how referrals work:
  - the referee's fee is 0.75% instead of 1%;
  - the code owner earns 0.25%;
  - "custom code tiers" are unlocked by a `minimum_active` count of active referees, which means unMineable expects
    referrers with many referees.

  Otium's own test run confirmed that the code sits on the worker and the fee drops to 0.75%
  (`docs/real-mining-run.md` lines 267–268). Nothing found prohibits an app putting its own code on every user's
  worker, but nothing expressly permits it either. **Get written confirmation** (as `docs/technical.md:536` already
  suggests).
- **Custody, KYC and payouts.** unMineable holds custody of balances until payout (§6.1). It may change thresholds
  (§6.2), delay or refuse payouts for compliance reasons (§6.7), and require "identity verification, source-of-funds
  information, enhanced due diligence" (§6.8).
- **Fees.** From 1% of rewards, and up to 10% for some assets or routes. Fees can change at any time and are accepted by
  continued use (§7.2–7.4).
- **Dormancy and forfeiture.** A balance is inactive after **six continuous months without mining or referral rewards**
  (§12.1). Inactive data and "any remaining balance" may then be deleted (§12.2) and are "permanently forfeited" (§12.3).
  Notice is optional (§12.5). → Users who stop mining below the payout threshold can lose the balance. Otium's own fee
  address is exposed in the same way if fee slices stop.
- **Governing law and disputes.** "The laws applicable in the place from which unMineable.com operates" (§17). No
  entity or country is named, so it is uncertain which law applies and who the counterparty is.

### MoneroOcean (the pool for US users)
- **No terms of service found.** The current site (a single-page app) shows a Help section, a payout policy and a
  local-history consent. The site script has no ToS, no botnet clause, no age clause and no country clause. Source
  [Primary]: https://moneroocean.stream/#/help, read from the script snapshot
  https://web.archive.org/web/20260928153202/https://moneroocean.stream/script.js?v=660da4b (28 Sep 2026).
- **Payouts.** Payouts "auto-send when due balance reaches the threshold." There is a minimum and default threshold, and
  a fee that falls to zero above a "zeroFeeThreshold". All rewards are paid in XMR (same source). No dormancy or
  forfeiture rule was found.
- **Operator.** The operator's identity and jurisdiction are not stated **[Unverified]**. A pool with no terms means no
  contractual permission and no contractual prohibition. The risk is mainly operational (it can block or ban workers at
  will).

---

## 2. Miner licences

### XMRig (GPL-3.0)
- **The licence is GPLv3** [Primary]: https://github.com/xmrig/xmrig/blob/master/LICENSE.
- **Running it.** GPLv3 §2: "This License explicitly affirms your unlimited permission to run the unmodified Program."
  It adds: "You may make, run and propagate covered works that you do not convey, without conditions."
- **Downloading it.** GPLv3 §9: "You are not required to accept this License in order to receive or run a copy."
- **Conveying.** GPLv3 §0: "To 'convey' a work means any kind of propagation that enables other parties to make or
  receive copies." Source: https://www.gnu.org/licenses/gpl-3.0.html.
- **Otium's position.** Otium downloads the official release from `github.com/xmrig/xmrig/releases` on the user's
  machine and checks its SHA-256 (`XmrigRelease.cs`). Each user therefore receives the copy from the XMRig project, not
  from Otium, and Otium does not modify it. On that reading Otium conveys nothing and has **no GPL obligations** (no
  source offer, and no copyleft reaching Otium's own code).
  - Otium only drives XMRig through a config file, the command line and a local HTTP API. That is the arm's-length
    pattern GPLv3 treats as separate works (§5, "aggregate").
  - Obligations would start if Otium **mirrored or bundled** the binary (for example on its own CDN, or inside the
    installer), or shipped a patched build. Otium would then have to give the licence text and a source offer (§4–6).
  - **Counsel to confirm** that a download triggered by Otium's software from a third-party URL is not "making
    available" by Otium under Norwegian or EU copyright law.
- **Donate level.** XMRig's source sets `kDefaultDonateLevel = 1; kMinimumDonateLevel = 1` and describes the donation as
  1 minute in every 100 mined to the developer's pool [Primary]:
  https://raw.githubusercontent.com/xmrig/xmrig/master/src/donate.h. Otium sets `donate-level = 1`
  (`XmrigConfig.cs:22,58`), so it keeps the developer donation.
  - This is **not a licence obligation**. The GPL cannot require it, and recompiling with 0 would be a lawful
    modification.
  - It is a fairness and reputation point. Users should be told that about 1% of CPU time goes to the XMRig developer.
  - `donate-over-proxy = 0` is Otium's choice and does not affect compliance.

### SRBMiner-Multi 3.7.6 (closed source)
- **Licence text.** The licence is in the README [Primary]: https://github.com/doktor83/SRBMiner-Multi. It says:
  - "Copyright © doktor83. All rights reserved."
  - "This software may be used only for lawful cryptocurrency mining on hardware or systems that you own or are
    authorized to use."
  - "precompiled GPU binaries ... may be executed only as part of an official or otherwise expressly authorized
    SRBMiner-MULTI release."
  - "Extracting, copying, modifying, reverse engineering ... redistributing, sublicensing, selling, publishing,
    incorporating into another product or otherwise using these binaries without prior written permission from the
    copyright holder is prohibited."
  - The repo has no separate EULA file.
- **What Otium does.** Otium downloads the official release archive from `github.com/doktor83/SRBMiner-Multi/releases`,
  checks its hash (`SrbMinerRelease.cs:22–23`), and runs it unmodified on the user's own hardware. That fits "official
  release" and "hardware you own."
- **The open question.** It is ambiguous whether a third-party app that downloads, configures and supervises the miner
  automatically is "incorporating into another product." The clause gives the author a basis to object. → **Get written
  permission from doktor83**, or at least a documented "no objection."
- **Dev fee.** The README lists a percentage for each algorithm (for example `[0.85%]`, `[3.00%]`, `[0.00%]`) next to the
  hardware tags. The README does not label these figures, so reading them as the built-in dev fee is an inference
  (same URL). Otium should disclose the dev fee to users.

---

## 3. Swap and payout partners, and Proxybase

### Relay (relay.link, Uneven Labs, Inc., Delaware)
Terms of Use, "Last updated: September 10, 2025" [Primary]: https://relay.link/terms. The live page returned
403/429, so it was read from the snapshot of 5 May 2026,
http://web.archive.org/web/20260505110759/https://relay.link/terms.
- **Eligibility.** Users must be of "the age of majority (e.g., 18 years of age)" (§1.1).
- **Restricted persons (§1.2).** Not sanctioned or on an OFAC list. Not in "Cuba, Iran, North Korea, Syria, the Crimea,
  Donetsk, or Luhansk Regions of Ukraine, or any other country ... against which the U.S. maintains comprehensive
  economic sanctions or an arms embargo." No VPN evasion is allowed.
  - Syria is still listed even though US Syria sanctions ended. Treat the Relay list as binding regardless (see
    section 5).
  - **The US is not excluded.**
- **Apps and integrators (§4.1).** Data "may not be directly resold"; an app "may incorporate the data into your own
  product ... for your own end-users."
- **Prohibited activity relevant to Otium (§4.2(viii)).** "Any activity that seeks to carry out financial activities
  subject to registration or licensing." → If Otium's app fee on swaps makes Otium a regulated crypto-asset service
  provider (MiCA, which Norway has applied since 1 Jul 2025;
  https://www.finanstilsynet.no/konsesjon/kryptoeiendeler/), Otium would breach Relay's terms as well as the law.
- **Disputes.** Binding individual arbitration and a class-action waiver (§14).
- **App fees.** "App fees are additional fees set by third-party developers on top of existing fees." They are set in
  basis points, accrue as an off-chain USDC balance, and are claimed to an EVM address [Primary]:
  https://docs.relay.link/features/app-fees.
- **Address screening.** Relay screens addresses with Chainalysis and HackBounty data and its own blocklist. "A fill-time
  screening failure does not trigger an automatic refund" [Primary]: https://docs.relay.link/security/compliance.

### SideShift.ai (Paradigm Ltd, c/o IIC Management, Basseterre, St Kitts)
Source [Primary]: https://sideshift.ai/legal. The page has no "last updated" date (footer "© 2026").
- **Restricted (§3.4).** The service is "not intended for use by individuals who are citizens or nationals of or
  resident in" the **United States, Cuba, Iran, North Korea, Saint Kitts and Nevis, Syria**. SideShift may add countries
  at any time (§3.3).
- **Age.** 18 or the local legal age (§3.5).
- **KYC.** No standing KYC. Automated screening may freeze a shift "for a period of time necessary to complete the
  investigation" (§3.11).
- **Referrers.** Under the referral programme "Referrers are solely responsible for any violations" (§4).
- **API rules for integrators** [Primary]: https://docs.sideshift.ai/api-intro/permissions.
  - "SideShift.ai does not allow proxying user requests."
  - A server-side integrator must set `x-user-ip` to the end user's real IP.
  - Integrators should call `/v2/permissions` before creating a shift.

  → Otium's LTC/XMR route must forward the user's IP (or call from the client) and must not offer SideShift to US users.
- **Affiliate commission.** 0.5% by default, adjustable from 0 to 2% [Primary]: https://docs.sideshift.ai/api-intro/monetization.

### ChangeNOW (CHN Group LLC, Kingstown, St Vincent and the Grenadines)
Terms, last updated 18 Sep 2026 [Primary]: https://changenow.io/terms-of-use/changenow-terms.
- **Prohibited jurisdictions (§11.1).** Users must not be "located in, or ... a citizen or resident of the **United
  Kingdom, the United States of America, Republic of India**." They must not be located in or resident of the **Russian
  Federation**, "a country subject to United Nations Sanctions Lists," or "a country where usage of cryptocurrency is
  forbidden." US users may use ChangeNOW Pro only, with an account.
- **Seizure (§11.4).** "ChangeNOW may seize any funds from the Users in these jurisdictions and donate them to a
  charity." → Routing UK or Indian Otium users through ChangeNOW risks losing their funds.
- **Mandatory KYC (§3.2).** Residents of the **EU**, US, Japan, South Korea, Singapore, Hong Kong, UAE, Australia and
  Canada must pass KYC before using the service. Norway (EEA, not EU) is not named. **[Uncertain]** whether ChangeNOW
  treats EEA residents as EU.
- **Risk-triggered KYC (§3.4–3.9).** ChangeNOW may freeze funds "for any period of time necessary," move them to cold
  storage, and refuse transactions. The KYC provider is SumSub, the verification window is 3 days, and a user who
  refuses KYC is refunded.
- **Data sharing (§4.2).** IP addresses may be shared with "business partners or governmental bodies."
- **Governing law.** St Vincent and the Grenadines (§15).
- **API terms** (last updated 9 Jul 2025) [Primary]: https://changenow.io/terms-of-use/changenow-api. ChangeNOW's
  liability to an API partner is capped at USD 100, and it can suspend the partner at its discretion.

### Proxybase (PROXYBASE XYZ LLC)
- **Terms of Service** (last updated March 2026) [Primary]: https://proxybase.xyz/terms. They are written for buyers and
  API users and contain **no seller terms**: nothing on seller age, eligible countries, seller consent, or who is
  responsible for traffic leaving the seller's IP.
- **Prohibited buyer uses (§2).** "DDoS attacks, botnets, or network stress testing"; "credential stuffing, carding, or
  brute-forcing"; spam; "distributing malware, phishing, or illegal materials"; pool scanning and reselling. Proxybase
  may revoke keys and end sessions immediately when it detects abuse (§2, §4).
- **Logging (§6).** Proxybase logs "only byte counters for billing and telemetry" and "do[es] not maintain personal
  identification records."
- **Privacy policy** (March 2026) [Primary]: https://proxybase.xyz/privacy. It complies with a "legally binding subpoena,
  court order, or warrant" but says "the only records we can provide are the bandwidth totals and transaction logs."
  It records tunnel start and end times and does not say whether destinations are logged.
- **Seller page** [Primary]: https://proxybase.xyz/earn/sell-internet. Buyers are "AI developers, web scrapers, ad
  verification companies, and researchers." Sellers need "no identity verification, no documents." Minimum payout is
  $1. The page says nothing about how to stop or uninstall.
- **Homepage** [Primary]: https://proxybase.xyz/. Buyers sign in with Google and deposit crypto. The page carries a
  **"No KYC Proxy"** link and calls the service "Autonomous programmatic SOCKS5 proxy infrastructure built exclusively
  for AI agents."
  → If traffic is abused, there is probably **no record linking the traffic to a buyer**. The seller's IP is the only
  visible party. This makes the user's exposure worse (section 4).
- **Client code (proxybase-cli v0.1.60, the version Otium pins).** It is open source under Apache-2.0/MIT
  (https://github.com/proxybasehq/proxybase-cli; `ProxybaseRelease.cs:17`). In the cached source, the seller relay dials
  whatever `target_ip:target_port` the backend sends ("Direct connect (no upstream proxy)",
  `src/main.rs` `run_stream_relay`).
  - There is **no client-side block** on private or LAN ranges (RFC 1918, link-local, loopback) and none on SMTP
    port 25.
  - The backend may filter targets, but that is **[Unverified]**.
  - Google found that IPIDEA's software "also sent traffic to the device, in order to compromise it" and exposed the
    user's home network (section 4). Otium's consent text says "Nothing on your computer is accessed"
    (`InternetSharingViewModel.cs:131`). **Without a LAN block that statement may be inaccurate.** Ask Proxybase, or
    add a local egress filter.

---

## 4. Residential proxy and bandwidth-sharing risk

### ISP terms (sharing or reselling a connection)

| ISP | Terms | Source |
|---|---|---|
| **Telia Norge** (General terms for private customers, in force 01.12.2025) | Telia may close or limit access if the customer "videreselger, pantsetter, låner bort, leier ut eller på annen måte tilbyr Tjenesten ... til tredjepart" (resells, lends, rents out or otherwise offers the service to a third party) or uses the service against law. The customer must be over 18 and is responsible for use of the service. | https://www.telia.no/globalassets/pdf/vilkar/telias-brukervilkar-for-privatpersoner.pdf |
| **Telenor Norge** (TV and broadband terms) | "Det er ikke tillatt å sette opp servere for kommersielle aktiviteter, eller videreselge / leie ut linjekapasitet" (it is not allowed to set up servers for commercial activity, or resell or rent out line capacity). The customer is responsible for securing against unauthorised use. | https://www.telenor.no/vilkar/tv-og-bredband/ |
| **Ice** (General terms, updated 30 Jul 2026) | "Kunden kan ikke videreselge eller videreformidle noen del av Tjenesten, herunder leie eller låne ut linjekapasitet til andre" (the customer may not resell or pass on any part of the service, including renting or lending line capacity). The service "kan ikke benyttes til kommersielle formål" (may not be used for commercial purposes). The customer is responsible for others' use (§4.2–4.4). | https://www.ice.no/globalassets/dokumenter/vilkar/generelle-vilkar-for-privatkunder_30.07.2026.pdf |
| **Comcast/Xfinity** (AUP, 1 Feb 2021) | Prohibits making the service "available to anyone outside the Premises," running "proxy services and servers," and use other than "personal and non-commercial residential use." The subscriber is responsible for misuse "by anyone else you permit." | https://www.xfinity.com/support/articles/comcast-acceptable-use-policy |
| Telenor Danmark (business internet; 2017; read only via search snippet) **[Secondary/Unverified]** | No letting others use the connection without written permission. | https://www.telenor.dk/imagevault/publishedmedia/j3gi8wly0g46acu4ph1f/Business_Internet_V06_DK.pdf |

→ Paid bandwidth sharing very likely breaches the standard consumer terms of the major Norwegian ISPs and Comcast. The
realistic sanctions are suspension, throttling or termination of the subscription, not criminal liability. Honeygain
pushes this risk onto the user: users must confirm "sharing Internet traffic is not prohibited by them," that they are
the account holder or have express permission, and that they are "SOLELY RESPONSIBLE"
(https://www.honeygain.com/terms-of-use/, updated 3 Aug 2026).

### Liability and consequences for the user
- **FBI PSA I-031226-PSA (12 Mar 2026)** [Primary]: https://www.ic3.gov/PSA/2026/PSA260312.
  - Residential proxies get IPs where "The owner of the device provides consent" or without consent. Proxy services pay
    app developers to embed SDKs.
  - Criminals use the IP to hide activity, "making the consumer appear responsible."
  - Some apps "promise to pay them for their internet bandwidth."
  - Businesses are advised to "block IP addresses linked to residential proxy networks."
- **Google GTIG on IPIDEA (28 Jan 2026)** [Primary]: https://cloud.google.com/blog/topics/threat-intelligence/disrupting-largest-residential-proxy-network.
  - Over 550 threat groups used IPIDEA exit nodes in one week of January 2026, including groups from China, DPRK, Iran
    and Russia.
  - Users "provide their IP address and device as a launchpad for hacking ... potentially causing them to be flagged as
    suspicious or blocked."
  - IPIDEA "also sent traffic to the device, in order to compromise it."
  - "Consumers should be extremely wary of applications that offer payment in exchange for 'unused bandwidth' or
    'sharing your internet.'"
  - "App developers have a responsibility to vet the monetization SDKs they integrate."
  - "Any claims of 'ethical sourcing' must be backed by transparent, auditable proof of user consent."
  - Google acted through domain takedowns, Play Protect removals and Cloudflare. Early data showed the pool cut by
    about 40% **[Secondary]**: https://cyberscoop.com/ipidea-proxy-network-disrupted-google-lumen/.
- **Google GTIG on NetNut/"Popa" (2 Jul 2026)** [Primary]: https://cloud.google.com/blog/topics/threat-intelligence/google-continued-disruption-residential-proxy-networks.
  - The botnet has at least 2 million devices, and many brands are "whitelabeling the NetNut botnet."
  - Play Protect disabled apps containing the NetNut SDK.
  - Users "can have their legitimate traffic flagged as suspicious, or blocked by their service providers."
- **911 S5 (May 2024)** [Primary]: https://www.justice.gov/archives/opa/pr/911-s5-botnet-dismantled-and-its-administrator-arrested-coordinated-international-operation
  and https://www.ic3.gov/PSA/2024/PSA240529.
  - The network reached more than 19 million IPs through malware-laden "free VPN" apps.
  - The administrator was arrested, and domains of a reconstitution attempt (Clourouter.io) were seized.
  - This case was non-consensual (malware). The distinction from Otium is consent, but regulators and platforms now
    discount "consent" claims (Google, above).
- **FBI PSA I-060525-PSA (5 Jun 2025, BADBOX 2.0)** [Primary]: https://www.ic3.gov/PSA/2025/PSA250605. Infected devices
  were sold as residential proxies.
- **Practical consequences for users** (inferred from the above): more CAPTCHAs; IP reputation blocks from banks,
  streaming and email; ISP abuse notices or suspension; police or copyright inquiries addressed to the subscriber. The
  user cannot point to a buyer because Proxybase keeps no buyer identity or destination records (section 3).
- **Consequences for the operator**: the platform-level actions above. Google Play Protect, and possibly Defender, now
  classify proxy SDK apps as harmful. Otium's reputation as "a bandwidth-sharing app" is at risk, as are its AV
  detections (section 6).

### Best-practice consent standards
- **Bright Data SDK** (self-described) [Primary]: https://brightdata.com/trustcenter/bright-sdk-ethical-data-practices and
  https://docs.brightdata.com/general/policy/How_do_consumers_join_the_Bright_Data_network_of_peers_.
  - Opt-in before the SDK runs, opt-out "whenever they wish," and an app-by-app compliance review.
  - Uses the device only when idle, plugged in, and preferring Wi-Fi.
  - Reporting describes its consent screen as naming Bright Data, linking its privacy policy and licence, and offering
    a 2-step opt-out **[Secondary]**: https://thehackernews.com/2026/06/free-apps-are-quietly-turning-smart-tvs.html.
- **EWDCI** (i2Coalition Ethical Web Data Collection Initiative). Its principles are legality, ethics, ecosystem and
  social responsibility; the full proxy-consent text was not retrieved **[Unverified]**:
  https://i2coalition.com/working-groups-and-initiatives/community-collaboration/ewdci/.
- **Honeygain.** 18+ only. Blocked countries: Cuba, Iran, North Korea, Syria, Russia, Belarus. Users must have ISP and
  account-holder permission (https://www.honeygain.com/terms-of-use/).
- **Microsoft's criteria** (section 6). Prominent notice, visible when active, easy stop and uninstall, consent before
  transmitting data.

### What Otium's sharing consent screen and terms should say (recommendation)
The current text (`InternetSharingViewModel.cs:128–135`) is plain and good. It should add:
1. **Who uses the connection and for what.** Name Proxybase, a US LLC, and state that its buyers are not identity-checked
   ("No KYC") and that Otium cannot see or control the traffic.
2. **Risks, stated plainly.** Websites, banks or streaming services may block or challenge the IP. The ISP may treat it
   as a breach of its terms and suspend the service. Abusive traffic can be traced to the user's address, leading to
   police, ISP or copyright notices. There is no buyer record the user can point to.
3. **ISP and account-holder confirmation.** A checkbox: "I am 18 or over, I am the internet subscriber or have their
   permission, and my ISP's terms allow this." Link examples (Telia, Telenor, Ice).
4. **Remove "Nothing on your computer is accessed"** unless local and LAN destinations are verifiably blocked. Replace
   it with what is actually guaranteed.
5. **Controls.** Off by default. A separate opt-in from mining. One-click off and a visible status indicator. A data cap
   option (there is currently none for Proxybase). Idle-only option. Full removal of the client on opt-out or uninstall
   (Otium already offers "Removed ...").
6. **Countries.** Not available in sanctioned or blocked countries (section 5).
7. **Fees.** Otium's percentage and the conversion costs (already present).
8. **Record keeping.** Keep a consent record (timestamp, text version), which `BandwidthConsentAt` already does. Re-ask
   whenever the text changes.
9. **In Otium's own terms.** No warranty for partner conduct. The user indemnifies for breach of ISP terms (consumer law
   limits this). Otium may switch sharing off. A contact point for abuse reports.

---

## 5. Countries: mining or crypto bans, and sanctions

### Mining or crypto bans relevant to distributing a mining app
- **Overview.** The Law Library of Congress's Nov 2021 update counts **9 absolute bans and 42 implicit bans** [Primary]:
  https://www.loc.gov/item/2021687419/. Absolute bans as tabulated: Algeria, Bangladesh, China, Egypt, Iraq, Morocco,
  Nepal, Qatar, Tunisia. I confirmed the rows for Algeria, Bangladesh and Iraq directly; the other six are as commonly
  reported from the table. **Dated** [partly Superseded].
- **China.** NDRC notice 发改运行〔2021〕1283号 (24 Sep 2021) classes mining as an industry to be eliminated, bans new
  projects and bars financing [Primary]: https://www.ndrc.gov.cn/xxgk/zcfb/tz/202109/t20210924_1297474.html. Together
  with the PBOC's Sept 2021 ban on crypto transactions, this was reaffirmed in an 8-department PBOC-led notice on
  6 Feb 2026 **[Secondary]**: https://www.ithome.com/0/919/986.htm. → **Block.**
- **Algeria.** The 2018 Finance Law (art. 117) banned buying, selling, using and holding crypto (LoC table).
  **Law 25-10 of 24 Jul 2025** criminalises issuing, buying, selling, possessing, using or promoting crypto-assets,
  reportedly including mining, with 2 months to 1 year in prison **[Secondary]**:
  https://algeriatech.news/algeria-crypto-ban-law/. → **Block.**
- **Egypt.** Law 194/2020 art. 206 bars issuing, trading or promoting crypto without a CBE licence, and none have been
  granted **[Secondary]**: https://eg.andersen.com/legality-cryptocurrency-in-egypt/. → **Block** (conservative).
- **Bangladesh.** Bangladesh Bank cautionary notices (LoC table) → **Block or warn.**
- **Nepal.** NRB treats crypto trading and mining as illegal. A July 2025 warning threatens punishment **[Secondary]**:
  https://www.b360nepal.com/nrb-warns-of-action-against-those-involved-in-virtual-currency-networking-business.
  → **Block.**
- **Iraq, Morocco, Qatar, Tunisia.** Bans per the LoC table. Morocco and Qatar are moving toward regulated regimes
  **[Unverified]**. → **Block** until counsel says otherwise.
- **Kosovo.** Mining has been banned since Jan 2022 (energy emergency) **[Secondary]**:
  https://seenews.com/news/kosovo-bans-cryptocurrency-mining-amid-energy-crisis-report-768080. Kosovo adopted a
  crypto-asset law (08/L-295) in Nov 2024. Whether the mining ban survives is **[Unverified]**. → **Block or warn.**
- **Russia.**
  - A blanket mining ban in 10 regions runs from 1 Jan 2025 to 15 Mar 2031: Dagestan, Ingushetia, Kabardino-Balkaria,
    Karachay-Cherkessia, North Ossetia, Chechnya, the "DPR/LPR", and Zaporizhzhia and Kherson. Seasonal bans apply in
    parts of Irkutsk, Buryatia and Zabaykalsky.
  - Year-round bans in Buryatia and Zabaykalsky run from 1 Apr 2026.
  - Resolution No. 936 (25 Jul 2026) adds **Moscow, the Moscow Region and parts of Kursk** from 15 Aug 2026 to the end
    of 2032 **[Secondary]**: https://cointelegraph.com/news/russia-expands-crypto-mining-ban-moscow-2032,
    https://tass.com/politics/2152151.
  - Whether the bans also cover household or pool mining under Law 221-FZ is **[Unverified]**.
  - → **Block Russia** anyway, for the sanctions and partner reasons below.
- **Iran.** Mining needs a licence. Unlicensed and household-power mining is fined and raided; authorities say over 95%
  of 427,000 devices are unlicensed (Nov 2025) **[Secondary]**:
  https://blockworks.com/news/iran-cracks-down-on-crypto-miners-as-electricity-demand-surges. Iran is also comprehensively
  sanctioned. → **Block.**
- **Kazakhstan.** Law No. 193-VII on Digital Assets (in force 1 Apr 2023) requires a licence for digital mining, open
  only to individual entrepreneurs and legal entities **[Secondary]**:
  https://www.morganlewis.com/pubs/2023/02/kazakhstan-introduces-new-regulation-of-digital-assets. Some read it as
  barring private individuals. → **Warn or block.**
- **Kuwait.** An April 2025 Interior Ministry crackdown on home mining cited the Penal Code and other laws; over 1,000
  sites are under suspicion **[Secondary]**: https://decrypt.co/315982/kuwait-government-warning-1000-sites-mining-cryptocurrency.
  → **Block.**
- **Angola.** A mining ban law has applied since April 2024, with prison terms reportedly including for possession of
  mining equipment **[Secondary]**: https://www.verangola.net/va/en/032024/Economy/39128/Bitcoin-Angola-community-regrets-ban-on-cryptocurrency-mining-in-the-country.htm.
  → **Block.**
- **Venezuela.** Since May 2024 the Ministry of Electric Power has disconnected and confiscated mining equipment
  **[Secondary]**: https://nobsbitcoin.com/venezuela-cracks-down-on-bitcoin-mining. → **Warn or block.**
- **Norway.**
  - **There is no ban on household mining.** The government is studying a temporary, narrowly drawn ban on new
    power-intensive *crypto-mining data centres* under the Planning and Building Act §6-3 [Primary]:
    https://www.regjeringen.no/no/aktuelt/utvinning-av-kryptovaluta/id3110205/.
  - A September 2026 ministerial answer reportedly says nothing has been decided yet, with follow-up in the 2027
    budget **[Secondary/Unverified]**: https://jqrgen.github.io/kryptonytt/. Parliamentary material:
    https://www.stortinget.no/globalassets/pdf/innstillinger/stortinget/2025-2026/inns-202526-140s-vedlegg.pdf.
  - The government itself warns that a general ban could conflict with the EEA Agreement.
  - → Home mining in Norway is lawful. Watch for a ban that could expand.

### Sanctions
- **OFAC programmes** [Primary]: https://ofac.treasury.gov/sanctions-programs-and-country-information (listing seen
  Oct 2026).
  - Comprehensive or territorial: **Cuba, Iran, North Korea**, and the **Crimea and so-called DNR/LNR regions** (under
    "Ukraine-/Russia-related").
  - **Syria is no longer a country programme.** EO 14312 (30 Jun 2025) revoked the Syria emergency from 1 Jul 2025. The
    Syrian Sanctions Regulations were removed from the CFR effective 26 Aug 2025. OFAC says the Caesar Act was repealed
    by Public Law 119-60. List-based sanctions remain (Assad, captagon; "PAARSS") [Primary]:
    https://ofac.treasury.gov/sanctions-programs-and-country-information/syria-related-sanctions. **[Superseded]** for any
    source that calls Syria "comprehensively sanctioned." But **Relay, SideShift and Honeygain still list Syria**, so
    Otium should keep blocking it for partner compliance.
  - **Venezuela.** Not comprehensive. After 3 Jan 2026 OFAC eased sanctions through general licences (GL 46B, 48A, 52,
    24A) while the government and SDN sanctions remain **[Secondary]**:
    https://sanctionsnews.bakermckenzie.com/category/sanctioned-targets/sanctions-targeting-venezuela/us-sanctions-against-venezuela/.
  - Belarus and Russia: list-based and sectoral.
- **EU and Norway on Russia.**
  - EU Reg. 833/2014 art. 5b already barred crypto wallet, account and custody services to Russian nationals and
    residents.
  - The **19th package** (adopted 23 Oct 2025, applying from 25 Nov 2025) extends the ban to crypto-asset services as
    defined in MiCA. EEA/Swiss nationals and residence-permit holders are carved out **[Secondary]**:
    https://www.matheson.com/insights/the-european-unions-19th-sanctions-package-against-russia-key-takeaways/.
  - **Norway applied the 19th package from 27 Mar 2026** and a 20th package from 14 Jul 2026 (more crypto-service
    measures), through forskrift 15.08.2014 nr. 1076 [Primary]:
    https://www.regjeringen.no/no/aktuelt/nye-sanksjoner-mot-russland-er-innfort-i-norsk-rett/id3155117/ (press releases;
    the exact package-to-page mapping is **[Unverified]**).
  - → Whether Otium's non-custodial fee and swap features are a "crypto-asset service" to Russian residents is a
    question for counsel. **Block Russia and Belarus** to be safe. The EU Belarus regime has parallel crypto provisions
    **[Unverified]**.
- **EU on Syria.** Most economic sanctions were lifted in May 2025 **[Unverified here]**. Norway normally aligns.

### Recommended country handling

| Treatment | Countries | Reason |
|---|---|---|
| **Block the whole app** (install and run: mining, sharing and swaps) | Cuba, Iran, North Korea, Syria, Russia, Belarus, Crimea/"DNR"/"LNR" and the occupied Zaporizhzhia/Kherson regions (via Russia/Ukraine region IP data where possible) | Sanctions; Relay §1.2; SideShift §3.4; ChangeNOW §11; Honeygain list; Russian regional mining bans; EU/NO art. 5b |
| **Block mining** (and the app, since mining is its core) | China, Algeria, Nepal, Egypt, Bangladesh, Iraq, Morocco, Qatar, Tunisia, Angola, Kuwait | Explicit or criminal bans (some dated; review yearly) |
| **Warn, with user confirmation, or block** | Kosovo, Kazakhstan, Venezuela, Uzbekistan and Kyrgyzstan (licensing regimes, **[Unverified]**) | Unclear or licensing-based regimes |
| **US** | Route mining to MoneroOcean (unMineable §2.4). Do **not** offer SideShift (§3.4) or ChangeNOW (§11.1). Relay is allowed. Also catch US citizens abroad by self-declaration, because the region/IP check misses them. | Partner terms |
| **UK, India** | Do **not** route through ChangeNOW (§11.1, seizure risk §11.4). SideShift or Relay are fine. | Partner terms |
| **EU** (and possibly EEA) | Expect mandatory KYC at ChangeNOW (§3.2). Prefer SideShift or Relay. | Partner terms |

Detection should combine IP geolocation at the server (for sign-up and swap quotes), the Windows region, and a
self-declaration (residence and US-person status) at onboarding.

---

## 6. Antivirus and PUA classification
- **Microsoft's criteria** ("How Microsoft identifies malware and potentially unwanted applications", ms.date 2026-01-29,
  updated 2026-09-10) [Primary]: https://learn.microsoft.com/en-us/defender-xdr/criteria.
  - **PUA categories include "Cryptomining software (Enterprise only): Software that uses your device resources to mine
    cryptocurrencies."** On managed or enterprise devices with PUA protection on, Otium will be blocked by category.
    Consumers are affected through "Poor industry reputation" (other vendors' detections) and "Evasion software."
  - **"Unwanted software"** covers lack of choice: failing to give "prominent notice about the behavior," failing to
    "clearly indicate when the software is active," attempting "to hide or disguise its presence," or installing
    software "without a clear indication of its relationship to the primary software." It also covers lack of control
    ("must be able to start, stop, or otherwise revoke") and a poor removal experience (not using "Add or Remove
    Programs").
  - Data transmission needs "notice and ... consent."
  - The **"Downloader"** malware class covers software that "downloads other malware." Benign runtime download of miners
    is not malware, but it looks similar to heuristics.
  - **"Tampering software"** covers tools that weaken security. **"Vulnerable drivers"** is a separate class.
- **WinRing0.** XMRig's MSR mod on Windows uses the WinRing0 driver. Since 2025 Defender flags `WinRing0x64.sys` as
  `HackTool:Win32/Winring0` / `VulnerableDriver` (CVE-2020-14979) **[Secondary]**:
  https://www.neowin.net/news/windows-1110-is-flagging-winring0-on-your-pc-monitoring-fan-control-apps-heres-why/. →
  Run without admin or MSR, or disable MSR, so the driver never loads.
- **Defender exclusion.** Otium offers a one-click Defender exclusion. It runs a **hidden** PowerShell with
  `Add-MpPreference -ExclusionPath` through UAC (`WindowsSecurity.cs`). The user starts it and the scope is limited to
  the engines folder, which is good. But adding an exclusion with hidden PowerShell is a classic defence-evasion pattern
  (MITRE ATT&CK T1562.001, https://attack.mitre.org/techniques/T1562/001/; the page was not retrievable here) and fits
  Microsoft's "Tampering" and "Evasion" language.
  - The risk is that this behaviour itself triggers detections or a "Poor industry reputation" label.
  - Prefer guiding the user through the Windows Security UI, or show the exact command in a visible window.
  - unMineable publishes the same kind of guide for its own users:
    https://unmineable.com/support/article/how-to-make-an-exclusion-on-windows-defender.
- **Other vendors.**
  - Malwarebytes uses "RiskWare.CoinMiner" / "RiskWare.BitCoinMiner" for miners "that may be active on a system without
    user consent." It also notes they can be "installed by the users themselves." [Primary]:
    https://www.malwarebytes.com/blog/detections/riskware-coinminer,
    https://www.malwarebytes.com/blog/detections/riskware-bitcoinminer.
  - Malwarebytes also **blocks unmineable.com as a PUP domain** (seen on this machine).
  - Trend Micro classifies XMRig builds as PUA [Primary]:
    https://www.trendmicro.com/vinfo/us/threat-encyclopedia/malware/PUA.Linux.XMRig.AH.
- **Certification route.** AppEsteem certification. AV vendors consume its Deceptor list; its requirements (ACRs) are at
  https://customer.appesteem.com/Home/AppCertReqs (not read). Bright Data cites AppEsteem certification for its SDK
  (Bright Data trust centre, above).
- **What Otium should do.** Many of these Otium partly does already:
  - Code-sign the app (see `docs/code-signing.md`).
  - Submit releases to the Microsoft WDSI portal as a software developer (https://www.microsoft.com/wdsi/filesubmission).
  - Use a standard installer and uninstaller that removes the miners and the Proxybase client.
  - Show a persistent tray icon and status while mining or sharing.
  - Never hide from Task Manager. Don't run the miner windowless without the UI indicating it.
  - Autostart only by explicit opt-in.
  - Show a first-run screen that names XMRig, SRBMiner and Proxybase, and lists the fees (XMRig donation about 1%,
    SRBMiner dev fee, Otium 2%, unMineable 0.75% plus the 0.25% referral share Otium earns).
  - Don't use packers or obfuscation.
  - Seek AppEsteem certification.

---

## Risk assessment for Otium

| # | Risk | Likelihood / impact | Notes |
|---|---|---|---|
| 1 | **Internet sharing** exposes users to ISP termination and to abuse being attributed to their IP. Buyers are "No KYC" and Proxybase keeps no buyer records. A consent claim ("nothing on your computer is accessed") may be inaccurate given no LAN egress filter. | **High / High** | The biggest legal and reputational exposure. Google and the FBI in 2026 single out "earn from unused bandwidth" apps. |
| 2 | **Partner country breaches**: SideShift used by US users, ChangeNOW used by UK, India or Russia users (fund seizure), Relay or SideShift used from sanctioned countries; no country gating in the app beyond the US region check. | **High / Medium–High** | Mostly fixable in code. |
| 3 | **unMineable discretion**: one referral code on all workers, 6-month forfeiture, possible KYC, unknown operator and law, broad "US person" definition. | **Medium / Medium** | Get written confirmation of referral use and warn users about dormancy. |
| 4 | **AV / PUA classification**: cryptomining is a Defender enterprise PUA category; Malwarebytes already blocks unMineable; WinRing0 and hidden-PowerShell exclusion behaviours. | **High / Medium** | Affects distribution more than legality. |
| 5 | **SRBMiner licence**: closed source, "incorporating into another product" needs written permission. | **Low–Medium / Medium** | The author could object or rotate release assets. |
| 6 | **XMRig GPL** | **Low / Low** | No obligations while Otium neither conveys nor modifies. Keep the donate level. |
| 7 | **Regulatory status of Otium's fee on swaps** (MiCA, Relay §4.2(viii), EU/NO Russia art. 5b) | **Medium / High** | Covered in financial_regulation.md; counsel to decide. |
| 8 | **Mining-ban countries**: users in Algeria, China, Angola and others face criminal exposure; Otium may be seen as facilitating. | **Medium / Medium** | Geoblock. |
| 9 | **Undisclosed fee layers** (XMRig donation, SRBMiner dev fee, the referral share Otium earns from unMineable) | **Medium / Low–Medium** | Consumer-law transparency; see consumer_law_and_terms.md. |

## Questions for counsel
1. **unMineable.** Does putting Otium's referral code on every user's worker (Otium earns 0.25% of each user's rewards
   on top of a 2% fee slice) need unMineable's express consent, and must it be disclosed to users as a commission? Which
   law governs unMineable's terms when the operator is unnamed? Can Otium rely on users accepting unMineable's terms by
   clicking through Otium?
2. **US persons.** Is a Windows-region check enough to meet unMineable §2.4 ("US person" includes citizens abroad), or
   should Otium add a self-declaration and IP check? Is Otium itself at risk of being "for the benefit of" US persons
   if US users' installs share code paths?
3. **GPL.** Does Otium's automatic download of XMRig from the project's GitHub count as "conveying" or "making
   available" by Otium under Norwegian or EU copyright law? Would hosting a mirror change this?
4. **SRBMiner.** Is Otium's automated download-and-run "incorporating into another product"? Should Otium obtain written
   permission from doktor83 before launch?
5. **Swap fees.** Does an app fee on Relay swaps (and SideShift or ChangeNOW affiliate income) make Otium a CASP under
   MiCA as applied in Norway (reception and transmission of orders, or exchange), and therefore also a breach of Relay
   §4.2(viii)?
6. **Russia and Belarus.** Do the EU/Norway art. 5b-type prohibitions (19th/20th packages, forskrift 2014-08-15-1076)
   reach Otium's non-custodial features? Confirm the full block list and that IP plus region plus self-declaration
   gating is a reasonable control.
7. **Syria.** Since US and EU sanctions on Syria have largely been lifted, should Otium keep blocking Syria only because
   partners require it? Is there any remaining Norwegian restriction?
8. **Internet sharing liability.** Under Norwegian law, can Otium be liable (contributory, or under ekomloven or
   markedsføringsloven) for users breaching ISP terms, or for abusive traffic sent through users' IPs by Proxybase
   buyers? Should Otium require an indemnity from Proxybase, a written acceptable-use and KYC commitment, and LAN/SMTP
   egress blocking?
9. **Consent validity.** Is the proposed consent screen (section 4) enough for informed consent under the GDPR and the
   ePrivacy/ekom rules (the IP is personal data, and traffic passes through the user's terminal)? Should sharing be
   limited to 18+ with ISP-permission confirmation?
10. **Consumer law.** Can Otium shift ISP-breach risk to users (Honeygain-style "solely responsible") under Norwegian
    consumer law? Must Otium warn that Telia, Telenor and Ice terms prohibit resale?
11. **Mining-ban countries.** Is geoblocking the listed countries enough to avoid aiding or abetting liability? Which of
    the dated bans (Morocco, Qatar, Tunisia, Bangladesh, Kosovo) still apply?
12. **Dormancy.** Must Otium warn users about unMineable's 6-month forfeiture, and does Otium bear any duty if its app
    stops mining (an update or block) and users' balances are forfeited?
13. **Defender exclusion.** Is offering an automated Defender exclusion (UAC-gated) defensible, or does it create
    liability if a compromised update were later excluded from scanning?
14. **Proxybase as counterparty.** Who contracts with the user (Proxybase directly, or Otium as intermediary)? Proxybase's
    terms contain no seller terms. Should Otium require Proxybase to publish them before launch?
