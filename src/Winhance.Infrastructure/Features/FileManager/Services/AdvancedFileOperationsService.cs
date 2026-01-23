using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.FileManager.Interfaces;
using Microsoft.VisualBasic.FileIO;

namespace Winhance.Infrastructure.Features.FileManager.Services
{
    /// <summary>
    /// Advanced file operations that extend Windows Explorer functionality.
    /// Pure performance with parallel processing and smart resource management.
    /// </summary>
    public class AdvancedFileOperationsService : IAdvancedFileOperations
    {
        private readonly ILogService _logService;
        private readonly INexusIndexerService? _nexusIndexer;

        public AdvancedFileOperationsService(ILogService logService)
        {
            _logService = logService;
            _nexusIndexer = null;
        }

        // ====================================================================
        // BULK COPY - High performance with verification
        // ====================================================================

        public async Task<BulkOperationResult> BulkCopyAsync(IEnumerable<string> sources, string destination,
            BulkCopyOptions options, IProgress<BulkProgress>? progress = null, CancellationToken ct = default)
        {
            var result = new BulkOperationResult();
            var sw = Stopwatch.StartNew();
            var sourceList = sources.ToList();
            var totalFiles = sourceList.Count;
            var processedFiles = 0;
            long totalBytes = 0;
            var lockObj = new object();

            Directory.CreateDirectory(destination);

            foreach (var source in sourceList)
            {
                if (ct.IsCancellationRequested) break;
                
                try
                {
                    var fileName = Path.GetFileName(source);
                    var destPath = Path.Combine(destination, fileName);

                    destPath = HandleCollision(destPath, options.Collision);
                    if (destPath == null)
                    {
                        result.SkippedCount++;
                        continue;
                    }

                    if (File.Exists(source))
                    {
                        await CopyFileWithBufferAsync(source, destPath, options.BufferSizeMb * 1024 * 1024, ct);
                        var fileInfo = new FileInfo(source);
                        totalBytes += fileInfo.Length;

                        if (options.PreserveTimestamps)
                        {
                            File.SetCreationTime(destPath, fileInfo.CreationTime);
                            File.SetLastWriteTime(destPath, fileInfo.LastWriteTime);
                        }

                        if (options.VerifyAfterCopy)
                        {
                            var srcHash = await ComputeHashAsync(source, ct);
                            var dstHash = await ComputeHashAsync(destPath, ct);
                            if (srcHash != dstHash)
                            {
                                result.Errors.Add(new OperationError { Path = source, Message = "Verification failed" });
                                result.FailedCount++;
                                continue;
                            }
                        }
                    }
                    else if (Directory.Exists(source))
                    {
                        CopyDirectory(source, destPath, options.PreserveTimestamps);
                    }

                    result.SuccessCount++;
                    processedFiles++;

                    progress?.Report(new BulkProgress
                    {
                        CurrentFile = processedFiles,
                        TotalFiles = totalFiles,
                        CurrentFileName = fileName,
                        BytesProcessed = totalBytes,
                        Phase = "Copying"
                    });
                }
                catch (Exception ex)
                {
                    result.Errors.Add(new OperationError { Path = source, Message = ex.Message, Exception = ex });
                    result.FailedCount++;
                }
            }

            sw.Stop();
            result.Duration = sw.Elapsed;
            result.TotalBytesProcessed = totalBytes;
            result.CanUndo = true;

            _logService.Log(LogLevel.Info, $"Bulk copy: {result.SuccessCount} succeeded, {result.FailedCount} failed");
            return result;
        }

        private async Task CopyFileWithBufferAsync(string source, string dest, int bufferSize, CancellationToken ct)
        {
            await using var sourceStream = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize, true);
            await using var destStream = new FileStream(dest, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize, true);
            await sourceStream.CopyToAsync(destStream, bufferSize, ct);
        }

        private async Task<string> ComputeHashAsync(string path, CancellationToken ct)
        {
            using var sha = System.Security.Cryptography.SHA256.Create();
            await using var stream = File.OpenRead(path);
            var hash = await sha.ComputeHashAsync(stream, ct);
            return BitConverter.ToString(hash).Replace("-", "");
        }

