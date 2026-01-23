# Advanced File Manager Guide 📁

The Advanced File Manager is Winhance-FS's comprehensive file management system, combining the best features from legendary file managers (Total Commander, Directory Opus, Explorer++, Files, XYplorer, FreeCommander) with modern AI-powered organization.

## Table of Contents

- [Overview](#overview)
- [Dashboard Architecture](#dashboard-architecture)
- [Dual-Pane Browser](#dual-pane-browser)
- [Tabbed Interface](#tabbed-interface)
- [Quick Access Panel](#quick-access-panel)
- [File Operations](#file-operations)
- [Context Menu Integration](#context-menu-integration)
- [Batch Rename System](#batch-rename-system)
- [Smart Organizer](#smart-organizer)
- [Search & Filter](#search--filter)
- [Keyboard Shortcuts](#keyboard-shortcuts)
- [Configuration](#configuration)

---

## Overview

The Advanced File Manager provides a professional-grade file management experience with:

```
┌─────────────────────────────────────────────────────────────────────────────────┐
│  WINHANCE-FS ADVANCED FILE MANAGER                                               │
├─────────────────────────────────────────────────────────────────────────────────┤
│                                                                                  │
│  ┌─ TOOLBAR ─────────────────────────────────────────────────────────────────┐  │
│  │ [◄][►][↑][⟳] │ [Cut][Copy][Paste][Delete] │ [New▼] │ [View▼] │ [Tools▼] │  │
│  └───────────────────────────────────────────────────────────────────────────┘  │
│                                                                                  │
│  ┌─ TABS ────────────────────────────────────────────────────────────────────┐  │
│  │ [📁 Documents] [📁 Downloads] [📁 Projects] [+]                           │  │
│  └───────────────────────────────────────────────────────────────────────────┘  │
│                                                                                  │
│  ┌─ ADDRESS BAR ─────────────────────────────────────────────────────────────┐  │
│  │ 📁 C: › Users › Admin › Documents                      [🔍 Search...   ] │  │
│  └───────────────────────────────────────────────────────────────────────────┘  │
│                                                                                  │
│  ┌─ QUICK ACCESS ─┐  ┌─ LEFT PANE ──────────┐  ┌─ RIGHT PANE ─────────────┐   │
│  │                │  │                       │  │                          │   │
│  │ ★ Favorites    │  │ Name          Size ▼ │  │ Name          Size       │   │
│  │   Desktop      │  │ ───────────────────── │  │ ────────────────────────  │   │
│  │   Downloads    │  │ 📁 Projects   --     │  │ 📁 Backup     --         │   │
│  │   Documents    │  │ 📁 Work       --     │  │ 📄 notes.txt  12 KB      │   │
│  │                │  │ 📄 report.pdf 2.4 MB │  │ 📄 data.json  156 KB     │   │
│  │ 💾 Drives      │  │ 📄 image.png  890 KB │  │ 📷 photo.jpg  3.2 MB     │   │
│  │   C: (44 GB)   │  │                       │  │                          │   │
│  │   D: (1.2 TB)  │  │                       │  │                          │   │
│  │   E: (67 GB)   │  │                       │  │                          │   │
│  │                │  │                       │  │                          │   │
│  │ 🕒 Recent      │  ├───────────────────────┤  ├──────────────────────────┤   │
│  │   project/     │  │ 4 items, 3.3 MB       │  │ 4 items, 3.4 MB          │   │
│  │   report.pdf   │  │ Free: 44 GB / 256 GB  │  │ Free: 1.2 TB / 2 TB      │   │
│  └────────────────┘  └───────────────────────┘  └──────────────────────────┘   │
│                                                                                  │
│  ┌─ STATUS BAR ──────────────────────────────────────────────────────────────┐  │
│  │ 4 items selected (3.3 MB) │ Total: 1,247 items │ Hidden: 23 │ [⚙ Options] │  │
│  └───────────────────────────────────────────────────────────────────────────┘  │
│                                                                                  │
└─────────────────────────────────────────────────────────────────────────────────┘
```

### Key Features

| Feature                   | Description                        | Inspired By            |
| ------------------------- | ---------------------------------- | ---------------------- |
| **Dual-Pane Browser**     | Side-by-side directory comparison  | Total Commander        |
| **Tabbed Interface**      | Multiple locations in one window   | Chrome, Directory Opus |
| **Breadcrumb Navigation** | Click-to-navigate path segments    | Windows Explorer       |
| **Quick Access Panel**    | Favorites, drives, recent items    | Files App              |
| **Column Customization**  | Show/hide any file attribute       | XYplorer               |
| **Preview Pane**          | Quick file preview without opening | macOS Finder           |
| **Batch Rename**          | Powerful multi-file renaming       | Bulk Rename Utility    |
| **Smart Organizer**       | AI-powered file categorization     | Winhance-FS Original   |
| **Full Path Display**     | Always visible complete paths      | Developer request      |

---

## Dashboard Architecture

The File Manager is organized into specialized dashboards accessible via tabs:

### Dashboard Tabs

```
┌──────────────────────────────────────────────────────────────────────────────┐
│ [📁 Browser] [🔄 Batch Rename] [📊 Organizer] [🔍 Search] [⚙ Settings]      │
└──────────────────────────────────────────────────────────────────────────────┘
```

| Dashboard        | Purpose                                        |
| ---------------- | ---------------------------------------------- |
| **Browser**      | Main file browsing with dual-pane support      |
| **Batch Rename** | Multi-file renaming with preview               |
| **Organizer**    | AI-powered file organization and cleanup       |
| **Search**       | Advanced search with filters and saved queries |
| **Settings**     | Configuration and customization                |

### Feature Registration

```csharp
// FeatureIds.cs
public const string FileManager = "FileManager";
public const string FileManagerBrowser = "FileManager.Browser";
public const string FileManagerBatchRename = "FileManager.BatchRename";
public const string FileManagerOrganizer = "FileManager.Organizer";
public const string FileManagerSearch = "FileManager.Search";

// FeatureDefinitions.cs
new(FeatureIds.FileManager, "File Manager", "FolderMultiple", "Files", 1),
new(FeatureIds.FileManagerBatchRename, "Batch Rename", "RenameBox", "Files", 2),
new(FeatureIds.FileManagerOrganizer, "Smart Organizer", "FolderStar", "Files", 3),
```

---

## Dual-Pane Browser

Professional dual-pane file browsing for efficient file management.

### Layout Modes

| Mode                       | Description                       | Use Case        |
| -------------------------- | --------------------------------- | --------------- |
| **Single Pane**            | Traditional single directory view | Simple browsing |
| **Dual Pane (Horizontal)** | Left/right side-by-side           | Wide monitors   |
| **Dual Pane (Vertical)**   | Top/bottom stacked                | Tall monitors   |
| **Preview Pane**           | File list + preview panel         | Document review |

### Pane Synchronization

```
┌─ SYNC OPTIONS ────────────────────────────────────────┐
│                                                        │
│  ○ Independent    - Panes navigate independently       │
│  ○ Mirror         - Both panes show same location      │
│  ● Linked         - Navigate relative to each other    │
│                                                        │
│  [x] Sync selection                                    │
│  [ ] Sync scroll position                              │
│  [x] Show diff highlighting                            │
└────────────────────────────────────────────────────────┘
```

### Directory Comparison

Visual diff highlighting between panes:

- **Green** - File exists only in this pane
- **Yellow** - File differs (size/date)
- **Red** - File missing from this pane
- **Gray** - Identical in both panes

---

## Tabbed Interface

Browser-style tabs for managing multiple locations.

### Tab Features

| Feature                | Description                        |
| ---------------------- | ---------------------------------- |
| **Drag & Drop**        | Reorder tabs by dragging           |
| **Middle-Click Close** | Close tab with middle mouse button |
| **Duplicate Tab**      | Ctrl+Click to duplicate            |
| **Pin Tab**            | Lock important tabs from closing   |
| **Tab Groups**         | Color-code related tabs            |
| **Session Restore**    | Remember tabs on restart           |

### Tab Context Menu

```
┌─────────────────────────────┐
│ New Tab                Ctrl+T│
│ Duplicate Tab               │
│ ─────────────────────────── │
│ Pin Tab                     │
│ Move to New Window          │
│ ─────────────────────────── │
│ Close Tab              Ctrl+W│
│ Close Other Tabs            │
│ Close Tabs to the Right     │
│ ─────────────────────────── │
│ Reopen Closed Tab      Ctrl+Z│
└─────────────────────────────┘
```

---

## Quick Access Panel

Fast navigation to frequently used locations.

### Sections

```
┌─ QUICK ACCESS ────────────────────┐
│                                    │
│ ★ FAVORITES                        │
│   📁 Desktop                       │
│   📁 Downloads                     │
│   📁 Documents                     │
│   📁 Projects                [📌]  │
│   📁 AI Models               [📌]  │
│                                    │
│ 💾 DRIVES                          │
│   🟢 C: System (44 GB free)        │
│   🟢 D: Data (1.2 TB free)         │
│   🟡 E: Backup (67 GB free)        │
│   🔴 F: Archive (12 GB free)       │
│                                    │
│ 🕒 RECENT LOCATIONS                │
│   📁 C:\Users\Admin\Projects       │
│   📁 D:\Models\LLM                 │
│   📁 E:\Backups\2026               │
│                                    │
│ 🔖 SAVED SEARCHES                  │
│   🔍 Large files (>1GB)            │
│   🔍 Modified today                │
│   🔍 AI models (*.gguf)            │
│                                    │
└────────────────────────────────────┘
```

### Custom Favorites

Add any folder to favorites:
- Drag & drop folders to the panel
- Right-click → "Add to Favorites"
- Assign custom icons and colors
- Organize into groups

---

## File Operations

Comprehensive file operations with progress tracking and undo support.

### Operation Types

| Operation            | Shortcut     | Description                 |
| -------------------- | ------------ | --------------------------- |
| **Copy**             | Ctrl+C       | Copy to clipboard           |
| **Cut**              | Ctrl+X       | Cut to clipboard            |
| **Paste**            | Ctrl+V       | Paste from clipboard        |
| **Delete**           | Del          | Move to Recycle Bin         |
| **Permanent Delete** | Shift+Del    | Bypass Recycle Bin          |
| **Rename**           | F2           | Rename single item          |
| **New Folder**       | Ctrl+Shift+N | Create folder               |
| **New File**         | Ctrl+N       | Create file (with template) |
| **Properties**       | Alt+Enter    | Show file properties        |

### Progress Dialog

```
┌─ COPY OPERATION ──────────────────────────────────────────────────┐
│                                                                    │
│  Copying 47 files to D:\Backup...                                 │
│                                                                    │
│  Current: Llama-3.1-70B-Q4_K_M.gguf                               │
│  ████████████████████░░░░░░░░░░░░░░░░░░░░  52%                    │
│                                                                    │
│  Speed: 245 MB/s    Remaining: ~2 min 15 sec                      │
│  Copied: 24.5 GB    Total: 47.2 GB                                │
│                                                                    │
│  [Pause] [Cancel] [Run in Background]                             │
│                                                                    │
│  ☐ Close when complete                                            │
│  ☐ Shutdown when complete                                         │
└────────────────────────────────────────────────────────────────────┘
```

### Conflict Resolution

```
┌─ FILE CONFLICT ───────────────────────────────────────────────────┐
│                                                                    │
│  ⚠ File already exists: report.pdf                                │
│                                                                    │
│  Source:       D:\Documents\report.pdf                            │
│  Size:         2.4 MB  |  Modified: Jan 18, 2026 10:30 AM        │
│                                                                    │
│  Destination:  E:\Backup\report.pdf                               │
│  Size:         1.8 MB  |  Modified: Jan 15, 2026 03:45 PM        │
│                                                                    │
│  [Replace] [Skip] [Rename] [Compare]                              │
│                                                                    │
│  ☐ Apply to all conflicts                                         │
│                                                                    │
└────────────────────────────────────────────────────────────────────┘
```

---

## Context Menu Integration

Rich right-click context menus with full functionality.

### File Context Menu

```
┌─────────────────────────────────────────┐
│ Open                              Enter │
│ Open With...                      ▶     │
│ ─────────────────────────────────────── │
│ Cut                              Ctrl+X │
│ Copy                             Ctrl+C │
│ Copy Path                   Ctrl+Shift+C│
│ Copy Full Path                          │
│ Paste                            Ctrl+V │
│ ─────────────────────────────────────── │
│ 🔄 Batch Rename...                   F2 │
│ 📊 Smart Organize...                    │
│ ─────────────────────────────────────── │
│ Delete                             Del  │
│ Rename                             F2   │
│ ─────────────────────────────────────── │
│ 📁 New                              ▶   │
│ ─────────────────────────────────────── │
│ Properties                    Alt+Enter │
└─────────────────────────────────────────┘
```

### Multi-Selection Context Menu

When multiple files are selected:

```
┌─────────────────────────────────────────┐
│ 47 items selected (12.4 GB)             │
│ ─────────────────────────────────────── │
│ Cut                              Ctrl+X │
│ Copy                             Ctrl+C │
│ ─────────────────────────────────────── │
│ 🔄 Batch Rename...               Ctrl+M │
│ 📊 Smart Organize...                    │
│ 📦 Archive Selected...                  │
│ ─────────────────────────────────────── │
│ Move to...                          ▶   │
│ Copy to...                          ▶   │
│ ─────────────────────────────────────── │
│ Select All                       Ctrl+A │
│ Invert Selection                        │
│ ─────────────────────────────────────── │
│ Delete                             Del  │
│ ─────────────────────────────────────── │
│ Properties                    Alt+Enter │
└─────────────────────────────────────────┘
```

### Windows Shell Integration

Register Winhance-FS context menu items in Windows Explorer:

```
┌─ SHELL INTEGRATION ───────────────────────────────────────────────┐
│                                                                    │
│  Add to Windows Explorer context menu:                            │
│                                                                    │
│  [x] "Open with Winhance File Manager"                            │
│  [x] "Batch Rename with Winhance"                                 │
│  [x] "Organize with Winhance"                                     │
│  [ ] "Scan with Winhance Deep Scan"                               │
│                                                                    │
│  [Register] [Unregister]                                          │
│                                                                    │
└────────────────────────────────────────────────────────────────────┘
```

---

## Batch Rename System

See [BATCH_RENAME.md](BATCH_RENAME.md) for complete documentation.

### Quick Preview

```
┌─ BATCH RENAME ────────────────────────────────────────────────────────────────┐
│                                                                                │
│  47 files selected                                                             │
│                                                                                │
│  ┌─ RENAME RULES ──────────────────────────────────────────────────────────┐  │
│  │                                                                          │  │
│  │  [1] Find & Replace:  "IMG_"  →  "Photo_"                               │  │
│  │  [2] Add Counter:     [Name]_[###].[ext]    Start: 001                  │  │
│  │  [3] Change Case:     Title Case                                        │  │
│  │                                                                          │  │
│  │  [+ Add Rule]                                                            │  │
│  └──────────────────────────────────────────────────────────────────────────┘  │
│                                                                                │
│  ┌─ PREVIEW ───────────────────────────────────────────────────────────────┐  │
│  │  Original Name              →  New Name                                 │  │
│  │  ──────────────────────────────────────────────────────────────────────  │  │
│  │  IMG_20260118_001.jpg       →  Photo_001.jpg                    ✓       │  │
│  │  IMG_20260118_002.jpg       →  Photo_002.jpg                    ✓       │  │
│  │  IMG_20260118_003.jpg       →  Photo_003.jpg                    ✓       │  │
│  │  IMG_vacation_sunset.jpg    →  Photo_004.jpg                    ✓       │  │
│  │  ...                                                                    │  │
│  └──────────────────────────────────────────────────────────────────────────┘  │
│                                                                                │
│  [Preview] [Apply Rename] [Undo Last] [Save Preset]                           │
│                                                                                │
└────────────────────────────────────────────────────────────────────────────────┘
```

---

## Smart Organizer

See [FILE_ORGANIZER.md](FILE_ORGANIZER.md) for complete documentation.

### Quick Preview

```
┌─ SMART ORGANIZER ─────────────────────────────────────────────────────────────┐
│                                                                                │
│  Source: C:\Users\Admin\Downloads (847 files, 23.4 GB)                        │
│                                                                                │
│  ┌─ ORGANIZATION PREVIEW ──────────────────────────────────────────────────┐  │
│  │                                                                          │  │
│  │  📁 Documents (124 files)                                               │  │
│  │     ├── 📄 PDF (45 files)                                               │  │
│  │     ├── 📝 Word (32 files)                                              │  │
│  │     └── 📊 Excel (47 files)                                             │  │
│  │                                                                          │  │
│  │  📁 Images (312 files)                                                  │  │
│  │     ├── 📷 Photos (245 files)                                           │  │
│  │     └── 🎨 Graphics (67 files)                                          │  │
│  │                                                                          │  │
│  │  📁 Videos (89 files)                                                   │  │
│  │  📁 Audio (156 files)                                                   │  │
│  │  📁 Archives (78 files)                                                 │  │
│  │  📁 Code (88 files)                                                     │  │
│  │                                                                          │  │
│  └──────────────────────────────────────────────────────────────────────────┘  │
│                                                                                │
│  [Organize by Type] [Organize by Date] [Organize by Project] [Custom Rules]   │
│                                                                                │
└────────────────────────────────────────────────────────────────────────────────┘
```

---

## Search & Filter

Advanced search with real-time results powered by Rust SIMD backend.

### Search Interface

```
┌─ ADVANCED SEARCH ─────────────────────────────────────────────────────────────┐
│                                                                                │
│  🔍 [*.pdf modified:today size:>1MB                                        ]  │
│                                                                                │
│  ┌─ FILTERS ───────────────────────────────────────────────────────────────┐  │
│  │                                                                          │  │
│  │  Location:  [C:\Users\Admin               ▼] [x] Include subfolders    │  │
│  │                                                                          │  │
│  │  Name:      [                              ] [x] Regex  [ ] Case       │  │
│  │                                                                          │  │
│  │  Type:      [All Types                    ▼]                            │  │
│  │                                                                          │  │
│  │  Size:      [Any     ▼]  to  [Any     ▼]                               │  │
│  │                                                                          │  │
│  │  Modified:  [Any     ▼]  to  [Any     ▼]                               │  │
│  │                                                                          │  │
│  │  Content:   [                              ] [ ] Index content          │  │
│  │                                                                          │  │
│  └──────────────────────────────────────────────────────────────────────────┘  │
│                                                                                │
│  Found 47 files in 0.003 seconds                                              │
│                                                                                │
│  ┌─ RESULTS ───────────────────────────────────────────────────────────────┐  │
│  │  Name                    Path                          Size    Modified │  │
│  │  ───────────────────────────────────────────────────────────────────────  │  │
│  │  📄 report_Q4.pdf       C:\Users\Admin\Documents      2.4 MB  Today    │  │
│  │  📄 invoice_jan.pdf     C:\Users\Admin\Downloads      156 KB  Today    │  │
│  │  📄 manual.pdf          C:\Users\Admin\Projects       4.1 MB  Today    │  │
│  └──────────────────────────────────────────────────────────────────────────┘  │
│                                                                                │
│  [Save Search] [Export Results] [Select All Results]                          │
│                                                                                │
└────────────────────────────────────────────────────────────────────────────────┘
```

### Search Syntax

| Syntax      | Example           | Description               |
| ----------- | ----------------- | ------------------------- |
| `*`         | `*.pdf`           | Wildcard matching         |
| `?`         | `file?.txt`       | Single character wildcard |
| `name:`     | `name:report`     | Search by name            |
| `ext:`      | `ext:pdf,docx`    | Filter by extension       |
| `size:`     | `size:>1GB`       | Filter by size            |
| `modified:` | `modified:today`  | Filter by date            |
| `created:`  | `created:2026`    | Filter by creation        |
| `path:`     | `path:projects`   | Filter by path            |
| `content:`  | `content:"hello"` | Search file contents      |
| `type:`     | `type:document`   | Filter by type category   |
| `regex:`    | `regex:^IMG_\d+`  | Regular expression        |

---

## Keyboard Shortcuts

Complete keyboard navigation support.

### Navigation

| Shortcut    | Action               |
| ----------- | -------------------- |
| `Enter`     | Open file/folder     |
| `Backspace` | Go to parent folder  |
| `Alt+←`     | Navigate back        |
| `Alt+→`     | Navigate forward     |
| `Alt+↑`     | Go to parent folder  |
| `Ctrl+L`    | Focus address bar    |
| `F5`        | Refresh              |
| `Tab`       | Switch between panes |

### File Operations

| Shortcut       | Action                  |
| -------------- | ----------------------- |
| `Ctrl+C`       | Copy                    |
| `Ctrl+X`       | Cut                     |
| `Ctrl+V`       | Paste                   |
| `Ctrl+Shift+C` | Copy path               |
| `Del`          | Delete (to Recycle Bin) |
| `Shift+Del`    | Permanent delete        |
| `F2`           | Rename                  |
| `Ctrl+M`       | Batch rename            |
| `Ctrl+Shift+N` | New folder              |
| `Ctrl+N`       | New file                |

### Selection

| Shortcut       | Action           |
| -------------- | ---------------- |
| `Ctrl+A`       | Select all       |
| `Ctrl+Click`   | Toggle selection |
| `Shift+Click`  | Range select     |
| `Ctrl+Shift+A` | Invert selection |
| `*` (numpad)   | Invert selection |
| `Esc`          | Clear selection  |

### Tabs

| Shortcut         | Action            |
| ---------------- | ----------------- |
| `Ctrl+T`         | New tab           |
| `Ctrl+W`         | Close tab         |
| `Ctrl+Tab`       | Next tab          |
| `Ctrl+Shift+Tab` | Previous tab      |
| `Ctrl+1-9`       | Switch to tab N   |
| `Ctrl+Shift+T`   | Reopen closed tab |

### View

| Shortcut | Action              |
| -------- | ------------------- |
| `Ctrl+1` | Details view        |
| `Ctrl+2` | List view           |
| `Ctrl+3` | Icons view          |
| `Ctrl+4` | Tiles view          |
| `Ctrl+H` | Show hidden files   |
| `F11`    | Toggle full screen  |
| `Ctrl+P` | Toggle preview pane |

---

## Configuration

### Settings File

Located at `%APPDATA%\Winhance-FS\file-manager.json`:

```json
{
  "defaultView": "details",
  "dualPaneMode": "horizontal",
  "showHiddenFiles": false,
  "showSystemFiles": false,
  "showFileExtensions": true,
  "confirmDelete": true,
  "confirmOverwrite": true,
  "rememberTabs": true,
  "maxTabs": 20,
  
  "columns": {
    "visible": ["name", "size", "type", "modified", "path"],
    "widths": {
      "name": 250,
      "size": 100,
      "type": 120,
      "modified": 150,
      "path": 300
    }
  },
  
  "favorites": [
    {
      "name": "Projects",
      "path": "D:\\Projects",
      "icon": "folder-code",
      "color": "#4CAF50"
    }
  ],
  
  "quickAccess": {
    "showFavorites": true,
    "showDrives": true,
    "showRecent": true,
    "maxRecentItems": 10
  },
  
  "shortcuts": {
    "customShortcuts": {
      "Ctrl+G": "goToPath",
      "Ctrl+Shift+F": "advancedSearch"
    }
  }
}
```

### Column Configuration

Available columns for file list:

| Column        | Description            |
| ------------- | ---------------------- |
| `name`        | File/folder name       |
| `ext`         | File extension         |
| `size`        | File size              |
| `type`        | File type description  |
| `modified`    | Last modified date     |
| `created`     | Creation date          |
| `accessed`    | Last accessed date     |
| `attributes`  | File attributes (RHSA) |
| `path`        | Full path              |
| `owner`       | File owner             |
| `permissions` | NTFS permissions       |

---

## Integration with Winhance-FS

### Storage Intelligence Integration

The File Manager integrates with Storage Intelligence features:

- **Space Analysis** - Visual indicators for large files
- **Recovery Items** - Highlight recoverable space
- **AI Models** - Special handling for model files
- **Duplicates** - Mark duplicate files

### MCP Tools

File Manager operations exposed via MCP:

```python
@mcp.tool()
async def fm_browse(path: str) -> dict:
    """Browse a directory and list contents."""
    
@mcp.tool()
async def fm_batch_rename(files: list, rules: list) -> dict:
    """Apply batch rename rules to files."""
    
@mcp.tool()
async def fm_organize(source: str, strategy: str) -> dict:
    """Organize files using smart categorization."""
```

### CLI Access

```bash
# Open File Manager
winhance-fs fm

# Browse specific path
winhance-fs fm --path "D:\Projects"

# Batch rename
winhance-fs fm rename --pattern "*.jpg" --rule "replace:IMG_:Photo_"

# Organize folder
winhance-fs fm organize --source "~/Downloads" --strategy "type"
```

---

*See also: [BATCH_RENAME.md](BATCH_RENAME.md) | [FILE_ORGANIZER.md](FILE_ORGANIZER.md) | [FEATURES.md](FEATURES.md)*
