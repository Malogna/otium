#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Runs the whole Otium loop locally: simulated pool + server + app, with no real mining and no real money.

.DESCRIPTION
    Starts
      - Otium.DevPool on http://localhost:5090: a stand-in for the MoneroOcean API (earnings sped up by -Speedup)
      - Otium.Server  on http://localhost:5080: dev settings (credits every 20 s, 1-minute hold, fake PayPal/Monero payouts)
      - the Otium app with simulated CPU and GPU miners (one window per -Computers)
    and prints email sign-in codes here, because no real email is sent in dev mode.
    Everything stops when you close the app window(s) or press Ctrl+C. Data lives in artifacts/dev (use -Reset to start over).

.EXAMPLE
    pwsh build/dev.ps1                 # one simulated computer
    pwsh build/dev.ps1 -Computers 2    # two computers, to try signing in on a second PC
    pwsh build/dev.ps1 -Reset          # wipe dev data first
#>
param(
    [ValidateRange(1, 4)] [int] $Computers = 1,
    [double] $Speedup = 3000,
    [switch] $Reset,
    [switch] $NoBuild
)

$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..')
$dev = Join-Path $root 'artifacts/dev'
$poolUrl = 'http://localhost:5090'
$serverUrl = 'http://localhost:5080'
# Any valid mainnet address works here: the simulated pool never pays anyone. (Monero project's general fund.)
$devPoolWallet = '44AFFq5kSiGBoZ4NMDwYtN18obc8AemS33DBLWs3H7otXft3XjrpDtQGv7SqSsaBYBb98uNbr2VBBEt7f2wfn3RVGQBEP3A'

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

$processes = @()
try {
    # Simulated pool
    $processes += Start-Background 'devpool' 'Otium.DevPool' @('--Speedup', $Speedup, '--Urls', $poolUrl)
    Wait-Healthy "$poolUrl/network/stats" 'devpool'

    # Server with fast dev timings and fake payouts
    Set-DevEnv @{
        ASPNETCORE_ENVIRONMENT                = 'Development'
        ASPNETCORE_URLS                       = $serverUrl
        OTIUM_POOL_API                        = $poolUrl
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
            OTIUM_POOL_API     = $poolUrl
            OTIUM_SIMULATE     = '1'
            OTIUM_DEVICE_SEED  = "dev-pc$n"
        }
        # Errors (e.g. a crash's stack trace) go to artifacts/dev/pcN.log.
        $apps += Start-Process $exe -PassThru -RedirectStandardError (Join-Path $dev "pc$n.log") -RedirectStandardOutput (Join-Path $dev "pc$n.out.log")
    }

    Write-Host ''
    Write-Host "Otium dev mode is running ($Computers computer(s), earnings ×$Speedup)." -ForegroundColor Green
    Write-Host "  Simulated pool : $poolUrl"
    Write-Host "  Server         : $serverUrl   (admin token: dev, e.g. GET /admin/summary)"
    Write-Host "  Logs and data  : artifacts/dev"
    Write-Host '  Sign-in codes appear below. Close the app window(s) or press Ctrl+C to stop everything.'
    Write-Host ''

    $seen = @{}
    while ($apps | Where-Object { -not $_.HasExited }) {
        try {
            foreach ($mail in @(Invoke-RestMethod "$serverUrl/dev/outbox" -TimeoutSec 2) | Where-Object { $_ -and $_.to }) {
                $key = "$($mail.to)|$($mail.sentAt)"
                if ($seen.ContainsKey($key)) { continue }
                $seen[$key] = $true
                $code = [regex]::Match($mail.body, '\b\d{6}\b').Value
                Write-Host ("  ✉  Sign-in code for {0}: {1}" -f $mail.to, $code) -ForegroundColor Yellow
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
