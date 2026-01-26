using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;

namespace Winhance.Core.Features.FileManager.Interfaces
{
    /// <summary>
    /// Service for monitoring folders and executing automated rules on file changes.
    /// </summary>
    public interface IWatchFolderService : IDisposable
    {
        /// <summary>
        /// Gets the collection of active watch folders.
        /// </summary>
        ObservableCollection<WatchFolder> WatchFolders { get; }

        /// <summary>
        /// Gets whether the watch service is running.
        /// </summary>
        bool IsRunning { get; }

        /// <summary>
        /// Event raised when a file event is detected.
        /// </summary>
        event EventHandler<WatchEventArgs>? FileEventDetected;

        /// <summary>
        /// Event raised when a rule is executed.
        /// </summary>
        event EventHandler<RuleExecutionEventArgs>? RuleExecuted;

        /// <summary>
        /// Event raised when an error occurs.
        /// </summary>
        event EventHandler<WatchErrorEventArgs>? ErrorOccurred;

        /// <summary>
        /// Creates a new watch folder.
        /// </summary>
        /// <param name="path">The folder path to watch.</param>
        /// <param name="name">Optional name for the watch folder.</param>
        /// <returns>The created watch folder.</returns>
        WatchFolder CreateWatchFolder(string path, string? name = null);

        /// <summary>
        /// Removes a watch folder.
        /// </summary>
        /// <param name="watchFolder">The watch folder to remove.</param>
        void RemoveWatchFolder(WatchFolder watchFolder);

        /// <summary>
        /// Starts monitoring a watch folder.
        /// </summary>
        /// <param name="watchFolder">The watch folder to start.</param>
        void StartWatching(WatchFolder watchFolder);

        /// <summary>
        /// Stops monitoring a watch folder.
        /// </summary>
        /// <param name="watchFolder">The watch folder to stop.</param>
        void StopWatching(WatchFolder watchFolder);

        /// <summary>
        /// Starts all enabled watch folders.
        /// </summary>
        void StartAll();

        /// <summary>
        /// Stops all watch folders.
        /// </summary>
        void StopAll();

        /// <summary>
        /// Adds a rule to a watch folder.
        /// </summary>
        /// <param name="watchFolder">The watch folder.</param>
        /// <param name="rule">The rule to add.</param>
        void AddRule(WatchFolder watchFolder, WatchRule rule);

        /// <summary>
        /// Removes a rule from a watch folder.
        /// </summary>
        /// <param name="watchFolder">The watch folder.</param>
        /// <param name="rule">The rule to remove.</param>
        void RemoveRule(WatchFolder watchFolder, WatchRule rule);

        /// <summary>
        /// Processes existing files in a watch folder using current rules.
        /// </summary>
        /// <param name="watchFolder">The watch folder to process.</param>
        /// <param name="dryRun">If true, simulates without making changes.</param>
        /// <param name="progress">Progress reporter.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Processing results.</returns>
        Task<ProcessingResult> ProcessExistingFilesAsync(
            WatchFolder watchFolder,
            bool dryRun = false,
            IProgress<WatchProcessingProgress>? progress = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Tests a rule against a specific file.
        /// </summary>
        /// <param name="rule">The rule to test.</param>
        /// <param name="filePath">The file path to test against.</param>
        /// <returns>Whether the rule matches.</returns>
        bool TestRule(WatchRule rule, string filePath);

        /// <summary>
        /// Gets the execution history for a watch folder.
        /// </summary>
        /// <param name="watchFolder">The watch folder.</param>
        /// <param name="maxEntries">Maximum entries to return.</param>
        /// <returns>Execution history entries.</returns>
        IEnumerable<ExecutionHistoryEntry> GetHistory(WatchFolder watchFolder, int maxEntries = 100);

        /// <summary>
        /// Clears the execution history.
        /// </summary>
        /// <param name="watchFolder">The watch folder (null for all).</param>
        void ClearHistory(WatchFolder? watchFolder = null);

        /// <summary>
        /// Saves watch folder configuration.
        /// </summary>
        Task SaveConfigurationAsync();

        /// <summary>
        /// Loads watch folder configuration.
        /// </summary>
        Task LoadConfigurationAsync();

        /// <summary>
        /// Exports watch folders to JSON.
        /// </summary>
        /// <param name="filePath">Export file path.</param>
        Task ExportAsync(string filePath);

        /// <summary>
        /// Imports watch folders from JSON.
        /// </summary>
        /// <param name="filePath">Import file path.</param>
        Task ImportAsync(string filePath);
    }

