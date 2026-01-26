using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Winhance.Core.Features.FileManager.Interfaces
{
    /// <summary>
    /// Service for synchronizing folders.
    /// </summary>
    public interface ISyncService
    {
        /// <summary>
        /// Gets active sync jobs.
        /// </summary>
        IReadOnlyList<SyncJob> ActiveJobs { get; }

        /// <summary>
        /// Event raised when sync progress changes.
        /// </summary>
        event EventHandler<SyncProgressEventArgs>? ProgressChanged;

        /// <summary>
        /// Event raised when sync completes.
        /// </summary>
        event EventHandler<SyncCompletedEventArgs>? SyncCompleted;

        /// <summary>
        /// Synchronizes two folders.
        /// </summary>
        /// <param name="source">Source folder path.</param>
        /// <param name="destination">Destination folder path.</param>
        /// <param name="options">Sync options.</param>
        /// <param name="progress">Progress reporter.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Sync result.</returns>
        Task<SyncResult> SyncFoldersAsync(
            string source,
            string destination,
            SyncOptions? options = null,
            IProgress<SyncProgress>? progress = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Previews a sync operation without making changes.
        /// </summary>
        /// <param name="source">Source folder path.</param>
        /// <param name="destination">Destination folder path.</param>
        /// <param name="options">Sync options.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Sync preview.</returns>
        Task<SyncPreview> PreviewSyncAsync(
            string source,
            string destination,
            SyncOptions? options = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates a scheduled sync job.
        /// </summary>
        /// <param name="source">Source folder path.</param>
        /// <param name="destination">Destination folder path.</param>
        /// <param name="options">Sync options.</param>
        /// <param name="schedule">Schedule options.</param>
        /// <returns>The created job.</returns>
        SyncJob CreateScheduledJob(string source, string destination, SyncOptions options, SyncSchedule schedule);

        /// <summary>
        /// Removes a scheduled sync job.
        /// </summary>
        /// <param name="jobId">Job ID.</param>
        void RemoveJob(string jobId);

        /// <summary>
        /// Runs a scheduled job immediately.
        /// </summary>
        /// <param name="jobId">Job ID.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task RunJobAsync(string jobId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets sync history.
        /// </summary>
        /// <param name="jobId">Job ID (null for all).</param>
        /// <param name="maxEntries">Maximum entries.</param>
        IEnumerable<SyncHistoryEntry> GetHistory(string? jobId = null, int maxEntries = 50);

        /// <summary>
        /// Saves sync configuration.
        /// </summary>
        Task SaveConfigurationAsync();

        /// <summary>
        /// Loads sync configuration.
        /// </summary>
        Task LoadConfigurationAsync();
    }

    /// <summary>
    /// Sync options.
    /// </summary>
    public class SyncOptions
    {
        /// <summary>Gets or sets the sync direction.</summary>
        public SyncDirection Direction { get; set; } = SyncDirection.SourceToDestination;

        /// <summary>Gets or sets the sync mode.</summary>
        public SyncMode Mode { get; set; } = SyncMode.Update;

        /// <summary>Gets or sets whether to sync recursively.</summary>
        public bool Recursive { get; set; } = true;

        /// <summary>Gets or sets whether to delete extra files in destination.</summary>
        public bool DeleteExtraFiles { get; set; } = false;

        /// <summary>Gets or sets whether to preserve timestamps.</summary>
        public bool PreserveTimestamps { get; set; } = true;

        /// <summary>Gets or sets whether to preserve attributes.</summary>
        public bool PreserveAttributes { get; set; } = true;

        /// <summary>Gets or sets whether to include hidden files.</summary>
        public bool IncludeHidden { get; set; } = false;

        /// <summary>Gets or sets patterns to exclude.</summary>
        public string[]? ExcludePatterns { get; set; }

        /// <summary>Gets or sets patterns to include (null = all).</summary>
        public string[]? IncludePatterns { get; set; }

        /// <summary>Gets or sets conflict resolution.</summary>
        public SyncConflictResolution ConflictResolution { get; set; } = SyncConflictResolution.NewerWins;

        /// <summary>Gets or sets whether to verify with checksum.</summary>
        public bool VerifyWithChecksum { get; set; } = false;

        /// <summary>Gets or sets whether to use archive attribute.</summary>
        public bool UseArchiveAttribute { get; set; } = false;
    }

    /// <summary>
    /// Sync direction.
    /// </summary>
    public enum SyncDirection
    {
        /// <summary>Copy from source to destination.</summary>
        SourceToDestination,

        /// <summary>Copy from destination to source.</summary>
        DestinationToSource,

        /// <summary>Two-way synchronization.</summary>
        Bidirectional
    }

    /// <summary>
    /// Sync mode.
    /// </summary>
    public enum SyncMode
    {
        /// <summary>Mirror source to destination (exact copy).</summary>
        Mirror,

        /// <summary>Update destination with newer files.</summary>
        Update,

        /// <summary>Echo - copy new/updated, optionally delete.</summary>
        Echo,

        /// <summary>Contribute - add to destination only.</summary>
        Contribute
    }

    /// <summary>
    /// How to resolve conflicts.
    /// </summary>
    public enum SyncConflictResolution
    {
        /// <summary>Newer file wins.</summary>
        NewerWins,

        /// <summary>Larger file wins.</summary>
        LargerWins,

        /// <summary>Source always wins.</summary>
        SourceWins,

        /// <summary>Destination always wins.</summary>
        DestinationWins,

        /// <summary>Skip conflicting files.</summary>
        Skip,

        /// <summary>Keep both (rename one).</summary>
        KeepBoth,

        /// <summary>Ask user.</summary>
        Ask
    }

    /// <summary>
    /// A scheduled sync job.
    /// </summary>
    public class SyncJob
    {
        /// <summary>Gets or sets the job ID.</summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>Gets or sets the job name.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Gets or sets the source path.</summary>
        public string SourcePath { get; set; } = string.Empty;

        /// <summary>Gets or sets the destination path.</summary>
        public string DestinationPath { get; set; } = string.Empty;

        /// <summary>Gets or sets the sync options.</summary>
        public SyncOptions Options { get; set; } = new();

        /// <summary>Gets or sets the schedule.</summary>
        public SyncSchedule Schedule { get; set; } = new();

        /// <summary>Gets or sets whether job is enabled.</summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>Gets or sets the last run time.</summary>
        public DateTime? LastRunTime { get; set; }

        /// <summary>Gets or sets the next run time.</summary>
        public DateTime? NextRunTime { get; set; }

        /// <summary>Gets or sets the last result status.</summary>
        public SyncStatus LastStatus { get; set; } = SyncStatus.NotRun;

        /// <summary>Gets or sets the creation time.</summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Sync schedule options.
    /// </summary>
    public class SyncSchedule
    {
        /// <summary>Gets or sets the schedule type.</summary>
        public ScheduleType Type { get; set; } = ScheduleType.Manual;

        /// <summary>Gets or sets the interval (for Interval type).</summary>
        public TimeSpan Interval { get; set; } = TimeSpan.FromHours(1);

        /// <summary>Gets or sets the time of day (for Daily/Weekly).</summary>
        public TimeSpan TimeOfDay { get; set; } = new TimeSpan(2, 0, 0); // 2 AM

        /// <summary>Gets or sets the days of week (for Weekly).</summary>
        public DayOfWeek[]? DaysOfWeek { get; set; }

        /// <summary>Gets or sets the day of month (for Monthly).</summary>
        public int DayOfMonth { get; set; } = 1;
    }

    /// <summary>
    /// Schedule types.
    /// </summary>
    public enum ScheduleType
    {
        /// <summary>Manual only.</summary>
        Manual,

        /// <summary>On file change.</summary>
        OnChange,

        /// <summary>At regular intervals.</summary>
        Interval,

        /// <summary>Daily at specific time.</summary>
        Daily,

        /// <summary>Weekly on specific days.</summary>
        Weekly,

        /// <summary>Monthly on specific day.</summary>
        Monthly
    }

    /// <summary>
    /// Sync status.
    /// </summary>
    public enum SyncStatus
    {
        /// <summary>Never run.</summary>
        NotRun,

        /// <summary>Currently running.</summary>
        Running,

        /// <summary>Completed successfully.</summary>
        Success,

        /// <summary>Completed with warnings.</summary>
        Warning,

        /// <summary>Failed.</summary>
        Failed,

        /// <summary>Cancelled.</summary>
        Cancelled
    }

    /// <summary>
    /// Preview of sync operation.
    /// </summary>
    public class SyncPreview
    {
        /// <summary>Gets or sets files to be copied to destination.</summary>
        public List<SyncFileAction> FilesToCopy { get; set; } = new();

        /// <summary>Gets or sets files to be updated in destination.</summary>
        public List<SyncFileAction> FilesToUpdate { get; set; } = new();

        /// <summary>Gets or sets files to be deleted from destination.</summary>
        public List<SyncFileAction> FilesToDelete { get; set; } = new();

        /// <summary>Gets or sets files with conflicts.</summary>
        public List<SyncFileAction> Conflicts { get; set; } = new();

        /// <summary>Gets total files to process.</summary>
        public int TotalFiles => FilesToCopy.Count + FilesToUpdate.Count + FilesToDelete.Count;

        /// <summary>Gets total bytes to transfer.</summary>
        public long TotalBytes { get; set; }
    }

    /// <summary>
    /// A file action in sync.
    /// </summary>
    public class SyncFileAction
    {
        /// <summary>Gets or sets the relative path.</summary>
        public string RelativePath { get; set; } = string.Empty;

        /// <summary>Gets or sets the source path.</summary>
        public string SourcePath { get; set; } = string.Empty;

        /// <summary>Gets or sets the destination path.</summary>
        public string DestinationPath { get; set; } = string.Empty;

        /// <summary>Gets or sets the action type.</summary>
        public SyncActionType Action { get; set; }

        /// <summary>Gets or sets the file size.</summary>
        public long Size { get; set; }

        /// <summary>Gets or sets the source modified date.</summary>
        public DateTime? SourceModified { get; set; }

        /// <summary>Gets or sets the destination modified date.</summary>
        public DateTime? DestinationModified { get; set; }

        /// <summary>Gets or sets the reason for action.</summary>
        public string? Reason { get; set; }
    }

    /// <summary>
    /// Sync action types.
    /// </summary>
    public enum SyncActionType
    {
        /// <summary>Copy new file.</summary>
        Copy,

        /// <summary>Update existing file.</summary>
        Update,

        /// <summary>Delete file.</summary>
        Delete,

        /// <summary>Conflict needs resolution.</summary>
        Conflict,

        /// <summary>Skip (no action needed).</summary>
        Skip
    }

    /// <summary>
    /// Sync progress.
    /// </summary>
    public class SyncProgress
    {
        /// <summary>Gets or sets the current file.</summary>
        public string CurrentFile { get; set; } = string.Empty;

        /// <summary>Gets or sets total files.</summary>
        public int TotalFiles { get; set; }

        /// <summary>Gets or sets processed files.</summary>
        public int ProcessedFiles { get; set; }

        /// <summary>Gets or sets total bytes.</summary>
        public long TotalBytes { get; set; }

        /// <summary>Gets or sets processed bytes.</summary>
        public long ProcessedBytes { get; set; }

        /// <summary>Gets or sets the current action.</summary>
        public SyncActionType CurrentAction { get; set; }

        /// <summary>Gets the percent complete.</summary>
        public int PercentComplete => TotalFiles > 0 ? ProcessedFiles * 100 / TotalFiles : 0;
    }

    /// <summary>
    /// Sync result.
    /// </summary>
    public class SyncResult
    {
        /// <summary>Gets or sets whether sync succeeded.</summary>
        public bool Success { get; set; }

        /// <summary>Gets or sets files copied.</summary>
        public int FilesCopied { get; set; }

        /// <summary>Gets or sets files updated.</summary>
        public int FilesUpdated { get; set; }

        /// <summary>Gets or sets files deleted.</summary>
        public int FilesDeleted { get; set; }

        /// <summary>Gets or sets files skipped.</summary>
        public int FilesSkipped { get; set; }

        /// <summary>Gets or sets files failed.</summary>
        public int FilesFailed { get; set; }

        /// <summary>Gets or sets bytes transferred.</summary>
        public long BytesTransferred { get; set; }

        /// <summary>Gets or sets the duration.</summary>
        public TimeSpan Duration { get; set; }

        /// <summary>Gets or sets errors.</summary>
        public List<string> Errors { get; set; } = new();

        /// <summary>Gets or sets warnings.</summary>
        public List<string> Warnings { get; set; } = new();
    }

    /// <summary>
    /// Sync history entry.
    /// </summary>
    public class SyncHistoryEntry
    {
        /// <summary>Gets or sets the entry ID.</summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>Gets or sets the job ID.</summary>
        public string JobId { get; set; } = string.Empty;

        /// <summary>Gets or sets the start time.</summary>
        public DateTime StartTime { get; set; }

        /// <summary>Gets or sets the end time.</summary>
        public DateTime EndTime { get; set; }

        /// <summary>Gets or sets the status.</summary>
        public SyncStatus Status { get; set; }

        /// <summary>Gets or sets the result.</summary>
        public SyncResult? Result { get; set; }
    }

    /// <summary>
    /// Event args for sync progress.
    /// </summary>
    public class SyncProgressEventArgs : EventArgs
    {
        /// <summary>Gets or sets the job ID.</summary>
        public string JobId { get; set; } = string.Empty;

        /// <summary>Gets or sets the progress.</summary>
        public SyncProgress Progress { get; set; } = new();
    }

    /// <summary>
    /// Event args for sync completion.
    /// </summary>
    public class SyncCompletedEventArgs : EventArgs
    {
        /// <summary>Gets or sets the job ID.</summary>
        public string JobId { get; set; } = string.Empty;

        /// <summary>Gets or sets the result.</summary>
        public SyncResult Result { get; set; } = new();
    }
}
