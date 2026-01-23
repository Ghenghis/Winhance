# Storage Intelligence Module

## Overview

The Storage Intelligence module provides deep file system analysis, space recovery recommendations, and safe file operations with complete rollback support.

## Features

### 1. Drive Space Analysis

Real-time overview of all connected drives with health indicators.

```
+------------------------------------------------------------------+
| DRIVE OVERVIEW                                                    |
|                                                                   |
|  +--------+  +--------+  +--------+  +--------+  +--------+      |
|  |  C:    |  |  D:    |  |  E:    |  |  F:    |  |  G:    |      |
|  | 44 GB  |  | 1.2 TB |  | 67 GB  |  | 739 GB |  | 177 GB |      |
|  | [CRIT] |  | [Good] |  | [Low]  |  | [Good] |  | [Low]  |      |
|  +--------+  +--------+  +--------+  +--------+  +--------+      |
+------------------------------------------------------------------+
```

Status indicators:
- **CRITICAL** - <5% free space (red)
- **Low** - 5-15% free space (yellow)
- **Good** - 15-50% free space (green)
- **Excellent** - >50% free space (blue)

### 2. Space Recovery Recommendations

Intelligent detection of space-consuming items with safe actions.

```
+------------------------------------------------------------------+
| SPACE RECOVERY OPPORTUNITIES                                      |
|                                                                   |
|  [*] .lmstudio (C:)           337 GB  [Relocate to D:]           |
|  [*] .ollama (C:)             163 GB  [Relocate to D:]           |
|  [*] .cache (C:)               44 GB  [Clean] [Relocate]         |
|  [ ] node_modules (various)    28 GB  [Clean unused]             |
|  [ ] Temp files                12 GB  [Safe Delete]              |
|  [ ] Duplicate files            8 GB  [Review]                   |
|                                                                   |
|  Total Recoverable: 592 GB   [Execute Selected] [Preview]        |
+------------------------------------------------------------------+
```

Detected categories:
- **AI Models** - .lmstudio, .ollama, .cache/huggingface
- **Development** - node_modules, .venv, target/, build/
- **System Cache** - Temp files, browser cache, logs
- **Duplicates** - Identical files across drives
- **Large Files** - Single files over configurable threshold
- **Old Files** - Unused files beyond age threshold

### 3. Deep Scan (MFT-Based)

Ultra-fast scanning using direct NTFS MFT access.

**Performance:**
- 1 million files: <1 second
- Full drive index: 3-5 seconds
- Real-time updates via USN Journal

**Detected information:**
- File path, name, size
- Creation, modification, access times
- NTFS attributes (hidden, system, compressed)
- Alternate Data Streams (ADS)
- Deleted file remnants

### 4. AI Model Manager

Specialized management for AI/ML model files.

```
+------------------------------------------------------------------+
| AI MODEL MANAGER                                                  |
|                                                                   |
| Current Location: C:\Users\Admin\.lmstudio                        |
| Total Size: 337 GB (428 models)                                   |
|                                                                   |
| Top 10 Largest Models:                                            |
|  [*] Llama-3.1-70B-Q4_K_M.gguf      42.5 GB                      |
|  [*] Qwen2.5-72B-Q4_K_M.gguf        41.2 GB                      |
|  [ ] DeepSeek-V3-Q4_K_S.gguf        38.8 GB                      |
|  [ ] Mixtral-8x22B-Q3_K_M.gguf      36.1 GB                      |
|  ...                                                              |
|                                                                   |
| [Relocate Selected to D:\Models]  [Create Symlinks]              |
+------------------------------------------------------------------+
```

Features:
- Automatic detection of .gguf, .safetensors, .bin models
- Size analysis and recommendations
- One-click relocation with symlink creation
- Duplicate model detection

### 5. Forensics Tools

Advanced NTFS analysis capabilities.

**Alternate Data Streams (ADS)**
```
File: document.pdf
Main Data: 1.2 MB
Streams:
  :Zone.Identifier     - 26 bytes (download source)
  :encryptable         - 0 bytes (EFS marker)
  :$DATA               - 1.2 MB (main content)
```

**VSS Shadow Copies**
```
Shadow Copies for C:\
  ID: {abc123...}  Created: 2026-01-15 10:30  Size: 12 GB
  ID: {def456...}  Created: 2026-01-10 08:15  Size: 15 GB

[Restore File from Shadow] [Compare Versions]
```

**Deleted File Recovery**
- MFT-based detection of recently deleted files
- Recovery before overwrite
- Secure deletion verification

## Safe Operations

### Transaction System

Every file operation is logged for rollback:

