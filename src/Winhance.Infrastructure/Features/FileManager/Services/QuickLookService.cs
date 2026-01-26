using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Microsoft.Extensions.Logging;
using Winhance.Core.Features.FileManager.Interfaces;

namespace Winhance.Infrastructure.Features.FileManager.Services
{
    /// <summary>
    /// Service for Quick Look (spacebar preview) functionality.
    /// Provides content data for the UI layer to display.
    /// </summary>
    public class QuickLookService : IQuickLookService, IDisposable
    {
        private readonly ILogger<QuickLookService> _logger;
        private readonly object _lock = new();

        // State
        private bool _isShowing;
        private string? _currentFilePath;
        private bool _isEnabled = true;
        private bool _isFullScreen;
        private double _zoomLevel = 1.0;
        private bool _isSlideshowActive;
        private int _slideshowIndex;
        private string[]? _slideshowFiles;
        private System.Timers.Timer? _slideshowTimer;
        private bool _disposed;

        // Content type mappings
        private static readonly Dictionary<string, QuickLookContentType> ExtensionToContentType = new(StringComparer.OrdinalIgnoreCase)
        {
            // Images
            { ".jpg", QuickLookContentType.Image },
            { ".jpeg", QuickLookContentType.Image },
            { ".png", QuickLookContentType.Image },
            { ".gif", QuickLookContentType.Image },
            { ".bmp", QuickLookContentType.Image },
            { ".webp", QuickLookContentType.Image },
            { ".ico", QuickLookContentType.Image },
            { ".tiff", QuickLookContentType.Image },
            { ".tif", QuickLookContentType.Image },
            { ".svg", QuickLookContentType.Image },
            { ".heic", QuickLookContentType.Image },
            { ".heif", QuickLookContentType.Image },
            { ".raw", QuickLookContentType.Image },

            // Videos
            { ".mp4", QuickLookContentType.Video },
            { ".mkv", QuickLookContentType.Video },
            { ".avi", QuickLookContentType.Video },
            { ".mov", QuickLookContentType.Video },
            { ".wmv", QuickLookContentType.Video },
            { ".flv", QuickLookContentType.Video },
            { ".webm", QuickLookContentType.Video },
            { ".m4v", QuickLookContentType.Video },
            { ".mpeg", QuickLookContentType.Video },
            { ".mpg", QuickLookContentType.Video },

            // Audio
            { ".mp3", QuickLookContentType.Audio },
            { ".wav", QuickLookContentType.Audio },
            { ".flac", QuickLookContentType.Audio },
            { ".aac", QuickLookContentType.Audio },
            { ".ogg", QuickLookContentType.Audio },
            { ".wma", QuickLookContentType.Audio },
            { ".m4a", QuickLookContentType.Audio },
            { ".opus", QuickLookContentType.Audio },

            // Text
            { ".txt", QuickLookContentType.Text },
            { ".log", QuickLookContentType.Text },
            { ".ini", QuickLookContentType.Text },
            { ".cfg", QuickLookContentType.Text },
            { ".conf", QuickLookContentType.Text },

            // Code
            { ".cs", QuickLookContentType.Code },
            { ".js", QuickLookContentType.Code },
            { ".ts", QuickLookContentType.Code },
            { ".py", QuickLookContentType.Code },
            { ".java", QuickLookContentType.Code },
            { ".cpp", QuickLookContentType.Code },
            { ".c", QuickLookContentType.Code },
            { ".h", QuickLookContentType.Code },
            { ".rs", QuickLookContentType.Code },
            { ".go", QuickLookContentType.Code },
            { ".html", QuickLookContentType.Code },
            { ".css", QuickLookContentType.Code },
            { ".scss", QuickLookContentType.Code },
            { ".json", QuickLookContentType.Code },
            { ".xml", QuickLookContentType.Code },
            { ".yaml", QuickLookContentType.Code },
            { ".yml", QuickLookContentType.Code },
            { ".sql", QuickLookContentType.Code },
            { ".sh", QuickLookContentType.Code },
            { ".ps1", QuickLookContentType.Code },
            { ".bat", QuickLookContentType.Code },
            { ".cmd", QuickLookContentType.Code },
            { ".xaml", QuickLookContentType.Code },

            // PDF
            { ".pdf", QuickLookContentType.Pdf },

            // Markdown
            { ".md", QuickLookContentType.Markdown },
            { ".markdown", QuickLookContentType.Markdown },

            // Archives
            { ".zip", QuickLookContentType.Archive },
            { ".rar", QuickLookContentType.Archive },
            { ".7z", QuickLookContentType.Archive },
            { ".tar", QuickLookContentType.Archive },
            { ".gz", QuickLookContentType.Archive },
            { ".bz2", QuickLookContentType.Archive },
            { ".xz", QuickLookContentType.Archive },
            { ".iso", QuickLookContentType.Archive }
        };

