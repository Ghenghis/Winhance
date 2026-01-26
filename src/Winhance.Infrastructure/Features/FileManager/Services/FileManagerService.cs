using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.FileManager.Interfaces;
using Winhance.Core.Features.FileManager.Models;

namespace Winhance.Infrastructure.Features.FileManager.Services
{
    /// <summary>
    /// Robust implementation of file manager operations with transaction support.
    /// </summary>
    public class FileManagerService : IFileManagerService
    {
        private readonly ILogService _logService;
        private readonly string _transactionLogPath;

        public FileManagerService(ILogService logService)
        {
            _logService = logService;
            _transactionLogPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Winhance", "FileManager", "transactions");
            Directory.CreateDirectory(_transactionLogPath);
        }

        public async Task<IEnumerable<FileSystemEntry>> GetDirectoryContentsAsync(
            string path,
            CancellationToken cancellationToken = default)
        {
            var entries = new List<FileSystemEntry>();

            try
            {
                if (!Directory.Exists(path))
                {
                    _logService.Log(LogLevel.Warning, $"Directory not found: {path}");
                    return entries;
                }

                var dirInfo = new DirectoryInfo(path);

                // Add parent directory entry
                if (dirInfo.Parent != null)
                {
                    entries.Add(new FileSystemEntry
                    {
                        Name = "..",
                        FullPath = dirInfo.Parent.FullName,
                        IsDirectory = true,
                        DateModified = dirInfo.Parent.LastWriteTime,
                    });
                }

                // Get directories
                await Task.Run(
                    () =>
                {
                    foreach (var dir in dirInfo.EnumerateDirectories())
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        try
                        {
                            entries.Add(new FileSystemEntry
                            {
                                Name = dir.Name,
                                FullPath = dir.FullName,
                                IsDirectory = true,
                                DateModified = dir.LastWriteTime,
                                DateCreated = dir.CreationTime,
                                DateAccessed = dir.LastAccessTime,
                                Attributes = dir.Attributes,
                            });
                        }
                        catch (UnauthorizedAccessException)
                        {
                            // Skip inaccessible directories,
                        }
                        catch (Exception ex)
                        {
                            _logService.Log(LogLevel.Warning, $"Error reading directory {dir.Name}: {ex.Message}");
                        }
                    }

                    // Get files
                    foreach (var file in dirInfo.EnumerateFiles())
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        try
                        {
                            entries.Add(new FileSystemEntry
                            {
                                Name = file.Name,
                                FullPath = file.FullName,
                                IsDirectory = false,
                                Size = file.Length,
                                DateModified = file.LastWriteTime,
                                DateCreated = file.CreationTime,
                                DateAccessed = file.LastAccessTime,
                                Extension = file.Extension,
                                Attributes = file.Attributes,
                            });
                        }
                        catch (UnauthorizedAccessException)
                        {
                            // Skip inaccessible files,
                        }
                        catch (Exception ex)
                        {
                            _logService.Log(LogLevel.Warning, $"Error reading file {file.Name}: {ex.Message}");
                        }
                    }
                }, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error getting directory contents: {ex.Message}");
            }

            return entries.OrderByDescending(e => e.IsDirectory).ThenBy(e => e.Name);
        }

        public async Task<IEnumerable<FileManagerDriveInfo>> GetDrivesAsync(
            CancellationToken cancellationToken = default)
        {
            var drives = new List<FileManagerDriveInfo>();

            await Task.Run(
                () =>
            {
                foreach (var drive in System.IO.DriveInfo.GetDrives())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    try
                    {
                        drives.Add(new FileManagerDriveInfo
                        {
                            Name = drive.Name,
                            Label = drive.IsReady ? drive.VolumeLabel : string.Empty,
                            DriveType = drive.DriveType.ToString(),
                            FileSystem = drive.IsReady ? drive.DriveFormat : string.Empty,
                            TotalSize = drive.IsReady ? drive.TotalSize : 0,
                            FreeSpace = drive.IsReady ? drive.TotalFreeSpace : 0,
                            IsReady = drive.IsReady,
                        });
                    }
                    catch (Exception ex)
                    {
                        _logService.Log(LogLevel.Warning, $"Error reading drive {drive.Name}: {ex.Message}");
                    }
                }
            }, cancellationToken);

