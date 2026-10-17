#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Builds Otium installers with Velopack.

.DESCRIPTION
    For each runtime: publishes a self-contained build, then packs it with vpk.
      win-x64     -> Otium-win-x64-Setup.exe (per-user install, Start menu + desktop shortcut) and a portable zip
      linux-x64   -> Otium.AppImage
      osx-arm64   -> Otium .app bundle in a .pkg installer and a zip (must run on macOS)
      osx-x64     -> same, for Intel Macs (must run on macOS)

    Windows and Linux packages can be built on any OS; macOS packages need a Mac (Apple's codesign/pkgbuild).
    Output lands in artifacts/releases/<runtime>/, which is also the Velopack update feed for that channel.

    Release settings baked into the app (parameters, or environment variables in CI):
      -ServerUrl     / OTIUM_RELEASE_SERVER_URL    the production Otium server, e.g. https://api.example.com
      -FeeWallet     / OTIUM_RELEASE_FEE_WALLET    fee payout targets, e.g. 'USDT:T...;XMR:4...' (one per pool)
      -UnmineableReferral / OTIUM_RELEASE_UNMINEABLE_REFERRAL  Otium's unMineable referral code (default ztjq-jr7s)
      -HoneygainKey  / OTIUM_RELEASE_HONEYGAIN_KEY Honeygain SDK publisher key (internet sharing)
    Without a server URL the build talks to http://localhost:5080, which is only right for local testing.
    Release notes come from docs/release-notes/<version>.md when that file exists.

    Optional signing (set as environment variables, e.g. CI secrets). See docs/code-signing.md.
      SC_WIN_SIGN_PARAMS          signtool.exe arguments, e.g. '/a /fd sha256 /tr http://timestamp.digicert.com /td sha256'
      SC_WIN_SIGN_TEMPLATE        a custom signing command with {{file}}, e.g. for a cloud-HSM vendor's tool
      SC_WIN_AZURE_SIGN_METADATA  path to an Azure Artifact (Trusted) Signing metadata.json
      SC_APPLE_APP_IDENTITY       "Developer ID Application: Name (TEAMID)"
      SC_APPLE_INSTALL_IDENTITY   "Developer ID Installer: Name (TEAMID)"
      SC_APPLE_NOTARY_PROFILE     keychain profile created with `xcrun notarytool store-credentials`

.EXAMPLE
    pwsh build/package.ps1                          # default runtimes for this OS
    pwsh build/package.ps1 -Runtimes win-x64 -Version 0.2.0
    pwsh build/package.ps1 -Runtimes linux-x64 -Clean      # rebuild the current version
    pwsh build/package.ps1 -Runtimes win-x64 -ServerUrl https://api.example.com
#>
param(
    [string[]] $Runtimes,
    [string] $Version,
    [string] $Configuration = 'Release',
    [string] $ServerUrl = $env:OTIUM_RELEASE_SERVER_URL,
    [string] $FeeWallet = $env:OTIUM_RELEASE_FEE_WALLET,
    [string] $UnmineableReferral = $env:OTIUM_RELEASE_UNMINEABLE_REFERRAL,
    [string] $HoneygainKey = $env:OTIUM_RELEASE_HONEYGAIN_KEY,
    # Delete existing packages for these runtimes first. Needed to rebuild the same version locally;
    # leave it off for real releases so Velopack can build delta updates from the previous version.
    [switch] $Clean
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = Resolve-Path (Join-Path $PSScriptRoot '..')
$app = Join-Path $root 'src/Otium.App/Otium.App.csproj'
$icons = Join-Path $root 'build/icons'
$packId = 'Otium.Desktop'   # also the Windows install folder (%LocalAppData%\Otium.Desktop), kept apart from user data
$title = 'Otium'

if (-not $Version) {
    $props = [xml](Get-Content (Join-Path $root 'Directory.Build.props'))
    $Version = ($props.Project.PropertyGroup | Where-Object { $_.Version } | Select-Object -First 1).Version
}
# `pwsh -File ... -Runtimes a,b` passes one string, so accept comma-separated values too.
$Runtimes = @($Runtimes | ForEach-Object { $_ -split ',' } | ForEach-Object { $_.Trim() } | Where-Object { $_ })
if (-not $Runtimes) {
    $Runtimes = if ($IsMacOS) { 'osx-arm64', 'osx-x64' } elseif ($IsLinux) { 'linux-x64' } else { 'win-x64', 'linux-x64' }
}

function Get-HostOs { if ($IsMacOS) { 'osx' } elseif ($IsLinux) { 'linux' } else { 'win' } }

# Settings baked into the app (read by AppIdentity). Empty ones are left out, so the code defaults apply.
$releaseProps = @()
if ($ServerUrl) { $releaseProps += "-p:OtiumServerUrl=$($ServerUrl.Trim())" }
else { Write-Warning 'No -ServerUrl / OTIUM_RELEASE_SERVER_URL: this build talks to http://localhost:5080 (fine for testing, not for users).' }
# MSBuild splits -p values on ";", so the list of fee targets is passed escaped.
if ($FeeWallet) { $releaseProps += "-p:OtiumFeeWallet=$($FeeWallet.Trim().Replace(';', '%3B'))" }
if ($UnmineableReferral) { $releaseProps += "-p:OtiumUnmineableReferral=$($UnmineableReferral.Trim())" }
if ($HoneygainKey) { $releaseProps += "-p:OtiumHoneygainKey=$($HoneygainKey.Trim())" }
$releaseNotes = Join-Path $root "docs/release-notes/$Version.md"

Push-Location $root
try {
    dotnet tool restore | Out-Null

    foreach ($rid in $Runtimes) {
        $os = $rid.Split('-')[0]
        $publishDir = Join-Path $root "artifacts/publish/$rid"
        $releaseDir = Join-Path $root "artifacts/releases/$rid"
        Write-Host "==> $rid  (version $Version)" -ForegroundColor Cyan

        if ($os -eq 'osx' -and -not $IsMacOS) {
            Write-Warning "Skipping ${rid}: macOS packages can only be built on a Mac (use the release workflow)."
            continue
        }

        if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }
        if ($Clean -and (Test-Path $releaseDir)) { Remove-Item $releaseDir -Recurse -Force }
        dotnet publish $app -c $Configuration -r $rid --self-contained true `
            -p:Version=$Version -p:DebugType=None -p:DebugSymbols=false @releaseProps -o $publishDir
        if ($LASTEXITCODE) { throw "dotnet publish failed for $rid" }

        $mainExe = if ($os -eq 'win') { 'Otium.exe' } else { 'Otium' }
        $vpk = @('vpk')
        if ($os -ne (Get-HostOs)) { $vpk += "[$os]" }   # cross-packaging needs an explicit OS directive
        $packArgs = @(
            'pack',
            '--packId', $packId,
            '--packVersion', $Version,
            '--packDir', $publishDir,
            '--mainExe', $mainExe,
            '--packTitle', $title,
            '--packAuthors', 'Otium',
            '--runtime', $rid,
            '--channel', $rid,
            '--outputDir', $releaseDir
        )
        if (Test-Path $releaseNotes) { $packArgs += '--releaseNotes', $releaseNotes }

        switch ($os) {
            'win' {
                $packArgs += '--icon', (Join-Path $root 'src/Otium.App/Assets/otium.ico')
                $packArgs += '--shortcuts', 'Desktop,StartMenuRoot'
                if ($env:SC_WIN_SIGN_PARAMS) { $packArgs += '--signParams', $env:SC_WIN_SIGN_PARAMS }
                if ($env:SC_WIN_SIGN_TEMPLATE) { $packArgs += '--signTemplate', $env:SC_WIN_SIGN_TEMPLATE }
                if ($env:SC_WIN_AZURE_SIGN_METADATA) { $packArgs += '--azureTrustedSignFile', $env:SC_WIN_AZURE_SIGN_METADATA }
            }
            'linux' {
                $packArgs += '--icon', (Join-Path $icons 'otium.png')
                $packArgs += '--categories', 'Utility'
            }
            'osx' {
                $packArgs += '--icon', (Join-Path $icons 'otium.icns')
                $packArgs += '--bundleId', 'com.otium.app'
                if ($env:SC_APPLE_APP_IDENTITY) { $packArgs += '--signAppIdentity', $env:SC_APPLE_APP_IDENTITY }
                if ($env:SC_APPLE_INSTALL_IDENTITY) { $packArgs += '--signInstallIdentity', $env:SC_APPLE_INSTALL_IDENTITY }
                if ($env:SC_APPLE_NOTARY_PROFILE) { $packArgs += '--notaryProfile', $env:SC_APPLE_NOTARY_PROFILE }
            }
        }

        dotnet @vpk @packArgs
        if ($LASTEXITCODE) { throw "vpk pack failed for $rid" }

        Write-Host "    -> $releaseDir" -ForegroundColor Green
        Get-ChildItem $releaseDir -File | Where-Object { $_.Name -notmatch '\.(json|nupkg)$|^RELEASES' } |
            ForEach-Object { Write-Host ("       {0,-48} {1,8:N1} MB" -f $_.Name, ($_.Length / 1MB)) }
    }
}
finally {
    Pop-Location
}
