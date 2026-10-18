# Otium legal review (October 2026)

> **Not legal advice.** This review was prepared from public law, regulator guidance and partners' terms, as a
> starting point for a Norwegian lawyer. It finds the problems and proposes fixes; a lawyer has to confirm the
> conclusions marked **counsel** before v0.1.0. The research behind it, with a source for every claim, is in
> [research_notes/Otium legal review/](../research_notes/Otium%20legal%20review/):
> `financial_regulation.md`, `data_protection.md`, `consumer_law_and_terms.md`, `partners_sharing_and_countries.md`.

Assumed facts: the operator is in Norway (EEA), the server runs on AWS Lightsail in London behind Cloudflare, and the
app is offered worldwide for free, with Otium earning 2% of mining time, unMineable's referral share, and 2% on
converting internet-sharing earnings.

## Verdict

**Mining is low risk.** Mining to the user's own address, Otium's fee slices and referral mining aren't a regulated
financial service under MiCA (EEA) or FinCEN guidance (US), because Otium never holds or moves anyone's money.

**Five things should be settled before v0.1.0:**

| # | Issue | Risk | Status |
|---|---|---|---|
| 1 | **The 2% fee on converting internet-sharing earnings.** Otium's server picked the route, asked Relay (or SideShift/ChangeNOW) for the deposit address and earned a fee on each swap, with no confirmation by the user. That fits MiCA's "reception and transmission of orders", a licensed service. Unlicensed activity is a crime in Norway (kryptoeiendelsloven § 18, up to 3 years), and the transition period ended 30 June 2026. | **High → medium** | **Changed in this branch:** Otium keeps its 2%, but no longer from the swap. The app pays Otium's share from the user's sharing earnings straight to Otium's address, like the mining fee, and the conversion carries no Otium fee. Still open for counsel: the server still requests the conversion quote |
| 2 | **"Help show what PCs really earn" (now "Share analytics") is ticked by default.** Reading data on the user's PC and sending it for a purpose the service doesn't need requires consent (ekomloven § 3-15, ePrivacy art. 5(3)); a pre-ticked box is not consent (*Planet49*). | **High** | **Fixed in this branch:** a Yes/No question with nothing preselected (D2) |
| 3 | **No terms, no trader details, no withdrawal information.** Required for consumers (ehandelsloven § 8, angrerettloven § 8, markedsføringsloven § 10 a). Because users "pay" with mining time, Norway's digital-content law (digitalytelsesloven) applies in full. | **High** | Drafts added: [terms.md](terms.md), [privacy.md](privacy.md). You fill in the company details |
| 4 | **Absolute claims.** "Never costs you money", "Never at a loss" and "Nothing on your computer is accessed" can't be proved in every case; under digitalytelsesloven § 8 marketing claims become contract terms, so a user whose bill went up could claim damages. | **High** | **Fixed in this branch** |
| 5 | **Internet sharing's risk to users.** Proxybase's buyers aren't identity-checked and it keeps no buyer records, so misuse traces back only to the user's internet address. Telia, Telenor, Ice and Comcast forbid sharing or reselling the connection. Google and the FBI acted against "earn from your bandwidth" networks in 2026. | **High** for users' trust | **Fixed in this branch:** plain risk text, and Quick start asks with its own tick box |

The rest is medium or low and mostly paperwork or partner permissions (below).

## Changed in this branch

- **Claims.** The welcome screen, site and README no longer say "Never costs you money" or "Never at a loss". They say
  Otium pauses when electricity would likely cost more than it earns, based on the user's electricity price.
- **Internet-sharing consent.** "Nothing on your computer is accessed" is gone: Proxybase's client has no block on
  connections into the user's home network, so it can't be promised. The text now says what the client does, that
  Otium can't see or control the traffic, that misuse would show the user's internet address, and that some internet
  providers don't allow sharing a connection.
- **Privacy policy** ([privacy.md](privacy.md)): updated to the current app (Honeygain and Mysterium removed, problem
  reports, Proxybase, Relay, SideShift and ChangeNOW added), wallet addresses treated as personal data, retention
  periods proposed, transfers outside the EEA listed, minimum age 18.
- **Terms of use** ([terms.md](terms.md)): a first draft in plain language, following Norwegian consumer law.
- **Server logs** now rotate (`deploy/docker-compose.yml`), so IP addresses aren't kept indefinitely.
- **Internet-sharing fee (D1):** Otium's 2% is kept back from each sharing payout at Proxybase and, once $1 of it has
  built up, the app pays it to Otium's own address on Tempo (`Sharing:FeeRecipient`). Only the user's 98% goes into
  the conversion, which has no app fee, so Otium earns nothing from exchanging coins.
