---
name: append
description: Add or update a StateKeep application YAML definition for /append <application>, following docs/APP-CONFIG-SPEC.md, checking for duplicate IDs, and validating root determinism and evidence requirements.
---

# Append an application configuration

Use this skill when the user invokes `/append <application>` or asks to add a bundled application definition to StateKeep.

## Workflow

1. Read `docs/APP-CONFIG-SPEC.md` before making changes. Treat it as the source of truth for the YAML schema, path semantics, evidence predicates, and filtering rules.
2. Interpret the argument as the human-readable application to add. If it is missing or ambiguous, ask for clarification rather than guessing.
3. Look up the application's current Windows storage locations online before finalizing the config. Prefer official vendor documentation or reliable technical documentation. Verify product name, roaming/local location, profile layout, and which files are relevant. Tell the user when online details could not be verified.
4. Inspect `apps/` and derive a stable ID using lowercase ASCII and only `a-z`, `0-9`, and `-`. Search all existing YAML definitions for that ID before creating anything.
5. Ensure exactly one application definition has the chosen ID:
   - If no definition has the ID, create `apps/<id>.yaml`.
   - If one definition has the ID, update that definition rather than creating a second one, preserving unrelated user changes where possible.
   - If multiple definitions already have the ID, stop and report the conflict; do not silently delete or merge definitions.
6. Choose roots that are deterministic and no broader than necessary. Prefer a known application data directory over a generic parent such as `%USERPROFILE%`.
7. Evaluate whether the root can be identified safely from its path alone:
   - Use no evidence only when the path is sufficiently application-specific and accidental selection is unlikely.
   - Add `evidence.any` or `evidence.all` when the path is shared, variable, broad, or otherwise could identify an unrelated directory. Use only predicates supported by the spec (`file`, `directory`, and `fileContains`).
   - Evidence paths are relative to the candidate root. Ensure evidence remains valid for the selected root and does not assume a single profile when profiles can vary.
8. Keep the definition focused on the application's intended state. Use `include` to select only the requested files or directories, and add `exclude` only when necessary. Do not include caches, logs, or unrelated data without a concrete requirement.
9. Validate the resulting YAML and review the diff. Confirm required fields (`version`, `id`, `name`, and `roots`) are present, the ID is unique, paths use supported Windows environment variables, and every root name is present when multiple independent roots exist.

## Output expectations

Report:

- The file created or updated.
- The application ID and root paths.
- Whether evidence was added and why, or why the root was deterministic without it.
- The online source(s) consulted, when available.
- Validation performed and any uncertainty that remains.

Do not commit changes unless the user explicitly asks for a commit.
