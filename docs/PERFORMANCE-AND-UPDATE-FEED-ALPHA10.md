# v0.8.0-alpha.10 — Responsive Scans, Exit Fix, and Update Feed Workflow

## Exit freeze

The previous close path synchronously blocked Avalonia's UI thread on `SaveAsync(...).GetAwaiter().GetResult()`. An async file-write continuation could need that same UI thread, creating a deadlock.

alpha.10 uses a small synchronous local-state write only during shutdown. Startup registration is already persisted when its setting changes, so shutdown does not need to run an async platform configuration operation.

## Scan responsiveness

Disk, registry, manifest, PE/DLL metadata, OBS log, scene asset, GPU, Elgato, Sonar, creator-software, and Pre-Flight scan work is moved off the Avalonia UI thread where appropriate. UI controls are updated only after worker tasks complete. The application lifetime cancellation token is cancelled during shutdown.

## Memory / allocation reduction

- Slow OBS recording-path, free-space, and installed-version probes are cached for 30 seconds rather than repeated every 2-second health tick.
- Scene JSON is parsed from a stream instead of first allocating a complete file string.
- Installed and Update plugin views share the same immutable result list rather than duplicating it.
- The project explicitly uses workstation GC rather than server GC for a smaller desktop steady-state footprint.
- No forced `GC.Collect()` calls are used; forced collections can create visible pauses and would work against the responsiveness goal.

## Update feed

The GitHub repository itself is public, so alpha.10 removes the unnecessary second distribution-repository architecture. The normal release workflow:

1. Builds and publishes the versioned installer/portable release in the existing repository.
2. Requires the configured `NETSPARKLE_PUBLIC_KEY` variable and `NETSPARKLE_PRIVATE_KEY` secret.
3. Generates the signed NetSparkle appcast using AppCastGenerator 2.9.0.
4. Creates or refreshes the `update-feed` release in the same repository.
5. Uploads `appcast-preview.xml` + `.signature` for alpha/beta releases.
6. Verifies the feed is publicly downloadable without GitHub credentials.

`Repair Update Feed` is a separate manual workflow. Supply an existing version tag and it re-downloads that release's installer, rebuilds/signs the appcast, refreshes `update-feed`, and verifies anonymous access.
