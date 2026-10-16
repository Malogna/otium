# Bandwidth-sharing SDKs for an embedded desktop integration (Otium)

*Research date: 2026-10-16. Rates below are vendor "up to" claims unless noted. Items marked **[unverified]** could not be confirmed from a primary source. No sign-ups or forms were submitted.*

## Context: the 2026 landscape
- **IPIDEA takedown (28 Jan 2026).** Google GTIG, working with Cloudflare, Spur and Lumen, took legal action against the domains of IPIDEA, the largest residential proxy network. GTIG tied IPIDEA to four SDK brands: **Castar SDK, PacketSDK, EarnSDK and HexSDK**. It found the SDKs in more than 600 Android apps and over 3,075 Windows PE hashes, some disguised as "OneDriveSync" or "Windows Update". Play Protect now warns about and removes apps that contain these SDKs. Google's message to developers was that they "have a responsibility to vet the monetization SDKs they integrate" and that consent claims must be "transparent, auditable". On 2026-10-16, `castarsdk.com` and `packetsdk.com` returned NXDOMAIN when I checked.
- **Bright Data SDK (June 2026).** Include Security and Buchodi published research on the Bright Data SDK in smart-TV and phone apps. It reported vague consent wording, a cap of about 200 GB a month per device, and traffic used for AI scraping. Secondary coverage says Roku, Google TV and Fire OS have restricted or blocked background proxy SDKs **[unverified at primary source]**. A September 2026 QuoIntelligence test of Ludo Club, a Bright SDK partner app, saw no proxy opt-in prompt on first launch.
- **Microsoft Defender.** I found no named Defender signature for any proxyware vendor **[unverified]**. Microsoft's criteria have no dedicated proxyware category, but Defender would flag software that fails to give "prominent notice" or to "clearly indicate when the software is active", and it has a "poor industry reputation" PUA catch-all. Cisco Talos has argued that proxyware should be treated as PUA in the same way as cryptominers. Trend Micro already detects old Luminati DLLs as `PUA.Win32.Lutimani`. Expect some antivirus noise. Use EV code signing (Windows) and Developer ID notarization (macOS), and keep the feature visible and opt-in.

## Comparison

