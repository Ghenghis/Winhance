//! File metadata extraction
//!
//! Extracts comprehensive metadata from files including:
//! - Basic attributes (size, dates, permissions)
//! - Extended attributes (hidden, system, readonly)
//! - File type detection

use crate::FileEntry;
use chrono::{DateTime, Utc};
use std::fs;
use std::path::Path;
use std::time::SystemTime;

/// Metadata extractor for files
pub struct MetadataExtractor {
    /// Whether to follow symlinks
    follow_symlinks: bool,
}

impl MetadataExtractor {
    /// Create a new metadata extractor
    pub fn new() -> Self {
        Self {
            follow_symlinks: false,
        }
    }

    /// Extract metadata from a file path
    pub fn extract(&self, path: &Path) -> Option<FileEntry> {
        let metadata = if self.follow_symlinks {
            fs::metadata(path).ok()?
        } else {
            fs::symlink_metadata(path).ok()?
        };

        let name = path.file_name()?.to_string_lossy().to_string();
        let path_str = path.to_string_lossy().to_string();

        // Extract extension
        let extension = if !metadata.is_dir() {
            path.extension().map(|e| e.to_string_lossy().to_lowercase())
        } else {
            None
        };

        // Extract parent
        let parent = path
            .parent()
            .map(|p| p.to_string_lossy().to_string())
            .unwrap_or_default();

        // Extract drive letter
        let drive = path_str.chars().next().unwrap_or('C').to_ascii_uppercase();

        // Convert times
        let created = metadata.created().ok().and_then(system_time_to_datetime);

        let modified = metadata.modified().ok().and_then(system_time_to_datetime);

        let accessed = metadata.accessed().ok().and_then(system_time_to_datetime);

        // Check file attributes (Windows-specific)
        let (is_hidden, is_system) = get_file_attributes(path);

        Some(FileEntry {
            path: path_str,
            name,
            extension,
            size: metadata.len(),
            created,
            modified,
            accessed,
            is_dir: metadata.is_dir(),
            is_hidden,
            is_system,
            content_hash: None,
            parent,
            drive,
        })
    }
}

impl Default for MetadataExtractor {
    fn default() -> Self {
        Self::new()
    }
}

/// Convert SystemTime to DateTime<Utc>
fn system_time_to_datetime(time: SystemTime) -> Option<DateTime<Utc>> {
    time.duration_since(SystemTime::UNIX_EPOCH)
        .ok()
        .and_then(|d| DateTime::from_timestamp(d.as_secs() as i64, d.subsec_nanos()))
}

/// Get Windows file attributes
#[cfg(windows)]
fn get_file_attributes(path: &Path) -> (bool, bool) {
    use std::os::windows::fs::MetadataExt;

    if let Ok(metadata) = fs::metadata(path) {
        let attrs = metadata.file_attributes();
        let is_hidden = (attrs & 0x02) != 0; // FILE_ATTRIBUTE_HIDDEN
        let is_system = (attrs & 0x04) != 0; // FILE_ATTRIBUTE_SYSTEM
        (is_hidden, is_system)
    } else {
        (false, false)
    }
}

#[cfg(not(windows))]
fn get_file_attributes(path: &Path) -> (bool, bool) {
    // On non-Windows, check if filename starts with '.'
    let is_hidden = path
        .file_name()
        .map(|n| n.to_string_lossy().starts_with('.'))
        .unwrap_or(false);
    (is_hidden, false)
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::fs::File;
    use tempfile::tempdir;

    #[test]
    fn test_extract_file_metadata() {
        let dir = tempdir().unwrap();
        let file_path = dir.path().join("test.txt");
        File::create(&file_path).unwrap();

        let extractor = MetadataExtractor::new();
        let entry = extractor.extract(&file_path).unwrap();

        assert_eq!(entry.name, "test.txt");
        assert_eq!(entry.extension, Some("txt".to_string()));
        assert!(!entry.is_dir);
    }

    #[test]
    fn test_extract_dir_metadata() {
        let dir = tempdir().unwrap();
        let sub_dir = dir.path().join("subdir");
        fs::create_dir(&sub_dir).unwrap();

        let extractor = MetadataExtractor::new();
        let entry = extractor.extract(&sub_dir).unwrap();

        assert_eq!(entry.name, "subdir");
        assert!(entry.extension.is_none());
        assert!(entry.is_dir);
    }
}
