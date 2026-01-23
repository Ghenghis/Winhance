# Winhance-FS Architecture Guide 🏗️

This document describes the architecture of Winhance-FS, following the same patterns established by Winhance.

## Overview

Winhance-FS uses a **3-tier architecture** with a high-performance Rust backend:

```
┌─────────────────────────────────────────────────────────────────────────┐
│                     WINHANCE-FS ARCHITECTURE                             │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                          │
│  ┌────────────────────────────────────────────────────────────────────┐ │
│  │  PRESENTATION LAYER (Winhance.WPF)                                 │ │
│  │  ──────────────────────────────────                                │ │
│  │  • WPF + Fluent Design System                                      │ │
│  │  • MVVM with CommunityToolkit.Mvvm                                 │ │
│  │  • Storage Intelligence Views                                      │ │
│  │  • Borg Theme Studio                                               │ │
│  └────────────────────────────────────────────────────────────────────┘ │
│                              │                                           │
│                    Dependency Injection                                  │
│                              ▼                                           │
│  ┌────────────────────────────────────────────────────────────────────┐ │
│  │  SERVICE LAYER (Winhance.Infrastructure)                           │ │
│  │  ───────────────────────────────────────                           │ │
│  │  • C# Service Implementations                                      │ │
│  │  • Rust Interop via UniFFI                                         │ │
│  │  • OperationResult<T> Pattern                                      │ │
│  │  • PowerShell Execution                                            │ │
│  └────────────────────────────────────────────────────────────────────┘ │
│                              │                                           │
│                         FFI Boundary                                     │
│                              ▼                                           │
│  ┌────────────────────────────────────────────────────────────────────┐ │
│  │  DOMAIN LAYER (Winhance.Core)                                      │ │
│  │  ────────────────────────────                                      │ │
│  │  • Interfaces (IMftService, IVssShadowService, etc.)              │ │
│  │  • Models (MftEntry, ShadowCopy, ClassifiedFile, etc.)            │ │
│  │  • Events (FileScanProgressEvent, MemoryRecoveryEvent)            │ │
│  └────────────────────────────────────────────────────────────────────┘ │
│                              │                                           │
│                         UniFFI Bindings                                  │
│                              ▼                                           │
│  ┌────────────────────────────────────────────────────────────────────┐ │
│  │  NATIVE LAYER (nexus-native - Rust)                                │ │
│  │  ──────────────────────────────────                                │ │
│  │  • MFT Parser (ntfs crate)                                         │ │
│  │  • SIMD Search (memchr, tantivy)                                   │ │
│  │  • VSS Shadow Copy Access                                          │ │
│  │  • Memory Recovery (Standby List)                                  │ │
│  └────────────────────────────────────────────────────────────────────┘ │
│                                                                          │
└─────────────────────────────────────────────────────────────────────────┘
```

## Layer Details

### 1. Presentation Layer (Winhance.WPF)

The presentation layer follows Winhance's established patterns:

**Feature Registration:**
```csharp
// FeatureIds.cs
public static class FeatureIds
{
    public const string StorageIntelligence = "StorageIntelligence";
    public const string DeepScan = "DeepScan";
    public const string ModelManager = "ModelManager";
    public const string CacheManager = "CacheManager";
    public const string ForensicsTools = "ForensicsTools";
}

// FeatureDefinitions.cs
public static readonly List<FeatureDefinition> All = new()
{
    new(FeatureIds.StorageIntelligence, "Storage Intelligence", "ChartDonut3", "Storage", 1),
    new(FeatureIds.DeepScan, "Deep Scan", "Radar", "Storage", 2),
    new(FeatureIds.ModelManager, "AI Model Manager", "Brain", "Storage", 3),
};
```

**ViewModel Hierarchy:**
```
BaseViewModel
└── BaseFeatureViewModel
    └── BaseSettingsFeatureViewModel
        ├── StorageIntelligenceViewModel
        ├── DeepScanViewModel
        └── ModelManagerViewModel
```

### 2. Service Layer (Winhance.Infrastructure)

Services implement the `OperationResult<T>` pattern:

```csharp
public class MftService : IMftService
{
    private readonly INexusNativeService _native;

    public async Task<OperationResult<MftScanResult>> ScanMftAsync(string driveLetter)
    {
        try
        {
            var result = await _native.ScanMftAsync(driveLetter);
            return OperationResult.CreateSuccess(result);
        }
        catch (Exception ex)
        {
            return OperationResult.CreateFailure<MftScanResult>(ex.Message, ex);
        }
    }
}
```

### 3. Domain Layer (Winhance.Core)

Contains interfaces, models, and events:

```csharp
// Interfaces
public interface IMftService
{
    Task<OperationResult<MftScanResult>> ScanMftAsync(string driveLetter);
    Task<OperationResult<SearchResults>> SearchAsync(string query, SearchOptions options);
}

// Models
public record MftEntry(
    ulong RecordNumber,
    string FileName,
    string? FullPath,
    ulong FileSize,
    DateTime CreationTime,
    bool IsDirectory,
    bool IsDeleted
);
```

### 4. Native Layer (nexus-native - Rust)

High-performance Rust backend with UniFFI exports:

```rust
// nexus.udl - UniFFI interface definition
namespace nexus {
    [Async]
    MftScanResult scan_mft(string drive_letter);

    [Async]
    SearchResults search(string query, SearchOptions options);

    [Async, Throws=NexusError]
    MoveResult relocate_with_symlink(
        string source,
        string destination,
        boolean create_symlink,
        boolean verify
    );
};
```

## Performance Architecture

### Search Pipeline

```
Query → Bloom Filter → SIMD Match → Tantivy Index → Results
         (~23ns)      (10.99 GB/s)   (2x Lucene)
```

### Performance Benchmarks

| Component | Crate | Performance |
|-----------|-------|-------------|
| MFT Parser | ntfs | 3.87s with cache |
| String Match | memchr | 10.99 GB/s |
| Bloom Filter | fastbloom | ~23ns lookup |
| Full-Text | tantivy | 2x Lucene |

## Feature Module Structure

```
src/
├── Winhance.Core/Features/Storage/
│   ├── Interfaces/
│   │   ├── IMftService.cs
│   │   ├── IVssShadowService.cs
│   │   └── ISpaceAnalysisService.cs
│   └── Models/
│       ├── MftEntry.cs
│       └── SpaceRecoveryItem.cs
│
├── Winhance.Infrastructure/Features/Storage/
│   ├── Services/
│   │   └── MftService.cs
│   └── Native/
│       └── NexusNative.cs
│
├── Winhance.WPF/Features/
│   ├── Storage/
│   │   ├── ViewModels/
│   │   └── Views/
│   └── ThemeStudio/
│       ├── ViewModels/
│       └── Views/
│
└── nexus-native/src/
    ├── lib.rs
    ├── nexus.udl
    ├── mft/
    ├── search/
    └── vss/
```

## Security Considerations

1. **Transaction Logging** - All file operations logged for rollback
2. **VSS Integration** - Shadow copy references before modifications
3. **Permission Checks** - Admin elevation only when required
4. **Sandbox Mode** - Preview-only by default

---

*For more details, see the [Development Guide](DEVELOPMENT.md).*
