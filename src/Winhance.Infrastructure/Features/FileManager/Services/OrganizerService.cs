using System;
using System.Collections.Generic;
using System.IO;
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
    /// Robust implementation of file organization with AI-powered categorization and rollback support.
    /// </summary>
    public class OrganizerService : IOrganizerService
    {
        private readonly ILogService _logService;
        private readonly string _transactionLogPath;
        private readonly string _rulesPath;
        private readonly Dictionary<string, string[]> _fileTypeCategories;

        public OrganizerService(ILogService logService)
        {
            _logService = logService;

            var appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Winhance", "FileManager");

            _transactionLogPath = Path.Combine(appDataPath, "organize-transactions");
            _rulesPath = Path.Combine(appDataPath, "organize-rules");

            Directory.CreateDirectory(_transactionLogPath);
            Directory.CreateDirectory(_rulesPath);

            _fileTypeCategories = InitializeFileTypeCategories();
        }

        public async Task<OrganizationPlan> AnalyzeAsync(
            string sourcePath,
            OrganizationStrategy strategy,
            CancellationToken cancellationToken = default)
        {
            var plan = new OrganizationPlan
            {
                SourcePath = sourcePath,
                Strategy = strategy,
                AnalysisDate = DateTime.UtcNow,
            };

            var categories = new Dictionary<string, OrganizationCategory>();

            await Task.Run(
                () =>
            {
                if (!Directory.Exists(sourcePath))
                {
                    _logService.Log(LogLevel.Warning, $"Source path not found: {sourcePath}");
                    return;
                }

                var files = Directory.EnumerateFiles(sourcePath, "*", SearchOption.TopDirectoryOnly);

                foreach (var filePath in files)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        var fileInfo = new FileInfo(filePath);
                        var categoryName = GetCategoryName(fileInfo, strategy);
                        var destinationFolder = Path.Combine(sourcePath, categoryName);

                        if (!categories.TryGetValue(categoryName, out var category))
                        {
                            category = new OrganizationCategory
                            {
                                Name = categoryName,
                                DestinationFolder = destinationFolder,
                                Items = new List<OrganizationItem>(),
                            };
                            categories[categoryName] = category;
                        }

                        ((List<OrganizationItem>)category.Items).Add(new OrganizationItem
                        {
                            SourcePath = filePath,
                            DestinationPath = Path.Combine(destinationFolder, fileInfo.Name),
                            FileName = fileInfo.Name,
                            Size = fileInfo.Length,
                            DateModified = fileInfo.LastWriteTime,
                            Extension = fileInfo.Extension,
                        });

                        category.FileCount++;
                        category.TotalSize += fileInfo.Length;
                    }
                    catch (Exception ex)
                    {
                        _logService.Log(LogLevel.Warning, $"Error analyzing file {filePath}: {ex.Message}");
                    }
                }
            }, cancellationToken);

            plan.Categories = categories.Values.OrderByDescending(c => c.FileCount).ToList();
            plan.TotalFiles = plan.Categories.Sum(c => c.FileCount);
            plan.TotalSize = plan.Categories.Sum(c => c.TotalSize);

            return plan;
        }

        public async Task<OrganizationResult> ExecuteAsync(
            OrganizationPlan plan,
            CancellationToken cancellationToken = default)
        {
            var result = new OrganizationResult { Success = true };
            var errors = new List<string>();
            var transactionId = Guid.NewGuid().ToString();
            var transactionLog = new List<OrganizationTransaction>();

            try
            {
                foreach (var category in plan.Categories)
                {
                    // Create destination folder
                    if (!Directory.Exists(category.DestinationFolder))
                    {
                        Directory.CreateDirectory(category.DestinationFolder);
                    }

                    foreach (var item in category.Items)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        try
                        {
                            if (File.Exists(item.SourcePath))
                            {
                                var destPath = GetUniqueDestinationPath(item.DestinationPath);

                                await Task.Run(() => File.Move(item.SourcePath, destPath), cancellationToken);

                                transactionLog.Add(new OrganizationTransaction
                                {
                                    SourcePath = item.SourcePath,
                                    DestinationPath = destPath,
                                    Category = category.Name,
                                    Timestamp = DateTime.UtcNow,
                                });

                                result.FilesOrganized++;
                                result.BytesProcessed += item.Size;

                                _logService.Log(LogLevel.Debug, $"Moved: {item.FileName} -> {category.Name}");
                            }
                        }
                        catch (Exception ex)
                        {
                            errors.Add($"{item.FileName}: {ex.Message}");
                            result.FilesFailed++;
                            _logService.Log(LogLevel.Error, $"Failed to move {item.FileName}: {ex.Message}");
                        }
                    }
                }

                // Save transaction log
                await SaveTransactionAsync(transactionId, transactionLog);
                result.TransactionId = transactionId;

                _logService.Log(
                    LogLevel.Info,
                    $"Organization complete: {result.FilesOrganized} files organized, {result.FilesFailed} failed");
            }
            catch (OperationCanceledException)
            {
                result.Success = false;
                throw;
            }
            catch (Exception ex)
            {
                result.Success = false;
                _logService.Log(LogLevel.Error, $"Organization failed: {ex.Message}");
            }

            result.Errors = errors;
            result.Success = result.FilesFailed == 0;

            return result;
        }

        public async Task<OrganizationResult> UndoAsync(
            string transactionId,
            CancellationToken cancellationToken = default)
        {
            var result = new OrganizationResult { Success = true };
            var errors = new List<string>();

            try
            {
                var transactionPath = Path.Combine(_transactionLogPath, $"{transactionId}.json");

                if (!File.Exists(transactionPath))
                {
                    result.Success = false;
                    errors.Add("Transaction not found");
                    result.Errors = errors;
                    return result;
                }

                var json = await File.ReadAllTextAsync(transactionPath, cancellationToken);
                var transactions = JsonSerializer.Deserialize<List<OrganizationTransaction>>(json)
                    ?? new List<OrganizationTransaction>();

                // Undo in reverse order
                foreach (var transaction in transactions.AsEnumerable().Reverse())
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        if (File.Exists(transaction.DestinationPath))
                        {
                            // Ensure original directory exists
                            var originalDir = Path.GetDirectoryName(transaction.SourcePath);
                            if (!string.IsNullOrEmpty(originalDir) && !Directory.Exists(originalDir))
                            {
                                Directory.CreateDirectory(originalDir);
                            }

                            await Task.Run(
                                () => File.Move(transaction.DestinationPath, transaction.SourcePath),
                                cancellationToken);

                            result.FilesOrganized++;
                            _logService.Log(
                                LogLevel.Debug,
                                $"Restored: {Path.GetFileName(transaction.SourcePath)}");
                        }
                        else
                        {
                            result.FilesSkipped++;
                        }
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"{transaction.DestinationPath}: {ex.Message}");
                        result.FilesFailed++;
                    }
                }

                // Clean up empty category folders
                await CleanupEmptyFoldersAsync(transactions, cancellationToken);

                // Delete transaction log on success
                if (result.FilesFailed == 0)
                {
                    File.Delete(transactionPath);
                }

                _logService.Log(
                    LogLevel.Info,
                    $"Undo complete: {result.FilesOrganized} files restored");
            }
            catch (Exception ex)
            {
                result.Success = false;
                _logService.Log(LogLevel.Error, $"Undo failed: {ex.Message}");
            }

            result.Errors = errors;
            result.Success = result.FilesFailed == 0;

            return result;
        }

        public async Task<SpaceRecoveryAnalysis> AnalyzeSpaceRecoveryAsync(
            string driveLetter,
            CancellationToken cancellationToken = default)
        {
            var analysis = new SpaceRecoveryAnalysis
            {
                DriveLetter = driveLetter,
            };

            var opportunities = new List<RecoveryOpportunity>();

            await Task.Run(
                () =>
            {
                // Check AI model caches
                var aiModelPaths = new[]
                {
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".lmstudio", "models"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ollama", "models"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cache", "huggingface", "hub"),
                };

                foreach (var modelPath in aiModelPaths)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (Directory.Exists(modelPath))
                    {
                        try
                        {
                            var size = CalculateDirectorySize(modelPath);
                            if (size > 0)
                            {
                                opportunities.Add(new RecoveryOpportunity
                                {
                                    Name = Path.GetFileName(Path.GetDirectoryName(modelPath)) + " Models",
                                    Path = modelPath,
                                    Size = size,
                                    Category = "AI Models",
                                    Description = "AI model files that can be relocated to another drive",
                                    Action = RecoveryAction.Relocate,
                                    Priority = size > 10L * 1024 * 1024 * 1024 ? "High" : "Medium",
                                });
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Skipped inaccessible item: {ex.Message}");
                        }
                    }
                }

                // Check development caches
                var devCachePaths = new[]
                {
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cargo", "registry"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".npm"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "pip", "cache"),
                };

                foreach (var cachePath in devCachePaths)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (Directory.Exists(cachePath))
                    {
                        try
                        {
                            var size = CalculateDirectorySize(cachePath);
                            if (size > 500 * 1024 * 1024) // > 500MB
                            {
                                opportunities.Add(new RecoveryOpportunity
                                {
                                    Name = Path.GetFileName(cachePath) + " Cache",
                                    Path = cachePath,
                                    Size = size,
                                    Category = "Development Cache",
                                    Description = "Development package cache that can be cleaned",
                                    Action = RecoveryAction.Clean,
                                    Priority = size > 5L * 1024 * 1024 * 1024 ? "High" : "Low",
                                });
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Skipped inaccessible item: {ex.Message}");
                        }
                    }
                }

                // Check Windows temp folders
                var tempPaths = new[]
                {
                    Path.GetTempPath(),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Temp"),
                };

                foreach (var tempPath in tempPaths)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (Directory.Exists(tempPath))
                    {
                        try
                        {
                            var size = CalculateDirectorySize(tempPath);
                            if (size > 100 * 1024 * 1024) // > 100MB
                            {
                                opportunities.Add(new RecoveryOpportunity
                                {
                                    Name = "Temporary Files",
                                    Path = tempPath,
                                    Size = size,
                                    Category = "System Cache",
                                    Description = "Temporary files that can be safely cleaned",
                                    Action = RecoveryAction.Clean,
                                    Priority = "Low",
                                });
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Skipped inaccessible item: {ex.Message}");
                        }
                    }
                }
            }, cancellationToken);

            analysis.Opportunities = opportunities.OrderByDescending(o => o.Size).ToList();
            analysis.TotalRecoverableSize = opportunities.Sum(o => o.Size);

            return analysis;
        }

        public async Task<OrganizationResult> RelocateModelsAsync(
            string sourcePath,
            string destinationPath,
            bool createSymlinks,
            CancellationToken cancellationToken = default)
        {
            var result = new OrganizationResult { Success = true };
            var errors = new List<string>();
            var transactionId = Guid.NewGuid().ToString();
            var transactionLog = new List<OrganizationTransaction>();

            try
            {
                if (!Directory.Exists(sourcePath))
                {
                    result.Success = false;
                    errors.Add("Source path not found");
                    result.Errors = errors;
                    return result;
                }

                // Create destination
                Directory.CreateDirectory(destinationPath);

                // Move all files and folders
                foreach (var dir in Directory.GetDirectories(sourcePath))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        var destDir = Path.Combine(destinationPath, Path.GetFileName(dir));
                        await Task.Run(() => Directory.Move(dir, destDir), cancellationToken);

                        transactionLog.Add(new OrganizationTransaction
                        {
                            SourcePath = dir,
                            DestinationPath = destDir,
                            Category = "AI Model Relocation",
                            Timestamp = DateTime.UtcNow,
                        });

                        result.FilesOrganized++;
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"{dir}: {ex.Message}");
                        result.FilesFailed++;
                    }
                }

                foreach (var file in Directory.GetFiles(sourcePath))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        var destFile = Path.Combine(destinationPath, Path.GetFileName(file));
                        var fileInfo = new FileInfo(file);

                        await Task.Run(() => File.Move(file, destFile), cancellationToken);

                        transactionLog.Add(new OrganizationTransaction
                        {
                            SourcePath = file,
                            DestinationPath = destFile,
                            Category = "AI Model Relocation",
                            Timestamp = DateTime.UtcNow,
                        });

                        result.FilesOrganized++;
                        result.BytesProcessed += fileInfo.Length;
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"{file}: {ex.Message}");
                        result.FilesFailed++;
                    }
                }

                // Create symbolic link from source to destination
                if (result.FilesFailed == 0)
                {
                    try
                    {
                        // Remove original directory if empty
                        if (Directory.GetFileSystemEntries(sourcePath).Length == 0)
                        {
                            Directory.Delete(sourcePath);
                        }

                        // Create junction/symlink
                        await CreateSymbolicLinkAsync(sourcePath, destinationPath);

                        _logService.Log(
                            LogLevel.Info,
                            $"Created symbolic link: {sourcePath} -> {destinationPath}");
                    }
                    catch (Exception ex)
                    {
                        _logService.Log(
                            LogLevel.Warning,
                            $"Could not create symbolic link: {ex.Message}");
                    }
                }

                await SaveTransactionAsync(transactionId, transactionLog);
                result.TransactionId = transactionId;

                _logService.Log(
                    LogLevel.Info,
                    $"Model relocation complete: {result.BytesProcessed / (1024 * 1024)} MB moved");
            }
            catch (OperationCanceledException)
            {
                result.Success = false;
                throw;
            }
            catch (Exception ex)
            {
                result.Success = false;
                _logService.Log(LogLevel.Error, $"Relocation failed: {ex.Message}");
            }

            result.Errors = errors;
            result.Success = result.FilesFailed == 0;

            return result;
        }

        public async Task<IEnumerable<DuplicateGroup>> FindDuplicatesAsync(
            string searchPath,
            DuplicateSearchMethod method,
            CancellationToken cancellationToken = default)
        {
            var duplicates = new List<DuplicateGroup>();
            var fileGroups = new Dictionary<string, List<DuplicateFile>>();

            await Task.Run(
                () =>
            {
                if (!Directory.Exists(searchPath))
                {
                    return;
                }

                var files = Directory.EnumerateFiles(searchPath, "*", SearchOption.AllDirectories);

                foreach (var filePath in files)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        var fileInfo = new FileInfo(filePath);
                        string key;

                        switch (method)
                        {
                            case DuplicateSearchMethod.Hash:
                                key = ComputeFileHash(filePath);
                                break;
                            case DuplicateSearchMethod.Name:
                                key = fileInfo.Name.ToLowerInvariant();
                                break;
                            case DuplicateSearchMethod.Size:
                                key = fileInfo.Length.ToString();
                                break;
                            case DuplicateSearchMethod.NameAndSize:
                            default:
                                key = $"{fileInfo.Name.ToLowerInvariant()}_{fileInfo.Length}";
                                break;
                        }

                        if (!fileGroups.TryGetValue(key, out var group))
                        {
                            group = new List<DuplicateFile>();
                            fileGroups[key] = group;
                        }

                        group.Add(new DuplicateFile
                        {
                            Path = filePath,
                            Name = fileInfo.Name,
                            Size = fileInfo.Length,
                            DateModified = fileInfo.LastWriteTime,
                            Hash = method == DuplicateSearchMethod.Hash ? key : null,
                        });
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Skipped inaccessible item: {ex.Message}");
                    }
                }
            }, cancellationToken);

            // Filter to only groups with duplicates
            foreach (var kvp in fileGroups.Where(g => g.Value.Count > 1))
            {
                var group = kvp.Value;
                var totalSize = group.Sum(f => f.Size);
                var wastedSize = totalSize - group.First().Size;

                duplicates.Add(new DuplicateGroup
                {
                    Key = kvp.Key,
                    Files = group,
                    TotalSize = totalSize,
                    WastedSize = wastedSize,
                    FileCount = group.Count,
                });
            }

            return duplicates.OrderByDescending(d => d.WastedSize);
        }

        public async Task<IEnumerable<OrganizationRule>> GetRulesAsync(CancellationToken cancellationToken = default)
        {
            var rules = new List<OrganizationRule>();

            await Task.Run(
                () =>
            {
                try
                {
                    foreach (var file in Directory.GetFiles(_rulesPath, "*.json"))
                    {
                        try
                        {
                            var json = File.ReadAllText(file);
                            var rule = JsonSerializer.Deserialize<OrganizationRule>(json);
                            if (rule != null)
                            {
                                rules.Add(rule);
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Skipped inaccessible item: {ex.Message}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Skipped inaccessible item: {ex.Message}");
                }
            }, cancellationToken);

            return rules.OrderBy(r => r.Priority);
        }

        public async Task SaveRuleAsync(OrganizationRule rule, CancellationToken cancellationToken = default)
        {
            var fileName = $"{rule.Id}.json";
            var filePath = Path.Combine(_rulesPath, fileName);

            var json = JsonSerializer.Serialize(rule, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(filePath, json, cancellationToken);

            _logService.Log(LogLevel.Info, $"Saved organization rule: {rule.Name}");
        }

        public async Task DeleteRuleAsync(string ruleId, CancellationToken cancellationToken = default)
        {
            var filePath = Path.Combine(_rulesPath, $"{ruleId}.json");

            if (File.Exists(filePath))
            {
                await Task.Run(() => File.Delete(filePath), cancellationToken);
                _logService.Log(LogLevel.Info, $"Deleted organization rule: {ruleId}");
            }
        }

        private string GetCategoryName(FileInfo fileInfo, OrganizationStrategy strategy)
        {
            return strategy switch
            {
                OrganizationStrategy.ByType => GetTypeCategoryName(fileInfo.Extension),
                OrganizationStrategy.ByDate => fileInfo.LastWriteTime.ToString("yyyy-MM"),
                OrganizationStrategy.BySize => GetSizeCategoryName(fileInfo.Length),
                OrganizationStrategy.ByProject => "Misc",
                OrganizationStrategy.ByAICategory => GetAICategoryName(fileInfo),
                _ => "Other",
            };
        }

        private string GetTypeCategoryName(string extension)
        {
            extension = extension.TrimStart('.').ToLowerInvariant();

            foreach (var category in _fileTypeCategories)
            {
                if (category.Value.Contains(extension))
                {
                    return category.Key;
                }
            }

            return "Other";
        }

        private string GetSizeCategoryName(long size)
        {
            return size switch
            {
                < 1024 => "Tiny (< 1KB)",
                < 1024 * 1024 => "Small (1KB - 1MB)",
                < 10 * 1024 * 1024 => "Medium (1MB - 10MB)",
                < 100 * 1024 * 1024 => "Large (10MB - 100MB)",
                < 1024 * 1024 * 1024 => "Very Large (100MB - 1GB)",
                _ => "Huge (> 1GB)",
            };
        }

        private string GetAICategoryName(FileInfo fileInfo)
        {
            // Simple AI-like categorization based on file patterns
            var name = fileInfo.Name.ToLowerInvariant();
            var ext = fileInfo.Extension.TrimStart('.').ToLowerInvariant();

            if (name.Contains("screenshot", StringComparison.Ordinal) || name.Contains("screen", StringComparison.Ordinal) || name.Contains("capture", StringComparison.Ordinal))
            {
                return "Screenshots";
            }

            if (name.Contains("download", StringComparison.Ordinal) || fileInfo.DirectoryName?.Contains("Downloads", StringComparison.Ordinal) == true)
            {
                return "Downloads";
            }

            if (ext == "pdf" && (name.Contains("invoice", StringComparison.Ordinal) || name.Contains("receipt", StringComparison.Ordinal)))
            {
                return "Financial Documents";
            }

            if (ext == "pdf" && (name.Contains("resume", StringComparison.Ordinal) || name.Contains("cv", StringComparison.Ordinal)))
            {
                return "Career Documents";
            }

            if (new[] { "jpg", "jpeg", "png", "gif" }.Contains(ext) && name.Contains("img_", StringComparison.Ordinal))
            {
                return "Camera Photos";
            }

            return GetTypeCategoryName(fileInfo.Extension);
        }

        private string GetUniqueDestinationPath(string path)
        {
            if (!File.Exists(path))
            {
                return path;
            }

            var directory = Path.GetDirectoryName(path) ?? string.Empty;
            var name = Path.GetFileNameWithoutExtension(path);
            var extension = Path.GetExtension(path);
            var counter = 1;

            string newPath;
            do
            {
                newPath = Path.Combine(directory, $"{name} ({counter}){extension}");
                counter++;
            }
            while (File.Exists(newPath));

            return newPath;
        }

        private long CalculateDirectorySize(string path)
        {
            long size = 0;
            try
            {
                foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                {
                    try
                    {
                        size += new FileInfo(file).Length;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Skipped inaccessible item: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Skipped inaccessible item: {ex.Message}");
            }

            return size;
        }

        private string ComputeFileHash(string filePath)
        {
            try
            {
                using var stream = File.OpenRead(filePath);
                using var md5 = MD5.Create();
                var hash = md5.ComputeHash(stream);
                return BitConverter.ToString(hash).Replace("-", string.Empty, StringComparison.Ordinal).ToLowerInvariant();
            }
            catch (Exception)
            {
                return Guid.NewGuid().ToString();
            }
        }

        private async Task SaveTransactionAsync(string transactionId, List<OrganizationTransaction> transactions)
        {
            var filePath = Path.Combine(_transactionLogPath, $"{transactionId}.json");
            var json = JsonSerializer.Serialize(transactions, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(filePath, json);
        }

        private async Task CleanupEmptyFoldersAsync(
            List<OrganizationTransaction> transactions,
            CancellationToken cancellationToken)
        {
            var folders = transactions
                .Select(t => Path.GetDirectoryName(t.DestinationPath))
                .Where(d => !string.IsNullOrEmpty(d))
                .Distinct();

            foreach (var folder in folders)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    if (!string.IsNullOrEmpty(folder) && Directory.Exists(folder) && !Directory.EnumerateFileSystemEntries(folder).Any())
                    {
                        await Task.Run(() => Directory.Delete(folder), cancellationToken);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Skipped inaccessible item: {ex.Message}");
                }
            }
        }

        private async Task CreateSymbolicLinkAsync(string linkPath, string targetPath)
        {
            await Task.Run(() =>
            {
                // Use mklink /J for directory junction (doesn't require admin)
                var process = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = $"/c mklink /J \"{linkPath}\" \"{targetPath}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                    },
                };
                process.Start();
                process.WaitForExit();
            });
        }

        private Dictionary<string, string[]> InitializeFileTypeCategories()
        {
            return new Dictionary<string, string[]>
            {
                ["Documents"] = new[] { "doc", "docx", "pdf", "txt", "rtf", "odt", "xls", "xlsx", "ppt", "pptx", "csv" },
                ["Images"] = new[] { "jpg", "jpeg", "png", "gif", "bmp", "svg", "webp", "ico", "tiff", "heic", "raw" },
                ["Videos"] = new[] { "mp4", "avi", "mkv", "mov", "wmv", "flv", "webm", "m4v", "mpeg", "mpg" },
                ["Audio"] = new[] { "mp3", "wav", "flac", "aac", "ogg", "wma", "m4a", "opus" },
                ["Archives"] = new[] { "zip", "rar", "7z", "tar", "gz", "bz2", "xz", "iso" },
                ["Code"] = new[] { "cs", "py", "js", "ts", "java", "cpp", "c", "h", "rs", "go", "rb", "php", "html", "css", "json", "xml", "yaml", "yml" },
                ["Executables"] = new[] { "exe", "msi", "dll", "bat", "cmd", "ps1", "sh" },
                ["Fonts"] = new[] { "ttf", "otf", "woff", "woff2", "eot" },
                ["3D Models"] = new[] { "obj", "fbx", "stl", "blend", "dae", "3ds", "gltf", "glb" },
                ["AI Models"] = new[] { "gguf", "ggml", "safetensors", "bin", "pt", "pth", "onnx", "h5" },
            };
        }

        private class OrganizationTransaction
        {
            public string SourcePath { get; set; } = string.Empty;

            public string DestinationPath { get; set; } = string.Empty;

            public string Category { get; set; } = string.Empty;

            public DateTime Timestamp { get; set; }
        }
    }
}
