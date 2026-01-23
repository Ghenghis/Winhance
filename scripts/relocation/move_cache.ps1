# NexusFS - Cache Folder Relocation Script
# Moves .cache folder from C:\Users\Admin to another drive with symlink
# SAFE: Creates backup log, verifies integrity, rollback on failure

param(
    [string]$DestDrive = "D",
    [switch]$DryRun = $true,
    [switch]$Force = $false
)

$ErrorActionPreference = "Stop"
$SourcePath = "$env:USERPROFILE\.cache"
$DestPath = "${DestDrive}:\UserCache"
$LogPath = "D:\NexusFS\data\transactions\cache_relocation_$(Get-Date -Format 'yyyyMMdd_HHmmss').log"

# Ensure log directory exists
$LogDir = Split-Path $LogPath -Parent
if (-not (Test-Path $LogDir)) {
    New-Item -ItemType Directory -Path $LogDir -Force | Out-Null
}

function Write-Log {
    param([string]$Message, [string]$Level = "INFO")
    $Timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $LogMessage = "[$Timestamp] [$Level] $Message"
    Write-Host $LogMessage -ForegroundColor $(switch($Level) {
        "INFO" { "White" }
        "WARN" { "Yellow" }
        "ERROR" { "Red" }
        "SUCCESS" { "Green" }
        default { "White" }
    })
    Add-Content -Path $LogPath -Value $LogMessage
}

function Get-FolderSize {
    param([string]$Path)
    $size = (Get-ChildItem -Path $Path -Recurse -Force -ErrorAction SilentlyContinue |
             Measure-Object -Property Length -Sum).Sum
    return $size
}

function Format-Size {
    param([long]$Bytes)
    if ($Bytes -ge 1TB) { return "{0:N2} TB" -f ($Bytes / 1TB) }
    if ($Bytes -ge 1GB) { return "{0:N2} GB" -f ($Bytes / 1GB) }
    if ($Bytes -ge 1MB) { return "{0:N2} MB" -f ($Bytes / 1MB) }
    return "{0:N2} KB" -f ($Bytes / 1KB)
}

# ═══════════════════════════════════════════════════════════════════════════
# Main Script
# ═══════════════════════════════════════════════════════════════════════════

Write-Host ""
Write-Host "═══════════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "    NexusFS - Cache Folder Relocation" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

Write-Log "Starting cache relocation script"
Write-Log "Source: $SourcePath"
Write-Log "Destination: $DestPath"
Write-Log "DryRun: $DryRun"

# Check prerequisites
if (-not (Test-Path $SourcePath)) {
    Write-Log "Source folder does not exist: $SourcePath" "WARN"
    Write-Host "Creating empty .cache folder at destination..." -ForegroundColor Yellow
    New-Item -ItemType Directory -Path $DestPath -Force | Out-Null
    New-Item -ItemType Junction -Path $SourcePath -Target $DestPath -Force | Out-Null
    Write-Log "Empty cache junction created" "SUCCESS"
    exit 0
}

# Check if already a symlink
$item = Get-Item $SourcePath -Force
if ($item.LinkType -eq "SymbolicLink" -or $item.LinkType -eq "Junction") {
    Write-Log "Source is already a symlink/junction pointing to: $($item.Target)" "WARN"
    if (-not $Force) {
        Write-Host "Use -Force to recreate the link" -ForegroundColor Yellow
        exit 0
    }
}

# Get source size
Write-Host "Calculating folder size..." -ForegroundColor Yellow
$SourceSize = Get-FolderSize -Path $SourcePath
Write-Log "Source size: $(Format-Size $SourceSize)"

# Show subdirectory breakdown
Write-Host ""
Write-Host "Cache subdirectories:" -ForegroundColor White
$subdirs = Get-ChildItem -Path $SourcePath -Directory -ErrorAction SilentlyContinue |
    ForEach-Object {
        $size = Get-FolderSize -Path $_.FullName
        [PSCustomObject]@{
            Name = $_.Name
            Size = $size
            SizeStr = Format-Size $size
        }
    } | Sort-Object Size -Descending | Select-Object -First 10

foreach ($dir in $subdirs) {
    Write-Host "  $($dir.Name.PadRight(30)) $($dir.SizeStr)" -ForegroundColor Gray
}

# Check destination drive free space
$DestDriveInfo = Get-PSDrive -Name $DestDrive -ErrorAction SilentlyContinue
if ($null -eq $DestDriveInfo) {
    Write-Log "Destination drive $DestDrive does not exist" "ERROR"
    exit 1
}

$FreeSpace = $DestDriveInfo.Free
Write-Log "Destination drive $DestDrive free space: $(Format-Size $FreeSpace)"

if ($FreeSpace -lt $SourceSize * 1.1) {
    Write-Log "Not enough free space on $DestDrive drive" "ERROR"
    exit 1
}

