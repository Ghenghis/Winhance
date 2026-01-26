using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Winhance.Core.Features.FileManager.Interfaces;

namespace Winhance.Infrastructure.Features.FileManager.Services
{
    /// <summary>
    /// Service for managing view modes in the file browser.
    /// </summary>
    public class ViewModeService : IViewModeService
    {
        private readonly string _preferencesPath;
        private readonly Dictionary<string, ViewMode> _folderViewModes = new();
        private readonly List<ViewModeInfo> _availableViewModes;

        private ViewMode _currentViewMode = ViewMode.Details;
        private ViewMode _defaultViewMode = ViewMode.Details;
        private bool _rememberViewPerFolder = true;
        private int _iconSize = 48;
        private int _thumbnailSize = 128;

        /// <inheritdoc/>
        public ViewMode CurrentViewMode
        {
            get => _currentViewMode;
            set
            {
                if (_currentViewMode != value)
                {
                    var oldMode = _currentViewMode;
                    _currentViewMode = value;
                    ViewModeChanged?.Invoke(this, new ViewModeChangedEventArgs
                    {
                        OldMode = oldMode,
                        NewMode = value
                    });
                }
            }
        }

        /// <inheritdoc/>
        public ViewMode DefaultViewMode
        {
            get => _defaultViewMode;
            set => _defaultViewMode = value;
        }

        /// <inheritdoc/>
        public bool RememberViewPerFolder
        {
            get => _rememberViewPerFolder;
            set => _rememberViewPerFolder = value;
        }

        /// <inheritdoc/>
        public event EventHandler<ViewModeChangedEventArgs>? ViewModeChanged;

        /// <summary>
        /// Initializes a new instance of the ViewModeService.
        /// </summary>
        public ViewModeService()
        {
            _preferencesPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Winhance", "FileManager", "viewmode-preferences.json");

            _availableViewModes = new List<ViewModeInfo>
            {
                new ViewModeInfo
                {
                    Mode = ViewMode.Details,
                    DisplayName = "Details",
                    Icon = "ViewDetails",
                    KeyboardShortcut = "Ctrl+Shift+6",
                    Description = "Shows files in a list with columns for details",
                    DefaultIconSize = 16,
                    IsIconBased = false,
                    SupportsThumbnails = false
                },
                new ViewModeInfo
                {
                    Mode = ViewMode.List,
                    DisplayName = "List",
                    Icon = "ViewList",
                    KeyboardShortcut = "Ctrl+Shift+5",
                    Description = "Shows files in a compact list",
                    DefaultIconSize = 16,
                    IsIconBased = false,
                    SupportsThumbnails = false
                },
                new ViewModeInfo
                {
                    Mode = ViewMode.SmallIcons,
                    DisplayName = "Small Icons",
                    Icon = "ViewSmallIcons",
                    KeyboardShortcut = "Ctrl+Shift+1",
                    Description = "Shows files with small icons",
                    DefaultIconSize = 16,
                    IsIconBased = true,
                    SupportsThumbnails = false
                },
                new ViewModeInfo
                {
                    Mode = ViewMode.MediumIcons,
                    DisplayName = "Medium Icons",
                    Icon = "ViewMediumIcons",
                    KeyboardShortcut = "Ctrl+Shift+2",
                    Description = "Shows files with medium-sized icons",
                    DefaultIconSize = 48,
                    IsIconBased = true,
                    SupportsThumbnails = true
                },
                new ViewModeInfo
                {
                    Mode = ViewMode.LargeIcons,
                    DisplayName = "Large Icons",
                    Icon = "ViewLargeIcons",
                    KeyboardShortcut = "Ctrl+Shift+3",
                    Description = "Shows files with large icons",
                    DefaultIconSize = 96,
                    IsIconBased = true,
                    SupportsThumbnails = true
                },
                new ViewModeInfo
                {
                    Mode = ViewMode.ExtraLargeIcons,
                    DisplayName = "Extra Large Icons",
                    Icon = "ViewExtraLargeIcons",
                    KeyboardShortcut = "Ctrl+Shift+4",
                    Description = "Shows files with extra large icons",
                    DefaultIconSize = 256,
                    IsIconBased = true,
                    SupportsThumbnails = true
                },
                new ViewModeInfo
                {
                    Mode = ViewMode.Tiles,
                    DisplayName = "Tiles",
                    Icon = "ViewTiles",
                    KeyboardShortcut = "Ctrl+Shift+7",
                    Description = "Shows files as tiles with icons and details",
                    DefaultIconSize = 48,
                    IsIconBased = true,
                    SupportsThumbnails = true
                },
                new ViewModeInfo
                {
                    Mode = ViewMode.Content,
                    DisplayName = "Content",
                    Icon = "ViewContent",
                    KeyboardShortcut = "Ctrl+Shift+8",
                    Description = "Shows files with preview and metadata",
                    DefaultIconSize = 48,
                    IsIconBased = true,
                    SupportsThumbnails = true
                },
                new ViewModeInfo
                {
                    Mode = ViewMode.Thumbnails,
                    DisplayName = "Thumbnails",
                    Icon = "ViewThumbnails",
                    KeyboardShortcut = "Ctrl+Shift+9",
                    Description = "Shows image thumbnails in a grid",
                    DefaultIconSize = 128,
                    IsIconBased = true,
                    SupportsThumbnails = true
                },
                new ViewModeInfo
                {
                    Mode = ViewMode.Columns,
                    DisplayName = "Columns",
                    Icon = "ViewColumns",
                    KeyboardShortcut = "Ctrl+Shift+0",
                    Description = "Miller columns view for hierarchical navigation",
                    DefaultIconSize = 16,
                    IsIconBased = false,
                    SupportsThumbnails = false
                }
            };
        }

