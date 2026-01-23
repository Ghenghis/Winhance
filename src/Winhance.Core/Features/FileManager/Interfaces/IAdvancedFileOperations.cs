using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Winhance.Core.Features.FileManager.Interfaces
{
    /// <summary>
    /// Advanced file operations that Windows Explorer lacks.
    /// Provides bulk operations, smart rename, and enhanced file management.
    /// </summary>
    public interface IAdvancedFileOperations
    {
        // ====================================================================
        // BULK OPERATIONS - What Windows Should Have
        // ====================================================================

        /// <summary>
        /// Copy files with detailed progress and resume capability.
        /// </summary>
        Task<BulkOperationResult> BulkCopyAsync(IEnumerable<string> sources, string destination, 
            BulkCopyOptions options, IProgress<BulkProgress>? progress = null, CancellationToken ct = default);

        /// <summary>
        /// Move files with collision handling and undo support.
        /// </summary>
        Task<BulkOperationResult> BulkMoveAsync(IEnumerable<string> sources, string destination,
            CollisionHandling collision, IProgress<BulkProgress>? progress = null, CancellationToken ct = default);

        /// <summary>
        /// Delete files with recycle bin option and secure wipe.
        /// </summary>
        Task<BulkOperationResult> BulkDeleteAsync(IEnumerable<string> paths, DeleteOptions options,
            IProgress<BulkProgress>? progress = null, CancellationToken ct = default);

        // ====================================================================
        // SMART RENAME - Pattern-based batch renaming
        // ====================================================================

        /// <summary>
        /// Rename files using patterns (date, counter, regex, etc).
        /// </summary>
        Task<IEnumerable<SmartRenameResult>> SmartRenameAsync(IEnumerable<string> files, 
            SmartRenamePattern pattern, CancellationToken ct = default);

        /// <summary>
        /// Preview rename without applying changes.
        /// </summary>
        IEnumerable<SmartRenamePreviewItem> PreviewSmartRename(IEnumerable<string> files, SmartRenamePattern pattern);

        // ====================================================================
        // FILE ATTRIBUTES - Batch modification
        // ====================================================================

        /// <summary>
        /// Set file attributes in batch (hidden, readonly, system, etc).
        /// </summary>
        Task<int> SetAttributesAsync(IEnumerable<string> paths, FileAttributeChanges changes, 
            bool recursive = false, CancellationToken ct = default);

        /// <summary>
        /// Set file timestamps (created, modified, accessed).
        /// </summary>
        Task<int> SetTimestampsAsync(IEnumerable<string> paths, TimestampChanges changes,
            bool recursive = false, CancellationToken ct = default);

        // ====================================================================
        // PATH OPERATIONS - What Windows desperately needs
        // ====================================================================

        /// <summary>
        /// Copy full path to clipboard.
        /// </summary>
        void CopyPathToClipboard(string path, PathFormat format = PathFormat.Windows);

        /// <summary>
        /// Copy file contents to clipboard (for small files).
        /// </summary>
        Task<bool> CopyFileContentsToClipboardAsync(string path, int maxSizeKb = 100);

        /// <summary>
        /// Open command prompt/PowerShell at location.
        /// </summary>
        void OpenTerminalAt(string path, TerminalType terminal = TerminalType.PowerShell);

        /// <summary>
        /// Open as administrator.
        /// </summary>
        void OpenAsAdmin(string path);

        // ====================================================================
        // SYMBOLIC LINKS - Proper support
        // ====================================================================

        /// <summary>
        /// Create symbolic link (file or directory).
        /// </summary>
        Task<bool> CreateSymbolicLinkAsync(string linkPath, string targetPath, bool isDirectory = false);

        /// <summary>
        /// Create hard link.
        /// </summary>
        Task<bool> CreateHardLinkAsync(string linkPath, string targetPath);

        /// <summary>
        /// Create junction point.
        /// </summary>
        Task<bool> CreateJunctionAsync(string linkPath, string targetPath);

        /// <summary>
        /// Get link target if path is a link.
        /// </summary>
        string? GetLinkTarget(string path);

        // ====================================================================
        // FILE SPLITTING/JOINING
        // ====================================================================

        /// <summary>
        /// Split large file into parts.
        /// </summary>
        Task<IEnumerable<string>> SplitFileAsync(string path, long partSizeBytes,
            IProgress<BulkProgress>? progress = null, CancellationToken ct = default);

        /// <summary>
        /// Join split file parts.
        /// </summary>
        Task<string> JoinFilesAsync(IEnumerable<string> parts, string outputPath,
            IProgress<BulkProgress>? progress = null, CancellationToken ct = default);

        // ====================================================================
        // TAKE OWNERSHIP - Admin operations
        // ====================================================================

        /// <summary>
        /// Take ownership of file/folder.
        /// </summary>
        Task<bool> TakeOwnershipAsync(string path, bool recursive = false);

        /// <summary>
        /// Grant full control to current user.
        /// </summary>
        Task<bool> GrantFullControlAsync(string path, bool recursive = false);
    }

    // ====================================================================
    // SUPPORTING TYPES
    // ====================================================================

    public class BulkCopyOptions
    {
        public CollisionHandling Collision { get; set; } = CollisionHandling.Ask;
        public bool PreserveTimestamps { get; set; } = true;
        public bool PreserveAttributes { get; set; } = true;
        public bool VerifyAfterCopy { get; set; } = false;
        public bool ResumeOnError { get; set; } = true;
        public int RetryCount { get; set; } = 3;
        public int BufferSizeMb { get; set; } = 4;
    }

    public enum CollisionHandling
    {
        Ask,
        Skip,
        Overwrite,
        OverwriteIfNewer,
        OverwriteIfDifferent,
        Rename,
        RenameWithNumber
    }

    public class DeleteOptions
    {
        public bool UseRecycleBin { get; set; } = true;
        public bool SecureWipe { get; set; } = false;
        public int SecureWipePasses { get; set; } = 3;
        public bool DeleteReadOnly { get; set; } = false;
        public bool DeleteSystemFiles { get; set; } = false;
    }

    public class BulkOperationResult
    {
        public int SuccessCount { get; set; }
        public int FailedCount { get; set; }
        public int SkippedCount { get; set; }
        public long TotalBytesProcessed { get; set; }
        public TimeSpan Duration { get; set; }
        public List<OperationError> Errors { get; set; } = new();
        public bool CanUndo { get; set; }
    }

    public class OperationError
    {
        public string Path { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public Exception? Exception { get; set; }
    }

    public class BulkProgress
    {
        public int CurrentFile { get; set; }
        public int TotalFiles { get; set; }
        public string CurrentFileName { get; set; } = string.Empty;
        public long BytesProcessed { get; set; }
        public long TotalBytes { get; set; }
        public double SpeedBytesPerSec { get; set; }
        public TimeSpan EstimatedRemaining { get; set; }
        public string Phase { get; set; } = string.Empty;
    }

    public class SmartRenamePattern
    {
        public string Pattern { get; set; } = string.Empty;
        public int CounterStart { get; set; } = 1;
        public int CounterPadding { get; set; } = 3;
        public string DateFormat { get; set; } = "yyyy-MM-dd";
        public bool UseExifDate { get; set; } = false;
        public string RegexFind { get; set; } = string.Empty;
        public string RegexReplace { get; set; } = string.Empty;
        public CaseTransform CaseTransform { get; set; } = CaseTransform.None;
        public bool TrimSpaces { get; set; } = true;
        public string InvalidCharReplacement { get; set; } = "_";
    }

    public enum CaseTransform
    {
        None,
        LowerCase,
        UpperCase,
        TitleCase,
        SentenceCase
    }

    public class SmartRenameResult
    {
        public string OriginalPath { get; set; } = string.Empty;
        public string NewPath { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string? Error { get; set; }
    }

    public class SmartRenamePreviewItem
    {
        public string OriginalName { get; set; } = string.Empty;
        public string NewName { get; set; } = string.Empty;
        public bool HasConflict { get; set; }
        public string? ConflictReason { get; set; }
    }

    public class FileAttributeChanges
    {
        public bool? Hidden { get; set; }
        public bool? ReadOnly { get; set; }
        public bool? System { get; set; }
        public bool? Archive { get; set; }
    }

    public class TimestampChanges
    {
        public DateTime? CreatedTime { get; set; }
        public DateTime? ModifiedTime { get; set; }
        public DateTime? AccessedTime { get; set; }
        public bool UseSourceFile { get; set; }
        public string? SourceFilePath { get; set; }
    }

    public enum PathFormat
    {
        Windows,      // C:\Folder\File.txt
        Unix,         // /c/Folder/File.txt
        Uri,          // file:///C:/Folder/File.txt
        Escaped,      // C:\\Folder\\File.txt
        FileName,     // File.txt
        Directory,    // C:\Folder
        Relative      // .\File.txt
    }

    public enum TerminalType
    {
        CommandPrompt,
        PowerShell,
        WindowsTerminal,
        GitBash,
        WSL
    }
}
