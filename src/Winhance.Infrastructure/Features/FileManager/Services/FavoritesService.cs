using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.FileManager.Interfaces;

namespace Winhance.Infrastructure.Features.FileManager.Services
{
    /// <summary>
    /// Service for managing favorites, recent locations, and frequently accessed paths.
    /// </summary>
    public class FavoritesService : IFavoritesService
    {
        private readonly ILogService _logService;
        private readonly ConcurrentDictionary<string, VisitData> _visitHistory = new();
        private readonly string _favoritesPath;
        private readonly string _historyPath;
        private const int MaxRecentLocations = 50;
        private const int MaxFrequentLocations = 20;
        private const string FavoritesFile = "favorites.json";
        private const string HistoryFile = "history.json";

        public FavoritesService(ILogService logService)
        {
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
            
            var appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Winhance");
            
            _favoritesPath = Path.Combine(appDataPath, FavoritesFile);
            _historyPath = Path.Combine(appDataPath, HistoryFile);

            Directory.CreateDirectory(appDataPath);
        }

        public ObservableCollection<FavoriteItem> Favorites { get; } = new();

        public FavoriteItem AddFavorite(string path, string? customName = null, string? group = null)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("Path cannot be empty", nameof(path));
            }

            // Check if already exists
            var existing = GetFavoriteByPath(path);
            if (existing != null)
            {
                _logService.Log(LogLevel.Warning, $"Path already in favorites: {path}");
                return existing;
            }

            // Create favorite item
            var favorite = new FavoriteItem
            {
                Path = path,
                Name = customName ?? GetDisplayName(path),
                Group = group,
                SortOrder = Favorites.Count,
                IconPath = GetIconPath(path)
            };

            Favorites.Add(favorite);
            _logService.Log(LogLevel.Info, $"Added favorite: {favorite.Name} ({path})");

            // Save asynchronously
            _ = Task.Run(SaveAsync);
            
