# GitHub Alpha Release Checklist — 0.7.0-alpha.9

## Repository
- [ ] Repository root contains `.github`, `src`, `installer`, `scripts`, and `README.md`.
- [ ] Source ZIP itself was not committed as the project root.
- [ ] `main` is pushed to GitHub.
- [ ] GitHub Actions is enabled.

## Main build
- [ ] `Build Windows` completes successfully.
- [ ] Artifact `Nerdspace-OBS-Ground-Control-Windows` is present.
- [ ] Setup EXE is present in the artifact.
- [ ] Portable ZIP is present in the artifact.

## Windows test
- [ ] Setup launches.
- [ ] App installs without requiring .NET separately.
- [ ] Start Menu shortcut launches Ground Control.
- [ ] Optional Desktop shortcut works.
- [ ] Footer shows `Nerdspace Labs by OneEyedNerdy • v0.7.0-alpha.9`.
- [ ] OBS detection works.
- [ ] Third-party plugin scanner excludes stock OBS modules.
- [ ] Plugin update comparison works for a known supported plugin.
- [ ] Plugin Discover loads the embedded catalog.
- [ ] Pre-Flight does not open OBS by default.
- [ ] Uninstall works.

## Public alpha
- [ ] Tag `v0.7.0-alpha.9` is created only after the main artifact is tested.
- [ ] `Release Windows` completes successfully.
- [ ] GitHub marks the alpha as a Pre-release.
- [ ] Setup EXE appears under Release Assets.
- [ ] Portable ZIP appears under Release Assets.
- [ ] `SHA256SUMS.txt` appears under Release Assets.
- [ ] Downloaded Setup EXE launches on the test machine.

## Do not publish as stable yet if
- [ ] the installer has not been tested on a second Windows machine
- [ ] Ground Control cannot reliably identify third-party plugins
- [ ] recovery behavior has not been tested with OBS open and closed
- [ ] a failed Pre-Flight can accidentally launch OBS
