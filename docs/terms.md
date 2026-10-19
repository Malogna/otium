# Otium terms of use

*Version 1, 19 October 2026*

## In short

- Otium is free to download. It earns crypto with your computer while you're away from it, and the mining pool pays
  your own wallet. Otium never holds your money.
- Otium keeps 2% of mining time, and the mining software and the pool take their own small fees. All of them are shown
  before you start.
- Earnings are estimates, not promises. Crypto prices go up and down, and mining uses electricity and warms your
  computer.
- You must be 18 or over, and use only a computer and electricity you're allowed to use.
- You can stop, uninstall or delete your account at any time. These terms don't take away any rights consumer law
  gives you.

## 1. Who we are

Otium is made and run by an independent developer, for people all over the world. You can reach us at
**contact@otium.lol**, or from the app with Settings → Send a problem report.

## 2. What Otium does

Otium is a desktop app for Windows (macOS and Linux are in beta: they work but have been tested less). While you're
away from the computer, it runs mining software on your processor and graphics card:

- **XMRig** for the processor and **SRBMiner-Multi** for graphics cards, downloaded from their official releases.
- The mining pool **unMineable** (or **MoneroOcean** for users in the United States) converts what you mine and pays
  your own wallet address, in the coin you choose, once you reach its payout minimum.
- **Internet sharing** (optional): Proxybase's client lets Proxybase's customers use some of your unused internet
  connection. Its earnings are converted to your coin and paid to your address.

Otium stops mining when you use the computer, and pauses when electricity would likely cost more than mining earns,
based on the electricity price for your area or the price you enter.

## 3. Who can use Otium

You can use Otium if:
- you are **18 or over**;
- the computer is yours, or you have permission to use it (and its electricity) for mining. Don't install Otium on an
  employer's, school's or someone else's computer without permission;
