# Consumer protection, marketing law and Terms of Service for Otium

Research notes for a lawyer to check later, current as of 19 October 2026. This is not legal advice. Each claim has a
source after it. Items marked **[verify]** are uncertain or come from secondary sources. Items marked **[superseded]**
are no longer current.

Assumed facts: a Norwegian operator (EEA), consumers worldwide, a free Windows app (macOS/Linux in beta). It mines to
the user's own wallet. Otium earns from 2% of mining time, a pool referral code, and 2% on converting optional
internet-sharing earnings. Updates install automatically (Velopack), the app isn't code-signed, and there are no
Terms of Service yet.

---

## 0. Status of the legal framework (October 2026)

| Instrument | EU status | Norway (EEA) status |
|---|---|---|
| UCPD 2005/29/EC, as amended by the Omnibus Directive 2019/2161 | In force; Omnibus changes apply from 28 May 2022 | Implemented in markedsføringsloven (Marketing Control Act) §§ 6–9 and the blacklist regulation FOR-2009-06-01-565. Omnibus implemented by LOV-2023-06-16-38, in force **1 Oct 2023** |
| EmpCo Directive 2024/825 (green claims, software-update blacklist items) | Applies from **27 Sep 2026** | Prop. 38 LS (2025–2026) passed by the Storting on 11 Jun 2026 (it seems to be lov 19. juni 2026 nr. 34 [verify]). In force "no earlier than the first half of 2027", once the EEA Committee decides |
| CRD 2011/83 art. 11a "withdrawal button" (from Dir. 2023/2673) | Applies from **19 Jun 2026** | New angrerettloven (Right of Withdrawal Act) § 20 a in Prop. 38 LS. Entry into force may be postponed until 2023/2673 is in the EEA Agreement |
| Digital Content Directive 2019/770 | Applies from 1 Jan 2022 | digitalytelsesloven (Digital Services Act), LOV-2022-06-17-56, in force **1 Jan 2023** (the brief's "2024?" is wrong) |
| Unfair Terms Directive 93/13 | In force | avtaleloven (Contracts Act) §§ 36–37 and markedsføringsloven § 22 |
| E-Commerce Directive 2000/31 art. 5 | In force | e-handelsloven (E-commerce Act) § 8 |
| ODR Regulation 524/2013 (platform link duty) | **[superseded]**. Repealed by Reg. 2024/3228 with effect from 20 Jul 2025; no complaints after 20 Mar 2025 | markedsføringsloven § 10 a amended by LOV-2025-06-20-41 (in force 1 Jul 2025) |
| New ADR Directive 2025/2647 | In force since 19 Jan 2026; transposition by 20 Mar 2028 | Not yet |
| European Accessibility Act 2019/882 | Applies from 28 Jun 2025 | **Not part of the EEA Agreement yet** ("under scrutiny") |
| Product Liability Directive 2024/2853 | Transposition by **9 Dec 2026**; covers products placed on the market after that date (one corrigendum suggests 8 Dec [verify]) | **Not part of the EEA Agreement yet** ("under scrutiny"). The produktansvarsloven (Product Liability Act) of 1988 still applies |
| Cyber Resilience Act 2024/2847 | Reporting duties from 11 Sep 2026; main duties from 11 Dec 2027 | EEA status not checked [verify] |
| MiCA 2023/1114 | Fully applicable since 30 Dec 2024 | kryptoeiendelsloven (Crypto-Assets Act), lov 27. mai 2025 nr. 20, in force 1 Jul 2025 |
| Digital Fairness Act | Proposal expected "autumn 2026". No adoption found as of 19 Oct 2026 [verify] | n/a |

Sources:
- UCPD (consolidated) https://eur-lex.europa.eu/eli/dir/2005/29/oj/eng
- Omnibus https://eur-lex.europa.eu/eli/dir/2019/2161/oj/eng
- Norwegian Omnibus act, Storting case (Prop. 50 LS (2022–2023), Lovvedtak 80, in force 01.10.2023) https://stortinget.no/no/Saker-og-publikasjoner/Saker/Sak/?p=93401
- markedsføringsloven https://lovdata.no/dokument/NL/lov/2009-01-09-2
- Blacklist regulation https://lovdata.no/dokument/SF/forskrift/2009-06-01-565
- EmpCo https://eur-lex.europa.eu/eli/dir/2024/825/oj/eng
- Prop. 38 LS (2025–2026), see pp. 9 and 15 on § 20 a and possible postponement https://www.regjeringen.no/contentassets/53ab7f9b55ed4aec8081c1de9ef403c0/no/pdfs/prp202520260038000dddpdfs.pdf
- CMS EmpCo tracker for Norway (adopted 11 Jun 2026; earliest first half of 2027) https://cms.law/en/int/expert-guides/cms-implementation-tracker-for-the-empco-directive/norway
- Dir. 2023/2673 https://eur-lex.europa.eu/eli/dir/2023/2673/oj/eng
- digitalytelsesloven https://lovdata.no/dokument/NL/lov/2022-06-17-56
- ODR repeal https://eur-lex.europa.eu/eli/reg/2024/3228/oj/eng
- ADR 2025/2647 summary https://eapil.org/2026/01/23/directive-eu-2025-2647-amending-the-adr-framework-for-consumer-disputes/
- EAA in EEA-lex https://www.efta.int/eea-lex/32019l0882
- PLD in EEA-lex https://www.efta.int/eea-lex/32024l2853
- PLD date discrepancy https://www.reedsmith.com/articles/eu-product-liability-directive-where-things-stand-with-member-state-transposition/
- CRA dates https://www.mccannfitzgerald.com/knowledge/data-privacy-and-cyber-risk/cyber-resilience-act-reporting-obligations-apply-from-11-september-2026
- Norway MiCA https://schjodt.com/news/norway-mica-fully-implemented-transitional-period-prologned
- Finanstilsynet hearing note (Lov 27. mai 2025 nr. 20, in force 1 Jul 2025) https://www.finanstilsynet.no/contentassets/dcd8c37636a2452087ea0dd3f1a28eb8/horingsnotat-om-overgangsregelen-i-kryptoeiendelsloven.pdf
- DFA timing https://iapp.org/news/a/european-commissioner-mcgrath-discusses-digital-simplification-upcoming-digital-fairness-act-proposal

Other changes pending in Norway: markedsføringsloven §§ 7–8 and angrerettloven §§ 2, 5, 8, 16 and 20 carry an
"Endres ved lov 19 juni 2026 nr. 34 (i kraft fra den tid Kongen bestemmer)" note on Lovdata, meaning they change on a
date the King sets. Check again before launch. https://lovdata.no/dokument/NL/lov/2014-06-20-27

---

## 1. Marketing claims: UCPD and markedsføringsloven

### 1.1 Legal tests

- **General ban.** Unfair commercial practices are prohibited (UCPD art. 5; mfl. § 6). Misleading actions (art. 6;
  mfl. § 7), misleading omissions (art. 7; mfl. § 8) and aggressive practices (arts. 8–9; mfl. § 9) are always unfair
  (mfl. § 6(4)). https://lovdata.no/dokument/NL/lov/2009-01-09-2
- **Results of use.** "Main characteristics" include "the results to be expected from its use" (UCPD art. 6(1)(b);
  mfl. § 7(1)(b)). Earnings and "never costs" claims fall here. https://eur-lex.europa.eu/eli/dir/2005/29/oj/eng
- **Substantiation in Norway.** Factual claims "skal kunne dokumenteres", and the documentation must be in the
  advertiser's hands when the marketing runs (mfl. § 3). That means proof must exist before the claim goes out, not
  after a complaint. https://lovdata.no/dokument/NL/lov/2009-01-09-2
- **Substantiation in the EU.** Authorities may require proof of factual claims, and the burden is on the trader (UCPD
  art. 12; Commission UCPD Guidance 2021, § 3.3). https://eur-lex.europa.eu/legal-content/EN/TXT/?uri=CELEX:52021XC1229(05)
- **Transactional decision.** This includes pre-purchase steps such as deciding to download or start, not only paying
  (CJEU C-281/12 *Trento Sviluppo*). https://curia.europa.eu/juris/liste.jsf?num=C-281/12
- **Average consumer.** This consumer may be affected by cognitive biases, so prominent claims can mislead even when
  the fine print is correct (CJEU C-646/22 *Compass Banca*, 14 Nov 2024 [verify citation]).
  https://curia.europa.eu/juris/liste.jsf?num=C-646/22
- **Splitting cost components.** Showing an attractive figure up front and the cost components elsewhere can mislead
  (CJEU C-611/14 *Canal Digital Danmark*). https://curia.europa.eu/juris/liste.jsf?num=C-611/14
- **Sanctions in Norway.** Forbrukertilsynet (the Consumer Authority) can issue bans, coercive fines (§ 41) and
  infringement fines (§ 42) for § 6 breaches and blacklist breaches. Material breaches of §§ 7–8 can also carry
  criminal liability (§ 48). A new § 48 c gives harmed consumers individual remedies (damages, price reduction,
  rescission), in force 1 Oct 2023. https://lovdata.no/dokument/NL/lov/2009-01-09-2
- **Sanctions in the EU.** In coordinated CPC actions, fines go up to at least 4% of annual turnover, or EUR 2 million
  where turnover is unknown (UCPD art. 13(3), inserted by the Omnibus). https://eur-lex.europa.eu/eli/dir/2019/2161/oj/eng
- **Reach of Norwegian law.** It applies to practices aimed at consumers in Norway (mfl. § 4). EU users' national UCPD
  laws apply to practices aimed at them. https://lovdata.no/dokument/NL/lov/2009-01-09-2

### 1.2 "Free" claims

- **Blacklist item 20.** It is always unfair to call a product "gratis", "vederlagsfri", "uten betaling" or similar if
  the consumer must pay more than the unavoidable cost of responding (FOR-2009-06-01-565 § 1 no. 20; UCPD Annex I
  no. 20). https://lovdata.no/dokument/SF/forskrift/2009-06-01-565
- **Data and other non-money "payment".** The Commission's guidance says the UCPD doesn't require payment in money.
  Calling a data-funded product "free" without explaining how user data is used can mislead (UCPD Guidance 2021 § 3.4,
  with the Italian AGCM and Facebook example).
  https://eur-lex.europa.eu/legal-content/EN/TXT/?uri=CELEX:52021XC1229(05)
- **Forbrukertilsynet, data-funded services.** If the business model is earning from consumers' data, "bør du ikke
  markedsføre tjenesten som «gratis» uten at du samtidig gir tydelig informasjon om denne forretningsmodellen" (don't
  call it free without clearly explaining that model at the same time). It also says not to disclaim liability for
  third parties' data processing. https://www.forbrukertilsynet.no/wp-content/uploads/2020/02/Veiledning-om-forbrukerdata.pdf
