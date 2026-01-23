# Winhance Remediation Action Plan

**For Use With:** Windsurf IDE, Cursor, VS Code, or any AI-assisted IDE
**Created:** January 22, 2026
**Based On:** Fagan Audit Report

---

## Quick Start for AI IDE

Copy and paste each phase's instructions into your AI IDE to execute the remediation systematically.

---

## Phase 1: Critical Error Handling (Priority: CRITICAL)

### Task 1.1: Fix OutputParser.cs

**File:** `src/Winhance.Core/Features/Common/Utils/OutputParser.cs`

**Instructions for AI IDE:**
```
Open src/Winhance.Core/Features/Common/Utils/OutputParser.cs

Find all instances of:
catch { return null; }
catch { return false; }

Replace each bare catch block with proper exception handling:

1. For lines 102-105 (ExtractGuid):
Replace:
catch { return null; }
With:
catch (FormatException ex)
{
    System.Diagnostics.Debug.WriteLine($"Format error in ExtractGuid: {ex.Message}");
    return null;
}
catch (Exception ex)
{
    System.Diagnostics.Debug.WriteLine($"Unexpected error in ExtractGuid: {ex.Message}");
    return null;
}

2. Apply similar pattern to lines: 123-126, 148-151, 185-188, 222-225, 340-343, 452-455

Each catch block should:
- Catch specific exceptions first (FormatException, ArgumentException)
- Catch general Exception last
- Log to Debug output (no ILogService in static utility)
- Return appropriate default value
```

---

### Task 1.2: Fix WindowsRegistryService.cs

**File:** `src/Winhance.Infrastructure/Features/Common/Services/WindowsRegistryService.cs`

**Instructions for AI IDE:**
```
Open src/Winhance.Infrastructure/Features/Common/Services/WindowsRegistryService.cs

This class already has ILogService injected. Update all exception handlers to log errors.

Find all instances of:
catch (Exception) { return false; }
catch (Exception) { return null; }

Replace with differentiated exception handling:

Example for KeyExists() at lines 27-30:
Replace:
catch (Exception)
{
    return false;
}
With:
catch (SecurityException ex)
{
    _logService?.Log("WindowsRegistryService", $"Access denied checking key {keyPath}: {ex.Message}");
    return false;
}
catch (UnauthorizedAccessException ex)
{
    _logService?.Log("WindowsRegistryService", $"Unauthorized access to {keyPath}: {ex.Message}");
    return false;
}
catch (Exception ex)
{
    _logService?.Log("WindowsRegistryService", $"Error checking key existence {keyPath}: {ex.Message}");
    return false;
}

Apply this pattern to all 12 catch blocks at lines:
- 27-30, 50-53, 64-67, 81-84, 99-102, 113-116
- 130-133, 215-218, 264-267, 315-318, 451-457, 593-598

Add using statement if needed:
using System.Security;
```

---

### Task 1.3: Fix WimUtilService.cs Bare Catches

**File:** `src/Winhance.Infrastructure/Features/AdvancedTools/Services/WimUtilService.cs`

**Instructions for AI IDE:**
```
Open src/Winhance.Infrastructure/Features/AdvancedTools/Services/WimUtilService.cs

Find all bare catch blocks (catch { }) at lines:
- 262-265, 433-436, 694-697, 788-791, 1272-1275, 1491-1494

Replace each with:
catch (Exception ex)
{
    _logService?.Log("WimUtilService", $"Error in [CONTEXT]: {ex.Message}");
    // Keep existing behavior (return/continue/etc)
}

Where [CONTEXT] describes the operation:
- Line 262: "GetImageInfo PowerShell execution"
- Line 433: "PowerShell script execution"
- Line 694: "Driver extraction"
- Line 788: "Cleanup operation"
- Line 1272: "File operation"
- Line 1491: "Process cleanup"
```

---

### Task 1.4: Fix AutounattendScriptBuilder.cs

