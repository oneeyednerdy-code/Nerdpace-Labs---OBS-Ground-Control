param(
    [string]$Version = "0.7.0-alpha.11",
    [switch]$SkipCatalogRefresh,
    [switch]$SkipInstaller
)

$ErrorActionPreference = "Stop"
$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
Set-Location $RepoRoot

Write-Host "Nerdspace Labs OBS Ground Control $Version" -ForegroundColor Cyan
Write-Host "Windows x64 release build" -ForegroundColor Cyan

if (-not $SkipCatalogRefresh) {
    Write-Host "`n[1/4] Refreshing official OBS plugin catalog..." -ForegroundColor Yellow
    python scripts/refresh-plugin-catalog.py --require-min-resources 250
    if ($LASTEXITCODE -ne 0) { throw "OBS plugin catalog refresh failed." }
} else {
    Write-Host "`n[1/4] Catalog refresh skipped; using the committed embedded catalog." -ForegroundColor Yellow
}

Write-Host "`n[2/4] Setting application version..." -ForegroundColor Yellow
python scripts/set-version.py $Version
if ($LASTEXITCODE -ne 0) { throw "Version update failed." }

Write-Host "`n[3/4] Publishing self-contained .NET 10 win-x64 build..." -ForegroundColor Yellow
$PublishDir = Join-Path $RepoRoot "publish\win-x64"
if (Test-Path $PublishDir) { Remove-Item $PublishDir -Recurse -Force }
dotnet publish src/Nerdspace.OBSRecovery/Nerdspace.OBSRecovery.csproj `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:PublishTrimmed=false `
    -o $PublishDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed." }

$Exe = Join-Path $PublishDir "Nerdspace.OBSRecovery.exe"
if (-not (Test-Path $Exe)) { throw "Published executable not found: $Exe" }

if (-not $SkipInstaller) {
    Write-Host "`n[4/4] Building Windows installer..." -ForegroundColor Yellow
    $iscc = @(
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
        "$env:LOCALAPPDATA\Programs\Inno Setup 7\ISCC.exe",
        "${env:ProgramFiles}\Inno Setup 7\ISCC.exe",
        "${env:ProgramFiles(x86)}\Inno Setup 7\ISCC.exe",
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
    ) | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1

    if (-not $iscc) {
        throw "Inno Setup ISCC.exe was not found. Install it with: winget install --id JRSoftware.InnoSetup -e"
    }

    & $iscc "/DMyAppVersion=$Version" "/DPublishDir=$PublishDir" "installer\GroundControl.iss"
    if ($LASTEXITCODE -ne 0) { throw "Inno Setup failed with exit code $LASTEXITCODE" }
} else {
    Write-Host "`n[4/4] Installer build skipped." -ForegroundColor Yellow
}

Write-Host "`nRelease build complete." -ForegroundColor Green
Write-Host "Published app: $Exe"
if (-not $SkipInstaller) {
    Get-ChildItem "$RepoRoot\dist\Nerdspace-OBS-Ground-Control-Setup-*.exe" -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending | Select-Object -First 1 | ForEach-Object { Write-Host "Installer: $($_.FullName)" }
}
