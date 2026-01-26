using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Winhance.Core.Features.FileManager.Interfaces;

namespace Winhance.WPF.Features.FileManager.ViewModels
{
    /// <summary>
    /// ViewModel for the BreadcrumbBar control.
    /// </summary>
    public partial class BreadcrumbBarViewModel : ObservableObject
    {
        private readonly IAddressBarService? _addressBarService;
        private readonly IFavoritesService? _favoritesService;

        [ObservableProperty]
        private string _currentPath = string.Empty;

        [ObservableProperty]
        private ObservableCollection<BreadcrumbSegment> _segments = new();

        [ObservableProperty]
        private bool _isEditing;

        [ObservableProperty]
        private string _editText = string.Empty;

        [ObservableProperty]
        private ObservableCollection<PathSuggestion> _suggestions = new();

        [ObservableProperty]
        private ObservableCollection<PathSuggestion> _recentLocations = new();

        [ObservableProperty]
        private string _driveIcon = "Harddisk";

        public BreadcrumbBarViewModel(IAddressBarService? addressBarService, IFavoritesService? favoritesService)
        {
            _addressBarService = addressBarService;
            _favoritesService = favoritesService;

            // Load recent locations
            LoadRecentLocations();
        }

        /// <summary>
        /// Set the current path and update breadcrumbs.
        /// </summary>
        public async Task SetPathAsync(string path)
        {
            CurrentPath = path;
            UpdateSegments(path);
            UpdateDriveIcon(path);
            
            // Add to recent locations
            await AddToRecentAsync(path);
        }

        /// <summary>
        /// Navigate to a breadcrumb segment.
        /// </summary>
        [RelayCommand]
        private async Task NavigateToSegmentAsync(BreadcrumbSegment segment)
        {
            if (segment != null)
            {
                await SetPathAsync(segment.FullPath);
                PathNavigated?.Invoke(this, segment.FullPath);
            }
        }

        /// <summary>
        /// Navigate to a specific path.
        /// </summary>
        [RelayCommand]
        private async Task NavigateToPathAsync(string path)
        {
            if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
            {
                await SetPathAsync(path);
                PathNavigated?.Invoke(this, path);
            }
        }

        /// <summary>
        /// Start editing the path.
        /// </summary>
        [RelayCommand]
        private void StartEdit()
        {
            EditText = CurrentPath;
            IsEditing = true;
        }

        /// <summary>
        /// Accept the edited path.
        /// </summary>
        [RelayCommand]
        private async Task AcceptEditAsync()
        {
            if (!string.IsNullOrEmpty(EditText))
            {
                // Validate and resolve path
                var resolvedPath = await ResolvePathAsync(EditText);
                if (!string.IsNullOrEmpty(resolvedPath))
                {
                    await SetPathAsync(resolvedPath);
                    PathNavigated?.Invoke(this, resolvedPath);
                }
            }
            IsEditing = false;
        }

        /// <summary>
        /// Cancel editing.
        /// </summary>
        [RelayCommand]
        private void CancelEdit()
        {
            IsEditing = false;
            EditText = CurrentPath;
        }

        /// <summary>
        /// Copy current path to clipboard.
        /// </summary>
        [RelayCommand]
        private void CopyPath()
        {
            Clipboard.SetText(CurrentPath);
        }

        /// <summary>
        /// Show recent locations dropdown.
        /// </summary>
        [RelayCommand]
        private void ShowRecent()
        {
            // Implementation for showing recent locations dropdown
        }

        private void UpdateSegments(string path)
        {
            Segments.Clear();
            
            if (string.IsNullOrEmpty(path)) return;

            var root = Path.GetPathRoot(path);
            var parts = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                          .Where(p => !string.IsNullOrEmpty(p))
                          .ToList();

            // Add root segment
            Segments.Add(new BreadcrumbSegment
            {
                Name = root,
                FullPath = root,
                IsFirst = true
            });

            // Add path segments
            var currentPath = root;
            for (int i = 0; i < parts.Count; i++)
            {
                if (i == 0 && parts[i] == root.TrimEnd('\\')) continue;
                
                currentPath = Path.Combine(currentPath, parts[i]);
                Segments.Add(new BreadcrumbSegment
                {
                    Name = parts[i],
                    FullPath = currentPath,
                    IsFirst = false
                });

                // Load subfolders for dropdown
                LoadSubFolders(Segments.Last());
            }
        }

