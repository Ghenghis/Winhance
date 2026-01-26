using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Winhance.Core.Features.FileManager.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Winhance.WPF.Features.FileManager.ViewModels
{
    /// <summary>
    /// ViewModel for the reusable FileListControl.
    /// </summary>
    public partial class FileListViewModel : ObservableObject
    {
        private readonly IFileManagerService? _fileManagerService;
        private readonly ISelectionService? _selectionService;
        private readonly ISortingService? _sortingService;
        private readonly IViewModeService? _viewModeService;
        private readonly IServiceProvider? _serviceProvider;

        [ObservableProperty]
        private ObservableCollection<FileItemViewModel> _items = new();

        [ObservableProperty]
        private ObservableCollection<FileItemViewModel> _filteredItems = new();

        [ObservableProperty]
        private ObservableCollection<FileItemViewModel> _selectedItems = new();

        [ObservableProperty]
        private FileItemViewModel? _selectedItem;

        [ObservableProperty]
        private string _currentPath = string.Empty;

        [ObservableProperty]
        private string _searchText = string.Empty;

        [ObservableProperty]
        private bool _hasSearchText;

        [ObservableProperty]
        private string _viewMode = "Details";

        [ObservableProperty]
        private string _sortColumn = "Name";

        [ObservableProperty]
        private bool _sortAscending = true;

        [ObservableProperty]
        private string _statusMessage = "Ready";

        [ObservableProperty]
        private bool _showToolbar = true;

        [ObservableProperty]
        private bool _showStatusBar = true;

        [ObservableProperty]
        private ObservableCollection<ContextMenuItemViewModel> _contextMenuItems = new();

        public int ItemCount => FilteredItems.Count;
        public int SelectedCount => SelectedItems.Count;

        public FileListViewModel(IFileManagerService? fileManagerService,
                                ISelectionService? selectionService,
                                ISortingService? sortingService,
                                IViewModeService? viewModeService,
                                IServiceProvider? serviceProvider)
        {
            _fileManagerService = fileManagerService;
            _selectionService = selectionService;
            _sortingService = sortingService;
            _viewModeService = viewModeService;
            _serviceProvider = serviceProvider;

            // Initialize context menu
            if (_serviceProvider != null)
            {
                var contextMenuViewModel = _serviceProvider.GetRequiredService<ContextMenuViewModel>();
                ContextMenuItems = contextMenuViewModel.MenuItems;
            }

            // Setup property change handlers
            PropertyChanged += FileListViewModel_PropertyChanged;
        }

        private void FileListViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(SearchText):
                    HasSearchText = !string.IsNullOrEmpty(SearchText);
                    ApplyFilter();
                    break;
                case nameof(SelectedItems):
                    OnPropertyChanged(nameof(SelectedCount));
                    UpdateStatusMessage();
                    break;
                case nameof(FilteredItems):
                    OnPropertyChanged(nameof(ItemCount));
                    UpdateStatusMessage();
                    break;
            }
        }

        private void UpdateStatusMessage()
        {
            if (SelectedCount > 0)
            {
                StatusMessage = $"{SelectedCount} of {ItemCount} items selected";
            }
            else if (HasSearchText)
            {
                StatusMessage = $"{ItemCount} items (filtered)";
            }
            else
            {
                StatusMessage = $"{ItemCount} items";
            }
        }

        /// <summary>
        /// Load files for the specified path.
        /// </summary>
        public async Task LoadPathAsync(string path)
        {
            CurrentPath = path;
            
            try
            {
                IsLoading = true;
                StatusMessage = "Loading...";

                if (_fileManagerService != null)
                {
                    var files = await _fileManagerService.GetDirectoryContentsAsync(path);
                    
                    Items.Clear();
                    foreach (var file in files)
                    {
                        Items.Add(new FileItemViewModel(file));
                    }

                    ApplySorting();
                    ApplyFilter();
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Refresh the current directory.
        /// </summary>
        [RelayCommand]
        private async Task RefreshAsync()
        {
            if (!string.IsNullOrEmpty(CurrentPath))
            {
                await LoadPathAsync(CurrentPath);
            }
        }

        /// <summary>
        /// Navigate to parent directory.
        /// </summary>
        [RelayCommand]
        private async Task NavigateUpAsync()
        {
            var parentPath = System.IO.Path.GetDirectoryName(CurrentPath);
            if (!string.IsNullOrEmpty(parentPath))
            {
                await LoadPathAsync(parentPath);
            }
        }

        /// <summary>
        /// Navigate back in history.
        /// </summary>
        [RelayCommand]
        private void NavigateBack()
        {
            // Implementation for navigation history
        }

        /// <summary>
        /// Set the view mode.
        /// </summary>
        [RelayCommand]
        private void SetViewMode(string mode)
        {
            ViewMode = mode;
            _viewModeService?.SetViewMode(mode);
        }

        /// <summary>
        /// Toggle sort order.
        /// </summary>
        [RelayCommand]
        private void ToggleSortOrder()
        {
            SortAscending = !SortAscending;
            ApplySorting();
        }

        /// <summary>
        /// Show sort options dialog.
        /// </summary>
        [RelayCommand]
        private void ShowSortOptions()
        {
            // Implementation for sort options dialog
        }

        /// <summary>
        /// Clear search text.
        /// </summary>
        [RelayCommand]
        private void ClearSearch()
        {
            SearchText = string.Empty;
        }

        /// <summary>
        /// Select all items.
        /// </summary>
        [RelayCommand]
        private void SelectAll()
        {
            SelectedItems.Clear();
            foreach (var item in FilteredItems.Where(i => !i.IsParentDirectory))
            {
                SelectedItems.Add(item);
            }
        }

        /// <summary>
        /// Clear selection.
        /// </summary>
        [RelayCommand]
        private void ClearSelection()
        {
            SelectedItems.Clear();
            SelectedItem = null;
        }

        /// <summary>
        /// Invert selection.
        /// </summary>
        [RelayCommand]
        private void InvertSelection()
        {
            var toSelect = FilteredItems.Where(i => !i.IsParentDirectory && !SelectedItems.Contains(i)).ToList();
            var toDeselect = SelectedItems.ToList();

            foreach (var item in toDeselect)
            {
                SelectedItems.Remove(item);
            }

            foreach (var item in toSelect)
            {
                SelectedItems.Add(item);
            }
        }

        /// <summary>
        /// Sort by column.
        /// </summary>
        public void SortByColumn(string columnName)
        {
            if (SortColumn == columnName)
            {
                SortAscending = !SortAscending;
            }
            else
            {
                SortColumn = columnName;
                SortAscending = true;
            }

            ApplySorting();
        }

        private void ApplySorting()
        {
            if (_sortingService != null)
            {
                var sorted = _sortingService.SortItems(Items, SortColumn, SortAscending);
                
                Items.Clear();
                foreach (var item in sorted)
                {
                    Items.Add(item);
                }
            }
        }

        private void ApplyFilter()
        {
            FilteredItems.Clear();

            if (string.IsNullOrEmpty(SearchText))
            {
                foreach (var item in Items)
                {
                    FilteredItems.Add(item);
                }
            }
            else
            {
                var searchLower = SearchText.ToLowerInvariant();
                var filtered = Items.Where(i => 
                    i.Name.ToLowerInvariant().Contains(searchLower) ||
                    i.FullPath.ToLowerInvariant().Contains(searchLower));

                foreach (var item in filtered)
                {
                    FilteredItems.Add(item);
                }
            }
        }

        [ObservableProperty]
        private bool _isLoading;
    }
}
