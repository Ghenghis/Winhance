using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Winhance.Core.Features.FileManager.Interfaces;

namespace Winhance.Infrastructure.Features.FileManager.Services
{
    /// <summary>
    /// Service for generating file previews (images, text, code, video, audio, PDF, hex).
    /// Returns platform-agnostic data types for UI layer consumption.
    /// </summary>
    public class PreviewService : IPreviewService
    {
        private readonly ILogger<PreviewService> _logger;
        private readonly Dictionary<string, PreviewType> _extensionMap;
        private readonly string _preferencesPath;
        private readonly object _lock = new();

        private const int MaxTextPreviewBytes = 1024 * 1024; // 1MB
        private const int MaxHexPreviewBytes = 64 * 1024; // 64KB

        // State
        private bool _isPreviewVisible = true;
        private PreviewPosition _position = PreviewPosition.Right;
        private double _previewWidth = 300;
        private double _previewHeight = 200;

        public PreviewService(ILogger<PreviewService> logger)
        {
            _logger = logger;
            _extensionMap = BuildExtensionMap();
            _preferencesPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Winhance", "preview_preferences.json");
        }

        /// <inheritdoc />
        public bool IsPreviewVisible
        {
            get => _isPreviewVisible;
            set
            {
                if (_isPreviewVisible != value)
                {
                    _isPreviewVisible = value;
                    OnPreviewChanged(string.Empty, PreviewType.None, true);
                }
            }
        }

        /// <inheritdoc />
        public PreviewPosition Position
        {
            get => _position;
            set
            {
                if (_position != value)
                {
                    _position = value;
                    OnPreviewChanged(string.Empty, PreviewType.None, true);
                }
            }
        }

        /// <inheritdoc />
        public double PreviewWidth
        {
            get => _previewWidth;
            set => _previewWidth = Math.Max(100, Math.Min(value, 800));
        }

        /// <inheritdoc />
        public double PreviewHeight
        {
            get => _previewHeight;
            set => _previewHeight = Math.Max(100, Math.Min(value, 600));
        }

        /// <inheritdoc />
        public event EventHandler<PreviewChangedEventArgs>? PreviewChanged;

        /// <inheritdoc />
        public PreviewType GetPreviewType(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) return PreviewType.None;

            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            return _extensionMap.TryGetValue(ext, out var type) ? type : PreviewType.None;
        }

        /// <inheritdoc />
        public bool CanPreview(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return false;
            return GetPreviewType(filePath) != PreviewType.None;
        }

        /// <inheritdoc />
        public async Task<Stream?> GetImagePreviewAsync(
            string filePath,
            int maxWidth = 800,
            int maxHeight = 600,
            CancellationToken cancellationToken = default)
        {
            if (!File.Exists(filePath))
            {
                OnPreviewChanged(filePath, PreviewType.Image, false, "File not found");
                return null;
            }

            try
            {
                // Return raw file stream - UI layer handles decoding/resizing
                var memoryStream = new MemoryStream();
                using var fileStream = File.OpenRead(filePath);
                await fileStream.CopyToAsync(memoryStream, cancellationToken);
                memoryStream.Position = 0;

                OnPreviewChanged(filePath, PreviewType.Image, true);
                return memoryStream;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate image preview for: {FilePath}", filePath);
                OnPreviewChanged(filePath, PreviewType.Image, false, ex.Message);
                return null;
            }
        }

        /// <inheritdoc />
        public async Task<TextPreviewResult> GetTextPreviewAsync(
            string filePath,
            int maxLines = 500,
            CancellationToken cancellationToken = default)
        {
            var result = new TextPreviewResult();

            if (!File.Exists(filePath))
            {
                OnPreviewChanged(filePath, PreviewType.Text, false, "File not found");
                return result;
            }

            try
            {
                var fileInfo = new FileInfo(filePath);
                result.FileSize = fileInfo.Length;
                result.IsTruncated = fileInfo.Length > MaxTextPreviewBytes;

                using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

                var lines = new List<string>();
                var totalBytes = 0L;
                var totalLineCount = 0;

                while (!reader.EndOfStream)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var line = await reader.ReadLineAsync();
                    totalLineCount++;

                    if (line != null && lines.Count < maxLines && totalBytes < MaxTextPreviewBytes)
                    {
                        lines.Add(line);
                        totalBytes += Encoding.UTF8.GetByteCount(line) + 2;
                    }
                }

                result.Content = string.Join(Environment.NewLine, lines);
                result.PreviewedLines = lines.Count;
                result.TotalLines = totalLineCount;
                result.Encoding = reader.CurrentEncoding.WebName;
                result.IsTruncated = totalLineCount > maxLines || fileInfo.Length > MaxTextPreviewBytes;

                OnPreviewChanged(filePath, PreviewType.Text, true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate text preview for: {FilePath}", filePath);
                OnPreviewChanged(filePath, PreviewType.Text, false, ex.Message);
            }

            return result;
        }

