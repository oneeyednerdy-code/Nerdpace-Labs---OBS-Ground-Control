# Ground Control Pre-Flight

Pre-Flight is a read-mostly readiness check. It does not install operating-system updates, GPU drivers, Elgato software/firmware, or OBS plugins.

## Checks

1. OBS installation and executable discovery.
2. OBS process health / stuck-process state.
3. Installed OBS version and explicit online release check.
4. Recording-drive free space.
5. GPU and graphics driver state:
   - NVIDIA on Windows, with `nvidia-smi` telemetry when available.
   - AMD installed display-driver state on Windows.
6. Elgato desktop software inventory and connected USB-device signals on Windows.
7. Windows Update main-update check on Windows only.
8. OBS plugin inventory and load-issue evidence.
9. Latest OBS log warning patterns.
10. Missing local scene assets.
11. Ground Control backup age.
12. Recent OBS crash-report presence.

## Windows Update definition of “main updates”

Ground Control asks Windows Update Agent for uninstalled, non-hidden, non-optional software updates. It then excludes Preview releases, driver updates, routine Defender definition/security-intelligence updates, and the Malicious Software Removal Tool.

This is a readiness signal, not a replacement for Windows Update. The user chooses whether and when to install anything.

## Result levels

- PASS: no action detected for that check.
- INFO: informational or not applicable.
- WARNING: review before going live.
- FAIL: OBS state is unsafe to proceed without correction.


## Creator hardware/audio checks

- **Elgato Hardware & Software** reports installed Elgato applications separately from connected USB hardware. Neither is assumed from the other.
- **SteelSeries Sonar** checks SteelSeries GG installation and local Sonar runtime/audio-endpoint evidence on Windows.
- If Elgato or Sonar is not used on the computer, **Not detected** is informational and does not lower readiness.
