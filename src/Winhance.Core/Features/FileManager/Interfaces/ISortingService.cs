using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Winhance.Core.Features.FileManager.Models;

namespace Winhance.Core.Features.FileManager.Interfaces
{
    /// <summary>
    /// Service for sorting and grouping files in the file browser.
    /// </summary>
    public interface ISortingService
    {
        /// <summary>
        /// Gets or sets the current sort column.
        /// </summary>
        SortColumn CurrentSortColumn { get; set; }

        /// <summary>
        /// Gets or sets the sort direction.
        /// </summary>
        SortDirection CurrentSortDirection { get; set; }

        /// <summary>
        /// Gets or sets the current grouping.
        /// </summary>
        GroupBy CurrentGrouping { get; set; }

        /// <summary>
        /// Gets or sets whether folders appear first.
        /// </summary>
        bool FoldersFirst { get; set; }

        /// <summary>
        /// Gets or sets whether to use natural number sorting.
        /// </summary>
        bool UseNaturalSort { get; set; }

        /// <summary>
        /// Gets or sets whether sorting is case-sensitive.
        /// </summary>
        bool CaseSensitive { get; set; }

        /// <summary>
        /// Gets or sets the secondary sort column.
        /// </summary>
        SortColumn? SecondarySortColumn { get; set; }

        /// <summary>
        /// Gets or sets the secondary sort direction.
        /// </summary>
        SortDirection SecondarySortDirection { get; set; }

        /// <summary>
        /// Event raised when sort settings change.
        /// </summary>
        event EventHandler<SortChangedEventArgs>? SortChanged;

        /// <summary>
        /// Event raised when grouping changes.
        /// </summary>
        event EventHandler<GroupChangedEventArgs>? GroupChanged;

        /// <summary>
        /// Sorts a collection of file entries.
        /// </summary>
        /// <param name="files">The files to sort.</param>
        /// <returns>Sorted file collection.</returns>
        IEnumerable<FileSystemEntry> Sort(IEnumerable<FileSystemEntry> files);

        /// <summary>
        /// Groups a collection of file entries.
        /// </summary>
        /// <param name="files">The files to group.</param>
        /// <returns>Grouped file collection.</returns>
        IEnumerable<FileGroup> Group(IEnumerable<FileSystemEntry> files);

        /// <summary>
        /// Sorts and groups files.
        /// </summary>
        /// <param name="files">The files to sort and group.</param>
        /// <returns>Sorted and grouped file collection.</returns>
        IEnumerable<FileGroup> SortAndGroup(IEnumerable<FileSystemEntry> files);

        /// <summary>
        /// Gets the sort settings for a specific folder.
        /// </summary>
        /// <param name="folderPath">The folder path.</param>
        /// <returns>Sort settings for this folder.</returns>
        FolderSortSettings GetSortSettingsForFolder(string folderPath);

        /// <summary>
        /// Sets the sort settings for a specific folder.
        /// </summary>
        /// <param name="folderPath">The folder path.</param>
        /// <param name="settings">The sort settings.</param>
        void SetSortSettingsForFolder(string folderPath, FolderSortSettings settings);

        /// <summary>
        /// Clears the sort settings for a specific folder.
        /// </summary>
        /// <param name="folderPath">The folder path.</param>
        void ClearSortSettingsForFolder(string folderPath);

        /// <summary>
        /// Gets a comparer for the current sort settings.
        /// </summary>
        IComparer<FileSystemEntry> GetComparer();

        /// <summary>
        /// Gets the group key for a file.
        /// </summary>
        /// <param name="file">The file entry.</param>
        /// <returns>The group key.</returns>
        string GetGroupKey(FileSystemEntry file);

        /// <summary>
        /// Saves sort preferences.
        /// </summary>
        Task SavePreferencesAsync();

        /// <summary>
        /// Loads sort preferences.
        /// </summary>
        Task LoadPreferencesAsync();
    }

    /// <summary>
    /// Columns available for sorting.
    /// </summary>
    public enum SortColumn
    {
        /// <summary>File/folder name.</summary>
        Name,