**File:** `src/Winhance.Infrastructure/Features/AdvancedTools/Services/AutounattendScriptBuilder.cs`

**Instructions for AI IDE:**
```
Open src/Winhance.Infrastructure/Features/AdvancedTools/Services/AutounattendScriptBuilder.cs

Find all bare catch blocks at lines:
- 1044, 1065, 1086, 1103, 1120, 1161, 1196, 1450

Add logging to each:
catch (Exception ex)
{
    System.Diagnostics.Debug.WriteLine($"AutounattendScriptBuilder error: {ex.Message}");
    // Keep existing behavior
}
```

---

### Task 1.5: Fix LogService.cs Error Handling

**File:** `src/Winhance.Core/Features/Common/Services/LogService.cs`

**Instructions for AI IDE:**
```
Open src/Winhance.Core/Features/Common/Services/LogService.cs

The logging service needs fallback error handling. Update catch blocks at lines 104-107, 120-124, 167-170:

Replace:
catch (Exception ex)
{
    // Logging failed - silently ignore
}

With:
catch (Exception ex)
{
    // Fallback to Debug output when logging fails
    System.Diagnostics.Debug.WriteLine($"WINHANCE LOGGING FAILURE: {ex.Message}");
    System.Diagnostics.Debug.WriteLine($"Original message: {message}");

    // Optionally write to Windows Event Log for critical failures
    try
    {
        if (!System.Diagnostics.EventLog.SourceExists("Winhance"))
        {
            System.Diagnostics.EventLog.CreateEventSource("Winhance", "Application");
        }
        System.Diagnostics.EventLog.WriteEntry("Winhance",
            $"Logging system failure: {ex.Message}",
            System.Diagnostics.EventLogEntryType.Warning);
    }
    catch { /* Last resort - ignore */ }
}
```

---

## Phase 2: Async Anti-Patterns (Priority: CRITICAL)

### Task 2.1: Fix TooltipRefreshEventHandler.cs

**File:** `src/Winhance.Infrastructure/Features/Common/EventHandlers/TooltipRefreshEventHandler.cs`

**Instructions for AI IDE:**
```
Open src/Winhance.Infrastructure/Features/Common/EventHandlers/TooltipRefreshEventHandler.cs

Convert async void event handlers to safe pattern.

At line 38, change:
private async void HandleSettingApplied(SettingAppliedEvent settingAppliedEvent)
{
    // existing code
}

To:
private void HandleSettingApplied(SettingAppliedEvent settingAppliedEvent)
{
    _ = HandleSettingAppliedAsync(settingAppliedEvent);
}

private async Task HandleSettingAppliedAsync(SettingAppliedEvent settingAppliedEvent)
{
    try
    {
        // Move existing async code here
    }
    catch (Exception ex)
    {
        System.Diagnostics.Debug.WriteLine($"Error in HandleSettingApplied: {ex.Message}");
    }
}

Apply same pattern to HandleFeatureComposed at line 65.
```

---

### Task 2.2: Fix BaseSettingsFeatureViewModel.cs

**File:** `src/Winhance.WPF/Features/Common/ViewModels/BaseSettingsFeatureViewModel.cs`

**Instructions for AI IDE:**
```
Open src/Winhance.WPF/Features/Common/ViewModels/BaseSettingsFeatureViewModel.cs

At line 64, convert async void to safe pattern:

Change:
private async void OnLanguageChanged(object? sender, EventArgs e)
{
    lock (_loadingLock)
    {
        _settingsLoaded = false;
    }
    OnPropertyChanged(nameof(DisplayName));
    await LoadSettingsAsync();
}

To:
private void OnLanguageChanged(object? sender, EventArgs e)
{
    _ = OnLanguageChangedAsync();
}

private async Task OnLanguageChangedAsync()
{
    try
    {
        lock (_loadingLock)
        {
            _settingsLoaded = false;
        }
        OnPropertyChanged(nameof(DisplayName));
        await LoadSettingsAsync();
    }
    catch (Exception ex)
    {
        _logService?.Log("BaseSettingsFeatureViewModel", $"Error during language change: {ex.Message}");
    }
}
```

