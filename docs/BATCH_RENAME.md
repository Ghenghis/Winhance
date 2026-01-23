# Batch Rename System Guide 🔄

The Batch Rename System is a powerful multi-file renaming tool with real-time preview, undo support, and context menu integration.

## Table of Contents

- [Overview](#overview)
- [Quick Start](#quick-start)
- [Rename Rules](#rename-rules)
- [Pattern Variables](#pattern-variables)
- [Presets](#presets)
- [Context Menu Integration](#context-menu-integration)
- [CLI Usage](#cli-usage)
- [MCP Integration](#mcp-integration)
- [Configuration](#configuration)

---

## Overview

```
┌─ BATCH RENAME DASHBOARD ──────────────────────────────────────────────────────┐
│                                                                                │
│  ┌─ FILE SELECTION ────────────────────────────────────────────────────────┐  │
│  │                                                                          │  │
│  │  Source: C:\Users\Admin\Pictures\Vacation  [Browse...] [Add Files...]   │  │
│  │                                                                          │  │
│  │  Filter: [*.jpg, *.png              ] [x] Include subfolders            │  │
│  │                                                                          │  │
│  │  247 files selected (1.2 GB)                                            │  │
│  │                                                                          │  │
│  └──────────────────────────────────────────────────────────────────────────┘  │
│                                                                                │
│  ┌─ RENAME RULES ──────────────────────────────────────────────────────────┐  │
│  │                                                                          │  │
│  │  [1] ⬆⬇ Find & Replace    "IMG_"        →  "Vacation_"         [×]     │  │
│  │  [2] ⬆⬇ Remove            "_BURST"                              [×]     │  │
│  │  [3] ⬆⬇ Add Counter       Suffix: _[###]  Start: 001           [×]     │  │
│  │  [4] ⬆⬇ Change Case       Title Case                            [×]     │  │
│  │                                                                          │  │
│  │  [+ Add Rule ▼]  [Load Preset ▼]  [Save Preset]  [Clear All]            │  │
│  │                                                                          │  │
│  └──────────────────────────────────────────────────────────────────────────┘  │
│                                                                                │
│  ┌─ LIVE PREVIEW ──────────────────────────────────────────────────────────┐  │
│  │                                                                          │  │
│  │  #   Original Name                    New Name                  Status  │  │
│  │  ─────────────────────────────────────────────────────────────────────  │  │
│  │  1   IMG_20260115_001_BURST.jpg   →  Vacation_001.jpg            ✓     │  │
│  │  2   IMG_20260115_002_BURST.jpg   →  Vacation_002.jpg            ✓     │  │
│  │  3   IMG_20260115_003.jpg         →  Vacation_003.jpg            ✓     │  │
│  │  4   IMG_sunset_beach.jpg         →  Vacation_004.jpg            ✓     │  │
│  │  5   IMG_20260116_001_BURST.jpg   →  Vacation_005.jpg            ✓     │  │
│  │  ...                                                                    │  │
│  │                                                                          │  │
│  │  ⚠ 2 conflicts detected (duplicate names)                [Show Only]   │  │
│  │                                                                          │  │
│  └──────────────────────────────────────────────────────────────────────────┘  │
│                                                                                │
│  [Preview Refresh] [Apply Rename] [Undo Last Batch] [Export List]             │
│                                                                                │
└────────────────────────────────────────────────────────────────────────────────┘
```

### Key Features

| Feature                 | Description                     |
| ----------------------- | ------------------------------- |
| **Live Preview**        | See results before applying     |
| **Rule Stacking**       | Combine multiple rules in order |
| **Drag & Drop Rules**   | Reorder rules by dragging       |
| **Conflict Detection**  | Warns about duplicate names     |
| **Full Undo**           | Revert entire batch operations  |
| **Presets**             | Save and load rule combinations |
| **Regex Support**       | Advanced pattern matching       |
| **Metadata Extraction** | Use EXIF, ID3, file attributes  |

---

## Quick Start

### Basic Rename Workflow

1. **Select files** - Drag files/folders or use Browse
2. **Add rules** - Choose rename operations
3. **Preview** - Verify changes in real-time
4. **Apply** - Execute rename with undo support

### Simple Example

**Goal:** Rename `IMG_20260118_001.jpg` → `Photo_001.jpg`

```
Rule 1: Find & Replace
  Find:    "IMG_"
  Replace: "Photo_"

Rule 2: Remove
  Pattern: "_20260118"
```

---

## Rename Rules

### Find & Replace

Replace text in filenames.

```
┌─ FIND & REPLACE ────────────────────────────────┐
│                                                  │
│  Find:     [IMG_                             ]  │
│  Replace:  [Photo_                           ]  │
│                                                  │
│  Options:                                        │
│  [x] Case sensitive                             │
│  [ ] Match whole word only                      │
│  [ ] Use regular expression                     │
│                                                  │
└──────────────────────────────────────────────────┘
```

**Examples:**
| Find                    | Replace    | Before         | After          |
| ----------------------- | ---------- | -------------- | -------------- |
| `IMG_`                  | `Photo_`   | IMG_001.jpg    | Photo_001.jpg  |
| `_BURST`                | ``         | file_BURST.jpg | file.jpg       |
| `(\d{4})(\d{2})(\d{2})` | `$1-$2-$3` | 20260118.txt   | 2026-01-18.txt |

### Add Text

Insert text at specific positions.

```
┌─ ADD TEXT ──────────────────────────────────────┐
│                                                  │
│  Text:     [vacation_                        ]  │
│                                                  │
│  Position:                                       │
│  ● Prefix (before name)                         │
│  ○ Suffix (after name, before extension)        │
│  ○ At position: [  5  ]                         │
│  ○ Before text: [      ]                        │
│  ○ After text:  [      ]                        │
│                                                  │
└──────────────────────────────────────────────────┘
```

### Remove Text

Remove characters from filenames.

```
┌─ REMOVE TEXT ───────────────────────────────────┐
│                                                  │
│  Mode:                                           │
│  ● Remove specific text: [_BURST             ]  │
│  ○ Remove first N chars: [   ]                  │
│  ○ Remove last N chars:  [   ]                  │
│  ○ Remove from position: [   ] to [   ]         │
│  ○ Remove pattern (regex): [                 ]  │
│                                                  │
│  [ ] Case sensitive                             │
│                                                  │
└──────────────────────────────────────────────────┘
```

### Counter / Numbering

Add sequential numbers to filenames.

```
┌─ COUNTER ───────────────────────────────────────┐
│                                                  │
│  Format:   [###                              ]  │
│            (# = digit, ## = 01, ### = 001)      │
│                                                  │
│  Start at: [1      ]  Step: [1      ]           │
│                                                  │
│  Position:                                       │
│  ○ Replace name entirely                        │
│  ● Prefix: [Name]_[###]                         │
│  ○ Suffix: [###]_[Name]                         │
│                                                  │
│  Reset counter:                                  │
│  ○ Never                                        │
│  ● Per folder                                   │
│  ○ When pattern changes: [          ]           │
│                                                  │
└──────────────────────────────────────────────────┘
```

### Change Case

Transform text case.

```
┌─ CHANGE CASE ───────────────────────────────────┐
│                                                  │
│  Apply to:                                       │
│  ● Filename only                                │
│  ○ Extension only                               │
│  ○ Both                                         │
│                                                  │
│  Case style:                                     │
│  ○ lowercase         → my file name.txt         │
│  ○ UPPERCASE         → MY FILE NAME.TXT         │
│  ● Title Case        → My File Name.txt         │
│  ○ Sentence case     → My file name.txt         │
│  ○ camelCase         → myFileName.txt           │
│  ○ PascalCase        → MyFileName.txt           │
│  ○ snake_case        → my_file_name.txt         │
│  ○ kebab-case        → my-file-name.txt         │
│                                                  │
└──────────────────────────────────────────────────┘
```

### Change Extension

Modify file extensions.

```
┌─ CHANGE EXTENSION ──────────────────────────────┐
│                                                  │
│  Mode:                                           │
│  ○ Replace extension:  [.jpeg  ] → [.jpg     ]  │
│  ● Set extension:      [.txt                 ]  │
│  ○ Add extension:      [.bak                 ]  │
│  ○ Remove extension                             │
│                                                  │
│  [ ] Case sensitive                             │
│                                                  │
└──────────────────────────────────────────────────┘
```

### Date/Time

Add timestamps from file metadata.

```
┌─ DATE/TIME ─────────────────────────────────────┐
│                                                  │
│  Source:                                         │
│  ● File modified date                           │
│  ○ File created date                            │
│  ○ EXIF date taken (photos)                     │
│  ○ Custom date: [2026-01-18           ]         │
│                                                  │
│  Format:   [YYYY-MM-DD                       ]  │
│                                                  │
│  Common formats:                                 │
│  • YYYY-MM-DD        → 2026-01-18               │
│  • YYYYMMDD          → 20260118                 │
│  • DD-MMM-YYYY       → 18-Jan-2026              │
│  • YYYY-MM-DD_HHmmss → 2026-01-18_143052        │
│                                                  │
│  Position:                                       │
│  ● Prefix                                       │
│  ○ Suffix                                       │
│  ○ Replace name                                 │
│                                                  │
└──────────────────────────────────────────────────┘
```

### Metadata Extraction

Use file metadata in names.

```
┌─ METADATA ──────────────────────────────────────┐
│                                                  │
│  Available metadata:                             │
│                                                  │
│  Images (EXIF):                                  │
│  • {camera}        → Canon EOS R5               │
│  • {lens}          → RF 24-70mm                 │
│  • {iso}           → 400                        │
│  • {aperture}      → f/2.8                      │
│  • {resolution}    → 8192x5464                  │
│                                                  │
│  Audio (ID3):                                    │
│  • {artist}        → Artist Name                │
│  • {album}         → Album Title                │
│  • {track}         → 01                         │
│  • {title}         → Song Title                 │
│                                                  │
│  Video:                                          │
│  • {duration}      → 00:05:32                   │
│  • {codec}         → H.264                      │
│  • {resolution}    → 1920x1080                  │
│                                                  │
│  Format: [{artist} - {title}                 ]  │
│                                                  │
└──────────────────────────────────────────────────┘
```

### Regular Expression

Advanced pattern matching.

```
┌─ REGULAR EXPRESSION ────────────────────────────┐
│                                                  │
│  Pattern:    [^(\d{4})(\d{2})(\d{2})_(.*)   ]  │
│  Replace:    [$1-$2-$3 $4                    ]  │
│                                                  │
│  Test:                                           │
│  Input:  20260118_vacation_photo.jpg            │
│  Output: 2026-01-18 vacation_photo.jpg      ✓  │
│                                                  │
│  Quick patterns:                                 │
│  • \d+          → Match numbers                 │
│  • [a-zA-Z]+    → Match letters                 │
│  • \s+          → Match whitespace              │
│  • .*           → Match anything                │
│  • ^            → Start of name                 │
│  • $            → End of name                   │
│                                                  │
│  [Regex Helper...]  [Test Pattern]              │
│                                                  │
└──────────────────────────────────────────────────┘
```

---

## Pattern Variables

Use variables in rename patterns:

| Variable         | Description            | Example        |
| ---------------- | ---------------------- | -------------- |
| `[N]`            | Original filename      | vacation_photo |
| `[E]`            | Original extension     | jpg            |
| `[P]`            | Parent folder name     | Pictures       |
| `[G]`            | Grandparent folder     | Users          |
| `[C]` or `[###]` | Counter                | 001            |
| `[Y]`            | Year (4 digit)         | 2026           |
| `[M]`            | Month (2 digit)        | 01             |
| `[D]`            | Day (2 digit)          | 18             |
| `[h]`            | Hour (24h)             | 14             |
| `[m]`            | Minute                 | 30             |
| `[s]`            | Second                 | 52             |
| `[date]`         | Full date (YYYY-MM-DD) | 2026-01-18     |
| `[time]`         | Full time (HHmmss)     | 143052         |

### Pattern Examples

| Pattern               | Input              | Output               |
| --------------------- | ------------------ | -------------------- |
| `[P]_[C].[E]`         | Vacation/photo.jpg | Vacation_001.jpg     |
| `[Y]-[M]-[D]_[N].[E]` | photo.jpg          | 2026-01-18_photo.jpg |
| `[N]_backup.[E]`      | document.pdf       | document_backup.pdf  |

---

## Presets

### Built-in Presets

| Preset                | Description                      |
| --------------------- | -------------------------------- |
| **Photo Organizer**   | Date prefix + counter from EXIF  |
| **Music Organizer**   | Artist - Title from ID3 tags     |
| **Clean Filenames**   | Remove special chars, fix spaces |
| **Date Prefix**       | Add YYYY-MM-DD prefix            |
| **Lowercase All**     | Convert everything to lowercase  |
| **Number Files**      | Simple sequential numbering      |
| **Remove Duplicates** | Strip "(1)", "Copy of", etc.     |

### Custom Presets

Save your rule combinations:

```json
{
  "name": "My Photo Rename",
  "description": "Rename photos with date and location",
  "rules": [
    {
      "type": "find_replace",
      "find": "IMG_",
      "replace": ""
    },
    {
      "type": "date",
      "source": "exif",
      "format": "YYYY-MM-DD",
      "position": "prefix"
    },
    {
      "type": "counter",
      "format": "###",
      "position": "suffix"
    }
  ]
}
```

---

## Context Menu Integration

### Windows Explorer Integration

Right-click selected files to access batch rename:

```
┌─────────────────────────────────────────┐
│ 47 files selected                       │
│ ─────────────────────────────────────── │
│ ...                                     │
│ 🔄 Batch Rename with Winhance     Ctrl+M│
│    ├── Quick: Add Counter               │
│    ├── Quick: Add Date Prefix           │
│    ├── Quick: Change Case               │
│    └── Open Batch Rename Dashboard...   │
│ ...                                     │
└─────────────────────────────────────────┘
```

### Quick Actions

Common operations without opening dashboard:

| Quick Action  | Shortcut | Description                     |
| ------------- | -------- | ------------------------------- |
| Add Counter   | -        | Append sequential numbers       |
| Add Date      | -        | Prefix with file date           |
| Lowercase     | -        | Convert to lowercase            |
| Remove Spaces | -        | Replace spaces with underscores |

---

## CLI Usage

### Basic Commands

```powershell
# Preview rename (dry run)
winhance-fs rename --path "C:\Photos" --pattern "*.jpg" --preview

# Find and replace
winhance-fs rename --path "C:\Photos" --find "IMG_" --replace "Photo_"

# Add counter
winhance-fs rename --path "C:\Photos" --counter "###" --start 1

# Use preset
winhance-fs rename --path "C:\Photos" --preset "photo-organizer"

# Complex rename with multiple rules
winhance-fs rename --path "C:\Photos" \
  --rule "find:IMG_:Photo_" \
  --rule "remove:_BURST" \
  --rule "counter:suffix:###"
```

### CLI Options

| Option        | Description            |
| ------------- | ---------------------- |
| `--path`      | Target directory       |
| `--pattern`   | File filter (glob)     |
| `--recursive` | Include subfolders     |
| `--preview`   | Show preview only      |
| `--find`      | Find text              |
| `--replace`   | Replace text           |
| `--counter`   | Add counter format     |
| `--preset`    | Use saved preset       |
| `--rule`      | Add rename rule        |
| `--output`    | Export results to file |

---

## MCP Integration

### Available Tools

```python
@mcp.tool()
async def batch_rename_preview(
    path: str,
    rules: list[dict],
    pattern: str = "*"
) -> dict:
    """
    Preview batch rename operation without executing.

    Args:
        path: Directory containing files
        rules: List of rename rules to apply
        pattern: File filter pattern

    Returns:
        Preview of old → new names with conflict detection.
    """

@mcp.tool()
async def batch_rename_execute(
    path: str,
    rules: list[dict],
    pattern: str = "*"
) -> dict:
    """
    Execute batch rename operation with undo support.

    Returns:
        Transaction ID for potential rollback.
    """

@mcp.tool()
async def batch_rename_undo(transaction_id: str) -> dict:
    """Undo a batch rename operation."""
```

### Example Usage

```python
# Claude Code example
rules = [
    {"type": "find_replace", "find": "IMG_", "replace": "Photo_"},
    {"type": "counter", "format": "###", "position": "suffix"}
]

# Preview first
preview = await batch_rename_preview(
    path="C:\\Photos",
    rules=rules,
    pattern="*.jpg"
)

# Execute if preview looks good
result = await batch_rename_execute(
    path="C:\\Photos",
    rules=rules,
    pattern="*.jpg"
)

# Undo if needed
await batch_rename_undo(result["transaction_id"])
```

---

## Configuration

### Settings

Located at `%APPDATA%\Winhance-FS\batch-rename.json`:

```json
{
  "defaultRules": [],
  "recentPresets": [],
  "maxPreviewItems": 1000,
  "confirmBeforeRename": true,
  "createUndoPoint": true,
  "preserveTimestamps": true,
  "shellIntegration": {
    "enabled": true,
    "quickActions": ["counter", "date", "lowercase"]
  },
  "regex": {
    "defaultFlags": "gi",
    "timeout": 5000
  }
}
```

### Transaction Log

All operations logged for undo:

```json
{
  "transaction_id": "br_20260118_143052_abc123",
  "timestamp": "2026-01-18T14:30:52Z",
  "source_path": "C:\\Photos",
  "file_count": 247,
  "rules_applied": [
    {"type": "find_replace", "find": "IMG_", "replace": "Photo_"}
  ],
  "renames": [
    {
      "original": "C:\\Photos\\IMG_001.jpg",
      "renamed": "C:\\Photos\\Photo_001.jpg"
    }
  ],
  "status": "completed"
}
```

---

## Conflict Resolution

### Duplicate Name Detection

When multiple files would get the same name:

```
┌─ CONFLICT DETECTED ─────────────────────────────┐
│                                                  │
│  ⚠ 3 files would be renamed to "Photo_001.jpg"  │
│                                                  │
│  Conflicting files:                              │
│  • IMG_20260118_001.jpg                         │
│  • IMG_20260119_001.jpg                         │
│  • IMG_vacation_001.jpg                         │
│                                                  │
│  Resolution:                                     │
│  ○ Auto-number duplicates (Photo_001_1.jpg)     │
│  ● Skip duplicates                              │
│  ○ Overwrite (DANGER)                           │
│                                                  │
│  [Apply Resolution] [Edit Rules]                │
│                                                  │
└──────────────────────────────────────────────────┘
```

### Invalid Characters

Automatic handling of invalid filename characters:

| Character                 | Replacement       |
| ------------------------- | ----------------- |
| `< > : " / \ \| ? *`      | `_` (underscore)  |
| Leading/trailing spaces   | Removed           |
| Reserved names (CON, PRN) | Prefixed with `_` |

---

*See also: [FILE_MANAGER.md](FILE_MANAGER.md) | [FILE_ORGANIZER.md](FILE_ORGANIZER.md) | [FEATURES.md](FEATURES.md)*
