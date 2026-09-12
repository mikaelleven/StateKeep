"""Build and package a StateKeep release."""
from __future__ import annotations

import argparse
import shutil
import subprocess
import sys
from datetime import datetime
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(Path(__file__).resolve().parent))
from version import load  # noqa: E402


def main() -> int:
    parser = argparse.ArgumentParser(add_help=False)
    parser.add_argument("--local", action="store_true")
    parser.add_argument("--nobuild", action="store_true")
    parser.add_argument("--verbose", action="store_true")
    args, unknown = parser.parse_known_args()

    build_arguments = list(unknown)
    configuration = next(
        (build_arguments[index + 1] for index, argument in enumerate(build_arguments[:-1])
         if argument in ("--configuration", "-c")),
        None,
    )
    if configuration is None:
        configuration = "Release"
        build_arguments.extend(("--configuration", configuration))

    if not args.nobuild:
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
    binary_root = ROOT / "src" / "StateKeep" / "bin"
    binary_source = binary_root / configuration / "net8.0"
    if args.nobuild and not binary_source.is_dir():
        candidates = [path for path in binary_root.glob("*/net8.0") if path.is_dir()]
        if candidates:
            binary_source = max(candidates, key=lambda path: path.stat().st_mtime)
            configuration = binary_source.parent.name
    if not binary_source.is_dir():
        print(f"Build output was not found: {binary_source}", file=sys.stderr)
        return 1
    shutil.copytree(binary_source, destination, dirs_exist_ok=True)
    shutil.copytree(ROOT / "apps", destination / "apps", dirs_exist_ok=True)

    local = None
    if args.local:
        local = Path.home() / "AppData" / "Local" / "StateKeep"
        local.mkdir(parents=True, exist_ok=True)
        shutil.copytree(binary_source, local, dirs_exist_ok=True)
        shutil.copytree(ROOT / "apps", local / "apps", dirs_exist_ok=True)

    if args.verbose:
        print(f"Version: {version_text}")
        print(f"Destination: {destination}")
        print(f"Build output: {binary_source}")
        print(f"Build output time: {datetime.fromtimestamp(binary_source.stat().st_mtime).isoformat(sep=' ', timespec='seconds')}")
        print("Artifacts:")
        for item in sorted(destination.rglob("*")):
            if item.is_file():
                print(f"  {item.relative_to(destination)}")
        if args.local:
            print(f"Local installation: {local}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
