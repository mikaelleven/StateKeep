"""Read and update the StateKeep version file."""
from __future__ import annotations

import argparse
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
VERSION_FILE = ROOT / "version.json"


def load() -> dict[str, int]:
    with VERSION_FILE.open(encoding="utf-8") as stream:
        version = json.load(stream)
    return {part: int(version.get(part, 0)) for part in ("major", "minor", "patch", "build")}


def save(version: dict[str, int]) -> None:
    VERSION_FILE.write_text(json.dumps(version, indent=2) + "\n", encoding="utf-8")


def bump(part: str) -> dict[str, int]:
    version = load()
    if part == "major":
        version["major"] += 1
        version["minor"] = version["patch"] = version["build"] = 0
    elif part == "minor":
        version["minor"] += 1
        version["patch"] = version["build"] = 0
    elif part == "patch":
        version["patch"] += 1
        version["build"] = 0
    else:
        version["build"] += 1
    save(version)
    return version


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("action", choices=("bump", "show"))
    parser.add_argument("part", nargs="?", default="build", choices=("major", "minor", "patch", "build"))
    args = parser.parse_args()
    version = bump(args.part) if args.action == "bump" else load()
    print("{major}.{minor}.{patch}.{build}".format(**version))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
