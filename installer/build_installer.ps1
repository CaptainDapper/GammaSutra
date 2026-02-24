# Gamma Sutra — Build Installer
# Publishes the app then compiles setup.iss with Inno Setup 6.
# Requires Inno Setup 6: https://jrsoftware.org/isinfo.php

$ErrorActionPreference = "Stop"

$iscc      = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
$proj      = Join-Path $PSScriptRoot "..\GammaControl.csproj"
$publishDir = Join-Path $PSScriptRoot "..\publish"
$iss       = Join-Path $PSScriptRoot "setup.iss"

Write-Host ""
Write-Host "  Gamma Sutra — Build Installer" -ForegroundColor Cyan
Write-Host "  ───────────────────────────────" -ForegroundColor DarkCyan
Write-Host ""

# ── Publish ──────────────────────────────────────────────────────────────────

Write-Host "  Publishing..." -ForegroundColor Yellow

dotnet publish $proj `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -o $publishDir `
    --nologo -v quiet

if ($LASTEXITCODE -ne 0) {
    Write-Host "  dotnet publish failed." -ForegroundColor Red; exit 1
}

Write-Host "  Published to: $publishDir" -ForegroundColor Green

# ── Compile installer ────────────────────────────────────────────────────────

if (-not (Test-Path $iscc)) {
    Write-Host ""
    Write-Host "  Inno Setup not found at: $iscc" -ForegroundColor Red
    Write-Host "  Download from https://jrsoftware.org/isinfo.php" -ForegroundColor Yellow
    exit 1
}

Write-Host "  Compiling installer..." -ForegroundColor Yellow
& $iscc $iss

if ($LASTEXITCODE -ne 0) {
    Write-Host "  ISCC failed." -ForegroundColor Red; exit 1
}

Write-Host ""
Write-Host "  Done!  installer\GammaSutraSetup.exe is ready." -ForegroundColor Cyan
Write-Host ""
