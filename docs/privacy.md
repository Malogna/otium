# Otium privacy policy

> **DRAFT – NOT FOR PUBLICATION.** Written by the developers as a starting point for legal review. It has not been
> checked by a lawyer or a data protection specialist and must not be shown to users as Otium's policy until it has.
> Everything in `[square brackets]` is a placeholder or an open question for the reviewer.

*Last updated: [date of publication]*

Otium is a desktop app that earns money for you from your computer's idle processor, graphics card and (if you choose)
unused internet connection. The mining pools pay you directly at your own wallet address; Otium doesn't receive or
hold your earnings. This policy explains what personal data Otium handles, why, who else receives it, how long
it is kept and how you use your rights.

## 1. Who is responsible

The controller of your personal data is **[legal name, company number, address, country]**.
Contact: **[privacy email address]**. [Data protection officer, if one is appointed or required.]

## 2. What we collect and why

### Data on Otium's server

| Data | Why we use it | Legal basis [to confirm] |
|---|---|---|
| **Account:** a random account id, a pool worker id, your invite code, the date you joined | Running invites and fee-free periods for your computers | Contract (GDPR art. 6(1)(b)) |
| **Payout address:** the coin you chose and your wallet address for it (for example USDT on TRON). A wallet address is public on its blockchain and can be linked to you | Mining the referral share to you when you invite friends, and checking with your pool that friends you invited really mine | Contract |
| **Email address** (only if you add one) and one-time sign-in codes, stored only as salted hashes that expire after 15 minutes | Keeping your invite code and invited friends when you reinstall, signing in on several computers, confirming account deletion | Contract |
| **Computers:** a device id (a one-way hash of your computer's machine id; the machine id itself never leaves your computer), a hash of the computer's secret key, when it first and last connected | Recognising your computers and keeping others out of your account | Contract; legitimate interest in security (art. 6(1)(f)) |
| **Invites:** who invited you and whom you invited, and whether their pool shows that they started mining | Fee-free periods and the referral share of Otium's fee | Contract |
| **Mysterium registrations** (only if you turn internet sharing on): your node's Mysterium identity and channel address, your computer, the internet address it asked from, and the Polygon transaction in which Otium paid the registration | Paying each node's registration once, and limiting how many we pay for | Contract; legitimate interest in preventing abuse |
| **Earnings statistics** (only if you tick "Help show what PCs really earn"): every few hours, your graphics card model, processor name, how long each mined and what it earned and used in electricity, linked to your computer's device id. Never your address | Publishing real per-card earnings on otium.lol, only as medians across at least three computers, so no single computer's numbers are shown | Consent (art. 6(1)(a)); you can withdraw it in Settings at any time |
| **What the pools paid your address:** the coin, the total the pool reports as paid to your payout address and its last 30 days, checked about twice a day from the pools' public APIs | Showing on otium.lol the total paid to Otium users (one sum over everyone, never per person) | Legitimate interest in showing what Otium is worth [to confirm] |
| **Server logs:** IP addresses and request details, [kept for N days] | Security and troubleshooting | Legitimate interest |

We don't collect your name, address, phone number or browsing history, and we don't use your data for advertising or
sell it. Because the pools pay you directly, Otium keeps no balance, earnings ledger or payout records about you.

[Reviewer: the server also has a custodial mode, off in this deployment, in which Otium holds a balance per account
and pays cash-outs. If it is ever turned on, this policy needs the earnings ledger, cash-out records (destination,
amount, provider reference, review notes), internet sharing reports with IP addresses, and tax details near reporting
thresholds, with their legal bases and retention.]

### Data that stays on your computer

The app keeps its settings (including your payout address and the result of the "measure my PC" test), a copy of
your account summary (invite code, email) and local earnings statistics (hour by hour for 14 days, month by month for
two years)
in its data folder (Advanced → About → Open data folder). It isn't sent anywhere except as described here.

## 3. Who else receives data

| Recipient | What they receive | Why |
|---|---|---|
| **Mining pools:** unMineable [company, country]; MoneroOcean (if you're paid in Monero) | Your computer's IP address (the miner connects to them directly), your payout address and coin, the worker name, and Otium's referral code | Doing the mining work and paying you directly. Each pool is a separate controller under its own terms and privacy notice [links] |
| **unMineable's and MoneroOcean's public APIs** | Your payout address, when the app or server looks up your balance or checks an invite | Showing your progress to the next payout; verifying invites |
| **The blockchain of your coin** | Your address and the amounts the pool pays you, published by the pool, not by Otium | Paying you |
| **Email service** [name] | Your email address and the sign-in code | Sending codes |
| **Honeygain** (internet sharing partner, only if you turn sharing on) | Runs inside Otium and routes its customers' traffic through your connection; sees your IP address and the traffic it routes. [Link to Honeygain's privacy notice; confirm their role (separate controller?) and whether a data processing agreement is needed.] | Internet sharing |
| **Mysterium Network** (only if you turn internet sharing on) | Runs a node on your computer that routes its business customers' traffic through your connection; sees your IP address and the traffic it routes, and pays your Polygon address. [Link to Mysterium's privacy notice; it is a separate controller under its own terms.] | Internet sharing |
| **Polygon blockchain** | Your node's channel address and Otium's registration payment; later, Mysterium's payments to your Polygon address | Registering your node; paying you |
| **CoinGecko** | Nothing about you: the server asks it for today's coin prices | Converting what the pools paid into dollars for otium.lol |
| **Electricity price services:** hvakosterstrommen.no, elprisetjustnu.se | Your computer's IP address when the app fetches today's prices for your price area (no account data) | Pausing in expensive hours |
| **GitHub** | Your computer's IP address when the app checks for updates | Updates |
| **Hosting provider** [name, location] | Everything on the server, as our processor | Running the server |
| **Authorities** | What the law requires us to hand over | Legal obligations |

