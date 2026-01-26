using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Winhance.Core.Features.FileManager.Interfaces;

namespace Winhance.Infrastructure.Features.FileManager.Services
{
    /// <summary>
    /// Service for clipboard operations including cut, copy, paste for files and folders.
    /// </summary>
    public class ClipboardService : IClipboardService
    {
        private ClipboardOperation _currentOperation = ClipboardOperation.None;
        private List<string> _clipboardItems = new();
        private readonly object _lock = new();
        private bool _isMonitoring;

        /// <inheritdoc />
        public ClipboardOperation CurrentOperation
        {
            get { lock (_lock) return _currentOperation; }
        }

        /// <inheritdoc />
        public IReadOnlyList<string> ClipboardItems
        {
            get { lock (_lock) return _clipboardItems.AsReadOnly(); }
        }

        /// <inheritdoc />
        public bool HasItems => ClipboardItems.Count > 0;

        /// <inheritdoc />
        public bool CanPaste => HasItems && _currentOperation != ClipboardOperation.None;

        /// <inheritdoc />
        public event EventHandler<ClipboardChangedEventArgs>? ClipboardChanged;

        /// <inheritdoc />
        public void Copy(IEnumerable<string> paths)
        {
            lock (_lock)
            {
                _clipboardItems = paths.Where(p => File.Exists(p) || Directory.Exists(p)).ToList();
                _currentOperation = ClipboardOperation.Copy;
                OnClipboardChanged();
            }
        }

        /// <inheritdoc />
        public void Cut(IEnumerable<string> paths)
        {
            lock (_lock)
            {
                _clipboardItems = paths.Where(p => File.Exists(p) || Directory.Exists(p)).ToList();
                _currentOperation = ClipboardOperation.Cut;
                OnClipboardChanged();
            }
        }

        /// <inheritdoc />
        public async Task<PasteResult> PasteAsync(string destinationPath, IProgress<PasteProgress>? progress = null)
        {
            List<string> items;
            ClipboardOperation operation;

            lock (_lock)
            {
                items = _clipboardItems.ToList();
                operation = _currentOperation;
            }

            if (items.Count == 0)
            {
                return new PasteResult
                {
                    Success = false,
                    Errors = new List<PasteError> { new PasteError { Message = "No items in clipboard" } }
                };
            }

            var result = new PasteResult
            {
                Operation = operation
            };

            try
            {
                long totalSize = CalculateTotalSize(items);
                var copyState = new CopyProgressState { TotalSize = totalSize };
                int processedCount = 0;

                foreach (var sourcePath in items)
                {
                    var fileName = Path.GetFileName(sourcePath);
                    var destPath = Path.Combine(destinationPath, fileName);

                    // Handle name conflicts
                    destPath = GetUniqueDestinationPath(destPath);

                    try
                    {
                        if (Directory.Exists(sourcePath))
                        {
                            if (operation == ClipboardOperation.Cut)
                            {
                                Directory.Move(sourcePath, destPath);
                            }
                            else
                            {
                                await CopyDirectoryAsync(sourcePath, destPath, progress, processedCount, items.Count, copyState);
                            }
                            result.ItemsPasted++;
                        }
                        else if (File.Exists(sourcePath))
                        {
                            var fileInfo = new FileInfo(sourcePath);
                            var fileSize = fileInfo.Length;

                            if (operation == ClipboardOperation.Cut)
                            {
                                File.Move(sourcePath, destPath);
                            }
                            else
                            {
                                await CopyFileWithProgressAsync(sourcePath, destPath, progress, processedCount, items.Count, copyState);
                            }

                            result.BytesProcessed += fileSize;
                            result.ItemsPasted++;
                        }
                    }
                    catch (Exception ex)
                    {
                        result.Errors.Add(new PasteError
                        {
                            SourcePath = sourcePath,
                            DestinationPath = destPath,
                            Message = ex.Message,
                            Exception = ex
                        });
                        result.ItemsFailed++;
                    }

                    processedCount++;
                    progress?.Report(new PasteProgress
                    {
                        CurrentFile = fileName,
                        ProcessedItems = processedCount,
                        TotalItems = items.Count,
                        BytesProcessed = copyState.ProcessedSize,
                        TotalBytes = totalSize
                    });
                }

                // Clear clipboard if it was a cut operation
                if (operation == ClipboardOperation.Cut && result.ItemsFailed == 0)
                {
                    Clear();
                }

                result.Success = result.ItemsFailed == 0;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Errors.Add(new PasteError { Message = ex.Message, Exception = ex });
            }

            return result;
        }

        /// <inheritdoc />
        public void Clear()
        {
            lock (_lock)
            {
                _clipboardItems.Clear();
                _currentOperation = ClipboardOperation.None;
                OnClipboardChanged();
            }
        }

        /// <inheritdoc />
        public void CopyAsText(IEnumerable<string> paths, PathTextFormat format = PathTextFormat.FullPath)
        {
            var formatted = paths.Select(p => FormatPath(p, format));
            var text = format == PathTextFormat.MultiLine
                ? string.Join(Environment.NewLine, formatted)
                : format == PathTextFormat.Semicolon
                    ? string.Join(";", formatted)
                    : string.Join(Environment.NewLine, formatted);

            // Note: Actual clipboard text setting would need platform-specific implementation
            // For now, we store it internally
        }

        /// <inheritdoc />
        public void CopyPathAsText(string path, PathTextFormat format = PathTextFormat.FullPath)
        {
            CopyAsText(new[] { path }, format);
        }

