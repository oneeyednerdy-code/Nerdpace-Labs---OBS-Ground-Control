# v0.7.0-alpha.2 Alpha Patch Notes

## Focus: verified OBS plugin updates

This alpha tightens plugin update handling around three rules:

1. Ground Control scans every discovered Windows OBS plugin locally.
2. It only claims an update when it can compare a readable installed version with a newer release from a trusted mapped source.
3. The update action opens the exact verified latest release page; Ground Control does not download or install plugin binaries automatically.

## New plugin workflow

- Installed version → latest verified version in the plugin list.
- Current / Update available / Version unknown / Update source not verified states.
- Remind in 1 Week for the exact currently available version.
- Skip This Version without hiding future newer releases.
- Clear Reminder / Skip state.
- Deferred and skipped versions remain visible but neutral during Pre-Flight.

## Initial trusted source mappings

- Aitum Multistream — Aitum/obs-aitum-multistream
- Aitum Vertical — Aitum/obs-vertical-canvas
- Source Record — exeldro/obs-source-record

Unknown plugins remain inventoried and can still expose load-health signals, but Ground Control will not fabricate a latest version or update link.

## Alpha test priority

Test with OBS fully closed first, then test plugin inventory/update checks with the plugins you actually use. Report the detected plugin name, installed version, latest version/status, and whether the release link went to the expected official project. Do not include stream keys, OAuth tokens, or private URLs in bug reports.


## Installer patch

- Added a normal Windows Setup EXE build using Inno Setup.
- Per-user install path avoids an unnecessary installer UAC prompt.
- Adds Start Menu entry, optional Desktop shortcut, Windows uninstall entry, and launch-after-install option.
- GitHub Releases now publish both the recommended installer and portable ZIP.
- The installer is unsigned until the separate Authenticode signing step is configured.