---

### Task 2.3: Fix App.xaml.cs Exception Handling

**File:** `src/Winhance.WPF/App.xaml.cs`

**Instructions for AI IDE:**
```
Open src/Winhance.WPF/App.xaml.cs

The OnStartup and OnExit methods are async void (required by framework).
Add comprehensive try-catch at the top level.

At line 152, wrap entire OnStartup content:

protected override async void OnStartup(StartupEventArgs e)
{
    try
    {
        // ALL existing startup code goes here
    }
    catch (Exception ex)
    {
        MessageBox.Show(
            $"A critical error occurred during startup:\n\n{ex.Message}\n\nThe application will now close.",
            "Winhance - Startup Error",
            MessageBoxButton.OK,
            MessageBoxImage.Error);

        LogStartupError($"Critical startup failure: {ex}");
        Current.Shutdown(1);
    }
}

Apply similar pattern to OnExit at line 218.
```

---

### Task 2.4: Remove GC.Collect Anti-Pattern

**File:** `src/Winhance.WPF/Features/SoftwareApps/ViewModels/SoftwareAppsViewModel.cs`

**Instructions for AI IDE:**
```
Open src/Winhance.WPF/Features/SoftwareApps/ViewModels/SoftwareAppsViewModel.cs

Find and remove the manual GC.Collect call at lines 549-553:

DELETE these lines:
System.Threading.Tasks.Task.Run(() =>
{
    System.Threading.Thread.Sleep(100);
    GC.Collect(0, GCCollectionMode.Optimized);
});

If there's a memory pressure issue, it should be fixed at the source, not with manual GC calls.
```

---

## Phase 3: Memory Leak Prevention (Priority: HIGH)

### Task 3.1: Add IDisposable to MainViewModel

**File:** `src/Winhance.WPF/Features/Common/ViewModels/MainViewModel.cs`

**Instructions for AI IDE:**
```
Open src/Winhance.WPF/Features/Common/ViewModels/MainViewModel.cs

1. Add IDisposable to class declaration:
public partial class MainViewModel : ObservableObject, IDisposable

2. Add disposal tracking field:
private bool _disposed = false;

3. Add Dispose method at end of class:

public void Dispose()
{
    Dispose(true);
    GC.SuppressFinalize(this);
}

protected virtual void Dispose(bool disposing)
{
    if (_disposed) return;

    if (disposing)
    {
        // Unsubscribe from events
        if (_navigationService != null)
        {
            _navigationService.Navigated -= NavigationService_Navigated;
            _navigationService.Navigating -= NavigationService_Navigating;
        }

        if (_taskProgressService != null)
        {
            _taskProgressService.ProgressUpdated -= OnProgressUpdated;
        }

        // Dispose child ViewModels if they implement IDisposable
        (_currentViewModel as IDisposable)?.Dispose();
    }

    _disposed = true;
}

~MainViewModel()
{
    Dispose(false);
}
```

---

### Task 3.2: Fix SoftwareAppsViewModel Event Subscriptions

**File:** `src/Winhance.WPF/Features/SoftwareApps/ViewModels/SoftwareAppsViewModel.cs`