        private void CopyDirectory(string source, string dest, bool preserveTimestamps)
        {
            Directory.CreateDirectory(dest);
            foreach (var file in Directory.GetFiles(source))
            {
                var destFile = Path.Combine(dest, Path.GetFileName(file));
                File.Copy(file, destFile, true);
                if (preserveTimestamps)
                {
                    var fi = new FileInfo(file);
                    File.SetCreationTime(destFile, fi.CreationTime);
                    File.SetLastWriteTime(destFile, fi.LastWriteTime);
                }
            }
            foreach (var dir in Directory.GetDirectories(source))
            {
                CopyDirectory(dir, Path.Combine(dest, Path.GetFileName(dir)), preserveTimestamps);
            }
        }

        private string? HandleCollision(string path, CollisionHandling handling)
        {
            if (!File.Exists(path) && !Directory.Exists(path)) return path;

            return handling switch
            {
                CollisionHandling.Skip => null,
                CollisionHandling.Overwrite => path,
                CollisionHandling.OverwriteIfNewer => path, // Caller handles logic
                CollisionHandling.Rename or CollisionHandling.RenameWithNumber => GetUniqueFileName(path),
                _ => path
            };
        }

        private string GetUniqueFileName(string path)
        {
            var dir = Path.GetDirectoryName(path) ?? "";
            var name = Path.GetFileNameWithoutExtension(path);
            var ext = Path.GetExtension(path);
            var counter = 1;

            while (File.Exists(path) || Directory.Exists(path))
            {
                path = Path.Combine(dir, $"{name} ({counter++}){ext}");
            }
            return path;
        }

        // ====================================================================
        // BULK MOVE
        // ====================================================================

        public async Task<BulkOperationResult> BulkMoveAsync(IEnumerable<string> sources, string destination,
            CollisionHandling collision, IProgress<BulkProgress>? progress = null, CancellationToken ct = default)
        {
            var result = new BulkOperationResult();
            var sw = Stopwatch.StartNew();
            var sourceList = sources.ToList();

            Directory.CreateDirectory(destination);

            foreach (var source in sourceList)
            {
                if (ct.IsCancellationRequested) break;

                try
                {
                    var fileName = Path.GetFileName(source);
                    var destPath = HandleCollision(Path.Combine(destination, fileName), collision);
                    if (destPath == null)
                    {
                        result.SkippedCount++;
                        continue;
                    }

                    if (File.Exists(source))
                    {
                        File.Move(source, destPath, collision == CollisionHandling.Overwrite);
                    }
                    else if (Directory.Exists(source))
                    {
                        Directory.Move(source, destPath);
                    }

                    result.SuccessCount++;
                }
                catch (Exception ex)
                {
                    result.Errors.Add(new OperationError { Path = source, Message = ex.Message, Exception = ex });
                    result.FailedCount++;
                }
            }

            sw.Stop();
            result.Duration = sw.Elapsed;
            result.CanUndo = true;
            return result;
        }

        // ====================================================================
        // BULK DELETE
        // ====================================================================

