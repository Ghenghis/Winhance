using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Winhance.Core.Features.FileManager.Interfaces;

namespace Winhance.Infrastructure.Features.FileManager.Services
{
    /// <summary>
    /// Service for reading and writing file metadata (EXIF, ID3, document properties).
    /// Uses raw file parsing to avoid WinForms/WPF dependencies.
    /// </summary>
    public class MetadataService : IMetadataService
    {
        private readonly ILogger<MetadataService> _logger;

        private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".tiff", ".tif", ".webp", ".heic", ".heif", ".raw"
        };

        private static readonly HashSet<string> AudioExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".mp3", ".flac", ".wav", ".aac", ".ogg", ".wma", ".m4a", ".opus", ".aiff"
        };

        private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".flv", ".webm", ".m4v", ".mpeg", ".mpg"
        };

        private static readonly HashSet<string> DocumentExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".odt", ".ods", ".odp", ".rtf"
        };

        public MetadataService(ILogger<MetadataService> logger)
        {
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task<ExtendedMetadata> GetMetadataAsync(string filePath, CancellationToken cancellationToken = default)
        {
            var result = new ExtendedMetadata { FilePath = filePath };

            if (!File.Exists(filePath))
            {
                return result;
            }

            try
            {
                var extension = Path.GetExtension(filePath).ToLowerInvariant();
                result.FileType = GetFileType(extension);

                // Get basic properties
                var fileInfo = new FileInfo(filePath);
                result.AllProperties["Size"] = fileInfo.Length.ToString();
                result.AllProperties["Created"] = fileInfo.CreationTime.ToString("O");
                result.AllProperties["Modified"] = fileInfo.LastWriteTime.ToString("O");

                // Get type-specific metadata
                switch (result.FileType)
                {
                    case MetadataFileType.Image:
                        result.ImageMetadata = await GetImageMetadataAsync(filePath, cancellationToken);
                        break;
                    case MetadataFileType.Audio:
                        result.AudioMetadata = await GetAudioMetadataAsync(filePath, cancellationToken);
                        break;
                    case MetadataFileType.Video:
                        result.VideoMetadata = await GetVideoMetadataAsync(filePath, cancellationToken);
                        break;
                    case MetadataFileType.Document:
                        result.DocumentMetadata = await GetDocumentMetadataAsync(filePath, cancellationToken);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get metadata for: {FilePath}", filePath);
            }

            return result;
        }

        /// <inheritdoc />
        public async Task<ImageExifMetadata> GetImageMetadataAsync(string filePath, CancellationToken cancellationToken = default)
        {
            var result = new ImageExifMetadata();

            if (!File.Exists(filePath))
            {
                return result;
            }

            try
            {
                await using var stream = File.OpenRead(filePath);
                var header = new byte[24];
                await stream.ReadAsync(header, 0, Math.Min(24, (int)stream.Length), cancellationToken);

                // Get dimensions based on format
                if (header[0] == 0x89 && header[1] == 0x50) // PNG
                {
                    result.Width = (header[16] << 24) | (header[17] << 16) | (header[18] << 8) | header[19];
                    result.Height = (header[20] << 24) | (header[21] << 16) | (header[22] << 8) | header[23];
                    result.BitDepth = header[24] == 0 ? 8 : header[24];
                    result.ColorSpace = "RGBA";
                }
                else if (header[0] == 0x42 && header[1] == 0x4D) // BMP
                {
                    stream.Seek(18, SeekOrigin.Begin);
                    var buffer = new byte[8];
                    await stream.ReadAsync(buffer, 0, 8, cancellationToken);
                    result.Width = BitConverter.ToInt32(buffer, 0);
                    result.Height = Math.Abs(BitConverter.ToInt32(buffer, 4));
                }
                else if (header[0] == 0xFF && header[1] == 0xD8) // JPEG
                {
                    await ParseJpegMetadataAsync(stream, result, cancellationToken);
                }
                else if (header[0] == 0x47 && header[1] == 0x49 && header[2] == 0x46) // GIF
                {
                    result.Width = header[6] | (header[7] << 8);
                    result.Height = header[8] | (header[9] << 8);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get image metadata for: {FilePath}", filePath);
            }

            return result;
        }

        private async Task ParseJpegMetadataAsync(Stream stream, ImageExifMetadata result, CancellationToken cancellationToken)
        {
            stream.Seek(2, SeekOrigin.Begin);

            while (stream.Position < stream.Length - 10)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (stream.ReadByte() != 0xFF) continue;
                var marker = stream.ReadByte();

                // SOF markers contain dimensions
                if (marker >= 0xC0 && marker <= 0xC3)
                {
                    stream.Seek(3, SeekOrigin.Current);
                    var heightBytes = new byte[2];
                    var widthBytes = new byte[2];
                    await stream.ReadAsync(heightBytes, 0, 2, cancellationToken);
                    await stream.ReadAsync(widthBytes, 0, 2, cancellationToken);
                    result.Height = (heightBytes[0] << 8) | heightBytes[1];
                    result.Width = (widthBytes[0] << 8) | widthBytes[1];
                    break;
                }
                else if (marker == 0xE1) // EXIF marker
                {
                    var lengthBytes = new byte[2];
                    await stream.ReadAsync(lengthBytes, 0, 2, cancellationToken);
                    var length = (lengthBytes[0] << 8) | lengthBytes[1];

                    // Skip EXIF data for now - full parsing requires EXIF library
                    stream.Seek(length - 2, SeekOrigin.Current);
                }
                else if (marker != 0xFF && marker != 0x00 && marker != 0xD8 && marker != 0xD9)
                {
                    var lengthBytes = new byte[2];
                    await stream.ReadAsync(lengthBytes, 0, 2, cancellationToken);
                    var length = (lengthBytes[0] << 8) | lengthBytes[1];
                    stream.Seek(length - 2, SeekOrigin.Current);
                }
            }
        }

        /// <inheritdoc />
        public async Task<AudioTagMetadata> GetAudioMetadataAsync(string filePath, CancellationToken cancellationToken = default)
        {
            var result = new AudioTagMetadata();

            if (!File.Exists(filePath))
            {
                return result;
            }

            try
            {
                var fileInfo = new FileInfo(filePath);
                var extension = fileInfo.Extension.ToLowerInvariant();

                result.Codec = extension.TrimStart('.').ToUpperInvariant();

                await using var stream = File.OpenRead(filePath);
                var header = new byte[128];
                await stream.ReadAsync(header, 0, Math.Min(128, (int)stream.Length), cancellationToken);

                // Try to detect MP3 and read basic info
                if (header[0] == 0xFF && (header[1] & 0xE0) == 0xE0) // MP3 frame sync
                {
                    // Parse MP3 header for bitrate/sample rate
                    var version = (header[1] >> 3) & 3;
                    var layer = (header[1] >> 1) & 3;
                    var bitrateIndex = (header[2] >> 4) & 0xF;
                    var sampleRateIndex = (header[2] >> 2) & 3;

                    result.BitRate = GetMp3Bitrate(version, layer, bitrateIndex);
                    result.SampleRate = GetMp3SampleRate(version, sampleRateIndex);
                    result.Channels = ((header[3] >> 6) & 3) == 3 ? 1 : 2;

                    if (result.BitRate > 0)
                    {
                        result.Duration = TimeSpan.FromSeconds(fileInfo.Length * 8.0 / (result.BitRate * 1000));
                    }

                    // Try to read ID3v1 tag at end of file
                    if (stream.Length >= 128)
                    {
                        stream.Seek(-128, SeekOrigin.End);
                        var id3v1 = new byte[128];
                        await stream.ReadAsync(id3v1, 0, 128, cancellationToken);

                        if (id3v1[0] == 'T' && id3v1[1] == 'A' && id3v1[2] == 'G')
                        {
                            result.Title = ReadId3String(id3v1, 3, 30);
                            result.Artist = ReadId3String(id3v1, 33, 30);
                            result.Album = ReadId3String(id3v1, 63, 30);
                            var yearStr = ReadId3String(id3v1, 93, 4);
                            if (int.TryParse(yearStr, out var year))
                                result.Year = year;
                        }
                    }
                }
                else if (header[0] == 'f' && header[1] == 'L' && header[2] == 'a' && header[3] == 'C') // FLAC
                {
                    result.Codec = "FLAC";
                    // Would need full FLAC parser for metadata
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get audio metadata for: {FilePath}", filePath);
            }

            return result;
        }

        private string ReadId3String(byte[] data, int offset, int length)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < length && offset + i < data.Length; i++)
            {
                var b = data[offset + i];
                if (b == 0) break;
                sb.Append((char)b);
            }
            return sb.ToString().Trim();
        }

        private int GetMp3Bitrate(int version, int layer, int index)
        {
            // MPEG 1 Layer 3 bitrates
            int[] mp3Bitrates = { 0, 32, 40, 48, 56, 64, 80, 96, 112, 128, 160, 192, 224, 256, 320, 0 };
            if (version == 3 && layer == 1 && index > 0 && index < 15)
                return mp3Bitrates[index];
            return 0;
        }

        private int GetMp3SampleRate(int version, int index)
        {
            int[,] sampleRates = {
                { 11025, 12000, 8000, 0 },  // MPEG 2.5
                { 0, 0, 0, 0 },              // Reserved
                { 22050, 24000, 16000, 0 }, // MPEG 2
                { 44100, 48000, 32000, 0 }  // MPEG 1
            };
            if (index < 3)
                return sampleRates[version, index];
            return 0;
        }

        /// <inheritdoc />
        public async Task<VideoFileMetadata> GetVideoMetadataAsync(string filePath, CancellationToken cancellationToken = default)
        {
            var result = new VideoFileMetadata();

            if (!File.Exists(filePath))
            {
                return result;
            }

            try
            {
                var fileInfo = new FileInfo(filePath);
                var extension = fileInfo.Extension.ToLowerInvariant();

                result.ContainerFormat = extension.TrimStart('.').ToUpperInvariant();

                // Basic video detection would require FFprobe or similar
                // For now, just return container format
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get video metadata for: {FilePath}", filePath);
            }

            return result;
        }

        /// <inheritdoc />
        public async Task<DocumentMetadata> GetDocumentMetadataAsync(string filePath, CancellationToken cancellationToken = default)
        {
            var result = new DocumentMetadata();

            if (!File.Exists(filePath))
            {
                return result;
            }

            try
            {
                var extension = Path.GetExtension(filePath).ToLowerInvariant();

                await using var stream = File.OpenRead(filePath);
                var header = new byte[8];
                await stream.ReadAsync(header, 0, Math.Min(8, (int)stream.Length), cancellationToken);

                // Check for PDF
                if (header[0] == '%' && header[1] == 'P' && header[2] == 'D' && header[3] == 'F')
                {
                    // Basic PDF - would need PDF library for full parsing
                    result.Creator = "PDF Document";
                }
                // Check for OOXML (Office 2007+)
                else if (header[0] == 0x50 && header[1] == 0x4B && header[2] == 0x03 && header[3] == 0x04)
                {
                    result.Creator = extension switch
                    {
                        ".docx" => "Microsoft Word",
                        ".xlsx" => "Microsoft Excel",
                        ".pptx" => "Microsoft PowerPoint",
                        _ => "Office Document"
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get document metadata for: {FilePath}", filePath);
            }

            return result;
        }

        /// <inheritdoc />
        public async Task SetAudioMetadataAsync(string filePath, AudioTagMetadata metadata, CancellationToken cancellationToken = default)
        {
            // Setting audio metadata requires TagLib# or similar library
            // This is a placeholder for when the dependency is added
            _logger.LogWarning("SetAudioMetadataAsync not implemented - requires TagLib# library");
            await Task.CompletedTask;
        }

        /// <inheritdoc />
        public async Task RemoveMetadataAsync(string filePath, MetadataTypes metadataTypes = MetadataTypes.All, CancellationToken cancellationToken = default)
        {
            // Metadata removal requires format-specific handling
            _logger.LogWarning("RemoveMetadataAsync not implemented - requires format-specific libraries");
            await Task.CompletedTask;
        }

        /// <inheritdoc />
        public async Task CopyMetadataAsync(string sourcePath, string destPath, MetadataTypes metadataTypes = MetadataTypes.All, CancellationToken cancellationToken = default)
        {
            // Copy basic file attributes
            try
            {
                if (File.Exists(sourcePath) && File.Exists(destPath))
                {
                    var sourceInfo = new FileInfo(sourcePath);
                    File.SetCreationTime(destPath, sourceInfo.CreationTime);
                    File.SetLastWriteTime(destPath, sourceInfo.LastWriteTime);
                    File.SetAttributes(destPath, sourceInfo.Attributes);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to copy metadata from {Source} to {Dest}", sourcePath, destPath);
            }

            await Task.CompletedTask;
        }

        /// <inheritdoc />
        public IEnumerable<MetadataField> GetAvailableFields(string fileExtension)
        {
            var ext = fileExtension.ToLowerInvariant();
            var fields = new List<MetadataField>();

            if (ImageExtensions.Contains(ext))
            {
                fields.AddRange(new[]
                {
                    new MetadataField { Name = "Width", DisplayName = "Width", FieldType = MetadataFieldType.Integer, Category = "Dimensions" },
                    new MetadataField { Name = "Height", DisplayName = "Height", FieldType = MetadataFieldType.Integer, Category = "Dimensions" },
                    new MetadataField { Name = "BitDepth", DisplayName = "Bit Depth", FieldType = MetadataFieldType.Integer, Category = "Format" },
                    new MetadataField { Name = "ColorSpace", DisplayName = "Color Space", FieldType = MetadataFieldType.String, Category = "Format" },
                    new MetadataField { Name = "DateTaken", DisplayName = "Date Taken", FieldType = MetadataFieldType.DateTime, Category = "EXIF" },
                    new MetadataField { Name = "CameraMake", DisplayName = "Camera Make", FieldType = MetadataFieldType.String, Category = "EXIF" },
                    new MetadataField { Name = "CameraModel", DisplayName = "Camera Model", FieldType = MetadataFieldType.String, Category = "EXIF" }
                });
            }
            else if (AudioExtensions.Contains(ext))
            {
                fields.AddRange(new[]
                {
                    new MetadataField { Name = "Title", DisplayName = "Title", FieldType = MetadataFieldType.String, IsWritable = true, Category = "Tags" },
                    new MetadataField { Name = "Artist", DisplayName = "Artist", FieldType = MetadataFieldType.String, IsWritable = true, Category = "Tags" },
                    new MetadataField { Name = "Album", DisplayName = "Album", FieldType = MetadataFieldType.String, IsWritable = true, Category = "Tags" },
                    new MetadataField { Name = "Year", DisplayName = "Year", FieldType = MetadataFieldType.Integer, IsWritable = true, Category = "Tags" },
                    new MetadataField { Name = "Genre", DisplayName = "Genre", FieldType = MetadataFieldType.String, IsWritable = true, Category = "Tags" },
                    new MetadataField { Name = "Duration", DisplayName = "Duration", FieldType = MetadataFieldType.String, Category = "Audio" },
                    new MetadataField { Name = "BitRate", DisplayName = "Bit Rate", FieldType = MetadataFieldType.Integer, Category = "Audio" },
                    new MetadataField { Name = "SampleRate", DisplayName = "Sample Rate", FieldType = MetadataFieldType.Integer, Category = "Audio" }
                });
            }
            else if (VideoExtensions.Contains(ext))
            {
                fields.AddRange(new[]
                {
                    new MetadataField { Name = "Width", DisplayName = "Width", FieldType = MetadataFieldType.Integer, Category = "Video" },
                    new MetadataField { Name = "Height", DisplayName = "Height", FieldType = MetadataFieldType.Integer, Category = "Video" },
                    new MetadataField { Name = "Duration", DisplayName = "Duration", FieldType = MetadataFieldType.String, Category = "Video" },
                    new MetadataField { Name = "FrameRate", DisplayName = "Frame Rate", FieldType = MetadataFieldType.Decimal, Category = "Video" },
                    new MetadataField { Name = "VideoCodec", DisplayName = "Video Codec", FieldType = MetadataFieldType.String, Category = "Codec" },
                    new MetadataField { Name = "AudioCodec", DisplayName = "Audio Codec", FieldType = MetadataFieldType.String, Category = "Codec" }
                });
            }
            else if (DocumentExtensions.Contains(ext))
            {
                fields.AddRange(new[]
                {
                    new MetadataField { Name = "Title", DisplayName = "Title", FieldType = MetadataFieldType.String, IsWritable = true, Category = "Document" },
                    new MetadataField { Name = "Author", DisplayName = "Author", FieldType = MetadataFieldType.String, IsWritable = true, Category = "Document" },
                    new MetadataField { Name = "Subject", DisplayName = "Subject", FieldType = MetadataFieldType.String, IsWritable = true, Category = "Document" },
                    new MetadataField { Name = "Keywords", DisplayName = "Keywords", FieldType = MetadataFieldType.String, IsWritable = true, Category = "Document" },
                    new MetadataField { Name = "PageCount", DisplayName = "Page Count", FieldType = MetadataFieldType.Integer, Category = "Document" }
                });
            }

            return fields;
        }

        /// <inheritdoc />
        public bool SupportsMetadata(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) return false;

            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            return ImageExtensions.Contains(ext) ||
                   AudioExtensions.Contains(ext) ||
                   VideoExtensions.Contains(ext) ||
                   DocumentExtensions.Contains(ext);
        }

        private MetadataFileType GetFileType(string extension)
        {
            if (ImageExtensions.Contains(extension)) return MetadataFileType.Image;
            if (AudioExtensions.Contains(extension)) return MetadataFileType.Audio;
            if (VideoExtensions.Contains(extension)) return MetadataFileType.Video;
            if (DocumentExtensions.Contains(extension)) return MetadataFileType.Document;
            return MetadataFileType.Unknown;
        }
    }
}