- **Quick start (D3):** internet sharing is its own tick box with the risk text, unticked; mining stays one click.
- **Countries (D4):** the app won't run, and the server won't register or serve accounts, in sanctioned countries
  (Cuba, Iran, North Korea, Syria, Russia, Belarus) or where mining is banned (China, Algeria, Nepal, Egypt,
  Bangladesh, Iraq, Morocco, Qatar, Tunisia, Angola, Kuwait). The app checks the Windows region and the server
  Cloudflare's visitor country; data export and deletion stay open everywhere. The payout step asks "I'm a US citizen
  or live in the US", which routes the user to MoneroOcean like a US computer. Not covered: Crimea and the occupied
  regions of Ukraine (no country code of their own; needs region-level IP data).

## On being an open-source project

Publishing the code is protected and carries little risk. Running the service is different: whoever runs otium.lol,
publishes the builds and receives the fees is the operator in the eyes of regulators, consumer authorities and courts,
company or not. Without a company that's a private person, personally liable, and consumer law still requires a name
and contact address on the site. The safest small step is an AS (D5); the fee redesign above lowers the main risk
either way.

## Decisions for you

**D1. The fee on converting internet-sharing earnings.** *Decided: keep 2%, paid directly instead of from the swap
(done in this branch).* The options were:
- **(a) Recommended for v0.1.0: no Otium fee on conversions.** Set `Sharing__FeeBps=0` on the server; conversion keeps
  working, and users still get paid in their own coin. Then, with counsel, move the quote request into the app (the
  user's computer asks Relay directly, so Otium's server can never set where money goes) before adding any fee back.
