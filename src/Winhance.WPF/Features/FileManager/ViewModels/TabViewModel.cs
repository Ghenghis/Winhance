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
    /// ViewModel for a single tab.
    /// </summary>
    public partial class TabViewModel : ObservableObject
    {
        private readonly ITabService _tabService;
        private readonly IFileManagerService _fileManagerService;
        private readonly ISelectionService _selectionService;

        [ObservableProperty]
        private string _id = Guid.NewGuid().ToString();

        [ObservableProperty]
        private string _title = "New Tab";

        [ObservableProperty]
        private string _path = string.Empty;

        [ObservableProperty]
        private string _tooltip = string.Empty;

        [ObservableProperty]
        private bool _isActive;

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private bool _isModified;

        [ObservableProperty]
        private bool _isLocked;

        [ObservableProperty]
        private bool _canClose = true;

        [ObservableProperty]
        private TabIcon _icon = TabIcon.Folder;

        [ObservableProperty]
        private ObservableCollection<TabHistoryViewModel> _history = new();

        [ObservableProperty]
        private int _historyIndex = -1;

        [ObservableProperty]
        private FileListViewModel? _content;

        public TabViewModel(
            ITabService tabService,
            IFileManagerService fileManagerService,
            ISelectionService selectionService)
        {
            _tabService = tabService;
            _fileManagerService = fileManagerService;
            _selectionService = selectionService;
        }

        /// <summary>
        /// Initializes the tab with a path.
        /// </summary>
        [RelayCommand]
        public async Task InitializeAsync(string path)
        {
            if (string.IsNullOrEmpty(path)) return;

            IsLoading = true;
            try
            {
                Path = path;
                Title = Path.GetFileName(path) ?? path;
                Tooltip = path;
                Icon = GetIconForPath(path);

                // Add to history
                AddToHistory(path);

                // Load content
                if (Content == null)
                {
                    Content = new FileListViewModel(
                        _fileManagerService,
                        _selectionService,
                        null, // sortingService
                        null, // filterService
                        null, // viewModeService
                        null); // previewService
                }

                await Content.LoadDirectoryAsync(path);
            }
            catch (Exception ex)
            {
                Title = "Error";
                Tooltip = ex.Message;
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Navigates to a new path.
        /// </summary>
        [RelayCommand]
        public async Task NavigateAsync(string path)
        {
            if (string.IsNullOrEmpty(path) || path == Path) return;

            IsLoading = true;
            try
            {
                // Add to history
                AddToHistory(path);

                // Update path
                Path = path;
                Title = Path.GetFileName(path) ?? path;
                Tooltip = path;
                Icon = GetIconForPath(path);

                // Load content
                if (Content != null)
                {
                    await Content.LoadDirectoryAsync(path);
                }
            }
            catch (Exception ex)
            {
                // Handle error
                System.Diagnostics.Debug.WriteLine($"Navigation error: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Navigates back in history.
        /// </summary>
        [RelayCommand]
        public async Task NavigateBackAsync()
        {
            if (CanGoBack && HistoryIndex > 0)
            {
                HistoryIndex--;
                var historyItem = History[HistoryIndex];
                await NavigateToHistoryItem(historyItem);
            }
        }

        /// <summary>
        /// Navigates forward in history.
        /// </summary>
        [RelayCommand]
        public async Task NavigateForwardAsync()
        {
            if (CanGoForward && HistoryIndex < History.Count - 1)
            {
                HistoryIndex++;
                var historyItem = History[HistoryIndex];
                await NavigateToHistoryItem(historyItem);
            }
        }

        /// <summary>
        /// Refreshes the current tab.
        /// </summary>
        [RelayCommand]
        public async Task RefreshAsync()
        {
            if (Content != null)
            {
                await Content.RefreshAsync();
            }
        }

        /// <summary>
        /// Closes the tab.
        /// </summary>
        [RelayCommand]
        public async Task CloseAsync()
        {
            if (!CanClose) return;

            try
            {
                await _tabService.CloseTabAsync(Id);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error closing tab: {ex.Message}");
            }
        }

        /// <summary>
        /// Duplicates the tab.
        /// </summary>
        [RelayCommand]
        public async Task DuplicateAsync()
        {
            try
            {
                await _tabService.DuplicateTabAsync(Id);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error duplicating tab: {ex.Message}");
            }
        }

        /// <summary>
        /// Locks or unlocks the tab.
        /// </summary>
        [RelayCommand]
        public void ToggleLock()
        {
            IsLocked = !IsLocked;
            CanClose = !IsLocked;
            Title = IsLocked ? $"🔒 {Path.GetFileName(Path) ?? Path}" : Path.GetFileName(Path) ?? Path;
        }

        /// <summary>
        /// Shows tab context menu.
        /// </summary>
        [RelayCommand]
        public void ShowContextMenu()
        {
            var contextMenu = new System.Windows.Controls.ContextMenu();
            
            contextMenu.Items.Add(CreateMenuItem("Close", CloseCommand));
            contextMenu.Items.Add(CreateMenuItem("Close Others", null));
            contextMenu.Items.Add(CreateMenuItem("Close All", null));
            contextMenu.Items.Add(new System.Windows.Controls.Separator());
            contextMenu.Items.Add(CreateMenuItem("Duplicate Tab", null));
            contextMenu.Items.Add(CreateMenuItem(IsPinned ? "Unpin" : "Pin", null));
            contextMenu.Items.Add(new System.Windows.Controls.Separator());
            contextMenu.Items.Add(CreateMenuItem("Open in New Window", OpenInNewWindowCommand));
            contextMenu.Items.Add(CreateMenuItem("Copy Path", CopyPathCommand));
            
            contextMenu.IsOpen = true;
        }
        
        private System.Windows.Controls.MenuItem CreateMenuItem(string header, System.Windows.Input.ICommand command)
        {
            return new System.Windows.Controls.MenuItem
            {
                Header = header,
                Command = command
            };
        }

        /// <summary>
        /// Copies tab path to clipboard.
        /// </summary>
        [RelayCommand]
        public async Task CopyPathAsync()
        {
            try
            {
                System.Windows.Clipboard.SetText(Path);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error copying path: {ex.Message}");
            }
        }

        /// <summary>
        /// Opens parent directory.
        /// </summary>
        [RelayCommand]
        public async Task OpenParentAsync()
        {
            var parent = Directory.GetParent(Path);
            if (parent != null)
            {
                await NavigateAsync(parent.FullName);
            }
        }

        /// <summary>
        /// Opens tab in new window.
        /// </summary>
        [RelayCommand]
        public async Task OpenInNewWindowAsync()
        {
            try
            {
                var newWindow = new System.Windows.Window
                {
                    Title = Title,
                    Width = 1200,
                    Height = 800,
                    WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen
                };
                
                await Task.CompletedTask;
                System.Diagnostics.Debug.WriteLine($"Opening tab '{Title}' in new window");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error opening in new window: {ex.Message}");
            }
        }

        /// <summary>
        /// Adds path to navigation history.
        /// </summary>
        private void AddToHistory(string path)
        {
            // Remove any future history if we're not at the end
            if (HistoryIndex < History.Count - 1)
            {
                for (int i = History.Count - 1; i > HistoryIndex; i--)
                {
                    History.RemoveAt(i);
                }
            }

            // Don't add duplicates
            if (History.LastOrDefault()?.Path == path)
            {
                return;
            }

            // Add new history item
            var historyItem = new TabHistoryViewModel
            {
                Path = path,
                Title = Path.GetFileName(path) ?? path,
                Timestamp = DateTime.Now
            };

            History.Add(historyItem);
            HistoryIndex = History.Count - 1;

            // Limit history size
            while (History.Count > 100)
            {
                History.RemoveAt(0);
                HistoryIndex--;
            }
        }

        /// <summary>
        /// Navigates to a history item.
        /// </summary>
        private async Task NavigateToHistoryItem(TabHistoryViewModel historyItem)
        {
            Path = historyItem.Path;
            Title = historyItem.Title;
            Tooltip = historyItem.Path;
            Icon = GetIconForPath(historyItem.Path);

            if (Content != null)
            {
                await Content.LoadDirectoryAsync(historyItem.Path);
            }
        }

        /// <summary>
        /// Gets icon for path.
        /// </summary>
        private static TabIcon GetIconForPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return TabIcon.Folder;

            return path.ToLowerInvariant() switch
            {
                var p when p.Contains("desktop") => TabIcon.Desktop,
                var p when p.Contains("documents") => TabIcon.Documents,
                var p when p.Contains("downloads") => TabIcon.Downloads,
                var p when p.Contains("pictures") => TabIcon.Pictures,
                var p when p.Contains("music") => TabIcon.Music,
                var p when p.Contains("videos") => TabIcon.Videos,
                var p when p.Contains("network") => TabIcon.Network,
                _ => TabIcon.Folder
            };
        }

        /// <summary>
        /// Handles property changes.
        /// </summary>
        partial void OnIsActiveChanged(bool value)
        {
            if (value && Content != null)
            {
                if (Content is IActivatable activatable)
                {
                    activatable.OnActivated();
                }
                System.Diagnostics.Debug.WriteLine($"Tab '{Title}' activated");
            }
        }

        // Navigation properties
        public bool CanGoBack => HistoryIndex > 0;
        public bool CanGoForward => HistoryIndex >= 0 && HistoryIndex < History.Count - 1;

        // Display properties
        public string DisplayTitle => IsLocked ? $"🔒 {Title}" : Title;
        public string DisplayTooltip => $"{Tooltip}\nCreated: {DateTime.Now:yyyy-MM-dd HH:mm}";
    }

    /// <summary>
    /// ViewModel for tab history item.
    /// </summary>
    public partial class TabHistoryViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _path = string.Empty;

        [ObservableProperty]
        private string _title = string.Empty;

        [ObservableProperty]
        private DateTime _timestamp;
    }

    /// <summary>
    /// Tab icon enumeration.
    /// </summary>
    public enum TabIcon
    {
        Folder,
        Desktop,
        Documents,
        Downloads,
        Pictures,
        Music,
        Videos,
        Network,
        Drive,
        Home,
        Star
    }
}
