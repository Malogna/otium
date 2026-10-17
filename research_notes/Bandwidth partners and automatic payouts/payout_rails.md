# Payout rails for automatic mass payouts (as of October 2026)

Scope: rails a tiny Norway/EEA operator (Otium) can use to pay many users $2–$50 automatically by API, for earnings from crypto mining and bandwidth sharing. Research done 2026-10-16. About 20 searches and fetches. Many provider pricing pages could not be fetched or are quote-only, so several rails are covered only partly; see Gaps.

## 1. Rail-by-rail comparison: allowed business types, onboarding, coverage, fees at $2–$50, API, tax forms, KYC, funding, failures

### Takeaway
The gift-card/incentive platform Tremendous is the cheapest automatic rail for tiny payouts (gift cards at face value; PayPal/Venmo at 4% with a $0.25 minimum, and the API is free). It also collects W-9s and prepares 1099s. Stripe Connect is effectively ruled out: as of 2026-09-22 Stripe lists "Cryptocurrency mining and staking" as prohibited. PayPal Payouts (about 2% per payout, capped, plus FX) is still the natural primary cash rail once PayPal approves it. Trolley, Hyperwallet and Payoneer are priced for larger B2B/creator payouts, and their per-payment bank fees ($1–$4+) make no sense at $2–$5. Wise, Revolut and Airwallex are business bank accounts with batch-transfer APIs, not consumer-payout platforms, and their crypto policies are unverified.

### Cited Findings

