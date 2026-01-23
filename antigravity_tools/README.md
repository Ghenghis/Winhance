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
