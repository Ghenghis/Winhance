using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Winhance.Core.Features.FileManager.Interfaces
{
    /// <summary>
    /// Service for managing favorites, recent locations, and frequently accessed paths.
    /// </summary>
    public interface IFavoritesService
    {
        /// <summary>
        /// Observable collection of favorite items.
        /// </summary>
        ObservableCollection<FavoriteItem> Favorites { get; }

        /// <summary>
        /// Add a path to favorites.
        /// </summary>
        /// <param name="path">Path to add.</param>
        /// <param name="customName">Optional custom display name.</param>
        /// <param name="group">Optional group name.</param>
        /// <returns>The created favorite item.</returns>
        FavoriteItem AddFavorite(string path, string? customName = null, string? group = null);

        /// <summary>
        /// Remove a favorite.
        /// </summary>
        /// <param name="item">Favorite item to remove.</param>
        void RemoveFavorite(FavoriteItem item);

        /// <summary>
        /// Update a favorite's properties.
        /// </summary>
        /// <param name="item">Favorite item to update.</param>
        void UpdateFavorite(FavoriteItem item);

        /// <summary>
        /// Reorder favorites.
        /// </summary>
        /// <param name="item">Item to move.</param>
        /// <param name="newIndex">New index position.</param>
        void MoveFavorite(FavoriteItem item, int newIndex);

        /// <summary>
        /// Get recent locations.
        /// </summary>
        /// <param name="count">Maximum number of locations to return.</param>
        /// <returns>Collection of recent locations.</returns>
        IEnumerable<RecentLocation> GetRecentLocations(int count = 20);

        /// <summary>
        /// Get frequently accessed locations.
        /// </summary>
        /// <param name="count">Maximum number of locations to return.</param>
        /// <returns>Collection of frequent locations.</returns>
        IEnumerable<FrequentLocation> GetFrequentLocations(int count = 10);

        /// <summary>
        /// Clear recent history.
        /// </summary>
        void ClearRecentHistory();

        /// <summary>
        /// Record a location visit (for tracking).
        /// </summary>
        /// <param name="path">Path that was visited.</param>
        void RecordVisit(string path);

        /// <summary>
        /// Check if path is a favorite.
        /// </summary>
        /// <param name="path">Path to check.</param>
        /// <returns>True if path is in favorites.</returns>
        bool IsFavorite(string path);

        /// <summary>
        /// Get favorite groups.
        /// </summary>
        /// <returns>Collection of group names.</returns>
        IEnumerable<string> GetGroups();

        /// <summary>
        /// Create a new group.
        /// </summary>
        /// <param name="name">Group name.</param>
        void CreateGroup(string name);

        /// <summary>
        /// Delete a group (moves items to ungrouped).
        /// </summary>
        /// <param name="name">Group name to delete.</param>
        void DeleteGroup(string name);

        /// <summary>
        /// Save favorites to persistent storage.
        /// </summary>
        Task SaveAsync();

        /// <summary>
        /// Load favorites from persistent storage.
        /// </summary>
        Task LoadAsync();

        /// <summary>
        /// Get favorites by group.
        /// </summary>
        /// <param name="group">Group name (null for ungrouped).</param>
        /// <returns>Favorites in the specified group.</returns>
        IEnumerable<FavoriteItem> GetFavoritesByGroup(string? group = null);

        /// <summary>
        /// Get favorite by path.
        /// </summary>
        /// <param name="path">Path to search for.</param>
        /// <returns>Favorite item or null if not found.</returns>
        FavoriteItem? GetFavoriteByPath(string path);
    }

    /// <summary>
    /// Represents a favorite location.
    /// </summary>
    public class FavoriteItem : ObservableObject
    {
        private string _name = string.Empty;
        private string _path = string.Empty;
        private string? _group;
        private int _sortOrder;
        private string _iconPath = string.Empty;

        /// <summary>
        /// Unique identifier.
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Display name.
        /// </summary>
        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        /// <summary>
        /// Full path to the location.
        /// </summary>
        public string Path
        {
            get => _path;
            set => SetProperty(ref _path, value);
        }

        /// <summary>
        /// Optional group name.
        /// </summary>
        public string? Group
        {
            get => _group;
            set => SetProperty(ref _group, value);
        }

        /// <summary>
        /// Sort order within the group.
        /// </summary>
        public int SortOrder
        {
            get => _sortOrder;
            set => SetProperty(ref _sortOrder, value);
        }

        /// <summary>
        /// When the favorite was added.
        /// </summary>
        public DateTime AddedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Path to icon image.
        /// </summary>
        public string IconPath
        {
            get => _iconPath;
            set => SetProperty(ref _iconPath, value);
        }

        /// <summary>
        /// Whether the path still exists.
        /// </summary>
        public bool IsValid => System.IO.Directory.Exists(Path) || System.IO.File.Exists(Path);

        /// <summary>
        /// Whether this is a system location.
        /// </summary>
        public bool IsSystemLocation => IsSystemPath(Path);

        /// <summary>
        /// Type of location (file, directory, drive).
        /// </summary>
        public FavoriteLocationType LocationType
        {
            get
            {
                if (System.IO.Directory.Exists(Path))
                {
                    return Path.Length == 3 && Path[1] == ':' ? FavoriteLocationType.Drive : FavoriteLocationType.Directory;
                }
                return System.IO.File.Exists(Path) ? FavoriteLocationType.File : FavoriteLocationType.Unknown;
            }
        }

        private static bool IsSystemPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;

            var systemPaths = new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                Environment.GetFolderPath(Environment.SpecialFolder.System),
                Environment.GetFolderPath(Environment.SpecialFolder.SystemX86)
            };

            return systemPaths.Any(sysPath => 
                path.Equals(sysPath, StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith(sysPath + "\\", StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>
    /// Type of favorite location.
    /// </summary>
    public enum FavoriteLocationType
    {
        Unknown,
        File,
        Directory,
        Drive
    }

    /// <summary>
    /// Represents a recently accessed location.
    /// </summary>
    public class RecentLocation
    {
        /// <summary>
        /// Full path.
        /// </summary>
        public string Path { get; set; } = string.Empty;

        /// <summary>
        /// Display name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// When it was last visited.
        /// </summary>
        public DateTime LastVisited { get; set; }

        /// <summary>
        /// Number of times accessed.
        /// </summary>
        public int VisitCount { get; set; } = 1;

        /// <summary>
        /// Whether the path still exists.
        /// </summary>
        public bool IsValid => System.IO.Directory.Exists(Path) || System.IO.File.Exists(Path);
    }

    /// <summary>
    /// Represents a frequently accessed location.
    /// </summary>
    public class FrequentLocation
    {
        /// <summary>
        /// Full path.
        /// </summary>
        public string Path { get; set; } = string.Empty;

        /// <summary>
        /// Display name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Number of visits.
        /// </summary>
        public int VisitCount { get; set; }

        /// <summary>
        /// When it was last visited.
        /// </summary>
        public DateTime LastVisited { get; set; }

        /// <summary>
        /// Access frequency score (visits per day).
        /// </summary>
        public double FrequencyScore
        {
            get
            {
                var daysSinceFirst = Math.Max(1, (DateTime.UtcNow - LastVisited).TotalDays);
                return VisitCount / daysSinceFirst;
            }
        }

        /// <summary>
        /// Whether the path still exists.
        /// </summary>
        public bool IsValid => System.IO.Directory.Exists(Path) || System.IO.File.Exists(Path);
    }

    /// <summary>
    /// Statistics about favorite usage.
    /// </summary>
    public class FavoriteStatistics
    {
        /// <summary>
        /// Total number of favorites.
        /// </summary>
        public int TotalFavorites { get; set; }

        /// <summary>
        /// Number of groups.
        /// </summary>
        public int GroupCount { get; set; }

        /// <summary>
        /// Number of invalid/missing favorites.
        /// </summary>
        public int InvalidFavorites { get; set; }

        /// <summary>
        /// Most accessed favorite.
        /// </summary>
        public FavoriteItem? MostAccessed { get; set; }

        /// <summary>
        /// Recently added favorites.
        /// </summary>
        public List<FavoriteItem> RecentlyAdded { get; set; } = new();
    }
}
