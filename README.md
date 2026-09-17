# StateKeep

[![License: MIT](https://img.shields.io/badge/license-MIT-green)](LICENSE)
[![Status: beta](https://img.shields.io/badge/status-beta-blue)](https://github.com/mikaelleven/StateKeep/releases)
[![Downloads](https://img.shields.io/github/downloads/mikaelleven/StateKeep/total?label=downloads&color=yellow)](https://github.com/mikaelleven/StateKeep/releases)

StateKeep is a Windows command-line tool that automatically backs up and restores application settings. Point it at **any folder**—ideally a cloud-synchronized drive such as OneDrive or a NAS share—and StateKeep keeps a readable copy of your supported app configurations there.

It is designed to be set up once and then left to run unattended. Use it to:

- recover from accidental configuration changes;
- restore settings after reinstalling Windows or an application; and
- transfer application settings between computers through a shared backup folder.

StateKeep stores ordinary files rather than proprietary archives or databases, so backups remain understandable and accessible. It backs up application configuration only; it is **not** a replacement for a full-system backup.

## Installation

### Prerequisites

To install the official release, you need:

- 64-bit Windows;
- Windows PowerShell, which is included with supported Windows versions; and
- an internet connection that can reach GitHub to download the installer and release.

No separate dependencies need to be installed. The official release is
self-contained and includes its .NET runtime; you do **not** need the .NET
runtime or SDK, Python, GitHub CLI, or administrator privileges.

### Install the latest release

Open PowerShell and run:

```powershell
irm https://raw.githubusercontent.com/mikaelleven/StateKeep/main/scripts/install.ps1 | iex
```

The installer downloads and SHA-256 verifies the latest GitHub release, installs it to `%LOCALAPPDATA%\StateKeep`, and adds that folder to your user `PATH`. Open a new terminal when it finishes.

> [!WARNING]
> This one-liner downloads and executes the installer directly from GitHub. Review the [installer script](https://raw.githubusercontent.com/mikaelleven/StateKeep/main/scripts/install.ps1) before running it, and run it only if you trust the source and its release process.


## Basic usage

### 1. Choose a backup folder

Run setup once. The folder can be any writable location; a cloud-synchronized folder or NAS directory makes backups available on other computers.

```powershell
statekeep setup "%ONEDRIVE%\MyBackups" # Special folder alias for OneDrive
statekeep setup . # Use current directory
statekeep setup "C:\MyBackups" # Explicit path
statekeep validate
```

### 2. Back up your settings

Back up all detected configured applications:

```powershell
statekeep backup
```

Your backup is stored in a per-device folder, for example:

```text
%ONEDRIVE%\MyBackups\DESKTOP_A7K3M9Q2\apps\...
```

### 3. Restore an application

Restore settings from this computer’s backup:

```powershell
statekeep restore zed
```

Run `statekeep --help` to see every command and supported application ID.

## Advanced usage

### Run automatic backups

Install or update the Windows Task Scheduler task to make backups unattended:

```powershell
statekeep install task
```

The command is idempotent: running it again updates the existing StateKeep task instead of adding duplicates. See [Scheduling](docs/SCHEDULING.md) for task behavior and silent execution details.

### Restore from another computer

When computers use the same backup folder, restore an app by its device ID, computer name, or full backup-folder name:

```powershell
statekeep restore zed --from OTHER-PC
statekeep restore zed --from OTHER-PC_A7K3M9Q2
```

Restore every configured application with `--all`:

```powershell
statekeep restore --all --from OTHER-PC
```

Preview a restore before changing files with `--dryrun`. Use `--force` only when you intend to overwrite conflicts:

```powershell
statekeep restore zed --from OTHER-PC --dryrun
statekeep restore zed --force
```

See the [CLI specification](docs/CLI-SPEC.md) and [restore-safety guidance](docs/RESTORE-SAFETY.md) for complete command and conflict-handling details.

## Customization

StateKeep’s supported applications are described by human-readable YAML definition files in the `apps\` directory alongside the executable. To add an application, create a definition with a unique ID, its configuration root paths, and evidence files or directories that identify the correct location.

Start with [App Configuration Specification](docs/APP-CONFIG-SPEC.md) for the complete schema, path rules, file filters, and examples. Validate definitions after editing them:

```powershell
statekeep validate
```

## Additional details

- [Developer guide](docs/DEVELOPER.md) — local setup, development, and release workflow
- [Architecture](docs/ARCHITECTURE.md) — system design and technical decisions
- [Requirements](docs/REQUIREMENTS.md) — functional and non-functional requirements
- [CLI specification](docs/CLI-SPEC.md) — commands and output behavior
- [App configuration specification](docs/APP-CONFIG-SPEC.md) — application-definition format
- [Restore safety](docs/RESTORE-SAFETY.md) — conflicts and restore semantics
- [Scheduling](docs/SCHEDULING.md) — scheduled task installation and silent execution

## License

StateKeep is distributed under the [MIT License](LICENSE).
