//! Content hashing for file deduplication
//!
//! Provides fast content hashing using xxHash for quick comparison
//! and SHA-256 for verification.

use sha2::{Digest, Sha256};
use std::fs::File;
use std::io::{BufReader, Read};
use std::path::Path;
use xxhash_rust::xxh3::xxh3_64;

/// Content hasher for file deduplication
pub struct ContentHasher {
    /// Maximum file size to hash (in bytes)
    max_size: u64,
    /// Buffer size for reading files
    buffer_size: usize,
}

impl ContentHasher {
    /// Create a new content hasher
    pub fn new(max_size: u64) -> Self {
        Self {
            max_size,
            buffer_size: 64 * 1024, // 64KB buffer
        }
    }

    /// Compute a fast hash (xxHash3) for quick comparison
    pub fn quick_hash(&self, path: &Path) -> Option<u64> {
        let file = File::open(path).ok()?;
        let metadata = file.metadata().ok()?;

        if metadata.len() > self.max_size {
            return None;
        }

        let mut reader = BufReader::with_capacity(self.buffer_size, file);
        let mut buffer = Vec::new();
        reader.read_to_end(&mut buffer).ok()?;

        Some(xxh3_64(&buffer))
    }

    /// Compute a SHA-256 hash for verification
    pub fn sha256_hash(&self, path: &Path) -> Option<String> {
        let file = File::open(path).ok()?;
        let metadata = file.metadata().ok()?;

        if metadata.len() > self.max_size {
            return None;
        }

        let mut reader = BufReader::with_capacity(self.buffer_size, file);
        let mut hasher = Sha256::new();
        let mut buffer = vec![0u8; self.buffer_size];

        loop {
            let bytes_read = reader.read(&mut buffer).ok()?;
            if bytes_read == 0 {
                break;
            }
            hasher.update(&buffer[..bytes_read]);
        }

        let result = hasher.finalize();
        Some(format!("{:x}", result))
    }

    /// Compute both quick and secure hash
    pub fn full_hash(&self, path: &Path) -> Option<(u64, String)> {
        let quick = self.quick_hash(path)?;
        let secure = self.sha256_hash(path)?;
        Some((quick, secure))
    }

    /// Compare two files for content equality
    pub fn files_equal(&self, path1: &Path, path2: &Path) -> Option<bool> {
        // First compare sizes
        let meta1 = std::fs::metadata(path1).ok()?;
        let meta2 = std::fs::metadata(path2).ok()?;

        if meta1.len() != meta2.len() {
            return Some(false);
        }

        // Then compare quick hashes
        let hash1 = self.quick_hash(path1)?;
        let hash2 = self.quick_hash(path2)?;

        if hash1 != hash2 {
            return Some(false);
        }

        // Finally, verify with SHA-256
        let sha1 = self.sha256_hash(path1)?;
        let sha2 = self.sha256_hash(path2)?;

        Some(sha1 == sha2)
    }
}

impl Default for ContentHasher {
    fn default() -> Self {
        Self::new(100 * 1024 * 1024) // 100MB default
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use tempfile::tempdir;

    #[test]
    fn test_quick_hash() {
        let dir = tempdir().unwrap();
        let file_path = dir.path().join("test.txt");
        std::fs::write(&file_path, b"Hello, World!").unwrap();

        let hasher = ContentHasher::default();
        let hash = hasher.quick_hash(&file_path);

        assert!(hash.is_some());
    }

    #[test]
    fn test_sha256_hash() {
        let dir = tempdir().unwrap();
        let file_path = dir.path().join("test.txt");
        std::fs::write(&file_path, b"Hello, World!").unwrap();

        let hasher = ContentHasher::default();
        let hash = hasher.sha256_hash(&file_path);

        assert!(hash.is_some());
        assert_eq!(hash.unwrap().len(), 64); // SHA-256 = 64 hex chars
    }

    #[test]
    fn test_files_equal() {
        let dir = tempdir().unwrap();

        let file1 = dir.path().join("file1.txt");
        let file2 = dir.path().join("file2.txt");
        let file3 = dir.path().join("file3.txt");

        std::fs::write(&file1, b"Same content").unwrap();
        std::fs::write(&file2, b"Same content").unwrap();
        std::fs::write(&file3, b"Different content").unwrap();

        let hasher = ContentHasher::default();

        assert_eq!(hasher.files_equal(&file1, &file2), Some(true));
        assert_eq!(hasher.files_equal(&file1, &file3), Some(false));
    }
}
