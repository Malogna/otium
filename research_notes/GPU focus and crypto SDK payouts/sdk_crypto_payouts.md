# Bandwidth publisher SDKs: who pays the publisher in crypto (Oct 2026)

Question: which bandwidth or proxy publisher SDKs (an app embeds the SDK and the vendor pays the app developer) pay the publisher in crypto? Would any of them let Otium, a Windows-first desktop app, pass most of the money on to its users? In that model Otium is custodial: it receives the vendor payout, then pays users in crypto minus a fee.

Method: official SDK pages, FAQs and partner terms, read with WebFetch and WebSearch on 2026-10-17. Nothing was signed up for, downloaded or run. Several vendor pages are JavaScript-rendered or 403/404, and those gaps are noted. "Not stated" means the public page and terms are silent, which usually means the answer is in a contract you get after applying.

## 1. SDKs with evidence of crypto payouts to the publisher

### Traffmonetizer SDK (DataImpulse / Softoria)
- **Crypto:** yes. The developer page says developers can "withdraw money on-demand using BTC, Paypal, Skrill, Payeer Wire Transfer". BTC is the only coin named. Reviews of the consumer app say user cash-outs are now USDT TRC-20, with wire for $1,000 and up. — https://traffmonetizer.com/for-developers/ ; https://paidfromsurveys.com/traffmonetizer-review
- **Minimum and frequency:** withdrawals are on demand. No minimum is stated for developers. Consumer reviews report a $10 minimum.
- **Rate:** "$0.10 per 1 GB of traffic". The developer can cap the traffic shared.
- **Desktop:** Windows, macOS, Android and a Docker CLI (`traffmonetizer/cli_v2`). No C, C++ or .NET library is documented.
- **KYC:** not stated.
- **Paying users:** not addressed.
- **Consent:** none required on the page. It says the SDK "runs in the background and does not affect user experience". That is a red flag.
- **Risk: HIGH.**
  - Traffmonetizer is the IP-sourcing app and SDK for DataImpulse (owned by Softoria), and Synthient rates DataImpulse "HIGH RISK". — https://proxyway.com/reviews/dataimpulse ; https://synthient.com/context/provider/DATAIMPULSE
  - It was found hidden in the Flix Vision app with a remote switch to turn it on. — https://github.com/fmhy/edit/issues/2835
  - Trustpilot has complaints about crypto withdrawals not being approved. — https://au.trustpilot.com/review/traffmonetizer.com

### PacketSDK (IPIDEA group; Castar, Earn and Hex SDKs are siblings)
- **Crypto:** yes. The PacketSDK FAQ article "What payment methods are available?" lists PayPal, USDT on TRC-20, and bank cards (seen via the search index). — https://faq.packetsdk.com/en/articles/10326987-what-payment-methods-are-available
- **Minimum and frequency:** NET45, paid on the 15th of the month, for average DAU with consent. — https://www.businessofapps.com/?p=99777
- **Rate:** vendor posts claim up to $0.60 per active user per month in the US.
- **Desktop:** Windows, Linux, macOS, Android, iOS and Android TV.
- **Status:** in January 2026 Google's GTIG disrupted IPIDEA. A court order took down SDK domains (packetsdk.io, castarsdk.com, earnsdk.io), Play Protect now removes the SDKs, and GTIG found more than 550 threat groups using IPIDEA exit nodes. packetsdk.com and castarsdk.com did not resolve from this environment (Oct 2026). — https://securityaffairs.com/187463/security/google-targets-ipidea-in-crackdown-on-global-residential-proxy-networks ; https://en.wikipedia.org/wiki/Ipidea
- **Risk: DO NOT USE.**

