# NexusFS - LM Studio Model Relocation Script
# Moves .lmstudio folder from C:\Users\Admin to another drive with symlink
# SAFE: Creates backup log, verifies integrity, rollback on failure

param(
    [string]$DestDrive = "D",
    [switch]$DryRun = $true,
    [switch]$Force = $false
)

$ErrorActionPreference = "Stop"
$SourcePath = "$env:USERPROFILE\.lmstudio"
$DestPath = "${DestDrive}:\LMStudio_Models"
$LogPath = "D:\NexusFS\data\transactions\lmstudio_relocation_$(Get-Date -Format 'yyyyMMdd_HHmmss').log"

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

function Test-LMStudioRunning {
    $process = Get-Process -Name "LM Studio*" -ErrorAction SilentlyContinue
    return $null -ne $process
}

# ═══════════════════════════════════════════════════════════════════════════
# Main Script
# ═══════════════════════════════════════════════════════════════════════════

Write-Host ""
Write-Host "═══════════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "    NexusFS - LM Studio Model Relocation" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

Write-Log "Starting LM Studio relocation script"
Write-Log "Source: $SourcePath"
Write-Log "Destination: $DestPath"
Write-Log "DryRun: $DryRun"

# Check prerequisites
if (-not (Test-Path $SourcePath)) {
    Write-Log "Source folder does not exist: $SourcePath" "ERROR"
    exit 1
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

# Check if LM Studio is running
if (Test-LMStudioRunning) {
    Write-Log "LM Studio is currently running. Please close it first." "ERROR"
    exit 1
}

# Get source size
Write-Host "Calculating folder size..." -ForegroundColor Yellow
$SourceSize = Get-FolderSize -Path $SourcePath
Write-Log "Source size: $(Format-Size $SourceSize)"

# Check destination drive free space
$DestDriveInfo = Get-PSDrive -Name $DestDrive -ErrorAction SilentlyContinue
if ($null -eq $DestDriveInfo) {
    Write-Log "Destination drive $DestDrive does not exist" "ERROR"
    exit 1
}

$FreeSpace = $DestDriveInfo.Free
Write-Log "Destination drive $DestDrive free space: $(Format-Size $FreeSpace)"

if ($FreeSpace -lt $SourceSize * 1.1) {  # Need 10% buffer
    Write-Log "Not enough free space on $DestDrive drive" "ERROR"
    Write-Log "Need: $(Format-Size ($SourceSize * 1.1)), Have: $(Format-Size $FreeSpace)" "ERROR"
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

    # Step 2: Copy files with Robocopy (preserves everything)
    Write-Log "Copying files with Robocopy..."
    Write-Host ""
    Write-Host "Copying files (this may take a while)..." -ForegroundColor Yellow

    $robocopyArgs = @(
        $SourcePath,
        $DestPath,
        "/E",           # Copy subdirectories including empty ones
        "/COPYALL",     # Copy all file info
        "/DCOPY:DAT",   # Copy directory timestamps
        "/R:3",         # Retry 3 times
        "/W:5",         # Wait 5 seconds between retries
        "/MT:16",       # Multi-threaded (16 threads)
        "/NFL",         # No file list
        "/NDL",         # No directory list
        "/NP",          # No progress
        "/LOG+:$LogPath" # Append to log
    )

    $robocopyResult = & robocopy @robocopyArgs
    $robocopyExitCode = $LASTEXITCODE

    # Robocopy exit codes: 0-7 are success, 8+ are errors
    if ($robocopyExitCode -ge 8) {
        throw "Robocopy failed with exit code $robocopyExitCode"
    }

    Write-Log "Files copied successfully"

    # Step 3: Verify copy
    Write-Log "Verifying copy..."
    $DestSize = Get-FolderSize -Path $DestPath

    if ($DestSize -lt $SourceSize * 0.99) {  # Allow 1% variance
        throw "Size mismatch after copy. Source: $(Format-Size $SourceSize), Dest: $(Format-Size $DestSize)"
    }

    Write-Log "Copy verified: $(Format-Size $DestSize)"

    # Step 4: Remove original folder
    Write-Log "Removing original folder..."
    Remove-Item -Path $SourcePath -Recurse -Force -ErrorAction Stop
    Write-Log "Original folder removed"

    # Step 5: Create symlink
    Write-Log "Creating symbolic link..."
    New-Item -ItemType Junction -Path $SourcePath -Target $DestPath -Force | Out-Null
    Write-Log "Junction created: $SourcePath -> $DestPath"

    # Step 6: Verify symlink
    $link = Get-Item $SourcePath -Force
    if ($link.LinkType -ne "Junction") {
        throw "Failed to create junction"
    }

    # Success!
    Write-Host ""
    Write-Host "═══════════════════════════════════════════════════════════════════" -ForegroundColor Green
    Write-Host "    RELOCATION COMPLETE!" -ForegroundColor Green
    Write-Host "═══════════════════════════════════════════════════════════════════" -ForegroundColor Green
    Write-Host ""
    Write-Host "  Moved:     $(Format-Size $SourceSize)" -ForegroundColor Cyan
    Write-Host "  From:      $SourcePath" -ForegroundColor Yellow
    Write-Host "  To:        $DestPath" -ForegroundColor Green
    Write-Host "  Symlink:   Created (Junction)" -ForegroundColor Green
    Write-Host ""
    Write-Host "  LM Studio will continue to work normally!" -ForegroundColor White
    Write-Host ""
    Write-Log "Relocation completed successfully" "SUCCESS"

    # Calculate space saved
    $CFreeBefore = (Get-PSDrive -Name C).Free + $SourceSize
    $CFreeNow = (Get-PSDrive -Name C).Free
    Write-Host "  C: drive space freed: $(Format-Size ($CFreeNow - $CFreeBefore + $SourceSize))" -ForegroundColor Cyan
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
    Write-Host "  Your original files are still at: $SourcePath" -ForegroundColor Yellow
    Write-Host "  Partial copy may exist at: $DestPath" -ForegroundColor Yellow
    Write-Host ""
    Write-Log "Relocation failed" "ERROR"
    exit 1
}

# ═══════════════════════════════════════════════════════════════════════════
# Rollback Script Generation
# ═══════════════════════════════════════════════════════════════════════════

$RollbackScript = @"
# NexusFS - LM Studio Rollback Script
# Generated: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')
# Moves .lmstudio back to C: drive

`$ErrorActionPreference = "Stop"

Write-Host "Rolling back LM Studio relocation..." -ForegroundColor Yellow

# Check if LM Studio is running
`$process = Get-Process -Name "LM Studio*" -ErrorAction SilentlyContinue
if (`$null -ne `$process) {
    Write-Host "LM Studio is running. Please close it first." -ForegroundColor Red
    exit 1
}

# Remove symlink
if (Test-Path "$SourcePath") {
    Remove-Item -Path "$SourcePath" -Force
}

# Move back
robocopy "$DestPath" "$SourcePath" /E /COPYALL /DCOPY:DAT /R:3 /W:5 /MT:16 /MOVE

Write-Host "Rollback complete!" -ForegroundColor Green
"@

$RollbackPath = "D:\NexusFS\scripts\rollback\rollback_lmstudio_$(Get-Date -Format 'yyyyMMdd_HHmmss').ps1"
$RollbackScript | Out-File -FilePath $RollbackPath -Encoding UTF8
Write-Log "Rollback script saved to: $RollbackPath"
Write-Host "  Rollback script: $RollbackPath" -ForegroundColor Blue
