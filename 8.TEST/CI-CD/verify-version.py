#!/usr/bin/env python3
"""Verify that the release version is identical in every source and package file."""
from __future__ import annotations

import argparse
import json
import re
import sys
from pathlib import Path


def read(path: Path) -> str:
    return path.read_text(encoding="utf-8")


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--output", help="Optional 10.OUTPUT/<version> directory")
    args = parser.parse_args()

    root = Path(__file__).resolve().parents[2]
    src = root / "9.SRC"
    manifest = json.loads(read(src / "manifest.json"))
    settings = json.loads(read(src / "settings.cfg"))
    csproj = read(src / "sts2_statsthespire_local.csproj")
    config = read(src / "src" / "Config" / "ModConfig.cs")
    zh = read(src / "README_ZH.md")
    en = read(src / "README_EN.md")

    versions = {
        "manifest.json": manifest.get("version"),
        "settings.cfg": settings.get("version"),
        "csproj <Version>": re.search(r"<Version>([^<]+)</Version>", csproj).group(1),
        "ModConfig.ModVersion": re.search(r'ModVersion\s*=\s*"([^"]+)"', config).group(1),
        "README_ZH": re.search(r"^\*\*版本\*\*:\s*([^\s]+)", zh, re.M).group(1),
        "README_EN": re.search(r"^\*\*Version\*\*:\s*([^\s]+)", en, re.M).group(1),
    }
    if args.output:
        out = Path(args.output)
        pkg_manifest = json.loads(read(out / "manifest.json"))
        pkg_settings = json.loads(read(out / "settings.cfg"))
        versions["package manifest"] = pkg_manifest.get("version")
        versions["package settings.cfg"] = pkg_settings.get("version")
        if not (out / "sts2_statsthespire_local.dll").is_file():
            versions["package DLL"] = "MISSING"
        versions["package README_ZH"] = re.search(
            r"^\*\*版本\*\*:\s*([^\s]+)", read(out / "README_ZH.md"), re.M
        ).group(1)
        versions["package README_EN"] = re.search(
            r"^\*\*Version\*\*:\s*([^\s]+)", read(out / "README_EN.md"), re.M
        ).group(1)

    unique = set(versions.values())
    if len(unique) != 1 or "MISSING" in unique:
        for name, value in versions.items():
            print(f"{name}: {value}")
        return 1

    version = unique.pop()
    print(f"version-ok {version}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
