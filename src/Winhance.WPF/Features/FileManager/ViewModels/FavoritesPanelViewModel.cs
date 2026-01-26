using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Winhance.WPF.Features.FileManager.ViewModels
{
    /// <summary>
    /// ViewModel for the Favorites/Quick Access panel.
    /// Provides favorites management, recent locations, and frequent locations.
    /// </summary>
    public partial class FavoritesPanelViewModel : ObservableObject
    {
        private readonly string _favoritesFilePath;
        private readonly ObservableCollection<RecentLocationItem> _recentLocations = new();

        [ObservableProperty]
        private ObservableCollection<FavoriteItem> _favorites = new();

        [ObservableProperty]
        private ObservableCollection<FavoriteGroupViewModel> _groups = new();

        [ObservableProperty]
        private FavoriteItem? _selectedFavorite;

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        [ObservableProperty]
        private bool _showRecent = true;

        [ObservableProperty]
        private bool _showFrequent = true;

        public ObservableCollection<RecentLocationItem> RecentLocations => _recentLocations;

        public IEnumerable<FrequentLocationItem> FrequentLocations =>
            _recentLocations
                .GroupBy(r => r.Path)
                .Select(g => new FrequentLocationItem
                {
                    Path = g.Key,
                    Name = g.First().Name,
                    VisitCount = g.Count(),
                    LastVisited = g.Max(r => r.LastVisited),
                })
                .OrderByDescending(f => f.VisitCount)
                .Take(10);

        public event EventHandler<string>? NavigateToPathRequested;

        public FavoritesPanelViewModel()
        {
            _favoritesFilePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Winhance",
                "favorites.json");

            InitializeDefaultFavorites();
            _ = LoadAsync();
        }

        private void InitializeDefaultFavorites()
        {
            // Add default quick access locations
            var defaultLocations = new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                Environment.GetFolderPath(Environment.SpecialFolder.MyMusic),
                Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"),
            };

            foreach (var path in defaultLocations)
            {
                if (Directory.Exists(path) && !Favorites.Any(f => f.Path.Equals(path, StringComparison.OrdinalIgnoreCase)))
                {
                    Favorites.Add(new FavoriteItem
                    {
                        Id = Guid.NewGuid().ToString(),
                        Path = path,
                        Name = Path.GetFileName(path),
                        IsDefault = true,
                        AddedAt = DateTime.UtcNow,
                    });
                }
            }

            // Add drives
            foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady))
            {
                Favorites.Add(new FavoriteItem
                {
                    Id = Guid.NewGuid().ToString(),
                    Path = drive.Name,
                    Name = string.IsNullOrEmpty(drive.VolumeLabel)
                        ? $"Local Disk ({drive.Name.TrimEnd('\\')})"
                        : $"{drive.VolumeLabel} ({drive.Name.TrimEnd('\\')})",
                    Group = "Drives",
                    IsDefault = true,
                    AddedAt = DateTime.UtcNow,
                });
            }

            UpdateGroups();
        }

        [RelayCommand]
        public void AddToFavorites(string path)
        {
            AddFavorite(path);
        }

        public FavoriteItem AddFavorite(string path, string? customName = null, string? group = null)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentException("Path cannot be empty", nameof(path));
            }

            // Check if already exists
            var existing = Favorites.FirstOrDefault(f => f.Path.Equals(path, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                return existing;
            }

            var item = new FavoriteItem
            {
                Id = Guid.NewGuid().ToString(),
                Path = path,
                Name = customName ?? Path.GetFileName(path.TrimEnd('\\', '/')),
                Group = group,
                AddedAt = DateTime.UtcNow,
            };

            if (string.IsNullOrEmpty(item.Name))
            {
                item.Name = path;
            }

            Favorites.Add(item);
            UpdateGroups();
            _ = SaveAsync();

            StatusMessage = $"Added '{item.Name}' to favorites";
            return item;
        }

        [RelayCommand]
        public void RemoveFavorite(FavoriteItem? item)
        {
            if (item == null || item.IsDefault)
            {
                return;
            }

            Favorites.Remove(item);
            UpdateGroups();
            _ = SaveAsync();

            StatusMessage = $"Removed '{item.Name}' from favorites";
        }

        [RelayCommand]
        public void RenameFavorite(FavoriteItem? item)
        {
            // This would typically show a dialog - for now just mark as edited
            if (item == null)
            {
                return;
            }

            item.IsEditing = true;
        }

        [RelayCommand]
        public void SaveRename(FavoriteItem? item)
        {
            if (item == null)
            {
                return;
            }

            item.IsEditing = false;
            _ = SaveAsync();
        }

        public void MoveFavorite(FavoriteItem? item, int newIndex)
        {
            if (item == null || newIndex < 0 || newIndex >= Favorites.Count)
            {
                return;
            }

            var currentIndex = Favorites.IndexOf(item);
            if (currentIndex == newIndex)
            {
                return;
            }

            Favorites.Move(currentIndex, newIndex);

            // Update sort orders
            for (int i = 0; i < Favorites.Count; i++)
            {
                Favorites[i].SortOrder = i;
            }

            _ = SaveAsync();
        }

        [RelayCommand]
        public void NavigateToFavorite(FavoriteItem? item)
        {
            if (item == null)
            {
                return;
            }

            if (!item.IsValid)
            {
                StatusMessage = $"Location '{item.Name}' no longer exists";
                return;
            }

            RecordVisit(item.Path);
            NavigateToPathRequested?.Invoke(this, item.Path);
        }

        [RelayCommand]
        public void NavigateToRecent(RecentLocationItem? item)
        {
            if (item == null || !Directory.Exists(item.Path))
            {
                return;
            }

            RecordVisit(item.Path);
            NavigateToPathRequested?.Invoke(this, item.Path);
        }

        [RelayCommand]
        public void NavigateToFrequent(FrequentLocationItem? item)
        {
            if (item == null || !Directory.Exists(item.Path))
            {
                return;
            }

            RecordVisit(item.Path);
            NavigateToPathRequested?.Invoke(this, item.Path);
        }

        public void RecordVisit(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            var item = new RecentLocationItem
            {
                Path = path,
                Name = Path.GetFileName(path.TrimEnd('\\', '/')) ?? path,
                LastVisited = DateTime.UtcNow,
            };

            if (string.IsNullOrEmpty(item.Name))
            {
                item.Name = path;
            }

            _recentLocations.Insert(0, item);

            // Keep only last 50 entries
            while (_recentLocations.Count > 50)
            {
                _recentLocations.RemoveAt(_recentLocations.Count - 1);
            }

            OnPropertyChanged(nameof(FrequentLocations));
        }

        [RelayCommand]
        public void ClearRecentHistory()
        {
            _recentLocations.Clear();
            OnPropertyChanged(nameof(FrequentLocations));
            StatusMessage = "Recent history cleared";
        }

        public bool IsFavorite(string path)
        {
            return Favorites.Any(f => f.Path.Equals(path, StringComparison.OrdinalIgnoreCase));
        }

        [RelayCommand]
        public void ToggleFavorite(string path)
        {
            var existing = Favorites.FirstOrDefault(f => f.Path.Equals(path, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                RemoveFavorite(existing);
            }
            else
            {
                AddFavorite(path);
            }
        }

        [RelayCommand]
        public void CreateGroup(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return;
            }

            if (Groups.Any(g => g.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            {
                StatusMessage = $"Group '{name}' already exists";
                return;
            }

            Groups.Add(new FavoriteGroupViewModel { Name = name });
            StatusMessage = $"Created group '{name}'";
        }

        [RelayCommand]
        public void DeleteGroup(string name)
        {
            var group = Groups.FirstOrDefault(g => g.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (group == null)
            {
                return;
            }

            // Move items to ungrouped
            foreach (var item in Favorites.Where(f => f.Group == name))
            {
                item.Group = null;
            }

            Groups.Remove(group);
            UpdateGroups();
            _ = SaveAsync();

            StatusMessage = $"Deleted group '{name}'";
        }

        public void MoveToGroup(FavoriteItem? item, string? groupName)
        {
            if (item == null)
            {
                return;
            }

            item.Group = groupName;
            UpdateGroups();
            _ = SaveAsync();
        }

        private void UpdateGroups()
        {
            var groupNames = Favorites
                .Where(f => !string.IsNullOrEmpty(f.Group))
                .Select(f => f.Group!)
                .Distinct()
                .OrderBy(g => g);

            Groups.Clear();
            foreach (var name in groupNames)
            {
                Groups.Add(new FavoriteGroupViewModel
                {
                    Name = name,
                    Items = new ObservableCollection<FavoriteItem>(
                        Favorites.Where(f => f.Group == name).OrderBy(f => f.SortOrder)),
                });
            }
        }

        [RelayCommand]
        public async Task SaveAsync()
        {
            try
            {
                var directory = Path.GetDirectoryName(_favoritesFilePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var data = Favorites.Where(f => !f.IsDefault).Select(f => new
                {
                    f.Id,
                    f.Path,
                    f.Name,
                    f.Group,
                    f.SortOrder,
                    f.AddedAt,
                });

                var json = System.Text.Json.JsonSerializer.Serialize(data, new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true,
                });

                await File.WriteAllTextAsync(_favoritesFilePath, json);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Failed to save favorites: {ex.Message}";
            }
        }

        [RelayCommand]
        public async Task LoadAsync()
        {
            if (!File.Exists(_favoritesFilePath))
            {
                return;
            }

            IsLoading = true;

            try
            {
                var json = await File.ReadAllTextAsync(_favoritesFilePath);
                var items = System.Text.Json.JsonSerializer.Deserialize<List<FavoriteItemData>>(json);

                if (items != null)
                {
                    foreach (var data in items)
                    {
                        if (!Favorites.Any(f => f.Path.Equals(data.Path, StringComparison.OrdinalIgnoreCase)))
                        {
                            Favorites.Add(new FavoriteItem
                            {
                                Id = data.Id ?? Guid.NewGuid().ToString(),
                                Path = data.Path ?? string.Empty,
                                Name = data.Name ?? Path.GetFileName(data.Path ?? string.Empty),
                                Group = data.Group,
                                SortOrder = data.SortOrder,
                                AddedAt = data.AddedAt,
                            });
                        }
                    }

                    UpdateGroups();
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Failed to load favorites: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private class FavoriteItemData
        {
            public string? Id { get; set; }
            public string? Path { get; set; }
            public string? Name { get; set; }
            public string? Group { get; set; }
            public int SortOrder { get; set; }
            public DateTime AddedAt { get; set; }
        }
    }

    /// <summary>
    /// Represents a favorite location item.
    /// </summary>
    public partial class FavoriteItem : ObservableObject
    {
        [ObservableProperty]
        private string _id = string.Empty;

        [ObservableProperty]
        private string _path = string.Empty;

        [ObservableProperty]
        private string _name = string.Empty;

        [ObservableProperty]
        private string? _group;

        [ObservableProperty]
        private int _sortOrder;

        [ObservableProperty]
        private DateTime _addedAt = DateTime.UtcNow;

        [ObservableProperty]
        private bool _isDefault;

        [ObservableProperty]
        private bool _isEditing;

        public bool IsValid => Directory.Exists(Path) || File.Exists(Path);

        public bool IsDrive => Path.Length <= 3 && Path.Contains(":\\");
    }

    /// <summary>
    /// Represents a group of favorites.
    /// </summary>
    public partial class FavoriteGroupViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _name = string.Empty;

        [ObservableProperty]
        private bool _isExpanded = true;

        [ObservableProperty]
        private ObservableCollection<FavoriteItem> _items = new();
    }

    /// <summary>
    /// Represents a recently visited location.
    /// </summary>
    public class RecentLocationItem
    {
        public string Path { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public DateTime LastVisited { get; set; }
    }

    /// <summary>
    /// Represents a frequently visited location.
    /// </summary>
    public class FrequentLocationItem
    {
        public string Path { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int VisitCount { get; set; }
        public DateTime LastVisited { get; set; }
    }
}
