using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.FileManager.Interfaces;

namespace Winhance.Infrastructure.Features.FileManager.Services
{
    /// <summary>
    /// Service for archive operations - browsing, extracting, and creating archives.
    /// </summary>
    public class ArchiveService : IArchiveService
    {
        private readonly List<ArchiveFormat> _supportedFormats;

        /// <inheritdoc />
        public IReadOnlyList<ArchiveFormat> SupportedFormats => _supportedFormats.AsReadOnly();

        public ArchiveService()
        {
            _supportedFormats = new List<ArchiveFormat>
            {
                new() { Name = "ZIP", Extensions = new[] { ".zip" }, CanCreate = true, CanExtract = true, SupportsEncryption = true },
                new() { Name = "7-Zip", Extensions = new[] { ".7z" }, CanCreate = false, CanExtract = false, SupportsEncryption = true },
                new() { Name = "RAR", Extensions = new[] { ".rar" }, CanCreate = false, CanExtract = false, SupportsEncryption = true },
                new() { Name = "TAR", Extensions = new[] { ".tar" }, CanCreate = false, CanExtract = false },
                new() { Name = "GZip", Extensions = new[] { ".gz", ".tgz" }, CanCreate = false, CanExtract = false },
            };
        }

        /// <inheritdoc />
        public bool IsArchive(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) return false;
            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            return _supportedFormats.Any(f => f.Extensions.Contains(ext));
        }

        /// <inheritdoc />
        public async Task<ArchiveInfo> GetArchiveInfoAsync(string archivePath, CancellationToken cancellationToken = default)
        {
            var info = new ArchiveInfo
            {
                Path = archivePath,
                Format = GetFormatName(archivePath)
            };

            try
            {
                var fileInfo = new FileInfo(archivePath);
                info.Size = fileInfo.Length;
                info.CreatedDate = fileInfo.CreationTime;

                if (IsZipArchive(archivePath))
                {
                    using var archive = ZipFile.OpenRead(archivePath);
                    info.EntryCount = archive.Entries.Count(e => !e.FullName.EndsWith("/"));
                    info.FolderCount = archive.Entries.Count(e => e.FullName.EndsWith("/"));
                    info.UncompressedSize = archive.Entries.Sum(e => e.Length);
                }
            }
            catch (Exception)
            {
                // Return partial info on error
            }

            return info;
        }

