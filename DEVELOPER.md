# Developer Guide

## Purpose

Windows command-line tool for backing up and restoring application settings to a cloud-synchronized folder..

## Development targets

- Platform(s): windows
- Primary technology: .NET
- Primary language: csharp
- Language/toolchain version: 10.0.302
- Secondary/support technology: Python
- Secondary language: python
- Blueprint: csharp

## Local development setup

Run `setup.cmd` from the repository root to restore the .NET dependencies. The script accepts `--dev` for consistency with other project scaffolds, but it does not change this project's .NET dependency restoration.

## Running the development version

From the repository root, run:

```cmd
run.cmd
```

Arguments are passed through to the development version. For example:

```cmd
run.cmd --help
```

The wrapper can also be run from another working directory because it resolves the project path relative to its own location. The equivalent direct command is:

```cmd
dotnet run --project src\StateKeep\StateKeep.csproj -- --help
```

The project is currently configured as a class library and does not yet contain an executable entry point, so the command above will fail until the application project is changed to an executable and a `Program.cs` entry point is added.

## Development conventions

- Use deterministic support scripts for repeatable setup and validation.
- Use the resolved secondary technology for non-trivial support automation.
- Do not introduce another scripting runtime unless it materially simplifies the project.

## Architecture

See `ARCHITECTURE.md` for architectural rationale and constraints.
