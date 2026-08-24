# Windows installer

OBS Ground Control ships with two Windows x64 release options:

- **Setup EXE** — recommended for most alpha testers.
- **Portable ZIP** — useful for testing without installation.

## Setup behavior

The installer is built with Inno Setup and installs per-user by default:

`%LOCALAPPDATA%\Programs\Nerdspace Labs\OBS Ground Control`

This design avoids requiring Administrator permission simply to install Ground Control. The application may still request UAC later when a specific OBS recovery operation needs elevated process permissions.

The installer:

- creates a Start Menu shortcut under **Nerdspace Labs**;
- optionally creates a Desktop shortcut;
- registers a normal Windows uninstall entry;
- closes the app before replacing files during an upgrade when Windows permits it;
- preserves Ground Control settings/logs stored outside the installation directory;
- does not install services, kernel drivers, browser extensions, or scheduled tasks.

## Build locally

Requirements:

- Windows 10/11 x64
- .NET 10 SDK
- Inno Setup 6.3+ or 7

First publish the application:

```powershell
dotnet publish src/Nerdspace.OBSRecovery/Nerdspace.OBSRecovery.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishTrimmed=false -o publish/win-x64
```

Then compile the setup package:

```powershell
& "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe" "/DMyAppVersion=0.7.0-alpha.9" "/DPublishDir=$((Resolve-Path 'publish/win-x64').Path)" installer\GroundControl.iss
```

The resulting installer is placed in `dist/`.

## Signing

The alpha installer is structurally ready for Authenticode signing, but code signing is a separate release-hardening step. Signing the app executable and installer should happen after compilation and before publishing the GitHub Release.

## .NET runtime packaging

Ground Control is published as a **self-contained Windows x64 application**. The generated installer includes the runtime and application dependencies emitted by `dotnet publish`.

Users do **not** need to install the .NET Desktop Runtime before running Ground Control, and setup does not download a runtime from the internet. This intentionally makes the installer larger, but it gives alpha testers a predictable install on a clean supported Windows machine.

The release pipeline explicitly publishes with:

```powershell
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishTrimmed=false
```

`PublishTrimmed` is deliberately disabled for the alpha to avoid removing framework/reflection paths used by the desktop UI.
