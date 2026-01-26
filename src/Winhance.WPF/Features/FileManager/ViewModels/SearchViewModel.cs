using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Winhance.Core.Features.FileManager.Interfaces;
using Winhance.Core.Features.FileManager.Models;

namespace Winhance.WPF.Features.FileManager.ViewModels
{
    /// <summary>
    /// ViewModel for search functionality.
    /// </summary>
    public partial class SearchViewModel : ObservableObject
    {
        private readonly ISearchService _searchService;
        private readonly ISelectionService _selectionService;
        private CancellationTokenSource? _cancellationTokenSource;

        [ObservableProperty]
        private string _searchQuery = string.Empty;

        [ObservableProperty]
        private string _searchPath = string.Empty;

        [ObservableProperty]
        private bool _isSearching;

        [ObservableProperty]
        private ObservableCollection<SearchResultViewModel> _searchResults = new();

        [ObservableProperty]
        private SearchResultViewModel? _selectedResult;

        [ObservableProperty]
        private int _resultCount;

        [ObservableProperty]
        private string _statusText = "Ready to search";

        [ObservableProperty]
        private bool _searchSubfolders = true;

        [ObservableProperty]
        private bool _searchFileNames = true;

        [ObservableProperty]
        private bool _searchFileContent;

        [ObservableProperty]
        private bool _caseSensitive;

        [ObservableProperty]
        private bool _useRegex;

        [ObservableProperty]
        private bool _includeHiddenFiles;

        [ObservableProperty]
        private DateTime? _dateFrom;

        [ObservableProperty]
        private DateTime? _dateTo;

        [ObservableProperty]
        private long? _sizeMin;

        [ObservableProperty]
        private long? _sizeMax;

        [ObservableProperty]
        private string _fileTypes = string.Empty;

        [ObservableProperty]
        private ObservableCollection<string> _searchHistory = new();

        [ObservableProperty]
        private bool _showPreview;

        public SearchViewModel(ISearchService searchService, ISelectionService selectionService)
        {
            _searchService = searchService;
            _selectionService = selectionService;
            
            // Set default search path to user profile
            SearchPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            
            // Load search history
            LoadSearchHistory();
        }

        /// <summary>
        /// Starts the search operation.
        /// </summary>
        [RelayCommand]
        public async Task SearchAsync()
        {
            if (string.IsNullOrWhiteSpace(SearchQuery))
            {
                StatusText = "Please enter a search query";
                return;
            }

            if (!Directory.Exists(SearchPath))
            {
                StatusText = "Search path does not exist";
                return;
            }

            // Cancel previous search
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource = new CancellationTokenSource();

            IsSearching = true;
            StatusText = "Searching...";
            ResultCount = 0;
            SearchResults.Clear();

            try
            {
                // Add to search history
                AddToSearchHistory(SearchQuery);

                // Create search options
                var searchOptions = new SearchOptions
                {
                    Query = SearchQuery,
                    Path = SearchPath,
                    IncludeSubfolders = SearchSubfolders,
                    SearchFileNames = SearchFileNames,
                    SearchContent = SearchFileContent,
                    CaseSensitive = CaseSensitive,
                    UseRegex = UseRegex,
                    IncludeHidden = IncludeHiddenFiles,
                    DateFrom = DateFrom,
                    DateTo = DateTo,
                    SizeMin = SizeMin,
                    SizeMax = SizeMax,
                    FileTypes = ParseFileTypes(FileTypes)
                };

                // Execute search
                await foreach (var result in _searchService.SearchAsync(searchOptions, _cancellationTokenSource.Token))
                {
                    if (_cancellationTokenSource.Token.IsCancellationRequested)
                        break;

                    var resultViewModel = new SearchResultViewModel(result);
                    
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        SearchResults.Add(resultViewModel);
                        ResultCount++;
                        StatusText = $"Found {ResultCount} item{(ResultCount != 1 ? "s" : "")}...";
                    });
                }

