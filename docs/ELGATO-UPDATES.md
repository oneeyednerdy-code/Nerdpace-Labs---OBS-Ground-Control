# Elgato Software Update Checks

Streamer Mission Control keeps Elgato software and Elgato hardware separate.

## Software

On Windows, Mission Control detects installed versions from Windows software-registration data and can compare supported applications with Elgato's official release-note catalogs.

Supported release-note comparisons:

- Stream Deck
- Wave Link
- Camera Hub
- Control Center
- 4K Capture Utility
- Elgato Studio

A software result can be:

- Current
- Update available
- Newer than catalog
- Version unknown
- Latest version unavailable
- Update check unavailable

Mission Control does not install Elgato software automatically.

## Hardware

Currently connected Elgato hardware is detected separately through Windows present-device enumeration.

A connected device does not imply that every Elgato application should be installed.

Hardware firmware is not guessed from a generic product name. Firmware should be checked and applied through the official Elgato software that owns that device.

## Empty results

A completed scan that finds no matching hardware or software explicitly reports:

`Nothing found`

This is a successful scan state, not an error.