        /// <inheritdoc />
        public async Task<CodePreviewResult> GetCodePreviewAsync(
            string filePath,
            int maxLines = 500,
            CancellationToken cancellationToken = default)
        {
            var textResult = await GetTextPreviewAsync(filePath, maxLines, cancellationToken);

            var codeResult = new CodePreviewResult
            {
                Content = textResult.Content,
                TotalLines = textResult.TotalLines,
                PreviewedLines = textResult.PreviewedLines,
                FileSize = textResult.FileSize,
                IsTruncated = textResult.IsTruncated,
                Encoding = textResult.Encoding,
                Language = DetectLanguage(filePath),
                Tokens = Array.Empty<SyntaxToken>() // Basic tokenization could be added
            };

            OnPreviewChanged(filePath, PreviewType.Code, true);
            return codeResult;
        }

        /// <inheritdoc />
        public async Task<Stream?> GetVideoThumbnailAsync(
            string filePath,
            TimeSpan? position = null,
            CancellationToken cancellationToken = default)
        {
            // Video thumbnail generation requires FFmpeg or similar
            // Return null - UI layer can use MediaElement for thumbnails
            OnPreviewChanged(filePath, PreviewType.Video, false, "Video thumbnails require FFmpeg");
            return await Task.FromResult<Stream?>(null);
        }

