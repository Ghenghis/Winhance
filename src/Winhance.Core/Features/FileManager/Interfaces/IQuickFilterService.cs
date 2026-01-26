using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Winhance.Core.Features.FileManager.Models;

namespace Winhance.Core.Features.FileManager.Interfaces
{
    /// <summary>
    /// Service for quick filtering files in the current view.
    /// </summary>
    public interface IQuickFilterService
    {
        /// <summary>
        /// Gets or sets the current filter text.
        /// </summary>
        string FilterText { get; set; }

        /// <summary>
        /// Gets or sets whether filtering is active.
        /// </summary>
        bool IsFiltering { get; }

        /// <summary>
        /// Gets or sets the current filter mode.
        /// </summary>
        FilterMode Mode { get; set; }

        /// <summary>
        /// Gets or sets whether filter is case-sensitive.
        /// </summary>
        bool CaseSensitive { get; set; }

        /// <summary>
        /// Gets or sets the extension filter.
        /// </summary>
        string[]? ExtensionFilter { get; set; }

        /// <summary>
        /// Gets or sets the size filter.
        /// </summary>
        SizeFilter? SizeFilter { get; set; }

        /// <summary>
        /// Gets or sets the date filter.
        /// </summary>
        DateFilter? DateFilter { get; set; }

        /// <summary>
        /// Gets or sets the attribute filter.
        /// </summary>
        AttributeFilter? AttributeFilter { get; set; }

        /// <summary>
        /// Event raised when filter changes.
        /// </summary>
        event EventHandler<FilterChangedEventArgs>? FilterChanged;

        /// <summary>
        /// Filters a collection of files.
        /// </summary>
        /// <param name="files">Files to filter.</param>
        /// <returns>Filtered files.</returns>
        IEnumerable<FileSystemEntry> Filter(IEnumerable<FileSystemEntry> files);

        /// <summary>
        /// Checks if a file matches the current filter.
        /// </summary>
        /// <param name="file">File to check.</param>
        /// <returns>True if file matches filter.</returns>
        bool Matches(FileSystemEntry file);

        /// <summary>
        /// Clears all filters.
        /// </summary>
        void ClearAll();

        /// <summary>
        /// Clears just the text filter.
        /// </summary>
        void ClearTextFilter();

        /// <summary>
        /// Clears extension filter.
        /// </summary>
        void ClearExtensionFilter();

        /// <summary>
        /// Clears size filter.
        /// </summary>
        void ClearSizeFilter();

        /// <summary>
        /// Clears date filter.
        /// </summary>
        void ClearDateFilter();

        /// <summary>
        /// Clears attribute filter.
        /// </summary>
        void ClearAttributeFilter();

        /// <summary>
        /// Sets extension filter from common presets.
        /// </summary>
        /// <param name="preset">The preset to apply.</param>
        void SetExtensionPreset(ExtensionPreset preset);

        /// <summary>
        /// Gets the filter history.
        /// </summary>
        /// <param name="maxItems">Maximum items to return.</param>
        IEnumerable<string> GetFilterHistory(int maxItems = 10);

        /// <summary>
        /// Adds a filter to history.
        /// </summary>
        /// <param name="filter">Filter text.</param>
        void AddToHistory(string filter);

        /// <summary>
        /// Clears filter history.
        /// </summary>
        void ClearHistory();

        /// <summary>
        /// Saves a filter preset.
        /// </summary>
        /// <param name="name">Preset name.</param>
        /// <param name="preset">Filter settings.</param>
        Task SavePresetAsync(string name, FilterPreset preset);

        /// <summary>
        /// Loads a filter preset.
        /// </summary>
        /// <param name="name">Preset name.</param>
        Task LoadPresetAsync(string name);

        /// <summary>
        /// Gets all saved presets.
        /// </summary>
        IEnumerable<FilterPreset> GetPresets();

        /// <summary>
        /// Deletes a preset.
        /// </summary>
        /// <param name="name">Preset name.</param>
        void DeletePreset(string name);

        /// <summary>
        /// Gets the current filter as a preset.
        /// </summary>
        FilterPreset GetCurrentAsPreset();

        /// <summary>
        /// Gets filter statistics for current results.
        /// </summary>
        /// <param name="allFiles">All files before filter.</param>
        /// <param name="filteredFiles">Files after filter.</param>
        FilterStatistics GetStatistics(IEnumerable<FileSystemEntry> allFiles, IEnumerable<FileSystemEntry> filteredFiles);
    }

    /// <summary>
    /// Filter mode.
    /// </summary>
    public enum FilterMode
    {
        /// <summary>Simple contains match.</summary>
        Contains,

        /// <summary>Wildcard pattern (*, ?).</summary>
        Wildcard,

        /// <summary>Regular expression.</summary>
        Regex,

        /// <summary>Exact match.</summary>
        Exact
    }

    /// <summary>
    /// Size filter settings.
    /// </summary>
    public class SizeFilter
    {
        /// <summary>Gets or sets minimum size in bytes.</summary>
        public long? MinSize { get; set; }

        /// <summary>Gets or sets maximum size in bytes.</summary>
        public long? MaxSize { get; set; }

        /// <summary>Gets or sets size category preset.</summary>
        public SizeCategory? Category { get; set; }
    }

    /// <summary>
    /// Size categories for quick filtering.
    /// </summary>
    public enum SizeCategory
    {
        /// <summary>Empty files (0 bytes).</summary>
        Empty,

        /// <summary>Tiny (less than 16 KB).</summary>
        Tiny,

        /// <summary>Small (16 KB - 1 MB).</summary>
        Small,

