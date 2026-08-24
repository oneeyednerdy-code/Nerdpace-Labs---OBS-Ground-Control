#!/usr/bin/env python3
"""Build Ground Control's preloaded OBS plugin catalog from the official OBS resource directory.

Trust rules:
- Every catalog entry must be discovered from https://obsproject.com/forum/plugins/.
- The OBS resource page itself is the canonical resource link.
- A source URL is marked verified only when that URL is published by the OBS resource page.
- GitHub release checks are enabled only when that verified source URL resolves to a GitHub owner/repository.
- Module matching aliases come from maintainer overrides plus conservative exact-name derivation.
"""
from __future__ import annotations

import argparse
import html
import json
import re
import sys
import time
from pathlib import Path
from urllib.parse import urljoin, urlparse
from urllib.request import Request, urlopen

BASE = "https://obsproject.com"
DIRECTORY = f"{BASE}/forum/plugins/"
UA = "Nerdspace-OBS-Ground-Control-Catalog/0.7 (+https://github.com/)"
RESOURCE_RE = re.compile(r'href=["\'](?P<href>/forum/resources/[^"\'#?]+?\.(?P<id>\d+)/?)["\']', re.I)
TAG_RE = re.compile(r"<[^>]+>")
SPACE_RE = re.compile(r"\s+")


def fetch(url: str, retries: int = 3) -> str:
    last = None
    for attempt in range(retries):
        try:
            req = Request(url, headers={"User-Agent": UA, "Accept": "text/html,application/xhtml+xml"})
            with urlopen(req, timeout=25) as response:
                return response.read().decode("utf-8", "replace")
        except Exception as exc:
            last = exc
            if attempt + 1 < retries:
                time.sleep(1.0 + attempt)
    raise RuntimeError(f"Could not fetch {url}: {last}")


def clean(fragment: str) -> str:
    value = TAG_RE.sub(" ", fragment)
    return SPACE_RE.sub(" ", html.unescape(value)).strip()


def meta_content(page: str, name: str) -> str:
    patterns = [
        rf'<meta[^>]+property=["\']{re.escape(name)}["\'][^>]+content=["\']([^"\']*)["\']',
        rf'<meta[^>]+content=["\']([^"\']*)["\'][^>]+property=["\']{re.escape(name)}["\']',
        rf'<meta[^>]+name=["\']{re.escape(name)}["\'][^>]+content=["\']([^"\']*)["\']',
    ]
    for pattern in patterns:
        m = re.search(pattern, page, re.I)
        if m:
            return clean(m.group(1))
    return ""


def labelled_link(page: str, label: str) -> str:
    idx = page.lower().find(label.lower())
    if idx < 0:
        return ""
    window = page[idx: idx + 2500]
    m = re.search(r'href=["\']([^"\']+)["\']', window, re.I)
    return html.unescape(m.group(1)).strip() if m else ""


def labelled_text(page: str, label: str) -> str:
    idx = page.lower().find(label.lower())
    if idx < 0:
        return ""
    window = page[idx: idx + 1200]
    # Prefer the next dd/details-ish block, then fall back to cleaned nearby text.
    m = re.search(r'</dt>\s*<dd[^>]*>(.*?)</dd>', window, re.I | re.S)
    if m:
        return clean(m.group(1))
    return clean(window)[:300]


def extract_author(page: str) -> str:
    # XenForo resource pages commonly expose an Author label followed by a profile link.
    idx = page.lower().find("author")
    if idx >= 0:
        window = page[idx:idx + 1800]
        m = re.search(r'<a[^>]+href=["\']/forum/members/[^"\']+["\'][^>]*>(.*?)</a>', window, re.I | re.S)
        if m:
            return clean(m.group(1))
    return "Unknown"


def extract_title(page: str) -> str:
    title = meta_content(page, "og:title")
    if not title:
        m = re.search(r'<h1[^>]*>(.*?)</h1>', page, re.I | re.S)
        title = clean(m.group(1)) if m else "Unknown plugin"
    # OBS page titles often append " | OBS Forums".
    title = re.sub(r"\s*\|\s*OBS Forums\s*$", "", title, flags=re.I)
    return title.strip()


def extract_description(page: str) -> str:
    return meta_content(page, "og:description") or meta_content(page, "description") or "OBS Studio plugin resource."


def split_name_version(title: str) -> tuple[str, str | None]:
    # Resource headings usually append a dotted version. Use the last dotted numeric
    # token so names such as 4K Capture are not misidentified as versions.
    matches = list(re.finditer(r"(?:^|\s)(v?\d+(?:\.\d+){1,3}(?:[-+][A-Za-z0-9.-]+)?)", title, re.I))
    if not matches:
        return title.strip(), None
    match = matches[-1]
    version = match.group(1).lstrip("vV")
    name = (title[:match.start()] + title[match.end():]).strip(" -–—")
    return name or title.strip(), version


def github_repository(source_url: str) -> str | None:
    if not source_url:
        return None
    parsed = urlparse(source_url)
    if parsed.netloc.lower() not in {"github.com", "www.github.com"}:
        return None
    parts = [p for p in parsed.path.split("/") if p]
    if len(parts) < 2:
        return None
    repo = parts[1][:-4] if parts[1].endswith(".git") else parts[1]
    return f"{parts[0]}/{repo}"