        /// <inheritdoc />
        public IEnumerable<string> GetPathsFromText()
        {
            // Note: Would need platform-specific clipboard access
            // Return empty for now - UI layer should handle actual clipboard access
            return Enumerable.Empty<string>();
        }

        /// <inheritdoc />
        public bool ContainsPath(string path)
        {
            lock (_lock)
            {
                return _clipboardItems.Any(p =>
                    string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
            }
        }

        /// <inheritdoc />
        public bool IsCutOperation(string path)
        {
            lock (_lock)
            {
                return _currentOperation == ClipboardOperation.Cut &&
                       _clipboardItems.Any(p =>
                           string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
            }
        }

        /// <inheritdoc />
        public void StartMonitoring()
        {
            _isMonitoring = true;
            // Note: Actual clipboard monitoring would need platform-specific implementation
            // The UI layer (WPF) should handle actual Windows clipboard monitoring
        }

        /// <inheritdoc />
        public void StopMonitoring()
        {
            _isMonitoring = false;
        }

        private string GetUniqueDestinationPath(string destPath)
        {
            if (!File.Exists(destPath) && !Directory.Exists(destPath))
                return destPath;

            var directory = Path.GetDirectoryName(destPath) ?? string.Empty;
            var fileName = Path.GetFileNameWithoutExtension(destPath);
            var extension = Path.GetExtension(destPath);

            int counter = 1;
            string newPath;
            do
            {
                newPath = Path.Combine(directory, $"{fileName} ({counter}){extension}");
                counter++;
            }
            while (File.Exists(newPath) || Directory.Exists(newPath));

            return newPath;
        }

        private long CalculateTotalSize(IEnumerable<string> paths)
        {
            long total = 0;
            foreach (var path in paths)
            {
                try
                {
                    if (File.Exists(path))
                    {
                        total += new FileInfo(path).Length;
                    }
                    else if (Directory.Exists(path))
                    {
                        total += GetDirectorySize(path);
                    }
                }
                catch { }
            }
            return total;
        }

        private long GetDirectorySize(string path)
        {
            long size = 0;
            try
            {
                foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                {
                    try { size += new FileInfo(file).Length; } catch { }
                }
            }
            catch { }
            return size;
        }

        /// <summary>
        /// State class to track copy progress across async calls.
        /// </summary>
        private class CopyProgressState
        {
            public long ProcessedSize { get; set; }
            public long TotalSize { get; set; }
        }

        private async Task CopyDirectoryAsync(
            string sourceDir,
            string destDir,
            IProgress<PasteProgress>? progress,
            int currentItem,
            int totalItems,
            CopyProgressState state)
        {
            Directory.CreateDirectory(destDir);

            foreach (var file in Directory.GetFiles(sourceDir))
            {
                var destFile = Path.Combine(destDir, Path.GetFileName(file));
                await CopyFileWithProgressAsync(file, destFile, progress, currentItem, totalItems, state);
            }

            foreach (var dir in Directory.GetDirectories(sourceDir))
            {
                var destSubDir = Path.Combine(destDir, Path.GetFileName(dir));
                await CopyDirectoryAsync(dir, destSubDir, progress, currentItem, totalItems, state);
            }
        }

        private async Task CopyFileWithProgressAsync(
            string source,
            string destination,
            IProgress<PasteProgress>? progress,
            int currentItem,
            int totalItems,
            CopyProgressState state)
        {
            const int bufferSize = 81920; // 80KB buffer
            var buffer = new byte[bufferSize];
            var fileInfo = new FileInfo(source);
            var fileSize = fileInfo.Length;
            long bytesRead = 0;

            using var sourceStream = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize, true);
            using var destStream = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize, true);

            int read;
            while ((read = await sourceStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                await destStream.WriteAsync(buffer, 0, read);
                bytesRead += read;

                progress?.Report(new PasteProgress
                {
                    CurrentFile = Path.GetFileName(source),
                    ProcessedItems = currentItem,
                    TotalItems = totalItems,
                    BytesProcessed = state.ProcessedSize + bytesRead,
                    TotalBytes = state.TotalSize
                });
            }

            state.ProcessedSize += fileSize;

            // Preserve file attributes
            try
            {
                File.SetAttributes(destination, fileInfo.Attributes);
                File.SetCreationTime(destination, fileInfo.CreationTime);
                File.SetLastWriteTime(destination, fileInfo.LastWriteTime);
            }
            catch { }
        }

        private string FormatPath(string path, PathTextFormat format)
        {
            return format switch
            {
                PathTextFormat.FullPath => path,
                PathTextFormat.NameOnly => Path.GetFileName(path),
                PathTextFormat.Quoted => $"\"{path}\"",
                PathTextFormat.MultiLine => path,
                PathTextFormat.Semicolon => path,
                PathTextFormat.UnixStyle => "/" + path.Replace(":", "").Replace("\\", "/"),
                PathTextFormat.HtmlLinks => $"<a href=\"file:///{path.Replace("\\", "/").Replace(" ", "%20")}\">{Path.GetFileName(path)}</a>",
                _ => path
            };
        }

        private void OnClipboardChanged()
        {
            ClipboardChanged?.Invoke(this, new ClipboardChangedEventArgs
            {
                Operation = _currentOperation,
                ItemCount = _clipboardItems.Count,
                HasFiles = _clipboardItems.Count > 0,
                HasText = false
            });
        }
    }
}
