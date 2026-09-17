# Restore Safety

## 1. Default rule

Restore must be conservative.

An existing destination file shall never be overwritten unless StateKeep can prove it is identical or the user explicitly supplies `--force`.

## 2. Conflict detection

For each restore file:

1. destination missing -> restore
2. destination exists -> compare metadata/content
3. identical -> no action required
4. different -> conflict

A conflict report shall include source and destination:

- size
- last modified date/time
- SHA-256 hash

The path shall also be shown clearly.

## 3. Default conflict behavior

Without `--force`:

```text
conflict -> display details -> skip file
```

No interactive overwrite confirmation is required for v1. This keeps behavior deterministic and safe for scripting.

## 4. Forced overwrite behavior

With `--force`:

```text
existing destination
        |
create destination.bak
        |
verify .bak creation succeeded
        |
overwrite destination
```

If `.bak` creation fails, the original destination shall remain untouched.

## 5. Existing `.bak` files

Recommended v1 policy:

- if `<file>.bak` does not exist, create it
- if it exists, create a timestamped variant

Example:

```text
settings.json.bak
settings.json.bak.20260912-132500
```

This avoids destroying the previous safety copy.

## 6. Directory behavior

Restore may create missing directories as needed.

Restore shall not delete unrelated destination files by default.

A future destructive/mirroring restore mode should require an explicit separate option and is out of scope for v1.
