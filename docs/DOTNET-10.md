# .NET 10 baseline

Streamer Mission Control v0.8.0-alpha.6 targets `net10.0-windows` and is built with the .NET 10 SDK.

## Developers / GitHub Actions

- SDK baseline: 10.0.400
- GitHub Actions channel: `10.0.x`
- Target framework: `net10.0-windows`
- Runtime identifier: `win-x64`

Developers need the .NET 10 SDK to restore, build, test, and publish the source.

## End users

End users do **not** need the .NET SDK. Mission Control is published with:

- `--self-contained true`
- `PublishSingleFile=true`
- `PublishTrimmed=false`
- `IncludeNativeLibrariesForSelfExtract=true`

The setup package includes the .NET 10 runtime components emitted by the self-contained publish. The installer performs no .NET download and works without a separately installed Desktop Runtime.

The SDK itself is intentionally not bundled: it contains compilers and developer tooling that are unnecessary for running Mission Control and would substantially increase installer size and attack surface.
