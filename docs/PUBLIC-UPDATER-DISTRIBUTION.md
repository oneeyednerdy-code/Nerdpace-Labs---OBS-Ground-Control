# Public Updater Distribution

Streamer Mission Control source code may remain in a **private GitHub repository**.

The self-updater cannot, however, use release assets in a private GitHub repository as an anonymous download endpoint. Installed copies of the app do not contain a GitHub access token and must never embed one.

NetSparkle Strict mode fetches the appcast and its `.signature` sidecar from the configured update URL. The installer referenced by the appcast must also be publicly downloadable.

## Recommended architecture

```text
PRIVATE SOURCE REPOSITORY
  Nerdpace-Labs---OBS-Ground-Control
        |
        | GitHub Actions builds + signs
        v
PUBLIC RELEASE-ONLY REPOSITORY
  e.g. NerdSpace-Streamer-Mission-Control-Releases
        |
        +-- v0.8.0-alpha.9
        |    +-- Setup.exe
        |    +-- portable ZIP
        |    +-- SHA256SUMS.txt
        |
        +-- update-feed
             +-- appcast-preview.xml
             +-- appcast-preview.xml.signature
             +-- appcast-stable.xml (after stable release)
             +-- appcast-stable.xml.signature
```

No application source code needs to be placed in the public release repository.

## One-time GitHub setup

1. Create a **public** GitHub repository for binary releases.
   - A suggested name is `NerdSpace-Streamer-Mission-Control-Releases`.
   - Initialize it with a README so it has a default branch.
2. In the private source repository, open:
   `Settings -> Secrets and variables -> Actions`.
3. Add repository variable:

```text
UPDATE_DISTRIBUTION_REPOSITORY
```

Value example:

```text
oneeyednerdy-code/NerdSpace-Streamer-Mission-Control-Releases
```

4. Create a **fine-grained GitHub personal access token** that has access only to the public distribution repository and grants:
   - Repository permissions -> Contents: Read and write
5. Add that token to the private source repository as Actions secret:

```text
UPDATE_DISTRIBUTION_TOKEN
```

Existing updater signing values remain unchanged:

```text
NETSPARKLE_PUBLIC_KEY   (Actions variable)
NETSPARKLE_PRIVATE_KEY  (Actions secret)
```

## Release behavior

For each `v*.*.*` tag, the private source workflow now:

1. builds Mission Control;
2. publishes the normal source-repository release;
3. verifies that the configured updater distribution repository is **public**;
4. mirrors installer/portable/checksum assets to the public release-only repository;
5. generates the NetSparkle appcast with installer URLs pointing at that public repository;
6. signs the appcast and installer metadata with the existing Ed25519 signing identity;
7. publishes the signed appcast to the public `update-feed` release.

The release fails closed if the configured distribution repository is private or inaccessible.

## Why no GitHub token is embedded in Mission Control

Embedding a token would allow anyone with the installed application to extract that credential. Mission Control therefore uses only anonymous public download URLs plus cryptographic Ed25519 verification.
