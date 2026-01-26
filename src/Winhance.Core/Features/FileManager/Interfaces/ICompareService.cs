using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Winhance.Core.Features.FileManager.Interfaces
{
    /// <summary>
    /// Service for comparing files and folders.
    /// </summary>
    public interface ICompareService
    {
        /// <summary>
        /// Compares two files.
        /// </summary>
        /// <param name="file1">First file path.</param>
        /// <param name="file2">Second file path.</param>
        /// <param name="options">Comparison options.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>File comparison result.</returns>
        Task<FileCompareResult> CompareFilesAsync(
            string file1,
            string file2,
            FileCompareOptions? options = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Compares two folders.
        /// </summary>
        /// <param name="folder1">First folder path.</param>
        /// <param name="folder2">Second folder path.</param>
        /// <param name="options">Comparison options.</param>
        /// <param name="progress">Progress reporter.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Folder comparison result.</returns>
        Task<FolderCompareResult> CompareFoldersAsync(
            string folder1,
            string folder2,
            FolderCompareOptions? options = null,
            IProgress<CompareProgress>? progress = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets text diff between two text files.
        /// </summary>
        /// <param name="file1">First file path.</param>
        /// <param name="file2">Second file path.</param>
        /// <param name="options">Diff options.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Text diff result.</returns>
        Task<TextDiffResult> GetTextDiffAsync(
            string file1,
            string file2,
            TextDiffOptions? options = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks if two files are identical.
        /// </summary>
        /// <param name="file1">First file path.</param>
        /// <param name="file2">Second file path.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if files are identical.</returns>
        Task<bool> AreFilesIdenticalAsync(string file1, string file2, CancellationToken cancellationToken = default);

        /// <summary>
        /// Compares file metadata only.
        /// </summary>
        /// <param name="file1">First file path.</param>
        /// <param name="file2">Second file path.</param>
        /// <returns>Metadata comparison result.</returns>
        Task<MetadataCompareResult> CompareMetadataAsync(string file1, string file2);
    }

    /// <summary>
    /// Options for file comparison.
    /// </summary>
    public class FileCompareOptions
    {
        /// <summary>Gets or sets whether to compare content.</summary>
        public bool CompareContent { get; set; } = true;

        /// <summary>Gets or sets whether to compare size.</summary>
        public bool CompareSize { get; set; } = true;

        /// <summary>Gets or sets whether to compare dates.</summary>
        public bool CompareDates { get; set; } = true;

        /// <summary>Gets or sets whether to compare attributes.</summary>
        public bool CompareAttributes { get; set; } = false;

        /// <summary>Gets or sets whether to use hash for content comparison.</summary>
        public bool UseHash { get; set; } = true;

        /// <summary>Gets or sets whether to do byte-by-byte comparison.</summary>
        public bool ByteByByte { get; set; } = false;
    }

    /// <summary>
    /// Options for folder comparison.
    /// </summary>
    public class FolderCompareOptions
    {
        /// <summary>Gets or sets whether to compare recursively.</summary>
        public bool Recursive { get; set; } = true;

        /// <summary>Gets or sets whether to compare file contents.</summary>
        public bool CompareContents { get; set; } = false;

        /// <summary>Gets or sets whether to compare file sizes.</summary>
        public bool CompareSizes { get; set; } = true;

        /// <summary>Gets or sets whether to compare dates.</summary>
        public bool CompareDates { get; set; } = true;

        /// <summary>Gets or sets whether to include hidden files.</summary>
        public bool IncludeHidden { get; set; } = false;

        /// <summary>Gets or sets patterns to exclude.</summary>
        public string[]? ExcludePatterns { get; set; }

        /// <summary>Gets or sets patterns to include (null = all).</summary>
        public string[]? IncludePatterns { get; set; }
    }

    /// <summary>
    /// Options for text diff.
    /// </summary>
    public class TextDiffOptions
    {
        /// <summary>Gets or sets whether to ignore whitespace.</summary>
        public bool IgnoreWhitespace { get; set; } = false;

        /// <summary>Gets or sets whether to ignore case.</summary>
        public bool IgnoreCase { get; set; } = false;

        /// <summary>Gets or sets whether to ignore blank lines.</summary>
        public bool IgnoreBlankLines { get; set; } = false;

        /// <summary>Gets or sets number of context lines.</summary>
        public int ContextLines { get; set; } = 3;
    }

    /// <summary>
    /// Result of file comparison.
    /// </summary>
    public class FileCompareResult
    {
        /// <summary>Gets or sets whether files are identical.</summary>
        public bool AreIdentical { get; set; }

        /// <summary>Gets or sets the first file path.</summary>
        public string File1 { get; set; } = string.Empty;

        /// <summary>Gets or sets the second file path.</summary>
        public string File2 { get; set; } = string.Empty;

        /// <summary>Gets or sets size of first file.</summary>
        public long Size1 { get; set; }

        /// <summary>Gets or sets size of second file.</summary>
        public long Size2 { get; set; }

        /// <summary>Gets or sets whether sizes match.</summary>
        public bool SizesMatch { get; set; }

        /// <summary>Gets or sets hash of first file.</summary>
        public string? Hash1 { get; set; }

        /// <summary>Gets or sets hash of second file.</summary>
        public string? Hash2 { get; set; }

        /// <summary>Gets or sets whether hashes match.</summary>
        public bool? HashesMatch { get; set; }

        /// <summary>Gets or sets modified date of first file.</summary>
        public DateTime ModifiedDate1 { get; set; }

        /// <summary>Gets or sets modified date of second file.</summary>
        public DateTime ModifiedDate2 { get; set; }

        /// <summary>Gets or sets which file is newer.</summary>
        public CompareNewer NewerFile { get; set; }

        /// <summary>Gets or sets the differences found.</summary>
        public List<string> Differences { get; set; } = new();
    }

    /// <summary>
    /// Which file is newer.
    /// </summary>
    public enum CompareNewer
    {
        /// <summary>Files have same date.</summary>
        Same,

        /// <summary>First file is newer.</summary>
        First,

        /// <summary>Second file is newer.</summary>
        Second
    }

    /// <summary>
    /// Result of folder comparison.
    /// </summary>
    public class FolderCompareResult
    {
        /// <summary>Gets or sets the first folder path.</summary>
        public string Folder1 { get; set; } = string.Empty;

        /// <summary>Gets or sets the second folder path.</summary>
        public string Folder2 { get; set; } = string.Empty;

        /// <summary>Gets or sets files only in first folder.</summary>
        public List<string> OnlyInFirst { get; set; } = new();

        /// <summary>Gets or sets files only in second folder.</summary>
        public List<string> OnlyInSecond { get; set; } = new();

        /// <summary>Gets or sets files that are identical.</summary>
        public List<string> Identical { get; set; } = new();

        /// <summary>Gets or sets files that are different.</summary>
        public List<FileDifference> Different { get; set; } = new();

        /// <summary>Gets total files in first folder.</summary>
        public int TotalInFirst => OnlyInFirst.Count + Identical.Count + Different.Count;

        /// <summary>Gets total files in second folder.</summary>
        public int TotalInSecond => OnlyInSecond.Count + Identical.Count + Different.Count;

        /// <summary>Gets whether folders are identical.</summary>
        public bool AreIdentical => OnlyInFirst.Count == 0 && OnlyInSecond.Count == 0 && Different.Count == 0;
    }

    /// <summary>
    /// Information about a file difference.
    /// </summary>
    public class FileDifference
    {
        /// <summary>Gets or sets the relative path.</summary>
        public string RelativePath { get; set; } = string.Empty;

        /// <summary>Gets or sets size in first folder.</summary>
        public long Size1 { get; set; }

        /// <summary>Gets or sets size in second folder.</summary>
        public long Size2 { get; set; }

        /// <summary>Gets or sets date in first folder.</summary>
        public DateTime Date1 { get; set; }

        /// <summary>Gets or sets date in second folder.</summary>
        public DateTime Date2 { get; set; }

        /// <summary>Gets or sets the difference type.</summary>
        public DifferenceType Type { get; set; }
    }

    /// <summary>
    /// Type of difference.
    /// </summary>
    public enum DifferenceType
    {
        /// <summary>Content is different.</summary>
        Content,

        /// <summary>Size is different.</summary>
        Size,

        /// <summary>Date is different.</summary>
        Date,

        /// <summary>Multiple differences.</summary>
        Multiple
    }

    /// <summary>
    /// Result of text diff.
    /// </summary>
    public class TextDiffResult
    {
        /// <summary>Gets or sets whether files are identical.</summary>
        public bool AreIdentical { get; set; }

        /// <summary>Gets or sets the diff hunks.</summary>
        public List<DiffHunk> Hunks { get; set; } = new();

        /// <summary>Gets or sets total lines in first file.</summary>
        public int LinesInFirst { get; set; }

        /// <summary>Gets or sets total lines in second file.</summary>
        public int LinesInSecond { get; set; }

        /// <summary>Gets or sets lines added.</summary>
        public int LinesAdded { get; set; }

        /// <summary>Gets or sets lines removed.</summary>
        public int LinesRemoved { get; set; }

        /// <summary>Gets or sets lines modified.</summary>
        public int LinesModified { get; set; }
    }

    /// <summary>
    /// A hunk in a diff.
    /// </summary>
    public class DiffHunk
    {
        /// <summary>Gets or sets the start line in first file.</summary>
        public int StartLine1 { get; set; }

        /// <summary>Gets or sets the line count in first file.</summary>
        public int LineCount1 { get; set; }

        /// <summary>Gets or sets the start line in second file.</summary>
        public int StartLine2 { get; set; }

        /// <summary>Gets or sets the line count in second file.</summary>
        public int LineCount2 { get; set; }

        /// <summary>Gets or sets the diff lines.</summary>
        public List<DiffLine> Lines { get; set; } = new();
    }

    /// <summary>
    /// A line in a diff.
    /// </summary>
    public class DiffLine
    {
        /// <summary>Gets or sets the line type.</summary>
        public DiffLineType Type { get; set; }

        /// <summary>Gets or sets the line content.</summary>
        public string Content { get; set; } = string.Empty;

        /// <summary>Gets or sets the line number in first file.</summary>
        public int? LineNumber1 { get; set; }

        /// <summary>Gets or sets the line number in second file.</summary>
        public int? LineNumber2 { get; set; }
    }

    /// <summary>
    /// Type of diff line.
    /// </summary>
    public enum DiffLineType
    {
        /// <summary>Unchanged line.</summary>
        Context,

        /// <summary>Line added in second file.</summary>
        Added,

        /// <summary>Line removed from first file.</summary>
        Removed,

        /// <summary>Line modified.</summary>
        Modified
    }

    /// <summary>
    /// Result of metadata comparison.
    /// </summary>
    public class MetadataCompareResult
    {
        /// <summary>Gets or sets size comparison.</summary>
        public bool SizeMatches { get; set; }

        /// <summary>Gets or sets date comparison.</summary>
        public bool DateMatches { get; set; }

        /// <summary>Gets or sets attributes comparison.</summary>
        public bool AttributesMatch { get; set; }

        /// <summary>Gets or sets the differences.</summary>
        public Dictionary<string, MetadataDifference> Differences { get; set; } = new();
    }

    /// <summary>
    /// A metadata difference.
    /// </summary>
    public class MetadataDifference
    {
        /// <summary>Gets or sets the property name.</summary>
        public string Property { get; set; } = string.Empty;

        /// <summary>Gets or sets the value in first file.</summary>
        public string Value1 { get; set; } = string.Empty;

        /// <summary>Gets or sets the value in second file.</summary>
        public string Value2 { get; set; } = string.Empty;
    }

    /// <summary>
    /// Progress for comparison operations.
    /// </summary>
    public class CompareProgress
    {
        /// <summary>Gets or sets the current item.</summary>
        public string CurrentItem { get; set; } = string.Empty;

        /// <summary>Gets or sets total items.</summary>
        public int TotalItems { get; set; }

        /// <summary>Gets or sets processed items.</summary>
        public int ProcessedItems { get; set; }

        /// <summary>Gets the percent complete.</summary>
        public int PercentComplete => TotalItems > 0 ? ProcessedItems * 100 / TotalItems : 0;
    }
}