- (b) Keep the 2% and ask a fintech lawyer (and possibly Finanstilsynet's innovation hub) first. Release internet
  sharing only after a written opinion.
- (c) Pay internet sharing in USDC on Tempo to an address the user gives. No conversion, no licence question, but it
  breaks "one wallet, one coin".

Either way, **SideShift and ChangeNOW** (used for LTC and XMR payouts) add their own problems: they're unlicensed in
the EEA as far as found, ChangeNOW bans UK, US, Indian and Russian users and can seize their funds, SideShift bans US
users, and Otium's server sends them the user's IP address outside the EEA without safeguards. Recommendation: for
LTC and XMR users, either don't offer internet sharing payouts yet, or ask them to give a USDT or BTC address for it.

**D2. Earnings statistics opt-in.** *Decided and done:* a "Yes, share" / "No thanks" question at the end of both
setups, nothing preselected, Start waits for an answer; the app records when it was answered, and turning it off in
Settings deletes what the computer sent. Still to do: publish medians only from 5 or more computers. The background:
you asked for this to be on by default. Norwegian law requires an active
choice, and a ticked box shown during onboarding is exactly what the *Planet49* ruling rejected. What does work: ask
with two equal buttons ("Yes, share" / "No thanks"), neither preselected, before Start; most people say yes when asked
plainly. Keep a record of the answer; turning it off deletes that computer's raw statistics. Also publish medians
only from 5 or more computers, not 3.

**D3. Quick start turning on internet sharing.** *Decided and done:* its own tick box, unticked.

**D4. Countries.** *Decided and done* (see above). The list as researched:
- **Block entirely:** Cuba, Iran, North Korea, Syria, Russia, Belarus, Crimea and the occupied regions of Ukraine
  (sanctions; EU/Norway ban crypto services to Russians since the 19th package).
- **Block for mining bans:** China, Algeria (now a crime), Nepal, Egypt, Bangladesh, Iraq, Morocco, Qatar, Tunisia,
  Angola, Kuwait.
- **US persons:** unMineable excludes US citizens abroad too, so ask at onboarding ("Are you a US citizen or
  resident?") as well as checking the region.
- Detection: server-side IP country (Cloudflare already provides it) plus the Windows region plus that question.

**D5. Company.** Set up an AS before launch: it limits personal liability, and the terms, privacy policy and website
need a legal name, organisation number and address.

## Partners: what to ask

- **unMineable:** written OK to put Otium's referral code on every user's worker (their terms are silent). Their terms
  forfeit balances after 6 months without mining; the app should warn users who stop with a balance below the payout
  minimum. Otium's own fee address faces the same rule.
- **SRBMiner (doktor83):** its licence forbids "incorporating into another product" without written permission.
  Otium downloads and runs the official release unmodified, which is arguable, but ask for permission.
- **XMRig:** no GPL obligations while Otium downloads the unmodified release from GitHub and doesn't bundle or modify it.
- **Proxybase:** ask whether its servers block connections into sellers' home networks (private IP ranges) and port
  25, and whether they have seller terms. Their published terms only cover buyers.
- **Relay:** its terms forbid activity that needs a licence, which ties back to D1.
- **ChangeNOW:** its partner commission on XMR conversions (from 0.4%) is a payment for routing swaps; set it to 0 in
  ChangeNOW's partner dashboard, so Otium's only sharing income is its direct 2%.

## Data protection: remaining paperwork

- Records of processing (GDPR art. 30): required, the small-business exemption doesn't apply.
- A short DPIA: Norway's list arguably requires one, because Otium combines pool data with account data.
- Processor agreements with AWS, Cloudflare, the email provider and the backup host. Encrypt backups (rclone
  `crypt`) and keep them in the EEA or UK.
- Retention (proposed in privacy.md): logs 14 days, backups 14 days, raw statistics 90 days, problem reports 90 days,
  inactive accounts 24 months.
- Transfers: UK (AWS London) is covered by the renewed adequacy decision to 2031; Cloudflare and GitHub by the EU-US
  Data Privacy Framework. SideShift (St Kitts) and ChangeNOW (St Vincent) have no safeguard: see D1.
- Minimum age 18: Norwegian minors can't enter binding contracts, and unMineable and SideShift require adults.

## Consumer law: remaining items

- **Right of withdrawal.** Free apps "paid" with data or mining time have a 14-day withdrawal right; without the
  information it runs for 12 months. The draft terms explain it and offer a refund of Otium's fee share for the first
  14 days on request. An in-app "withdraw / delete my account" already exists (Delete my account).
- **Norwegian language.** Marketing aimed at Norway (Nordic electricity prices) needs the key information and terms in
  Norwegian (angrerettloven § 8). Translate terms.md once counsel approves it.
- **Earnings figures.** The two-minute test and the site's per-card figures should show they're estimates: a range,
  what they assume (hours a day, electricity price, today's coin prices) and, for medians, how many computers and when.
  Forbrukertilsynet's 28 May 2026 letter on income claims expects this. Keep a dated copy of the method and data.
- **Complaints.** The terms must name Forbrukertilsynet and Forbrukerklageutvalget. Don't link the EU ODR platform:
  it closed on 20 July 2025.
- **Hardware.** The EU's new product liability rules cover software from 9 December 2026 and can't be excluded by
  contract. Otium doesn't overclock; keep it that way, keep efficiency mode's power limit conservative, and keep test
  records.

## Antivirus and reputation

- Microsoft lists cryptomining software as a potentially unwanted category on business PCs, and Malwarebytes blocks
  unmineable.com. Code signing and submitting each release to Microsoft (WDSI) help most.
- The one-click Defender exclusion runs PowerShell in a hidden window, a pattern malware uses (MITRE T1562.001).
  Recommendation: run it visibly, or open Windows Security on the right page and guide the user.
- XMRig's WinRing0 driver is flagged by Defender; Otium runs XMRig without administrator rights, so it never loads.

## Questions for counsel (Norwegian fintech and consumer lawyer)

1. Is the internet-sharing conversion "reception and transmission of orders" or a "transfer service" under
   kryptoeiendelsloven/MiCA, as built (server-side quote, automatic send, 2% fee) and after D1(a) (no fee, quote from
   the user's computer)?
2. Does recital 83 (non-custodial wallet software) cover an app with a key on the device, automatic payouts and a
   built-in swap?
3. Does referring EEA users to unMineable, SideShift or ChangeNOW for a commission make Otium a person acting for a
   third-country firm (MiCA art. 61)?
4. Is Otium's 2% of mining time "other counter-performance" under digitalytelsesloven § 1, and is liability for
   defects under §§ 25 and 48 strict? What liability cap is allowed?
5. Does the 14-day withdrawal right apply, and is refunding Otium's fee share for that period enough?
6. Can the earnings-statistics consent be part of the welcome screen as an unticked box, and is a problem report the
   user sends covered by the "service the user asked for" exception?
7. Is Otium a joint controller with unMineable or Proxybase for what the app sends them (*Fashion ID*)?
8. Which countries must be blocked (sanctions, mining bans), and is an IP check plus self-declaration enough?
9. Do the terms need a Norwegian version now, and can English apply to everyone else?
10. Personal liability before an AS exists; timing of incorporation.
11. (UK counsel) Does the London server or UK users bring Otium under the UK's crypto rules (MLR "arranging", the new
    regime from 25 October 2027)?
12. (Tax adviser) Income tax on mined fee coins at receipt; VAT on software supplied for hashrate.

## Also found

- **Email sign-in doesn't work on the live server:** no SMTP provider is configured, so "Email me a code" can't send.
  Pick a provider (with a processor agreement) before v0.1.0, or hide the email option.
