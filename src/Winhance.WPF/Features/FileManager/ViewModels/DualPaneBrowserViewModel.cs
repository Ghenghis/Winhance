using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Winhance.Core.Features.FileManager.Interfaces;
using Winhance.Core.Features.FileManager.Models;
using Microsoft.Extensions.DependencyInjection;

namespace Winhance.WPF.Features.FileManager.ViewModels
{
    /// <summary>
    /// ViewModel for the dual-pane file browser.
    /// </summary>
    public partial class DualPaneBrowserViewModel : ObservableObject
    {
        private readonly IFileManagerService? _fileManagerService;
        private readonly IServiceProvider? _serviceProvider;
        private readonly IFavoritesService? _favoritesService;
        private readonly ITabService? _tabService;
        private readonly IOperationQueueService? _operationQueueService;
        private readonly ISearchService? _searchService;
        private readonly IClipboardService? _clipboardService;
        private readonly ISelectionService? _selectionService;
        private readonly IAddressBarService? _addressBarService;
        private readonly IViewModeService? _viewModeService;
        private readonly ISortingService? _sortingService;
        private readonly IQuickFilterService? _filterService;
        private readonly IPreviewService? _previewService;

        // Left Panel
        [ObservableProperty]
        private FileListViewModel _leftPanel;

        // Right Panel
        [ObservableProperty]
        private FileListViewModel _rightPanel;

        // Active Panel
        [ObservableProperty]
        private FileListViewModel _activePanel;

        // Splitter Position
        [ObservableProperty]
        private double _splitterPosition = 0.5;

        // Preview Visible
        [ObservableProperty]
        private bool _isPreviewVisible;

        // Favorites Visible
        [ObservableProperty]
        private bool _isFavoritesVisible;

        // Status Bar
        [ObservableProperty]
        private string _statusText = "Ready";

        [ObservableProperty]
        private bool _isOperationInProgress;

        [ObservableProperty]
        private string _operationStatus = string.Empty;

        [ObservableProperty]
        private bool _isLeftPaneActive = true;

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private string _filterText = string.Empty;

        [ObservableProperty]
        private ObservableCollection<DriveItemViewModel> _drives = new();

        [ObservableProperty]
        private string _statusMessage = "Ready";

        [ObservableProperty]
        private List<string> _clipboardPaths = new();

        [ObservableProperty]
        private bool _clipboardIsCut;

        [ObservableProperty]
        private FileItemViewModel? _renamingItem;

        // Sorting properties (FM-029 to FM-036)
        [ObservableProperty]
        private string _sortColumn = "Name";

        [ObservableProperty]
        private bool _sortAscending = true;

        [ObservableProperty]
        private bool _foldersFirst = true;

        // View mode properties
        [ObservableProperty]
        private string _viewMode = "Details"; // Details, Icons, List, Tiles

        // P0-009 to P0-011: Quick Access sidebar properties
        [ObservableProperty]
        private bool _showQuickAccess = true;

        [ObservableProperty]
        private double _quickAccessWidth = 220;

        [ObservableProperty]
        private FavoritesPanelViewModel? _favoritesPanelViewModel;

        // Advanced selection dialog
        [ObservableProperty]
        private AdvancedSelectionViewModel? _advancedSelectionViewModel;

        // Keyboard shortcuts
        [ObservableProperty]
        private KeyboardShortcutViewModel? _keyboardShortcutViewModel;

        public DualPaneBrowserViewModel()
        {
            // Design-time constructor
            LoadDesignTimeData();
            InitializeQuickAccess();
        }

        public DualPaneBrowserViewModel(IFileManagerService? fileManagerService, IServiceProvider? serviceProvider)
        {
            _fileManagerService = fileManagerService;
            _serviceProvider = serviceProvider;
            
            // Inject all available services
            if (_serviceProvider != null)
            {
                _favoritesService = _serviceProvider.GetService<IFavoritesService>();
                _tabService = _serviceProvider.GetService<ITabService>();
                _operationQueueService = _serviceProvider.GetService<IOperationQueueService>();
                _searchService = _serviceProvider.GetService<ISearchService>();
                
                // Initialize view models
                _favoritesPanelViewModel = _serviceProvider.GetService<FavoritesPanelViewModel>();
                _keyboardShortcutViewModel = _serviceProvider.GetService<KeyboardShortcutViewModel>();
                
                // Initialize keyboard shortcuts with this instance
                if (_keyboardShortcutViewModel != null)
                {
                    _keyboardShortcutViewModel = new KeyboardShortcutViewModel(this);
                }
            }
            
            InitializeQuickAccess();
            _ = InitializeAsync();
        }

        private void InitializeQuickAccess()
        {
            if (_serviceProvider != null)
            {
                FavoritesPanelViewModel = _serviceProvider.GetRequiredService<FavoritesPanelViewModel>();
            }
            else
            {
                FavoritesPanelViewModel = new FavoritesPanelViewModel();
            }
            FavoritesPanelViewModel.NavigateToPathRequested += OnFavoriteNavigationRequested;
        }

        private async void OnFavoriteNavigationRequested(object? sender, string path)
        {
            if (IsLeftPaneActive)
            {
                LeftPanePath = path;
                await LoadLeftPaneAsync().ConfigureAwait(false);
            }
            else
            {
                RightPanePath = path;
                await LoadRightPaneAsync().ConfigureAwait(false);
            }
        }

        [RelayCommand]
        private void ToggleQuickAccess()
        {
            ShowQuickAccess = !ShowQuickAccess;
        }

        // P0-006-008: Tab management commands
        [ObservableProperty]
        private TabContainerViewModel? _tabContainerViewModel;

        [RelayCommand]
        private void NewTab()
        {
            if (_serviceProvider != null)
            {
                TabContainerViewModel ??= _serviceProvider.GetRequiredService<TabContainerViewModel>();
            }
            else
            {
                TabContainerViewModel ??= new TabContainerViewModel();
            }
            TabContainerViewModel.CreateTab(IsLeftPaneActive ? LeftPanePath : RightPanePath);
        }

        [RelayCommand]
        private void CloseTab()
        {
            TabContainerViewModel?.CloseTab(TabContainerViewModel.ActiveTab);
        }

