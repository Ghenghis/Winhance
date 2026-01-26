using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Winhance.Core.Features.FileManager.Interfaces;

namespace Winhance.Infrastructure.Features.FileManager.Services
{
    /// <summary>
    /// Service for synchronizing folders.
    /// </summary>
    public class SyncService : ISyncService, IDisposable
    {
        private readonly ILogger<SyncService> _logger;
        private readonly ConcurrentDictionary<string, SyncJob> _jobs;
        private readonly ConcurrentDictionary<string, CancellationTokenSource> _runningJobs;
        private readonly List<SyncHistoryEntry> _history;
        private readonly SemaphoreSlim _syncSemaphore;
        private readonly string _configPath;
        private bool _disposed;

        public SyncService(ILogger<SyncService> logger)
        {
            _logger = logger;
            _jobs = new ConcurrentDictionary<string, SyncJob>();
            _runningJobs = new ConcurrentDictionary<string, CancellationTokenSource>();
            _history = new List<SyncHistoryEntry>();
            _syncSemaphore = new SemaphoreSlim(Environment.ProcessorCount);
            _configPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Winhance", "sync-config.json");
        }

        /// <inheritdoc />
        public IReadOnlyList<SyncJob> ActiveJobs =>
            _jobs.Values.Where(j => j.LastStatus == SyncStatus.Running).ToList().AsReadOnly();

        /// <inheritdoc />
        public event EventHandler<SyncProgressEventArgs>? ProgressChanged;

        /// <inheritdoc />
        public event EventHandler<SyncCompletedEventArgs>? SyncCompleted;

        /// <inheritdoc />
        public async Task<SyncResult> SyncFoldersAsync(
            string source,
            string destination,
            SyncOptions? options = null,
            IProgress<SyncProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            options ??= new SyncOptions();
            var startTime = DateTime.UtcNow;

            var jobId = Guid.NewGuid().ToString();
            var tempJob = new SyncJob
            {
                Id = jobId,
                Name = $"Sync {Path.GetFileName(source)} to {Path.GetFileName(destination)}",
                SourcePath = source,
                DestinationPath = destination,
                Options = options,
                LastStatus = SyncStatus.Running,
                LastRunTime = DateTime.UtcNow
            };

            _jobs[jobId] = tempJob;

            try
            {
                _logger.LogInformation("Starting sync: {Source} -> {Destination}", source, destination);

                // Validate paths
                if (!Directory.Exists(source))
                {
                    return CreateErrorResult("Source directory does not exist", startTime);
                }

                // Ensure destination exists
                Directory.CreateDirectory(destination);

                // Analyze sync operation
                var preview = await AnalyzeSyncAsync(source, destination, options, cancellationToken);

                // Report analysis progress
                var analysisProgress = new SyncProgress
                {
                    CurrentFile = "Analyzing...",
                    TotalFiles = preview.TotalFiles,
                    ProcessedFiles = 0,
                    TotalBytes = preview.TotalBytes,
                    ProcessedBytes = 0,
                    CurrentAction = SyncActionType.Skip
                };
                progress?.Report(analysisProgress);
                ProgressChanged?.Invoke(this, new SyncProgressEventArgs { JobId = jobId, Progress = analysisProgress });

                // Execute sync
                var result = await ExecuteSyncAsync(source, destination, preview, progress, jobId, cancellationToken);
                result.Duration = DateTime.UtcNow - startTime;

                tempJob.LastStatus = result.Success ? SyncStatus.Success : SyncStatus.Failed;

                // Add to history
                _history.Add(new SyncHistoryEntry
                {
                    Id = Guid.NewGuid().ToString(),
                    JobId = jobId,
                    StartTime = startTime,
                    EndTime = DateTime.UtcNow,
                    Status = tempJob.LastStatus,
                    Result = result
                });

                SyncCompleted?.Invoke(this, new SyncCompletedEventArgs
                {
                    JobId = jobId,
                    Result = result
                });

                return result;
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Sync cancelled: {Source} -> {Destination}", source, destination);
                tempJob.LastStatus = SyncStatus.Cancelled;

                var cancelResult = new SyncResult
                {
                    Success = false,
                    Duration = DateTime.UtcNow - startTime,
                    Errors = new List<string> { "Sync was cancelled" }
                };

                SyncCompleted?.Invoke(this, new SyncCompletedEventArgs { JobId = jobId, Result = cancelResult });
                return cancelResult;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Sync failed: {Source} -> {Destination}", source, destination);
                tempJob.LastStatus = SyncStatus.Failed;

                var errorResult = CreateErrorResult(ex.Message, startTime);
                SyncCompleted?.Invoke(this, new SyncCompletedEventArgs { JobId = jobId, Result = errorResult });
                return errorResult;
            }
            finally
            {
                _jobs.TryRemove(jobId, out _);
            }
        }

