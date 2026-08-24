# Windows signing plan

OBS Ground Control v0.7.0-alpha.9 is Windows-only.

The source/release workflow intentionally does not contain signing credentials. Public releases should eventually use Authenticode signing through a protected CI identity.

Recommended release order:
1. build Windows x64
2. run automated tests
3. sign the published executable/binaries
4. verify Authenticode signatures
5. package the release or installer
6. sign the MSI/MSIX if used
7. generate SHA-256 checksums
8. publish the GitHub Release

Never commit certificate private keys, PFX passwords, Azure credentials, or signing secrets to the repository.
