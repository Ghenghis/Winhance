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
    /// ViewModel for quick access panel
    /// </summary>
    public partial class QuickAccessViewModel : ObservableObject
    {
        private readonly IQuickAccessService _quickAccessService;
        private readonly INavigationService _navigationService;
        private ObservableCollection<QuickAccessItem> _quickAccessItems = new();

        [ObservableProperty]
        private QuickAccessItem? _selectedItem;

        [ObservableProperty]
        private bool _isEditing;

        [ObservableProperty]
        private string _newItemName = string.Empty;

        [ObservableProperty]
        private string _newItemPath = string.Empty;

        public ObservableCollection<QuickAccessItem> QuickAccessItems
        {
            get => _quickAccessItems;
            set => SetProperty(ref _quickAccessItems, value);
        }

        public QuickAccessViewModel(IQuickAccessService quickAccessService, INavigationService navigationService)
        {
            _quickAccessService = quickAccessService;
            _navigationService = navigationService;
            _ = LoadQuickAccessItemsAsync();
        }

        private async Task LoadQuickAccessItemsAsync()
        {
            try
            {
                var items = await _quickAccessService.GetQuickAccessItemsAsync();
                QuickAccessItems.Clear();
                foreach (var item in items.OrderBy(i => i.Order))
                {
                    QuickAccessItems.Add(item);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to load quick access items: {ex.Message}",
                    "Load Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task NavigateToItemAsync(QuickAccessItem? item)
        {
            if (item == null) return;

            try
            {
                await _navigationService.NavigateToPathAsync(item.Path);
                
                // Update access count
                item.AccessCount++;
                item.LastAccessed = DateTime.Now;
                await _quickAccessService.UpdateQuickAccessItemAsync(item);
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
            NewItemName = string.Empty;
            NewItemPath = string.Empty;
        }

        [RelayCommand]
        private async Task AddQuickAccessItemAsync()
        {
            if (string.IsNullOrEmpty(NewItemName) || string.IsNullOrEmpty(NewItemPath)) return;

            try
            {
                var newItem = new QuickAccessItem
                {
                    Name = NewItemName,
                    Path = NewItemPath,
                    Order = QuickAccessItems.Count,
                    DateAdded = DateTime.Now,
                    AccessCount = 0
                };

                await _quickAccessService.AddQuickAccessItemAsync(newItem);
                QuickAccessItems.Add(newItem);
                
                CancelEdit();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to add item: {ex.Message}",
                    "Add Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task RemoveItemAsync(QuickAccessItem? item)
        {
            if (item == null) return;

            try
            {
                await _quickAccessService.RemoveQuickAccessItemAsync(item.Id);
                QuickAccessItems.Remove(item);
                
                // Reorder remaining items
                for (int i = 0; i < QuickAccessItems.Count; i++)
                {
                    QuickAccessItems[i].Order = i;
                    await _quickAccessService.UpdateQuickAccessItemAsync(QuickAccessItems[i]);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to remove item: {ex.Message}",
                    "Remove Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task MoveItemUpAsync(QuickAccessItem? item)
        {
            if (item == null) return;

            var index = QuickAccessItems.IndexOf(item);
            if (index <= 0) return;

            try
            {
                // Swap with previous item
                var previousItem = QuickAccessItems[index - 1];
                QuickAccessItems.Move(index, index - 1);
                
                // Update orders
                item.Order = index - 1;
                previousItem.Order = index;
                
                await _quickAccessService.UpdateQuickAccessItemAsync(item);
                await _quickAccessService.UpdateQuickAccessItemAsync(previousItem);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to move item up: {ex.Message}",
                    "Move Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task MoveItemDownAsync(QuickAccessItem? item)
        {
            if (item == null) return;

            var index = QuickAccessItems.IndexOf(item);
            if (index >= QuickAccessItems.Count - 1) return;

            try
            {
                // Swap with next item
                var nextItem = QuickAccessItems[index + 1];
                QuickAccessItems.Move(index, index + 1);
                
                // Update orders
                item.Order = index + 1;
                nextItem.Order = index;
                
                await _quickAccessService.UpdateQuickAccessItemAsync(item);
                await _quickAccessService.UpdateQuickAccessItemAsync(nextItem);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to move item down: {ex.Message}",
                    "Move Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task RefreshAsync()
        {
            await LoadQuickAccessItemsAsync();
        }
    }

    /// <summary>
    /// ViewModel for bookmarks management
    /// </summary>
    public partial class BookmarksViewModel : ObservableObject
    {
        private readonly IBookmarkService _bookmarkService;
        private ObservableCollection<BookmarkCategory> _bookmarkCategories = new();
        private ObservableCollection<BookmarkItem> _currentBookmarks = new();

        [ObservableProperty]
        private BookmarkCategory? _selectedCategory;

        [ObservableProperty]
        private BookmarkItem? _selectedBookmark;

        [ObservableProperty]
        private bool _isAddingBookmark;

        [ObservableProperty]
        private string _newBookmarkName = string.Empty;

        [ObservableProperty]
        private string _newBookmarkPath = string.Empty;

        [ObservableProperty]
        private string _newBookmarkUrl = string.Empty;

        [ObservableProperty]
        private bool _isAddingCategory;

        [ObservableProperty]
        private string _newCategoryName = string.Empty;

        public ObservableCollection<BookmarkCategory> BookmarkCategories
        {
            get => _bookmarkCategories;
            set => SetProperty(ref _bookmarkCategories, value);
        }

        public ObservableCollection<BookmarkItem> CurrentBookmarks
        {
            get => _currentBookmarks;
            set => SetProperty(ref _currentBookmarks, value);
        }

        public BookmarksViewModel(IBookmarkService bookmarkService)
        {
            _bookmarkService = bookmarkService;
            _ = LoadBookmarksAsync();
        }

        private async Task LoadBookmarksAsync()
        {
            try
            {
                var categories = await _bookmarkService.GetBookmarkCategoriesAsync();
                BookmarkCategories.Clear();
                foreach (var category in categories)
                {
                    BookmarkCategories.Add(category);
                }

                // Select first category by default
                if (BookmarkCategories.Any())
                {
                    SelectedCategory = BookmarkCategories[0];
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

        partial void OnSelectedCategoryChanged(BookmarkCategory? value)
        {
            if (value != null)
            {
                _ = LoadCategoryBookmarksAsync(value);
            }
        }

        private async Task LoadCategoryBookmarksAsync(BookmarkCategory category)
        {
            try
            {
                var bookmarks = await _bookmarkService.GetBookmarksInCategoryAsync(category.Id);
                CurrentBookmarks.Clear();
                foreach (var bookmark in bookmarks.OrderBy(b => b.Name))
                {
                    CurrentBookmarks.Add(bookmark);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to load category bookmarks: {ex.Message}",
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
            NewBookmarkPath = string.Empty;
            NewBookmarkUrl = string.Empty;
        }

        [RelayCommand]
        private void CancelAddBookmark()
        {
            IsAddingBookmark = false;
            NewBookmarkName = string.Empty;
            NewBookmarkPath = string.Empty;
            NewBookmarkUrl = string.Empty;
        }

        [RelayCommand]
        private async Task AddBookmarkAsync()
        {
            if (SelectedCategory == null || string.IsNullOrEmpty(NewBookmarkName)) return;

            try
            {
                var bookmark = new BookmarkItem
                {
                    Name = NewBookmarkName,
                    Path = NewBookmarkPath,
                    Url = NewBookmarkUrl,
                    CategoryId = SelectedCategory.Id,
                    DateAdded = DateTime.Now
                };

                await _bookmarkService.AddBookmarkAsync(bookmark);
                CurrentBookmarks.Add(bookmark);
                
                CancelAddBookmark();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to add bookmark: {ex.Message}",
                    "Add Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task RemoveBookmarkAsync(BookmarkItem? bookmark)
        {
            if (bookmark == null) return;

            try
            {
                await _bookmarkService.RemoveBookmarkAsync(bookmark.Id);
                CurrentBookmarks.Remove(bookmark);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to remove bookmark: {ex.Message}",
                    "Remove Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task EditBookmarkAsync(BookmarkItem? bookmark)
        {
            if (bookmark == null) return;

            var newName = Microsoft.VisualBasic.Interaction.InputBox(
                "Enter new name:",
                "Edit Bookmark",
                bookmark.Name);

            if (!string.IsNullOrEmpty(newName))
            {
                try
                {
                    bookmark.Name = newName;
                    await _bookmarkService.UpdateBookmarkAsync(bookmark);
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show(
                        $"Failed to update bookmark: {ex.Message}",
                        "Update Error",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Error);
                }
            }
        }

        [RelayCommand]
        private void StartAddCategory()
        {
            IsAddingCategory = true;
            NewCategoryName = string.Empty;
        }

        [RelayCommand]
        private void CancelAddCategory()
        {
            IsAddingCategory = false;
            NewCategoryName = string.Empty;
        }

        [RelayCommand]
        private async Task AddCategoryAsync()
        {
            if (string.IsNullOrEmpty(NewCategoryName)) return;

            try
            {
                var category = new BookmarkCategory
                {
                    Name = NewCategoryName,
                    DateCreated = DateTime.Now
                };

                await _bookmarkService.AddBookmarkCategoryAsync(category);
                BookmarkCategories.Add(category);
                
                CancelAddCategory();
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
        private async Task DeleteCategoryAsync(BookmarkCategory? category)
        {
            if (category == null) return;

            try
            {
                await _bookmarkService.DeleteBookmarkCategoryAsync(category.Id);
                BookmarkCategories.Remove(category);
                
                if (SelectedCategory == category)
                {
                    SelectedCategory = BookmarkCategories.FirstOrDefault();
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to delete category: {ex.Message}",
                    "Delete Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task ImportBookmarksAsync()
        {
            try
            {
                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*",
                    Title = "Import Bookmarks"
                };

                if (dialog.ShowDialog() == true)
                {
                    await _bookmarkService.ImportBookmarksAsync(dialog.FileName);
                    await LoadBookmarksAsync();
                    System.Windows.MessageBox.Show(
                        "Bookmarks imported successfully.",
                        "Import Complete",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to import bookmarks: {ex.Message}",
                    "Import Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task ExportBookmarksAsync()
        {
            try
            {
                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*",
                    DefaultExt = ".json",
                    FileName = $"Bookmarks_{DateTime.Now:yyyyMMdd}.json",
                    Title = "Export Bookmarks"
                };

                if (dialog.ShowDialog() == true)
                {
                    await _bookmarkService.ExportBookmarksAsync(dialog.FileName);
                    System.Windows.MessageBox.Show(
                        $"Bookmarks exported to {dialog.FileName}",
                        "Export Complete",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to export bookmarks: {ex.Message}",
                    "Export Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }
    }

    /// <summary>
    /// ViewModel for recent items
    /// </summary>
    public partial class RecentItemsViewModel : ObservableObject
    {
        private readonly IRecentItemsService _recentItemsService;
        private ObservableCollection<RecentItem> _recentFiles = new();
        private ObservableCollection<RecentItem> _recentFolders = new();

        [ObservableProperty]
        private RecentItem? _selectedFile;

        [ObservableProperty]
        private RecentItem? _selectedFolder;

        [ObservableProperty]
        private int _maxRecentItems = 20;

        [ObservableProperty]
        private bool _showFiles = true;

        [ObservableProperty]
        private bool _showFolders = true;

        public ObservableCollection<RecentItem> RecentFiles
        {
            get => _recentFiles;
            set => SetProperty(ref _recentFiles, value);
        }

        public ObservableCollection<RecentItem> RecentFolders
        {
            get => _recentFolders;
            set => SetProperty(ref _recentFolders, value);
        }

        public RecentItemsViewModel(IRecentItemsService recentItemsService)
        {
            _recentItemsService = recentItemsService;
            _ = LoadRecentItemsAsync();
        }

        private async Task LoadRecentItemsAsync()
        {
            try
            {
                if (ShowFiles)
                await LoadRecentFilesAsync();
                if (ShowFolders)
                    await LoadRecentFoldersAsync();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to load recent items: {ex.Message}",
                    "Load Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        private async Task LoadRecentFilesAsync()
        {
            var files = await _recentItemsService.GetRecentFilesAsync(MaxRecentItems);
            RecentFiles.Clear();
            foreach (var file in files)
            {
                RecentFiles.Add(file);
            }
        }

        private async Task LoadRecentFoldersAsync()
        {
            var folders = await _recentItemsService.GetRecentFoldersAsync(MaxRecentItems);
            RecentFolders.Clear();
            foreach (var folder in folders)
            {
                RecentFolders.Add(folder);
            }
        }

        [RelayCommand]
        private async Task OpenRecentFileAsync(RecentItem? item)
        {
            if (item == null) return;

            try
            {
                await _recentItemsService.OpenRecentItemAsync(item);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to open recent file: {ex.Message}",
                    "Open Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task OpenRecentFolderAsync(RecentItem? item)
        {
            if (item == null) return;

            try
            {
                await _recentItemsService.OpenRecentItemAsync(item);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to open recent folder: {ex.Message}",
                    "Open Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task RemoveFromRecentAsync(RecentItem? item)
        {
            if (item == null) return;

            try
            {
                await _recentItemsService.RemoveFromRecentAsync(item.Id);
                RecentFiles.Remove(item);
                RecentFolders.Remove(item);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to remove from recent: {ex.Message}",
                    "Remove Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task ClearRecentFilesAsync()
        {
            try
            {
                await _recentItemsService.ClearRecentFilesAsync();
                RecentFiles.Clear();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to clear recent files: {ex.Message}",
                    "Clear Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task ClearRecentFoldersAsync()
        {
            try
            {
                await _recentItemsService.ClearRecentFoldersAsync();
                RecentFolders.Clear();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to clear recent folders: {ex.Message}",
                    "Clear Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task ClearAllRecentAsync()
        {
            try
            {
                await _recentItemsService.ClearAllRecentAsync();
                RecentFiles.Clear();
                RecentFolders.Clear();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to clear all recent items: {ex.Message}",
                    "Clear Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task RefreshAsync()
        {
            await LoadRecentItemsAsync();
        }

        partial void OnMaxRecentItemsChanged(int value)
        {
            _ = LoadRecentItemsAsync();
        }

        partial void OnShowFilesChanged(bool value)
        {
            if (value) _ = LoadRecentFilesAsync();
            else RecentFiles.Clear();
        }

        partial void OnShowFoldersChanged(bool value)
        {
            if (value) _ = LoadRecentFoldersAsync();
            else RecentFolders.Clear();
        }
    }

    // Model classes
    public class QuickAccessItem
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string? Icon { get; set; }
        public int Order { get; set; }
        public DateTime DateAdded { get; set; }
        public DateTime LastAccessed { get; set; }
        public int AccessCount { get; set; }
    }

    public class BookmarkCategory
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Icon { get; set; }
        public DateTime DateCreated { get; set; }
        public int BookmarkCount { get; set; }
    }

    public class BookmarkItem
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string? Url { get; set; }
        public string? Description { get; set; }
        public string CategoryId { get; set; } = string.Empty;
        public string? Icon { get; set; }
        public DateTime DateAdded { get; set; }
        public DateTime LastAccessed { get; set; }
        public int AccessCount { get; set; }
        public bool IsFavorite { get; set; }
    }

    public class RecentItem
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string? Icon { get; set; }
        public DateTime LastAccessed { get; set; }
        public long Size { get; set; }
        public bool IsFolder { get; set; }
        public string? FileType { get; set; }
    }
}
