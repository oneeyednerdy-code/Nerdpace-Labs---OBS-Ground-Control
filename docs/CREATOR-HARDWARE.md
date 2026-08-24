# Creator Hardware & Audio Health

Ground Control treats **software installed**, **hardware connected**, and **feature active** as different signals. A missing vendor product is informational, not a failure.

## Elgato Hardware & Software

### Software inventory
Ground Control looks for supported Elgato creator applications such as:
- Stream Deck
- Wave Link
- Camera Hub
- Control Center
- 4K Capture Utility
- Elgato Studio
- Video Capture / legacy Game Capture HD where present

### Hardware inventory
- Windows: present-device enumeration through Windows SetupAPI, so previously connected but currently absent USB devices are not treated as connected.

The app never infers that a device is connected simply because its software is installed. Some Elgato products are driverless or use OS-provided drivers, so Ground Control uses the wording **Hardware & Software** rather than **Elgato Driver**.

## SteelSeries Sonar

Sonar is delivered through SteelSeries GG and is treated as a Windows audio-software feature. Ground Control can report:
- SteelSeries GG installed/version signal
- Sonar runtime process signal
- Sonar virtual-audio endpoint signal

If GG is installed but Sonar is not active, Ground Control reports that state without calling it broken. If GG/Sonar is absent, the Pre-Flight result is neutral.

## Safety
Ground Control does not silently install vendor applications, firmware, or drivers. Official vendor pages/tools remain the destination for updates. Sanitized support reports do not include USB serial numbers or raw hardware IDs.
