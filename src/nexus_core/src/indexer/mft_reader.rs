//! MFT (Master File Table) Reader for ultra-fast NTFS scanning
//!
//! Reads the NTFS Master File Table directly for sub-second file listing.
//! This is the same technique used by "Everything" search.

use crate::{FileEntry, NexusError, Result};
use std::collections::HashMap;
use tracing::{debug, info, warn};

#[cfg(windows)]
use windows::{
    core::PCWSTR,
    Win32::{
        Foundation::{CloseHandle, HANDLE, INVALID_HANDLE_VALUE},
        Storage::FileSystem::{
            CreateFileW, FILE_FLAG_BACKUP_SEMANTICS, FILE_SHARE_DELETE, FILE_SHARE_READ,
            FILE_SHARE_WRITE, OPEN_EXISTING,
        },
        System::Ioctl::{FSCTL_ENUM_USN_DATA, FSCTL_GET_NTFS_VOLUME_DATA, NTFS_VOLUME_DATA_BUFFER},
    },
};

/// MFT Reader for NTFS volumes
pub struct MftReader {
    #[allow(dead_code)]
    drive: char,
    #[cfg(windows)]
    volume_handle: Option<HANDLE>,
}

impl MftReader {
    /// Create a new MFT reader for a drive
    pub fn new(drive: char) -> Self {
        Self {
            drive,
            #[cfg(windows)]
            volume_handle: None,
        }
    }

    /// Scan an NTFS volume using MFT
    ///
    /// This is the fastest method to enumerate all files on an NTFS volume.
    /// Returns all file entries in sub-second time for typical drives.
    #[cfg(windows)]
    pub fn scan_volume(drive: char) -> Result<Vec<FileEntry>> {
        info!("Scanning drive {} using MFT reader", drive);

        let volume_path: Vec<u16> = format!("\\\\.\\{}:", drive)
            .encode_utf16()
            .chain(std::iter::once(0))
            .collect();

        // Open volume for reading
        let volume_handle = unsafe {
            CreateFileW(
                PCWSTR(volume_path.as_ptr()),
                0x80000000, // GENERIC_READ
                FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE,
                None,
                OPEN_EXISTING,
                FILE_FLAG_BACKUP_SEMANTICS,
                None,
            )
        };

        let handle = match volume_handle {
            Ok(h) if h != INVALID_HANDLE_VALUE => h,
            _ => {
                return Err(NexusError::PermissionDenied(format!(
                    "Cannot open volume {}:\\ - requires Administrator privileges",
                    drive
                )));
            }
        };

        // Get NTFS volume data
        let mut volume_data = NTFS_VOLUME_DATA_BUFFER::default();
        let mut bytes_returned: u32 = 0;

        let result = unsafe {
            windows::Win32::System::IO::DeviceIoControl(
                handle,
                FSCTL_GET_NTFS_VOLUME_DATA,
                None,
                0,
                Some(&mut volume_data as *mut _ as *mut _),
                std::mem::size_of::<NTFS_VOLUME_DATA_BUFFER>() as u32,
                Some(&mut bytes_returned),
                None,
            )
        };

        if result.is_err() {
            let _ = unsafe { CloseHandle(handle) };
            return Err(NexusError::Windows("Failed to get NTFS volume data".into()));
        }

        info!(
            "NTFS Volume {}: Total clusters: {}, Bytes per cluster: {}",
            drive, volume_data.TotalClusters, volume_data.BytesPerCluster
        );

        // Enumerate USN data to get all files
        let entries = Self::enumerate_usn_data(handle, drive, &volume_data)?;

        let _ = unsafe { CloseHandle(handle) };

        Ok(entries)
    }