```json
{
  "transaction_id": "tx_20260118_143052_abc123",
  "timestamp": "2026-01-18T14:30:52Z",
  "operations": [
    {
      "type": "move",
      "source": "C:\\Users\\Admin\\.lmstudio\\models\\Llama-3.1-70B.gguf",
      "destination": "D:\\Models\\Llama-3.1-70B.gguf",
      "size_bytes": 45632847820,
      "hash_sha256": "abc123...",
      "symlink_created": "C:\\Users\\Admin\\.lmstudio\\models\\Llama-3.1-70B.gguf"
    }
  ],
  "status": "completed"
}
```

### Rollback Support

```
+------------------------------------------------------------------+
| ROLLBACK MANAGER                                                  |
|                                                                   |
| Recent Transactions:                                              |
|  [>] 2026-01-18 14:30 - Moved 3 models (127 GB)         [Undo]   |
|  [>] 2026-01-17 09:15 - Cleaned temp files (8 GB)       [Undo]   |
|  [>] 2026-01-15 16:45 - Relocated .ollama (163 GB)      [Undo]   |
|                                                                   |
| [View All Transactions]  [Auto-Generated Scripts]                 |
+------------------------------------------------------------------+
```

Auto-generated rollback scripts:
- `.bat` scripts for command-line restore
- `.ps1` scripts for PowerShell
- JSON transaction logs

## Integration with Winhance

### Feature Registration

```csharp
// FeatureIds.cs
public const string StorageIntelligence = "StorageIntelligence";
public const string DeepScan = "DeepScan";
public const string ModelManager = "ModelManager";
public const string CacheManager = "CacheManager";
public const string ForensicsTools = "ForensicsTools";

// FeatureDefinitions.cs
new(FeatureIds.StorageIntelligence, "Storage Intelligence", "ChartDonut3", "Storage", 1),
new(FeatureIds.DeepScan, "Deep Scan", "Radar", "Storage", 2),
new(FeatureIds.ModelManager, "AI Model Manager", "Brain", "Storage", 3),
```

### ViewModel Pattern

```csharp
public partial class StorageIntelligenceViewModel : BaseSettingsFeatureViewModel
{
    private readonly INexusNativeService _nativeService;
    private readonly ISpaceAnalysisService _spaceService;

    public override string ModuleId => FeatureIds.StorageIntelligence;

    [ObservableProperty]
    private ObservableCollection<DriveInfoModel> _drives;

    [ObservableProperty]
    private ObservableCollection<SpaceRecoveryItem> _recoveryItems;

    [RelayCommand]
    private async Task ScanDrivesAsync()
    {
        IsLoading = true;
        try
        {
            var result = await _nativeService.ScanAllDrivesAsync();
            if (result.Success)
            {
                Drives = new(result.Result.Drives);
                RecoveryItems = new(result.Result.RecoveryItems);
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task RelocateWithSymlinkAsync(RelocateRequest request)
    {
        var result = await _nativeService.RelocateWithSymlinkAsync(
            request.Source,
            request.Destination,
            createSymlink: true,
            verifyAfterMove: true
        );
        // Handle result...
    }
}
```

## Rust Backend Integration

Storage Intelligence uses the Rust backend for performance-critical operations:

```rust
// nexus-native/src/lib.rs

#[uniffi::export]
pub async fn scan_all_drives() -> DrivesScanResult {
    // Fast MFT-based scanning
}

#[uniffi::export]
pub async fn relocate_with_symlink(
    source: String,
    destination: String,
    create_symlink: bool,
    verify: bool,
) -> Result<MoveResult, NexusError> {
    // Transaction-based file move
}

#[uniffi::export]
pub async fn get_space_recovery_items(
    drive_letter: String,
) -> Vec<SpaceRecoveryItem> {
    // AI model detection, cache analysis
}
```

## Configuration

Settings in `appsettings.json`:

```json
{
  "StorageIntelligence": {
    "ScanIntervalMinutes": 60,
    "LargeFileThresholdMB": 100,
    "OldFileThresholdDays": 180,
    "AutoDetectAIModels": true,
    "ModelPaths": [
      ".lmstudio",
      ".ollama",
      ".cache/huggingface"
    ],
    "ExcludePaths": [
      "Windows",
      "Program Files",
      "$Recycle.Bin"
    ],
    "DefaultTargetDrive": "D:",
    "CreateSymlinksOnRelocate": true,
    "VerifyAfterMove": true
  }
}
```

## CLI Access

Storage Intelligence features are available via CLI:

```bash
# Scan drives
winhance-fs scan --all

# List recovery items
winhance-fs recovery --list

# Relocate AI models
winhance-fs relocate --source "C:\.lmstudio" --dest "D:\Models" --symlink

# Deep scan
winhance-fs deepscan C: --output report.json

# Rollback
winhance-fs rollback --transaction tx_20260118_143052_abc123
```

## MCP Integration

See [MCP_INTEGRATION.md](MCP_INTEGRATION.md) for AI tool integration.

```python
# Example: Claude Code accessing Storage Intelligence
await nexus_scan()           # Scan all drives
await nexus_recovery()       # Get recovery suggestions
await nexus_relocate(...)    # Move files with rollback
```
