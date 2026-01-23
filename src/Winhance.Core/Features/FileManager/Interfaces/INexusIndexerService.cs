using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Winhance.Core.Features.FileManager.Interfaces
{
    /// <summary>
    /// High-performance file indexing service powered by Rust nexus_core.
    /// Uses MFT reading for sub-second full drive enumeration.
    /// </summary>
    public interface INexusIndexerService
    {
        /// <summary>
        /// Gets whether the native library is available.
        /// </summary>
        bool IsAvailable { get; }

        /// <summary>
        /// Gets the current indexing statistics.
        /// </summary>
        NexusIndexStats Stats { get; }

        /// <summary>
        /// Initialize the indexer.
        /// </summary>
        bool Initialize();

        /// <summary>
        /// Index all available drives using MFT (ultra-fast).
        /// </summary>
        Task<long> IndexAllDrivesAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Index a specific directory.
        /// </summary>
        Task<long> IndexDirectoryAsync(string path, CancellationToken cancellationToken = default);

        /// <summary>
        /// Search indexed files with query.
        /// </summary>
        Task<IEnumerable<NexusFileEntry>> SearchAsync(string query, int maxResults = 100, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get last error message if any operation failed.
        /// </summary>
        string? GetLastError();
    }

    /// <summary>
    /// File entry from Nexus indexer.
    /// </summary>
    public class NexusFileEntry
    {
        public string Path { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Extension { get; set; }
        public long Size { get; set; }
        public bool IsDirectory { get; set; }
        public DateTime? Created { get; set; }
        public DateTime? Modified { get; set; }
        public string? ContentHash { get; set; }
        public char Drive { get; set; }
    }

    /// <summary>
    /// Indexing statistics.
    /// </summary>
    public class NexusIndexStats
    {
        public long TotalFiles { get; set; }
        public long TotalDirectories { get; set; }
        public long TotalSize { get; set; }
        public long IndexTimeMs { get; set; }
        public List<char> DrivesIndexed { get; set; } = new();
        public DateTime LastIndexTime { get; set; }
    }
}
