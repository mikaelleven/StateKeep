# Developer Guide

## Purpose

Windows command-line tool for backing up and restoring application settings to a cloud-synchronized folder.

## Development targets

- Platform: Windows
- Primary technology: .NET
- Primary language: C#
- Language/toolchain version: 10.0.302
- Secondary/support technology: Python
- Secondary language: Python
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

## Building and releasing

### Build a local release

Create a self-contained Windows x64 release ZIP and install it locally without publishing:

```cmd
release.cmd --local --no-publish
```

This increments the build version in `version.json` and creates
`releases\StateKeep-<version>-win-x64.zip`. The ZIP contains the runtime
executable and bundled application definitions, but excludes source and build
scripts.

### Publish a release

`release.cmd` publishes by default. Install and authenticate the GitHub CLI
before publishing:

```powershell
gh auth login
release.cmd --patch
```

The script increments the selected version component (or the build component by
default), creates the release ZIP and SHA-256 checksum, commits `version.json`,
tags the commit as `v<version>`, pushes it, and creates the GitHub release.

To protect release integrity, publishing stops if the working tree has
uncommitted changes outside `version.json`. An existing change to `version.json`
is allowed and becomes the base for the requested version bump. The GitHub
Actions **Release** workflow provides the equivalent process from GitHub; select
the version component when starting the workflow.

## Development conventions

- Use deterministic support scripts for repeatable setup and validation.
- Use the resolved secondary technology for non-trivial support automation.
- Do not introduce another scripting runtime unless it materially simplifies the project.

## Architecture

See `ARCHITECTURE.md` for architectural rationale and constraints.
