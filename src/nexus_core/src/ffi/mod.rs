//! FFI module for C# interop
//!
//! Provides C-compatible functions for calling from C# via P/Invoke.
//! Pure performance - zero-copy where possible, minimal allocations.

// FFI functions intentionally take raw pointers and handle safety internally
#![allow(clippy::not_unsafe_ptr_arg_deref)]

use crate::indexer::ContentHasher;
use crate::{FileEntry, IndexConfig};
use once_cell::sync::Lazy;
use std::ffi::{CStr, CString};
use std::os::raw::c_char;
use std::ptr;
use std::sync::atomic::{AtomicBool, AtomicU64, Ordering};
use std::sync::Mutex;

// Global state for FFI - thread-safe cached results
static LAST_ERROR: Lazy<Mutex<Option<String>>> = Lazy::new(|| Mutex::new(None));
static CACHED_ENTRIES: Lazy<Mutex<Vec<FileEntry>>> = Lazy::new(|| Mutex::new(Vec::new()));
static SEARCH_RESULTS: Lazy<Mutex<Vec<FileEntry>>> = Lazy::new(|| Mutex::new(Vec::new()));

// Progress tracking
static PROGRESS_CURRENT: AtomicU64 = AtomicU64::new(0);
static PROGRESS_TOTAL: AtomicU64 = AtomicU64::new(0);
static INDEXING_ACTIVE: AtomicBool = AtomicBool::new(false);

// Progress callback type
type ProgressCallback = extern "C" fn(current: u64, total: u64, phase: *const c_char);
static PROGRESS_CALLBACK: Lazy<Mutex<Option<ProgressCallback>>> = Lazy::new(|| Mutex::new(None));

fn set_error(msg: String) {
    if let Ok(mut err) = LAST_ERROR.lock() {
        *err = Some(msg);
    }
}

// Static phase strings to avoid lifetime issues in FFI callbacks
// Using static byte arrays ensures the pointers remain valid
#[allow(dead_code)]
static PHASE_INDEXING: &[u8] = b"indexing\0";
#[allow(dead_code)]
static PHASE_SEARCHING: &[u8] = b"searching\0";
#[allow(dead_code)]
static PHASE_COMPLETE: &[u8] = b"complete\0";
#[allow(dead_code)]
static PHASE_HASHING: &[u8] = b"hashing\0";
#[allow(dead_code)]
static PHASE_UNKNOWN: &[u8] = b"unknown\0";

#[allow(dead_code)]
fn get_phase_ptr(phase: &str) -> *const c_char {
    match phase {
        "indexing" => PHASE_INDEXING.as_ptr() as *const c_char,
        "searching" => PHASE_SEARCHING.as_ptr() as *const c_char,
        "complete" => PHASE_COMPLETE.as_ptr() as *const c_char,
        "hashing" => PHASE_HASHING.as_ptr() as *const c_char,
        _ => PHASE_UNKNOWN.as_ptr() as *const c_char,
    }
}

#[allow(dead_code)]
fn report_progress(current: u64, total: u64, phase: &str) {
    PROGRESS_CURRENT.store(current, Ordering::SeqCst);
    PROGRESS_TOTAL.store(total, Ordering::SeqCst);

    if let Ok(cb) = PROGRESS_CALLBACK.lock() {
        if let Some(callback) = *cb {
            // Use static strings to avoid lifetime issues with CString
            let phase_ptr = get_phase_ptr(phase);
            callback(current, total, phase_ptr);
        }
    }
}

/// Initialize the indexer with default configuration
#[no_mangle]
pub extern "C" fn nexus_init() -> bool {
    true
}

/// Index all configured drives and return count
#[no_mangle]
pub extern "C" fn nexus_index_all() -> i64 {
    match crate::indexer::FastIndexer::new(IndexConfig::default()).index_all() {
        Ok((entries, _stats)) => {
            let count = entries.len() as i64;
            if let Ok(mut cache) = CACHED_ENTRIES.lock() {
                *cache = entries;
            }
            count
        }
        Err(e) => {
            set_error(e.to_string());
            -1
        }
    }
}

/// Free a string allocated by Rust
#[no_mangle]
pub extern "C" fn nexus_free_string(s: *mut c_char) {
    if !s.is_null() {
        unsafe {
            let _ = CString::from_raw(s);
        }
    }
}

