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
    /// ViewModel for navigation history
    /// </summary>
    public partial class NavigationHistoryViewModel : ObservableObject
    {
        private readonly IAddressBarService _addressBarService;
        private ObservableCollection<FileItem> _backwardHistory = new();
        private ObservableCollection<FileItem> _forwardHistory = new();
        private FileItem? _currentLocation;

        public ObservableCollection<FileItem> BackwardHistory
        {
            get => _backwardHistory;
            set => SetProperty(ref _backwardHistory, value);
        }

        public ObservableCollection<FileItem> ForwardHistory
        {
            get => _forwardHistory;
            set => SetProperty(ref _forwardHistory, value);
        }

        public FileItem? CurrentLocation
        {
            get => _currentLocation;
            set => SetProperty(ref _currentLocation, value);
        }

        public NavigationHistoryViewModel(IAddressBarService addressBarService)
        {
            _addressBarService = addressBarService;
        }

        [RelayCommand]
        private async Task NavigateBackwardAsync(FileItem? item)
        {
            if (item == null) return;

            ForwardHistory.Insert(0, CurrentLocation!);
            CurrentLocation = item;
            BackwardHistory.Remove(item);
            
            await _addressBarService.NavigateToAsync(item.FullPath);
        }

        [RelayCommand]
        private async Task NavigateForwardAsync(FileItem? item)
        {
            if (item == null) return;

            BackwardHistory.Insert(0, CurrentLocation!);
            CurrentLocation = item;
            ForwardHistory.Remove(item);
            
            await _addressBarService.NavigateToAsync(item.FullPath);
        }

        public void AddToHistory(FileItem location)
        {
            if (CurrentLocation != null && CurrentLocation.FullPath != location.FullPath)
            {
                BackwardHistory.Insert(0, CurrentLocation);
                ForwardHistory.Clear();
            }
            CurrentLocation = location;
        }
    }

    /// <summary>
    /// ViewModel for breadcrumb navigation
    /// </summary>
    public partial class BreadcrumbNavigationViewModel : ObservableObject
    {
        private readonly IAddressBarService _addressBarService;
        private ObservableCollection<BreadcrumbItem> _breadcrumbs = new();

        public ObservableCollection<BreadcrumbItem> Breadcrumbs
        {
            get => _breadcrumbs;
            set => SetProperty(ref _breadcrumbs, value);
        }

        public BreadcrumbNavigationViewModel(IAddressBarService addressBarService)
        {
            _addressBarService = addressBarService;
        }

        [RelayCommand]
        private async Task NavigateToBreadcrumbAsync(BreadcrumbItem? item)
        {
            if (item == null) return;

            await _addressBarService.NavigateToAsync(item.FullPath);
        }

        public void UpdateBreadcrumbs(string path)
        {
            Breadcrumbs.Clear();
            
            if (string.IsNullOrEmpty(path)) return;

            var parts = path.Split('\\', StringSplitOptions.RemoveEmptyEntries);
            string currentPath = "";

            foreach (var part in parts)
            {
                currentPath = string.IsNullOrEmpty(currentPath) ? part : $"{currentPath}\\{part}";
                Breadcrumbs.Add(new BreadcrumbItem
                {
                    Name = part,
                    FullPath = currentPath,
                    IsRoot = parts.IndexOf(part) == 0
                });
            }
        }
    }

    /// <summary>
    /// Breadcrumb item model
    /// </summary>
    public class BreadcrumbItem
    {
        public string Name { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public bool IsRoot { get; set; }
        public ObservableCollection<BreadcrumbItem> SubFolders { get; set; } = new();
    }

    /// <summary>
    /// ViewModel for path autocomplete
    /// </summary>
    public partial class PathAutocompleteViewModel : ObservableObject
    {
        private readonly IAddressBarService _addressBarService;
        private ObservableCollection<string> _suggestions = new();
        private string _currentInput = string.Empty;

        public ObservableCollection<string> Suggestions
        {
            get => _suggestions;
            set => SetProperty(ref _suggestions, value);
        }

        public string CurrentInput
        {
            get => _currentInput;
            set
            {
                SetProperty(ref _currentInput, value);
                _ = UpdateSuggestionsAsync(value);
            }
        }

        public PathAutocompleteViewModel(IAddressBarService addressBarService)
        {
            _addressBarService = addressBarService;
        }

        private async Task UpdateSuggestionsAsync(string input)
        {
            Suggestions.Clear();
            
            if (string.IsNullOrEmpty(input) || input.Length < 2) return;

            var paths = await _addressBarService.GetPathSuggestionsAsync(input);
            foreach (var path in paths.Take(10))
            {
                Suggestions.Add(path);
            }
        }

        [RelayCommand]
        private async Task SelectSuggestionAsync(string? suggestion)
        {
            if (suggestion == null) return;

            CurrentInput = suggestion;
            Suggestions.Clear();
            await _addressBarService.NavigateToAsync(suggestion);
        }
    }

    /// <summary>
    /// ViewModel for recent and frequent locations
    /// </summary>
    public partial class LocationHistoryViewModel : ObservableObject
    {
        private readonly IAddressBarService _addressBarService;
        private ObservableCollection<FileItem> _recentLocations = new();
        private ObservableCollection<FileItem> _frequentLocations = new();

        public ObservableCollection<FileItem> RecentLocations
        {
            get => _recentLocations;
            set => SetProperty(ref _recentLocations, value);
        }

        public ObservableCollection<FileItem> FrequentLocations
        {
            get => _frequentLocations;
            set => SetProperty(ref _frequentLocations, value);
        }

        public LocationHistoryViewModel(IAddressBarService addressBarService)
        {
            _addressBarService = addressBarService;
            _ = LoadLocationsAsync();
        }

        private async Task LoadLocationsAsync()
        {
            var recent = await _addressBarService.GetRecentLocationsAsync();
            var frequent = await _addressBarService.GetFrequentLocationsAsync();

            foreach (var item in recent.Take(10))
            {
                RecentLocations.Add(item);
            }

            foreach (var item in frequent.Take(10))
            {
                FrequentLocations.Add(item);
            }
        }

        [RelayCommand]
        private async Task NavigateToLocationAsync(FileItem? location)
        {
            if (location == null) return;

            await _addressBarService.NavigateToAsync(location.FullPath);
        }

        [RelayCommand]
        private async Task ClearRecentAsync()
        {
            await _addressBarService.ClearRecentLocationsAsync();
            RecentLocations.Clear();
        }
    }

    /// <summary>
    /// ViewModel for go to folder dialog
    /// </summary>
    public partial class GoToFolderViewModel : ObservableObject
    {
        private readonly IAddressBarService _addressBarService;
        private string _selectedPath = string.Empty;
        private ObservableCollection<string> _bookmarks = new();

        public string SelectedPath
        {
            get => _selectedPath;
            set => SetProperty(ref _selectedPath, value);
        }

        public ObservableCollection<string> Bookmarks
        {
            get => _bookmarks;
            set => SetProperty(ref _bookmarks, value);
        }

        public GoToFolderViewModel(IAddressBarService addressBarService)
        {
            _addressBarService = addressBarService;
            _ = LoadBookmarksAsync();
        }

        private async Task LoadBookmarksAsync()
        {
            var bookmarks = await _addressBarService.GetBookmarksAsync();
            foreach (var bookmark in bookmarks)
            {
                Bookmarks.Add(bookmark);
            }
        }

        [RelayCommand]
        private async Task NavigateToPathAsync()
        {
            if (string.IsNullOrEmpty(SelectedPath)) return;

            await _addressBarService.NavigateToAsync(SelectedPath);
        }

        [RelayCommand]
        private void BrowseForFolder()
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Select Folder"
            };

            if (dialog.ShowDialog() == true)
            {
                CustomPath = dialog.FolderName;
            }
        }
    }

    /// <summary>
    /// ViewModel for path copy formats
    /// </summary>
    public partial class PathCopyViewModel : ObservableObject
    {
        private string _currentPath = string.Empty;

        public PathCopyViewModel()
        {
        }

        public void SetCurrentPath(string path)
        {
            CurrentPath = path;
        }

        public string CurrentPath
        {
            get => _currentPath;
            set => SetProperty(ref _currentPath, value);
        }

        [RelayCommand]
        private void CopyFullPath()
        {
            Clipboard.SetText(CurrentPath);
        }

        [RelayCommand]
        private void CopyQuotedPath()
        {
            Clipboard.SetText($"\"{CurrentPath}\"");
        }

        [RelayCommand]
        private void CopyUncPath()
        {
            // Convert to UNC format if local path
            var uncPath = CurrentPath.Replace("C:", @"\\localhost\C$");
            Clipboard.SetText(uncPath);
        }

        [RelayCommand]
        private void CopyUrlPath()
        {
            var urlPath = CurrentPath.Replace('\\', '/');
            Clipboard.SetText($"file:///{urlPath.TrimStart('/')}");
        }

        [RelayCommand]
        private void CopyFileName()
        {
            var fileName = System.IO.Path.GetFileName(CurrentPath);
            Clipboard.SetText(fileName);
        }

        [RelayCommand]
        private void CopyDirectoryName()
        {
            var dirName = System.IO.Path.GetDirectoryName(CurrentPath);
            if (!string.IsNullOrEmpty(dirName))
            {
                Clipboard.SetText(System.IO.Path.GetFileName(dirName));
            }
        }
    }

    /// <summary>
    /// ViewModel for special folder shortcuts
    /// </summary>
    public partial class SpecialFolderViewModel : ObservableObject
    {
        private readonly IAddressBarService _addressBarService;
        private ObservableCollection<SpecialFolder> _specialFolders = new();

        public ObservableCollection<SpecialFolder> SpecialFolders
        {
            get => _specialFolders;
            set => SetProperty(ref _specialFolders, value);
        }

        public SpecialFolderViewModel(IAddressBarService addressBarService)
        {
            _addressBarService = addressBarService;
            LoadSpecialFolders();
        }

        private void LoadSpecialFolders()
        {
            SpecialFolders.Add(new SpecialFolder { Name = "Desktop", Path = Environment.GetFolderPath(Environment.SpecialFolder.Desktop), Icon = "🖥️" });
            SpecialFolders.Add(new SpecialFolder { Name = "Documents", Path = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), Icon = "📄" });
            SpecialFolders.Add(new SpecialFolder { Name = "Downloads", Path = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "\\Downloads", Icon = "⬇️" });
            SpecialFolders.Add(new SpecialFolder { Name = "Pictures", Path = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), Icon = "🖼️" });
            SpecialFolders.Add(new SpecialFolder { Name = "Music", Path = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic), Icon = "🎵" });
            SpecialFolders.Add(new SpecialFolder { Name = "Videos", Path = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), Icon = "🎬" });
            SpecialFolders.Add(new SpecialFolder { Name = "Program Files", Path = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), Icon = "⚙️" });
            SpecialFolders.Add(new SpecialFolder { Name = "Program Files (x86)", Path = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), Icon = "⚙️" });
        }

        [RelayCommand]
        private async Task NavigateToSpecialFolderAsync(SpecialFolder? folder)
        {
            if (folder == null) return;

            await _addressBarService.NavigateToAsync(folder.Path);
        }
    }

    /// <summary>
    /// Special folder model
    /// </summary>
    public class SpecialFolder
    {
        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string Icon { get; set; } = "📁";
    }

    /// <summary>
    /// ViewModel for environment variable expansion
    /// </summary>
    public partial class EnvironmentVariableViewModel : ObservableObject
    {
        [RelayCommand]
        private string ExpandEnvironmentVariables(string input)
        {
            return Environment.ExpandEnvironmentVariables(input);
        }

        public ObservableCollection<EnvironmentVariable> CommonVariables { get; } = new()
        {
            new() { Name = "%USERPROFILE%", Value = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) },
            new() { Name = "%APPDATA%", Value = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) },
            new() { Name = "%LOCALAPPDATA%", Value = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) },
            new() { Name = "%TEMP%", Value = Path.GetTempPath() },
            new() { Name = "%WINDIR%", Value = Environment.GetFolderPath(Environment.SpecialFolder.Windows) },
            new() { Name = "%PROGRAMFILES%", Value = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles) },
            new() { Name = "%PROGRAMFILES(X86)%", Value = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86) }
        };
    }

    /// <summary>
    /// Environment variable model
    /// </summary>
    public class EnvironmentVariable
    {
        public string Name { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
    }
}