        public QuickLookService(ILogger<QuickLookService> logger)
        {
            _logger = logger;
        }

        /// <inheritdoc />
        public bool IsShowing => _isShowing;

        /// <inheritdoc />
        public string? CurrentFilePath => _currentFilePath;

        /// <inheritdoc />
        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (_isEnabled != value)
                {
                    _isEnabled = value;
                    if (!_isEnabled && _isShowing)
                    {
                        Hide();
                    }
                }
            }
        }

        /// <inheritdoc />
        public event EventHandler<QuickLookStateEventArgs>? StateChanged;

        /// <inheritdoc />
        public async Task ShowQuickLookAsync(string filePath, Stream? previewImage = null, CancellationToken cancellationToken = default)
        {
            if (!_isEnabled || string.IsNullOrEmpty(filePath))
                return;

            try
            {
                lock (_lock)
                {
                    _currentFilePath = filePath;
                    _isShowing = true;
                    _zoomLevel = 1.0;
                }

                OnStateChanged();
                _logger.LogDebug("Quick Look shown for: {FilePath}", filePath);
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to show Quick Look for: {FilePath}", filePath);
                Hide();
            }
        }

        /// <inheritdoc />
        public void Hide()
        {
            lock (_lock)
            {
                if (!_isShowing) return;

                _isShowing = false;
                _currentFilePath = null;
                _isFullScreen = false;
                StopSlideshow();
            }

            OnStateChanged();
            _logger.LogDebug("Quick Look hidden");
        }

        /// <inheritdoc />
        public async Task ToggleAsync(string filePath)
        {
            if (_isShowing && _currentFilePath == filePath)
            {
                Hide();
            }
            else
            {
                await ShowQuickLookAsync(filePath);
            }
        }

        /// <inheritdoc />
        public async Task NavigateNextAsync(string[] files)
        {
            if (files == null || files.Length == 0 || _currentFilePath == null)
                return;

            var currentIndex = Array.IndexOf(files, _currentFilePath);
            if (currentIndex < 0) currentIndex = 0;

            var nextIndex = (currentIndex + 1) % files.Length;
            await ShowQuickLookAsync(files[nextIndex]);
        }

        /// <inheritdoc />
        public async Task NavigatePreviousAsync(string[] files)
        {
            if (files == null || files.Length == 0 || _currentFilePath == null)
                return;

            var currentIndex = Array.IndexOf(files, _currentFilePath);
            if (currentIndex < 0) currentIndex = 0;

            var prevIndex = (currentIndex - 1 + files.Length) % files.Length;
            await ShowQuickLookAsync(files[prevIndex]);
        }

        /// <inheritdoc />
        public void EnterFullScreen()
        {
            lock (_lock)
            {
                _isFullScreen = true;
            }
            OnStateChanged();
        }

        /// <inheritdoc />
        public void ExitFullScreen()
        {
            lock (_lock)
            {
                _isFullScreen = false;
            }
            OnStateChanged();
        }

        /// <inheritdoc />
        public void ToggleFullScreen()
        {
            lock (_lock)
            {
                _isFullScreen = !_isFullScreen;
            }
            OnStateChanged();
        }

        /// <inheritdoc />
        public void ZoomIn()
        {
            lock (_lock)
            {
                _zoomLevel = Math.Min(_zoomLevel * 1.25, 10.0);
            }
            OnStateChanged();
        }

        /// <inheritdoc />
        public void ZoomOut()
        {
            lock (_lock)
            {
                _zoomLevel = Math.Max(_zoomLevel / 1.25, 0.1);
            }
            OnStateChanged();
        }

        /// <inheritdoc />
        public void ZoomToFit()
        {
            lock (_lock)
            {
                _zoomLevel = 1.0;
            }
            OnStateChanged();
        }

        /// <inheritdoc />
        public void SetZoom(double zoomLevel)
        {
            lock (_lock)
            {
                _zoomLevel = Math.Max(0.1, Math.Min(zoomLevel, 10.0));
            }
            OnStateChanged();
        }

        /// <inheritdoc />
        public double GetZoom()
        {
            lock (_lock)
            {
                return _zoomLevel;
            }
        }

        /// <inheritdoc />
        public void OpenWithDefaultApp()
        {
            var filePath = _currentFilePath;
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                return;

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = filePath,
                    UseShellExecute = true
                });
                Hide();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to open file with default app: {FilePath}", filePath);
            }
        }

        /// <inheritdoc />
        public async Task StartSlideshowAsync(string[] files, int intervalSeconds = 3)
        {
            if (files == null || files.Length == 0)
                return;

            StopSlideshow();

            lock (_lock)
            {
                _slideshowFiles = files;
                _slideshowIndex = 0;
                _isSlideshowActive = true;
            }

            await ShowQuickLookAsync(files[0]);

            _slideshowTimer = new System.Timers.Timer(intervalSeconds * 1000);
            _slideshowTimer.Elapsed += async (s, e) => await OnSlideshowTimerElapsed();
            _slideshowTimer.Start();

            OnStateChanged();
        }

        /// <inheritdoc />
        public void StopSlideshow()
        {
            lock (_lock)
            {
                _isSlideshowActive = false;
                _slideshowFiles = null;
                _slideshowIndex = 0;
            }

            if (_slideshowTimer != null)
            {
                _slideshowTimer.Stop();
                _slideshowTimer.Dispose();
                _slideshowTimer = null;
            }

            OnStateChanged();
        }

        private async Task OnSlideshowTimerElapsed()
        {
            string[]? files;
            int nextIndex;

            lock (_lock)
            {
                if (!_isSlideshowActive || _slideshowFiles == null)
                    return;

                files = _slideshowFiles;
                _slideshowIndex = (_slideshowIndex + 1) % files.Length;
                nextIndex = _slideshowIndex;
            }

            await ShowQuickLookAsync(files[nextIndex]);
        }

        /// <inheritdoc />
        public async Task<QuickLookContent> GetContentAsync(string filePath, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                return new QuickLookContent
                {
                    ContentType = QuickLookContentType.Error,
                    ErrorMessage = "Invalid file path"
                };
            }

            if (!File.Exists(filePath))
            {
                return new QuickLookContent
                {
                    ContentType = QuickLookContentType.Error,
                    FilePath = filePath,
                    FileName = Path.GetFileName(filePath),
                    ErrorMessage = "File not found"
                };
            }

            try
            {
                var fileInfo = new FileInfo(filePath);
                var extension = fileInfo.Extension.ToLowerInvariant();
                var contentType = GetContentType(extension);

                var content = new QuickLookContent
                {
                    ContentType = contentType,
                    FilePath = filePath,
                    FileName = fileInfo.Name,
                    FileSize = fileInfo.Length,
                    FileSizeDisplay = FormatFileSize(fileInfo.Length),
                    InfoLines = GetInfoLines(fileInfo)
                };

                // Load content based on type
                switch (contentType)
                {
                    case QuickLookContentType.Image:
                        await LoadImageContentAsync(content, filePath, cancellationToken);
                        break;
                    case QuickLookContentType.Text:
                    case QuickLookContentType.Code:
                    case QuickLookContentType.Markdown:
                        await LoadTextContentAsync(content, filePath, cancellationToken);
                        break;
                    case QuickLookContentType.Video:
                    case QuickLookContentType.Audio:
                        content.CanPlay = true;
                        break;
                    case QuickLookContentType.Pdf:
                        // PDF handled by UI layer
                        break;
                    case QuickLookContentType.Archive:
                        await LoadArchiveInfoAsync(content, filePath, cancellationToken);
                        break;
                    default:
                        content.ContentType = QuickLookContentType.IconOnly;
                        break;
                }

                return content;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get content for: {FilePath}", filePath);
                return new QuickLookContent
                {
                    ContentType = QuickLookContentType.Error,
                    FilePath = filePath,
                    FileName = Path.GetFileName(filePath),
                    ErrorMessage = ex.Message
                };
            }
        }

        private QuickLookContentType GetContentType(string extension)
        {
            if (ExtensionToContentType.TryGetValue(extension, out var type))
                return type;
            return QuickLookContentType.Unknown;
        }

        private async Task LoadImageContentAsync(QuickLookContent content, string filePath, CancellationToken cancellationToken)
        {
            try
            {
                // Load image as stream for UI layer
                var memoryStream = new MemoryStream();
                using var fileStream = File.OpenRead(filePath);
                await fileStream.CopyToAsync(memoryStream, cancellationToken);
                memoryStream.Position = 0;
                content.ImageStream = memoryStream;

                // Try to get dimensions from common image formats
                TryGetImageDimensions(filePath, content);
            }
            catch (Exception ex)
            {
                content.ErrorMessage = $"Failed to load image: {ex.Message}";
            }
        }

        private void TryGetImageDimensions(string filePath, QuickLookContent content)
        {
            try
            {
                // Read just the header bytes to get dimensions
                using var stream = File.OpenRead(filePath);
                var header = new byte[24];
                stream.Read(header, 0, Math.Min(24, (int)stream.Length));

                var extension = Path.GetExtension(filePath).ToLowerInvariant();

                // PNG
                if (header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47)
                {
                    content.NaturalWidth = (header[16] << 24) | (header[17] << 16) | (header[18] << 8) | header[19];
                    content.NaturalHeight = (header[20] << 24) | (header[21] << 16) | (header[22] << 8) | header[23];
                }
                // BMP
                else if (header[0] == 0x42 && header[1] == 0x4D)
                {
                    stream.Seek(18, SeekOrigin.Begin);
                    var buffer = new byte[8];
                    stream.Read(buffer, 0, 8);
                    content.NaturalWidth = BitConverter.ToInt32(buffer, 0);
                    content.NaturalHeight = Math.Abs(BitConverter.ToInt32(buffer, 4));
                }
                // JPEG
                else if (header[0] == 0xFF && header[1] == 0xD8)
                {
                    stream.Seek(2, SeekOrigin.Begin);
                    while (stream.Position < stream.Length - 10)
                    {
                        if (stream.ReadByte() != 0xFF) continue;
                        var marker = stream.ReadByte();
                        if (marker >= 0xC0 && marker <= 0xC3)
                        {
                            stream.Seek(3, SeekOrigin.Current);
                            var heightBytes = new byte[2];
                            var widthBytes = new byte[2];
                            stream.Read(heightBytes, 0, 2);
                            stream.Read(widthBytes, 0, 2);
                            content.NaturalHeight = (heightBytes[0] << 8) | heightBytes[1];
                            content.NaturalWidth = (widthBytes[0] << 8) | widthBytes[1];
                            break;
                        }
                        else if (marker != 0xFF)
                        {
                            var lengthBytes = new byte[2];
                            stream.Read(lengthBytes, 0, 2);
                            var length = (lengthBytes[0] << 8) | lengthBytes[1];
                            stream.Seek(length - 2, SeekOrigin.Current);
                        }
                    }
                }
            }
            catch
            {
                // Dimensions not critical
            }
        }

        private async Task LoadTextContentAsync(QuickLookContent content, string filePath, CancellationToken cancellationToken)
        {
            try
            {
                const int maxChars = 100_000;
                const int maxBytes = 500_000;

                var fileInfo = new FileInfo(filePath);
                if (fileInfo.Length > maxBytes)
                {
                    // Read first portion only
                    using var stream = File.OpenRead(filePath);
                    using var reader = new StreamReader(stream, Encoding.UTF8, true);
                    var buffer = new char[maxChars];
                    var read = await reader.ReadAsync(buffer, 0, maxChars);
                    content.TextContent = new string(buffer, 0, read);
                    if (fileInfo.Length > maxBytes)
                    {
                        content.TextContent += $"\n\n... (truncated, file is {FormatFileSize(fileInfo.Length)})";
                    }
                }
                else
                {
                    content.TextContent = await File.ReadAllTextAsync(filePath, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                content.ErrorMessage = $"Failed to load text: {ex.Message}";
            }
        }

        private async Task LoadArchiveInfoAsync(QuickLookContent content, string filePath, CancellationToken cancellationToken)
        {
            try
            {
                var extension = Path.GetExtension(filePath).ToLowerInvariant();
                var infoLines = new List<string>(content.InfoLines);

                if (extension == ".zip")
                {
                    using var archive = System.IO.Compression.ZipFile.OpenRead(filePath);
                    infoLines.Add($"Entries: {archive.Entries.Count}");

                    long uncompressedSize = 0;
                    foreach (var entry in archive.Entries)
                    {
                        uncompressedSize += entry.Length;
                    }
                    infoLines.Add($"Uncompressed: {FormatFileSize(uncompressedSize)}");
                }
                else
                {
                    infoLines.Add("Archive preview available for .zip files");
                }

                content.InfoLines = infoLines.ToArray();
            }
            catch (Exception ex)
            {
                content.ErrorMessage = $"Failed to read archive: {ex.Message}";
            }

            await Task.CompletedTask;
        }

        private string[] GetInfoLines(FileInfo fileInfo)
        {
            return new[]
            {
                $"Modified: {fileInfo.LastWriteTime:g}",
                $"Created: {fileInfo.CreationTime:g}",
                $"Attributes: {GetAttributesString(fileInfo.Attributes)}"
            };
        }

        private string GetAttributesString(FileAttributes attributes)
        {
            var parts = new List<string>();
            if (attributes.HasFlag(FileAttributes.ReadOnly)) parts.Add("Read-only");
            if (attributes.HasFlag(FileAttributes.Hidden)) parts.Add("Hidden");
            if (attributes.HasFlag(FileAttributes.System)) parts.Add("System");
            if (attributes.HasFlag(FileAttributes.Archive)) parts.Add("Archive");
            return parts.Count > 0 ? string.Join(", ", parts) : "Normal";
        }

        private string FormatFileSize(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
            int suffixIndex = 0;
            double size = bytes;

            while (size >= 1024 && suffixIndex < suffixes.Length - 1)
            {
                size /= 1024;
                suffixIndex++;
            }

            return suffixIndex == 0
                ? $"{size:N0} {suffixes[suffixIndex]}"
                : $"{size:N2} {suffixes[suffixIndex]}";
        }

        private void OnStateChanged()
        {
            StateChanged?.Invoke(this, new QuickLookStateEventArgs
            {
                IsShowing = _isShowing,
                FilePath = _currentFilePath,
                IsFullScreen = _isFullScreen,
                IsSlideshowActive = _isSlideshowActive
            });
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (!_disposed)
            {
                StopSlideshow();
                _disposed = true;
            }
        }
    }
}