        [RelayCommand]
        private void NextTab()
        {
            if (TabContainerViewModel == null || TabContainerViewModel.Tabs.Count <= 1) return;
            var currentIndex = TabContainerViewModel.Tabs.IndexOf(TabContainerViewModel.ActiveTab!);
            var nextIndex = (currentIndex + 1) % TabContainerViewModel.Tabs.Count;
            TabContainerViewModel.ActivateTab(TabContainerViewModel.Tabs[nextIndex]);
        }

        [RelayCommand]
        private void PreviousTab()
        {
            if (TabContainerViewModel == null || TabContainerViewModel.Tabs.Count <= 1) return;
            var currentIndex = TabContainerViewModel.Tabs.IndexOf(TabContainerViewModel.ActiveTab!);
            var prevIndex = (currentIndex - 1 + TabContainerViewModel.Tabs.Count) % TabContainerViewModel.Tabs.Count;
            TabContainerViewModel.ActivateTab(TabContainerViewModel.Tabs[prevIndex]);
        }

        [RelayCommand]
        private void FocusSearch()
        {
            // Trigger search focus event - will be handled by view
            SearchFocusRequested?.Invoke(this, EventArgs.Empty);
        }

        [RelayCommand]
        private void AddToFavorites()
        {
            var path = IsLeftPaneActive ? LeftPanePath : RightPanePath;
            FavoritesPanelViewModel?.AddToFavoritesCommand.Execute(path);
        }

        public event EventHandler? SearchFocusRequested;

        // P0-003-005: Search integration
        [ObservableProperty]
        private bool _showSearchPanel;

        [ObservableProperty]
        private SearchResultsViewModel? _searchResultsViewModel;

        [ObservableProperty]
        private string _quickSearchText = string.Empty;

        partial void OnQuickSearchTextChanged(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                ShowSearchPanel = false;
                return;
            }

            ShowSearchPanel = true;
            if (_serviceProvider != null)
            {
                SearchResultsViewModel ??= _serviceProvider.GetRequiredService<SearchResultsViewModel>();
            }
            else
            {
                SearchResultsViewModel ??= new SearchResultsViewModel();
            }
            SearchResultsViewModel.SearchQuery = value;
            SearchResultsViewModel.SearchPath = IsLeftPaneActive ? LeftPanePath : RightPanePath;
            _ = SearchResultsViewModel.SearchAsync();
        }

        [RelayCommand]
        private void CloseSearch()
        {
            ShowSearchPanel = false;
            QuickSearchText = string.Empty;
        }

        // Navigation commands
        [ObservableProperty]
        private Stack<string> _leftPathHistory = new();
        [ObservableProperty]
        private Stack<string> _rightPathHistory = new();
        [ObservableProperty]
        private Stack<string> _leftForwardHistory = new();
        [ObservableProperty]
        private Stack<string> _rightForwardHistory = new();

        [RelayCommand]
        private async void NavigateUp()
        {
            var currentPath = IsLeftPaneActive ? LeftPanePath : RightPanePath;
            var parentPath = System.IO.Directory.GetParent(currentPath)?.FullName;

            if (!string.IsNullOrEmpty(parentPath))
            {
                if (IsLeftPaneActive)
                {
                    LeftPathHistory.Push(LeftPanePath);
                    LeftPanePath = parentPath;
                    await LoadLeftPaneAsync().ConfigureAwait(false);
                }
                else
                {
                    RightPathHistory.Push(RightPanePath);
                    RightPanePath = parentPath;
                    await LoadRightPaneAsync().ConfigureAwait(false);
                }
            }
        }

        [RelayCommand]
        private async void GoBack()
        {
            if (IsLeftPaneActive && LeftPathHistory.Count > 0)
            {
                var previousPath = LeftPathHistory.Pop();
                LeftForwardHistory.Push(LeftPanePath);
                LeftPanePath = previousPath;
                await LoadLeftPaneAsync().ConfigureAwait(false);
            }
            else if (!IsLeftPaneActive && RightPathHistory.Count > 0)
            {
                var previousPath = RightPathHistory.Pop();
                RightForwardHistory.Push(RightPanePath);
                RightPanePath = previousPath;
                await LoadRightPaneAsync().ConfigureAwait(false);
            }
        }

        [RelayCommand]
        private async void GoForward()
        {
            if (IsLeftPaneActive && LeftForwardHistory.Count > 0)
            {
                var nextPath = LeftForwardHistory.Pop();
                LeftPathHistory.Push(LeftPanePath);
                LeftPanePath = nextPath;
                await LoadLeftPaneAsync().ConfigureAwait(false);
            }
            else if (!IsLeftPaneActive && RightForwardHistory.Count > 0)
            {
                var nextPath = RightForwardHistory.Pop();
                RightPathHistory.Push(RightPanePath);
                RightPanePath = nextPath;
                await LoadRightPaneAsync().ConfigureAwait(false);
            }
        }

        [RelayCommand]
        private void SelectAll()
        {
            if (IsLeftPaneActive)
            {
                SelectedLeftItems.Clear();
                foreach (var item in LeftPaneItems.Where(i => !i.IsParentDirectory))
                {
                    SelectedLeftItems.Add(item);
                }
            }
            else
            {
                SelectedRightItems.Clear();
                foreach (var item in RightPaneItems.Where(i => !i.IsParentDirectory))
                {
                    SelectedRightItems.Add(item);
                }
            }
        }

        // Sorting commands (FM-029 to FM-036)
        [RelayCommand]
        private async Task SortByNameAsync()
        {
            SortColumn = "Name";
            await RefreshAsync();
            StatusMessage = $"Sorted by Name ({(SortAscending ? "A-Z" : "Z-A")})";
        }

        [RelayCommand]
        private async Task SortBySizeAsync()
        {
            SortColumn = "Size";
            await RefreshAsync();
            StatusMessage = $"Sorted by Size ({(SortAscending ? "smallest first" : "largest first")})";
        }

        [RelayCommand]
        private async Task SortByTypeAsync()
        {
            SortColumn = "Type";
            await RefreshAsync();
            StatusMessage = $"Sorted by Type ({(SortAscending ? "A-Z" : "Z-A")})";
        }