        /// <inheritdoc />
        public async Task<IEnumerable<ArchiveEntry>> ListContentsAsync(string archivePath, string internalPath = "", CancellationToken cancellationToken = default)
        {
            var entries = new List<ArchiveEntry>();

            if (!IsZipArchive(archivePath))
                return entries;

            using var archive = ZipFile.OpenRead(archivePath);
            var prefix = string.IsNullOrEmpty(internalPath) ? "" : internalPath.TrimEnd('/') + "/";

            foreach (var entry in archive.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var fullName = entry.FullName.Replace('\\', '/');
                if (!string.IsNullOrEmpty(prefix) && !fullName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    continue;

                var relativePath = string.IsNullOrEmpty(prefix) ? fullName : fullName.Substring(prefix.Length);
                if (string.IsNullOrEmpty(relativePath)) continue;

                // Only include direct children
                var slashIndex = relativePath.IndexOf('/');
                if (slashIndex >= 0 && slashIndex < relativePath.Length - 1)
                    continue;

                entries.Add(new ArchiveEntry
                {
                    Name = Path.GetFileName(relativePath.TrimEnd('/')),
                    FullPath = fullName,
                    IsDirectory = entry.FullName.EndsWith("/"),
                    UncompressedSize = entry.Length,
                    CompressedSize = entry.CompressedLength,
                    ModifiedDate = entry.LastWriteTime.DateTime,
                    Crc = entry.Crc32
                });
            }

            return entries.OrderByDescending(e => e.IsDirectory).ThenBy(e => e.Name);
        }

        /// <inheritdoc />
        public async Task<ExtractionResult> ExtractAllAsync(
            string archivePath,
            string destinationPath,
            ExtractionOptions? options = null,
            IProgress<ExtractionProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            options ??= new ExtractionOptions();
            var result = new ExtractionResult { DestinationPath = destinationPath };

            if (!IsZipArchive(archivePath))
            {
                result.Errors.Add("Unsupported archive format");
                return result;
            }

            try
            {
                Directory.CreateDirectory(destinationPath);

                using var archive = ZipFile.OpenRead(archivePath);
                var entries = archive.Entries.Where(e => !e.FullName.EndsWith("/")).ToList();
                var totalEntries = entries.Count;
                var totalBytes = entries.Sum(e => e.Length);
                long extractedBytes = 0;

                for (int i = 0; i < entries.Count; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var entry = entries[i];
                    var destPath = options.PreserveDirectoryStructure
                        ? Path.Combine(destinationPath, entry.FullName)
                        : Path.Combine(destinationPath, entry.Name);

                    // Check exclude patterns
                    if (options.ExcludePatterns != null && MatchesPattern(entry.Name, options.ExcludePatterns))
                    {
                        result.EntriesSkipped++;
                        continue;
                    }

                    try
                    {
                        if (File.Exists(destPath) && !options.OverwriteExisting)
                        {
                            result.EntriesSkipped++;
                            continue;
                        }

                        var destDir = Path.GetDirectoryName(destPath);
                        if (!string.IsNullOrEmpty(destDir))
                            Directory.CreateDirectory(destDir);

                        entry.ExtractToFile(destPath, options.OverwriteExisting);

                        if (options.PreserveTimestamps)
                        {
                            File.SetLastWriteTime(destPath, entry.LastWriteTime.DateTime);
                        }

                        result.EntriesExtracted++;
                        extractedBytes += entry.Length;
                        result.BytesExtracted = extractedBytes;
                    }
                    catch (Exception ex)
                    {
                        result.EntriesFailed++;
                        result.Errors.Add($"{entry.Name}: {ex.Message}");
                    }

                    progress?.Report(new ExtractionProgress
                    {
                        CurrentEntry = entry.Name,
                        TotalEntries = totalEntries,
                        ExtractedEntries = result.EntriesExtracted + result.EntriesSkipped + result.EntriesFailed,
                        TotalBytes = totalBytes,
                        ExtractedBytes = extractedBytes
                    });
                }

                result.Success = result.EntriesFailed == 0;
            }
            catch (Exception ex)
            {
                result.Errors.Add(ex.Message);
            }

            return result;
        }

        /// <inheritdoc />
        public async Task<ExtractionResult> ExtractEntriesAsync(
            string archivePath,
            IEnumerable<string> entries,
            string destinationPath,
            ExtractionOptions? options = null,
            IProgress<ExtractionProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            options ??= new ExtractionOptions();
            var result = new ExtractionResult { DestinationPath = destinationPath };
            var entryPaths = entries.ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (!IsZipArchive(archivePath))
            {
                result.Errors.Add("Unsupported archive format");
                return result;
            }

            try
            {
                Directory.CreateDirectory(destinationPath);

                using var archive = ZipFile.OpenRead(archivePath);
                var matchingEntries = archive.Entries
                    .Where(e => entryPaths.Contains(e.FullName) && !e.FullName.EndsWith("/"))
                    .ToList();

                var totalEntries = matchingEntries.Count;
                var totalBytes = matchingEntries.Sum(e => e.Length);
                long extractedBytes = 0;

                foreach (var entry in matchingEntries)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var destPath = options.PreserveDirectoryStructure
                        ? Path.Combine(destinationPath, entry.FullName)
                        : Path.Combine(destinationPath, entry.Name);

                    try
                    {
                        if (File.Exists(destPath) && !options.OverwriteExisting)
                        {
                            result.EntriesSkipped++;
                            continue;
                        }

                        var destDir = Path.GetDirectoryName(destPath);
                        if (!string.IsNullOrEmpty(destDir))
                            Directory.CreateDirectory(destDir);

                        entry.ExtractToFile(destPath, options.OverwriteExisting);
                        result.EntriesExtracted++;
                        extractedBytes += entry.Length;
                        result.BytesExtracted = extractedBytes;
                    }
                    catch (Exception ex)
                    {
                        result.EntriesFailed++;
                        result.Errors.Add($"{entry.Name}: {ex.Message}");
                    }

                    progress?.Report(new ExtractionProgress
                    {
                        CurrentEntry = entry.Name,
                        TotalEntries = totalEntries,
                        ExtractedEntries = result.EntriesExtracted + result.EntriesSkipped + result.EntriesFailed,
                        TotalBytes = totalBytes,
                        ExtractedBytes = extractedBytes
                    });
                }

                result.Success = result.EntriesFailed == 0;
            }
            catch (Exception ex)
            {
                result.Errors.Add(ex.Message);
            }

            return result;
        }

