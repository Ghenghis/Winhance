using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Winhance.Core.Features.FileManager.Interfaces;
using Winhance.Core.Features.FileManager.Models;

namespace Winhance.WPF.Features.FileManager.ViewModels
{
    /// <summary>
    /// ViewModel for favorites panel
    /// </summary>
    public partial class FavoritesPanelViewModel : ObservableObject
    {
        private readonly IFavoritesService _favoritesService;
        private ObservableCollection<FavoriteItem> _favorites = new();
        private ObservableCollection<FavoriteGroup> _groups = new();

        [ObservableProperty]
        private FavoriteItem? _selectedFavorite;

        [ObservableProperty]
        private bool _isEditing;

        [ObservableProperty]
        private string? _newFavoriteName;

        [ObservableProperty]
        private string? _newFavoritePath;

        public ObservableCollection<FavoriteItem> Favorites
        {
            get => _favorites;
            set => SetProperty(ref _favorites, value);
        }

        public ObservableCollection<FavoriteGroup> Groups
        {
            get => _groups;
            set => SetProperty(ref _groups, value);
        }

        public FavoritesPanelViewModel(IFavoritesService favoritesService)
        {
            _favoritesService = favoritesService;
            _ = LoadFavoritesAsync();
        }

        private async Task LoadFavoritesAsync()
        {
            try
            {
                var favorites = await _favoritesService.GetFavoritesAsync();
                Favorites.Clear();
                foreach (var favorite in favorites)
                {
                    Favorites.Add(favorite);
                }

                var groups = await _favoritesService.GetGroupsAsync();
                Groups.Clear();
                foreach (var group in groups)
                {
                    Groups.Add(group);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to load favorites: {ex.Message}",
                    "Load Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task AddFavoriteAsync()
        {
            if (string.IsNullOrEmpty(NewFavoriteName) || string.IsNullOrEmpty(NewFavoritePath)) return;

            try
            {
                var favorite = new FavoriteItem
                {
                    Name = NewFavoriteName,
                    Path = NewFavoritePath,
                    Icon = GetFolderIcon(NewFavoritePath),
                    CreatedDate = DateTime.Now
                };

                await _favoritesService.AddFavoriteAsync(favorite);
                Favorites.Add(favorite);

                NewFavoriteName = null;
                NewFavoritePath = null;
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to add favorite: {ex.Message}",
                    "Add Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task RemoveFavoriteAsync(FavoriteItem? favorite)
        {
            if (favorite == null) return;

            try
            {
                await _favoritesService.RemoveFavoriteAsync(favorite.Id);
                Favorites.Remove(favorite);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to remove favorite: {ex.Message}",
                    "Remove Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task UpdateFavoriteAsync(FavoriteItem? favorite)
        {
            if (favorite == null) return;

            try
            {
                await _favoritesService.UpdateFavoriteAsync(favorite);
                IsEditing = false;
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to update favorite: {ex.Message}",
                    "Update Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task NavigateToFavoriteAsync(FavoriteItem? favorite)
        {
            if (favorite == null) return;

            try
            {
                if (System.IO.Directory.Exists(favorite.Path))
                {
                    System.Diagnostics.Process.Start("explorer.exe", favorite.Path);
                    favorite.LastAccessed = DateTime.Now;
                    favorite.AccessCount++;
                    await _favoritesService.UpdateFavoriteAsync(favorite);
                }
                else
                {
                    System.Windows.MessageBox.Show(
                        $"Path not found: {favorite.Path}",
                        "Navigation Error",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to navigate: {ex.Message}",
                    "Navigation Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void StartEdit()
        {
            IsEditing = true;
        }

        [RelayCommand]
        private void CancelEdit()
        {
            IsEditing = false;
        }

        [RelayCommand]
        private async Task AddGroupAsync(string groupName)
        {
            if (string.IsNullOrEmpty(groupName)) return;

            try
            {
                var group = new FavoriteGroup
                {
                    Name = groupName,
                    CreatedDate = DateTime.Now
                };

                await _favoritesService.AddGroupAsync(group);
                Groups.Add(group);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to add group: {ex.Message}",
                    "Add Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task AddToGroupAsync(FavoriteItem? favorite, FavoriteGroup? group)
        {
            if (favorite == null || group == null) return;

            try
            {
                favorite.GroupId = group.Id;
                await _favoritesService.UpdateFavoriteAsync(favorite);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to add to group: {ex.Message}",
                    "Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        private string GetFolderIcon(string path)
        {
            return path.ToLowerInvariant() switch
            {
                var p when p.Contains("desktop") => "🖥️",
                var p when p.Contains("documents") => "📄",
                var p when p.Contains("downloads") => "⬇️",
                var p when p.Contains("pictures") => "🖼️",
                var p when p.Contains("music") => "🎵",
                var p when p.Contains("videos") => "🎬",
                _ => "📁"
            };
        }
    }

    /// <summary>
    /// ViewModel for quick access
    /// </summary>
    public partial class QuickAccessViewModel : ObservableObject
    {
        private readonly IFavoritesService _favoritesService;
        private ObservableCollection<QuickAccessItem> _quickAccessItems = new();

        [ObservableProperty]
        private ObservableCollection<RecentItem> _recentItems = new();

        [ObservableProperty]
        private ObservableCollection<PinnedItem> _pinnedItems = new();

        public ObservableCollection<QuickAccessItem> QuickAccessItems
        {
            get => _quickAccessItems;
            set => SetProperty(ref _quickAccessItems, value);
        }

        public ObservableCollection<RecentItem> RecentItems
        {
            get => _recentItems;
            set => SetProperty(ref _recentItems, value);
        }

        public ObservableCollection<PinnedItem> PinnedItems
        {
            get => _pinnedItems;
            set => SetProperty(ref _pinnedItems, value);
        }

        public QuickAccessViewModel(IFavoritesService favoritesService)
        {
            _favoritesService = favoritesService;
            _ = LoadQuickAccessAsync();
        }

        private async Task LoadQuickAccessAsync()
        {
            try
            {
                // Load pinned folders
                var pinned = await _favoritesService.GetPinnedFoldersAsync();
                PinnedItems.Clear();
                foreach (var item in pinned)
                {
                    PinnedItems.Add(item);
                }

                // Load recent folders
                var recent = await _favoritesService.GetRecentFoldersAsync();
                RecentItems.Clear();
                foreach (var item in recent.Take(10))
                {
                    RecentItems.Add(item);
                }

                // Load quick access items
                var quickAccess = await _favoritesService.GetQuickAccessAsync();
                QuickAccessItems.Clear();
                foreach (var item in quickAccess)
                {
                    QuickAccessItems.Add(item);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to load quick access: {ex.Message}",
                    "Load Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task PinFolderAsync(string folderPath)
        {
            try
            {
                var pinnedItem = new PinnedItem
                {
                    Name = System.IO.Path.GetFileName(folderPath),
                    Path = folderPath,
                    PinnedDate = DateTime.Now
                };

                await _favoritesService.PinFolderAsync(pinnedItem);
                PinnedItems.Add(pinnedItem);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to pin folder: {ex.Message}",
                    "Pin Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task UnpinFolderAsync(PinnedItem? item)
        {
            if (item == null) return;

            try
            {
                await _favoritesService.UnpinFolderAsync(item.Id);
                PinnedItems.Remove(item);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to unpin folder: {ex.Message}",
                    "Unpin Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task AddToQuickAccessAsync(string folderPath)
        {
            try
            {
                var quickAccessItem = new QuickAccessItem
                {
                    Name = System.IO.Path.GetFileName(folderPath),
                    Path = folderPath,
                    AddedDate = DateTime.Now
                };

                await _favoritesService.AddToQuickAccessAsync(quickAccessItem);
                QuickAccessItems.Add(quickAccessItem);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to add to quick access: {ex.Message}",
                    "Add Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task RemoveFromQuickAccessAsync(QuickAccessItem? item)
        {
            if (item == null) return;

            try
            {
                await _favoritesService.RemoveFromQuickAccessAsync(item.Id);
                QuickAccessItems.Remove(item);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to remove from quick access: {ex.Message}",
                    "Remove Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async void ClearRecent()
        {
            try
            {
                await _favoritesService.ClearRecentFoldersAsync();
                RecentItems.Clear();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to clear recent: {ex.Message}",
                    "Clear Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task RefreshAsync()
        {
            await LoadQuickAccessAsync();
        }
    }

    /// <summary>
    /// ViewModel for bookmarks
    /// </summary>
    public partial class BookmarksViewModel : ObservableObject
    {
        private readonly IFavoritesService _favoritesService;
        private ObservableCollection<BookmarkItem> _bookmarks = new();
        private ObservableCollection<BookmarkCategory> _categories = new();

        [ObservableProperty]
        private BookmarkItem? _selectedBookmark;

        [ObservableProperty]
        private bool _isAddingBookmark;

        [ObservableProperty]
        private string _newBookmarkName = string.Empty;

        [ObservableProperty]
        private string _newBookmarkUrl = string.Empty;

        [ObservableProperty]
        private BookmarkCategory? _selectedCategory;

        public ObservableCollection<BookmarkItem> Bookmarks
        {
            get => _bookmarks;
            set => SetProperty(ref _bookmarks, value);
        }

        public ObservableCollection<BookmarkCategory> Categories
        {
            get => _categories;
            set => SetProperty(ref _categories, value);
        }

        public BookmarksViewModel(IFavoritesService favoritesService)
        {
            _favoritesService = favoritesService;
            _ = LoadBookmarksAsync();
        }

        private async Task LoadBookmarksAsync()
        {
            try
            {
                var bookmarks = await _favoritesService.GetBookmarksAsync();
                Bookmarks.Clear();
                foreach (var bookmark in bookmarks)
                {
                    Bookmarks.Add(bookmark);
                }

                var categories = await _favoritesService.GetBookmarkCategoriesAsync();
                Categories.Clear();
                foreach (var category in categories)
                {
                    Categories.Add(category);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to load bookmarks: {ex.Message}",
                    "Load Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void StartAddBookmark()
        {
            IsAddingBookmark = true;
            NewBookmarkName = string.Empty;
            NewBookmarkUrl = string.Empty;
        }

        [RelayCommand]
        private void CancelAddBookmark()
        {
            IsAddingBookmark = false;
            NewBookmarkName = string.Empty;
            NewBookmarkUrl = string.Empty;
        }

        [RelayCommand]
        private async Task SaveBookmarkAsync()
        {
            if (string.IsNullOrEmpty(NewBookmarkName) || string.IsNullOrEmpty(NewBookmarkUrl)) return;

            try
            {
                var bookmark = new BookmarkItem
                {
                    Name = NewBookmarkName,
                    Url = NewBookmarkUrl,
                    CategoryId = SelectedCategory?.Id,
                    CreatedDate = DateTime.Now
                };

                await _favoritesService.AddBookmarkAsync(bookmark);
                Bookmarks.Add(bookmark);

                IsAddingBookmark = false;
                NewBookmarkName = string.Empty;
                NewBookmarkUrl = string.Empty;
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to save bookmark: {ex.Message}",
                    "Save Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task DeleteBookmarkAsync(BookmarkItem? bookmark)
        {
            if (bookmark == null) return;

            try
            {
                await _favoritesService.DeleteBookmarkAsync(bookmark.Id);
                Bookmarks.Remove(bookmark);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to delete bookmark: {ex.Message}",
                    "Delete Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task NavigateToBookmarkAsync(BookmarkItem? bookmark)
        {
            if (bookmark == null) return;

            try
            {
                if (!string.IsNullOrEmpty(bookmark.Url))
                {
                    var psi = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = bookmark.Url,
                        UseShellExecute = true
                    };
                    System.Diagnostics.Process.Start(psi);
                    bookmark.LastVisited = DateTime.Now;
                    bookmark.VisitCount++;
                    await _favoritesService.UpdateBookmarkAsync(bookmark);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to navigate to bookmark: {ex.Message}",
                    "Navigation Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task AddCategoryAsync(string categoryName)
        {
            if (string.IsNullOrEmpty(categoryName)) return;

            try
            {
                var category = new BookmarkCategory
                {
                    Name = categoryName,
                    CreatedDate = DateTime.Now
                };

                await _favoritesService.AddBookmarkCategoryAsync(category);
                Categories.Add(category);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to add category: {ex.Message}",
                    "Add Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void FilterByCategory(BookmarkCategory? category)
        {
            if (category == null)
            {
                // Show all bookmarks
                _ = LoadBookmarksAsync();
            }
            else
            {
                // Filter by category
                var filtered = Bookmarks.Where(b => b.CategoryId == category.Id).ToList();
                Bookmarks.Clear();
                foreach (var bookmark in filtered)
                {
                    Bookmarks.Add(bookmark);
                }
            }
        }
    }

    // Model classes
    public class FavoriteItem
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string Icon { get; set; } = "📁";
        public string? GroupId { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime LastAccessed { get; set; }
        public int AccessCount { get; set; }
        public bool IsPinned { get; set; }
    }

    public class FavoriteGroup
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string? Icon { get; set; }
        public DateTime CreatedDate { get; set; }
        public int SortOrder { get; set; }
    }

    public class QuickAccessItem
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public DateTime AddedDate { get; set; }
        public int AccessCount { get; set; }
    }

    public class RecentItem
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public DateTime LastAccessed { get; set; }
        public int AccessCount { get; set; }
    }

    public class PinnedItem
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public DateTime PinnedDate { get; set; }
        public int SortOrder { get; set; }
    }

    public class BookmarkItem
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string? CategoryId { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime LastVisited { get; set; }
        public int VisitCount { get; set; }
    }

    public class BookmarkCategory
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string? Icon { get; set; }
        public DateTime CreatedDate { get; set; }
        public int SortOrder { get; set; }
    }
}
