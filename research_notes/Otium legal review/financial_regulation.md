# Otium: where it stands under financial regulation (crypto-assets, money transmission, AML). Research notes as of 19 Oct 2026

These notes feed a legal review that a lawyer will check. They are not legal advice. A source URL follows each claim. Tags used below:
- **[SECONDARY]**: the source is a law firm or press report, not a primary source.
- **[VERIFY]**: I could not confirm it against the primary text in this session.
- **[HISTORICAL]**: the rule has been superseded.

---

## 0. The facts as the regulators would see them

| Otium activity | What actually happens | First-pass regulatory label |
|---|---|---|
| Users mine with XMRig/SRBMiner to their own address on unMineable or MoneroOcean | The pool pays the user directly. Otium never touches the funds. | Mining plus a pool payout. Generally outside MiCA and FinCEN rules (§1.6, §3.1). |
| 2% fee mining (about 29 min per 24 h to Otium's address) | Otium is mining for itself, paid by the pool. | Otium's own mining income. No service to clients. Taxable (§6). |
| unMineable referral code (Otium receives 0.25–0.5% of users' rewards out of unMineable's fee) | An affiliate commission from a non-EEA pool that converts coins. | Not a CASP service by Otium. Possible issue of soliciting EEA users for a third-country provider (§1.5). |
| Bandwidth payout conversion: Otium's server asks Relay for a quote and deposit address, the user's local client sends USDC there, Relay's solvers convert and pay the user's address, and Otium earns a 2% Relay "app fee". SideShift/ChangeNOW are used for LTC/XMR. | Non-custodial. However, Otium is an identifiable operator. It picks the route, requests the order server-side and is paid per conversion. | **The main exposure.** Possibly "reception and transmission of orders" (RTO) under MiCA, "arranging" under the UK regime, and "brokering" under US rules (§1.3, §3, §4). |
| Referrals (for 6 months, a quarter of Otium's fee slice is mined to the inviter's address) | The miner switches login to the inviter's address. | Not a financial service. Tax is the inviter's matter. Sanctions screening of payout addresses is relevant (§5). |
| Dormant "custodial mode" | Otium would hold balances and pay cash-outs. | Would need MiCA CASP authorisation for custody/transfer (and possibly exchange). It would also need PSD2/EMI rules for e-money-token (EMT) or fiat legs, FinCEN MSB registration plus US state licences, and UK MLR registration or FSMA authorisation. **Keep it off.** |

Relay mechanics, from the provider's own docs:
- Integrators set app fees in basis points of the input value. The fees accrue to an **off-chain USDC balance** claimable by an EVM address, so they are not paid to Otium on-chain per transaction. https://docs.relay.link/features/app-fees
- Relay also offers integrators a **revenue share on its own platform fees**. https://docs.relay.link/references/api/api_core_concepts/fees
- Solvers fill orders with their own capital, and user deposits go to a Depository contract. The protocol uses authorised signers and MPC (an operator-run model), and is not described as permissionless. https://docs.relay.link/references/protocol/how-it-works
- **Implication:** Relay is unlikely to be "fully decentralised without any intermediary" in the sense of MiCA recital 22. So Otium cannot shelter behind Relay's decentralisation.

---

## 1. EU MiCA as applied in Norway

### 1.1 Status in Norway
- MiCA was incorporated into the EEA Agreement by EEA Joint Committee decision of 20 Feb 2025 (Annex IX no. 31r). https://finanstilsynet.no/regelverk/virtuelle-valutatjenester ; https://lovdata.no/dokument/NL/lov/2025-05-27-20
- **Lov om kryptoeiendeler (kryptoeiendelsloven), LOV-2025-05-27-20**, in force **1 July 2025**:
  - § 1 makes MiCA apply as Norwegian law.
  - § 2 makes Finanstilsynet the competent authority.
  - Preparatory works: Prop. 55 LS (2024–2025) and Innst. 254 L (2024–2025).
  - https://lovdata.no/dokument/NL/lov/2025-05-27-20
- Finanstilsynet: "For å tilby kryptoeiendelstjenester i EØS kreves det som hovedregel tillatelse etter kryptoeiendelsforordningen". A CASP must have a physical presence in the EEA. https://finanstilsynet.no/tema/kryptoeiendeler-mica
- **Sanctions under the act:**
  - Breach of MiCA art. 59 (providing services without authorisation) is a criminal offence with up to **3 years** (§ 18).
  - Administrative fines apply (§ 17).
  - Finanstilsynet can order websites blocked or domain names deleted as a last resort (§ 8).
  - https://lovdata.no/dokument/NL/lov/2025-05-27-20
- **Transitional period, now expired:**
  - § 19(2) originally let providers registered under the AML rules continue until 30 Dec 2025.
  - Finanstilsynet extended this by regulation of 18 Dec 2025 to **30 June 2026**. https://www.finanstilsynet.no/nyhetsarkiv/nyheter/2025/utvidelse-av-overgangsordningen-etter-kryptoeiendelsloven/
  - The EU-wide ceiling under MiCA art. 143(3) was 1 July 2026. ESMA and Finanstilsynet told unauthorised providers to wind down immediately and stop onboarding. https://www.finanstilsynet.no/nyhetsarkiv/nyheter/2026/uttalelse-om-utlopet-av-overgangsordningen-etter-kryptoeiendelsloven-mica/
  - **As of Oct 2026 there is no transitional relief. Any CASP activity needs a MiCA authorisation.**
- First Norwegian CASP authorisations were granted in 2026 (e.g., AK Jensen Norway AS, Týr Markets AS). https://www.finanstilsynet.no/nyhetsarkiv/nyheter/2026/tyr-markets-as-far-tillatelse-som-casp-foretak/
- I found **no Finanstilsynet guidance specific to DeFi, front-ends or non-custodial software.** Its pages restate the MiCA list of services only. https://finanstilsynet.no/tema/kryptoeiendeler-mica

### 1.2 The MiCA tests that matter (Regulation (EU) 2023/1114)
https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=CELEX:32023R1114

- **CASP (art. 3(1)(15)):** a legal person or other undertaking whose occupation or business is "the provision of one or more crypto-asset services to clients on a professional basis". Art. 59 requires authorisation.
- **Crypto-asset services (art. 3(1)(16)):** the relevant ones are (a) custody and administration on behalf of clients, (c)/(d) exchange of crypto-assets for funds or for other crypto-assets, (e) execution of orders on behalf of clients, (g) **reception and transmission of orders on behalf of clients**, (h) advice, and (j) transfer services on behalf of clients.
- **Art. 3(1)(19)–(20), exchange:** the CASP concludes contracts "using proprietary capital". Otium does not do this.
- **Art. 3(1)(23), RTO:** receiving from a person an order to buy or sell one or more crypto-assets, and transmitting that order to a third party for execution.
- **Art. 3(1)(24), advice:** personalised recommendations. Otium automatically choosing a route is unlikely to be personalised advice.
- **Art. 3(1)(26), transfer services:** transfer "on behalf of a natural or legal person" of crypto-assets from one DLT address or account to another.
- **Recital 22:** MiCA applies "including when part of such activities or services is performed in a decentralised manner". Only "Where crypto-asset services are provided in a fully decentralised manner without any intermediary" are they out of scope.
- **Recital 83:** "Hardware or software providers of non-custodial wallets should not fall within the scope of this Regulation."
- **Mining:** MiCA does not regulate mining or validation. Art. 4(3)(b) exempts crypto-assets "automatically created as a reward for the maintenance of the distributed ledger or the validation of transactions" from the offer-to-the-public (white paper) rules. Recitals 7 and 26 refer to consensus or validation. There is **no** mining-related crypto-asset service. **Conclusion: Otium's mining and the receipt of mining rewards (users' and Otium's own) are outside MiCA's service perimeter.**
- **Art. 80(2) together with ESMA Q&A 2087 (29 Jan 2024):** CASPs providing RTO or execution may **not** receive remuneration, discounts or non-monetary benefits for routing client orders to a particular trading platform or another CASP. This confirms that the ban applies to both RTO and execution. https://www.esma.europa.eu/publications-data/questions-answers/2087
  - **Relevance:** a Relay/ChangeNOW revenue share for routing would be prohibited even for a licensed RTO provider. An app fee charged openly to the user as Otium's own commission is arguably different. **[VERIFY with counsel]**
- **Capital and authorisation burden:** if RTO or transfer services were in scope, they are class 1 under Annex IV (minimum €50,000 permanent capital). Authorisation also brings governance, a complaints function, conflicts management, and the AML/TFR obligations in §2. Annex IV of MiCA, same EUR-Lex URL.

### 1.3 Is the Relay conversion with an app fee "RTO"? Analysis

**Arguments that it is in scope:**
1. There is an "order": the user's standing instruction to pay out in coin X means that each conversion of USDC into X is an order to sell USDC and buy X.
2. Otium's **server** requests the quote and deposit address from Relay, a third party that executes through solvers' own capital. Functionally, that is receiving the user's instruction and transmitting it to a third party for execution (art. 3(1)(23)). ESMA Q&A 2653 says RTO applies "when a CASP transmits orders to a third-party for execution or onward transmission". It also says classification turns on operational reality, and that in doubt with retail clients a conservative approach applies. https://www.esma.europa.eu/publications-data/questions-answers/2653
3. "Professional basis" and "clients": Otium is a business earning a fee per conversion (2%).
4. Recital 22 is narrow:
   - ESMA's response to the Commission's MiCA review (30 Sep 2026, ESMA75-113276571-1721) reports a "divergence of views" on "full decentralisation".
   - It warns of "decentralisation washing, where an identifiable operator relies on DeFi-type language to avoid MiCA obligations".
   - It says the DeFi exemption "should be as narrow as possible".
   - It proposes a new regulated **"gatekeeper" service** for CASPs that "provides a technical interface enabling clients to interact with DeFi protocols" or "facilitates transaction routing". Obligations would include transparency on "protocol selection and routing practices".
   - https://www.esma.europa.eu/sites/default/files/2026-09/ESMA75-113276571-1721_Response_to_the_EC_consultation_MiCA_regulation_review.pdf
   - This is a **policy proposal, not law**. It shows that ESMA regards interfaces and routing by identifiable operators as intermediation that is either currently in scope or should be. The same paper says "open-source development, self-custody, automated smart contracts and permissionless infrastructure should not automatically amount to regulated intermediation".
5. The EBA/ESMA joint report of January 2025 under art. 142 cautions that industry labels such as "DeFi" do not settle the level of decentralisation for the purposes of recital 22. https://www.esma.europa.eu/sites/default/files/2025-01/ESMA75-453128700-1391_Joint_Report_on_recent_developments_in_crypto-assets__Art_142_MiCA_.pdf

**Arguments that it is out of scope:**
1. Otium never holds the funds or keys. The user's own software signs and sends to Relay's Depository, consistent with recital 83 (non-custodial wallet software).
2. There is arguably no "order received from a person" per conversion. The software converts automatically according to the user's configuration, much like a wallet with built-in swap.
3. Otium does not act "on behalf of clients" in a legal agency sense. It supplies software and requests a public quote.

**Assessment:** **genuinely uncertain, leaning toward risk.** No ESMA Q&A, ESMA guideline or Finanstilsynet statement addresses a non-custodial app that requests swap quotes server-side and earns a per-swap fee **[VERIFY: searched the ESMA Q&A tool and did not find one]**. The facts that worsen Otium's position are:
- the server-side quote request;
- Otium choosing the route and provider;
- the per-conversion fee;
- the absence of user confirmation per conversion;
- the server supplying the destination address that the local client pays automatically.

### 1.4 Transfer services and custody
- **Custody, art. 3(1)(16)(a):** not provided, provided the Proxybase/Tempo key is generated and kept only on the user's device and is never transmitted to Otium.
- **Transfer services, art. 3(1)(26):** ESMA Q&A 2071 says transfer services are a self-standing service even when part of another service. https://www.esma.europa.eu/publications-data/questions-answers/2071
  - Otium's local client automatically sends USDC to a server-supplied address. That is defensible as non-custodial wallet software (recital 83).
  - However, an Otium server that can steer where user funds go weakens the "user controls" narrative.
  - ESMA Q&A 2932 (on transient holding by non-authorised entities) was submitted 1 Oct 2026 and is unanswered, forwarded to the Commission. https://www.esma.europa.eu/publications-data/questions-answers/2932
- **EMT/PSD2 overlap:** USDC is an e-money token.
  - The EBA no-action letter (10 Jun 2025) told national authorities to require PSD2 authorisation from **2 March 2026** for CASP transfers of EMTs and for custodial EMT wallets. It said not to treat exchange of crypto for crypto, or intermediation, as payment services.
  - This matters only if Otium were a CASP doing transfers or custody. https://www.eba.europa.eu/publications-and-media/press-releases/eba-publishes-no-action-letter-interplay-between-payment-services-directive-psd23-and-markets-crypto

### 1.5 Referrals to third-country providers (unMineable, SideShift, ChangeNOW)
- Under MiCA art. 61 (reverse solicitation), a third-country firm may serve EU/EEA clients without authorisation **only** at the client's exclusive initiative.
  - Solicitation "through an entity acting on its behalf" defeats the exemption.
  - ESMA's guidelines (final report ESMA35-1872330276-1899, 17 Dec 2024) treat the exemption as very narrow and scrutinise third-party promotion and referrals. https://www.esma.europa.eu/document/final-report-guidelines-reverse-solicitation-under-markets-crypto-assets-regulation-mica
- **Risk:** Otium is an EEA entity that routes EEA users to SideShift/ChangeNOW (custodial instant exchangers, which clearly do crypto-for-crypto exchange) and earns affiliate fees. This could be seen as soliciting for unauthorised third-country CASPs. **[VERIFY: whether SideShift and ChangeNOW hold MiCA authorisation or block EEA users. I did not confirm this. Check the ESMA interim MiCA register.]**
- For unMineable, the conversion is incidental to pool payouts. Whether a pool's conversion of mined coins counts as an "exchange" CASP service is untested **[VERIFY]**. Lower risk.

### 1.6 Coming next (future law)
- The EU AML Regulation 2024/1624 (CASPs are obliged entities) applies from 10 July 2027. EEA incorporation is pending **[VERIFY]**.
- The Commission's MiCA review may add a statutory DeFi definition and the "gatekeeper" service (ESMA paper above).

---

## 2. Norwegian AML (hvitvaskingsloven)

- **Old regime [HISTORICAL]:**
  - From 15 Oct 2018, providers of exchange services between virtual currency and official currency, and custody services for virtual currency, had to **register with Finanstilsynet** (hvitvaskingsloven § 4 fifth paragraph; hvitvaskingsforskriften § 1-3). https://lovdata.no/dokument/SF/forskrift/2018-09-14-1324
  - From memory, it did not cover crypto-to-crypto exchange or non-custodial wallets **[VERIFY]**.
- **Repeal:** hvitvaskingsforskriften § 1-3 was "opphevet ved forskrift 28 mai 2025 nr. 904 (i kraft 1 juli 2025)". https://lovdata.no/dokument/SF/forskrift/2018-09-14-1324
- **New regime:** LOV-2025-05-27-19 implements the Transfer of Funds Regulation (EU) 2023/1113. It adds **§ 4(1)(p) "kryptoeiendelstjenesteytere"** as reporting entities.
  - The definition in § 2(m) is a CASP as defined in MiCA art. 3(1)(15) providing art. 3(1)(16) services, **excluding advice (h)**.
  - The law also adds § 17 a, which requires a risk assessment and at least one enhanced due-diligence measure for transfers to or from self-hosted addresses, and § 52, which applies the TFR as Norwegian law.
  - https://lovdata.no/dokument/LTI/lov/2025-05-27-19 ; https://lovdata.no/dokument/NL/lov/2018-06-01-23
  - The registration route was a transitional bridge, with registered firms allowed to continue until 30 June 2026 (§1.1).
- **Answer: there is no longer any separate AML "registration" duty to consider. AML status now follows MiCA status.**
  - If Otium is not a CASP, it is not a reporting entity under hvitvaskingsloven.
  - If any activity is a CASP service, Otium needs MiCA authorisation and becomes a full AML/TFR reporting entity (KYC, travel rule, self-hosted address controls). That is hard to reconcile with an anonymous, free desktop app.

---

## 3. United States

### 3.1 FinCEN guidance FIN-2019-G001 (9 May 2019; still FinCEN's operative guidance, not withdrawn as far as I found)
https://www.fincen.gov/sites/default/files/2019-05/FinCEN%20Guidance%20CVC%20FINAL%20508.pdf

- **§ 4.2.1, unhosted wallets:** these are software on the user's device, and the owner "has total independent control over the value". An unhosted wallet **provider** that does not accept and transmit value is not a money transmitter.
- **§ 4.5.1(b) together with 31 CFR 1010.100(ff)(5)(ii)(A):** suppliers of "delivery, communication, or network access services" and software tools "are engaged in trade and not money transmission".
- **§ 5.1:** a platform that only provides a forum where parties settle "through individual wallets or other wallets not hosted by the trading platform" is not a money transmitter.
- **§ 4.4, DApps:** when DApps "accept and transmit value", the money-transmitter definition applies "to the DApp, the owners/operators of the DApp, or both". This applies regardless of profit.
- **§ 5.4, mining pools:** a pool leader's distribution of rewards to members "does not qualify as money transmission" because it is integral to the service, unless the pool also hosts wallets. The 2013 guidance, recapped in § 3, says the method of obtaining CVC (including "mining") does not by itself make someone an exchanger. FIN-2014-R001 says miners using CVC on their own account are users.
- **Caution, § 3 recap of 2013 guidance:** exchangers are money transmitters "regardless of whether the exchangers are directly brokering the transactions between two or more persons" or are parties to them. "Brokering" is the hook a regulator could use against a fee-earning router, **but** money transmission still requires accepting and transmitting value. Otium does neither.
- **§ 1.1 caveat:** the interpretations apply only to identical facts. A person can be exempt as a software provider yet still be a money transmitter if it uses the software to accept and transmit value.
- **Territorial reach:** a foreign-located MSB is covered if doing business "wholly or in substantial part within the United States" (31 CFR 1010.100(ff)). This is engaged if conversions reach US users. **[VERIFY exact current text]**
- **Assessment:** under FinCEN's guidance, the non-custodial Relay flow with an app fee is **probably not** money transmission, because Otium never accepts or transmits value. The risk factor is the server's ability to set the destination address. That could be argued as "control" over the transmission path (see the BRCA test in §3.3).

### 3.2 Criminal enforcement developments (18 U.S.C. § 1960)
- **DOJ "Ending Regulation by Prosecution" memo (DAG Blanche, 7 Apr 2025):** prosecutors should not charge regulatory violations such as unlicensed money transmission under § 1960(b)(1)(A)–(B) without evidence the defendant knew of the licensing requirement. It also says DOJ will stop targeting mixers, exchanges and "offline wallets" for their end users' acts. This is policy, not law, and can change. https://www.justice.gov/dag/media/1395781/dl
- **Galeotti remarks (21 Aug 2025):** where software is "truly decentralized and solely automates peer-to-peer transactions", and "a third party does not have custody and control over user assets", new § 1960(b)(1)(C) charges against the third party "will not be approved". https://www.justice.gov/opa/speech/acting-assistant-attorney-general-matthew-r-galeotti-delivers-remarks-american
  - **Otium does not cleanly fit:** it is not "truly decentralized", and it routes to a centrally operated solver network.
- **Roman Storm (Tornado Cash):** the jury convicted Storm on the § 1960 conspiracy count (Aug 2025) and hung on money laundering and sanctions. DOJ sought a retrial of the hung counts. The retrial was reportedly adjourned to **April 2027**, with an acquittal motion pending. **[SECONDARY]** https://www.theblock.co/post/392937/roman-storm-tornado-cash-retrial ; https://www.osl.com/en/bits/article/roman-storms-tornado-cash-retrial-pushed-to-april-2027-3e39441b46
- **Samourai Wallet:** the founders pleaded guilty to conspiracy to operate an unlicensed money transmitting business (2025). They were sentenced in **Nov 2025** to 5 and 4 years, with forfeiture equal to the fees earned. https://www.justice.gov/usao-sdny/pr/founders-samourai-wallet-cryptocurrency-mixing-service-sentenced-five-and-four-years
  - A presidential pardon review was publicly floated in Dec 2025. Outcome unknown **[VERIFY]**.
  - **Lesson:** fee-earning, non-custodial software operators have been prosecuted under § 1960 where facts were bad (marketing to criminals, coordination). Fee forfeiture is a real remedy.

### 3.3 Legislation
- **CLARITY Act (H.R. 3633):** passed the House on 17 Jul 2025 (294–134). Senate Banking advanced its version 15–9 on 14 May 2026. **Senate cloture failed 49–50 on 15 Sep 2026.** A motion to reconsider is pending, and passage this Congress is widely viewed as unlikely. **[SECONDARY]** https://www.orrick.com/en/Insights/2026/10/The-CLARITY-Act-Stalls-in-the-Senate-Whats-Next-for-Digital-Asset-Regulation ; bill page https://www.congress.gov/bill/119th-congress/house-bill/3633
- **Blockchain Regulatory Certainty Act (BRCA):**
  - Incorporated into both House and Senate CLARITY texts. The Senate Banking text adds a criminal-law savings clause.
  - It protects developers or providers without the legal right or unilateral ability to "control, initiate, or effectuate transactions" involving user assets from money-transmitter classification.
  - **Not enacted.** **[SECONDARY]** https://coincenter.org/the-brca-is-ready-to-become-law/
  - **Note for Otium:** under that test, a server that can make the client send funds to a server-chosen address could be argued to "initiate" transactions. That is a design fix to make now (§7).
- **GENIUS Act (stablecoins, signed 18 Jul 2025):** regulates payment-stablecoin issuers, so it is not directly relevant to Otium **[VERIFY Pub. L. number]**.

### 3.4 State money-transmission laws
- Most state money transmitter licences (MTLs) turn on receiving money or value for transmission. A non-custodial flow is generally outside them, but definitions vary by state.
- **New York (23 NYCRR 200.2(q)):** "The development and dissemination of software in and of itself does not constitute Virtual Currency Business Activity". https://regulations.justia.com/states/new-york/title-23/chapter-i/part-200/section-200-2 (third-party copy; **[VERIFY]** against the official NYCRR)
- **California DFAL:** licensing has been live since **1 July 2026**. https://dfpi.ca.gov/digital-financial-assets/
  - Fin. Code § 3103(b)(7)(A) exempts a person that "contributes only connectivity software or computing power to securing a network" or to a protocol. https://california.public.law/codes/financial_code_section_3103
  - DFAL's "exchange" concept is control-based (it means assuming control of the asset) **[VERIFY Fin. Code § 3102 text]**.
  - A $50,000 de minimis exemption reportedly exists **[SECONDARY, VERIFY]**.
- **CSBS Money Transmission Modernization Act (MTMA):** adopted in full by about 13 states, but only ME, MN and VT took the virtual-currency article, as of July 2026. **[SECONDARY]** https://www.vixio.com/articles/money-transmission-modernization-act-adoption-across-us-states
- **Practical point:** Otium already sends US users to MoneroOcean (native XMR, no conversion). The cleanest US position is to **geoblock the conversion and affiliate-exchange features for US IPs**. Then no state-by-state analysis is needed.

---

## 4. United Kingdom

- **Current law (MLRs 2017, reg. 14A):** a "cryptoasset exchange provider" includes anyone who by way of business provides "exchanging, or arranging or making arrangements with a view to the exchange of, one cryptoasset for another". https://www.legislation.gov.uk/uksi/2017/692/regulation/14A
  - **The "arranging" limb plausibly captures the Relay route-and-fee model if Otium carries on business in the UK.**
- **Territorial test (FCA, updated 8 Oct 2026):**
  - Having "no UK office or other UK activity, beyond simply having a client in the UK" likely means no UK business.
  - A UK office or a UK ATM is a factor pointing the other way.
  - The FCA page does **not** address servers.
  - https://www.fca.org.uk/firms/cryptoassets-aml-ctf-regime/cryptoassets-who-needs-register
  - **Otium's London AWS Lightsail server is an open point [VERIFY with UK counsel].** Moving the quote server out of the UK is cheap risk reduction.
- **Financial promotions:** the cryptoasset financial promotion regime "applies to all firms marketing cryptoassets to UK consumers regardless of where the firm is based". Unregistered firms need approval by an authorised person or an exemption (same FCA URL). In-app messaging that invites UK users to convert or acquire coins could be a financial promotion **[VERIFY]**.
- **New regime:**
  - The Financial Services and Markets Act 2000 (Cryptoassets) Regulations 2026 bring new regulated activities into force on **25 Oct 2027**, including arranging deals in qualifying cryptoassets (RAO art. 9Y). Core FCA rules were published 30 Jun 2026 (PS26/9–13). Perimeter guidance PS26/18 was published 16 Sep 2026.
  - The authorisation gateway is open **30 Sep 2026 to 28 Feb 2027** for transitional benefit.
  - https://www.fca.org.uk/publication/policy/ps26-18.pdf ; https://www.fca.org.uk/firms/new-regime-cryptoasset-regulation
- **Key passages from PS26/18:**
  - "Developing software is not a regulated activity as such". The persons needing authorisation "will typically be those person(s) who provide access to, or use of, that software".
  - "where a website host or app provider is providing users with the means by which they can place orders, this is likely to amount to" arranging.
  - Interfaces to automated protocols are assessed case by case for an "identifiable person".
  - The FCA's consultation version had no technical-services exclusion from arranging. A government SI now adds "certain technical services" exclusions, and the FCA will consult on them **[VERIFY final SI text]**.
  - **Overseas reach (FSMA s. 418(6C)):** an overseas person "involved in the sale or subscription of a qualifying cryptoasset to, or by, a consumer in the UK" is deemed to act in the UK, unless an authorised dealer is interposed.
  - **Implication:** from Oct 2027, the conversion feature offered to UK consumers very likely needs FCA authorisation, or must be geoblocked, or must run through an FCA-authorised dealer.
- **Mining:** no UK regulated activity covers it. Validator-type technical services are discussed in PS26/18 only in a staking context.

---

## 5. Sanctions

- **EU and Norway on Russia:**
  - Reg. 833/2014 art. 5b(2), as amended by Reg. 2025/2033 (19th package), now prohibits **"crypto-asset services"** to Russian nationals, natural persons residing in Russia, and Russian-established entities. Previously the ban covered only wallet, account and custody services. EC FAQ of 13 Mar 2026: https://finance.ec.europa.eu/document/download/8b0e76f4-6a5e-4fd3-a85e-04d7ecdfe340_en?filename=faqs-sanctions-russia-payments-services_en.pdf
  - Norway implemented the 19th package in FOR-2014-08-15-1076, in force **27 Mar 2026**. It will also join the 20th package, which includes crypto measures; that legislative work is ongoing. https://www.regjeringen.no/no/aktuelt/nye-sanksjoner-mot-russland-er-innfort-i-norsk-rett/id3155117/
  - **If any Otium activity is a "crypto-asset service", serving Russian (and similarly Belarusian **[VERIFY]**) users is a sanctions breach on top of a licensing breach.**
- **Other EU and Norwegian regimes (Iran, DPRK, Syria, etc.):** mostly list-based (asset freezes and making funds available), plus sectoral bans. DPRK and the occupied Ukrainian regions have broader prohibitions **[VERIFY per regime]**.
  - Otium *receiving* fee-mining coins generated by users in those countries is not, as such, a listed prohibition.
  - Otium *paying* referral rewards to a designated person's address would be "making funds available".
- **OFAC (Oct 2021 Sanctions Compliance Guidance for the Virtual Currency Industry):**
  - Strict liability.
  - "Any transaction that causes a violation — including a transaction by a non-U.S. person that causes a U.S. person to violate sanctions" is prohibited.
  - Recommends "geolocation tools and IP address blocking controls".
  - https://ofac.treasury.gov/media/913571/download?inline
- **Otium's US nexus:** AWS, Cloudflare and likely GitHub or US code-signing vendors. Serving Iran, Cuba, DPRK or Crimea/DNR/LNR through US infrastructure is the realistic "causing" exposure, and it is also a breach of those providers' terms.
  - The Syria program was largely lifted from 1 Jul 2025 (EO 14312) **[VERIFY current status]**.
- **Recommendation:**
  - Geoblock the download, the API and the miner config for comprehensively sanctioned jurisdictions (OFAC and EU/Norway).
  - Additionally block the conversion feature for RU/BY.
  - Screen Otium's referral-payout and conversion destination addresses against the OFAC SDN digital-currency address list.
  - Record IP country at registration and heartbeat (data-minimised).

---

## 6. Tax for the operator (Norway; brief, for a tax adviser)

- **Skatteetaten's rule:** mined crypto is taxable as it is received ("Kryptovaluta blir skattepliktig etter hvert som du mottar kryptovaluta som følge av mining"). It is valued at market value in NOK at the time it is mined, using Norges Bank rates, and that value becomes the cost basis on later sale. In business, income is recognised when there is an unconditional right ("ubetinget rett"). https://www.skatteetaten.no/person/skatt/hjelp-til-riktig-skatt/aksjer-og-verdipapirer/om/virtuell-valuta/mining/
- **Application to Otium:** fee-mining receipts, Relay app fees (accruing as a claimable USDC balance), and pool, SideShift and ChangeNOW affiliate commissions are business income of the operator. Otium should keep per-receipt NOK valuation logs.
- **VAT:**
  - A 2018 principle statement treats mining itself as VAT-exempt financial activity (mval. § 3-6), but it is not final ("følge rettsutviklingen nøye"). https://www.skatteetaten.no/en/rettskilder/type/uttalelser/prinsipputtalelser/utvinning-av-kryptovaluta/
  - **Open question for the adviser:** whether Otium's supply of software to users *in exchange for* 2% of their hashrate is a VAT-able barter supply of electronic services. That is a separate issue from mining.
- Users bear their own income tax on mining and bandwidth earnings. A user-facing tax notice is advisable.

---

## 7. Concrete recommendations (ordered by risk reduction per effort)

1. **Take Otium's server out of the order path.**
   - Have the user's local client request the Relay quote and deposit address directly from Relay's public API, or use the provider's own widget or SDK, and sign locally.
   - The client should verify that the deposit address belongs to the Relay Depository flow before sending.
   - Otium's server must never be able to supply or alter a destination address. This addresses the BRCA "initiate" test and the MiCA "transmission of orders" theory.
2. **Per-conversion user confirmation.**
   - Show the provider name, route, rate, all fees (Relay fee and Otium's 2%), and the provider's terms.
   - Get an explicit "Convert" click, or at least an opt-in standing instruction that can be revoked, with each execution logged and visible.
   - Default to **no conversion**: pay USDC to the user's own address and let them convert where they choose.
3. **Reconsider the app fee on conversions in the EEA (decide with counsel).**
   - A per-swap fee is the strongest fact pointing to "professional" RTO.
   - Options:
     - (a) drop the conversion fee and monetise through mining and bandwidth margin only;
     - (b) send users to a **MiCA-authorised CASP's** own front-end or a licensed partner, with Otium as a disclosed, non-routing referrer (subject to Art. 80(2) and inducement rules);
     - (c) apply for MiCA authorisation (class 1, €50k capital, AML/KYC). This is unrealistic for an anonymous free app.
   - Do **not** take routing revenue shares (Relay platform-fee share, ChangeNOW affiliate share). ESMA Q&A 2087 shows that is prohibited even for licensed RTO providers.
4. **Stop routing EEA users to unauthorised third-country exchangers (SideShift, ChangeNOW)** unless they are confirmed MiCA-authorised. Alternatively, offer LTC/XMR payouts only via mining (unMineable or MoneroOcean pay natively).
5. **Geoblock:**
   - comprehensively sanctioned jurisdictions (all features);
   - RU/BY for the conversion feature;
   - **US** for conversion and affiliate-exchange features (already mining-only via MoneroOcean);
   - **UK** for conversion before 25 Oct 2027 at the latest. Consider now, given MLR "arranging" and promotion rules.
   - Consider moving the API server out of London.
6. **Wording.**
   - Never say "we convert", "we pay you", "your balance" or "withdraw". Say that "your app sends your USDC to [Relay], an independent service, which converts it and pays your address; Otium charges X%".
   - Remove investment and return language ("earn passive income" is acceptable, "returns" or "yield" is not).
   - State in the ToS and privacy policy that Otium does not custody, exchange or transmit funds, and name the third parties.
7. **Document (an evidence file for Finanstilsynet or a lawyer):**
   - an architecture memo showing keys are generated and kept on the device, are never transmitted, and that the server cannot move or redirect funds;
   - a fee-flow diagram;
   - a list of third-party providers and their licence status;
   - the geoblocking list and the sanctions-screening procedure;
   - a decision log showing custodial mode is disabled and gated behind licensing.
8. **Entity:** set up a Norwegian AS before monetisation scales. This limits personal exposure, given the § 18 criminal provisions.
9. **Consider a no-action-type dialogue.** Finanstilsynet offers dialogue and has a fintech sandbox/innovation hub **[VERIFY current name and terms]**. A written query on the conversion design (after fixes 1–3) would give comfort before scaling.

---

## Risk assessment for Otium (as of Oct 2026)

| Area | Risk | Why |
|---|---|---|
| Mining to user's address / fee mining / referral mining | **Low** | MiCA does not cover mining. FinCEN § 5.4 and FIN-2014-R001. No custody. |
| unMineable referral commission | **Low to medium** | Not a CASP service by Otium. The open question is whether it counts as solicitation for a non-EEA converter (MiCA art. 61). |
| Relay conversion with 2% app fee, server-side quotes, automatic sending (EEA) | **Medium to high** | Plausible RTO by an identifiable operator. ESMA is pushing a narrow DeFi exemption. There is no transitional relief after 30 Jun 2026. Up to 3 years criminal exposure for art. 59 breaches. |
| SideShift/ChangeNOW routing with affiliate fees (EEA) | **Medium to high** | Routing to likely unauthorised custodial exchangers. Art. 80(2) concerns about inducements. |
| Norwegian AML registration | **Low** (no separate duty) | The registration regime was repealed 1 Jul 2025. AML status follows MiCA status. |
| US (if conversion reaches US users) | **Low to medium** | No accept-and-transmit under FIN-2019-G001. DOJ policy is favourable but discretionary. BRCA is not enacted. Storm and Samourai show the downside. Geoblocking removes the question. |
| UK | **Medium now, high from 25 Oct 2027** | MLR "arranging exchange" limb. London server. Promotion rules. New art. 9Y arranging with s. 418(6C) overseas reach. |
| Sanctions | **Medium** without geoblocking, **low** with it | OFAC "causing" through US infrastructure. EU/Norway RU/BY crypto-service ban if any CASP characterisation applies. |
| Custodial mode | **High** if enabled | Requires MiCA, PSD2/EMI, FinCEN and state, and UK licences. Keep it off. |

---

## Questions for counsel (Norwegian fintech lawyer)

1. Does the Relay flow amount to "reception and transmission of orders" (art. 3(1)(16)(g)) or "transfer services" (j) under kryptoeiendelsloven/MiCA:
   - (a) as currently built (server-side quote, automatic send, 2% app fee); and
   - (b) after the changes in §7 items 1–3 (client-side quote, per-conversion confirmation, with and without a fee)?
2. Does recital 83 (non-custodial wallet software) cover an app that bundles a locally generated key, automatic payouts and a built-in swap from which the developer earns a fee? Has Finanstilsynet expressed any view?
3. Is an app fee charged openly to the user different, under art. 80(2) and ESMA Q&A 2087, from a routing payment by the venue? Are Relay's platform-fee revenue share and ChangeNOW affiliate fees prohibited inducements?
4. Does referring EEA users to unMineable, SideShift or ChangeNOW for affiliate revenue make Otium a person "acting on behalf of" a third-country firm under MiCA art. 61? Which of these providers hold MiCA authorisation?
5. Is a pool's conversion of mined coins into a payout coin (unMineable) a crypto-asset service in its own right?
6. Is Otium a reporting entity under hvitvaskingsloven § 4(1)(p) on any reading? If so, what is the minimum compliant design?
7. Would Finanstilsynet entertain an informal pre-application or innovation-hub dialogue on the redesigned flow? What should the written query contain?
8. Personal versus AS liability under kryptoeiendelsloven §§ 17–18. Timing of incorporating an AS.
9. Sanctions:
   - Does Norway's FOR-2014-08-15-1076, after the 19th and 20th packages, reach non-custodial swap facilitation for RU/BY users?
   - What geoblocking standard is expected (IP only, or plus self-declaration)?
   - Does receiving fee-mining proceeds from users in Iran or DPRK raise Norwegian sanctions issues?
10. UK (separate UK counsel):
    - Does a London-hosted API server mean "carrying on business in the UK" under the MLRs?
    - Does the conversion feature fall under the MLR "arranging" limb now, or under art. 9Y from 25 Oct 2027?
    - Do the new technical-services exclusions in the government SI help?
11. US (separate US counsel): with conversion geoblocked for US users, is any residual FinCEN or state exposure left from mining-only US users (MoneroOcean) and fee mining?
12. Tax adviser: VAT treatment of software supplied in exchange for hashrate. Valuation and recognition timing for Relay's accruing off-chain USDC app-fee balance.
