# Scheduling

## 1. Principle

AppBackup does not implement its own scheduler or Windows service in v1.

Windows Task Scheduler is the scheduling mechanism.

## 2. Installation command

AppBackup shall provide:

```powershell
appbackup install task
```

The command must be idempotent.

Repeated execution shall:

- create the task if missing
- update the existing AppBackup task when configuration differs
- never create duplicate scheduled tasks

## 3. Scheduled action

Recommended scheduled action:

```powershell
appbackup.exe backup --silent
```

The configured executable path should be absolute when registered with Task Scheduler.

## 4. Task identity

Recommended fixed task name:

```text
AppBackup Automatic Backup
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

The task installer should configure Task Scheduler to use AppBackup's exit code as the authoritative task result.

AppBackup itself remains responsible for per-app failure isolation and logging.