**Instructions for AI IDE:**
```
Open src/Winhance.WPF/Features/SoftwareApps/ViewModels/SoftwareAppsViewModel.cs

1. Convert lambda event handlers (lines 114-136) to named methods.

Find:
WindowsAppsViewModel.PropertyChanged += (s, e) => {
    if (e.PropertyName == nameof(WindowsAppsViewModel.IsLoading))
    {
        OnPropertyChanged(nameof(IsLoading));
    }
};

Replace with:
WindowsAppsViewModel.PropertyChanged += OnWindowsAppsPropertyChanged;

Add named method:
private void OnWindowsAppsPropertyChanged(object? sender, PropertyChangedEventArgs e)
{
    if (e.PropertyName == nameof(WindowsAppsViewModel.IsLoading))
    {
        OnPropertyChanged(nameof(IsLoading));
    }
}

2. Do the same for ExternalAppsViewModel subscription.

3. Update Dispose method to unsubscribe all:

protected override void Dispose(bool disposing)
{
    if (disposing)
    {
        // Existing unsubscriptions
        _localizationService.LanguageChanged -= OnLanguageChanged;

        // Add new unsubscriptions
        this.PropertyChanged -= SoftwareAppsViewModel_PropertyChanged;
        WindowsAppsViewModel.PropertyChanged -= OnWindowsAppsPropertyChanged;
        ExternalAppsViewModel.PropertyChanged -= OnExternalAppsPropertyChanged;
    }
    base.Dispose(disposing);
}
```

---

## Phase 4: Security Hardening (Priority: HIGH)

### Task 4.1: Fix PowerShell Command Injection in WimUtilService

**File:** `src/Winhance.Infrastructure/Features/AdvancedTools/Services/WimUtilService.cs`

**Instructions for AI IDE:**
```
Open src/Winhance.Infrastructure/Features/AdvancedTools/Services/WimUtilService.cs

1. Add path validation helper method:

private static string ValidateAndSanitizePath(string path, string parameterName)
{
    if (string.IsNullOrWhiteSpace(path))
        throw new ArgumentNullException(parameterName);

    // Get canonical path to prevent traversal
    string fullPath = Path.GetFullPath(path);

    // Verify it's not trying to escape (basic check)
    if (fullPath.Contains(".."))
        throw new ArgumentException($"Invalid path: {parameterName}");

    return fullPath;
}

2. For PowerShell scripts, use parameter binding instead of string interpolation.

Find patterns like (around line 248):
$imagePath = '{imagePath.Replace("'", "''")}'

Replace entire script execution approach with parameterized version:

var script = @"
param(
    [Parameter(Mandatory=$true)]
    [string]$ImagePath
)
Get-WindowsImage -ImagePath $ImagePath
";

// Use PowerShell parameter binding
using var ps = PowerShell.Create();
ps.AddScript(script);
ps.AddParameter("ImagePath", ValidateAndSanitizePath(imagePath, nameof(imagePath)));
var results = await Task.Run(() => ps.Invoke());

Apply this pattern to ALL PowerShell script executions in the file.
```

---

### Task 4.2: Add Path Validation to DriverCategorizer

**File:** `src/Winhance.Infrastructure/Features/AdvancedTools/Helpers/DriverCategorizer.cs`

**Instructions for AI IDE:**
```
Open src/Winhance.Infrastructure/Features/AdvancedTools/Helpers/DriverCategorizer.cs

1. Add validation at the start of CategorizeAndCopyDrivers (around line 84):

public static async Task<Dictionary<string, List<string>>> CategorizeAndCopyDrivers(
    string sourceDirectory,
    string targetDirectory,
    string? workingDirectoryToExclude = null,
    IProgress<string>? progress = null)
{
    // Validate inputs
    if (string.IsNullOrWhiteSpace(sourceDirectory))
        throw new ArgumentNullException(nameof(sourceDirectory));
    if (string.IsNullOrWhiteSpace(targetDirectory))
        throw new ArgumentNullException(nameof(targetDirectory));

    // Canonicalize paths
    sourceDirectory = Path.GetFullPath(sourceDirectory);
    targetDirectory = Path.GetFullPath(targetDirectory);

    // Verify source exists
    if (!Directory.Exists(sourceDirectory))
        throw new DirectoryNotFoundException($"Source directory not found: {sourceDirectory}");

    // Continue with existing logic...
}

2. Add validation to IsStorageDriver (line 31):

public static bool IsStorageDriver(string infPath)
{
    if (string.IsNullOrWhiteSpace(infPath))
        return false;

    if (!File.Exists(infPath))
        return false;

    // Continue with existing logic...
}
```

