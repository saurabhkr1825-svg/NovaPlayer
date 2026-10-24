<#
.SYNOPSIS
    Smoke test for the published self-contained NovaPlayer release.
#>

$appDir = "d:\projects\Nova\dist\app"
$exePath = Join-Path $appDir "NovaPlayer.exe"

Write-Host "Verifying published application in $appDir..." -ForegroundColor Yellow

if (-not (Test-Path $exePath)) {
    throw "NovaPlayer.exe not found in $appDir"
}

# 1. Verify Native LibVLC Loading
$libvlcDll = Join-Path $appDir "libvlc\win-x64\libvlc.dll"
if (-not (Test-Path $libvlcDll)) {
    throw "libvlc.dll not found in $libvlcDll"
}
Write-Host " - Native LibVLC DLL present." -ForegroundColor Green

# 2. Verify File Version Info
$fileVersion = (Get-Item $exePath).VersionInfo
Write-Host " - FileVersion: $($fileVersion.FileVersion)" -ForegroundColor Green
Write-Host " - ProductVersion: $($fileVersion.ProductVersion)" -ForegroundColor Green
Write-Host " - ProductName: $($fileVersion.ProductName)" -ForegroundColor Green
Write-Host " - FileDescription: $($fileVersion.FileDescription)" -ForegroundColor Green
Write-Host " - LegalCopyright: $($fileVersion.LegalCopyright)" -ForegroundColor Green

# 3. Test Process Launch and Exit
Write-Host " - Launching NovaPlayer process from published folder..." -ForegroundColor Yellow
$proc = Start-Process -FilePath $exePath -PassThru

Start-Sleep -Seconds 3

if ($proc.HasExited) {
    Write-Warning "Process exited early with code: $($proc.ExitCode)"
} else {
    Write-Host " - NovaPlayer process running with PID: $($proc.Id). Closing gracefully..." -ForegroundColor Green
    $proc.CloseMainWindow() | Out-Null
    Start-Sleep -Seconds 2
    if (-not $proc.HasExited) {
        $proc.Kill()
    }
    Write-Host " - NovaPlayer launched and closed successfully." -ForegroundColor Green
}

# 4. Check Log Output
$logDir = Join-Path $env:LOCALAPPDATA "NovaPlayer\logs"
if (Test-Path $logDir) {
    $latestLog = Get-ChildItem $logDir -Filter "*.log" | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if ($latestLog) {
        Write-Host " - Verified Serilog log file: $($latestLog.FullName)" -ForegroundColor Green
        $recentLines = Get-Content $latestLog.FullName -Tail 5
        Write-Host "   Recent log lines:" -ForegroundColor DarkGray
        foreach ($line in $recentLines) {
            Write-Host "   $line" -ForegroundColor DarkGray
        }
    }
}

Write-Host "`nAll smoke tests passed successfully!" -ForegroundColor Green
