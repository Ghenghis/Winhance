# Smart File Organizer Guide 📊

The Smart File Organizer uses AI-powered categorization and rule-based automation to intelligently organize files across your drives.

## Table of Contents

- [Overview](#overview)
- [Organization Strategies](#organization-strategies)
- [AI Classification](#ai-classification)
- [Custom Rules](#custom-rules)
- [Automation](#automation)
- [Space Recovery](#space-recovery)
- [CLI Usage](#cli-usage)
- [MCP Integration](#mcp-integration)
- [Configuration](#configuration)

---

## Overview

```
┌─ SMART ORGANIZER DASHBOARD ───────────────────────────────────────────────────┐
│                                                                                │
│  ┌─ SOURCE SELECTION ──────────────────────────────────────────────────────┐  │
│  │                                                                          │  │
│  │  Source: C:\Users\Admin\Downloads         [Browse...] [Recent ▼]        │  │
│  │                                                                          │  │
│  │  📊 847 files (23.4 GB) │ 156 folders │ Last modified: Today            │  │
│  │                                                                          │  │
│  └──────────────────────────────────────────────────────────────────────────┘  │
│                                                                                │
│  ┌─ ORGANIZATION STRATEGY ─────────────────────────────────────────────────┐  │
│  │                                                                          │  │
│  │  ● By File Type     ○ By Date       ○ By Project    ○ By AI Category   │  │
│  │  ○ By Size          ○ By Source     ○ Custom Rules  ○ Duplicate Clean  │  │
│  │                                                                          │  │
│  │  Destination: [Same folder with subfolders              ▼]              │  │
│  │               ☐ Move to: D:\Organized\[Category]                        │  │
│  │                                                                          │  │
│  └──────────────────────────────────────────────────────────────────────────┘  │
│                                                                                │
│  ┌─ PREVIEW ───────────────────────────────────────────────────────────────┐  │
│  │                                                                          │  │
│  │  📁 Documents (124 files → D:\Organized\Documents)                      │  │
│  │     ├── 📄 PDF (45 files, 234 MB)                                       │  │
│  │     ├── 📝 Word (32 files, 89 MB)                                       │  │
│  │     ├── 📊 Excel (28 files, 156 MB)                                     │  │
│  │     └── 📑 Other (19 files, 45 MB)                                      │  │
│  │                                                                          │  │
│  │  📁 Images (312 files → D:\Organized\Images)                            │  │
│  │     ├── 📷 Photos (245 files, 2.3 GB)                                   │  │
│  │     │     └── By Date: 2026-01, 2026-02, ...                            │  │
│  │     └── 🎨 Graphics (67 files, 890 MB)                                  │  │
│  │                                                                          │  │
│  │  📁 Videos (89 files → D:\Organized\Videos)                             │  │
│  │  📁 Audio (156 files → D:\Organized\Audio)                              │  │
│  │  📁 Archives (78 files → D:\Organized\Archives)                         │  │
│  │  📁 Code (88 files → D:\Organized\Code)                                 │  │
│  │                                                                          │  │
│  │  ⚠ Unclassified: 0 files                                               │  │
│  │                                                                          │  │
│  └──────────────────────────────────────────────────────────────────────────┘  │
│                                                                                │
│  [Analyze] [Preview Changes] [Apply Organization] [Create Undo Point]         │
│                                                                                │
└────────────────────────────────────────────────────────────────────────────────┘
```

### Key Features

| Feature                 | Description                                            |
| ----------------------- | ------------------------------------------------------ |
| **AI Classification**   | Intelligent file categorization using content analysis |
| **Multiple Strategies** | Organize by type, date, project, size, or custom rules |
| **Preview Mode**        | See all changes before applying                        |
| **Transaction Logging** | Complete rollback support                              |
| **Automation**          | Schedule recurring organization tasks                  |
| **Space Recovery**      | Identify and clean duplicate/temp files                |
| **Cross-Drive Support** | Organize across C:, D:, E:, F:, G: drives              |

---

## Organization Strategies

### By File Type

Organize files into category folders based on extension:

```
Downloads/
├── Documents/
│   ├── PDF/
│   ├── Word/
│   ├── Excel/
│   ├── PowerPoint/
│   └── Text/
├── Images/
│   ├── Photos/ (jpg, jpeg, png, heic)
│   └── Graphics/ (svg, ai, psd, xcf)
├── Videos/
├── Audio/
├── Archives/ (zip, rar, 7z, tar)
├── Code/ (py, js, ts, cs, rs, cpp)
├── Executables/ (exe, msi, bat, ps1)
└── Other/
```

### By Date

Organize by modification or creation date:

```
Downloads/
├── 2026/
│   ├── January/
│   │   ├── Week 1/
│   │   ├── Week 2/
│   │   └── ...
│   ├── February/
│   └── ...
└── 2025/
    └── ...
```

### By Project

AI-powered project detection and grouping:

```
Downloads/
├── Winhance-FS/
│   ├── docs/
│   ├── src/
│   └── assets/
├── AI-Models/
│   ├── LLaMA/
│   ├── Mistral/
│   └── ...
├── Evony-Project/
└── Uncategorized/
```

### By Size

Group files by size ranges:

```
Downloads/
├── Large (>1GB)/
├── Medium (100MB-1GB)/
├── Small (1MB-100MB)/
└── Tiny (<1MB)/
```

### By AI Category

Semantic categorization using content analysis:

```
Downloads/
├── Work/
│   ├── Reports/
│   ├── Invoices/
│   └── Presentations/
├── Personal/
│   ├── Receipts/
│   ├── Photos/
│   └── Documents/
├── Development/
│   ├── Source Code/
│   ├── Libraries/
│   └── Documentation/
└── Media/
    ├── Entertainment/
    └── Educational/
```

---

## AI Classification

### How It Works

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                        AI CLASSIFICATION PIPELINE                            │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│   ┌─────────────┐     ┌─────────────┐     ┌─────────────┐                  │
│   │   File      │     │  Content    │     │  Embedding  │                  │
│   │  Discovery  │ ──► │  Analysis   │ ──► │  Generation │                  │
│   └─────────────┘     └─────────────┘     └─────────────┘                  │
│         │                   │                   │                           │
│         │                   │                   │                           │
│         ▼                   ▼                   ▼                           │
│   ┌─────────────┐     ┌─────────────┐     ┌─────────────┐                  │
│   │  Metadata   │     │    OCR      │     │   Vector    │                  │
│   │  Extraction │     │  (Images)   │     │   Search    │                  │
│   └─────────────┘     └─────────────┘     └─────────────┘                  │
│         │                   │                   │                           │
│         └───────────────────┼───────────────────┘                           │
│                             │                                               │
│                             ▼                                               │
│                    ┌─────────────────┐                                      │
│                    │   Category      │                                      │
│                    │   Assignment    │                                      │
│                    └─────────────────┘                                      │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
```

### Supported Analysis

| File Type     | Analysis Method                       |
| ------------- | ------------------------------------- |
| **Documents** | Text extraction, keyword analysis     |
| **Images**    | CLIP embeddings, OCR, EXIF metadata   |
| **Audio**     | ID3 tags, speech-to-text (optional)   |
| **Video**     | Keyframe analysis, metadata           |
| **Code**      | Language detection, project inference |
| **Archives**  | Content listing, nested analysis      |

### Classification Confidence

```
┌─ CLASSIFICATION RESULTS ────────────────────────────────────────────────────┐
│                                                                              │
│  File: annual_report_2025.pdf                                               │
│                                                                              │
│  Detected Categories:                                                        │
│  ├── 📊 Work/Reports ────────────────────────── 94% ████████████████████░  │
│  ├── 📄 Documents/PDF ───────────────────────── 88% █████████████████░░░░  │
│  └── 💼 Business/Financial ──────────────────── 72% ██████████████░░░░░░░  │
│                                                                              │
│  Suggested Location: D:\Organized\Work\Reports\2025\                        │
│                                                                              │
│  Keywords: annual, report, financial, revenue, Q4, 2025                     │
│                                                                              │
│  [Accept] [Change Category] [Skip] [Add to Training]                        │
│                                                                              │
└──────────────────────────────────────────────────────────────────────────────┘
```

---

## Custom Rules

### Rule Editor

```
┌─ CUSTOM RULES ──────────────────────────────────────────────────────────────┐
│                                                                              │
│  ┌─ Rule 1: Screenshots to Desktop ────────────────────────────────────┐   │
│  │                                                                      │   │
│  │  IF:   name contains "Screenshot" OR name contains "Snip"           │   │
│  │  AND:  extension is (png, jpg)                                      │   │
│  │  THEN: move to "C:\Users\Admin\Desktop\Screenshots"                 │   │
│  │        rename to "[date]_[time]_[original]"                         │   │
│  │                                                                      │   │
│  │  [Edit] [Disable] [Delete]                              Priority: 1 │   │
│  └──────────────────────────────────────────────────────────────────────┘   │
│                                                                              │
│  ┌─ Rule 2: AI Models to G: Drive ─────────────────────────────────────┐   │
│  │                                                                      │   │
│  │  IF:   extension is (gguf, safetensors, bin, ckpt, pt)              │   │
│  │  AND:  size > 1GB                                                   │   │
│  │  THEN: move to "G:\AI-Models\[parent-folder]"                       │   │
│  │        create symlink at original location                          │   │
│  │                                                                      │   │
│  │  [Edit] [Disable] [Delete]                              Priority: 2 │   │
│  └──────────────────────────────────────────────────────────────────────┘   │
│                                                                              │
│  ┌─ Rule 3: Old Downloads Cleanup ─────────────────────────────────────┐   │
│  │                                                                      │   │
│  │  IF:   location is "Downloads"                                      │   │
│  │  AND:  modified > 30 days ago                                       │   │
│  │  AND:  not accessed in 30 days                                      │   │
│  │  THEN: move to "E:\Archive\Downloads\[year]\[month]"                │   │
│  │                                                                      │   │
│  │  [Edit] [Disable] [Delete]                              Priority: 3 │   │
│  └──────────────────────────────────────────────────────────────────────┘   │
│                                                                              │
│  [+ Add Rule] [Import Rules] [Export Rules]                                  │
│                                                                              │
└──────────────────────────────────────────────────────────────────────────────┘
```

### Rule Conditions

| Condition   | Operators                              | Examples                     |
| ----------- | -------------------------------------- | ---------------------------- |
| `name`      | contains, starts, ends, matches, regex | `name contains "report"`     |
| `extension` | is, is not, in                         | `extension in (pdf, docx)`   |
| `size`      | >, <, >=, <=, between                  | `size > 100MB`               |
| `modified`  | >, <, =, between                       | `modified > 30 days ago`     |
| `created`   | >, <, =, between                       | `created this year`          |
| `accessed`  | >, <, =, between                       | `not accessed in 90 days`    |
| `location`  | is, contains, under                    | `location under "Downloads"` |
| `type`      | is                                     | `type is image`              |
| `content`   | contains (with indexing)               | `content contains "invoice"` |

### Rule Actions

| Action     | Parameters  | Description                |
| ---------- | ----------- | -------------------------- |
| `move`     | destination | Move file to new location  |
| `copy`     | destination | Copy file to location      |
| `rename`   | pattern     | Rename using pattern       |
| `delete`   | to_recycle  | Delete file                |
| `tag`      | tags        | Add metadata tags          |
| `compress` | format      | Compress file              |
| `symlink`  | -           | Create symlink at original |
| `notify`   | message     | Show notification          |

### Rule Variables

| Variable   | Description           | Example    |
| ---------- | --------------------- | ---------- |
| `[name]`   | Original filename     | report     |
| `[ext]`    | Original extension    | pdf        |
| `[date]`   | Current date          | 2026-01-18 |
| `[time]`   | Current time          | 143052     |
| `[year]`   | Year                  | 2026       |
| `[month]`  | Month                 | 01         |
| `[day]`    | Day                   | 18         |
| `[parent]` | Parent folder name    | Downloads  |
| `[size]`   | File size             | 2.4MB      |
| `[hash:4]` | First 4 chars of hash | a1b2       |

---

## Automation

### Watch Folders

Monitor folders for automatic organization:

```
┌─ WATCH FOLDERS ─────────────────────────────────────────────────────────────┐
│                                                                              │
│  Folder                          Rules            Status      Last Run      │
│  ─────────────────────────────────────────────────────────────────────────  │
│  C:\Users\Admin\Downloads        3 rules          ● Active    2 min ago    │
│  C:\Users\Admin\Desktop          1 rule           ● Active    5 min ago    │
│  D:\Projects                     2 rules          ○ Paused    1 hour ago   │
│                                                                              │
│  [+ Add Watch Folder] [Pause All] [View Logs]                               │
│                                                                              │
└──────────────────────────────────────────────────────────────────────────────┘
```

### Scheduled Tasks

```
┌─ SCHEDULED TASKS ───────────────────────────────────────────────────────────┐
│                                                                              │
│  Task                      Schedule            Next Run        Status       │
│  ─────────────────────────────────────────────────────────────────────────  │
│  Daily Downloads Cleanup   Daily at 2:00 AM    Tomorrow        ● Enabled   │
│  Weekly Archive Move       Sundays at 3:00 AM  Jan 21, 2026    ● Enabled   │
│  Monthly Duplicate Scan    1st of month        Feb 1, 2026     ● Enabled   │
│                                                                              │
│  [+ Add Schedule] [Edit] [Run Now]                                          │
│                                                                              │
└──────────────────────────────────────────────────────────────────────────────┘
```

---

## Space Recovery

### Recovery Dashboard

```
┌─ SPACE RECOVERY ────────────────────────────────────────────────────────────┐
│                                                                              │
│  Drive: C:\ (44 GB free of 256 GB)  [CRITICAL]                             │
│  ████████████████████████████████████████████░░░░  83% used                │
│                                                                              │
│  ┌─ RECOVERY OPPORTUNITIES ────────────────────────────────────────────┐   │
│  │                                                                      │   │
│  │  Category                    Size        Items    Action             │   │
│  │  ────────────────────────────────────────────────────────────────   │   │
│  │  ☑ AI Models (.lmstudio)    337 GB       428    [Relocate to D:]   │   │
│  │  ☑ AI Models (.ollama)      163 GB       89     [Relocate to D:]   │   │
│  │  ☑ Development Cache        44 GB        12k    [Clean]            │   │
│  │  ☐ Temp Files               12 GB        45k    [Safe Delete]      │   │
│  │  ☐ Duplicate Files          8 GB         234    [Review]           │   │
│  │  ☐ Browser Cache            6 GB         89k    [Clean]            │   │
│  │  ☐ Old Downloads            4 GB         156    [Archive to E:]    │   │
│  │                                                                      │   │
│  │  Total Selected: 544 GB                                             │   │
│  │                                                                      │   │
│  └──────────────────────────────────────────────────────────────────────┘   │
│                                                                              │
│  [Analyze Selected] [Execute Selected] [Create Recovery Plan]               │
│                                                                              │
└──────────────────────────────────────────────────────────────────────────────┘
```

### Model Relocation

Special handling for AI model files:

```
┌─ AI MODEL MANAGER ──────────────────────────────────────────────────────────┐
│                                                                              │
│  Current Location: C:\Users\Admin\.lmstudio\models                          │
│  Total Size: 337 GB (428 models)                                            │
│                                                                              │
│  ┌─ TOP 10 LARGEST MODELS ─────────────────────────────────────────────┐   │
│  │  ☑ Llama-3.1-70B-Q4_K_M.gguf         42.5 GB    Last used: Today   │   │
│  │  ☑ Qwen2.5-72B-Q4_K_M.gguf           41.2 GB    Last used: Today   │   │
│  │  ☑ DeepSeek-V3-Q4_K_S.gguf           38.8 GB    Last used: 2 days  │   │
│  │  ☐ Mixtral-8x22B-Q3_K_M.gguf         36.1 GB    Last used: 1 week  │   │
│  │  ☐ Yi-34B-200K-Q4_K_M.gguf           24.2 GB    Last used: 1 month │   │
│  │  ...                                                                │   │
│  └──────────────────────────────────────────────────────────────────────┘   │
│                                                                              │
│  Destination: D:\AI-Models\LMStudio                                         │
│                                                                              │
│  Options:                                                                    │
│  ☑ Create symlinks at original location (apps still work)                  │
│  ☑ Verify integrity after move (SHA256)                                    │
│  ☑ Generate rollback script                                                │
│                                                                              │
│  [Preview] [Relocate Selected] [Relocate All]                               │
│                                                                              │
└──────────────────────────────────────────────────────────────────────────────┘
```

### Duplicate Detection

```
┌─ DUPLICATE FILES ───────────────────────────────────────────────────────────┐
│                                                                              │
│  Scan Type:  ● By Hash (exact)  ○ By Name  ○ By Size  ○ Similar Images     │
│                                                                              │
│  Found: 234 duplicate groups (8.2 GB recoverable)                           │
│                                                                              │
│  ┌─ GROUP 1: report_final.pdf (5 copies, 15 MB total) ─────────────────┐   │
│  │  ☑ Keep: C:\Users\Admin\Documents\report_final.pdf        (original) │   │
│  │  ☐ C:\Users\Admin\Desktop\report_final.pdf                (copy)     │   │
│  │  ☐ C:\Users\Admin\Downloads\report_final.pdf              (copy)     │   │
│  │  ☐ D:\Backup\Documents\report_final.pdf                   (copy)     │   │
│  │  ☐ E:\Archive\2025\report_final.pdf                       (copy)     │   │
│  └──────────────────────────────────────────────────────────────────────┘   │
│                                                                              │
│  ┌─ GROUP 2: photo_backup.zip (3 copies, 1.2 GB total) ────────────────┐   │
│  │  ...                                                                 │   │
│  └──────────────────────────────────────────────────────────────────────┘   │
│                                                                              │
│  [Auto-Select (keep oldest)] [Delete Selected] [Move to Duplicates Folder] │
│                                                                              │
└──────────────────────────────────────────────────────────────────────────────┘
```

---

## CLI Usage

### Basic Commands

```powershell
# Analyze folder
winhance-fs organize analyze C:\Users\Admin\Downloads

# Preview organization by type
winhance-fs organize preview --strategy type

# Apply organization
winhance-fs organize apply --strategy type --destination D:\Organized

# Run custom rules
winhance-fs organize rules --run

# Watch folder for changes
winhance-fs organize watch C:\Users\Admin\Downloads

# Space recovery analysis
winhance-fs organize space C:\ --find-recoverable

# Duplicate scan
winhance-fs organize duplicates C:\ --by-hash

# Model relocation
winhance-fs organize models --relocate D:\AI-Models --symlink
```

### Options

| Option          | Description                                                   |
| --------------- | ------------------------------------------------------------- |
| `--strategy`    | Organization strategy (type, date, project, size, ai, custom) |
| `--destination` | Destination folder                                            |
| `--preview`     | Preview only, don't execute                                   |
| `--recursive`   | Include subfolders                                            |
| `--min-size`    | Minimum file size                                             |
| `--max-size`    | Maximum file size                                             |
| `--older-than`  | Files older than duration                                     |
| `--symlink`     | Create symlinks at original location                          |
| `--verify`      | Verify integrity after move                                   |
| `--dry-run`     | Show what would be done                                       |

---

## MCP Integration

### Available Tools

```python
@mcp.tool()
async def organize_analyze(
    path: str,
    strategy: str = "type"
) -> dict:
    """
    Analyze folder and preview organization.

    Args:
        path: Folder to analyze
        strategy: Organization strategy (type, date, project, size, ai)

    Returns:
        Preview of organization with categories and file counts.
    """

@mcp.tool()
async def organize_apply(
    path: str,
    strategy: str,
    destination: str | None = None
) -> dict:
    """
    Apply organization to folder.

    Returns:
        Transaction ID and summary of changes.
    """

@mcp.tool()
async def organize_recover_space(
    drive: str,
    actions: list[str] | None = None
) -> dict:
    """
    Find and recover wasted space on drive.

    Args:
        drive: Drive letter (e.g., "C:")
        actions: Specific actions (models, cache, duplicates, temp)

    Returns:
        Recoverable items with size and recommended actions.
    """

@mcp.tool()
async def organize_relocate_models(
    source: str,
    destination: str,
    create_symlinks: bool = True
) -> dict:
    """
    Relocate AI models to another drive with symlinks.

    Returns:
        Transaction ID and list of relocated models.
    """
```

### Example Workflow

```python
# Claude Code example
# 1. Analyze Downloads folder
analysis = await organize_analyze(
    path="C:\\Users\\Admin\\Downloads",
    strategy="type"
)

# 2. Review analysis
print(f"Found {analysis['total_files']} files in {len(analysis['categories'])} categories")

# 3. Apply organization
result = await organize_apply(
    path="C:\\Users\\Admin\\Downloads",
    strategy="type",
    destination="D:\\Organized"
)

# 4. Check space recovery options
recovery = await organize_recover_space(drive="C:")
print(f"Recoverable: {recovery['total_recoverable_gb']} GB")

# 5. Relocate AI models if needed
if recovery['models']['size_gb'] > 100:
    await organize_relocate_models(
        source=recovery['models']['path'],
        destination="D:\\AI-Models",
        create_symlinks=True
    )
```

---

## Configuration

### Settings File

Located at `%APPDATA%\Winhance-FS\organizer.json`:

```json
{
  "defaultStrategy": "type",
  "defaultDestination": "D:\\Organized",
  "createSymlinks": true,
  "verifyIntegrity": true,
  "preserveTimestamps": true,
  
  "categories": {
    "documents": ["pdf", "doc", "docx", "xls", "xlsx", "ppt", "pptx", "txt", "rtf", "odt"],
    "images": ["jpg", "jpeg", "png", "gif", "bmp", "svg", "webp", "heic", "raw", "tiff"],
    "videos": ["mp4", "mkv", "avi", "mov", "wmv", "flv", "webm"],
    "audio": ["mp3", "wav", "flac", "aac", "ogg", "wma", "m4a"],
    "archives": ["zip", "rar", "7z", "tar", "gz", "bz2"],
    "code": ["py", "js", "ts", "cs", "rs", "cpp", "c", "h", "java", "go", "rb"],
    "models": ["gguf", "safetensors", "bin", "ckpt", "pt", "pth"]
  },
  
  "watchFolders": [
    {
      "path": "C:\\Users\\Admin\\Downloads",
      "rules": ["screenshots", "models", "old-downloads"],
      "enabled": true
    }
  ],
  
  "schedules": [
    {
      "name": "Daily Cleanup",
      "cron": "0 2 * * *",
      "action": "organize",
      "path": "C:\\Users\\Admin\\Downloads",
      "strategy": "type"
    }
  ],
  
  "spaceRecovery": {
    "criticalThreshold": 50,
    "warningThreshold": 100,
    "modelPaths": [
      ".lmstudio/models",
      ".ollama/models",
      ".cache/huggingface"
    ],
    "cachePaths": [
      ".cache",
      ".npm",
      "node_modules",
      ".venv",
      "target"
    ]
  }
}
```

---

## Transaction & Rollback

All organization operations are logged for complete rollback:

```json
{
  "transaction_id": "org_20260118_143052_abc123",
  "timestamp": "2026-01-18T14:30:52Z",
  "operation": "organize",
  "strategy": "type",
  "source": "C:\\Users\\Admin\\Downloads",
  "destination": "D:\\Organized",
  "files_moved": 847,
  "total_size_bytes": 25123456789,
  "moves": [
    {
      "source": "C:\\Users\\Admin\\Downloads\\report.pdf",
      "destination": "D:\\Organized\\Documents\\PDF\\report.pdf",
      "size": 2456789,
      "hash": "sha256:abc123..."
    }
  ],
  "rollback_script": "D:\\NexusFS\\data\\transactions\\rollback_org_20260118_143052_abc123.ps1"
}
```

### Rollback Commands

```powershell
# List recent organization operations
winhance-fs rollback list --type organize

# Undo specific organization
winhance-fs rollback org_20260118_143052_abc123

# Generate rollback script without executing
winhance-fs rollback org_20260118_143052_abc123 --script-only
```

---

*See also: [FILE_MANAGER.md](FILE_MANAGER.md) | [BATCH_RENAME.md](BATCH_RENAME.md) | [STORAGE.md](STORAGE.md)*