        private async Task LoadSubFolders(BreadcrumbSegment segment)
        {
            try
            {
                if (_addressBarService != null && Directory.Exists(segment.FullPath))
                {
                    var subFolders = await _addressBarService.GetSubFoldersAsync(segment.FullPath);
                    segment.SubFolders.Clear();
                    
                    foreach (var folder in subFolders.Take(20)) // Limit to 20 items
                    {
                        segment.SubFolders.Add(new PathSuggestion
                        {
                            Name = Path.GetFileName(folder),
                            FullPath = folder,
                            Type = "Folder"
                        });
                    }
                }
            }
            catch
            {
                // Ignore errors when loading subfolders
            }
        }

        private void UpdateDriveIcon(string path)
        {
            if (string.IsNullOrEmpty(path)) return;

            var root = Path.GetPathRoot(path)?.ToUpperInvariant();
            DriveIcon = root switch
            {
                "C:\\" => "Harddisk",
                "D:\\" or "E:\\" or "F:\\" => "Harddisk",
                var p when p.StartsWith(@"\\") => "Network",
                _ => "Folder"
            };
        }

        private async Task<string> ResolvePathAsync(string input)
        {
            try
            {
                // Handle environment variables
                input = Environment.ExpandEnvironmentVariables(input);

                // Handle relative paths
                if (!Path.IsPathRooted(input))
                {
                    input = Path.Combine(CurrentPath, input);
                }

                // Clean up the path
                input = Path.GetFullPath(input);

                // Check if path exists
                if (Directory.Exists(input))
                {
                    return input;
                }

                // Try to find closest match
                if (_addressBarService != null)
                {
                    var suggestions = await _addressBarService.GetSuggestionsAsync(input);
                    return suggestions.FirstOrDefault()?.FullPath ?? string.Empty;
                }
            }
            catch
            {
                // Return empty on error
            }

            return string.Empty;
        }

        private async Task AddToRecentAsync(string path)
        {
            try
            {
                // Remove if already exists
                var existing = RecentLocations.FirstOrDefault(r => r.FullPath == path);
                if (existing != null)
                {
                    RecentLocations.Remove(existing);
                }

                // Add to beginning
                RecentLocations.Insert(0, new PathSuggestion
                {
                    Name = Path.GetFileName(path) ?? path,
                    FullPath = path,
                    Type = "Recent"
                });

                // Keep only last 20 items
                while (RecentLocations.Count > 20)
                {
                    RecentLocations.RemoveAt(RecentLocations.Count - 1);
                }

                // Save to favorites service if available
                if (_favoritesService != null)
                {
                    await _favoritesService.AddRecentLocationAsync(path);
                }
            }
            catch
            {
                // Ignore errors
            }
        }

        private void LoadRecentLocations()
        {
            // Load from service or use defaults
            RecentLocations.Clear();
            
            // Add common locations
            var commonPaths = new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                Environment.GetFolderPath(Environment.SpecialFolder.Documents),
                Environment.GetFolderPath(Environment.SpecialFolder.Downloads),
                Environment.GetFolderPath(Environment.SpecialFolder.Pictures),
                Environment.GetFolderPath(Environment.SpecialFolder.Music),
                Environment.GetFolderPath(Environment.SpecialFolder.Videos)
            };

            foreach (var path in commonPaths)
            {
                if (Directory.Exists(path))
                {
                    RecentLocations.Add(new PathSuggestion
                    {
                        Name = Path.GetFileName(path) ?? path,
                        FullPath = path,
                        Type = "Common"
                    });
                }
            }
        }

        /// <summary>
        /// Event raised when navigation is requested.
        /// </summary>
        public event EventHandler<string>? PathNavigated;
    }

    /// <summary>
    /// Represents a segment in the breadcrumb path.
    /// </summary>
    public partial class BreadcrumbSegment : ObservableObject
    {
        [ObservableProperty]
        private string _name = string.Empty;

        [ObservableProperty]
        private string _fullPath = string.Empty;

        [ObservableProperty]
        private bool _isFirst;

        [ObservableProperty]
        private ObservableCollection<PathSuggestion> _subFolders = new();
    }

    /// <summary>
    /// Represents a path suggestion.
    /// </summary>
    public partial class PathSuggestion : ObservableObject
    {
        [ObservableProperty]
        private string _name = string.Empty;

        [ObservableProperty]
        private string _fullPath = string.Empty;

        [ObservableProperty]
        private string _type = string.Empty;
    }
}
