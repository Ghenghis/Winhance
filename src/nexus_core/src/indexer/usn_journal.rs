//! USN (Update Sequence Number) Journal Reader
//!
//! Monitors real-time file system changes on NTFS volumes using the USN Journal.
//! This provides instant notification of file creates, deletes, renames, and modifications.

use crate::Result;
use std::sync::mpsc::{channel, Receiver, Sender};
use std::thread;
use tracing::{info, warn};

/// Types of file system changes
#[derive(Debug, Clone, PartialEq, Eq)]
pub enum ChangeType {
    Created,
    Deleted,
    Modified,
    Renamed { old_name: String },
    SecurityChange,
    Unknown,
}

/// A file system change event from the USN Journal
#[derive(Debug, Clone)]
pub struct UsnChange {
    pub path: String,
    pub change_type: ChangeType,
    pub is_directory: bool,
    pub timestamp: chrono::DateTime<chrono::Utc>,
}

/// USN Journal monitor for real-time file changes
pub struct UsnJournal {
    drive: char,
    running: std::sync::Arc<std::sync::atomic::AtomicBool>,
}

impl UsnJournal {
    /// Create a new USN Journal monitor for a drive
    pub fn new(drive: char) -> Self {
        Self {
            drive,
            running: std::sync::Arc::new(std::sync::atomic::AtomicBool::new(false)),
        }
    }

    /// Start monitoring and return a receiver for change events
    #[cfg(windows)]
    pub fn start_monitoring(&self) -> Result<Receiver<UsnChange>> {
        use std::sync::atomic::Ordering;
        use windows::{
            core::PCWSTR,
            Win32::{
                Foundation::{CloseHandle, INVALID_HANDLE_VALUE},
                Storage::FileSystem::{
                    CreateFileW, FILE_FLAG_BACKUP_SEMANTICS, FILE_SHARE_DELETE, FILE_SHARE_READ,
                    FILE_SHARE_WRITE, OPEN_EXISTING,
                },
                System::Ioctl::{FSCTL_QUERY_USN_JOURNAL, FSCTL_READ_USN_JOURNAL},
            },
        };

        let (tx, rx): (Sender<UsnChange>, Receiver<UsnChange>) = channel();
        let drive = self.drive;

        self.running.store(true, Ordering::SeqCst);
        let running = self.running.clone();

        thread::spawn(move || {
            let volume_path: Vec<u16> = format!("\\\\.\\{}:", drive)
                .encode_utf16()
                .chain(std::iter::once(0))
                .collect();

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
                    warn!("Failed to open volume {}:\\ for USN monitoring", drive);
                    return;
                }
            };

            info!("USN Journal monitoring started for drive {}", drive);

            // Query USN Journal
            #[repr(C)]
            #[allow(non_snake_case)]
            struct UsnJournalData {
                UsnJournalID: u64,
                FirstUsn: i64,
                NextUsn: i64,
                LowestValidUsn: i64,
                MaxUsn: i64,
                MaximumSize: u64,
                AllocationDelta: u64,
            }

            let mut journal_data = UsnJournalData {
                UsnJournalID: 0,
                FirstUsn: 0,
                NextUsn: 0,
                LowestValidUsn: 0,
                MaxUsn: 0,
                MaximumSize: 0,
                AllocationDelta: 0,
            };
            let mut bytes_returned: u32 = 0;

            let result = unsafe {
                windows::Win32::System::IO::DeviceIoControl(
                    handle,
                    FSCTL_QUERY_USN_JOURNAL,
                    None,
                    0,
                    Some(&mut journal_data as *mut _ as *mut _),
                    std::mem::size_of::<UsnJournalData>() as u32,
                    Some(&mut bytes_returned),
                    None,
                )
            };

            if result.is_err() {
                warn!("Failed to query USN Journal for drive {}", drive);
                let _ = unsafe { CloseHandle(handle) };
                return;
            }

            // Read USN records
            #[repr(C)]
            #[allow(non_snake_case)]
            struct ReadUsnJournalData {
                StartUsn: i64,
                ReasonMask: u32,
                ReturnOnlyOnClose: u32,
                Timeout: u64,
                BytesToWaitFor: u64,
                UsnJournalID: u64,
            }

            let mut read_data = ReadUsnJournalData {
                StartUsn: journal_data.NextUsn,
                ReasonMask: 0xFFFFFFFF, // All reasons
                ReturnOnlyOnClose: 0,
                Timeout: 0,
                BytesToWaitFor: 0,
                UsnJournalID: journal_data.UsnJournalID,
            };

            let buffer_size = 64 * 1024;
            let mut buffer = vec![0u8; buffer_size];

            while running.load(Ordering::SeqCst) {
                let result = unsafe {
                    windows::Win32::System::IO::DeviceIoControl(
                        handle,
                        FSCTL_READ_USN_JOURNAL,
                        Some(&read_data as *const _ as *const _),
                        std::mem::size_of::<ReadUsnJournalData>() as u32,
                        Some(buffer.as_mut_ptr() as *mut _),
                        buffer_size as u32,
                        Some(&mut bytes_returned),
                        None,
                    )
                };

                if result.is_err() || bytes_returned <= 8 {
                    thread::sleep(std::time::Duration::from_millis(100));
                    continue;
                }

                // Parse USN records
                let next_usn = unsafe { *(buffer.as_ptr() as *const i64) };
                let mut offset = 8usize;

                while offset < bytes_returned as usize {
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
                    }

                    if offset + std::mem::size_of::<UsnRecord>() > bytes_returned as usize {
                        break;
                    }

                    let record = unsafe { &*(buffer.as_ptr().add(offset) as *const UsnRecord) };

                    if record.RecordLength == 0 {
                        break;
                    }

                    // Extract filename
                    let name_offset = offset + record.FileNameOffset as usize;
                    let name_len = record.FileNameLength as usize / 2;

                    if name_offset + record.FileNameLength as usize <= bytes_returned as usize {
                        let name_ptr = unsafe { buffer.as_ptr().add(name_offset) as *const u16 };
                        let name_slice = unsafe { std::slice::from_raw_parts(name_ptr, name_len) };
                        let name = String::from_utf16_lossy(name_slice);

                        // Determine change type
                        let change_type = reason_to_change_type(record.Reason);
                        let is_directory = (record.FileAttributes & 0x10) != 0;

                        let change = UsnChange {
                            path: format!("{}:\\...\\{}", drive, name), // Simplified path
                            change_type,
                            is_directory,
                            timestamp: chrono::Utc::now(),
                        };

                        if tx.send(change).is_err() {
                            break;
                        }
                    }

                    offset += record.RecordLength as usize;
                }

                read_data.StartUsn = next_usn;
            }

