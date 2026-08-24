#!/usr/bin/env python3
import argparse
import json
from pathlib import Path

parser = argparse.ArgumentParser()
parser.add_argument(
    "--repository",
    required=True,
    help="Public GitHub owner/repository that hosts Mission Control release assets",
)
parser.add_argument("--public-key", default="", help="NetSparkle Ed25519 public key (base64)")
args = parser.parse_args()

repo = args.repository.strip()
public_key = args.public_key.strip()

if not repo or "/" not in repo:
    raise SystemExit("--repository must look like owner/repository")

config = {
    "repository": repo,
    "publicKey": public_key,
    "stableFeedUrl": f"https://github.com/{repo}/releases/download/update-feed/appcast-stable.xml",
    "previewFeedUrl": f"https://github.com/{repo}/releases/download/update-feed/appcast-preview.xml",
}

path = Path("src/Nerdspace.OBSRecovery/Data/update-config.json")
path.write_text(json.dumps(config, indent=2) + "\n", encoding="utf-8")

if public_key:
    print(f"Configured signed self-update distribution for {repo}.")
else:
    print(
        f"Configured public release URLs for {repo}, but NETSPARKLE_PUBLIC_KEY is empty. "
        "The application will safely disable Update Now until signing is configured."
    )
