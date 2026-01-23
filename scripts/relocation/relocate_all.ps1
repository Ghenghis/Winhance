# NexusFS - Master Relocation Script
# Relocates .lmstudio, .ollama, and .cache folders to free up C: drive space
# Expected space savings: ~544 GB

param(
    [string]$DestDrive = "D",
    [switch]$DryRun = $true,
    [switch]$SkipLMStudio = $false,
    [switch]$SkipOllama = $false,
    [switch]$SkipCache = $false
)

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

Write-Host ""
Write-Host "═══════════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "    NexusFS - Master Relocation Script" -ForegroundColor Cyan
Write-Host "    Free up space on C: drive by relocating large folders" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

# Show current C: drive space
$CDrive = Get-PSDrive -Name C
$CFreeBefore = $CDrive.Free / 1GB
Write-Host "C: Drive Current Free Space: $([math]::Round($CFreeBefore, 2)) GB" -ForegroundColor $(if ($CFreeBefore -lt 50) { "Red" } else { "Green" })
Write-Host ""

# Calculate sizes
function Get-FolderSizeGB {
    param([string]$Path)
    if (-not (Test-Path $Path)) { return 0 }
    $size = (Get-ChildItem -Path $Path -Recurse -Force -ErrorAction SilentlyContinue |
             Measure-Object -Property Length -Sum -ErrorAction SilentlyContinue).Sum
    return [math]::Round($size / 1GB, 2)
}

$lmstudioPath = "$env:USERPROFILE\.lmstudio"
$ollamaPath = "$env:USERPROFILE\.ollama"
$cachePath = "$env:USERPROFILE\.cache"

Write-Host "Folder Sizes:" -ForegroundColor White
$lmSize = Get-FolderSizeGB $lmstudioPath
$ollamaSize = Get-FolderSizeGB $ollamaPath
$cacheSize = Get-FolderSizeGB $cachePath

Write-Host "  .lmstudio:  $lmSize GB" -ForegroundColor $(if ($lmSize -gt 100) { "Red" } else { "Yellow" })
Write-Host "  .ollama:    $ollamaSize GB" -ForegroundColor $(if ($ollamaSize -gt 50) { "Red" } else { "Yellow" })
Write-Host "  .cache:     $cacheSize GB" -ForegroundColor Yellow
Write-Host ""
Write-Host "  Total:      $([math]::Round($lmSize + $ollamaSize + $cacheSize, 2)) GB" -ForegroundColor Cyan
Write-Host ""

# Check destination drive
$DestDriveInfo = Get-PSDrive -Name $DestDrive -ErrorAction SilentlyContinue
if ($null -eq $DestDriveInfo) {
    Write-Host "ERROR: Destination drive $DestDrive does not exist!" -ForegroundColor Red
    exit 1
}

$DestFree = $DestDriveInfo.Free / 1GB
Write-Host "Destination Drive $DestDrive Free Space: $([math]::Round($DestFree, 2)) GB" -ForegroundColor Green
Write-Host ""

if ($DryRun) {
    Write-Host "[DRY RUN MODE - No files will be moved]" -ForegroundColor Magenta
    Write-Host ""
    Write-Host "To execute, run: .\relocate_all.ps1 -DryRun:`$false" -ForegroundColor Yellow
    Write-Host ""
}

# Run individual scripts
$scriptsToRun = @()

if (-not $SkipLMStudio -and $lmSize -gt 0) {
    $scriptsToRun += @{
        Name = "LM Studio"
        Script = Join-Path $ScriptDir "move_lmstudio.ps1"
        Size = $lmSize
    }
}

if (-not $SkipOllama -and $ollamaSize -gt 0) {
    $scriptsToRun += @{
        Name = "Ollama"
        Script = Join-Path $ScriptDir "move_ollama.ps1"
        Size = $ollamaSize
    }
}

if (-not $SkipCache -and $cacheSize -gt 0) {
    $scriptsToRun += @{
        Name = "Cache"
        Script = Join-Path $ScriptDir "move_cache.ps1"
        Size = $cacheSize
    }
}

Write-Host "═══════════════════════════════════════════════════════════════════" -ForegroundColor White
Write-Host "    Relocation Plan" -ForegroundColor White
Write-Host "═══════════════════════════════════════════════════════════════════" -ForegroundColor White
Write-Host ""

foreach ($script in $scriptsToRun) {
    Write-Host "  [$($script.Name)] $($script.Size) GB -> ${DestDrive}:\" -ForegroundColor Green
}

Write-Host ""

if (-not $DryRun) {
    $confirm = Read-Host "Execute all relocations? (yes/no)"
    if ($confirm -ne "yes") {
        Write-Host "Cancelled by user" -ForegroundColor Yellow
        exit 0
    }

    Write-Host ""

    foreach ($script in $scriptsToRun) {
        Write-Host "═══════════════════════════════════════════════════════════════════" -ForegroundColor Cyan
        Write-Host "    Relocating $($script.Name)..." -ForegroundColor Cyan
        Write-Host "═══════════════════════════════════════════════════════════════════" -ForegroundColor Cyan
        Write-Host ""

        & $script.Script -DestDrive $DestDrive -DryRun:$false -Force

        Write-Host ""
    }

    # Show final results
    $CDriveAfter = Get-PSDrive -Name C
    $CFreeAfter = $CDriveAfter.Free / 1GB
    $Saved = $CFreeAfter - $CFreeBefore

    Write-Host ""
    Write-Host "═══════════════════════════════════════════════════════════════════" -ForegroundColor Green
    Write-Host "    ALL RELOCATIONS COMPLETE!" -ForegroundColor Green
    Write-Host "═══════════════════════════════════════════════════════════════════" -ForegroundColor Green
    Write-Host ""
    Write-Host "  C: Drive Before: $([math]::Round($CFreeBefore, 2)) GB free" -ForegroundColor Yellow
    Write-Host "  C: Drive After:  $([math]::Round($CFreeAfter, 2)) GB free" -ForegroundColor Green
    Write-Host "  Space Saved:     $([math]::Round($Saved, 2)) GB" -ForegroundColor Cyan
    Write-Host ""
}