        /// <summary>Medium (1 MB - 128 MB).</summary>
        Medium,

        /// <summary>Large (128 MB - 1 GB).</summary>
        Large,

        /// <summary>Huge (greater than 1 GB).</summary>
        Huge
    }

    /// <summary>
    /// Date filter settings.
    /// </summary>
    public class DateFilter
    {
        /// <summary>Gets or sets the date type to filter on.</summary>
        public DateType DateType { get; set; } = DateType.Modified;

        /// <summary>Gets or sets the start date.</summary>
        public DateTime? StartDate { get; set; }

        /// <summary>Gets or sets the end date.</summary>
        public DateTime? EndDate { get; set; }

        /// <summary>Gets or sets a relative date preset.</summary>
        public DatePreset? Preset { get; set; }
    }

    /// <summary>
    /// Date presets for quick filtering.
    /// </summary>
    public enum DatePreset
    {
        /// <summary>Today.</summary>
        Today,

        /// <summary>Yesterday.</summary>
        Yesterday,

        /// <summary>This week.</summary>
        ThisWeek,

        /// <summary>Last week.</summary>
        LastWeek,

        /// <summary>This month.</summary>
        ThisMonth,

        /// <summary>Last month.</summary>
        LastMonth,

        /// <summary>This year.</summary>
        ThisYear,

        /// <summary>Last year.</summary>
        LastYear,

        /// <summary>Older than a year.</summary>
        OlderThanYear
    }

    /// <summary>
    /// Attribute filter settings.
    /// </summary>
    public class AttributeFilter
    {
        /// <summary>Gets or sets whether to include hidden files.</summary>
        public bool? IncludeHidden { get; set; }

        /// <summary>Gets or sets whether to include system files.</summary>
        public bool? IncludeSystem { get; set; }

        /// <summary>Gets or sets whether to include read-only files.</summary>
        public bool? IncludeReadOnly { get; set; }

        /// <summary>Gets or sets whether to show only directories.</summary>
        public bool? DirectoriesOnly { get; set; }

        /// <summary>Gets or sets whether to show only files.</summary>
        public bool? FilesOnly { get; set; }
    }

    /// <summary>
    /// Extension presets.
    /// </summary>
    public enum ExtensionPreset
    {
        /// <summary>All files.</summary>
        All,

        /// <summary>Images (jpg, png, gif, bmp, etc.).</summary>
        Images,

        /// <summary>Videos (mp4, avi, mkv, etc.).</summary>
        Videos,

        /// <summary>Audio (mp3, wav, flac, etc.).</summary>
        Audio,

        /// <summary>Documents (pdf, doc, txt, etc.).</summary>
        Documents,

        /// <summary>Archives (zip, rar, 7z, etc.).</summary>
        Archives,

        /// <summary>Code files (cs, js, py, etc.).</summary>
        Code,

        /// <summary>Executables (exe, msi, bat, etc.).</summary>
        Executables
    }

    /// <summary>
    /// A saved filter preset.
    /// </summary>
    public class FilterPreset
    {
        /// <summary>Gets or sets the preset name.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Gets or sets the filter text.</summary>
        public string? FilterText { get; set; }

        /// <summary>Gets or sets the filter mode.</summary>
        public FilterMode Mode { get; set; }

        /// <summary>Gets or sets whether case-sensitive.</summary>
        public bool CaseSensitive { get; set; }

        /// <summary>Gets or sets extension filter.</summary>
        public string[]? Extensions { get; set; }

        /// <summary>Gets or sets size filter.</summary>
        public SizeFilter? SizeFilter { get; set; }

        /// <summary>Gets or sets date filter.</summary>
        public DateFilter? DateFilter { get; set; }

        /// <summary>Gets or sets attribute filter.</summary>
        public AttributeFilter? AttributeFilter { get; set; }

        /// <summary>Gets or sets the creation date.</summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Filter statistics.
    /// </summary>
    public class FilterStatistics
    {
        /// <summary>Gets or sets total files before filter.</summary>
        public int TotalFiles { get; set; }

        /// <summary>Gets or sets files shown after filter.</summary>
        public int ShownFiles { get; set; }

        /// <summary>Gets or sets files hidden by filter.</summary>
        public int HiddenFiles { get; set; }

        /// <summary>Gets or sets total size before filter.</summary>
        public long TotalSize { get; set; }

        /// <summary>Gets or sets shown size after filter.</summary>
        public long ShownSize { get; set; }

        /// <summary>Gets the percentage of files shown.</summary>
        public double PercentShown => TotalFiles > 0 ? (double)ShownFiles / TotalFiles * 100 : 100;
    }

    /// <summary>
    /// Event args for filter changes.
    /// </summary>
    public class FilterChangedEventArgs : EventArgs
    {
        /// <summary>Gets or sets whether filter is active.</summary>
        public bool IsActive { get; set; }

        /// <summary>Gets or sets the filter text.</summary>
        public string? FilterText { get; set; }

        /// <summary>Gets or sets the change type.</summary>
        public FilterChangeType ChangeType { get; set; }
    }

    /// <summary>
    /// Types of filter changes.
    /// </summary>
    public enum FilterChangeType
    {
        /// <summary>Text filter changed.</summary>
        TextChanged,

        /// <summary>Extension filter changed.</summary>
        ExtensionChanged,

        /// <summary>Size filter changed.</summary>
        SizeChanged,

        /// <summary>Date filter changed.</summary>
        DateChanged,

        /// <summary>Attribute filter changed.</summary>
        AttributeChanged,

        /// <summary>All filters cleared.</summary>
        Cleared,

        /// <summary>Preset loaded.</summary>
        PresetLoaded
    }
}