---

### Task 4.3: Add Input Validation to DuplicateFinderService

**File:** `src/Winhance.Infrastructure/Features/FileManager/Services/DuplicateFinderService.cs`

**Instructions for AI IDE:**
```
Open src/Winhance.Infrastructure/Features/FileManager/Services/DuplicateFinderService.cs

Update ScanForDuplicatesAsync (around line 30):

public async Task<DuplicateScanResult> ScanForDuplicatesAsync(
    IEnumerable<string> paths,
    DuplicateScanOptions options,
    IProgress<DuplicateScanProgress>? progress = null,
    CancellationToken cancellationToken = default)
{
    // Validate inputs
    ArgumentNullException.ThrowIfNull(paths);
    ArgumentNullException.ThrowIfNull(options);

    // Validate and canonicalize paths
    var validatedPaths = paths
        .Where(p => !string.IsNullOrWhiteSpace(p))
        .Select(p => {
            try { return Path.GetFullPath(p); }
            catch { return null; }
        })
        .Where(p => p != null && Directory.Exists(p))
        .Cast<string>()
        .ToList();

    if (!validatedPaths.Any())
    {
        _logService?.Log("DuplicateFinderService", "No valid paths provided for duplicate scan");
        return new DuplicateScanResult { Success = false, ErrorMessage = "No valid paths provided" };
    }

    // Continue with existing logic using validatedPaths...
}
```

---

## Phase 5: Thread Safety (Priority: HIGH)

### Task 5.1: Fix Race Condition in SoftwareAppsViewModel

**File:** `src/Winhance.WPF/Features/SoftwareApps/ViewModels/SoftwareAppsViewModel.cs`

**Instructions for AI IDE:**
```
Open src/Winhance.WPF/Features/SoftwareApps/ViewModels/SoftwareAppsViewModel.cs

Find the UpdateButtonStates method (around line 334).

Change:
private bool _isUpdatingButtonStates = false;

private void UpdateButtonStates()
{
    if (_isUpdatingButtonStates) return;
    _isUpdatingButtonStates = true;
    try
    {
        // existing code
    }
    finally
    {
        _isUpdatingButtonStates = false;
    }
}

To:
private int _isUpdatingButtonStates = 0;

private void UpdateButtonStates()
{
    // Atomic check-and-set
    if (Interlocked.CompareExchange(ref _isUpdatingButtonStates, 1, 0) != 0)
        return;

    try
    {
        // existing code
    }
    finally
    {
        Interlocked.Exchange(ref _isUpdatingButtonStates, 0);
    }
}

Add using statement if needed:
using System.Threading;
```

---

### Task 5.2: Fix Cache Race in WindowsAppsViewModel

**File:** `src/Winhance.WPF/Features/SoftwareApps/ViewModels/WindowsAppsViewModel.cs`

**Instructions for AI IDE:**
```
Open src/Winhance.WPF/Features/SoftwareApps/ViewModels/WindowsAppsViewModel.cs

Find the HasSelectedItems property (around line 101).

Add thread-safe caching:

private bool _hasSelectedItems;
private int _hasSelectedItemsCacheValid = 0;
private readonly object _cacheLock = new object();

public bool HasSelectedItems
{
    get
    {
        // Double-check locking pattern
        if (Interlocked.CompareExchange(ref _hasSelectedItemsCacheValid, 0, 0) == 0)
        {
            lock (_cacheLock)
            {
                if (_hasSelectedItemsCacheValid == 0)
                {
                    _hasSelectedItems = Items?.Any(i => i.IsSelected) ?? false;
                    Interlocked.Exchange(ref _hasSelectedItemsCacheValid, 1);
                }
            }
        }
        return _hasSelectedItems;
    }
}

// When cache needs invalidation:
public void InvalidateSelectionCache()
{
    Interlocked.Exchange(ref _hasSelectedItemsCacheValid, 0);
}
```

