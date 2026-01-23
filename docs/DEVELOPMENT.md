# Winhance-FS Development Guide 🛠️

This guide covers setting up your development environment for Winhance-FS.

## Prerequisites

### Required Software

| Software | Version | Purpose |
|----------|---------|---------|
| Visual Studio 2022 | 17.8+ | IDE for C#/WPF development |
| .NET SDK | 9.0+ | Runtime and build tools |
| Rust | 1.75+ | Native backend development |
| Python | 3.12+ | AI agents and MCP server |
| Git | 2.40+ | Version control |

### Optional Tools

- **Rust Analyzer** - VS Code extension for Rust
- **ReSharper** - Enhanced C# development
- **Inno Setup** - Installer creation

## Quick Start

### 1. Clone the Repository

```powershell
git clone https://github.com/Ghenghis/Winhance-FS.git
cd Winhance-FS
```

### 2. Build the Rust Backend

```powershell
cd src/nexus-native
cargo build --release
```

### 3. Build the .NET Solution

```powershell
cd ../..
dotnet restore Winhance.sln
dotnet build Winhance.sln --configuration Release
```

### 4. Run the Application

```powershell
dotnet run --project src/Winhance.WPF
```

## Project Structure

```
Winhance-FS/
├── .github/                    # CI/CD workflows
├── docs/                       # Documentation
├── extras/                     # Build scripts, installer
├── src/
│   ├── Winhance.Core/         # Domain layer (interfaces, models)
│   ├── Winhance.Infrastructure/# Service implementations
│   ├── Winhance.WPF/          # Presentation layer
│   ├── nexus-native/          # Rust backend
│   └── nexus-agents/          # Python AI agents
├── themes/                     # Borg theme presets
├── Winhance.sln               # Solution file
└── README.md
```

## Development Workflow

### Adding a New Feature

1. **Define the Feature ID** in `Winhance.Core/Features/Common/Constants/FeatureIds.cs`:

```csharp
public const string MyNewFeature = "MyNewFeature";
```

2. **Add Feature Definition** in `FeatureDefinitions.cs`:

```csharp
new(FeatureIds.MyNewFeature, "My New Feature", "Star", "Storage", 10),
```

3. **Create the ViewModel** in `Winhance.WPF/Features/Storage/ViewModels/`:

```csharp
public partial class MyNewFeatureViewModel : BaseSettingsFeatureViewModel
{
    public override string ModuleId => FeatureIds.MyNewFeature;

    protected override string GetDisplayNameKey()
        => StringKeys.Features.MyNewFeature_Name;
}
```

4. **Create the View** in `Winhance.WPF/Features/Storage/Views/`:

```xml
<UserControl x:Class="Winhance.WPF.Features.Storage.Views.MyNewFeatureView">
    <StackPanel>
        <TextBlock Text="{Binding DisplayName}" />
        <!-- Your UI here -->
    </StackPanel>
</UserControl>
```

5. **Register in DI Container**:

```csharp
// ViewModelExtensions.cs
services.AddTransient<MyNewFeatureViewModel>();

// ViewExtensions.cs
services.AddTransient<MyNewFeatureView>();

// FeatureRegistry.cs
[FeatureIds.MyNewFeature] = (typeof(MyNewFeatureViewModel), typeof(MyNewFeatureView)),
```

### Rust Backend Development

#### Building

```powershell
cd src/nexus-native
cargo build --release
```

#### Running Tests

```powershell
cargo test
```

#### Generating UniFFI Bindings

```powershell
cargo run --bin uniffi-bindgen generate src/nexus.udl --language csharp
```

#### Adding a New Rust Function

1. **Define in UDL** (`src/nexus.udl`):

```
[Async]
MyResult my_new_function(string param);
```

2. **Implement in Rust** (`src/lib.rs`):

```rust
pub async fn my_new_function(param: String) -> MyResult {
    // Implementation
}
```

3. **Regenerate bindings** and copy to `Winhance.Infrastructure/Native/`.

### Python Agent Development

#### Setup Environment

```powershell
cd src/nexus-agents
python -m venv .venv
.venv\Scripts\activate
pip install -e .[dev]
```

#### Running the MCP Server

```powershell
python -m nexus_agents.mcp.server
```

#### Adding a New MCP Tool

```python
# src/nexus_agents/mcp/tools/my_tool.py
from mcp.server.fastmcp import FastMCP

mcp = FastMCP("My Tool")

@mcp.tool()
async def my_new_tool(param: str) -> str:
    """Tool description."""
    return f"Result: {param}"
```

## Testing

### Unit Tests (C#)

```powershell
dotnet test Winhance.sln
```

### Unit Tests (Rust)

```powershell
cd src/nexus-native
cargo test
```

### Integration Tests

```powershell
dotnet test --filter Category=Integration
```

## Code Style

### C# Guidelines

- Follow Winhance's existing patterns
- Use `OperationResult<T>` for all service methods
- Prefer `async/await` for I/O operations
- Use `[ObservableProperty]` and `[RelayCommand]` attributes

### Rust Guidelines

- Use `rustfmt` for formatting
- Run `cargo clippy` before commits
- Prefer `Result<T, E>` over panics
- Document public APIs

### Commit Messages

Follow conventional commits:

```
feat: add deep scan functionality
fix: resolve memory leak in MFT parser
docs: update development guide
refactor: simplify search pipeline
```

## Debugging

### Debugging the WPF App

1. Set `Winhance.WPF` as startup project
2. Press F5 to start with debugger

### Debugging Rust

1. Build with debug symbols:
   ```powershell
   cargo build
   ```

2. Use `rust-lldb` or VS Code debugger

### Debugging Python Agents

```powershell
python -m debugpy --listen 5678 -m nexus_agents.mcp.server
```

## Building for Release

### Full Build Script

```powershell
# Build everything
.\extras\build-and-package.ps1
```

### Manual Steps

1. Build Rust:
   ```powershell
   cargo build --release --target x86_64-pc-windows-msvc
   ```

2. Copy DLL:
   ```powershell
   Copy-Item "target\release\nexus_native.dll" "..\Winhance.Infrastructure\Native\"
   ```

3. Build .NET:
   ```powershell
   dotnet publish -c Release -r win-x64
   ```

4. Create Installer:
   ```powershell
   iscc extras\Winhance.Installer.iss
   ```

## Troubleshooting

### Common Issues

**Rust DLL not found:**
- Ensure the DLL is copied to the correct location
- Check the build target matches (x64)

**UniFFI binding errors:**
- Regenerate bindings after UDL changes
- Ensure Rust and C# types match

**WPF Designer not loading:**
- Clean and rebuild solution
- Check for missing design-time dependencies

## Resources

- [Winhance Repository](https://github.com/memstechtips/Winhance)
- [CommunityToolkit.Mvvm](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/)
- [UniFFI Documentation](https://mozilla.github.io/uniffi-rs/)
- [Rust Windows Crate](https://microsoft.github.io/windows-docs-rs/)

---

*See also: [Architecture Guide](ARCHITECTURE.md) | [Theming Guide](THEMING.md)*
