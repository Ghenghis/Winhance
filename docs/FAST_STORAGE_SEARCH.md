# Fast Storage Search Guide

**The Fastest Way to Find What's Eating Your 3.4TB**

---

## Quick Start Commands

### Immediate Space Analysis (CLI)

```powershell
# Find what's taking up space on ALL drives
nexus space analyze C:\ --large 1
nexus space analyze D:\ --large 1
nexus space analyze E:\ --large 1
nexus space analyze F:\ --large 1
nexus space analyze G:\ --large 1

# Or use the Python script directly for maximum speed
python -m nexus_ai.tools.space_analyzer C:\

# Find files larger than 10GB across all drives
nexus space large C:\ --min 10 --limit 100
nexus space large D:\ --min 10 --limit 100
```

### What The Space Analyzer Shows

```
================================================================================
  NexusFS Space Analysis: C:\
================================================================================

Drive: 212.00 GB used / 256.00 GB total (44.00 GB free)
Scanned: 847,234 files, 125,678 directories in 3,421ms

--- HUGE FILES (>10 GB) ---
     42.50 GB  C:\Users\Admin\.lmstudio\models\Llama-3.1-70B-Q4_K_M.gguf
     41.20 GB  C:\Users\Admin\.lmstudio\models\Qwen2.5-72B-Q4_K_M.gguf
     38.80 GB  C:\Users\Admin\.ollama\models\deepseek-v3
     36.10 GB  C:\Users\Admin\.lmstudio\models\Mixtral-8x22B-Q3_K_M.gguf

--- LARGE FILES (>1 GB) ---
      8.50 GB  C:\Users\Admin\AppData\Local\Docker\wsl\data\ext4.vhdx
      6.20 GB  C:\Users\Admin\.cache\huggingface\transformers\...
      4.80 GB  C:\pagefile.sys
      ...

--- MODEL FILES ---
Total model storage: 337.45 GB
     42.50 GB  C:\Users\Admin\.lmstudio\models\Llama-3.1-70B-Q4_K_M.gguf
     ...

--- LARGEST DIRECTORIES ---
    337.45 GB  C:\Users\Admin\.lmstudio\models
    163.20 GB  C:\Users\Admin\.ollama\models
     44.80 GB  C:\Users\Admin\.cache
     28.30 GB  C:\Users\Admin\node_modules
     ...
```

---

## Common Space Hogs (3.4TB Analysis)

### Typical Breakdown for Power Users

| Category | Typical Size | Location |
|----------|-------------|----------|
| **AI Models** | 100-500 GB | `.lmstudio`, `.ollama`, `.cache/huggingface` |
| **Games** | 200-1000 GB | Steam, Epic, Xbox Game Pass |
| **Docker/WSL** | 50-200 GB | `AppData/Local/Docker`, WSL distros |
| **Development** | 20-100 GB | `node_modules`, `.venv`, `target` |
| **Browser Cache** | 5-30 GB | Chrome, Firefox, Edge profiles |
| **Virtual Machines** | 50-500 GB | VHD/VHDX files |
| **Media Files** | Variable | Videos, photos, music |
| **Backup Files** | Variable | Windows backup, Time Machine |

### AI Model Locations

```powershell
# Check these paths for AI models
dir "$env:USERPROFILE\.lmstudio\models" -Recurse | Measure-Object -Property Length -Sum
dir "$env:USERPROFILE\.ollama\models" -Recurse | Measure-Object -Property Length -Sum
dir "$env:USERPROFILE\.cache\huggingface" -Recurse | Measure-Object -Property Length -Sum
dir "C:\ComfyUI\models" -Recurse | Measure-Object -Property Length -Sum
```

### Game Locations

```powershell
# Check Steam
dir "C:\Program Files (x86)\Steam\steamapps\common" -Recurse | Measure-Object -Property Length -Sum

# Check Epic
dir "C:\Program Files\Epic Games" -Recurse | Measure-Object -Property Length -Sum

# Check Xbox Game Pass
dir "C:\XboxGames" -Recurse | Measure-Object -Property Length -Sum
```

### Developer Cache Locations

```powershell
# npm global cache
dir "$env:APPDATA\npm-cache" -Recurse | Measure-Object -Property Length -Sum

# Cargo cache
dir "$env:USERPROFILE\.cargo\registry" -Recurse | Measure-Object -Property Length -Sum

# Python venvs (search for)
Get-ChildItem -Path C:\ -Filter "venv" -Directory -Recurse -ErrorAction SilentlyContinue

# node_modules (search for)
Get-ChildItem -Path C:\ -Filter "node_modules" -Directory -Recurse -ErrorAction SilentlyContinue
```

---

## Native Windows Commands for Quick Analysis

### Find Largest Files (PowerShell)

```powershell
# Find top 50 largest files on C: drive
Get-ChildItem C:\ -Recurse -File -ErrorAction SilentlyContinue |
    Sort-Object Length -Descending |
    Select-Object -First 50 @{N='Size(GB)';E={[math]::Round($_.Length/1GB,2)}}, FullName

# Find files larger than 10GB
Get-ChildItem C:\ -Recurse -File -ErrorAction SilentlyContinue |
    Where-Object { $_.Length -gt 10GB } |
    Sort-Object Length -Descending |
    Select-Object @{N='Size(GB)';E={[math]::Round($_.Length/1GB,2)}}, FullName
```

### Find Largest Folders (PowerShell)

```powershell
# Get folder sizes (first level only)
Get-ChildItem C:\Users\$env:USERNAME -Directory |
    ForEach-Object {
        $size = (Get-ChildItem $_.FullName -Recurse -File -ErrorAction SilentlyContinue |
                 Measure-Object -Property Length -Sum).Sum
        [PSCustomObject]@{
            Folder = $_.Name
            'Size(GB)' = [math]::Round($size/1GB, 2)
        }
    } | Sort-Object 'Size(GB)' -Descending
```