    /// <summary>
    /// Represents a folder being monitored.
    /// </summary>
    public class WatchFolder
    {
        /// <summary>Gets or sets the unique identifier.</summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>Gets or sets the display name.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Gets or sets the folder path.</summary>
        public string Path { get; set; } = string.Empty;

        /// <summary>Gets or sets whether this watch folder is enabled.</summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>Gets or sets whether to watch subfolders.</summary>
        public bool IncludeSubfolders { get; set; } = false;

        /// <summary>Gets or sets the file filter pattern (e.g., "*.txt").</summary>
        public string Filter { get; set; } = "*.*";

        /// <summary>Gets or sets patterns to exclude.</summary>
        public List<string> ExcludePatterns { get; set; } = new();

        /// <summary>Gets or sets the settle time in milliseconds (wait for file to be ready).</summary>
        public int SettleTimeMs { get; set; } = 1000;

        /// <summary>Gets or sets which events to monitor.</summary>
        public WatchEventTypes MonitoredEvents { get; set; } = WatchEventTypes.Created;

        /// <summary>Gets or sets the rules to apply.</summary>
        public List<WatchRule> Rules { get; set; } = new();

        /// <summary>Gets or sets the current status.</summary>
        public WatchStatus Status { get; set; } = WatchStatus.Stopped;

        /// <summary>Gets or sets the last error message.</summary>
        public string? LastError { get; set; }

        /// <summary>Gets or sets the creation time.</summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>Gets or sets the last event time.</summary>
        public DateTime? LastEventAt { get; set; }

        /// <summary>Gets or sets the total files processed.</summary>
        public long TotalFilesProcessed { get; set; }
    }

    /// <summary>
    /// Watch folder status.
    /// </summary>
    public enum WatchStatus
    {
        /// <summary>Not monitoring.</summary>
        Stopped,

        /// <summary>Actively monitoring.</summary>
        Running,

        /// <summary>Paused by user.</summary>
        Paused,

        /// <summary>Error state.</summary>
        Error
    }

    /// <summary>
    /// Types of file system events to monitor.
    /// </summary>
    [Flags]
    public enum WatchEventTypes
    {
        /// <summary>No events.</summary>
        None = 0,

        /// <summary>File created.</summary>
        Created = 1,

        /// <summary>File modified.</summary>
        Changed = 2,

        /// <summary>File deleted.</summary>
        Deleted = 4,

        /// <summary>File renamed.</summary>
        Renamed = 8,

        /// <summary>All events.</summary>
        All = Created | Changed | Deleted | Renamed
    }

    /// <summary>
    /// Rule to apply when a watch event occurs.
    /// </summary>
    public class WatchRule
    {
        /// <summary>Gets or sets the unique identifier.</summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>Gets or sets the rule name.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Gets or sets whether this rule is enabled.</summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>Gets or sets the rule priority (lower = higher priority).</summary>
        public int Priority { get; set; } = 100;

        /// <summary>Gets or sets the conditions that must match.</summary>
        public List<WatchRuleCondition> Conditions { get; set; } = new();

        /// <summary>Gets or sets how conditions are combined.</summary>
        public ConditionLogic ConditionLogic { get; set; } = ConditionLogic.All;

        /// <summary>Gets or sets the action to perform.</summary>
        public WatchRuleAction Action { get; set; } = new();

        /// <summary>Gets or sets whether to stop processing more rules if this matches.</summary>
        public bool StopProcessing { get; set; } = true;
    }

    /// <summary>
    /// How to combine multiple conditions.
    /// </summary>
    public enum ConditionLogic
    {
        /// <summary>All conditions must match (AND).</summary>
        All,

        /// <summary>Any condition must match (OR).</summary>
        Any,

        /// <summary>No conditions must match (NOT).</summary>
        None
    }

    /// <summary>
    /// A condition for watch rule matching.
    /// </summary>
    public class WatchRuleCondition
    {
        /// <summary>Gets or sets the condition type.</summary>
        public WatchConditionType Type { get; set; }

        /// <summary>Gets or sets the operator.</summary>
        public WatchConditionOperator Operator { get; set; }

        /// <summary>Gets or sets the value to compare.</summary>
        public string Value { get; set; } = string.Empty;

        /// <summary>Gets or sets the secondary value (for between).</summary>
        public string? Value2 { get; set; }

        /// <summary>Gets or sets whether comparison is case-sensitive.</summary>
        public bool CaseSensitive { get; set; } = false;
    }

