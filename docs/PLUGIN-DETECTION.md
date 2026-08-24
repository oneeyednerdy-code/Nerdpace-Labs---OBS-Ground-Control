# Third-Party Plugin Detection

Mission Control intentionally hides OBS Studio's bundled modules from the Plugins tab.

## Windows locations

Mission Control scans:

- `C:\ProgramData\obs-studio\plugins` — OBS's recommended external plugin location. Entries here are treated as user-installed.
- `<OBS install>\obs-plugins\64bit` — legacy mixed location. Mission Control excludes known OBS-bundled modules and keeps third-party DLLs.
- Paths configured through `OBS_PLUGINS_PATH` — treated as external plugin locations.

## Why the legacy folder needs filtering

OBS itself ships many modules in the same legacy directory where older third-party installers may also place DLLs. Mission Control therefore filters only known bundled module identities in that specific OBS installation directory.

## Safety rule

A plugin is not classified as third-party merely because Mission Control does not recognize it. Unknown DLLs in an external plugin location remain visible. The bundled exclusion list is applied only to the OBS installation's own mixed module directory.
