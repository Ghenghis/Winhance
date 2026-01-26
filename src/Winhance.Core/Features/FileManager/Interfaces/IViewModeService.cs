using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Winhance.Core.Features.FileManager.Interfaces
{
    /// <summary>
    /// Service for managing view modes (Details, Icons, Tiles, etc.) in the file browser.
    /// </summary>
    public interface IViewModeService
    {
        /// <summary>
        /// Gets or sets the current view mode.
        /// </summary>
        ViewMode CurrentViewMode { get; set; }

        /// <summary>
        /// Gets or sets the global default view mode.
        /// </summary>
        ViewMode DefaultViewMode { get; set; }

        /// <summary>
        /// Gets or sets whether to remember view per folder.
        /// </summary>
        bool RememberViewPerFolder { get; set; }

        /// <summary>
        /// Event raised when view mode changes.
        /// </summary>
        event EventHandler<ViewModeChangedEventArgs>? ViewModeChanged;

        /// <summary>
        /// Gets all available view modes.
        /// </summary>
        IReadOnlyList<ViewModeInfo> GetAvailableViewModes();

        /// <summary>
        /// Gets the view mode for a specific folder.
        /// </summary>
        /// <param name="folderPath">The folder path.</param>
        /// <returns>The view mode for this folder.</returns>
        ViewMode GetViewModeForFolder(string folderPath);

        /// <summary>
        /// Sets the view mode for a specific folder.
        /// </summary>
        /// <param name="folderPath">The folder path.</param>
        /// <param name="viewMode">The view mode to set.</param>
        void SetViewModeForFolder(string folderPath, ViewMode viewMode);

        /// <summary>
        /// Clears the view mode for a specific folder (use default).
        /// </summary>
        /// <param name="folderPath">The folder path.</param>
        void ClearViewModeForFolder(string folderPath);

        /// <summary>
        /// Gets the icon size for the current view mode.
        /// </summary>
        int GetIconSize();

        /// <summary>
        /// Sets custom icon size for icon-based views.
        /// </summary>
        /// <param name="size">The icon size in pixels.</param>
        void SetIconSize(int size);

        /// <summary>
        /// Gets thumbnail size for thumbnail view.
        /// </summary>
        int GetThumbnailSize();

        /// <summary>
        /// Sets thumbnail size for thumbnail view.
        /// </summary>
        /// <param name="size">The thumbnail size in pixels.</param>
        void SetThumbnailSize(int size);

        /// <summary>
        /// Saves view mode preferences.
        /// </summary>
        Task SavePreferencesAsync();

        /// <summary>
        /// Loads view mode preferences.
        /// </summary>
        Task LoadPreferencesAsync();
    }

    /// <summary>
    /// Available view modes.
    /// </summary>
    public enum ViewMode
    {
        /// <summary>Details view with columns.</summary>
        Details,

        /// <summary>Compact list view.</summary>
        List,

        /// <summary>Small icons (16x16).</summary>
        SmallIcons,

        /// <summary>Medium icons (48x48).</summary>
        MediumIcons,

        /// <summary>Large icons (96x96).</summary>
        LargeIcons,

        /// <summary>Extra large icons (256x256).</summary>
        ExtraLargeIcons,

        /// <summary>Tiles view with icons and details.</summary>
        Tiles,

        /// <summary>Content view with preview.</summary>
        Content,

        /// <summary>Thumbnail grid for images.</summary>
        Thumbnails,

        /// <summary>Column view (macOS Miller columns style).</summary>
        Columns
    }

    /// <summary>
    /// Information about a view mode.
    /// </summary>
    public class ViewModeInfo
    {
        /// <summary>Gets or sets the view mode.</summary>
        public ViewMode Mode { get; set; }

        /// <summary>Gets or sets the display name.</summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>Gets or sets the icon identifier.</summary>
        public string Icon { get; set; } = string.Empty;

        /// <summary>Gets or sets the keyboard shortcut.</summary>
        public string? KeyboardShortcut { get; set; }

        /// <summary>Gets or sets the description.</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>Gets or sets the default icon size.</summary>
        public int DefaultIconSize { get; set; }

        /// <summary>Gets or sets whether this is icon-based.</summary>
        public bool IsIconBased { get; set; }

        /// <summary>Gets or sets whether this supports thumbnails.</summary>
        public bool SupportsThumbnails { get; set; }
    }

    /// <summary>
    /// Event args for view mode changes.
    /// </summary>
    public class ViewModeChangedEventArgs : EventArgs
    {
        /// <summary>Gets or sets the old view mode.</summary>
        public ViewMode OldMode { get; set; }

        /// <summary>Gets or sets the new view mode.</summary>
        public ViewMode NewMode { get; set; }

        /// <summary>Gets or sets the folder path (if folder-specific).</summary>
        public string? FolderPath { get; set; }
    }
}