        /// <inheritdoc/>
        public IReadOnlyList<ViewModeInfo> GetAvailableViewModes() => _availableViewModes.AsReadOnly();

        /// <inheritdoc/>
        public ViewMode GetViewModeForFolder(string folderPath)
        {
            if (!_rememberViewPerFolder)
                return _defaultViewMode;

            var normalizedPath = NormalizePath(folderPath);
            return _folderViewModes.TryGetValue(normalizedPath, out var mode) ? mode : _defaultViewMode;
        }

        /// <inheritdoc/>
        public void SetViewModeForFolder(string folderPath, ViewMode viewMode)
        {
            var normalizedPath = NormalizePath(folderPath);
            _folderViewModes[normalizedPath] = viewMode;

            ViewModeChanged?.Invoke(this, new ViewModeChangedEventArgs
            {
                OldMode = CurrentViewMode,
                NewMode = viewMode,
                FolderPath = folderPath
            });
        }

        /// <inheritdoc/>
        public void ClearViewModeForFolder(string folderPath)
        {
            var normalizedPath = NormalizePath(folderPath);
            _folderViewModes.Remove(normalizedPath);
        }

        /// <inheritdoc/>
        public int GetIconSize()
        {
            var info = _availableViewModes.Find(v => v.Mode == CurrentViewMode);
            return info?.IsIconBased == true ? _iconSize : info?.DefaultIconSize ?? 16;
        }

        /// <inheritdoc/>
        public void SetIconSize(int size)
        {
            _iconSize = Math.Clamp(size, 16, 512);
        }

        /// <inheritdoc/>
        public int GetThumbnailSize() => _thumbnailSize;

        /// <inheritdoc/>
        public void SetThumbnailSize(int size)
        {
            _thumbnailSize = Math.Clamp(size, 64, 512);
        }

        /// <inheritdoc/>
        public async Task SavePreferencesAsync()
        {
            try
            {
                var dir = Path.GetDirectoryName(_preferencesPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var preferences = new ViewModePreferences
                {
                    DefaultViewMode = _defaultViewMode,
                    CurrentViewMode = _currentViewMode,
                    RememberViewPerFolder = _rememberViewPerFolder,
                    IconSize = _iconSize,
                    ThumbnailSize = _thumbnailSize,
                    FolderViewModes = new Dictionary<string, ViewMode>(_folderViewModes)
                };

                var json = JsonSerializer.Serialize(preferences, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(_preferencesPath, json);
            }
            catch
            {
                // Log error but don't throw
            }
        }

        /// <inheritdoc/>
        public async Task LoadPreferencesAsync()
        {
            try
            {
                if (!File.Exists(_preferencesPath))
                    return;

                var json = await File.ReadAllTextAsync(_preferencesPath);
                var preferences = JsonSerializer.Deserialize<ViewModePreferences>(json);

                if (preferences != null)
                {
                    _defaultViewMode = preferences.DefaultViewMode;
                    _currentViewMode = preferences.CurrentViewMode;
                    _rememberViewPerFolder = preferences.RememberViewPerFolder;
                    _iconSize = preferences.IconSize;
                    _thumbnailSize = preferences.ThumbnailSize;

                    _folderViewModes.Clear();
                    if (preferences.FolderViewModes != null)
                    {
                        foreach (var kvp in preferences.FolderViewModes)
                            _folderViewModes[kvp.Key] = kvp.Value;
                    }
                }
            }
            catch
            {
                // Log error but don't throw
            }
        }

        private static string NormalizePath(string path)
        {
            return path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).ToLowerInvariant();
        }

        private class ViewModePreferences
        {
            public ViewMode DefaultViewMode { get; set; }
            public ViewMode CurrentViewMode { get; set; }
            public bool RememberViewPerFolder { get; set; }
            public int IconSize { get; set; }
            public int ThumbnailSize { get; set; }
            public Dictionary<string, ViewMode>? FolderViewModes { get; set; }
        }
    }
}
