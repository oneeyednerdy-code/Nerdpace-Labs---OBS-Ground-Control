# OBS Plugin Catalog Build

Ground Control ships a preloaded snapshot of the official **OBS Studio Plugins** resource category.

## Trust model

A catalog record has two independent trust states:

1. **OBS resource verified** — the resource was enumerated from `https://obsproject.com/forum/plugins/`.
2. **Source verified by OBS resource page** — the plugin's OBS resource page itself publishes a `Source Code URL`.

Ground Control does **not** infer a source repository from search engines, filenames, forks, mirrors, or similarly named projects.

If no Source Code URL is published, the plugin remains searchable in Discover but the Source / Releases action is disabled and automatic release-version lookup is not attempted.

## Release-time generation

GitHub release builds run:

```powershell
python scripts/refresh-plugin-catalog.py --require-min-resources 250
```

The generator:

1. enumerates the OBS Studio Plugins category pages;
2. de-duplicates official resource IDs;
3. reads each official resource page;
4. records author, description, platform metadata, minimum OBS version, and official resource URL where available;
5. records `Source Code URL` only when published by the OBS resource page;
6. recognizes GitHub repositories for safe GitHub Releases lookups;
7. merges conservative module matching aliases from `data/plugin-catalog-overrides.json`;
8. writes `src/Nerdspace.OBSRecovery/Data/plugin-catalog.json`;
9. refuses to replace the catalog if the crawl appears badly incomplete.

The JSON is embedded into the self-contained Windows application at compile time. Ground Control does not need to scrape the OBS website at runtime to display Discover results.

## Local refresh

Before a local release build, run:

```powershell
python scripts/refresh-plugin-catalog.py
```

Then publish normally.

A committed fallback catalog remains in the repository so development builds still work if the OBS site is temporarily unavailable. Public release builds require a successful full refresh.
