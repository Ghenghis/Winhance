//! NexusFS Core - Ultra-fast file indexing engine
//!
//! This crate provides the core indexing functionality for NexusFS,
//! including MFT-based scanning, USN Journal monitoring, and Tantivy search.

pub mod ffi;
pub mod indexer;
pub mod search;
pub mod watcher;

use thiserror::Error;

/// Core error types for NexusFS
#[derive(Error, Debug)]
pub enum NexusError {
    #[error("IO error: {0}")]
    Io(#[from] std::io::Error),

    #[error("Index error: {0}")]
    Index(String),

    #[error("Search error: {0}")]
    Search(String),

    #[error("Windows API error: {0}")]
    Windows(String),

    #[error("Permission denied: {0}")]
    PermissionDenied(String),

    #[error("Invalid path: {0}")]
    InvalidPath(String),
}

pub type Result<T> = std::result::Result<T, NexusError>;

/// File entry with metadata
#[derive(Debug, Clone, serde::Serialize, serde::Deserialize)]
pub struct FileEntry {
    /// Full path to the file
    pub path: String,
    /// File name only
    pub name: String,
    /// File extension (lowercase, without dot)
    pub extension: Option<String>,
    /// File size in bytes
    pub size: u64,
    /// Creation time
    pub created: Option<chrono::DateTime<chrono::Utc>>,
    /// Last modified time
    pub modified: Option<chrono::DateTime<chrono::Utc>>,
    /// Last accessed time
    pub accessed: Option<chrono::DateTime<chrono::Utc>>,
    /// Is directory
    pub is_dir: bool,
    /// Is hidden
    pub is_hidden: bool,
    /// Is system file
    pub is_system: bool,
    /// Content hash (optional, for deduplication)
    pub content_hash: Option<String>,
    /// Parent directory
    pub parent: String,
    /// Drive letter
    pub drive: char,
}

impl FileEntry {
    /// Get human-readable file size
    pub fn human_size(&self) -> String {
        const UNITS: &[&str] = &["B", "KB", "MB", "GB", "TB"];
        let mut size = self.size as f64;
        let mut unit_idx = 0;

        while size >= 1024.0 && unit_idx < UNITS.len() - 1 {
            size /= 1024.0;
            unit_idx += 1;
        }

        if unit_idx == 0 {
            format!("{} {}", self.size, UNITS[unit_idx])
        } else {
            format!("{:.2} {}", size, UNITS[unit_idx])
        }
    }
}

/// Index statistics
#[derive(Debug, Clone, Default, serde::Serialize, serde::Deserialize)]
pub struct IndexStats {
    pub total_files: u64,
    pub total_dirs: u64,
    pub total_size: u64,
    pub index_time_ms: u64,
    pub drives_indexed: Vec<char>,
}

/// Re-export commonly used types
pub use indexer::{FastIndexer, IndexConfig};
pub use search::{SearchEngine, SearchQuery, SearchResult};
pub use watcher::{FileWatcher, WatchEvent};
