# Releasing Otium

A release goes: **tag** → the workflow **builds and publishes** → **sign** it on the developer's PC → **check**.

Pushing a tag like `v0.1.0` runs `.github/workflows/release.yml`. It checks the release settings, runs the tests on
Windows and Linux, builds installers for Windows, Linux and macOS (Intel and Apple silicon), and publishes them as one
GitHub release. That release is also the auto-update feed: installed copies ask otium.lol's `/updates/stable/` (or
`/updates/pre/` for release candidates), which reads the GitHub releases every 10 minutes, and fall back to GitHub
itself (`AppIdentity.UpdateUrl`, `https://github.com/Malogna/otium`). Both only work while the repository is public.
The website's download page (otium.lol/download) offers the same release's installers.

Installed copies only install a release whose update feeds are **signed** with a key built into Otium, and otium.lol
only serves signed feeds. The workflow can't sign (the key never leaves the developer's PC), so a published release
reaches nobody until it's signed: see [Signing a release](release-candidate.md#signing-a-release-every-release-on-the-developers-pc).

## Before the first release (v0.1.0)

1. **Deploy the server** behind HTTPS (see "Running the server" and "Before launch" in [technical.md](technical.md)) and check
   `https://<server>/healthz` answers `ok`.
2. **Set the repository variable `OTIUM_SERVER_URL`** to that URL. It's baked into the app at build time
   (`-p:OtiumServerUrl`). The workflow refuses to release without it, because a build without it talks to
   `http://localhost:5080`.
3. Optional settings:
   - Variable `OTIUM_FEE_WALLET`: Otium's fee address, `USDT:T…`. Everyone outside the US mines on unMineable
     (Monero users too), so this one address receives the whole fee. Optionally add `;XMR:4…` for US users, whose
     Monero mines on MoneroOcean; without it their fee slice stays with them. Otium's
     unMineable referral code (`ztjq-jr7s`) is built in; `-UnmineableReferral` in `build/package.ps1` changes it.
   - Secret `OTIUM_HONEYGAIN_KEY`: custodial servers only. A build with it shares internet through the Honeygain SDK
     (whose libraries must sit next to the app) instead of Proxybase, so leave it unset for the non-custodial app; the
     `check` job's warning about it doesn't apply then.
4. **Code signing** (optional but strongly recommended): see [code-signing.md](code-signing.md). Without it, Windows
   warns on first run and macOS refuses to open the app.
5. Check `<Version>` in `Directory.Build.props` matches the tag (it's `0.1.0`; a candidate `v0.1.0-rc.N` builds the
   version it's a candidate for), and that `docs/release-notes/<version>.md` has the release notes (they're packed
   into the release).
6. On the PC that will sign releases, the update-signing key is in place (`tools/Otium.UpdateSigner`; see
   [release-candidate.md](release-candidate.md#signing-a-release-every-release-on-the-developers-pc)).

## Cutting the release

1. Merge the release branch into `main` and run `dotnet test` there (the workflow runs the tests again before
   building).
2. Tag the merge commit and push the tag:

   ```bash
   git switch main
   git pull
   git tag -a v0.1.0 -m "Otium 0.1.0"
   git push origin v0.1.0
   ```

3. Watch the run under **Actions → Release**. The `check` job lists anything missing (unsigned packages, no
   Honeygain key) as warnings, and fails on a version mismatch or a missing server URL.
4. Once the release is published, sign its update feeds on the PC that holds the key, then check what otium.lol
   serves (`/updates/pre/` for a release candidate):

   ```bash
   dotnet run --project tools/Otium.UpdateSigner -- sign v0.1.0 --key <key file>
   dotnet run --project tools/Otium.UpdateSigner -- check https://otium.lol/updates/stable/
   ```

   `sign` needs the GitHub CLI (`gh`, or its path in `GH_PATH`). Until a release is signed, installed copies stay on
   the previous signed one.

To try the pipeline without publishing to users, run the workflow by hand (**Actions → Release → Run workflow**) with
a pre-release version such as `0.1.0-rc.1` after setting `<Version>` to match, and tick "allow local server" only if
you really want a localhost build. Don't sign it, and delete the test release afterwards: the website's download page
offers the newest release with installers, signed or not, until a newer one appears.

## After publishing

- Download each installer from the release page and install it on a clean machine (or VM): it should start, connect
  to the production server (the overview shows a balance, not "Can't reach Otium") and start earning.
- Check the signatures (see [code-signing.md](code-signing.md#checking-a-signed-build)).
- Submit the Windows installer to Microsoft Defender as a false positive if it gets flagged.
- From the second release on, Velopack builds delta updates from the previous release's packages, and installed
  copies update themselves once the release is signed: a downloaded update installs while the user is away
  (Advanced → Settings → About shows it, with "Install updates while I'm away").

## If something goes wrong

- A failed run publishes nothing; fix the cause and re-run the failed jobs, or delete and re-push the tag:

  ```bash
  git push --delete origin v0.1.0
  git tag -d v0.1.0
  ```

- A published release with a bad build: delete it on GitHub before many people install it, bump the version
  (`0.1.1`), and release again. Never re-use a version number that installed copies may already have.
