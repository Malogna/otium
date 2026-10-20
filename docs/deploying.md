# Deploying the Otium server

This runs the Otium server on one Linux VPS with Docker Compose:

- **otium**: the ASP.NET Core server (`src/Otium.Server/Dockerfile`), as a non-root user, with its SQLite database on
  the `otium-data` volume. It publishes no ports.
- **caddy**: the only public entry point. It gets and renews a Let's Encrypt certificate on its own, serves HTTPS on
  443, redirects HTTP, and proxies to the server with each visitor's address in `X-Forwarded-For`.
- **backup**: takes an online SQLite backup every few hours, keeps a number of days, and can copy each one off the
  machine with rclone.

Everything lives in [`deploy/`](../deploy): `docker-compose.yml`, `Caddyfile`, `.env.example` (every server setting,
commented), `backup.env.example` and `backup/`.

```
internet ──443──▶ caddy ──8080──▶ otium ──▶ /data/otium.db ◀── backup ──▶ (optional) off-site storage
                                    │
                                    └──private network──▶ bitcoind (payouts) and monero-wallet-rpc (custodial only)
```

## 1. Provision the machine

- A small VPS is enough to start: 1–2 vCPUs, 2 GB RAM, 20 GB disk, Ubuntu 24.04 LTS or Debian 12.
- Log in with an SSH key, create a non-root user for day-to-day work, disable password logins, and turn on automatic
  security updates (`unattended-upgrades` on Ubuntu/Debian).
