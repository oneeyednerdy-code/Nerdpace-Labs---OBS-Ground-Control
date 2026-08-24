# GitHub Publish Guide

This guide takes **Nerdspace Labs OBS Ground Control 0.7.0-alpha.11** from the source folder to a downloadable Windows installer on GitHub.

## What GitHub builds

The repository contains two Windows workflows:

- **Build Windows** runs on pushes to `main`, pull requests, and manual runs.
- **Release Windows** runs when you push a version tag such as `v0.7.0-alpha.11`.

The release workflow builds:

- `Nerdspace-OBS-Ground-Control-Setup-v0.7.0-alpha.11.exe` — recommended installer
- `Nerdspace-OBS-Ground-Control-v0.7.0-alpha.11-win-x64.zip` — portable build
- `SHA256SUMS.txt` — integrity hashes

The Windows application is a self-contained .NET 10 x64 build. Testers do not need to install the .NET SDK or Desktop Runtime.

---

## 1. Extract the source ZIP

Extract the package you received from ChatGPT.

Inside it is:

```text
nerdspace-obs-ground-control
```

**Upload the contents of that folder to GitHub. Do not upload the outer ZIP as the repository.**

The repository root should contain:

```text
.github/
data/
docs/
installer/
scripts/
src/
.gitignore
global.json
README.md
CHANGELOG.md
```

---

## 2. Create the GitHub repository

On GitHub:

1. Choose **New repository**.
2. Recommended repository name:

   `nerdspace-obs-ground-control`

3. Choose Public or Private.
4. Do **not** initialize the new repository with a README, `.gitignore`, or license if you are pushing this prepared source tree.
5. Create the repository.

---

## 3. Push the project from PowerShell

Open PowerShell in the extracted `nerdspace-obs-ground-control` folder.

Confirm you are in the correct directory:

```powershell
Get-ChildItem
```

You should see `src`, `.github`, `installer`, `scripts`, and `README.md`.

Then run:

```powershell
git init
git add .
git commit -m "OBS Ground Control 0.7.0-alpha.11"
git branch -M main
git remote add origin https://github.com/YOUR-GITHUB-NAME/nerdspace-obs-ground-control.git
git push -u origin main
```

Replace `YOUR-GITHUB-NAME` with your GitHub account or organization.

If the repository already has a remote called `origin`, inspect it with:

```powershell
git remote -v
```

and update it if necessary:

```powershell
git remote set-url origin https://github.com/YOUR-GITHUB-NAME/nerdspace-obs-ground-control.git
```

---

## 4. Watch the first Windows build

After `main` is pushed:

1. Open the repository on GitHub.
2. Select **Actions**.
3. Open **Build Windows**.
4. Open the newest run.

The workflow performs:

```text
Checkout source
      ↓
Set up .NET 10
      ↓
Refresh preloaded OBS plugin catalog
      ↓
Restore
      ↓
Build
      ↓
Publish self-contained Windows x64
      ↓
Verify runtime is bundled
      ↓
Create portable ZIP
      ↓
Install Inno Setup on the GitHub runner
      ↓
Build Windows Setup EXE
      ↓
Upload build artifact
```

A successful workflow has a green check mark.

At the bottom of the workflow run, download the artifact:

```text
Nerdspace-OBS-Ground-Control-Windows
```

It should contain the portable ZIP and Setup EXE.

---

## 5. Test the installer before publishing an alpha release

Test the Setup EXE from the successful `main` workflow before creating the public tag.

Recommended alpha checks:

- installer opens normally
- correct Nerdspace Labs branding
- publisher currently shows as unsigned/unknown until code signing is configured
- installs without requiring a separate .NET runtime
- Start Menu shortcut works
- optional Desktop shortcut works
- Ground Control launches
- footer reports `0.7.0-alpha.11`
- OBS is detected
- third-party plugin scanner excludes bundled OBS modules
- plugin update checks work
- Discover displays the preloaded OBS resource catalog
- Pre-Flight does not launch OBS unless explicitly selected
- uninstall entry appears under Windows Installed Apps

Do not treat a successful compiler build as equivalent to a successful application test.

---

## 6. Create the alpha release

After the `main` build has been tested:

```powershell
git tag v0.7.0-alpha.11
git push origin v0.7.0-alpha.11
```

The `Release Windows` workflow starts automatically.

Because the version contains `-alpha`, GitHub will publish it as a **Pre-release**.

---

## 7. What the release workflow does