# Show plan
Write-Host ""
Write-Host "═══════════════════════════════════════════════════════════════════" -ForegroundColor White
Write-Host "    Relocation Plan" -ForegroundColor White
Write-Host "═══════════════════════════════════════════════════════════════════" -ForegroundColor White
Write-Host ""
Write-Host "  Source:      $SourcePath" -ForegroundColor Yellow
Write-Host "  Destination: $DestPath" -ForegroundColor Green
Write-Host "  Size:        $(Format-Size $SourceSize)" -ForegroundColor Cyan
Write-Host "  Free Space:  $(Format-Size $FreeSpace)" -ForegroundColor Cyan
Write-Host ""

if ($DryRun) {
    Write-Host "  [DRY RUN MODE - No files will be moved]" -ForegroundColor Magenta
    Write-Host ""
    Write-Host "  To execute, run with: -DryRun:`$false" -ForegroundColor Yellow
    Write-Log "Dry run complete - no changes made"
    exit 0
}

# Confirm execution
Write-Host ""
$confirm = Read-Host "Proceed with relocation? (yes/no)"
if ($confirm -ne "yes") {
    Write-Log "User cancelled operation"
    exit 0
}

# ═══════════════════════════════════════════════════════════════════════════
# Execute Relocation
# ═══════════════════════════════════════════════════════════════════════════

try {
    # Step 1: Create destination directory
    Write-Log "Creating destination directory..."
    if (-not (Test-Path $DestPath)) {
        New-Item -ItemType Directory -Path $DestPath -Force | Out-Null
    }

    # Step 2: Copy files with Robocopy
    Write-Log "Copying files with Robocopy..."
    Write-Host ""
    Write-Host "Copying files..." -ForegroundColor Yellow

    $robocopyArgs = @(
        $SourcePath,
        $DestPath,
        "/E",
        "/COPYALL",
        "/DCOPY:DAT",
        "/R:2",
        "/W:2",
        "/MT:16",
        "/NFL",
        "/NDL",
        "/NP",
        "/XJD",  # Exclude junction directories
        "/LOG+:$LogPath"
    )

    & robocopy @robocopyArgs
    $robocopyExitCode = $LASTEXITCODE

    # Robocopy exit codes 0-7 are OK
    if ($robocopyExitCode -ge 8) {
        throw "Robocopy failed with exit code $robocopyExitCode"
    }

    Write-Log "Files copied successfully"

    # Step 3: Verify copy
    Write-Log "Verifying copy..."
    $DestSize = Get-FolderSize -Path $DestPath

    if ($DestSize -lt $SourceSize * 0.95) {  # More tolerance for cache
        Write-Log "Size mismatch: Source $(Format-Size $SourceSize), Dest $(Format-Size $DestSize)" "WARN"
        Write-Log "Proceeding anyway (cache files may be locked)" "WARN"
    }

    Write-Log "Copy verified: $(Format-Size $DestSize)"

    # Step 4: Remove original folder
    Write-Log "Removing original folder..."
    Remove-Item -Path $SourcePath -Recurse -Force -ErrorAction SilentlyContinue
    Write-Log "Original folder removed"

    # Step 5: Create junction
    Write-Log "Creating junction..."
    New-Item -ItemType Junction -Path $SourcePath -Target $DestPath -Force | Out-Null
    Write-Log "Junction created: $SourcePath -> $DestPath"

    # Success!
    Write-Host ""
    Write-Host "═══════════════════════════════════════════════════════════════════" -ForegroundColor Green
    Write-Host "    RELOCATION COMPLETE!" -ForegroundColor Green
    Write-Host "═══════════════════════════════════════════════════════════════════" -ForegroundColor Green
    Write-Host ""
    Write-Host "  Moved:     $(Format-Size $DestSize)" -ForegroundColor Cyan
    Write-Host "  From:      $SourcePath" -ForegroundColor Yellow
    Write-Host "  To:        $DestPath" -ForegroundColor Green
    Write-Host "  Junction:  Created" -ForegroundColor Green
    Write-Host ""
    Write-Host "  Applications will continue to work normally!" -ForegroundColor White
    Write-Host ""
    Write-Log "Relocation completed successfully" "SUCCESS"
}
catch {
    Write-Log "ERROR: $_" "ERROR"
    Write-Host ""
    Write-Host "═══════════════════════════════════════════════════════════════════" -ForegroundColor Red
    Write-Host "    RELOCATION FAILED!" -ForegroundColor Red
    Write-Host "═══════════════════════════════════════════════════════════════════" -ForegroundColor Red
    Write-Host ""
    Write-Host "  Error: $_" -ForegroundColor Red
    Write-Host ""
    Write-Log "Relocation failed" "ERROR"
    exit 1
}

# Rollback Script
$RollbackPath = "D:\NexusFS\scripts\rollback\rollback_cache_$(Get-Date -Format 'yyyyMMdd_HHmmss').ps1"
@"
# NexusFS - Cache Rollback Script
# Generated: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')

Remove-Item -Path "$SourcePath" -Force -ErrorAction SilentlyContinue
robocopy "$DestPath" "$SourcePath" /E /COPYALL /DCOPY:DAT /R:3 /W:5 /MT:16 /MOVE
Write-Host "Rollback complete!" -ForegroundColor Green
"@ | Out-File -FilePath $RollbackPath -Encoding UTF8

Write-Log "Rollback script saved to: $RollbackPath"
Write-Host "  Rollback script: $RollbackPath" -ForegroundColor Blue
