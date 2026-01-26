using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Winhance.Core.Features.FileManager.Interfaces
{
    /// <summary>
    /// Service for archive operations - browsing, extracting, and creating archives.
    /// </summary>
    public interface IArchiveService
    {
        /// <summary>
        /// Gets supported archive formats.
        /// </summary>
        IReadOnlyList<ArchiveFormat> SupportedFormats { get; }

        /// <summary>
        /// Checks if a file is a supported archive.
        /// </summary>
        /// <param name="filePath">The file path.</param>
        /// <returns>True if the file is a supported archive.</returns>
        bool IsArchive(string filePath);

        /// <summary>
        /// Gets archive information.
        /// </summary>
        /// <param name="archivePath">Path to the archive.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Archive information.</returns>
        Task<ArchiveInfo> GetArchiveInfoAsync(string archivePath, CancellationToken cancellationToken = default);

        /// <summary>
        /// Lists contents of an archive.
        /// </summary>
        /// <param name="archivePath">Path to the archive.</param>
        /// <param name="internalPath">Path within the archive (empty for root).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of archive entries.</returns>
        Task<IEnumerable<ArchiveEntry>> ListContentsAsync(string archivePath, string internalPath = "", CancellationToken cancellationToken = default);

        /// <summary>
        /// Extracts all contents from an archive.
        /// </summary>
        /// <param name="archivePath">Path to the archive.</param>
        /// <param name="destinationPath">Destination folder.</param>
        /// <param name="options">Extraction options.</param>
        /// <param name="progress">Progress reporter.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Extraction result.</returns>
        Task<ExtractionResult> ExtractAllAsync(
            string archivePath,
            string destinationPath,
            ExtractionOptions? options = null,
            IProgress<ExtractionProgress>? progress = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Extracts specific entries from an archive.
        /// </summary>
        /// <param name="archivePath">Path to the archive.</param>
        /// <param name="entries">Entries to extract.</param>
        /// <param name="destinationPath">Destination folder.</param>
        /// <param name="options">Extraction options.</param>
        /// <param name="progress">Progress reporter.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Extraction result.</returns>
        Task<ExtractionResult> ExtractEntriesAsync(
            string archivePath,
            IEnumerable<string> entries,
            string destinationPath,
            ExtractionOptions? options = null,
            IProgress<ExtractionProgress>? progress = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Extracts a single entry to a stream.
        /// </summary>
        /// <param name="archivePath">Path to the archive.</param>
        /// <param name="entryPath">Path of the entry within the archive.</param>
        /// <param name="outputStream">Output stream.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task ExtractToStreamAsync(string archivePath, string entryPath, Stream outputStream, CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates a new archive.
        /// </summary>
        /// <param name="archivePath">Path for the new archive.</param>
        /// <param name="sourcePaths">Files/folders to add.</param>
        /// <param name="options">Creation options.</param>
        /// <param name="progress">Progress reporter.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Creation result.</returns>
        Task<ArchiveResult> CreateArchiveAsync(
            string archivePath,
            IEnumerable<string> sourcePaths,
            ArchiveOptions? options = null,
            IProgress<ArchiveProgress>? progress = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Adds files to an existing archive.
        /// </summary>
        /// <param name="archivePath">Path to the archive.</param>
        /// <param name="sourcePaths">Files/folders to add.</param>
        /// <param name="options">Options.</param>
        /// <param name="progress">Progress reporter.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Result.</returns>
        Task<ArchiveResult> AddToArchiveAsync(
            string archivePath,
            IEnumerable<string> sourcePaths,
            ArchiveOptions? options = null,
            IProgress<ArchiveProgress>? progress = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Tests the integrity of an archive.
        /// </summary>
        /// <param name="archivePath">Path to the archive.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Test result.</returns>
        Task<ArchiveTestResult> TestArchiveAsync(string archivePath, CancellationToken cancellationToken = default);

        /// <summary>
        /// Searches within an archive.
        /// </summary>
        /// <param name="archivePath">Path to the archive.</param>
        /// <param name="pattern">Search pattern.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Matching entries.</returns>
        Task<IEnumerable<ArchiveEntry>> SearchAsync(string archivePath, string pattern, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Supported archive format.
    /// </summary>
    public class ArchiveFormat
    {
        /// <summary>Gets or sets the format name.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Gets or sets the file extensions.</summary>
        public string[] Extensions { get; set; } = Array.Empty<string>();

        /// <summary>Gets or sets whether creation is supported.</summary>
        public bool CanCreate { get; set; }

        /// <summary>Gets or sets whether extraction is supported.</summary>
        public bool CanExtract { get; set; }

        /// <summary>Gets or sets whether encryption is supported.</summary>
        public bool SupportsEncryption { get; set; }

        /// <summary>Gets or sets whether split archives are supported.</summary>
        public bool SupportsSplitting { get; set; }
    }

    /// <summary>
    /// Information about an archive.
    /// </summary>
    public class ArchiveInfo
    {
        /// <summary>Gets or sets the archive path.</summary>
        public string Path { get; set; } = string.Empty;

        /// <summary>Gets or sets the format.</summary>
        public string Format { get; set; } = string.Empty;

        /// <summary>Gets or sets the archive size.</summary>
        public long Size { get; set; }

        /// <summary>Gets or sets the uncompressed size.</summary>
        public long UncompressedSize { get; set; }

        /// <summary>Gets the compression ratio.</summary>
        public double CompressionRatio => UncompressedSize > 0 ? (double)Size / UncompressedSize : 1;

        /// <summary>Gets or sets the entry count.</summary>
        public int EntryCount { get; set; }

        /// <summary>Gets or sets the folder count.</summary>
        public int FolderCount { get; set; }

        /// <summary>Gets or sets whether the archive is encrypted.</summary>
        public bool IsEncrypted { get; set; }

        /// <summary>Gets or sets whether it's a split archive.</summary>
        public bool IsSplit { get; set; }

        /// <summary>Gets or sets the volume count (for split).</summary>
        public int VolumeCount { get; set; }

        /// <summary>Gets or sets the comment.</summary>
        public string? Comment { get; set; }

        /// <summary>Gets or sets the creation date.</summary>
        public DateTime? CreatedDate { get; set; }
    }

    /// <summary>
    /// An entry within an archive.
    /// </summary>
    public class ArchiveEntry
    {
        /// <summary>Gets or sets the entry name.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Gets or sets the full path within archive.</summary>
        public string FullPath { get; set; } = string.Empty;

        /// <summary>Gets or sets whether it's a directory.</summary>
        public bool IsDirectory { get; set; }

        /// <summary>Gets or sets the compressed size.</summary>
        public long CompressedSize { get; set; }

        /// <summary>Gets or sets the uncompressed size.</summary>
        public long UncompressedSize { get; set; }

        /// <summary>Gets the compression ratio.</summary>
        public double CompressionRatio => UncompressedSize > 0 ? (double)CompressedSize / UncompressedSize : 1;

        /// <summary>Gets or sets the modification date.</summary>
        public DateTime ModifiedDate { get; set; }

        /// <summary>Gets or sets the creation date.</summary>
        public DateTime? CreatedDate { get; set; }

        /// <summary>Gets or sets the CRC.</summary>
        public uint? Crc { get; set; }

        /// <summary>Gets or sets whether the entry is encrypted.</summary>
        public bool IsEncrypted { get; set; }

        /// <summary>Gets or sets the compression method.</summary>
        public string? CompressionMethod { get; set; }

        /// <summary>Gets or sets the file attributes.</summary>
        public FileAttributes Attributes { get; set; }
    }

    /// <summary>
    /// Options for extracting archives.
    /// </summary>
    public class ExtractionOptions
    {
        /// <summary>Gets or sets whether to preserve directory structure.</summary>
        public bool PreserveDirectoryStructure { get; set; } = true;

        /// <summary>Gets or sets whether to overwrite existing files.</summary>
        public bool OverwriteExisting { get; set; } = false;

        /// <summary>Gets or sets the password for encrypted archives.</summary>
        public string? Password { get; set; }

        /// <summary>Gets or sets whether to extract to a subfolder named after archive.</summary>
        public bool ExtractToSubfolder { get; set; } = false;

        /// <summary>Gets or sets whether to preserve file timestamps.</summary>
        public bool PreserveTimestamps { get; set; } = true;

        /// <summary>Gets or sets whether to preserve file attributes.</summary>
        public bool PreserveAttributes { get; set; } = true;

        /// <summary>Gets or sets file patterns to exclude.</summary>
        public string[]? ExcludePatterns { get; set; }
    }

    /// <summary>
    /// Options for creating archives.
    /// </summary>
    public class ArchiveOptions
    {
        /// <summary>Gets or sets the compression level (0-9).</summary>
        public int CompressionLevel { get; set; } = 5;

        /// <summary>Gets or sets the password for encryption.</summary>
        public string? Password { get; set; }

        /// <summary>Gets or sets the encryption method.</summary>
        public EncryptionMethod EncryptionMethod { get; set; } = EncryptionMethod.AES256;

        /// <summary>Gets or sets whether to encrypt file names.</summary>
        public bool EncryptFileNames { get; set; } = false;

        /// <summary>Gets or sets the split volume size (0 for no split).</summary>
        public long SplitVolumeSize { get; set; } = 0;

        /// <summary>Gets or sets whether to include base folder.</summary>
        public bool IncludeBaseFolder { get; set; } = false;

        /// <summary>Gets or sets the archive comment.</summary>
        public string? Comment { get; set; }

        /// <summary>Gets or sets whether to create solid archive (7z).</summary>
        public bool SolidArchive { get; set; } = false;

        /// <summary>Gets or sets file patterns to exclude.</summary>
        public string[]? ExcludePatterns { get; set; }
    }

    /// <summary>
    /// Encryption methods.
    /// </summary>
    public enum EncryptionMethod
    {
        /// <summary>No encryption.</summary>
        None,

        /// <summary>ZIP traditional encryption (weak).</summary>
        ZipCrypto,

        /// <summary>AES-128.</summary>
        AES128,

        /// <summary>AES-192.</summary>
        AES192,

        /// <summary>AES-256.</summary>
        AES256
    }

    /// <summary>
    /// Progress for extraction.
    /// </summary>
    public class ExtractionProgress
    {
        /// <summary>Gets or sets the current entry name.</summary>
        public string CurrentEntry { get; set; } = string.Empty;

        /// <summary>Gets or sets total entries.</summary>
        public int TotalEntries { get; set; }

        /// <summary>Gets or sets entries extracted.</summary>
        public int ExtractedEntries { get; set; }

        /// <summary>Gets or sets total bytes.</summary>
        public long TotalBytes { get; set; }

        /// <summary>Gets or sets bytes extracted.</summary>
        public long ExtractedBytes { get; set; }

        /// <summary>Gets the percent complete.</summary>
        public int PercentComplete => TotalEntries > 0 ? ExtractedEntries * 100 / TotalEntries : 0;
    }

    /// <summary>
    /// Result of extraction.
    /// </summary>
    public class ExtractionResult
    {
        /// <summary>Gets or sets whether extraction succeeded.</summary>
        public bool Success { get; set; }

        /// <summary>Gets or sets entries extracted.</summary>
        public int EntriesExtracted { get; set; }

        /// <summary>Gets or sets entries skipped.</summary>
        public int EntriesSkipped { get; set; }

        /// <summary>Gets or sets entries failed.</summary>
        public int EntriesFailed { get; set; }

        /// <summary>Gets or sets total bytes extracted.</summary>
        public long BytesExtracted { get; set; }

        /// <summary>Gets or sets the destination path.</summary>
        public string DestinationPath { get; set; } = string.Empty;

        /// <summary>Gets or sets error messages.</summary>
        public List<string> Errors { get; set; } = new();
    }

    /// <summary>
    /// Progress for archive creation.
    /// </summary>
    public class ArchiveProgress
    {
        /// <summary>Gets or sets the current file.</summary>
        public string CurrentFile { get; set; } = string.Empty;

        /// <summary>Gets or sets total files.</summary>
        public int TotalFiles { get; set; }

        /// <summary>Gets or sets files processed.</summary>
        public int ProcessedFiles { get; set; }

        /// <summary>Gets or sets total bytes.</summary>
        public long TotalBytes { get; set; }

        /// <summary>Gets or sets bytes processed.</summary>
        public long ProcessedBytes { get; set; }

        /// <summary>Gets the percent complete.</summary>
        public int PercentComplete => TotalFiles > 0 ? ProcessedFiles * 100 / TotalFiles : 0;
    }

    /// <summary>
    /// Result of archive creation.
    /// </summary>
    public class ArchiveResult
    {
        /// <summary>Gets or sets whether creation succeeded.</summary>
        public bool Success { get; set; }

        /// <summary>Gets or sets the archive path.</summary>
        public string ArchivePath { get; set; } = string.Empty;

        /// <summary>Gets or sets files added.</summary>
        public int FilesAdded { get; set; }

        /// <summary>Gets or sets folders added.</summary>
        public int FoldersAdded { get; set; }

        /// <summary>Gets or sets original size.</summary>
        public long OriginalSize { get; set; }

        /// <summary>Gets or sets compressed size.</summary>
        public long CompressedSize { get; set; }

        /// <summary>Gets or sets error messages.</summary>
        public List<string> Errors { get; set; } = new();
    }

    /// <summary>
    /// Result of archive test.
    /// </summary>
    public class ArchiveTestResult
    {
        /// <summary>Gets or sets whether archive is valid.</summary>
        public bool IsValid { get; set; }

        /// <summary>Gets or sets entries tested.</summary>
        public int EntriesTested { get; set; }

        /// <summary>Gets or sets entries passed.</summary>
        public int EntriesPassed { get; set; }

        /// <summary>Gets or sets entries failed.</summary>
        public int EntriesFailed { get; set; }

        /// <summary>Gets or sets error messages.</summary>
        public List<string> Errors { get; set; } = new();
    }
}
