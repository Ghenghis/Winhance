using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Winhance.Core.Features.FileManager.Interfaces
{
    /// <summary>
    /// Service for clipboard operations (cut, copy, paste) in the file manager.
    /// </summary>
    public interface IClipboardService
    {
        /// <summary>
        /// Gets the current clipboard operation type.
        /// </summary>
        ClipboardOperation CurrentOperation { get; }

        /// <summary>
        /// Gets the items currently on the clipboard.
        /// </summary>
        IReadOnlyList<string> ClipboardItems { get; }

        /// <summary>
        /// Gets whether clipboard has items.
        /// </summary>
        bool HasItems { get; }

        /// <summary>
        /// Gets whether clipboard has files/folders that can be pasted.
        /// </summary>
        bool CanPaste { get; }

        /// <summary>
        /// Event raised when clipboard contents change.
        /// </summary>
        event EventHandler<ClipboardChangedEventArgs>? ClipboardChanged;

        /// <summary>
        /// Copies files/folders to clipboard.
        /// </summary>
        /// <param name="paths">The paths to copy.</param>
        void Copy(IEnumerable<string> paths);

        /// <summary>
        /// Cuts files/folders to clipboard.
        /// </summary>
        /// <param name="paths">The paths to cut.</param>
        void Cut(IEnumerable<string> paths);

        /// <summary>
        /// Pastes clipboard contents to a destination.
        /// </summary>
        /// <param name="destinationPath">The destination folder.</param>
        /// <param name="progress">Progress reporter.</param>
        /// <returns>Result of paste operation.</returns>
        Task<PasteResult> PasteAsync(string destinationPath, IProgress<PasteProgress>? progress = null);

        /// <summary>
        /// Clears the clipboard.
        /// </summary>
        void Clear();

        /// <summary>
        /// Copies paths as text to clipboard.
        /// </summary>
        /// <param name="paths">The paths to copy.</param>
        /// <param name="format">The text format.</param>
        void CopyAsText(IEnumerable<string> paths, PathTextFormat format = PathTextFormat.FullPath);

        /// <summary>
        /// Copies a single path as text.
        /// </summary>
        /// <param name="path">The path to copy.</param>
        /// <param name="format">The text format.</param>
        void CopyPathAsText(string path, PathTextFormat format = PathTextFormat.FullPath);

        /// <summary>
        /// Gets paths from text on clipboard.
        /// </summary>
        /// <returns>List of paths found in clipboard text.</returns>
        IEnumerable<string> GetPathsFromText();

        /// <summary>
        /// Checks if a specific path is on the clipboard.
        /// </summary>
        /// <param name="path">The path to check.</param>
        /// <returns>True if path is on clipboard.</returns>
        bool ContainsPath(string path);

        /// <summary>
        /// Gets whether a path is marked for cut (move).
        /// </summary>
        /// <param name="path">The path to check.</param>
        /// <returns>True if path is marked for cut.</returns>
        bool IsCutOperation(string path);

        /// <summary>
        /// Registers for clipboard change notifications.
        /// </summary>
        void StartMonitoring();

        /// <summary>
        /// Stops clipboard change monitoring.
        /// </summary>
        void StopMonitoring();
    }

    /// <summary>
    /// Clipboard operation types.
    /// </summary>
    public enum ClipboardOperation
    {
        /// <summary>No operation.</summary>
        None,

        /// <summary>Copy operation.</summary>
        Copy,

        /// <summary>Cut (move) operation.</summary>
        Cut
    }

    /// <summary>
    /// Text format for copying paths.
    /// </summary>
    public enum PathTextFormat
    {
        /// <summary>Full path.</summary>
        FullPath,

        /// <summary>Name only.</summary>
        NameOnly,

        /// <summary>Path in quotes.</summary>
        Quoted,

        /// <summary>One path per line.</summary>
        MultiLine,

        /// <summary>Paths separated by semicolons.</summary>
        Semicolon,

        /// <summary>Unix-style paths.</summary>
        UnixStyle,

        /// <summary>HTML links.</summary>
        HtmlLinks
    }

    /// <summary>
    /// Event args for clipboard changes.
    /// </summary>
    public class ClipboardChangedEventArgs : EventArgs
    {
        /// <summary>Gets or sets the operation type.</summary>
        public ClipboardOperation Operation { get; set; }

        /// <summary>Gets or sets the item count.</summary>
        public int ItemCount { get; set; }

        /// <summary>Gets or sets whether clipboard has files.</summary>
        public bool HasFiles { get; set; }

        /// <summary>Gets or sets whether clipboard has text.</summary>
        public bool HasText { get; set; }
    }

    /// <summary>
    /// Progress for paste operation.
    /// </summary>
    public class PasteProgress
    {
        /// <summary>Gets or sets the current file being processed.</summary>
        public string CurrentFile { get; set; } = string.Empty;

        /// <summary>Gets or sets the total items.</summary>
        public int TotalItems { get; set; }

        /// <summary>Gets or sets items processed.</summary>
        public int ProcessedItems { get; set; }

        /// <summary>Gets or sets bytes processed.</summary>
        public long BytesProcessed { get; set; }

        /// <summary>Gets or sets total bytes.</summary>
        public long TotalBytes { get; set; }

        /// <summary>Gets the percent complete.</summary>
        public int PercentComplete => TotalItems > 0 ? ProcessedItems * 100 / TotalItems : 0;
    }

    /// <summary>
    /// Result of paste operation.
    /// </summary>
    public class PasteResult
    {
        /// <summary>Gets or sets whether operation succeeded.</summary>
        public bool Success { get; set; }

        /// <summary>Gets or sets items successfully pasted.</summary>
        public int ItemsPasted { get; set; }

        /// <summary>Gets or sets items that failed.</summary>
        public int ItemsFailed { get; set; }

        /// <summary>Gets or sets items skipped (conflicts).</summary>
        public int ItemsSkipped { get; set; }

        /// <summary>Gets or sets total bytes copied/moved.</summary>
        public long BytesProcessed { get; set; }

        /// <summary>Gets or sets errors that occurred.</summary>
        public List<PasteError> Errors { get; set; } = new();

        /// <summary>Gets or sets the operation type that was performed.</summary>
        public ClipboardOperation Operation { get; set; }
    }

    /// <summary>
    /// Error during paste operation.
    /// </summary>
    public class PasteError
    {
        /// <summary>Gets or sets the source path.</summary>
        public string SourcePath { get; set; } = string.Empty;

        /// <summary>Gets or sets the destination path.</summary>
        public string DestinationPath { get; set; } = string.Empty;

        /// <summary>Gets or sets the error message.</summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>Gets or sets the exception.</summary>
        public Exception? Exception { get; set; }
    }
}
