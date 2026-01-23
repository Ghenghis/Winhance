# Contributing to Winhance-FS

Thank you for your interest in contributing to Winhance-FS! This document provides guidelines and information for contributors.

## Code of Conduct

By participating in this project, you agree to maintain a respectful and inclusive environment for everyone.

## How to Contribute

### Reporting Issues

Before creating an issue, please:

1. **Search existing issues** to avoid duplicates
2. **Use the issue templates** when available
3. **Provide detailed information**:
   - Windows version
   - Winhance-FS version
   - Steps to reproduce
   - Expected vs actual behavior
   - Screenshots if applicable

### Feature Requests

We welcome feature suggestions! Please:

1. Check if the feature already exists or is planned
2. Describe the use case clearly
3. Explain how it benefits users
4. Consider implementation complexity

### Pull Requests

#### Before You Start

1. **Fork the repository** and create your branch from `main`
2. **Read the [Development Guide](docs/DEVELOPMENT.md)** for setup instructions
3. **Follow the [Architecture Guide](docs/ARCHITECTURE.md)** for design patterns

#### Development Process

1. **Create a feature branch**:
   ```bash
   git checkout -b feature/your-feature-name
   ```

2. **Follow the coding standards** (see below)

3. **Write tests** for new functionality

4. **Update documentation** if needed

5. **Commit with conventional messages**:
   ```
   feat: add deep scan progress indicator
   fix: resolve memory leak in MFT parser
   docs: update development guide
   refactor: simplify search pipeline
   ```

6. **Push and create a Pull Request**

#### PR Requirements

- [ ] Code follows project style guidelines
- [ ] All tests pass
- [ ] New features include tests
- [ ] Documentation is updated
- [ ] Commit messages follow conventional format
- [ ] PR description explains the changes

## Coding Standards

### C# Guidelines

Follow Winhance's established patterns:

```csharp
// Use OperationResult<T> for service methods
public async Task<OperationResult<ScanResult>> ScanAsync(string path)
{
    try
    {
        var result = await _nativeService.ScanAsync(path);
        return OperationResult.CreateSuccess(result);
    }
    catch (Exception ex)
    {
        return OperationResult.CreateFailure<ScanResult>(ex.Message, ex);
    }
}

// Use CommunityToolkit.Mvvm attributes
public partial class MyViewModel : BaseSettingsFeatureViewModel
{
    [ObservableProperty]
    private bool _isScanning;

    [RelayCommand]
    private async Task StartScanAsync()
    {
        IsScanning = true;
        // ...
    }
}
```

**Key Conventions:**
- Use `async/await` for I/O operations
- Prefer `[ObservableProperty]` over manual property implementation
- Use `[RelayCommand]` for command binding
- Follow the feature registration pattern for new features

### Rust Guidelines

```rust
// Use Result<T, E> for error handling
pub async fn scan_mft(drive_letter: String) -> Result<MftScanResult, NexusError> {
    let handle = open_volume(&drive_letter)?;
    let entries = parse_mft(&handle).await?;
    Ok(MftScanResult { entries })
}

// Document public APIs
/// Searches files using SIMD-optimized pattern matching.
///
/// # Arguments
/// * `query` - The search pattern
/// * `options` - Search configuration
///
/// # Returns
/// Search results with matched files
pub async fn search(query: String, options: SearchOptions) -> SearchResults {
    // Implementation
}
```

**Key Conventions:**
- Run `cargo fmt` before commits
- Run `cargo clippy` and address warnings
- Prefer `Result<T, E>` over panics
- Document all public functions

### XAML Guidelines

```xml
<!-- Use consistent naming and structure -->
<UserControl x:Class="Winhance.WPF.Features.Storage.Views.MyView"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:local="clr-namespace:Winhance.WPF.Features.Storage.Views">

    <Grid>
        <!-- Use DynamicResource for theme-aware colors -->
        <Border Background="{DynamicResource ContentSectionBackground}">
            <TextBlock Text="{Binding DisplayName}"
                       Foreground="{DynamicResource PrimaryTextColor}" />
        </Border>
    </Grid>
</UserControl>
```

### Python Guidelines

```python
# Use type hints
async def search_files(query: str, options: SearchOptions | None = None) -> list[FileResult]:
    """Search files using the given query.

    Args:
        query: The search pattern
        options: Optional search configuration

    Returns:
        List of matching files
    """
    pass

# Use dataclasses or Pydantic models
@dataclass
class FileResult:
    path: str
    size: int
    modified: datetime
```

## Project Structure

When adding new features, follow this structure:

```
src/
├── Winhance.Core/Features/Storage/
│   ├── Interfaces/IMyService.cs      # Interface definition
│   └── Models/MyModel.cs             # Data models
│
├── Winhance.Infrastructure/Features/Storage/
│   └── Services/MyService.cs         # Service implementation
│
└── Winhance.WPF/Features/Storage/
    ├── ViewModels/MyViewModel.cs     # ViewModel
    └── Views/MyView.xaml             # View
```

## Testing

### Running Tests

```powershell
# C# Tests
dotnet test Winhance.sln

# Rust Tests
cd src/nexus-native
cargo test

# Python Tests
cd src/nexus-agents
pytest
```

### Writing Tests

- Write unit tests for new functionality
- Include integration tests for cross-layer features
- Test edge cases and error conditions

## Documentation

- Update relevant docs when adding features
- Include XML comments for public C# APIs
- Add rustdoc comments for public Rust functions
- Keep README.md up to date

## Getting Help

- **Discord**: Join our community server
- **GitHub Issues**: For bugs and features
- **Discussions**: For questions and ideas

## Recognition

Contributors are recognized in:
- Release notes
- CONTRIBUTORS.md file
- GitHub contributors page

Thank you for helping make Winhance-FS better!

---

*See also: [Development Guide](docs/DEVELOPMENT.md) | [Architecture Guide](docs/ARCHITECTURE.md)*