- Install Docker Engine and the Compose plugin from Docker's own repository
  ([docs.docker.com/engine/install](https://docs.docker.com/engine/install/)), then add your user to the `docker` group.
  Compose 2.24 or later is needed (`docker compose version`).
- Firewall: allow only SSH, 80/tcp, 443/tcp and 443/udp (HTTP/3), e.g. with `ufw`. Port 80 must stay open: Let's
  Encrypt validates through it and Caddy uses it to redirect to HTTPS. Note that Docker's published ports bypass
  `ufw`; that's why only Caddy publishes any.

## 2. DNS

Create an **A record** for the server's name (e.g. `api.otium.example`) pointing at the VPS's IPv4 address, and wait
until `dig +short api.otium.example` returns it. Caddy can't get a certificate before that.

Don't add an **AAAA record** unless you also enable IPv6 on the compose network. On Docker's default setup, IPv6
visitors reach Caddy through Docker's userland proxy and all appear to come from one internal address, which breaks
per-IP rate limits, the per-connection quotas, invite checks and invite links, and the "one paid sharer per internet
connection" rule for internet sharing.

**Behind Cloudflare (proxied, orange cloud).** This works too, and hides the server's address. Set
`CADDYFILE=Caddyfile.cloudflare` in `.env` (step 3) and Cloudflare's SSL/TLS mode to **Full (strict)**. Caddy still gets
its own Let's Encrypt certificate, because Cloudflare passes `/.well-known/acme-challenge/` through to port 80. It takes
each visitor's address from Cloudflare's `CF-Connecting-IP` header, trusted only from Cloudflare's published ranges
(listed in that file; update them if Cloudflare adds any). `ACME_EMAIL` isn't needed. That file also redirects
`www.<domain>` to the bare domain, so point both at the server. Cloudflare's own AAAA records are fine: Cloudflare
reaches the server over IPv4.

Requests that reach the server's own address directly, not through Cloudflare, get a `403`: they would skip Cloudflare's
firewall and could send a made-up `CF-IPCountry` to get around the country checks. Also limit 80/tcp, 443/tcp and
443/udp to [Cloudflare's IPv4 ranges](https://www.cloudflare.com/ips-v4/) in the VPS provider's firewall (on Lightsail:
the instance's **Networking** tab, one rule per range). Let's Encrypt still reaches port 80 through Cloudflare. Check
from your own machine that `curl --resolve <domain>:443:<server IP> https://<domain>/` no longer answers (or answers
`403`), while `https://<domain>/` still works.

## 3. Get the code and configure it

```bash
git clone https://github.com/Malogna/otium.git
cd otium/deploy
cp .env.example .env
chmod 600 .env
```

Edit `.env`. Each setting has a comment; the essentials:

| Setting | Value |
|---|---|
| `OTIUM_DOMAIN` | The DNS name from step 2 |
| `ACME_EMAIL` | Your email, for Let's Encrypt notices |
| `Referral__PublicBaseUrl` | `https://` + `OTIUM_DOMAIN` |
| `Admin__Token` | A long random secret: `openssl rand -hex 32` |
| `Custody__Enabled` | Leave unset (non-custodial: pools pay users directly). `true` turns on balances, cash-outs and the settings below marked "custodial" |
| `Unmineable__OtiumTarget` | Otium's fee address on unMineable, e.g. `USDT:T…`, shown with its referral earnings on the admin dashboard |
| `Sharing__RelayApiKey`, `Sharing__FeeRecipient`, `Sharing__FeeBps` | Internet sharing payouts: Relay converts each Proxybase payout to the user's coin; Otium's cut (basis points, default the mining fee) is paid by the app from the user's sharing earnings to your 0x address on Tempo, never taken from the conversion |
| `Sharing__SideShiftSecret`, `Sharing__SideShiftAffiliateId`, `Sharing__ChangeNowApiKey` | Optional: LTC (SideShift) and XMR (ChangeNOW) sharing payouts; without them those users are asked to pick another coin for sharing |
| `Mysterium__Enabled`, `Mysterium__SponsorPrivateKey` | Earlier, not offered now. Internet sharing: Otium pays each new Mysterium node's registration (about 0.13 MYST) from this Polygon wallet. Keep only a few days' MYST and a little POL in it |
| `Earnings__PoolWallets__0` | Custodial: your XMR receiving (sub)address, unless the wallet RPC creates them |
| `Email__*` | Your SMTP service, so email sign-in and receipts work |
| `BitcoinWallet__*` | Custodial: the Bitcoin treasury that pays every crypto cash-out (see [Bitcoin treasury](#bitcoin-treasury)) |
| `Kryptex__*`, `CpuMining__KryptexPercent` | Custodial: Kryptex Pool for GPU coins and, when moved there, CPU Monero |
| `MoneroWallet__*` | Custodial, and only with a wallet RPC for pool addresses (see [Monero wallet](#monero-wallet)) |

Leave optional settings commented out rather than empty: an empty value overrides the default. Values containing `$`
must be wrapped in single quotes. The [Running the server](technical.md#running-the-server) table describes
every setting.

`docker-compose.yml` sets `ReverseProxy__Enabled=true` and `ReverseProxy__KnownProxies__0=172.30.0.10` (Caddy's fixed
address) for the server itself, so it takes visitors' addresses from `X-Forwarded-For` and ignores that header from
anyone but Caddy. If `172.30.0.0/24` clashes with another network on the host, change the subnet, Caddy's
`ipv4_address` and `ReverseProxy__KnownProxies__0` together.

## 4. First start

```bash
docker compose up -d --build
docker compose ps
docker compose logs -f caddy     # watch for "certificate obtained successfully", then Ctrl+C
```

The first build takes a few minutes. The server creates `/data/otium.db` on its first start.

## 5. Check it

```bash
curl https://api.otium.example/healthz
```

answers `ok` over a valid certificate. Then check the admin API with your token:

```bash
curl -H "X-Admin-Token: $TOKEN" https://api.otium.example/admin/summary
```

It returns totals (all zero on a new server), and `401` with a wrong token. The **operator dashboard** is at
`https://api.otium.example/admin` (in builds that include it): enter the `Admin__Token` to see the totals and the
cash-out review queue. Serve it only over HTTPS, as here. To also limit `/admin` to your own address, add this to the
site block in the `Caddyfile` and run `docker compose restart caddy`:

```
@admin_elsewhere {
	path /admin /admin/*
	not remote_ip 198.51.100.7
}
respond @admin_elsewhere 404
```

Behind Cloudflare (`Caddyfile.cloudflare`) write `not client_ip 198.51.100.7` instead: `remote_ip` would be
Cloudflare's address there.

**Behind Cloudflare, prefer Cloudflare Access** to an address rule (otium.lol does this). In Zero Trust, add a
self-hosted application for `<domain>/admin` and `<domain>/admin/*` with a policy that allows only your email, signing
in with a one-time PIN. Cloudflare then asks for a code emailed to you before anyone reaches the dashboard; the
`Admin__Token` is still needed after that. Access also catches `/ADMIN`, `//admin` and `/%61dmin`, which ASP.NET would
otherwise route to the same pages. The `curl` check above then needs an Access service token (Zero Trust → Access
controls → Service credentials, allowed by a Service Auth policy on the same application), sent as
`-H "CF-Access-Client-Id: …" -H "CF-Access-Client-Secret: …"` next to the `X-Admin-Token`.

Changing the token: edit `Admin__Token` in `.env`, then `docker compose up -d` (it recreates the server container
with the new value).

Finally, point the desktop app at the server: set the GitHub repository variable `OTIUM_SERVER_URL` to
`https://api.otium.example` (Settings → Secrets and variables → Actions → Variables, or
`gh variable set OTIUM_SERVER_URL --body https://api.otium.example`). The release workflow bakes it into the app.

**The website.** The server's address is also Otium's website: `/` is the front page (what Otium does, an estimate of
what a PC could earn next to the around-the-clock figure comparison sites quote, and the cost breakdown, built from the
server's `Fees:*` settings), and invite links open `/r/<code>`, which also remembers the code for a week for that
internet connection, so Otium installed there fills it in. The Download buttons go to `/download`, Otium's own page:
it starts the installer for the visitor's system from the newest release (the newest release candidate until there's
a stable one), and `/download/{windows|macos|macos-intel|linux}` link each installer directly. Only while no release
can be read from GitHub do they go to `Referral__DownloadUrl` (GitHub's releases page by default). The app's update
feed is served at `/updates/{stable|pre}/`, only from releases with a valid signature. Privacy and terms pages appear at
`/privacy` and `/terms`, and in every page's footer, once you add their reviewed text as HTML (just the body, no
`<html>` or `<head>`):

```bash
docker compose exec otium mkdir -p /data/site
docker compose cp privacy.html otium:/data/site/privacy.html
docker compose cp terms.html otium:/data/site/terms.html
```

They're read on every request, so no restart is needed. Backups copy only the database, so keep your own copy of
these files. `docs/privacy.md` is a draft for legal review, not a policy to publish as it is.

## 6. Updating

```bash
cd otium/deploy
docker compose run --rm backup once     # fresh backup first
git pull
docker compose up -d --build
docker compose logs --tail 50 otium
curl https://api.otium.example/healthz
```

The server upgrades the database schema at startup and never downgrades it. To roll back, check out the previous
version, and if the update changed the schema, also restore the backup taken just before it.

Update Caddy and the backup image now and then with `docker compose pull caddy && docker compose build --pull backup
&& docker compose up -d`.

## 7. Backups

The `backup` service runs `sqlite3 .backup`, which takes a consistent snapshot while the server keeps writing (the
database is in WAL mode, so copying `otium.db` with `cp` could produce a broken copy). Each backup is checked with
`PRAGMA integrity_check`, gzipped, and stored on the `otium-backups` volume as `otium-YYYYMMDDTHHMMSSZ.db.gz`.

| `.env` setting | Default | |
|---|---|---|
| `BACKUP_INTERVAL_HOURS` | 6 | Hours between backups (also one at container start) |
| `BACKUP_KEEP_DAYS` | 14 | Local backups older than this are deleted |
| `BACKUP_RCLONE_REMOTE` | empty | Off-site destination, e.g. `offsite:otium-backups` |
| `BACKUP_RCLONE_KEEP_DAYS` | empty | Delete off-site copies older than this; empty keeps them all |

```bash
docker compose logs backup                                   # results of each run
docker compose run --rm backup once                          # back up now
docker compose exec backup ls -lh /backups                   # list backups
docker compose cp backup:/backups ./otium-backups-copy       # copy them all to the host
```

**Off-site copies.** A backup on the same disk doesn't survive losing the VPS. Copy `backup.env.example` to
`backup.env` (`chmod 600`), define an rclone remote there (S3-compatible storage, Backblaze B2, SFTP, ...), and set
`BACKUP_RCLONE_REMOTE` in `.env`. Those credentials go to the backup container only. Where the provider allows it,
give them write-only access and expire old copies with a lifecycle rule, so a compromised server can't delete its own
backups. Test with `docker compose run --rm backup once`.

On Cloudflare R2 (otium.lol's setup): give the backup container an R2 token with **Object Read & Write** on that one
bucket only (an Admin token could remove the lock below), and add a **bucket lock** rule (R2 → bucket → Settings →
Bucket lock) so no copy can be deleted or overwritten until it's 30 days old. Then set `BACKUP_RCLONE_KEEP_DAYS=31`:
copies older than that are pruned, which the privacy policy relies on ("backups are kept for up to 31 days"), and the
lock never blocks the pruning.

### Restore

1. Pick a backup (`docker compose exec backup ls -lh /backups`). For an off-site copy, put it into the volume first,
   e.g. `docker compose cp ./otium-20261016T060000Z.db.gz backup:/backups/`.

2. Stop the server and the scheduled backups so nothing touches the database during the swap:

   ```bash
   docker compose stop otium backup
   ```

3. Restore it, keeping the current database next to it as `otium.db.before-restore`:

   ```bash
   docker compose run --rm --entrypoint sh backup -c '
     set -e
     cd /data
     gunzip -c /backups/otium-20261016T060000Z.db.gz > otium.db.restore
     [ "$(sqlite3 otium.db.restore "PRAGMA integrity_check;")" = ok ]
     for f in otium.db otium.db-wal otium.db-shm; do
       if [ -e "$f" ]; then mv "$f" "otium.db.before-restore${f#otium.db}"; fi
     done
     mv otium.db.restore otium.db
     ls -l'
   ```

4. Start the server and check it:

   ```bash
   docker compose start otium backup
   curl https://api.otium.example/healthz
   curl -H "X-Admin-Token: $TOKEN" https://api.otium.example/admin/summary
   ```

Everything recorded after the backup's timestamp is lost: credits, new accounts and sign-ins, cash-out requests and
their status. Check payouts sent since the backup against the Bitcoin treasury's transactions (and THORChain swaps)
before approving anything, so nobody is paid twice. Practise a restore on a spare machine before you need one.

## Bitcoin treasury

Every crypto cash-out is paid from a `bitcoind` wallet: BTC directly, ETH/BNB/SOL by sending BTC to THORChain with a
swap memo. It holds spendable funds, so treat it like the Monero wallet below:

- **Never expose its RPC port** (8332). Run `bitcoind` (pruned, e.g. `prune=10000`, is enough) on the host or a
  separate machine on a private network, with `rpcauth` credentials and `rpcallowip` limited to the Otium server.
  From the server container it's reachable through the Docker host or the private network; put that address in
  `BitcoinWallet__RpcUrl`.
- Create a descriptor wallet for payouts (`bitcoin-cli createwallet otium-payouts`) and set `BitcoinWallet__Wallet`.
  Payouts and swap change use native SegWit addresses, as THORChain requires.
- Keep only a few weeks of payouts in it, and back up its descriptors/seed separately; the database backups don't
  include it. The admin dashboard shows its balance and warns when it's below the open cash-outs.
- Top it up by sweeping the Kryptex account's BTC about weekly (Kryptex account withdrawals are confirmed by email).

Without it, crypto cash-outs wait in the review queue and can be paid by hand and recorded with `mark-paid`.

## Monero wallet

`monero-wallet-rpc` creates MoneroOcean pool subaddresses for balance accounts (and sends Monero cash-outs only if
`xmr` is re-enabled). It holds spendable funds, so:

- **Never expose its port.** Run it either as the commented-out `monero-wallet-rpc` service in `docker-compose.yml`,
  on the `internal` network `wallet` that only the server joins, or on a separate machine reached over a private
  network (WireGuard, Tailscale, the provider's private VLAN), with its firewall allowing only the Otium server.
  Don't publish `18083` in compose and don't open it in the firewall.
- Start it with `--rpc-login user:password` and put the same values in `MoneroWallet__Username`/`Password`. Inside
  compose, `--rpc-bind-ip=0.0.0.0 --confirm-external-bind` is needed so the server container can reach it on the
  private network; `MoneroWallet__RpcUrl=http://monero-wallet-rpc:18083/json_rpc`.
- Point it at your own `monerod` or a remote node you trust (`--daemon-address`).
- Keep only a working float in this hot wallet and top it up from cold storage. Its wallet files need their own
  backup (the seed above all); the database backups don't include them.

Without a wallet RPC, set `Earnings__PoolWallets__0` to a receiving subaddress instead.

## Troubleshooting

- **No certificate:** check that the A record points here, ports 80 and 443 are open, and `docker compose logs caddy`.
  Let's Encrypt rate-limits repeated failures, so fix DNS before retrying.
- **`502` from Caddy:** the server isn't running or is still starting; see `docker compose logs otium`.
- **Every internet-sharing user after the first is refused as "same connection":** the server is seeing Caddy's
  address instead of visitors'. Check that `ReverseProxy__KnownProxies__0` matches Caddy's address and that there's no
  AAAA record (see [DNS](#2-dns)).
- **`network ... overlaps`:** another network uses `172.30.0.0/24`; change it as described in step 3.
