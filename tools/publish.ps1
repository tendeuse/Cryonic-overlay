# Build a release artifact for distribution.
#
#   pwsh tools/publish.ps1
#
# Produces ONE self-contained .exe. A tester downloads it and double-clicks —
# no .NET runtime to install first. That matters more than the file size: "it
# won't start" is almost always a missing runtime, and it is the one failure
# you cannot debug over chat.
#
# Also prints a SHA-256. The binary is UNSIGNED, so Windows SmartScreen will
# warn on it; a published hash at least lets a cautious tester confirm the file
# they downloaded is the file you built. It is not a substitute for signing.

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$proj = Join-Path $root "OverlayMVP\OverlayMVP.csproj"
$out  = Join-Path $root "dist"

if (-not (Test-Path $proj)) { throw "project not found: $proj" }

# Read the version from the csproj so the artifact name cannot disagree with
# what the app reports — the update check compares them, and a mismatch means
# every user is told to update to the build they already have.
[xml]$xml = Get-Content $proj
$version = ($xml.Project.PropertyGroup.Version | Where-Object { $_ }) | Select-Object -First 1
if (-not $version) { throw "no <Version> in $proj" }

Write-Host "Publishing OverlayMVP v$version (self-contained, single file)..." -ForegroundColor Cyan

if (Test-Path $out) { Remove-Item $out -Recurse -Force }

dotnet publish $proj `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    -o $out

if ($LASTEXITCODE -ne 0) { throw "publish failed" }

$exe = Join-Path $out "OverlayMVP.exe"
if (-not (Test-Path $exe)) { throw "expected $exe" }

# Name the artifact after the version. GitHub Releases keeps every upload
# forever, and "OverlayMVP.exe" three times over is unusable as history.
$named = Join-Path $out "CryonicOverlay-v$version-win-x64.exe"
Move-Item $exe $named

$size = [math]::Round((Get-Item $named).Length / 1MB, 1)
$hash = (Get-FileHash $named -Algorithm SHA256).Hash

Write-Host ""
Write-Host "  artifact : $named"       -ForegroundColor Green
Write-Host "  size     : $size MB"     -ForegroundColor Green
Write-Host "  sha256   : $hash"        -ForegroundColor Green
Write-Host ""
Write-Host "Next:" -ForegroundColor Cyan
Write-Host "  1. Create a GitHub release tagged v$version and attach that file."
Write-Host "  2. Panel -> Updates: set version $version and the release's download URL."
Write-Host "  3. Publish the SHA-256 in the release notes."
