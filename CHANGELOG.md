# Changelog

## 0.8.0-alpha.1 - Streamer Mission Control Rebrand

- Rebranded the public product to **NerdSpace Labs - Streamer Mission Control**.
- Matched the shared NerdSpace Labs black/violet design system.
- Replaced decorative orange with NerdSpace violet while preserving red/amber/green for status semantics.
- Added a flat Sputnik-inspired Windows desktop/taskbar/tray icon.
- Simplified the splash screen to the product text, desktop icon, and loading state.
- Updated installer names, shortcuts, tray labels, About UI, metadata, README, scripts, and GitHub release artifacts.
- Preserved the existing Inno Setup AppId for upgrade continuity.
- Added migration from the legacy `Nerdspace Labs\OBS Ground Control` local settings/backups directory.
- Renamed the published executable to `NerdSpace.StreamerMissionControl.exe`.

## 0.8.0-alpha.1 - Creator Bot Update Checks

- Added installed-version detection and stable update checks for **Mix It Up**, **Streamer.bot**, and **Firebot**.
- Mix It Up uses the official `MixItUpBot/Desktop` GitHub Releases feed.
- Firebot uses the official `crowbartools/Firebot` GitHub Releases feed.
- Streamer.bot uses the official Streamer.bot stable Downloads page because its GitHub repository is not the current release feed.
- Added automatic detection through running processes, standard locations, Windows uninstall metadata where applicable, and common portable folders.
- Added optional executable-path overrides for all three tools in Settings.
- Added all three creator bots to **Check Everything**.
- Added `Current`, `Update available`, `Newer than catalog`, `Version unknown`, and `Not detected automatically` states.
- Mission Control opens official release/download destinations only; it never silently installs or overwrites creator tools.
- Improved loose version comparison so equivalent versions such as `1.0.4` and `1.0.4.0` compare correctly.

## 0.7.0-alpha.10 - Window & Launch UX

- Closing the main window with **X** now fully shuts down Mission Control instead of hiding it to the tray.
- Added an explicit **Exit** button with full-shutdown behavior.
- Added a branded animated startup screen with an indeterminate loading animation.
- Reduced the default window size for 14-inch/smaller laptops.
- Reduced minimum window dimensions to `760 × 540` and kept tab contents scrollable.
- Mission Control now remembers the last normal window size.
- Tightened general button/tab spacing for smaller displays.
- Added Settings copy explaining the difference between Close/Exit and Minimize.

## 0.7.0-alpha.9 - GitHub Release Guide

- Added a complete GitHub repository and first-publish walkthrough.
- Added a Windows alpha release checklist.
- Alpha-tagged releases are now explicitly created as GitHub Pre-releases.
- Release publishing now fails when expected installer/portable artifacts are missing.
- Preserves automatic .NET 10 self-contained publishing, Inno Setup packaging, plugin-catalog refresh, and SHA-256 generation.

## 0.7.0-alpha.8 - Full OBS Resource Catalog

- Added a build-time catalog generator for the official OBS Studio Plugins resource directory.
- Release builds enumerate the OBS plugin resource catalog and embed the resulting JSON into Mission Control.
- Every catalog entry carries its official OBS resource URL.
- Source repositories are marked verified only when the OBS resource page publishes the source URL.
- GitHub release/version checks run only for OBS-page-verified GitHub repositories.
- Plugins with no published source URL remain discoverable but are never presented as source-verified.
- Added catalog date/resource/source-verification counts to Plugin Discovery.
- Windows-only discovery hides resources explicitly marked non-Windows while retaining entries with unspecified platform metadata.
- Added maintainer overrides for reliable local module matching without weakening catalog trust.

## 0.7.0-alpha.7 - Trusted Plugin Registry & Discovery

- Split Plugin Control into **Installed**, **Updates**, and **Discover** views.
- Added a curated trusted plugin registry with verified OBS resource pages and official GitHub repositories.
- Expanded verified update coverage beyond Aitum/Source Record to include Move Transition, Advanced Scene Switcher, obs-shaderfilter, Composite Blur, Downstream Keyer, and Aitum Stream Suite.
- Added local plugin discovery search by plugin name, author, feature description, or repository.
- Added optional latest-version refresh from each plugin's verified GitHub Releases source.
- Added a direct **Browse Official OBS Plugins** action for the full OBS resource directory.
- Discovery remains read-only: Mission Control does not automatically download or install third-party plugins.
- Installed plugin inventory continues to hide OBS bundled modules.

