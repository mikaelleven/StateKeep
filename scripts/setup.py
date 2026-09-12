#!/usr/bin/env python3
"""Cross-platform development support entry point."""
from __future__ import annotations

from pathlib import Path
import shutil
import subprocess
import sys


ROOT = Path(__file__).resolve().parent.parent
PROJECT_FILE = ROOT / "src" / "StateKeep" / "StateKeep.csproj"
COMMAND = ["dotnet", "restore", str(PROJECT_FILE)]


def main() -> int:
    if not COMMAND:
        print("No automated support setup is defined for this project.")
        return 0
    if shutil.which(COMMAND[0]) is None:
        print(f"{COMMAND[0]} is required for support setup.", file=sys.stderr)
        return 1
    return subprocess.run(COMMAND, cwd=ROOT, check=False).returncode


if __name__ == "__main__":
    raise SystemExit(main())
