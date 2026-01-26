using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Winhance.Core.Features.FileManager.Interfaces
{
    /// <summary>
    /// Disk space analyzer with treemap visualization data.
    /// Uses Rust MFT reader for instant full-drive analysis.
    /// </summary>
    public interface ISpaceAnalyzerService
    {
        /// <summary>
        /// Analyze disk space usage for a path.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task<SpaceAnalysisResult> AnalyzeAsync(string path, SpaceAnalysisOptions options,
            IProgress<SpaceAnalysisProgress>? progress = null, CancellationToken ct = default);

        /// <summary>
        /// Get largest files in a path.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task<IEnumerable<FileSpaceInfo>> GetLargestFilesAsync(string path, int count = 100,
            CancellationToken ct = default);

        /// <summary>
        /// Get largest folders in a path.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task<IEnumerable<FolderSpaceInfo>> GetLargestFoldersAsync(string path, int count = 100,
            CancellationToken ct = default);

        /// <summary>
        /// Get space usage by file type.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task<IEnumerable<FileTypeSpaceInfo>> GetSpaceByFileTypeAsync(
            string path,
            CancellationToken ct = default);

        /// <summary>
        /// Get space usage by age (old files).
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task<IEnumerable<AgeGroupSpaceInfo>> GetSpaceByAgeAsync(
            string path,
            CancellationToken ct = default);

        /// <summary>
        /// Find empty folders.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task<IEnumerable<string>> FindEmptyFoldersAsync(string path, bool recursive = true,
            CancellationToken ct = default);

        /// <summary>
        /// Find temporary/cache files that can be cleaned.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task<CleanupSuggestions> GetCleanupSuggestionsAsync(
            string? path = null,
            CancellationToken ct = default);
    }

    public class SpaceAnalysisOptions
    {
        public bool IncludeHiddenFiles { get; set; } = true;

        public bool IncludeSystemFiles { get; set; } = true;

        public int MaxDepth { get; set; } = int.MaxValue;

        public long MinFileSize { get; set; } = 0;

        public bool CalculateTreemap { get; set; } = true;
    }

    public class SpaceAnalysisResult
    {
        public string RootPath { get; set; } = string.Empty;

        public long TotalSize { get; set; }

        public long TotalFiles { get; set; }

        public long TotalFolders { get; set; }

        public TreemapNode RootNode { get; set; } = new();

        public List<FileSpaceInfo> LargestFiles { get; set; } = new();

        public List<FolderSpaceInfo> LargestFolders { get; set; } = new();

        public List<FileTypeSpaceInfo> SpaceByType { get; set; } = new();

        public TimeSpan AnalysisDuration { get; set; }

        public string FormattedSize => FormatBytes(TotalSize);

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

    public class TreemapNode
    {
        public string Name { get; set; } = string.Empty;

        public string FullPath { get; set; } = string.Empty;

        public long Size { get; set; }

        public bool IsFile { get; set; }

        public List<TreemapNode> Children { get; set; } = new();

        public double Percentage { get; set; }

        public string Color { get; set; } = "#4A90D9"; // Default blue,
    }

    public class FileSpaceInfo
    {
        public string Path { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string Extension { get; set; } = string.Empty;

        public long Size { get; set; }

        public DateTime ModifiedTime { get; set; }

        public string FormattedSize => FormatBytes(Size);

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

    public class FolderSpaceInfo
    {
        public string Path { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public long Size { get; set; }

        public long FileCount { get; set; }

        public long FolderCount { get; set; }

        public double Percentage { get; set; }

        public string FormattedSize => FormatBytes(Size);

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

    public class FileTypeSpaceInfo
    {
        public string Extension { get; set; } = string.Empty;

        public string Category { get; set; } = string.Empty; // Documents, Images, Video, Audio, etc.

        public long TotalSize { get; set; }

        public long FileCount { get; set; }

        public double Percentage { get; set; }

        public string Color { get; set; } = "#808080";

        public string FormattedSize => FormatBytes(TotalSize);

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

    public class AgeGroupSpaceInfo
    {
        public string AgeGroup { get; set; } = string.Empty; // "< 1 month", "1-6 months", etc.

        public long TotalSize { get; set; }

        public long FileCount { get; set; }

        public DateTime OldestFile { get; set; }

        public DateTime NewestFile { get; set; }
    }

    public class SpaceAnalysisProgress
    {
        public string CurrentPath { get; set; } = string.Empty;

        public long FilesProcessed { get; set; }

        public long FoldersProcessed { get; set; }

        public long BytesProcessed { get; set; }

        public string Phase { get; set; } = string.Empty;
    }

    public class CleanupSuggestions
    {
        public List<CleanupItem> TempFiles { get; set; } = new();

        public List<CleanupItem> CacheFiles { get; set; } = new();

        public List<CleanupItem> LogFiles { get; set; } = new();

        public List<CleanupItem> OldDownloads { get; set; } = new();

        public List<CleanupItem> RecycleBin { get; set; } = new();

        public List<CleanupItem> BrowserCache { get; set; } = new();

        public List<CleanupItem> WindowsUpdate { get; set; } = new();

        public List<CleanupItem> Thumbnails { get; set; } = new();

        public long TotalReclaimableSpace =>
            TempFiles.Sum(x => x.Size) + CacheFiles.Sum(x => x.Size) +
            LogFiles.Sum(x => x.Size) + OldDownloads.Sum(x => x.Size) +
            RecycleBin.Sum(x => x.Size) + BrowserCache.Sum(x => x.Size) +
            WindowsUpdate.Sum(x => x.Size) + Thumbnails.Sum(x => x.Size);
    }

    public class CleanupItem
    {
        public string Path { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public long Size { get; set; }

        public CleanupRisk Risk { get; set; }

        public bool IsSelected { get; set; }
    }

    public enum CleanupRisk
    {
        Safe,       // Can always be deleted
        Low,        // Usually safe
        Medium,     // May cause minor issues
        High,        // Could cause problems,
    }
}
