using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Winhance.Core.Features.FileManager.Models;

namespace Winhance.Core.Features.FileManager.Interfaces
{
    /// <summary>
    /// Service for managing file selection in the file browser.
    /// </summary>
    public interface ISelectionService
    {
        /// <summary>
        /// Gets the currently selected items.
        /// </summary>
        ObservableCollection<FileItem> SelectedItems { get; }

        /// <summary>
        /// Gets the selection count.
        /// </summary>
        int SelectionCount { get; }

        /// <summary>
        /// Gets the total size of selected items.
        /// </summary>
        long SelectionSize { get; }

        /// <summary>
        /// Gets the formatted selection size string.
        /// </summary>
        string SelectionSizeDisplay { get; }

        /// <summary>
        /// Gets or sets whether checkbox selection mode is enabled.
        /// </summary>
        bool CheckboxMode { get; set; }

        /// <summary>
        /// Gets or sets the anchor item for range selection.
        /// </summary>
        FileItem? AnchorItem { get; set; }

        /// <summary>
        /// Event raised when selection changes.
        /// </summary>
        event EventHandler<SelectionChangedEventArgs>? SelectionChanged;

        /// <summary>
        /// Selects a single item.
        /// </summary>
        /// <param name="item">The item to select.</param>
        /// <param name="clearExisting">Whether to clear existing selection.</param>
        void Select(FileItem item, bool clearExisting = true);

        /// <summary>
        /// Selects multiple items.
        /// </summary>
        /// <param name="items">The items to select.</param>
        /// <param name="clearExisting">Whether to clear existing selection.</param>
        void SelectMany(IEnumerable<FileItem> items, bool clearExisting = true);

        /// <summary>
        /// Toggles selection of an item.
        /// </summary>
        /// <param name="item">The item to toggle.</param>
        void Toggle(FileItem item);

        /// <summary>
        /// Selects a range of items from anchor to the specified item.
        /// </summary>
        /// <param name="item">The end item of the range.</param>
        /// <param name="allItems">All items in the list (for range calculation).</param>
        void SelectRange(FileItem item, IList<FileItem> allItems);

        /// <summary>
        /// Deselects a single item.
        /// </summary>
        /// <param name="item">The item to deselect.</param>
        void Deselect(FileItem item);

        /// <summary>
        /// Deselects multiple items.
        /// </summary>
        /// <param name="items">The items to deselect.</param>
        void DeselectMany(IEnumerable<FileItem> items);

        /// <summary>
        /// Selects all items.
        /// </summary>
        /// <param name="allItems">All items to select.</param>
        void SelectAll(IEnumerable<FileItem> allItems);

        /// <summary>
        /// Clears all selection.
        /// </summary>
        void ClearSelection();

        /// <summary>
        /// Inverts the current selection.
        /// </summary>
        /// <param name="allItems">All items (for inversion).</param>
        void InvertSelection(IEnumerable<FileItem> allItems);

        /// <summary>
        /// Selects items matching a pattern.
        /// </summary>
        /// <param name="pattern">Wildcard pattern (e.g., "*.txt").</param>
        /// <param name="allItems">All items to search.</param>
        /// <param name="addToSelection">Whether to add to existing selection.</param>
        void SelectByPattern(string pattern, IEnumerable<FileItem> allItems, bool addToSelection = false);

        /// <summary>
        /// Selects items matching a regex pattern.
        /// </summary>
        /// <param name="regexPattern">Regex pattern.</param>
        /// <param name="allItems">All items to search.</param>
        /// <param name="addToSelection">Whether to add to existing selection.</param>
        void SelectByRegex(string regexPattern, IEnumerable<FileItem> allItems, bool addToSelection = false);

        /// <summary>
        /// Selects items by extension.
        /// </summary>
        /// <param name="extensions">Extensions to select (e.g., ".txt", ".pdf").</param>
        /// <param name="allItems">All items to search.</param>
        /// <param name="addToSelection">Whether to add to existing selection.</param>
        void SelectByExtension(IEnumerable<string> extensions, IEnumerable<FileItem> allItems, bool addToSelection = false);