                StatusText = ResultCount == 0 
                    ? "No results found" 
                    : $"Found {ResultCount} item{(ResultCount != 1 ? "s" : "")}";
            }
            catch (OperationCanceledException)
            {
                StatusText = "Search cancelled";
            }
            catch (UnauthorizedAccessException)
            {
                StatusText = "Access denied to some locations";
            }
            catch (Exception ex)
            {
                StatusText = $"Search error: {ex.Message}";
            }
            finally
            {
                IsSearching = false;
            }
        }

        /// <summary>
        /// Cancels the current search.
        /// </summary>
        [RelayCommand]
        public void CancelSearch()
        {
            _cancellationTokenSource?.Cancel();
            StatusText = "Cancelling search...";
        }

        /// <summary>
        /// Clears the search results.
        /// </summary>
        [RelayCommand]
        public void ClearResults()
        {
            _cancellationTokenSource?.Cancel();
            SearchResults.Clear();
            ResultCount = 0;
            StatusText = "Ready to search";
        }

        /// <summary>
        /// Opens the selected result in file explorer.
        /// </summary>
        [RelayCommand]
        public void OpenResultLocation(SearchResultViewModel? result)
        {
            if (result == null) return;

            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"/select,\"{result.FullPath}\"",
                    UseShellExecute = true
                };
                System.Diagnostics.Process.Start(psi);
            }
            catch (Exception ex)
            {
                StatusText = $"Cannot open location: {ex.Message}";
            }
        }

        /// <summary>
        /// Opens the selected result.
        /// </summary>
        [RelayCommand]
        public async Task OpenResultAsync(SearchResultViewModel? result)
        {
            if (result == null) return;

            try
            {
                if (result.IsDirectory)
                {
                    // Navigate to directory
                    SearchPath = result.FullPath;
                    await SearchAsync();
                }
                else
                {
                    // Open file with default application
                    var psi = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = result.FullPath,
                        UseShellExecute = true
                    };
                    System.Diagnostics.Process.Start(psi);
                }
            }
            catch (Exception ex)
            {
                StatusText = $"Cannot open item: {ex.Message}";
            }
        }

        /// <summary>
        /// Selects all search results.
        /// </summary>
        [RelayCommand]
        public void SelectAllResults()
        {
            var paths = SearchResults.Select(r => r.FullPath);
            _selectionService.SetSelection(paths);
        }

        /// <summary>
        /// Exports search results to a file.
        /// </summary>
        [RelayCommand]
        public async Task ExportResultsAsync()
        {
            if (SearchResults.Count == 0)
            {
                StatusText = "No results to export";
                return;
            }

            try
            {
                var saveFileDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "CSV files (*.csv)|*.csv|Text files (*.txt)|*.txt|All files (*.*)|*.*",
                    DefaultExt = ".csv",
                    FileName = $"SearchResults_{DateTime.Now:yyyyMMdd_HHmmss}"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    var extension = Path.GetExtension(saveFileDialog.FileName).ToLower();
                    
                    if (extension == ".csv")
                    {
                        await ExportToCsvAsync(saveFileDialog.FileName);
                    }
                    else
                    {
                        await ExportToTextAsync(saveFileDialog.FileName);
                    }
                    
                    StatusText = $"Results exported to {Path.GetFileName(saveFileDialog.FileName)}";
                }
            }
            catch (Exception ex)
            {
                StatusText = $"Export failed: {ex.Message}";
            }
        }

        /// <summary>
        /// Browses for a search directory.
        /// </summary>
        [RelayCommand]
        public void BrowseSearchPath()
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                SelectedPath = SearchPath,
                Description = "Select folder to search in",
                ShowNewFolderButton = false
            };

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                SearchPath = dialog.SelectedPath;
            }
        }

        /// <summary>
        /// Loads search history from settings.
        /// </summary>
        private void LoadSearchHistory()
        {
            try
            {
                var historyPath = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Winhance-FS",
                    "SearchHistory.json");

                if (System.IO.File.Exists(historyPath))
                {
                    var json = System.IO.File.ReadAllText(historyPath);
                    var history = System.Text.Json.JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
                    SearchHistory = new ObservableCollection<string>(history);
                }
                else
                {
                    SearchHistory = new ObservableCollection<string>
                    {
                    "*.txt",
                    "*.pdf",
                    "config",
                    "readme",
                    "test"
                };
            }
            catch
            {
                SearchHistory = new ObservableCollection<string>();
            }
        }

        /// <summary>
        /// Adds query to search history.
        /// </summary>
        private void AddToSearchHistory(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return;

            // Remove if already exists
            SearchHistory.Remove(query);
            
            // Add to beginning
            SearchHistory.Insert(0, query);
            
            // Keep only last 20 items
            while (SearchHistory.Count > 20)
            {
                SearchHistory.RemoveAt(SearchHistory.Count - 1);
            }

            try
            {
                var historyPath = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Winhance-FS",
                    "SearchHistory.json");

                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(historyPath)!);
                var json = System.Text.Json.JsonSerializer.Serialize(SearchHistory.ToList(),
                    new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                System.IO.File.WriteAllText(historyPath, json);
            }
            catch
            {
                // Ignore save errors
            }
        }

        /// <summary>
        /// Parses file types string into array.
        /// </summary>
        private static string[] ParseFileTypes(string fileTypes)
        {
            if (string.IsNullOrWhiteSpace(fileTypes))
                return Array.Empty<string>();

            return fileTypes.Split(new[] { ';', ',', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                           .Select(t => t.Trim())
                           .Where(t => !string.IsNullOrEmpty(t))
                           .ToArray();
        }

        /// <summary>
        /// Exports results to CSV format.
        /// </summary>
        private async Task ExportToCsvAsync(string filePath)
        {
            await Task.Run(() =>
            {
                using var writer = new StreamWriter(filePath, false, System.Text.Encoding.UTF8);
                
                // Write header
                await writer.WriteLineAsync("Name,Path,Size,Modified,Type,Match");
                
                // Write results
                foreach (var result in SearchResults)
                {
                    var line = $"\"{result.Name}\",\"{result.FullPath}\",{result.Size},{result.DateModified:yyyy-MM-dd HH:mm:ss},\"{result.Type}\",\"{result.MatchedText}\"";
                    await writer.WriteLineAsync(line);
                }
            });
        }

        /// <summary>
        /// Exports results to text format.
        /// </summary>
        private async Task ExportToTextAsync(string filePath)
        {
            await Task.Run(() =>
            {
                using var writer = new StreamWriter(filePath, false, System.Text.Encoding.UTF8);
                
                await writer.WriteLineAsync($"Search Results - {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                await writer.WriteLineAsync($"Query: {SearchQuery}");
                await writer.WriteLineAsync($"Path: {SearchPath}");
                await writer.WriteLineAsync($"Total Results: {ResultCount}");
                await writer.WriteLineAsync(new string('-', 80));
                
                foreach (var result in SearchResults)
                {
                    await writer.WriteLineAsync($"{result.Name}");
                    await writer.WriteLineAsync($"  Path: {result.FullPath}");
                    await writer.WriteLineAsync($"  Size: {result.SizeFormatted}");
                    await writer.WriteLineAsync($"  Modified: {result.DateModified:yyyy-MM-dd HH:mm:ss}");
                    if (!string.IsNullOrEmpty(result.MatchedText))
                    {
                        await writer.WriteLineAsync($"  Match: {result.MatchedText}");
                    }
                    await writer.WriteLineAsync();
                }
            });
        }

        /// <summary>
        /// Handles property changes.
        /// </summary>
        partial void OnSearchQueryChanged(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                ClearResults();
            }
        }

        /// <summary>
        /// Disposes the ViewModel.
        /// </summary>
        public void Dispose()
        {
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
        }
    }

    /// <summary>
    /// ViewModel for a single search result.
    /// </summary>
    public partial class SearchResultViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _name = string.Empty;

        [ObservableProperty]
        private string _fullPath = string.Empty;

        [ObservableProperty]
        private long _size;

        [ObservableProperty]
        private DateTime _dateModified;

        [ObservableProperty]
        private string _type = string.Empty;

        [ObservableProperty]
        private bool _isDirectory;

        [ObservableProperty]
        private string _matchedText = string.Empty;

        [ObservableProperty]
        private int _matchLine;

        [ObservableProperty]
        private int _matchColumn;

        public string SizeFormatted => FormatSize(_size);

        public SearchResultViewModel(SearchResult result)
        {
            Name = Path.GetFileName(result.Path);
            FullPath = result.Path;
            Size = result.Size;
            DateModified = result.ModifiedDate;
            IsDirectory = result.IsDirectory;
            Type = IsDirectory ? "Folder" : GetFileType(Path.GetExtension(result.Path));
            MatchedText = result.MatchedText ?? string.Empty;
            MatchLine = result.MatchLine;
            MatchColumn = result.MatchColumn;
        }

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

        private static string GetFileType(string extension)
        {
            return extension.ToLowerInvariant() switch
            {
                ".txt" => "Text Document",
                ".pdf" => "PDF Document",
                ".doc" or ".docx" => "Word Document",
                ".xls" or ".xlsx" => "Excel Spreadsheet",
                ".jpg" or ".jpeg" => "JPEG Image",
                ".png" => "PNG Image",
                ".mp4" => "MP4 Video",
                ".mp3" => "MP3 Audio",
                ".zip" => "ZIP Archive",
                _ => $"{extension.TrimStart('.').ToUpper()} File"
            };
        }
    }
}
