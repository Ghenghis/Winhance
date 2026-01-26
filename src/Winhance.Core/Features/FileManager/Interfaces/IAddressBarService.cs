using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Winhance.Core.Features.FileManager.Interfaces
{
    /// <summary>
    /// Service for address bar functionality including breadcrumb navigation,
    /// path autocomplete, and recent/frequent locations.
    /// </summary>
    public interface IAddressBarService
    {
        /// <summary>
        /// Gets or sets the current path.
        /// </summary>
        string CurrentPath { get; set; }

        /// <summary>
        /// Gets the breadcrumb segments for the current path.
        /// </summary>
        IReadOnlyList<BreadcrumbSegment> Breadcrumbs { get; }

        /// <summary>
        /// Gets whether the address bar is in edit mode.
        /// </summary>
        bool IsEditing { get; set; }

        /// <summary>
        /// Event raised when the path changes.
        /// </summary>
        event EventHandler<PathChangedEventArgs>? PathChanged;

        /// <summary>
        /// Event raised when breadcrumbs are updated.
        /// </summary>
        event EventHandler? BreadcrumbsChanged;

        /// <summary>
        /// Navigates to the specified path.
        /// </summary>
        /// <param name="path">The path to navigate to.</param>
        /// <param name="addToHistory">Whether to add to navigation history.</param>
        /// <returns>True if navigation succeeded.</returns>
        Task<bool> NavigateAsync(string path, bool addToHistory = true);

        /// <summary>
        /// Navigates to a breadcrumb segment.
        /// </summary>
        /// <param name="segment">The breadcrumb segment to navigate to.</param>
        /// <returns>True if navigation succeeded.</returns>
        Task<bool> NavigateToBreadcrumbAsync(BreadcrumbSegment segment);

        /// <summary>
        /// Gets autocomplete suggestions for the given input.
        /// </summary>
        /// <param name="input">The current input text.</param>
        /// <param name="maxResults">Maximum number of results.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of path suggestions.</returns>
        Task<IEnumerable<PathSuggestion>> GetSuggestionsAsync(
            string input,
            int maxResults = 10,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets subfolders for a breadcrumb dropdown.
        /// </summary>
        /// <param name="segment">The breadcrumb segment.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of subfolders.</returns>
        Task<IEnumerable<FolderItem>> GetSubfoldersAsync(
            BreadcrumbSegment segment,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Parses a path string into breadcrumb segments.
        /// </summary>
        /// <param name="path">The path to parse.</param>
        /// <returns>List of breadcrumb segments.</returns>
        IEnumerable<BreadcrumbSegment> ParsePath(string path);

        /// <summary>
        /// Validates a path string.
        /// </summary>
        /// <param name="path">The path to validate.</param>
        /// <returns>Validation result.</returns>
        PathValidationResult ValidatePath(string path);

        /// <summary>
        /// Expands environment variables and special folders in a path.
        /// </summary>
        /// <param name="path">The path to expand.</param>
        /// <returns>The expanded path.</returns>
        string ExpandPath(string path);

        /// <summary>
        /// Gets the display name for a path.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <returns>Display-friendly name.</returns>
        string GetDisplayName(string path);

        /// <summary>
        /// Gets the icon for a path.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <returns>Icon identifier.</returns>
        string GetPathIcon(string path);

        /// <summary>
        /// Copies the current path to clipboard.
        /// </summary>
        /// <param name="format">The format to copy.</param>
        void CopyPathToClipboard(PathCopyFormat format = PathCopyFormat.FullPath);
    }

    /// <summary>
    /// Represents a breadcrumb segment in the path.
    /// </summary>
    public class BreadcrumbSegment
    {
        /// <summary>Gets or sets the display name.</summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>Gets or sets the full path.</summary>
        public string FullPath { get; set; } = string.Empty;

        /// <summary>Gets or sets the icon identifier.</summary>
        public string Icon { get; set; } = string.Empty;

        /// <summary>Gets or sets whether this is a special folder.</summary>
        public bool IsSpecialFolder { get; set; }

        /// <summary>Gets or sets the special folder type.</summary>
        public SpecialFolderType? SpecialFolder { get; set; }

        /// <summary>Gets or sets whether this segment has children.</summary>
        public bool HasChildren { get; set; }

        /// <summary>Gets or sets whether this is the last segment.</summary>
        public bool IsLast { get; set; }

        /// <summary>Gets or sets the segment index (0-based).</summary>
        public int Index { get; set; }
    }

    /// <summary>
    /// Special folder types for breadcrumbs.
    /// </summary>
    public enum SpecialFolderType
    {
        /// <summary>This PC / Computer.</summary>
        ThisPc,

        /// <summary>Drive root.</summary>
        DriveRoot,

        /// <summary>Desktop folder.</summary>
        Desktop,

        /// <summary>Documents folder.</summary>
        Documents,

        /// <summary>Downloads folder.</summary>
        Downloads,

        /// <summary>Pictures folder.</summary>
        Pictures,

        /// <summary>Music folder.</summary>
        Music,

        /// <summary>Videos folder.</summary>
        Videos,

        /// <summary>User profile folder.</summary>
        UserProfile,

        /// <summary>Network location.</summary>
        Network,

        /// <summary>Recycle Bin.</summary>
        RecycleBin,

        /// <summary>Quick Access.</summary>
        QuickAccess,

        /// <summary>OneDrive.</summary>
        OneDrive
    }

    /// <summary>
    /// Path autocomplete suggestion.
    /// </summary>
    public class PathSuggestion
    {
        /// <summary>Gets or sets the suggestion type.</summary>
        public SuggestionType Type { get; set; }

        /// <summary>Gets or sets the display text.</summary>
        public string DisplayText { get; set; } = string.Empty;

        /// <summary>Gets or sets the full path.</summary>
        public string FullPath { get; set; } = string.Empty;

        /// <summary>Gets or sets the icon.</summary>
        public string Icon { get; set; } = string.Empty;

        /// <summary>Gets or sets secondary text (e.g., path or date).</summary>
        public string? SecondaryText { get; set; }

        /// <summary>Gets or sets the match score (0-100).</summary>
        public int Score { get; set; }

        /// <summary>Gets or sets the matched portion start index.</summary>
        public int MatchStart { get; set; }

        /// <summary>Gets or sets the matched portion length.</summary>
        public int MatchLength { get; set; }
    }

    /// <summary>
    /// Types of path suggestions.
    /// </summary>
    public enum SuggestionType
    {
        /// <summary>Existing path on disk.</summary>
        Path,

        /// <summary>Recent location.</summary>
        Recent,

        /// <summary>Frequent location.</summary>
        Frequent,

        /// <summary>Favorite location.</summary>
        Favorite,

        /// <summary>Special folder.</summary>
        SpecialFolder,

        /// <summary>Drive root.</summary>
        Drive,

        /// <summary>Network location.</summary>
        Network,

        /// <summary>Search query.</summary>
        Search
    }

    /// <summary>
    /// Folder item for breadcrumb dropdowns.
    /// </summary>
    public class FolderItem
    {
        /// <summary>Gets or sets the folder name.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Gets or sets the full path.</summary>
        public string FullPath { get; set; } = string.Empty;

        /// <summary>Gets or sets the icon.</summary>
        public string Icon { get; set; } = string.Empty;

        /// <summary>Gets or sets whether it's hidden.</summary>
        public bool IsHidden { get; set; }

        /// <summary>Gets or sets whether it's a system folder.</summary>
        public bool IsSystem { get; set; }
    }

    /// <summary>
    /// Path validation result.
    /// </summary>
    public class PathValidationResult
    {
        /// <summary>Gets or sets whether the path is valid.</summary>
        public bool IsValid { get; set; }

        /// <summary>Gets or sets whether the path exists.</summary>
        public bool Exists { get; set; }

        /// <summary>Gets or sets the normalized path.</summary>
        public string NormalizedPath { get; set; } = string.Empty;

        /// <summary>Gets or sets the error message if invalid.</summary>
        public string? ErrorMessage { get; set; }

        /// <summary>Gets or sets invalid character positions.</summary>
        public int[] InvalidCharPositions { get; set; } = Array.Empty<int>();
    }

    /// <summary>
    /// Event args for path changes.
    /// </summary>
    public class PathChangedEventArgs : EventArgs
    {
        /// <summary>Gets or sets the old path.</summary>
        public string OldPath { get; set; } = string.Empty;

        /// <summary>Gets or sets the new path.</summary>
        public string NewPath { get; set; } = string.Empty;

        /// <summary>Gets or sets whether it was added to history.</summary>
        public bool AddedToHistory { get; set; }
    }

    /// <summary>
    /// Format for copying paths to clipboard.
    /// </summary>
    public enum PathCopyFormat
    {
        /// <summary>Full path (C:\Folder\File.txt).</summary>
        FullPath,

        /// <summary>Name only (File.txt).</summary>
        NameOnly,

        /// <summary>Path in quotes ("C:\Folder\File.txt").</summary>
        Quoted,

        /// <summary>Unix-style path (/c/Folder/File.txt).</summary>
        UnixStyle,

        /// <summary>UNC path (\\?\C:\Folder\File.txt).</summary>
        UncPath,

        /// <summary>URL file path (file:///C:/Folder/File.txt).</summary>
        FileUrl
    }
}
