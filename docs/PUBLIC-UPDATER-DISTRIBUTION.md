# Public Update Feed

Streamer Mission Control already lives in a public GitHub repository, so a second distribution repository and a cross-repository PAT are not required.

## Repository

The build and updater both use the repository identified by GitHub Actions as `GITHUB_REPOSITORY`:

```text
oneeyednerdy-code/Nerdpace-Labs---OBS-Ground-Control
```

## Required Actions configuration

Repository variable:

```text
NETSPARKLE_PUBLIC_KEY
```

Repository secret:

```text
NETSPARKLE_PRIVATE_KEY
```

`UPDATE_DISTRIBUTION_REPOSITORY` and `UPDATE_DISTRIBUTION_TOKEN` are no longer used. They may be deleted from repository Actions settings.

## Release layout

Each version tag creates the normal GitHub release with the installer, portable ZIP and checksum. The same workflow creates or refreshes a fixed release/tag named `update-feed` containing the signed appcast files.

For preview releases:

```text
appcast-preview.xml
appcast-preview.xml.signature
```

For stable releases, the workflow also publishes:

```text
appcast-stable.xml
appcast-stable.xml.signature
```

The release workflow finishes by downloading the preview appcast and signature anonymously. If either cannot be fetched publicly, the release fails instead of shipping a broken updater.

## Repair workflow

GitHub Actions also includes **Repair Update Feed**. Run it manually and supply an existing versioned release tag. It downloads that release's installer, regenerates the signed appcast with the existing Ed25519 keys, refreshes `update-feed`, and verifies anonymous access.
