using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.FileManager.Models;

namespace Winhance.Core.Features.FileManager.Interfaces
{
    /// <summary>
    /// Service interface for file manager operations.
    /// </summary>
    public interface IFileManagerService
    {
        /// <summary>
        /// Gets the contents of a directory.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task<IEnumerable<FileSystemEntry>> GetDirectoryContentsAsync(string path, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets drive information for all available drives.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task<IEnumerable<FileManagerDriveInfo>> GetDrivesAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Copies files to a destination.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task<FileOperationResult> CopyFilesAsync(IEnumerable<string> sourcePaths, string destinationPath, CancellationToken cancellationToken = default);

        /// <summary>
        /// Moves files to a destination.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task<FileOperationResult> MoveFilesAsync(IEnumerable<string> sourcePaths, string destinationPath, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes files or directories.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task<FileOperationResult> DeleteFilesAsync(IEnumerable<string> paths, bool permanent = false, CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates a new directory.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task<FileOperationResult> CreateDirectoryAsync(string path, CancellationToken cancellationToken = default);

        /// <summary>
        /// Renames a file or directory.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task<FileOperationResult> RenameAsync(string path, string newName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets file or directory properties.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task<FileProperties> GetPropertiesAsync(string path, CancellationToken cancellationToken = default);

        /// <summary>
        /// Searches for files matching a pattern.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task<IEnumerable<FileSystemEntry>> SearchAsync(string searchPath, string pattern, FileSearchOptions options, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Represents detailed file properties.
    /// </summary>
    public class FileProperties
    {
        public string Name { get; set; } = string.Empty;

        public string FullPath { get; set; } = string.Empty;

        public string Type { get; set; } = string.Empty;

        public long Size { get; set; }

        public DateTime DateModified { get; set; }

        public DateTime DateCreated { get; set; }

        public DateTime DateAccessed { get; set; }

        public FileAttributes Attributes { get; set; }

        public bool IsReadOnly { get; set; }

        public bool IsHidden { get; set; }

        public bool IsSystem { get; set; }

        public string Hash { get; set; } = string.Empty;
    }

    /// <summary>
    /// Search options for file search operations.
    /// </summary>
    public class FileSearchOptions
    {
        public bool Recursive { get; set; } = true;

        public bool IncludeHidden { get; set; } = false;

        public bool IncludeSystem { get; set; } = false;

        public bool CaseSensitive { get; set; } = false;

        public bool UseRegex { get; set; } = false;

        public long? MinSize { get; set; }

        public long? MaxSize { get; set; }

        public DateTime? ModifiedAfter { get; set; }

        public DateTime? ModifiedBefore { get; set; }

        public IEnumerable<string>? Extensions { get; set; }
    }

    /// <summary>
    /// Result of a file operation (used by IFileManagerService).
    /// </summary>
    public class FileOperationResult
    {
        public bool Success { get; set; }

        public string Message { get; set; } = string.Empty;

        public int ItemsProcessed { get; set; }

        public int ItemsFailed { get; set; }

        public IEnumerable<string> Errors { get; set; } = Array.Empty<string>();

        public string TransactionId { get; set; } = string.Empty;
    }

    /// <summary>
    /// Drive information for file manager.
    /// </summary>
    public class FileManagerDriveInfo
    {
        public string Name { get; set; } = string.Empty;

        public string Label { get; set; } = string.Empty;

        public string DriveType { get; set; } = string.Empty;

        public string FileSystem { get; set; } = string.Empty;

        public long TotalSize { get; set; }

        public long FreeSpace { get; set; }

        public long UsedSpace => TotalSize - FreeSpace;

        public double UsedPercentage => TotalSize > 0 ? (double)UsedSpace / TotalSize * 100 : 0;

        public bool IsReady { get; set; }
    }
}