def slug_from_url(url: str) -> str:
    m = re.search(r"/resources/([^/]+?)\.\d+/?$", url)
    return m.group(1) if m else ""


def derive_tokens(title: str, resource_url: str, repository: str | None) -> list[str]:
    values = []
    slug = slug_from_url(resource_url)
    if slug:
        values.append(slug)
    if repository:
        values.append(repository.split("/", 1)[-1])
    # Remove a trailing semantic-looking version from the page title for a conservative name token.
    name = re.sub(r"\s+v?\d+(?:\.\d+){1,3}(?:[-+][A-Za-z0-9.-]+)?\s*$", "", title, flags=re.I).strip()
    if len(name) >= 4:
        values.append(name)
    out = []
    seen = set()
    for value in values:
        value = value.strip().lower()
        if value and value not in seen:
            out.append(value)
            seen.add(value)
    return out


def load_overrides(path: Path) -> dict[str, dict]:
    if not path.exists():
        return {}
    raw = json.loads(path.read_text(encoding="utf-8"))
    return {str(item["resourceId"]): item for item in raw.get("overrides", [])}


def enumerate_resources(max_pages: int) -> list[tuple[str, str]]:
    found: dict[str, str] = {}
    empty_pages = 0
    for page_number in range(1, max_pages + 1):
        url = DIRECTORY if page_number == 1 else f"{DIRECTORY}?page={page_number}"
        body = fetch(url)
        page_new = 0
        for match in RESOURCE_RE.finditer(body):
            rid = match.group("id")
            absolute = urljoin(BASE, match.group("href"))
            if rid not in found:
                found[rid] = absolute
                page_new += 1
        print(f"OBS directory page {page_number}: {page_new} new resource(s)")
        if page_new == 0:
            empty_pages += 1
            if empty_pages >= 2:
                break
        else:
            empty_pages = 0
    return sorted(found.items(), key=lambda x: int(x[0]))


def build_entry(resource_id: str, url: str, override: dict | None) -> dict:
    page = fetch(url)
    title = extract_title(page)
    display_name, resource_version = split_name_version(title)
    author = extract_author(page)
    description = extract_description(page)
    source_url = labelled_link(page, "Source Code URL")
    repository = github_repository(source_url)
    minimum_obs = labelled_text(page, "Minimum OBS Studio Version")
    platforms_text = labelled_text(page, "Supported Platforms")
    supported = [p for p in ["Windows", "macOS", "Linux"] if p.lower() in platforms_text.lower()]
    tokens = derive_tokens(title, url, repository)
    if override:
        tokens = list(dict.fromkeys([*(override.get("matchTokens") or []), *tokens]))

    return {
        "id": resource_id,
        "displayName": display_name or title,
        "author": author,
        "repository": repository,
        "sourceUrl": source_url or None,
        "sourceVerification": "OBS resource page" if source_url else "No source URL published",
        "obsResourceUrl": url,
        "description": description,
        "resourceVersion": resource_version,
        "supportedPlatforms": supported,
        "minimumObsVersion": minimum_obs,
        "matchTokens": tokens,
    }


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--output", default="src/Nerdspace.OBSRecovery/Data/plugin-catalog.json")
    parser.add_argument("--overrides", default="data/plugin-catalog-overrides.json")
    parser.add_argument("--max-pages", type=int, default=40)
    parser.add_argument("--delay-ms", type=int, default=120)
    parser.add_argument("--require-min-resources", type=int, default=250)
    args = parser.parse_args()

    output = Path(args.output)
    overrides = load_overrides(Path(args.overrides))
    resources = enumerate_resources(args.max_pages)
    if len(resources) < args.require_min_resources:
        raise RuntimeError(f"Only {len(resources)} resources were discovered; refusing to replace the catalog because the directory crawl looks incomplete.")

    entries = []
    failures = []
    for index, (rid, url) in enumerate(resources, start=1):
        try:
            entry = build_entry(rid, url, overrides.get(rid))
            entries.append(entry)
            print(f"[{index}/{len(resources)}] {entry['displayName']} | source={entry['sourceVerification']}")
        except Exception as exc:
            failures.append({"id": rid, "url": url, "error": str(exc)})
            print(f"[{index}/{len(resources)}] ERROR {url}: {exc}", file=sys.stderr)
        if args.delay_ms:
            time.sleep(args.delay_ms / 1000.0)

    # Do not silently publish a badly incomplete catalog.
    if len(entries) < args.require_min_resources:
        raise RuntimeError(f"Only {len(entries)} resource pages parsed successfully; refusing to publish an incomplete catalog.")

    payload = {
        "generatedAtUtc": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()),
        "source": DIRECTORY,
        "resourceCount": len(entries),
        "sourceVerifiedCount": sum(1 for e in entries if e.get("sourceUrl")),
        "failedResourceCount": len(failures),
        "entries": sorted(entries, key=lambda e: e["displayName"].lower()),
        "failures": failures,
    }
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(payload, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    print(f"Wrote {len(entries)} OBS plugin resources to {output}; {payload['sourceVerifiedCount']} publish a source URL on the OBS resource page.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
