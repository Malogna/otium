# Otium

*Latin for "idle time".* Otium earns money with your computer while you're away from it. Your processor and graphics
card mine the best-paying coin, and the mining pool pays **your own wallet**, in the coin you choose. Otium sets it all
up, switches coins for you and steps aside the moment you touch the mouse.

**[Download at otium.lol](https://otium.lol)** · Windows · macOS and Linux (beta)

<table>
  <tr>
    <td width="50%">
      <picture>
        <source media="(prefers-color-scheme: dark)" srcset="docs/screens/welcome-dark.png">
        <img alt="Welcome screen with Quick start and Customize" src="docs/screens/welcome-light.png">
      </picture>
    </td>
    <td width="50%">
      <picture>
        <source media="(prefers-color-scheme: dark)" srcset="docs/screens/home-dark.png">
        <img alt="Overview with the two-minute test and what this PC earned" src="docs/screens/home-light.png">
      </picture>
    </td>
  </tr>
</table>

<sub>Drawn from the app in dev mode, with a simulated Ryzen 7 7700X and RTX 4090. Your own numbers come from the
two-minute test on your PC.</sub>

## How it works

1. **Install and pick Quick start** (everything on) or **Customize** (see what this PC could earn and choose each part).
2. **Paste one wallet address.** USDT on TRON is the default; BTC, LTC, ETH, BNB, SOL, TRX and Monero work too. The app
   checks the address and its network as you type. That address is the only place money goes.
3. **Press Start earning.** A two-minute test measures what this PC really earns, after fees and electricity. From
   then on Otium earns only while you're away (after 3 minutes without keyboard or mouse) and stops the moment you
   come back.
4. **The pool pays you.** [unMineable](https://unmineable.com) pays your address once a day after you reach its minimum
   (for example 1.5 USDT). The app shows how far you are from the next payout. Users in the US are paid in Monero by
   [MoneroOcean](https://moneroocean.stream), because unMineable doesn't serve US residents.

Otium never holds your money. There's no balance to cash out and no account you have to trust with funds.

## What you get

- **Watches the price of power.** Otium pauses when electricity would likely cost more than it earns. The price is
  estimated for your country (or enter your own), or follows live hourly prices in Norway, Sweden, Denmark and
  Finland. Grid fees and taxes may not be included, so treat it as an estimate.
- **Steps aside for you.** Mining runs at the lowest priority, only while you're away. It keeps the computer awake
  while it earns (the screen still turns off), never keeps a laptop on battery awake, and makes the computer warmer
  and its fans louder.
- **The best coin for your card.** The graphics card mines whichever coin pays most for it right now, and moves on if
  the pool stops crediting one.
- **Internet sharing (optional).** Earn from internet you're not using through [Proxybase](https://proxybase.xyz),
  paid to the same wallet in the same coin. It's off until you agree to it, and the app explains who uses it first.
- **Plain reports.** "While you were away: $0.84 in 7 h 40 min", a monthly summary, and notifications for payouts and
  problems. Each can be shared as a picture with your invite code.
- **Options in Settings:** efficiency mode for NVIDIA cards (70% power while mining), let the graphics card earn while
  you browse (stops for games and full-screen video), and keep the miner ready so it starts again instantly.
- **Invites.** Friends you invite mine fee-free for 30 days, and part of Otium's fee is mined to you for 6 months.

## What it costs

| Who | Takes |
|---|---|
| Otium | 2% of mining time (about 29 minutes per day of mining), mined to Otium's address. Nothing while paused. |
| Mining software | XMRig 1% (processor), SRBMiner 0.85–2% (graphics card, depending on the coin) |
| Pool | unMineable 0.75% (1% without Otium's referral code; unMineable pays Otium part of its own fee for the code) |
| Internet sharing | 2% of each payout's conversion to your coin |

In total about 3.75% on the processor and 3.6–4.75% on the graphics card. The app shows the same breakdown before you
start.

## Safety and privacy

- Otium runs well-known miners, [XMRig](https://github.com/xmrig/xmrig) (open source) and
  [SRBMiner-Multi](https://github.com/doktor83/SRBMiner-Multi), downloaded from their official releases and checked
  against fixed SHA-256 hashes. Some antivirus tools flag any miner; the app tells you if yours blocks it and how to
  allow it.
- Otium's server keeps your payout address, your computers and invite codes. An email address is optional (it lets
  you keep your invites when you reinstall). Settings has **Download my data** and **Delete my account**.
- "Help show what PCs really earn" (on by default, can be turned off at the start or in Settings) sends your graphics
  card and processor model and what they earned, never your address. otium.lol shows only totals across several PCs.
- Uninstalling removes the miners, Otium's scheduled tasks and its downloaded files.

## Help

Something not working? In the app, go to **Settings → Send a problem report**. It sends what each miner is doing and
their recent messages, with your addresses and keys taken out, and gives you a reference. Then
[open an issue](https://github.com/Malogna/otium/issues) and mention it.

## For developers

```bash
dotnet test                              # unit and API tests
pwsh build/dev.ps1                       # dev mode: app, server and a simulated pool on one PC
dotnet run --project src/Otium.App       # the app on its own
```

The app is .NET 10 with Avalonia, and the server is ASP.NET Core with SQLite. How everything works, the server's
settings, building installers and releasing are in [docs/technical.md](docs/technical.md). Also:
[deploying](docs/deploying.md), [releasing](docs/releasing.md), [release candidates](docs/release-candidate.md),
[privacy policy draft](docs/privacy.md), [terms of use draft](docs/terms.md), [legal review](docs/legal-review.md).
