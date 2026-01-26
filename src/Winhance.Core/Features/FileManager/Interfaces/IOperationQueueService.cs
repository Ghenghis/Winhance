using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Winhance.Core.Features.FileManager.Interfaces
{
    /// <summary>
    /// Service for managing background file operations with progress tracking and conflict resolution.
    /// </summary>
    public interface IOperationQueueService
    {
        /// <summary>
        /// Observable collection of queued operations.
        /// </summary>
        ObservableCollection<FileOperation> Operations { get; }

        /// <summary>
        /// Currently running operation.
        /// </summary>
        FileOperation? CurrentOperation { get; }

        /// <summary>
        /// Event for operation progress updates.
        /// </summary>
        event EventHandler<OperationProgressEventArgs>? ProgressChanged;

        /// <summary>
        /// Event when operation completes.
        /// </summary>
        event EventHandler<OperationCompletedEventArgs>? OperationCompleted;

        /// <summary>
        /// Queue a copy operation.
        /// </summary>
        /// <param name="sources">Source files/directories.</param>
        /// <param name="destination">Destination path.</param>
        /// <returns>The created operation.</returns>
        FileOperation QueueCopy(IEnumerable<string> sources, string destination);

        /// <summary>
        /// Queue a move operation.
        /// </summary>
        /// <param name="sources">Source files/directories.</param>
        /// <param name="destination">Destination path.</param>
        /// <returns>The created operation.</returns>
        FileOperation QueueMove(IEnumerable<string> sources, string destination);

        /// <summary>
        /// Queue a delete operation.
        /// </summary>
        /// <param name="paths">Paths to delete.</param>
        /// <param name="permanent">Whether to permanently delete (skip recycle bin).</param>
        /// <returns>The created operation.</returns>
        FileOperation QueueDelete(IEnumerable<string> paths, bool permanent = false);

        /// <summary>
        /// Pause an operation.
        /// </summary>
        /// <param name="operation">Operation to pause.</param>
        void Pause(FileOperation operation);

        /// <summary>
        /// Resume a paused operation.
        /// </summary>
        /// <param name="operation">Operation to resume.</param>
        void Resume(FileOperation operation);

        /// <summary>
        /// Cancel an operation.
        /// </summary>
        /// <param name="operation">Operation to cancel.</param>
        void Cancel(FileOperation operation);

        /// <summary>
        /// Change operation priority (move up/down in queue).
        /// </summary>
        /// <param name="operation">Operation to move.</param>
        /// <param name="newPosition">New position in queue.</param>
        void ChangePriority(FileOperation operation, int newPosition);

        /// <summary>
        /// Retry a failed operation.
        /// </summary>
        /// <param name="operation">Operation to retry.</param>
        void Retry(FileOperation operation);

        /// <summary>
        /// Handle a conflict.
        /// </summary>
        /// <param name="operation">Operation with conflict.</param>
        /// <param name="resolution">Conflict resolution choice.</param>
        void ResolveConflict(FileOperation operation, ConflictResolution resolution);

        /// <summary>
        /// Get operation history.
        /// </summary>
        /// <param name="count">Maximum number of operations to return.</param>
        /// <returns>Historical operations.</returns>
        IEnumerable<FileOperation> GetHistory(int count = 50);

        /// <summary>
        /// Clear completed operations from history.
        /// </summary>
        void ClearHistory();

        /// <summary>
        /// Get all operations with a specific status.
        /// </summary>
        /// <param name="status">Status to filter by.</param>
        /// <returns>Operations with the specified status.</returns>
        IEnumerable<FileOperation> GetOperationsByStatus(OperationStatus status);

        /// <summary>
        /// Get estimated time remaining for an operation.
        /// </summary>
        /// <param name="operation">Operation to check.</param>
        /// <returns>Estimated time remaining, or null if unknown.</returns>
        TimeSpan? GetEstimatedTimeRemaining(FileOperation operation);

        /// <summary>
        /// Get detailed operation statistics.
        /// </summary>
        /// <returns>Statistics about all operations.</returns>
        OperationStatistics GetStatistics();

        /// <summary>
        /// Pause all operations.
        /// </summary>
        void PauseAll();

        /// <summary>
        /// Resume all paused operations.
        /// </summary>
        void ResumeAll();

        /// <summary>
        /// Cancel all operations.
        /// </summary>
        void CancelAll();

        /// <summary>
        /// Set global operation settings.
        /// </summary>
        /// <param name="settings">Settings to apply.</param>
        void SetSettings(OperationSettings settings);

        /// <summary>
        /// Get current operation settings.
        /// </summary>
        /// <returns>Current settings.</returns>
        OperationSettings GetSettings();
    }

    /// <summary>
    /// Represents a file operation.
    /// </summary>
    public class FileOperation : ObservableObject
    {
        private string _id = Guid.NewGuid().ToString();
        private OperationType _type;
        private OperationStatus _status;
        private IEnumerable<string> _sourcePaths = Array.Empty<string>();
        private string? _destinationPath;
        private long _totalBytes;
        private long _processedBytes;
        private int _totalFiles;
        private int _processedFiles;
        private string _currentFile = string.Empty;
        private double _speedBytesPerSecond;
        private TimeSpan _estimatedRemaining;
        private DateTime _startedAt = DateTime.UtcNow;
        private DateTime? _completedAt;
        private string? _errorMessage;
        private ConflictInfo? _pendingConflict;
        private int _priority = 0;

        /// <summary>
        /// Unique identifier.
        /// </summary>
        public string Id
        {
            get => _id;
            set => SetProperty(ref _id, value);
        }

        /// <summary>
        /// Type of operation.
        /// </summary>
        public OperationType Type
        {
            get => _type;
            set => SetProperty(ref _type, value);
        }

        /// <summary>
        /// Current status.
        /// </summary>
        public OperationStatus Status
        {
            get => _status;
            set => SetProperty(ref _status, value);
        }

        /// <summary>
        /// Source paths.
        /// </summary>
        public IEnumerable<string> SourcePaths
        {
            get => _sourcePaths;
            set => SetProperty(ref _sourcePaths, value);
        }

        /// <summary>
        /// Destination path (for copy/move).
        /// </summary>
        public string? DestinationPath
        {
            get => _destinationPath;
            set => SetProperty(ref _destinationPath, value);
        }

        /// <summary>
        /// Total bytes to process.
        /// </summary>
        public long TotalBytes
        {
            get => _totalBytes;
            set => SetProperty(ref _totalBytes, value);
        }

        /// <summary>
        /// Bytes processed so far.
        /// </summary>
        public long ProcessedBytes
        {
            get => _processedBytes;
            set => SetProperty(ref _processedBytes, value);
        }

        /// <summary>
        /// Total files to process.
        /// </summary>
        public int TotalFiles
        {
            get => _totalFiles;
            set => SetProperty(ref _totalFiles, value);
        }

        /// <summary>
        /// Files processed so far.
        /// </summary>
        public int ProcessedFiles
        {
            get => _processedFiles;
            set => SetProperty(ref _processedFiles, value);
        }

        /// <summary>
        /// Currently processing file.
        /// </summary>
        public string CurrentFile
        {
            get => _currentFile;
            set => SetProperty(ref _currentFile, value);
        }

        /// <summary>
        /// Processing speed in bytes per second.
        /// </summary>
        public double SpeedBytesPerSecond
        {
            get => _speedBytesPerSecond;
            set => SetProperty(ref _speedBytesPerSecond, value);
        }

        /// <summary>
        /// Estimated time remaining.
        /// </summary>
        public TimeSpan EstimatedRemaining
        {
            get => _estimatedRemaining;
            set => SetProperty(ref _estimatedRemaining, value);
        }

        /// <summary>
        /// When the operation started.
        /// </summary>
        public DateTime StartedAt
        {
            get => _startedAt;
            set => SetProperty(ref _startedAt, value);
        }

        /// <summary>
        /// When the operation completed.
        /// </summary>
        public DateTime? CompletedAt
        {
            get => _completedAt;
            set => SetProperty(ref _completedAt, value);
        }

        /// <summary>
        /// Error message if failed.
        /// </summary>
        public string? ErrorMessage
        {
            get => _errorMessage;
            set => SetProperty(ref _errorMessage, value);
        }

        /// <summary>
        /// Pending conflict information.
        /// </summary>
        public ConflictInfo? PendingConflict
        {
            get => _pendingConflict;
            set => SetProperty(ref _pendingConflict, value);
        }

        /// <summary>
        /// Priority in queue (higher = more priority).
        /// </summary>
        public int Priority
        {
            get => _priority;
            set => SetProperty(ref _priority, value);
        }

        /// <summary>
        /// Custom tag data for operation metadata.
        /// </summary>
        public Dictionary<string, object>? TagData { get; set; }

        /// <summary>
        /// Percentage complete (0-100).
        /// </summary>
        public int PercentComplete => TotalBytes > 0 ? (int)(ProcessedBytes * 100 / TotalBytes) : 0;

        /// <summary>
        /// Formatted speed display.
        /// </summary>
        public string FormattedSpeed => FormatSpeed(SpeedBytesPerSecond);

        /// <summary>
        /// Formatted time remaining.
        /// </summary>
        public string FormattedTimeRemaining => FormatTimeSpan(EstimatedRemaining);

        /// <summary>
        /// Elapsed time since start.
        /// </summary>
        public TimeSpan Elapsed => DateTime.UtcNow - StartedAt;

        /// <summary>
        /// Formatted elapsed time.
        /// </summary>
        public string FormattedElapsedTime => FormatTimeSpan(Elapsed);

        private static string FormatSpeed(double bytesPerSecond)
        {
            if (bytesPerSecond < 1024)
                return $"{bytesPerSecond:F0} B/s";
            
            if (bytesPerSecond < 1024 * 1024)
                return $"{bytesPerSecond / 1024:F1} KB/s";
            
            if (bytesPerSecond < 1024 * 1024 * 1024)
                return $"{bytesPerSecond / (1024 * 1024):F1} MB/s";
            
            return $"{bytesPerSecond / (1024 * 1024 * 1024):F2} GB/s";
        }

        private static string FormatTimeSpan(TimeSpan span)
        {
            if (span.TotalSeconds < 60)
                return $"{span.Seconds}s";
            
            if (span.TotalMinutes < 60)
                return $"{span.Minutes}m {span.Seconds}s";
            
            if (span.TotalHours < 24)
                return $"{span.Hours}h {span.Minutes}m";
            
            return $"{span.Days}d {span.Hours}h";
        }
    }

    /// <summary>
    /// Type of file operation.
    /// </summary>
    public enum OperationType
    {
        Copy,
        Move,
        Delete
    }

    /// <summary>
    /// Status of an operation.
    /// </summary>
    public enum OperationStatus
    {
        Queued,
        Running,
        Paused,
        Conflict,
        Completed,
        Failed,
        Cancelled
    }

    /// <summary>
    /// Information about a file conflict.
    /// </summary>
    public class ConflictInfo
    {
        /// <summary>
        /// Source file path.
        /// </summary>
        public string SourcePath { get; set; } = string.Empty;

        /// <summary>
        /// Destination file path.
        /// </summary>
        public string DestinationPath { get; set; } = string.Empty;

        /// <summary>
        /// Source file size.
        /// </summary>
        public long SourceSize { get; set; }

        /// <summary>
        /// Destination file size.
        /// </summary>
        public long DestinationSize { get; set; }

        /// <summary>
        /// Source file modified date.
        /// </summary>
        public DateTime SourceModified { get; set; }

        /// <summary>
        /// Destination file modified date.
        /// </summary>
        public DateTime DestinationModified { get; set; }

        /// <summary>
        /// Type of conflict.
        /// </summary>
        public ConflictType ConflictType { get; set; }

        /// <summary>
        /// Recommended resolution.
        /// </summary>
        public ConflictResolution RecommendedResolution { get; set; }
    }

    /// <summary>
    /// Type of conflict.
    /// </summary>
    public enum ConflictType
    {
        FileExists,
        FolderExists,
        ReadOnly,
        InsufficientPermissions
    }

    /// <summary>
    /// Options for resolving conflicts.
    /// </summary>
    public enum ConflictResolution
    {
        Prompt,
        Skip,
        SkipAll,
        Overwrite,
        OverwriteAll,
        OverwriteIfNewer,
        OverwriteIfNewerAll,
        Rename,
        RenameAll,
        Cancel,
    }

    /// <summary>
    /// Event arguments for operation progress.
    /// </summary>
    public class OperationProgressEventArgs : EventArgs
    {
        public FileOperation Operation { get; set; } = null!;
    }

    /// <summary>
    /// Event arguments for operation completion.
    /// </summary>
    public class OperationCompletedEventArgs : EventArgs
    {
        public FileOperation Operation { get; set; } = null!;
        public bool Success { get; set; }
        public int FilesProcessed { get; set; }
        public int FilesFailed { get; set; }
    }

    /// <summary>
    /// Statistics about operations.
    /// </summary>
    public class OperationStatistics
    {
        /// <summary>
        /// Total operations ever performed.
        /// </summary>
        public int TotalOperations { get; set; }

        /// <summary>
        /// Operations in queue.
        /// </summary>
        public int QueuedOperations { get; set; }

        /// <summary>
        /// Currently running operations.
        /// </summary>
        public int RunningOperations { get; set; }

        /// <summary>
        /// Completed operations.
        /// </summary>
        public int CompletedOperations { get; set; }

        /// <summary>
        /// Failed operations.
        /// </summary>
        public int FailedOperations { get; set; }

        /// <summary>
        /// Total bytes transferred.
        /// </summary>
        public long TotalBytesTransferred { get; set; }

        /// <summary>
        /// Average transfer speed.
        /// </summary>
        public double AverageSpeedBytesPerSecond { get; set; }

        /// <summary>
        /// Total time spent on operations.
        /// </summary>
        public TimeSpan TotalOperationTime { get; set; }
    }

    /// <summary>
    /// Settings for file operations.
    /// </summary>
    public class OperationSettings
    {
        /// <summary>
        /// Whether to verify files after copy.
        /// </summary>
        public bool VerifyAfterCopy { get; set; } = false;

        /// <summary>
        /// Buffer size for file operations (in bytes).
        /// </summary>
        public int BufferSize { get; set; } = 64 * 1024; // 64KB

        /// <summary>
        /// Number of parallel operations.
        /// </summary>
        public int ParallelOperations { get; set; } = 1;

        /// <summary>
        /// Whether to preserve timestamps.
        /// </summary>
        public bool PreserveTimestamps { get; set; } = true;

        /// <summary>
        /// Whether to preserve attributes.
        /// </summary>
        public bool PreserveAttributes { get; set; } = true;

        /// <summary>
        /// Default conflict resolution.
        /// </summary>
        public ConflictResolution DefaultConflictResolution { get; set; } = ConflictResolution.Prompt;

        /// <summary>
        /// Whether to use recycle bin for delete.
        /// </summary>
        public bool UseRecycleBin { get; set; } = true;

        /// <summary>
        /// Maximum operation history size.
        /// </summary>
        public int MaxHistorySize { get; set; } = 100;
    }
}
