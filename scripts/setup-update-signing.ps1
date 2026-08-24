param(
    [string]$KeyDirectory = "$env:USERPROFILE\.nerdspace-streamer-mission-control\update-keys",
    [switch]$ConfigureGitHub
)

$ErrorActionPreference = "Stop"

Write-Host "Streamer Mission Control secure updater setup"
Write-Host ""
Write-Host "Mission Control uses NetSparkle core 3.1.0 and AppCastGenerator CLI 2.9.0."
Write-Host "GitHub Actions installs .NET 9 automatically for release signing."
Write-Host ""

$tool = Get-Command netsparkle-generate-appcast -ErrorAction SilentlyContinue
if ($tool) {
    Write-Host "Found existing netsparkle-generate-appcast tool."
}
if (-not $tool) {
    Write-Host "Installing NetSparkle AppCastGenerator 2.9.0..."
    dotnet tool install --global NetSparkleUpdater.Tools.AppCastGenerator --version 2.9.0
    if ($LASTEXITCODE -ne 0) {
        throw "Could not install NetSparkleUpdater.Tools.AppCastGenerator 2.9.0 from NuGet. This is a tool-package install failure, not proof that your .NET runtime is missing."
    }

    # Refresh PATH lookup after dotnet installs the global tool.
    $dotnetTools = Join-Path $env:USERPROFILE ".dotnet\tools"
    if ($env:PATH -notlike "*$dotnetTools*") {
        $env:PATH = "$env:PATH;$dotnetTools"
    }
    $tool = Get-Command netsparkle-generate-appcast -ErrorAction SilentlyContinue
}

if (-not $tool) {
    throw "AppCastGenerator 2.9.0 was installed but netsparkle-generate-appcast is still not on PATH. Reopen PowerShell or add $env:USERPROFILE\.dotnet\tools to PATH, then run this script again."
}

New-Item -ItemType Directory -Force -Path $KeyDirectory | Out-Null

$publicPath = Join-Path $KeyDirectory "NetSparkle_Ed25519.pub"
$privatePath = Join-Path $KeyDirectory "NetSparkle_Ed25519.priv"

if ((Test-Path $publicPath) -or (Test-Path $privatePath)) {
    throw "A key already exists in $KeyDirectory. Mission Control will not overwrite updater signing keys."
}

netsparkle-generate-appcast --generate-keys --key-path $KeyDirectory

$public = (Get-Content $publicPath -Raw).Trim()
$private = (Get-Content $privatePath -Raw).Trim()

Write-Host ""
Write-Host "Keys generated."
Write-Host "PUBLIC KEY (safe to store as a GitHub Actions variable):"
Write-Host $public
Write-Host ""
Write-Host "PRIVATE KEY saved locally at:"
Write-Host $privatePath
Write-Host "Never commit or share the private key."

if ($ConfigureGitHub) {
    if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
        throw "GitHub CLI (gh) is required for -ConfigureGitHub."
    }

    gh variable set NETSPARKLE_PUBLIC_KEY --body $public
    gh secret set NETSPARKLE_PRIVATE_KEY --body $private
    Write-Host ""
    Write-Host "Configured GitHub Actions variable NETSPARKLE_PUBLIC_KEY and secret NETSPARKLE_PRIVATE_KEY."
}