        /// <summary>File size.</summary>
        Size,

        /// <summary>File type/extension.</summary>
        Type,

        /// <summary>Date modified.</summary>
        DateModified,

        /// <summary>Date created.</summary>
        DateCreated,

        /// <summary>Date accessed.</summary>
        DateAccessed,

        /// <summary>File attributes.</summary>
        Attributes,

        /// <summary>Full path.</summary>
        Path,

        /// <summary>Owner.</summary>
        Owner
    }

    /// <summary>
    /// Sort direction.
    /// </summary>
    public enum SortDirection
    {
        /// <summary>Ascending (A-Z, 0-9, oldest first).</summary>
        Ascending,

        /// <summary>Descending (Z-A, 9-0, newest first).</summary>
        Descending
    }

    /// <summary>
    /// Grouping options.
    /// </summary>
    public enum GroupBy
    {
        /// <summary>No grouping.</summary>
        None,

        /// <summary>Group by file type/extension.</summary>
        Type,

        /// <summary>Group by date modified.</summary>
        DateModified,

        /// <summary>Group by date created.</summary>
        DateCreated,

        /// <summary>Group by size category.</summary>
        Size,

        /// <summary>Group by first letter.</summary>
        FirstLetter,

        /// <summary>Group by folder/file.</summary>
        Kind,

        /// <summary>Group by custom property.</summary>
        Custom
    }

    /// <summary>
    /// A group of files.
    /// </summary>
    public class FileGroup
    {
        /// <summary>Gets or sets the group key.</summary>
        public string Key { get; set; } = string.Empty;

        /// <summary>Gets or sets the display name.</summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>Gets or sets the files in this group.</summary>
        public List<FileSystemEntry> Files { get; set; } = new();

        /// <summary>Gets or sets the total size of files in group.</summary>
        public long TotalSize { get; set; }

        /// <summary>Gets the file count.</summary>
        public int FileCount => Files.Count;

        /// <summary>Gets or sets whether the group is expanded.</summary>
        public bool IsExpanded { get; set; } = true;

        /// <summary>Gets or sets the sort order.</summary>
        public int SortOrder { get; set; }
    }

    /// <summary>
    /// Sort settings for a folder.
    /// </summary>
    public class FolderSortSettings
    {
        /// <summary>Gets or sets the sort column.</summary>
        public SortColumn SortColumn { get; set; } = SortColumn.Name;

        /// <summary>Gets or sets the sort direction.</summary>
        public SortDirection SortDirection { get; set; } = SortDirection.Ascending;

        /// <summary>Gets or sets the grouping.</summary>
        public GroupBy GroupBy { get; set; } = GroupBy.None;

        /// <summary>Gets or sets whether folders appear first.</summary>
        public bool FoldersFirst { get; set; } = true;

        /// <summary>Gets or sets the secondary sort column.</summary>
        public SortColumn? SecondarySortColumn { get; set; }

        /// <summary>Gets or sets the secondary sort direction.</summary>
        public SortDirection SecondarySortDirection { get; set; } = SortDirection.Ascending;
    }

    /// <summary>
    /// Event args for sort changes.
    /// </summary>
    public class SortChangedEventArgs : EventArgs
    {
        /// <summary>Gets or sets the old sort column.</summary>
        public SortColumn OldColumn { get; set; }

        /// <summary>Gets or sets the new sort column.</summary>
        public SortColumn NewColumn { get; set; }

        /// <summary>Gets or sets the old direction.</summary>
        public SortDirection OldDirection { get; set; }

        /// <summary>Gets or sets the new direction.</summary>
        public SortDirection NewDirection { get; set; }
    }

    /// <summary>
    /// Event args for group changes.
    /// </summary>
    public class GroupChangedEventArgs : EventArgs
    {
        /// <summary>Gets or sets the old grouping.</summary>
        public GroupBy OldGrouping { get; set; }

        /// <summary>Gets or sets the new grouping.</summary>
        public GroupBy NewGrouping { get; set; }
    }
}