        /// <inheritdoc />
        public async Task<SyncPreview> PreviewSyncAsync(
            string source,
            string destination,
            SyncOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            options ??= new SyncOptions();

            _logger.LogInformation("Previewing sync: {Source} -> {Destination}", source, destination);

            if (!Directory.Exists(source))
            {
                return new SyncPreview
                {
                    FilesToCopy = new List<SyncFileAction>(),
                    FilesToUpdate = new List<SyncFileAction>(),
                    FilesToDelete = new List<SyncFileAction>(),
                    Conflicts = new List<SyncFileAction>()
                };
            }

            return await AnalyzeSyncAsync(source, destination, options, cancellationToken);
        }

        /// <inheritdoc />
        public SyncJob CreateScheduledJob(string source, string destination, SyncOptions options, SyncSchedule schedule)
        {
            var job = new SyncJob
            {
                Id = Guid.NewGuid().ToString(),
                Name = $"Sync: {Path.GetFileName(source)}",
                SourcePath = source,
                DestinationPath = destination,
                Options = options,
                Schedule = schedule,
                IsEnabled = true,
                CreatedAt = DateTime.UtcNow
            };

            // Calculate next run time based on schedule
            job.NextRunTime = CalculateNextRunTime(schedule);

            _jobs[job.Id] = job;
            return job;
        }

        /// <inheritdoc />
        public void RemoveJob(string jobId)
        {
            if (_jobs.TryRemove(jobId, out var job))
            {
                _logger.LogInformation("Removed sync job: {JobId}", jobId);

                // Cancel if running
                if (_runningJobs.TryRemove(jobId, out var cts))
                {
                    cts.Cancel();
                    cts.Dispose();
                }
            }
        }

        /// <inheritdoc />
        public async Task RunJobAsync(string jobId, CancellationToken cancellationToken = default)
        {
            if (!_jobs.TryGetValue(jobId, out var job))
            {
                throw new ArgumentException($"Job not found: {jobId}", nameof(jobId));
            }

            var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _runningJobs[jobId] = cts;

            try
            {
                await SyncFoldersAsync(
                    job.SourcePath,
                    job.DestinationPath,
                    job.Options,
                    cancellationToken: cts.Token);
            }
            finally
            {
                _runningJobs.TryRemove(jobId, out _);
                cts.Dispose();
            }
        }

        /// <inheritdoc />
        public IEnumerable<SyncHistoryEntry> GetHistory(string? jobId = null, int maxEntries = 50)
        {
            var query = _history.AsEnumerable();

            if (!string.IsNullOrEmpty(jobId))
            {
                query = query.Where(h => h.JobId == jobId);
            }

            return query
                .OrderByDescending(h => h.StartTime)
                .Take(maxEntries)
                .ToList();
        }

