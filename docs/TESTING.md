# v0.7.0-alpha.5 Windows Alpha Testing Checklist

## Build / release
- [ ] Windows x64 GitHub Actions build succeeds.
- [ ] No Linux or macOS jobs are present in build/release workflows.
- [ ] Footer shows `Nerdspace Labs by OneEyedNerdy • v0.7.0-alpha.5` for the branch build.
- [ ] Tagged release uses the tag version in the footer/app metadata.
- [ ] Release ZIP contains the self-contained Windows app.
- [ ] SHA256SUMS.txt matches the release ZIP.

## Windows compatibility
- [ ] Launch on Windows 10 x64.
- [ ] Launch on Windows 11 x64.
- [ ] Start-at-login integration works.
- [ ] Open Folder actions use Explorer.
- [ ] No runtime paths or UI copy refer to macOS/Linux.

## UI / organization
- [ ] Dashboard contains OBS operations but not update-vendor clutter.
- [ ] Updates contains OBS, NVIDIA, AMD, Elgato Hardware & Software, SteelSeries Sonar, and Windows Update.
- [ ] Bandwidth contains network/profile controls.
- [ ] Pre-Flight contains per-run options.
- [ ] Force Close is visually separated as destructive.
- [ ] Primary actions expose tooltips.
- [ ] Long-running checks expose loading/progress state.
- [ ] UI remains usable at minimum window size.

## Pre-Flight
- [ ] Running with OBS closed does NOT launch OBS by default.
- [ ] `Launch OBS after a ready result` is off by default.
- [ ] Ready result launches OBS only when the option is enabled.
- [ ] Failure result never auto-launches OBS.
- [ ] `Skip software update checks this run` skips OBS online release + Windows main-update checks.
- [ ] Skip-updates mode still checks local OBS health, storage, GPU state, Elgato, SteelSeries Sonar, plugins, logs, assets, backups, and crashes.
- [ ] Bandwidth scan runs only when explicitly enabled.

## Recovery
- [ ] Healthy OBS is never force-killed by Recovery Protection.
- [ ] A normally closed OBS remains closed.
- [ ] Stuck OBS shutdown is cleaned after the configured threshold.
- [ ] Stuck-shutdown cleanup leaves OBS closed.
- [ ] Sustained hung-window recovery follows relaunch preference.
- [ ] Elevated OBS triggers UAC only when termination actually requires elevation.
- [ ] Elevated helper verifies the target PID belongs to `obs64`.
- [ ] Recovery-loop protection pauses repeated automatic recovery.

## Neutral hardware states
- [ ] No NVIDIA GPU is informational.
- [ ] No AMD GPU is informational.
- [ ] No Elgato hardware/software is informational.
- [ ] Elgato software-only state does not claim hardware is connected.
- [ ] Elgato hardware-only state does not claim software is installed.
- [ ] No SteelSeries GG/Sonar is informational.
- [ ] Missing vendor hardware does not make Pre-Flight NOT READY.

## Bandwidth Advisor
- [ ] Automatic scan clearly discloses network-test data use.
- [ ] Multiple upload samples are collected.
- [ ] Stable result is not simply the fastest sample.
- [ ] Safe budget equals stable upload / 4.
- [ ] Audio/protocol room is reserved before video bitrate.
- [ ] Manual measured-upload input works without running a test.
- [ ] Invalid values are rejected.
- [ ] Twitch/YouTube/generic recommendations obey their configured ceilings.
- [ ] Motion profile changes the resolution/FPS recommendation.
- [ ] Current OBS profile bitrate comparison works when readable.
- [ ] Advisor never modifies OBS automatically.

## Updates
- [ ] OBS update check works.
- [ ] NVIDIA local driver/telemetry works when available.
- [ ] AMD installed-driver detection works.
- [ ] Official NVIDIA and AMD buttons open vendor pages.
- [ ] Elgato software and hardware are separate signals.
- [ ] SteelSeries GG/Sonar check is neutral when absent.
- [ ] Windows main-update check excludes drivers, previews, optional/browse-only updates, routine Defender definitions, and MSRT.
- [ ] Ground Control never auto-installs system/vendor updates.

## Backup / plugin / diagnostics
- [ ] Backup creates valid manifest and ZIP.
- [ ] Sensitive service/browser/cache files remain excluded.
- [ ] Restore refuses while OBS is running.
- [ ] Restore creates safety backup when enabled.
- [ ] Plugin quarantine remains reversible.
- [ ] Latest OBS log analysis works.
- [ ] Missing local assets scan works.
- [ ] Sanitized support report does not include secrets, private URLs, USB serial numbers, or raw device IDs.


## Plugin update alpha regression
- [ ] Local scan inventories plugins without making network requests.
- [ ] Update scan compares installed version against the latest verified release for mapped plugins.
- [ ] Plugin row renders `Installed X → Latest Y`.
- [ ] A newer verified release shows `Update available`.
- [ ] Same version shows `Current`.
- [ ] Unknown installed version does not falsely claim an update.
- [ ] Unmapped plugin shows `Update source not verified`.
- [ ] `Update Plugin` opens the exact latest verified release URL.
- [ ] `Remind in 1 Week` stores the exact version and displays a deferred date.
- [ ] Deferred exact version does not create a repeated Pre-Flight warning.
- [ ] Reminder expires after its date and the update becomes actionable again.
- [ ] `Skip This Version` suppresses only that exact release.
- [ ] A newer release is actionable even when the previous release was skipped.
- [ ] `Clear Reminder` restores normal update status immediately.
- [ ] Plugin update actions never download/install binaries automatically.

## Windows installer regression checks (v0.7.0-alpha.5+)

- [ ] GitHub Actions produces `Nerdspace-OBS-Ground-Control-Setup-vX.Y.Z.exe`.
- [ ] Installer runs without requesting Administrator privileges for a normal per-user install.
- [ ] Default path is `%LOCALAPPDATA%\Programs\Nerdspace Labs\OBS Ground Control`.
- [ ] Start Menu shortcut launches the installed app.
- [ ] Optional Desktop shortcut works when selected.
- [ ] Windows Settings > Apps shows a normal uninstall entry.
- [ ] Installing a newer alpha upgrades the existing Ground Control install rather than creating a duplicate product entry.
- [ ] Installer prompts/closes a running Ground Control instance before replacing locked files when required.
- [ ] Uninstall removes installed program files and shortcuts but does not silently delete user-created backups/logs/settings outside the install directory.
- [ ] Portable ZIP continues to run independently of the installed build.


## Self-contained runtime regression checks (v0.7.0-alpha.5+)

- [ ] Install on a supported clean Windows VM/test machine with no separately installed .NET Desktop Runtime.
- [ ] Ground Control launches normally after installation.
- [ ] Setup never prompts to download/install .NET.
- [ ] Unplug/disconnect the network during install and confirm setup still completes.
- [ ] Portable ZIP also launches without installing .NET separately.
