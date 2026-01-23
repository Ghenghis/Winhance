# Rust Backend (nexus-native)

## Overview

The `nexus-native` crate provides high-performance file system operations for Winhance-FS, implemented in Rust for maximum speed and safety.

## Why Rust?

- **Performance**: Direct NTFS/MFT access, SIMD operations
- **Safety**: Memory-safe without garbage collection
- **Interop**: UniFFI generates C# bindings automatically
- **Ecosystem**: Excellent crates for Windows APIs

## Architecture

```
nexus-native/
+-- Cargo.toml           # Dependencies and build config
+-- build.rs             # UniFFI build script
+-- src/
    +-- lib.rs           # Library entry point
    +-- nexus.udl        # UniFFI interface definition
    +-- mft/             # MFT parsing
    |   +-- mod.rs
    |   +-- parser.rs
    |   +-- iterator.rs
    +-- search/          # SIMD search engine
    |   +-- mod.rs
    |   +-- simd.rs
    |   +-- bloom.rs
    |   +-- tantivy.rs
    +-- vss/             # Shadow copies
    |   +-- mod.rs
    |   +-- shadow.rs
    +-- memory/          # Memory recovery
    |   +-- mod.rs
    |   +-- standby.rs
    |   +-- pool_tags.rs
    +-- classification/  # File classification
        +-- mod.rs
        +-- entropy.rs
        +-- magic.rs
```

## Key Dependencies

```toml
[dependencies]
# NTFS Access
ntfs = "0.4"                    # MFT parsing

# SIMD Search
memchr = "2.7"                  # 10.99 GB/s string matching
aho-corasick = "1.1"            # Multi-pattern matching
regex = "1.10"                  # SIMD regex

# Bloom Filters
fastbloom = "0.7"               # Fast negative lookups

# Full-text Search
tantivy = "0.22"                # Rust search engine

# Windows APIs
windows = "0.58"                # Official Microsoft bindings

# FFI
uniffi = "0.28"                 # C# binding generation
```

## Building

### Prerequisites

