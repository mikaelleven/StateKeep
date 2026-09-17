# Requirements

## 1. Scope

Initial scope is Windows only.

## 2. Functional requirements

### FR-1 Manual operation

The tool shall support manual execution from a terminal.

Minimum commands:

```text
statekeep backup
statekeep backup <app>
statekeep restore <app>
statekeep restore --all
statekeep apps list
statekeep apps open
statekeep status
statekeep validate
statekeep install task
```

### FR-2 Automatic operation

The tool shall support unattended execution through Windows Task Scheduler.

`statekeep install task` shall idempotently create or update the scheduled task required to run automatic backups.

Repeated execution of the command shall leave the system in the same desired state without creating duplicate tasks.

### FR-3 Silent mode

The tool shall support a silent mode intended for scheduled/background execution.

Silent mode shall:

- suppress spinners, progress bars and ordinary console output
- never prompt for user input
- continue writing logs
- preserve meaningful process exit codes

Recommended CLI option:

```text
--silent
```

### FR-4 Global configuration

A global human-readable configuration file shall contain at minimum:

```yaml
version: 1
backupPath: "%ONEDRIVE%\\MyBackups"
```

Environment variables shall be expanded.

### FR-5 Device identity

Each computer shall have a persistent unique short identifier.

- generated once when missing
- 8-12 alphanumeric characters
- persisted locally
- combined with the current Windows computer name in the backup folder

Format:

```text
<cloud-backup-path>\<COMPUTER-NAME>_<device-id>\apps\...
```

### FR-6 Bundled per-app definitions

Application definitions shall be distributed with StateKeep.

Default location:

```text
.\apps\
```

where `.` is the directory containing the StateKeep executable.

### FR-7 Application discovery

Each app definition shall support one or more candidate root folders.

Each candidate root shall support evidence proving that the folder belongs to the expected application.

Initial evidence types:

- file exists
- directory exists
- file contains a specific text signature

### FR-8 Include/exclude filtering

Each app definition shall support gitignore-style path patterns for:

- include whitelist
- exclude blacklist

If include rules are omitted, all files are considered included before excludes are applied.

### FR-9 Relative path preservation

Backups shall preserve the relative path below each detected application root.

Multiple roots shall be stored under stable logical root names.

### FR-10 Current-state backup

The destination shall represent the current selected application state rather than StateKeep-managed timestamped snapshots.

Files that no longer exist in the selected source set may be removed from the backup mirror only after successful source scanning/planning.

### FR-11 Restore conflict detection

A restore conflict exists when the restore target already contains a file that is not identical to the backup source.

For every conflict, StateKeep shall display both source and destination metadata:

- path
- size
- last modified date/time
- cryptographic hash

SHA-256 is recommended as the initial hash algorithm.

### FR-12 Safe restore behavior

Without `--force`, conflicting destination files shall not be overwritten.

With `--force`:

1. the existing destination file shall first be copied/renamed to a `.bak` file
2. the restore source shall then overwrite the destination
3. if creation of the `.bak` file fails, the destination shall not be overwritten

### FR-13 Progress feedback

Interactive commands shall support terminal-friendly progress indicators.

At minimum:

1. per-app spinner
2. current-processing label
3. overall progress bar

Examples:

```text
| Scanning Visual Studio Code
Processing: [ settings.json                         ]
Overall:    [##########----------] 5/10
```

Spinner sequence may use ASCII characters only:

```text
| / - \
```

### FR-14 Logging

Logs shall be written to a local mutable application-data directory.

Example:

```text
%LOCALAPPDATA%\StateKeep\logs\
```

Logging shall remain active in silent mode.

### FR-15 Failure isolation

Failure while processing one app shall not prevent unrelated apps from being processed.

## 3. Non-functional requirements

### NFR-1 Stability

Predictable and safe behavior takes precedence over feature count or presentation.

### NFR-2 KISS

Use direct filesystem operations and simple declarative configuration wherever possible.

### NFR-3 YAGNI

Do not add:

- databases
- cloud-provider APIs
- background Windows services
- scripting in app definitions
- registry-based detection
- regex-based evidence

until a demonstrated use case requires them.

### NFR-4 Recoverability

Backups shall be ordinary files and directories so that users can manually inspect and recover data without StateKeep.

### NFR-5 Determinism

Given the same source filesystem, global configuration and app definition, backup planning should be deterministic.

## 4. Suggested exit codes

```text
0 = success
1 = one or more app operations failed or restore conflicts were skipped
2 = fatal initialization or configuration error
```
