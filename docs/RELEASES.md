# Releases

OBS Ground Control currently publishes **Windows x64 only**.

The release workflow triggers on tags matching:

`v*.*.*`

Example:

```powershell
git tag v0.7.0-alpha.3
git push origin v0.7.0-alpha.3
```

Release artifacts:
- `Nerdspace-OBS-Ground-Control-Setup-vX.Y.Z.exe` — recommended installer
- `Nerdspace-OBS-Ground-Control-vX.Y.Z-win-x64.zip` — portable build
- `SHA256SUMS.txt`

## Version synchronization

The release workflow runs the repository version script before compilation. The same assembly version is used by the executable and the footer:

`Nerdspace Labs by OneEyedNerdy • vX.Y.Z`

## Planned release hardening
1. Authenticode code signing
2. signed installer
3. automated signature verification in CI
4. optional MSI/MSIX distribution if deployment requirements justify it