### EarnFM "Fleetshare" SDK
- **Crypto:** yes, but only on the marketing page. The apps page lists "Pay creators monthly (bank, PayPal, crypto)" (seen via the search index, because the page is JS-rendered). — https://earn.fm/en/business/apps
- The SDK docs describe something narrower. Partners withdraw through "the standard EarnFM account" with "the standard minimum withdrawal amount of $15". Bank transfer by invoice (SEPA or ACH) is available above $300 per month. — https://sdk-docs.earn.fm/overview/
- The coin list is not published for partners. Third-party consumer reviews mention USDT on BEP-20, TRC-20 and Polygon (MATIC) with a $15 minimum. That is unverified. — https://paidfromsurveys.com/earnfm-review
- **Rate:** $0.10/GB residential and $0.04/GB datacenter. Marketing says partners get "$120–$200 per 1,000 devices" online, which is $0.12–0.20 per device per month.
- **Desktop:**
  - Windows works via `earnfm_sdk.exe <api_key>`, run as a subprocess. — https://sdk-docs.earn.fm/integration-guides/windows-generic/
  - There is also an in-process C# library (`FleetShareSDK.StartFleetShareSDK(apiKey, silent, deviceName)`), with files supplied by the team. — https://sdk-docs.earn.fm/integration-guides/csharp/
  - Java, Node and Python guides exist too. Browser extensions are not supported.
- **KYC:** not stated.
- **Paying users:** incentives are explicitly allowed, for example "an ad free experience or free credits". Cash is not addressed.
- **Consent:** opt-in before any sharing, a plain-language screen, and an opt-out toggle. "every consent decision must be recorded with EarnFM through the Consent API", and devices without consent are rejected. — https://sdk-docs.earn.fm/get-started/user-consent/
- **Risk: MEDIUM.** Trustpilot reviews are mixed (slow payouts, slow support) and per-GB rates have fallen. download.earn.fm serves an "InfiniteProxies" page, which suggests a shared operator. — https://it.trustpilot.com/review/earn.fm

### MystNodes SDK (Mysterium)
- **Crypto: probably, but unconfirmed.** Node earnings are MYST on Polygon. The SDK client page says nothing about how developers are paid and needs "the token obtained from mystnodes team". — https://hub.docker.com/r/mysteriumnetwork/mystnodes-sdk-client
- **Desktop:** Windows, Android, macOS and Linux. The client was updated recently.
- **Fee:** the network takes a 20% fee. — https://docs.mysterium.network/faq
- **Consent:** not documented.
- **Next step:** ask the MystNodes team whether SDK partner earnings settle in MYST to a wallet. If they do, it fits Otium's non-custodial model better than any fiat SDK. See also `research_notes/Non-custodial bandwidth/`.

## 2. SDKs that do not pay the publisher in crypto (or say nothing about it)

| SDK | Publisher payout | Rate | Desktop / languages | KYC | Paying users | Source |
|---|---|---|---|---|---|---|
| Honeygain SDK | "direct bank transfers or PayPal". Terms need banking info. $50 minimum rolls over. Net 30 from invoice | Per monthly avg DAU. Site says "up to $500 per average DAU monthly" (likely per 1,000). Proxyway estimates $100–400 per 1,000 DAU | All OS, Windows included. Unity | ID verification may be required (cl. 2.6) | FAQ suggests "premium features, bonuses and rewards". Terms are silent on cash | https://sdk.honeygain.com/faq ; https://sdk.honeygain.com/tos-for-partners/ ; https://proxyway.com/research/internet-sharing-sdks-app-monetization |
| Bright SDK (Bright Data) | "Wire, PayPal and Payoneer", against an invoice, within 10 days of month-end | $100–350 per 1,000 IPs per month (Proxyway) | Windows, macOS, mobile and TV | "KYP": a partnership manager onboards you and every app is reviewed | Not addressed. **Policy lists "Crypto Mining & Cryptojacking" as a restricted vertical** | https://help.bright-sdk.com/hc/en-us/articles/16551122467089-How-do-I-get-paid ; https://bright-sdk.com/acceptable-partnership-policy |
| Massive | FAQ says "monthly via PayPal". Partner page says "PayPal or wire" | $0.05–0.20 per user per month | Windows 7+ 64-bit, macOS 10.10+, Android | Not stated | Users opt in for "a perk you define — premium features, ad removal, currency, etc." | https://www.joinmassive.com/faq ; https://www.joinmassive.com/getpaid |
| Decodo | Not stated (monthly) | Up to $0.10–0.20 per active user per month (page contradicts itself) | Windows, macOS, Android, iOS | Not stated | "Users must be compensated for being part of the network" | https://decodo.com/monetize |
| Infatica SDK | Not stated | Up to $0.60 per user per month. Needs 50k+ MAU | Windows and macOS software and games, mobile | Not stated | Not stated. Requires an EULA update | https://infatica-sdk.io/ |
| Pawns.app SDK (IPRoyal) | Not stated. Terms need "payment and banking information" | $0.20/GB | Android, iOS, Windows, macOS, Linux | Verification, possibly by a third-party ID service (1.5, 3.6.1, 3.13) | **Allowed.** The partner sets remuneration rules in its own terms (3.6.4(c)) and indemnifies Pawns for incentives (6.11(d)) | https://business.pawns.app/sdk/ ; https://business.pawns.app/sdk-terms/ |
| Cash Raven (Rayobyte) | Not stated. Monthly; user side pays via Tipalti | Up to $0.50 per user per month (a user counts after 60 min of sharing a day) | Native `cr_sdk.dll`, macOS dylib, `libcr_sdk.so`. C/C++, C#, Go, Python, Rust and JS bindings, Electron | "Publishers are verified and KYC'd before payout" | Not addressed (older FAQ: in-game currency, ad-free) | https://cashraven.io/ ; https://cashraven.io/platforms ; https://cashraven.io/earnings |
| Live Proxies | Not stated | Not stated | Windows, macOS, Android | Not stated | "ad-free experiences" | https://liveproxies.io/sdk-monetization |
| SOAX | SDK ("Partner Solutions") exists. Terms not public | – | – | – | – | https://soax.com/legal/privacy-policy |
| Repocket | SDK page exists. Terms not public | 4–6 cents per MAU (third party) | – | – | Users get "rewards" | https://repocket.webflow.io/sdk-integration |

