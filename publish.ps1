# Builds a self-contained, unpackaged x64 drop of Gauge and zips it for distribution.
#
# Output:
#   dist\app\win-x64\          - the runnable folder (Gauge.exe + runtime + Assets + Gauge.pri)
#   dist\Gauge-win-x64.zip     - that folder + README, ready to hand out
#
# Recipients need nothing installed (the .NET and Windows App SDK runtimes are bundled);
# they just unzip and run Gauge.exe. See packaging\README.txt.
#
# Usage:  pwsh -File publish.ps1

$ErrorActionPreference = 'Stop'
$root    = $PSScriptRoot
$rid     = 'win-x64'
$appDir  = Join-Path $root "dist\app\$rid"
$zipPath = Join-Path $root "dist\Gauge-$rid.zip"

Write-Host "==> Stopping any running Gauge..." -ForegroundColor Cyan
Get-Process Gauge -ErrorAction SilentlyContinue | Stop-Process -Force

Write-Host "==> Cleaning $appDir ..." -ForegroundColor Cyan
if (Test-Path $appDir) { Remove-Item $appDir -Recurse -Force }

Write-Host "==> Publishing (self-contained, $rid)..." -ForegroundColor Cyan
# SelfContained + WindowsAppSDKSelfContained come from the csproj; the CopyAppPriToPublish
# target there copies Gauge.pri into the output (publish drops it for unpackaged WinUI).
dotnet publish (Join-Path $root 'Gauge.csproj') `
    -c Release -r $rid -p:Platform=x64 --self-contained true `
    -o $appDir -v minimal
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed ($LASTEXITCODE)" }

# Fail fast if the resource index is missing — without it the app crashes on launch.
if (-not (Test-Path (Join-Path $appDir 'Gauge.pri'))) {
    throw "Gauge.pri is missing from the publish output; the app would crash at startup."
}

Write-Host "==> Adding README..." -ForegroundColor Cyan
Copy-Item (Join-Path $root 'packaging\README.txt') (Join-Path $appDir 'README.txt') -Force

Write-Host "==> Zipping -> $zipPath ..." -ForegroundColor Cyan
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
Compress-Archive -Path (Join-Path $appDir '*') -DestinationPath $zipPath -CompressionLevel Optimal

$sizeMB = [math]::Round((Get-Item $zipPath).Length / 1MB, 1)
Write-Host "==> Done. $zipPath ($sizeMB MB)" -ForegroundColor Green
