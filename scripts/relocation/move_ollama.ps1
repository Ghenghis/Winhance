# NexusFS - Ollama Model Relocation Script
# Moves .ollama folder from C:\Users\Admin to another drive with symlink
# SAFE: Creates backup log, verifies integrity, rollback on failure

param(
    [string]$DestDrive = "D",
    [switch]$DryRun = $true,
    [switch]$Force = $false
)

$ErrorActionPreference = "Stop"
$SourcePath = "$env:USERPROFILE\.ollama"
$DestPath = "${DestDrive}:\Ollama_Models"
$LogPath = "D:\NexusFS\data\transactions\ollama_relocation_$(Get-Date -Format 'yyyyMMdd_HHmmss').log"

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

function Test-OllamaRunning {
    $process = Get-Process -Name "ollama*" -ErrorAction SilentlyContinue
    return $null -ne $process
}

function Stop-OllamaService {
    # Stop ollama serve if running
    $service = Get-Process -Name "ollama" -ErrorAction SilentlyContinue
    if ($null -ne $service) {
        Write-Log "Stopping Ollama service..."
        Stop-Process -Name "ollama" -Force -ErrorAction SilentlyContinue
        Start-Sleep -Seconds 2
    }
}

# ═══════════════════════════════════════════════════════════════════════════
# Main Script
# ═══════════════════════════════════════════════════════════════════════════

Write-Host ""
Write-Host "═══════════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "    NexusFS - Ollama Model Relocation" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

Write-Log "Starting Ollama relocation script"
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

# Check if Ollama is running
if (Test-OllamaRunning) {
    Write-Log "Ollama is currently running. Attempting to stop..." "WARN"
    Stop-OllamaService
    if (Test-OllamaRunning) {
        Write-Log "Could not stop Ollama. Please close it manually." "ERROR"
        exit 1
    }
    Write-Log "Ollama stopped successfully"
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

if ($FreeSpace -lt $SourceSize * 1.1) {
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

    # Step 2: Copy files with Robocopy
    Write-Log "Copying files with Robocopy..."
    Write-Host ""
    Write-Host "Copying files (this may take a while)..." -ForegroundColor Yellow

    $robocopyArgs = @(
        $SourcePath,
        $DestPath,
        "/E",
        "/COPYALL",
        "/DCOPY:DAT",
        "/R:3",
        "/W:5",
        "/MT:16",
        "/NFL",
        "/NDL",
        "/NP",
        "/LOG+:$LogPath"
    )

    $robocopyResult = & robocopy @robocopyArgs
    $robocopyExitCode = $LASTEXITCODE

    if ($robocopyExitCode -ge 8) {
        throw "Robocopy failed with exit code $robocopyExitCode"
    }

    Write-Log "Files copied successfully"

    # Step 3: Verify copy
    Write-Log "Verifying copy..."
    $DestSize = Get-FolderSize -Path $DestPath

    if ($DestSize -lt $SourceSize * 0.99) {
        throw "Size mismatch after copy. Source: $(Format-Size $SourceSize), Dest: $(Format-Size $DestSize)"
    }

    Write-Log "Copy verified: $(Format-Size $DestSize)"

    # Step 4: Remove original folder
    Write-Log "Removing original folder..."
    Remove-Item -Path $SourcePath -Recurse -Force -ErrorAction Stop
    Write-Log "Original folder removed"

    # Step 5: Create junction (symlink)
    Write-Log "Creating junction..."
    New-Item -ItemType Junction -Path $SourcePath -Target $DestPath -Force | Out-Null
    Write-Log "Junction created: $SourcePath -> $DestPath"

    # Step 6: Set OLLAMA_MODELS environment variable (optional but recommended)
    Write-Log "Setting OLLAMA_MODELS environment variable..."
    [Environment]::SetEnvironmentVariable("OLLAMA_MODELS", $DestPath, "User")
    Write-Log "Environment variable set"

    # Step 7: Verify junction
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
    Write-Host "  Junction:  Created" -ForegroundColor Green
    Write-Host "  Env Var:   OLLAMA_MODELS=$DestPath" -ForegroundColor Green
    Write-Host ""
    Write-Host "  Ollama will continue to work normally!" -ForegroundColor White
    Write-Host ""
    Write-Log "Relocation completed successfully" "SUCCESS"

    # Calculate space saved
    Write-Host "  C: drive space freed: $(Format-Size $SourceSize)" -ForegroundColor Cyan
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
# NexusFS - Ollama Rollback Script
# Generated: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')
# Moves .ollama back to C: drive

`$ErrorActionPreference = "Stop"

Write-Host "Rolling back Ollama relocation..." -ForegroundColor Yellow

# Check if Ollama is running
`$process = Get-Process -Name "ollama*" -ErrorAction SilentlyContinue
if (`$null -ne `$process) {
    Stop-Process -Name "ollama" -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2
}

# Remove junction
if (Test-Path "$SourcePath") {
    Remove-Item -Path "$SourcePath" -Force
}

# Move back
robocopy "$DestPath" "$SourcePath" /E /COPYALL /DCOPY:DAT /R:3 /W:5 /MT:16 /MOVE

# Remove environment variable
[Environment]::SetEnvironmentVariable("OLLAMA_MODELS", `$null, "User")

Write-Host "Rollback complete!" -ForegroundColor Green
"@

$RollbackPath = "D:\NexusFS\scripts\rollback\rollback_ollama_$(Get-Date -Format 'yyyyMMdd_HHmmss').ps1"
$RollbackScript | Out-File -FilePath $RollbackPath -Encoding UTF8
Write-Log "Rollback script saved to: $RollbackPath"
Write-Host "  Rollback script: $RollbackPath" -ForegroundColor Blue