        /// <inheritdoc />
        public async Task SaveConfigurationAsync()
        {
            try
            {
                var dir = Path.GetDirectoryName(_configPath);
                if (!string.IsNullOrEmpty(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                var config = new SyncConfiguration
                {
                    Jobs = _jobs.Values.ToList(),
                    History = _history.Take(100).ToList() // Keep last 100 entries
                };

                var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(_configPath, json);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save sync configuration");
            }
        }

        /// <inheritdoc />
        public async Task LoadConfigurationAsync()
        {
            try
            {
                if (File.Exists(_configPath))
                {
                    var json = await File.ReadAllTextAsync(_configPath);
                    var config = JsonSerializer.Deserialize<SyncConfiguration>(json);

                    if (config != null)
                    {
                        foreach (var job in config.Jobs)
                        {
                            _jobs[job.Id] = job;
                        }

                        _history.Clear();
                        _history.AddRange(config.History);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load sync configuration");
            }
        }

        private async Task<SyncPreview> AnalyzeSyncAsync(
            string source,
            string destination,
            SyncOptions options,
            CancellationToken cancellationToken)
        {
            var preview = new SyncPreview
            {
                FilesToCopy = new List<SyncFileAction>(),
                FilesToUpdate = new List<SyncFileAction>(),
                FilesToDelete = new List<SyncFileAction>(),
                Conflicts = new List<SyncFileAction>()
            };

            // Get all files in source
            var sourceFiles = await GetAllFilesAsync(source, options, cancellationToken);
            var destFiles = Directory.Exists(destination)
                ? await GetAllFilesAsync(destination, options, cancellationToken)
                : new Dictionary<string, FileInfo>();

            // Analyze each source file
            foreach (var kvp in sourceFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var relativePath = kvp.Key;
                var sourceFile = kvp.Value;

                if (destFiles.TryGetValue(relativePath, out var destFile))
                {
                    // File exists in destination
                    if (sourceFile.LastWriteTime > destFile.LastWriteTime)
                    {
                        preview.FilesToUpdate.Add(new SyncFileAction
                        {
                            RelativePath = relativePath,
                            SourcePath = sourceFile.FullName,
                            DestinationPath = destFile.FullName,
                            Action = SyncActionType.Update,
                            Size = sourceFile.Length,
                            SourceModified = sourceFile.LastWriteTime,
                            DestinationModified = destFile.LastWriteTime,
                            Reason = "Source is newer"
                        });
                        preview.TotalBytes += sourceFile.Length;
                    }
                    else if (destFile.LastWriteTime > sourceFile.LastWriteTime &&
                             options.Direction == SyncDirection.Bidirectional)
                    {
                        // Potential conflict in bidirectional mode
                        preview.Conflicts.Add(new SyncFileAction
                        {
                            RelativePath = relativePath,
                            SourcePath = sourceFile.FullName,
                            DestinationPath = destFile.FullName,
                            Action = SyncActionType.Conflict,
                            Size = Math.Max(sourceFile.Length, destFile.Length),
                            SourceModified = sourceFile.LastWriteTime,
                            DestinationModified = destFile.LastWriteTime,
                            Reason = "Destination is newer than source"
                        });
                    }
                }
                else
                {
                    // File doesn't exist in destination
                    preview.FilesToCopy.Add(new SyncFileAction
                    {
                        RelativePath = relativePath,
                        SourcePath = sourceFile.FullName,
                        DestinationPath = Path.Combine(destination, relativePath),
                        Action = SyncActionType.Copy,
                        Size = sourceFile.Length,
                        SourceModified = sourceFile.LastWriteTime,
                        Reason = "New file"
                    });
                    preview.TotalBytes += sourceFile.Length;
                }
            }

            // Check for files in destination that don't exist in source (for mirror/delete modes)
            if (options.Mode == SyncMode.Mirror || options.DeleteExtraFiles)
            {
                foreach (var kvp in destFiles)
                {
                    if (!sourceFiles.ContainsKey(kvp.Key))
                    {
                        preview.FilesToDelete.Add(new SyncFileAction
                        {
                            RelativePath = kvp.Key,
                            DestinationPath = kvp.Value.FullName,
                            Action = SyncActionType.Delete,
                            Size = kvp.Value.Length,
                            DestinationModified = kvp.Value.LastWriteTime,
                            Reason = "Not in source"
                        });
                    }
                }
            }

            return preview;
        }

        private async Task<SyncResult> ExecuteSyncAsync(
            string source,
            string destination,
            SyncPreview preview,
            IProgress<SyncProgress>? progress,
            string jobId,
            CancellationToken cancellationToken)
        {
            var result = new SyncResult
            {
                Success = true,
                Errors = new List<string>(),
                Warnings = new List<string>()
            };

            var totalOperations = preview.TotalFiles;
            var completedOperations = 0;
            var processedBytes = 0L;

            // Copy files
            foreach (var action in preview.FilesToCopy)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var destDir = Path.GetDirectoryName(action.DestinationPath);
                    if (!string.IsNullOrEmpty(destDir))
                    {
                        Directory.CreateDirectory(destDir);
                    }
                    File.Copy(action.SourcePath, action.DestinationPath, true);

                    result.FilesCopied++;
                    result.BytesTransferred += action.Size;
                    processedBytes += action.Size;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to copy file: {Path}", action.RelativePath);
                    result.Errors.Add($"Failed to copy {action.RelativePath}: {ex.Message}");
                    result.FilesFailed++;
                }

                completedOperations++;
                ReportProgress(progress, jobId, action.RelativePath, SyncActionType.Copy,
                    completedOperations, totalOperations, processedBytes, preview.TotalBytes);
            }

            // Update files
            foreach (var action in preview.FilesToUpdate)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    File.Copy(action.SourcePath, action.DestinationPath, true);

                    result.FilesUpdated++;
                    result.BytesTransferred += action.Size;
                    processedBytes += action.Size;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to update file: {Path}", action.RelativePath);
                    result.Errors.Add($"Failed to update {action.RelativePath}: {ex.Message}");
                    result.FilesFailed++;
                }

                completedOperations++;
                ReportProgress(progress, jobId, action.RelativePath, SyncActionType.Update,
                    completedOperations, totalOperations, processedBytes, preview.TotalBytes);
            }

            // Delete files
            foreach (var action in preview.FilesToDelete)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    File.Delete(action.DestinationPath);
                    result.FilesDeleted++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to delete file: {Path}", action.RelativePath);
                    result.Errors.Add($"Failed to delete {action.RelativePath}: {ex.Message}");
                    result.FilesFailed++;
                }

