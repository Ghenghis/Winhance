using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Winhance.Core.Features.FileManager.Interfaces
{
    /// <summary>
    /// High-performance duplicate file finder using Rust xxHash3 + SHA-256.
    /// Multi-stage detection: size → quick hash → full hash → byte comparison.
    /// </summary>
    public interface IDuplicateFinderService
    {
        /// <summary>
        /// Scan directories for duplicate files.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task<DuplicateScanResult> ScanForDuplicatesAsync(
            IEnumerable<string> paths,
            DuplicateScanOptions options,
            IProgress<DuplicateScanProgress>? progress = null,
            CancellationToken ct = default);

        /// <summary>
        /// Verify if two files are identical (byte-level comparison).
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task<bool> AreFilesIdenticalAsync(string path1, string path2, CancellationToken ct = default);

        /// <summary>
        /// Get file hash (quick xxHash3 or full SHA-256).
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task<string> GetFileHashAsync(string path, HashType type = HashType.Quick, CancellationToken ct = default);

        /// <summary>
        /// Find files similar to a given file.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task<IEnumerable<SimilarFile>> FindSimilarFilesAsync(
            string sourcePath,
            IEnumerable<string> searchPaths, SimilarityOptions options, CancellationToken ct = default);

        /// <summary>
        /// Delete duplicates keeping specified strategy.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task<DuplicateDeleteResult> DeleteDuplicatesAsync(
            IEnumerable<DuplicateFileGroup> groups,
            DuplicateKeepStrategy strategy,
            bool useRecycleBin = true,
            CancellationToken ct = default);
    }

    public class DuplicateScanOptions
    {
        public long MinFileSize { get; set; } = 1; // 1 byte minimum

        public long MaxFileSize { get; set; } = long.MaxValue;

        public bool IncludeHiddenFiles { get; set; } = false;

        public bool IncludeSystemFiles { get; set; } = false;

        public bool ScanSubfolders { get; set; } = true;

        public HashSet<string>? IncludeExtensions { get; set; } // null = all

        public HashSet<string>? ExcludeExtensions { get; set; }

        public HashSet<string>? ExcludePaths { get; set; }

        public bool VerifyWithFullHash { get; set; } = true;

        public bool VerifyWithByteComparison { get; set; } = false;

        public int MaxParallelism { get; set; } = Environment.ProcessorCount;
    }

    public class DuplicateScanResult
    {
        public List<DuplicateFileGroup> Groups { get; set; } = new();

        public long TotalFilesScanned { get; set; }

        public long TotalBytesScanned { get; set; }

        public long DuplicateCount { get; set; }

        public long WastedBytes { get; set; }

        public TimeSpan ScanDuration { get; set; }

        public string FormattedWastedSpace => FormatBytes(WastedBytes);

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

    public class DuplicateFileGroup
    {
        public string Hash { get; set; } = string.Empty;

        public long FileSize { get; set; }

        public List<DuplicateFileEntry> Files { get; set; } = new();

        public int Count => Files.Count;

        public long WastedBytes => FileSize * (Count - 1);

        public string FormattedSize => FormatBytes(FileSize);

        public string FormattedWasted => FormatBytes(WastedBytes);

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

    public class DuplicateFileEntry
    {
        public string Path { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string Directory { get; set; } = string.Empty;

        public DateTime ModifiedTime { get; set; }

        public DateTime CreatedTime { get; set; }

        public bool IsSelected { get; set; }

        public bool IsOriginal { get; set; }
    }

    public class DuplicateScanProgress
    {
        public string Phase { get; set; } = string.Empty; // Scanning, Hashing, Verifying

        public long FilesProcessed { get; set; }

        public long TotalFiles { get; set; }

        public long BytesProcessed { get; set; }

        public string CurrentFile { get; set; } = string.Empty;

        public int DuplicateGroupsFound { get; set; }

        public double PercentComplete => TotalFiles > 0 ? (double)FilesProcessed / TotalFiles * 100 : 0;
    }

    public enum HashType
    {
        Quick,  // xxHash3 - very fast
        Full,   // SHA-256 - cryptographic
        Both,    // Quick first, then full for verification,
    }

    public class SimilarFile
    {
        public string Path { get; set; } = string.Empty;

        public double SimilarityPercent { get; set; }

        public SimilarityType Type { get; set; }
    }

    public enum SimilarityType
    {
        Identical,
        SameSize,
        SameName,
        SimilarName,
        SameExtension,
    }

    public class SimilarityOptions
    {
        public bool MatchBySize { get; set; } = true;

        public bool MatchByName { get; set; } = true;

        public bool FuzzyNameMatch { get; set; } = true;

        public double MinSimilarityPercent { get; set; } = 80;
    }

    public enum DuplicateKeepStrategy
    {
        KeepFirst,
        KeepLast,
        KeepOldest,
        KeepNewest,
        KeepShortestPath,
        KeepLongestPath,
        KeepInFolder,
        KeepSelected,
    }

    public class DuplicateDeleteResult
    {
        public int DeletedCount { get; set; }

        public long BytesRecovered { get; set; }

        public int FailedCount { get; set; }

        public List<string> Errors { get; set; } = new();
    }
}