## 0.7.0-alpha.6 - Third-Party Plugin Isolation

- Plugins tab now inventories third-party/user-installed OBS plugins instead of every bundled OBS module.
- Filters OBS-bundled Windows modules from the legacy mixed `obs-plugins\64bit` directory.
- Keeps the recommended ProgramData plugin directory as third-party/user-installed inventory.
- Adds support for custom `OBS_PLUGINS_PATH` directories.
- Pre-Flight plugin counts now represent third-party plugins only.
- Updated plugin scan labels, tooltips, documentation, and regression tests.

## 0.7.0-alpha.5 - .NET 10 Compile Fix

- Fixed `string.Split` overload usage in `ObsConfigurationInspectorService`.
- Fixed the same overload issue in `PluginInventoryService`.
- Restores successful compilation under the .NET 10 SDK without changing user-facing behavior.

## 0.7.0-alpha.4 - .NET 10 SDK / Runtime Baseline

- Moved the Windows application target from `net8.0-windows` to `net10.0-windows`.
- Pinned developer/build tooling to the .NET 10 SDK 10.0.400 baseline with feature-band roll-forward enabled.
- Updated GitHub Actions to install the .NET 10 SDK (`10.0.x`).
- Kept Windows publishing self-contained and single-file so end users do not need the .NET 10 SDK or Desktop Runtime installed.
- Installer remains offline-capable and packages the runtime files produced by `dotnet publish`.
- The full developer SDK is intentionally not bundled into the installer because it is only needed to build Mission Control, not run it.

## 0.7.0-alpha.3 - Self-Contained Windows Runtime

- Hardened Windows publishing as self-contained `win-x64`.
- Installer now explicitly carries the required .NET runtime/application dependencies; no separate .NET Desktop Runtime install is required.
- Added CI verification that release publishing remains self-contained and single-file.
- Kept trimming disabled during alpha to protect Avalonia/reflection compatibility.
- Added clean-machine/offline installer regression checks.

## 0.7.0-alpha.2

### Added
- Windows Setup EXE installer project using Inno Setup 6.
- Per-user installation under LocalAppData with Start Menu integration and optional Desktop shortcut.
- GitHub Actions installer build and release artifact.
- Installer documentation and alpha regression coverage.

### Changed
- GitHub releases now publish the installer as the recommended Windows alpha package while keeping the portable ZIP available.

## 0.7.0-alpha.1 - Verified Plugin Updates & Deferrals

### Added
- Plugin rows now show **Installed → Latest verified** version comparisons.
- Exact verified GitHub release URL is retained from release metadata and used by **Update Plugin**.
- **Remind in 1 Week** for an exact plugin release.
- **Skip This Version** without suppressing future newer releases.
- **Clear Reminder** to remove a plugin update deferral.
- Generic update-deferral storage designed to be reused by OBS/vendor update reminders later.
- Pre-Flight can check verified plugin update sources when update checks are enabled.

### Changed
- Plugins without a trusted update source now explicitly show **Update source not verified** instead of implying they are current.
- Plugins with an unknown installed version never get a fabricated update result.
- Deferred/skipped plugin updates are neutral in Pre-Flight until the reminder expires or a newer version appears.
- Plugin update actions open the exact verified latest release when available; Mission Control still does not auto-install plugin updates.
- Footer/build metadata now identifies this alpha as **v0.7.0-alpha.1**.

## 0.6.0 - Windows-first development
- Narrowed supported development/release target to Windows 10/11 x64.
- Removed macOS/Linux build and release jobs.
- Removed macOS/Linux OBS platform implementations from the active source tree.
- Targeted `net8.0-windows`.
- Simplified plugin, GPU, Elgato, SteelSeries Sonar, folder-launch, and Windows Update paths around Windows behavior.
- Improved Elgato hardware detection to enumerate currently present Windows devices through SetupAPI instead of relying on historical USB registry entries.
- Retained `IObsPlatformService` as a future porting seam.
- Added a future-platform roadmap instead of shipping partially supported ports.
- Kept all v0.5.0 Mission Control features: Recovery Protection, Pre-Flight, Bandwidth Advisor, Updates, Plugins, Backups, Diagnostics, Elgato Hardware & Software, and SteelSeries Sonar.