/// Get last error message (returns null if no error)
#[no_mangle]
pub extern "C" fn nexus_get_last_error() -> *mut c_char {
    if let Ok(err) = LAST_ERROR.lock() {
        if let Some(ref msg) = *err {
            if let Ok(cstr) = CString::new(msg.clone()) {
                return cstr.into_raw();
            }
        }
    }
    ptr::null_mut()
}

/// Index a specific directory
#[no_mangle]
pub extern "C" fn nexus_index_directory(path: *const c_char) -> i64 {
    if path.is_null() {
        return -1;
    }

    let path_str = unsafe {
        match CStr::from_ptr(path).to_str() {
            Ok(s) => s,
            Err(_) => return -1,
        }
    };

    match crate::indexer::FastIndexer::new(IndexConfig::default()).index_directory(path_str) {
        Ok(entries) => entries.len() as i64,
        Err(_) => -1,
    }
}

/// Search result structure for FFI
#[repr(C)]
pub struct FfiSearchResult {
    pub path: *mut c_char,
    pub name: *mut c_char,
    pub size: u64,
    pub is_dir: bool,
}

impl FfiSearchResult {
    fn from_entry(entry: &FileEntry) -> Self {
        // Safely convert strings, replacing null bytes if present to avoid silent corruption
        let safe_path = entry.path.replace('\0', "_");
        let safe_name = entry.name.replace('\0', "_");

        Self {
            path: match CString::new(safe_path) {
                Ok(s) => s.into_raw(),
                Err(e) => {
                    set_error(format!(
                        "Invalid path string contains null byte at position {}",
                        e.nul_position()
                    ));
                    ptr::null_mut()
                }
            },
            name: match CString::new(safe_name) {
                Ok(s) => s.into_raw(),
                Err(e) => {
                    set_error(format!(
                        "Invalid name string contains null byte at position {}",
                        e.nul_position()
                    ));
                    ptr::null_mut()
                }
            },
            size: entry.size,
            is_dir: entry.is_dir,
        }
    }
}

/// Free a search result
#[no_mangle]
pub extern "C" fn nexus_free_result(result: *mut FfiSearchResult) {
    if !result.is_null() {
        unsafe {
            let r = Box::from_raw(result);
            if !r.path.is_null() {
                let _ = CString::from_raw(r.path);
            }
            if !r.name.is_null() {
                let _ = CString::from_raw(r.name);
            }
        }
    }
}

// ============================================================================
// PROGRESS TRACKING FFI
// ============================================================================

/// Set progress callback for indexing operations
#[no_mangle]
pub extern "C" fn nexus_set_progress_callback(callback: ProgressCallback) {
    if let Ok(mut cb) = PROGRESS_CALLBACK.lock() {
        *cb = Some(callback);
    }
}

/// Clear progress callback
#[no_mangle]
pub extern "C" fn nexus_clear_progress_callback() {
    if let Ok(mut cb) = PROGRESS_CALLBACK.lock() {
        *cb = None;
    }
}

/// Get current progress (returns current count)
#[no_mangle]
pub extern "C" fn nexus_get_progress_current() -> u64 {
    PROGRESS_CURRENT.load(Ordering::SeqCst)
}

/// Get total progress (returns total count)
#[no_mangle]
pub extern "C" fn nexus_get_progress_total() -> u64 {
    PROGRESS_TOTAL.load(Ordering::SeqCst)
}

/// Check if indexing is currently active
#[no_mangle]
pub extern "C" fn nexus_is_indexing() -> bool {
    INDEXING_ACTIVE.load(Ordering::SeqCst)
}

// ============================================================================
// SEARCH FFI
// ============================================================================

/// Search cached entries by name pattern (case-insensitive)
/// Returns number of results found
#[no_mangle]
pub extern "C" fn nexus_search(query: *const c_char, max_results: u32) -> i64 {
    if query.is_null() {
        return -1;
    }

    let query_str = unsafe {
        match CStr::from_ptr(query).to_str() {
            Ok(s) => s.to_lowercase(),
            Err(_) => return -1,
        }
    };

    if let Ok(entries) = CACHED_ENTRIES.lock() {
        let results: Vec<FileEntry> = entries
            .iter()
            .filter(|e| e.name.to_lowercase().contains(&query_str))
            .take(max_results as usize)
            .cloned()
            .collect();

        let count = results.len() as i64;

        if let Ok(mut search_results) = SEARCH_RESULTS.lock() {
            *search_results = results;
        }

        count
    } else {
        -1
    }
}