        [RelayCommand]
        private async Task SortByDateAsync()
        {
            SortColumn = "Date";
            await RefreshAsync();
            StatusMessage = $"Sorted by Date ({(SortAscending ? "oldest first" : "newest first")})";
        }

        [RelayCommand]
        private async Task ToggleSortOrderAsync()
        {
            SortAscending = !SortAscending;
            await RefreshAsync();
            StatusMessage = $"Sort order: {(SortAscending ? "Ascending" : "Descending")}";
        }

        [RelayCommand]
        private async Task ToggleFoldersFirstAsync()
        {
            FoldersFirst = !FoldersFirst;
            await RefreshAsync();
            StatusMessage = FoldersFirst ? "Folders shown first" : "Files and folders mixed";
        }

        // View mode commands
        [RelayCommand]
        private void SetDetailsView()
        {
            ViewMode = "Details";
            StatusMessage = "View: Details";
        }

        [RelayCommand]
        private void SetIconsView()
        {
            ViewMode = "Icons";
            StatusMessage = "View: Icons";
        }

        [RelayCommand]
        private void SetListView()
        {
            ViewMode = "List";
            StatusMessage = "View: List";
        }

        [RelayCommand]
        private void SetTilesView()
        {
            ViewMode = "Tiles";
            StatusMessage = "View: Tiles";
        }

        /// <summary>
        /// Sort items based on current sort settings.
        /// </summary>
        private IEnumerable<FileItemViewModel> ApplySorting(IEnumerable<FileItemViewModel> items)
        {
            // Always keep parent directory (..) at top
            var parentDir = items.Where(i => i.IsParentDirectory);
            var regularItems = items.Where(i => !i.IsParentDirectory);

            IEnumerable<FileItemViewModel> sorted;

            if (FoldersFirst)
            {
                var folders = regularItems.Where(i => i.IsDirectory);
                var files = regularItems.Where(i => !i.IsDirectory);

                folders = SortItems(folders);
                files = SortItems(files);

                sorted = folders.Concat(files);
            }
            else
            {
                sorted = SortItems(regularItems);
            }

            return parentDir.Concat(sorted);
        }

        private IEnumerable<FileItemViewModel> SortItems(IEnumerable<FileItemViewModel> items)
        {
            return SortColumn switch
            {
                "Name" => SortAscending
                    ? items.OrderBy(i => i.Name, NaturalStringComparer.Instance)
                    : items.OrderByDescending(i => i.Name, NaturalStringComparer.Instance),
                "Size" => SortAscending
                    ? items.OrderBy(i => i.Size)
                    : items.OrderByDescending(i => i.Size),
                "Type" => SortAscending
                    ? items.OrderBy(i => i.Extension, StringComparer.OrdinalIgnoreCase)
                    : items.OrderByDescending(i => i.Extension, StringComparer.OrdinalIgnoreCase),
                "Date" => SortAscending
                    ? items.OrderBy(i => i.DateModified)
                    : items.OrderByDescending(i => i.DateModified),
                _ => items.OrderBy(i => i.Name, NaturalStringComparer.Instance),
            };
        }

        private async void NavigateToPath(string path)
        {
            if (IsLeftPaneActive)
            {
                LeftPathHistory.Push(LeftPanePath);
                LeftPanePath = path;
                await LoadLeftPaneAsync().ConfigureAwait(false);
            }
            else
            {
                RightPathHistory.Push(RightPanePath);
                RightPanePath = path;
                await LoadRightPaneAsync().ConfigureAwait(false);
            }
        }

        private async Task InitializeAsync()
        {
            await LoadDrivesAsync();
            await RefreshAsync();
        }

        private void LoadDesignTimeData()
        {
            LeftPaneItems.Add(new FileItemViewModel { Name = "Documents", IsDirectory = true, Size = 0 });
            LeftPaneItems.Add(new FileItemViewModel { Name = "Downloads", IsDirectory = true, Size = 0 });
            LeftPaneItems.Add(new FileItemViewModel { Name = "report.pdf", IsDirectory = false, Size = 1024000 });

            RightPaneItems.Add(new FileItemViewModel { Name = "Program Files", IsDirectory = true, Size = 0 });
            RightPaneItems.Add(new FileItemViewModel { Name = "Windows", IsDirectory = true, Size = 0 });
        }

        [RelayCommand]
        private async Task LoadDrivesAsync()
        {
            Drives.Clear();

            if (_fileManagerService != null)
            {
                var drives = await _fileManagerService.GetDrivesAsync();
                foreach (var drive in drives.Where(d => d.IsReady))
                {
                    Drives.Add(new DriveItemViewModel
                    {
                        Name = drive.Name,
                        Label = string.IsNullOrEmpty(drive.Label) ? drive.Name : drive.Label,
                        TotalSize = drive.TotalSize,
                        FreeSpace = drive.FreeSpace,
                        DriveType = drive.DriveType,
                    });
                }
            }
            else
            {
                // Fallback to System.IO
                foreach (var drive in System.IO.DriveInfo.GetDrives().Where(d => d.IsReady))
                {
                    Drives.Add(new DriveItemViewModel
                    {
                        Name = drive.Name,
                        Label = string.IsNullOrEmpty(drive.VolumeLabel) ? drive.Name : drive.VolumeLabel,
                        TotalSize = drive.TotalSize,
                        FreeSpace = drive.TotalFreeSpace,
                        DriveType = drive.DriveType.ToString(),
                    });
                }
            }
        }

