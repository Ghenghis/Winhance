# Winhance-FS Features Guide

Complete documentation of all Winhance-FS features and capabilities.

> **Implementation Status Legend:**
> - ✅ **Implemented** - Feature is complete with UI and backend
> - ⚠️ **Backend Only** - Backend/CLI available, UI not yet implemented
> - 🚧 **Planned** - Documented for future implementation

---

## Storage Intelligence Dashboard 🚧 PLANNED

*Status: Backend services exist (`SpaceAnalyzerService.cs`), UI not yet implemented*

The main hub for storage analysis and management.

### Drive Overview

Real-time visualization of all connected drives:

```
┌──────────────────────────────────────────────────────────────────┐
│  DRIVE OVERVIEW                                                   │
│  ┌──────┐ ┌──────┐ ┌──────┐ ┌──────┐ ┌──────┐                   │
│  │ C:   │ │ D:   │ │ E:   │ │ F:   │ │ G:   │                   │
│  │ 44GB │ │1.2TB │ │ 67GB │ │739GB │ │177GB │                   │
│  │ CRIT │ │ Good │ │ Low  │ │ Good │ │ Low  │                   │
│  └──────┘ └──────┘ └──────┘ └──────┘ └──────┘                   │
└──────────────────────────────────────────────────────────────────┘
```

**Features:**
- Color-coded health indicators (Critical, Low, Good)
- Free space percentage
- Drive type detection (SSD, HDD, NVMe)
- Click to drill down into drive details

### Space Recovery Opportunities

Intelligent detection of reclaimable space:

| Category | Description | Typical Size |
|----------|-------------|--------------|
| **AI Models** | LM Studio, Ollama, HuggingFace models | 100GB - 500GB |
| **Developer Caches** | node_modules, .nuget, cargo target | 10GB - 50GB |
| **System Caches** | Windows temp, browser caches | 5GB - 20GB |
| **Duplicate Files** | Identical content across drives | Variable |
| **Large Files** | Files over configurable threshold | Variable |

**Recovery Actions:**
- **Relocate**: Move to larger drive with symlink
- **Clean**: Remove unnecessary files
- **Archive**: Compress rarely-used data
- **Delete**: Remove with confirmation

---

## Deep Scan Engine ✅ IMPLEMENTED

*Status: Fully implemented in Rust backend (`nexus_core/src/indexer/`)*

Ultra-fast file system scanning powered by Rust.

### MFT Direct Access

Bypasses Windows file APIs for maximum speed:

```
Performance Comparison:
┌────────────────────┬──────────────┬────────────┐
│ Method             │ 1M Files     │ 10M Files  │
├────────────────────┼──────────────┼────────────┤
│ Windows API        │ 45 seconds   │ 8 minutes  │
│ Everything Search  │ 2 seconds    │ 20 seconds │
│ Winhance-FS (MFT)  │ <1 second    │ <10 seconds│
└────────────────────┴──────────────┴────────────┘
```

### Search Capabilities

| Feature | Description |
|---------|-------------|
| **Instant Search** | Results as you type |
| **Regex Support** | Full regular expression patterns |
| **Fuzzy Matching** | Find similar names |
| **Content Search** | Search inside files |
| **Filter by Type** | Extension, size, date filters |
| **SIMD Acceleration** | 10+ GB/s throughput |

### Real-Time Monitoring

USN Journal integration for live updates:
- New file detection
- Modification tracking
- Deletion monitoring
- Rename tracking

---

## AI Model Manager ⚠️ BACKEND ONLY

*Status: Backend implemented (`nexus_ai/tools/model_relocator.py`), available via MCP tools. UI not yet implemented.*

Specialized management for AI/ML model collections.

### Supported Platforms

| Platform | Model Location | Supported |
|----------|----------------|-----------|
| LM Studio | `~/.lmstudio/models` | Yes |
| Ollama | `~/.ollama/models` | Yes |
| HuggingFace | `~/.cache/huggingface` | Yes |
| ComfyUI | `ComfyUI/models` | Yes |
| Stable Diffusion | `models/Stable-diffusion` | Yes |

### Features

