# Otium: data protection obligations (research notes, checked 19 Oct 2026)

These are research notes for a lawyer to check. They are not legal advice. Confidence tags:
**[P]** = read in the primary source; **[S]** = secondary source only (law firm, tracker, vendor); **[I]** = my inference
or recommendation, not stated by any source. Facts about Otium come from the brief and from `docs/privacy.md`.

## 0. Bottom line

- **The pre-ticked "Help show what PCs really earn" box is the biggest problem.** The app reads hardware and usage data
  on the user's PC and sends it to the server for a purpose that isn't needed for the service. Under ePrivacy Art. 5(3)
  and ekomloven § 3-15 that needs prior consent at GDPR standard, and a pre-ticked box is not consent. Legitimate
  interest can't replace that consent. Fix: ship the box unticked (opt-in). [P, see §3.2]
- **Wallet addresses are personal data in Otium's hands**, because Otium links them to an account, a device, an IP and
  sometimes an email. EDPB Guidelines 02/2025 v2.0 (final, 7 July 2026) says so expressly. [P, §2]
- **Transfers:** the UK (AWS London) is covered by the renewed UK adequacy decision (to 27 Dec 2031, in force in the
  EEA since 1 May 2026). Cloudflare and GitHub are certified under the EU-US DPF. The DPF survived *Latombe* at first
  instance, but the appeal (C-703/25 P) is pending. The weak spot is **SideShift (St Kitts address) and ChangeNOW (Saint
  Vincent and the Grenadines)**: Otium's own server sends them the user's IP and address, and those countries have no
  adequacy decision. [P/S, §5]
- **Paperwork that is mandatory:** an Art. 30 record (the under-250 exemption doesn't apply, because the processing is
  not occasional), processor agreements, a breach log, and a documented DPIA screening. The Norwegian DPIA list
  arguably *requires* a DPIA here, because Otium collects data from third parties (pool APIs) and combines it with
  other data. A DPO is probably not required. [P/I, §7]
- **Set the minimum age at 18.** Norway's GDPR Art. 8 age is still 13 (a proposal to raise it to 15 is pending).
  But Norwegian minors can't make binding contracts, SideShift requires users to be 18, and Otium involves crypto and
  the user's electricity bill. [P, §8]

---

## 1. Applicable law and regulators

- **GDPR applies in Norway** through the EEA Agreement and personopplysningsloven § 1, which makes the GDPR Norwegian
  law. Datatilsynet is the supervisory authority (§ 20). — https://lovdata.no/dokument/NL/lov/2018-06-15-38 [P]
- **The cookie rule is now ekomloven § 3-15** (LOV-2024-12-13-76, in force 1 Jan 2025; last amended by LOV-2025-12-22-128
  from 1 Jul 2026). It bans storing information on, or gaining access to information in, a user's "kommunikasjonsutstyr"
  unless the user is informed (what is processed, why, and by whom) and has consented. *"Samtykke skal oppfylle kravene
  til samtykke i personvernforordningen."* The exceptions are (a) transmitting a communication and (b) what is
  *strictly necessary* to provide an information society service *the user has expressly requested*. Breaches of
  § 3-15(1) can be fined (overtredelsesgebyr, § 15-12). — https://lovdata.no/dokument/NL/lov/2024-12-13-76 [P]
  - **Superseded:** the old rule (ekomloven 2003 § 2-7b), under which browser settings could count as consent, no
    longer applies. [P: Datatilsynet page below]
  - **Split supervision:** Nkom decides whether a technology is in scope and whether an exception applies. Datatilsynet
    decides whether the information given is sufficient and whether the consent meets GDPR standards. The rule is
    technology-neutral, covers computers, and applies "uavhengig av om det er snakk om personopplysninger eller ikke"
    (whether or not personal data is involved). —
    https://www.datatilsynet.no/personvern-pa-ulike-omrader/internett-og-apper/cookies/ (last modified 27.01.2025) [P]
- **ePrivacy Directive Art. 5(3)** is the EU source of § 3-15. — https://eur-lex.europa.eu/eli/dir/2002/58/oj [P]
  **It has no GDPR one-stop-shop:** each EEA state's own Art. 5(3) law can apply to its residents, so Otium can't rely
  on Norway's regulator alone. — EDPB Opinion 5/2019 on the interplay between ePrivacy and GDPR,
  https://www.edpb.europa.eu/sites/default/files/files/file1/201905_edpb_opinion_eprivacydir_gdpr_interplay_en_0.pdf [P]
  In practice every EEA state requires consent for non-essential access, so one opt-in design satisfies them all. [I]
- **Norwegian e-commerce law:** ehandelsloven § 8 requires a provider of information society services to give its
  name, address, email, the business register it is in and its organisation number. This is relevant to the
  undecided legal entity. — https://lovdata.no/dokument/NL/lov/2003-05-23-35 [P]
- **UK users:** the UK GDPR and PECR apply to a non-UK controller that offers services to people in the UK (UK GDPR
  Art. 3(2)). — https://www.legislation.gov.uk/eur/2016/679/article/27 [P for Art. 27; Art. 3(2) I]. Since **5 Feb 2026**,
  the Data (Use and Access) Act 2025 s.112 has amended PECR reg. 6 to allow storage and access for *statistical*
  purposes without consent, if the user is told and can object. This is **UK only** and does not help Otium in the EEA.
  — https://www.wiggin.co.uk/insight/key-provisions-of-the-data-use-and-access-act-2025-come-into-force/ [S]
- **Pending EU reform (not law):** the Digital Omnibus, COM(2025) 836, would move device access involving personal data
  into a new GDPR Art. 88a. Art. 88a(3)(c) would add a consent exception for "aggregated information about the usage of
  an online service to measure the audience… solely for its own use". The EDPB and EDPS confirm that **"No such
  exceptions are provided under Article 5(3) ePrivacy Directive"** today. — EDPB-EDPS Joint Opinion 2/2026 (10 Feb
  2026), paras 97–103,
  https://www.edpb.europa.eu/system/files/documents/2026-02/edpb_edps_jointopinion_202602_digitalomnibus_en.pdf [P]
  Status: as of mid-September 2026 there was no Council general approach, and Parliament was "awaiting committee
  decision". A Council mandate vote was reported as planned for 11 Oct 2026, but I couldn't confirm that it happened.
  — https://gdprlocal.com/digital-omnibus-what-leaked-documents-say-about-ai-and-personal-data/ ;
  https://ppc.land/eu-council-draft-cuts-cookie-re-prompt-wait-from-6-months-to-4/ [S]. Even once adopted, it would
  need EEA incorporation before applying in Norway, and Otium's public website medians may not count as "solely for its
  own use". [I] **Don't design around it.**

