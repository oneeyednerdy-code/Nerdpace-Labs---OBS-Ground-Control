# GitHub Alpha Release Checklist — 0.8.0-alpha.6

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
- [ ] Start Menu shortcut launches Mission Control.
- [ ] Optional Desktop shortcut works.
- [ ] Footer shows `NerdSpace Labs by OneEyedNerdy • v0.8.0-alpha.6`.
- [ ] OBS detection works.
- [ ] Third-party plugin scanner excludes stock OBS modules.
- [ ] Plugin update comparison works for a known supported plugin.
- [ ] Plugin Discover loads the embedded catalog.
- [ ] Pre-Flight does not open OBS by default.
- [ ] Uninstall works.

## Public alpha
- [ ] Tag `v0.8.0-alpha.6` is created only after the main artifact is tested.
- [ ] `Release Windows` completes successfully.
- [ ] GitHub marks the alpha as a Pre-release.
- [ ] Setup EXE appears under Release Assets.
- [ ] Portable ZIP appears under Release Assets.
- [ ] `SHA256SUMS.txt` appears under Release Assets.
- [ ] Downloaded Setup EXE launches on the test machine.

## Do not publish as stable yet if
- [ ] the installer has not been tested on a second Windows machine
- [ ] Mission Control cannot reliably identify third-party plugins
- [ ] recovery behavior has not been tested with OBS open and closed
- [ ] a failed Pre-Flight can accidentally launch OBS

## 0.8.0-alpha.6 creator-bot regression checks
- [ ] Mix It Up installed copy is detected and reports its executable version.
- [ ] Mix It Up compares against the official stable GitHub release.
- [ ] Streamer.bot portable copy is detected or can be supplied via Settings.
- [ ] Streamer.bot compares against the official stable Downloads page.
- [ ] Firebot installed copy is detected and reports its executable version.
- [ ] Firebot compares against the official stable `crowbartools/Firebot` release.
- [ ] Missing creator bots are informational, not errors.
- [ ] Update buttons open official destinations only.
- [ ] Check Everything includes all three creator bots.


## 0.8.0-alpha.6 self-update checks
- [ ] GitHub Actions variable `NETSPARKLE_PUBLIC_KEY` is configured.
- [ ] GitHub Actions secret `NETSPARKLE_PRIVATE_KEY` is configured.
- [ ] Versioned release completes successfully.
- [ ] Fixed `update-feed` release exists.
- [ ] `appcast-preview.xml` and `.signature` are present for alpha/beta releases.
- [ ] A stable release produces `appcast-stable.xml` and `.signature`.
- [ ] Check Now reports the installed and latest version.
- [ ] Update Now remains disabled when the feed/signature cannot be verified.
- [ ] Update Now downloads the installer and keeps the progress animation responsive.
- [ ] Mission Control fully exits before Inno Setup replaces installed files.
- [ ] Existing settings/backups remain after updating.
