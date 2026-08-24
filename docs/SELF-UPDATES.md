# Streamer Mission Control Self-Updates

Version: 0.8.0-alpha.6

Streamer Mission Control uses **NetSparkleUpdater.SparkleUpdater 3.1.0** with Mission Control's own Avalonia UI.

The runtime library and signing CLI have different version lines: the app uses core **3.1.0**, while the currently published `NetSparkleUpdater.Tools.AppCastGenerator` CLI is **2.9.0**.

The application does not blindly execute a GitHub download. The update feed and installer are verified with **Ed25519 signatures** before installation.

## User experience

The Update Center contains a Streamer Mission Control card with:

- installed version
- latest signed version
- Stable or Preview channel
- Check Now
- Update Now
- Later
- View Release Notes / View Releases
- animated download/progress state

Automatic checks are enabled by default and run at most once every 24 hours. Automatic checking never automatically installs an update.

## Update channels

**Stable**

Normal SemVer releases such as:

`v0.9.0`

**Preview**

Alpha/beta builds such as:

`v0.8.0-alpha.6`

A stable release refreshes both the Stable and Preview feeds so preview testers can move naturally onto a stable build.

## GitHub architecture

Normal versioned releases continue to contain:

- Windows Setup EXE
- portable ZIP
- SHA256SUMS.txt

GitHub also maintains a fixed prerelease/tag named:

`update-feed`

It contains:

- `appcast-stable.xml`
- `appcast-stable.xml.signature`
- `appcast-preview.xml`
- `appcast-preview.xml.signature`

These are machine-readable metadata files. Users should still download versioned releases normally.

## One-time signing setup

Generate an Ed25519 key pair:

```powershell
.\scripts\setup-update-signing.ps1
```

Or, when GitHub CLI is authenticated for the repository:

```powershell
.\scripts\setup-update-signing.ps1 -ConfigureGitHub
```

The GitHub repository needs:

**Actions variable**

`NETSPARKLE_PUBLIC_KEY`

**Actions secret**

`NETSPARKLE_PRIVATE_KEY`

The public key is safe to expose. The private key must never be committed.

If the keys are not configured:

- normal GitHub releases still build
- Mission Control's signed in-app updater reports that self-update signing is not configured
- Update Now stays disabled
- View Releases remains available

This is intentional fail-safe behavior.

## Release flow

For a tag such as:

```powershell
git tag v0.8.0-alpha.6
git push origin v0.8.0-alpha.6
```

GitHub:

1. injects the repository and public key into `update-config.json`
2. builds/tests/publishes Mission Control
3. builds the Inno Setup installer
4. publishes the normal GitHub release
5. signs the installer/appcast with the private Ed25519 key
6. refreshes the fixed `update-feed` release
7. existing Mission Control installations can detect the new signed release

## Update Now flow

```text
Check signed appcast
        ↓
Newer compatible version found
        ↓
User chooses Update Now
        ↓
Download installer
        ↓
Verify Ed25519 signature
        ↓
Save Mission Control settings
        ↓
Fully close Mission Control
        ↓
Run Inno Setup upgrade
```

The existing Inno Setup AppId is unchanged so the updater upgrades the current installation instead of creating an unrelated second application.

## Why only the NetSparkle core package?

Mission Control uses its own NerdSpace Labs UI instead of NetSparkle's built-in Avalonia UI package. This keeps the update interface visually consistent with the rest of Mission Control and avoids tying the app's UI to a different Avalonia major version.


### NuGet says AppCastGenerator 3.1.0 does not exist

That is expected: `NetSparkleUpdater.SparkleUpdater` and
`NetSparkleUpdater.Tools.AppCastGenerator` do not currently share the same
version number.

Use:

```powershell
dotnet tool install --global NetSparkleUpdater.Tools.AppCastGenerator --version 2.9.0
```

Then verify:

```powershell
netsparkle-generate-appcast --help
```

The application can still use `NetSparkleUpdater.SparkleUpdater` 3.1.0.
