# Contributing to Winhance-FS

Thank you for your interest in contributing to Winhance-FS! This document provides guidelines for contributing to the project.

## Code of Conduct

Please be respectful and constructive in all interactions. We welcome contributions from everyone.

## Development Setup

### Prerequisites

- Windows 10/11 (x64)
- .NET 9.0 SDK
- Rust 1.75+ (for nexus-native)
- Python 3.11+ (for nexus-agents)
- Visual Studio 2022 or VS Code
- Git

### Clone Repository

```bash
git clone https://github.com/Ghenghis/Winhance-FS.git
cd Winhance-FS
```

### Build C# Solution

```bash
# Restore dependencies
dotnet restore Winhance.sln

# Build
dotnet build Winhance.sln --configuration Release

# Run tests
dotnet test Winhance.sln
```

### Build Rust Backend

```bash
cd src/nexus-native

# Build
cargo build --release

# Run tests
cargo test

# Generate UniFFI bindings
cargo run --bin uniffi-bindgen generate src/nexus.udl --language csharp
```

### Setup Python Agents

```bash
cd src/nexus-agents

# Create virtual environment
python -m venv .venv
.venv\Scripts\activate

# Install in development mode
pip install -e .[dev]

# Install Playwright browsers
playwright install chromium

# Run tests
pytest tests/ -v
```

## Project Structure

```
Winhance-FS/
+-- src/
|   +-- Winhance.Core/           # C# interfaces and models
|   +-- Winhance.Infrastructure/ # C# service implementations
|   +-- Winhance.WPF/            # C# WPF presentation layer
|   +-- nexus-native/            # Rust backend
|   +-- nexus-agents/            # Python AI agents
+-- docs/                        # Documentation
+-- extras/                      # Build scripts
+-- themes/                      # Theme presets
```

## Coding Standards

### C# Guidelines

Follow Winhance existing patterns:

```csharp
// Use primary constructors (C# 12)
public partial class MyViewModel(
    IMyService myService,
    ILogService logService)
    : BaseFeatureViewModel
{
    // Use ObservableProperty for bindable properties
    [ObservableProperty]
    private string _myProperty = string.Empty;

    // Use RelayCommand for commands
    [RelayCommand]
    private async Task DoSomethingAsync()
    {
        // Implementation
    }
}
```

**Naming conventions:**
- `PascalCase` for public members
- `_camelCase` for private fields
- `I` prefix for interfaces
- Suffix ViewModels with `ViewModel`
- Suffix Services with `Service`

### Rust Guidelines

```rust
// Use rustfmt for formatting
// cargo fmt

// Use clippy for linting
// cargo clippy -- -D warnings

// Document public items
/// Scans the MFT for a given drive.
///
/// # Arguments
/// * `drive_letter` - The drive to scan (e.g., "C:")
///
/// # Returns
/// Scan results including file count and entries.
pub async fn scan_mft(drive_letter: &str) -> Result<MftScanResult, NexusError> {
    // Implementation
}
```

### Python Guidelines

```python
# Use Black for formatting
# black src/nexus_agents

# Use type hints
async def scan_drive(drive_letter: str | None = None) -> ScanResult:
    """
    Scan a drive for space analysis.

    Args:
        drive_letter: Optional drive to scan. Scans all if None.

    Returns:
        ScanResult containing drive info and recovery items.
    """
    # Implementation
```

## Making Changes

### 1. Create a Branch

```bash
git checkout -b feature/my-feature
# or
git checkout -b fix/bug-description
```

### 2. Make Changes

- Write clean, readable code
- Add tests for new functionality
- Update documentation as needed
- Follow existing patterns

### 3. Test Changes

```bash
# C#
dotnet test Winhance.sln

# Rust
cd src/nexus-native && cargo test

# Python
cd src/nexus-agents && pytest tests/ -v
```

### 4. Commit Changes

Use clear, descriptive commit messages:

```bash
git commit -m "Add Storage Intelligence drive scanning feature"
git commit -m "Fix memory leak in MFT parser"
git commit -m "Update theming documentation"
```

### 5. Push and Create PR

```bash
git push origin feature/my-feature
```

Then create a Pull Request on GitHub.

## Pull Request Guidelines

### Title

Use a clear, descriptive title:
- `Add: Storage Intelligence module`
- `Fix: Memory leak in drive scanner`
- `Update: Theme documentation`

### Description

Include:
- What changes were made
- Why changes were needed
- How to test the changes
- Screenshots for UI changes

### Checklist

Before submitting:
- [ ] Code follows project style guidelines
- [ ] Tests pass locally
- [ ] Documentation updated if needed
- [ ] No breaking changes (or documented if unavoidable)
- [ ] PR targets `develop` branch (not `main`)

## Areas for Contribution

### Good First Issues

Look for issues labeled `good first issue`:
- Documentation improvements
- Localization fixes
- Minor UI tweaks
- Test coverage

### Feature Development

For new features:
1. Check existing issues/discussions
2. Open an issue to discuss approach
3. Wait for approval before major work
4. Follow Winhance architecture patterns

### Bug Fixes

1. Reproduce the bug
2. Write a failing test
3. Fix the bug
4. Verify test passes
5. Submit PR with fix

## Testing Guidelines

### C# Unit Tests

```csharp
[TestClass]
public class StorageServiceTests
{
    private readonly Mock<INexusNativeService> _mockNative = new();
    private StorageService _service = null!;

    [TestInitialize]
    public void Setup()
    {
        _service = new StorageService(_mockNative.Object);
    }

    [TestMethod]
    public async Task ScanDrives_ReturnsAllDrives()
    {
        // Arrange
        _mockNative.Setup(x => x.ScanAllDrivesAsync())
            .ReturnsAsync(new DrivesScanResult { /* ... */ });

        // Act
        var result = await _service.ScanDrivesAsync();

        // Assert
        Assert.IsTrue(result.Success);
        Assert.IsTrue(result.Result.Drives.Any());
    }
}
```

### Rust Tests

```rust
#[cfg(test)]
mod tests {
    use super::*;

    #[tokio::test]
    async fn test_mft_scan() {
        let result = scan_mft("C:").await;
        assert!(result.is_ok());

        let scan = result.unwrap();
        assert!(scan.file_count > 0);
    }

    #[test]
    fn test_entropy_calculation() {
        let data = b"Hello, World!";
        let entropy = calculate_entropy(data);
        assert!(entropy > 0.0 && entropy < 8.0);
    }
}
```

### Python Tests

```python
import pytest
from nexus_agents.core.storage import StorageScanner

@pytest.mark.asyncio
async def test_scan_drive():
    scanner = StorageScanner()
    result = await scanner.scan("C:")

    assert result is not None
    assert result.drive_letter == "C:"
    assert result.total_bytes > 0
```

## Documentation

### Where to Document

- **Code comments**: Complex logic, non-obvious behavior
- **docs/*.md**: User and developer guides
- **README.md**: Project overview, quick start
- **API docs**: Function/method signatures

### Documentation Style

- Use clear, simple language
- Include code examples
- Add diagrams where helpful
- Keep up to date with code changes

## Questions?

- Open a [GitHub Discussion](https://github.com/Ghenghis/Winhance-FS/discussions)
- Check existing issues
- Review documentation

Thank you for contributing!
