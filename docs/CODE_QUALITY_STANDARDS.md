# Winhance Code Quality Standards

This document defines the coding standards and best practices for the Winhance project. All contributors must follow these guidelines to maintain code quality, reliability, and consistency.

## Table of Contents

1. [Error Handling Standards](#1-error-handling-standards)
2. [Async/Await Best Practices](#2-asyncawait-best-practices)
3. [Resource Management](#3-resource-management)
4. [MVVM Compliance](#4-mvvm-compliance)
5. [Thread Safety](#5-thread-safety)
6. [Input Validation](#6-input-validation)
7. [Logging Standards](#7-logging-standards)
8. [Testing Requirements](#8-testing-requirements)
9. [Code Review Checklist](#9-code-review-checklist)

---

## 1. Error Handling Standards

### 1.1 No Bare Catch Blocks

Never use empty catch blocks that swallow exceptions silently. This hides bugs and makes debugging extremely difficult.

```csharp
// BAD - Swallows all exceptions silently
try
{
    await ProcessFileAsync(filePath);
}
catch
{
    // Silent failure - NEVER do this
}

// BAD - Catches but does nothing meaningful
try
{
    await ProcessFileAsync(filePath);
}
catch (Exception)
{
    // Still bad - exception information is lost
}

// GOOD - Log and handle appropriately
try
{
    await ProcessFileAsync(filePath);
}
catch (Exception ex)
{
    _loggerService.LogError($"Failed to process file '{filePath}': {ex.Message}", ex);
    throw; // Re-throw if caller needs to know, or return error result
}
```

### 1.2 Catch Specific Exceptions First

Always order catch blocks from most specific to most general. Handle known failure modes explicitly.

```csharp
// BAD - Generic exception hides specific errors
try
{
    var content = await File.ReadAllTextAsync(configPath);
    var config = JsonSerializer.Deserialize<AppConfig>(content);
}
catch (Exception ex)
{
    _loggerService.LogError("Config error", ex);
    return null;
}

// GOOD - Handle specific exceptions with appropriate responses
try
{
    var content = await File.ReadAllTextAsync(configPath);
    var config = JsonSerializer.Deserialize<AppConfig>(content);
    return config;
}
catch (FileNotFoundException)
{
    _loggerService.LogInfo($"Config file not found at '{configPath}', using defaults");
    return AppConfig.CreateDefault();
}
catch (JsonException ex)
{
    _loggerService.LogError($"Invalid JSON in config file '{configPath}': {ex.Message}", ex);
    return AppConfig.CreateDefault();
}
catch (UnauthorizedAccessException ex)
{
    _loggerService.LogError($"Access denied to config file '{configPath}'", ex);
    throw new ConfigurationException($"Cannot access configuration file: {configPath}", ex);
}
catch (IOException ex)
{
    _loggerService.LogError($"IO error reading config file '{configPath}'", ex);
    throw;
}
```

### 1.3 Use OperationResult Pattern

Use the `OperationResult<T>` pattern for operations that can fail in expected ways. This provides explicit success/failure handling without exceptions for control flow.

```csharp
// BAD - Using exceptions for expected failure cases
public async Task<RegistryValue> GetRegistryValueAsync(string keyPath, string valueName)
{
    try
    {
        using var key = Registry.LocalMachine.OpenSubKey(keyPath);
        if (key == null)
            throw new KeyNotFoundException($"Registry key not found: {keyPath}");
        
        var value = key.GetValue(valueName);
        if (value == null)
            throw new ValueNotFoundException($"Value not found: {valueName}");
        
        return new RegistryValue(valueName, value);
    }
    catch (SecurityException ex)
    {
        throw new RegistryAccessException("Access denied", ex);
    }
}

// Caller has to use try-catch for normal flow
try
{
    var value = await GetRegistryValueAsync(path, name);
    // Use value
}
catch (KeyNotFoundException) { /* Handle missing key */ }
catch (ValueNotFoundException) { /* Handle missing value */ }
catch (RegistryAccessException) { /* Handle access denied */ }

// GOOD - Use OperationResult for expected outcomes
public async Task<OperationResult<RegistryValue>> GetRegistryValueAsync(string keyPath, string valueName)
{
    try
    {
        using var key = Registry.LocalMachine.OpenSubKey(keyPath);
        if (key == null)
        {
            return OperationResult<RegistryValue>.Failure(
                $"Registry key not found: {keyPath}",
                ErrorCode.KeyNotFound);
        }
        
        var value = key.GetValue(valueName);
        if (value == null)
        {
            return OperationResult<RegistryValue>.Failure(
                $"Value not found: {valueName}",
                ErrorCode.ValueNotFound);
        }
        
        return OperationResult<RegistryValue>.Success(new RegistryValue(valueName, value));
    }
    catch (SecurityException ex)
    {
        _loggerService.LogError($"Access denied to registry key '{keyPath}'", ex);
        return OperationResult<RegistryValue>.Failure(
            "Access denied to registry",
            ErrorCode.AccessDenied,
            ex);
    }
    catch (Exception ex)
    {
        _loggerService.LogError($"Unexpected error accessing registry key '{keyPath}'", ex);
        return OperationResult<RegistryValue>.Failure(
            "Unexpected error accessing registry",
            ErrorCode.Unknown,
            ex);
    }
}

// Caller uses clean pattern matching
var result = await GetRegistryValueAsync(path, name);
if (result.IsSuccess)
{
    ProcessValue(result.Value);
}
else
{
    HandleError(result.ErrorCode, result.ErrorMessage);
}
```

### 1.4 Logging Service Error Handling

The logging service itself must never throw exceptions. Failed logging should not crash the application.

```csharp
// BAD - Logging failure crashes the app
public class LoggerService : ILoggerService
{
    public void LogError(string message, Exception ex)
    {
        // If file write fails, exception propagates up
        File.AppendAllText(_logPath, FormatLogEntry(message, ex));
    }
}

// GOOD - Logging is resilient
public class LoggerService : ILoggerService
{
    public void LogError(string message, Exception? ex = null)
    {
        try
        {
            var entry = FormatLogEntry(LogLevel.Error, message, ex);
            WriteToLog(entry);
        }
        catch
        {
            // Logging must never throw - use fallback
            try
            {
                System.Diagnostics.Debug.WriteLine($"[LOG FAILURE] {message}");
                System.Diagnostics.Trace.WriteLine($"[LOG FAILURE] {message}");
            }
            catch
            {
                // Absolute last resort - silently ignore
                // This is the ONLY acceptable bare catch
            }
        }
    }
}
```

---

## 2. Async/Await Best Practices

### 2.1 Never Use async void (Except Framework-Required)

`async void` methods cannot be awaited, their exceptions cannot be caught, and they make testing difficult. The only acceptable use is for event handlers required by the framework.

```csharp
// BAD - async void method
public async void LoadSettingsAsync()
{
    var settings = await _settingsService.LoadAsync();
    ApplySettings(settings);
}

// BAD - async void in command execution
public ICommand LoadCommand => new RelayCommand(async () => 
{
    await LoadDataAsync(); // This creates async void lambda!
});

// GOOD - Return Task for async methods
public async Task LoadSettingsAsync()
{
    var settings = await _settingsService.LoadAsync();
    ApplySettings(settings);
}

// GOOD - Use AsyncRelayCommand for commands
public IAsyncRelayCommand LoadCommand { get; }

public MyViewModel()
{
    LoadCommand = new AsyncRelayCommand(LoadDataAsync);
}

private async Task LoadDataAsync()
{
    await _dataService.LoadAsync();
}

// ACCEPTABLE - Framework-required event handler
private async void Window_Loaded(object sender, RoutedEventArgs e)
{
    try
    {
        await InitializeAsync();
    }
    catch (Exception ex)
    {
        // MUST handle exceptions in async void!
        _loggerService.LogError("Initialization failed", ex);
        ShowErrorDialog("Failed to initialize application");
    }
}
```

### 2.2 Never Block on Async Code

Never use `.Result`, `.Wait()`, or `.GetAwaiter().GetResult()` on async code. This causes deadlocks in UI applications and thread pool starvation in server applications.

```csharp
// BAD - Causes deadlock in UI thread
public void Initialize()
{
    var settings = LoadSettingsAsync().Result; // DEADLOCK!
    ApplySettings(settings);
}

// BAD - Same problem with Wait()
public void Initialize()
{
    LoadSettingsAsync().Wait(); // DEADLOCK!
}

// BAD - GetAwaiter().GetResult() is no better
public void Initialize()
{
    var settings = LoadSettingsAsync().GetAwaiter().GetResult(); // Still deadlocks!
    ApplySettings(settings);
}

// GOOD - Make the calling method async
public async Task InitializeAsync()
{
    var settings = await LoadSettingsAsync();
    ApplySettings(settings);
}

// GOOD - If you truly cannot make it async, use proper patterns
// (Only use in constructors or truly synchronous contexts)
public void Initialize()
{
    // Run on thread pool to avoid deadlock (last resort)
    var settings = Task.Run(() => LoadSettingsAsync()).GetAwaiter().GetResult();
    ApplySettings(settings);
}

// BETTER - Defer async work
public void Initialize()
{
    // Schedule async work without blocking
    _ = InitializeAsync();
}

private async Task InitializeAsync()
{
    var settings = await LoadSettingsAsync();
    await _dispatcher.InvokeAsync(() => ApplySettings(settings));
}
```

### 2.3 Use ConfigureAwait Appropriately

In library code and non-UI code, use `ConfigureAwait(false)` to avoid unnecessary context switches. In UI code where you need to update the UI after await, omit it or use `ConfigureAwait(true)`.

```csharp
// BAD - Library code captures UI context unnecessarily
public class RegistryService : IRegistryService
{
    public async Task<string> ReadValueAsync(string path)
    {
        await Task.Delay(10); // Captures UI context!
        return Registry.GetValue(path, "", "")?.ToString() ?? "";
    }
}

// GOOD - Library code uses ConfigureAwait(false)
public class RegistryService : IRegistryService
{
    public async Task<string> ReadValueAsync(string path)
    {
        await Task.Delay(10).ConfigureAwait(false);
        return Registry.GetValue(path, "", "")?.ToString() ?? "";
    }
}

// GOOD - ViewModel needs UI context for property updates
public class SettingsViewModel : ViewModelBase
{
    public async Task LoadAsync()
    {
        IsLoading = true;
        
        // Do not use ConfigureAwait(false) here - we need UI context
        var settings = await _settingsService.LoadAsync();
        
        // This runs on UI thread, can update bound properties
        Settings = settings;
        IsLoading = false;
    }
}

// GOOD - Mix appropriately in complex scenarios
public async Task ProcessFilesAsync(IEnumerable<string> files)
{
    // CPU-bound work does not need UI context
    var processedData = await Task.Run(() => 
        files.Select(ProcessFile).ToList()
    ).ConfigureAwait(false);
    
    // Switch back to UI thread for updates
    await _dispatcher.InvokeAsync(() =>
    {
        foreach (var item in processedData)
        {
            Items.Add(item);
        }
    });
}
```

### 2.4 Support Cancellation

All long-running async operations should support cancellation via `CancellationToken`.

```csharp
// BAD - No cancellation support
public async Task<List<FileInfo>> ScanDirectoryAsync(string path)
{
    var files = new List<FileInfo>();
    await foreach (var file in EnumerateFilesAsync(path))
    {
        files.Add(file);
    }
    return files;
}

// GOOD - Supports cancellation
public async Task<List<FileInfo>> ScanDirectoryAsync(
    string path, 
    CancellationToken cancellationToken = default)
{
    var files = new List<FileInfo>();
    
    await foreach (var file in EnumerateFilesAsync(path, cancellationToken)
        .WithCancellation(cancellationToken)
        .ConfigureAwait(false))
    {
        cancellationToken.ThrowIfCancellationRequested();
        files.Add(file);
    }
    
    return files;
}

// GOOD - ViewModel with cancellation management
public class ScanViewModel : ViewModelBase
{
    private CancellationTokenSource? _scanCts;
    
    public async Task StartScanAsync()
    {
        // Cancel any existing scan
        await CancelScanAsync();
        
        _scanCts = new CancellationTokenSource();
        
        try
        {
            IsScanning = true;
            var files = await _scanService.ScanDirectoryAsync(
                SelectedPath, 
                _scanCts.Token);
            Files = new ObservableCollection<FileInfo>(files);
        }
        catch (OperationCanceledException)
        {
            _loggerService.LogInfo("Scan cancelled by user");
        }
        finally
        {
            IsScanning = false;
        }
    }
    
    public async Task CancelScanAsync()
    {
        if (_scanCts != null)
        {
            await _scanCts.CancelAsync();
            _scanCts.Dispose();
            _scanCts = null;
        }
    }
}
```

---

## 3. Resource Management

### 3.1 Implement IDisposable Correctly

Follow the standard dispose pattern for classes that own unmanaged resources or other disposable objects.

```csharp
// BAD - Incomplete dispose pattern
public class FileWatcher : IDisposable
{
    private FileSystemWatcher _watcher;
    
    public void Dispose()
    {
        _watcher.Dispose(); // No null check, no GC.SuppressFinalize
    }
}

// GOOD - Complete dispose pattern
public class FileWatcher : IDisposable
{
    private FileSystemWatcher? _watcher;
    private bool _disposed;
    
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
    
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;
            
        if (disposing)
        {
            // Dispose managed resources
            _watcher?.Dispose();
            _watcher = null;
        }
        
        // Free unmanaged resources (if any)
        
        _disposed = true;
    }
    
    // Only include finalizer if you have unmanaged resources
    // ~FileWatcher()
    // {
    //     Dispose(disposing: false);
    // }
    
    protected void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(FileWatcher));
    }
}
```

### 3.2 ViewModels with Events MUST Implement IDisposable

Any ViewModel that subscribes to events must implement IDisposable and unsubscribe to prevent memory leaks.

```csharp
// BAD - Memory leak from event subscription
public class SettingsViewModel : ViewModelBase
{
    private readonly ISettingsService _settingsService;
    
    public SettingsViewModel(ISettingsService settingsService)
    {
        _settingsService = settingsService;
        _settingsService.SettingsChanged += OnSettingsChanged;
        // Never unsubscribed - ViewModel is leaked!
    }
    
    private void OnSettingsChanged(object? sender, EventArgs e)
    {
        RefreshSettings();
    }
}

// GOOD - Proper cleanup with IDisposable
public class SettingsViewModel : ViewModelBase, IDisposable
{
    private readonly ISettingsService _settingsService;
    private bool _disposed;
    
    public SettingsViewModel(ISettingsService settingsService)
    {
        _settingsService = settingsService;
        _settingsService.SettingsChanged += OnSettingsChanged;
    }
    
    private void OnSettingsChanged(object? sender, EventArgs e)
    {
        if (_disposed) return;
        RefreshSettings();
    }
    
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
    
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;
            
        if (disposing)
        {
            _settingsService.SettingsChanged -= OnSettingsChanged;
        }
        
        _disposed = true;
    }
}

// BETTER - Use weak event pattern for long-lived publishers
public class SettingsViewModel : ViewModelBase
{
    public SettingsViewModel(ISettingsService settingsService)
    {
        WeakEventManager<ISettingsService, EventArgs>.AddHandler(
            settingsService,
            nameof(ISettingsService.SettingsChanged),
            OnSettingsChanged);
    }
    
    private void OnSettingsChanged(object? sender, EventArgs e)
    {
        RefreshSettings();
    }
}
```

### 3.3 Never Use Manual GC.Collect

Let the garbage collector do its job. Manual GC calls hurt performance and indicate architectural problems.

```csharp
// BAD - Manual GC calls
public void ClearCache()
{
    _cache.Clear();
    GC.Collect(); // NEVER do this
    GC.WaitForPendingFinalizers(); // Or this
    GC.Collect(); // Definitely not this
}

// BAD - "Helping" the GC
public void ProcessLargeFile(string path)
{
    var data = File.ReadAllBytes(path);
    ProcessData(data);
    data = null; // Unnecessary
    GC.Collect(); // Harmful
}

// GOOD - Let GC manage memory
public void ClearCache()
{
    _cache.Clear();
    // GC will collect when appropriate
}

// GOOD - Use proper patterns for large data
public async Task ProcessLargeFileAsync(string path)
{
    // Stream large files instead of loading entirely
    await using var stream = File.OpenRead(path);
    await ProcessStreamAsync(stream);
}

// GOOD - Use ArrayPool for temporary large arrays
public void ProcessData(int size)
{
    var buffer = ArrayPool<byte>.Shared.Rent(size);
    try
    {
        // Use buffer
        DoWork(buffer.AsSpan(0, size));
    }
    finally
    {
        ArrayPool<byte>.Shared.Return(buffer);
    }
}
```

### 3.4 Use using Statements

Always use `using` statements or declarations for disposable objects.

```csharp
// BAD - Resource leak if exception occurs
public string ReadFile(string path)
{
    var stream = File.OpenRead(path);
    var reader = new StreamReader(stream);
    var content = reader.ReadToEnd();
    reader.Dispose();
    stream.Dispose();
    return content;
}

// BAD - Complex try-finally
public string ReadFile(string path)
{
    FileStream? stream = null;
    StreamReader? reader = null;
    try
    {
        stream = File.OpenRead(path);
        reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
    finally
    {
        reader?.Dispose();
        stream?.Dispose();
    }
}

// GOOD - using statements
public string ReadFile(string path)
{
    using var stream = File.OpenRead(path);
    using var reader = new StreamReader(stream);
    return reader.ReadToEnd();
}

// GOOD - Async with await using
public async Task<string> ReadFileAsync(string path)
{
    await using var stream = File.OpenRead(path);
    using var reader = new StreamReader(stream);
    return await reader.ReadToEndAsync();
}
```

---

## 4. MVVM Compliance

### 4.1 No Logic in Code-Behind

Code-behind files should only contain UI-specific code that cannot be expressed in XAML or belongs in the ViewModel.

```csharp
// BAD - Business logic in code-behind
public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
    }
    
    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        // Business logic in code-behind - WRONG!
        var settings = new Settings
        {
            Theme = ThemeComboBox.SelectedItem.ToString(),
            AutoUpdate = AutoUpdateCheckBox.IsChecked ?? false
        };
        
        var json = JsonSerializer.Serialize(settings);
        await File.WriteAllTextAsync("settings.json", json);
        
        MessageBox.Show("Settings saved!");
    }
}

// GOOD - Minimal code-behind, logic in ViewModel
public partial class SettingsWindow : Window
{
    public SettingsWindow(SettingsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
    
    // Only UI-specific code that cannot be in XAML
    protected override void OnClosing(CancelEventArgs e)
    {
        if (DataContext is SettingsViewModel vm && vm.HasUnsavedChanges)
        {
            var result = MessageBox.Show(
                "You have unsaved changes. Close anyway?",
                "Confirm",
                MessageBoxButton.YesNo);
            e.Cancel = result == MessageBoxResult.No;
        }
        base.OnClosing(e);
    }
}

// ViewModel contains all logic
public class SettingsViewModel : ViewModelBase
{
    private readonly ISettingsService _settingsService;
    
    public string Theme { get; set; }
    public bool AutoUpdate { get; set; }
    public bool HasUnsavedChanges { get; private set; }
    
    public IAsyncRelayCommand SaveCommand { get; }
    
    public SettingsViewModel(ISettingsService settingsService)
    {
        _settingsService = settingsService;
        SaveCommand = new AsyncRelayCommand(SaveAsync, CanSave);
    }
    
    private async Task SaveAsync()
    {
        await _settingsService.SaveAsync(new Settings
        {
            Theme = Theme,
            AutoUpdate = AutoUpdate
        });
        HasUnsavedChanges = false;
    }
    
    private bool CanSave() => HasUnsavedChanges;
}
```

### 4.2 Use Commands Instead of Event Handlers

Use data binding and commands instead of event handlers in XAML.

```xml
<!-- BAD - Event handlers in XAML -->
<Button Content="Save" Click="SaveButton_Click" />
<TextBox TextChanged="SearchBox_TextChanged" />
<ListView SelectionChanged="ListView_SelectionChanged" />

<!-- GOOD - Commands and bindings -->
<Button Content="Save" Command="{Binding SaveCommand}" />

<TextBox Text="{Binding SearchText, UpdateSourceTrigger=PropertyChanged}" />

<ListView 
    ItemsSource="{Binding Items}" 
    SelectedItem="{Binding SelectedItem}">
    <i:Interaction.Triggers>
        <i:EventTrigger EventName="MouseDoubleClick">
            <i:InvokeCommandAction Command="{Binding OpenItemCommand}" />
        </i:EventTrigger>
    </i:Interaction.Triggers>
</ListView>
```

```csharp
// GOOD - ViewModel with commands
public class SearchViewModel : ViewModelBase
{
    private string _searchText = string.Empty;
    private object? _selectedItem;
    
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                // React to property changes
                SearchCommand.Execute(null);
            }
        }
    }
    
    public object? SelectedItem
    {
        get => _selectedItem;
        set => SetProperty(ref _selectedItem, value);
    }
    
    public ICommand SaveCommand { get; }
    public ICommand SearchCommand { get; }
    public ICommand OpenItemCommand { get; }
}
```

### 4.3 No Service Resolution in Views

Views should receive their dependencies through constructors, not resolve services directly.

```csharp
// BAD - Service locator anti-pattern in view
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        
        // Service locator - makes dependencies hidden and testing hard
        var container = App.Current.Services;
        var viewModel = container.GetRequiredService<MainViewModel>();
        DataContext = viewModel;
    }
}

// BAD - Static service access
public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
        
        // Static access hides dependencies
        DataContext = ServiceLocator.Current.GetInstance<SettingsViewModel>();
    }
}

// GOOD - Constructor injection
public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}

// GOOD - For UserControls, use DataContext from parent or dependency property
public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
        // DataContext inherited from parent or set via binding
    }
}

// Registration in composition root
services.AddTransient<MainWindow>();
services.AddTransient<MainViewModel>();
```

---

## 5. Thread Safety

### 5.1 Use Interlocked for Atomic Operations

Use `Interlocked` class for thread-safe operations on shared variables.

```csharp
// BAD - Race condition
public class Counter
{
    private int _count;
    
    public void Increment()
    {
        _count++; // Not thread-safe!
    }
    
    public int Count => _count;
}

// BAD - Lock for simple operations is overkill
public class Counter
{
    private readonly object _lock = new();
    private int _count;
    
    public void Increment()
    {
        lock (_lock)
        {
            _count++;
        }
    }
}

// GOOD - Use Interlocked for atomic operations
public class Counter
{
    private int _count;
    
    public void Increment()
    {
        Interlocked.Increment(ref _count);
    }
    
    public int Count => Volatile.Read(ref _count);
}

// GOOD - More complex Interlocked operations
public class AtomicState
{
    private int _state;
    
    public bool TryTransition(int expectedState, int newState)
    {
        return Interlocked.CompareExchange(ref _state, newState, expectedState) == expectedState;
    }
    
    public int AddAndGet(int value)
    {
        return Interlocked.Add(ref _state, value);
    }
}
```

### 5.2 Use Dispatcher for UI Updates

All UI updates must happen on the UI thread. Use `Dispatcher` to marshal calls to the UI thread.

```csharp
// BAD - UI update from background thread
public async Task LoadDataAsync()
{
    var data = await Task.Run(() => FetchData());
    Items.Clear(); // Crash! Wrong thread
    foreach (var item in data)
    {
        Items.Add(item); // Crash!
    }
}

// BAD - Using Invoke when InvokeAsync is available
public async Task LoadDataAsync()
{
    var data = await Task.Run(() => FetchData());
    
    Application.Current.Dispatcher.Invoke(() =>
    {
        Items.Clear();
        foreach (var item in data)
        {
            Items.Add(item);
        }
    });
}

// GOOD - Use IDispatcherService abstraction
public class MainViewModel : ViewModelBase
{
    private readonly IDispatcherService _dispatcher;
    
    public async Task LoadDataAsync()
    {
        var data = await Task.Run(() => FetchData()).ConfigureAwait(false);
        
        await _dispatcher.InvokeAsync(() =>
        {
            Items.Clear();
            foreach (var item in data)
            {
                Items.Add(item);
            }
        });
    }
}

// GOOD - Check if dispatch is needed
public async Task UpdateStatusAsync(string status)
{
    if (_dispatcher.CheckAccess())
    {
        Status = status;
    }
    else
    {
        await _dispatcher.InvokeAsync(() => Status = status);
    }
}

// GOOD - Use progress reporting pattern
public async Task ProcessFilesAsync(IEnumerable<string> files, IProgress<int> progress)
{
    int processed = 0;
    await Parallel.ForEachAsync(files, async (file, ct) =>
    {
        await ProcessFileAsync(file, ct);
        progress.Report(Interlocked.Increment(ref processed));
    });
}

// In ViewModel
var progress = new Progress<int>(count => 
{
    // Automatically marshaled to UI thread
    ProcessedCount = count;
});
await ProcessFilesAsync(files, progress);
```

### 5.3 Prefer Immutable Data Structures

Use immutable types where possible to avoid threading issues entirely.

```csharp
// BAD - Mutable shared state
public class Configuration
{
    public string Theme { get; set; }
    public bool AutoSave { get; set; }
    public List<string> RecentFiles { get; set; } = new();
}

// Thread A reads RecentFiles while Thread B modifies it - crash!

// GOOD - Immutable configuration
public record Configuration(
    string Theme,
    bool AutoSave,
    ImmutableList<string> RecentFiles)
{
    public static Configuration Default => new(
        Theme: "Light",
        AutoSave: true,
        RecentFiles: ImmutableList<string>.Empty);
    
    public Configuration WithRecentFile(string file) =>
        this with { RecentFiles = RecentFiles.Insert(0, file).Take(10).ToImmutableList() };
}

// GOOD - Thread-safe updates with immutable snapshots
public class ConfigurationService : IConfigurationService
{
    private Configuration _config = Configuration.Default;
    private readonly object _lock = new();
    
    public Configuration Current => _config; // Safe to read without lock
    
    public void Update(Func<Configuration, Configuration> updater)
    {
        lock (_lock)
        {
            _config = updater(_config);
        }
        OnConfigurationChanged();
    }
    
    public void AddRecentFile(string file)
    {
        Update(config => config.WithRecentFile(file));
    }
}
```

---
