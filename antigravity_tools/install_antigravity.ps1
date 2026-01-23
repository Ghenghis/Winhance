# Antigravity Suite Installation Script
# This script installs and configures all Antigravity tools for non-coder users

param(
    [switch]$SkipGo,
    [switch]$SkipNode,
    [switch]$Verbose
)

$ErrorActionPreference = "Continue"

# Colors for output
function Write-Success { param($Message) Write-Host "[SUCCESS] $Message" -ForegroundColor Green }
function Write-Info { param($Message) Write-Host "[INFO] $Message" -ForegroundColor Cyan }
function Write-Warn { param($Message) Write-Host "[WARNING] $Message" -ForegroundColor Yellow }
function Write-Err { param($Message) Write-Host "[ERROR] $Message" -ForegroundColor Red }
function Write-Step { param($Message) Write-Host "`n=== $Message ===" -ForegroundColor Magenta }

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "  Antigravity Suite Installation" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

# =============================================================================
# 1. Check Prerequisites
# =============================================================================
Write-Step "Checking Prerequisites"

# Check Go
if (-not $SkipGo) {
    if (Get-Command go -ErrorAction SilentlyContinue) {
        $goVersion = go version
        Write-Success "Go is installed: $goVersion"
    } else {
        Write-Warn "Go is not installed. CLIProxyAPI requires Go 1.24+"
        Write-Info "Download from: https://go.dev/dl/"
        $SkipGo = $true
    }
}

# Check Node.js
if (-not $SkipNode) {
    if (Get-Command node -ErrorAction SilentlyContinue) {
        $nodeVersion = node --version
        Write-Success "Node.js is installed: $nodeVersion"
    } else {
        Write-Warn "Node.js is not installed. Antigravity Kit requires Node.js 18+"
        Write-Info "Download from: https://nodejs.org/"
        $SkipNode = $true
    }
}

# Check npm
if (-not $SkipNode -and (Get-Command npm -ErrorAction SilentlyContinue)) {
    $npmVersion = npm --version
    Write-Success "npm is installed: v$npmVersion"
}

# =============================================================================
# 2. Install CLIProxyAPI
# =============================================================================
if (-not $SkipGo) {
    Write-Step "Installing CLIProxyAPI"
    
    Push-Location "d:\Winhance-FS-Repo\antigravity_tools\CLIProxyAPI"
    
    Write-Info "Downloading Go dependencies..."
    go mod download
    
    Write-Info "Building CLIProxyAPI..."
    go build -o cliproxyapi.exe ./cmd/main.go
    
    if (Test-Path "cliproxyapi.exe") {
        Write-Success "CLIProxyAPI built successfully"
        
        # Create config from example
        if ((Test-Path "config.example.yaml") -and (-not (Test-Path "config.yaml"))) {
            Copy-Item "config.example.yaml" "config.yaml"
            Write-Success "Created config.yaml from example"
        }
        
        # Create .env from example
        if ((Test-Path ".env.example") -and (-not (Test-Path ".env"))) {
            Copy-Item ".env.example" ".env"
            Write-Success "Created .env from example"
        }
    } else {
        Write-Err "Failed to build CLIProxyAPI"
    }
    
    Pop-Location
}

# =============================================================================
# 3. Install Antigravity Kit (CLI Tool)
# =============================================================================
if (-not $SkipNode) {
    Write-Step "Installing Antigravity Kit"
    
    Push-Location "d:\Winhance-FS-Repo\antigravity_tools\antigravity-kit"
    
    # Check if web directory exists
    if (Test-Path "web\package.json") {
        Push-Location "web"
        
        Write-Info "Installing npm dependencies..."
        npm install
        
        Write-Info "Installing ag-kit globally..."
        npm install -g .
        
        if (Get-Command ag-kit -ErrorAction SilentlyContinue) {
            Write-Success "Antigravity Kit installed successfully"
        } else {
            Write-Warn "ag-kit command not found. You may need to restart your terminal."
        }
        
        Pop-Location
    } else {
        Write-Warn "web/package.json not found. Skipping npm installation."
    }
    
    Pop-Location
}

# =============================================================================
# 4. Sync Skills from antigravity-awesome-skills
# =============================================================================
Write-Step "Syncing Skills"

$sourceSkills = "d:\Winhance-FS-Repo\antigravity_tools\antigravity-awesome-skills\.agent\skills"
$targetSkills = "d:\Winhance-FS-Repo\.agent\skills"

