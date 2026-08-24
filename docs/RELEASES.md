# Releases

OBS Ground Control currently publishes **Windows x64 only**.

The release workflow triggers on tags matching:

`v*.*.*`

Example:

```powershell
git tag v0.7.0-alpha.1
git push origin v0.7.0-alpha.1
```

Release artifacts:
- `Nerdspace-OBS-Ground-Control-vX.Y.Z-win-x64.zip`
- `SHA256SUMS.txt`

## Version synchronization

The release workflow runs the repository version script before compilation. The same assembly version is used by the executable and the footer:

`Nerdspace Labs by OneEyedNerdy • vX.Y.Z`

## Planned release hardening
1. Authenticode code signing
2. MSI/MSIX installer
3. signed installer
4. automated signature verification in CI
