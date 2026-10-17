#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Runs the whole Otium loop locally: simulated pool + server + app, with no real mining and no real money.

.DESCRIPTION
    Starts
      - Otium.DevPool on http://localhost:5090: a stand-in for the MoneroOcean, unMineable, Kryptex and WhatToMine APIs
        (earnings sped up by -Speedup)
      - Otium.Server  on http://localhost:5080: non-custodial like production (accounts and referrals only), or with
        -Custodial: balances with dev settings (credits every 20 s, 1-minute hold, fake payouts)
      - the Otium app with simulated CPU and GPU miners (one window per -Computers)
    and prints email sign-in codes here, because no real email is sent in dev mode.
    Everything stops when you close the app window(s) or press Ctrl+C. Data lives in artifacts/dev (use -Reset to start over).

.EXAMPLE
    pwsh build/dev.ps1                 # one simulated computer
    pwsh build/dev.ps1 -Computers 2    # two computers, to try signing in on a second PC
    pwsh build/dev.ps1 -Reset          # wipe dev data first
    pwsh build/dev.ps1 -Custodial      # the server holds balances (cash-outs, Kryptex, internet sharing)
    pwsh build/dev.ps1 -RealMiners     # real XMRig/SRBMiner on this PC, real MoneroOcean (see below)
    pwsh build/dev.ps1 -RealMiners -Wallet <xmr> -KryptexAccount krxABC123   # also Pearl/Quantus on real Kryptex

.NOTES
    -RealMiners tests the real engines on this hardware: the app downloads XMRig and SRBMiner and mines on the real
    MoneroOcean pool, crediting the dev pool wallet (the Monero general fund, so nobody is paid). The GPU mines
    MoneroOcean's algorithms even though they usually lose money, so they can be tested without a Kryptex account.
    Data lives in artifacts/dev-real; internet sharing is off. Stop it after a few minutes: this uses real electricity.

    -KryptexAccount (with -RealMiners) routes Pearl and Quantus to that real Kryptex Pool account, priced by the live
    WhatToMine feed, as the production server does. MoneroOcean's GPU algorithms then keep their normal tiers.
#>
param(
    [ValidateRange(1, 4)] [int] $Computers = 1,
    [double] $Speedup = 3000,
    [switch] $Reset,
    [switch] $NoBuild,
    [switch] $RealMiners,
    # Run the server with custody on (balances, cash-outs, Kryptex, internet sharing) instead of non-custodial.
    [switch] $Custodial,
    # Route every balance account's CPU to the simulated Kryptex Monero pool instead of MoneroOcean.
    [switch] $KryptexCpu,
    # With -RealMiners: the Monero address the real pool credits (MoneroOcean refuses the dev default).
    [string] $Wallet,
    # With -RealMiners: a Kryptex Pool account (e.g. krxABC123) to mine Pearl/Quantus into, instead of skipping them.
    [string] $KryptexAccount
)

$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..')
$dev = Join-Path $root ($RealMiners ? 'artifacts/dev-real' : 'artifacts/dev')
$poolUrl = 'http://localhost:5090'
$serverUrl = 'http://localhost:5080'
# Any valid mainnet address works here: the simulated pool never pays anyone. (Monero project's general fund.)
$devPoolWallet = '44AFFq5kSiGBoZ4NMDwYtN18obc8AemS33DBLWs3H7otXft3XjrpDtQGv7SqSsaBYBb98uNbr2VBBEt7f2wfn3RVGQBEP3A'
if ($RealMiners) {
    if (-not $Wallet) { throw 'With -RealMiners, pass -Wallet <your Monero address>: MoneroOcean refuses the dev default (the Monero general fund).' }
    $devPoolWallet = $Wallet.Trim()
}

if ($Reset -and (Test-Path $dev)) { Remove-Item $dev -Recurse -Force }
New-Item -ItemType Directory -Force $dev | Out-Null

if (-not $NoBuild) {
    Write-Host 'Building…' -ForegroundColor Cyan
    dotnet build (Join-Path $root 'Otium.slnx') -nologo -v q
    if ($LASTEXITCODE) { throw 'Build failed' }
}

