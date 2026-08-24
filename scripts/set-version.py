#!/usr/bin/env python3
from pathlib import Path
import re
import sys

if len(sys.argv) != 2:
    raise SystemExit("Usage: python scripts/set-version.py MAJOR.MINOR.PATCH")

version = sys.argv[1].strip()
if version.startswith("v"):
    version = version[1:]

match = re.fullmatch(r"(\d+)\.(\d+)\.(\d+)(?:-([0-9A-Za-z.-]+))?", version)
if not match:
    raise SystemExit(f"Invalid SemVer: {version}")

numeric = ".".join(match.group(i) for i in range(1, 4))
project = Path(__file__).resolve().parents[1] / "src" / "Nerdspace.OBSRecovery" / "Nerdspace.OBSRecovery.csproj"
text = project.read_text(encoding="utf-8")

replacements = {
    "Version": version,
    "AssemblyVersion": f"{numeric}.0",
    "FileVersion": f"{numeric}.0",
    "InformationalVersion": version,
}

for tag, value in replacements.items():
    text, count = re.subn(fr"<{tag}>.*?</{tag}>", f"<{tag}>{value}</{tag}>", text, count=1)
    if count != 1:
        raise SystemExit(f"Could not update <{tag}> in {project}")

project.write_text(text, encoding="utf-8")
print(f"Set Nerdspace OBS Ground Control version to {version}")
