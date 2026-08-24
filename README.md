# Nerdspace Labs OBS Ground Control

**Nerdspace Labs by OneEyedNerdy**

A Windows-first companion for OBS Studio focused on **pre-flight readiness, bandwidth advice, maintenance, backups, recovery, diagnostics, plugin health, creator hardware, and workstation confidence**.

Current development version: **v0.7.0-alpha.5**

## Supported platform

**Windows 10/11 x64**

Ground Control is intentionally Windows-only for the current development cycle. macOS and Linux are deferred until the Windows recovery, diagnostics, hardware detection, update checks, installer/signing, and OBS integration are mature.

The source keeps a small platform-service boundary so future ports can be added without weakening the Windows implementation.

## What Ground Control does

### Dashboard
- launch OBS
- bring OBS forward
- restart OBS
- launch OBS Safe Mode
- manually force-close OBS
- Recovery Protection
- recovery-loop protection
- local OBS/process health and recovery history

### Pre-Flight
A non-invasive readiness scan that **does not launch OBS by default**.

Checks include:
- OBS installation/process health
- OBS version/update state
- recording drive free space
- NVIDIA/AMD local graphics-driver state
- NVIDIA telemetry when `nvidia-smi` is available
- Elgato Hardware & Software
- SteelSeries GG / Sonar
- Windows main updates
- OBS plugins
- latest OBS log warnings/errors
- missing local scene assets
- backup freshness
- recent OBS crash reports
- optional Bandwidth Advisor

Per-run options:
- skip software update checks
- run Bandwidth Advisor
- launch OBS only after a ready result

### Bandwidth Advisor
Ground Control favors stability over theoretical maximum quality.

It can:
- run several upload samples using generated test data
- accept a manually measured upload result
- use a conservative sustained result instead of the peak
- calculate **stable upload ÷ 4**
- reserve room for audio/protocol overhead
- recommend video bitrate, audio bitrate, resolution, and FPS
- account for low-motion, balanced, and high-motion content
- use Twitch, Twitch Enhanced Broadcasting, YouTube, or generic profiles
- compare the recommendation against the current OBS profile when readable

### Updates
Ground Control separates maintenance from the main OBS controls:
- OBS release check
- NVIDIA installed driver + official NVIDIA driver page
- AMD installed driver + official AMD driver page
- Elgato installed software inventory + connected-hardware state
- SteelSeries GG/Sonar state
- Windows **main updates only**

Ground Control does **not** silently install drivers, firmware, OBS updates, Elgato/SteelSeries software, or Windows updates.

### Plugins
- Windows OBS plugin inventory
- installed version → latest verified release comparison for supported/trusted sources
- exact verified release-page link for updates
- Remind in 1 Week, Skip This Version, and Clear Reminder controls
- newer releases automatically override a skip for an older exact version
- explicit neutral/unknown state when installed version or update source cannot be verified
- local compatibility/load signals from OBS logs
- reversible plugin quarantine when the plugin directory can be safely moved
- no automatic plugin installation

### Backups
- timestamped OBS configuration ZIP backups
- SHA-256 manifests
- secret/browser/cache exclusions
- selective restore
- automatic safety backup before restore
- backup-to-current comparison
- restore refusal while OBS is running

### Diagnostics
- OBS log-pattern analysis
- missing local asset detection
- crash history
- sanitized support report
- Ground Control local logs

## Neutral device absence

Missing vendor hardware is not a failure.

Examples:

- `NVIDIA — Not detected. Check skipped.`
- `AMD — Not detected. Check skipped.`
- `Elgato — No supported hardware or software detected. Check skipped.`
- `SteelSeries Sonar — Not detected. Check skipped.`

Ground Control distinguishes **not present** from **detection failed**.

## Privacy

Ground Control is local-first and has no usage telemetry.

Backups and support reports intentionally exclude OBS service credentials, stream keys, OAuth tokens, browser cookies/cache, private browser-source URLs, chat/message content, USB serial numbers, and raw device IDs.

The optional automatic Bandwidth Advisor sends generated test bytes to Cloudflare's public speed-test upload endpoint. It does not upload OBS scenes, recordings, credentials, chat data, or personal files.

## Branding

The app footer is generated from assembly version metadata:

`Nerdspace Labs by OneEyedNerdy • vMAJOR.MINOR.PATCH`

## Development

Requirements:
- Windows 10 or Windows 11
- .NET 10 SDK
- x64 development target

```powershell
dotnet restore src/Nerdspace.OBSRecovery/Nerdspace.OBSRecovery.csproj
dotnet build src/Nerdspace.OBSRecovery/Nerdspace.OBSRecovery.csproj -c Release
dotnet publish src/Nerdspace.OBSRecovery/Nerdspace.OBSRecovery.csproj -c Release -r win-x64 --self-contained true
```

## GitHub builds

GitHub Actions now builds **Windows x64 only**.

Push a SemVer tag such as:

```powershell
git tag v0.7.0-alpha.5
git push origin v0.7.0-alpha.5
```

The release workflow produces:

- `Nerdspace-OBS-Ground-Control-Setup-vX.Y.Z.exe`
- `Nerdspace-OBS-Ground-Control-vX.Y.Z-win-x64.zip`
- `SHA256SUMS.txt`

The Setup EXE is the recommended alpha download. The portable ZIP remains available for no-install testing. Code signing is the next release-hardening step.

## Windows installer

**No separate .NET runtime is required.** Release builds are published self-contained for Windows x64, so the installer carries the .NET runtime and managed/native dependencies needed by Ground Control. The installer does not download .NET during setup.


Ground Control now includes an Inno Setup project and GitHub release automation for a normal per-user Windows installer. See `docs/INSTALLER.md`.

## Future platforms

macOS and Linux are explicitly deferred, not abandoned. See `docs/FUTURE-PLATFORMS.md`.

## Security

See `PRIVACY.md` and `SECURITY.md`.

## License

A license is intentionally not selected yet. Choose the repository license before accepting outside contributions.