---

## Phase 6: DI & Architecture Fixes (Priority: MEDIUM)

### Task 6.1: Fix LogService Constructor Injection

**File:** `src/Winhance.Core/Features/Common/Services/LogService.cs`

**Instructions for AI IDE:**
```
Open src/Winhance.Core/Features/Common/Services/LogService.cs

Remove the Initialize() method pattern and use constructor injection.

Change from:
private IWindowsVersionService? _versionService;

public void Initialize(IWindowsVersionService versionService)
{
    _versionService = versionService;
}

To:
private readonly IWindowsVersionService _versionService;

public LogService(IWindowsVersionService versionService)
{
    _versionService = versionService ?? throw new ArgumentNullException(nameof(versionService));

    // Move any initialization logic from Initialize() here
    InitializeLogFile();
}

Then update DI registration in InfrastructureServicesExtensions.cs to ensure
IWindowsVersionService is registered before ILogService.
```

---

### Task 6.2: Fix DuplicateFinderService Null Dependency

**File:** `src/Winhance.Infrastructure/Features/FileManager/Services/DuplicateFinderService.cs`

**Instructions for AI IDE:**
```
Open src/Winhance.Infrastructure/Features/FileManager/Services/DuplicateFinderService.cs

Either inject INexusIndexerService properly or remove the unused field.

Option A - Remove unused dependency:
// Remove this line:
private readonly INexusIndexerService? _nexusIndexer;

// Remove from constructor:
_nexusIndexer = null;

Option B - Inject properly:
public DuplicateFinderService(
    ILogService logService,
    INexusIndexerService? nexusIndexer = null)
{
    _logService = logService ?? throw new ArgumentNullException(nameof(logService));
    _nexusIndexer = nexusIndexer; // Optional dependency
}

Then update DI registration to provide the service when available.
```

---

## Phase 7: Testing Infrastructure (Priority: HIGH)

### Task 7.1: Create C# Test Project Structure

**Instructions for AI IDE:**
```
Create the following test project structure:

1. Create tests/Winhance.Core.Tests/Winhance.Core.Tests.csproj:
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0-windows</TargetFramework>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.8.0" />
    <PackageReference Include="xunit" Version="2.6.1" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.5.3" />
    <PackageReference Include="Moq" Version="4.20.69" />
    <PackageReference Include="FluentAssertions" Version="6.12.0" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\Winhance.Core\Winhance.Core.csproj" />
  </ItemGroup>
</Project>

2. Create tests/Winhance.Core.Tests/Utils/OutputParserTests.cs with tests for:
- ExtractGuid with valid input
- ExtractGuid with invalid input (should not throw)
- ExtractGuid with null input
- ParseBulkPowerSettingsOutput with various formats

3. Create tests/Winhance.Infrastructure.Tests/ with similar structure

4. Add test projects to Winhance.sln
```

---

### Task 7.2: Create Sample Test File

**File:** `tests/Winhance.Core.Tests/Utils/OutputParserTests.cs`

**Instructions for AI IDE:**
```
Create tests/Winhance.Core.Tests/Utils/OutputParserTests.cs:

using Xunit;
using FluentAssertions;
using Winhance.Core.Features.Common.Utils;

namespace Winhance.Core.Tests.Utils;

public class OutputParserTests
{
    [Fact]
    public void ExtractGuid_WithValidGuid_ReturnsGuid()
    {
        // Arrange
        var input = "Some text {12345678-1234-1234-1234-123456789012} more text";

        // Act
        var result = OutputParser.ExtractGuid(input);

        // Assert
        result.Should().NotBeNull();
        result.Should().Be("12345678-1234-1234-1234-123456789012");
    }

    [Fact]
    public void ExtractGuid_WithNoGuid_ReturnsNull()
    {
        // Arrange
        var input = "Some text without a GUID";

        // Act
        var result = OutputParser.ExtractGuid(input);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void ExtractGuid_WithNullInput_ReturnsNull()
    {
        // Act
        var result = OutputParser.ExtractGuid(null);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void ExtractGuid_WithEmptyString_ReturnsNull()
    {
        // Act
        var result = OutputParser.ExtractGuid(string.Empty);

        // Assert
        result.Should().BeNull();
    }
}
```

