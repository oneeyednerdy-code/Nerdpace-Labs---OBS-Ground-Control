# Creator Bot Update Checks — 0.7.0-alpha.11

OBS Ground Control now checks three creator-side streaming automation tools in Update Center.

## Mix It Up

Installed version: executable metadata from `MixItUp.exe`.

Stable release authority: `https://github.com/MixItUpBot/Desktop/releases`

## Streamer.bot

Installed version: executable metadata from `Streamer.bot.exe`.

Streamer.bot is portable, so Ground Control checks a running process, common folders, and an optional Settings override.

Stable release authority: `https://streamer.bot/downloads`

Ground Control intentionally does not treat the Streamerbot GitHub repository as a release feed.

## Firebot

Installed version: executable metadata from `Firebot v5.exe` or `Firebot.exe`.

Ground Control checks running processes, Windows uninstall metadata, common Electron/LocalAppData locations, common user folders, and an optional Settings override.

Stable release authority: `https://github.com/crowbartools/Firebot/releases`

## Safety

All three integrations are update-check only. Ground Control never downloads, unpacks, installs, upgrades, or overwrites these applications automatically.