        public async Task<BulkOperationResult> BulkDeleteAsync(IEnumerable<string> paths, DeleteOptions options,
            IProgress<BulkProgress>? progress = null, CancellationToken ct = default)
        {
            var result = new BulkOperationResult();
            var sw = Stopwatch.StartNew();
            var pathList = paths.ToList();

            foreach (var path in pathList)
            {
                if (ct.IsCancellationRequested) break;

                try
                {
                    if (File.Exists(path))
                    {
                        if (options.SecureWipe)
                        {
                            await SecureWipeFileAsync(path, options.SecureWipePasses, ct);
                        }
                        else if (options.UseRecycleBin)
                        {
                            Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
                        }
                        else
                        {
                            if (options.DeleteReadOnly)
                            {
                                File.SetAttributes(path, FileAttributes.Normal);
                            }
                            File.Delete(path);
                        }
                    }
                    else if (Directory.Exists(path))
                    {
                        if (options.UseRecycleBin)
                        {
                            Microsoft.VisualBasic.FileIO.FileSystem.DeleteDirectory(path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
                        }
                        else
                        {
                            Directory.Delete(path, true);
                        }
                    }

                    result.SuccessCount++;
                }
                catch (Exception ex)
                {
                    result.Errors.Add(new OperationError { Path = path, Message = ex.Message, Exception = ex });
                    result.FailedCount++;
                }
            }

            sw.Stop();
            result.Duration = sw.Elapsed;
            return result;
        }

        private async Task SecureWipeFileAsync(string path, int passes, CancellationToken ct)
        {
            var fileInfo = new FileInfo(path);
            var length = fileInfo.Length;
            var random = new Random();
            var buffer = new byte[64 * 1024];

            for (int pass = 0; pass < passes; pass++)
            {
                await using var stream = new FileStream(path, FileMode.Open, FileAccess.Write);
                stream.Position = 0;

                while (stream.Position < length)
                {
                    if (ct.IsCancellationRequested) return;
                    random.NextBytes(buffer);
                    var bytesToWrite = (int)Math.Min(buffer.Length, length - stream.Position);
                    await stream.WriteAsync(buffer.AsMemory(0, bytesToWrite), ct);
                }
                await stream.FlushAsync(ct);
            }

            File.Delete(path);
        }

        // ====================================================================
        // SMART RENAME
        // ====================================================================

        public async Task<IEnumerable<SmartRenameResult>> SmartRenameAsync(IEnumerable<string> files,
            SmartRenamePattern pattern, CancellationToken ct = default)
        {
            var results = new List<SmartRenameResult>();
            var counter = pattern.CounterStart;

            foreach (var file in files)
            {
                if (ct.IsCancellationRequested) break;

                var result = new SmartRenameResult { OriginalPath = file };
                try
                {
                    var newName = ApplyPattern(file, pattern, counter++);
                    var dir = Path.GetDirectoryName(file) ?? "";
                    var newPath = Path.Combine(dir, newName);

                    if (newPath != file)
                    {
                        File.Move(file, newPath);
                        result.NewPath = newPath;
                        result.Success = true;
                    }
                    else
                    {
                        result.NewPath = file;
                        result.Success = true;
                    }
                }
                catch (Exception ex)
                {
                    result.Success = false;
                    result.Error = ex.Message;
                }
                results.Add(result);
            }

            return results;
        }

        public IEnumerable<SmartRenamePreviewItem> PreviewSmartRename(IEnumerable<string> files, SmartRenamePattern pattern)
        {
            var counter = pattern.CounterStart;
            var previews = new List<SmartRenamePreviewItem>();
            var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var file in files)
            {
                var originalName = Path.GetFileName(file);
                var newName = ApplyPattern(file, pattern, counter++);
                var hasConflict = usedNames.Contains(newName);

                previews.Add(new SmartRenamePreviewItem
                {
                    OriginalName = originalName,
                    NewName = newName,
                    HasConflict = hasConflict,
                    ConflictReason = hasConflict ? "Duplicate name" : null
                });

                usedNames.Add(newName);
            }

            return previews;
        }

        private string ApplyPattern(string filePath, SmartRenamePattern pattern, int counter)
        {
            var name = Path.GetFileNameWithoutExtension(filePath);
            var ext = Path.GetExtension(filePath);
            var fileInfo = new FileInfo(filePath);

            var result = pattern.Pattern
                .Replace("{name}", name)
                .Replace("{ext}", ext.TrimStart('.'))
                .Replace("{counter}", counter.ToString().PadLeft(pattern.CounterPadding, '0'))
                .Replace("{date}", fileInfo.LastWriteTime.ToString(pattern.DateFormat))
                .Replace("{created}", fileInfo.CreationTime.ToString(pattern.DateFormat))
                .Replace("{size}", fileInfo.Length.ToString());

            if (!string.IsNullOrEmpty(pattern.RegexFind))
            {
                result = Regex.Replace(result, pattern.RegexFind, pattern.RegexReplace);
            }

            result = pattern.CaseTransform switch
            {
                CaseTransform.LowerCase => result.ToLowerInvariant(),
                CaseTransform.UpperCase => result.ToUpperInvariant(),
                CaseTransform.TitleCase => System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(result.ToLower()),
                _ => result
            };

            if (pattern.TrimSpaces)
            {
                result = result.Trim();
            }

            foreach (var c in Path.GetInvalidFileNameChars())
            {
                result = result.Replace(c.ToString(), pattern.InvalidCharReplacement);
            }

            return result + ext;
        }

        // ====================================================================
        // FILE ATTRIBUTES
        // ====================================================================

        public async Task<int> SetAttributesAsync(IEnumerable<string> paths, FileAttributeChanges changes,
            bool recursive = false, CancellationToken ct = default)
        {
            int count = 0;
            foreach (var path in paths)
            {
                if (ct.IsCancellationRequested) break;
                count += SetAttributesRecursive(path, changes, recursive);
            }
            return count;
        }