- for internet sharing, you are the internet subscriber or have their permission, and your provider's terms allow it;
- you're not in a country where Otium isn't available (sanctions or mining bans; the app tells you, and the
  [list is public](https://github.com/Malogna/otium/blob/main/src/Otium.Core/Availability.cs)) and you're not subject to
  sanctions;
- mining and holding crypto is legal where you live.

If you are a US citizen or resident, Otium uses MoneroOcean and pays you in Monero, because unMineable doesn't serve
US persons.

## 4. What it costs

Otium is free to download, with no subscription. You pay with a share of mining time:

| Who | Takes |
|---|---|
| Otium | 2% of mining time, about 29 minutes per day of mining, mined to Otium's address. Nothing while Otium is paused or during fee-free days. |
| Mining software | XMRig 1% (processor); SRBMiner 0.85–2% (graphics card, depending on the coin) |
| Pool | unMineable 0.75%. Otium's referral code lowers it from 1%, and unMineable pays Otium 0.25–0.5% of what you mine there, out of its own fee |
| Internet sharing | Otium: 2% of what Proxybase pays you, paid to Otium's address from your sharing earnings. The conversion to your coin has no Otium fee; its network costs come off each payout |

If you join with an invite code, Otium's fee is off for 30 days. If you invite friends, part of Otium's fee on their
mining is mined to your address for 6 months.

We'll tell you at least 30 days before we raise any fee, in the app and by email if you gave one. If you don't
accept the change, you can stop using Otium at no cost.

## 5. Earnings are estimates

Figures in the app and on otium.lol, including the two-minute test, are **estimates** based on today's coin prices,
mining difficulty, your hardware and your electricity price. What you actually earn depends on how long the computer
is left alone, coin prices, the pool, and your real electricity costs (including grid fees and taxes, which Otium's
price may not include). **We don't promise any income.** You're paid in crypto, whose value can fall. Taxes on what
you earn are your responsibility.

## 6. Effects on your computer

While Otium earns:
- your computer uses more electricity, gets warmer and its fans may be louder;
- graphics cards and fans may wear somewhat faster under long use;
- Otium keeps the computer awake (the screen still turns off), and never keeps a laptop on battery awake;
- if you turn on "Keep the processor miner ready", it keeps about 2.5 GB of memory reserved for up to 10 minutes
  after you come back;
- internet sharing uses some of your internet connection and data allowance.

Otium doesn't overclock your hardware. Efficiency mode lowers the graphics card's power limit while mining and
restores it afterwards. You can turn each part off in Settings.

## 7. Partners and payouts

Payouts come from the mining pools (and for internet sharing, from the conversion service), not from Otium. Each
works under its own terms, which apply to you as well: unMineable, MoneroOcean, Proxybase, Relay, SideShift and
ChangeNOW.

Things to know about the pools:
- you're paid only once you reach the pool's payout minimum;
- unMineable may switch off automatic payouts for an address (new TRON addresses start with it off); the app warns
  you and links the setting;
- **unMineable may delete a balance that has had no mining for 6 months.** If you stop using Otium with a balance
  below the minimum, collect it from the pool's website first.

We choose these partners carefully and are responsible for how Otium works with them, but we don't control their
services.

## 8. Your wallet

You give Otium the address to be paid at. Check it carefully: payments sent to a wrong address can't be reversed.
**Otium never asks for your private keys or recovery phrase.**

## 9. Internet sharing

Internet sharing is optional and off until you agree to it. When it's on, Proxybase's customers (mostly AI agents and
businesses that collect public web pages or check ads) send traffic through your connection:
- websites they visit see your internet address, and some may block it or show you more CAPTCHAs;
- Otium can't see or control that traffic, and Proxybase doesn't verify who its customers are. If someone misuses it,
  the traffic shows your internet address;
- some internet providers don't allow sharing a connection. Check your provider's terms before turning it on.

You can turn it off at any time in Settings; Otium stops Proxybase's client straight away. Uninstalling Otium
removes it.

## 10. Updates

Otium updates itself, including security updates, and tells you when it has. We keep Otium working and secure for as
long as we offer it. If you block updates, parts of it may stop working.

## 11. Antivirus

Some antivirus programs flag mining software even when you installed it yourself. Otium tells you if yours blocks it
and how to allow just Otium's folder. Never turn your antivirus off altogether.

## 12. Privacy

How we handle your data is in the [privacy policy](https://otium.lol/privacy). Sharing your earnings statistics is
your choice and can be changed in Settings.

## 13. Withdrawal and stopping

You may withdraw from this agreement **within 14 days** of starting Otium, without giving a reason: use Settings →
Your data → **Delete my account**, or email us at contact@otium.lol (a short message is enough; no form needed). On
request, we'll pay to your address what Otium's fee mined from your computer during those 14 days. This applies
wherever you live, even if your local law gives you a shorter right or none.

You can also stop at any time after that: pause Otium, uninstall it, or delete your account. Anything waiting at your
pool stays yours there.

## 14. If something goes wrong

You have the rights the consumer law of the country where you live gives you. **Nothing in these terms limits them.**

If Otium doesn't work as described, tell us (Settings → Send a problem report, or email) and we'll fix it within a
reasonable time. We don't exclude liability for death or personal injury, intent or gross negligence, or anything
consumer law doesn't allow us to exclude. We aren't liable for losses in a business that uses Otium.

## 15. When we may suspend Otium

We may stop providing Otium to a computer or account, with notice where we can, if:
- it's used on computers without their owner's permission, or to break the law;
- the user is in a country where Otium isn't available or is subject to sanctions;
- it's needed to protect users or Otium from a security problem;
- a partner ends the service Otium depends on.

What's at your pool stays yours in any case.

## 16. Changes to these terms

We may change these terms for good reason, such as new law, new features or a partner's changed terms. We'll tell you
at least 30 days before an important change takes effect, in the app and by email if you gave one. If you don't
accept it, you can stop using Otium and delete your account at no cost. Earlier versions are available on request
at contact@otium.lol.

## 17. Invites

Share your invite code only with people you know, and don't send spam. Fee-free days and fee shares follow the rules
shown in the app. We may refuse invites used to get around those rules (for example inviting yourself).

## 18. Law and disputes

These terms are governed by the law of Norway, where Otium is run from. If you're a consumer, you also keep the
protection of the mandatory consumer law of the country where you live, and you can take a dispute to the courts there.

If you have a complaint, contact us first at contact@otium.lol; most things can be sorted out that way. If we can't
agree, you can turn to the consumer protection authority or consumer dispute service where you live. In the EU, EEA
and UK, your national European Consumer Centre can help for free.
