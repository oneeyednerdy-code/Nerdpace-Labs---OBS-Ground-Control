# Windows platform notes

Streamer Mission Control v0.8.0-alpha.5 is developed and released for **Windows 10/11 x64 only**.

## Why Windows first?

The current product depends heavily on Windows-specific creator-workstation capabilities:

- OBS `obs64.exe` process/window health
- `Process.Responding` and native window handles
- UAC elevation for terminating an elevated stuck OBS process
- Windows Registry inspection
- Windows Update Agent
- NVIDIA `nvidia-smi`
- AMD/NVIDIA installed display-driver inventory
- Elgato installed-software and USB-device signals
- SteelSeries GG/Sonar process and virtual-audio endpoint signals
- Windows startup integration
- Explorer, ProgramData, AppData, and Windows OBS paths

Focusing on one OS lets these features become reliable before platform abstraction becomes a maintenance burden.

## Future ports

The `IObsPlatformService` boundary is intentionally retained as a future seam. macOS and Linux implementations can be reintroduced later without being part of the current build/release/support promise.

See `FUTURE-PLATFORMS.md`.