- **Forbrukertilsynet, attached costs.** "Free" can't be used where costs are attached, and any condition must sit
  next to the claim (real-estate guidance; contest guidance on "paying" with personal data).
  https://www.forbrukertilsynet.no/lov-og-rett/veiledninger-og-retningslinjer/forbrukertilsynets-veiledning-om-markedsforing-av-eiendomsmeglingstjenester
  https://www.forbrukertilsynet.no/lov-og-rett/veiledninger-og-retningslinjer/veileder-for-bruk-av-konkurranser-i-markedsforing-av-varer-og-tjenester
- **How strictly "no cost" is read.** For prize claims (blacklist no. 31), the CJEU held that *any* cost to the
  consumer, even a trivial one, breaks the "free" impression (C-428/11 *Purely Creative*). This is by analogy only:
  item 20 has its own "unavoidable cost" wording. https://curia.europa.eu/juris/liste.jsf?num=C-428/11
- **Application to Otium.** Otium has no money price. The consumer does give up 2% of mining output and 2% of
  conversion proceeds, pays the electricity, accepts hardware wear, and (if statistics sharing is on) supplies data.
  An unqualified "free" headline is therefore exposed. "Free to download. No subscription. Otium keeps 2%" is
  defensible if the fee sits next to the claim.

### 1.3 Earnings and "make money" claims

