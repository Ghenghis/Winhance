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
    /// ViewModel for navigation panel and history.
    /// </summary>
    public partial class NavigationViewModel : ObservableObject
    {
        private readonly IAddressBarService _addressBarService;
        private readonly ITabService _tabService;
        private readonly IFavoritesService _favoritesService;
        private readonly IFileManagerService _fileManagerService;

        [ObservableProperty]
        private ObservableCollection<NavigationItemViewModel> _navigationTree = new();

        [ObservableProperty]
        private ObservableCollection<NavigationHistoryViewModel> _forwardHistory = new();

        [ObservableProperty]
        private ObservableCollection<NavigationHistoryViewModel> _backHistory = new();

        [ObservableProperty]
        private NavigationHistoryViewModel? _currentLocation;

        [ObservableProperty]
        private NavigationItemViewModel? _selectedNode;

        [ObservableProperty]
        private string _currentPath = string.Empty;

        [ObservableProperty]
        private bool _showHiddenDrives = false;

        [ObservableProperty]
        private bool _showNetworkLocations = true;

        [ObservableProperty]
        private bool _isExpanded = true;

        [ObservableProperty]
        private bool _isLoading;

        public NavigationViewModel(
            IAddressBarService addressBarService,
            ITabService tabService,
            IFavoritesService favoritesService,
            IFileManagerService fileManagerService)
        {
            _addressBarService = addressBarService;
            _tabService = tabService;
            _favoritesService = favoritesService;
            _fileManagerService = fileManagerService;

            // Initialize navigation
            _ = InitializeAsync();
        }

        /// <summary>
        /// Initializes the navigation tree.
        /// </summary>
        [RelayCommand]
        public async Task InitializeAsync()
        {
            IsLoading = true;
            try
            {
                await LoadDrivesAsync();
                await LoadSpecialFoldersAsync();
                await LoadNetworkLocationsAsync();
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Refreshes the navigation tree.
        /// </summary>
        [RelayCommand]
        public async Task RefreshAsync()
        {
            await InitializeAsync();
        }

        /// <summary>
        /// Navigates to selected node.
        /// </summary>
        [RelayCommand]
        public async Task NavigateToNodeAsync(NavigationItemViewModel? node)
        {
            if (node?.FullPath == null) return;

            if (!node.IsAccessible)
            {
                await _addressBarService.ShowErrorAsync("Location is not accessible");
                return;
            }

            try
            {
                // Add to history
                AddToHistory(CurrentPath, node.FullPath);
                
                // Navigate
                CurrentPath = node.FullPath;
                CurrentLocation = new NavigationHistoryViewModel
                {
                    Path = node.FullPath,
                    Name = node.Name,
                    Icon = node.Icon,
                    Timestamp = DateTime.Now
                };

                await _addressBarService.NavigateAsync(node.FullPath);
                
                // Update access count
                node.AccessCount++;
                node.LastAccessed = DateTime.Now;
            }
            catch (Exception ex)
            {
                await _addressBarService.ShowErrorAsync($"Cannot navigate to {node.Name}: {ex.Message}");
            }
        }

        /// <summary>
        /// Navigates back in history.
        /// </summary>
        [RelayCommand]
        public async Task NavigateBackAsync()
        {
            if (BackHistory.Count == 0) return;

            var previous = BackHistory.Last();
            BackHistory.Remove(previous);
            
            // Add current to forward history
            if (CurrentLocation != null)
            {
                ForwardHistory.Insert(0, CurrentLocation);
            }

            await NavigateToPathAsync(previous.Path);
        }

        /// <summary>
        /// Navigates forward in history.
        /// </summary>
        [RelayCommand]
        public async Task NavigateForwardAsync()
        {
            if (ForwardHistory.Count == 0) return;

            var next = ForwardHistory.First();
            ForwardHistory.Remove(next);
            
            // Add current to back history
            if (CurrentLocation != null)
            {
                BackHistory.Add(CurrentLocation);
            }

            await NavigateToPathAsync(next.Path);
        }

        /// <summary>
        /// Navigates up one level.
        /// </summary>
        [RelayCommand]
        public async Task NavigateUpAsync()
        {
            var parent = Directory.GetParent(CurrentPath);
            if (parent != null)
            {
                await NavigateToPathAsync(parent.FullName);
            }
        }

        /// <summary>
        /// Navigates to home directory.
        /// </summary>
        [RelayCommand]
        public async Task NavigateHomeAsync()
        {
            await NavigateToPathAsync(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        }

        /// <summary>
        /// Opens node in new tab.
        /// </summary>
        [RelayCommand]
        public async Task OpenInNewTabAsync(NavigationItemViewModel? node)
        {
            if (node?.FullPath == null) return;

            try
            {
                await _tabService.CreateTabAsync(node.FullPath);
                node.AccessCount++;
            }
            catch (Exception ex)
            {
                await _addressBarService.ShowErrorAsync($"Cannot open in new tab: {ex.Message}");
            }
        }

        /// <summary>
        /// Expands the node to show children.
        /// </summary>
        [RelayCommand]
        public async Task ExpandNodeAsync(NavigationItemViewModel? node)
        {
            if (node == null || node.IsExpanded || node.Children.Count > 0) return;

            try
            {
                IsLoading = true;
                await LoadNodeChildrenAsync(node);
                node.IsExpanded = true;
            }
            catch (Exception ex)
            {
                await _addressBarService.ShowErrorAsync($"Cannot expand {node.Name}: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Collapses the node.
        /// </summary>
        [RelayCommand]
        public void CollapseNode(NavigationItemViewModel? node)
        {
            if (node != null)
            {
                node.IsExpanded = false;
            }
        }

        /// <summary>
        /// Shows context menu for node.
        /// </summary>
        [RelayCommand]
        public async Task ShowNodeContextMenuAsync(NavigationItemViewModel? node)
        {
            if (node == null) return;

            var menu = new System.Text.StringBuilder();
            menu.AppendLine($"Path: {node.FullPath}");
            menu.AppendLine();
            menu.AppendLine("Available actions:");
            menu.AppendLine("\u2022 Double-click to open");
            menu.AppendLine("\u2022 Right-click \u2192 Open in New Tab");
            menu.AppendLine("\u2022 Right-click \u2192 Add to Favorites");
            menu.AppendLine("\u2022 Right-click \u2192 Properties");

            System.Windows.MessageBox.Show(
                menu.ToString(),
                $"Context Menu: {node.Name}",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
        }

        /// <summary>
        /// Loads all drives.
        /// </summary>
        private async Task LoadDrivesAsync()
        {
            var drivesGroup = new NavigationItemViewModel
            {
                Name = "This PC",
                Icon = "🖥️",
                NodeType = NavigationNodeType.Group,
                IsExpanded = true
            };

            try
            {
                var drives = await _fileManagerService.GetDrivesAsync();
                foreach (var drive in drives)
                {
                    if (!ShowHiddenDrives && drive.DriveType == DriveType.Ram) continue;

                    var driveNode = new NavigationItemViewModel
                    {
                        Name = string.IsNullOrEmpty(drive.Label) ? drive.Name : $"{drive.Name} ({drive.Label})",
                        FullPath = drive.Name,
                        Icon = GetDriveIcon(drive.DriveType),
                        NodeType = NavigationNodeType.Drive,
                        IsAccessible = drive.IsReady,
                        Size = drive.TotalSize,
                        FreeSpace = drive.FreeSpace,
                        DriveType = drive.DriveType.ToString()
                    };

                    if (drive.IsReady)
                    {
                        // Load root directories
                        await LoadNodeChildrenAsync(driveNode);
                    }

                    drivesGroup.Children.Add(driveNode);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading drives: {ex.Message}");
            }

            NavigationTree.Insert(0, drivesGroup);
        }

        /// <summary>
        /// Loads special folders (Desktop, Documents, etc.).
        /// </summary>
        private async Task LoadSpecialFoldersAsync()
        {
            var specialGroup = new NavigationItemViewModel
            {
                Name = "Quick Access",
                Icon = "⭐",
                NodeType = NavigationNodeType.Group,
                IsExpanded = true
            };

            var specialFolders = new[]
            {
                (Environment.SpecialFolder.Desktop, "Desktop", "🖥️"),
                (Environment.SpecialFolder.Documents, "Documents", "📄"),
                (Environment.SpecialFolder.Downloads, "Downloads", "⬇️"),
                (Environment.SpecialFolder.Pictures, "Pictures", "🖼️"),
                (Environment.SpecialFolder.Music, "Music", "🎵"),
                (Environment.SpecialFolder.Videos, "Videos", "🎬"),
                (Environment.SpecialFolder.Recent, "Recent", "🕐")
            };

            foreach (var (folder, name, icon) in specialFolders)
            {
                try
                {
                    var path = Environment.GetFolderPath(folder);
                    if (Directory.Exists(path))
                    {
                        var folderNode = new NavigationItemViewModel
                        {
                            Name = name,
                            FullPath = path,
                            Icon = icon,
                            NodeType = NavigationNodeType.Folder,
                            IsAccessible = true
                        };

                        specialGroup.Children.Add(folderNode);
                    }
                }
                catch
                {
                    // Skip inaccessible folders
                }
            }

            NavigationTree.Add(specialGroup);
        }

        /// <summary>
        /// Loads network locations.
        /// </summary>
        private async Task LoadNetworkLocationsAsync()
        {
            if (!ShowNetworkLocations) return;

            var networkGroup = new NavigationItemViewModel
            {
                Name = "Network",
                Icon = "🌐",
                NodeType = NavigationNodeType.Group,
                IsExpanded = false
            };

            try
            {
                // Add network computers
                // This would require network enumeration
                // For now, just add a placeholder
                networkGroup.Children.Add(new NavigationItemViewModel
                {
                    Name = "Entire Network",
                    FullPath = "\\\\",
                    Icon = "🌐",
                    NodeType = NavigationNodeType.Network,
                    IsAccessible = true
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading network: {ex.Message}");
            }

            NavigationTree.Add(networkGroup);
        }

        /// <summary>
        /// Loads children for a node.
        /// </summary>
        private async Task LoadNodeChildrenAsync(NavigationItemViewModel node)
        {
            if (string.IsNullOrEmpty(node.FullPath) || !Directory.Exists(node.FullPath))
                return;

            try
            {
                var directories = await _fileManagerService.GetDirectoriesAsync(node.FullPath);
                
                foreach (var dir in directories.Take(100)) // Limit to 100 for performance
                {
                    var childNode = new NavigationItemViewModel
                    {
                        Name = Path.GetFileName(dir),
                        FullPath = dir,
                        Icon = "📁",
                        NodeType = NavigationNodeType.Folder,
                        IsAccessible = true,
                        Parent = node
                    };

                    node.Children.Add(childNode);
                }
            }
            catch (UnauthorizedAccessException)
            {
                node.IsAccessible = false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading children for {node.FullPath}: {ex.Message}");
            }
        }

        /// <summary>
        /// Navigates to a specific path.
        /// </summary>
        private async Task NavigateToPathAsync(string path)
        {
            CurrentPath = path;
            CurrentLocation = new NavigationHistoryViewModel
            {
                Path = path,
                Name = Path.GetFileName(path) ?? path,
                Icon = GetIconForPath(path),
                Timestamp = DateTime.Now
            };

            await _addressBarService.NavigateAsync(path);
        }

        /// <summary>
        /// Adds navigation to history.
        /// </summary>
        private void AddToHistory(string fromPath, string toPath)
        {
            if (!string.IsNullOrEmpty(fromPath))
            {
                BackHistory.Add(new NavigationHistoryViewModel
                {
                    Path = fromPath,
                    Name = Path.GetFileName(fromPath) ?? fromPath,
                    Icon = GetIconForPath(fromPath),
                    Timestamp = DateTime.Now
                });
            }

            // Limit history size
            while (BackHistory.Count > 50)
            {
                BackHistory.RemoveAt(0);
            }

            ForwardHistory.Clear();
        }

        /// <summary>
        /// Gets icon for drive type.
        /// </summary>
        private static string GetDriveIcon(DriveType driveType)
        {
            return driveType switch
            {
                DriveType.Fixed => "💾",
                DriveType.Removable => "💿",
                DriveType.Network => "🌐",
                DriveType.CDRom => "💽",
                DriveType.Ram => "🧠",
                _ => "💾"
            };
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
                var p when p.Contains("pictures") => "🖼️",
                var p when p.Contains("music") => "🎵",
                var p when p.Contains("videos") => "🎬",
                _ => "📁"
            };
        }

        /// <summary>
        /// Handles property changes.
        /// </summary>
        partial void OnShowHiddenDrivesChanged(bool value)
        {
            _ = RefreshAsync();
        }

        partial void OnShowNetworkLocationsChanged(bool value)
        {
            _ = RefreshAsync();
        }
    }

    /// <summary>
    /// ViewModel for navigation tree item.
    /// </summary>
    public partial class NavigationItemViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _name = string.Empty;

        [ObservableProperty]
        private string _fullPath = string.Empty;

        [ObservableProperty]
        private string _icon = "📁";

        [ObservableProperty]
        private NavigationNodeType _nodeType;

        [ObservableProperty]
        private bool _isExpanded;

        [ObservableProperty]
        private bool _isAccessible = true;

        [ObservableProperty]
        private long _size;

        [ObservableProperty]
        private long _freeSpace;

        [ObservableProperty]
        private string _driveType = string.Empty;

        [ObservableProperty]
        private int _accessCount;

        [ObservableProperty]
        private DateTime _lastAccessed = DateTime.Now;

        [ObservableProperty]
        private ObservableCollection<NavigationItemViewModel> _children = new();

        [ObservableProperty]
        private NavigationItemViewModel? _parent;

        public string SizeFormatted => FormatSize(Size);
        public string FreeSpaceFormatted => FormatSize(FreeSpace);
        public string ToolTip => $"{FullPath}\nType: {NodeType}\nSize: {SizeFormatted}\nFree: {FreeSpaceFormatted}\nAccessed: {LastAccessed:yyyy-MM-dd HH:mm}";

        private static string FormatSize(long bytes)
        {
            if (bytes == 0) return "0 B";
            
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
    }

    /// <summary>
    /// ViewModel for navigation history.
    /// </summary>
    public partial class NavigationHistoryViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _path = string.Empty;

        [ObservableProperty]
        private string _name = string.Empty;

        [ObservableProperty]
        private string _icon = "📁";

        [ObservableProperty]
        private DateTime _timestamp;
    }

    /// <summary>
    /// Navigation node types.
    /// </summary>
    public enum NavigationNodeType
    {
        Group,
        Drive,
        Folder,
        Network,
        Special
    }
}
