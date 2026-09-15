# Architecture

## 1. Architectural principles

StateKeep follows these principles:

- **KISS** — prefer straightforward filesystem operations and declarative configuration.
- **YAGNI** — do not introduce subsystems before a concrete need exists.
- **Stability first** — predictable and safe behavior is more important than feature count.
- **Cloud-provider agnostic** — StateKeep writes to a normal local filesystem path. OneDrive, Dropbox or another sync client is outside StateKeep's responsibility.
- **Declarative app knowledge** — application-specific paths and evidence belong in bundled app definitions, not hard-coded engine logic.
- **Recoverable without StateKeep** — backups are ordinary files and folders.
- **Failure isolation** — failure in one application must not prevent independent applications from being processed.
- **Safe restore** — existing destination files are never overwritten implicitly.

## 2. High-level architecture

```text
                   +--------------------------+
                   |           CLI            |
                   | backup / restore / list  |
                   | validate / install task  |
                   +------------+-------------+
                                |
                         +------v-------+
                         | Application  |
                         |    Engine    |
                         +------+-------+
                                |
          +---------------------+----------------------+
          |                     |                      |
   +------v------+       +------v------+        +------v------+
   | App Config  |       | App Locator |        | Backup /    |
   |   Loader    |       | & Detector  |        | Restore     |
   +-------------+       +-------------+        | Engine      |
                                                +------+------+
                                                       |
                                                +------v------+
                                                | Filesystem  |
                                                |   Storage   |
                                                +-------------+
```

## 3. Runtime layout

Recommended installation layout:

```text
<StateKeep installation>\
├── statekeep.exe
├── apps\
│   ├── vscode.yaml
│   ├── zed.yaml
│   └── ...
└── ...
```

The default app-definition directory is always resolved relative to the executable:

```text
.\apps\
```

The bundled definitions are part of the application distribution.

Local mutable state belongs outside the installation directory:

```text
%LOCALAPPDATA%\StateKeep\
├── config.yaml
├── device-id
└── logs\
```

## 4. Backup destination layout

Global backup root:

```text
<cloud-backup-path>\<COMPUTER-NAME>_<device-id>\
```

Example:

```text
%ONEDRIVE%\MyBackups\DESKTOP_A7K3M9Q2\
```

Application backups:

```text
<device>\
├── vscode\
│   └── ...\
└── zed\
    └── ...
```

Relative paths below the detected application root are preserved.

If an application has multiple independent roots, every root must have a stable logical name and is stored below that name.

## 5. Device identity

A short random device ID is generated once when no ID exists.

Requirements:

- 8-12 alphanumeric characters
- generated randomly
- persisted in `%LOCALAPPDATA%\StateKeep\device-id`
- never derived from mutable hardware identifiers

The effective backup device folder combines the current computer name and persistent device ID.

## 6. Main processing flow

### Backup

```text
Load global config
      |
Load app definitions
      |
For each app
      |
Locate candidate root(s)
      |
Validate evidence
      |
Enumerate files
      |
Apply include/exclude filters
      |
Compare source and backup
      |
Copy changed files safely
      |
Remove obsolete backup files
      |
Write/update informational manifest
```

The backup represents current application state rather than an internal snapshot history. Cloud-provider version history may provide historical file versions.

### Restore

```text
Load app definition
      |
Locate restore target root(s)
      |
Enumerate backup files
      |
Compare backup source and destination
      |
No conflict -> restore
Conflict -> show source/destination metadata
      |
Without --force -> skip conflict
With --force -> create .bak then overwrite
```

## 7. Core components

Suggested logical components:

```text
Configuration
  GlobalConfig
  AppConfig
  ConfigLoader

Discovery
  AppLocator
  EvidenceEvaluator

Filtering
  PathFilter

Backup
  BackupEngine
  BackupPlanner
  FileCopier

Restore
  RestoreEngine
  ConflictEvaluator
  BackupFileCreator

Identity
  DeviceIdentity

Scheduling
  ScheduledTaskInstaller

Console
  ProgressRenderer

Storage
  FileSystemStorage
```

These can initially live in a single .NET project. Separate assemblies should only be introduced if later justified.

## 8. Robustness rules

- Destination files must be written using temporary files followed by replace/rename where practical.
- Existing destination files must not be deleted before a replacement has been written successfully.
- One app failure must not abort processing of unrelated apps.
- Fatal initialization/configuration errors must terminate execution with a non-zero exit code.
- Silent mode must suppress interactive/progress output but retain logs and exit codes.
- Restore conflicts must never be overwritten unless `--force` is explicitly supplied.