- **No dedicated guidance.** I found no Forbrukertilsynet guidance aimed specifically at "tjen penger" (make money)
  claims for software [verify with Forbrukertilsynet's decision database]. The nearest authority is the joint
  Forbrukertilsynet, Finanstilsynet and Økokrim letter of **28 May 2026** about BE, Sagemaster and DeepSage. It says
  all marketing must be "korrekt og balansert" (correct and balanced), and that costs, terms, risk of loss "og hvordan
  man faktisk tjener penger" (how money is actually earned) must come out clearly. It criticises messaging built on
  high income and success stories. https://www.forbrukertilsynet.no/advarsel-markedsforing-av-be-kan-vaere-ulovlig
  (Earlier joint warning: https://www.forbrukertilsynet.no/forbrukertilsynet-og-finanstilsynet-advarer-mot-better-experience)
- **Pyramid warning signs.** Forbrukertilsynet's list includes promises of high income and investment or crypto
  "education". Otium isn't a pyramid scheme: nobody pays to join, and referral rewards come from Otium's own fee. Keep
  invite marketing away from "earn from your friends" framing. https://www.forbrukertilsynet.no/vi-jobber-med/pyramidespill
- **Crypto and children.** Forbrukertilsynet has supported treating crypto as a product unsuitable for marketing to
  children (consultation position). Gaming audiences skew young.
  https://www.forbrukertilsynet.no/stotter-strengere-reklamevern-for-barn
  Children and young people guidance (May 2022): https://www.forbrukertilsynet.no/wp-content/uploads/2022/11/forbrukertilsynets-veiledning-om-markedsforing-overfor-barn-og-unge.pdf
- **Influencers and affiliates.** Paid posts must be labelled "reklame" or "annonse", visibly, without the reader
  pressing "more". The guidance was updated 18 May 2026, and YouTubers are covered from 1 May 2026. The advertiser
  can share liability (mfl. § 2, contributory liability).
  https://www.forbrukertilsynet.no/lov-og-rett/veiledninger-og-retningslinjer/someveiledning
  EU equivalent: the Commission/CPC "Influencer Legal Hub" (2023) [verify URL]
  https://commission.europa.eu/live-work-travel-eu/consumer-rights-and-complaints/influencer-legal-hub_en
- **Invite messages.** Otium's ready-made WhatsApp, Discord and email texts are "tips en venn" functions in
  Forbrukertilsynet's terms. They are outside mfl. § 15 (no consent needed) because the consumer sends them. Once the
  sender is **rewarded** (here, fee-free days and a share of the fee), a "konkret vurdering" (case-by-case
  assessment) under § 6 is needed. Otium must not add its own separate marketing content beyond a link (e-mail/SMS
  guidance § 2.6.6, updated 8 May 2026; MR-sak 05/01 and 24/05).
  https://www.forbrukertilsynet.no/lov-og-rett/veiledninger-og-retningslinjer/forbrukertilsynets-veiledning-markedsforing-via-e-post-sms-o-l
- **EU crypto marketing, 2024–2026.**
  - EBA, EIOPA and ESMA warned consumers jointly on 6 Oct 2025, pointing to aggressive finfluencer promotion and
    limited protection outside MiCA.
    https://esma.europa.eu/press-news/esma-news/eu-supervisory-authorities-warn-consumers-risks-and-limited-protection-certain
  - BEUC filed a CPC alert on crypto ads on social media in 2023. I found no 2025–2026 CPC coordinated action aimed
    at crypto marketing [verify]. https://www.mondaq.com/italy/fin-tech/1343412/europe-rejects-crypto-influencer
- **MiCA scope.** MiCA's marketing rules cover offerors and issuers of crypto-assets (arts. 7, 29 and 53) and CASPs'
  communications (art. 66: "fair, clear and not misleading", identified as marketing). Mining software that pays
  pool rewards to the user's wallet isn't a crypto-asset offer or a CASP service, so MiCA marketing rules most likely
  **don't** apply to the mining claims. https://eur-lex.europa.eu/eli/reg/2023/1114/oj/eng
  https://www.esma.europa.eu/publications-and-data/interactive-single-rulebook/mica/article-66-obligation-act-honestly-fairly
  **Exception:** the 2% fee on *converting* internet-sharing earnings could be an "exchange of crypto-assets for funds
  or other crypto-assets" (MiCA art. 3(1)(16)(c)–(d)) if Otium takes part in the exchange, or holds the funds. That
  would need a CASP licence under kryptoeiendelsloven, and only a few Norwegian firms hold one. This is outside
  consumer law, but it is high-impact [counsel]. The non-custodial plan in `docs/non-custodial-plan.md` moves
  internet sharing to payouts straight to the user's wallet. https://schjodt.com/news/norway-mica-fully-implemented-transitional-period-prologned
- **US users.** US earnings claims are a separate analysis. The FTC sent its Notice of Penalty Offenses on
  money-making opportunities to over 1,000 companies (2021). [verify; US out of scope]
  https://www.ftc.gov/enforcement/penalty-offenses/money-making-opportunities

### 1.4 Otium's claims, one by one

| Claim | Risk | Why | Sources |
|---|---|---|---|
| "Never costs you money" | **High** | An absolute claim about results, which needs proof (mfl. § 3). It is false in foreseeable cases: <br>• Electricity prices are estimates (national averages miss grid tariffs, taxes and time-of-use rates). Nordic hourly prices exclude nettleie (grid fees) unless modelled.<br>• Keeping the PC awake adds idle power the profitability check may not count.<br>• Hardware wear and extra cooling.<br>• Coin price falls between mining and payout.<br>• Pool payout thresholds, and balances forfeited after 6 months idle (unMineable).<br>• Auto-pay is off by default for new TRC-20 addresses.<br>• Data costs on metered connections when internet sharing is on.<br>It also becomes a **contract term** under digitalytelsesloven § 8 (see § 2.3), so it creates a defect and damages exposure. | mfl. §§ 3, 7 https://lovdata.no/dokument/NL/lov/2009-01-09-2 ; docs/non-custodial-plan.md (forfeiture, auto-pay) |
| "Earn money while your computer rests" | Medium | You earn crypto, not money, and its value swings. GPU algorithms are "free power only" at normal prices, so many users earn little. Acceptable with a realistic net range next to it. | UCPD art. 6(1)(b); Forbrukertilsynet 28 May 2026 letter |
| "Free" | Medium | Blacklist no. 20, and guidance on non-money payment. | FOR-2009-06-01-565 no. 20; UCPD Guidance § 3.4 |
| Per-card monthly estimates (WhatToMine plus medians of 3 or more users) | Medium to high | Needs proof on file when published (§ 3).<br>• Must say whether figures are gross or net of the pool fee, Otium's 2% and electricity.<br>• A median of 3 users is statistically weak and open to survivorship bias, so show *n*, the period and the date.<br>• Figures must update with difficulty and price. | mfl. § 3; UCPD art. 12 |
| "Two-minute test: $41.20 a month" | Medium to high | Precise figures suggest certainty. A two-minute benchmark extrapolated to a month at today's prices and difficulty is a projection. Show a range, the assumptions (24/7 vs. idle hours, price and kWh used) and "estimate, not guaranteed" next to the figure. | C-611/14; C-646/22 |
| "Never in your way" | Medium | The app keeps the PC awake, reserves 2.5 GB of RAM, and causes fan noise, heat and possible game stutter. An absolute performance claim. | mfl. § 7(1)(b) |
| "Paid straight to your own wallet" / "Otium never holds your money" | Low for mining; **high** for conversion if custodial | True for pool payouts to the user's address, subject to pool thresholds. False if Otium receives internet-sharing proceeds and converts them. That also triggers MiCA, AML and payment-services questions. | MiCA art. 3(1)(16); kryptoeiendelsloven |
| Fee breakdown shown before start | Positive | This supports compliance with § 8 (material information) and the "free" rules. Keep it on the website too, not only in the app. | mfl. § 8 |
| Quick start turns everything on with one "Start earning" button | Medium | Material information about internet sharing (third-party traffic through the user's IP, Proxybase terms, data use) and statistics sharing (data, GDPR consent must be granular) is bundled. A dark-pattern focus is likely under the coming DFA. Separate opt-ins are recommended, especially for internet sharing. | UCPD art. 7; Forbrukertilsynet data guidance; DFA consultation https://thelens.slaughterandmay.com/post/102kxp5/digital-fairness-act-european-commission-launches-consultation-and-call-for-evid |
| Any "green", "uses idle power" or eco framing | Medium (EU from 27 Sep 2026) | EmpCo blacklists generic environmental claims (new Annex I no. 4a). Crypto mining's energy use makes eco framing risky. | Dir. 2024/825; Prop. 38 LS |
| Update prompts | Low to medium | New blacklist items 23d and 23e: hiding that an update harms functionality, and calling an update "necessary" when it only adds features. EU from 27 Sep 2026; Norway from 2027 at the earliest. | Prop. 38 LS p. ~29 (blacklist list) |

---

## 2. Digital Content Directive and digitalytelsesloven

### 2.1 Does it apply? Yes, in Norway clearly; in the EU probably

- **Norway: any counter-performance counts.** The Act covers digital content and digital services "mot vederlag"
  (against payment). "Med vederlag menes betaling i penger **eller annen motytelse**" (payment in money or any other
  counter-performance), plus personal data unless it is used only to deliver the service or meet legal duties
  (digitalytelsesloven § 1). Norway is wider than the Directive here: **any counter-performance** counts. Otium's 2%
  of the user's mining time, and the pool referral benefit, are counter-performance, so the Act applies to Norwegian
  users. It can't be waived to the consumer's detriment (§ 3). https://lovdata.no/dokument/NL/lov/2022-06-17-56
- **EU: price or personal data only.** The Directive covers contracts where the consumer pays a "price" (money or "a
  digital representation of value", art. 2(7)) or provides personal data (art. 3(1)). Virtual currencies count only
  "to the extent that they are recognised by national law" (recital 23). Data collected only to supply the service is
  excluded (recital 25). https://eur-lex.europa.eu/eli/dir/2019/770/oj/eng
  - Mining time paid in kind (Otium's address receives the coins directly) arguably isn't a "price" [counsel].
  - But if earnings statistics, hardware IDs or usage data are used beyond running the service (for example
    publishing website medians, analytics or referrals), the personal-data route applies.
  - Several member states extend the Directive to other counter-performance. It is safest to assume it applies EU-wide.
- **What the app is.** The app is "digital content" and the server, pool management and statistics are a "digital
  service". Supply is **continuous** (§ 10 second paragraph), so conformity is owed for the whole period and the
  supplier carries the burden of proof (§ 11). https://lovdata.no/dokument/NL/lov/2022-06-17-56

### 2.2 Conformity and updates (§§ 7–11; DCD arts. 6–8)

- **Agreed requirements.** These include compatibility and updates (§ 7). https://lovdata.no/dokument/NL/lov/2022-06-17-56
- **Objective requirements.** These include the latest version available at contract time, fitness for normal
  purposes, and what the consumer may expect "under hensyn til ... opplysninger om ytelsen fra leverandørens side"
  (taking into account the supplier's own statements). That expressly includes **statements in marketing** by the
  supplier (§ 8; DCD art. 8(1)(b)). The supplier escapes only if it proves the statement was corrected beforehand in
  the same way, or couldn't have influenced the contract. https://lovdata.no/dokument/NL/lov/2022-06-17-56
- **Updates.** The supplier must tell the consumer about and supply updates, "herunder sikkerhetsoppdateringer"
  (including security updates), needed to stay in conformity (§ 9; DCD art. 8(2)). The consumer can't rely on a defect
  caused only by failing to install a notified update in reasonable time (§ 10). Velopack auto-update helps meet
  § 9. https://lovdata.no/dokument/NL/lov/2022-06-17-56
- **Changes beyond conformity** (new features, fee changes, removed coins):
  - Allowed only if the contract provides for them, they cost the consumer nothing, and the consumer is told clearly
    (§ 41). DCD art. 19(1)(a) also requires the contract to give a **valid reason** for EU users.
  - If a change affects the consumer negatively and not insignificantly, the consumer may terminate within 30 days.
    Notice must come in reasonable time on a **durable medium** and explain that right (§ 42).
  - Raising Otium's 2% fee is the obvious case.
  https://lovdata.no/dokument/NL/lov/2022-06-17-56 ; https://eur-lex.europa.eu/eli/dir/2019/770/oj/eng
- **Consumer termination.** The consumer can end a continuous-supply contract on reasonable notice and simply (§ 33).
  An "Uninstall / Stop / delete account" path is enough, but the ToS should say so. https://lovdata.no/dokument/NL/lov/2022-06-17-56

### 2.3 Remedies and damages: the key exposure for hardware damage and electricity

- **Remedies for defects.** Withholding payment, cure, price reduction, termination and **damages** (§§ 19–25).
  https://lovdata.no/dokument/NL/lov/2022-06-17-56
- **Damages for a defect** (§ 25) are measured under chapter 11 (§ 48). They cover loss "herunder utlegg,
  prisforskjell og tapt fortjeneste" (including expenses, price difference and lost profit), limited to what was
  **reasonably foreseeable**. They also cover a concrete and non-trivial inconvenience, but **not** personal injury or
  business losses. Damages can be reduced if unreasonable (§ 50).
  - § 25, unlike the delay rule in § 16, states no control-liability defence in its text. Read literally, liability
    for defects doesn't depend on fault [verify against the preparatory works, Prop. 33 L (2021–2022)].
  - Example: the app fails to pause when unprofitable, against the "never costs you money" statement. Electricity
    paid would then be a recoverable expense. Hardware damage caused by a defect (for example a broken thermal or
    power safeguard) is foreseeable property loss.
  - Under § 3, **a ToS clause can't exclude these consumer rights in Norway.**
  https://lovdata.no/dokument/NL/lov/2022-06-17-56
- **EU users.** The DCD leaves damages to national law (art. 3(10)), so they vary by country.
  https://eur-lex.europa.eu/eli/dir/2019/770/oj/eng
- **Enforcement.** Forbrukertilsynet and Markedsrådet (the Market Council) supervise the Act through mfl. §§ 32–43
  (§ 55). https://lovdata.no/dokument/NL/lov/2022-06-17-56

### 2.4 Information duties before the contract

- Pre-contract information under angrerettloven § 8 (CRD art. 6) applies to contracts where the consumer gives
  personal data (angrerettloven § 1). Items (b) functionality, (c) compatibility and interoperability, and (l) the
  legal conformity guarantee are relevant to software. https://lovdata.no/dokument/NL/lov/2014-06-20-27
- **Norwegian language.** "Når næringsdrivende retter sin markedsføring mot norske forbrukere, skal opplysningene ...
  gis på norsk" (when marketing targets Norwegian consumers, the information must be in Norwegian; § 8). Otium
  targets Norway (it has Nordic hourly electricity prices), so it needs Norwegian pre-contract information, or at
  least Norwegian versions of the key information and ToS [counsel]. https://lovdata.no/dokument/NL/lov/2014-06-20-27

---

## 3. Withdrawal rights: Consumer Rights Directive and angrerettloven

- **Scope.** Distance contracts where the consumer pays **or provides personal data** (angrerettloven § 1; CRD
  art. 3(1a), inserted by the Omnibus). Whether the 2% mining time counts as "pays" under the CRD is uncertain.
  Otium's personal data (worker id, statistics) probably brings it in anyway. https://lovdata.no/dokument/NL/lov/2014-06-20-27
  https://eur-lex.europa.eu/eli/dir/2011/83/oj/eng
- **Digital content exception.** It applies only where "avtalen forplikter forbrukeren til å betale" (the contract
  obliges the consumer to pay) **and** the consumer gave express prior consent and acknowledged loss of the right
  (§ 22 n; CRD art. 16(m) as amended). For a free, data or in-kind contract the exception doesn't apply, so a
  **14-day right of withdrawal exists**. If the withdrawal information is missing, the period runs up to 12 months
  plus 14 days (§ 21). https://lovdata.no/dokument/NL/lov/2014-06-20-27
- **Effects of withdrawal.** The trader must stop using content the consumer provided or created, other than personal
  data, which GDPR governs (§ 24 a). The consumer must stop using the content, and the trader may block access (§ 26).
  - Whether Otium must hand back its 2% mining share earned during the withdrawal period is unclear [counsel].
  - A practical fix is to promise in the ToS a refund of Otium's fee share for the first 14 days on request. It costs
    little and closes the gap.
  https://lovdata.no/dokument/NL/lov/2014-06-20-27
- **Withdrawal button** (CRD art. 11a, applies in the EU from 19 Jun 2026):
  - Distance contracts made through an online interface need a "withdraw from contract here" function. It must be
    available throughout the withdrawal period, with a confirmation step.
  - It is arguably required for EU users of Otium's contract flow, since the app and site are an online interface.
  - Norway: § 20 a has been passed, but its start may be postponed.
  - Simplest route: a "Withdraw / stop and delete my account" item in the app's settings and on the website.
  https://eur-lex.europa.eu/eli/dir/2023/2673/oj/eng ; Prop. 38 LS p. 15 ;
  https://www.iubenda.com/en/blog/the-new-online-withdrawal-function-what-eu-directive-2023-2673-means-for-your-business/
- **Confirmation on a durable medium** within a reasonable time (angrerettloven § 18; CRD art. 8(7)). A hyperlink on
  a website alone doesn't count as information "given" or a durable medium (CJEU C-49/11 *Content Services*). An
  email or a downloadable PDF of the ToS and the information is safer. Otium has no mandatory email, so consider
  showing a saveable PDF or text file at first start. https://curia.europa.eu/juris/liste.jsf?num=C-49/11
- **Standard withdrawal form.** Providing the standard form counts as giving information items (h)–(j) (§ 8).
  https://lovdata.no/dokument/NL/lov/2014-06-20-27

---

## 4. Unfair terms (93/13) and what a fair ToS must say

### 4.1 The legal limits

- **The test.** A non-negotiated term is unfair if, against good faith, it creates a significant imbalance to the
  consumer's detriment. Terms must be plain and intelligible, and doubts are read in the consumer's favour (contra
  proferentem). An unfair term doesn't bind the consumer, and the rest of the contract survives (93/13 arts. 3–6).
  https://eur-lex.europa.eu/eli/dir/1993/13/oj/eng
- **Norway.** avtaleloven § 37 (based on 93/13) applies with § 36:
  - Later circumstances can't be used to make an unfair term fair.
  - The consumer may keep the rest of the contract.
  - "Ved tvil om tolkningen ... tolkes til fordel for forbrukeren" (doubtful terms are read in the consumer's favour).
  - The trader must prove a term was individually negotiated.
  - Choosing a non-EEA law can't reduce protection.
  https://lovdata.no/dokument/NL/lov/1918-05-31-4/KAPITTEL_4
- **Forbrukertilsynet** can act against "urimelige avtalevilkår" (unfair contract terms) in standard terms (mfl.
  § 22), with infringement fines under § 42. https://lovdata.no/dokument/NL/lov/2009-01-09-2
- **Indicative grey list** (93/13 Annex 1). The relevant items are terms that:
  - (a) exclude liability for death or personal injury;
  - (b) inappropriately exclude or limit rights for non-performance;
  - (j) let the trader change terms unilaterally without a valid reason stated in the contract;
  - (k) let the trader change product characteristics unilaterally;
  - (l) set price increases without a right to cancel;
  - (q) exclude or hinder legal action, or impose arbitration not covered by law, or shift the burden of proof.
  https://eur-lex.europa.eu/eli/dir/1993/13/oj/eng
- **Commission guidance on 93/13** (2019). https://eur-lex.europa.eu/legal-content/EN/TXT/?uri=CELEX:52019XC0927(01)
- **CJEU case law.**
  - National courts can't rewrite an unfair term down to a lawful level, so an over-broad disclaimer falls entirely
    (C-618/10 *Banco Español de Crédito*). https://curia.europa.eu/juris/liste.jsf?num=C-618/10
  - Transparency means the consumer must understand the economic consequences (C-26/13 *Kásler*), which matters for
    fee terms. https://curia.europa.eu/juris/liste.jsf?num=C-26/13
  - An exclusive jurisdiction clause pointing to the trader's seat is unfair (C-240/98 *Océano Grupo*).
    https://curia.europa.eu/juris/liste.jsf?num=C-240/98
  - A choice-of-law clause is unfair if it misleads the consumer into thinking only the chosen law applies. It must
    say that the consumer keeps the mandatory protection of the law of their habitual residence (C-191/15 *VKI v
    Amazon*). https://curia.europa.eu/juris/liste.jsf?num=C-191/15

### 4.2 Governing law, courts and dispute resolution

- **Courts (Lugano Convention 2007).** Norway is party to Lugano, the parallel to Brussels I bis. A consumer can sue a
  trader that "directs activities" to their state in the consumer's own courts. The trader can sue the consumer only
  there. Jurisdiction agreements made before a dispute arises can't take that away (Lugano arts. 15–17; Brussels I bis
  arts. 17–19). A worldwide site in English with country-specific electricity pricing likely "directs" to many states
  (C-585/08 *Pammer/Alpenhof* criteria).
  https://eur-lex.europa.eu/legal-content/EN/TXT/?uri=CELEX:22007A1221(03) ;
  https://eur-lex.europa.eu/eli/reg/2012/1215/oj/eng ; https://curia.europa.eu/juris/liste.jsf?num=C-585/08
- **Governing law.** For EU consumers, EU courts apply Rome I art. 6. Norwegian law may be chosen, but the consumer
  keeps the mandatory rules of their home country. https://eur-lex.europa.eu/eli/reg/2008/593/oj/eng
  Norway isn't bound by Rome I. Its consumer laws carry their own mandatory-scope rules (for example avtaleloven § 37
  on non-EEA law) [counsel: Norwegian conflict-of-law rules for consumer contracts].
- **ADR information duty (mfl. § 10 a).** Traders must give clear information about the approved klageorgan
  (complaints body) that can handle disputes, linking to its website. It must appear at minimum in the general terms
  and on the website, easy to find, and again in writing when a dispute can't be settled.
  https://lovdata.no/dokument/NL/lov/2009-01-09-2
  - The Norwegian route is mediation at Forbrukertilsynet, then a decision by Forbrukerklageutvalget (the Consumer
    Disputes Commission). It covers purchases of goods and services from traders established in Norway or targeting
    Norway (forbrukerklageloven §§ 1–3). Whether a free app is a "kjøp" (purchase) under the Act is uncertain [verify].
    https://lovdata.no/dokument/NL/lov/2020-06-23-98
- **ODR link [superseded].** Since 20 Jul 2025, do **not** add the old EU ODR platform link. Remove any template text
  that refers to it. https://eur-lex.europa.eu/eli/reg/2024/3228/oj/eng ;
  https://cm.twobirds.com/en/insights/2025/global/the-end-of-the-european-online-dispute-resolution-platform
- **New ADR Directive 2025/2647.** It extends ADR to digital-content disputes, pre-contract disputes and non-EU traders
  directing activities to the EU. Traders must answer ADR bodies within 20 working days (30 if complex). Member states
  transpose by 20 Mar 2028. https://eapil.org/2026/01/23/directive-eu-2025-2647-amending-the-adr-framework-for-consumer-disputes/
- **Arbitration and class waivers.** Don't use them for EEA consumers (grey list (q)). If needed, use them only in a
  separate US addendum [US counsel].

---

## 5. Trader information on the website

- **E-handelsloven § 8 (ECD art. 5).** Always give the name, address, email address and other details allowing quick
  direct contact. Also give (a) the business register and **organisation number**, and VAT status, and (b) any
  required authorisations and the supervising authority. It must be "lett og direkte tilgjengelig" (easy and direct
  to find). https://lovdata.no/dokument/NL/lov/2003-05-23-35 ; https://eur-lex.europa.eu/eli/dir/2000/31/oj/eng
- **E-handelsloven § 11.** Before an electronic "order", explain the technical steps, whether the contract is
  archived, how to correct input errors, the languages, and any codes of conduct. Terms must be storable and
  reproducible. https://lovdata.no/dokument/NL/lov/2003-05-23-35
- **Angrerettloven § 8 d–e (CRD art. 6(1)(b)–(c)).** Identity, **geographic address**, telephone and email (the
  Omnibus removed fax and requires any other online channel offered to be listed).
  https://lovdata.no/dokument/NL/lov/2014-06-20-27
- **Practical points.**
  - A sole proprietor (ENK) must publish a geographic address. Consider an AS (limited company), or a business
    address that isn't a home address.
  - Register in Enhetsregisteret (the Central Coordinating Register) and show the org number.
  - Put this in a footer "Imprint / About Otium" on otium.lol and in the app's About screen.

---

## 6. Accessibility

- **The EAA.** It covers specified products and services: e-commerce services ("services provided at a distance,
  through websites and mobile device-based services ... at the individual request of a consumer with a view to
  concluding a consumer contract"), e-books and dedicated software, banking and others (EAA arts. 2(2) and 3(30)).
  https://eur-lex.europa.eu/eli/dir/2019/882/oj/eng
  - A free mining app isn't a listed product or service.
  - otium.lol could at most arguably be an "e-commerce service" for concluding the free contract.
  - **Microenterprises providing services** (fewer than 10 staff and turnover or balance sheet of EUR 2 million or
    less) are exempt (art. 4(5)).
  - Conclusion: very likely not covered, and in any case exempt. https://eur-lex.europa.eu/eli/dir/2019/882/oj/eng
- **Norway.** The EAA is **not yet part of the EEA Agreement** (EEA-lex: "under scrutiny"), so it doesn't bind a
  Norwegian operator directly. It would bind EU-established traders. https://www.efta.int/eea-lex/32019l0882
- **Norwegian national law that does apply.** Private businesses' web solutions aimed at the public must meet **WCAG
  2.0 A/AA**, minus success criteria 1.2.3–1.2.5 (forskrift om universell utforming av IKT-løsninger, the universal
  design of ICT regulation, §§ 2–4). That covers web solutions and automats, so **otium.lol is covered and the desktop
  app isn't** (desktop software isn't a "nettløsning", a web solution). Digdir (the Norwegian Digitalisation Agency)
  enforces it. https://lovdata.no/dokument/SF/forskrift/2013-06-21-732

---

## 7. Product safety and product liability

- **New PLD 2024/2853.**
  - "Product" includes **software**, whether standalone or embedded.
  - Compensable damage includes damage to property (other than the defective product itself and property used only
    for work) and destruction or corruption of non-work **data**. **Hardware damaged by defective mining software is
    in scope.**
  - The old EUR 500 threshold is gone.
  - Defectiveness considers reasonably foreseeable use and cybersecurity requirements.
  - A defect caused by a software update, or by a missing security update within the manufacturer's control, isn't
    excused by the "later-existing defect" defence.
  - Software supplied outside a commercial activity (free/open-source) is excluded. **Otium is monetised, so it
    isn't excluded.**
  - Liability can't be excluded or limited by contract.
  - It applies to products placed on the market or put into service after 9 Dec 2026. Under the "substantial
    modification" rules, later versions may count as placed on the market anew [verify article numbering and how it
    applies to continuously updated software].
  https://eur-lex.europa.eu/eli/dir/2024/2853/oj/eng ; https://cms.law/en/int/publication/the-new-eu-product-liability-directive-eu-2024-2853
- **Who is affected.** For **EU users**, national PLD implementations apply to Otium as manufacturer from December
  2026, and a non-EU manufacturer can still be sued. **Norway** hasn't incorporated the Directive. The 1988
  produktansvarsloven still applies, and whether it covers standalone software is unresolved in Norwegian literature.
  https://www.efta.int/eea-lex/32024l2853 ; https://lovdata.no/dokument/NL/lov/1988-12-23-104 ;
  https://cms.law/en/int/expert-guides/expert-guide-to-product-liability-and-warranty-litigation/norway ;
  https://svw.no/en/svw-digital-governance-tracker/artificial-intelligence/product-liability-directive/
- **Old PLD 85/374 [superseded for new products].** It still governs products placed on the market before 9 Dec 2026.
  Software wasn't clearly within its scope. Compare CJEU C-65/20 *Krone-Verlag*: inaccurate advice in a newspaper isn't
  a defective product. That is relevant by analogy to earnings *estimates*, as opposed to software behaviour.
  https://curia.europa.eu/juris/liste.jsf?num=C-65/20
- **Mining and defects.** Mining-induced wear under normal load isn't by itself a "defect" if it is clearly warned
  about. Risk rises sharply if Otium:
  - changes power limits, clocks, voltages or fan curves ("efficiency mode");
  - lacks temperature cut-offs;
  - mines on laptops or batteries without safeguards.
  Document the safeguards and the testing.
- **Cyber Resilience Act.** Otium is a "product with digital elements" made available commercially. Reporting of
  actively exploited vulnerabilities and severe incidents applies **from 11 Sep 2026**, even to products already on
  the market. Essential requirements, including secure updates, apply from 11 Dec 2027. That is relevant to unsigned
  auto-updates. EEA status not checked [verify]. https://eur-lex.europa.eu/eli/reg/2024/2847/oj/eng ;
  https://cms.law/en/aut/legal-updates/ready-respond-report-the-cyber-resilience-act-s-reporting-regime-is-here
- **GPSR 2023/988.** Whether standalone software is a "product" under the General Product Safety Regulation is
  doubtful. Low relevance [verify]. https://eur-lex.europa.eu/eli/reg/2023/988/oj/eng
- **Antivirus and code signing.** Don't tell users to switch off antivirus or SmartScreen globally. Explain narrow
  exclusions and the reason. Avoid "safe" or "secure" claims until the app is code-signed, because these are
  documentation-sensitive under mfl. § 3. Keep a visible tray icon and no stealth or silent mode. Hidden miners are
  how cryptojacking malware behaves, and misuse on other people's PCs raises criminal-law issues for the installer.

---

## 8. Recommended wording and a ToS outline

### 8.1 Claim rewrites

| Current | Suggested | Notes |
|---|---|---|
| "Never costs you money" | "Pauses itself when your electricity would cost more than you'd earn." Next to it, in small text, link to "How we estimate": "Based on estimated electricity prices for your country (live hourly prices in the Nordics). Your actual tariff, grid fees and taxes may differ. You can enter your own price." | Remove the absolute. If a guarantee is kept, it becomes a contract term (digitalytelsesloven § 8). Count idle "keep-awake" power and grid fees in the profitability check, or disclose that they aren't counted. |
| "Earn money while your computer rests" | "Earn crypto while your computer rests" plus a realistic net range: "Typical: $X–$Y a month after fees and electricity, for a [card] in [country]. Not guaranteed." | Say "crypto", and that its value swings. |
| "Free" | "Free to download. No subscription. Otium keeps 2% of mining time, and 2% when converting internet-sharing earnings." | Put the fee in the same view as "free" (blacklist no. 20). |
| Per-card estimates | "Estimated net earnings, [date], at today's coin prices and network difficulty, after pool fee and Otium's 2%, before/after electricity at [x]/kWh. Based on WhatToMine data and the median of [n] Otium users over [period]." | Archive the methodology and data snapshots (mfl. § 3). Require a larger *n* before showing medians, or label them "early data". |
| "Two-minute test: $41.20 a month" | "If today's conditions held, about $35–$45 a month (estimate)." Add a "What this assumes" link covering hours per day, price, difficulty and electricity. | Avoid false precision. Show the electricity cost line. |
| "Never in your way" | "Steps aside when you use your computer." Add a disclosure: "Keeps your PC awake while earning, reserves 2.5 GB of memory, and makes your PC warmer and its fans louder." | |
| "Paid straight to your own wallet" | "Pools pay you directly to your own wallet once you reach their payout minimum." | Disclose thresholds, auto-pay settings and pool forfeiture rules (for example unMineable's 6-month idle rule). |
| "Otium never holds your money" | Keep only if true for **every** feature, including conversion of internet-sharing earnings. | Otherwise rewrite and get regulatory advice (MiCA/AML). |
| Quick start | Keep the summary, but give each item its own visible switch. Leave internet sharing **off by default**, or require a separate tick with a plain explanation that "other companies' traffic will use your internet connection and IP address" and a link to the partner's terms. Statistics sharing needs a separate choice if it relies on consent. | UCPD art. 7, GDPR consent rules, DFA direction of travel. |

### 8.2 Terms of Service outline (consumer-facing, plain language)

Present it in layers: a one-screen summary, then the full terms (Forbrukertilsynet recommends a summary first). Offer
Norwegian and English versions, with the Norwegian one binding for Norway. Give the user a downloadable PDF or text
copy (durable medium). Use version numbers and dates.

1. **Who we are.** Legal name, org number, geographic address, email, VAT status (e-handelsloven § 8; angrerettloven
   § 8).
2. **What Otium does.** Plain description: it mines with your CPU and GPU when idle, pools pay you, and the optional
   internet sharing is run by a partner. Supported systems; macOS and Linux are "beta". Compatibility and minimum
   requirements (angrerettloven § 8 b–c).
3. **Who can use it.** 18 or older, because of capacity to contract (vergemålsloven, the Guardianship Act) and
   crypto's unsuitability for children. Only on devices you own or are allowed to use, with electricity you are
   allowed to use. That excludes employer, school and landlord devices or power unless permitted. Not for
   sanctioned persons or regions. Regional routing for US users explained.
4. **How Otium earns, and fees.** 2% of mining time to Otium's address, the pool referral code (and what it means for
   the user's pool fee), 2% on conversions, and fee-free referral periods. All shown before start. Fee changes only
   with advance notice on a durable medium, and the user may stop with no cost (digitalytelsesloven §§ 41–42; 93/13
   Annex (l)).
5. **Earnings are estimates, not promises.** How estimates are made. The factors: coin prices, difficulty, pool
   performance, hours idle, the electricity price and its accuracy. No guaranteed income. Crypto is volatile, and
   taxes are the user's responsibility. Don't contradict the marketing: the ToS can't cure a misleading ad, and the
   ad becomes part of the contract (§ 8).
6. **Costs and effects on your computer.** Electricity, heat, fan noise, possible faster wear of fans and GPUs, the PC
   kept awake, 2.5 GB of RAM reserved, battery and laptop behaviour, and data use for internet sharing. The
   automatic pause rule and its limits. How to set your own electricity price. Safeguards (temperature limits and
   so on) and what the user can change.
7. **Third-party services.** Mining pools (unMineable, MoneroOcean and others) and the internet-sharing partner
   (Proxybase or Mysterium), each with its own terms linked. Payouts come from them, not from Otium. Their thresholds,
   auto-pay and forfeiture rules. Otium is responsible for its own software and for choosing and integrating these
   partners. Avoid a blanket "we're not responsible for third parties". Forbrukertilsynet warns against disclaiming
   responsibility for third-party data sharing, and there is § 26 (claims against earlier links in the chain).
8. **Your wallet.** The user controls the address. Mistyped addresses can't be reversed. Otium never asks for private
   keys or seed phrases.
9. **Updates.** Automatic updates, including security updates (§ 9). The user is told about updates. What happens if
   the user blocks updates (§ 10). Changes to features only as allowed by the contract, for valid reasons (DCD
   art. 19), with notice. Don't present feature updates as "required" (EmpCo blacklist 23e).
10. **Antivirus and code signing.** Why security software may flag miners. The app's current signing status. Never
    disable protection globally.
11. **Privacy.** Short pointer to the separate privacy notice. Which data counts as counter-performance (statistics
    sharing). Opt-outs.
12. **Your right to withdraw** (14 days, standard form, the "withdraw here" function, refund of Otium's fee share for
    that period on request) **and to stop at any time** (uninstall or account deletion; digitalytelsesloven § 33).
13. **If something goes wrong (statutory rights).** "You have the rights consumer law gives you, including under the
    Norwegian Digital Services Act and the consumer law of your country. These terms don't limit them." Remedies:
    cure, price reduction, termination, damages. Liability limits only where the law allows. Never exclude liability
    for death or personal injury, intent or gross negligence, or mandatory consumer remedies. Exclude business use
    (consequential business losses). Any cap must leave statutory rights alone (C-618/10: a term that goes too far
    falls entirely).
14. **Suspension and termination by Otium.** Only for objective, listed reasons: fraud, botnets or installs on others'
    devices, sanctions, security emergencies, or the end of a partner integration. Notice where possible. What
    happens to pending pool balances (still the user's, at the pool).
15. **Changes to these terms.** Valid reasons listed. Advance notice on a durable medium (for example 30 days, by email
    if given, and in the app). A right to stop without cost. No "continued use = acceptance" for material changes
    (grey list (j)).
16. **Referrals.** Rules for fee-free days and fee shares. No spam. The user must only send invites to people they
    know. Otium supplies no extra advertising content beyond the link (Forbrukertilsynet's "tips en venn" guidance).
17. **Governing law and courts.** Norwegian law, but "if you are a consumer, you also keep the protection of the
    mandatory consumer law of the country where you live" (C-191/15). Consumers may sue in their home courts (Lugano /
    Brussels I bis). No exclusive-venue clause against consumers.
18. **Complaints and dispute resolution.** Contact first. Then Forbrukertilsynet mediation and Forbrukerklageutvalget,
    with links (mfl. § 10 a). EU consumers may use their national ADR bodies or ECC-Net. **No ODR platform link.**
    https://www.forbrukertilsynet.no ; https://www.forbrukerklageutvalget.no [verify URL]
19. **US addendum** (if needed). A separate document, reviewed by US counsel.
20. **Contact, versions and archive.** Previous ToS versions, downloadable.

---

## Risk assessment for Otium

| # | Issue | Likelihood | Impact | Rating | Fix |
|---|---|---|---|---|---|
| 1 | "Never costs you money" is an absolute claim that can't be proved, and under digitalytelsesloven § 8 it becomes a contract promise. Electricity and hardware losses become recoverable damages (§§ 25 and 48, possibly strict liability) | High | High | **High** | Rewrite (§ 8.1). Include keep-awake idle power and grid fees in the calculation. Keep a methodology file |
| 2 | Earnings figures ($41.20, per-card medians of 3 or more users) without proof on file or net/gross clarity (mfl. § 3; Forbrukertilsynet May 2026 letter) | High | Medium to high | **High** | Ranges, assumptions, *n* and date, archived snapshots |
| 3 | "Otium never holds your money" while converting internet-sharing earnings for a fee. Possibly a false claim and unlicensed CASP activity under kryptoeiendelsloven | Medium (depends on how conversion works) | Very high | **High** | Make conversion non-custodial, or remove the claim and get a MiCA/AML opinion |
| 4 | No ToS, no trader information, no ADR information (e-handelsloven § 8; mfl. § 10 a; angrerettloven § 8) | Certain today | Medium (infringement fines) | **High until fixed** | Publish /terms, an imprint and ADR text before general availability |
| 5 | Bundled quick start (internet sharing and statistics sharing switched on with one click) | Medium | Medium | Medium | Separate opt-ins; internet sharing off by default |
| 6 | "Free" without the fee next to it (blacklist no. 20) | Medium | Medium (blacklist means no materiality test) | Medium | Add the fee line |
| 7 | Withdrawal rights and the withdrawal button not provided (angrerettloven §§ 21–22; CRD art. 11a for the EU from 19 Jun 2026) | High | Low to medium (period extended to 12 months, small money at stake) | Medium | Withdrawal text, form and menu item; refund the fee share |
| 8 | Norwegian-language information for marketing aimed at Norway (angrerettloven § 8) | High | Low to medium | Medium | Norwegian ToS and key information |
| 9 | Hardware damage claims: PLD for EU users from Dec 2026; produktansvarsloven and general tort in Norway; contract damages | Low to medium | High per incident | Medium | Thermal and power safeguards, no overclocking, clear warnings, testing records, insurance |
| 10 | Unsigned auto-updates, antivirus flags, CRA vulnerability reporting (from 11 Sep 2026, EEA unclear) | Medium | Medium | Medium | Code signing, update signing, a vulnerability-handling process |
| 11 | Referral "tips en venn" with rewards | Low | Low | Low | Keep messages user-sent, link-only, no Otium promotional add-ons |
| 12 | Accessibility: EAA not applicable (not in the EEA; microenterprise exemption); WCAG 2.0 AA applies to otium.lol under Norwegian law | Medium (website not audited) | Low | Low | Audit the website against WCAG |
| 13 | EmpCo green-claim and update-messaging bans (EU from 27 Sep 2026, Norway 2027 at the earliest) | Low | Medium | Low | No eco claims; neutral update wording |
| 14 | Minors using the app (capacity to contract, children's marketing guidance) | Medium | Low to medium | Low to medium | 18+ in the ToS; no youth-targeted ads |
| 15 | Coming Digital Fairness Act (dark patterns, defaults, influencers) | n/a (proposal pending) | Medium | Watch | Design the opt-ins now |

---

## Questions for counsel

1. **Counter-performance.** Do you agree that Otium's 2% of mining time is "annen motytelse" (other
   counter-performance) under digitalytelsesloven § 1, so the Act applies in full to Norwegian users? For EU users, is
   in-kind mining time a "price" under DCD art. 2(7), or does the Directive apply only through personal data? Which
   member states extend it to other counter-performance?
2. **Damages.** Is liability for defects under digitalytelsesloven § 25 / § 48 strict (no control-liability
   defence)? Can a ToS cap or exclude indirect losses such as hardware damage and electricity for Norwegian
   consumers, given § 3?
3. **Marketing as contract.** Does "Never costs you money", even if rewritten, become a conformity requirement under
   § 8? What wording keeps the pause feature as a described function rather than a guarantee?
4. **Withdrawal.** Does angrerettloven § 1 apply because of personal data, or also because of mining time? On
   withdrawal, must Otium refund the value of its 2% share (§ 24)? Is a voluntary refund of the fee share enough?
5. **Withdrawal button.** Does CRD art. 11a apply to Otium's in-app contract for EU users from 19 Jun 2026? When will
   angrerettloven § 20 a take effect?
6. **Language.** Do the angrerettloven § 8 rules on Norwegian-language information apply to an English-first site that
   offers Nordic hourly electricity pricing? Must the ToS itself be in Norwegian (§ 18: "Er opplysningene gitt på
   norsk, skal avtalen utformes på norsk", if the information is in Norwegian the contract must be too)?
7. **Complaints body.** Which approved klageorgan must mfl. § 10 a name for a free software service? Is a free app a
   "kjøp" (purchase) under forbrukerklageloven § 1 and its regulation?
8. **Conversion.** Is converting internet-sharing earnings for a 2% fee a CASP service (exchange) under MiCA and
   kryptoeiendelsloven, or a payment or money-remittance service? Does the analysis change if a third-party swap is
   used non-custodially? Does Otium need AML registration?
9. **Earnings claims.** What documentation standard does Forbrukertilsynet expect under mfl. § 3 for earnings
   projections (sample size, update frequency, net of what)? Is a median of 3 users defensible?
10. **Free.** Is "Free to download" with the 2% fee next to it compliant with blacklist no. 20, given that the user
    also carries electricity and hardware costs?
11. **Conflict of laws.** What are the Norwegian rules for consumer contracts, given that Norway isn't bound by Rome
    I? Is a Norwegian-law clause with a C-191/15-style carve-out enough for EU, UK and other users?
12. **Product liability.** When does the new PLD apply to software that updates continuously and was first released
    before 9 Dec 2026? When will the PLD be incorporated into the EEA? Does the 1988 produktansvarsloven cover
    standalone software?
13. **CRA.** Is the Cyber Resilience Act part of the EEA Agreement yet? Must a Norwegian manufacturer selling into the
    EU file CRA vulnerability reports from 11 Sep 2026?
14. **Quick start.** Does turning on internet sharing and statistics sharing with a single "Start earning" button
    breach UCPD art. 7 or 8 or GDPR consent rules, given the summary shown first? Is a separate tick needed for the
    partner's terms to bind the user?
15. **Legal form.** Should the operator use an AS rather than an ENK, for limited liability and to avoid publishing a
    home address?
16. **Insurance.** Is product or professional liability insurance for hardware-damage claims available, and does it
    make sense?
17. **Regions outside the EEA.** The US is routed differently. Which other big markets (UK under the DMCC Act 2024,
    Switzerland) need separate terms or claim reviews?
