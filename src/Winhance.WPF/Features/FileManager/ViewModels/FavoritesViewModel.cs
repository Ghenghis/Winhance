using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Winhance.Core.Features.FileManager.Interfaces;

namespace Winhance.WPF.Features.FileManager.ViewModels
{
    /// <summary>
    /// ViewModel for managing favorites.
    /// </summary>
    public partial class FavoritesViewModel : ObservableObject
    {
        private readonly IFavoritesService _favoritesService;
        private readonly IAddressBarService _addressBarService;
        private readonly ITabService _tabService;

        [ObservableProperty]
        private ObservableCollection<FavoriteItemViewModel> _favorites = new();

        [ObservableProperty]
        private ObservableCollection<FavoriteGroupViewModel> _favoriteGroups = new();

        [ObservableProperty]
        private FavoriteItemViewModel? _selectedFavorite;

        [ObservableProperty]
        private FavoriteGroupViewModel? _selectedGroup;

        [ObservableProperty]
        private bool _isEditing;

        [ObservableProperty]
        private string _newFavoriteName = string.Empty;

        [ObservableProperty]
        private string _newFavoritePath = string.Empty;

        [ObservableProperty]
        private string _newGroupName = string.Empty;

        [ObservableProperty]
        private bool _showGroups = true;

        [ObservableProperty]
        private string _searchText = string.Empty;

        [ObservableProperty]
        private ObservableCollection<FavoriteItemViewModel> _filteredFavorites = new();

        public FavoritesViewModel(
            IFavoritesService favoritesService,
            IAddressBarService addressBarService,
            ITabService tabService)
        {
            _favoritesService = favoritesService;
            _addressBarService = addressBarService;
            _tabService = tabService;

            // Load favorites
            _ = LoadFavoritesAsync();
        }

        /// <summary>
        /// Loads all favorites from the service.
        /// </summary>
        [RelayCommand]
        public async Task LoadFavoritesAsync()
        {
            try
            {
                IsEditing = false;
                
                // Load favorite groups
                var groups = await _favoritesService.GetGroupsAsync();
                FavoriteGroups = new ObservableCollection<FavoriteGroupViewModel>(
                    groups.Select(g => new FavoriteGroupViewModel
                    {
                        Id = g.Id,
                        Name = g.Name,
                        Color = g.Color,
                        IsExpanded = g.IsExpanded
                    }));

                // Load favorites
                var favorites = await _favoritesService.GetFavoritesAsync();
                Favorites = new ObservableCollection<FavoriteItemViewModel>(
                    favorites.Select(f => new FavoriteItemViewModel
                    {
                        Id = f.Id,
                        Name = f.Name,
                        Path = f.Path,
                        GroupId = f.GroupId,
                        Icon = GetIconForPath(f.Path),
                        IsAccessible = Directory.Exists(f.Path),
                        DateAdded = f.DateAdded,
                        DateAccessed = f.DateAccessed,
                        AccessCount = f.AccessCount
                    }));

                // Apply filter
                ApplyFilter();
            }
            catch (Exception ex)
            {
                // Handle error
                System.Diagnostics.Debug.WriteLine($"Error loading favorites: {ex.Message}");
            }
        }

        /// <summary>
        /// Adds current directory to favorites.
        /// </summary>
        [RelayCommand]
        public async Task AddCurrentToFavoritesAsync(string currentPath)
        {
            if (string.IsNullOrEmpty(currentPath) || !Directory.Exists(currentPath))
                return;

            try
            {
                var favorite = new FavoriteItem
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = Path.GetFileName(currentPath) ?? currentPath,
                    Path = currentPath,
                    GroupId = SelectedGroup?.Id,
                    DateAdded = DateTime.Now,
                    DateAccessed = DateTime.Now,
                    AccessCount = 0
                };

                await _favoritesService.AddFavoriteAsync(favorite);
                await LoadFavoritesAsync();
                
                await _addressBarService.ShowMessageAsync("Added to favorites");
            }
            catch (Exception ex)
            {
                await _addressBarService.ShowErrorAsync($"Cannot add to favorites: {ex.Message}");
            }
        }