- Rust 1.75+ (install via [rustup](https://rustup.rs))
- Windows 10/11 SDK
- Visual Studio Build Tools

### Build Commands

```bash
cd src/nexus-native

# Debug build
cargo build

# Release build (optimized)
cargo build --release

# Run tests
cargo test

# Run benchmarks
cargo bench

# Generate C# bindings
cargo run --bin uniffi-bindgen generate src/nexus.udl --language csharp --out-dir ../Winhance.Infrastructure/Native/RustInterop/
```

## UniFFI Interface

The `nexus.udl` file defines the interface exposed to C#:

```
namespace nexus {
    // Async function for drive scanning
    [Async]
    DrivesScanResult scan_all_drives();

    // Search with options
    [Async]
    SearchResults search(string query, SearchOptions options);

    // File move with transaction
    [Async, Throws=NexusError]
    MoveResult relocate_with_symlink(
        string source,
        string destination,
        boolean create_symlink,
        boolean verify
    );
};

dictionary DrivesScanResult {
    sequence<DriveInfo> drives;
    sequence<SpaceRecoveryItem> recovery_items;
    u64 total_recoverable_bytes;
};

[Error]
enum NexusError {
    "IoError",
    "PermissionDenied",
    "NotFound",
    // ...
};
```

### Generated C# Bindings

UniFFI generates `NexusNative.cs`:

```csharp
namespace Nexus;

public static class NexusMethods
{
    public static async Task<DrivesScanResult> ScanAllDrivesAsync()
    {
        // Auto-generated FFI call
    }

    public static async Task<SearchResults> SearchAsync(
        string query,
        SearchOptions options)
    {
        // Auto-generated FFI call
    }
}
```

## Core Modules

### MFT Parser

Direct NTFS Master File Table access for ultra-fast file enumeration:

```rust
// mft/parser.rs
use ntfs::Ntfs;

pub struct MftParser {
    ntfs: Ntfs,
}

impl MftParser {
    pub fn new(drive_letter: &str) -> Result<Self, NexusError> {
        let path = format!("\\\\.\\{}:", drive_letter);
        let file = std::fs::File::open(&path)?;
        let ntfs = Ntfs::new(file)?;
        Ok(Self { ntfs })
    }

    pub fn scan(&self) -> impl Iterator<Item = MftEntry> + '_ {
        self.ntfs.mft_records()
            .filter_map(|record| record.ok())
            .map(|record| MftEntry::from(record))
    }
}
```

**Performance**: 1M files in <1 second (vs 10+ seconds for FindFirstFile)

### SIMD Search

High-performance string matching using memchr:

```rust
// search/simd.rs
use memchr::memmem;

pub fn simd_search(haystack: &[u8], needle: &[u8]) -> Vec<usize> {
    let finder = memmem::Finder::new(needle);
    finder.find_iter(haystack).collect()
}

// Multi-pattern matching
use aho_corasick::AhoCorasick;

pub fn multi_pattern_search(text: &str, patterns: &[&str]) -> Vec<Match> {
    let ac = AhoCorasick::new(patterns).unwrap();
    ac.find_iter(text)
        .map(|m| Match {
            pattern_index: m.pattern().as_usize(),
            start: m.start(),
            end: m.end(),
        })
        .collect()
}
```

**Performance**: 10.99 GB/s throughput with memchr

### Bloom Filter

Fast pre-filtering for search:

```rust
// search/bloom.rs
use fastbloom::BloomFilter;

pub struct SearchIndex {
    bloom: BloomFilter,
    // ... other index data
}

impl SearchIndex {
    pub fn might_contain(&self, term: &str) -> bool {
        // ~23ns lookup time
        self.bloom.contains(term)
    }

    pub fn search(&self, query: &str) -> Vec<SearchResult> {
        // Quick negative check
        if !self.might_contain(query) {
            return vec![];
        }

        // Full search only if bloom filter passes
        self.full_search(query)
    }
}
```

### Entropy Calculator

Detect encrypted/compressed files:

```rust
// classification/entropy.rs

pub fn calculate_entropy(data: &[u8]) -> f64 {
    if data.is_empty() {
        return 0.0;
    }

    let mut frequencies = [0u64; 256];
    for &byte in data {
        frequencies[byte as usize] += 1;
    }

    let len = data.len() as f64;
    let mut entropy = 0.0;

    for &count in &frequencies {
        if count > 0 {
            let probability = count as f64 / len;
            entropy -= probability * probability.log2();
        }
    }

    entropy  // 0.0-8.0, >7.5 = likely encrypted
}
```

### Windows API Integration

Using official Microsoft Rust bindings:

```rust
// memory/standby.rs
use windows::Win32::System::SystemInformation::*;
use windows::Win32::System::Memory::*;

pub fn clear_standby_list() -> Result<u64, NexusError> {
    unsafe {
        // Requires SeIncreaseQuotaPrivilege
        let command = MemoryPurgeStandbyList;
        let status = NtSetSystemInformation(
            SystemMemoryListInformation,
            &command as *const _ as *mut _,
            std::mem::size_of_val(&command) as u32,
        );

        if status.is_ok() {
            Ok(get_freed_bytes())
        } else {
            Err(NexusError::MemoryRecoveryError)
        }
    }
}
```

## Testing

### Unit Tests

```rust
#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_entropy_empty() {
        assert_eq!(calculate_entropy(&[]), 0.0);
    }

    #[test]
    fn test_entropy_uniform() {
        // All same bytes = 0 entropy
        let data = vec![0u8; 1000];
        assert_eq!(calculate_entropy(&data), 0.0);
    }

    #[test]
    fn test_entropy_random() {
        // Random data = high entropy
        let data: Vec<u8> = (0..1000).map(|_| rand::random()).collect();
        let entropy = calculate_entropy(&data);
        assert!(entropy > 7.0);
    }

    #[tokio::test]
    async fn test_scan_drive() {
        let result = scan_mft("C:").await;
        assert!(result.is_ok());
    }
}
```

### Benchmarks

```rust
// benches/mft_scan.rs
use criterion::{criterion_group, criterion_main, Criterion};

fn bench_mft_scan(c: &mut Criterion) {
    c.bench_function("mft_scan_c_drive", |b| {
        b.iter(|| {
            let parser = MftParser::new("C:").unwrap();
            let count = parser.scan().count();
            assert!(count > 0);
        })
    });
}

fn bench_simd_search(c: &mut Criterion) {
    let haystack = vec![b'a'; 1_000_000];
    let needle = b"pattern";

    c.bench_function("simd_search_1mb", |b| {
        b.iter(|| simd_search(&haystack, needle))
    });
}

criterion_group!(benches, bench_mft_scan, bench_simd_search);
criterion_main!(benches);
```

Run benchmarks:
```bash
cargo bench
```

## Error Handling

```rust
use thiserror::Error;

#[derive(Error, Debug, uniffi::Error)]
pub enum NexusError {
    #[error("IO error: {0}")]
    IoError(String),

    #[error("Permission denied: requires elevation")]
    PermissionDenied,

    #[error("File not found: {0}")]
    NotFound(String),

    #[error("Operation failed: {0}")]
    OperationFailed(String),

    #[error("Memory recovery failed")]
    MemoryRecoveryError,
}

impl From<std::io::Error> for NexusError {
    fn from(err: std::io::Error) -> Self {
        NexusError::IoError(err.to_string())
    }
}
```

## Performance Optimization

### Release Profile

```toml
[profile.release]
opt-level = 3        # Maximum optimization
lto = "fat"          # Link-time optimization
codegen-units = 1    # Single codegen unit for better optimization
panic = "abort"      # Smaller binary
strip = true         # Strip symbols
```

### SIMD Targeting

```toml
# .cargo/config.toml
[build]
rustflags = ["-C", "target-cpu=native"]
```

### Memory Efficiency

- Use `memmap2` for large file access
- Pre-allocate vectors with known sizes
- Use `parking_lot` for faster locks
- Avoid unnecessary allocations

## Debugging

### Logging

```rust
use tracing::{info, warn, error, instrument};

#[instrument(skip(data))]
pub fn process_file(path: &str, data: &[u8]) -> Result<(), NexusError> {
    info!("Processing file: {}", path);

    if data.is_empty() {
        warn!("Empty file: {}", path);
        return Ok(());
    }

    // Processing...

    Ok(())
}
```

Enable logging:
```bash
RUST_LOG=debug cargo run
```

### Windows Debugging

```rust
// Print Win32 error details
use windows::Win32::Foundation::GetLastError;

unsafe {
    let error = GetLastError();
    eprintln!("Win32 error: {:?}", error);
}
```

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for Rust-specific guidelines.

Key points:
- Run `cargo fmt` before committing
- Run `cargo clippy -- -D warnings` for linting
- Add tests for new functionality
- Document public APIs with doc comments
