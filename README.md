# StateKeep

StateKeep is a Windows-only command-line tool for backing up and restoring application settings to a cloud-synchronized folder such as OneDrive.

The tool is intentionally simple: it reads declarative per-application definitions, locates known application settings folders, filters the relevant files, and mirrors the resulting application state into a per-device backup folder.

## Purpose

StateKeep exists to make application configuration easy to preserve, version through the cloud provider, and restore after a fresh Windows installation or accidental configuration change.

It is **not** intended to replace a full-system backup product.

## Goals

1. Run backups and restores manually or unattended.
2. Keep application-specific knowledge in human-readable configuration files.
3. Store backups in a per-device folder while preserving each application's relative path structure.
4. Make backups understandable and recoverable without proprietary archives or databases.
5. Favor stability, resilience, and robustness, following KISS and YAGNI over adding features or visual design.

## Example

```powershell
statekeep backup
statekeep restore vscode
statekeep install task
```

## Installation

### Install the latest release

Open PowerShell and run:

```powershell
irm https://raw.githubusercontent.com/mikaelleven/StateKeep/main/scripts/install.ps1 | iex
```

The installer downloads and SHA-256 verifies the latest GitHub release, extracts it to
`%LOCALAPPDATA%\StateKeep`, and adds that folder to your user `PATH`. Open a
new terminal afterwards, then configure a cloud-synchronized backup location:

```powershell
statekeep setup "$env:OneDrive\MyBackups"
statekeep validate
```

Run `statekeep --help` to see all commands. Re-run the installer to update an
existing installation. To install somewhere else or leave `PATH` unchanged,
download `install.ps1` and invoke it with `-InstallPath` or `-NoPathUpdate`.

### Build a local release

Developers can create a self-contained Windows x64 release ZIP and install it
locally with one command:

```cmd
release.cmd --local
```

This increments the build version in `version.json` and creates
`releases\StateKeep-<version>-win-x64.zip`. The ZIP contains only the runtime
executable and bundled application definitions; it excludes source and build
scripts.

Project maintainers publish a version from GitHub’s **Actions → Release → Run
workflow** page. Select the semantic version component to increment; the
workflow builds the release, commits the version update, tags it as
`v<version>`, and creates the GitHub release with the ZIP and SHA-256 checksum.

## Restore

Restore one application from this computer's backup folder:

```powershell
statekeep restore zed
```

To restore from another computer or device, provide its device ID, computer
name, or full backup folder name with `--from`:

```powershell
statekeep restore zed --from MYCOMPUTER
statekeep restore zed --from OTHER-PC_A7K3M9Q2
```

Restore all configured applications with `--all`. The optional `--from` form
also works when restoring all applications:

```powershell
statekeep restore --all
statekeep restore --all --from MYCOMPUTER
```

Use an application ID instead of `--all`, but not both. Add `--dryrun` to
preview the files that would be restored without changing the destination:

```powershell
statekeep restore zed --dryrun
statekeep restore --all --from MYCOMPUTER --dryrun
```

If no matching backup files exist for the selected application on the chosen
computer/device, StateKeep displays a warning and reports zero files restored.

Default application definitions are bundled with StateKeep under:

```text
.\apps\
```

Example backup destination:

```text
%ONEDRIVE%\MyBackups\DESKTOP_A7K3M9Q2\apps\...
```

## License

StateKeep is distributed under the [MIT License](LICENSE).

## Documentation

- [ARCHITECTURE.md](docs/ARCHITECTURE.md) — system architecture and design decisions
- [REQUIREMENTS.md](docs/REQUIREMENTS.md) — functional and non-functional requirements
- [APP-CONFIG-SPEC.md](docs/APP-CONFIG-SPEC.md) — per-application definition format
- [CLI-SPEC.md](docs/CLI-SPEC.md) — command-line interface and output behavior
- [RESTORE-SAFETY.md](docs/RESTORE-SAFETY.md) — conflict handling and restore semantics
- [SCHEDULING.md](docs/SCHEDULING.md) — scheduled task installation and silent execution
