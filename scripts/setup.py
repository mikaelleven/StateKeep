#!/usr/bin/env python3
"""Cross-platform development support entry point."""
from __future__ import annotations

import shutil
import subprocess
import sys


COMMAND = ['dotnet', 'restore']


def main() -> int:
    if not COMMAND:
        print("No automated support setup is defined for this project.")
        return 0
    if shutil.which(COMMAND[0]) is None:
        print(f"{COMMAND[0]} is required for support setup.", file=sys.stderr)
        return 1
    return subprocess.run(COMMAND, check=False).returncode


if __name__ == "__main__":
    raise SystemExit(main())