**Model Discovery:**
- Automatic detection of model directories
- Model metadata extraction (size, format, quantization)
- Duplicate model detection across platforms

**Model Relocation:**
```
Before:
  C:\Users\You\.lmstudio\models\  (337 GB on SSD)

After:
  D:\AI-Models\.lmstudio\models\  (moved to HDD)
  C:\Users\You\.lmstudio\models → symlink to D:\

Result: 337 GB freed on C: drive
```

**Model Organization:**
- Sort by size, date, format
- Group by model family
- Tag and categorize
- Export/import model lists

---

## Cache Manager

Intelligent cache analysis and cleanup.

### Cache Categories

| Category | Locations | Safe to Clean |
|----------|-----------|---------------|
| **Package Managers** | npm, pip, cargo, nuget | Yes |
| **Build Outputs** | node_modules, target, bin | Mostly |
| **Browser Caches** | Chrome, Firefox, Edge | Yes |
| **Windows Temp** | %TEMP%, Windows\Temp | Yes |
| **Application Caches** | Various app data | Varies |

### Cleanup Rules

Create automated cleanup rules:

```yaml
rule: clean-old-node-modules
  pattern: "**/node_modules"
  condition: not_modified_days > 30
  action: delete
  confirm: false  # Auto-clean

rule: archive-old-projects
  pattern: "~/Projects/*"
  condition: not_accessed_days > 90
  action: archive
  destination: "D:/Archives"
  confirm: true
```

---

## Forensics Tools

Advanced file analysis capabilities.

### File Classification

AI-powered file type detection:

| Classification | Method |
|----------------|--------|
| **Magic Bytes** | Binary signature analysis |
| **Entropy** | Encryption/compression detection |
| **Structure** | Format validation |
| **Content** | Semantic analysis |

### Deleted File Recovery

Access deleted files via VSS:

```
┌─────────────────────────────────────────────────┐
│  SHADOW COPY BROWSER                            │
│                                                 │
│  Available Snapshots:                           │
│  ├─ 2024-01-15 10:30:22 (System Restore)       │
│  ├─ 2024-01-14 03:00:00 (Scheduled)            │
│  └─ 2024-01-12 15:45:11 (Manual)               │
│                                                 │
│  [Browse] [Restore Selected] [Compare]          │
└─────────────────────────────────────────────────┘
```

### Duplicate Detection

Find and manage duplicate files:

- **Hash-based**: xxHash3 for speed, SHA-256 for verification
- **Content-aware**: Ignore metadata differences
- **Smart grouping**: Keep originals, mark copies
- **Safe deletion**: Preserve at least one copy

---

## Borg Theme Studio 🚧 PLANNED

*Status: Theme infrastructure exists (`ThemeManager.cs`), custom studio UI not yet implemented.*

Visual customization with 1-5 color simplicity.

### Pre-defined Palettes

| Palette | Primary | Use Case |
|---------|---------|----------|
| **Borg Green** | #00FF41 | Classic terminal aesthetic |
| **Borg Red** | #FF0040 | Alert/tactical theme |
| **Borg Blue** | #00D4FF | Professional/calm |
| **Borg Purple** | #9B30FF | Creative/modern |
| **Borg Gold** | #FFD700 | Warm/premium |
| **Borg Orange** | #FF6600 | Energetic/vibrant |
| **Borg Pink** | #FF00FF | Bold/expressive |
| **Borg Neon** | #00FF00 | High contrast |

### Color Slots

Only 5 colors to configure:

1. **Primary** - Main accent color
2. **Secondary** - Supporting backgrounds
3. **Accent** - Highlights and focus
4. **Background** - Window backgrounds
5. **Text** - Primary text color

### Interactive Preview

Click any UI region to change its color:

```
┌─────────────────────────────────────────────────┐
│  SIMULATED GUI PREVIEW                          │
│  ┌──────────┐  ┌─────────────────────────────┐ │
│  │ Nav      │  │     Content Area            │ │
│  │ [click]  │  │     [click any region]      │ │
│  │          │  │  ┌─────────────────────┐    │ │
│  │ • Item   │  │  │   Feature Card      │    │ │
│  │ • Item   │  │  │   [clickable]       │    │ │
│  │ • Item   │  │  └─────────────────────┘    │ │
│  └──────────┘  └─────────────────────────────┘ │
└─────────────────────────────────────────────────┘
```

