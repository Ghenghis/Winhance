using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Winhance.Core.Features.FileManager.Interfaces
{
    /// <summary>
    /// Service interface for fail-safe backup protection during file operations.
    /// Provides automatic backup creation and multi-level protection.
    /// </summary>
    public interface IBackupProtectionService
    {
        /// <summary>
        /// Gets or sets a value indicating whether backup protection is enabled.
        /// </summary>
        bool IsEnabled { get; set; }

        /// <summary>
        /// Gets or sets the backup protection level.
        /// </summary>
        BackupProtectionLevel ProtectionLevel { get; set; }

        /// <summary>
        /// Creates a backup before a file operation.
        /// </summary>
        /// <param name="operation">The operation to be performed.</param>
        /// <returns>Backup session ID.</returns>
        Task<BackupSession> CreateBackupAsync(
            FileOperationRequest operation,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates backups for multiple files in a batch operation.
        /// </summary>
        /// <param name="operations">The operations to be performed.</param>
        /// <returns>Backup session with all file backups.</returns>
        Task<BackupSession> CreateBatchBackupAsync(
            IEnumerable<FileOperationRequest> operations,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Restores files from a backup session.
        /// </summary>
        /// <param name="sessionId">The backup session ID.</param>
        /// <returns>Restore result.</returns>
        Task<BackupRestoreResult> RestoreBackupAsync(
            string sessionId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Restores a specific file from a backup session.
        /// </summary>
        /// <param name="sessionId">The backup session ID.</param>
        /// <param name="filePath">The original file path to restore.</param>
        /// <returns>Restore result.</returns>
        Task<BackupRestoreResult> RestoreFileAsync(
            string sessionId,
            string filePath,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Commits a backup session (deletes backup after successful operation).
        /// </summary>
        /// <param name="sessionId">The backup session ID.</param>
        Task CommitBackupAsync(string sessionId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Rolls back a backup session (restores all files).
        /// </summary>
        /// <param name="sessionId">The backup session ID.</param>
        /// <returns>Rollback result.</returns>
        Task<BackupRestoreResult> RollbackAsync(
            string sessionId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets all active backup sessions.
        /// </summary>
        /// <returns>Collection of active backup sessions.</returns>
        Task<IEnumerable<BackupSession>> GetActiveSessionsAsync(
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets backup history for a specific file.
        /// </summary>
        /// <param name="filePath">The file path.</param>
        /// <returns>Backup history entries.</returns>
        Task<IEnumerable<BackupHistoryEntry>> GetFileHistoryAsync(
            string filePath,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Cleans up old backups based on retention policy.
        /// </summary>
        /// <param name="retentionDays">Number of days to retain backups.</param>
        /// <returns>Cleanup result.</returns>
        Task<BackupCleanupResult> CleanupOldBackupsAsync(
            int retentionDays = 7,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Verifies backup integrity.
        /// </summary>
        /// <param name="sessionId">The backup session ID.</param>
        /// <returns>Verification result.</returns>
        Task<BackupVerificationResult> VerifyBackupAsync(
            string sessionId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets total backup storage used.
        /// </summary>
        /// <returns>Storage information.</returns>
        Task<BackupStorageInfo> GetStorageInfoAsync(CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Backup protection levels.
    /// </summary>
    public enum BackupProtectionLevel
    {
        /// <summary>
        /// No backup protection.
        /// </summary>
        None = 0,

        /// <summary>
        /// Basic - In-memory backup for small files, no persistent storage.
        /// </summary>
        Basic = 1,

        /// <summary>
        /// Standard - Backup to local temp directory with hash verification.
        /// </summary>
        Standard = 2,

        /// <summary>
        /// Enhanced - Backup with compression and deduplication.
        /// </summary>
        Enhanced = 3,

        /// <summary>
        /// Maximum - Full backup with shadow copy integration and multiple redundancy.
        /// </summary>
        Maximum = 4,
    }

    /// <summary>
    /// File operation request for backup.
    /// </summary>
    public class FileOperationRequest
    {
        /// <summary>
        /// Gets or sets the source file path.
        /// </summary>
        public string SourcePath { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the destination path (for move/copy operations).
        /// </summary>
        public string? DestinationPath { get; set; }

        /// <summary>
        /// Gets or sets the operation type.
        /// </summary>
        public FileOperationType OperationType { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to backup directory contents recursively.
        /// </summary>
        public bool Recursive { get; set; }

        /// <summary>
        /// Gets or sets the priority of this operation.
        /// </summary>
        public OperationPriority Priority { get; set; } = OperationPriority.Normal;
    }

    /// <summary>
    /// File operation types.
    /// </summary>
    public enum FileOperationType
    {
        Move,
        Copy,
        Delete,
        Rename,
        Modify,
        Replace,
    }

    /// <summary>
    /// Operation priority levels.
    /// </summary>
    public enum OperationPriority
    {
        Low,
        Normal,
        High,
        Critical,
    }

    /// <summary>
    /// Backup session information.
    /// </summary>
    public class BackupSession
    {
        /// <summary>
        /// Gets or sets the unique session ID.
        /// </summary>
        public string SessionId { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Gets or sets when the session was created.
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets the session status.
        /// </summary>
        public BackupSessionStatus Status { get; set; } = BackupSessionStatus.Active;

        /// <summary>
        /// Gets or sets the protection level used.
        /// </summary>
        public BackupProtectionLevel ProtectionLevel { get; set; }

        /// <summary>
        /// Gets or sets the backup storage path.
        /// </summary>
        public string BackupPath { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the files in this backup.
        /// </summary>
        public IList<BackupFileEntry> Files { get; set; } = new List<BackupFileEntry>();

        /// <summary>
        /// Gets or sets the total size of backed up files.
        /// </summary>
        public long TotalSize { get; set; }

        /// <summary>
        /// Gets or sets the compressed size (if compression is used).
        /// </summary>
        public long CompressedSize { get; set; }

        /// <summary>
        /// Gets or sets a description of the operation.
        /// </summary>
        public string Description { get; set; } = string.Empty;
    }

    /// <summary>
    /// Backup session status.
    /// </summary>
    public enum BackupSessionStatus
    {
        Active,
        Committed,
        RolledBack,
        Failed,
        Expired,
    }

    /// <summary>
    /// Backup file entry.
    /// </summary>
    public class BackupFileEntry
    {
        /// <summary>
        /// Gets or sets the original file path.
        /// </summary>
        public string OriginalPath { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the backup file path.
        /// </summary>
        public string BackupPath { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the file size in bytes.
        /// </summary>
        public long Size { get; set; }

        /// <summary>
        /// Gets or sets the file hash for verification.
        /// </summary>
        public string Hash { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the original file's last modified time.
        /// </summary>
        public DateTime LastModified { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the file is a directory.
        /// </summary>
        public bool IsDirectory { get; set; }

        /// <summary>
        /// Gets or sets the original file attributes.
        /// </summary>
        public System.IO.FileAttributes Attributes { get; set; }
    }

    /// <summary>
    /// Backup restore result.
    /// </summary>
    public class BackupRestoreResult
    {
        /// <summary>
        /// Gets or sets a value indicating whether the restore was successful.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets the number of files restored.
        /// </summary>
        public int FilesRestored { get; set; }

        /// <summary>
        /// Gets or sets the number of files that failed to restore.
        /// </summary>
        public int FilesFailed { get; set; }

        /// <summary>
        /// Gets or sets the total bytes restored.
        /// </summary>
        public long BytesRestored { get; set; }

        /// <summary>
        /// Gets or sets error messages.
        /// </summary>
        public IList<string> Errors { get; set; } = new List<string>();

        /// <summary>
        /// Gets or sets the restore duration.
        /// </summary>
        public TimeSpan Duration { get; set; }
    }

    /// <summary>
    /// Backup history entry.
    /// </summary>
    public class BackupHistoryEntry
    {
        /// <summary>
        /// Gets or sets the session ID.
        /// </summary>
        public string SessionId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the backup timestamp.
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Gets or sets the file path.
        /// </summary>
        public string FilePath { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the file size.
        /// </summary>
        public long Size { get; set; }

        /// <summary>
        /// Gets or sets the operation that triggered the backup.
        /// </summary>
        public FileOperationType OperationType { get; set; }

        /// <summary>
        /// Gets or sets whether the backup is still available.
        /// </summary>
        public bool IsAvailable { get; set; }
    }

    /// <summary>
    /// Backup cleanup result.
    /// </summary>
    public class BackupCleanupResult
    {
        /// <summary>
        /// Gets or sets the number of sessions cleaned up.
        /// </summary>
        public int SessionsCleaned { get; set; }

        /// <summary>
        /// Gets or sets the number of files deleted.
        /// </summary>
        public int FilesDeleted { get; set; }

        /// <summary>
        /// Gets or sets the bytes freed.
        /// </summary>
        public long BytesFreed { get; set; }

        /// <summary>
        /// Gets or sets error messages.
        /// </summary>
        public IList<string> Errors { get; set; } = new List<string>();
    }

    /// <summary>
    /// Backup verification result.
    /// </summary>
    public class BackupVerificationResult
    {
        /// <summary>
        /// Gets or sets a value indicating whether all files are valid.
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// Gets or sets the number of valid files.
        /// </summary>
        public int ValidFiles { get; set; }

        /// <summary>
        /// Gets or sets the number of corrupted files.
        /// </summary>
        public int CorruptedFiles { get; set; }

        /// <summary>
        /// Gets or sets the number of missing files.
        /// </summary>
        public int MissingFiles { get; set; }

        /// <summary>
        /// Gets or sets the corrupted file paths.
        /// </summary>
        public IList<string> CorruptedPaths { get; set; } = new List<string>();
    }

    /// <summary>
    /// Backup storage information.
    /// </summary>
    public class BackupStorageInfo
    {
        /// <summary>
        /// Gets or sets the backup storage path.
        /// </summary>
        public string StoragePath { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the total storage used.
        /// </summary>
        public long TotalUsed { get; set; }

        /// <summary>
        /// Gets or sets the number of active sessions.
        /// </summary>
        public int ActiveSessions { get; set; }

        /// <summary>
        /// Gets or sets the number of total files backed up.
        /// </summary>
        public int TotalFiles { get; set; }

        /// <summary>
        /// Gets or sets the oldest backup date.
        /// </summary>
        public DateTime? OldestBackup { get; set; }

        /// <summary>
        /// Gets or sets the newest backup date.
        /// </summary>
        public DateTime? NewestBackup { get; set; }
    }
}