        /// <inheritdoc />
        public async Task<PdfPreviewResult> GetPdfPreviewAsync(
            string filePath,
            int pageNumber = 1,
            CancellationToken cancellationToken = default)
        {
            var result = new PdfPreviewResult
            {
                CurrentPage = pageNumber
            };

            if (!File.Exists(filePath))
            {
                OnPreviewChanged(filePath, PreviewType.Pdf, false, "File not found");
                return result;
            }

            try
            {
                // Basic PDF info from file header
                using var stream = File.OpenRead(filePath);
                var header = new byte[1024];
                await stream.ReadAsync(header, 0, Math.Min(1024, (int)stream.Length), cancellationToken);

                // Check PDF signature
                if (header[0] == 0x25 && header[1] == 0x50 && header[2] == 0x44 && header[3] == 0x46)
                {
                    // It's a PDF - would need PDF library for full rendering
                    result.TotalPages = 1; // Unknown without library
                    OnPreviewChanged(filePath, PreviewType.Pdf, true);
                }
                else
                {
                    OnPreviewChanged(filePath, PreviewType.Pdf, false, "Invalid PDF file");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate PDF preview for: {FilePath}", filePath);
                OnPreviewChanged(filePath, PreviewType.Pdf, false, ex.Message);
            }

            return result;
        }

        /// <inheritdoc />
        public async Task<AudioPreviewResult> GetAudioPreviewAsync(
            string filePath,
            int width = 400,
            int height = 100,
            CancellationToken cancellationToken = default)
        {
            var result = new AudioPreviewResult
            {
                Format = Path.GetExtension(filePath).TrimStart('.').ToUpperInvariant()
            };

            if (!File.Exists(filePath))
            {
                OnPreviewChanged(filePath, PreviewType.Audio, false, "File not found");
                return result;
            }

            try
            {
                // Basic audio info - would need TagLib# or NAudio for full metadata
                var fileInfo = new FileInfo(filePath);

                // Try to estimate bitrate from file size
                // This is approximate - actual metadata requires audio library
                result.Format = Path.GetExtension(filePath).TrimStart('.').ToUpperInvariant();

                OnPreviewChanged(filePath, PreviewType.Audio, true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate audio preview for: {FilePath}", filePath);
                OnPreviewChanged(filePath, PreviewType.Audio, false, ex.Message);
            }

            return result;
        }

        /// <inheritdoc />
        public async Task<HexPreviewResult> GetHexPreviewAsync(
            string filePath,
            long offset = 0,
            int length = 512,
            CancellationToken cancellationToken = default)
        {
            var result = new HexPreviewResult
            {
                StartOffset = offset
            };

            if (!File.Exists(filePath))
            {
                OnPreviewChanged(filePath, PreviewType.Hex, false, "File not found");
                return result;
            }

            try
            {
                var fileInfo = new FileInfo(filePath);
                result.FileSize = fileInfo.Length;

                var readLength = (int)Math.Min(length, Math.Min(MaxHexPreviewBytes, fileInfo.Length - offset));
                if (readLength <= 0)
                {
                    result.Lines = Array.Empty<HexLine>();
                    result.BytesShown = 0;
                    return result;
                }

                var buffer = new byte[readLength];

                using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                stream.Seek(offset, SeekOrigin.Begin);
                var bytesRead = await stream.ReadAsync(buffer, 0, readLength, cancellationToken);

                result.BytesShown = bytesRead;
                result.Lines = GenerateHexLines(buffer, bytesRead, offset);

                OnPreviewChanged(filePath, PreviewType.Hex, true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate hex preview for: {FilePath}", filePath);
                OnPreviewChanged(filePath, PreviewType.Hex, false, ex.Message);
            }

            return result;
        }

        /// <inheritdoc />
        public async Task<FileMetadata> GetFileMetadataAsync(
            string filePath,
            CancellationToken cancellationToken = default)
        {
            var result = new FileMetadata
            {
                FullPath = filePath,
                Name = Path.GetFileName(filePath)
            };

            if (!File.Exists(filePath))
            {
                return result;
            }

            try
            {
                var fileInfo = new FileInfo(filePath);
                result.Size = fileInfo.Length;
                result.SizeDisplay = FormatFileSize(fileInfo.Length);
                result.CreatedTime = fileInfo.CreationTime;
                result.ModifiedTime = fileInfo.LastWriteTime;
                result.AccessedTime = fileInfo.LastAccessTime;
                result.Attributes = fileInfo.Attributes;
                result.IsReadOnly = fileInfo.IsReadOnly;
                result.IsHidden = (fileInfo.Attributes & FileAttributes.Hidden) != 0;
                result.IsSystem = (fileInfo.Attributes & FileAttributes.System) != 0;
                result.TypeDescription = GetTypeDescription(filePath);

                var previewType = GetPreviewType(filePath);

                // Add type-specific metadata
                if (previewType == PreviewType.Image)
                {
                    result.ImageInfo = await GetImageMetadataAsync(filePath, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get metadata for: {FilePath}", filePath);
            }

            return result;
        }

        private async Task<ImageMetadata?> GetImageMetadataAsync(string filePath, CancellationToken cancellationToken)
        {
            try
            {
                using var stream = File.OpenRead(filePath);
                var header = new byte[24];
                await stream.ReadAsync(header, 0, Math.Min(24, (int)stream.Length), cancellationToken);

                var result = new ImageMetadata();

                // PNG dimensions
                if (header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47)
                {
                    result.Width = (header[16] << 24) | (header[17] << 16) | (header[18] << 8) | header[19];
                    result.Height = (header[20] << 24) | (header[21] << 16) | (header[22] << 8) | header[23];
                    result.ColorSpace = "RGBA";
                    return result;
                }

                // BMP dimensions
                if (header[0] == 0x42 && header[1] == 0x4D)
                {
                    stream.Seek(18, SeekOrigin.Begin);
                    var buffer = new byte[8];
                    await stream.ReadAsync(buffer, 0, 8, cancellationToken);
                    result.Width = BitConverter.ToInt32(buffer, 0);
                    result.Height = Math.Abs(BitConverter.ToInt32(buffer, 4));
                    return result;
                }

                // JPEG dimensions
                if (header[0] == 0xFF && header[1] == 0xD8)
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
                            await stream.ReadAsync(heightBytes, 0, 2, cancellationToken);
                            await stream.ReadAsync(widthBytes, 0, 2, cancellationToken);
                            result.Height = (heightBytes[0] << 8) | heightBytes[1];
                            result.Width = (widthBytes[0] << 8) | widthBytes[1];
                            return result;
                        }
                        else if (marker != 0xFF)
                        {
                            var lengthBytes = new byte[2];
                            await stream.ReadAsync(lengthBytes, 0, 2, cancellationToken);
                            var length = (lengthBytes[0] << 8) | lengthBytes[1];
                            stream.Seek(length - 2, SeekOrigin.Current);
                        }
                    }
                }

                return result;
            }
            catch
            {
                return null;
            }
        }

        /// <inheritdoc />
        public void ClearCache()
        {
            // Clear any cached previews
            GC.Collect(0, GCCollectionMode.Optimized);
        }

        /// <inheritdoc />
        public async Task SavePreferencesAsync()
        {
            try
            {
                var dir = Path.GetDirectoryName(_preferencesPath);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);

                var prefs = new PreviewPreferences
                {
                    IsVisible = _isPreviewVisible,
                    Position = _position,
                    Width = _previewWidth,
                    Height = _previewHeight
                };

                var json = JsonSerializer.Serialize(prefs, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(_preferencesPath, json);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save preview preferences");
            }
        }

        /// <inheritdoc />
        public async Task LoadPreferencesAsync()
        {
            try
            {
                if (File.Exists(_preferencesPath))
                {
                    var json = await File.ReadAllTextAsync(_preferencesPath);
                    var prefs = JsonSerializer.Deserialize<PreviewPreferences>(json);
                    if (prefs != null)
                    {
                        _isPreviewVisible = prefs.IsVisible;
                        _position = prefs.Position;
                        _previewWidth = prefs.Width;
                        _previewHeight = prefs.Height;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load preview preferences");
            }
        }

        private HexLine[] GenerateHexLines(byte[] data, int length, long baseOffset)
        {
            const int bytesPerLine = 16;
            var lines = new List<HexLine>();

            for (int i = 0; i < length; i += bytesPerLine)
            {
                var lineLength = Math.Min(bytesPerLine, length - i);
                var bytes = new byte[lineLength];
                Array.Copy(data, i, bytes, 0, lineLength);

                var ascii = new StringBuilder();
                for (int j = 0; j < lineLength; j++)
                {
                    var b = bytes[j];
                    ascii.Append(b >= 32 && b < 127 ? (char)b : '.');
                }

                lines.Add(new HexLine
                {
                    Offset = baseOffset + i,
                    Bytes = bytes,
                    Ascii = ascii.ToString()
                });
            }

            return lines.ToArray();
        }

        private string DetectLanguage(string path)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            return ext switch
            {
                ".cs" => "csharp",
                ".js" => "javascript",
                ".ts" or ".tsx" => "typescript",
                ".jsx" => "javascript",
                ".py" => "python",
                ".java" => "java",
                ".cpp" or ".c" or ".h" or ".hpp" => "cpp",
                ".rs" => "rust",
                ".go" => "go",
                ".rb" => "ruby",
                ".php" => "php",
                ".swift" => "swift",
                ".kt" => "kotlin",
                ".scala" => "scala",
                ".sql" => "sql",
                ".html" or ".htm" => "html",
                ".css" => "css",
                ".scss" or ".sass" => "scss",
                ".less" => "less",
                ".json" => "json",
                ".xml" or ".xaml" => "xml",
                ".yaml" or ".yml" => "yaml",
                ".toml" => "toml",
                ".md" or ".markdown" => "markdown",
                ".sh" or ".bash" => "bash",
                ".ps1" => "powershell",
                ".bat" or ".cmd" => "batch",
                _ => "plaintext"
            };
        }

        private string GetTypeDescription(string path)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            return ext switch
            {
                ".txt" => "Text Document",
                ".pdf" => "PDF Document",
                ".doc" or ".docx" => "Word Document",
                ".xls" or ".xlsx" => "Excel Spreadsheet",
                ".ppt" or ".pptx" => "PowerPoint Presentation",
                ".jpg" or ".jpeg" => "JPEG Image",
                ".png" => "PNG Image",
                ".gif" => "GIF Image",
                ".bmp" => "Bitmap Image",
                ".mp3" => "MP3 Audio",
                ".wav" => "WAV Audio",
                ".mp4" => "MP4 Video",
                ".mkv" => "MKV Video",
                ".avi" => "AVI Video",
                ".zip" => "ZIP Archive",
                ".rar" => "RAR Archive",
                ".7z" => "7-Zip Archive",
                ".exe" => "Executable",
                ".dll" => "Dynamic Link Library",
                ".cs" => "C# Source File",
                ".js" => "JavaScript File",
                ".py" => "Python Script",
                _ => $"{ext.TrimStart('.').ToUpperInvariant()} File"
            };
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

        private void OnPreviewChanged(string filePath, PreviewType type, bool success, string? errorMessage = null)
        {
            PreviewChanged?.Invoke(this, new PreviewChangedEventArgs
            {
                FilePath = filePath,
                PreviewType = type,
                Success = success,
                ErrorMessage = errorMessage
            });
        }

        private Dictionary<string, PreviewType> BuildExtensionMap()
        {
            return new Dictionary<string, PreviewType>(StringComparer.OrdinalIgnoreCase)
            {
                // Images
                { ".jpg", PreviewType.Image }, { ".jpeg", PreviewType.Image },
                { ".png", PreviewType.Image }, { ".gif", PreviewType.Image },
                { ".bmp", PreviewType.Image }, { ".webp", PreviewType.Image },
                { ".ico", PreviewType.Image }, { ".tiff", PreviewType.Image },
                { ".tif", PreviewType.Image }, { ".svg", PreviewType.Image },
                { ".heic", PreviewType.Image }, { ".heif", PreviewType.Image },

                // Videos
                { ".mp4", PreviewType.Video }, { ".mkv", PreviewType.Video },
                { ".avi", PreviewType.Video }, { ".mov", PreviewType.Video },
                { ".wmv", PreviewType.Video }, { ".flv", PreviewType.Video },
                { ".webm", PreviewType.Video }, { ".m4v", PreviewType.Video },
                { ".mpeg", PreviewType.Video }, { ".mpg", PreviewType.Video },

                // Audio
                { ".mp3", PreviewType.Audio }, { ".wav", PreviewType.Audio },
                { ".flac", PreviewType.Audio }, { ".aac", PreviewType.Audio },
                { ".ogg", PreviewType.Audio }, { ".wma", PreviewType.Audio },
                { ".m4a", PreviewType.Audio }, { ".opus", PreviewType.Audio },

                // Text
                { ".txt", PreviewType.Text }, { ".log", PreviewType.Text },
                { ".ini", PreviewType.Text }, { ".cfg", PreviewType.Text },
                { ".conf", PreviewType.Text },

                // Code
                { ".cs", PreviewType.Code }, { ".js", PreviewType.Code },
                { ".ts", PreviewType.Code }, { ".tsx", PreviewType.Code },
                { ".jsx", PreviewType.Code }, { ".py", PreviewType.Code },
                { ".java", PreviewType.Code }, { ".cpp", PreviewType.Code },
                { ".c", PreviewType.Code }, { ".h", PreviewType.Code },
                { ".hpp", PreviewType.Code }, { ".rs", PreviewType.Code },
                { ".go", PreviewType.Code }, { ".rb", PreviewType.Code },
                { ".php", PreviewType.Code }, { ".html", PreviewType.Code },
                { ".htm", PreviewType.Code }, { ".css", PreviewType.Code },
                { ".scss", PreviewType.Code }, { ".sass", PreviewType.Code },
                { ".json", PreviewType.Code }, { ".xml", PreviewType.Code },
                { ".xaml", PreviewType.Code }, { ".yaml", PreviewType.Code },
                { ".yml", PreviewType.Code }, { ".toml", PreviewType.Code },
                { ".sql", PreviewType.Code }, { ".sh", PreviewType.Code },
                { ".bash", PreviewType.Code }, { ".ps1", PreviewType.Code },
                { ".bat", PreviewType.Code }, { ".cmd", PreviewType.Code },

                // Markdown
                { ".md", PreviewType.Markdown }, { ".markdown", PreviewType.Markdown },

                // PDF
                { ".pdf", PreviewType.Pdf },

                // Office
                { ".doc", PreviewType.Office }, { ".docx", PreviewType.Office },
                { ".xls", PreviewType.Office }, { ".xlsx", PreviewType.Office },
                { ".ppt", PreviewType.Office }, { ".pptx", PreviewType.Office },

                // Archives
                { ".zip", PreviewType.Archive }, { ".7z", PreviewType.Archive },
                { ".rar", PreviewType.Archive }, { ".tar", PreviewType.Archive },
                { ".gz", PreviewType.Archive }, { ".bz2", PreviewType.Archive },
                { ".xz", PreviewType.Archive }
            };
        }

        private class PreviewPreferences
        {
            public bool IsVisible { get; set; } = true;
            public PreviewPosition Position { get; set; } = PreviewPosition.Right;
            public double Width { get; set; } = 300;
            public double Height { get; set; } = 200;
        }
    }
}
