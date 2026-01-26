using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.FileManager.Interfaces;

namespace Winhance.Infrastructure.Features.FileManager.Services
{
    /// <summary>
    /// High-performance duplicate file finder.
    /// Uses multi-stage detection: size → quick hash → full hash → byte comparison.
    /// </summary>
    public class DuplicateFinderService : IDuplicateFinderService
    {
        private readonly ILogService _logService;
        private readonly INexusIndexerService? _nexusIndexer;

        public DuplicateFinderService(ILogService logService)
        {
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
            _nexusIndexer = null;
        }

        /// <summary>
        /// Validates a path for safe file operations.
        /// </summary>
        private string ValidatePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("Path cannot be null or empty", nameof(path));
            }

            // Normalize the path
            var fullPath = Path.GetFullPath(path);

            // Check for path traversal
            if (path.Contains("..", StringComparison.Ordinal))
            {
                throw new ArgumentException("Path traversal patterns are not allowed", nameof(path));
            }

            return fullPath;
        }

        /// <summary>
        /// Validates options to prevent resource exhaustion attacks.
        /// </summary>
        private void ValidateOptions(DuplicateScanOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);

            // Prevent scanning extremely small files which could cause memory issues
            if (options.MinFileSize < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(options), "MinFileSize cannot be negative");
            }

            // Prevent unreasonably large max file size that could cause integer overflow
            if (options.MaxFileSize < options.MinFileSize)
            {
                throw new ArgumentOutOfRangeException(nameof(options), "MaxFileSize must be >= MinFileSize");
            }
        }

        public async Task<DuplicateScanResult> ScanForDuplicatesAsync(
            IEnumerable<string> paths,
            DuplicateScanOptions options,
            IProgress<DuplicateScanProgress>? progress = null,
            CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(paths);
            ValidateOptions(options);

            var result = new DuplicateScanResult();
            var sw = System.Diagnostics.Stopwatch.StartNew();

            // Validate and collect paths
            var validatedPaths = new List<string>();
            foreach (var path in paths)
            {
                try
                {
                    validatedPaths.Add(ValidatePath(path));
                }
                catch (ArgumentException ex)
                {
                    _logService.Log(LogLevel.Warning, $"Skipping invalid path: {path} - {ex.Message}");
                }
            }

            if (validatedPaths.Count == 0)
            {
                _logService.Log(LogLevel.Warning, "No valid paths provided for duplicate scan");
                return result;
            }

            // Phase 1: Collect all files
            progress?.Report(new DuplicateScanProgress { Phase = "Scanning directories" });
            var allFiles = new List<FileInfo>();

            foreach (var path in validatedPaths)
            {
                if (ct.IsCancellationRequested)
                {
                    break;
                }

                CollectFiles(path, allFiles, options);
            }

            result.TotalFilesScanned = allFiles.Count;
            result.TotalBytesScanned = allFiles.Sum(f => f.Length);

            // Phase 2: Group by size (quick filter)
            progress?.Report(new DuplicateScanProgress { Phase = "Grouping by size", TotalFiles = allFiles.Count });
            var sizeGroups = allFiles
                .Where(f => f.Length >= options.MinFileSize && f.Length <= options.MaxFileSize)
                .GroupBy(f => f.Length)
                .Where(g => g.Count() > 1)
                .ToList();

            // Phase 3: Hash files in size groups
            var duplicateGroups = new ConcurrentBag<DuplicateFileGroup>();
            long filesProcessed = 0;

            foreach (var sizeGroup in sizeGroups)
            {
                if (ct.IsCancellationRequested)
                {
                    break;
                }

                var hashGroups = new Dictionary<string, List<FileInfo>>();

                foreach (var file in sizeGroup)
                {
                    if (ct.IsCancellationRequested)
                    {
                        break;
                    }

                    filesProcessed++;
                    progress?.Report(new DuplicateScanProgress
                    {
                        Phase = "Hashing files",
                        FilesProcessed = filesProcessed,
                        TotalFiles = allFiles.Count,
                        CurrentFile = file.Name,
                        DuplicateGroupsFound = duplicateGroups.Count,
                    });

                    try
                    {
                        var hash = await ComputeQuickHashAsync(file.FullName, ct);
                        if (!hashGroups.ContainsKey(hash))
                        {
                            hashGroups[hash] = new List<FileInfo>();
                        }

                        hashGroups[hash].Add(file);
                    }
                    catch (Exception ex)
                    {
                        _logService.Log(LogLevel.Warning, $"Failed to hash {file.FullName}: {ex.Message}");
                    }
                }

                // Create duplicate groups from hash matches
                foreach (var hashGroup in hashGroups.Where(g => g.Value.Count > 1))
                {
                    // Optionally verify with full hash
                    if (options.VerifyWithFullHash)
                    {
                        var verifiedGroups = await VerifyWithFullHashAsync(hashGroup.Value, ct);
                        foreach (var vg in verifiedGroups)
                        {
                            duplicateGroups.Add(vg);
                        }
                    }
                    else
                    {
                        duplicateGroups.Add(CreateDuplicateGroup(hashGroup.Key, hashGroup.Value));
                    }
                }
            }

            result.Groups = duplicateGroups.ToList();
            result.DuplicateCount = result.Groups.Sum(g => g.Count - 1);
            result.WastedBytes = result.Groups.Sum(g => g.WastedBytes);

            sw.Stop();
            result.ScanDuration = sw.Elapsed;

            _logService.Log(
                LogLevel.Info,
                $"Duplicate scan complete: {result.Groups.Count} groups, {result.DuplicateCount} duplicates, {result.FormattedWastedSpace} wasted");

            return result;
        }

        private void CollectFiles(string path, List<FileInfo> files, DuplicateScanOptions options)
        {
            try
            {
                if (File.Exists(path))
                {
                    var fi = new FileInfo(path);
                    if (ShouldIncludeFile(fi, options))
                    {
                        files.Add(fi);
                    }

                    return;
                }

                if (!Directory.Exists(path))
                {
                    return;
                }

                var searchOption = options.ScanSubfolders ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

                foreach (var file in Directory.EnumerateFiles(path, "*", searchOption))
                {
                    try
                    {
                        if (options.ExcludePaths?.Any(ep => file.StartsWith(ep, StringComparison.OrdinalIgnoreCase)) == true)
                        {
                            continue;
                        }

                        var fi = new FileInfo(file);
                        if (ShouldIncludeFile(fi, options))
                        {
                            files.Add(fi);
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Skip inaccessible file: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Warning, $"Error scanning {path}: {ex.Message}");
            }
        }

        private bool ShouldIncludeFile(FileInfo fi, DuplicateScanOptions options)
        {
            if (fi.Length < options.MinFileSize || fi.Length > options.MaxFileSize)
            {
                return false;
            }

            if (!options.IncludeHiddenFiles && fi.Attributes.HasFlag(FileAttributes.Hidden))
            {
                return false;
            }

            if (!options.IncludeSystemFiles && fi.Attributes.HasFlag(FileAttributes.System))
            {
                return false;
            }

            var ext = fi.Extension.TrimStart('.').ToLowerInvariant();
            if (options.IncludeExtensions != null && !options.IncludeExtensions.Contains(ext))
            {
                return false;
            }

            if (options.ExcludeExtensions?.Contains(ext) == true)
            {
                return false;
            }

            return true;
        }

        private async Task<string> ComputeQuickHashAsync(string path, CancellationToken ct)
        {
            // Use xxHash-style quick hash (read first/last 64KB + size)
            const int sampleSize = 64 * 1024;
            var fileInfo = new FileInfo(path);

            using var md5 = MD5.Create();
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);

            var buffer = new byte[(sampleSize * 2) + 8];
            var pos = 0;

            // Add file size
            BitConverter.GetBytes(fileInfo.Length).CopyTo(buffer, pos);
            pos += 8;

            // Read first chunk
            var firstRead = await stream.ReadAsync(buffer.AsMemory(pos, Math.Min(sampleSize, (int)fileInfo.Length)), ct);
            pos += firstRead;

            // Read last chunk if file is large enough
            if (fileInfo.Length > sampleSize * 2)
            {
                stream.Seek(-sampleSize, SeekOrigin.End);
                var lastRead = await stream.ReadAsync(buffer.AsMemory(pos, sampleSize), ct);
                pos += lastRead;
            }

            var hash = md5.ComputeHash(buffer, 0, pos);
            return BitConverter.ToString(hash).Replace("-", string.Empty, StringComparison.Ordinal);
        }

        private async Task<string> ComputeFullHashAsync(string path, CancellationToken ct)
        {
            using var sha256 = SHA256.Create();
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true);
            var hash = await sha256.ComputeHashAsync(stream, ct);
            return BitConverter.ToString(hash).Replace("-", string.Empty, StringComparison.Ordinal);
        }

        private async Task<List<DuplicateFileGroup>> VerifyWithFullHashAsync(List<FileInfo> files, CancellationToken ct)
        {
            var fullHashGroups = new Dictionary<string, List<FileInfo>>();

            foreach (var file in files)
            {
                if (ct.IsCancellationRequested)
                {
                    break;
                }

                try
                {
                    var fullHash = await ComputeFullHashAsync(file.FullName, ct);
                    if (!fullHashGroups.ContainsKey(fullHash))
                    {
                        fullHashGroups[fullHash] = new List<FileInfo>();
                    }

                    fullHashGroups[fullHash].Add(file);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Skip file that can't be hashed: {ex.Message}");
                }
            }

            return fullHashGroups
                .Where(g => g.Value.Count > 1)
                .Select(g => CreateDuplicateGroup(g.Key, g.Value))
                .ToList();
        }

        private DuplicateFileGroup CreateDuplicateGroup(string hash, List<FileInfo> files)
        {
            var group = new DuplicateFileGroup
            {
                Hash = hash,
                FileSize = files.First().Length,
            };

            var oldest = files.OrderBy(f => f.CreationTime).First();

            foreach (var file in files)
            {
                group.Files.Add(new DuplicateFileEntry
                {
                    Path = file.FullName,
                    Name = file.Name,
                    Directory = file.DirectoryName ?? string.Empty,
                    ModifiedTime = file.LastWriteTime,
                    CreatedTime = file.CreationTime,
                    IsOriginal = file.FullName == oldest.FullName,
                });
            }

            return group;
        }

        public async Task<bool> AreFilesIdenticalAsync(string path1, string path2, CancellationToken ct = default)
        {
            var validatedPath1 = ValidatePath(path1);
            var validatedPath2 = ValidatePath(path2);

            var fi1 = new FileInfo(validatedPath1);
            var fi2 = new FileInfo(validatedPath2);

            if (fi1.Length != fi2.Length)
            {
                return false;
            }

            const int bufferSize = 64 * 1024;
            var buffer1 = new byte[bufferSize];
            var buffer2 = new byte[bufferSize];

            await using var stream1 = new FileStream(path1, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize, true);
            await using var stream2 = new FileStream(path2, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize, true);

            while (true)
            {
                if (ct.IsCancellationRequested)
                {
                    return false;
                }

                var read1 = await stream1.ReadAsync(buffer1, ct);
                var read2 = await stream2.ReadAsync(buffer2, ct);

                if (read1 != read2)
                {
                    return false;
                }

                if (read1 == 0)
                {
                    return true;
                }

                if (!buffer1.AsSpan(0, read1).SequenceEqual(buffer2.AsSpan(0, read2)))
                {
                    return false;
                }
            }
        }

        public async Task<string> GetFileHashAsync(string path, HashType type = HashType.Quick, CancellationToken ct = default)
        {
            var validatedPath = ValidatePath(path);

            return type switch
            {
                HashType.Quick => await ComputeQuickHashAsync(validatedPath, ct),
                HashType.Full => await ComputeFullHashAsync(validatedPath, ct),
                HashType.Both => $"{await ComputeQuickHashAsync(validatedPath, ct)}:{await ComputeFullHashAsync(validatedPath, ct)}",
                _ => await ComputeQuickHashAsync(validatedPath, ct),
            };
        }

        public async Task<IEnumerable<SimilarFile>> FindSimilarFilesAsync(
            string sourcePath,
            IEnumerable<string> searchPaths, SimilarityOptions options, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(searchPaths);
            ArgumentNullException.ThrowIfNull(options);

            var validatedSourcePath = ValidatePath(sourcePath);
            var results = new List<SimilarFile>();
            var sourceInfo = new FileInfo(validatedSourcePath);
            var sourceName = Path.GetFileNameWithoutExtension(validatedSourcePath).ToLowerInvariant();

            // Validate search paths
            var validatedSearchPaths = new List<string>();
            foreach (var path in searchPaths)
            {
                try
                {
                    validatedSearchPaths.Add(ValidatePath(path));
                }
                catch (ArgumentException ex)
                {
                    _logService.Log(LogLevel.Warning, $"Skipping invalid search path: {path} - {ex.Message}");
                }
            }

            foreach (var searchPath in validatedSearchPaths)
            {
                if (ct.IsCancellationRequested)
                {
                    break;
                }

                var searchOption = SearchOption.AllDirectories;
                foreach (var file in Directory.EnumerateFiles(searchPath, "*", searchOption))
                {
                    if (ct.IsCancellationRequested)
                    {
                        break;
                    }

                    if (file.Equals(sourcePath, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    try
                    {
                        var fi = new FileInfo(file);
                        var similarity = 0.0;
                        var type = SimilarityType.SameExtension;

                        // Check size match
                        if (options.MatchBySize && fi.Length == sourceInfo.Length)
                        {
                            similarity = 100;
                            type = SimilarityType.SameSize;

                            // Verify if identical
                            if (await AreFilesIdenticalAsync(sourcePath, file, ct))
                            {
                                type = SimilarityType.Identical;
                            }
                        }

                        // Check name match
                        if (options.MatchByName)
                        {
                            var targetName = Path.GetFileNameWithoutExtension(file).ToLowerInvariant();
                            if (targetName == sourceName)
                            {
                                similarity = Math.Max(similarity, 90);
                                type = type == SimilarityType.SameSize ? SimilarityType.Identical : SimilarityType.SameName;
                            }
                            else if (options.FuzzyNameMatch)
                            {
                                var nameSimilarity = CalculateStringSimilarity(sourceName, targetName) * 100;
                                if (nameSimilarity >= options.MinSimilarityPercent)
                                {
                                    similarity = Math.Max(similarity, nameSimilarity);
                                    if (type == SimilarityType.SameExtension)
                                    {
                                        type = SimilarityType.SimilarName;
                                    }
                                }
                            }
                        }

                        if (similarity >= options.MinSimilarityPercent)
                        {
                            results.Add(new SimilarFile
                            {
                                Path = file,
                                SimilarityPercent = similarity,
                                Type = type,
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Skip inaccessible file: {ex.Message}");
                    }
                }
            }

            return results.OrderByDescending(r => r.SimilarityPercent);
        }

        private double CalculateStringSimilarity(string s1, string s2)
        {
            if (string.IsNullOrEmpty(s1) || string.IsNullOrEmpty(s2))
            {
                return 0;
            }

            var longer = s1.Length > s2.Length ? s1 : s2;
            var shorter = s1.Length > s2.Length ? s2 : s1;

            if (longer.Length == 0)
            {
                return 1.0;
            }

            return (longer.Length - LevenshteinDistance(longer, shorter)) / (double)longer.Length;
        }

        private int LevenshteinDistance(string s1, string s2)
        {
            var costs = new int[s2.Length + 1];
            for (int i = 0; i <= s1.Length; i++)
            {
                int lastValue = i;
                for (int j = 0; j <= s2.Length; j++)
                {
                    if (i == 0)
                    {
                        costs[j] = j;
                    }
                    else if (j > 0)
                    {
                        int newValue = costs[j - 1];
                        if (s1[i - 1] != s2[j - 1])
                        {
                            newValue = Math.Min(Math.Min(newValue, lastValue), costs[j]) + 1;
                        }

                        costs[j - 1] = lastValue;
                        lastValue = newValue;
                    }
                }

                if (i > 0)
                {
                    costs[s2.Length] = lastValue;
                }
            }

            return costs[s2.Length];
        }

        public async Task<DuplicateDeleteResult> DeleteDuplicatesAsync(
            IEnumerable<DuplicateFileGroup> groups,
            DuplicateKeepStrategy strategy,
            bool useRecycleBin = true,
            CancellationToken ct = default)
        {
            var result = new DuplicateDeleteResult();

            foreach (var group in groups)
            {
                if (ct.IsCancellationRequested)
                {
                    break;
                }

                var toKeep = SelectFileToKeep(group, strategy);

                foreach (var file in group.Files.Where(f => f.Path != toKeep.Path))
                {
                    if (ct.IsCancellationRequested)
                    {
                        break;
                    }

                    try
                    {
                        if (useRecycleBin)
                        {
                            Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(
                                file.Path,
                                Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                                Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
                        }
                        else
                        {
                            File.Delete(file.Path);
                        }

                        result.DeletedCount++;
                        result.BytesRecovered += group.FileSize;
                    }
                    catch (Exception ex)
                    {
                        result.FailedCount++;
                        result.Errors.Add($"Failed to delete {file.Path}: {ex.Message}");
                    }
                }
            }

            _logService.Log(
                LogLevel.Info,
                $"Deleted {result.DeletedCount} duplicates, recovered {FormatBytes(result.BytesRecovered)}");

            return result;
        }

        private DuplicateFileEntry SelectFileToKeep(DuplicateFileGroup group, DuplicateKeepStrategy strategy)
        {
            return strategy switch
            {
                DuplicateKeepStrategy.KeepFirst => group.Files.First(),
                DuplicateKeepStrategy.KeepLast => group.Files.Last(),
                DuplicateKeepStrategy.KeepOldest => group.Files.OrderBy(f => f.CreatedTime).First(),
                DuplicateKeepStrategy.KeepNewest => group.Files.OrderByDescending(f => f.CreatedTime).First(),
                DuplicateKeepStrategy.KeepShortestPath => group.Files.OrderBy(f => f.Path.Length).First(),
                DuplicateKeepStrategy.KeepLongestPath => group.Files.OrderByDescending(f => f.Path.Length).First(),
                DuplicateKeepStrategy.KeepSelected => group.Files.FirstOrDefault(f => f.IsSelected) ?? group.Files.First(),
                _ => group.Files.First(f => f.IsOriginal),
            };
        }

        private static string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            int order = 0;
            double size = bytes;
            while (size >= 1024 && order < sizes.Length - 1)
            {
                order++;
                size /= 1024;
            }

            return $"{size:0.##} {sizes[order]}";
        }
    }
}
