"""Build StateKeep with its persistent four-part version."""
from __future__ import annotations

import argparse
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
PROJECT = ROOT / "src" / "StateKeep" / "StateKeep.csproj"
sys.path.insert(0, str(Path(__file__).resolve().parent))
from version import bump, load  # noqa: E402


def main() -> int:
    parser = argparse.ArgumentParser(add_help=False)
    parser.add_argument("--major", action="store_true")
    parser.add_argument("--minor", action="store_true")
    parser.add_argument("--patch", action="store_true")
    parser.add_argument("--build", action="store_true")
    parser.add_argument("--verbose", action="store_true")
    args, passthrough = parser.parse_known_args()

    requested = [name for name in ("major", "minor", "patch", "build") if getattr(args, name)]
    version = bump(requested[0]) if requested else bump("build")
    version_text = "{major}.{minor}.{patch}.{build}".format(**version)
    command = [
        "dotnet", "build", str(PROJECT), *passthrough,
        f"/p:Version={version_text}",
        f"/p:InformationalVersion={version_text}",
        "/p:VersionBumpHandled=true",
    ]
    if args.verbose:
        print(f"Version: {version['major']}.{version['minor']}.{version['patch']}.{version['build']}")
        print("Command: " + " ".join(command))
    result = subprocess.run(command, cwd=ROOT)
    if result.returncode == 0 and args.verbose:
        output = ROOT / "src" / "StateKeep" / "bin"
        print(f"Artifacts: {output}")
    return result.returncode


if __name__ == "__main__":
    raise SystemExit(main())
