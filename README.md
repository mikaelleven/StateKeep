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

## Documentation

- [ARCHITECTURE.md](docs/ARCHITECTURE.md) — system architecture and design decisions
- [REQUIREMENTS.md](docs/REQUIREMENTS.md) — functional and non-functional requirements
- [APP-CONFIG-SPEC.md](docs/APP-CONFIG-SPEC.md) — per-application definition format
- [CLI-SPEC.md](docs/CLI-SPEC.md) — command-line interface and output behavior
- [RESTORE-SAFETY.md](docs/RESTORE-SAFETY.md) — conflict handling and restore semantics
- [SCHEDULING.md](docs/SCHEDULING.md) — scheduled task installation and silent execution