    /// <summary>
    /// Types of watch conditions.
    /// </summary>
    public enum WatchConditionType
    {
        /// <summary>File name.</summary>
        FileName,

        /// <summary>File extension.</summary>
        Extension,

        /// <summary>Full path.</summary>
        FullPath,

        /// <summary>File size in bytes.</summary>
        FileSize,

        /// <summary>Date modified.</summary>
        DateModified,

        /// <summary>Date created.</summary>
        DateCreated,

        /// <summary>File attributes.</summary>
        Attributes,

        /// <summary>Content contains (for text files).</summary>
        ContentContains
    }

    /// <summary>
    /// Watch condition operators.
    /// </summary>
    public enum WatchConditionOperator
    {
        /// <summary>Equals.</summary>
        Equals,

        /// <summary>Not equals.</summary>
        NotEquals,

        /// <summary>Contains.</summary>
        Contains,

        /// <summary>Not contains.</summary>
        NotContains,

        /// <summary>Starts with.</summary>
        StartsWith,

        /// <summary>Ends with.</summary>
        EndsWith,

        /// <summary>Matches regex.</summary>
        MatchesRegex,

        /// <summary>Greater than.</summary>
        GreaterThan,

        /// <summary>Less than.</summary>
        LessThan,

        /// <summary>Between (inclusive).</summary>
        Between,

        /// <summary>In list.</summary>
        In
    }

    /// <summary>
    /// Action to perform when a watch rule matches.
    /// </summary>
    public class WatchRuleAction
    {
        /// <summary>Gets or sets the action type.</summary>
        public WatchActionType Type { get; set; } = WatchActionType.Move;

        /// <summary>Gets or sets the destination path (for move/copy).</summary>
        public string DestinationPath { get; set; } = string.Empty;

        /// <summary>Gets or sets the new name pattern (for rename).</summary>
        public string? RenamePattern { get; set; }

        /// <summary>Gets or sets how to handle conflicts.</summary>
        public WatchConflictAction ConflictAction { get; set; } = WatchConflictAction.Rename;

        /// <summary>Gets or sets the script path (for RunScript).</summary>
        public string? ScriptPath { get; set; }

        /// <summary>Gets or sets script arguments.</summary>
        public string? ScriptArguments { get; set; }

        /// <summary>Gets or sets the tag to add (for AddTag).</summary>
        public string? Tag { get; set; }

        /// <summary>Gets or sets whether to create missing directories.</summary>
        public bool CreateDirectories { get; set; } = true;
    }

    /// <summary>
    /// Types of watch actions.
    /// </summary>
    public enum WatchActionType
    {
        /// <summary>Move to destination.</summary>
        Move,

        /// <summary>Copy to destination.</summary>
        Copy,

        /// <summary>Delete the file.</summary>
        Delete,

        /// <summary>Rename the file.</summary>
        Rename,

        /// <summary>Add a tag.</summary>
        AddTag,

        /// <summary>Compress to archive.</summary>
        Compress,

        /// <summary>Run a script.</summary>
        RunScript,

        /// <summary>Create a symlink.</summary>
        CreateSymlink,

        /// <summary>Send notification only.</summary>
        Notify
    }

    /// <summary>
    /// How to handle file conflicts in watch actions.
    /// </summary>
    public enum WatchConflictAction
    {
        /// <summary>Skip the file.</summary>
        Skip,

        /// <summary>Overwrite existing.</summary>
        Overwrite,

        /// <summary>Overwrite if newer.</summary>
        OverwriteIfNewer,

        /// <summary>Rename with number suffix.</summary>
        Rename
    }

    /// <summary>
    /// Event args for watch events.
    /// </summary>
    public class WatchEventArgs : EventArgs
    {
        /// <summary>Gets or sets the watch folder.</summary>
        public WatchFolder WatchFolder { get; set; } = null!;

        /// <summary>Gets or sets the event type.</summary>
        public WatchEventTypes EventType { get; set; }

        /// <summary>Gets or sets the file path.</summary>
        public string FilePath { get; set; } = string.Empty;

        /// <summary>Gets or sets the old path (for rename).</summary>
        public string? OldPath { get; set; }

        /// <summary>Gets or sets the event time.</summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Event args for rule execution.
    /// </summary>
    public class RuleExecutionEventArgs : EventArgs
    {
        /// <summary>Gets or sets the watch folder.</summary>
        public WatchFolder WatchFolder { get; set; } = null!;