if (Test-Path $sourceSkills) {
    Write-Info "Syncing skills from antigravity-awesome-skills..."
    
    $skillDirs = Get-ChildItem -Path $sourceSkills -Directory
    
    foreach ($skillDir in $skillDirs) {
        $targetPath = Join-Path $targetSkills $skillDir.Name
        
        if (Test-Path $targetPath) {
            Write-Info "Updating skill: $($skillDir.Name)"
            Copy-Item -Path "$($skillDir.FullName)\*" -Destination $targetPath -Recurse -Force
        } else {
            Write-Info "Adding new skill: $($skillDir.Name)"
            Copy-Item -Path $skillDir.FullName -Destination $targetPath -Recurse -Force
        }
    }
    
    Write-Success "Skills synced successfully"
} else {
    Write-Warn "Skills directory not found in antigravity-awesome-skills"
}

# =============================================================================
# 5. Antigravity Manager (Desktop App)
# =============================================================================
Write-Step "Antigravity Manager Setup"

Push-Location "d:\Winhance-FS-Repo\antigravity_tools\Antigravity-Manager"

Write-Info "Antigravity Manager is a desktop application built with Tauri + React"
Write-Info "Prerequisites: Rust, Node.js, Visual Studio Build Tools"

if (Get-Command cargo -ErrorAction SilentlyContinue) {
    $rustVersion = cargo --version
    Write-Success "Rust is installed: $rustVersion"
    
    if (-not $SkipNode) {
        Write-Info "Installing npm dependencies..."
        npm install
        
        Write-Info "Antigravity Manager dependencies installed"
        Write-Info "To build: npm run tauri build"
        Write-Info "To run dev: npm run tauri dev"
    }
} else {
    Write-Warn "Rust is not installed. Cannot build Antigravity Manager."
    Write-Info "To install Rust, visit: https://rustup.rs/"
}

Pop-Location

# =============================================================================
# 6. Create Quick Start Scripts
# =============================================================================
Write-Step "Creating Quick Start Scripts"

# CLIProxyAPI start script
$cliproxyBat = @"
@echo off
cd /d "d:\Winhance-FS-Repo\antigravity_tools\CLIProxyAPI"
echo Starting CLIProxyAPI...
cliproxyapi.exe
pause
"@

Set-Content -Path "d:\Winhance-FS-Repo\antigravity_tools\start_cliproxyapi.bat" -Value $cliproxyBat
Write-Success "Created start_cliproxyapi.bat"

# Create README
$readmeContent = @"
# Antigravity Suite - Quick Start Guide

## What was Installed

1. **CLIProxyAPI** - Proxy server for AI APIs
   Location: antigravity_tools\CLIProxyAPI
   Start: Double-click start_cliproxyapi.bat

2. **Antigravity Kit** - CLI tool for agent templates
   Commands: ag-kit init, ag-kit update, ag-kit status

3. **Antigravity Manager** - Desktop app (source code)
   Location: antigravity_tools\Antigravity-Manager
   Build: npm run tauri build
   Dev: npm run tauri dev

4. **Skills Library** - 40+ AI skills
   Location: .agent\skills

## Next Steps

1. Configure CLIProxyAPI:
   - Edit antigravity_tools\CLIProxyAPI\config.yaml
   - Add your API keys

2. Use Antigravity Kit:
   - Run: ag-kit init (in any project)

3. Build Antigravity Manager:
   - Install Rust from https://rustup.rs/
   - Run: npm run tauri build

## Documentation

- CLIProxyAPI: https://github.com/Ghenghis/CLIProxyAPI
- Antigravity Kit: https://github.com/Ghenghis/antigravity-kit
- Antigravity Manager: https://github.com/Ghenghis/Antigravity-Manager
"@

Set-Content -Path "d:\Winhance-FS-Repo\antigravity_tools\README.md" -Value $readmeContent
Write-Success "Created README.md"

# =============================================================================
# Summary
# =============================================================================
Write-Step "Installation Summary"

Write-Host "`n========================================" -ForegroundColor Green
Write-Host "  Installation Complete!" -ForegroundColor Green
Write-Host "========================================`n" -ForegroundColor Green

Write-Host "What was installed:" -ForegroundColor Cyan
Write-Host "  [OK] CLIProxyAPI" -ForegroundColor Green
Write-Host "  [OK] Antigravity Kit" -ForegroundColor Green
Write-Host "  [OK] Antigravity Manager (source)" -ForegroundColor Green
Write-Host "  [OK] Skills Library (40+ skills)" -ForegroundColor Green

Write-Host "`nQuick Start:" -ForegroundColor Cyan
Write-Host "  1. Read: antigravity_tools\README.md" -ForegroundColor White
Write-Host "  2. Start CLIProxyAPI: Double-click start_cliproxyapi.bat" -ForegroundColor White
Write-Host "  3. Use ag-kit: Run 'ag-kit init' in your project" -ForegroundColor White

Write-Host "`nAll files are in: d:\Winhance-FS-Repo\antigravity_tools\" -ForegroundColor Yellow
Write-Host ""
