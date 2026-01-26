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
    /// ViewModel for breadcrumb navigation.
    /// </summary>
    public partial class BreadcrumbViewModel : ObservableObject
    {
        private readonly IAddressBarService _addressBarService;
        private readonly ITabService _tabService;

        [ObservableProperty]
        private ObservableCollection<BreadcrumbItemViewModel> _breadcrumbs = new();

        [ObservableProperty]
        private BreadcrumbItemViewModel? _selectedBreadcrumb;

        [ObservableProperty]
        private string _currentPath = string.Empty;

        [ObservableProperty]
        private bool _showDriveLetters = true;

        [ObservableProperty]
        private bool _showFullPath = false;

        public BreadcrumbViewModel(IAddressBarService addressBarService, ITabService tabService)
        {
            _addressBarService = addressBarService;
            _tabService = tabService;
        }

        /// <summary>
        /// Updates the breadcrumbs for the given path.
        /// </summary>
        public void UpdatePath(string path)
        {
            if (string.IsNullOrEmpty(path)) return;

            CurrentPath = path;
            var breadcrumbItems = new ObservableCollection<BreadcrumbItemViewModel>();

            try
            {
                // Add root/drive
                var root = Path.GetPathRoot(path);
                if (!string.IsNullOrEmpty(root))
                {
                    breadcrumbItems.Add(new BreadcrumbItemViewModel
                    {
                        Name = root.TrimEnd('\\'),
                        FullPath = root,
                        IsRoot = true,
                        IsDrive = root.Length == 3 && root[1] == ':'
                    });
                }

                // Add path segments
                var segments = GetPathSegments(path);
                var currentPath = root;

                foreach (var segment in segments)
                {
                    if (string.IsNullOrEmpty(segment)) continue;

                    currentPath = Path.Combine(currentPath, segment);
                    var displayName = ShowFullPath ? currentPath : segment;

                    breadcrumbItems.Add(new BreadcrumbItemViewModel
                    {
                        Name = displayName,
                        FullPath = currentPath,
                        IsRoot = false,
                        IsDrive = false
                    });
                }

                Breadcrumbs = breadcrumbItems;
            }
            catch (Exception ex)
            {
                // Handle invalid path
                Breadcrumbs = new ObservableCollection<BreadcrumbItemViewModel>
                {
                    new BreadcrumbItemViewModel
                    {
                        Name = "Invalid Path",
                        FullPath = path,
                        IsRoot = true,
                        IsDrive = false
                    }
                };
            }
        }

        /// <summary>
        /// Navigates to the selected breadcrumb.
        /// </summary>
        [RelayCommand]
        public async Task NavigateToBreadcrumbAsync(BreadcrumbItemViewModel? breadcrumb)
        {
            if (breadcrumb?.FullPath == null) return;

            try
            {
                SelectedBreadcrumb = breadcrumb;
                await _addressBarService.NavigateAsync(breadcrumb.FullPath);
            }
            catch (Exception ex)
            {
                await _addressBarService.ShowErrorAsync($"Cannot navigate to {breadcrumb.Name}: {ex.Message}");
            }
        }

        /// <summary>
        /// Shows context menu for breadcrumb.
        /// </summary>
        [RelayCommand]
        public async Task ShowBreadcrumbContextMenuAsync(BreadcrumbItemViewModel? breadcrumb)
        {
            if (breadcrumb == null) return;

            var menu = new System.Text.StringBuilder();
            menu.AppendLine($"Path: {breadcrumb.FullPath}");
            menu.AppendLine();
            menu.AppendLine("Available actions:");
            menu.AppendLine("• Copy Path (Ctrl+C)");
            menu.AppendLine("• Open in New Tab (Ctrl+T)");
            menu.AppendLine("• Open in File Explorer");
            menu.AppendLine("• Add to Favorites");

            System.Windows.MessageBox.Show(
                menu.ToString(),
                $"Breadcrumb: {breadcrumb.Name}",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
            await Task.CompletedTask;
        }

        /// <summary>
        /// Copies the breadcrumb path to clipboard.
        /// </summary>
        [RelayCommand]
        public async Task CopyBreadcrumbPathAsync(BreadcrumbItemViewModel? breadcrumb)
        {
            if (breadcrumb?.FullPath == null) return;

            try
            {
                System.Windows.Clipboard.SetText(breadcrumb.FullPath);
                await _addressBarService.ShowMessageAsync("Path copied to clipboard");
            }
            catch (Exception ex)
            {
                await _addressBarService.ShowErrorAsync($"Cannot copy path: {ex.Message}");
            }
        }

        /// <summary>
        /// Opens the breadcrumb in a new tab.
        /// </summary>
        [RelayCommand]
        public async Task OpenInNewTabAsync(BreadcrumbItemViewModel? breadcrumb)
        {
            if (breadcrumb?.FullPath == null) return;

            try
            {
                await _tabService.CreateTabAsync(breadcrumb.FullPath);
            }
            catch (Exception ex)
            {
                await _addressBarService.ShowErrorAsync($"Cannot open in new tab: {ex.Message}");
            }
        }

        /// <summary>
        /// Opens the breadcrumb in file explorer.
        /// </summary>
        [RelayCommand]
        public async Task OpenInExplorerAsync(BreadcrumbItemViewModel? breadcrumb)
        {
            if (breadcrumb?.FullPath == null) return;

            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = breadcrumb.FullPath,
                    UseShellExecute = true
                };
                System.Diagnostics.Process.Start(psi);
            }
            catch (Exception ex)
            {
                await _addressBarService.ShowErrorAsync($"Cannot open in explorer: {ex.Message}");
            }
        }

        /// <summary>
        /// Toggles between showing full path and just folder names.
        /// </summary>
        [RelayCommand]
        public void TogglePathDisplay()
        {
            ShowFullPath = !ShowFullPath;
            UpdatePath(CurrentPath);
        }

        /// <summary>
        /// Refreshes the current breadcrumb display.
        /// </summary>
        [RelayCommand]
        public void Refresh()
        {
            UpdatePath(CurrentPath);
        }

        /// <summary>
        /// Gets path segments from full path.
        /// </summary>
        private static string[] GetPathSegments(string path)
        {
            var root = Path.GetPathRoot(path);
            if (!string.IsNullOrEmpty(root))
            {
                path = path.Substring(root.Length);
            }

            return path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar, 
                StringSplitOptions.RemoveEmptyEntries);
        }

        /// <summary>
        /// Handles property changes.
        /// </summary>
        partial void OnShowFullPathChanged(bool value)
        {
            UpdatePath(CurrentPath);
        }
    }

    /// <summary>
    /// ViewModel for a single breadcrumb item.
    /// </summary>
    public partial class BreadcrumbItemViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _name = string.Empty;

        [ObservableProperty]
        private string _fullPath = string.Empty;

        [ObservableProperty]
        private bool _isRoot;

        [ObservableProperty]
        private bool _isDrive;

        [ObservableProperty]
        private bool _isSelected;

        public string ToolTip => FullPath;
        public bool CanNavigate => Directory.Exists(FullPath);
    }
}
