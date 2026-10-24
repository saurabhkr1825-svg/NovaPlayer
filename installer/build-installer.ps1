<#
.SYNOPSIS
    Builds the reproducible Release binaries and WiX v5 MSI installer for NovaPlayer.
.DESCRIPTION
    1. Validates .NET 8 SDK and WiX Toolset v5.
    2. Publishes NovaPlayer as a self-contained win-x64 application.
    3. Cleans up development artifacts and redundant architectures.
    4. Compiles the Windows Installer (.msi) package via WiX v5.
#>

[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$Version = "1.0.0"
)

$ErrorActionPreference = "Stop"

$repoRoot = (Get-Item $PSScriptRoot).Parent.FullName
Set-Location $repoRoot

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host " NovaPlayer Packaging & Release Build Engine" -ForegroundColor Cyan
Write-Host " Version: $Version | Runtime: $Runtime | Config: $Configuration" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

# 1. Environment Verification
Write-Host "`n[1/5] Verifying Build Tools..." -ForegroundColor Yellow

$env:DOTNET_ROOT = "$env:USERPROFILE\.dotnet"
$env:PATH = "$env:USERPROFILE\.dotnet;$env:USERPROFILE\.dotnet\tools;$env:PATH"

$dotnetVersion = dotnet --version
Write-Host " - .NET SDK: $dotnetVersion" -ForegroundColor Green

$wixVersion = wix --version
Write-Host " - WiX Toolset: $wixVersion" -ForegroundColor Green

# 2. Prepare Output Directories
Write-Host "`n[2/5] Preparing distribution directories..." -ForegroundColor Yellow
$distDir = Join-Path $repoRoot "dist"
$appDistDir = Join-Path $distDir "app"
$installerDistDir = Join-Path $distDir "installer"

if (Test-Path $distDir) {
    Write-Host " - Cleaning existing dist folder: $distDir"
    Remove-Item $distDir -Recurse -Force -ErrorAction SilentlyContinue
}

New-Item -ItemType Directory -Path $appDistDir -Force | Out-Null
New-Item -ItemType Directory -Path $installerDistDir -Force | Out-Null

# 3. Publish Self-Contained Application
Write-Host "`n[3/5] Publishing self-contained $Runtime application..." -ForegroundColor Yellow
$projectPath = Join-Path $repoRoot "src\Nova.App\Nova.App.csproj"

$publishArgs = @(
    "publish",
    $projectPath,
    "-c", $Configuration,
    "-r", $Runtime,
    "--self-contained", "true",
    "-o", $appDistDir,
    "/p:Version=$Version",
    "/p:AssemblyVersion=$Version.0",
    "/p:FileVersion=$Version.0"
)

& dotnet @publishArgs
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}
Write-Host " - Application published to: $appDistDir" -ForegroundColor Green

# 4. Post-Publish Pruning & Native Verification
Write-Host "`n[4/5] Pruning redundant architectures and verifying native runtime..." -ForegroundColor Yellow

# Remove unneeded x86 native files if present in x64 package
$x86Dir = Join-Path $appDistDir "libvlc\win-x86"
if (Test-Path $x86Dir) {
    Write-Host " - Removing redundant 32-bit LibVLC runtime: $x86Dir"
    Remove-Item $x86Dir -Recurse -Force
}

# Verify x64 LibVLC runtime
$x64Dir = Join-Path $appDistDir "libvlc\win-x64"
$libvlcDll = Join-Path $x64Dir "libvlc.dll"
$libvlccoreDll = Join-Path $x64Dir "libvlccore.dll"
$pluginsDir = Join-Path $x64Dir "plugins"

if (-not (Test-Path $libvlcDll) -or -not (Test-Path $libvlccoreDll) -or -not (Test-Path $pluginsDir)) {
    throw "Missing native LibVLC components in $x64Dir!"
}
$pluginCount = (Get-ChildItem -Recurse $pluginsDir -Filter "*.dll").Count
Write-Host " - Verified native LibVLC runtime: libvlc.dll, libvlccore.dll, and $pluginCount plugins." -ForegroundColor Green

# Remove PDB debug symbols from distribution
$pdbs = Get-ChildItem $appDistDir -Filter "*.pdb"
foreach ($pdb in $pdbs) {
    Remove-Item $pdb.FullName -Force
}
Write-Host " - Removed $($pdbs.Count) debug PDB symbols." -ForegroundColor Green

# Verify Third-Party Licenses file
$licenseTxt = Join-Path $appDistDir "THIRD_PARTY_LICENSES.txt"
if (-not (Test-Path $licenseTxt)) {
    Copy-Item (Join-Path $repoRoot "THIRD_PARTY_LICENSES.txt") $licenseTxt -Force
    Write-Host " - Bundled THIRD_PARTY_LICENSES.txt into app root." -ForegroundColor Green
}

# 5. Build WiX MSI Installer
Write-Host "`n[5/5] Compiling WiX v5 Windows Installer (.msi)..." -ForegroundColor Yellow

$wxsFile = Join-Path $repoRoot "installer\NovaPlayer.wxs"
$msiOut = Join-Path $installerDistDir "NovaPlayer-$Version-x64.msi"

$wixArgs = @(
    "build",
    "-arch", "x64",
    "-ext", "WixToolset.UI.wixext",
    "-ext", "WixToolset.Util.wixext",
    "-d", "PublishDir=$appDistDir",
    "-d", "ResourceDir=$(Join-Path $repoRoot 'src\Nova.App\Resources')",
    $wxsFile,
    "-o", $msiOut
)

Write-Host " - Running: wix $($wixArgs -join ' ')"
& wix @wixArgs
if ($LASTEXITCODE -ne 0) {
    throw "WiX build failed with exit code $LASTEXITCODE"
}

if (-not (Test-Path $msiOut)) {
    throw "Installer output not found at: $msiOut"
}

$msiItem = Get-Item $msiOut
$msiSizeMb = [Math]::Round($msiItem.Length / 1MB, 2)
$hash = (Get-FileHash $msiOut -Algorithm SHA256).Hash

Write-Host "`n==========================================================" -ForegroundColor Green
Write-Host " RELEASE BUILD & PACKAGING COMPLETE!" -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Green
Write-Host " Installer: $msiOut" -ForegroundColor Cyan
Write-Host " File Size: $msiSizeMb MB ($($msiItem.Length) bytes)" -ForegroundColor Cyan
Write-Host " SHA-256:   $hash" -ForegroundColor Cyan
Write-Host "==========================================================`n" -ForegroundColor Green
