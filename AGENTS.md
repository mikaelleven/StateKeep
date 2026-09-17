# Agent Instructions

Act as an experienced .NET/C# architect and developer; prefer simple, idiomatic, maintainable .NET solutions and established SDK/project conventions.

## Project guidance

- Follow `docs/DEVELOPER.md` for setup, build, test, and operational workflow.
- Follow `docs/ARCHITECTURE.md` for canonical architectural principles and technical decisions.
- Prefer deterministic project scripts over recreating setup or scaffold logic manually.
- Apply KISS and YAGNI. Add files, dependencies, abstractions, and automation only when a concrete requirement needs them.
- Keep support automation in the resolved secondary technology; use thin native shell wrappers only as platform entry points.
