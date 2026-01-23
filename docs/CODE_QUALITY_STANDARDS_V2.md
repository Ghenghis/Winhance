# Winhance Code Quality Standards V2

**Version:** 2.0  
**Last Updated:** January 22, 2026  
**Applies To:** All C#, Rust, and Python code

---

## 1. Error Handling Standards

### 1.1 C# Exception Handling

**REQUIRED:** Never use bare catch blocks.

```csharp
// ❌ FORBIDDEN
catch { }
catch { return null; }

// ✅ REQUIRED
catch (SpecificException ex)
{
    _logService?.Log(LogLevel.Warning, $"Context: {ex.Message}");
    return OperationResult<T>.Failure(ex.Message);
}

// ✅ ACCEPTABLE for non-critical cleanup
catch (Exception ex)
{
    System.Diagnostics.Debug.WriteLine($"[ClassName] Cleanup error: {ex.Message}");
}
```

### 1.2 Exception Specificity

Order catches from most specific to least:

```csharp
try { /* code */ }
catch (FileNotFoundException ex) { /* handle missing file */ }
catch (UnauthorizedAccessException ex) { /* handle permission */ }
catch (IOException ex) { /* handle other IO */ }
catch (Exception ex) { /* last resort */ }
```

### 1.3 Rust Error Handling

**REQUIRED:** Use Result types, not panics.

```rust
// ❌ FORBIDDEN in library code
panic!("Error occurred");
unwrap(); // without safety comment

// ✅ REQUIRED
fn operation() -> Result<T, Error> {
    let value = risky_operation()?;
    Ok(value)
}

// ✅ ACCEPTABLE with safety comment
// SAFETY: Validated non-empty above
let first = items.first().unwrap();
```

---

## 2. Async/Await Standards

### 2.1 No Async Void

**REQUIRED:** Never use `async void` except for event handlers with try-catch.

```csharp
// ❌ FORBIDDEN
private async void DoWork() { await Task.Delay(1000); }

// ✅ REQUIRED
private async Task DoWorkAsync() { await Task.Delay(1000); }

// ✅ ACCEPTABLE for event handlers
private async void Button_Click(object sender, EventArgs e)
{
    try { await DoWorkAsync(); }
    catch (Exception ex) { HandleError(ex); }
}
```

### 2.2 No Blocking Async

**REQUIRED:** Never block on async code.

```csharp
// ❌ FORBIDDEN
var result = asyncMethod.Result;
var result = asyncMethod.GetAwaiter().GetResult();
asyncMethod.Wait();

// ✅ REQUIRED
var result = await asyncMethod;
```

### 2.3 Async All The Way

**REQUIRED:** Once async, stay async up the call stack.

```csharp
// ❌ FORBIDDEN
public void Method()
{
    var data = GetDataAsync().GetAwaiter().GetResult();
}

// ✅ REQUIRED
public async Task MethodAsync()
{
    var data = await GetDataAsync();
}
```

---

## 3. Memory Management Standards

### 3.1 IDisposable Implementation

**REQUIRED:** Implement IDisposable for classes with:
- Event subscriptions
- Unmanaged resources
- IDisposable dependencies

```csharp
public class MyViewModel : IDisposable
{
    private bool _disposed;
    
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
            // Unsubscribe events
            _service.Event -= OnEvent;
            // Dispose managed resources
        }
        _disposed = true;
    }
}
```

### 3.2 Event Subscription Cleanup

**REQUIRED:** Always unsubscribe from events.

```csharp
// ❌ FORBIDDEN - Memory leak
_service.DataChanged += (s, e) => UpdateUI();

// ✅ REQUIRED - Named handler for cleanup
_service.DataChanged += OnDataChanged;
// In Dispose:
_service.DataChanged -= OnDataChanged;
```

### 3.3 No Manual GC

**REQUIRED:** Never call GC.Collect() in production code.

```csharp
// ❌ FORBIDDEN
GC.Collect();
GC.WaitForPendingFinalizers();

// ✅ Let CLR manage garbage collection
```

---

## 4. Thread Safety Standards

### 4.1 Shared State Protection

**REQUIRED:** Use appropriate synchronization.

```csharp
// ❌ FORBIDDEN - Race condition
private bool _isProcessing;
public void Process()
{
    if (_isProcessing) return;
    _isProcessing = true;
}

// ✅ REQUIRED - Thread-safe
private int _isProcessing;
public void Process()
{
    if (Interlocked.CompareExchange(ref _isProcessing, 1, 0) != 0) return;
    try { /* work */ }
    finally { Interlocked.Exchange(ref _isProcessing, 0); }
}
```

### 4.2 Collection Thread Safety

