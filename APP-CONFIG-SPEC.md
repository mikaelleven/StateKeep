# App Configuration Specification

## 1. Location

Bundled application definitions are stored relative to the StateKeep executable:

```text
.\apps\
```

Example:

```text
apps\
├── vscode.yaml
├── zed.yaml
└── winscp.yaml
```

## 2. Format

YAML is the recommended initial format because it is human-readable, concise, and supports comments.

## 3. Required fields

```yaml
version: 1
id: vscode
name: Visual Studio Code
roots: []
```

### `version`

Schema version. The current schema version is `1`.

### `id`

Stable machine-oriented application identifier.

Recommended constraints:

- lowercase
- ASCII
- `a-z`, `0-9`, and `-`

### `name`

Human-readable application title.

### `roots`

One or more root definitions. Each root represents a different application folder. Roots are evaluated independently; the same filesystem path may be used by more than one root.

## 4. Root definition

```yaml
roots:
  - name: user-data
    path:
      - "%APPDATA%\\Example"
      - "%LOCALAPPDATA%\\Example"
    evidence:
      any:
        - file: "settings.json"
        - directory: "profiles"
```

### `name`

Optional for a single logical root. Required when the application definition contains multiple independent roots that can be active simultaneously. Names must be unique within an application because they are used in the backup layout.

### `path`

A Windows path, or an ordered list of alternative Windows paths, supporting environment-variable expansion.

A scalar path is equivalent to a one-item list:

```yaml
path: "%APPDATA%\\Example"
```

The alternatives are evaluated in the order written. An alternative matches only when:

1. The path exists as a directory; and
2. Its evidence matches, if evidence is defined.

After the first alternative matches, it is selected and no later alternative for that root is evaluated. If no alternative matches, the root is skipped. Alternatives are not merged or scanned together.

A path can be used by multiple roots. Such roots remain independent: each root must match its own evidence, and a match for one root does not make another root match.

### `evidence`

Optional predicates used to prevent accidental selection of an unrelated folder. Evidence paths are relative to the candidate root.

The supported logical groups are:

```yaml
evidence:
  any:
    - file: "settings.json"
```

```yaml
evidence:
  all:
    - file: "settings.json"
    - directory: "profiles"
```

`any` requires at least one predicate to match. `all` requires every predicate to match. An empty or omitted evidence group matches any existing candidate directory.

Supported predicates:

```yaml
- file: "settings.json"
```

Matches when the relative path is an existing file.

```yaml
- directory: "profiles"
```

Matches when the relative path is an existing directory.

```yaml
- fileContains:
    file: "config.ini"
    text: "[SomeApplication]"
```

Matches when the relative file exists and contains the specified text.

## 5. Complete example

This example defines two independent folders. The user-data root has three alternatives, including one path shared with the installation root. The shared path is evaluated separately for each root using that root's evidence.

```yaml
version: 1
id: raw-accel
name: Raw Accel

roots:
  - name: installation
    path: "%PROGRAMFILES%\\Raw Accel"
    evidence:
      any:
        - file: "rawaccel.exe"

  - name: user-data
    path:
      - "C:\\Programs\\RawAccel"
      - "%PROGRAMFILES%\\Raw Accel"
      - "%LOCALAPPDATA%\\Programs\\RawAccel"
    evidence:
      any:
        - file: "settings.json"
        - file: ".config"

include:
  - "settings.json"
  - ".config"
```

## 6. Filtering

```yaml
include:
  - "**/*.json"
  - "snippets/**"

exclude:
  - "cache/**"
  - "**/*.tmp"
```

Filtering is applied after a root has been selected:

```text
candidate file
     |
matches include? -- no --> ignore
     |
    yes
     |
matches exclude? -- yes --> ignore
     |
    no
     |
   backup
```

If `include` is omitted, this is implied:

```yaml
include:
  - "**"
```

## 7. Backup layout

Each selected root is stored below its application ID and logical root name. Relative paths below the selected root are preserved.

```text
apps\raw-accel\
├── installation\
│   └── ...
└── user-data\
    └── ...
```

Each root contributes files only from the first matching path in its own ordered path list.