| SDK | Status 2026 | Desktop OS / format | .NET path | Publisher pay (vendor claims) | Per-device data | Consent rules |
|---|---|---|---|---|---|---|
| **Honeygain SDK** | Alive, onboarding new partners. Security assessment by Blaze Infosec, Jan 2026 | **Win** x86/x64/arm64 `hgsdk.dll`; **macOS** Swift + C; **Linux** `libhgsdk.so` (x86/x64/ARMv7/ARM64; glibc, musl, bionic); Electron | **Official C# P/Invoke wrapper** (`Hgsdk.Init/OptIn/Start/Stop/IsRunning/Identify/RequestConsent`). No NuGet package | Paid on DAU who become participants, weighted by uptime and geo. "Up to $500 per avg DAU/month" is probably per 1k DAU **[unclear]**. Monthly payout by bank transfer or PayPal. Business KYC, partnership agreement, app review | `Identify()` returns a stable node ID. Dashboard shows active nodes, throughput and earnings. A per-device breakdown via API is **[unverified]** | Explicit "I accept" popup (built-in `RequestConsent()` or text approved by Honeygain). Opt-out must be always available in settings. Manual review before and after launch |
| **Massive** | Alive | **Windows only** in docs (`MassiveClient.dll` C API, x64, plus `MassiveInstaller.exe`). FAQ also lists macOS 10.10+ but no macOS docs exist **[conflict]**. No Linux | P/Invoke to C API. A C# sample app is listed | **$0.05–0.20 per user/month**, monthly via PayPal. Self-serve sign-up | Hourly revenue dashboard. No per-device stats | Mandatory opt-in dialog before `MassiveStart`. Taskbar usage chart. EV signing required. Blocks finance/adult/mail ports, LAN and metadata endpoints |
| **Bright SDK** (Bright Data / EarnApp) | Alive, largest vendor, under 2026 research and press scrutiny | Windows `lum_sdk32/64.dll` (historical). Node/Electron wrapper exists. macOS/Linux embedding **[unverified]** | P/Invoke (Windows) **[unverified docs]** | "Fixed daily rate", "extra $200 CPM". Partner tiers by daily qualified devices, Net 30 / Net 7. Research says per-GB **[conflicting]** | Dashboard. Granularity unknown | Configurable opt-in screen. Bright measures opt-in rate (target 65% or more) and display rate (90% or more). Idle and power conditions apply |
| **Pawns.app SDK** (IPRoyal) | Alive (`business.pawns.app`) | Claims Win/macOS/Linux "subject to approval". Format undisclosed | Unknown | **$0.20/GB fixed** (FAQ); "up to $200 CPM" in marketing | Real-time revenue dashboard | Claims full opt-in. No public docs; onboarding by meeting or form |
| **Infatica SDK** | Alive (`infatica-sdk.io`) | Win/macOS native lib (`.dll`, `.framework`). **No Linux** | P/Invoke | **Up to $0.60 per MAU**. **Requires 50k+ MAU** | Not stated | Disclosure in the EULA plus consent. Infatica reviews a test build |
| **MystNodes SDK** (Mysterium) | Alive. Docker image updated within the last month | Claims Win/macOS/Linux/Android. Public artifact is a Docker container with `--user.token` | Process / Docker only | Undisclosed. Likely MYST/crypto **[unverified]** | mystnodes.com node dashboard | Not documented |
| Castar / PacketSDK / EarnSDK / HexSDK | **IPIDEA, disrupted by Google. Avoid** | — | — | ~$0.6 per US user/month (old marketing) | — | Linked to non-consensual enrolment and botnets |
| Traffmonetizer, Repocket, Proxyrack, PacketStream, EarnFM | Consumer apps and Docker images only. **No public publisher SDK found** **[unverified]** | — | — | — | — | — |
| Peer2Profit | Website offline. Payouts reportedly via a Telegram bot only. **Avoid** | — | — | — | — | — |

## Per-user attribution (Otium's hard requirement)
**None of these vendors publicly documents per-device earnings.** Revenue arrives as a pooled publisher payout, either per DAU/MAU (Honeygain, Massive, Infatica, Bright) or per GB (Pawns). Plan to attribute it in-house:
1. Record opted-in sharing time per user. Poll `IsRunning()` / `MassiveIsStarted` and store minutes, the node ID (`Identify()`), and coarse geo or country.
2. Each month, split the vendor payout by user-minutes, optionally weighted by country tier. This mirrors how per-DAU vendors pay (uptime × geo).
3. Hold users' earnings until the vendor pays you, or show them as "pending". Ask the vendor whether it can provide per-node CSV or API reports keyed by `Identify()` (Honeygain is the most promising for this).

Also confirm contractually that **paying cash to users for opting in** is allowed. Most vendors frame the reward as "premium features or no ads", and cash incentives can look like incentivized or duplicate nodes **[ask vendor]**. Confirm IP deduplication rules as well, for example a user who also runs the vendor's consumer app on the same IP.

## Alternative: running our own residential proxy network
For this, Otium itself becomes the exit node, and we would need a gateway, a backconnect routing layer, billing and customer onboarding. We would also need:
- **KYC of buyers** (identity, use case, sanctions screening, as Massive does).
- **Domain and port blocklists** (banking, government, mail/SMTP, login endpoints).
- **Abuse desk and logging**, to answer law-enforcement and abuse complaints.
- **Per-node rate caps.**

Legal exposure:
- Users' home IPs carry third-party traffic. Talos and Trend Micro observed SQL-injection probing, account-creation fraud and credential stuffing through proxyware exit nodes.
- We would face potential liability under computer-misuse and fraud laws, ISP terms-of-service conflicts for users, and GDPR purpose-limitation questions in the EU.
- Defender and Play-style reputation actions apply here too, as IPIDEA showed: 550+ threat groups used its exit nodes in a single week.