---

## MCP Integration ✅ IMPLEMENTED

*Status: Fully implemented (`nexus_mcp/server.py`)*

Connect with AI coding assistants.

### Available Tools

| Tool | Description |
|------|-------------|
| `nexus_search` | Search files with advanced filters |
| `nexus_index` | Build or update file index |
| `nexus_organize` | AI organization suggestions |
| `nexus_rollback` | Undo file operations |
| `nexus_space` | Disk space analysis |
| `nexus_models` | AI model management |
| `nexus_similar` | Find similar files |

### Configuration

**Claude Code:**
```json
{
  "mcpServers": {
    "winhance-fs": {
      "command": "python",
      "args": ["-m", "nexus_mcp.server"],
      "cwd": "C:/Program Files/Winhance-FS"
    }
  }
}
```

**Windsurf:**
```json
{
  "mcp": {
    "servers": {
      "winhance-fs": {
        "command": "winhance-fs",
        "args": ["--mcp"]
      }
    }
  }
}
```

---

## Transaction System ✅ IMPLEMENTED

*Status: Fully implemented (`nexus_ai/organization/transaction_manager.py`)*

Safe operations with full rollback support.

### How It Works

Every file operation is wrapped in a transaction:

```
1. Create VSS snapshot (restore point)
2. Log operation details
3. Execute operation
4. Verify success
5. Commit or rollback
```

### Rollback Capabilities

| Operation | Rollback Method |
|-----------|----------------|
| Move | Move back + remove symlink |
| Delete | Restore from VSS |
| Rename | Rename back |
| Clean | Restore from transaction log |

### Transaction Log

View and manage past operations:

```
┌───────────────────────────────────────────────────────────────┐
│  TRANSACTION HISTORY                                          │
│                                                               │
│  ID      Date       Operation    Status    [Actions]          │
│  T-001   Jan 15     Relocate     Success   [View] [Rollback]  │
│  T-002   Jan 15     Clean        Success   [View] [Rollback]  │
│  T-003   Jan 14     Delete       Rolled    [View]             │
└───────────────────────────────────────────────────────────────┘
```

---

## Command Line Interface

Full functionality via CLI.

### Basic Commands

```powershell
# Scan drives
winhance-fs scan --all

# Search files
winhance-fs search "*.gguf" --size ">1GB"

# Analyze storage
winhance-fs analyze C:

# Relocate models
winhance-fs relocate ~/.lmstudio D:/AI-Models --symlink

# Clean caches
winhance-fs clean --dry-run
winhance-fs clean --confirm

# Start MCP server
winhance-fs mcp --port 8080
```

### Output Formats

```powershell
# JSON output for scripting
winhance-fs scan --format json

# Table output for readability
winhance-fs scan --format table

# CSV for spreadsheets
winhance-fs scan --format csv
```

---

## Configuration

### Settings File

Located at `%APPDATA%\Winhance-FS\settings.json`:

```json
{
  "theme": "borg-green",
  "scanOnStartup": false,
  "monitorDrives": true,
  "alertThreshold": 0.1,
  "defaultRelocateTarget": "D:\\",
  "excludePaths": [
    "C:\\Windows",
    "C:\\Program Files"
  ],
  "cleanupRules": [
    {
      "name": "Old node_modules",
      "pattern": "**/node_modules",
      "maxAgeDays": 30
    }
  ]
}
```

### Environment Variables

| Variable | Description | Default |
|----------|-------------|---------|
| `WINHANCE_FS_HOME` | Installation directory | `%ProgramFiles%\Winhance-FS` |
| `WINHANCE_FS_DATA` | Data directory | `%APPDATA%\Winhance-FS` |
| `WINHANCE_FS_LOG_LEVEL` | Logging verbosity | `info` |

---

*See also: [Architecture Guide](ARCHITECTURE.md) | [Development Guide](DEVELOPMENT.md) | [Theming Guide](THEMING.md)*
