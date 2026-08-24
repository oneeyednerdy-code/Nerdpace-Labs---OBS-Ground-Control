param(
    [string]$KeyDirectory = "$env:USERPROFILE\.nerdspace-streamer-mission-control\update-keys",
    [switch]$ConfigureGitHub,
    [string]$Repository = ""
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

$hasPublic = Test-Path $publicPath
$hasPrivate = Test-Path $privatePath

if ($hasPublic -xor $hasPrivate) {
    throw "Only one updater signing key exists in $KeyDirectory. Mission Control will not overwrite or regenerate an incomplete key pair. Restore the matching key from backup before continuing."
}

if ($hasPublic -and $hasPrivate) {
    Write-Host "Existing Mission Control updater signing key pair found."
    Write-Host "Reusing existing keys. They will NOT be regenerated."
}
else {
    Write-Host "No updater signing keys found. Generating a new Ed25519 key pair..."
    netsparkle-generate-appcast --generate-keys --key-path $KeyDirectory
    if ($LASTEXITCODE -ne 0) {
        throw "NetSparkle could not generate the updater signing key pair."
    }
    Write-Host "New updater signing key pair generated."
}

$public = (Get-Content $publicPath -Raw).Trim()
$private = (Get-Content $privatePath -Raw).Trim()

if ([string]::IsNullOrWhiteSpace($public) -or [string]::IsNullOrWhiteSpace($private)) {
    throw "The updater signing key pair exists but one or both files are empty."
}

Write-Host ""
Write-Host "Updater signing key pair is ready."
Write-Host "PUBLIC KEY (safe to store as a GitHub Actions variable):"
Write-Host $public
Write-Host ""
Write-Host "PRIVATE KEY remains at:"
Write-Host $privatePath
Write-Host "Never commit or share the private key."

if ($ConfigureGitHub) {
    if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
        throw "GitHub CLI (gh) is required for -ConfigureGitHub. You can also add NETSPARKLE_PUBLIC_KEY and NETSPARKLE_PRIVATE_KEY manually in GitHub Settings > Secrets and variables > Actions."
    }

    $repoArgs = @()
    if (-not [string]::IsNullOrWhiteSpace($Repository)) {
        $repoArgs = @("--repo", $Repository)
    }

    Write-Host ""
    Write-Host "Configuring GitHub Actions signing values..."

    & gh variable set NETSPARKLE_PUBLIC_KEY @repoArgs --body $public
    if ($LASTEXITCODE -ne 0) {
        throw "GitHub CLI could not set NETSPARKLE_PUBLIC_KEY. Confirm that the authenticated GitHub account has repository ADMIN/write access to Actions variables, or add it manually in GitHub Settings."
    }

    # Pipe the private key through stdin instead of placing it directly in the
    # visible command line.
    $private | & gh secret set NETSPARKLE_PRIVATE_KEY @repoArgs
    if ($LASTEXITCODE -ne 0) {
        throw "GitHub CLI could not set NETSPARKLE_PRIVATE_KEY. Confirm repository permissions, or add it manually in GitHub Settings."
    }

    Write-Host ""
    Write-Host "Configured GitHub Actions variable NETSPARKLE_PUBLIC_KEY and secret NETSPARKLE_PRIVATE_KEY."
}