            let _ = unsafe { CloseHandle(handle) };
            info!("USN Journal monitoring stopped for drive {}", drive);
        });

        Ok(rx)
    }

    #[cfg(not(windows))]
    pub fn start_monitoring(&self) -> Result<Receiver<UsnChange>> {
        Err(NexusError::Windows(
            "USN Journal is only available on Windows".into(),
        ))
    }

    /// Stop monitoring
    pub fn stop(&self) {
        self.running
            .store(false, std::sync::atomic::Ordering::SeqCst);
    }
}

/// Convert USN reason flags to ChangeType
fn reason_to_change_type(reason: u32) -> ChangeType {
    const USN_REASON_FILE_CREATE: u32 = 0x00000100;
    const USN_REASON_FILE_DELETE: u32 = 0x00000200;
    const USN_REASON_DATA_OVERWRITE: u32 = 0x00000001;
    const USN_REASON_DATA_EXTEND: u32 = 0x00000002;
    const USN_REASON_DATA_TRUNCATION: u32 = 0x00000004;
    const USN_REASON_RENAME_OLD_NAME: u32 = 0x00001000;
    const USN_REASON_RENAME_NEW_NAME: u32 = 0x00002000;
    const USN_REASON_SECURITY_CHANGE: u32 = 0x00000800;

    if reason & USN_REASON_FILE_CREATE != 0 {
        ChangeType::Created
    } else if reason & USN_REASON_FILE_DELETE != 0 {
        ChangeType::Deleted
    } else if reason
        & (USN_REASON_DATA_OVERWRITE | USN_REASON_DATA_EXTEND | USN_REASON_DATA_TRUNCATION)
        != 0
    {
        ChangeType::Modified
    } else if reason & (USN_REASON_RENAME_OLD_NAME | USN_REASON_RENAME_NEW_NAME) != 0 {
        ChangeType::Renamed {
            old_name: String::new(),
        }
    } else if reason & USN_REASON_SECURITY_CHANGE != 0 {
        ChangeType::SecurityChange
    } else {
        ChangeType::Unknown
    }
}
