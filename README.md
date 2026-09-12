# AppBackup

AppBackup is a Windows-only command-line tool for backing up and restoring application settings to a cloud-synchronized folder such as OneDrive.

The tool is intentionally simple: it reads declarative per-application definitions, locates known application settings folders, filters the relevant files, and mirrors the resulting application state into a per-device backup folder.

## Purpose

AppBackup exists to make application configuration easy to preserve, version through the cloud provider, and restore after a fresh Windows installation or accidental configuration change.

It is **not** intended to replace a full-system backup product.

## Goals

1. Run backups and restores manually or unattended.
2. Keep application-specific knowledge in human-readable configuration files.
3. Store backups in a per-device folder while preserving each application's relative path structure.
4. Make backups understandable and recoverable without proprietary archives or databases.
5. Favor stability, resilience, robustness, KISS and YAGNI over features or visual design.

## Example

```powershell
appbackup backup
appbackup restore vscode
appbackup install task
```

Default application definitions are bundled with AppBackup under:

```text
.\apps\
```

Example backup destination:

```text
%ONEDRIVE%\MyBackups\DESKTOP_A7K3M9Q2\apps\...
```

## Documentation

- [ARCHITECTURE.md](ARCHITECTURE.md) — system architecture and design decisions
- [REQUIREMENTS.md](REQUIREMENTS.md) — functional and non-functional requirements
- [APP-CONFIG-SPEC.md](APP-CONFIG-SPEC.md) — per-application definition format
- [CLI-SPEC.md](CLI-SPEC.md) — command-line interface and output behavior
- [RESTORE-SAFETY.md](RESTORE-SAFETY.md) — conflict handling and restore semantics
- [SCHEDULING.md](SCHEDULING.md) — scheduled task installation and silent execution
