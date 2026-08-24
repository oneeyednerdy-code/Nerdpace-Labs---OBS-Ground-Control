# Privacy

NerdSpace Labs - Streamer Mission Control is designed to be local-first.

It does **not** intentionally collect or transmit:
- Twitch credentials or OAuth tokens
- OBS WebSocket passwords
- stream keys
- chat messages
- scene/source contents
- browser-source URLs
- creator identity data
- usage telemetry

## Bandwidth Advisor
Bandwidth Advisor is an explicit network test. When the user runs it, Mission Control sends generated test bytes to Cloudflare's public speed-test upload endpoint at `https://speed.cloudflare.com/__up` to estimate sustained upload performance.

- No personal files are uploaded.
- OBS scenes/configuration files are not sent.
- Stream keys, credentials, browser URLs, and chat are not sent.
- A normal scan uploads approximately 25 MB plus a small warm-up request.
- The test is not required for normal Mission Control operation.

Cloudflare independently receives the network request and may process connection metadata according to Cloudflare's own policies.

Mission Control logs contain local application events such as OBS process state, timestamps, platform information, process IDs, and summarized Bandwidth Advisor results. Diagnostic reports are saved locally for the user to inspect before sharing.
