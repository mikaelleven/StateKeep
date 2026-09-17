"""Build, package, install, and optionally publish a StateKeep release."""
from __future__ import annotations

import argparse
import shutil
import subprocess
import sys
import zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
PROJECT = ROOT / "src" / "StateKeep" / "StateKeep.csproj"
RELEASES = ROOT / "releases"
sys.path.insert(0, str(Path(__file__).resolve().parent))
from version import bump, load  # noqa: E402


def version_text(version: dict[str, int]) -> str:
    return "{major}.{minor}.{patch}.{build}".format(**version)


def run(command: list[str]) -> None:
    print("+ " + " ".join(command))
    subprocess.run(command, cwd=ROOT, check=True)


def ensure_clean_worktree() -> None:
    result = subprocess.run(
        ["git", "status", "--porcelain"], cwd=ROOT, check=True, capture_output=True, text=True
    )
    if result.stdout.strip():
        raise RuntimeError("Refusing to publish from a working tree with uncommitted changes.")


def publish(version: str, archive: Path, checksum: Path) -> None:
    tag = f"v{version}"
    run(["git", "add", "version.json"])
    run(["git", "commit", "-m", f"Release {tag}"])
    run(["git", "tag", "-a", tag, "-m", f"StateKeep {tag}"])
    run(["git", "push", "origin", "HEAD"])
    run(["git", "push", "origin", tag])
    run([
        "gh", "release", "create", tag, str(archive), str(checksum),
        "--title", f"StateKeep {tag}", "--generate-notes",
    ])


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    bump_group = parser.add_mutually_exclusive_group()
    for part in ("major", "minor", "patch", "build"):
        bump_group.add_argument(f"--{part}", action="store_const", dest="bump_part", const=part)
    parser.add_argument("--local", action="store_true", help="Install the packaged release locally.")
    parser.add_argument("--publish", action="store_true", help="Commit, tag, push, and create a GitHub release.")
    parser.add_argument("--nobump", action="store_true", help="Package the current version without changing it.")
    args = parser.parse_args()

    if args.publish and args.nobump:
        parser.error("--publish cannot be used with --nobump.")
    if args.publish:
        ensure_clean_worktree()

    version = load() if args.nobump else bump(args.bump_part or "build")
    version = version_text(version)
    publish_dir = RELEASES / f"StateKeep-{version}-win-x64"
    archive = RELEASES / f"StateKeep-{version}-win-x64.zip"
    checksum = RELEASES / f"StateKeep-{version}-win-x64.zip.sha256"

    shutil.rmtree(publish_dir, ignore_errors=True)
    archive.unlink(missing_ok=True)
    checksum.unlink(missing_ok=True)
    RELEASES.mkdir(exist_ok=True)

    run([
        "dotnet", "publish", str(PROJECT), "--configuration", "Release",
        "--runtime", "win-x64", "--self-contained", "true",
        "--output", str(publish_dir),
        f"/p:Version={version}", f"/p:InformationalVersion={version}",
        "/p:PublishSingleFile=true", "/p:IncludeNativeLibrariesForSelfExtract=true",
        "/p:DebugType=None", "/p:DebugSymbols=false", "/p:VersionBumpHandled=true",
    ])

    with zipfile.ZipFile(archive, "w", compression=zipfile.ZIP_DEFLATED) as package:
        for path in sorted(publish_dir.rglob("*")):
            if path.is_file():
                package.write(path, path.relative_to(publish_dir))
    checksum.write_text(f"{__import__('hashlib').sha256(archive.read_bytes()).hexdigest()}  {archive.name}\n", encoding="ascii")

    if args.local:
        install_dir = Path.home() / "AppData" / "Local" / "StateKeep"
        shutil.rmtree(install_dir, ignore_errors=True)
        shutil.copytree(publish_dir, install_dir)
        print(f"Installed locally: {install_dir}")

    print(f"Created release artifact: {archive}")
    if args.publish:
        publish(version, archive, checksum)
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (OSError, subprocess.CalledProcessError, RuntimeError) as error:
        print(f"Release failed: {error}", file=sys.stderr)
        raise SystemExit(1)
