# NerdSpace Labs - Streamer Mission Control v0.8.0-alpha.10

## Focus

Responsive scans, clean shutdown, lower background allocation churn, and a corrected same-repository signed update-feed workflow.

## Fixed

- **Exit freeze:** removed shutdown-time blocking on `SaveAsync(...).GetAwaiter().GetResult()` from the Avalonia UI thread.
- **Scan freezes:** plugin scanning, Pre-Flight, Diagnostics, GPU, Elgato, SteelSeries Sonar, creator-software detection, and support-report collection now move scan-heavy work to worker threads.
- **Shutdown during scans:** application lifetime cancellation is signaled before close so cancellable scan work can stop cleanly.
- **Background allocation churn:** OBS recording path, disk-free-space, and installed-version probes are cached for 30 seconds instead of repeated every two seconds.
- **Scene scan peak memory:** scene collection JSON is parsed from a stream rather than first loading a second full string copy into memory.
- **Plugin result duplication:** Installed Plugins and Plugin Updates share the same result set instead of duplicating the list.
- **Desktop GC:** workstation GC is explicitly selected for a smaller desktop steady-state footprint.

## Updater workflow

The project now uses the existing public GitHub repository directly:

```text
oneeyednerdy-code/Nerdpace-Labs---OBS-Ground-Control
```

No second release repository is required.

Required GitHub Actions values remain:

```text
Variable: NETSPARKLE_PUBLIC_KEY
Secret:   NETSPARKLE_PRIVATE_KEY
```

These old values are no longer used and can be deleted:

```text
UPDATE_DISTRIBUTION_REPOSITORY
UPDATE_DISTRIBUTION_TOKEN
```

When a version tag is pushed, `Release Windows` now:

1. builds the self-contained Windows application;
2. publishes the installer, portable ZIP, and SHA256SUMS to the normal versioned release;
3. generates a signed NetSparkle appcast with AppCastGenerator 2.9.0;
4. creates or refreshes the fixed `update-feed` release in the same repo;
5. uploads the appcast and `.signature` sidecar;
6. anonymously downloads both files to verify that installed copies can reach them.

A new manual workflow, **Repair Update Feed**, can rebuild the signed feed from any existing versioned release tag.

## Bootstrap note

If your currently installed build was compiled while the updater repository variable pointed to the incorrect/nonexistent release repository, that wrong URL is embedded in that EXE. Install the GitHub-built alpha.10 installer manually once. After that, publish alpha.11 and use alpha.10's **Check Now / Update Now** to validate the full in-app upgrade path.

## Validation performed in this source package

- Project/Avalonia XML parsed successfully.
- Embedded JSON parsed successfully.
- GitHub Actions YAML parsed successfully.
- No remaining `GetAwaiter().GetResult()` shutdown calls in application source.
- No `UPDATE_DISTRIBUTION_*` references remain in GitHub workflows.
- Project version is `0.8.0-alpha.10`.
- Full .NET compilation remains the GitHub Actions compiler gate because the local packaging environment does not contain the .NET SDK.
