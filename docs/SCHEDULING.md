# Scheduling

## 1. Principle

StateKeep does not implement its own scheduler or Windows service in v1.

Windows Task Scheduler is the scheduling mechanism.

## 2. Installation command

StateKeep shall provide:

```powershell
statekeep install task
```

The command must be idempotent.

Repeated execution shall:

- create the task if missing
- update the existing StateKeep task when configuration differs
- never create duplicate scheduled tasks

## 3. Scheduled action

The scheduled action uses the GUI-subsystem `wscript.exe` host to run a generated VBScript launcher with no window. The launcher invokes StateKeep synchronously with `backup --silent` and returns its exit code:

```text
wscript.exe //B //NoLogo "%LOCALAPPDATA%\\StateKeep\\scheduled-backup.vbs"
```

This keeps the installed executable as a normal console application for interactive CLI use while ensuring automatic backups do not display a terminal window. The launcher is regenerated when the task is installed or updated, and the configured executable path is absolute inside the launcher.

## 4. Task identity

Recommended fixed task name:

```text
StateKeep Automatic Backup
```

A fixed identity allows reliable idempotent updates.

## 5. Suggested default trigger

A conservative v1 default could be once daily.

Exact timing should be configurable later only if required. The initial implementation should avoid creating a separate scheduling configuration model unless needed.

## 6. Silent execution

Scheduled execution shall use `--silent` so that:

- no terminal UI is rendered
- no prompts are shown
- logs are still written
- failures are visible through process exit status and log files

## 7. Failure handling

The task installer should configure Task Scheduler to use StateKeep's exit code as the authoritative task result.

StateKeep itself remains responsible for per-app failure isolation and logging.
