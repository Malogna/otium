# Releasing Otium

Pushing a tag like `v0.1.0` runs `.github/workflows/release.yml`. It checks the release settings, runs the tests on
Windows and Linux, builds installers for Windows, Linux and macOS (Intel and Apple silicon), and publishes them as one
GitHub release. That release is also the auto-update feed: installed copies read it through `AppIdentity.UpdateUrl`
(`https://github.com/Malogna/otium`), which only works while the repository is public.

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
   - Secret `OTIUM_HONEYGAIN_KEY`: turns on internet sharing (needs the Honeygain SDK libraries next to the app).
4. **Code signing** (optional but strongly recommended): see [code-signing.md](code-signing.md). Without it, Windows
   warns on first run and macOS refuses to open the app.
5. Check `<Version>` in `Directory.Build.props` matches the tag (it's `0.1.0`), and that
   `docs/release-notes/<version>.md` has the release notes (they're packed into the release).

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

To try the pipeline without publishing to users, run the workflow by hand (**Actions → Release → Run workflow**) with
a pre-release version such as `0.1.0-rc.1` after setting `<Version>` to match, and tick "allow local server" only if
you really want a localhost build. Delete the test release afterwards.

## After publishing

- Download each installer from the release page and install it on a clean machine (or VM): it should start, connect
  to the production server (the overview shows a balance, not "Can't reach Otium") and start earning.
- Check the signatures (see [code-signing.md](code-signing.md#checking-a-signed-build)).
- Submit the Windows installer to Microsoft Defender as a false positive if it gets flagged.
- From the second release on, Velopack builds delta updates from the previous release's packages, and installed
  copies update themselves (Advanced → Updates shows it).

## If something goes wrong

- A failed run publishes nothing; fix the cause and re-run the failed jobs, or delete and re-push the tag:

  ```bash
  git push --delete origin v0.1.0
  git tag -d v0.1.0
  ```

- A published release with a bad build: delete it on GitHub before many people install it, bump the version
  (`0.1.1`), and release again. Never re-use a version number that installed copies may already have.