                completedOperations++;
                ReportProgress(progress, jobId, action.RelativePath, SyncActionType.Delete,
                    completedOperations, totalOperations, processedBytes, preview.TotalBytes);
            }

            // Handle conflicts (skip for now, add to warnings)
            foreach (var conflict in preview.Conflicts)
            {
                result.FilesSkipped++;
                result.Warnings.Add($"Conflict skipped: {conflict.RelativePath} - {conflict.Reason}");
            }

            result.Success = result.Errors.Count == 0;

            return result;
        }

        private void ReportProgress(
            IProgress<SyncProgress>? progress,
            string jobId,
            string currentFile,
            SyncActionType action,
            int processed,
            int total,
            long processedBytes,
            long totalBytes)
        {
            var syncProgress = new SyncProgress
            {
                CurrentFile = currentFile,
                TotalFiles = total,
                ProcessedFiles = processed,
                TotalBytes = totalBytes,
                ProcessedBytes = processedBytes,
                CurrentAction = action
            };

            progress?.Report(syncProgress);
            ProgressChanged?.Invoke(this, new SyncProgressEventArgs
            {
                JobId = jobId,
                Progress = syncProgress
            });
        }

        private async Task<Dictionary<string, FileInfo>> GetAllFilesAsync(
            string rootPath,
            SyncOptions options,
            CancellationToken cancellationToken)
        {
            var files = new Dictionary<string, FileInfo>(StringComparer.OrdinalIgnoreCase);

            await Task.Run(() =>
            {
                var searchOption = options.Recursive
                    ? SearchOption.AllDirectories
                    : SearchOption.TopDirectoryOnly;

                foreach (var file in Directory.GetFiles(rootPath, "*", searchOption))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var fileInfo = new FileInfo(file);

                    // Skip hidden files if not included
                    if (!options.IncludeHidden && (fileInfo.Attributes & FileAttributes.Hidden) != 0)
                        continue;

                    var relativePath = Path.GetRelativePath(rootPath, file);

                    // Check include/exclude patterns
                    if (options.ExcludePatterns != null &&
                        options.ExcludePatterns.Any(p => MatchesPattern(relativePath, p)))
                        continue;

                    if (options.IncludePatterns != null &&
                        !options.IncludePatterns.Any(p => MatchesPattern(relativePath, p)))
                        continue;

                    files[relativePath] = fileInfo;
                }
            }, cancellationToken);

            return files;
        }

        private static bool MatchesPattern(string path, string pattern)
        {
            // Simple wildcard matching
            var regexPattern = "^" + System.Text.RegularExpressions.Regex.Escape(pattern)
                .Replace(@"\*", ".*")
                .Replace(@"\?", ".") + "$";
            return System.Text.RegularExpressions.Regex.IsMatch(
                path, regexPattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }

        private DateTime? CalculateNextRunTime(SyncSchedule schedule)
        {
            var now = DateTime.UtcNow;

            return schedule.Type switch
            {
                ScheduleType.Manual => null,
                ScheduleType.Interval => now.Add(schedule.Interval),
                ScheduleType.Daily => now.Date.Add(schedule.TimeOfDay) > now
                    ? now.Date.Add(schedule.TimeOfDay)
                    : now.Date.AddDays(1).Add(schedule.TimeOfDay),
                ScheduleType.Weekly => CalculateNextWeeklyRun(now, schedule),
                ScheduleType.Monthly => CalculateNextMonthlyRun(now, schedule),
                _ => null
            };
        }

        private DateTime? CalculateNextWeeklyRun(DateTime now, SyncSchedule schedule)
        {
            if (schedule.DaysOfWeek == null || schedule.DaysOfWeek.Length == 0)
                return null;

            var sortedDays = schedule.DaysOfWeek.OrderBy(d => d).ToList();
            var todayTimeSlot = now.Date.Add(schedule.TimeOfDay);

            // Check if we can run today
            if (sortedDays.Contains(now.DayOfWeek) && todayTimeSlot > now)
            {
                return todayTimeSlot;
            }

            // Find next day
            for (int i = 1; i <= 7; i++)
            {
                var nextDay = now.Date.AddDays(i);
                if (sortedDays.Contains(nextDay.DayOfWeek))
                {
                    return nextDay.Add(schedule.TimeOfDay);
                }
            }

            return null;
        }

        private DateTime? CalculateNextMonthlyRun(DateTime now, SyncSchedule schedule)
        {
            var day = Math.Min(schedule.DayOfMonth, DateTime.DaysInMonth(now.Year, now.Month));
            var thisMonthSlot = new DateTime(now.Year, now.Month, day).Add(schedule.TimeOfDay);

            if (thisMonthSlot > now)
            {
                return thisMonthSlot;
            }

            // Next month
            var nextMonth = now.Month == 12
                ? new DateTime(now.Year + 1, 1, 1)
                : new DateTime(now.Year, now.Month + 1, 1);

            day = Math.Min(schedule.DayOfMonth, DateTime.DaysInMonth(nextMonth.Year, nextMonth.Month));
            return new DateTime(nextMonth.Year, nextMonth.Month, day).Add(schedule.TimeOfDay);
        }

        private SyncResult CreateErrorResult(string errorMessage, DateTime startTime)
        {
            return new SyncResult
            {
                Success = false,
                Duration = DateTime.UtcNow - startTime,
                Errors = new List<string> { errorMessage }
            };
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _syncSemaphore?.Dispose();

                foreach (var cts in _runningJobs.Values)
                {
                    cts.Cancel();
                    cts.Dispose();
                }
                _runningJobs.Clear();

                _disposed = true;
            }
        }
    }

    /// <summary>
    /// Sync configuration for persistence.
    /// </summary>
    internal class SyncConfiguration
    {
        public List<SyncJob> Jobs { get; set; } = new();
        public List<SyncHistoryEntry> History { get; set; } = new();
    }
}