        private int SetAttributesRecursive(string path, FileAttributeChanges changes, bool recursive)
        {
            int count = 0;
            var attr = File.GetAttributes(path);

            if (changes.Hidden.HasValue)
                attr = changes.Hidden.Value ? attr | FileAttributes.Hidden : attr & ~FileAttributes.Hidden;
            if (changes.ReadOnly.HasValue)
                attr = changes.ReadOnly.Value ? attr | FileAttributes.ReadOnly : attr & ~FileAttributes.ReadOnly;
            if (changes.System.HasValue)
                attr = changes.System.Value ? attr | FileAttributes.System : attr & ~FileAttributes.System;
            if (changes.Archive.HasValue)
                attr = changes.Archive.Value ? attr | FileAttributes.Archive : attr & ~FileAttributes.Archive;

            File.SetAttributes(path, attr);
            count++;

            if (recursive && Directory.Exists(path))
            {
                foreach (var file in Directory.GetFiles(path))
                    count += SetAttributesRecursive(file, changes, false);
                foreach (var dir in Directory.GetDirectories(path))
                    count += SetAttributesRecursive(dir, changes, true);
            }

            return count;
        }

        public async Task<int> SetTimestampsAsync(IEnumerable<string> paths, TimestampChanges changes,
            bool recursive = false, CancellationToken ct = default)
        {
            int count = 0;
            foreach (var path in paths)
            {
                if (ct.IsCancellationRequested) break;
                if (changes.CreatedTime.HasValue)
                    File.SetCreationTime(path, changes.CreatedTime.Value);
                if (changes.ModifiedTime.HasValue)
                    File.SetLastWriteTime(path, changes.ModifiedTime.Value);
                if (changes.AccessedTime.HasValue)
                    File.SetLastAccessTime(path, changes.AccessedTime.Value);
                count++;
            }
            return count;
        }

        // ====================================================================
        // PATH OPERATIONS
        // ====================================================================

        public void CopyPathToClipboard(string path, PathFormat format = PathFormat.Windows)
        {
            var formattedPath = format switch
            {
                PathFormat.Unix => path.Replace('\\', '/').Replace("C:", "/c"),
                PathFormat.Uri => new Uri(path).AbsoluteUri,
                PathFormat.Escaped => path.Replace("\\", "\\\\"),
                PathFormat.FileName => Path.GetFileName(path),
                PathFormat.Directory => Path.GetDirectoryName(path) ?? path,
                _ => path
            };

            System.Windows.Clipboard.SetText(formattedPath);
        }

        public async Task<bool> CopyFileContentsToClipboardAsync(string path, int maxSizeKb = 100)
        {
            var fileInfo = new FileInfo(path);
            if (fileInfo.Length > maxSizeKb * 1024) return false;

            var contents = await File.ReadAllTextAsync(path);
            System.Windows.Clipboard.SetText(contents);
            return true;
        }

        public void OpenTerminalAt(string path, TerminalType terminal = TerminalType.PowerShell)
        {
            var dir = File.Exists(path) ? Path.GetDirectoryName(path) : path;
            var (exe, args) = terminal switch
            {
                TerminalType.CommandPrompt => ("cmd.exe", $"/k cd /d \"{dir}\""),
                TerminalType.PowerShell => ("powershell.exe", $"-NoExit -Command \"Set-Location '{dir}'\""),
                TerminalType.WindowsTerminal => ("wt.exe", $"-d \"{dir}\""),
                TerminalType.GitBash => ("git-bash.exe", $"--cd=\"{dir}\""),
                _ => ("powershell.exe", $"-NoExit -Command \"Set-Location '{dir}'\"")
            };

            Process.Start(new ProcessStartInfo(exe, args) { UseShellExecute = true });
        }