        /// <summary>
        /// Adds a new favorite with specified name and path.
        /// </summary>
        [RelayCommand]
        public async Task AddFavoriteAsync()
        {
            if (string.IsNullOrWhiteSpace(NewFavoriteName) || string.IsNullOrWhiteSpace(NewFavoritePath))
                return;

            if (!Directory.Exists(NewFavoritePath))
            {
                await _addressBarService.ShowErrorAsync("Path does not exist");
                return;
            }

            try
            {
                var favorite = new FavoriteItem
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = NewFavoriteName,
                    Path = NewFavoritePath,
                    GroupId = SelectedGroup?.Id,
                    DateAdded = DateTime.Now,
                    DateAccessed = DateTime.Now,
                    AccessCount = 0
                };

                await _favoritesService.AddFavoriteAsync(favorite);
                await LoadFavoritesAsync();
                
                NewFavoriteName = string.Empty;
                NewFavoritePath = string.Empty;
                IsEditing = false;
                
                await _addressBarService.ShowMessageAsync("Favorite added successfully");
            }
            catch (Exception ex)
            {
                await _addressBarService.ShowErrorAsync($"Cannot add favorite: {ex.Message}");
            }
        }

        /// <summary>
        /// Removes selected favorite.
        /// </summary>
        [RelayCommand]
        public async Task RemoveFavoriteAsync()
        {
            if (SelectedFavorite?.Id == null) return;

            try
            {
                await _favoritesService.RemoveFavoriteAsync(SelectedFavorite.Id);
                await LoadFavoritesAsync();
                
                await _addressBarService.ShowMessageAsync("Favorite removed");
            }
            catch (Exception ex)
            {
                await _addressBarService.ShowErrorAsync($"Cannot remove favorite: {ex.Message}");
            }
        }

        /// <summary>
        /// Navigates to selected favorite.
        /// </summary>
        [RelayCommand]
        public async Task NavigateToFavoriteAsync(FavoriteItemViewModel? favorite)
        {
            if (favorite?.Path == null) return;

            if (!Directory.Exists(favorite.Path))
            {
                await _addressBarService.ShowErrorAsync("Folder does not exist");
                return;
            }

            try
            {
                // Update access statistics
                await _favoritesService.UpdateAccessStatsAsync(favorite.Id);
                
                // Navigate
                await _addressBarService.NavigateAsync(favorite.Path);
                
                // Update local access count
                favorite.AccessCount++;
                favorite.DateAccessed = DateTime.Now;
            }
            catch (Exception ex)
            {
                await _addressBarService.ShowErrorAsync($"Cannot navigate to favorite: {ex.Message}");
            }
        }

        /// <summary>
        /// Opens favorite in new tab.
        /// </summary>
        [RelayCommand]
        public async Task OpenInNewTabAsync(FavoriteItemViewModel? favorite)
        {
            if (favorite?.Path == null) return;

            if (!Directory.Exists(favorite.Path))
            {
                await _addressBarService.ShowErrorAsync("Folder does not exist");
                return;
            }

            try
            {
                await _tabService.CreateTabAsync(favorite.Path);
                await _favoritesService.UpdateAccessStatsAsync(favorite.Id);
            }
            catch (Exception ex)
            {
                await _addressBarService.ShowErrorAsync($"Cannot open in new tab: {ex.Message}");
            }
        }

        /// <summary>
        /// Renames selected favorite.
        /// </summary>
        [RelayCommand]
        public async Task RenameFavoriteAsync()
        {
            if (SelectedFavorite == null) return;

            var newName = Microsoft.VisualBasic.Interaction.InputBox(
                "Enter new name for favorite:",
                "Rename Favorite",
                SelectedFavorite.Name);

            if (!string.IsNullOrWhiteSpace(newName) && newName != SelectedFavorite.Name)
            {
                try
                {
                    await _favoritesService.RenameFavoriteAsync(SelectedFavorite.Id, newName);
                    await LoadFavoritesAsync();
                    await _addressBarService.ShowMessageAsync("Favorite renamed");
                }
                catch (Exception ex)
                {
                    await _addressBarService.ShowErrorAsync($"Cannot rename favorite: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Creates a new favorite group.
        /// </summary>
        [RelayCommand]
        public async Task CreateGroupAsync()
        {
            if (string.IsNullOrWhiteSpace(NewGroupName))
                return;

            try
            {
                var group = new FavoriteGroup
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = NewGroupName,
                    Color = "#FF5722", // Default color
                    IsExpanded = true
                };

                await _favoritesService.AddGroupAsync(group);
                await LoadFavoritesAsync();
                
                NewGroupName = string.Empty;
                await _addressBarService.ShowMessageAsync("Group created");
            }
            catch (Exception ex)
            {
                await _addressBarService.ShowErrorAsync($"Cannot create group: {ex.Message}");
            }
        }

        /// <summary>
        /// Deletes selected group.
        /// </summary>
        [RelayCommand]
        public async Task DeleteGroupAsync()
        {
            if (SelectedGroup?.Id == null) return;

            try
            {
                var result = System.Windows.MessageBox.Show(
                    $"Delete group '{SelectedGroup.Name}' and all its favorites?",
                    "Confirm Delete",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Warning);

                if (result == System.Windows.MessageBoxResult.Yes)
                {
                    await _favoritesService.RemoveGroupAsync(SelectedGroup.Id);
                    await LoadFavoritesAsync();
                    
                    await _addressBarService.ShowMessageAsync("Group deleted");
                }
            }
            catch (Exception ex)
            {
                await _addressBarService.ShowErrorAsync($"Cannot delete group: {ex.Message}");
            }
        }

        /// <summary>
        /// Moves favorite to a different group.
        /// </summary>
        [RelayCommand]
        public async Task MoveToGroupAsync(FavoriteItemViewModel? favorite, FavoriteGroupViewModel? group)
        {
            if (favorite?.Id == null) return;

            try
            {
                await _favoritesService.MoveFavoriteToGroupAsync(favorite.Id, group?.Id);
                await LoadFavoritesAsync();
            }
            catch (Exception ex)
            {
                await _addressBarService.ShowErrorAsync($"Cannot move favorite: {ex.Message}");
            }
        }

        /// <summary>
        /// Shows context menu for favorite.
        /// </summary>
        [RelayCommand]
        public async Task ShowFavoriteContextMenuAsync(FavoriteItemViewModel? favorite)
        {
            if (favorite == null) return;

            var menu = new System.Text.StringBuilder();
            menu.AppendLine($"Favorite: {favorite.Name}");
            menu.AppendLine($"Path: {favorite.Path}");
            menu.AppendLine($"Added: {favorite.DateAdded:yyyy-MM-dd}");
            menu.AppendLine($"Last Accessed: {favorite.DateAccessed:yyyy-MM-dd}");
            menu.AppendLine($"Access Count: {favorite.AccessCount}");
            menu.AppendLine();
            menu.AppendLine("Available actions:");
            menu.AppendLine("• Double-click to open");
            menu.AppendLine("• Right-click → Open in New Tab");
            menu.AppendLine("• Right-click → Rename");
            menu.AppendLine("• Right-click → Remove");

            System.Windows.MessageBox.Show(
                menu.ToString(),
                "Favorite Properties",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
            await Task.CompletedTask;
        }

        /// <summary>
        /// Browses for folder to add as favorite.
        /// </summary>
        [RelayCommand]
        public void BrowseForFolder()
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "Select folder to add to favorites",
                ShowNewFolderButton = false
            };

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                NewFavoritePath = dialog.SelectedPath;
                if (string.IsNullOrWhiteSpace(NewFavoriteName))
                {
                    NewFavoriteName = Path.GetFileName(dialog.SelectedPath) ?? dialog.SelectedPath;
                }
            }
        }

        /// <summary>
        /// Toggles group view.
        /// </summary>
        [RelayCommand]
        public void ToggleGroupView()
        {
            ShowGroups = !ShowGroups;
        }

        /// <summary>
        /// Applies search filter to favorites.
        /// </summary>
        private void ApplyFilter()
        {
            if (string.IsNullOrWhiteSpace(SearchText))
            {
                FilteredFavorites = new ObservableCollection<FavoriteItemViewModel>(Favorites);
            }
            else
            {
                var filtered = Favorites.Where(f =>
                    f.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                    f.Path.Contains(SearchText, StringComparison.OrdinalIgnoreCase));

                FilteredFavorites = new ObservableCollection<FavoriteItemViewModel>(filtered);
            }
        }

        /// <summary>
        /// Gets icon for path.
        /// </summary>
        private static string GetIconForPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return "📁";

            return path.ToLowerInvariant() switch
            {
                var p when p.Contains("desktop") => "🖥️",
                var p when p.Contains("documents") => "📄",
                var p when p.Contains("downloads") => "⬇️",
                var p when p.Contains("pictures") or p.Contains("photos") => "🖼️",
                var p when p.Contains("music") => "🎵",
                var p when p.Contains("videos") => "🎬",
                var p when p.Contains("games") => "🎮",
                var p when p.Contains("projects") => "💼",
                var p when p.Contains("tools") or p.Contains("utilities") => "🛠️",
                _ => "📁"
            };
        }

        /// <summary>
        /// Handles property changes.
        /// </summary>
        partial void OnSearchTextChanged(string value)
        {
            ApplyFilter();
        }

        partial void OnShowGroupsChanged(bool value)
        {
            ApplyFilter();
        }
    }

    /// <summary>
    /// ViewModel for a favorite item.
    /// </summary>
    public partial class FavoriteItemViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _id = string.Empty;

        [ObservableProperty]
        private string _name = string.Empty;

        [ObservableProperty]
        private string _path = string.Empty;

        [ObservableProperty]
        private string? _groupId;

        [ObservableProperty]
        private string _icon = "📁";

        [ObservableProperty]
        private bool _isAccessible;

        [ObservableProperty]
        private DateTime _dateAdded;

        [ObservableProperty]
        private DateTime _dateAccessed;

        [ObservableProperty]
        private int _accessCount;

        public string DisplayPath => ShowFullPath ? Path : Path;
        public bool ShowFullPath { get; set; } = false;
        public string ToolTip => $"{Path}\nAdded: {DateAdded:yyyy-MM-dd}\nAccessed: {DateAccessed:yyyy-MM-dd}\nAccess count: {AccessCount}";
    }

    /// <summary>
    /// ViewModel for a favorite group.
    /// </summary>
    public partial class FavoriteGroupViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _id = string.Empty;

        [ObservableProperty]
        private string _name = string.Empty;

        [ObservableProperty]
        private string _color = "#FF5722";

        [ObservableProperty]
        private bool _isExpanded;

        [ObservableProperty]
        private int _favoriteCount;

        public string ColorBrush => Color;
    }
}