            return favorite;
        }

        public void RemoveFavorite(FavoriteItem item)
        {
            if (item == null || !Favorites.Contains(item))
            {
                return;
            }

            Favorites.Remove(item);
            _logService.Log(LogLevel.Info, $"Removed favorite: {item.Name}");

            // Reorder remaining items in the same group
            var groupItems = Favorites.Where(f => f.Group == item.Group).OrderBy(f => f.SortOrder);
            int order = 0;
            foreach (var fav in groupItems)
            {
                fav.SortOrder = order++;
            }

            // Save asynchronously
            _ = Task.Run(SaveAsync);
        }

        public void UpdateFavorite(FavoriteItem item)
        {
            if (item == null)
            {
                return;
            }

            // Find and update
            var existing = Favorites.FirstOrDefault(f => f.Id == item.Id);
            if (existing != null)
            {
                existing.Name = item.Name;
                existing.Group = item.Group;
                existing.IconPath = item.IconPath;
                
                _logService.Log(LogLevel.Debug, $"Updated favorite: {existing.Name}");
                _ = Task.Run(SaveAsync);
            }
        }

        public void MoveFavorite(FavoriteItem item, int newIndex)
        {
            if (item == null || newIndex < 0 || newIndex >= Favorites.Count)
            {
                return;
            }

            var oldIndex = Favorites.IndexOf(item);
            if (oldIndex == newIndex)
            {
                return;
            }

            Favorites.RemoveAt(oldIndex);
            Favorites.Insert(newIndex, item);

            // Update sort order for all items in the same group
            var groupItems = Favorites.Where(f => f.Group == item.Group).ToList();
            for (int i = 0; i < groupItems.Count; i++)
            {
                groupItems[i].SortOrder = i;
            }

            _logService.Log(LogLevel.Debug, $"Moved favorite {item.Name} to position {newIndex}");
            _ = Task.Run(SaveAsync);
        }

        public IEnumerable<RecentLocation> GetRecentLocations(int count = 20)
        {
            return _visitHistory.Values
                .Select(v => new RecentLocation
                {
                    Path = v.Path,
                    Name = GetDisplayName(v.Path),
                    LastVisited = v.LastVisited,
                    VisitCount = v.VisitCount
                })
                .OrderByDescending(r => r.LastVisited)
                .Take(Math.Min(count, MaxRecentLocations));
        }

        public IEnumerable<FrequentLocation> GetFrequentLocations(int count = 10)
        {
            var cutoff = DateTime.UtcNow.AddDays(-30); // Last 30 days
            
            return _visitHistory.Values
                .Where(v => v.LastVisited >= cutoff)
                .Select(v => new FrequentLocation
                {
                    Path = v.Path,
                    Name = GetDisplayName(v.Path),
                    VisitCount = v.VisitCount,
                    LastVisited = v.LastVisited
                })
                .OrderByDescending(f => f.FrequencyScore)
                .ThenByDescending(f => f.LastVisited)
                .Take(Math.Min(count, MaxFrequentLocations));
        }

        public void ClearRecentHistory()
        {
            _visitHistory.Clear();
            _logService.Log(LogLevel.Info, "Cleared recent location history");
            _ = Task.Run(SaveHistoryAsync);
        }

        public void RecordVisit(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            var visitData = _visitHistory.GetOrAdd(path, _ => new VisitData { Path = path });
            visitData.VisitCount++;
            visitData.LastVisited = DateTime.UtcNow;

            // Limit history size
            if (_visitHistory.Count > 1000)
            {
                var oldest = _visitHistory.Values
                    .OrderBy(v => v.LastVisited)
                    .Take(100)
                    .Select(v => v.Path)
                    .ToList();

                foreach (var oldPath in oldest)
                {
                    _visitHistory.TryRemove(oldPath, out _);
                }
            }

            // Save history periodically (not on every visit)
            if (visitData.VisitCount % 10 == 0)
            {
                _ = Task.Run(SaveHistoryAsync);
            }
        }

        public bool IsFavorite(string path)
        {
            return Favorites.Any(f => 
                string.Equals(f.Path, path, StringComparison.OrdinalIgnoreCase));
        }

        public IEnumerable<string> GetGroups()
        {
            return Favorites
                .Where(f => !string.IsNullOrEmpty(f.Group))
                .Select(f => f.Group!)
                .Distinct()
                .OrderBy(g => g);
        }

        public void CreateGroup(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return;
            }

            // Groups are virtual - just ensure no items have this group yet
            if (!GetGroups().Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                _logService.Log(LogLevel.Info, $"Created group: {name}");
            }
        }

        public void DeleteGroup(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return;
            }

            // Move all items in the group to ungrouped
            var groupItems = Favorites.Where(f => f.Group == name).ToList();
            foreach (var item in groupItems)
            {
                item.Group = null;
            }

            _logService.Log(LogLevel.Info, $"Deleted group: {name}, moved {groupItems.Count} items to ungrouped");
            _ = Task.Run(SaveAsync);
        }

        public async Task SaveAsync()
        {
            try
            {
                var data = new FavoritesData
                {
                    Favorites = Favorites.Select(f => new SerializedFavorite
                    {
                        Id = f.Id,
                        Name = f.Name,
                        Path = f.Path,
                        Group = f.Group,
                        SortOrder = f.SortOrder,
                        IconPath = f.IconPath,
                        AddedAt = f.AddedAt
                    }).ToList()
                };

                var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(_favoritesPath, json);

                _logService.Log(LogLevel.Debug, $"Saved {Favorites.Count} favorites");
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Failed to save favorites: {ex.Message}");
            }
        }

        public async Task LoadAsync()
        {
            try
            {
                if (!File.Exists(_favoritesPath))
                {
                    // Add default favorites
                    AddDefaultFavorites();
                    return;
                }

                var json = await File.ReadAllTextAsync(_favoritesPath);
                var data = JsonSerializer.Deserialize<FavoritesData>(json);

                if (data?.Favorites != null)
                {
                    Favorites.Clear();
                    
                    foreach (var fav in data.Favorites)
                    {
                        Favorites.Add(new FavoriteItem
                        {
                            Id = fav.Id,
                            Name = fav.Name,
                            Path = fav.Path,
                            Group = fav.Group,
                            SortOrder = fav.SortOrder,
                            IconPath = fav.IconPath,
                            AddedAt = fav.AddedAt
                        });
                    }

                    _logService.Log(LogLevel.Info, $"Loaded {Favorites.Count} favorites");
                }

                // Load visit history
                await LoadHistoryAsync();
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Failed to load favorites: {ex.Message}");
                AddDefaultFavorites();
            }
        }

        public IEnumerable<FavoriteItem> GetFavoritesByGroup(string? group = null)
        {
            return Favorites
                .Where(f => f.Group == group)
                .OrderBy(f => f.SortOrder);
        }

        public FavoriteItem? GetFavoriteByPath(string path)
        {
            return Favorites.FirstOrDefault(f => 
                string.Equals(f.Path, path, StringComparison.OrdinalIgnoreCase));
        }

        private void AddDefaultFavorites()
        {
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var defaultPaths = new[]
            {
                (Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Desktop"),
                (Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Documents"),
                (Path.Combine(userProfile, "Downloads"), "Downloads"),
                (Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Pictures"),
                (Environment.GetFolderPath(Environment.SpecialFolder.MyMusic), "Music"),
                (Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "Videos")
            };

            foreach (var (path, name) in defaultPaths)
            {
                if (Directory.Exists(path))
                {
                    AddFavorite(path, name, "Quick Access");
                }
            }

            // Add drives
            foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady))
            {
                AddFavorite(drive.RootDirectory.FullName, drive.Name, "Drives");
            }
        }

        private static string GetDisplayName(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return "Unknown";
            }

            // Special folders
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var specialFolders = new[]
            {
                (userProfile, "Home"),
                (Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Desktop"),
                (Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Documents"),
                (Path.Combine(userProfile, "Downloads"), "Downloads"),
                (Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Pictures"),
                (Environment.GetFolderPath(Environment.SpecialFolder.MyMusic), "Music"),
                (Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "Videos")
            };

            foreach (var (folderPath, name) in specialFolders)
            {
                if (path.Equals(folderPath, StringComparison.OrdinalIgnoreCase))
                {
                    return name;
                }
            }

            // Drive letters
            if (path.Length == 3 && path[1] == ':' && path[2] == '\\')
            {
                try
                {
                    var drive = new DriveInfo(path[0].ToString());
                    return string.IsNullOrEmpty(drive.VolumeLabel) ? path[0].ToString() : drive.VolumeLabel;
                }
                catch
                {
                    return path[0].ToString();
                }
            }

            // Get folder or file name
            return Path.GetFileName(path.TrimEnd('\\')) ?? path;
        }

        private static string GetIconPath(string path)
        {
            // This would typically resolve to actual icon resources
            // For now, return empty string - UI will handle icon resolution
            return string.Empty;
        }

        private async Task SaveHistoryAsync()
        {
            try
            {
                var data = new HistoryData
                {
                    Visits = _visitHistory.Values.ToList()
                };

                var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(_historyPath, json);
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Warning, $"Failed to save history: {ex.Message}");
            }
        }

        private async Task LoadHistoryAsync()
        {
            try
            {
                if (!File.Exists(_historyPath))
                {
                    return;
                }

                var json = await File.ReadAllTextAsync(_historyPath);
                var data = JsonSerializer.Deserialize<HistoryData>(json);

                if (data?.Visits != null)
                {
                    _visitHistory.Clear();
                    foreach (var visit in data.Visits)
                    {
                        _visitHistory[visit.Path] = visit;
                    }

                    _logService.Log(LogLevel.Debug, $"Loaded history for {_visitHistory.Count} locations");
                }
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Warning, $"Failed to load history: {ex.Message}");
            }
        }

        private class VisitData
        {
            public string Path { get; set; } = string.Empty;
            public int VisitCount { get; set; }
            public DateTime LastVisited { get; set; }
        }

        private class FavoritesData
        {
            public List<SerializedFavorite> Favorites { get; set; } = new();
        }

        private class SerializedFavorite
        {
            public string Id { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public string Path { get; set; } = string.Empty;
            public string? Group { get; set; }
            public int SortOrder { get; set; }
            public string IconPath { get; set; } = string.Empty;
            public DateTime AddedAt { get; set; }
        }

        private class HistoryData
        {
            public List<VisitData> Visits { get; set; } = new();
        }
    }
}
