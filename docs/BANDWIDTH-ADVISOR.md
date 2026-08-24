# Bandwidth Advisor

Ground Control's Bandwidth Advisor is designed to answer one question conservatively:

> What stream settings can this connection reasonably support while leaving meaningful headroom?

## Formula

Ground Control uses:

`stable measured upload ÷ 4 = conservative total stream budget`

It then reserves room for audio/protocol overhead before recommending video bitrate.

The `/4` divisor is intentionally more conservative than many streaming setup guides. It leaves room for games, voice chat, browser sources, other household devices, and normal ISP variation.

## Automatic test

The automatic test:
- sends a small warm-up request
- sends multiple generated upload samples (~25 MB total measured upload)
- uses Cloudflare's public speed-test upload endpoint
- records average, peak, variation, and a lower sustained value
- does **not** use the fastest result as the recommendation basis

This test measures the route from the computer to Cloudflare's test infrastructure. It is useful evidence, but it is not the same thing as a direct Twitch/YouTube ingest-server test.

## Manual input

Users can enter an upload result measured elsewhere. Ground Control applies the exact same `/4` recommendation logic.

Use a real measured upload result rather than the ISP plan's advertised maximum whenever possible.

## Content motion

- **Low Motion**: favors resolution/detail and is suited to Just Chatting, art, coding, and slower scenes.
- **Balanced**: general recommendation for MMO/RPG/variety streams.
- **High Motion**: favors 60 FPS and may lower resolution for shooters, racing, and other fast movement.

## Platform sources

Recommendation tables are intentionally kept simple and conservative, with current official platform guidance used as upper constraints.

As of the current Windows-first release:

- Twitch Enhanced Broadcasting documentation: https://help.twitch.tv/s/article/multiple-encodes
- Twitch 2K streaming requirements: https://help.twitch.tv/s/article/stream-quality
- YouTube live encoder bitrate/resolution settings: https://support.google.com/youtube/answer/2853702
- OBS stream connection troubleshooting: https://obsproject.com/kb/stream-connection-troubleshooting
- Cloudflare speed-test project/endpoint documentation: https://github.com/cloudflare/speedtest

## Safety

Ground Control only recommends settings. It does not automatically rewrite OBS output settings in the current release.
