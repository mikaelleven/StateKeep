"""Build and package a StateKeep release."""
from __future__ import annotations

import argparse
import shutil
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(Path(__file__).resolve().parent))
from version import load  # noqa: E402


def main() -> int:
    parser = argparse.ArgumentParser(add_help=False)
    parser.add_argument("--local", action="store_true")
    parser.add_argument("--verbose", action="store_true")
    args, unknown = parser.parse_known_args()

    build_arguments = list(unknown)
    if "--configuration" not in build_arguments and "-c" not in build_arguments:
        build_arguments.extend(("--configuration", "Release"))
    build_command = [str(ROOT / "build.cmd"), *build_arguments]
    if args.verbose:
        build_command.append("--verbose")
    result = subprocess.run(build_command, cwd=ROOT)
    if result.returncode:
        return result.returncode

    version = load()
    version_text = "{major}.{minor}.{patch}.{build}".format(**version)
    destination = ROOT / "releases" / f"{version_text}"
    if destination.exists():
        shutil.rmtree(destination)
    destination.mkdir(parents=True)
    binary_source = ROOT / "src" / "StateKeep" / "bin" / "Debug" / "net8.0"
    shutil.copytree(binary_source, destination, dirs_exist_ok=True)
    shutil.copytree(ROOT / "apps", destination / "apps", dirs_exist_ok=True)

    if args.local:
        local = Path.home() / "AppData" / "Local" / "StateKeep"
        local.mkdir(parents=True, exist_ok=True)
        shutil.copytree(binary_source, local, dirs_exist_ok=True)
        shutil.copytree(ROOT / "apps", local / "apps", dirs_exist_ok=True)

    if args.verbose:
        print(f"Version: {version_text}")
        print(f"Destination: {destination}")
        print("Artifacts:")
        for item in sorted(destination.rglob("*")):
            if item.is_file():
                print(f"  {item.relative_to(destination)}")
        if args.local:
            print(f"Local installation: {local}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