### Windows Built-in Tools

```cmd
# TreeSize-like output (limited)
dir C:\ /s /a /-c | find "File(s)"

# WinDirStat-style (use windirstat if installed)
winget install windirstat
windirstat C:\
```

---

## MCP Integration for AI-Powered Analysis

### Using with Claude Code

```python
# In Claude Code with MCP configured
await nexus_space(path="C:\\", large_gb=1.0, find_duplicates=True)

# Get model file summary
await nexus_models(action="scan")

# Suggest what to relocate
await nexus_models(action="suggest", dest_drive="D", min_size_gb=5)
```

### MCP Server Response Example

```json
{
  "path": "C:\\Users\\Admin",
  "file_count": 847234,
  "dir_count": 125678,
  "scan_time_ms": 3421,
  "large_files": [
    {"path": "C:\\...\\Llama-3.1-70B.gguf", "size_gb": 42.5, "extension": ".gguf"},
    {"path": "C:\\...\\Qwen2.5-72B.gguf", "size_gb": 41.2, "extension": ".gguf"}
  ],
  "huge_files": [...],
  "model_files_count": 428,
  "model_files_size_gb": 337.45
}
```

---

## Quick Actions for Space Recovery

### Relocate AI Models (Safest)

```powershell
# Using NexusFS
nexus model relocate --dest G --min 5 --execute

# Manual with symlinks
robocopy "C:\Users\Admin\.lmstudio\models" "G:\AI-Models\lmstudio" /MOVE /E
mklink /D "C:\Users\Admin\.lmstudio\models" "G:\AI-Models\lmstudio"
```

### Clean Development Caches

```powershell
# Clean npm cache (safe)
npm cache clean --force

# Clean pip cache (safe)
pip cache purge

# Clean cargo cache (mostly safe)
cargo cache -a

# Clean nuget cache (safe)
dotnet nuget locals all --clear
```

### Clean Windows Temp Files

```powershell
# Disk Cleanup (safe)
cleanmgr /d C: /VERYLOWDISK

# Clear temp folders (safe)
Remove-Item "$env:TEMP\*" -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item "C:\Windows\Temp\*" -Recurse -Force -ErrorAction SilentlyContinue
```

---

## Interpreting Space Usage

### File Extension Cheat Sheet

| Extension | Type | Typically Safe to Delete? |
|-----------|------|---------------------------|
| `.gguf` | AI Model (GGML) | Relocate, don't delete |
| `.safetensors` | AI Model | Relocate, don't delete |
| `.vhdx` | Virtual Disk | Check if VM is used |
| `.wim` | Windows Image | Check if needed |
| `.iso` | Disk Image | Check if needed |
| `.zip/.rar/.7z` | Archive | Check contents first |
| `.tmp` | Temp file | Usually safe |
| `.log` | Log file | Usually safe (check age) |
| `.bak` | Backup | Check what it's backing up |

### Hidden Space Consumers

1. **Windows.old** - Previous Windows installation (can be huge)
2. **hiberfil.sys** - Hibernation file (size = RAM)
3. **pagefile.sys** - Virtual memory (size = RAM or more)
4. **$Recycle.Bin** - Recycle bin (check all drives!)
5. **System Volume Information** - System restore points

---

## Automating Space Monitoring

### Create Scheduled Task

```powershell
# Run space analysis weekly
$action = New-ScheduledTaskAction -Execute "python" -Argument "-m nexus_ai.tools.space_analyzer C:\ --output D:\Reports\space_$(Get-Date -Format yyyyMMdd).json"
$trigger = New-ScheduledTaskTrigger -Weekly -DaysOfWeek Sunday -At 3am
Register-ScheduledTask -TaskName "NexusFS Space Analysis" -Action $action -Trigger $trigger
```

### Alert When Drive Gets Low

```powershell
# Add to profile or scheduled task
$threshold = 50GB
$drives = Get-PSDrive -PSProvider FileSystem | Where-Object { $_.Free -lt $threshold }
if ($drives) {
    $msg = "Low disk space warning: " + ($drives | ForEach-Object { "$($_.Name): $([math]::Round($_.Free/1GB,1))GB free" }) -join ", "
    # Send notification (Windows Toast)
    [Windows.UI.Notifications.ToastNotificationManager, Windows.UI.Notifications, ContentType = WindowsRuntime] | Out-Null
    # ... toast notification code
}
```

---

## Performance Tips

### For Fastest Scanning

1. **Run as Administrator** - Access to all files
2. **Disable Antivirus Temporarily** - Scanning overhead
3. **Use SSD for Index** - Faster index writes
4. **Close File-Heavy Apps** - Reduce file locks

### Expected Scan Times

| Drive Size | File Count | Approximate Time |
|------------|------------|------------------|
| 256 GB SSD | 500K files | 2-5 seconds |
| 1 TB SSD | 2M files | 5-15 seconds |
| 2 TB HDD | 5M files | 30-90 seconds |
| 4 TB HDD | 10M files | 1-3 minutes |

---

## Troubleshooting

### "Access Denied" Errors
- Run as Administrator
- Check if files are in use
- Some system files are protected

### Slow Scanning
- Check if antivirus is scanning every file access
- Use MFT reader (when implemented) for 10x speed
- Consider indexing once, then using the index

### Missing Files in Results
- Hidden files might be excluded
- System files might be excluded
- Check filter settings

---

*This guide focuses on practical, immediate solutions for analyzing large storage.*
*For the full TreeMap visualization UI, see [FILE_MANAGER.md](FILE_MANAGER.md)*
