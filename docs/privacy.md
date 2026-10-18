# Otium privacy policy

> **DRAFT – NOT FOR PUBLICATION.** Written by the developers as a starting point for legal review; see
> [legal-review.md](legal-review.md). It has not been checked by a lawyer and must not be shown to users as Otium's
> policy until it has. Everything in `[square brackets]` is a placeholder or an open question for the reviewer.
> Retention periods marked *(to build)* are proposals the server doesn't enforce yet.

*Last updated: [date of publication]*

Otium is a desktop app that earns for you with your computer's idle processor and graphics card and, if you choose,
your unused internet connection. Mining pools pay you directly at your own wallet address; Otium doesn't receive or
hold your earnings. This policy explains what personal data Otium handles, why, who else receives it, how long it is
kept and how you use your rights.

## 1. Who is responsible

The controller of your personal data is **[legal name], [organisation number], [address], Norway**.
Contact: **[privacy email address]**. [Data protection officer: not required at this size; name a privacy contact.]

## 2. What we collect and why

Your **wallet address** is personal data in our hands: it's public on its blockchain, and we link it to your account
and computers. We treat it with the same care as your email.

### Data on Otium's server

| Data | Why we use it | Legal basis [to confirm] |
|---|---|---|
| **Account:** a random account id, a pool worker id, your invite code, the date you joined | Running your account, invites and fee-free periods | Contract (GDPR art. 6(1)(b)) |
| **Payout address:** the coin you chose and your wallet address for it | Mining the referral share to you when you invite friends, showing your progress at the pool, and checking that friends you invited really mine | Contract |
| **Email address** (only if you add one) and one-time sign-in codes, stored only as salted hashes that expire after 15 minutes | Keeping your invites when you reinstall, signing in on several computers, confirming account deletion | Contract |
| **Computers:** a device id (a one-way hash of your computer's machine id; the machine id itself never leaves your computer), a hash of the computer's secret key, when it first and last connected | Recognising your computers and keeping others out of your account | Contract; legitimate interest in security (art. 6(1)(f)) |
| **Invites:** who invited you and whom you invited, and whether their pool shows that they started mining (yes or no, never amounts) | Fee-free periods and the referral share of Otium's fee. If you join with an invite code, the person who invited you can see whether you have started mining | Contract |
| **What the pools paid your address:** the coin, the total the pool reports as paid to your address and its last 30 days, read about twice a day from the pools' public APIs | Showing on otium.lol the total paid to Otium users (one sum over everyone, never per person) | Legitimate interest in showing what Otium is worth [to confirm]. Source: the pools' public APIs (art. 14) |
| **Earnings statistics**, only if you tick "Help show what PCs really earn": every few hours, your graphics card model, processor name, how long each mined, what it earned and the electricity it used, linked to your computer's device id. Never your address | Publishing real per-card earnings on otium.lol, only as medians across at least [5] computers | Consent (art. 6(1)(a) and ekomloven § 3-15). You can withdraw it in Settings at any time; we then delete that computer's statistics [to build] |
| **Problem reports**, only when you press "Send a problem report": the app version and operating system, your settings switches, what each miner is doing and its recent messages, your graphics card's test results, internet sharing's status, your pool balance and the name of your antivirus, with payout addresses, keys and the device secret removed, plus any note you write | Finding out why Otium isn't working for you, when you ask for help | Contract (support you asked for) |
| **Server logs:** IP addresses and request details | Security and troubleshooting | Legitimate interest |

We don't collect your name, address, phone number or browsing history, and we don't use your data for advertising or
sell it. Because the pools pay you directly, Otium keeps no balance, earnings ledger or payout records about you.

[Reviewer: the server also has a custodial mode, off in this deployment, in which Otium would hold a balance per
account and pay cash-outs. If it's ever turned on, this policy needs the earnings ledger, cash-out records and tax
details, with their legal bases and retention.]

### Data that stays on your computer

The app reads your processor and graphics card models, whether you're using the computer, whether a full-screen app is
running and which antivirus is installed, and uses them on your computer to decide when and how to mine. That
information isn't sent anywhere except in earnings statistics and problem reports, as described above.

The app keeps its settings (including your payout address and the result of the two-minute test), a copy of your
account summary (invite code, email) and local earnings statistics (hour by hour for 14 days, month by month for two
years) in its data folder (Settings → About → Open data folder). Uninstalling removes them.

## 3. Who else receives data

| Recipient | What they receive | Why |
|---|---|---|
| **Mining pools:** unMineable [operator and country unknown: ask]; MoneroOcean (US users) | Your computer's IP address (the miner connects to them directly), your payout address and coin, the worker name, and Otium's referral code | Doing the mining work and paying you directly. Each pool is a separate controller under its own terms [links] |
| **unMineable's and MoneroOcean's public APIs** | Your payout address, when the app or server looks up your balance or checks an invite | Showing your progress to the next payout; verifying invites |
| **The blockchain of your coin** | Your address and the amounts the pool pays you, published by the pool, not by Otium | Paying you |
| **Proxybase** (PROXYBASE XYZ LLC, USA; only if you turn internet sharing on) | Runs its client on your computer, which relays its customers' traffic through your connection; sees your IP address and the traffic it relays, and a wallet address made on your computer for your earnings | Internet sharing. A separate controller under its own terms [link]. [Reviewer: transfer basis] |
| **Relay** (Uneven Labs, Inc., USA) and, for LTC or XMR payouts, **SideShift** (St Kitts and Nevis) or **ChangeNOW** (St Vincent and the Grenadines) | Your payout address and coin, the amount, the sharing client's wallet address (for refunds); SideShift and ChangeNOW also your computer's IP address, which they require | Converting internet sharing earnings to your coin and paying your address. [Reviewer: these transfers need a safeguard; see legal-review.md D1] |
| **Email service** [name, country] | Your email address and the sign-in code | Sending codes (our processor) |
| **Amazon Web Services** (Lightsail, London) | Everything on the server | Hosting (our processor) |
| **Cloudflare** | Your IP address and requests to otium.lol | Protecting and delivering the website and API (our processor) |
| **Backup storage** [name, country] | Encrypted copies of the server database | Backups (our processor) |
| **GitHub** | Your computer's IP address when the app checks for updates and downloads the miners | Updates and miner downloads |
| **Electricity price services:** hvakosterstrommen.no, elprisetjustnu.se, elprisenligenu.dk, sahkotin.fi | Your computer's IP address when the app fetches today's prices for your price area (no account data) | Pausing in expensive hours |
| **CoinGecko** | Nothing about you: the server asks it for today's coin prices | Converting what the pools paid into dollars for otium.lol |
| **Authorities** | What the law requires us to hand over | Legal obligations |

### Outside the EEA

| Where | Recipients | Safeguard |
|---|---|---|
| United Kingdom | Amazon Web Services (London) | EU adequacy decision for the UK (valid to December 2031) |
| United States | Cloudflare, GitHub, Relay, Proxybase | EU-US Data Privacy Framework where the company is certified [check Relay and Proxybase]; otherwise standard contractual clauses [to sign] |
| St Kitts and Nevis, St Vincent and the Grenadines | SideShift, ChangeNOW | [None yet: see legal-review.md D1] |

The mining pools are reached directly by your computer, not by Otium. [Reviewer: confirm this isn't a transfer by
Otium.]

## 4. How long we keep it

- **While you have an account**, we keep the account data above.
- **When you delete your account**, we delete or anonymise everything that identifies you straight away: your email,
  payout address, invite code, the link between you and your mining worker, your computers and their keys, your
  sign-in codes, your problem reports, the earnings statistics you shared and the record of what the pools paid your
  address. What the pools themselves keep about your address is up to them.
- **Accounts no computer has contacted for 24 months** are deleted, after a warning email if you gave one *(to build)*.
- **Earnings statistics:** combined into anonymous totals and the raw rows deleted after 90 days *(to build)*.
- **Problem reports:** 90 days after we last worked on them *(to build)*.
- **Sign-in codes** stop working after 15 minutes; their hashes are deleted after 30 days *(to build)*.
- **Server logs:** 14 days.
- **Backups** of the server database are kept for 14 days, encrypted, so deleted data disappears from them within that
  time.
- **Records of your consent** to earnings statistics: for as long as you have an account and 3 years after, to show we
  asked.

## 5. Your rights

You can use these rights at any time, free of charge:

- **Access and portability:** in the app, go to Settings → Your data → **Download my data**. You get a JSON file with
  everything the server stores about your account. People you invited, or who invited you, appear in it without
  anything that identifies them.
- **Erasure:** Settings → Your data → **Delete my account**. If your account has an email, we send a code to it to
  make sure it's really you. Anything waiting at your pool stays yours there: deleting your Otium account doesn't touch
  your pool balance.
- **Rectification:** change your payout address in Settings, or your email by signing in with the new one. For
  anything else, email us.
- **Withdrawing consent:** turn off "Help show what PCs really earn" or internet sharing in Settings at any time. It
  takes effect immediately and doesn't affect what happened before.
- **Objection:** where we rely on legitimate interest (security logs, the site-wide total paid), you can object at
  [privacy email]; we'll stop unless we have compelling reasons.
- **Restriction:** contact us at [privacy email].
- **Complaints:** you can complain to Datatilsynet (Norway, datatilsynet.no) or to the data protection authority where
  you live.

If you can no longer use the app (for example your computer broke), email [privacy email] from the address on your
account and we'll help you export or delete your data.

## 6. Security

Computers authenticate with a random secret that is stored on the server only as a hash, sign-in codes are stored only
as salted hashes, the server is only reachable over HTTPS, and backups are encrypted. Problem reports have payout
addresses, keys and secrets removed on your computer before they're sent. If a breach puts your data at risk, we'll
tell Datatilsynet within 72 hours and tell you if the risk to you is high.

## 7. Age

Otium is for people aged 18 or over. The mining pools and exchange services it works with require adults too.

## 8. Changes

We'll show a notice in the app before changes to this policy take effect. The date at the top shows the latest
version; earlier versions are available on request.
