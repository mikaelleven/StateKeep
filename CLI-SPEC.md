# CLI Specification

## 1. Commands

### Backup all detected applications

```powershell
statekeep backup
```

### Backup one application

```powershell
statekeep backup vscode
```

### Restore one application

```powershell
statekeep restore vscode
```

### Restore all applications

```powershell
statekeep restore --all
```

### Force restore conflicts

```powershell
statekeep restore vscode --force
```

### List known applications

```powershell
statekeep list
```

### Show current status/configuration

```powershell
statekeep status
```

### Validate global and application configuration

```powershell
statekeep validate
```

### Install/update scheduled backup task

```powershell
statekeep install task
```

## 2. Global options

### `--silent`

Suppresses normal console output and all interactive progress rendering.

The process shall still:

- log errors and operational results to file
- return a meaningful exit code
- avoid prompting for input

Typical scheduled task action:

```powershell
statekeep backup --silent
```

## 3. Progress rendering

Interactive output should remain readable in Windows Terminal, PowerShell and classic console hosts.

Use ASCII-safe output by default.

### Per-app spinner

Example frames:

```text
| Scanning Visual Studio Code
/ Scanning Visual Studio Code
- Scanning Visual Studio Code
\ Scanning Visual Studio Code
```

### Current-processing indicator

```text
Processing: [ settings.json                              ]
```

The renderer may truncate long text to fit the current console width.

### Overall progress bar

```text
Overall:    [########------------] 4/10
```

The exact width may adapt to terminal width.

## 4. Sample backup output

```text
| Scanning Visual Studio Code
Processing: [ snippets\csharp.json                      ]
Overall:    [######--------------] 3/10

Visual Studio Code  OK   12 copied, 4 unchanged
Zed                 OK    3 copied, 8 unchanged
WinSCP              SKIP  not detected
```

## 5. Sample restore conflict output

```text
Conflict: settings.json

Source (backup)
  Size:     4,218 bytes
  Modified: 2026-09-11 20:14:02
  SHA-256:  3F8A...

Destination
  Size:     4,301 bytes
  Modified: 2026-09-12 09:41:17
  SHA-256:  A13C...

Skipped. Use --force to overwrite after creating settings.json.bak.
```

## 6. Exit codes

```text
0 = success
1 = one or more app operations failed or conflicts were skipped
2 = fatal initialization/configuration error
```