            return drives;
        }

        public async Task<FileOperationResult> CopyFilesAsync(
            IEnumerable<string> sourcePaths,
            string destinationPath,
            CancellationToken cancellationToken = default)
        {
            var result = new FileOperationResult { Success = true };
            var errors = new List<string>();
            var transactionId = Guid.NewGuid().ToString();
            var transactionLog = new List<TransactionEntry>();

            try
            {
                if (!Directory.Exists(destinationPath))
                {
                    Directory.CreateDirectory(destinationPath);
                }

                foreach (var sourcePath in sourcePaths)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        var fileName = Path.GetFileName(sourcePath);
                        var destPath = Path.Combine(destinationPath, fileName);

                        // Handle name conflicts
                        destPath = GetUniqueDestinationPath(destPath);

                        if (Directory.Exists(sourcePath))
                        {
                            await CopyDirectoryAsync(sourcePath, destPath, cancellationToken);
                        }
                        else if (File.Exists(sourcePath))
                        {
                            await Task.Run(() => File.Copy(sourcePath, destPath, false), cancellationToken);
                        }

                        transactionLog.Add(new TransactionEntry
                        {
                            Operation = "Copy",
                            SourcePath = sourcePath,
                            DestinationPath = destPath,
                            Timestamp = DateTime.UtcNow,
                        });

                        result.ItemsProcessed++;
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"{sourcePath}: {ex.Message}");
                        result.ItemsFailed++;
                        _logService.Log(LogLevel.Error, $"Error copying {sourcePath}: {ex.Message}");
                    }
                }

                // Save transaction log for rollback
                await SaveTransactionLogAsync(transactionId, transactionLog);
                result.TransactionId = transactionId;
            }
            catch (OperationCanceledException)
            {
                result.Success = false;
                result.Message = "Operation cancelled";
                throw;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = ex.Message;
                _logService.Log(LogLevel.Error, $"Copy operation failed: {ex.Message}");
            }

            result.Errors = errors;
            result.Success = result.ItemsFailed == 0;
            result.Message = result.Success
                ? $"Copied {result.ItemsProcessed} items"
                : $"Copied {result.ItemsProcessed}, failed {result.ItemsFailed}";

            return result;
        }

        public async Task<FileOperationResult> MoveFilesAsync(
            IEnumerable<string> sourcePaths,
            string destinationPath,
            CancellationToken cancellationToken = default)
        {
            var result = new FileOperationResult { Success = true };
            var errors = new List<string>();
            var transactionId = Guid.NewGuid().ToString();
            var transactionLog = new List<TransactionEntry>();

            try
            {
                if (!Directory.Exists(destinationPath))
                {
                    Directory.CreateDirectory(destinationPath);
                }

                foreach (var sourcePath in sourcePaths)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        var fileName = Path.GetFileName(sourcePath);
                        var destPath = Path.Combine(destinationPath, fileName);

                        // Handle name conflicts
                        destPath = GetUniqueDestinationPath(destPath);

                        if (Directory.Exists(sourcePath))
                        {
                            await Task.Run(() => Directory.Move(sourcePath, destPath), cancellationToken);
                        }
                        else if (File.Exists(sourcePath))
                        {
                            await Task.Run(() => File.Move(sourcePath, destPath), cancellationToken);
                        }

                        transactionLog.Add(new TransactionEntry
                        {
                            Operation = "Move",
                            SourcePath = sourcePath,
                            DestinationPath = destPath,
                            Timestamp = DateTime.UtcNow,
                        });

                        result.ItemsProcessed++;
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"{sourcePath}: {ex.Message}");
                        result.ItemsFailed++;
                        _logService.Log(LogLevel.Error, $"Error moving {sourcePath}: {ex.Message}");
                    }
                }

                // Save transaction log for rollback
                await SaveTransactionLogAsync(transactionId, transactionLog);
                result.TransactionId = transactionId;
            }
            catch (OperationCanceledException)
            {
                result.Success = false;
                result.Message = "Operation cancelled";
                throw;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = ex.Message;
                _logService.Log(LogLevel.Error, $"Move operation failed: {ex.Message}");
            }

            result.Errors = errors;
            result.Success = result.ItemsFailed == 0;
            result.Message = result.Success
                ? $"Moved {result.ItemsProcessed} items"
                : $"Moved {result.ItemsProcessed}, failed {result.ItemsFailed}";

            return result;
        }

        public async Task<FileOperationResult> DeleteFilesAsync(
            IEnumerable<string> paths,
            bool permanent = false,
            CancellationToken cancellationToken = default)
        {
            var result = new FileOperationResult { Success = true };
            var errors = new List<string>();
            var transactionId = Guid.NewGuid().ToString();
            var transactionLog = new List<TransactionEntry>();

            try
            {
                foreach (var path in paths)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        if (Directory.Exists(path))
                        {
                            if (permanent)
                            {
                                await Task.Run(() => Directory.Delete(path, true), cancellationToken);
                            }
                            else
                            {
                                // Move to recycle bin using Shell API
                                await MoveToRecycleBinAsync(path);
                            }
                        }
                        else if (File.Exists(path))
                        {
                            if (permanent)
                            {
                                await Task.Run(() => File.Delete(path), cancellationToken);
                            }
                            else
                            {
                                await MoveToRecycleBinAsync(path);
                            }
                        }

                        transactionLog.Add(new TransactionEntry
                        {
                            Operation = permanent ? "PermanentDelete" : "RecycleBinDelete",
                            SourcePath = path,
                            Timestamp = DateTime.UtcNow,
                        });

                        result.ItemsProcessed++;
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"{path}: {ex.Message}");
                        result.ItemsFailed++;
                        _logService.Log(LogLevel.Error, $"Error deleting {path}: {ex.Message}");
                    }
                }

                await SaveTransactionLogAsync(transactionId, transactionLog);
                result.TransactionId = transactionId;
            }
            catch (OperationCanceledException)
            {
                result.Success = false;
                result.Message = "Operation cancelled";
                throw;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = ex.Message;
                _logService.Log(LogLevel.Error, $"Delete operation failed: {ex.Message}");
            }

            result.Errors = errors;
            result.Success = result.ItemsFailed == 0;
            result.Message = result.Success
                ? $"Deleted {result.ItemsProcessed} items"
                : $"Deleted {result.ItemsProcessed}, failed {result.ItemsFailed}";

            return result;
        }

        public async Task<FileOperationResult> CreateDirectoryAsync(
            string path,
            CancellationToken cancellationToken = default)
        {
            var result = new FileOperationResult();

            try
            {
                // Get unique name if directory exists
                path = GetUniqueDestinationPath(path);

                await Task.Run(() => Directory.CreateDirectory(path), cancellationToken);

                result.Success = true;
                result.Message = $"Created directory: {Path.GetFileName(path)}";
                result.ItemsProcessed = 1;

                _logService.Log(LogLevel.Info, $"Created directory: {path}");
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = ex.Message;
                result.ItemsFailed = 1;
                result.Errors = new[] { ex.Message };
                _logService.Log(LogLevel.Error, $"Error creating directory: {ex.Message}");
            }

            return result;
        }

        public async Task<FileOperationResult> RenameAsync(
            string path,
            string newName,
            CancellationToken cancellationToken = default)
        {
            var result = new FileOperationResult();

            try
            {
                var directory = Path.GetDirectoryName(path);
                var newPath = Path.Combine(directory!, newName);

                if (Directory.Exists(path))
                {
                    await Task.Run(() => Directory.Move(path, newPath), cancellationToken);
                }
                else if (File.Exists(path))
                {
                    await Task.Run(() => File.Move(path, newPath), cancellationToken);
                }
                else
                {
                    throw new FileNotFoundException("File or directory not found", path);
                }

                result.Success = true;
                result.Message = $"Renamed to: {newName}";
                result.ItemsProcessed = 1;

                _logService.Log(LogLevel.Info, $"Renamed {path} to {newPath}");
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = ex.Message;
                result.ItemsFailed = 1;
                result.Errors = new[] { ex.Message };
                _logService.Log(LogLevel.Error, $"Error renaming: {ex.Message}");
            }

            return result;
        }

        public async Task<FileProperties> GetPropertiesAsync(
            string path,
            CancellationToken cancellationToken = default)
        {
            var properties = new FileProperties();

            await Task.Run(
                () =>
            {
                if (Directory.Exists(path))
                {
                    var dirInfo = new DirectoryInfo(path);
                    properties.Name = dirInfo.Name;
                    properties.FullPath = dirInfo.FullName;
                    properties.Type = "Directory";
                    properties.DateModified = dirInfo.LastWriteTime;
                    properties.DateCreated = dirInfo.CreationTime;
                    properties.DateAccessed = dirInfo.LastAccessTime;
                    properties.Attributes = dirInfo.Attributes;
                    properties.IsReadOnly = dirInfo.Attributes.HasFlag(FileAttributes.ReadOnly);
                    properties.IsHidden = dirInfo.Attributes.HasFlag(FileAttributes.Hidden);
                    properties.IsSystem = dirInfo.Attributes.HasFlag(FileAttributes.System);

                    // Calculate total size
                    properties.Size = CalculateDirectorySize(dirInfo);
                }
                else if (File.Exists(path))
                {
                    var fileInfo = new FileInfo(path);
                    properties.Name = fileInfo.Name;
                    properties.FullPath = fileInfo.FullName;
                    properties.Type = GetFileType(fileInfo.Extension);
                    properties.Size = fileInfo.Length;
                    properties.DateModified = fileInfo.LastWriteTime;
                    properties.DateCreated = fileInfo.CreationTime;
                    properties.DateAccessed = fileInfo.LastAccessTime;
                    properties.Attributes = fileInfo.Attributes;
                    properties.IsReadOnly = fileInfo.IsReadOnly;
                    properties.IsHidden = fileInfo.Attributes.HasFlag(FileAttributes.Hidden);
                    properties.IsSystem = fileInfo.Attributes.HasFlag(FileAttributes.System);

                    // Calculate hash for files
                    try
                    {
                        using var stream = File.OpenRead(path);
                        using var sha256 = SHA256.Create();
                        var hash = sha256.ComputeHash(stream);
                        properties.Hash = BitConverter.ToString(hash).Replace("-", string.Empty, StringComparison.Ordinal).ToLowerInvariant();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Hash calculation failed: {ex.Message}");
                    }
                }
            }, cancellationToken);

            return properties;
        }

        public async Task<IEnumerable<FileSystemEntry>> SearchAsync(
            string searchPath,
            string pattern,
            FileSearchOptions options,
            CancellationToken cancellationToken = default)
        {
            var results = new List<FileSystemEntry>();

            await Task.Run(
                () =>
            {
                var searchOption = options.Recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                var searchPattern = options.UseRegex ? "*" : $"*{pattern}*";

                try
                {
                    var enumOptions = new EnumerationOptions
                    {
                        RecurseSubdirectories = options.Recursive,
                        IgnoreInaccessible = true,
                        AttributesToSkip = options.IncludeHidden ? FileAttributes.None : FileAttributes.Hidden,
                    };

                    // Search directories
                    foreach (var dir in Directory.EnumerateDirectories(searchPath, searchPattern, enumOptions))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        try
                        {
                            var dirInfo = new DirectoryInfo(dir);
                            if (MatchesSearchCriteria(dirInfo.Name, pattern, options))
                            {
                                results.Add(new FileSystemEntry
                                {
                                    Name = dirInfo.Name,
                                    FullPath = dirInfo.FullName,
                                    IsDirectory = true,
                                    DateModified = dirInfo.LastWriteTime,
                                });
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Skip inaccessible directory: {ex.Message}");
                        }
                    }

                    // Search files
                    foreach (var file in Directory.EnumerateFiles(searchPath, searchPattern, enumOptions))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        try
                        {
                            var fileInfo = new FileInfo(file);

                            if (!MatchesSearchCriteria(fileInfo.Name, pattern, options))
                            {
                                continue;
                            }

                            // Apply size filters
                            if (options.MinSize.HasValue && fileInfo.Length < options.MinSize.Value)
                            {
                                continue;
                            }

                            if (options.MaxSize.HasValue && fileInfo.Length > options.MaxSize.Value)
                            {
                                continue;
                            }

                            // Apply date filters
                            if (options.ModifiedAfter.HasValue && fileInfo.LastWriteTime < options.ModifiedAfter.Value)
                            {
                                continue;
                            }

                            if (options.ModifiedBefore.HasValue && fileInfo.LastWriteTime > options.ModifiedBefore.Value)
                            {
                                continue;
                            }

                            // Apply extension filter
                            if (options.Extensions != null && options.Extensions.Any())
                            {
                                var ext = fileInfo.Extension.TrimStart('.').ToLowerInvariant();
                                if (!options.Extensions.Any(e => e.Equals(ext, StringComparison.OrdinalIgnoreCase)))
                                {
                                    continue;
                                }
                            }

                            results.Add(new FileSystemEntry
                            {
                                Name = fileInfo.Name,
                                FullPath = fileInfo.FullName,
                                IsDirectory = false,
                                Size = fileInfo.Length,
                                DateModified = fileInfo.LastWriteTime,
                                Extension = fileInfo.Extension,
                            });
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Skip inaccessible file: {ex.Message}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logService.Log(LogLevel.Error, $"Search error: {ex.Message}");
                }
            }, cancellationToken);

            return results;
        }

        private async Task CopyDirectoryAsync(string sourcePath, string destPath, CancellationToken cancellationToken)
        {
            Directory.CreateDirectory(destPath);

            foreach (var file in Directory.GetFiles(sourcePath))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var destFile = Path.Combine(destPath, Path.GetFileName(file));
                await Task.Run(() => File.Copy(file, destFile, false), cancellationToken);
            }

            foreach (var dir in Directory.GetDirectories(sourcePath))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var dirName = new DirectoryInfo(dir).Name;
                var destDir = Path.Combine(destPath, dirName);
                await CopyDirectoryAsync(dir, destDir, cancellationToken);
            }
        }

        private string GetUniqueDestinationPath(string path)
        {
            if (!File.Exists(path) && !Directory.Exists(path))
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
            while (File.Exists(newPath) || Directory.Exists(newPath));

            return newPath;
        }

        private async Task MoveToRecycleBinAsync(string path)
        {
            await Task.Run(() =>
            {
                // Use Microsoft.VisualBasic for recycle bin support
                Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(
                    path,
                    Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                    Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
            });
        }

        private long CalculateDirectorySize(DirectoryInfo dirInfo)
        {
            long size = 0;
            try
            {
                foreach (var file in dirInfo.EnumerateFiles("*", SearchOption.AllDirectories))
                {
                    try
                    {
                        size += file.Length;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Size calc error: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Directory enumeration error: {ex.Message}");
            }

            return size;
        }

        private string GetFileType(string extension)
        {
            return extension.ToLower() switch
            {
                ".txt" => "Text Document",
                ".pdf" => "PDF Document",
                ".doc" or ".docx" => "Word Document",
                ".xls" or ".xlsx" => "Excel Spreadsheet",
                ".ppt" or ".pptx" => "PowerPoint Presentation",
                ".jpg" or ".jpeg" => "JPEG Image",
                ".png" => "PNG Image",
                ".gif" => "GIF Image",
                ".mp3" => "MP3 Audio",
                ".mp4" => "MP4 Video",
                ".zip" => "ZIP Archive",
                ".rar" => "RAR Archive",
                ".exe" => "Application",
                ".dll" => "DLL Library",
                ".py" => "Python Script",
                ".cs" => "C# Source File",
                ".js" => "JavaScript File",
                ".ts" => "TypeScript File",
                ".rs" => "Rust Source File",
                _ => $"{extension.TrimStart('.').ToUpper()} File",
            };
        }

        private bool MatchesSearchCriteria(string name, string pattern, FileSearchOptions options)
        {
            if (options.UseRegex)
            {
                try
                {
                    return System.Text.RegularExpressions.Regex.IsMatch(
                        name,
                        pattern,
                        options.CaseSensitive
                            ? System.Text.RegularExpressions.RegexOptions.None
                            : System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Regex match error: {ex.Message}");
                    return false;
                }
            }

            var comparison = options.CaseSensitive
                ? StringComparison.Ordinal
                : StringComparison.OrdinalIgnoreCase;

            return name.Contains(pattern, comparison);
        }

        private async Task SaveTransactionLogAsync(string transactionId, List<TransactionEntry> entries)
        {
            var logPath = Path.Combine(_transactionLogPath, $"{transactionId}.json");
            var json = System.Text.Json.JsonSerializer.Serialize(entries, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
            });
            await File.WriteAllTextAsync(logPath, json);
        }

        private class TransactionEntry
        {
            public string Operation { get; set; } = string.Empty;

            public string SourcePath { get; set; } = string.Empty;

            public string DestinationPath { get; set; } = string.Empty;

            public DateTime Timestamp { get; set; }
        }
    }
}
