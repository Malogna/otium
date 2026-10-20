# Draws the link-preview pictures for the website's mining pages (Site/previews/*.jpg), which social sites and chat
# apps show when someone shares otium.lol/mining/rtx-4090. Run it again whenever the graphics card list changes.
#
#   1. Start the server in development:  dotnet run --project src/Otium.Server --launch-profile http
#   2. Then, from the repository root:   pwsh tools/render-site-previews.ps1
#
# Each picture is a screenshot, by headless Edge or Chrome, of the server's development-only /dev/preview/... page
# (SitePages.PreviewTemplate), saved as a JPEG so the 30-odd pictures stay small. Windows only (System.Drawing).
param(
    [string]$BaseUrl = "http://localhost:5080",
    [int]$Quality = 88
)
$ErrorActionPreference = "Stop"

$browser = @(
    "${env:ProgramFiles(x86)}\Microsoft\Edge\Application\msedge.exe",
    "$env:ProgramFiles\Microsoft\Edge\Application\msedge.exe",
    "$env:ProgramFiles\Google\Chrome\Application\chrome.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $browser) { throw "Neither Edge nor Chrome is installed." }

$output = Join-Path $PSScriptRoot "..\src\Otium.Server\Site\previews"
New-Item -ItemType Directory -Force $output | Out-Null
$scratch = Join-Path ([IO.Path]::GetTempPath()) "otium-previews-$([guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory $scratch | Out-Null

Add-Type -AssemblyName System.Drawing
$jpeg = [Drawing.Imaging.ImageCodecInfo]::GetImageEncoders() | Where-Object MimeType -eq "image/jpeg"
$settings = [Drawing.Imaging.EncoderParameters]::new(1)
$settings.Param[0] = [Drawing.Imaging.EncoderParameter]::new([Drawing.Imaging.Encoder]::Quality, [long]$Quality)

# Every mining page in the sitemap: /mining itself and one per graphics card.
[xml]$sitemap = (Invoke-WebRequest "$BaseUrl/sitemap.xml" -UseBasicParsing).Content
$paths = $sitemap.urlset.url.loc | ForEach-Object { ([Uri]$_).AbsolutePath } | Where-Object { $_ -like "/mining*" }

try {
    foreach ($path in $paths) {
        $name = if ($path -eq "/mining") { "mining" } else { $path.Split("/")[-1] }
        $png = Join-Path $scratch "$name.png"
        Start-Process $browser -Wait -WindowStyle Hidden -ArgumentList @(
            "--headless=new", "--disable-gpu", "--hide-scrollbars", "--force-device-scale-factor=1",
            "--virtual-time-budget=3000", "--window-size=1200,630", "--screenshot=$png", "$BaseUrl/dev/preview$path")
        $image = [Drawing.Image]::FromFile($png)
        try { $image.Save((Join-Path $output "$name.jpg"), $jpeg, $settings) } finally { $image.Dispose() }
        Write-Host "previews/$name.jpg"
    }
}
finally {
    Remove-Item -Recurse -Force $scratch
}
