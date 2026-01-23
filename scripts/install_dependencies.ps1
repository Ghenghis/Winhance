# NexusFS Dependency Installation Script
# Installs all required dependencies for NexusFS

param(
    [switch]$SkipRust,
    [switch]$SkipPython,
    [switch]$SkipNode,
    [switch]$Dev
)

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "   NexusFS Dependency Installation" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Check for admin
$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Host "[WARNING] Not running as Administrator. Some features may be limited." -ForegroundColor Yellow
}

# =============================================================================
# Rust Installation
# =============================================================================
if (-not $SkipRust) {
    Write-Host "`n--- Rust Installation ---" -ForegroundColor Yellow

    if (Get-Command cargo -ErrorAction SilentlyContinue) {
        $rustVersion = (rustc --version)
        Write-Host "Rust already installed: $rustVersion" -ForegroundColor Green
    } else {
        Write-Host "Installing Rust..." -ForegroundColor Cyan
        Invoke-WebRequest https://win.rustup.rs/x86_64 -OutFile rustup-init.exe
        .\rustup-init.exe -y --default-toolchain stable
        Remove-Item rustup-init.exe
        $env:PATH += ";$env:USERPROFILE\.cargo\bin"
        Write-Host "Rust installed successfully" -ForegroundColor Green
    }

    # Install Rust tools
    Write-Host "Installing fd (fast file finder)..." -ForegroundColor Cyan
    cargo install fd-find 2>$null

    Write-Host "Installing ripgrep..." -ForegroundColor Cyan
    cargo install ripgrep 2>$null
}

# =============================================================================
# Python Dependencies
# =============================================================================
if (-not $SkipPython) {
    Write-Host "`n--- Python Dependencies ---" -ForegroundColor Yellow

    if (-not (Get-Command python -ErrorAction SilentlyContinue)) {
        Write-Host "[ERROR] Python not found. Please install Python 3.11+ first." -ForegroundColor Red
        exit 1
    }

    $pythonVersion = python --version
    Write-Host "Python version: $pythonVersion" -ForegroundColor Green

    # Navigate to project directory
    Push-Location D:\NexusFS

    Write-Host "Installing Python dependencies..." -ForegroundColor Cyan

    # Create virtual environment if it doesn't exist
    if (-not (Test-Path ".venv")) {
        Write-Host "Creating virtual environment..." -ForegroundColor Cyan
        python -m venv .venv
    }

    # Activate venv
    . .\.venv\Scripts\Activate.ps1

    # Upgrade pip
    python -m pip install --upgrade pip

    # Install dependencies
    if ($Dev) {
        Write-Host "Installing development dependencies..." -ForegroundColor Cyan
        pip install -e ".[dev]"
    } else {
        Write-Host "Installing production dependencies..." -ForegroundColor Cyan
        pip install -e .
    }

    # Install additional tools
    Write-Host "Installing additional tools..." -ForegroundColor Cyan
    pip install tantivy
    pip install loguru
    pip install orjson
    pip install rich

    Pop-Location

    Write-Host "Python dependencies installed" -ForegroundColor Green
}

# =============================================================================
# Node.js Dependencies (for GUI)
# =============================================================================
if (-not $SkipNode) {
    Write-Host "`n--- Node.js Dependencies ---" -ForegroundColor Yellow

    if (Get-Command npm -ErrorAction SilentlyContinue) {
        $nodeVersion = node --version
        Write-Host "Node.js version: $nodeVersion" -ForegroundColor Green

        if (Test-Path "D:\NexusFS\src\nexus_gui\package.json") {
            Push-Location D:\NexusFS\src\nexus_gui
            Write-Host "Installing Node.js dependencies..." -ForegroundColor Cyan
            npm install
            Pop-Location
        }
    } else {
        Write-Host "[SKIP] Node.js not found. GUI will not be available." -ForegroundColor Yellow
    }
}

# =============================================================================
# Create Data Directories
# =============================================================================
Write-Host "`n--- Creating Data Directories ---" -ForegroundColor Yellow

$dirs = @(
    "D:\NexusFS\data\indices",
    "D:\NexusFS\data\vectors",
    "D:\NexusFS\data\transactions",
    "D:\NexusFS\data\backups",
    "D:\NexusFS\data\cache",
    "D:\NexusFS\data\logs"
)

foreach ($dir in $dirs) {
    if (-not (Test-Path $dir)) {
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
        Write-Host "Created: $dir" -ForegroundColor Gray
    }
}

# =============================================================================
# Summary
# =============================================================================
Write-Host "`n========================================" -ForegroundColor Green
Write-Host "   Installation Complete!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Cyan
Write-Host "  1. Activate the virtual environment:"
Write-Host "     . D:\NexusFS\.venv\Scripts\Activate.ps1" -ForegroundColor White
Write-Host ""
Write-Host "  2. Run the CLI:"
Write-Host "     python -m nexus_cli --help" -ForegroundColor White
Write-Host ""
Write-Host "  3. Build the index:"
Write-Host "     python -m nexus_cli index build" -ForegroundColor White
Write-Host ""
Write-Host "  4. Analyze space:"
Write-Host "     python -m nexus_cli space analyze C:\Users\Admin" -ForegroundColor White
Write-Host ""
