# Plugin Update Verification

OBS Ground Control scans all discovered Windows OBS plugin entries locally. Update status is only asserted when Ground Control can map a plugin to a trusted release source and read both an installed version and the latest verified release metadata.

## Status rules

- **Current**: installed and latest verified versions match.
- **Update available**: both versions are readable and latest is newer.
- **Deferred until DATE**: the exact available version is snoozed.
- **Skipped VERSION**: the exact available version was skipped. A newer release is not skipped automatically.
- **Version unknown**: one side of the comparison cannot be parsed/read reliably.
- **Update source not verified**: the plugin is inventoried, but Ground Control has no trusted update mapping for it.
- **Latest version unavailable**: a trusted source is known, but the online release check failed.

## Safety

`Update Plugin` opens the exact verified latest release page returned by the trusted repository's release API. Ground Control does not download or install plugin binaries automatically in this alpha.

## Initial trusted catalog

- Aitum Multistream → `Aitum/obs-aitum-multistream`
- Aitum Vertical → `Aitum/obs-vertical-canvas`
- Source Record → `exeldro/obs-source-record`

The catalog should only be expanded when the project identity and official release source are verified.