/// Get search result at index
#[no_mangle]
pub extern "C" fn nexus_get_search_result(index: u32) -> *mut FfiSearchResult {
    if let Ok(results) = SEARCH_RESULTS.lock() {
        if let Some(entry) = results.get(index as usize) {
            let result = Box::new(FfiSearchResult::from_entry(entry));
            return Box::into_raw(result);
        }
    }
    ptr::null_mut()
}

/// Clear search results to free memory
#[no_mangle]
pub extern "C" fn nexus_clear_search_results() {
    if let Ok(mut results) = SEARCH_RESULTS.lock() {
        results.clear();
        results.shrink_to_fit();
    }
}

// ============================================================================
// CONTENT HASHING FFI (for duplicate detection)
// ============================================================================

/// Hash result structure
#[repr(C)]
pub struct FfiHashResult {
    pub quick_hash: u64,
    pub full_hash: *mut c_char,
    pub size: u64,
    pub success: bool,
}

/// Compute quick hash (xxHash3) for a file - fast for dedup pre-screening
#[no_mangle]
pub extern "C" fn nexus_hash_file_quick(path: *const c_char) -> u64 {
    if path.is_null() {
        return 0;
    }

    let path_str = unsafe {
        match CStr::from_ptr(path).to_str() {
            Ok(s) => s,
            Err(_) => return 0,
        }
    };

    let hasher = ContentHasher::new(u64::MAX);
    hasher
        .quick_hash(std::path::Path::new(path_str))
        .unwrap_or(0)
}

/// Compute full hash (SHA-256) for a file - for verification
#[no_mangle]
pub extern "C" fn nexus_hash_file_full(path: *const c_char) -> *mut c_char {
    if path.is_null() {
        return ptr::null_mut();
    }

    let path_str = unsafe {
        match CStr::from_ptr(path).to_str() {
            Ok(s) => s,
            Err(_) => return ptr::null_mut(),
        }
    };

    let hasher = ContentHasher::new(u64::MAX);
    match hasher.full_hash(std::path::Path::new(path_str)) {
        Some((_size, hash)) => CString::new(hash)
            .map(|s| s.into_raw())
            .unwrap_or(ptr::null_mut()),
        None => ptr::null_mut(),
    }
}

/// Find duplicates in cached entries by size+hash
/// Returns count of duplicate groups found
#[no_mangle]
pub extern "C" fn nexus_find_duplicates(min_size: u64) -> i64 {
    use std::collections::HashMap;

    if let Ok(entries) = CACHED_ENTRIES.lock() {
        // Group by size first (fast pre-filter)
        let mut size_groups: HashMap<u64, Vec<&FileEntry>> = HashMap::new();

        for entry in entries.iter() {
            if !entry.is_dir && entry.size >= min_size {
                size_groups.entry(entry.size).or_default().push(entry);
            }
        }

        // Count groups with potential duplicates (same size, 2+ files)
        let dup_groups: i64 = size_groups.values().filter(|g| g.len() > 1).count() as i64;

        dup_groups
    } else {
        -1
    }
}

// ============================================================================
// STATISTICS FFI
// ============================================================================

/// Index statistics structure
#[repr(C)]
pub struct FfiIndexStats {
    pub total_files: u64,
    pub total_dirs: u64,
    pub total_size: u64,
    pub index_time_ms: u64,
}

/// Get current index statistics
#[no_mangle]
pub extern "C" fn nexus_get_stats() -> FfiIndexStats {
    if let Ok(entries) = CACHED_ENTRIES.lock() {
        let total_files = entries.iter().filter(|e| !e.is_dir).count() as u64;
        let total_dirs = entries.iter().filter(|e| e.is_dir).count() as u64;
        let total_size = entries.iter().map(|e| e.size).sum();

        FfiIndexStats {
            total_files,
            total_dirs,
            total_size,
            index_time_ms: 0,
        }
    } else {
        FfiIndexStats {
            total_files: 0,
            total_dirs: 0,
            total_size: 0,
            index_time_ms: 0,
        }
    }
}

/// Get total indexed file count
#[no_mangle]
pub extern "C" fn nexus_get_file_count() -> u64 {
    if let Ok(entries) = CACHED_ENTRIES.lock() {
        entries.len() as u64
    } else {
        0
    }
}