        public void OpenAsAdmin(string path)
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true, Verb = "runas" });
        }

        // ====================================================================
        // SYMBOLIC LINKS
        // ====================================================================

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool CreateSymbolicLink(string lpSymlinkFileName, string lpTargetFileName, int dwFlags);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool CreateHardLink(string lpFileName, string lpExistingFileName, IntPtr lpSecurityAttributes);

        public Task<bool> CreateSymbolicLinkAsync(string linkPath, string targetPath, bool isDirectory = false)
        {
            var result = CreateSymbolicLink(linkPath, targetPath, isDirectory ? 1 : 0);
            return Task.FromResult(result);
        }

        public Task<bool> CreateHardLinkAsync(string linkPath, string targetPath)
        {
            var result = CreateHardLink(linkPath, targetPath, IntPtr.Zero);
            return Task.FromResult(result);
        }

        public Task<bool> CreateJunctionAsync(string linkPath, string targetPath)
        {
            try
            {
                var process = Process.Start(new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c mklink /J \"{linkPath}\" \"{targetPath}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false
                });
                process?.WaitForExit();
                return Task.FromResult(Directory.Exists(linkPath));
            }
            catch (Exception)
            {
                return Task.FromResult(false);
            }
        }

        public string? GetLinkTarget(string path)
        {
            var fileInfo = new FileInfo(path);
            if (fileInfo.Attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                return fileInfo.LinkTarget;
            }
            return null;
        }

        // ====================================================================
        // FILE SPLITTING/JOINING
        // ====================================================================

        public async Task<IEnumerable<string>> SplitFileAsync(string path, long partSizeBytes,
            IProgress<BulkProgress>? progress = null, CancellationToken ct = default)
        {
            var parts = new List<string>();
            var fileInfo = new FileInfo(path);
            var totalParts = (int)Math.Ceiling((double)fileInfo.Length / partSizeBytes);
            var baseName = path;

            await using var source = File.OpenRead(path);
            var buffer = new byte[64 * 1024];

            for (int i = 0; i < totalParts; i++)
            {
                if (ct.IsCancellationRequested) break;

                var partPath = $"{baseName}.{i + 1:D3}";
                parts.Add(partPath);

                await using var dest = File.Create(partPath);
                long remaining = Math.Min(partSizeBytes, fileInfo.Length - source.Position);

                while (remaining > 0)
                {
                    var toRead = (int)Math.Min(buffer.Length, remaining);
                    var read = await source.ReadAsync(buffer.AsMemory(0, toRead), ct);
                    if (read == 0) break;
                    await dest.WriteAsync(buffer.AsMemory(0, read), ct);
                    remaining -= read;
                }

                progress?.Report(new BulkProgress
                {
                    CurrentFile = i + 1,
                    TotalFiles = totalParts,
                    Phase = "Splitting"
                });
            }

            return parts;
        }

        public async Task<string> JoinFilesAsync(IEnumerable<string> parts, string outputPath,
            IProgress<BulkProgress>? progress = null, CancellationToken ct = default)
        {
            var partList = parts.OrderBy(p => p).ToList();
            await using var output = File.Create(outputPath);
            var buffer = new byte[64 * 1024];
            var current = 0;

            foreach (var part in partList)
            {
                if (ct.IsCancellationRequested) break;

                await using var input = File.OpenRead(part);
                int read;
                while ((read = await input.ReadAsync(buffer, ct)) > 0)
                {
                    await output.WriteAsync(buffer.AsMemory(0, read), ct);
                }

                progress?.Report(new BulkProgress
                {
                    CurrentFile = ++current,
                    TotalFiles = partList.Count,
                    Phase = "Joining"
                });
            }

            return outputPath;
        }

        // ====================================================================
        // OWNERSHIP
        // ====================================================================

        public async Task<bool> TakeOwnershipAsync(string path, bool recursive = false)
        {
            try
            {
                var process = Process.Start(new ProcessStartInfo
                {
                    FileName = "takeown",
                    Arguments = recursive ? $"/F \"{path}\" /R /D Y" : $"/F \"{path}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    Verb = "runas"
                });
                await (process?.WaitForExitAsync() ?? Task.CompletedTask);
                return process?.ExitCode == 0;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task<bool> GrantFullControlAsync(string path, bool recursive = false)
        {
            try
            {
                var user = WindowsIdentity.GetCurrent().Name;
                var args = recursive ? $"\"{path}\" /grant {user}:F /T" : $"\"{path}\" /grant {user}:F";

                var process = Process.Start(new ProcessStartInfo
                {
                    FileName = "icacls",
                    Arguments = args,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    Verb = "runas"
                });
                await (process?.WaitForExitAsync() ?? Task.CompletedTask);
                return process?.ExitCode == 0;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
