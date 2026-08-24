# v0.8.0-alpha.7 — Signed Updater Bootstrap

This release is intentionally focused on proving the secure Streamer Mission Control self-update chain.

## Purpose

alpha.7 must be built by GitHub Actions **after** these repository-level values exist:

- Actions variable: `NETSPARKLE_PUBLIC_KEY`
- Actions secret: `NETSPARKLE_PRIVATE_KEY`

The public key is embedded into the application during the GitHub build. The private key is used only by the release workflow to sign the update metadata/package.

## Expected alpha.7 behavior

After installing the GitHub-built alpha.7 installer manually:

1. Open **Updates**.
2. Select **Preview**.
3. Click **Check Now**.
4. Expected result:

```text
Installed: 0.8.0-alpha.7
Latest: 0.8.0-alpha.7
Channel: Preview
Status: Up to date
```

`Self-update signing not configured` is a release-blocking result for this bootstrap test.

## Expected GitHub release workflow

The release workflow should:

1. Configure the embedded update feed with `NETSPARKLE_PUBLIC_KEY`.
2. Build and publish the normal Windows release.
3. Confirm both signing values are configured.
4. Install `NetSparkleUpdater.Tools.AppCastGenerator` 2.9.0.
5. Generate the signed preview appcast.
6. Create/update the fixed `update-feed` GitHub release.
7. Upload:
   - `appcast-preview.xml`
   - `appcast-preview.xml.signature`

## First end-to-end update test

Do not use alpha.7 to test updating *to itself*.

Leave GitHub-built alpha.7 installed, then publish alpha.8.

alpha.7 should detect alpha.8 and enable **Update Now**. A successful test is:

```text
alpha.7
  ↓
detect alpha.8
  ↓
download installer
  ↓
verify signature
  ↓
save settings
  ↓
close Mission Control
  ↓
launch Inno Setup upgrade
  ↓
alpha.8 installed
```

## Signing helper behavior

`setup-update-signing.ps1` now reuses a complete existing key pair.

It refuses only when one key exists without its matching pair, preventing accidental signing-identity replacement.