**REQUIRED:** Use concurrent collections for shared access.

```csharp
// ❌ FORBIDDEN for shared access
private List<T> _items = new();

// ✅ REQUIRED for shared access
private ConcurrentBag<T> _items = new();
// Or with explicit locking:
private readonly object _lock = new();
private List<T> _items = new();
```

---

## 5. API Design Standards

### 5.1 Immutable Collections in Public API

**REQUIRED:** Return read-only collections from public APIs.

```csharp
// ❌ FORBIDDEN - Allows external mutation
public List<string> Items { get; set; }
public Dictionary<string, int> Data { get; }

// ✅ REQUIRED
public IReadOnlyList<string> Items { get; }
public IReadOnlyDictionary<string, int> Data { get; }

// Implementation:
private readonly List<string> _items = new();
public IReadOnlyList<string> Items => _items.AsReadOnly();
```

### 5.2 Null Safety

**REQUIRED:** Use nullable reference types and validate.

```csharp
// ❌ FORBIDDEN - Null-forgiving without check
var dir = Path.GetDirectoryName(path)!;

// ✅ REQUIRED - Explicit null handling
var dir = Path.GetDirectoryName(path);
if (string.IsNullOrEmpty(dir))
    throw new ArgumentException("Invalid path");
```

---

## 6. MVVM Standards (WPF)

### 6.1 ViewModel Independence

**REQUIRED:** ViewModels must not reference Views.

```csharp
// ❌ FORBIDDEN
public class MyViewModel
{
    private Window _window; // View reference
}

// ✅ REQUIRED - Use services for UI interaction
public class MyViewModel
{
    private readonly IDialogService _dialogService;
}
```

### 6.2 Command Implementation

**REQUIRED:** Use RelayCommand/DelegateCommand pattern.

```csharp
public ICommand SaveCommand { get; }

public MyViewModel()
{
    SaveCommand = new RelayCommand(
        execute: () => Save(),
        canExecute: () => CanSave
    );
}
```

---

## 7. Rust-Specific Standards

### 7.1 Unsafe Block Documentation

**REQUIRED:** Document all unsafe blocks.

```rust
// ❌ FORBIDDEN - Undocumented unsafe
unsafe { ptr::read(data) }

// ✅ REQUIRED
// SAFETY: `data` is guaranteed non-null and properly aligned
// by the caller contract. Lifetime is valid for duration of call.
unsafe { ptr::read(data) }
```

### 7.2 FFI Safety

**REQUIRED:** Validate all FFI inputs.

```rust
#[no_mangle]
pub extern "C" fn process_data(ptr: *const u8, len: usize) -> i32 {
    // ✅ REQUIRED - Null check
    if ptr.is_null() { return -1; }
    
    // ✅ REQUIRED - Bounds validation
    if len > MAX_BUFFER_SIZE { return -2; }
    
    // SAFETY: Validated above
    let slice = unsafe { std::slice::from_raw_parts(ptr, len) };
    // ...
}
```

---

## 8. Python-Specific Standards

### 8.1 Type Hints

**REQUIRED:** Use type hints for all public functions.

```python
# ❌ FORBIDDEN
def process(data):
    return data.upper()

# ✅ REQUIRED
def process(data: str) -> str:
    return data.upper()
```

### 8.2 Path Safety

**REQUIRED:** Validate paths before file operations.

```python
from pathlib import Path

def safe_read(base_dir: Path, filename: str) -> str:
    # ✅ REQUIRED - Prevent traversal
    safe_path = (base_dir / filename).resolve()
    if not str(safe_path).startswith(str(base_dir.resolve())):
        raise ValueError("Path traversal detected")
    return safe_path.read_text()
```

---

## 9. Testing Standards

### 9.1 Test Coverage Requirements

| Component   | Minimum Coverage |
| ----------- | ---------------- |
| Core Domain | 80%              |
| Services    | 70%              |
| ViewModels  | 60%              |
| Utilities   | 90%              |

### 9.2 Test Naming

```csharp
// Pattern: MethodName_Scenario_ExpectedResult
[Fact]
public void ParseOutput_ValidInput_ReturnsExpectedResult()

[Fact]
public void ParseOutput_NullInput_ThrowsArgumentNullException()
```

---

## 10. Code Review Checklist

Before merging, verify:

- [ ] No bare catch blocks
- [ ] No async void (except wrapped event handlers)
- [ ] No blocking async calls
- [ ] IDisposable implemented where needed
- [ ] Events unsubscribed in Dispose
- [ ] Collections returned as read-only
- [ ] Null checks for nullable references
- [ ] Unsafe blocks documented (Rust)
- [ ] Type hints present (Python)
- [ ] Tests added for new code