**PayPal Payouts (and Venmo)**
- The sender pays a percentage of each payout, up to a cap. The percentage and cap vary by sending country and by domestic or international. PayPal says it "typically" applies a variable fee of 2%. Recipients are not charged, and the funding account must cover the payouts plus fees in the currency sent. The page shows no date. — [PayPal Developer: Payouts fees](https://developer.paypal.com/payouts/fees.md)
- US Venmo payouts cost the same as PayPal payouts. Payouts that need currency conversion carry extra fees (amount not stated on the page). — [PayPal Developer: Payouts fees](https://developer.paypal.com/payouts/fees.md)
- Per-item limits: USD 60,000 to registered and 20,000 to unregistered recipients; EUR 55,000 / 17,000; NOK 650,000 / 200,000. Unregistered recipients can create an account to claim the payout. — [PayPal Developer: Payouts fees](https://developer.paypal.com/payouts/fees.md)
- Example regional fee tables: 2.00% of the payout, capped per currency, with international caps such as 40 EUR, 60 CAD and 60 AUD in one table. The Israel schedule dated July 2026 adds VAT (2.36% effective), so some markets add VAT on top. — [PayPal Developer docs overview](https://docs.paypal.ai/growth/payouts/overview); [PayPal IL merchant fees, 6 July 2026 (PDF)](https://www.paypalobjects.com/marketing/ua/pdf/IL/en/il-en-merchant-fees-06-july-2026.pdf)
- Third-party blogs put PayPal's currency-conversion spread at about 3% above the base rate. This is unverified and varies by pair. — [ecomcalctools: PayPal business fees 2026](https://ecomcalctools.com/blog/paypal-business-fees-2026/); [dodopayments blog](https://dodopayments.com/blogs/paypal-business-fees-hidden-costs-2026/)
- Honeygain (a bandwidth-sharing app) pays through PayPal Payouts, which shows PayPal does approve bandwidth-reward businesses. Honeygain support says PayPal fees are "charges applied by PayPal on their end". — [Honeygain blog: PayPal payouts step by step](https://honeygain.com/blog/paypal-payouts-step-by-step-instructions); [Honeygain reply on Trustpilot](https://ca.trustpilot.com/review/honeygain.com?page=2)

**Tremendous (gift cards, prepaid Visa, PayPal, Venmo, bank, international wallets)**
- Pricing page, read October 2026 (footer ©2026, no effective date):
  - The platform, bulk sending, API and integrations are free. Fraud-prevention and tax tools are free.
  - Funding by bank transfer from a US, EU or UK account is free. Funding by credit card costs 3%.
  - Gift cards, prepaid Visa and charity donations are sent at face value.
  - Venmo, PayPal, Cash App, international digital wallets and bank transfers cost 4–6%. The FAQ says Venmo/PayPal is 4% with a $0.25 minimum, or 6% if the fee is passed to the recipient.
  - Volume discounts start above $200K/year.
  — [Tremendous pricing](https://www.tremendous.com/pricing/)
- Catalog covers "over 200 countries" and 2,300+ options. Automatic currency conversion is a listed feature, and no separate FX fee is stated. — [Tremendous pricing](https://www.tremendous.com/pricing/)
- Lists automated W-9 collection and 1099 preparation. Also lists fraud prevention, order approvals, spend limits, account-takeover prevention, and SOC 2. — [Tremendous pricing](https://www.tremendous.com/pricing/)
- The balance can be held in USD, EUR or GBP, and the API supports a EUR `currency_code`. A partner says Tremendous handles currency conversion, money movement and "cross-border regulations". — [Tremendous dev docs: handling currencies](https://developers.tremendous.com/docs/handling-currencies); [Extole docs: Tremendous](https://docs.extole.com/technical/partners/rewards-and-payments/tremendous); [GrowSurf guide](https://growsurf.com/integration-guides/tremendous-referral-reward-funding.md)

**Trolley (formerly Payment Rails)**
- Pricing is partly by quote, and third-party listings conflict:
  - Capterra lists plans: Standard Sync $35/mo, Standard Pay $49/mo, Standard Tax $199/mo. — [Capterra: Trolley](https://www.capterra.com/p/179294/Payment-Rails/)
  - Another listing says the bundled Standard plan is 7,197 USD/year. — [erpresearch: Trolley](https://erpresearch.com/erp-add-ons/global-payouts/trolley)
- Listed per-payment fees:
  - Domestic ACH $1.00
  - SEPA, FPS, EFT, NPP and BECS $4.00
  - Wire with FX $10.00
  - FX 2.00%
  - PayPal $0 from Trolley, but PayPal's own fees apply
  — [erpresearch](https://erpresearch.com/erp-add-ons/global-payouts/trolley); [fitgap](https://us.fitgap.com/products/058361/trolley)
- A different source says 0.5–1% per transaction plus a $500/month minimum. This conflicts with the per-method fees above. — [softabase: Trolley](https://softabase.com/en/software/accounting/trolley)
- Trolley collects W-9/W-8BEN at onboarding and automates 1099 and 1042-S production and distribution. — [Trolley: freelancer payouts](https://trolley.com/freelancer-payouts); [Trolley vs Payoneer](https://trolley.com/trolley-vs-payoneer/)
- Trolley's official pricing page (trolley.com/pricing/) returned 404 when fetched in October 2026.

**Payoneer Mass Payouts / Hyperwallet (PayPal)**
- Payoneer (from Tipalti, a competitor):
  - From 2025, Payoneer-to-Payoneer transfers are no longer free: $4 per same-country payment. — [Tipalti: Payoneer vs Hyperwallet](https://tipalti.com/resources/learn/payoneer-vs-hyperwallet/)
  - Receiving to a local bank from a non-Payoneer account costs 1%. — [Tipalti: Payoneer vs Hyperwallet](https://tipalti.com/resources/learn/payoneer-vs-hyperwallet/)
  - Batch limits are given as 200 per batch in one table and 1,000 per file (US) in another. — [Tipalti: Payoneer vs Hyperwallet](https://tipalti.com/resources/learn/payoneer-vs-hyperwallet/)
- Hyperwallet (from Wise and Tipalti, both competitors):
  - Recipient withdrawal fees: credit card $1.75, debit card $2.50, bank transfer $3. — [Wise: Hyperwallet review](https://wise.com/us/blog/hyperwallet-review)
  - A $3.00/month account fee after 90 days. — [Tipalti: Payoneer vs Hyperwallet](https://tipalti.com/resources/learn/payoneer-vs-hyperwallet/)
  - Up to 5,000 payments per transaction. — [Wise AU: Hyperwallet](https://wise.com/au/blog/hyperwallet-business-australia)
- Vendr lists Hyperwallet as sales-quoted, enterprise-style pricing. — [Vendr: Hyperwallet](https://www.vendr.com/marketplace/hyperwallet)

**Wise Business (batch transfers / API)**
- A batch can hold up to 1,000 payments, uploaded by spreadsheet. — [Wise: business invoices/batch](https://wise.com/au/business/invoices)
- Each transfer in a batch carries its own fee, and the batch total is the sum. — [Wise help: how do I pay for my batch](https://www.transferwise.com/help/articles/2942762/how-do-i-pay-for-my-batch)
- The API is free. Wise quotes the mid-market rate plus a transparent fee. — [Wise help: what's the Wise API](https://www.wise.com/help/articles/2958106/whats-the-wise-api)
- Wise's business acceptable-use URL returned 404, so its stance on crypto-mining businesses was not verified.

**Stripe Connect**
- Stripe's restricted list, last updated 2026-09-22, makes the following prohibited:
  - "Cryptocurrency mining and staking" (Non-fiat currency)
  - "schemes that claim to offer high rewards for very little effort or up-front work"
  - "Peer-to-peer money transmission"
  — [Stripe restricted businesses](https://stripe.com/legal/restricted-businesses)
- Stripe also restricts "Money transmitters, remittances … and other money service businesses" and crypto exchanges/wallets. It lists no bandwidth or proxy entries. — [Stripe restricted businesses](https://stripe.com/legal/restricted-businesses)
- Stripe's FAQ attributes these exclusions to financial-partner requirements and processor obligations. — [Stripe support FAQ](https://support.stripe.com/questions/prohibited-and-restricted-businesses-list-faqs)

**Stablecoin payouts (USDC)**
- Coinbase Business Payouts sends USDC to a wallet address, ENS name or email. Coinbase says transactions on Base are free for sender and recipient. — [Coinbase Business: payouts](https://www.coinbase.com/business/features-payouts)
- Payouts can be funded from the platform balance or a connected bank. — [Finextra: Coinbase rolls out business payment tools](https://www.finextra.com/pressarticle/107570/coinbase-rolls-out-business-payment-tools)
- Reported yield on held USDC is 3.35% APY on the Coinbase page, versus 4.1% in older coverage. — [Coinbase Business: payouts](https://www.coinbase.com/business/features-payouts); [Finextra](https://www.finextra.com/pressarticle/107570/coinbase-rolls-out-business-payment-tools)
- MassPay–Coinbase partnership (announced 11 June 2026): platforms on MassPay can fund payouts in USD (converted to USDC via Coinbase) or deposit USDC, covering about 180 countries. The 40–70% savings versus wires is MassPay's own unverified claim. — [MarketScreener](https://www.marketscreener.com/news/masspay-and-coinbase-partner-to-bring-stablecoin-powered-payouts-to-businesses-worldwide-ce7f5cd9df80f22c); [Stablecoin Insider](https://stablecoininsider.org/masspay-coinbase-stablecoin-payouts-2026/)

### Inferences

**Fees at $2–$50** (estimates from the figures above)
- PayPal at about 2%: about $0.04–$1.00 per payout, plus about 3% FX spread if currencies differ. This is the cheapest cash option for PayPal holders.
- Tremendous PayPal/Venmo: 4% with a $0.25 floor, so $0.25 on a $2–$6.25 payout and $2.00 on $50. Gift cards cost $0.
- Trolley SEPA: $4.00 per payment, 200% of a $2 cash-out.
- Hyperwallet: the recipient pays $3 to withdraw to a bank.
- Conclusion: only PayPal, gift cards and on-chain crypto are economic at $2–$10.

**Which rail fits Otium**
- Tremendous fits a tiny operator best because there is no subscription, the API is free and gift cards are free. Whether it accepts a non-US (Norwegian) company, and a crypto-mining reward use case, is unverified and must be asked of Tremendous sales (see Gaps).
- Monero or USDC on-chain payouts carry no third-party underwriting risk and cost almost nothing per send (Base USDC is free via Coinbase Business, Monero network fees are small). The operator remains the payer, though, and carries the AML and regulatory exposure (see section 2).

**Stripe**
- Stripe Connect is not viable, because "Cryptocurrency mining and staking" is prohibited outright.
- Stripe might accept a bandwidth-only entity, but the "high rewards for very little effort" clause is a risk for get-paid-to apps. Treat Stripe as excluded.

**Recommendation (for the report writer to weigh)**
- Primary: PayPal Payouts. It has the lowest fee for cash, Honeygain proves PayPal accepts bandwidth-reward apps, and the code already exists. Otium must get PayPal's pre-approval.
- Backup or parallel: Tremendous for gift cards worldwide at face value, plus PayPal/Venmo/bank/wallets where PayPal Payouts is unavailable. It also provides W-9/1099 handling for US users.
- Keep Monero (and optionally USDC on Base) as the crypto-native rail.
- Trolley, Payoneer and Hyperwallet make sense only if average payouts grow well above $50.

### Gaps
- **Tremendous:** no source found on whether it onboards non-US companies or a Norwegian ENK/AS. No source on whether its acceptable-use terms exclude crypto-mining or GPT-style reward businesses. The AUP was not located. Ask sales directly.
- **Not researched (budget limit):** no 2026 primary data was gathered for Tango Card/Runa, Revolut Business API, Airwallex, Paysend or Circle (Mint/Payments API). Background knowledge, unverified for 2026:
  - Runa and Tango offer gift-card and some cash payouts via API.
  - Revolut Business and Airwallex offer bulk-transfer APIs, but their acceptable-use policies restrict crypto activity.
  - Circle's business accounts require KYB and typically larger volumes.
  - Verify each before relying on it.
- **Payoneer:** official mass-payout pricing for the sender was not found. Hyperwallet sender-side fees are quote-only.
- **PayPal:** the exact Norway-sender international Payouts fee and cap, and the FX spread, were not found on an official page. Check PayPal's NO merchant-fees page.
- **Trolley:** its official pricing page returned 404, and third-party prices conflict.
- **Wise:** its acceptable-use policy on crypto mining, and its NOK/EUR fees for very small transfers, were not verified.
- **Recipient KYC and failure reporting:** detailed per-provider coverage was not gathered. From general knowledge, PayPal Payouts and Tremendous provide webhooks or status endpoints for unclaimed, returned and failed items, and unclaimed PayPal payouts are returned after 30 days. Not verified in this session.

## 2. Regulatory burden: which rails make the provider the regulated payer (Norway/EEA and US), flagged for counsel

### Takeaway
Paying users' own earned balances by bank or PayPal through a licensed payout provider generally leaves the provider as the regulated payment institution. Otium, however, holds user balances, which may itself raise payment-services or e-money questions in the EEA, and money-transmitter questions in the US. Gift cards and incentives have their own regimes: the PSD2 limited-network exclusion, applied inconsistently across countries, and FinCEN's prepaid-access rules. This session found no Norway-specific guidance. This area needs counsel.

### Cited Findings
- **EBA guidelines.** The EBA's final Guidelines on the PSD2 limited-network exclusion set how a network or range of goods is judged "limited" and how firms notify authorities. — [EBA: Guidelines on the limited network exclusion under PSD2](https://www.eba.europa.eu/regulation-and-policy/payment-services-and-electronic-money/guidelines-limited-network-exclusion-under-psd2)
- **UK reading of the exclusion.** The FCA says the exclusion covers instruments usable only in a limited way, such as some gift cards, fuel cards and store cards. — [FCA: scope and exclusions under PSRs/EMRs](https://www.fca.org.uk/firms/revised-payment-services-directive-psd2/scope-exclusions)
- **National regulators differ.** Austria's FMA treats non-personalised gift cards as not payment instruments, while BaFin holds they may fall under the ZAG. National application of the exclusion is "handled completely differently" across member states. — [EBA consultation response (Finnius)](https://www.eba.europa.eu/system/files/webform/documents/ddm-fileupload/1022070/Response%20Finnius%20advocaten%20to%20EBA%27s%20consulation%20on%20the%20draft%20guidelines%20limited%20network%20exclusion%20under%20PSD2.pdf); [PaySys report Nov 2021](https://paysys.de/en/wp-content/uploads/2022/11/2021_-6-7-report-november.pdf)
- **Stripe's view of money transmission.** Stripe classes "Money transmitters … and other money service businesses" as restricted and "Peer-to-peer money transmission" as prohibited. Payment providers will look at whether Otium itself looks like a money transmitter. — [Stripe restricted businesses](https://stripe.com/legal/restricted-businesses)
- **Tremendous on cross-border rules.** Tremendous is described as handling "currency conversions, money movement, and cross-border regulations" for senders. — [Extole docs: Tremendous](https://docs.extole.com/docs/tremendous)

### Inferences
- Outsourcing payouts makes the provider the regulated payer (PayPal is a Luxembourg-licensed bank in the EEA; Tremendous holds US licences or partners). Otium then pays its own users for services rendered, which is closer to paying contractors or rewards than to transmitting third-party funds. This is the lowest-burden structure. Counsel should confirm.
- Otium's own Monero or USDC payouts make it a crypto sender. In the EEA, MiCA and AML rules (CASP status) could apply if Otium is seen as transferring crypto on behalf of others. Paying its own obligations in crypto may fall outside that. Needs counsel.
- US: FinCEN's prepaid-access rule exempts closed-loop gift cards up to $2,000 per device per day. Payments for services are generally not money transmission. These are general knowledge, not verified this session.

### Gaps
- No Finanstilsynet page or Norwegian law-firm note was found on: payout-holding apps, the limited-network exclusion in Norway, the commercial-agent exemption, or how PSD3/PSR (status in 2026) changes these.
- No FinCEN source was fetched. The MSB, prepaid-access and rewards-program analysis above is unverified background.
- US tax reporting is not settled here. The $2,000 1099-NEC/MISC threshold (raised from $600 for payments from 2026 under the 2025 US tax law) is the operator's stated assumption. PayPal's 1099-K threshold rules were not verified.

## 3. What comparable reward apps use (2025–2026)

### Takeaway
Bandwidth and compute reward apps converge on PayPal plus gift cards plus crypto, with minimums of $5–$20:
- Pawns: $5 minimum. PayPal, Venmo, bank, gift cards, Bitcoin.
- Honeygain: $20 minimum. PayPal or JumpTask crypto.
- Salad: PayPal and gift cards from a rewards storefront, within 24 hours.
- Kryptex: PayPal via LTC, gift cards.

### Cited Findings
- **Honeygain.** $20 minimum (10 credits = $0.01). Payouts by PayPal (2–3 business days) or JumpTask crypto (up to a week). Support points users who want to avoid PayPal fees to JumpTask. — [Honeygain blog](https://honeygain.com/blog/paypal-payouts-step-by-step-instructions); [Honeygain on Trustpilot](https://ca.trustpilot.com/review/honeygain.com?page=2)
  - Unverified third-party claims of about 15% PayPal fees and a $50 Bitcoin minimum conflict with Honeygain's own statements. — [codegenes](https://www.codegenes.net/blog/honeygain-download/); [Gerald](https://joingerald.com/learn/work--income/honeygain-review-how-it-works)
- **Pawns.app.**
  - $5 cash-out minimum. Methods: PayPal, Venmo, bank transfer, "hundreds" of gift cards (Amazon, Visa, Walmart) and Bitcoin. Usually paid within minutes, up to 72 hours. Withdrawals are confirmed by email. Some methods "may have small withdrawal fees". — [Pawns cashout](https://pawns.app/cashout/)
  - Finder reports a 4% PayPal fee. — [Finder: Pawns review](https://finder.com/ca/make-money-online/pawns-app-review)
  - The choice of Venmo, gift cards and bank alongside PayPal strongly resembles a Tremendous-style catalog. Inference, not confirmed.
- **Salad.** PayPal rewards are bought in the Salad storefront and processed by an unnamed "provider" within 24 hours. The user must be in a supported country. — [Salad support: PayPal rewards FAQ](https://support.salad.com/article/228-faq-on-paypal-rewards); [Salad: how to redeem PayPal](https://support.salad.com/rewards/redeeming-your-rewards/how-to-redeem-paypal)
- **Kryptex.** Its PayPal route has a 0.015 LTC minimum and a 0.01 LTC fee. Kryptex sends LTC to the user's PayPal crypto address rather than using PayPal Payouts in fiat, which avoids PayPal's crypto-business approval. — [Kryptex: PayPal guide](https://www.kryptex.com/articles/kryptex-paypal-en)

### Inferences
- Kryptex's approach (send crypto to a PayPal crypto address) is a practical workaround for a mining app. Otium could offer "LTC or BTC to your PayPal or Coinbase address" alongside Monero, without PayPal Payouts approval. Availability is limited to countries where PayPal supports crypto (e.g. the US and some EU countries).
- A $5 minimum with PayPal, gift cards and crypto is the market norm. A $2 minimum is generous and only economic on PayPal Payouts, gift cards or crypto.

### Gaps
- Swagbucks, Mistplay and other GPT-site payout providers and minimums were not researched this session. From general knowledge, Swagbucks uses PayPal and gift cards from about $5 and Mistplay uses gift cards only. Unverified.
- Salad's payout provider and minimum were not confirmed.

## 4. Fraud and abuse controls that could replace manual review

### Takeaway
Tremendous advertises built-in fraud prevention, order approvals, spend limits and account-takeover protection. PayPal Payouts relies on recipients' existing KYC'd PayPal accounts. Pawns uses email confirmation of withdrawals. No provider was found that replaces Otium's own review of duplicate destinations and velocity. The best mix is the provider's tools plus Otium's own rules.

### Cited Findings
- Tremendous lists "fraud prevention tooling, order approvals, spend limits, and account takeover prevention", is SOC 2 compliant, and charges nothing for fraud and tax tools. — [Tremendous pricing](https://www.tremendous.com/pricing/)
- PayPal distinguishes registered from unregistered recipients and applies lower limits to unregistered ones (e.g. USD 20,000 versus 60,000 per item). Recipients must hold or create a PayPal account to claim. — [PayPal Developer: Payouts fees](https://developer.paypal.com/payouts/fees.md)
- Pawns requires email verification of each withdrawal and locks the payout method once a withdrawal is in progress. — [Pawns cashout](https://pawns.app/cashout/)
- Trolley's onboarding collects tax forms (W-9/W-8BEN) and identity data. This gives TIN-based de-duplication for US users. — [Trolley: freelancer payouts](https://trolley.com/freelancer-payouts)

### Inferences
Otium could auto-approve with these controls:
- Pay only to PayPal accounts (KYC'd by PayPal) or through Tremendous (which runs its own fraud screening).
- Require email confirmation per cash-out, as Pawns does.
- Keep Otium's own checks on duplicate destinations, velocity and new-account age.
- Have the provider (Tremendous or Trolley) collect W-9s from US users, which automates the $2,000/1099 hold.

Manual review could then be limited to flagged cases.

### Gaps
- No primary source was found for any provider's sanctions-screening or duplicate-recipient detection specifics, or how it is exposed via API.
- Velocity-limit features beyond Tremendous's "spend limits" were not documented in the sources reviewed.
