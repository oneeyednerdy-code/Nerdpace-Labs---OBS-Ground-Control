# Start Here — Publish Streamer Mission Control to GitHub

Current alpha: **v0.8.0-alpha.8**

1. Create a GitHub repository named `nerdspace-obs-ground-control`.
2. Push the **contents of this folder** to the repository.
3. Open GitHub → Actions and wait for **Build Windows**.
4. Download and test the `Nerdspace-OBS-Ground-Control-Windows` artifact.
5. When the installer passes your alpha test, run:

```powershell
git tag v0.8.0-alpha.8
git push origin v0.8.0-alpha.8
```

6. Open GitHub → Actions → **Release Windows**.
7. When it succeeds, open GitHub → Releases.
8. The recommended download is:

`Nerdspace-OBS-Ground-Control-Setup-v0.8.0-alpha.8.exe`

Detailed instructions:

`docs/GITHUB-PUBLISH-GUIDE.md`