        /// <inheritdoc />
        public async Task ExtractToStreamAsync(string archivePath, string entryPath, Stream outputStream, CancellationToken cancellationToken = default)
        {
            if (!IsZipArchive(archivePath))
                throw new NotSupportedException("Only ZIP format is supported");

            using var archive = ZipFile.OpenRead(archivePath);
            var entry = archive.GetEntry(entryPath);

            if (entry == null)
                throw new FileNotFoundException($"Entry not found: {entryPath}");

            using var entryStream = entry.Open();
            await entryStream.CopyToAsync(outputStream, cancellationToken);
        }

        /// <inheritdoc />
        public async Task<ArchiveResult> CreateArchiveAsync(
            string archivePath,
            IEnumerable<string> sourcePaths,
            ArchiveOptions? options = null,
            IProgress<ArchiveProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            options ??= new ArchiveOptions();
            var result = new ArchiveResult { ArchivePath = archivePath };

            try
            {
                var files = CollectFiles(sourcePaths, options);
                var totalFiles = files.Count;
                var totalBytes = files.Sum(f => new FileInfo(f.FullPath).Length);
                long processedBytes = 0;

                var compressionLevel = options.CompressionLevel switch
                {
                    0 => CompressionLevel.NoCompression,
                    <= 3 => CompressionLevel.Fastest,
                    <= 6 => CompressionLevel.Optimal,
                    _ => CompressionLevel.SmallestSize
                };

                using var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create);

                for (int i = 0; i < files.Count; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var (fullPath, relativePath) = files[i];

                    try
                    {
                        archive.CreateEntryFromFile(fullPath, relativePath, compressionLevel);
                        result.FilesAdded++;

                        var fileSize = new FileInfo(fullPath).Length;
                        result.OriginalSize += fileSize;
                        processedBytes += fileSize;
                    }
                    catch (Exception ex)
                    {
                        result.Errors.Add($"{relativePath}: {ex.Message}");
                    }

                    progress?.Report(new ArchiveProgress
                    {
                        CurrentFile = Path.GetFileName(fullPath),
                        TotalFiles = totalFiles,
                        ProcessedFiles = i + 1,
                        TotalBytes = totalBytes,
                        ProcessedBytes = processedBytes
                    });
                }

                result.Success = result.Errors.Count == 0;
                result.CompressedSize = new FileInfo(archivePath).Length;
            }
            catch (Exception ex)
            {
                result.Errors.Add(ex.Message);
            }

