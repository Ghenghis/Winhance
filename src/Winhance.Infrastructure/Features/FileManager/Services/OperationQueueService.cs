using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.FileManager.Interfaces;

namespace Winhance.Infrastructure.Features.FileManager.Services
{
    /// <summary>
    /// Service for managing background file operations with progress tracking and conflict resolution.
    /// </summary>
    public class OperationQueueService : IOperationQueueService, INotifyPropertyChanged, IDisposable
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        private readonly ILogService _logService;
        private readonly ConcurrentQueue<FileOperation> _operationHistory = new();
        private readonly SemaphoreSlim _operationSemaphore;
        private readonly CancellationTokenSource _cancellationTokenSource = new();
        private readonly Timer _speedTimer;
        private OperationSettings _settings = new();
        private FileOperation? _currentOperation;
        private Task? _processorTask;
        private bool _disposed;

        public OperationQueueService(ILogService logService)
        {
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
            _operationSemaphore = new SemaphoreSlim(1, 1);
            
            // Start the processor task
            _processorTask = Task.Run(ProcessQueueAsync, _cancellationTokenSource.Token);
            
            // Start speed monitoring timer
            _speedTimer = new Timer(UpdateSpeed, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
        }

        public ObservableCollection<FileOperation> Operations { get; } = new();

        public FileOperation? CurrentOperation
        {
            get => _currentOperation;
            private set
            {
                if (_currentOperation != value)
                {
                    _currentOperation = value;
                    OnPropertyChanged();
                }
            }
        }

        public event EventHandler<OperationProgressEventArgs>? ProgressChanged;
        public event EventHandler<OperationCompletedEventArgs>? OperationCompleted;

        public FileOperation QueueCopy(IEnumerable<string> sources, string destination)
        {
            if (sources == null)
                throw new ArgumentNullException(nameof(sources));
            if (string.IsNullOrWhiteSpace(destination))
                throw new ArgumentException("Destination cannot be empty", nameof(destination));

            var operation = new FileOperation
            {
                Type = OperationType.Copy,
                SourcePaths = sources.ToList(),
                DestinationPath = destination,
                Status = OperationStatus.Queued,
                StartedAt = DateTime.UtcNow
            };

            // Calculate total size
            operation.TotalBytes = CalculateTotalSize(sources);
            operation.TotalFiles = CountFiles(sources);

            // Insert in priority order
            InsertOperationByPriority(operation);

            _logService.Log(LogLevel.Info, $"Queued copy operation: {operation.TotalFiles} files to {destination}");
            return operation;
        }

        public FileOperation QueueMove(IEnumerable<string> sources, string destination)
        {
            if (sources == null)
                throw new ArgumentNullException(nameof(sources));
            if (string.IsNullOrWhiteSpace(destination))
                throw new ArgumentException("Destination cannot be empty", nameof(destination));

            var operation = new FileOperation
            {
                Type = OperationType.Move,
                SourcePaths = sources.ToList(),
                DestinationPath = destination,
                Status = OperationStatus.Queued,
                StartedAt = DateTime.UtcNow
            };

            // Calculate total size
            operation.TotalBytes = CalculateTotalSize(sources);
            operation.TotalFiles = CountFiles(sources);

            // Insert in priority order
            InsertOperationByPriority(operation);

            _logService.Log(LogLevel.Info, $"Queued move operation: {operation.TotalFiles} files to {destination}");
            return operation;
        }

        public FileOperation QueueDelete(IEnumerable<string> paths, bool permanent = false)
        {
            if (paths == null)
                throw new ArgumentNullException(nameof(paths));

            var operation = new FileOperation
            {
                Type = OperationType.Delete,
                SourcePaths = paths.ToList(),
                Status = OperationStatus.Queued,
                StartedAt = DateTime.UtcNow,
                TagData = new Dictionary<string, object> { ["Permanent"] = permanent }
            };

            // Calculate total size
            operation.TotalBytes = CalculateTotalSize(paths);
            operation.TotalFiles = CountFiles(paths);

            // Insert in priority order
            InsertOperationByPriority(operation);

            _logService.Log(LogLevel.Info, $"Queued delete operation: {operation.TotalFiles} files (permanent={permanent})");
            return operation;
        }

        public void Pause(FileOperation operation)
        {
            if (operation == null || operation.Status != OperationStatus.Running)
                return;

            operation.Status = OperationStatus.Paused;
            _logService.Log(LogLevel.Info, $"Paused operation {operation.Id}");
        }

        public void Resume(FileOperation operation)
        {
            if (operation == null || operation.Status != OperationStatus.Paused)
                return;

            operation.Status = OperationStatus.Queued;
            _logService.Log(LogLevel.Info, $"Resumed operation {operation.Id}");
        }

        public void Cancel(FileOperation operation)
        {
            if (operation == null)
                return;

            if (operation == CurrentOperation)
            {
                // Cancel current operation
                operation.Status = OperationStatus.Cancelled;
                operation.CompletedAt = DateTime.UtcNow;
                CurrentOperation = null;
            }
            else if (operation.Status == OperationStatus.Queued)
            {
                // Remove from queue
                Operations.Remove(operation);
                operation.Status = OperationStatus.Cancelled;
                operation.CompletedAt = DateTime.UtcNow;
            }

            _logService.Log(LogLevel.Info, $"Cancelled operation {operation.Id}");
        }

        public void ChangePriority(FileOperation operation, int newPosition)
        {
            if (operation == null || newPosition < 0 || newPosition >= Operations.Count)
                return;

            var oldIndex = Operations.IndexOf(operation);
            if (oldIndex == newPosition)
                return;

            Operations.RemoveAt(oldIndex);
            Operations.Insert(newPosition, operation);
            operation.Priority = newPosition;

            _logService.Log(LogLevel.Debug, $"Changed priority of operation {operation.Id} to {newPosition}");
        }

        public void Retry(FileOperation operation)
        {
            if (operation == null || (operation.Status != OperationStatus.Failed && operation.Status != OperationStatus.Cancelled))
                return;

            // Reset operation state
            operation.Status = OperationStatus.Queued;
            operation.ProcessedBytes = 0;
            operation.ProcessedFiles = 0;
            operation.CurrentFile = string.Empty;
            operation.ErrorMessage = null;
            operation.PendingConflict = null;
            operation.CompletedAt = null;
            operation.StartedAt = DateTime.UtcNow;

            _logService.Log(LogLevel.Info, $"Retrying operation {operation.Id}");
        }

        public void ResolveConflict(FileOperation operation, ConflictResolution resolution)
        {
            if (operation == null || operation.Status != OperationStatus.Conflict)
                return;

            operation.PendingConflict = null;
            operation.Status = OperationStatus.Queued;

            _logService.Log(LogLevel.Info, $"Resolved conflict for operation {operation.Id} with {resolution}");
        }

        public IEnumerable<FileOperation> GetHistory(int count = 50)
        {
            return _operationHistory
                .OrderByDescending(o => o.CompletedAt)
                .Take(count);
        }

        public void ClearHistory()
        {
            _operationHistory.Clear();
            _logService.Log(LogLevel.Info, "Cleared operation history");
        }

        public IEnumerable<FileOperation> GetOperationsByStatus(OperationStatus status)
        {
            return Operations.Where(o => o.Status == status);
        }

        public TimeSpan? GetEstimatedTimeRemaining(FileOperation operation)
        {
            if (operation == null || operation.SpeedBytesPerSecond <= 0)
                return null;

            var remainingBytes = operation.TotalBytes - operation.ProcessedBytes;
            return TimeSpan.FromSeconds(remainingBytes / operation.SpeedBytesPerSecond);
        }

        public OperationStatistics GetStatistics()
        {
            var allOperations = Operations.Concat(_operationHistory);
            
            return new OperationStatistics
            {
                TotalOperations = allOperations.Count(),
                QueuedOperations = Operations.Count(o => o.Status == OperationStatus.Queued),
                RunningOperations = Operations.Count(o => o.Status == OperationStatus.Running),
                CompletedOperations = allOperations.Count(o => o.Status == OperationStatus.Completed),
                FailedOperations = allOperations.Count(o => o.Status == OperationStatus.Failed),
                TotalBytesTransferred = allOperations.Where(o => o.Status == OperationStatus.Completed).Sum(o => o.TotalBytes),
                AverageSpeedBytesPerSecond = CalculateAverageSpeed(allOperations),
                TotalOperationTime = TimeSpan.FromTicks(allOperations.Where(o => o.CompletedAt.HasValue).Sum(o => (o.CompletedAt!.Value - o.StartedAt).Ticks))
            };
        }

        public void PauseAll()
        {
            foreach (var operation in Operations.Where(o => o.Status == OperationStatus.Running))
            {
                Pause(operation);
            }
        }

        public void ResumeAll()
        {
            foreach (var operation in Operations.Where(o => o.Status == OperationStatus.Paused))
            {
                Resume(operation);
            }
        }

        public void CancelAll()
        {
            // Cancel current operation
            if (CurrentOperation != null)
            {
                Cancel(CurrentOperation);
            }

            // Cancel all queued operations
            foreach (var operation in Operations.Where(o => o.Status == OperationStatus.Queued).ToList())
            {
                Cancel(operation);
            }
        }

        public void SetSettings(OperationSettings settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _logService.Log(LogLevel.Info, "Updated operation settings");
        }

        public OperationSettings GetSettings()
        {
            return _settings;
        }

        private async Task ProcessQueueAsync()
        {
            while (!_cancellationTokenSource.Token.IsCancellationRequested)
            {
                await _operationSemaphore.WaitAsync(_cancellationTokenSource.Token);
                
                try
                {
                    // Find next operation to process
                    var operation = Operations.FirstOrDefault(o => o.Status == OperationStatus.Queued);
                    
                    if (operation != null)
                    {
                        CurrentOperation = operation;
                        operation.Status = OperationStatus.Running;
                        
                        await ExecuteOperationAsync(operation);
                        
                        // Move to history
                        Operations.Remove(operation);
                        _operationHistory.Enqueue(operation);
                        
                        // Limit history size
                        while (_operationHistory.Count > _settings.MaxHistorySize)
                        {
                            _operationHistory.TryDequeue(out _);
                        }
                        
                        CurrentOperation = null;
                    }
                    else
                    {
                        // No operations to process, wait a bit
                        await Task.Delay(100, _cancellationTokenSource.Token);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logService.Log(LogLevel.Error, $"Error processing operations: {ex.Message}");
                }
                finally
                {
                    _operationSemaphore.Release();
                }
            }
        }

        private async Task ExecuteOperationAsync(FileOperation operation)
        {
            var stopwatch = Stopwatch.StartNew();
            var lastProgressUpdate = DateTime.UtcNow;
            
            try
            {
                switch (operation.Type)
                {
                    case OperationType.Copy:
                        await ExecuteCopyAsync(operation);
                        break;
                    case OperationType.Move:
                        await ExecuteMoveAsync(operation);
                        break;
                    case OperationType.Delete:
                        await ExecuteDeleteAsync(operation);
                        break;
                }

                operation.Status = OperationStatus.Completed;
                operation.CompletedAt = DateTime.UtcNow;
                
                OperationCompleted?.Invoke(this, new OperationCompletedEventArgs
                {
                    Operation = operation,
                    Success = true,
                    FilesProcessed = operation.ProcessedFiles,
                    FilesFailed = 0
                });
                
                _logService.Log(LogLevel.Info, $"Completed operation {operation.Id} in {stopwatch.ElapsedMilliseconds}ms");
            }
            catch (Exception ex)
            {
                operation.Status = OperationStatus.Failed;
                operation.ErrorMessage = ex.Message;
                operation.CompletedAt = DateTime.UtcNow;
                
                OperationCompleted?.Invoke(this, new OperationCompletedEventArgs
                {
                    Operation = operation,
                    Success = false,
                    FilesProcessed = operation.ProcessedFiles,
                    FilesFailed = operation.TotalFiles - operation.ProcessedFiles
                });
                
                _logService.Log(LogLevel.Error, $"Operation {operation.Id} failed: {ex.Message}");
            }
        }

        private async Task ExecuteCopyAsync(FileOperation operation)
        {
            foreach (var source in operation.SourcePaths)
            {
                if (operation.Status == OperationStatus.Cancelled)
                    break;
                    
                if (Directory.Exists(source))
                {
                    await CopyDirectoryAsync(source, operation.DestinationPath!, operation);
                }
                else if (File.Exists(source))
                {
                    await CopyFileAsync(source, Path.Combine(operation.DestinationPath!, Path.GetFileName(source)), operation);
                }
            }
        }

        private async Task ExecuteMoveAsync(FileOperation operation)
        {
            foreach (var source in operation.SourcePaths)
            {
                if (operation.Status == OperationStatus.Cancelled)
                    break;
                    
                var destPath = Path.Combine(operation.DestinationPath!, Path.GetFileName(source));
                
                if (Directory.Exists(source))
                {
                    if (Directory.Exists(destPath))
                    {
                        // Handle conflict
                        await HandleConflictAsync(source, destPath, operation);
                    }
                    
                    if (operation.Status != OperationStatus.Cancelled)
                    {
                        Directory.Move(source, destPath);
                        operation.ProcessedFiles++;
                    }
                }
                else if (File.Exists(source))
                {
                    if (File.Exists(destPath))
                    {
                        // Handle conflict
                        await HandleConflictAsync(source, destPath, operation);
                    }
                    
                    if (operation.Status != OperationStatus.Cancelled)
                    {
                        File.Move(source, destPath);
                        operation.ProcessedBytes += new FileInfo(source).Length;
                        operation.ProcessedFiles++;
                    }
                }
                
                UpdateProgress(operation);
            }
        }

        private async Task ExecuteDeleteAsync(FileOperation operation)
        {
            var permanent = operation.TagData?.ContainsKey("Permanent") == true && 
                           (bool)operation.TagData!["Permanent"];
            
            foreach (var path in operation.SourcePaths)
            {
                if (operation.Status == OperationStatus.Cancelled)
                    break;
                    
                operation.CurrentFile = path;
                
                try
                {
                    if (Directory.Exists(path))
                    {
                        if (permanent)
                        {
                            Directory.Delete(path, true);
                        }
                        else
                        {
                            // Send to recycle bin (requires shell API)
                            await SendToRecycleBinAsync(path);
                        }
                    }
                    else if (File.Exists(path))
                    {
                        if (permanent)
                        {
                            File.Delete(path);
                        }
                        else
                        {
                            await SendToRecycleBinAsync(path);
                        }
                    }
                    
                    operation.ProcessedFiles++;
                    UpdateProgress(operation);
                }
                catch (Exception ex)
                {
                    _logService.Log(LogLevel.Warning, $"Failed to delete {path}: {ex.Message}");
                }
            }
        }

        private async Task CopyDirectoryAsync(string source, string destination, FileOperation operation)
        {
            var sourceDir = new DirectoryInfo(source);
            var destDir = Directory.CreateDirectory(destination);
            
            // Copy directory attributes
            if (_settings.PreserveAttributes)
            {
                destDir.Attributes = sourceDir.Attributes;
            }
            
            // Copy files
            foreach (var file in sourceDir.GetFiles())
            {
                if (operation.Status == OperationStatus.Cancelled)
                    break;
                    
                await CopyFileAsync(file.FullName, Path.Combine(destination, file.Name), operation);
            }
            
            // Copy subdirectories
            foreach (var dir in sourceDir.GetDirectories())
            {
                if (operation.Status == OperationStatus.Cancelled)
                    break;
                    
                await CopyDirectoryAsync(dir.FullName, Path.Combine(destination, dir.Name), operation);
            }
        }

        private async Task CopyFileAsync(string source, string destination, FileOperation operation)
        {
            operation.CurrentFile = source;
            
            // Ensure destination directory exists
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            
            // Check for conflicts
            if (File.Exists(destination))
            {
                await HandleConflictAsync(source, destination, operation);
                
                if (operation.Status == OperationStatus.Cancelled)
                    return;
            }
            
            // Copy file
            var buffer = new byte[_settings.BufferSize];
            long totalBytes = 0;
            var fileInfo = new FileInfo(source);
            
            using (var sourceStream = File.OpenRead(source))
            using (var destStream = File.Create(destination))
            {
                int bytesRead;
                while ((bytesRead = await sourceStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    if (operation.Status == OperationStatus.Cancelled || operation.Status == OperationStatus.Paused)
                    {
                        if (operation.Status == OperationStatus.Paused)
                        {
                            await WaitForResumeAsync(operation);
                        }
                        else
                        {
                            break;
                        }
                    }
                    
                    await destStream.WriteAsync(buffer, 0, bytesRead);
                    totalBytes += bytesRead;
                    operation.ProcessedBytes += bytesRead;
                    
                    UpdateProgress(operation);
                }
            }
            
            // Preserve timestamps and attributes
            if (_settings.PreserveTimestamps)
            {
                File.SetCreationTime(destination, fileInfo.CreationTime);
                File.SetLastWriteTime(destination, fileInfo.LastWriteTime);
                File.SetLastAccessTime(destination, fileInfo.LastAccessTime);
            }
            
            if (_settings.PreserveAttributes)
            {
                File.SetAttributes(destination, fileInfo.Attributes);
            }
            
            // Verify if enabled
            if (_settings.VerifyAfterCopy)
            {
                await VerifyFileAsync(source, destination);
            }
            
            operation.ProcessedFiles++;
        }

        private async Task HandleConflictAsync(string source, string destination, FileOperation operation)
        {
            var sourceInfo = new FileInfo(source);
            var destInfo = new FileInfo(destination);
            
            operation.PendingConflict = new ConflictInfo
            {
                SourcePath = source,
                DestinationPath = destination,
                SourceSize = sourceInfo.Length,
                DestinationSize = destInfo.Length,
                SourceModified = sourceInfo.LastWriteTime,
                DestinationModified = destInfo.LastWriteTime,
                ConflictType = ConflictType.FileExists,
                RecommendedResolution = sourceInfo.LastWriteTime > destInfo.LastWriteTime 
                    ? ConflictResolution.Overwrite 
                    : ConflictResolution.Skip
            };
            
            operation.Status = OperationStatus.Conflict;
            
            // Wait for conflict resolution
            while (operation.Status == OperationStatus.Conflict && operation.Status != OperationStatus.Cancelled)
            {
                await Task.Delay(100);
            }
        }

        private async Task VerifyFileAsync(string source, string destination)
        {
            // Simple hash verification
            using var sourceStream = File.OpenRead(source);
            using var destStream = File.OpenRead(destination);
            
            var sourceHash = await ComputeHashAsync(sourceStream);
            var destHash = await ComputeHashAsync(destStream);
            
            if (!sourceHash.SequenceEqual(destHash))
            {
                throw new IOException("File verification failed - hashes do not match");
            }
        }

        private async Task<byte[]> ComputeHashAsync(Stream stream)
        {
            using var sha = System.Security.Cryptography.SHA256.Create();
            return await sha.ComputeHashAsync(stream);
        }

        private async Task SendToRecycleBinAsync(string path)
        {
            // This would use the Windows Shell API to send files to recycle bin
            // For now, just delete permanently
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
            else if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        private async Task WaitForResumeAsync(FileOperation operation)
        {
            while (operation.Status == OperationStatus.Paused && operation.Status != OperationStatus.Cancelled)
            {
                await Task.Delay(100);
            }
        }

        private void UpdateProgress(FileOperation operation)
        {
            var now = DateTime.UtcNow;
            if ((now - operation.StartedAt).TotalSeconds > 0)
            {
                operation.SpeedBytesPerSecond = operation.ProcessedBytes / (now - operation.StartedAt).TotalSeconds;
            }
            
            operation.EstimatedRemaining = GetEstimatedTimeRemaining(operation) ?? TimeSpan.Zero;
            
            ProgressChanged?.Invoke(this, new OperationProgressEventArgs { Operation = operation });
        }

        private void UpdateSpeed(object? state)
        {
            if (CurrentOperation != null && CurrentOperation.Status == OperationStatus.Running)
            {
                UpdateProgress(CurrentOperation);
            }
        }

        private void InsertOperationByPriority(FileOperation operation)
        {
            int insertIndex = 0;
            for (int i = 0; i < Operations.Count; i++)
            {
                if (Operations[i].Priority < operation.Priority)
                {
                    insertIndex = i;
                    break;
                }
                insertIndex = i + 1;
            }
            
            Operations.Insert(insertIndex, operation);
            
            // Update priorities
            for (int i = 0; i < Operations.Count; i++)
            {
                Operations[i].Priority = i;
            }
        }

        private static long CalculateTotalSize(IEnumerable<string> paths)
        {
            long total = 0;
            foreach (var path in paths)
            {
                try
                {
                    if (Directory.Exists(path))
                    {
                        total += CalculateDirectorySize(path);
                    }
                    else if (File.Exists(path))
                    {
                        total += new FileInfo(path).Length;
                    }
                }
                catch
                {
                    // Skip inaccessible files
                }
            }
            return total;
        }

        private static long CalculateDirectorySize(string path)
        {
            long size = 0;
            try
            {
                var dir = new DirectoryInfo(path);
                
                foreach (var file in dir.GetFiles())
                {
                    size += file.Length;
                }
                
                foreach (var subDir in dir.GetDirectories())
                {
                    size += CalculateDirectorySize(subDir.FullName);
                }
            }
            catch
            {
                // Skip inaccessible directories
            }
            return size;
        }

        private static int CountFiles(IEnumerable<string> paths)
        {
            int count = 0;
            foreach (var path in paths)
            {
                try
                {
                    if (Directory.Exists(path))
                    {
                        count += CountFilesInDirectory(path);
                    }
                    else if (File.Exists(path))
                    {
                        count++;
                    }
                }
                catch
                {
                    // Skip inaccessible files
                }
            }
            return count;
        }

        private static int CountFilesInDirectory(string path)
        {
            int count = 0;
            try
            {
                var dir = new DirectoryInfo(path);
                count += dir.GetFiles().Length;
                
                foreach (var subDir in dir.GetDirectories())
                {
                    count += CountFilesInDirectory(subDir.FullName);
                }
            }
            catch
            {
                // Skip inaccessible directories
            }
            return count;
        }

        private static double CalculateAverageSpeed(IEnumerable<FileOperation> operations)
        {
            var completedOps = operations.Where(o => o.Status == OperationStatus.Completed && o.CompletedAt.HasValue);
            if (!completedOps.Any())
                return 0;
                
            return completedOps.Average(o => 
            {
                var duration = (o.CompletedAt!.Value - o.StartedAt).TotalSeconds;
                return duration > 0 ? o.TotalBytes / duration : 0;
            });
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _cancellationTokenSource.Cancel();
                _speedTimer?.Dispose();
                _operationSemaphore?.Dispose();
                
                if (_processorTask != null && !_processorTask.IsCompleted)
                {
                    _processorTask.Wait();
                }
                
                _disposed = true;
            }
        }
    }
}
