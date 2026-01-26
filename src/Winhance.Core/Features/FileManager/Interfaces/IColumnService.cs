using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Winhance.Core.Features.FileManager.Models;

namespace Winhance.Core.Features.FileManager.Interfaces
{
    /// <summary>
    /// Service for managing column configuration in Details view.
    /// </summary>
    public interface IColumnService
    {
        /// <summary>
        /// Gets the current column configuration.
        /// </summary>
        IReadOnlyList<ColumnDefinition> CurrentColumns { get; }

        /// <summary>
        /// Gets all available columns.
        /// </summary>
        IReadOnlyList<ColumnDefinition> AvailableColumns { get; }

        /// <summary>
        /// Event raised when columns change.
        /// </summary>
        event EventHandler<ColumnsChangedEventArgs>? ColumnsChanged;

        /// <summary>
        /// Shows a column.
        /// </summary>
        /// <param name="columnId">The column ID.</param>
        void ShowColumn(string columnId);

        /// <summary>
        /// Hides a column.
        /// </summary>
        /// <param name="columnId">The column ID.</param>
        void HideColumn(string columnId);

        /// <summary>
        /// Toggles column visibility.
        /// </summary>
        /// <param name="columnId">The column ID.</param>
        void ToggleColumn(string columnId);

        /// <summary>
        /// Sets column width.
        /// </summary>
        /// <param name="columnId">The column ID.</param>
        /// <param name="width">The width in pixels.</param>
        void SetColumnWidth(string columnId, double width);

        /// <summary>
        /// Moves a column to a new position.
        /// </summary>
        /// <param name="columnId">The column ID.</param>
        /// <param name="newIndex">The new index.</param>
        void MoveColumn(string columnId, int newIndex);

        /// <summary>
        /// Resets columns to default configuration.
        /// </summary>
        void ResetToDefaults();

        /// <summary>
        /// Gets column configuration for a specific folder.
        /// </summary>
        /// <param name="folderPath">The folder path.</param>
        /// <returns>Column configuration.</returns>
        IReadOnlyList<ColumnDefinition> GetColumnsForFolder(string folderPath);

        /// <summary>
        /// Sets column configuration for a specific folder.
        /// </summary>
        /// <param name="folderPath">The folder path.</param>
        /// <param name="columns">The column configuration.</param>
        void SetColumnsForFolder(string folderPath, IEnumerable<ColumnDefinition> columns);

        /// <summary>
        /// Clears folder-specific column configuration.
        /// </summary>
        /// <param name="folderPath">The folder path.</param>
        void ClearColumnsForFolder(string folderPath);

        /// <summary>
        /// Auto-sizes a column to fit content.
        /// </summary>
        /// <param name="columnId">The column ID.</param>
        void AutoSizeColumn(string columnId);

        /// <summary>
        /// Auto-sizes all columns to fit content.
        /// </summary>
        void AutoSizeAllColumns();

        /// <summary>
        /// Saves column preferences.
        /// </summary>
        Task SavePreferencesAsync();

        /// <summary>
        /// Loads column preferences.
        /// </summary>
        Task LoadPreferencesAsync();

        /// <summary>
        /// Gets the value extractor for a column.
        /// </summary>
        /// <param name="columnId">The column ID.</param>
        /// <returns>Function to extract value from FileEntry.</returns>
        Func<FileSystemEntry, object?> GetValueExtractor(string columnId);

        /// <summary>
        /// Gets the display formatter for a column.
        /// </summary>
        /// <param name="columnId">The column ID.</param>
        /// <returns>Function to format value for display.</returns>
        Func<object?, string> GetDisplayFormatter(string columnId);
    }

    /// <summary>
    /// Definition of a column.
    /// </summary>
    public class ColumnDefinition
    {
        /// <summary>Gets or sets the column ID.</summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>Gets or sets the display name.</summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>Gets or sets whether the column is visible.</summary>
        public bool IsVisible { get; set; } = true;

        /// <summary>Gets or sets the width in pixels.</summary>
        public double Width { get; set; } = 100;

        /// <summary>Gets or sets the minimum width.</summary>
        public double MinWidth { get; set; } = 30;

        /// <summary>Gets or sets the maximum width.</summary>
        public double MaxWidth { get; set; } = 500;

        /// <summary>Gets or sets the display index.</summary>
        public int DisplayIndex { get; set; }

        /// <summary>Gets or sets the sort column mapping.</summary>
        public SortColumn SortColumn { get; set; }

        /// <summary>Gets or sets whether the column can be sorted.</summary>
        public bool CanSort { get; set; } = true;

        /// <summary>Gets or sets whether the column can be hidden.</summary>
        public bool CanHide { get; set; } = true;

        /// <summary>Gets or sets the text alignment.</summary>
        public ColumnAlignment Alignment { get; set; } = ColumnAlignment.Left;

        /// <summary>Gets or sets the column type.</summary>
        public ColumnType ColumnType { get; set; } = ColumnType.Text;

        /// <summary>Gets or sets the data binding path.</summary>
        public string BindingPath { get; set; } = string.Empty;

        /// <summary>Gets or sets the format string.</summary>
        public string? FormatString { get; set; }

        /// <summary>Gets or sets whether column width is auto.</summary>
        public bool IsAutoWidth { get; set; } = false;
    }

    /// <summary>
    /// Column alignment.
    /// </summary>
    public enum ColumnAlignment
    {
        /// <summary>Left-aligned.</summary>
        Left,

        /// <summary>Center-aligned.</summary>
        Center,

        /// <summary>Right-aligned.</summary>
        Right
    }

    /// <summary>
    /// Column data type.
    /// </summary>
    public enum ColumnType
    {
        /// <summary>Plain text.</summary>
        Text,

        /// <summary>File size.</summary>
        FileSize,

        /// <summary>Date/time.</summary>
        DateTime,

        /// <summary>File icon.</summary>
        Icon,

        /// <summary>Checkbox.</summary>
        Checkbox,

        /// <summary>Progress bar.</summary>
        Progress,

        /// <summary>Custom template.</summary>
        Custom
    }

    /// <summary>
    /// Event args for column changes.
    /// </summary>
    public class ColumnsChangedEventArgs : EventArgs
    {
        /// <summary>Gets or sets the change type.</summary>
        public ColumnChangeType ChangeType { get; set; }

        /// <summary>Gets or sets the affected column ID.</summary>
        public string? ColumnId { get; set; }

        /// <summary>Gets or sets the new columns configuration.</summary>
        public IReadOnlyList<ColumnDefinition> Columns { get; set; } = Array.Empty<ColumnDefinition>();
    }

    /// <summary>
    /// Types of column changes.
    /// </summary>
    public enum ColumnChangeType
    {
        /// <summary>Column visibility changed.</summary>
        VisibilityChanged,

        /// <summary>Column width changed.</summary>
        WidthChanged,

        /// <summary>Column order changed.</summary>
        OrderChanged,

        /// <summary>All columns reset.</summary>
        Reset,

        /// <summary>Column configuration loaded.</summary>
        Loaded
    }
}