---

## Phase 8: Accessibility Improvements (Priority: MEDIUM)

### Task 8.1: Add AutomationProperties to MainWindow

**File:** `src/Winhance.WPF/Features/Common/Views/MainWindow.xaml`

**Instructions for AI IDE:**
```
Open src/Winhance.WPF/Features/Common/Views/MainWindow.xaml

Find all Button elements and add AutomationProperties.

For window control buttons (around line 107-144), add:

<Button x:Name="MinimizeButton"
        AutomationProperties.Name="Minimize"
        AutomationProperties.HelpText="Minimize window to taskbar"
        ... existing attributes ... />

<Button x:Name="MaximizeButton"
        AutomationProperties.Name="Maximize"
        AutomationProperties.HelpText="Maximize window"
        ... existing attributes ... />

<Button x:Name="CloseButton"
        AutomationProperties.Name="Close"
        AutomationProperties.HelpText="Close application"
        ... existing attributes ... />

For navigation buttons, add appropriate names based on their function.
```

---

## Verification Checklist

After completing each phase, verify:

### Phase 1 Verification
- [ ] `dotnet build src/Winhance.Core` succeeds
- [ ] `dotnet build src/Winhance.Infrastructure` succeeds
- [ ] No bare `catch { }` blocks remain (search for `catch\s*{\s*}`)
- [ ] All catch blocks log their context

### Phase 2 Verification
- [ ] No `async void` methods except framework-required (search for `async void`)
- [ ] All async event handlers wrapped in try-catch
- [ ] No manual `GC.Collect()` calls

### Phase 3 Verification
- [ ] MainViewModel implements IDisposable
- [ ] All event subscriptions have corresponding unsubscriptions
- [ ] No lambda event handlers for cross-object events

### Phase 4 Verification
- [ ] No string interpolation in PowerShell scripts
- [ ] All file paths validated before use
- [ ] Path.GetFullPath() used for canonicalization

### Phase 5 Verification
- [ ] All shared mutable state uses proper synchronization
- [ ] `Interlocked` used for atomic flag operations
- [ ] No raw boolean flags for concurrency control

### Phase 6 Verification
- [ ] No manual Initialize() methods for DI services
- [ ] All dependencies injected via constructor
- [ ] No null dependencies that should be required

### Phase 7 Verification
- [ ] Test projects compile
- [ ] `dotnet test` runs successfully
- [ ] At least one test per critical utility class

### Phase 8 Verification
- [ ] All interactive controls have AutomationProperties
- [ ] Screen readers can navigate the UI
- [ ] Keyboard navigation works throughout

---

## Summary

| Phase | Priority | Tasks | Estimated Time |
|-------|----------|-------|----------------|
| 1 | CRITICAL | 5 | 2-3 days |
| 2 | CRITICAL | 4 | 2 days |
| 3 | HIGH | 2 | 1.5 days |
| 4 | HIGH | 3 | 2 days |
| 5 | HIGH | 2 | 1 day |
| 6 | MEDIUM | 2 | 1.5 days |
| 7 | HIGH | 2 | 3-4 days |
| 8 | MEDIUM | 1 | 0.5 days |
| **TOTAL** | | **21** | **14-18 days** |

---

*Document created: January 22, 2026*
*For use with: Windsurf IDE, Cursor, VS Code with AI assistance*
