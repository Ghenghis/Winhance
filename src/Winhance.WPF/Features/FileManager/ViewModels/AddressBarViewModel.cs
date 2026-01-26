using System;
using System.Collections.Generic;
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
    /// ViewModel for the address bar navigation control.
    /// </summary>
    public partial class AddressBarViewModel : ObservableObject
    {
        private readonly IAddressBarService _addressBarService;
        private readonly ITabService _tabService;
        private readonly IFavoritesService _favoritesService;

        [ObservableProperty]
        private string _currentPath = string.Empty;

        [ObservableProperty]
        private string _inputText = string.Empty;

        [ObservableProperty]
        private bool _isEditing;

        [ObservableProperty]
        private ObservableCollection<BreadcrumbItemViewModel> _breadcrumbs = new();

        [ObservableProperty]
        private ObservableCollection<string> _autocompleteSuggestions = new();

        [ObservableProperty]
        private bool _showAutocomplete;

        [ObservableProperty]
        private int _selectedSuggestionIndex = -1;

        [ObservableProperty]
        private ObservableCollection<string> _navigationHistory = new();

        [ObservableProperty]
        private int _historyIndex = -1;

        [ObservableProperty]
        private bool _canNavigateBack;

        [ObservableProperty]
        private bool _canNavigateForward;

        public AddressBarViewModel(
            IAddressBarService addressBarService,
            ITabService tabService,
            IFavoritesService favoritesService)
        {
            _addressBarService = addressBarService;
            _tabService = tabService;
            _favoritesService = favoritesService;

            // Set default path
            CurrentPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            InputText = CurrentPath;
        }

        /// <summary>
        /// Navigates to the specified path.
        /// </summary>
        [RelayCommand]
        public async Task NavigateAsync(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return;

            try
            {
                // Resolve environment variables
                path = Environment.ExpandEnvironmentVariables(path);
                
                // Handle special paths
                path = path switch
                {
                    "/" or "\\" => Path.GetPathRoot(Environment.SystemDirectory),
                    "~" => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".." => Path.GetDirectoryName(CurrentPath) ?? CurrentPath,
                    _ => path
                };

                // Get absolute path
                path = Path.GetFullPath(path);

                if (Directory.Exists(path))
                {
                    // Add to history
                    AddToHistory(path);
                    
                    // Update current path
                    CurrentPath = path;
                    InputText = path;
                    IsEditing = false;
                    ShowAutocomplete = false;
                    
                    // Update breadcrumbs
                    UpdateBreadcrumbs();
                    
                    // Notify navigation
                    await _addressBarService.NavigateAsync(path);
                }
                else
                {
                    // Try to find matching directory
                    var suggestions = await GetDirectorySuggestions(path);
                    if (suggestions.Count > 0)
                    {
                        AutocompleteSuggestions = new ObservableCollection<string>(suggestions);
                        ShowAutocomplete = true;
                    }
                    else
                    {
                        // Invalid path
                        await _addressBarService.ShowErrorAsync("Path does not exist");
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
                await _addressBarService.ShowErrorAsync("Access denied");
            }
            catch (Exception ex)
            {
                await _addressBarService.ShowErrorAsync($"Navigation error: {ex.Message}");
            }
        }

        /// <summary>
        /// Starts editing the address bar.
        /// </summary>
        [RelayCommand]
        public void BeginEdit()
        {
            IsEditing = true;
            InputText = CurrentPath;
            ShowAutocomplete = false;
            SelectedSuggestionIndex = -1;
        }

        /// <summary>
        /// Commits the address bar edit.
        /// </summary>
        [RelayCommand]
        public async Task CommitEdit()
        {
            if (IsEditing)
            {
                await NavigateAsync(InputText);
            }
        }

        /// <summary>
        /// Cancels the address bar edit.
        /// </summary>
        [RelayCommand]
        public void CancelEdit()
        {
            IsEditing = false;
            InputText = CurrentPath;
            ShowAutocomplete = false;
            AutocompleteSuggestions.Clear();
        }

        /// <summary>
        /// Navigates back in history.
        /// </summary>
        [RelayCommand]
        public async Task NavigateBackAsync()
        {
            if (CanNavigateBack && HistoryIndex > 0)
            {
                HistoryIndex--;
                var path = NavigationHistory[HistoryIndex];
                await NavigateToHistoryItem(path);
            }
        }

        /// <summary>
        /// Navigates forward in history.
        /// </summary>
        [RelayCommand]
        public async Task NavigateForwardAsync()
        {
            if (CanNavigateForward && HistoryIndex < NavigationHistory.Count - 1)
            {
                HistoryIndex++;
                var path = NavigationHistory[HistoryIndex];
                await NavigateToHistoryItem(path);
            }
        }

        /// <summary>
        /// Navigates to the parent directory.
        /// </summary>
        [RelayCommand]
        public async Task NavigateToParentAsync()
        {
            var parent = Path.GetDirectoryName(CurrentPath);
            if (!string.IsNullOrEmpty(parent))
            {
                await NavigateAsync(parent);
            }
        }

        /// <summary>
        /// Navigates to a breadcrumb item.
        /// </summary>
        [RelayCommand]
        public async Task NavigateToBreadcrumbAsync(BreadcrumbItemViewModel? breadcrumb)
        {
            if (breadcrumb?.FullPath != null)
            {
                await NavigateAsync(breadcrumb.FullPath);
            }
        }

        /// <summary>
        /// Adds current path to favorites.
        /// </summary>
        [RelayCommand]
        public async Task AddToFavoritesAsync()
        {
            try
            {
                await _favoritesService.AddFavoriteAsync(CurrentPath);
                await _addressBarService.ShowMessageAsync("Added to favorites");
            }
            catch (Exception ex)
            {
                await _addressBarService.ShowErrorAsync($"Cannot add to favorites: {ex.Message}");
            }
        }

        /// <summary>
        /// Copies the current path to clipboard.
        /// </summary>
        [RelayCommand]
        public async Task CopyPathAsync()
        {
            try
            {
                System.Windows.Clipboard.SetText(CurrentPath);
                await _addressBarService.ShowMessageAsync("Path copied to clipboard");
            }
            catch (Exception ex)
            {
                await _addressBarService.ShowErrorAsync($"Cannot copy path: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles text input changes.
        /// </summary>
        [RelayCommand]
        public async Task OnTextChanged()
        {
            if (!IsEditing || string.IsNullOrWhiteSpace(InputText))
            {
                ShowAutocomplete = false;
                AutocompleteSuggestions.Clear();
                return;
            }

            try
            {
                var suggestions = await GetDirectorySuggestions(InputText);
                AutocompleteSuggestions = new ObservableCollection<string>(suggestions);
                ShowAutocomplete = suggestions.Count > 0;
                SelectedSuggestionIndex = suggestions.Count > 0 ? 0 : -1;
            }
            catch
            {
                ShowAutocomplete = false;
                AutocompleteSuggestions.Clear();
            }
        }

        /// <summary>
        /// Selects the previous autocomplete suggestion.
        /// </summary>
        [RelayCommand]
        public void SelectPreviousSuggestion()
        {
            if (AutocompleteSuggestions.Count == 0) return;

            if (SelectedSuggestionIndex <= 0)
            {
                SelectedSuggestionIndex = AutocompleteSuggestions.Count - 1;
            }
            else
            {
                SelectedSuggestionIndex--;
            }

            UpdateInputFromSuggestion();
        }

        /// <summary>
        /// Selects the next autocomplete suggestion.
        /// </summary>
        [RelayCommand]
        public void SelectNextSuggestion()
        {
            if (AutocompleteSuggestions.Count == 0) return;

            if (SelectedSuggestionIndex >= AutocompleteSuggestions.Count - 1)
            {
                SelectedSuggestionIndex = 0;
            }
            else
            {
                SelectedSuggestionIndex++;
            }

            UpdateInputFromSuggestion();
        }

        /// <summary>
        /// Accepts the selected autocomplete suggestion.
        /// </summary>
        [RelayCommand]
        public void AcceptSuggestion()
        {
            if (SelectedSuggestionIndex >= 0 && SelectedSuggestionIndex < AutocompleteSuggestions.Count)
            {
                InputText = AutocompleteSuggestions[SelectedSuggestionIndex];
                ShowAutocomplete = false;
            }
        }

        /// <summary>
        /// Updates the breadcrumb items.
        /// </summary>
        private void UpdateBreadcrumbs()
        {
            var breadcrumbs = new List<BreadcrumbItemViewModel>();
            
            // Add root
            var root = Path.GetPathRoot(CurrentPath);
            if (!string.IsNullOrEmpty(root))
            {
                breadcrumbs.Add(new BreadcrumbItemViewModel
                {
                    Name = root,
                    FullPath = root,
                    IsRoot = true
                });
            }

            // Add path segments
            var segments = CurrentPath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var currentPath = root;

            for (int i = 1; i < segments.Length; i++)
            {
                if (string.IsNullOrEmpty(segments[i])) continue;

                currentPath = Path.Combine(currentPath, segments[i]);
                breadcrumbs.Add(new BreadcrumbItemViewModel
                {
                    Name = segments[i],
                    FullPath = currentPath,
                    IsRoot = false
                });
            }

            Breadcrumbs = new ObservableCollection<BreadcrumbItemViewModel>(breadcrumbs);
        }

        /// <summary>
        /// Gets directory suggestions for autocomplete.
        /// </summary>
        private async Task<List<string>> GetDirectorySuggestions(string input)
        {
            var suggestions = new List<string>();
            
            try
            {
                // Get directory part
                var directory = Path.GetDirectoryName(input);
                var prefix = Path.GetFileName(input);

                if (string.IsNullOrEmpty(directory))
                {
                    // Search in root directories
                    foreach (var drive in DriveInfo.GetDrives())
                    {
                        if (drive.IsReady && drive.Name.StartsWith(input, StringComparison.OrdinalIgnoreCase))
                        {
                            suggestions.Add(drive.Name);
                        }
                    }
                }
                else if (Directory.Exists(directory))
                {
                    // Search in directory
                    var directories = await Task.Run(() => 
                        Directory.GetDirectories(directory, $"{prefix}*"));
                    
                    suggestions.AddRange(directories);
                }

                // Add file suggestions if requested
                var files = await Task.Run(() => 
                    Directory.GetFiles(directory ?? CurrentPath, $"{prefix}*"));
                
                suggestions.AddRange(files.Take(10)); // Limit file suggestions
            }
            catch
            {
                // Ignore errors in autocomplete
            }

            return suggestions.Distinct().OrderBy(s => s).Take(20).ToList();
        }

        /// <summary>
        /// Updates input text from selected suggestion.
        /// </summary>
        private void UpdateInputFromSuggestion()
        {
            if (SelectedSuggestionIndex >= 0 && SelectedSuggestionIndex < AutocompleteSuggestions.Count)
            {
                InputText = AutocompleteSuggestions[SelectedSuggestionIndex];
            }
        }

        /// <summary>
        /// Adds path to navigation history.
        /// </summary>
        private void AddToHistory(string path)
        {
            // Remove any future history if we're not at the end
            if (HistoryIndex < NavigationHistory.Count - 1)
            {
                for (int i = NavigationHistory.Count - 1; i > HistoryIndex; i--)
                {
                    NavigationHistory.RemoveAt(i);
                }
            }

            // Don't add duplicates
            if (NavigationHistory.LastOrDefault() == path)
            {
                return;
            }

            // Add new path
            NavigationHistory.Add(path);
            HistoryIndex = NavigationHistory.Count - 1;

            // Limit history size
            while (NavigationHistory.Count > 100)
            {
                NavigationHistory.RemoveAt(0);
                HistoryIndex--;
            }

            UpdateNavigationButtons();
        }

        /// <summary>
        /// Navigates to a history item without adding to history.
        /// </summary>
        private async Task NavigateToHistoryItem(string path)
        {
            CurrentPath = path;
            InputText = path;
            IsEditing = false;
            ShowAutocomplete = false;
            UpdateBreadcrumbs();
            await _addressBarService.NavigateAsync(path);
            UpdateNavigationButtons();
        }

        /// <summary>
        /// Updates navigation button states.
        /// </summary>
        private void UpdateNavigationButtons()
        {
            CanNavigateBack = HistoryIndex > 0;
            CanNavigateForward = HistoryIndex < NavigationHistory.Count - 1;
        }

        /// <summary>
        /// Handles property changes.
        /// </summary>
        partial void OnCurrentPathChanged(string value)
        {
            if (!IsEditing)
            {
                InputText = value;
                UpdateBreadcrumbs();
            }
        }
    }

    /// <summary>
    /// ViewModel for a breadcrumb item.
    /// </summary>
    public partial class BreadcrumbItemViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _name = string.Empty;

        [ObservableProperty]
        private string _fullPath = string.Empty;

        [ObservableProperty]
        private bool _isRoot;
    }
}
