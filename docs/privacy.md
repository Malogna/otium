# Otium privacy policy

*Last updated: 19 October 2026*

Otium is a desktop app that earns for you with your computer's idle processor and graphics card and, if you choose,
your unused internet connection. Mining pools pay you directly at your own wallet address; Otium doesn't receive or
hold your earnings. This policy explains what personal data Otium handles, why, who else receives it, how long it is
kept and how you use your rights.

## 1. Who is responsible

The controller of your personal data is Otium's developer. Otium is run from within the European Economic Area, so
the EU's General Data Protection Regulation (GDPR) protects your data wherever in the world you live, and this policy
gives everyone the same rights. For anything about your data, email
**contact@otium.lol**. Otium is too small to need a data protection officer; that address reaches the person
responsible.

## 2. What we collect and why

Your **wallet address** is personal data in our hands: it's public on its blockchain, and we link it to your account
and computers. We treat it with the same care as your email.

### Data on Otium's server

| Data | Why we use it | Legal basis |
|---|---|---|
| **Account:** a random account id, a pool worker id, your invite code, the date you joined | Running your account, invites and fee-free periods | Contract (GDPR art. 6(1)(b)) |
| **Payout address:** the coin you chose and your wallet address for it | Mining the referral share to you when you invite friends, showing your progress at the pool, and checking that friends you invited really mine | Contract |
| **Email address** (only if you add one) and one-time sign-in codes, stored only as salted hashes that expire after 15 minutes | Keeping your invites when you reinstall, signing in on several computers, confirming account deletion | Contract |
| **Computers:** a device id (a one-way hash of your computer's machine id; the machine id itself never leaves your computer), a hash of the computer's secret key, when it first and last connected | Recognising your computers and keeping others out of your account | Contract; legitimate interest in security (art. 6(1)(f)) |
| **Invites:** who invited you and whom you invited, and whether their pool shows that they started mining (yes or no, never amounts) | Fee-free periods and the referral share of Otium's fee. If you join with an invite code, the person who invited you can see whether you have started mining | Contract |
| **What the pools paid your address:** the coin, the total the pool reports as paid to your address and its last 30 days, read about twice a day from the pools' public APIs | Showing on otium.lol the total paid to Otium users (one sum over everyone, never per person) | Legitimate interest in showing what Otium is worth. Source: the pools' public APIs (art. 14) |
| **Earnings statistics**, only if you answer yes to "Share analytics": every few hours, your graphics card model, processor name, how long each mined, what it earned and the electricity it used, and, if you share your internet, your country, how long it shared and what sharing earned, linked to your computer's device id. Never your address | Publishing real per-card and per-country sharing earnings on otium.lol, only as medians across at least 3 computers | Consent (GDPR art. 6(1)(a)). You can withdraw it in Settings at any time; we then delete that computer's statistics. The app records when you answered |
| **Problem reports**, only when you press "Send a problem report": the app version and operating system, your settings switches, what each miner is doing and its recent messages, your graphics card's test results, internet sharing's status, your pool balance and the name of your antivirus, with payout addresses, keys and the device secret removed, plus any note you write | Finding out why Otium isn't working for you, when you ask for help | Contract (support you asked for) |
| **Server logs:** IP addresses and request details | Security and troubleshooting | Legitimate interest |

We don't collect your name, address, phone number or browsing history, and we don't use your data for advertising or
sell it. Because the pools pay you directly, Otium keeps no balance, earnings ledger or payout records about you.

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
| **Mining pools:** unMineable; MoneroOcean (US users) | Your computer's IP address (the miner connects to them directly), your payout address and coin, the worker name, and Otium's referral code | Doing the mining work and paying you directly. Each pool is a separate controller under its own terms |
| **unMineable's and MoneroOcean's public APIs** | Your payout address, when the app or server looks up your balance or checks an invite | Showing your progress to the next payout; verifying invites |
| **The blockchain of your coin** | Your address and the amounts the pool pays you, published by the pool, not by Otium | Paying you |
| **Proxybase** (PROXYBASE XYZ LLC, USA; only if you turn internet sharing on) | Runs its client on your computer, which relays its customers' traffic through your connection; sees your IP address and the traffic it relays, and a wallet address made on your computer for your earnings | Internet sharing. A separate controller under its own terms |
| **Relay** (Uneven Labs, Inc., USA) and, for LTC or XMR payouts, **SideShift** (St Kitts and Nevis) or **ChangeNOW** (St Vincent and the Grenadines) | Your payout address and coin, the amount, the sharing client's wallet address (for refunds); SideShift and ChangeNOW also your computer's IP address, which they require | Converting internet sharing earnings to your coin and paying your address |
| **Amazon Web Services** (Simple Email Service, London) | Your email address and the sign-in code | Sending codes (our processor, under the same AWS agreement as hosting) |
| **Amazon Web Services** (Lightsail, London) | Everything on the server | Hosting (our processor) |
| **Cloudflare** | Your IP address and requests to otium.lol | Protecting and delivering the website and API (our processor) |
| **Cloudflare** (R2 storage, Europe) | Copies of the server database, encrypted at rest | Backups (our processor) |
| **GitHub** | Your computer's IP address when the app checks for updates and downloads the miners | Updates and miner downloads |
| **Electricity price services** where your area has one (today the Nordic countries: hvakosterstrommen.no, elprisetjustnu.se, elprisenligenu.dk, sahkotin.fi); elsewhere the app uses a typical price for your country or the one you enter | Your computer's IP address when the app fetches today's prices for your price area (no account data) | Pausing in expensive hours |
| **CoinGecko** | Nothing about you: the server asks it for today's coin prices | Converting what the pools paid into dollars for otium.lol |
| **Authorities** | What the law requires us to hand over | Legal obligations |

### Outside the EEA

| Where | Recipients | Safeguard |
|---|---|---|
| United Kingdom | Amazon Web Services (London: hosting and email) | EU adequacy decision for the UK (valid to December 2031) |
| United States | Cloudflare, GitHub | EU-US Data Privacy Framework |
| United States | Relay, Proxybase | Only if you turn internet sharing on, which can't work without them |
| St Kitts and Nevis, St Vincent and the Grenadines | SideShift, ChangeNOW | Only if you choose an LTC or XMR payout for internet sharing, which can't work without them |

The mining pools are reached directly by your computer, not by Otium.

## 4. How long we keep it

- **While you have an account**, we keep the account data above.
- **When you delete your account**, we delete or anonymise everything that identifies you straight away: your email,
  payout address, invite code, the link between you and your mining worker, your computers and their keys, your
  sign-in codes, your problem reports, the earnings statistics you shared and the record of what the pools paid your
  address. What the pools themselves keep about your address is up to them.
- **Sign-in codes** stop working after 15 minutes.
- **Server logs** are deleted as they rotate (at most 50 MB are kept), usually within a few weeks.
- **Backups** of the server database are kept for up to 31 days, encrypted at rest, so deleted data disappears from
  them within that time.
- We're adding automatic clean-up for data that's no longer needed: accounts no computer has contacted for 24 months
  (after a warning email if you gave one), raw earnings statistics after 90 days (kept only as anonymous totals),
  problem reports 90 days after we last worked on them, and old sign-in code hashes after 30 days. Until that's
  built, you can delete any of it yourself with **Delete my account**, or ask us at contact@otium.lol.
- **Your answer** to "Share analytics" and when you gave it are kept in the app's settings on your
  computer.

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
- **Withdrawing consent:** turn off "Share analytics" or internet sharing in Settings at any time. It
  takes effect immediately and doesn't affect what happened before.
- **Objection:** where we rely on legitimate interest (security logs, the site-wide total paid), you can object at
  contact@otium.lol; we'll stop unless we have compelling reasons.
- **Restriction:** contact us at contact@otium.lol.
- **Complaints:** you can complain to the data protection authority where you live. Our lead authority is Norway's
  Datatilsynet (datatilsynet.no), which you can also contact from anywhere.

If you can no longer use the app (for example your computer broke), email contact@otium.lol from the address on your
account and we'll help you export or delete your data.

## 6. Security

Computers authenticate with a random secret that is stored on the server only as a hash, sign-in codes are stored only
as salted hashes, the server is only reachable over HTTPS, and backups are encrypted. Problem reports have payout
addresses, keys and secrets removed on your computer before they're sent. If a breach puts your data at risk, we'll
tell the data protection authority within 72 hours and tell you if the risk to you is high.

## 7. Age

Otium is for people aged 18 or over. The mining pools and exchange services it works with require adults too.

## 8. Changes

We'll show a notice in the app before changes to this policy take effect. The date at the top shows the latest
version; earlier versions are available on request.
