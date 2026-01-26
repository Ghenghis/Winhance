using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.FileManager.Interfaces;

namespace Winhance.Infrastructure.Features.FileManager.Services
{
    /// <summary>
    /// Service for monitoring folders and executing automated rules on file changes.
    /// </summary>
    public class WatchFolderService : IWatchFolderService
    {
        private readonly object _lock = new();
        private readonly Dictionary<string, FileSystemWatcher> _watchers = new();
        private readonly Dictionary<string, CancellationTokenSource> _settleTimers = new();
        private readonly ConcurrentQueue<ExecutionHistoryEntry> _history = new();
        private readonly string _configPath;
        private const int MaxHistoryEntries = 10000;
        private bool _disposed;

        /// <inheritdoc />
        public ObservableCollection<WatchFolder> WatchFolders { get; } = new();

        /// <inheritdoc />
        public bool IsRunning => _watchers.Values.Any(w => w.EnableRaisingEvents);

        /// <inheritdoc />
        public event EventHandler<WatchEventArgs>? FileEventDetected;

        /// <inheritdoc />
        public event EventHandler<RuleExecutionEventArgs>? RuleExecuted;

        /// <inheritdoc />
        public event EventHandler<WatchErrorEventArgs>? ErrorOccurred;

        public WatchFolderService()
        {
            _configPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Winhance", "WatchFolders.json");
        }

        /// <inheritdoc />
        public WatchFolder CreateWatchFolder(string path, string? name = null)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("Path cannot be empty", nameof(path));

            if (!Directory.Exists(path))
                throw new DirectoryNotFoundException($"Directory not found: {path}");

            var watchFolder = new WatchFolder
            {
                Path = path,
                Name = name ?? Path.GetFileName(path) ?? path
            };

            lock (_lock)
            {
                WatchFolders.Add(watchFolder);
            }

            return watchFolder;
        }

        /// <inheritdoc />
        public void RemoveWatchFolder(WatchFolder watchFolder)
        {
            if (watchFolder == null) return;

            StopWatching(watchFolder);

            lock (_lock)
            {
                WatchFolders.Remove(watchFolder);
            }
        }

        /// <inheritdoc />
        public void StartWatching(WatchFolder watchFolder)
        {
            if (watchFolder == null) return;
            if (!Directory.Exists(watchFolder.Path))
            {
                watchFolder.Status = WatchStatus.Error;
                watchFolder.LastError = "Directory not found";
                RaiseError(watchFolder, "Directory not found", null, false);
                return;
            }

            lock (_lock)
            {
                if (_watchers.ContainsKey(watchFolder.Id))
                {
                    StopWatching(watchFolder);
                }

                try
                {
                    var watcher = new FileSystemWatcher(watchFolder.Path)
                    {
                        Filter = watchFolder.Filter,
                        IncludeSubdirectories = watchFolder.IncludeSubfolders,
                        NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite |
                                       NotifyFilters.Size | NotifyFilters.DirectoryName
                    };

                    if (watchFolder.MonitoredEvents.HasFlag(WatchEventTypes.Created))
                        watcher.Created += (s, e) => OnFileEvent(watchFolder, e, WatchEventTypes.Created);

                    if (watchFolder.MonitoredEvents.HasFlag(WatchEventTypes.Changed))
                        watcher.Changed += (s, e) => OnFileEvent(watchFolder, e, WatchEventTypes.Changed);

                    if (watchFolder.MonitoredEvents.HasFlag(WatchEventTypes.Deleted))
                        watcher.Deleted += (s, e) => OnFileEvent(watchFolder, e, WatchEventTypes.Deleted);

                    if (watchFolder.MonitoredEvents.HasFlag(WatchEventTypes.Renamed))
                        watcher.Renamed += (s, e) => OnRenameEvent(watchFolder, e);

                    watcher.Error += (s, e) => OnWatcherError(watchFolder, e);

                    watcher.EnableRaisingEvents = true;
                    _watchers[watchFolder.Id] = watcher;

                    watchFolder.Status = WatchStatus.Running;
                    watchFolder.LastError = null;
                }
                catch (Exception ex)
                {
                    watchFolder.Status = WatchStatus.Error;
                    watchFolder.LastError = ex.Message;
                    RaiseError(watchFolder, ex.Message, ex, false);
                }
            }
        }