## 0.5.0 - Creator Hardware & Audio Health

### Added
- Dedicated **SteelSeries Sonar** check in Updates and Pre-Flight on Windows.
- SteelSeries GG installation/version detection.
- Sonar runtime and virtual-audio endpoint signals without requiring SteelSeries hardware.
- Official SteelSeries Sonar destination button.
- Best-effort connected Elgato USB hardware detection on Windows, macOS, and Linux.

### Changed
- Elgato is now labeled **Elgato Hardware & Software**.
- Elgato installed software and connected hardware are reported as separate sections.
- Installing Wave Link/Stream Deck no longer implies an Elgato device is connected.
- No Elgato or SteelSeries product detected remains a neutral informational state and never blocks Pre-Flight.
- Update Center is reorganized into OBS, NVIDIA, AMD, Elgato Hardware & Software, SteelSeries Sonar, and Windows Update cards.
- Footer remains dynamic: **NerdSpace Labs by OneEyedNerdy • v0.5.0**.

### Safety / privacy
- Hardware inventory records only friendly/model-level device descriptions used for local status; Mission Control does not include USB serial numbers in sanitized support reports.
- Vendor software and firmware installation remains manual through official vendor tools/pages.

## 0.4.0 - Pre-Flight UX + Bandwidth Advisor

### Added
- Dedicated **Bandwidth** tab.
- Multi-sample upload test using Cloudflare's public speed-test upload endpoint.
- Conservative stable-upload calculation instead of trusting the fastest sample.
- User-requested `stable upload ÷ 4` safe stream-budget formula.
- Video/audio overhead reservation before video-bitrate recommendation.
- Twitch, YouTube, and generic platform-aware bitrate profiles.
- Low-motion, balanced, and high-motion recommendation modes.
- Twitch Enhanced Broadcasting option.
- Twitch added server-side transcode option for 2K requirement handling.
- Resolution, FPS, bitrate, audio bitrate, codec/mode, confidence, and rationale output.
- Read-only inspection of the current OBS output profile and bitrate comparison against the recommendation.
- Optional Bandwidth Advisor check inside Pre-Flight.
- **Skip software update checks this run** option.
- **Launch OBS after a ready result** option, disabled by default.
- Dedicated **Updates** tab for OBS/NVIDIA/AMD/Elgato/Windows checks.
- Loading/progress indicators for Pre-Flight, bandwidth testing, updates, plugin scans, backup operations, and diagnostics.
- Tooltips throughout primary controls.
- Neutral no-device/no-vendor Pre-Flight states for NVIDIA, AMD, and Elgato.

### Changed
- Pre-Flight no longer launches OBS as part of normal operation.
- Header Pre-Flight action opens the options page rather than immediately running a scan.
- Update controls moved out of Dashboard into Update Center.
- Destructive Force Close is visually isolated from routine OBS controls.
- Orange is reserved primarily for brand/primary-action emphasis instead of decorating every card equally.
- Footer remains dynamic: **NerdSpace Labs by OneEyedNerdy • v0.4.0**.

### Privacy / safety
- Bandwidth testing uploads generated bytes only; no user files are read or uploaded by the test.
- Bandwidth scans are explicit and disclose approximate data use (~25 MB upload).
- Skipping update checks does not disable local readiness checks.
- Mission Control still does not automatically install OBS, GPU drivers, Elgato software/firmware, or Windows updates.

## 0.3.0 - Mission Control expansion

### Added
- Full Pre-Flight dashboard.
- NVIDIA/AMD graphics state, Elgato software inventory, and Windows main-update check.
- plugin inventory/update evidence/quarantine.
- backup/restore, diagnostics, missing assets, crash history, and sanitized support report.

## 0.2.0 - Cross-platform launch

### Added
- Avalonia desktop UI shared across Windows, macOS, and Linux.
- macOS Apple Silicon (`osx-arm64`) and Intel (`osx-x64`) builds.
- Linux x64 build.
- cross-platform OBS Safe Mode launch.
- dynamic NerdSpace Labs footer.
