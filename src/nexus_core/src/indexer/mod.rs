//! Fast file indexing module
//!
//! Provides ultra-fast file indexing using:
//! - MFT (Master File Table) direct reading for NTFS volumes
//! - USN Journal for real-time change tracking
//! - Parallel directory traversal for non-NTFS volumes

mod content_hasher;
mod metadata_extractor;
mod mft_reader;
mod usn_journal;

pub use content_hasher::ContentHasher;
pub use metadata_extractor::MetadataExtractor;
pub use mft_reader::MftReader;
pub use usn_journal::UsnJournal;

use crate::{FileEntry, IndexStats, NexusError, Result};
use dashmap::DashMap;
use rayon::prelude::*;
use std::path::Path;
use std::sync::atomic::{AtomicU64, Ordering};
use std::sync::Arc;
use std::time::Instant;
use tracing::{debug, info, warn};

/// Index configuration
#[derive(Debug, Clone, serde::Serialize, serde::Deserialize)]
pub struct IndexConfig {
    /// Drives to index (e.g., ['C', 'D', 'E'])
    pub drives: Vec<char>,
    /// Include hidden files
    pub include_hidden: bool,
    /// Include system files
    pub include_system: bool,
    /// Compute content hashes for deduplication
    pub compute_hashes: bool,
    /// Maximum file size for hashing (in bytes)
    pub max_hash_size: u64,
    /// File extensions to index (empty = all)
    pub extensions: Vec<String>,
    /// Directories to exclude
    pub exclude_dirs: Vec<String>,
    /// Use MFT reader when available (faster)
    pub use_mft: bool,
    /// Number of parallel threads
    pub threads: usize,
}

impl Default for IndexConfig {
    fn default() -> Self {
        Self {
            drives: vec!['C', 'D', 'E', 'F', 'G'],
            include_hidden: true,
            include_system: false,
            compute_hashes: false,
            max_hash_size: 100 * 1024 * 1024, // 100MB
            extensions: vec![],
            exclude_dirs: vec![
                "$Recycle.Bin".to_string(),
                "System Volume Information".to_string(),
                "Windows".to_string(),
                "Program Files".to_string(),
                "Program Files (x86)".to_string(),
                "ProgramData".to_string(),
            ],
            use_mft: true,
            threads: num_cpus::get(),
        }
    }
}

/// Fast file indexer
pub struct FastIndexer {
    config: IndexConfig,
    metadata_extractor: MetadataExtractor,
    #[allow(dead_code)]
    content_hasher: ContentHasher,
}

impl FastIndexer {
    /// Create a new indexer with the given configuration
    pub fn new(config: IndexConfig) -> Self {
        Self {
            config: config.clone(),
            metadata_extractor: MetadataExtractor::new(),
            content_hasher: ContentHasher::new(config.max_hash_size),
        }
    }

    /// Index all configured drives
    pub fn index_all(&self) -> Result<(Vec<FileEntry>, IndexStats)> {
        let start = Instant::now();
        let entries: Arc<DashMap<String, FileEntry>> = Arc::new(DashMap::new());
        let total_files = AtomicU64::new(0);
        let total_dirs = AtomicU64::new(0);
        let total_size = AtomicU64::new(0);

        info!("Starting indexing of drives: {:?}", self.config.drives);

        // Index drives in parallel
        self.config.drives.par_iter().for_each(|&drive| {
            info!("Indexing drive {}:", drive);

            match self.index_drive(drive, &entries, &total_files, &total_dirs, &total_size) {
                Ok(count) => info!("Drive {}: indexed {} files", drive, count),
                Err(e) => warn!("Error indexing drive {}: {}", drive, e),
            }
        });

        let elapsed = start.elapsed();
        let stats = IndexStats {
            total_files: total_files.load(Ordering::Relaxed),
            total_dirs: total_dirs.load(Ordering::Relaxed),
            total_size: total_size.load(Ordering::Relaxed),
            index_time_ms: elapsed.as_millis() as u64,
            drives_indexed: self.config.drives.clone(),
        };

        info!(
            "Indexing complete: {} files, {} dirs, {} total in {}ms",
            stats.total_files,
            stats.total_dirs,
            format_size(stats.total_size),
            stats.index_time_ms
        );

        let result: Vec<FileEntry> = entries.iter().map(|e| e.value().clone()).collect();
        Ok((result, stats))
    }