        /// <inheritdoc />
        public void StopWatching(WatchFolder watchFolder)
        {
            if (watchFolder == null) return;

            lock (_lock)
            {
                if (_watchers.TryGetValue(watchFolder.Id, out var watcher))
                {
                    watcher.EnableRaisingEvents = false;
                    watcher.Dispose();
                    _watchers.Remove(watchFolder.Id);
                }

                if (_settleTimers.TryGetValue(watchFolder.Id, out var cts))
                {
                    cts.Cancel();
                    cts.Dispose();
                    _settleTimers.Remove(watchFolder.Id);
                }

                watchFolder.Status = WatchStatus.Stopped;
            }
        }

        /// <inheritdoc />
        public void StartAll()
        {
            foreach (var watchFolder in WatchFolders.Where(w => w.IsEnabled))
            {
                StartWatching(watchFolder);
            }
        }

        /// <inheritdoc />
        public void StopAll()
        {
            foreach (var watchFolder in WatchFolders.ToList())
            {
                StopWatching(watchFolder);
            }
        }

        /// <inheritdoc />
        public void AddRule(WatchFolder watchFolder, WatchRule rule)
        {
            if (watchFolder == null || rule == null) return;

            lock (_lock)
            {
                if (!watchFolder.Rules.Any(r => r.Id == rule.Id))
                {
                    watchFolder.Rules.Add(rule);
                    watchFolder.Rules = watchFolder.Rules.OrderBy(r => r.Priority).ToList();
                }
            }
        }

        /// <inheritdoc />
        public void RemoveRule(WatchFolder watchFolder, WatchRule rule)
        {
            if (watchFolder == null || rule == null) return;

            lock (_lock)
            {
                watchFolder.Rules.RemoveAll(r => r.Id == rule.Id);
            }
        }

        /// <inheritdoc />
        public async Task<ProcessingResult> ProcessExistingFilesAsync(
            WatchFolder watchFolder,
            bool dryRun = false,
            IProgress<WatchProcessingProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            var result = new ProcessingResult { WasDryRun = dryRun };

            if (watchFolder == null || !Directory.Exists(watchFolder.Path))
                return result;

            var searchOption = watchFolder.IncludeSubfolders
                ? SearchOption.AllDirectories
                : SearchOption.TopDirectoryOnly;

            var files = Directory.EnumerateFiles(watchFolder.Path, watchFolder.Filter, searchOption)
                .Where(f => !IsExcluded(f, watchFolder.ExcludePatterns))
                .ToList();

            result.TotalFiles = files.Count;

            for (int i = 0; i < files.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var filePath = files[i];
                var fileResult = new FileProcessingResult { SourcePath = filePath };

                try
                {
                    var matchingRule = FindMatchingRule(watchFolder, filePath);
                    if (matchingRule != null)
                    {
                        result.MatchedFiles++;
                        fileResult.MatchedRule = matchingRule.Name;
                        fileResult.Action = matchingRule.Action.Type;

                        if (!dryRun)
                        {
                            var execResult = await ExecuteRuleAsync(matchingRule, filePath, cancellationToken);
                            fileResult.Success = execResult.Success;
                            fileResult.DestinationPath = execResult.DestinationPath;
                            fileResult.ErrorMessage = execResult.ErrorMessage;

                            if (!execResult.Success)
                                result.ErrorFiles++;
                        }
                        else
                        {
                            fileResult.Success = true;
                            fileResult.DestinationPath = GetDestinationPath(matchingRule.Action, filePath);
                        }
                    }
                    else
                    {
                        result.SkippedFiles++;
                    }
                }
                catch (Exception ex)
                {
                    result.ErrorFiles++;
                    fileResult.Success = false;
                    fileResult.ErrorMessage = ex.Message;
                }

                result.FileResults.Add(fileResult);
                result.ProcessedFiles++;

                progress?.Report(new WatchProcessingProgress
                {
                    CurrentFile = Path.GetFileName(filePath),
                    TotalFiles = result.TotalFiles,
                    ProcessedFiles = result.ProcessedFiles,
                    MatchedFiles = result.MatchedFiles
                });
            }

            return result;
        }

        /// <inheritdoc />
        public bool TestRule(WatchRule rule, string filePath)
        {
            if (rule == null || string.IsNullOrEmpty(filePath))
                return false;

            return EvaluateConditions(rule.Conditions, rule.ConditionLogic, filePath);
        }

        /// <inheritdoc />
        public IEnumerable<ExecutionHistoryEntry> GetHistory(WatchFolder watchFolder, int maxEntries = 100)
        {
            var entries = _history.ToArray();

            if (watchFolder != null)
            {
                entries = entries.Where(e => e.WatchFolderId == watchFolder.Id).ToArray();
            }

            return entries
                .OrderByDescending(e => e.Timestamp)
                .Take(maxEntries);
        }