### No publisher SDK found
- **PacketStream:** "no SDK". Users are paid by PayPal at $0.10/GB. — https://packetstream.io/
- **Proxyrack:** a "Become a Peer" desktop app only. — https://www.proxyrack.com/become-a-peer/
- **Spider Peers:** an agent only. Payouts are in USD via Stripe. — https://peers.spider.cloud/
- **Grass, Oxylabs, NetNut, Proxy-Cheap:** no publisher SDK found.
  - NetNut sources IPs from ISPs. — https://www.cbinsights.com/investor/soax
  - Oxylabs has an exclusive deal with Honeygain (2022). — https://proxyway.com/research/internet-sharing-sdks-app-monetization

## 3. Reputation context
- QuoIntelligence (Sep 2026) criticises SDK opt-in wording that "does not readily convey" that a residential IP will carry third-party traffic. — https://quointelligence.eu/2026/09/residential-proxies-hide-inside-everyday-apps-users-carry-the-risk/
- Unit 42 (Aug 2025) found attackers deploying unmodified, legitimate bandwidth SDKs on hacked servers. Expect antivirus and EDR attention for any proxy SDK. — https://unit42.paloaltonetworks.com/attackers-sell-your-bandwidth-using-sdks/

## 4. Takeaways for Otium
- No reputable SDK confirms crypto payouts to the publisher in its public terms.
  - The vendors that do pay crypto are compromised (PacketSDK/IPIDEA) or high-risk (Traffmonetizer).
  - EarnFM advertises crypto, but its SDK docs only describe a $15-minimum account withdrawal.
- The practical model is fiat in, crypto out. Otium receives bank, PayPal or Payoneer payouts, converts them, and pays users in USDT or another coin. The rails are in `Bandwidth partners and automatic payouts/crypto_payout_swaps.md` and `payout_rails.md`.
- **Pawns.app SDK** is the only contract that explicitly contemplates the partner paying users. Clause 3.6.4 says the partner sets the remuneration, and Pawns is not obliged to pay it.
  - It also has the strictest consent rules: off by default, an affirmative toggle, consent separate from the general terms, an always-available opt-out, 18+, and consent records kept for 24 months.
- **Cash Raven** has the best desktop integration (native DLL with C# bindings) and a published per-user rate, but its terms on passing cash to users are unknown.
- **Bright SDK** restricts "Crypto Mining & Cryptojacking" apps. Otium's mining feature could block onboarding, so ask before applying.
- **Custodial pass-through** makes Otium a payer of user earnings. That means KYC/AML exposure and tax forms (see `Selling idle compute for money/apps_architecture_legal.md`).
- **Questions for any vendor:**
  - Can the payout be USDT or USDC to a corporate wallet?
  - Are cash or crypto pass-through rewards to users allowed?
  - Is there per-device reporting via API, so payouts can be attributed to each user?
  - Do they accept apps that also mine?