    /// Index a single drive
    fn index_drive(
        &self,
        drive: char,
        entries: &Arc<DashMap<String, FileEntry>>,
        total_files: &AtomicU64,
        total_dirs: &AtomicU64,
        total_size: &AtomicU64,
    ) -> Result<u64> {
        let root = format!("{}:\\", drive);

        // Try MFT reader first (fastest), fall back to walkdir
        if self.config.use_mft {
            match MftReader::scan_volume(drive) {
                Ok(mft_entries) => {
                    let count = mft_entries.len() as u64;
                    for entry in mft_entries {
                        if self.should_include(&entry) {
                            if entry.is_dir {
                                total_dirs.fetch_add(1, Ordering::Relaxed);
                            } else {
                                total_files.fetch_add(1, Ordering::Relaxed);
                                total_size.fetch_add(entry.size, Ordering::Relaxed);
                            }
                            entries.insert(entry.path.clone(), entry);
                        }
                    }
                    return Ok(count);
                }
                Err(e) => {
                    debug!(
                        "MFT reader failed for drive {}: {}, falling back to walkdir",
                        drive, e
                    );
                }
            }
        }

        // Fallback to walkdir (still parallel)
        self.index_with_walkdir(&root, entries, total_files, total_dirs, total_size)
    }

    /// Index using walkdir (fallback method)
    fn index_with_walkdir(
        &self,
        root: &str,
        entries: &Arc<DashMap<String, FileEntry>>,
        total_files: &AtomicU64,
        total_dirs: &AtomicU64,
        total_size: &AtomicU64,
    ) -> Result<u64> {
        use walkdir::WalkDir;

        let count = AtomicU64::new(0);

        WalkDir::new(root)
            .follow_links(false)
            .into_iter()
            .par_bridge()
            .filter_map(|e| e.ok())
            .for_each(|entry| {
                if let Some(file_entry) = self.metadata_extractor.extract(entry.path()) {
                    if self.should_include(&file_entry) {
                        if file_entry.is_dir {
                            total_dirs.fetch_add(1, Ordering::Relaxed);
                        } else {
                            total_files.fetch_add(1, Ordering::Relaxed);
                            total_size.fetch_add(file_entry.size, Ordering::Relaxed);
                        }
                        count.fetch_add(1, Ordering::Relaxed);
                        entries.insert(file_entry.path.clone(), file_entry);
                    }
                }
            });

        Ok(count.load(Ordering::Relaxed))
    }

    /// Check if a file entry should be included based on config
    fn should_include(&self, entry: &FileEntry) -> bool {
        // Check hidden
        if !self.config.include_hidden && entry.is_hidden {
            return false;
        }

        // Check system
        if !self.config.include_system && entry.is_system {
            return false;
        }

        // Check excluded directories
        for exclude in &self.config.exclude_dirs {
            if entry.path.contains(exclude) {
                return false;
            }
        }

        // Check extensions (if specified)
        if !self.config.extensions.is_empty() && !entry.is_dir {
            if let Some(ext) = &entry.extension {
                if !self
                    .config
                    .extensions
                    .iter()
                    .any(|e| e.eq_ignore_ascii_case(ext))
                {
                    return false;
                }
            } else {
                return false;
            }
        }

        true
    }

    /// Index a single directory
    pub fn index_directory<P: AsRef<Path>>(&self, path: P) -> Result<Vec<FileEntry>> {
        let path = path.as_ref();
        if !path.exists() {
            return Err(NexusError::InvalidPath(path.display().to_string()));
        }

        let entries: Arc<DashMap<String, FileEntry>> = Arc::new(DashMap::new());
        let total_files = AtomicU64::new(0);
        let total_dirs = AtomicU64::new(0);
        let total_size = AtomicU64::new(0);

        self.index_with_walkdir(
            path.to_str().unwrap_or_default(),
            &entries,
            &total_files,
            &total_dirs,
            &total_size,
        )?;

        Ok(entries.iter().map(|e| e.value().clone()).collect())
    }
}

/// Format file size for display
fn format_size(size: u64) -> String {
    const UNITS: &[&str] = &["B", "KB", "MB", "GB", "TB"];
    let mut size = size as f64;
    let mut unit_idx = 0;

    while size >= 1024.0 && unit_idx < UNITS.len() - 1 {
        size /= 1024.0;
        unit_idx += 1;
    }

    format!("{:.2} {}", size, UNITS[unit_idx])
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_format_size() {
        assert_eq!(format_size(0), "0.00 B");
        assert_eq!(format_size(1024), "1.00 KB");
        assert_eq!(format_size(1024 * 1024), "1.00 MB");
    }
}
