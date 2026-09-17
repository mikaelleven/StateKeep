"""Build, package, install, and optionally publish a StateKeep release."""
from __future__ import annotations

import argparse
import os
import shutil
import subprocess
import sys
import zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
PROJECT = ROOT / "src" / "StateKeep" / "StateKeep.csproj"
RELEASES = ROOT / "releases"
sys.path.insert(0, str(Path(__file__).resolve().parent))
from version import bump, load


def version_text(version: dict[str, int]) -> str:
    return "{major}.{minor}.{patch}.{build}".format(**version)


def run(command: list[str]) -> None:
    print("+ " + " ".join(command))
    subprocess.run(command, cwd=ROOT, check=True)


def ensure_publish_ready() -> None:
    result = subprocess.run(
        ["git", "status", "--porcelain"], cwd=ROOT, check=True, capture_output=True, text=True
    )
    changed_paths = [line[3:] for line in result.stdout.splitlines()]
    unexpected_changes = [path for path in changed_paths if path != "version.json"]
    if unexpected_changes:
        raise RuntimeError(
            "Refusing to publish with uncommitted changes outside version.json: "
            + ", ".join(unexpected_changes)
        )
    if shutil.which("gh") is None:
        raise RuntimeError("GitHub CLI (gh) is required to publish a release. Install it from https://cli.github.com/.")
    auth = subprocess.run(["gh", "auth", "status"], cwd=ROOT, capture_output=True, text=True, check=False)
    if auth.returncode:
        raise RuntimeError("GitHub CLI is not authenticated. Run 'gh auth login' before publishing.")


def add_to_user_path(path: Path) -> None:
    """Add path to the Windows user PATH without duplicating equivalent entries."""
    import winreg

    path_text = str(path)
    canonical = os.path.normcase(os.path.normpath(path_text.rstrip("\\/")))
    with winreg.OpenKey(winreg.HKEY_CURRENT_USER, r"Environment", 0, winreg.KEY_READ | winreg.KEY_WRITE) as key:
        try:
            current, value_type = winreg.QueryValueEx(key, "Path")
        except FileNotFoundError:
            current, value_type = "", winreg.REG_EXPAND_SZ
        entries = [entry for entry in str(current).split(";") if entry.strip()]
        if not any(os.path.normcase(os.path.normpath(entry.strip().strip('"').rstrip("\\/"))) == canonical for entry in entries):
            entries.append(path_text)
            winreg.SetValueEx(key, "Path", 0, value_type, ";".join(entries))
            print(f"Added StateKeep to your user PATH: {path_text}")
        os.environ["PATH"] = ";".join(entries)


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
    parser.add_argument("--publish", action="store_true", default=True, help="Publish the release (the default).")
    parser.add_argument("--no-publish", action="store_false", dest="publish", help="Create artifacts without publishing them.")
    parser.add_argument("--nobump", action="store_true", help="Package the current version without changing it.")
    args = parser.parse_args()

    if args.publish and args.nobump:
        parser.error("--publish cannot be used with --nobump.")
    if args.publish:
        ensure_publish_ready()

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
        # Keep the destination directory so rerunning the installer does not fail
        # if an existing executable is in use or removal is otherwise blocked.
        shutil.copytree(publish_dir, install_dir, dirs_exist_ok=True)
        add_to_user_path(install_dir)
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