Commercially, a few thousand desktop nodes are not a sellable pool. Buyers expect millions of IPs and geo/ASN targeting. **This is not realistic for a small team.** It is worth revisiting only as a reseller, for example with Massive's Reseller API, and that is the opposite side of the market.

## Recommendation
1. **Honeygain SDK (primary).** It is the only vendor with documented **Windows + macOS + Linux** native libraries and an **official C# P/Invoke wrapper**. It has an explicit consent and opt-out API and a stable `Identify()` node ID for attribution, and it is paid on DAU × uptime, which maps cleanly to per-user uptime accounting. Its 2026 third-party security assessment helps with antivirus and reputation reviews. For Avalonia, ship `hgsdk` natives under `runtimes/{win-x64,win-arm64,osx-arm64,osx-x64,linux-x64,linux-arm64}/native` and load them with `NativeLibrary`.
2. **Massive (secondary, Windows-only add-on or benchmark).** It has the clearest public docs, the strongest published traffic restrictions (blocked categories and ports, no LAN access) and a C API callable from .NET. Its public per-user rate is low ($0.05–0.20/month) and it has no Linux support, so use it for A/B rate comparison on Windows only.

Avoid the IPIDEA-family SDKs and Peer2Profit. Treat Bright SDK as commercially strong but reputationally risky in 2026. Approach Pawns and Infatica only after reading their gated docs; Infatica's 50k MAU minimum likely excludes Otium at launch.

## Sources
- Google GTIG, IPIDEA disruption: https://cloud.google.com/blog/topics/threat-intelligence/disrupting-largest-residential-proxy-network
- Help Net Security (2026-01-29): https://www.helpnetsecurity.com/2026/01/29/ipidea-proxy-network-disrupted/
- Proxyway on IPIDEA-related providers: https://proxyway.com/news/google-disrupts-10-ipidea-related-chinese-proxy-providers
- Bright SDK smart-TV research coverage (2026-06): https://cipherssecurity.com/bright-data-sdk-smart-tv-proxy-network-ai/
- QuoIntelligence (2026-09): https://quointelligence.eu/2026/09/residential-proxies-hide-inside-everyday-apps-users-carry-the-risk/
- Spur, proxy economy (per-GB pay): https://spur.us/blog/hidden-economy-residential-proxies
- Microsoft malware/PUA criteria (updated 2026-09): https://learn.microsoft.com/en-us/defender-xdr/criteria
- Cisco Talos, proxyware abuse: https://blog.talosintelligence.com/proxyware-abuse/
- Trend Micro, proxyware risks: https://trendmicro.com/en_us/research/23/b/hijacking-your-bandwidth-how-proxyware-apps-open-you-up-to-risk.html
- Trend Micro, PUA.Win32.Lutimani: https://www.trendmicro.com/vinfo/ae/threat-encyclopedia/malware/pua.win32.lutimani.sma
- Honeygain SDK: https://sdk.honeygain.com/ · FAQ https://sdk.honeygain.com/faq/ · Docs https://developers.honeygain.com/docs/ (supported-platforms, legal, integration-guides/windows/managed, integration-guides/linux)
- Honeygain security assessment (Jan 2026): https://webassets.honeygain.com/uploads/2026/01/13161127/Blaze-Honeygain-SDK-Security-Assessment-January-2026.pdf
- Massive docs: https://docs.joinmassive.com/monetization-sdk/desktop-windows · FAQ https://www.joinmassive.com/faq · https://joinmassive.com/launch-checklist
- Bright SDK: https://bright-sdk.com/ · https://bright-sdk.com/partners · https://brightdata.com/trustcenter/bright-sdk-ethical-data-practices
- Pawns.app SDK: https://business.pawns.app/sdk/
- Infatica SDK: https://infatica-sdk.io/
- MystNodes SDK: https://hub.docker.com/r/mysteriumnetwork/mystnodes-sdk-client
- Peer2Profit status: https://paidfromsurveys.com/peer2profit-review