            return result;
        }

        /// <inheritdoc />
        public async Task<ArchiveResult> AddToArchiveAsync(
            string archivePath,
            IEnumerable<string> sourcePaths,
            ArchiveOptions? options = null,
            IProgress<ArchiveProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            options ??= new ArchiveOptions();
            var result = new ArchiveResult { ArchivePath = archivePath };

            if (!IsZipArchive(archivePath))
            {
                result.Errors.Add("Unsupported archive format");
                return result;
            }

            try
            {
                var files = CollectFiles(sourcePaths, options);
                var totalFiles = files.Count;

                using var archive = ZipFile.Open(archivePath, ZipArchiveMode.Update);

                for (int i = 0; i < files.Count; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var (fullPath, relativePath) = files[i];

                    try
                    {
                        // Remove existing entry if present
                        var existingEntry = archive.GetEntry(relativePath);
                        existingEntry?.Delete();

                        archive.CreateEntryFromFile(fullPath, relativePath);
                        result.FilesAdded++;
                        result.OriginalSize += new FileInfo(fullPath).Length;
                    }
                    catch (Exception ex)
                    {
                        result.Errors.Add($"{relativePath}: {ex.Message}");
                    }

                    progress?.Report(new ArchiveProgress
                    {
                        CurrentFile = Path.GetFileName(fullPath),
                        TotalFiles = totalFiles,
                        ProcessedFiles = i + 1
                    });
                }

                result.Success = result.Errors.Count == 0;
                result.CompressedSize = new FileInfo(archivePath).Length;
            }
            catch (Exception ex)
            {
                result.Errors.Add(ex.Message);
            }

            return result;
        }

        /// <inheritdoc />
        public async Task<ArchiveTestResult> TestArchiveAsync(string archivePath, CancellationToken cancellationToken = default)
        {
            var result = new ArchiveTestResult();

            if (!IsZipArchive(archivePath))
            {
                result.Errors.Add("Unsupported archive format");
                return result;
            }

            try
            {
                using var archive = ZipFile.OpenRead(archivePath);

                foreach (var entry in archive.Entries)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (entry.FullName.EndsWith("/"))
                        continue;

                    result.EntriesTested++;

                    try
                    {
                        using var stream = entry.Open();
                        var buffer = new byte[4096];
                        while (await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken) > 0)
                        {
                            // Just read through to verify
                        }
                        result.EntriesPassed++;
                    }
                    catch (Exception ex)
                    {
                        result.EntriesFailed++;
                        result.Errors.Add($"{entry.Name}: {ex.Message}");
                    }
                }

                result.IsValid = result.EntriesFailed == 0;
            }
            catch (Exception ex)
            {
                result.Errors.Add(ex.Message);
            }

            return result;
        }

        /// <inheritdoc />
        public async Task<IEnumerable<ArchiveEntry>> SearchAsync(string archivePath, string pattern, CancellationToken cancellationToken = default)
        {
            var entries = new List<ArchiveEntry>();

            if (!IsZipArchive(archivePath))
                return entries;

            var regex = WildcardToRegex(pattern);

            using var archive = ZipFile.OpenRead(archivePath);

            foreach (var entry in archive.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (regex.IsMatch(entry.Name) || regex.IsMatch(entry.FullName))
                {
                    entries.Add(new ArchiveEntry
                    {
                        Name = Path.GetFileName(entry.FullName.TrimEnd('/')),
                        FullPath = entry.FullName,
                        IsDirectory = entry.FullName.EndsWith("/"),
                        UncompressedSize = entry.Length,
                        CompressedSize = entry.CompressedLength,
                        ModifiedDate = entry.LastWriteTime.DateTime,
                        Crc = entry.Crc32
                    });
                }
            }

            return entries;
        }

        private bool IsZipArchive(string path)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            return ext == ".zip";
        }

        private string GetFormatName(string path)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            return _supportedFormats.FirstOrDefault(f => f.Extensions.Contains(ext))?.Name ?? "Unknown";
        }

        private List<(string FullPath, string RelativePath)> CollectFiles(IEnumerable<string> sourcePaths, ArchiveOptions options)
        {
            var files = new List<(string FullPath, string RelativePath)>();

            foreach (var source in sourcePaths)
            {
                if (File.Exists(source))
                {
                    files.Add((source, Path.GetFileName(source)));
                }
                else if (Directory.Exists(source))
                {
                    var basePath = options.IncludeBaseFolder
                        ? Path.GetDirectoryName(source) ?? source
                        : source;

                    foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
                    {
                        // Check exclude patterns
                        if (options.ExcludePatterns != null && MatchesPattern(file, options.ExcludePatterns))
                            continue;

                        var relativePath = file.Substring(basePath.Length).TrimStart('\\', '/');
                        files.Add((file, relativePath));
                    }
                }
            }

            return files;
        }

        private bool MatchesPattern(string path, string[] patterns)
        {
            var fileName = Path.GetFileName(path);
            return patterns.Any(p => WildcardToRegex(p).IsMatch(fileName));
        }

        private Regex WildcardToRegex(string pattern)
        {
            var regexPattern = "^" + Regex.Escape(pattern)
                .Replace("\\*", ".*")
                .Replace("\\?", ".") + "$";
            return new Regex(regexPattern, RegexOptions.IgnoreCase);
        }
    }
}