$saved = @{}
function Set-DevEnv([hashtable] $vars) {
    foreach ($k in $vars.Keys) {
        if (-not $saved.ContainsKey($k)) { $saved[$k] = [Environment]::GetEnvironmentVariable($k) }
        [Environment]::SetEnvironmentVariable($k, [string]$vars[$k])
    }
}

function Start-Background([string] $name, [string] $project, [string[]] $arguments) {
    $log = Join-Path $dev "$name.log"
    $dll = Get-ChildItem (Join-Path $root "src/$project/bin/Debug") -Recurse -Filter "$project.dll" | Select-Object -First 1
    Start-Process dotnet -ArgumentList (@($dll.FullName) + $arguments) -WorkingDirectory (Join-Path $root "src/$project") `
        -RedirectStandardOutput $log -RedirectStandardError "$log.err" -WindowStyle Hidden -PassThru
}

function Wait-Healthy([string] $url, [string] $name) {
    for ($i = 0; $i -lt 60; $i++) {
        try { Invoke-RestMethod $url -TimeoutSec 2 | Out-Null; return } catch { Start-Sleep -Milliseconds 500 }
    }
    throw "$name didn't start; see artifacts/dev/$name.log"
}

# The app's Otium.exe launcher finds .NET through DOTNET_ROOT, not PATH, so point it at the SDK in use
# (needed when .NET is installed per-user, e.g. under %LOCALAPPDATA%\Microsoft\dotnet).
if (-not $env:DOTNET_ROOT) {
    $dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($dotnet) { Set-DevEnv @{ DOTNET_ROOT = Split-Path (Resolve-Path $dotnet.Source) } }
}

$processes = @()
try {
    # Simulated pool (not used with real miners, which talk to the real MoneroOcean)
    if (-not $RealMiners) {
        $processes += Start-Background 'devpool' 'Otium.DevPool' @('--Speedup', $Speedup, '--Urls', $poolUrl)
        Wait-Healthy "$poolUrl/network/stats" 'devpool'
    }

    # Server with fast dev timings and fake payouts
    Set-DevEnv @{
        ASPNETCORE_ENVIRONMENT                = 'Development'
        ASPNETCORE_URLS                       = $serverUrl
        OTIUM_POOL_API                        = $poolUrl
        OTIUM_UNMINEABLE_API                  = "$poolUrl/unmineable"
        Custody__Enabled                      = $Custodial ? 'true' : 'false'
        Unmineable__OtiumTarget               = 'USDT:TB2xNFAjGarYPykmt1dZQz8hJb2Rhry4wJ'
        ConnectionStrings__Otium              = "Data Source=$(Join-Path $dev 'otium.db')"
        Earnings__PoolWallets__0              = $devPoolWallet
        Earnings__HoldPeriod                  = '00:01:00'
        Earnings__CreditInterval              = '00:00:20'
        Cashouts__ProcessInterval             = '00:00:05'
        Cashouts__MinAccountAgeForAuto        = '00:00:00'
        DevMode__Enabled                      = 'true'
        DevMode__FakePayouts                  = 'true'
        Admin__Token                          = 'dev'
        # Internet sharing with a simulated partner, paid at rates sped up like mining.
        Bandwidth__Enabled                    = 'true'
        Bandwidth__PartnerName                = 'A simulated partner'
        Bandwidth__UsdPerGb                   = [string](0.10 * $Speedup)
        Bandwidth__UsdPerSharingHour          = [string](0.0006 * $Speedup)
        # GPU coins on a simulated Kryptex Pool, priced by a simulated WhatToMine feed.
        Kryptex__Enabled                      = 'true'
        Kryptex__Account                      = 'otium-dev'
        Kryptex__ApiBase                      = "$poolUrl/kryptex/"
        Kryptex__WhatToMineBase               = "$poolUrl/wtm/"
        Kryptex__CreditInterval               = '00:00:20'
        GpuPolicy__RefreshInterval            = '00:01:00'
    }
    if ($KryptexCpu) { Set-DevEnv @{ CpuMining__KryptexPercent = '100' } }
    if ($RealMiners) {
        Set-DevEnv @{
            OTIUM_POOL_API                    = ''        # the real MoneroOcean
            OTIUM_UNMINEABLE_API              = ''        # the real unMineable
            Bandwidth__Enabled                = 'false'
        }
        if ($KryptexAccount) {
            # Pearl/Quantus on the real Kryptex Pool and WhatToMine, as in production.
            Set-DevEnv @{
                Kryptex__Enabled              = 'true'
                Kryptex__Account              = $KryptexAccount.Trim()
                Kryptex__ApiBase              = 'https://pool.kryptex.com/'
                Kryptex__WhatToMineBase       = 'https://whattomine.com/'
                Kryptex__CreditInterval       = '00:10:00'
            }
        }
        else {
            Set-DevEnv @{
                Kryptex__Enabled              = 'false'
                # Let the GPU mine MoneroOcean's algorithms despite electricity, to exercise SRBMiner.
                GpuPolicy__Tiers__kawpow      = 'primary'
                GpuPolicy__Tiers__autolykos2  = 'primary'
                GpuPolicy__Tiers__etchash     = 'primary'
            }
        }
    }
    $processes += Start-Background 'server' 'Otium.Server' @()
    Wait-Healthy "$serverUrl/healthz" 'server'

    # App window(s), each a separate simulated computer with its own data folder
    $exe = Join-Path $root 'src/Otium.App/bin/Debug/net10.0/Otium.exe'
    if (-not (Test-Path $exe)) { $exe = Join-Path $root 'src/Otium.App/bin/Debug/net10.0/Otium' }
    $apps = @()
    for ($n = 1; $n -le $Computers; $n++) {
        $home_ = Join-Path $dev "pc$n"
        New-Item -ItemType Directory -Force $home_ | Out-Null
        $settings = Join-Path $home_ 'settings.json'
        # Mine even while you're using the PC, so you can watch it work.
        if (-not (Test-Path $settings)) { '{ "OnlyWhenIdle": false }' | Set-Content $settings }
        Set-DevEnv @{
            OTIUM_HOME         = $home_
            OTIUM_SERVER_URL   = $serverUrl
            OTIUM_POOL_API     = $RealMiners ? '' : $poolUrl
            OTIUM_UNMINEABLE_API = $RealMiners ? '' : "$poolUrl/unmineable"
            OTIUM_SIMULATE     = $RealMiners ? '' : '1'
            OTIUM_DEVICE_SEED  = "dev-pc$n"
        }
        # Errors (e.g. a crash's stack trace) go to artifacts/dev/pcN.log.
        $apps += Start-Process $exe -PassThru -RedirectStandardError (Join-Path $dev "pc$n.log") -RedirectStandardOutput (Join-Path $dev "pc$n.out.log")
    }

    Write-Host ''
    Write-Host "Otium dev mode is running ($Computers computer(s), earnings ×$Speedup)." -ForegroundColor Green
    Write-Host "  Simulated pool : $poolUrl"
    Write-Host "  Server         : $serverUrl   (admin token: dev, e.g. GET /admin/summary)"
    Write-Host "  Logs and data  : $dev"
    Write-Host '  Sign-in codes appear below. Close the app window(s) or press Ctrl+C to stop everything.'
    Write-Host ''

    $seen = @{}
    while ($apps | Where-Object { -not $_.HasExited }) {
        try {
            foreach ($mail in @(Invoke-RestMethod "$serverUrl/dev/outbox" -TimeoutSec 2) | Where-Object { $_ -and $_.to }) {
                $key = "$($mail.to)|$($mail.sentAt)"
                if ($seen.ContainsKey($key)) { continue }
                $seen[$key] = $true
                if ($mail.subject -like 'Your Otium code*') {
                    $code = [regex]::Match($mail.body, '\b\d{6}\b').Value
                    Write-Host ("  ✉  Sign-in code for {0}: {1}" -f $mail.to, $code) -ForegroundColor Yellow
                } else {
                    Write-Host ("  ✉  To {0}: {1}" -f $mail.to, $mail.subject) -ForegroundColor Yellow
                }
            }
        } catch { }
        Start-Sleep -Seconds 2
    }
}
finally {
    foreach ($p in $processes + $apps) { if ($p -and -not $p.HasExited) { Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue } }
    foreach ($k in $saved.Keys) { [Environment]::SetEnvironmentVariable($k, $saved[$k]) }
    Write-Host 'Dev mode stopped.' -ForegroundColor Cyan
}