        [RelayCommand]
        public async Task RefreshAsync()
        {
            IsLoading = true;
            try
            {
                await LoadLeftPaneAsync();
                await LoadRightPaneAsync();
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task LoadLeftPaneAsync()
        {
            await LoadPaneAsync(LeftPanePath, LeftPaneItems, true);
        }

        [RelayCommand]
        private async Task LoadRightPaneAsync()
        {
            await LoadPaneAsync(RightPanePath, RightPaneItems, false);
        }

        private async Task LoadPaneAsync(string path, ObservableCollection<FileItemViewModel> items, bool isLeftPane)
        {
            try
            {
                var tempItems = new List<FileItemViewModel>();

                if (_fileManagerService != null)
                {
                    var entries = await _fileManagerService.GetDirectoryContentsAsync(path);
                    foreach (var entry in entries)
                    {
                        tempItems.Add(new FileItemViewModel
                        {
                            Name = entry.Name,
                            FullPath = entry.FullPath,
                            IsDirectory = entry.IsDirectory,
                            Size = entry.Size,
                            DateModified = entry.DateModified,
                            Extension = entry.Extension,
                        });
                    }
                }
                else
                {
                    // Fallback to System.IO
                    var dirInfo = new DirectoryInfo(path);

                    // Add parent directory entry
                    if (dirInfo.Parent != null)
                    {
                        tempItems.Add(new FileItemViewModel
                        {
                            Name = "..",
                            FullPath = dirInfo.Parent.FullName,
                            IsDirectory = true,
                            IsParentDirectory = true,
                        });
                    }

                    foreach (var dir in dirInfo.GetDirectories())
                    {
                        try
                        {
                            tempItems.Add(new FileItemViewModel
                            {
                                Name = dir.Name,
                                FullPath = dir.FullName,
                                IsDirectory = true,
                                DateModified = dir.LastWriteTime,
                            });
                        }
                        catch (Exception)
                        { /* Skip inaccessible directories */
                        }
                    }

                    foreach (var file in dirInfo.GetFiles())
                    {
                        try
                        {
                            tempItems.Add(new FileItemViewModel
                            {
                                Name = file.Name,
                                FullPath = file.FullName,
                                IsDirectory = false,
                                Size = file.Length,
                                DateModified = file.LastWriteTime,
                                Extension = file.Extension,
                            });
                        }
                        catch (Exception)
                        { /* Skip inaccessible files */
                        }
                    }
                }

                // Apply sorting (FM-029 to FM-036) and replace entire collection for instant UI update
                var sortedItems = ApplySorting(tempItems);
                items.Clear();
                foreach (var item in sortedItems)
                {
                    items.Add(item);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading {path}: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task NavigateLeftAsync(string path)
        {
            LeftPathHistory.Push(LeftPanePath);
            LeftPanePath = path;
            await LoadLeftPaneAsync();
        }

        [RelayCommand]
        private async Task NavigateRightAsync(string path)
        {
            RightPathHistory.Push(RightPanePath);
            RightPanePath = path;
            await LoadRightPaneAsync();
        }

        [RelayCommand]
        private async Task NavigateUpLeftAsync()
        {
            var parent = Directory.GetParent(LeftPanePath);
            if (parent != null)
            {
                await NavigateLeftAsync(parent.FullName);
            }
        }

        [RelayCommand]
        private async Task NavigateUpRightAsync()
        {
            var parent = Directory.GetParent(RightPanePath);
            if (parent != null)
            {
                await NavigateRightAsync(parent.FullName);
            }
        }

        [RelayCommand]
        private async Task GoBackLeftAsync()
        {
            if (LeftPathHistory.Count > 0)
            {
                var previousPath = LeftPathHistory.Pop();
                LeftPanePath = previousPath;
                await LoadLeftPaneAsync();
            }
        }

        [RelayCommand]
        private async Task GoBackRightAsync()
        {
            if (RightPathHistory.Count > 0)
            {
                var previousPath = RightPathHistory.Pop();
                RightPanePath = previousPath;
                await LoadRightPaneAsync();
            }
        }

        [RelayCommand]
        private async Task CopyToOtherPaneAsync()
        {
            var sourceItems = IsLeftPaneActive ? SelectedLeftItems : SelectedRightItems;
            var destPath = IsLeftPaneActive ? RightPanePath : LeftPanePath;

            if (sourceItems.Count == 0 || _fileManagerService == null)
            {
                return;
            }

            var sourcePaths = sourceItems.Select(i => i.FullPath).ToList();
            await _fileManagerService.CopyFilesAsync(sourcePaths, destPath);
            await RefreshAsync();
        }

        [RelayCommand]
        private async Task MoveToOtherPaneAsync()
        {
            var sourceItems = IsLeftPaneActive ? SelectedLeftItems : SelectedRightItems;
            var destPath = IsLeftPaneActive ? RightPanePath : LeftPanePath;

            if (sourceItems.Count == 0 || _fileManagerService == null)
            {
                return;
            }

            var sourcePaths = sourceItems.Select(i => i.FullPath).ToList();
            await _fileManagerService.MoveFilesAsync(sourcePaths, destPath);
            await RefreshAsync();
        }

        [RelayCommand]
        private async Task DeleteSelectedAsync()
        {
            var sourceItems = IsLeftPaneActive ? SelectedLeftItems : SelectedRightItems;
            if (sourceItems.Count == 0 || _fileManagerService == null)
            {
                return;
            }

            var sourcePaths = sourceItems.Select(i => i.FullPath).ToList();
            await _fileManagerService.DeleteFilesAsync(sourcePaths, permanent: false);
            await RefreshAsync();
        }

        [RelayCommand]
        private async Task CreateNewFolderAsync()
        {
            var currentPath = IsLeftPaneActive ? LeftPanePath : RightPanePath;
            if (_fileManagerService == null)
            {
                return;
            }

            var newFolderPath = Path.Combine(currentPath, "New Folder");
            await _fileManagerService.CreateDirectoryAsync(newFolderPath);
            await RefreshAsync();
        }

        /// <summary>
        /// FM-015: Type-to-filter - Filter/jump to items as user types.
        /// </summary>
        partial void OnFilterTextChanged(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                // Clear filter - show all items
                ClearFilter();
                return;
            }

            // Get the active pane items
            var items = IsLeftPaneActive ? LeftPaneItems : RightPaneItems;

            // Find first matching item (case-insensitive)
            var matchingItem = items.FirstOrDefault(i =>
                !i.IsParentDirectory &&
                i.Name.StartsWith(value, StringComparison.OrdinalIgnoreCase));

            if (matchingItem != null)
            {
                // Select and scroll to the matching item
                if (IsLeftPaneActive)
                {
                    SelectedLeftItem = matchingItem;
                    SelectedLeftItems.Clear();
                    SelectedLeftItems.Add(matchingItem);
                }
                else
                {
                    SelectedRightItem = matchingItem;
                    SelectedRightItems.Clear();
                    SelectedRightItems.Add(matchingItem);
                }
                StatusMessage = $"Found: {matchingItem.Name}";

                // Raise event for view to scroll to item
                FilterMatchFound?.Invoke(this, matchingItem);
            }
            else
            {
                StatusMessage = $"No match for: {value}";
            }
        }

        /// <summary>
        /// Event raised when filter finds a match (for scrolling in view).
        /// </summary>
        public event EventHandler<FileItemViewModel>? FilterMatchFound;

        /// <summary>
        /// Clear the filter and restore normal view.
        /// </summary>
        [RelayCommand]
        private void ClearFilter()
        {
            FilterText = string.Empty;
            StatusMessage = "Ready";
        }

        /// <summary>
        /// Opens a file or navigates into a folder (double-click action).
        /// </summary>
        [RelayCommand]
        private async Task OpenItemAsync(FileItemViewModel? item)
        {
            if (item == null)
            {
                return;
            }

            if (item.IsDirectory)
            {
                // Navigate into the folder
                if (IsLeftPaneActive)
                {
                    await NavigateLeftAsync(item.FullPath);
                }
                else
                {
                    await NavigateRightAsync(item.FullPath);
                }
            }
            else
            {
                // Open file with default application
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = item.FullPath,
                        UseShellExecute = true,
                    };
                    Process.Start(psi);
                    StatusMessage = $"Opened: {item.Name}";
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Error opening file: {ex.Message}";
                }
            }
        }

        /// <summary>
        /// Copy selected items to clipboard.
        /// </summary>
        [RelayCommand]
        private void CopyToClipboard()
        {
            var items = IsLeftPaneActive ? SelectedLeftItems : SelectedRightItems;
            if (items.Count == 0)
            {
                // Try single selected item
                var single = IsLeftPaneActive ? SelectedLeftItem : SelectedRightItem;
                if (single != null && !single.IsParentDirectory)
                {
                    ClipboardPaths = new List<string> { single.FullPath };
                    ClipboardIsCut = false;
                    StatusMessage = $"Copied: {single.Name}";
                }
                return;
            }

            ClipboardPaths = items.Where(i => !i.IsParentDirectory).Select(i => i.FullPath).ToList();
            ClipboardIsCut = false;
            StatusMessage = $"Copied {ClipboardPaths.Count} item(s)";

            // Also copy to Windows clipboard
            try
            {
                var paths = new System.Collections.Specialized.StringCollection();
                foreach (var path in ClipboardPaths)
                {
                    paths.Add(path);
                }
                Clipboard.SetFileDropList(paths);
            }
            catch { /* Clipboard may fail */ }
        }

        /// <summary>
        /// Cut selected items to clipboard.
        /// </summary>
        [RelayCommand]
        private void CutToClipboard()
        {
            var items = IsLeftPaneActive ? SelectedLeftItems : SelectedRightItems;
            if (items.Count == 0)
            {
                var single = IsLeftPaneActive ? SelectedLeftItem : SelectedRightItem;
                if (single != null && !single.IsParentDirectory)
                {
                    ClipboardPaths = new List<string> { single.FullPath };
                    ClipboardIsCut = true;
                    StatusMessage = $"Cut: {single.Name}";
                }
                return;
            }

            ClipboardPaths = items.Where(i => !i.IsParentDirectory).Select(i => i.FullPath).ToList();
            ClipboardIsCut = true;
            StatusMessage = $"Cut {ClipboardPaths.Count} item(s)";
        }

        /// <summary>
        /// Paste items from clipboard to current pane.
        /// </summary>
        [RelayCommand]
        private async Task PasteFromClipboardAsync()
        {
            var destPath = IsLeftPaneActive ? LeftPanePath : RightPanePath;

            // Try Windows clipboard first
            if (Clipboard.ContainsFileDropList())
            {
                var files = Clipboard.GetFileDropList();
                ClipboardPaths = files.Cast<string>().ToList();
            }

            if (ClipboardPaths.Count == 0)
            {
                StatusMessage = "Nothing to paste";
                return;
            }

            IsLoading = true;
            StatusMessage = $"Pasting {ClipboardPaths.Count} item(s)...";

            try
            {
                foreach (var sourcePath in ClipboardPaths)
                {
                    var fileName = Path.GetFileName(sourcePath);
                    var destFilePath = Path.Combine(destPath, fileName);

                    // Handle name conflicts
                    destFilePath = GetUniqueDestPath(destFilePath);

                    if (Directory.Exists(sourcePath))
                    {
                        if (ClipboardIsCut)
                        {
                            Directory.Move(sourcePath, destFilePath);
                        }
                        else
                        {
                            CopyDirectory(sourcePath, destFilePath);
                        }
                    }
                    else if (File.Exists(sourcePath))
                    {
                        if (ClipboardIsCut)
                        {
                            File.Move(sourcePath, destFilePath);
                        }
                        else
                        {
                            File.Copy(sourcePath, destFilePath);
                        }
                    }
                }

                if (ClipboardIsCut)
                {
                    ClipboardPaths.Clear();
                    ClipboardIsCut = false;
                }

                StatusMessage = "Paste complete";
                await RefreshAsync();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Paste error: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private static string GetUniqueDestPath(string path)
        {
            if (!File.Exists(path) && !Directory.Exists(path))
            {
                return path;
            }

            var dir = Path.GetDirectoryName(path) ?? string.Empty;
            var name = Path.GetFileNameWithoutExtension(path);
            var ext = Path.GetExtension(path);
            int counter = 1;

            string newPath;
            do
            {
                newPath = Path.Combine(dir, $"{name} ({counter}){ext}");
                counter++;
            } while (File.Exists(newPath) || Directory.Exists(newPath));

            return newPath;
        }

        private static void CopyDirectory(string sourceDir, string destDir)
        {
            Directory.CreateDirectory(destDir);

            foreach (var file in Directory.GetFiles(sourceDir))
            {
                var destFile = Path.Combine(destDir, Path.GetFileName(file));
                File.Copy(file, destFile, true);
            }

            foreach (var dir in Directory.GetDirectories(sourceDir))
            {
                var destSubDir = Path.Combine(destDir, Path.GetFileName(dir));
                CopyDirectory(dir, destSubDir);
            }
        }

        /// <summary>
        /// Delete selected items (to recycle bin).
        /// </summary>
        [RelayCommand]
        private async Task DeleteAsync()
        {
            var items = IsLeftPaneActive ? SelectedLeftItems : SelectedRightItems;
            var itemsToDelete = items.Where(i => !i.IsParentDirectory).ToList();

            if (itemsToDelete.Count == 0)
            {
                var single = IsLeftPaneActive ? SelectedLeftItem : SelectedRightItem;
                if (single != null && !single.IsParentDirectory)
                {
                    itemsToDelete.Add(single);
                }
            }

            if (itemsToDelete.Count == 0)
            {
                return;
            }

            var result = MessageBox.Show(
                $"Delete {itemsToDelete.Count} item(s)?\n\nThis will move them to the Recycle Bin.",
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
            {
                return;
            }

            IsLoading = true;
            StatusMessage = $"Deleting {itemsToDelete.Count} item(s)...";

            try
            {
                foreach (var item in itemsToDelete)
                {
                    if (item.IsDirectory)
                    {
                        // Move to recycle bin using shell
                        Microsoft.VisualBasic.FileIO.FileSystem.DeleteDirectory(
                            item.FullPath,
                            Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                            Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
                    }
                    else
                    {
                        Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(
                            item.FullPath,
                            Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                            Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
                    }
                }

                StatusMessage = $"Deleted {itemsToDelete.Count} item(s)";
                await RefreshAsync();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Delete error: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Start renaming the selected item.
        /// </summary>
        [RelayCommand]
        private void StartRename()
        {
            var item = IsLeftPaneActive ? SelectedLeftItem : SelectedRightItem;
            if (item != null && !item.IsParentDirectory)
            {
                RenamingItem = item;
            }
        }

        /// <summary>
        /// Complete the rename operation.
        /// </summary>
        [RelayCommand]
        private async Task CompleteRenameAsync(string newName)
        {
            if (RenamingItem == null || string.IsNullOrWhiteSpace(newName))
            {
                RenamingItem = null;
                return;
            }

            var oldPath = RenamingItem.FullPath;
            var dir = Path.GetDirectoryName(oldPath) ?? string.Empty;
            var newPath = Path.Combine(dir, newName);

            if (oldPath == newPath)
            {
                RenamingItem = null;
                return;
            }

            try
            {
                if (RenamingItem.IsDirectory)
                {
                    Directory.Move(oldPath, newPath);
                }
                else
                {
                    File.Move(oldPath, newPath);
                }

                StatusMessage = $"Renamed to: {newName}";
                await RefreshAsync();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Rename error: {ex.Message}";
            }
            finally
            {
                RenamingItem = null;
            }
        }

        /// <summary>
        /// Create a new folder in the active pane.
        /// </summary>
        [RelayCommand]
        private async Task NewFolderAsync()
        {
            var basePath = IsLeftPaneActive ? LeftPanePath : RightPanePath;
            var newFolderName = "New Folder";
            var newFolderPath = Path.Combine(basePath, newFolderName);

            // Find unique name
            int counter = 1;
            while (Directory.Exists(newFolderPath))
            {
                newFolderPath = Path.Combine(basePath, $"{newFolderName} ({counter})");
                counter++;
            }

            try
            {
                Directory.CreateDirectory(newFolderPath);
                StatusMessage = $"Created: {Path.GetFileName(newFolderPath)}";
                await RefreshAsync();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error creating folder: {ex.Message}";
            }
        }

        /// <summary>
        /// Switch to a specific drive in the active pane.
        /// </summary>
        [RelayCommand]
        private async Task SwitchDriveAsync(string drivePath)
        {
            if (string.IsNullOrEmpty(drivePath))
            {
                return;
            }

            if (IsLeftPaneActive)
            {
                await NavigateLeftAsync(drivePath);
            }
            else
            {
                await NavigateRightAsync(drivePath);
            }
        }

        /// <summary>
        /// Set the active pane.
        /// </summary>
        [RelayCommand]
        private void SetActivePane(bool isLeft)
        {
            IsLeftPaneActive = isLeft;
        }

        /// <summary>
        /// Open file/folder properties.
        /// </summary>
        [RelayCommand]
        private void ShowProperties()
        {
            var item = IsLeftPaneActive ? SelectedLeftItem : SelectedRightItem;
            if (item == null || item.IsParentDirectory)
            {
                return;
            }

            try
            {
                // Use Windows shell to show properties
                var psi = new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"/select,\"{item.FullPath}\"",
                    UseShellExecute = true,
                };
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
            }
        }

        /// <summary>
        /// Open in Windows Explorer.
        /// </summary>
        [RelayCommand]
        private void OpenInExplorer()
        {
            var path = IsLeftPaneActive ? LeftPanePath : RightPanePath;
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"\"{path}\"",
                    UseShellExecute = true,
                });
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
            }
        }
    }

    /// <summary>
    /// ViewModel for a file/folder item.
    /// </summary>
    public partial class FileItemViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _name = string.Empty;

        [ObservableProperty]
        private string _fullPath = string.Empty;

        [ObservableProperty]
        private bool _isDirectory;

        [ObservableProperty]
        private bool _isParentDirectory;

        [ObservableProperty]
        private long _size;

        [ObservableProperty]
        private DateTime _dateModified;

        [ObservableProperty]
        private string _extension = string.Empty;

        [ObservableProperty]
        private bool _isSelected;

        public string SizeDisplay => IsDirectory ? string.Empty : FormatSize(Size);

        public string Icon => IsParentDirectory ? "ArrowUp" : (IsDirectory ? "Folder" : GetFileIcon(Extension));

        private static string FormatSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            int order = 0;
            double size = bytes;
            while (size >= 1024 && order < sizes.Length - 1)
            {
                order++;
                size /= 1024;
            }

            return $"{size:0.##} {sizes[order]}";
        }

        private static string GetFileIcon(string extension)
        {
            return extension.ToLower() switch
            {
                ".pdf" => "FilePdfBox",
                ".doc" or ".docx" => "FileWordBox",
                ".xls" or ".xlsx" => "FileExcelBox",
                ".ppt" or ".pptx" => "FilePowerpointBox",
                ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" => "FileImageBox",
                ".mp3" or ".wav" or ".flac" => "FileMusicBox",
                ".mp4" or ".mkv" or ".avi" => "FileVideoBox",
                ".zip" or ".rar" or ".7z" => "FolderZip",
                ".exe" => "Application",
                ".py" => "LanguagePython",
                ".cs" => "LanguageCsharp",
                ".js" or ".ts" => "LanguageJavascript",
                ".rs" => "LanguageRust",
                _ => "File",
            };
        }
    }

    /// <summary>
    /// ViewModel for a drive item.
    /// </summary>
    public partial class DriveItemViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _name = string.Empty;

        [ObservableProperty]
        private string _label = string.Empty;

        [ObservableProperty]
        private long _totalSize;

        [ObservableProperty]
        private long _freeSpace;

        [ObservableProperty]
        private string _driveType = string.Empty;

        public long UsedSpace => TotalSize - FreeSpace;

        public double UsedPercentage => TotalSize > 0 ? (double)UsedSpace / TotalSize * 100 : 0;

        public string SpaceDisplay => $"{FormatSize(FreeSpace)} free of {FormatSize(TotalSize)}";

        private static string FormatSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            int order = 0;
            double size = bytes;
            while (size >= 1024 && order < sizes.Length - 1)
            {
                order++;
                size /= 1024;
            }

            return $"{size:0.##} {sizes[order]}";
        }

        // Selection commands (WS-SEL features)
        [RelayCommand]
        private void SelectAll()
        {
            var items = IsLeftPaneActive ? LeftPaneItems : RightPaneItems;
            var selectedItems = IsLeftPaneActive ? SelectedLeftItems : SelectedRightItems;
            
            selectedItems.Clear();
            foreach (var item in items.Where(i => !i.IsParentDirectory))
            {
                selectedItems.Add(item);
            }
            
            StatusMessage = $"Selected {selectedItems.Count} items";
        }

        [RelayCommand]
        private void ClearSelection()
        {
            if (IsLeftPaneActive)
            {
                SelectedLeftItems.Clear();
                SelectedLeftItem = null;
            }
            else
            {
                SelectedRightItems.Clear();
                SelectedRightItem = null;
            }
            StatusMessage = "Selection cleared";
        }

        [RelayCommand]
        private void InvertSelection()
        {
            var items = IsLeftPaneActive ? LeftPaneItems : RightPaneItems;
            var selectedItems = IsLeftPaneActive ? SelectedLeftItems : SelectedRightItems;
            
            var toSelect = items.Where(i => !i.IsParentDirectory && !selectedItems.Contains(i)).ToList();
            var toDeselect = selectedItems.ToList();
            
            foreach (var item in toDeselect)
            {
                selectedItems.Remove(item);
            }
            
            foreach (var item in toSelect)
            {
                selectedItems.Add(item);
            }
            
            StatusMessage = $"Inverted selection: {selectedItems.Count} items";
        }

        /// <summary>
        /// Show advanced selection dialog (WS-SEL features).
        /// </summary>
        [RelayCommand]
        private void ShowAdvancedSelection()
        {
            if (_serviceProvider != null)
            {
                AdvancedSelectionViewModel = _serviceProvider.GetRequiredService<AdvancedSelectionViewModel>();
                
                // Set current items based on active pane
                var currentItems = IsLeftPaneActive ? LeftPaneItems : RightPaneItems;
                var selectedItems = IsLeftPaneActive ? SelectedLeftItems : SelectedRightItems;
                
                AdvancedSelectionViewModel.CurrentItems.Clear();
                foreach (var item in currentItems)
                {
                    AdvancedSelectionViewModel.CurrentItems.Add(item);
                }
                
                AdvancedSelectionViewModel.SelectedItems.Clear();
                foreach (var item in selectedItems)
                {
                    AdvancedSelectionViewModel.SelectedItems.Add(item);
                }
            }
        }

        /// <summary>
        /// Show keyboard shortcuts help (WS-KEY features).
        /// </summary>
        [RelayCommand]
        private void ShowKeyboardHelp()
        {
            if (KeyboardShortcutViewModel != null)
            {
                var shortcuts = KeyboardShortcutViewModel.GetAllShortcuts();
                var message = string.Join("\n", shortcuts.Take(50)); // Show first 50 shortcuts
                
                MessageBox.Show(message, "Keyboard Shortcuts", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        // Navigation commands (keyboard navigation)
        [RelayCommand]
        private void NavigateDown()
        {
            var items = IsLeftPaneActive ? LeftPaneItems : RightPaneItems;
            var current = IsLeftPaneActive ? SelectedLeftItem : SelectedRightItem;
            
            if (items.Count == 0) return;
            
            int currentIndex = current != null ? items.IndexOf(current) : -1;
            int nextIndex = Math.Min(currentIndex + 1, items.Count - 1);
            
            var nextItem = items[nextIndex];
            if (IsLeftPaneActive)
            {
                SelectedLeftItem = nextItem;
            }
            else
            {
                SelectedRightItem = nextItem;
            }
        }

        [RelayCommand]
        private void NavigateUpKey()
        {
            var items = IsLeftPaneActive ? LeftPaneItems : RightPaneItems;
            var current = IsLeftPaneActive ? SelectedLeftItem : SelectedRightItem;
            
            if (items.Count == 0) return;
            
            int currentIndex = current != null ? items.IndexOf(current) : items.Count;
            int prevIndex = Math.Max(currentIndex - 1, 0);
            
            var prevItem = items[prevIndex];
            if (IsLeftPaneActive)
            {
                SelectedLeftItem = prevItem;
            }
            else
            {
                SelectedRightItem = prevItem;
            }
        }

        [RelayCommand]
        private void NavigateLeft()
        {
            // Navigate to parent folder
            NavigateUpCommand.Execute(null);
        }

        [RelayCommand]
        private void NavigateRight()
        {
            // Navigate into selected folder
            var current = IsLeftPaneActive ? SelectedLeftItem : SelectedRightItem;
            if (current != null && current.IsDirectory)
            {
                OpenItemCommand.Execute(current);
            }
        }

        [RelayCommand]
        private void NavigateHome()
        {
            var items = IsLeftPaneActive ? LeftPaneItems : RightPaneItems;
            if (items.Count > 0 && !items[0].IsParentDirectory)
            {
                if (IsLeftPaneActive)
                {
                    SelectedLeftItem = items[0];
                }
                else
                {
                    SelectedRightItem = items[0];
                }
            }
        }

        [RelayCommand]
        private void NavigateEnd()
        {
            var items = IsLeftPaneActive ? LeftPaneItems : RightPaneItems;
            if (items.Count > 0)
            {
                var lastItem = items.LastOrDefault(i => !i.IsParentDirectory);
                if (lastItem != null)
                {
                    if (IsLeftPaneActive)
                    {
                        SelectedLeftItem = lastItem;
                    }
                    else
                    {
                        SelectedRightItem = lastItem;
                    }
                }
            }
        }

        [RelayCommand]
        private void NavigatePageUp()
        {
            // Move selection up by ~20 items
            var items = IsLeftPaneActive ? LeftPaneItems : RightPaneItems;
            var current = IsLeftPaneActive ? SelectedLeftItem : SelectedRightItem;
            
            if (items.Count == 0) return;
            
            int currentIndex = current != null ? items.IndexOf(current) : items.Count;
            int newIndex = Math.Max(currentIndex - 20, 0);
            
            var newItem = items[newIndex];
            if (IsLeftPaneActive)
            {
                SelectedLeftItem = newItem;
            }
            else
            {
                SelectedRightItem = newItem;
            }
        }

        [RelayCommand]
        private void NavigatePageDown()
        {
            // Move selection down by ~20 items
            var items = IsLeftPaneActive ? LeftPaneItems : RightPaneItems;
            var current = IsLeftPaneActive ? SelectedLeftItem : SelectedRightItem;
            
            if (items.Count == 0) return;
            
            int currentIndex = current != null ? items.IndexOf(current) : -1;
            int newIndex = Math.Min(currentIndex + 20, items.Count - 1);
            
            var newItem = items[newIndex];
            if (IsLeftPaneActive)
            {
                SelectedLeftItem = newItem;
            }
            else
            {
                SelectedRightItem = newItem;
            }
        }

        // Additional commands for keyboard shortcuts
        [RelayCommand]
        private void QuickView()
        {
            var item = IsLeftPaneActive ? SelectedLeftItem : SelectedRightItem;
            if (item != null && !item.IsDirectory)
            {
                try
                {
                    // Open file with default viewer in read-only mode
                    var psi = new ProcessStartInfo
                    {
                        FileName = item.FullPath,
                        UseShellExecute = true,
                        Verb = "open"
                    };
                    Process.Start(psi);
                    StatusMessage = $"Quick viewing: {item.Name}";
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Failed to quick view: {ex.Message}";
                }
            }
        }

        [RelayCommand]
        private void EditItem()
        {
            var item = IsLeftPaneActive ? SelectedLeftItem : SelectedRightItem;
            if (item != null && !item.IsDirectory)
            {
                try
                {
                    // Open file with default editor
                    var psi = new ProcessStartInfo
                    {
                        FileName = "notepad.exe",
                        Arguments = $"\"{item.FullPath}\"",
                        UseShellExecute = true
                    };
                    Process.Start(psi);
                    StatusMessage = $"Editing: {item.Name}";
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Failed to edit: {ex.Message}";
                }
            }
        }

        [RelayCommand]
        private void ShowCommandPalette()
        {
            var commands = new System.Text.StringBuilder();
            commands.AppendLine("Available Commands:");
            commands.AppendLine("F2 - Rename");
            commands.AppendLine("F3 - Quick View");
            commands.AppendLine("F4 - Edit");
            commands.AppendLine("F5 - Copy");
            commands.AppendLine("F6 - Move");
            commands.AppendLine("F7 - New Folder");
            commands.AppendLine("Del - Delete");
            commands.AppendLine("Ctrl+A - Select All");
            commands.AppendLine("Ctrl+F - Search");
            commands.AppendLine("Ctrl+T - New Tab");
            commands.AppendLine("Ctrl+W - Close Tab");
            commands.AppendLine("Tab - Switch Panel");

            System.Windows.MessageBox.Show(
                commands.ToString(),
                "Command Palette",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            
            StatusMessage = "Command palette shown";
        }
    }

    /// <summary>
    /// FM-035: Natural string comparer for sorting filenames with numbers.
    /// Sorts "file1, file2, file10" instead of "file1, file10, file2".
    /// </summary>
    public class NaturalStringComparer : IComparer<string>
    {
        public static readonly NaturalStringComparer Instance = new();

        public int Compare(string? x, string? y)
        {
            if (x == null && y == null) return 0;
            if (x == null) return -1;
            if (y == null) return 1;

            int ix = 0, iy = 0;

            while (ix < x.Length && iy < y.Length)
            {
                // Check if both characters are digits
                if (char.IsDigit(x[ix]) && char.IsDigit(y[iy]))
                {
                    // Extract complete numbers from both strings
                    long nx = 0, ny = 0;

                    while (ix < x.Length && char.IsDigit(x[ix]))
                    {
                        nx = nx * 10 + (x[ix] - '0');
                        ix++;
                    }

                    while (iy < y.Length && char.IsDigit(y[iy]))
                    {
                        ny = ny * 10 + (y[iy] - '0');
                        iy++;
                    }

                    if (nx != ny)
                        return nx.CompareTo(ny);
                }
                else
                {
                    // Compare characters case-insensitively
                    int cmp = char.ToLowerInvariant(x[ix]).CompareTo(char.ToLowerInvariant(y[iy]));
                    if (cmp != 0)
                        return cmp;

                    ix++;
                    iy++;
                }
            }

            // If we've exhausted one string, the shorter one comes first
            return x.Length.CompareTo(y.Length);
        }
    }
}