    #[cfg(windows)]
    fn enumerate_usn_data(
        handle: HANDLE,
        drive: char,
        _volume_data: &NTFS_VOLUME_DATA_BUFFER,
    ) -> Result<Vec<FileEntry>> {
        use std::mem::size_of;

        let mut entries = Vec::new();
        let mut file_refs: HashMap<u64, (String, u64)> = HashMap::new(); // file_ref -> (name, parent_ref)

        // MFT enumeration input buffer
        #[repr(C)]
        struct MftEnumData {
            start_file_reference: u64,
            low_usn: i64,
            high_usn: i64,
        }

        let mut enum_data = MftEnumData {
            start_file_reference: 0,
            low_usn: 0,
            high_usn: i64::MAX,
        };

        let buffer_size = 64 * 1024; // 64KB buffer
        let mut buffer = vec![0u8; buffer_size];
        let mut bytes_returned: u32 = 0;

        loop {
            let result = unsafe {
                windows::Win32::System::IO::DeviceIoControl(
                    handle,
                    FSCTL_ENUM_USN_DATA,
                    Some(&enum_data as *const _ as *const _),
                    size_of::<MftEnumData>() as u32,
                    Some(buffer.as_mut_ptr() as *mut _),
                    buffer_size as u32,
                    Some(&mut bytes_returned),
                    None,
                )
            };

            if result.is_err() || bytes_returned == 0 {
                break;
            }

            // Parse USN records from buffer
            let next_usn = unsafe { *(buffer.as_ptr() as *const u64) };
            let mut offset = 8usize; // Skip the next USN value

            while offset < bytes_returned as usize {
                // USN_RECORD_V2 structure
                #[repr(C)]
                #[allow(non_snake_case)]
                struct UsnRecord {
                    RecordLength: u32,
                    MajorVersion: u16,
                    MinorVersion: u16,
                    FileReferenceNumber: u64,
                    ParentFileReferenceNumber: u64,
                    Usn: i64,
                    TimeStamp: i64,
                    Reason: u32,
                    SourceInfo: u32,
                    SecurityId: u32,
                    FileAttributes: u32,
                    FileNameLength: u16,
                    FileNameOffset: u16,
                    // FileName follows (variable length)
                }

                // Validate we have enough bytes for the record header
                if offset + size_of::<UsnRecord>() > bytes_returned as usize {
                    break;
                }

                let record = unsafe { &*(buffer.as_ptr().add(offset) as *const UsnRecord) };

                // Validate record length is reasonable and non-zero
                if record.RecordLength == 0 || record.RecordLength as usize > buffer_size {
                    break;
                }

                // Validate the record doesn't extend beyond buffer
                if offset + record.RecordLength as usize > bytes_returned as usize {
                    warn!("USN record extends beyond buffer at offset {}", offset);
                    break;
                }

                // Extract filename
                let name_offset = offset + record.FileNameOffset as usize;
                let name_len = record.FileNameLength as usize / 2; // UTF-16

                // Validate filename offset and length are within the record bounds
                let name_end = name_offset + record.FileNameLength as usize;
                let record_end = offset + record.RecordLength as usize;

                if name_offset < offset + size_of::<UsnRecord>()
                    || name_end > record_end
                    || name_end > bytes_returned as usize
                {
                    debug!("Invalid filename bounds at offset {}: name_offset={}, name_len={}, record_len={}",
                        offset, name_offset, name_len, record.RecordLength);
                    offset += record.RecordLength as usize;
                    continue;
                }

                if name_len > 0
                    && name_offset + record.FileNameLength as usize <= bytes_returned as usize
                {
                    let name_ptr = unsafe { buffer.as_ptr().add(name_offset) as *const u16 };
                    let name_slice = unsafe { std::slice::from_raw_parts(name_ptr, name_len) };
                    let name = String::from_utf16_lossy(name_slice);

                    // Store for path reconstruction
                    file_refs.insert(
                        record.FileReferenceNumber & 0x0000FFFFFFFFFFFF, // Mask sequence number
                        (
                            name.clone(),
                            record.ParentFileReferenceNumber & 0x0000FFFFFFFFFFFF,
                        ),
                    );

                    // Create file entry
                    let is_dir = (record.FileAttributes & 0x10) != 0; // FILE_ATTRIBUTE_DIRECTORY
                    let is_hidden = (record.FileAttributes & 0x02) != 0; // FILE_ATTRIBUTE_HIDDEN
                    let is_system = (record.FileAttributes & 0x04) != 0; // FILE_ATTRIBUTE_SYSTEM

                    let extension = if !is_dir {
                        name.rsplit('.').next().map(|s| s.to_lowercase())
                    } else {
                        None
                    };

                    // We'll reconstruct full paths after collecting all records
                    entries.push((
                        record.FileReferenceNumber & 0x0000FFFFFFFFFFFF,
                        record.ParentFileReferenceNumber & 0x0000FFFFFFFFFFFF,
                        FileEntry {
                            path: String::new(), // Will be filled later
                            name: name.clone(),
                            extension,
                            size: 0, // MFT enum doesn't give size directly
                            created: None,
                            modified: None,
                            accessed: None,
                            is_dir,
                            is_hidden,
                            is_system,
                            content_hash: None,
                            parent: String::new(),
                            drive,
                        },
                    ));
                }

                offset += record.RecordLength as usize;
                if record.RecordLength == 0 {
                    break;
                }
            }

            // Update for next iteration
            enum_data.start_file_reference = next_usn;
        }

        info!("MFT enumeration found {} raw records", entries.len());

        // Reconstruct full paths
        let root_ref = 5u64; // MFT root directory reference
        file_refs.insert(root_ref, (format!("{}:", drive), 0));

        fn build_path(
            file_ref: u64,
            file_refs: &HashMap<u64, (String, u64)>,
            cache: &mut HashMap<u64, String>,
        ) -> String {
            if let Some(cached) = cache.get(&file_ref) {
                return cached.clone();
            }

            if let Some((name, parent_ref)) = file_refs.get(&file_ref) {
                let path = if *parent_ref == 0 || *parent_ref == file_ref {
                    name.clone()
                } else {
                    let parent_path = build_path(*parent_ref, file_refs, cache);
                    format!("{}\\{}", parent_path, name)
                };
                cache.insert(file_ref, path.clone());
                path
            } else {
                String::new()
            }
        }

        let mut path_cache: HashMap<u64, String> = HashMap::new();

        let final_entries: Vec<FileEntry> = entries
            .into_iter()
            .filter_map(|(file_ref, parent_ref, mut entry)| {
                let path = build_path(file_ref, &file_refs, &mut path_cache);
                if path.is_empty() {
                    return None;
                }

                entry.path = path.clone();
                entry.parent = build_path(parent_ref, &file_refs, &mut path_cache);

                Some(entry)
            })
            .collect();

        info!("Reconstructed {} file paths", final_entries.len());
        Ok(final_entries)
    }

    #[cfg(not(windows))]
    pub fn scan_volume(drive: char) -> Result<Vec<FileEntry>> {
        Err(NexusError::Windows(
            "MFT reader is only available on Windows".into(),
        ))
    }
}

impl Drop for MftReader {
    fn drop(&mut self) {
        #[cfg(windows)]
        if let Some(handle) = self.volume_handle.take() {
            if handle != INVALID_HANDLE_VALUE {
                let _ = unsafe { CloseHandle(handle) };
            }
        }
    }
}