        /// <inheritdoc />
        public void ClearHistory(WatchFolder? watchFolder = null)
        {
            if (watchFolder == null)
            {
                while (_history.TryDequeue(out _)) { }
            }
            else
            {
                var toKeep = _history.Where(e => e.WatchFolderId != watchFolder.Id).ToList();
                while (_history.TryDequeue(out _)) { }
                foreach (var entry in toKeep)
                {
                    _history.Enqueue(entry);
                }
            }
        }

        /// <inheritdoc />
        public async Task SaveConfigurationAsync()
        {
            var dir = Path.GetDirectoryName(_configPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(WatchFolders.ToList(), new JsonSerializerOptions
            {
                WriteIndented = true
            });

            await File.WriteAllTextAsync(_configPath, json);
        }

        /// <inheritdoc />
        public async Task LoadConfigurationAsync()
        {
            if (!File.Exists(_configPath))
                return;

            try
            {
                var json = await File.ReadAllTextAsync(_configPath);
                var folders = JsonSerializer.Deserialize<List<WatchFolder>>(json);

                if (folders != null)
                {
                    lock (_lock)
                    {
                        WatchFolders.Clear();
                        foreach (var folder in folders)
                        {
                            folder.Status = WatchStatus.Stopped;
                            WatchFolders.Add(folder);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                RaiseError(null, $"Failed to load configuration: {ex.Message}", ex, true);
            }
        }

        /// <inheritdoc />
        public async Task ExportAsync(string filePath)
        {
            var json = JsonSerializer.Serialize(WatchFolders.ToList(), new JsonSerializerOptions
            {
                WriteIndented = true
            });

            await File.WriteAllTextAsync(filePath, json);
        }

        /// <inheritdoc />
        public async Task ImportAsync(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("Import file not found", filePath);

            var json = await File.ReadAllTextAsync(filePath);
            var folders = JsonSerializer.Deserialize<List<WatchFolder>>(json);

            if (folders != null)
            {
                lock (_lock)
                {
                    foreach (var folder in folders)
                    {
                        folder.Id = Guid.NewGuid().ToString();
                        folder.Status = WatchStatus.Stopped;

                        foreach (var rule in folder.Rules)
                        {
                            rule.Id = Guid.NewGuid().ToString();
                        }

                        WatchFolders.Add(folder);
                    }
                }
            }
        }

        private void OnFileEvent(WatchFolder watchFolder, FileSystemEventArgs e, WatchEventTypes eventType)
        {
            if (IsExcluded(e.FullPath, watchFolder.ExcludePatterns))
                return;

            var args = new WatchEventArgs
            {
                WatchFolder = watchFolder,
                EventType = eventType,
                FilePath = e.FullPath
            };

            FileEventDetected?.Invoke(this, args);
            watchFolder.LastEventAt = DateTime.UtcNow;

            // Use settle time for file completion
            if (watchFolder.SettleTimeMs > 0 && eventType == WatchEventTypes.Created)
            {
                ScheduleWithSettleTime(watchFolder, e.FullPath, eventType);
            }
            else
            {
                _ = ProcessFileEventAsync(watchFolder, e.FullPath, eventType);
            }
        }

        private void OnRenameEvent(WatchFolder watchFolder, RenamedEventArgs e)
        {
            if (IsExcluded(e.FullPath, watchFolder.ExcludePatterns))
                return;

            var args = new WatchEventArgs
            {
                WatchFolder = watchFolder,
                EventType = WatchEventTypes.Renamed,
                FilePath = e.FullPath,
                OldPath = e.OldFullPath
            };

            FileEventDetected?.Invoke(this, args);
            watchFolder.LastEventAt = DateTime.UtcNow;

            _ = ProcessFileEventAsync(watchFolder, e.FullPath, WatchEventTypes.Renamed);
        }

        private void OnWatcherError(WatchFolder watchFolder, ErrorEventArgs e)
        {
            watchFolder.Status = WatchStatus.Error;
            watchFolder.LastError = e.GetException()?.Message ?? "Unknown error";
            RaiseError(watchFolder, watchFolder.LastError, e.GetException(), true);
        }

        private void ScheduleWithSettleTime(WatchFolder watchFolder, string filePath, WatchEventTypes eventType)
        {
            var key = $"{watchFolder.Id}:{filePath}";

            lock (_lock)
            {
                if (_settleTimers.TryGetValue(key, out var existingCts))
                {
                    existingCts.Cancel();
                    existingCts.Dispose();
                    _settleTimers.Remove(key);
                }

                var cts = new CancellationTokenSource();
                _settleTimers[key] = cts;

                Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(watchFolder.SettleTimeMs, cts.Token);

                        lock (_lock)
                        {
                            _settleTimers.Remove(key);
                        }

                        if (!cts.IsCancellationRequested && File.Exists(filePath))
                        {
                            await ProcessFileEventAsync(watchFolder, filePath, eventType);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        // Expected when new event comes in
                    }
                });
            }
        }

        private async Task ProcessFileEventAsync(WatchFolder watchFolder, string filePath, WatchEventTypes eventType)
        {
            try
            {
                if (!File.Exists(filePath) && eventType != WatchEventTypes.Deleted)
                    return;

                var matchingRule = FindMatchingRule(watchFolder, filePath);
                if (matchingRule == null)
                    return;

                var result = await ExecuteRuleAsync(matchingRule, filePath);

                var historyEntry = new ExecutionHistoryEntry
                {
                    WatchFolderId = watchFolder.Id,
                    RuleId = matchingRule.Id,
                    RuleName = matchingRule.Name,
                    EventType = eventType,
                    SourcePath = filePath,
                    DestinationPath = result.DestinationPath,
                    ActionType = matchingRule.Action.Type,
                    Success = result.Success,
                    ErrorMessage = result.ErrorMessage
                };

                AddHistoryEntry(historyEntry);
                watchFolder.TotalFilesProcessed++;

                RuleExecuted?.Invoke(this, new RuleExecutionEventArgs
                {
                    WatchFolder = watchFolder,
                    Rule = matchingRule,
                    SourcePath = filePath,
                    DestinationPath = result.DestinationPath,
                    Success = result.Success,
                    ErrorMessage = result.ErrorMessage
                });
            }
            catch (Exception ex)
            {
                RaiseError(watchFolder, $"Error processing {filePath}: {ex.Message}", ex, true);
            }
        }

        private WatchRule? FindMatchingRule(WatchFolder watchFolder, string filePath)
        {
            return watchFolder.Rules
                .Where(r => r.IsEnabled)
                .OrderBy(r => r.Priority)
                .FirstOrDefault(r => TestRule(r, filePath));
        }

        private bool EvaluateConditions(List<WatchRuleCondition> conditions, ConditionLogic logic, string filePath)
        {
            if (conditions == null || conditions.Count == 0)
                return true;

            var results = conditions.Select(c => EvaluateCondition(c, filePath)).ToList();

            return logic switch
            {
                ConditionLogic.All => results.All(r => r),
                ConditionLogic.Any => results.Any(r => r),
                ConditionLogic.None => results.All(r => !r),
                _ => true
            };
        }

        private bool EvaluateCondition(WatchRuleCondition condition, string filePath)
        {
            try
            {
                var value = GetConditionValue(condition.Type, filePath);
                var compareValue = condition.Value;
                var stringComparison = condition.CaseSensitive
                    ? StringComparison.Ordinal
                    : StringComparison.OrdinalIgnoreCase;

                return condition.Operator switch
                {
                    WatchConditionOperator.Equals => value.Equals(compareValue, stringComparison),
                    WatchConditionOperator.NotEquals => !value.Equals(compareValue, stringComparison),
                    WatchConditionOperator.Contains => value.Contains(compareValue, stringComparison),
                    WatchConditionOperator.NotContains => !value.Contains(compareValue, stringComparison),
                    WatchConditionOperator.StartsWith => value.StartsWith(compareValue, stringComparison),
                    WatchConditionOperator.EndsWith => value.EndsWith(compareValue, stringComparison),
                    WatchConditionOperator.MatchesRegex => Regex.IsMatch(value, compareValue,
                        condition.CaseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase),
                    WatchConditionOperator.GreaterThan => CompareNumeric(value, compareValue) > 0,
                    WatchConditionOperator.LessThan => CompareNumeric(value, compareValue) < 0,
                    WatchConditionOperator.Between => CompareNumeric(value, compareValue) >= 0 &&
                                                  CompareNumeric(value, condition.Value2 ?? compareValue) <= 0,
                    WatchConditionOperator.In => compareValue.Split(',', ';')
                        .Any(v => value.Equals(v.Trim(), stringComparison)),
                    _ => false
                };
            }
            catch
            {
                return false;
            }
        }

        private string GetConditionValue(WatchConditionType type, string filePath)
        {
            var fileInfo = new FileInfo(filePath);

            return type switch
            {
                WatchConditionType.FileName => Path.GetFileName(filePath),
                WatchConditionType.Extension => Path.GetExtension(filePath).TrimStart('.'),
                WatchConditionType.FullPath => filePath,
                WatchConditionType.FileSize => fileInfo.Exists ? fileInfo.Length.ToString() : "0",
                WatchConditionType.DateModified => fileInfo.Exists ? fileInfo.LastWriteTime.ToString("o") : "",
                WatchConditionType.DateCreated => fileInfo.Exists ? fileInfo.CreationTime.ToString("o") : "",
                WatchConditionType.Attributes => fileInfo.Exists ? fileInfo.Attributes.ToString() : "",
                WatchConditionType.ContentContains => File.Exists(filePath) ? File.ReadAllText(filePath) : "",
                _ => ""
            };
        }

        private int CompareNumeric(string value1, string value2)
        {
            if (long.TryParse(value1, out var num1) && long.TryParse(value2, out var num2))
                return num1.CompareTo(num2);
            return string.Compare(value1, value2, StringComparison.OrdinalIgnoreCase);
        }

        private async Task<(bool Success, string? DestinationPath, string? ErrorMessage)> ExecuteRuleAsync(
            WatchRule rule, string filePath, CancellationToken cancellationToken = default)
        {
            try
            {
                var action = rule.Action;
                string? destPath = null;

                switch (action.Type)
                {
                    case WatchActionType.Move:
                        destPath = GetDestinationPath(action, filePath);
                        EnsureDirectory(destPath);
                        destPath = HandleConflict(destPath, action.ConflictAction, filePath);
                        if (destPath != null)
                            File.Move(filePath, destPath);
                        break;

                    case WatchActionType.Copy:
                        destPath = GetDestinationPath(action, filePath);
                        EnsureDirectory(destPath);
                        destPath = HandleConflict(destPath, action.ConflictAction, filePath);
                        if (destPath != null)
                            File.Copy(filePath, destPath, action.ConflictAction == WatchConflictAction.Overwrite);
                        break;

                    case WatchActionType.Delete:
                        File.Delete(filePath);
                        break;

                    case WatchActionType.Rename:
                        if (!string.IsNullOrEmpty(action.RenamePattern))
                        {
                            var newName = ApplyRenamePattern(action.RenamePattern, filePath);
                            var dir = Path.GetDirectoryName(filePath) ?? "";
                            destPath = Path.Combine(dir, newName);
                            destPath = HandleConflict(destPath, action.ConflictAction, filePath);
                            if (destPath != null)
                                File.Move(filePath, destPath);
                        }
                        break;

                    case WatchActionType.Compress:
                        destPath = GetDestinationPath(action, filePath);
                        if (string.IsNullOrEmpty(Path.GetExtension(destPath)))
                            destPath = Path.ChangeExtension(filePath, ".zip");
                        EnsureDirectory(destPath);
                        using (var archive = System.IO.Compression.ZipFile.Open(destPath,
                            System.IO.Compression.ZipArchiveMode.Create))
                        {
                            archive.CreateEntryFromFile(filePath, Path.GetFileName(filePath));
                        }
                        break;

                    case WatchActionType.RunScript:
                        if (!string.IsNullOrEmpty(action.ScriptPath) && File.Exists(action.ScriptPath))
                        {
                            var args = (action.ScriptArguments ?? "").Replace("{file}", $"\"{filePath}\"");
                            var psi = new System.Diagnostics.ProcessStartInfo
                            {
                                FileName = action.ScriptPath,
                                Arguments = args,
                                UseShellExecute = false,
                                CreateNoWindow = true
                            };
                            using var process = System.Diagnostics.Process.Start(psi);
                            if (process != null)
                            {
                                await process.WaitForExitAsync(cancellationToken);
                            }
                        }
                        break;

                    case WatchActionType.CreateSymlink:
                        destPath = GetDestinationPath(action, filePath);
                        EnsureDirectory(destPath);
                        File.CreateSymbolicLink(destPath, filePath);
                        break;

                    case WatchActionType.Notify:
                        // Just raise event, no file operation
                        break;

                    case WatchActionType.AddTag:
                        // Tags would require a metadata service
                        break;
                }

                return (true, destPath, null);
            }
            catch (Exception ex)
            {
                return (false, null, ex.Message);
            }
        }

        private string GetDestinationPath(WatchRuleAction action, string sourcePath)
        {
            if (string.IsNullOrEmpty(action.DestinationPath))
                return sourcePath;

            var destDir = action.DestinationPath;
            destDir = destDir.Replace("{date}", DateTime.Now.ToString("yyyy-MM-dd"));
            destDir = destDir.Replace("{year}", DateTime.Now.Year.ToString());
            destDir = destDir.Replace("{month}", DateTime.Now.ToString("MM"));
            destDir = destDir.Replace("{day}", DateTime.Now.ToString("dd"));
            destDir = destDir.Replace("{ext}", Path.GetExtension(sourcePath).TrimStart('.'));

            return Path.Combine(destDir, Path.GetFileName(sourcePath));
        }

        private string ApplyRenamePattern(string pattern, string sourcePath)
        {
            var fileName = Path.GetFileNameWithoutExtension(sourcePath);
            var ext = Path.GetExtension(sourcePath);
            var fileInfo = new FileInfo(sourcePath);

            var result = pattern;
            result = result.Replace("{name}", fileName);
            result = result.Replace("{ext}", ext.TrimStart('.'));
            result = result.Replace("{date}", DateTime.Now.ToString("yyyy-MM-dd"));
            result = result.Replace("{time}", DateTime.Now.ToString("HHmmss"));
            result = result.Replace("{size}", fileInfo.Exists ? fileInfo.Length.ToString() : "0");
            result = result.Replace("{guid}", Guid.NewGuid().ToString("N")[..8]);

            // Counter pattern {n} or {n:3}
            var counterMatch = Regex.Match(result, @"\{n(?::(\d+))?\}");
            if (counterMatch.Success)
            {
                var digits = counterMatch.Groups[1].Success ? int.Parse(counterMatch.Groups[1].Value) : 1;
                result = result.Replace(counterMatch.Value, "1".PadLeft(digits, '0'));
            }

            if (!result.Contains('.'))
                result += ext;

            return result;
        }

        private void EnsureDirectory(string filePath)
        {
            var dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
        }

        private string? HandleConflict(string destPath, WatchConflictAction action, string sourcePath)
        {
            if (!File.Exists(destPath))
                return destPath;

            return action switch
            {
                WatchConflictAction.Skip => null,
                WatchConflictAction.Overwrite => destPath,
                WatchConflictAction.OverwriteIfNewer =>
                    File.GetLastWriteTime(sourcePath) > File.GetLastWriteTime(destPath) ? destPath : null,
                WatchConflictAction.Rename => GetUniqueFileName(destPath),
                _ => destPath
            };
        }

        private string GetUniqueFileName(string path)
        {
            if (!File.Exists(path))
                return path;

            var dir = Path.GetDirectoryName(path) ?? "";
            var name = Path.GetFileNameWithoutExtension(path);
            var ext = Path.GetExtension(path);

            int counter = 1;
            string newPath;
            do
            {
                newPath = Path.Combine(dir, $"{name} ({counter}){ext}");
                counter++;
            } while (File.Exists(newPath));

            return newPath;
        }

        private bool IsExcluded(string path, List<string> excludePatterns)
        {
            if (excludePatterns == null || excludePatterns.Count == 0)
                return false;

            var fileName = Path.GetFileName(path);
            return excludePatterns.Any(pattern =>
            {
                if (pattern.Contains('*') || pattern.Contains('?'))
                {
                    var regex = "^" + Regex.Escape(pattern).Replace("\\*", ".*").Replace("\\?", ".") + "$";
                    return Regex.IsMatch(fileName, regex, RegexOptions.IgnoreCase);
                }
                return fileName.Equals(pattern, StringComparison.OrdinalIgnoreCase);
            });
        }

        private void AddHistoryEntry(ExecutionHistoryEntry entry)
        {
            _history.Enqueue(entry);

            // Trim history if too large
            while (_history.Count > MaxHistoryEntries)
            {
                _history.TryDequeue(out _);
            }
        }

        private void RaiseError(WatchFolder? watchFolder, string message, Exception? ex, bool isRecoverable)
        {
            ErrorOccurred?.Invoke(this, new WatchErrorEventArgs
            {
                WatchFolder = watchFolder,
                Message = message,
                Exception = ex,
                IsRecoverable = isRecoverable
            });
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            StopAll();

            foreach (var cts in _settleTimers.Values)
            {
                cts.Cancel();
                cts.Dispose();
            }
            _settleTimers.Clear();
        }
    }
}
