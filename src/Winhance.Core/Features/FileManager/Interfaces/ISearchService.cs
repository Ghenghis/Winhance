using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Winhance.Core.Features.FileManager.Interfaces
{
    /// <summary>
    /// High-level search service providing comprehensive file search capabilities.
    /// Wraps the Nexus indexer for fast indexed search with fallback to filesystem search.
    /// </summary>
    public interface ISearchService
    {
        /// <summary>
        /// Search for files matching the query.
        /// </summary>
        /// <param name="query">Search query (supports wildcards, regex if enabled).</param>
        /// <param name="options">Search options for filtering and sorting.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Collection of matching search results.</returns>
        Task<IEnumerable<SearchResult>> SearchAsync(
            string query,
            SearchOptions options,
            CancellationToken ct = default);

        /// <summary>
        /// Index a drive for fast searching using MFT/USN journal.
        /// </summary>
        /// <param name="driveLetter">Drive letter to index (e.g., "C").</param>
        /// <param name="progress">Progress reporter.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>True if indexing succeeded.</returns>
        Task<bool> IndexDriveAsync(
            string driveLetter,
            IProgress<IndexProgress>? progress = null,
            CancellationToken ct = default);

        /// <summary>
        /// Get the current index status for all drives.
        /// </summary>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Index status information.</returns>
        Task<IndexStatus> GetIndexStatusAsync(CancellationToken ct = default);

        /// <summary>
        /// Cancel ongoing indexing operations.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task CancelIndexingAsync();

        /// <summary>
        /// Search with real-time results as they are found.
        /// </summary>
        /// <param name="query">Search query.</param>
        /// <param name="options">Search options.</param>
        /// <param name="onResult">Callback for each result found.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Total number of results found.</returns>
        Task<int> SearchStreamingAsync(
            string query,
            SearchOptions options,
            Action<SearchResult> onResult,
            CancellationToken ct = default);

        /// <summary>
        /// Get search suggestions based on partial query.
        /// </summary>
        /// <param name="partialQuery">Partial search query.</param>
        /// <param name="maxSuggestions">Maximum number of suggestions.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Collection of search suggestions.</returns>
        Task<IEnumerable<string>> GetSuggestionsAsync(
            string partialQuery,
            int maxSuggestions = 10,
            CancellationToken ct = default);

        /// <summary>
        /// Get recent search queries.
        /// </summary>
        /// <param name="count">Number of recent queries to return.</param>
        /// <returns>Collection of recent queries.</returns>
        IEnumerable<string> GetRecentSearches(int count = 10);

        /// <summary>
        /// Clear recent search history.
        /// </summary>
        void ClearSearchHistory();
    }

    /// <summary>
    /// Represents a single search result.
    /// </summary>
    public class SearchResult
    {
        /// <summary>
        /// Gets or sets the full path to the file or directory.
        /// </summary>
        public string FullPath { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the file or directory name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the parent directory path.
        /// </summary>
        public string Directory { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the file size in bytes.
        /// </summary>
        public long Size { get; set; }

        /// <summary>
        /// Gets or sets the last modified date.
        /// </summary>
        public DateTime DateModified { get; set; }

        /// <summary>
        /// Gets or sets the creation date.
        /// </summary>
        public DateTime DateCreated { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this is a directory.
        /// </summary>
        public bool IsDirectory { get; set; }

        /// <summary>
        /// Gets or sets the file extension (empty for directories).
        /// </summary>
        public string Extension { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the relevance score (0-100).
        /// </summary>
        public double Score { get; set; }

        /// <summary>
        /// Gets or sets highlighted text showing match context.
        /// </summary>
        public string MatchHighlight { get; set; } = string.Empty;

        /// <summary>
        /// Gets the formatted file size string.
        /// </summary>
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

    /// <summary>
    /// Options for controlling search behavior.
    /// </summary>
    public class SearchOptions
    {
        /// <summary>
        /// Gets or sets a value indicating whether to use regex pattern matching.
        /// </summary>
        public bool UseRegex { get; set; } = false;

        /// <summary>
        /// Gets or sets a value indicating whether search is case sensitive.
        /// </summary>
        public bool CaseSensitive { get; set; } = false;

        /// <summary>
        /// Gets or sets a value indicating whether to search file contents (not just names).
        /// </summary>
        public bool SearchContents { get; set; } = false;

        /// <summary>
        /// Gets or sets a value indicating whether to include hidden files.
        /// </summary>
        public bool IncludeHidden { get; set; } = false;

        /// <summary>
        /// Gets or sets file extensions to include (null = all).
        /// </summary>
        public string[]? Extensions { get; set; }

        /// <summary>
        /// Gets or sets file extensions to exclude.
        /// </summary>
        public string[]? ExcludeExtensions { get; set; }

        /// <summary>
        /// Gets or sets paths to search in (null = all indexed).
        /// </summary>
        public string[]? SearchPaths { get; set; }

        /// <summary>
        /// Gets or sets paths to exclude from search.
        /// </summary>
        public string[]? ExcludePaths { get; set; }

        /// <summary>
        /// Gets or sets minimum file size filter.
        /// </summary>
        public long? MinSize { get; set; }

        /// <summary>
        /// Gets or sets maximum file size filter.
        /// </summary>
        public long? MaxSize { get; set; }

        /// <summary>
        /// Gets or sets filter for files modified after this date.
        /// </summary>
        public DateTime? ModifiedAfter { get; set; }

        /// <summary>
        /// Gets or sets filter for files modified before this date.
        /// </summary>
        public DateTime? ModifiedBefore { get; set; }

        /// <summary>
        /// Gets or sets filter for files created after this date.
        /// </summary>
        public DateTime? CreatedAfter { get; set; }

        /// <summary>
        /// Gets or sets filter for files created before this date.
        /// </summary>
        public DateTime? CreatedBefore { get; set; }

        /// <summary>
        /// Gets or sets maximum number of results to return.
        /// </summary>
        public int MaxResults { get; set; } = 1000;

        /// <summary>
        /// Gets or sets the field to sort results by.
        /// </summary>
        public SearchSortBy SortBy { get; set; } = SearchSortBy.Relevance;

        /// <summary>
        /// Gets or sets a value indicating whether to sort in descending order.
        /// </summary>
        public bool SortDescending { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether to search only directories.
        /// </summary>
        public bool DirectoriesOnly { get; set; } = false;

        /// <summary>
        /// Gets or sets a value indicating whether to search only files.
        /// </summary>
        public bool FilesOnly { get; set; } = false;
    }

    /// <summary>
    /// Sort options for search results.
    /// </summary>
    public enum SearchSortBy
    {
        /// <summary>
        /// Sort by match relevance score.
        /// </summary>
        Relevance,

        /// <summary>
        /// Sort by file/folder name.
        /// </summary>
        Name,

        /// <summary>
        /// Sort by file size.
        /// </summary>
        Size,

        /// <summary>
        /// Sort by last modified date.
        /// </summary>
        DateModified,

        /// <summary>
        /// Sort by creation date.
        /// </summary>
        DateCreated,

        /// <summary>
        /// Sort by file extension.
        /// </summary>
        Extension,

        /// <summary>
        /// Sort by full path.
        /// </summary>
        Path,
    }

    /// <summary>
    /// Progress information during indexing.
    /// </summary>
    public class IndexProgress
    {
        /// <summary>
        /// Gets or sets the current path being indexed.
        /// </summary>
        public string CurrentPath { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the number of files indexed so far.
        /// </summary>
        public long FilesIndexed { get; set; }

        /// <summary>
        /// Gets or sets the total estimated files (if known).
        /// </summary>
        public long TotalFiles { get; set; }

        /// <summary>
        /// Gets or sets the completion percentage (0-100).
        /// </summary>
        public int PercentComplete { get; set; }

        /// <summary>
        /// Gets or sets the current indexing phase description.
        /// </summary>
        public string Phase { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the indexing speed (files/second).
        /// </summary>
        public double FilesPerSecond { get; set; }
    }

    /// <summary>
    /// Status information about the search index.
    /// </summary>
    public class IndexStatus
    {
        /// <summary>
        /// Gets or sets a value indicating whether indexing is currently in progress.
        /// </summary>
        public bool IsIndexing { get; set; }

        /// <summary>
        /// Gets or sets when the index was last updated.
        /// </summary>
        public DateTime? LastIndexed { get; set; }

        /// <summary>
        /// Gets or sets the total number of files in the index.
        /// </summary>
        public long TotalFilesIndexed { get; set; }

        /// <summary>
        /// Gets or sets the index size on disk in bytes.
        /// </summary>
        public long IndexSizeBytes { get; set; }

        /// <summary>
        /// Gets or sets per-drive index status.
        /// </summary>
        public Dictionary<string, DriveIndexStatus> DriveStatuses { get; set; } = new();

        /// <summary>
        /// Gets the formatted index size.
        /// </summary>
        public string FormattedIndexSize => FormatBytes(IndexSizeBytes);

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

    /// <summary>
    /// Index status for a single drive.
    /// </summary>
    public class DriveIndexStatus
    {
        /// <summary>
        /// Gets or sets the drive letter.
        /// </summary>
        public string DriveLetter { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether the drive is indexed.
        /// </summary>
        public bool IsIndexed { get; set; }

        /// <summary>
        /// Gets or sets when this drive was last indexed.
        /// </summary>
        public DateTime? LastIndexed { get; set; }

        /// <summary>
        /// Gets or sets the number of files indexed on this drive.
        /// </summary>
        public long FilesIndexed { get; set; }

        /// <summary>
        /// Gets or sets indexing error message if any.
        /// </summary>
        public string? ErrorMessage { get; set; }
    }
}
