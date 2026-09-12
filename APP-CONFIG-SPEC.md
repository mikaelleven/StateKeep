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

YAML is the recommended initial format because it is human-readable, concise and supports comments.

## 3. Example

```yaml
version: 1
id: vscode
name: Visual Studio Code

roots:
  - name: user
    path: "%APPDATA%\\Code\\User"
    evidence:
      any:
        - file: "settings.json"
        - file: "keybindings.json"

include:
  - "**"

exclude:
  - "workspaceStorage/**"
  - "History/**"
  - "**/*.log"
  - "**/*.tmp"
```

## 4. Required fields

```yaml
version: 1
id: vscode
name: Visual Studio Code
roots: []
```

### `version`

Schema version.

### `id`

Stable machine-oriented application identifier.

Recommended constraints:

- lowercase
- ASCII
- `a-z`, `0-9`, `-`

### `name`

Human-readable application title.

### `roots`

One or more candidate application roots.

## 5. Root definition

```yaml
roots:
  - name: user
    path: "%APPDATA%\\Code\\User"
    evidence:
      all:
        - file: "settings.json"
        - fileContains:
            file: "settings.json"
            text: "editor."
```

### `name`

Optional for a single logical root.

Required if the application definition contains multiple independent roots that can be active simultaneously.

### `path`

Windows path supporting environment-variable expansion.

### `evidence`

Evidence used to prevent accidental selection of an unrelated folder.

Supported logical groups:

```yaml
evidence:
  any: []
```

or:

```yaml
evidence:
  all: []
```

Initial evidence predicates:

```yaml
- file: "settings.json"
```

```yaml
- directory: "profiles"
```

```yaml
- fileContains:
    file: "config.ini"
    text: "[SomeApplication]"
```

Evidence paths are relative to the candidate root.

## 6. Filtering

```yaml
include:
  - "**/*.json"
  - "snippets/**"

exclude:
  - "cache/**"
  - "**/*.tmp"
```

Evaluation order:

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

## 7. Multiple roots

Example:

```yaml
version: 1
id: vscode
name: Visual Studio Code

roots:
  - name: user
    path: "%APPDATA%\\Code\\User"
    evidence:
      any:
        - file: "settings.json"

  - name: extensions
    path: "%USERPROFILE%\\.vscode\\extensions"
    evidence:
      any:
        - directory: "."
```

Backup layout:

```text
apps\vscode\
├── user\
│   └── ...
└── extensions\
    └── ...
```