The tagged release performs:

```text
v0.7.0-alpha.11
      ↓
Refresh full OBS plugin catalog
      ↓
Require at least 250 official OBS resources
      ↓
Set application version from the tag
      ↓
Build .NET 10 app
      ↓
Publish self-contained win-x64
      ↓
Build portable ZIP
      ↓
Install Inno Setup
      ↓
Build Setup EXE
      ↓
Generate SHA-256 checksums
      ↓
Create GitHub Pre-release
```

The GitHub Release should contain:

```text
Nerdspace-OBS-Ground-Control-Setup-v0.7.0-alpha.11.exe
Nerdspace-OBS-Ground-Control-v0.7.0-alpha.11-win-x64.zip
SHA256SUMS.txt
```

---

## 8. Where users download the installer

On the repository:

1. Open **Releases**.
2. Select `v0.7.0-alpha.11`.
3. Under **Assets**, download:

   `Nerdspace-OBS-Ground-Control-Setup-v0.7.0-alpha.11.exe`

The Setup EXE should be the recommended download.

The ZIP is the portable/no-install alternative.

---

## 9. If GitHub Actions does not run

Check:

**Repository → Settings → Actions → General**

Make sure Actions are allowed.

Also confirm these files exist in the repository:

```text
.github/workflows/build.yml
.github/workflows/release.yml
```

YAML filenames can be `.yml` or `.yaml`.

---

## 10. If the plugin catalog refresh fails

The normal `main` build treats the catalog refresh as non-fatal so development can continue with the committed fallback catalog.

The tagged release is stricter. It requires the catalog builder to discover at least 250 official OBS plugin resources.

If the release fails during catalog refresh:

1. Read the workflow error.
2. Check whether the OBS resource directory changed its HTML structure.
3. Run locally:

```powershell
python scripts/refresh-plugin-catalog.py --require-min-resources 250
```

4. Review `src/Nerdspace.OBSRecovery/Data/plugin-catalog.json`.

Do not lower the required count merely to force a release through unless the official OBS catalog itself has materially changed.

---

## 11. If the .NET build fails

Verify your local development environment with:

```powershell
dotnet --version
dotnet --list-sdks
```

The repository targets .NET 10.

For local troubleshooting:

```powershell
dotnet restore src/Nerdspace.OBSRecovery/Nerdspace.OBSRecovery.csproj

dotnet build src/Nerdspace.OBSRecovery/Nerdspace.OBSRecovery.csproj `
  -c Release

dotnet publish src/Nerdspace.OBSRecovery/Nerdspace.OBSRecovery.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:PublishTrimmed=false `
  -o .\publish\win-x64
```

The GitHub runner installs the .NET 10 SDK automatically.

---

## 12. If Inno Setup fails locally

The easiest local install is:

```powershell
winget install --id JRSoftware.InnoSetup -e
```

The included Windows release script searches common Inno Setup locations automatically:

```powershell
.\scripts\build-windows-release.ps1 -Version 0.7.0-alpha.11
```

For GitHub builds, Inno Setup is installed automatically on the GitHub Windows runner.

---

## 13. Build everything locally with one command

After installing:

- .NET 10 SDK
- Python
- Inno Setup

run:

```powershell
.\scripts\build-windows-release.ps1 -Version 0.7.0-alpha.11
```

This performs the catalog refresh, version update, self-contained publish, and installer compilation.

The Setup EXE is written to:

```text
dist\
```

---

## 14. Updating the alpha later

For another alpha:

1. Make your changes.
2. Update/add tests and documentation.
3. Commit and push `main`.
4. Let **Build Windows** pass.
5. Test the generated installer.
6. Create the next SemVer pre-release tag.
7. Push the tag.
8. Verify the GitHub Pre-release.

Example:

```powershell
git add .
git commit -m "Prepare next OBS Ground Control alpha"
git push

git tag v0.7.0-alpha.11
git push origin v0.7.0-alpha.11
```

Never reuse an already-published tag for a different build.

---

## 15. Code signing

The current alpha pipeline can build a normal Setup EXE, but an unsigned installer may still trigger Windows SmartScreen/Unknown Publisher warnings.

Code signing is a separate release-hardening step.

When signing is added, the preferred build order is:

```text
Build app
↓
Sign application EXE
↓
Build installer
↓
Sign Setup EXE
↓
Generate checksums
↓
Publish GitHub Release
```

Do not store a private signing certificate directly in the public repository.
