using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.FileManager.Interfaces;

namespace Winhance.Infrastructure.Features.FileManager.Services
{
    /// <summary>
    /// Fail-safe backup protection service for file operations.
    /// Provides multi-level backup protection with automatic rollback support.
    /// </summary>
    public class BackupProtectionService : IBackupProtectionService
    {
        private readonly ILogService _logService;
        private readonly string _backupBasePath;
        private readonly string _sessionIndexPath;
        private readonly ConcurrentDictionary<string, BackupSession> _activeSessions;
        private readonly SemaphoreSlim _sessionLock = new(1, 1);

        // Default settings - can be changed via properties
        private bool _isEnabled = true;
        private BackupProtectionLevel _protectionLevel = BackupProtectionLevel.Standard;

        public BackupProtectionService(ILogService logService)
        {
            _logService = logService;
            _activeSessions = new ConcurrentDictionary<string, BackupSession>();

            _backupBasePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Winhance", "FileManager", "Backups");

            _sessionIndexPath = Path.Combine(_backupBasePath, "sessions.json");

            Directory.CreateDirectory(_backupBasePath);
            LoadSessionIndex();
        }

        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                _isEnabled = value;
                _logService.Log(LogLevel.Info, $"Backup protection {(value ? "enabled" : "disabled")}");
            }
        }

        public BackupProtectionLevel ProtectionLevel
        {
            get => _protectionLevel;
            set
            {
                _protectionLevel = value;
                _logService.Log(LogLevel.Info, $"Backup protection level set to {value}");
            }
        }

        public async Task<BackupSession> CreateBackupAsync(
            FileOperationRequest operation,
            CancellationToken cancellationToken = default)
        {
            return await CreateBatchBackupAsync(new[] { operation }, cancellationToken).ConfigureAwait(false);
        }

        public async Task<BackupSession> CreateBatchBackupAsync(
            IEnumerable<FileOperationRequest> operations,
            CancellationToken cancellationToken = default)
        {
            var session = new BackupSession
            {
                ProtectionLevel = _protectionLevel,
                Description = $"Batch backup of {operations.Count()} operations",
            };

            if (!_isEnabled || _protectionLevel == BackupProtectionLevel.None)
            {
                session.Status = BackupSessionStatus.Committed;
                return session;
            }

            var sessionPath = Path.Combine(_backupBasePath, session.SessionId);
            Directory.CreateDirectory(sessionPath);
            session.BackupPath = sessionPath;

            try
            {
                foreach (var operation in operations)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (!File.Exists(operation.SourcePath) && !Directory.Exists(operation.SourcePath))
                    {
                        continue;
                    }

                    if (Directory.Exists(operation.SourcePath))
                    {
                        if (operation.Recursive)
                        {
                            await BackupDirectoryAsync(session, operation.SourcePath, sessionPath, cancellationToken).ConfigureAwait(false);
                        }
                    }
                    else
                    {
                        await BackupFileAsync(session, operation.SourcePath, sessionPath, cancellationToken).ConfigureAwait(false);
                    }
                }

                _activeSessions[session.SessionId] = session;
                await SaveSessionIndexAsync(cancellationToken).ConfigureAwait(false);

                _logService.Log(LogLevel.Info,
                    $"Backup session created: {session.SessionId} with {session.Files.Count} files ({FormatBytes(session.TotalSize)})");
            }
            catch (Exception ex)
            {
                session.Status = BackupSessionStatus.Failed;
                _logService.Log(LogLevel.Error, $"Backup creation failed: {ex.Message}");

                // Cleanup failed backup
                try
                {
                    if (Directory.Exists(sessionPath))
                    {
                        Directory.Delete(sessionPath, true);
                    }
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }

            return session;
        }

        public async Task<BackupRestoreResult> RestoreBackupAsync(
            string sessionId,
            CancellationToken cancellationToken = default)
        {
            var result = new BackupRestoreResult();
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            if (!_activeSessions.TryGetValue(sessionId, out var session))
            {
                result.Errors.Add($"Session not found: {sessionId}");
                return result;
            }

            try
            {
                foreach (var file in session.Files)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        if (!File.Exists(file.BackupPath))
                        {
                            result.FilesFailed++;
                            result.Errors.Add($"Backup file missing: {file.BackupPath}");
                            continue;
                        }

                        var destDir = Path.GetDirectoryName(file.OriginalPath);
                        if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
                        {
                            Directory.CreateDirectory(destDir);
                        }

                        if (_protectionLevel >= BackupProtectionLevel.Enhanced)
                        {
                            // Decompress backup
                            await DecompressFileAsync(file.BackupPath, file.OriginalPath, cancellationToken).ConfigureAwait(false);
                        }
                        else
                        {
                            // Direct copy
                            await CopyFileAsync(file.BackupPath, file.OriginalPath, cancellationToken).ConfigureAwait(false);
                        }

                        // Restore attributes
                        File.SetAttributes(file.OriginalPath, file.Attributes);
                        File.SetLastWriteTime(file.OriginalPath, file.LastModified);

                        result.FilesRestored++;
                        result.BytesRestored += file.Size;
                    }
                    catch (Exception ex)
                    {
                        result.FilesFailed++;
                        result.Errors.Add($"Failed to restore {file.OriginalPath}: {ex.Message}");
                    }
                }

                if (result.FilesFailed == 0)
                {
                    session.Status = BackupSessionStatus.RolledBack;
                    result.Success = true;
                    _logService.Log(LogLevel.Info, $"Backup restored: {result.FilesRestored} files");
                }
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Restore failed: {ex.Message}");
                _logService.Log(LogLevel.Error, $"Backup restore failed: {ex.Message}");
            }

            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;
            return result;
        }

        public async Task<BackupRestoreResult> RestoreFileAsync(
            string sessionId,
            string filePath,
            CancellationToken cancellationToken = default)
        {
            var result = new BackupRestoreResult();

            if (!_activeSessions.TryGetValue(sessionId, out var session))
            {
                result.Errors.Add($"Session not found: {sessionId}");
                return result;
            }

            var fileEntry = session.Files.FirstOrDefault(f =>
                f.OriginalPath.Equals(filePath, StringComparison.OrdinalIgnoreCase));

            if (fileEntry == null)
            {
                result.Errors.Add($"File not found in backup: {filePath}");
                return result;
            }

            try
            {
                var destDir = Path.GetDirectoryName(fileEntry.OriginalPath);
                if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
                {
                    Directory.CreateDirectory(destDir);
                }

                if (_protectionLevel >= BackupProtectionLevel.Enhanced)
                {
                    await DecompressFileAsync(fileEntry.BackupPath, fileEntry.OriginalPath, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    await CopyFileAsync(fileEntry.BackupPath, fileEntry.OriginalPath, cancellationToken).ConfigureAwait(false);
                }

                File.SetAttributes(fileEntry.OriginalPath, fileEntry.Attributes);
                File.SetLastWriteTime(fileEntry.OriginalPath, fileEntry.LastModified);

                result.FilesRestored = 1;
                result.BytesRestored = fileEntry.Size;
                result.Success = true;

                _logService.Log(LogLevel.Info, $"File restored: {filePath}");
            }
            catch (Exception ex)
            {
                result.FilesFailed = 1;
                result.Errors.Add($"Failed to restore: {ex.Message}");
            }

            return result;
        }

        public async Task CommitBackupAsync(string sessionId, CancellationToken cancellationToken = default)
        {
            if (!_activeSessions.TryGetValue(sessionId, out var session))
            {
                return;
            }

            try
            {
                // Delete backup files
                if (Directory.Exists(session.BackupPath))
                {
                    await Task.Run(() => Directory.Delete(session.BackupPath, true), cancellationToken).ConfigureAwait(false);
                }

                session.Status = BackupSessionStatus.Committed;
                _activeSessions.TryRemove(sessionId, out _);
                await SaveSessionIndexAsync(cancellationToken).ConfigureAwait(false);

                _logService.Log(LogLevel.Debug, $"Backup session committed and cleaned: {sessionId}");
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Warning, $"Failed to cleanup backup: {ex.Message}");
            }
        }

        public async Task<BackupRestoreResult> RollbackAsync(
            string sessionId,
            CancellationToken cancellationToken = default)
        {
            return await RestoreBackupAsync(sessionId, cancellationToken).ConfigureAwait(false);
        }

        public Task<IEnumerable<BackupSession>> GetActiveSessionsAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_activeSessions.Values.Where(s => s.Status == BackupSessionStatus.Active).AsEnumerable());
        }

        public async Task<IEnumerable<BackupHistoryEntry>> GetFileHistoryAsync(
            string filePath,
            CancellationToken cancellationToken = default)
        {
            var history = new List<BackupHistoryEntry>();

            await Task.Run(
                () =>
            {
                foreach (var session in _activeSessions.Values)
                {
                    var fileEntry = session.Files.FirstOrDefault(f =>
                        f.OriginalPath.Equals(filePath, StringComparison.OrdinalIgnoreCase));

                    if (fileEntry != null)
                    {
                        history.Add(new BackupHistoryEntry
                        {
                            SessionId = session.SessionId,
                            FilePath = fileEntry.OriginalPath,
                            Timestamp = session.CreatedAt,
                            Size = fileEntry.Size,
                            IsAvailable = File.Exists(fileEntry.BackupPath),
                        });
                    }
                }
            }, cancellationToken);

            return history.OrderByDescending(h => h.Timestamp);
        }

        public async Task<BackupCleanupResult> CleanupOldBackupsAsync(
            int retentionDays = 7,
            CancellationToken cancellationToken = default)
        {
            var result = new BackupCleanupResult();
            var cutoffDate = DateTime.UtcNow.AddDays(-retentionDays);

            var sessionsToRemove = _activeSessions
                .Where(kvp => kvp.Value.CreatedAt < cutoffDate &&
                              kvp.Value.Status != BackupSessionStatus.Active)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var sessionId in sessionsToRemove)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (_activeSessions.TryRemove(sessionId, out var session))
                {
                    try
                    {
                        if (Directory.Exists(session.BackupPath))
                        {
                            var files = Directory.GetFiles(session.BackupPath, "*", SearchOption.AllDirectories);
                            result.FilesDeleted += files.Length;
                            result.BytesFreed += files.Sum(f => new FileInfo(f).Length);

                            await Task.Run(() => Directory.Delete(session.BackupPath, true), cancellationToken).ConfigureAwait(false);
                        }

                        result.SessionsCleaned++;
                    }
                    catch (Exception ex)
                    {
                        result.Errors.Add($"Failed to cleanup session {sessionId}: {ex.Message}");
                    }
                }
            }

            await SaveSessionIndexAsync(cancellationToken).ConfigureAwait(false);

            _logService.Log(LogLevel.Info,
                $"Backup cleanup: {result.SessionsCleaned} sessions, {FormatBytes(result.BytesFreed)} freed");

            return result;
        }

        public async Task<BackupVerificationResult> VerifyBackupAsync(
            string sessionId,
            CancellationToken cancellationToken = default)
        {
            var result = new BackupVerificationResult { IsValid = true };

            if (!_activeSessions.TryGetValue(sessionId, out var session))
            {
                result.IsValid = false;
                return result;
            }

            foreach (var file in session.Files)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!File.Exists(file.BackupPath))
                {
                    result.MissingFiles++;
                    result.IsValid = false;
                    continue;
                }

                // Verify hash
                var currentHash = await ComputeFileHashAsync(file.BackupPath, cancellationToken).ConfigureAwait(false);
                if (!currentHash.Equals(file.Hash, StringComparison.OrdinalIgnoreCase))
                {
                    result.CorruptedFiles++;
                    result.CorruptedPaths.Add(file.OriginalPath);
                    result.IsValid = false;
                }
                else
                {
                    result.ValidFiles++;
                }
            }

            return result;
        }

        public async Task<BackupStorageInfo> GetStorageInfoAsync(CancellationToken cancellationToken = default)
        {
            var info = new BackupStorageInfo
            {
                StoragePath = _backupBasePath,
            };

            await Task.Run(
                () =>
            {
                if (Directory.Exists(_backupBasePath))
                {
                    var files = Directory.GetFiles(_backupBasePath, "*", SearchOption.AllDirectories);
                    info.TotalUsed = files.Sum(f =>
                    {
                        try
                        {
                            return new FileInfo(f).Length;
                        }
                        catch
                        {
                            return 0;
                        }
                    });
                    info.TotalFiles = files.Length;
                }

                info.ActiveSessions = _activeSessions.Count(s => s.Value.Status == BackupSessionStatus.Active);

                if (_activeSessions.Any())
                {
                    info.OldestBackup = _activeSessions.Values.Min(s => s.CreatedAt);
                    info.NewestBackup = _activeSessions.Values.Max(s => s.CreatedAt);
                }
            }, cancellationToken);

            return info;
        }

        private async Task BackupFileAsync(
            BackupSession session,
            string sourcePath,
            string sessionPath,
            CancellationToken cancellationToken)
        {
            var fileInfo = new FileInfo(sourcePath);
            var backupFileName = $"{Guid.NewGuid():N}{(_protectionLevel >= BackupProtectionLevel.Enhanced ? ".gz" : string.Empty)}";
            var backupPath = Path.Combine(sessionPath, backupFileName);

            var entry = new BackupFileEntry
            {
                OriginalPath = sourcePath,
                BackupPath = backupPath,
                Size = fileInfo.Length,
                LastModified = fileInfo.LastWriteTime,
                Attributes = fileInfo.Attributes,
            };

            if (_protectionLevel >= BackupProtectionLevel.Enhanced)
            {
                // Compress backup
                await CompressFileAsync(sourcePath, backupPath, cancellationToken).ConfigureAwait(false);
                entry.Hash = await ComputeFileHashAsync(backupPath, cancellationToken).ConfigureAwait(false);
                session.CompressedSize += new FileInfo(backupPath).Length;
            }
            else
            {
                // Direct copy
                await CopyFileAsync(sourcePath, backupPath, cancellationToken).ConfigureAwait(false);
                entry.Hash = await ComputeFileHashAsync(backupPath, cancellationToken).ConfigureAwait(false);
            }

            session.Files.Add(entry);
            session.TotalSize += fileInfo.Length;
        }

        private async Task BackupDirectoryAsync(
            BackupSession session,
            string sourcePath,
            string sessionPath,
            CancellationToken cancellationToken)
        {
            foreach (var file in Directory.EnumerateFiles(sourcePath, "*", SearchOption.AllDirectories))
            {
                cancellationToken.ThrowIfCancellationRequested();
                await BackupFileAsync(session, file, sessionPath, cancellationToken).ConfigureAwait(false);
            }
        }

        private static async Task CopyFileAsync(string source, string destination, CancellationToken cancellationToken)
        {
            const int bufferSize = 4 * 1024 * 1024; // 4MB buffer
            await using var sourceStream = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan);
            await using var destStream = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan);
            await sourceStream.CopyToAsync(destStream, bufferSize, cancellationToken).ConfigureAwait(false);
        }

        private static async Task CompressFileAsync(string source, string destination, CancellationToken cancellationToken)
        {
            await using var sourceStream = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read, 4 * 1024 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
            await using var destStream = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None, 4 * 1024 * 1024, FileOptions.Asynchronous);
            await using var gzipStream = new GZipStream(destStream, CompressionLevel.Fastest);
            await sourceStream.CopyToAsync(gzipStream, 4 * 1024 * 1024, cancellationToken).ConfigureAwait(false);
        }

        private static async Task DecompressFileAsync(string source, string destination, CancellationToken cancellationToken)
        {
            await using var sourceStream = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read, 4 * 1024 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
            await using var gzipStream = new GZipStream(sourceStream, CompressionMode.Decompress);
            await using var destStream = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None, 4 * 1024 * 1024, FileOptions.Asynchronous);
            await gzipStream.CopyToAsync(destStream, 4 * 1024 * 1024, cancellationToken).ConfigureAwait(false);
        }

        private static async Task<string> ComputeFileHashAsync(string filePath, CancellationToken cancellationToken)
        {
            await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4 * 1024 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
            using var sha256 = SHA256.Create();
            var hash = await sha256.ComputeHashAsync(stream, cancellationToken).ConfigureAwait(false);
            return BitConverter.ToString(hash).Replace("-", string.Empty, StringComparison.Ordinal).ToLowerInvariant();
        }

        private void LoadSessionIndex()
        {
            try
            {
                if (File.Exists(_sessionIndexPath))
                {
                    var json = File.ReadAllText(_sessionIndexPath);
                    var sessions = JsonSerializer.Deserialize<List<BackupSession>>(json);
                    if (sessions != null)
                    {
                        foreach (var session in sessions)
                        {
                            _activeSessions[session.SessionId] = session;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Warning, $"Failed to load backup session index: {ex.Message}");
            }
        }

        private async Task SaveSessionIndexAsync(CancellationToken cancellationToken)
        {
            await _sessionLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var json = JsonSerializer.Serialize(
                    _activeSessions.Values.ToList(),
                    new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(_sessionIndexPath, json, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Warning, $"Failed to save backup session index: {ex.Message}");
            }
            finally
            {
                _sessionLock.Release();
            }
        }

        private static string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }

            return $"{len:0.##} {sizes[order]}";
        }
    }
}