        /// <summary>Gets or sets the rule that executed.</summary>
        public WatchRule Rule { get; set; } = null!;

        /// <summary>Gets or sets the source file path.</summary>
        public string SourcePath { get; set; } = string.Empty;

        /// <summary>Gets or sets the destination path (if applicable).</summary>
        public string? DestinationPath { get; set; }

        /// <summary>Gets or sets whether execution succeeded.</summary>
        public bool Success { get; set; }

        /// <summary>Gets or sets the error message if failed.</summary>
        public string? ErrorMessage { get; set; }

        /// <summary>Gets or sets the execution time.</summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Event args for watch errors.
    /// </summary>
    public class WatchErrorEventArgs : EventArgs
    {
        /// <summary>Gets or sets the watch folder.</summary>
        public WatchFolder? WatchFolder { get; set; }

        /// <summary>Gets or sets the error message.</summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>Gets or sets the exception.</summary>
        public Exception? Exception { get; set; }

        /// <summary>Gets or sets whether the error is recoverable.</summary>
        public bool IsRecoverable { get; set; }
    }

    /// <summary>
    /// Progress for processing existing files.
    /// </summary>
    public class WatchProcessingProgress
    {
        /// <summary>Gets or sets the current file being processed.</summary>
        public string CurrentFile { get; set; } = string.Empty;

        /// <summary>Gets or sets the total files found.</summary>
        public int TotalFiles { get; set; }

        /// <summary>Gets or sets the files processed.</summary>
        public int ProcessedFiles { get; set; }

        /// <summary>Gets or sets the files matched by rules.</summary>
        public int MatchedFiles { get; set; }

        /// <summary>Gets or sets the percent complete.</summary>
        public int PercentComplete => TotalFiles > 0 ? ProcessedFiles * 100 / TotalFiles : 0;
    }

    /// <summary>
    /// Result of processing existing files.
    /// </summary>
    public class ProcessingResult
    {
        /// <summary>Gets or sets total files found.</summary>
        public int TotalFiles { get; set; }

        /// <summary>Gets or sets files processed.</summary>
        public int ProcessedFiles { get; set; }

        /// <summary>Gets or sets files matched by rules.</summary>
        public int MatchedFiles { get; set; }

        /// <summary>Gets or sets files that had errors.</summary>
        public int ErrorFiles { get; set; }

        /// <summary>Gets or sets files skipped.</summary>
        public int SkippedFiles { get; set; }

        /// <summary>Gets or sets whether it was a dry run.</summary>
        public bool WasDryRun { get; set; }

        /// <summary>Gets or sets detailed results for each file.</summary>
        public List<FileProcessingResult> FileResults { get; set; } = new();
    }

    /// <summary>
    /// Result for a single file processing.
    /// </summary>
    public class FileProcessingResult
    {
        /// <summary>Gets or sets the source path.</summary>
        public string SourcePath { get; set; } = string.Empty;

        /// <summary>Gets or sets the rule that matched.</summary>
        public string? MatchedRule { get; set; }

        /// <summary>Gets or sets the action taken.</summary>
        public WatchActionType? Action { get; set; }

        /// <summary>Gets or sets the destination path.</summary>
        public string? DestinationPath { get; set; }

        /// <summary>Gets or sets whether it succeeded.</summary>
        public bool Success { get; set; }

        /// <summary>Gets or sets the error message.</summary>
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// Execution history entry.
    /// </summary>
    public class ExecutionHistoryEntry
    {
        /// <summary>Gets or sets the entry ID.</summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>Gets or sets the watch folder ID.</summary>
        public string WatchFolderId { get; set; } = string.Empty;

        /// <summary>Gets or sets the rule ID.</summary>
        public string? RuleId { get; set; }

        /// <summary>Gets or sets the rule name.</summary>
        public string? RuleName { get; set; }

        /// <summary>Gets or sets the event type.</summary>
        public WatchEventTypes EventType { get; set; }

        /// <summary>Gets or sets the source path.</summary>
        public string SourcePath { get; set; } = string.Empty;

        /// <summary>Gets or sets the destination path.</summary>
        public string? DestinationPath { get; set; }

        /// <summary>Gets or sets the action type.</summary>
        public WatchActionType? ActionType { get; set; }

        /// <summary>Gets or sets whether it succeeded.</summary>
        public bool Success { get; set; }

        /// <summary>Gets or sets the error message.</summary>
        public string? ErrorMessage { get; set; }

        /// <summary>Gets or sets the timestamp.</summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
