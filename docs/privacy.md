# Otium privacy policy

> **DRAFT – NOT FOR PUBLICATION.** Written by the developers as a starting point for legal review. It has not been
> checked by a lawyer or a data protection specialist and must not be shown to users as Otium's policy until it has.
> Everything in `[square brackets]` is a placeholder or an open question for the reviewer.

*Last updated: [date of publication]*

Otium is a desktop app that earns money for you from your computer's idle processor, graphics card and (if you choose)
unused internet connection. This policy explains what personal data Otium handles, why, who else receives it, how long
it is kept and how you use your rights.

## 1. Who is responsible

The controller of your personal data is **[legal name, company number, address, country]**.
Contact: **[privacy email address]**. [Data protection officer, if one is appointed or required.]

## 2. What we collect and why

### Data on Otium's server

| Data | Why we use it | Legal basis [to confirm] |
|---|---|---|
| **Account:** a random account id, a pool worker id, your invite code, the date you joined | To credit the work your computer does to you | Contract (GDPR art. 6(1)(b)) |
| **Email address** (only if you add one) and one-time sign-in codes, stored only as salted hashes that expire after 15 minutes | Securing your balance, signing in on several computers, confirming account deletion | Contract |
| **Computers:** a device id (a one-way hash of your computer's machine id; the machine id itself never leaves your computer), a hash of the computer's secret key, when it first and last connected | Recognising your computers and keeping others out of your account | Contract; legitimate interest in security (art. 6(1)(f)) |
| **Earnings ledger:** every amount credited, held, cashed out or refunded, with dates | Showing and paying your balance; bookkeeping | Contract; legal obligation (art. 6(1)(c)) for bookkeeping |
| **Cash-outs:** method, PayPal email or Monero address, amount, status, the payment provider's reference, review notes | Paying you; preventing fraud (for example one person cashing out from many accounts); bookkeeping | Contract; legal obligation; legitimate interest in preventing fraud |
| **Invites:** who invited you and whom you invited, and whether they became active | Paying referral rewards and fee-free periods | Contract |
| **Internet sharing reports** (only if you turn sharing on): your public IP address, the period, how much data was shared and what you were paid | Paying you for sharing; making sure only one computer per internet connection is paid, as our partner requires | Consent (art. 6(1)(a)) to sharing; contract for the payment |
| **Own Monero wallet** (only in own-wallet mode) | Letting the mining pool pay you directly | Contract |
| **Server logs:** IP addresses and request details, [kept for N days] | Security and troubleshooting | Legitimate interest |

We don't collect your name, address, phone number or browsing history, and we don't use your data for advertising or
sell it. [If tax details (for example US W-9/W-8 forms) are collected from users near a reporting threshold, add them
here with their legal basis and retention.]

### Data that stays on your computer

The app keeps its settings, a copy of your account summary (balance, invite code, email) and local earnings statistics
in its data folder (Advanced → About → Open data folder). It isn't sent anywhere except as described here.

## 3. Who else receives data

| Recipient | What they receive | Why |
|---|---|---|
| **Mining pools:** MoneroOcean; Kryptex Pool (graphics card coins) | Your computer's IP address (the miner connects to them directly) and the pool worker id | Doing the mining work and paying for it |
| **PayPal** | Your PayPal email and the amount, when you cash out by PayPal | Paying you |
| **The Monero network** | Your Monero address and the amount, when you cash out in crypto (Monero hides both on the public blockchain) | Paying you |
| **Email service** [name] | Your email address and the sign-in code | Sending codes |
| **Honeygain** (internet sharing partner, only if you turn sharing on) | Runs inside Otium and routes its customers' traffic through your connection; sees your IP address and the traffic it routes. [Link to Honeygain's privacy notice; confirm their role (separate controller?) and whether a data processing agreement is needed.] | Internet sharing |
| **Electricity price services:** hvakosterstrommen.no, elprisetjustnu.se | Your computer's IP address when the app fetches today's prices for your price area (no account data) | Pausing in expensive hours |
| **GitHub** | Your computer's IP address when the app checks for updates | Updates |
| **Hosting provider** [name, location] | Everything on the server, as our processor | Running the server |
| **Authorities** | What the law requires us to hand over | Legal obligations |

[Reviewer: list transfers outside the EU/EEA (for example PayPal, GitHub, the email service, Honeygain, the pools) and
the safeguards used (adequacy decision, EU–US Data Privacy Framework, standard contractual clauses).]

## 4. How long we keep it

- **While you have an account**, we keep the data above.
- **When you delete your account**, we delete or anonymize everything that identifies you straight away: your email,
  invite code, the link between you and your mining worker, your computers and their keys, your sign-in codes, the IP
  addresses in your internet sharing reports and your full payout addresses.
- **Money records are kept, without anything that identifies you,** because bookkeeping law requires us to keep them
  for [5 years (Norway, Bokføringsloven § 13) / 7 years (Sweden) / … — reviewer to confirm per country of the
  controller]. The payment provider's reference stays with each cash-out so a payment can still be traced at PayPal or
  in our wallet if the law requires it.
- **Sign-in codes** stop working after 15 minutes. [Decide how long old used/expired code hashes are kept, e.g. 30 days.]
- **Server logs** are kept for [N] days.
- **Backups** of the server database are kept for [N] days, so deleted data disappears from them within that time.

## 5. Your rights

You can use these rights at any time, free of charge:

- **Access and portability:** in the app, go to Advanced → Settings → Your data → **Download my data**. You get a
  JSON file with everything the server stores about your account. People you invited, or who invited you, appear in it
  without anything that identifies them.
- **Erasure:** Advanced → Settings → Your data → **Delete my account**. If your account has an email, we send a code to
  it to make sure it's really you. Deletion isn't possible while a cash-out is on its way, and any balance left is given
  up, so cash out first if you can. Money records are kept anonymized as described in section 4.
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

Otium is not meant for people under [18 / the age of digital consent in your country], and cash-outs need an account
holder who can enter into a contract. [Reviewer: confirm the age limit and how it is enforced.]

## 8. Changes

We'll show a notice in the app before changes to this policy take effect. The date at the top shows the latest version.
