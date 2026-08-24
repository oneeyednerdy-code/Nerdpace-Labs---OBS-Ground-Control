# Plugin Registry

OBS Ground Control ships a **preloaded snapshot of the official OBS Studio Plugins resource catalog**.

The catalog is generated at release time from the official OBS resource directory rather than being maintained as a small handwritten list.

## Verification levels

### OBS resource verified

Every shipped catalog entry was enumerated from the official OBS Studio Plugins category and carries its canonical OBS resource page.

### Source verified by OBS resource page

A source URL is marked verified only when that plugin's official OBS resource page publishes a **Source Code URL**.

Ground Control does not infer official source repositories from:

- search-engine results;
- similarly named GitHub repositories;
- forks or mirrors;
- DLL filenames alone;
- third-party download sites.

If a resource has no published Source Code URL, it remains available in Discover but Ground Control does not label a repository as official and does not perform a live GitHub release comparison for it.

## Installed matching

The full catalog is excellent for discovery, but identifying an installed DLL safely is a different problem. `data/plugin-catalog-overrides.json` contains maintainer-reviewed module aliases for plugins where the local module identity is known.

The catalog generator also derives conservative name/repository tokens. Ground Control never uses catalog presence alone as proof that a similarly named local DLL is the same plugin.

## Updates

For installed plugins with an OBS-page-verified GitHub repository, Ground Control can compare:

```text
Installed version -> latest verified GitHub release
```

and open the exact release page.

For source URLs hosted somewhere other than GitHub, Ground Control opens the OBS-page-verified source but leaves version comparison as a manual check until a provider-specific adapter exists.

## Discovery

Discover searches the embedded catalog by plugin name, author, description, source URL, repository, and known module aliases.

Because Ground Control is currently Windows-only, resources explicitly marked as non-Windows are hidden from normal discovery. Resources whose platform metadata is absent remain visible rather than being guessed incompatible.

The **Browse Official OBS Plugins** action always opens the live OBS directory for the newest resources added after the current Ground Control release.

See `PLUGIN-CATALOG-BUILD.md` for the release-time generation process.