[Reviewer: list transfers outside the EU/EEA (for example GitHub, the email service, the internet sharing partner, the pools) and
the safeguards used (adequacy decision, EU–US Data Privacy Framework, standard contractual clauses).]

## 4. How long we keep it

- **While you have an account**, we keep the data above.
- **When you delete your account**, we delete or anonymize everything that identifies you straight away: your email,
  payout address, invite code, the link between you and your mining worker, your computers and their keys, your
  sign-in codes, the earnings statistics you shared and the record of what the pools paid your address. What the pools themselves keep about your address is up to them.
- **Sign-in codes** stop working after 15 minutes. [Decide how long old used/expired code hashes are kept, e.g. 30 days.]
- **Server logs** are kept for [N] days.
- **Backups** of the server database are kept for [N] days, so deleted data disappears from them within that time.

## 5. Your rights

You can use these rights at any time, free of charge:

- **Access and portability:** in the app, go to Advanced → Settings → Your data → **Download my data**. You get a
  JSON file with everything the server stores about your account. People you invited, or who invited you, appear in it
  without anything that identifies them.
- **Erasure:** Advanced → Settings → Your data → **Delete my account**. If your account has an email, we send a code to
  it to make sure it's really you. Anything waiting at your pool stays yours there: deleting your Otium account doesn't
  touch your pool balance.
- **Rectification:** you can change your email by signing in with the new one. [Process for other corrections.]
- **Withdrawing consent:** turn internet sharing off at any time (on the overview or in Advanced). This stops sharing
  immediately and withdraws your consent with our partner too. It doesn't affect sharing that already happened.
- **Objection and restriction:** contact us at [privacy email].
- **Complaints:** you can complain to your data protection authority, for example Datatilsynet (Norway), IMY (Sweden),
  Datatilsynet (Denmark), the Data Protection Ombudsman (Finland) or [the authority where the controller is
  established].

If you can no longer use the app (for example your computer broke), email [privacy email] from the address on your
account and we'll help you export or delete your data.

## 6. Security

Computers authenticate with a random secret that is stored on the server only as a hash, sign-in codes are stored only
as salted hashes, and [the production server is only reachable over HTTPS: confirm at deployment]. [Describe hosting security, encryption at rest,
access control and backups.]

## 7. Children

Otium is not meant for people under [18 / the age of digital consent in your country], and the pools' own terms set
age limits too. [Reviewer: confirm the age limit and how it is enforced.]

## 8. Changes

We'll show a notice in the app before changes to this policy take effect. The date at the top shows the latest version.
