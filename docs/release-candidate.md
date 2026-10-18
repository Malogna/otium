# Release candidate checklist

Before v0.1.0, a release candidate is installed on a real Windows PC and checked by hand. Tag `v0.1.0-rc.1` (then
`rc.2`, …): the release workflow builds the real installers and publishes them on GitHub as a **pre-release**, which
the website's Download button and stable installs ignore. Download the Windows installer from the pre-release page.

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
- [ ] Tag `v0.1.0-rc.2` (any small change): the installed rc.1 offers "Restart to update" within a few hours, and
      comes back as rc.2 with settings and earnings kept.
- [ ] Uninstall from Windows Settings: Otium asks for permission once (efficiency mode's tasks), and afterwards
      `schtasks /Query /TN "\Otium\GPU power cap"` finds nothing, no `xmrig`/`SRBMiner`/`proxybase-cli` process runs,
      and `%LOCALAPPDATA%\Otium\engines` is gone.

## Before tagging v0.1.0

- [ ] **Relay API key** on the server (`Sharing__RelayApiKey` in `deploy/.env`), so internet sharing payouts reach users'
      wallets; then one real sharing payout arrived (Advanced shows "paid out to your wallet", and the coin arrived).
- [ ] Reviewed privacy policy and terms uploaded to `/data/site` (they then appear on the site).
- [ ] Anything that went wrong above is fixed and checked again in a new candidate.