        /// <summary>
        /// Selects items by size range.
        /// </summary>
        /// <param name="minSize">Minimum size in bytes (null for no minimum).</param>
        /// <param name="maxSize">Maximum size in bytes (null for no maximum).</param>
        /// <param name="allItems">All items to search.</param>
        /// <param name="addToSelection">Whether to add to existing selection.</param>
        void SelectBySizeRange(long? minSize, long? maxSize, IEnumerable<FileItem> allItems, bool addToSelection = false);

        /// <summary>
        /// Selects items by date range.
        /// </summary>
        /// <param name="startDate">Start date (null for no start).</param>
        /// <param name="endDate">End date (null for no end).</param>
        /// <param name="dateType">Which date to compare.</param>
        /// <param name="allItems">All items to search.</param>
        /// <param name="addToSelection">Whether to add to existing selection.</param>
        void SelectByDateRange(DateTime? startDate, DateTime? endDate, DateType dateType, IEnumerable<FileItem> allItems, bool addToSelection = false);

        /// <summary>
        /// Selects only files (excludes folders).
        /// </summary>
        /// <param name="allItems">All items to filter.</param>
        void SelectFilesOnly(IEnumerable<FileItem> allItems);

        /// <summary>
        /// Selects only folders (excludes files).
        /// </summary>
        /// <param name="allItems">All items to filter.</param>
        void SelectFoldersOnly(IEnumerable<FileItem> allItems);

        /// <summary>
        /// Checks if an item is selected.
        /// </summary>
        /// <param name="item">The item to check.</param>
        /// <returns>True if selected.</returns>
        bool IsSelected(FileItem item);

        /// <summary>
        /// Saves the current selection as a named set.
        /// </summary>
        /// <param name="name">The set name.</param>
        Task SaveSelectionSetAsync(string name);

        /// <summary>
        /// Loads a saved selection set.
        /// </summary>
        /// <param name="name">The set name.</param>
        /// <param name="allItems">All available items.</param>
        Task LoadSelectionSetAsync(string name, IEnumerable<FileItem> allItems);

        /// <summary>
        /// Gets all saved selection sets.
        /// </summary>
        IEnumerable<SelectionSet> GetSelectionSets();

        /// <summary>
        /// Deletes a saved selection set.
        /// </summary>
        /// <param name="name">The set name.</param>
        void DeleteSelectionSet(string name);

        /// <summary>
        /// Copies the selection to clipboard as paths.
        /// </summary>
        void CopySelectionToClipboard();

        /// <summary>
        /// Selects items from clipboard paths.
        /// </summary>
        /// <param name="allItems">All available items.</param>
        void SelectFromClipboard(IEnumerable<FileItem> allItems);
    }

    /// <summary>
    /// Date type for date-based selection.
    /// </summary>
    public enum DateType
    {
        /// <summary>Date modified.</summary>
        Modified,

        /// <summary>Date created.</summary>
        Created,

        /// <summary>Date accessed.</summary>
        Accessed
    }

    /// <summary>
    /// A saved selection set.
    /// </summary>
    public class SelectionSet
    {
        /// <summary>Gets or sets the set name.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Gets or sets the selected paths.</summary>
        public List<string> Paths { get; set; } = new();

        /// <summary>Gets or sets the creation time.</summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>Gets the count of paths.</summary>
        public int Count => Paths.Count;
    }

    /// <summary>
    /// Event args for selection changes.
    /// </summary>
    public class SelectionChangedEventArgs : EventArgs
    {
        /// <summary>Gets or sets items that were added to selection.</summary>
        public IReadOnlyList<FileItem> AddedItems { get; set; } = Array.Empty<FileItem>();

        /// <summary>Gets or sets items that were removed from selection.</summary>
        public IReadOnlyList<FileItem> RemovedItems { get; set; } = Array.Empty<FileItem>();

        /// <summary>Gets or sets the current selection count.</summary>
        public int SelectionCount { get; set; }

        /// <summary>Gets or sets the current selection size.</summary>
        public long SelectionSize { get; set; }
    }
}
