# Code signing

Otium builds unsigned unless signing secrets are set. Unsigned builds work, but:

- **Windows:** SmartScreen shows "Windows protected your PC" on first run, and antivirus products trust unsigned
  miners even less than signed ones.
- **macOS:** Gatekeeper refuses to open apps that aren't signed with a Developer ID and notarized. Users have to
  right-click → Open, or allow it in System Settings, which most won't do.
- **Linux:** AppImages are normally unsigned. Nothing to do.

Signing happens inside `build/package.ps1` (Velopack's `vpk pack`), so the app binaries, `Setup.exe` and the update
packages are all signed in one go. The release workflow passes the secrets below to it.

## Windows

Since June 2023, publicly trusted code-signing keys must live in hardware (an HSM or token), so a `.pfx` file in a CI
secret is no longer an option for new certificates. That leaves two practical routes for a GitHub-hosted runner.

### Option A: Azure Artifact Signing (formerly Trusted Signing)

Microsoft's managed signing service. It's the cheapest and simplest route if you're eligible.

- **Cost:** Basic plan, $9.99 a month for 5,000 signatures (as of 2026).
- **Who can use it:** as of early 2026, organizations or individuals in the USA and Canada, and organizations in the
  EU and the UK. Individuals elsewhere (including Norway) can't. Check Microsoft's current eligibility list first.
- **Certificates** are short-lived (days) and renewed automatically; every signature is timestamped, so signed
  releases stay valid.

Setup:

1. In the Azure portal, create an **Artifact Signing account**, complete **identity validation** (it can take days),
   and create a **Public Trust certificate profile**.
2. Create an app registration (Microsoft Entra ID → App registrations) with a client secret, and give it the
   **Artifact Signing Certificate Profile Signer** role on the signing account.
3. In GitHub → Settings → Secrets and variables → Actions:
   - Secrets: `AZURE_TENANT_ID`, `AZURE_CLIENT_ID`, `AZURE_CLIENT_SECRET`.
   - Variables: `AZURE_SIGNING_ENDPOINT` (e.g. `https://weu.codesigning.azure.net`), `AZURE_SIGNING_ACCOUNT`,
     `AZURE_SIGNING_PROFILE`.

The workflow writes the `metadata.json` that `vpk pack --azureTrustedSignFile` needs. vpk 1.2.161 ships its own
`signtool.exe` and the Azure signing library, so nothing else needs installing; it signs on the Windows runner.

To sign locally instead, `az login`, write the metadata file yourself and run:

```powershell
$env:SC_WIN_AZURE_SIGN_METADATA = "C:\path\azure-signing.json"
pwsh build/package.ps1 -Runtimes win-x64 -ServerUrl https://api.example.com
```

### Option B: an OV or EV certificate from a certificate authority, held in a cloud HSM

For anyone who can't use Artifact Signing. Buy a code-signing certificate whose key lives in the CA's cloud signing
service, for example DigiCert KeyLocker, SSL.com eSigner, Certum SimplySign or GlobalSign, or put your own certificate
in Azure Key Vault Premium. An organization certificate needs a registered company; individual (OV) certificates are
available from some CAs, including Certum.

Each vendor ships a command-line signer or a signtool plug-in. Put the command in the `WIN_SIGN_TEMPLATE` secret, with
`{{file}}` where the file to sign goes. For example, with AzureSignTool and a key in Azure Key Vault:

```
AzureSignTool sign -kvu https://my-vault.vault.azure.net -kvc my-cert -kvi <client-id> -kvs <secret> -kvt <tenant> -tr http://timestamp.digicert.com -td sha256 {{file}}
```

If the vendor's tool needs installing first, add that step to the `package` job before **Build installer** (Windows
runner only). If your certificate is reachable through signtool directly (for example via the vendor's KSP), use the
`WIN_SIGN_PARAMS` secret with signtool arguments instead, e.g. `/fd sha256 /tr http://timestamp.digicert.com /td sha256
/sha1 <thumbprint>`.

Always timestamp, or signatures expire with the certificate.

### SmartScreen and antivirus

- Signing alone doesn't remove the SmartScreen warning at first; reputation builds up per certificate (or per file)
  as people install it. EV certificates no longer skip this.
- Mining software is a "potentially unwanted application" category for most antivirus products, whatever the
  signature says. Otium downloads XMRig and SRBMiner at runtime, and those get flagged, not Otium itself. After each
  release, submit the installer and the engines' hashes as false positives:
  [Microsoft Defender](https://www.microsoft.com/wdsi/filesubmission) first, then the vendors users report.
- Keep everything that runs visible in the app (it already is) and describe it plainly on the download page.

## macOS

1. Join the **Apple Developer Program** ($99 a year).
2. In Certificates, Identifiers & Profiles, create a **Developer ID Application** and a **Developer ID Installer**
   certificate. Install both in your keychain, then export them together as one `.p12` with a password.
3. At [appleid.apple.com](https://appleid.apple.com), create an **app-specific password** for notarization.
4. Add GitHub secrets:

   | Secret | Value |
   |---|---|
   | `APPLE_CERTIFICATES_P12` | `base64 -i certs.p12` output |
   | `APPLE_CERTIFICATES_PASSWORD` | the `.p12` password |
   | `APPLE_APP_IDENTITY` | `Developer ID Application: Your Name (TEAMID)` |
   | `APPLE_INSTALL_IDENTITY` | `Developer ID Installer: Your Name (TEAMID)` |
   | `APPLE_ID` | your Apple ID email |
   | `APPLE_TEAM_ID` | the 10-character team id |
   | `APPLE_APP_PASSWORD` | the app-specific password |

The workflow imports the certificates into a temporary keychain, stores notarization credentials, and vpk signs,
notarizes and staples both the `.app` and the `.pkg`.

## Checking a signed build

- Windows: right-click `Otium.Desktop-win-x64-Setup.exe` → Properties → Digital Signatures, or
  `Get-AuthenticodeSignature .\Otium.Desktop-win-x64-Setup.exe`.
- macOS: `spctl -a -vv /Applications/Otium.app` should say "accepted, source=Notarized Developer ID".
