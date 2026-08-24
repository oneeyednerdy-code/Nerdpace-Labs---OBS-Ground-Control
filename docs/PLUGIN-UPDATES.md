# Plugin Update Verification

Streamer Mission Control inventories **third-party/user-installed OBS plugins only**. OBS-bundled modules are intentionally excluded from the Plugins tab and Pre-Flight plugin counts. Update status is only asserted when Mission Control can map a third-party plugin to a trusted release source and read both an installed version and the latest verified release metadata.

On Windows, Mission Control treats the recommended `C:\ProgramData\obs-studio\plugins` structure and custom `OBS_PLUGINS_PATH` locations as external plugin locations. The legacy mixed `obs-plugins\64bit` directory inside the OBS installation is scanned with a bundled-module exclusion list so older third-party installs can still be found without showing OBS stock modules.

## Status rules

- **Current**: installed and latest verified versions match.
- **Update available**: both versions are readable and latest is newer.
- **Deferred until DATE**: the exact available version is snoozed.
- **Skipped VERSION**: the exact available version was skipped. A newer release is not skipped automatically.
- **Version unknown**: one side of the comparison cannot be parsed/read reliably.
- **Update source not verified**: the plugin is inventoried, but Mission Control has no trusted update mapping for it.
- **Latest version unavailable**: a trusted source is known, but the online release check failed.

## Safety

`Update Plugin` opens the exact verified latest release page returned by the trusted repository's release API. Mission Control does not download or install plugin binaries automatically in this alpha.

## Initial trusted catalog

- Aitum Multistream → `Aitum/obs-aitum-multistream`
- Aitum Vertical → `Aitum/obs-vertical-canvas`
- Source Record → `exeldro/obs-source-record`

The catalog should only be expanded when the project identity and official release source are verified.
