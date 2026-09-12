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

## Development conventions

- Use deterministic support scripts for repeatable setup and validation.
- Use the resolved secondary technology for non-trivial support automation.
- Do not introduce another scripting runtime unless it materially simplifies the project.

## Architecture

See `ARCHITECTURE.md` for architectural rationale and constraints.
