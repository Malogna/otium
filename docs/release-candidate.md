# Release candidate checklist

Before v0.1.0, a release candidate is installed on a real Windows PC and checked by hand. Tag `v0.1.0-rc.1` (then
`rc.2`, …): the release workflow builds the real installers and publishes them on GitHub as a **pre-release**, which stable
installs ignore. Until there is a stable release, the website's download page (otium.lol/download) offers the newest
release candidate; once there is one, it offers the newest stable release instead.

## Signing a release (every release, on the developer's PC)

Every release goes: tag → the release workflow builds and publishes it → sign it here → check. Installed copies (from
rc.15) only update from a feed signed with a key built into Otium (`AppIdentity.UpdateSigningKeys`), and otium.lol
only offers signed releases, even to older copies. The key never goes to
GitHub or the server, so taking over either can't push code to users. After the release workflow has published the
release, sign it on the PC that holds the key:

```bash
GH_PATH="C:/Program Files/GitHub CLI/gh.exe" dotnet run --project tools/Otium.UpdateSigner -- sign v0.1.0-rc.15 --key 'D:\otium-private\keys\update-signing.key'
dotnet run --project tools/Otium.UpdateSigner -- check https://otium.lol/updates/pre/
```

`sign` downloads the release's four `releases.*.json` feeds, signs them and uploads the `.sig` files next to them, then
checks them again. `check` confirms the update server serves feeds that verify (the server reads GitHub every 10
minutes). Until a release is signed, copies stay on the previous signed one.

The key file is protected with Windows DPAPI, so it only opens for the Windows account that made it. The **backup key**
(`update-signing-backup.pem`, trusted too) belongs offline: a USB stick or password manager, not this PC. If the PC or
its Windows account is lost, sign with the backup (`--key <backup>.pem`), then make a new key pair with
`keygen <folder>` and ship a release that trusts the new keys. If a key may have leaked, remove it from
`AppIdentity.UpdateSigningKeys` in the next release, signed with the other one.

## Before the first candidate

- [ ] `OTIUM_FEE_WALLET` and `OTIUM_SERVER_URL` are set as GitHub variables (done: USDT and XMR fee addresses, otium.lol).
- [ ] Code signing secrets, if ready (see [code-signing.md](code-signing.md)); without them Windows shows "Unknown publisher".

## On the PC (about an hour, mostly waiting)

**Install and start**
- [ ] The installer runs; Windows SmartScreen or the antivirus says something? Note exactly what.
- [ ] The welcome screen shows Quick start and Customize. Choose **Quick start**, paste a USDT (TRC-20) address.
- [ ] The summary lists processor, graphics card, internet sharing, keeping the miner ready and sharing earnings.
- [ ] Start earning: the two-minute test starts, shows progress and ends with "Measured on this PC" and a monthly figure.

**Mining**
- [ ] Step away for 5 minutes: both devices show "Earning". The PC doesn't go to sleep while it earns.
- [ ] Move the mouse: mining stops at once (the processor pauses), and starts again a few minutes after you leave.
- [ ] Coming back after 10+ minutes away: the "While you were away" card shows, and a Windows notification appeared.
- [ ] unMineable's page for the address (Advanced → open the pool page) shows the workers `ot…` and `ot…g`.
- [ ] After 90 minutes on Pearl the graphics card is still mining Pearl (unMineable credits it).

**Options**
- [ ] Settings → Efficiency mode on: one Windows permission prompt. While mining, NVIDIA's power limit is 70%
      (`nvidia-smi -q -d POWER` shows the lower "Current Power Limit"); after mining stops it's back to the default.
- [ ] Settings → Earn while I browse on: the graphics card mines while you browse; it stops when a game or a
      full-screen video starts.
- [ ] Settings → Send a problem report: it shows a reference; the admin page `/admin/problem-reports/<reference>`
      shows it, with no payout address in it.
- [ ] Internet sharing shows "Sharing" in Advanced (Proxybase's client running).

**Update and uninstall**
- [ ] Tag and sign the next candidate (any small change): within a few hours the installed one offers "Restart to
      update", or restarts into it by itself once you've been away 10 minutes with the window closed, and comes back
      as the new candidate with settings and earnings kept.
- [ ] Uninstall from Windows Settings: Otium asks for permission once (efficiency mode's tasks), and afterwards
      `schtasks /Query /TN "\Otium\GPU power cap"` finds nothing, no `xmrig`/`SRBMiner`/`proxybase-cli` process runs,
      and `%LOCALAPPDATA%\Otium\engines` is gone.

## Before tagging v0.1.0

- [ ] **Relay API key** on the server (`Sharing__RelayApiKey` in `deploy/.env`), so internet sharing payouts reach users'
      wallets; then one real sharing payout arrived (Advanced shows "paid out to your wallet", and the coin arrived).
- [ ] Reviewed privacy policy and terms uploaded to `/data/site` (they then appear on the site).
- [ ] Anything that went wrong above is fixed and checked again in a new candidate.
