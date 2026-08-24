# Future platform roadmap

macOS and Linux are deferred while Streamer Mission Control concentrates on a strong Windows release.

## Revisit macOS when
- Windows Recovery Protection is stable in beta
- Windows installer/signing is in place
- backup/restore format is stable
- diagnostics and plugin inventory have a documented compatibility contract

Potential macOS work:
- Apple Silicon and Intel or universal package
- `.app` / DMG packaging
- Developer ID signing + notarization
- native process/window-health implementation
- macOS OBS config/plugin paths

## Revisit Linux when
- shared recovery contracts are stable
- plugin inventory is well-defined
- release/support burden is manageable

Potential Linux work:
- native and Flatpak OBS detection
- AppImage/DEB packaging
- SIGTERM/SIGKILL recovery
- desktop-specific tray support
- distro-aware graphics/driver reporting

Until those milestones are reached, GitHub issues for macOS/Linux should be treated as roadmap input rather than supported-platform bugs.