## 2. What is personal data here

- **Wallet address:** "This identifier qualifies as personal data under Article 4 GDPR when it can be associated with
  an identified or identifiable natural person" (Glossary, para 137). Public keys qualify where individuals can be
  identified "by means reasonably likely to be used" (para 26). On-chain metadata "including… wallet addresses" may be
  personal data (fn 12). IP addresses seen when people interact through wallets or intermediaries are personal data
  too, even though they aren't on-chain (para 28). — EDPB Guidelines 02/2025 on blockchain, **v2.0 adopted 7 July
  2026** (v1.1 of 8 Apr 2025 is superseded),
  https://www.edpb.europa.eu/system/files/2026-07/edpb_guidelines_202502_blockchain_v2_en.pdf [P]
  - **What this means for Otium:** Otium stores the address next to an account id, a device id, last-seen times,
    server-log IPs and sometimes an email, so for Otium it is clearly personal data. Data being public on-chain doesn't
    take it outside the GDPR (*Satamedia*, C-73/07, publicly available data is still personal data,
    https://eur-lex.europa.eu/legal-content/EN/TXT/?uri=CELEX:62007CJ0073 [P: case exists; holding from memory]).
    Because the addresses come from the user, Art. 13 applies to them. Pool payout totals come from third-party APIs,
    so **Art. 14** applies to those (the notice must name the source). [I]
  - **Relative test:** whether data is personal is judged from the perspective of whoever holds it (*EDPS v SRB*,
    C-413/23 P, 4 Sep 2025, https://eur-lex.europa.eu/legal-content/EN/TXT/?uri=CELEX:62023CJ0413). On that test the
    pools and exchanges may also be processing personal data, and Otium certainly is. —
    https://www.cliffordchance.com/insights/resources/blogs/talking-tech/en/articles/2025/09/pseudonymized-data-after-edps-v-srb.html [S]
- **IP addresses** (logs, Cloudflare, pools, SideShift and ChangeNOW): dynamic IPs are personal data where there are
  legal means to identify the person (*Breyer*, C-582/14,
  https://eur-lex.europa.eu/legal-content/EN/TXT/?uri=CELEX:62014CJ0582) [P: case; holding well known]
- **Device id** (a hash of the machine id) and the hash of the device secret: these are pseudonymous identifiers and
  still personal data (GDPR recital 26). Compare *IAB Europe*, C-604/22: an identifier string combined with an IP is
  personal data. — https://eur-lex.europa.eu/legal-content/EN/TXT/?uri=CELEX:62022CJ0604 [P: case; I: analogy]
- **Earnings statistics** (GPU, CPU, hours, earnings, electricity, linked to device id): personal data while they are
  linked. Hours mined reveal when the user is away from the PC. [I]
- **Published medians over ≥3 computers:** probably anonymous, but three is a small group. A rare GPU and CPU pair, or
  a filter by country, can single someone out. The anonymisation reference is still WP29 Opinion 05/2014 (WP216),
  https://ec.europa.eu/justice/article-29/documentation/opinion-recommendation/files/2014/wp216_en.pdf [P]. It is
  **being replaced by** EDPB Guidelines 02/2026 on Anonymisation, which are a draft under consultation until 30 Oct
  2026 and test for isolation, linkage and inference from each recipient's perspective. —
  https://www.edpb.europa.eu/public-consultations/guidelines-022026-on-anonymisation_en [P: page];
  https://privacymatters.dlapiper.com/2026/08/eu-edpb-publishes-draft-guidelines-on-anonymisation/ [S]
- **Problem reports** contain miner logs, settings, antivirus names and the OS version. Windows paths such as
  `C:\Users\<name>\...`, hostnames and pool worker names can carry personal data even after addresses and keys are
  scrubbed. Antivirus product names are **not** special-category data (Art. 9). [I]

## 3. Lawful basis, purpose by purpose

The two regimes stack. **First** check ePrivacy/§ 3-15 for any storage on, or access to, the PC. **Then** find an
Art. 6 GDPR basis for what follows. When § 3-15 consent is needed, the follow-on processing for the same purpose
should rest on that same consent, not on legitimate interest. — EDPB Opinion 5/2019 (link above); EDPB-EDPS Joint
Opinion 2/2026 para 100 (same-purpose processing relies on the same consent) [P]

### 3.1 Does § 3-15 / Art. 5(3) reach a desktop app?

Yes. EDPB Guidelines 2/2023 on the technical scope of Art. 5(3), **v2.0 adopted 7 Oct 2024** (final; v1.0 of 14 Nov
2023 is superseded): https://www.edpb.europa.eu/our-work-tools/our-documents/guidelines/guidelines-22023-technical-scope-art-53-eprivacy-directive_en [P]
- Para 33: Art. 5(3) applies when the accessing entity "distributes software on the terminal equipment of the user
  that is stored and will then proactively call an… API endpoint over the network". [P]
- Para 36: this covers "customized software, regardless of who created or installed the protocols or software". [P]
- Para 44: information an installed app uses "strictly inside the terminal" is **not** access "as long as the
  information does not leave the device", but "when this information or any derivation… is accessed, Article 5(3)
  ePD would apply". [P]
- Para 53: locally produced information that is "sent back over the network to a server" is gaining access, and "the
  fact that this information is being produced locally does not preclude the application of Article 5(3)". [P]
- Para 45 names crypto-mining software. That paragraph is about malware, but it confirms that installing mining
  software is "storage" and that its later network reporting is "gaining of access". [P]
- Paras 55–56: the guidelines don't analyse the exceptions, and applying Art. 5(3) "does not systematically mean that
  consent needs to be collected". Use WP29 Opinion 04/2012 (WP194) on the cookie consent exemption for the
  strict-necessity test, applied **per purpose**:
  https://ec.europa.eu/justice/article-29/documentation/opinion-recommendation/files/2012/wp194_en.pdf [P]

Uncertainty: Nkom's application of § 3-15 to installed software has not been tested in any published Norwegian
decision I found. The Datatilsynet page is device-based and technology-neutral, which supports the EDPB reading. [I]

### 3.2 Table of purposes

| Purpose | § 3-15 / Art. 5(3) | GDPR Art. 6 basis | Notes |
|---|---|---|---|
| Install the app and store its settings, device secret and local stats | Storage is strictly necessary for the service the user requested (installing it is the request) → exception (b) | — (local only) | Para 44: local use isn't "access" [P] |
| Reading GPU/CPU, idle and full-screen state, antivirus via Security Center, **used locally** to run the miner | Not "access" while it stays on the device (para 44) | Otium is arguably not processing at all [I] | Say so in the notice for transparency |
| Account, worker id, invite code, payout coin and address, device id and secret hash, first/last seen | Sending device id and secret to authenticate = strictly necessary, exception (b) | 6(1)(b) contract; 6(1)(f) security | 6(1)(b) needs real Terms of Use. Necessity is judged objectively: EDPB Guidelines 2/2019, https://www.edpb.europa.eu/sites/default/files/files/file1/edpb_guidelines-art_6-1-b-adopted_after_public_consultation_en.pdf [P] |
| Optional email and sign-in codes | n/a | 6(1)(b) | |
| **Earnings statistics** (GPU, CPU, hours, earnings, electricity → server, a few times a day) | **Consent required.** Reading and sending this data is not strictly necessary for mining or paying the user. It serves Otium's marketing and transparency on the website | **6(1)(a) consent**, to match the § 3-15 consent | Pre-ticked = invalid. See below |
| Problem reports (sent by the user) | The user expressly asks for support, so exception (b) is defensible. Or treat the click as consent with clear information [I] | 6(1)(b) (support under the contract) or 6(1)(f) | Show what will be sent before sending; scrub usernames and paths |
| "What pools paid your address" (server reads pool public APIs twice a day) | No device access (server-side) | 6(1)(b) to show the user their own progress; 6(1)(f) for the site-wide sum | Art. 14 notice (source = pool APIs). Store only what the purpose needs |
| Invite checks (does the invitee mine? looked up by the invitee's address) | n/a | 6(1)(f), or 6(1)(b) for both parties if the referral terms are in the contract | Tell the **invitee** that the inviter sees whether they mine. Reveal only a yes/no, never amounts |
| Cloudflare CF-IPCountry → default electricity price | n/a (server-side header) | 6(1)(f) | Don't store it |
| Server logs | n/a | 6(1)(f), network security (recital 49) | Fix retention (§7.6) |
| Conversions via Relay / SideShift / ChangeNOW | n/a (server-side) | 6(1)(b) to pay the user | **Transfer problem** (§5) |
| Proxybase client (opt-in) | Installing and running it = storage and access. It's opt-in, so get § 3-15 consent at GDPR standard | 6(1)(a) | Proxybase is a separate controller, with a possible Fashion ID issue (§4) |
| GitHub update checks | Arguably strictly necessary, since security updates are part of the requested service [I]. The EDPB-EDPS support a no-consent rule for strictly necessary security updates *de lege ferenda* (JO 2/2026 para 103) [P] | 6(1)(f) | GitHub is an independent controller of the request logs |
| Electricity price APIs (hvakosterstrommen.no, elprisetjustnu.se), CoinGecko | n/a | 6(1)(f), the IP is incidental | All in the EEA, or no personal data |

**Earnings statistics in detail (Key question 1):**
- A pre-ticked box is not valid consent, either for Art. 5(3) or under the GDPR: *Planet49*, C-673/17, 1 Oct 2019. The
  same case holds that Art. 5(3) protects information whether or not it is personal data. —
  https://eur-lex.europa.eu/legal-content/EN/TXT/?uri=CELEX:62017CJ0673 [P: case]. GDPR recital 32 ("Silence,
  pre-ticked boxes or inactivity should not… constitute consent") and EDPB Guidelines 05/2020 on consent, v1.1 —
  https://www.edpb.europa.eu/our-work-tools/our-documents/guidelines/guidelines-052020-consent-under-regulation-2016679_en [P].
  Ekomloven § 3-15 and Datatilsynet both require "en aktiv handling" (an active act). [P]
- **Legitimate interest isn't a way around it.** Art. 5(3) has no legitimate-interest exception, and *Planet49* and
  Opinion 5/2019 rule out using Art. 6(1)(f) to get around a missing Art. 5(3) consent. [P/I]. Even on its own terms,
  6(1)(f) would be weak: a commercial interest can be legitimate (*KNLTB*, C-621/22, 4 Oct 2024,
  https://eur-lex.europa.eu/legal-content/EN/TXT/?uri=CELEX:62022CJ0621), but the Court pointed to asking for consent as
  a less intrusive alternative that undermines "necessity". —
  https://www.hsfkramer.com/notes/data/2024-posts/CJEU--Commercial-interest-may-be-considered-necessary-for-controller-s-legitimate-interest- [S]
  Also see *Meta v Bundeskartellamt*, C-252/21 (strict necessity),
  https://eur-lex.europa.eu/legal-content/EN/TXT/?uri=CELEX:62021CJ0252 [P: case], and the EDPB legitimate-interest
  guidelines 1/2024 (**still v1.0, consultation draft of 8 Oct 2024; I found no final version**),
  https://www.edpb.europa.eu/system/files/2024-10/edpb_guidelines_202401_legitimateinterest_en.pdf [P]
- **Data protection by default (Art. 25(2)):** only data necessary for each purpose should be processed by default.
  Pre-ticking an optional data flow conflicts with this too. — EDPB Guidelines 4/2019 v2.0,
  https://www.edpb.europa.eu/sites/default/files/files/file1/edpb_guidelines_201904_dataprotection_by_design_and_by_default_v2.0_en.pdf [P]
- **Options:** (a) **an unticked opt-in box on the welcome screen** (recommended), with the purpose, recipients
  ("published only as medians"), frequency and a withdrawal path in Settings. Log the consent: version of the text,
  timestamp, device id (Art. 7(1)). (b) Compute statistics only from data the server already holds lawfully, i.e. pool
  totals, which avoids § 3-15. But that data has no GPU or CPU model. (c) Waiting for Omnibus Art. 88a(3)(c) is
  speculative. [I]
- **UK users only:** the PECR statistics exemption (DUAA 2025) might cover an opt-out there, but one global opt-in is
  simpler. [S/I]

## 4. Roles: controller, processor, joint controller

- **Otium (the operator) is the controller** for the server data. It needs an identifiable legal person or individual.
  A sole trader (enkeltpersonforetak) would be the controller personally, and its name and address would have to be
  published (Art. 13(1)(a), ehandelsloven § 8). [P/I]
- **Processors (Art. 28 contract required):** AWS (Lightsail), Cloudflare (proxy, TLS termination, CF-IPCountry), the
  email/SMTP provider (to be decided), the backup destination behind rclone (to be decided). — EDPB Guidelines 07/2020 on
  controller and processor, v2.0,
  https://www.edpb.europa.eu/our-work-tools/our-documents/guidelines/guidelines-072020-concepts-controller-and-processor-gdpr_en [P]
  - AWS: the AWS GDPR DPA is part of the AWS Service Terms and "applies automatically" (since 25 May 2018). It includes
    SCCs. — https://docs.aws.amazon.com/whitepapers/latest/navigating-gdpr-compliance/aws-data-processing-addendum-dpa.html
    [P, but that page is marked "historical reference"; check the current Service Terms]
  - Cloudflare: Customer DPA v6.3 (20 June 2025) includes the EU SCCs as well as the DPF. —
    https://cf-assets.www.cloudflare.com/slt3lc6tev37/3LmXORq5FW5EuJ0OT1B871/f466268011407efbc07f4fadbd1af466/Cloudflare_Customer_DPA_v6.3_June_20__2025.pdf ;
    https://www.cloudflare.com/trust-hub/gdpr/ [S/P]. Confirm the DPA applies to the plan Otium uses (Free, Self-Serve). [I]
  - Email and backup: choose EEA or UK providers with a standard DPA. Encrypt backups client-side (rclone `crypt`) so
    the backup host sees only ciphertext. That lowers risk and breach exposure, but the backup host is still a
    processor. [I]
- **Independent controllers:** the mining pools, Relay, SideShift, ChangeNOW, Proxybase, GitHub, the electricity APIs.
  Each processes the data for its own purposes under its own terms. [I]
- **Joint-controller risk (Fashion ID, C-40/17):** a site that embeds a third-party component, which collects and
  transmits visitors' data, is a joint controller *for the collection and transmission* when both benefit. —
  https://eur-lex.europa.eu/legal-content/EN/TXT/?uri=CELEX:62017CJ0040 [P: case]. Otium configures the miner (pool,
  address, worker name, **Otium's referral code**) and bundles the Proxybase client, and it gains commercially from
  both. That makes joint controllership for the *transmission* step arguable, which would require an Art. 26
  arrangement that pools are unlikely to sign. [I, flag for counsel]

## 5. International transfers (Key question 3)

| Recipient | Location (as found) | Mechanism | Status, Oct 2026 |
|---|---|---|---|
| **AWS Lightsail, London** | UK | Adequacy | Renewed by **Commission Implementing Decision (EU) 2025/2574 of 19 Dec 2025** (amends 2021/1772), https://eur-lex.europa.eu/eli/dec_impl/2025/2574/oj [P: listing]. Runs until **27 Dec 2031**, with a review after 4 years: https://www.hunton.com/privacy-and-cybersecurity-law-blog/european-commission-renews-uk-data-adequacy-decisions [S]. In the **EEA by JCD 147/2026**, adopted 30 Apr 2026, in force **1 May 2026**: https://www.efta.int/node/306256 [P, via fetch]. **Superseded:** the 2021 decision as extended to 27 Dec 2025 by 2025/1226. Any AWS staff access from the US is covered by the SCCs in the DPA [P/S] |
| **Cloudflare** | US and global edge | EU-US DPF (+ UK extension) and SCCs as a fallback | Cloudflare says it relies on its DPF certifications and falls back to SCCs if they lapse: https://www.cloudflare.com/trust-hub/gdpr/ [S/P] |
| **GitHub** (update checks: IP) | US (GitHub, Inc.) / NL (GitHub B.V.) | DPF and SCCs | "GitHub also complies with the EU-U.S. Data Privacy Framework": https://docs.github.com/en/site-policy/privacy-policies/github-general-privacy-statement [P] |
| **DPF itself** | — | Implementing Decision (EU) 2023/1795; in the EEA via JCD 169/2024, https://eur-lex.europa.eu/eli/dec/2024/2568 [P] | Upheld by the General Court in ***Latombe v Commission*, T-553/23, 3 Sep 2025**, https://eur-lex.europa.eu/legal-content/EN/TXT/?uri=CELEX:62023TJ0553 [P: case]. **Appeal C-703/25 P** lodged 31 Oct 2025: https://digitalpolicyalert.org/event/35459-latombe-filed-appeal-against-general-court-dismissal-of-challenge-to-european-unionunited-states-data-protection-framework-adequacy-decision-in-latombe-v-commission [S]. Microsoft admitted as intervener in June 2026: https://www.dataguidance.com/news/eu-cjeu-admits-microsoft-intervener-appeal-concerning [S]. No judgment expected before 2027 [S]. The PCLOB has lacked a quorum since Jan 2025, which critics say weakens the DPF: https://cdt.org/insights/what-the-pclob-firings-mean-for-the-eu-us-data-privacy-framework/ [S]. **Advice:** rely on the DPF, but keep the SCC fallback in place (both DPAs above have it). Check certifications at https://www.dataprivacyframework.gov/list [I] |
| **SideShift** (LTC sharing payouts: address, amount, **IP sent by Otium's server**) | Paradigm Ltd, mailing address c/o IIC Management, Basseterre, **St Kitts** (country of incorporation not stated) | **None** | Terms: 18+, IP collected, St Kitts residents excluded: https://sideshift.ai/legal [P via fetch]. No adequacy decision → Art. 46 SCCs (unlikely to be offered) or Art. 49. Art. 49(1)(b) (necessary for the contract) is limited to **occasional** transfers under EDPB Guidelines 2/2018: https://www.edpb.europa.eu/our-work-tools/our-documents/guidelines/guidelines-22018-derogations-article-49-under-regulation_en [P: page]. Routine payout traffic is hard to call occasional [I] |
| **ChangeNOW** (XMR sharing payouts: same data) | **CHN Group LLC, Kingstown, Saint Vincent and the Grenadines** | **None** | Privacy policy names the entity, collects IP and device data, uses SumSub for KYC as an independent controller: https://changenow.io/privacy-policy [P via curl]. Same Art. 49 issue |
| **Relay** (all sharing payouts: address, amount, refund address) | Reservoir, a Delaware company per CB Insights [S]; relay.link legal pages blocked my fetch (HTTP 403/429) | Unknown (check the DPF list, ask for SCCs) | https://www.cbinsights.com/company/reservoir-2 [S] |
| **unMineable** (IP, address, worker) | **Not found.** The site refused TLS from my environment and searches gave nothing | ? | Ask unMineable for the entity, country and privacy notice |
| **MoneroOcean** | **Not found.** Seems to be run by an individual or a small team | ? | Same |
| **Proxybase** (opt-in: IP, relayed traffic, sharing wallet) | Terms: "operated by PROXYBASE XYZ LLC" (state not given; "LLC" suggests the US) [P via curl, https://proxybase.xyz/terms]. **A different site, proxybase.org, has a separate GDPR-style policy (19 Mar 2026) and no entity named.** Confirm which one is Otium's partner | ? | |
| Electricity APIs, CoinGecko | NO/SE; CoinGecko gets no personal data | n/a | — |

**Is a pool connection a "transfer" by Otium?** On EDPB Guidelines 05/2021 (Art. 3 vs Chapter V, v2.0), a data
subject disclosing data directly to a non-EEA recipient is not a transfer by the controller. —
https://www.edpb.europa.eu/system/files/2023-02/edpb_guidelines_05-2021_interplay_between_the_application_of_art3-chapter_v_of_the_gdpr_v2_en_0.pdf [P].
But in ***Bindl v Commission*, T-354/22 (8 Jan 2025)**, the Commission was held responsible for an IP address
transfer to Meta (US) triggered by a "Sign in with Facebook" link on its site, and paid €400 damages. —
https://eur-lex.europa.eu/legal-content/EN/TXT/?uri=CELEX:62022TJ0354 [P: case];
https://www.hunton.com/privacy-and-cybersecurity-law-blog/cjeu-orders-the-european-commission-to-pay-damages-for-data-transfers-to-the-u-s [S].
Appeals are pending (Bindl, C-211/25 P, and reportedly the Commission) [S]. Otium's app starts the connections to the
pools and Proxybase itself. Server-side calls to SideShift and ChangeNOW are Otium's own transfers with no real
doubt. [I]

## 6. Transparency: Art. 13/14 privacy notice (Key question 4)

Required content (Art. 13(1)–(2); Art. 14 adds categories of data and the source):
https://eur-lex.europa.eu/eli/reg/2016/679/oj [P]. Layering and in-app timing follow WP260 rev.01:
https://ec.europa.eu/newsroom/article29/items/622227 [P]
1. Controller identity and contact details (the legal entity is undecided: **placeholder**). EU and UK representative if
   needed (§9). DPO contact if one is appointed.
2. Purposes **and** the legal basis for each. For 6(1)(f), the specific legitimate interest (fraud and abuse
   prevention, security, the website total).
3. Recipients or categories of recipients: name each pool, exchange, Relay, Proxybase, GitHub, Cloudflare, AWS, email
   and backup providers. *SRB* says this is judged from the controller's viewpoint at collection. [S]
4. Third-country transfers and the safeguard for each (table §5), and how to get a copy.
5. Retention period, or the criteria for it, per category (§7.6).
6. Rights: access, rectification, erasure, restriction, portability, **objection (Art. 21, stated separately and
   prominently)**, withdrawing consent, and complaining to Datatilsynet (or the user's own regulator).
7. Whether providing the data is a contractual requirement (payout address yes, email no) and what happens if it's
   withheld.
8. No automated decisions with legal effects (say so).
9. Art. 14 items: categories and **source** (the pools' public APIs) for "what pools paid your address" and for the
   invitee mining check.
10. The § 3-15 information at the point of consent (what is read from the PC, why, who receives it), shown on the
    welcome screen next to the **unticked** box.

**Gaps in `docs/privacy.md`:** placeholders for the controller, email provider, host and country, log and backup
periods, and code-hash retention. It still lists **Honeygain and Mysterium**, which the brief doesn't mention
(confirm whether they are still shipped). It has no transfer section, no separate Art. 21 objection statement, no
Art. 14 source statement, and it treats the pools as separate controllers without considering Fashion ID. It also
gives Proxybase's domain as proxybase.xyz (check this against the partner's contract). [P: file]

## 7. Accountability obligations

### 7.1 Records of processing (Art. 30)
The Art. 30(5) exemption for organisations under 250 employees falls away if the processing is "not occasional",
likely to result in a risk, or involves Art. 9/10 data. Otium's processing is continuous, so a record **is
required**. Datatilsynet: "I praksis og nesten uten unntak, skal derfor alle virksomheter føre protokoller" (in
practice, almost without exception, every business must keep records). —
https://www.datatilsynet.no/rettigheter-og-plikter/virksomhetenes-plikter/protokoll-over-behandlingsaktiviteter/ [P]

### 7.2 DPIA (Art. 35)
- Norway's Art. 35(4) list (approved by the EDPB) **always requires** a DPIA for, among other things, *"Personopplysninger
  samlet inn via en tredjepart i følge med minst ett annet kriterium"* (personal data collected via a third party,
  combined with at least one other criterion), *"…innovativ teknologi i følge med minst ett annet kriterium"* (new
  technology plus one other criterion) and large-scale data collection through the Internet of Things. —
  https://www.datatilsynet.no/rettigheter-og-plikter/virksomhetenes-plikter/vurdering-av-personvernkonsekvenser/nar-ma-man-gjennomfore-en-vurdering-av-personvernkonsekvenser/ ;
  list (EN): https://www.datatilsynet.no/globalassets/global/dokumenter-pdfer-skjema-ol/regelverk/veiledere/dpia-veileder/dpialist280119.pdf [P]
- The criteria come from WP248 rev.01 (endorsed by the EDPB): https://ec.europa.eu/newsroom/article29/items/611236 [P].
  Relevant ones for Otium [I]: **matching or combining datasets** (pool API data + device + account + IP); **highly
  personal data** (financial: earnings and wallet links; WP248 counts financial data); **systematic monitoring**
  (idle and hours patterns, light); and possibly **innovative technology** (crypto payouts, bandwidth sharing).
  Otium collects pool payout data "via a third party" plus at least one of these, so on a plain reading of the
  Norwegian list a DPIA is required. **Recommendation:** write a short DPIA (5–10 pages). It is cheap, and Datatilsynet
  will ask for it. [I]
- The blockchain guidelines v2.0 §4.9 also recommend DPIAs for blockchain-based processing. [P: TOC]

### 7.3 DPO (Art. 37)
Mandatory only if the core activities involve **large-scale**, regular and systematic monitoring, or large-scale
special-category data. — WP243 rev.01, https://ec.europa.eu/newsroom/article29/items/612048 [P]. At launch scale Otium
is very unlikely to qualify. Look again if the earnings statistics grow to many thousands of devices. Personopplysningsloven
§ 19 lets the government add DPO duties by regulation; I found none that affects Otium [P: heading; I: none found].
Datatilsynet: https://www.datatilsynet.no/rettigheter-og-plikter/virksomhetenes-plikter/personvernombud/ [P: page]

### 7.4 Processor agreements (Art. 28)
AWS (automatic, through the Service Terms); Cloudflare (DPA v6.3, confirm it covers the plan); the **email provider**
(to be decided: pick one with an EEA or UK data location and a click-through DPA); the **rclone backup target** (to be
decided: same, and record its location in the Art. 30 record). Without a DPA, using these processors breaches
Art. 28(3). [P/I]

### 7.5 Security and breach notification
- Art. 32: appropriate measures. The existing hashed secrets and codes are good. Missing pieces [I]: log rotation, a
  documented admin access policy, backup encryption, a patching cadence.
- Art. 33: notify Datatilsynet **within 72 hours** unless the breach is unlikely to result in a risk. Art. 34: tell the
  users if the risk is high. **Art. 33(5): log every breach**, including ones not notified. — EDPB Guidelines 9/2022 on
  breach notification v2.0 (28 Mar 2023):
  https://www.edpb.europa.eu/system/files/2023-04/edpb_guidelines_202209_personal_data_breach_notification_v2.0_en.pdf [P].
  Example for Otium [I]: a database leak that links wallet addresses to emails or IPs is likely high risk
  (deanonymises on-chain finances and enables targeted phishing) → tell the users as well.
- If the controller has **no EEA establishment**, the one-stop-shop doesn't apply, and breaches must be notified to
  every supervisory authority whose residents are affected (Guidelines 9/2022). [P]

### 7.6 Retention: recommended concrete periods
The principle is Art. 5(1)(e). The periods below are recommendations from common practice and do not come from any
source unless one is cited. [I]

| Data | Recommended | Basis / practice |
|---|---|---|
| Container and app logs with IPs (Docker json-file) | **14 days** (30 at most). Set `log-opts` `max-size`/`max-file` **and** a time-based purge, or ship to journald with `MaxRetentionSec=14d`. Today there is no rotation, so logs grow without limit, which is a storage-limitation breach and a disk-full risk | Common practice for request logs is 7–30 days. Keep longer only for a live incident |
| Admin and audit logs (SSH, database admin, deploys) | **6–12 months** | The CNIL logging recommendation gives 6 months to 1 year for access logs of authorised users, up to 3 years if justified: https://www.cnil.fr/fr/la-cnil-publie-une-recommandation-relative-aux-mesures-de-journalisation [P] (French DPA, persuasive only) |
| Database backups (rclone) | **14 days rolling** (current setup is fine), encrypted, EEA or UK location. Tell users deleted data leaves the backups within 14 days. Re-apply deletions after any restore | ICO: put backup data "beyond use" and be clear with individuals: https://ico.org.uk/for-organisations/uk-gdpr-guidance-and-resources/individual-rights/individual-rights/right-to-erasure/ [P] (UK regulator, persuasive) |
| Raw earnings-statistics rows linked to a device id | Aggregate, then delete the raw rows after **90 days**, or 13 months if trends are needed. Keep only anonymous aggregates | Minimisation [I] |
| Problem reports | **90 days after the issue is closed** (6 months at most) | [I] |
| Used or expired sign-in code hashes | **24 hours to 30 days** | [I] |
| "What pools paid" snapshots | Keep only the current total + 30 days (as now). Delete with the account | [I] |
| Inactive accounts (no device contact) | Delete or anonymise after **24 months**, with a warning email first if one is on file | [I] |
| Consent records (statistics opt-in and opt-out) | For the life of the account + **3 years** (Norwegian general limitation period) to show compliance with Art. 7(1) | [I] |
| Breach log (Art. 33(5)) | **5 years** | [I] |
| Fee and referral bookkeeping | **5 years** (bokføringsloven § 13, primary documentation) | https://lovdata.no/dokument/NL/lov/2004-11-19-73 [I, verify] |

## 8. Children (Key question 5)

- Norway: *"Aldersgrensen er 13 år for samtykke etter personvernforordningen artikkel 6 nr. 1 bokstav a"* (the age
  limit is 13 for consent under GDPR Art. 6(1)(a)), personopplysningsloven § 5. —
  https://lovdata.no/dokument/NL/lov/2018-06-15-38 [P]. Art. 8 only bites where **consent** is the basis. For Otium
  that is the statistics opt-in and the Proxybase opt-in.
- **Pending:** the Justice Ministry consultation of 7 Oct 2025 proposes raising the age to **15**, and applying it to
  providers established anywhere when the child is in Norway (TRIS notification 2025/9028/NO, received 18 Sep 2025).
  The law would come into force when the King decides. Lovdata shows no amendment to § 5 yet (last amended 2021). —
  https://technical-regulation-information-system.ec.europa.eu/da/notification/27273/print [P];
  https://www.mediebedriftene.no/siteassets/dokumenter/horinger/2025/2025-10-07-horing-felles-endringer-i-personopplysningsloven-aldersgrense-for-barn.pdf [P: consultation response].
  A separate social-media age-limit law (access from 1 January of the year a child turns 16) was in the EEA
  consultation as of Aug 2026. It targets platforms, not Otium. — https://www.regjeringen.no/no/aktuelt/positiv-tilbakemelding-fra-eu-til-ny-lov-om-sosiale-medium/id3170219/ [S]
- **Why 18+ is the right setting** [I]:
  - Norwegian minors "kan ikke selv foreta rettslige handlinger eller råde over sine midler" (can't make legal
    transactions or manage their own money; vergemålsloven § 9). From 15 they may manage money earned by their own
    work (§ 12), but accepting Terms and running crypto payouts sits awkwardly with that. —
    https://lovdata.no/dokument/NL/lov/2010-03-26-9 [P]
  - SideShift requires 18+ (https://sideshift.ai/legal) [P]. unMineable's and MoneroOcean's terms couldn't be checked.
  - Mining uses the household's electricity, which is often the parents' contract.
  - Recommendation: 18+ in the Terms, a self-declaration on the welcome screen, and no hard age verification (that
    would be disproportionate; EDPB Statement 1/2025 on age assurance:
    https://www.edpb.europa.eu/our-work-tools/our-documents/statements/statement-12025-age-assurance_en [P: page]).
    Delete accounts reported as belonging to a child.

## 9. Representatives (Key question 6, brief)

- If the controller is **outside the EEA** and offers services to people in the EEA (Art. 3(2)(a)), it must appoint an
  **EU/EEA representative** under Art. 27 in a member state where users are. The exemption in Art. 27(2) requires
  *occasional* processing, which Otium's isn't, so it wouldn't apply. — EDPB Guidelines 3/2018 on territorial scope
  v2.1,
  https://www.edpb.europa.eu/sites/default/files/files/file1/edpb_guidelines_3_2018_territorial_scope_after_public_consultation_en_1.pdf [P]
  A non-EEA controller also loses the one-stop-shop.
- **Even with a Norwegian operator:** if Otium *targets* UK users, UK GDPR Art. 27 requires a **UK representative**. A
  London server alone probably doesn't make a UK establishment (EDPB 3/2018 calls for "stable arrangements"; the UK
  view is untested) [I]. — https://www.legislation.gov.uk/eur/2016/679/article/27 [P]
- Mere accessibility of the site is not "targeting". Pointers to targeting include English-only marketing aimed at the
  UK, a UK electricity-price default, GBP display, and UK-specific copy (EDPB 3/2018). [P/I]

## 10. Superseded or pending material

- EDPB blockchain guidelines **v1.1 (Apr 2025) → v2.0 (7 Jul 2026)**, with few substantive changes. [P]
- EDPB Art. 5(3) guidelines **v1.0 (Nov 2023) → v2.0 (7 Oct 2024)**. [P]
- UK adequacy: **2021/1772 (expired 27 Jun 2025) → extended to 27 Dec 2025 by 2025/1226 → renewed by 2025/2574
  (to 27 Dec 2031)**. In the EEA from 1 May 2026. [P/S]
- Ekomloven 2003 § 2-7b (browser settings could count as consent) → **ekomloven 2024 § 3-15** (consent at GDPR standard). [P]
- WP29 Opinion 05/2014 on anonymisation → **draft EDPB Guidelines 02/2026** (consultation to 30 Oct 2026). Not final. [P/S]
- EDPB legitimate-interest guidelines 1/2024: **still v1.0** (draft). No final version found. [P]
- EDPB pseudonymisation guidelines 01/2025: a final version may exist, but I couldn't confirm a date [S].
- Pending, not in force: the Digital Omnibus (Art. 88a/88b GDPR), the Norway age limit going to 15, the *Latombe*
  appeal C-703/25 P, the *Bindl* appeals.
- The PCLOB quorum problem (since Jan 2025) is unresolved as far as I found. [S]

## 11. Concrete fixes for Otium (priority order) [I]

1. **Make "Help show what PCs really earn" unticked by default.** Show what is sent, how often, and that only medians
   are published. Store a consent record (text version, timestamp). Offer withdrawal in Settings, and when consent is
   withdrawn, stop sending **and** delete that device's raw statistics rows.
2. **Move the SideShift and ChangeNOW calls off the server or out of scope.** Either have the client call them
   directly (smaller transfer argument, but *Bindl* risk), or get SCCs, or use only Relay (if it has DPF or SCCs) and
   EEA-based converters. At minimum tell users, in the moment, that their IP goes to a St Kitts or SVG company before
   they pick LTC or XMR sharing payouts. As a stopgap, consider Art. 49(1)(a) explicit consent covering the risks, but
   the EDPB treats it as for occasional use.
3. **Logs:** turn on Docker log rotation (`max-size`/`max-file`) plus a 14-day time cap. Avoid logging full IPs where
   possible: truncate them or rely on Cloudflare's `CF-Connecting-IP` only for rate limiting.
4. **Sign or confirm DPAs** with AWS, Cloudflare, the email provider and the backup host. Encrypt backups with rclone
   `crypt` and put them in the EEA or UK.
5. **Write the Art. 30 record, a short DPIA, a breach log and a retention schedule** (§7.6).
6. **Invitee transparency:** tell invitees, when they join, that their inviter will see whether they mine. Show only a
   yes/no.
7. **Problem reports:** a preview-before-send screen. Scrub Windows usernames, profile paths, hostnames, worker names
   and IPs. Delete reports 90 days after closing.
8. **Publication threshold:** raise the minimum group from 3 to ≥5–10 computers. Suppress rare GPU/CPU combinations.
   Never publish per-country slices below the threshold.
9. **Age:** 18+ in the Terms and on the welcome screen.
10. **Terms of Use:** the contract that 6(1)(b) depends on (referral share, fee, invite rules).
11. **Privacy notice:** fill the placeholders, add the transfer table, a separate Art. 21 objection section and the
    Art. 14 sources. Remove Honeygain and Mysterium if they're no longer shipped. Link it from the welcome screen and
    the website footer.
12. **Website (otium.lol):** check that it sets no non-essential cookies or analytics without consent. Cloudflare's own
    bot cookies count as strictly necessary security [I]. The ehandelsloven § 8 operator details must appear there.

## 12. Decisions and placeholders the operator must fill in

- Legal entity (AS or enkeltpersonforetak, Norway or elsewhere), organisation number, address, privacy contact email.
- Whether to appoint a DPO voluntarily, or just name a privacy contact.
- Email/SMTP provider (name, country, DPA) and the backup provider behind rclone (name, country, DPA, encryption).
- Retention periods: logs, backups, statistics, problem reports, code hashes, inactive accounts (proposals in §7.6).
- The statistics design: opt-in wording, publication threshold, raw-data lifetime.
- Conversion partners: keep SideShift and ChangeNOW? If so, the transfer mechanism; Relay's entity and DPF/SCC status.
- The entities and countries of unMineable, MoneroOcean and Proxybase (.xyz vs .org).
- Whether Honeygain and Mysterium are still in the product.
- Whether Otium targets the UK (→ UK representative, PECR).
- Minimum age (recommended 18) and how it is stated.
- The Terms of Use text.
- The supervisory authority for complaints (Datatilsynet if established in Norway).
- Breach response owner and contact path. Datatilsynet's breach report form (avviksmelding) is filed through Altinn [I, from memory, not checked].

---

## Risk assessment for Otium

| Issue | Likelihood of challenge | Impact | Rating |
|---|---|---|---|
| Pre-ticked statistics box (§ 3-15 / Art. 5(3) + Art. 6/7/25) | High: easy to spot, clear *Planet49* precedent, Datatilsynet active on tracking | Fine under ekomloven § 15-12 and/or GDPR Art. 83; reputational harm ("mining app collects data by default") | **High → fixed by opt-in** |
| Server-side transfers of IP and address to SideShift (St Kitts) and ChangeNOW (SVG) with no safeguard | Medium | Breach of Chapter V; damages claims possible (*Bindl*: €400 per user) | **High-medium** |
| Unbounded Docker logs with IPs | Medium (shows up in any audit) | Breach of storage limitation | **Medium → easy fix** |
| No Art. 30 record, DPIA, or DPAs with the email and backup providers | Medium (asked for first in any inquiry) | Art. 83(4) fines; weak defence | **Medium** |
| DPF invalidated by the CJEU on appeal (Cloudflare, GitHub) | Low-medium for 2027–28 | Need to fall back to SCCs (already in the DPAs) | **Low** |
| Fashion ID joint controllership with pools and Proxybase | Low-medium (theory-heavy) | Need for Art. 26 arrangements; shared liability for partners' processing | **Medium-low** |
| Re-identification from medians over 3 computers | Low | Statistics count as personal data → needs a lawful basis anyway (consent covers it) | **Low** |
| Children (13 now, maybe 15 later) | Low if 18+ | Invalid consent, contract capacity | **Low with 18+** |
| UK representative missing (if the UK is targeted) | Low | ICO enforcement is unlikely at this scale | **Low** |
| Wallet addresses treated as non-personal | — | Removed by recognising them as personal data in the notice and the record | **Low once documented** |

Overall: **medium**, dominated by two fixable items (the opt-in and the SideShift/ChangeNOW transfers). With the §11
fixes, the remaining risk is low for an app of this size. [I]

## Questions for counsel

1. Does Nkom or Datatilsynet accept that ekomloven § 3-15 applies to **installed desktop software** sending
   hardware and usage data (EDPB 2/2023 paras 33, 44, 53)? Is there any Norwegian practice on this?
2. Can the user's click to send a **problem report** be treated as the § 3-15(2)(b) exception (a service expressly
   requested), or is separate consent wording needed?
3. Are **GitHub update checks** (and the app's version and OS data in them) strictly necessary under § 3-15, or
   should they be on by default with an off switch?
4. **Controllership:** given Otium's referral code and its configuration of the miner, is Otium a joint controller
   with unMineable and MoneroOcean (and with Proxybase) for the transmission step under *Fashion ID*? If so, what is a
   workable Art. 26 arrangement?
5. **Transfers:** is a client-initiated connection to a non-EEA pool or exchange a Chapter V transfer by Otium (EDPB
   05/2021 v *Bindl*)? For the server-side SideShift and ChangeNOW calls, is there any route other than SCCs, for
   example Art. 49(1)(b) for each payout?
6. Do the **pool-payout lookups** (data from a third party) together with the combined account, device and IP data
   trigger the Norwegian DPIA list's "third party + one criterion" rule, as I read it?
7. Is **6(1)(f)** enough for the **invitee mining check** disclosed to the inviter, or is the referral relationship
   part of the contract with both users (6(1)(b))?
8. What is the right **legal-entity form** for GDPR and ehandelsloven § 8 purposes? Can a sole trader avoid publishing
   a home address?
9. Is Otium **targeting the UK** (UK GDPR Art. 3(2) and Art. 27, PECR)? Does a London server create a UK
   establishment? Does a non-UK controller owe the ICO data protection fee?
10. Does the Norwegian Art. 8 age, if raised to 15, matter given the planned **18+** Terms, and is self-declaration
    enough?
11. Will the **Digital Omnibus** Art. 88a(3)(c) "audience measurement" exception, if adopted and brought into the EEA,
    cover public website medians? (I think it won't: the publication isn't "solely for its own use".)
12. What do the **retention periods** in §7.6 need to be, especially the 14-day logs and backups, and the 3-year
    consent records?
