# Plugin Manifest Intelligence — v0.8.0-alpha.9

Streamer Mission Control now identifies installed OBS plugins **manifest first** instead of requiring every plugin to be hardcoded into Mission Control's catalog.

## Identification order

1. **OBS `manifest.json`** — primary source when present.
   - `id`
   - `display_name`
   - `name`
   - `version`
   - `description`
   - `urls.repository`
   - `urls.website`
   - `urls.support`
2. **Windows DLL version metadata** — fallback name/version/publisher information.
3. **Mission Control OBS catalog** — legacy fallback and cross-check for plugins that do not publish a manifest/source URL.
4. **Official OBS plugin directory** — final browse fallback when no plugin-specific source can be identified.

Mission Control reads plugin files statically. It does **not** load or initialize unknown plugin DLLs during inventory.

## Update lookup

When `urls.repository` points to GitHub, Mission Control normalizes the URL to `owner/repository` and can check the repository's latest release directly.

When a manifest publishes a non-GitHub repository/source URL, Mission Control preserves it as the plugin's source and offers a manual update/source action instead of guessing a release API.

The UI reports the evidence used:

- `OBS manifest`
- `OBS manifest + catalog match`
- `Official OBS catalog`
- `OBS catalog metadata`
- `DLL metadata`

A manifest is plugin-declared metadata, so Mission Control labels it as such rather than pretending it is independently authenticated.

## Find Plugins

The old passive **Discover** view is now **Find Plugins**.

Search is ranked across:

- plugin name
- author
- description/features
- catalog match terms
- repository/source metadata

**Get Plugin** opens, in priority order:

1. refreshed latest release URL;
2. official source/repository URL;
3. official OBS resource page.

Mission Control does not automatically execute arbitrary third-party plugin installers yet. OBS plugins can be distributed as EXE, MSI, ZIP, or custom packages with different dependencies and privilege requirements. A future installer engine must validate the package type and destination before offering one-click installation.

## Why keep the catalog?

The catalog is no longer required for modern installed-plugin identification. It remains useful as:

- the Find Plugins search index;
- a legacy fallback for plugins without manifests;
- a cross-check that can raise source confidence when manifest metadata and the official catalog agree.
