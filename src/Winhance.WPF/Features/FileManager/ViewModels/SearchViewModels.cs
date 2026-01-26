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
    /// ViewModel for real-time search
    /// </summary>
    public partial class RealTimeSearchViewModel : ObservableObject
    {
        private readonly ISearchService _searchService;
        private ObservableCollection<FileItem> _searchResults = new();
        private string _searchText = string.Empty;
        private bool _isSearching;

        public ObservableCollection<FileItem> SearchResults
        {
            get => _searchResults;
            set => SetProperty(ref _searchResults, value);
        }

        public string SearchText
        {
            get => _searchText;
            set
            {
                SetProperty(ref _searchText, value);
                _ = PerformSearchAsync(value);
            }
        }

        public bool IsSearching
        {
            get => _isSearching;
            set => SetProperty(ref _isSearching, value);
        }

        public RealTimeSearchViewModel(ISearchService searchService)
        {
            _searchService = searchService;
        }

        private async Task PerformSearchAsync(string searchText)
        {
            if (string.IsNullOrWhiteSpace(searchText))
            {
                SearchResults.Clear();
                return;
            }

            IsSearching = true;

            try
            {
                var results = await _searchService.SearchAsync(new SearchQuery
                {
                    Text = searchText,
                    SearchType = SearchType.Filename,
                    MaxResults = 100
                });

                SearchResults.Clear();
                foreach (var item in results)
                {
                    SearchResults.Add(item);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Search failed: {ex.Message}",
                    "Search Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
            finally
            {
                IsSearching = false;
            }
        }

        [RelayCommand]
        private async Task OpenSearchResultAsync(FileItem? item)
        {
            if (item == null) return;

            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = item.FullPath,
                    UseShellExecute = true
                };
                System.Diagnostics.Process.Start(psi);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to open file: {ex.Message}",
                    "Open Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
            await Task.CompletedTask;
        }
    }

    /// <summary>
    /// ViewModel for advanced search
    /// </summary>
    public partial class AdvancedSearchViewModel : ObservableObject
    {
        private readonly ISearchService _searchService;
        private ObservableCollection<FileItem> _searchResults = new();
        private ObservableCollection<SearchFilter> _filters = new();

        [ObservableProperty]
        private SearchQuery _searchQuery = new();

        [ObservableProperty]
        private bool _isSearching;

        [ObservableProperty]
        private string? _searchStatus;

        public ObservableCollection<FileItem> SearchResults
        {
            get => _searchResults;
            set => SetProperty(ref _searchResults, value);
        }

        public ObservableCollection<SearchFilter> Filters
        {
            get => _filters;
            set => SetProperty(ref _filters, value);
        }

        public AdvancedSearchViewModel(ISearchService searchService)
        {
            _searchService = searchService;
            InitializeFilters();
        }

        private void InitializeFilters()
        {
            Filters.Add(new SearchFilter { Name = "Size", Type = FilterType.SizeRange });
            Filters.Add(new SearchFilter { Name = "Date", Type = FilterType.DateRange });
            Filters.Add(new SearchFilter { Name = "Attributes", Type = FilterType.Attributes });
            Filters.Add(new SearchFilter { Name = "Extensions", Type = FilterType.Extensions });
        }

        [RelayCommand]
        private async Task ExecuteSearchAsync()
        {
            IsSearching = true;
            SearchStatus = "Searching...";

            try
            {
                var results = await _searchService.SearchAsync(SearchQuery);
                
                SearchResults.Clear();
                foreach (var item in results)
                {
                    SearchResults.Add(item);
                }

                SearchStatus = $"Found {results.Count} results";
            }
            catch (Exception ex)
            {
                SearchStatus = $"Search failed: {ex.Message}";
            }
            finally
            {
                IsSearching = false;
            }
        }

        [RelayCommand]
        private void ClearSearch()
        {
            SearchQuery = new SearchQuery();
            SearchResults.Clear();
            SearchStatus = null;
        }

        [RelayCommand]
        private async Task SaveSearchAsync()
        {
            try
            {
                await _searchService.SaveSearchAsync(SearchQuery);
                System.Windows.MessageBox.Show(
                    "Search saved successfully.",
                    "Save Complete",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to save search: {ex.Message}",
                    "Save Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task LoadSavedSearchesAsync()
        {
            try
            {
                var savedSearches = await _searchService.GetSavedSearchesAsync();
                var message = "Saved Searches:\n\n";
                if (savedSearches.Any())
                {
                    message += string.Join("\n", savedSearches.Select(s => $"- {s.Name}: {s.Text}"));
                }
                else
                {
                    message += "No saved searches found.";
                }
                System.Windows.MessageBox.Show(message, "Saved Searches",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to load saved searches: {ex.Message}",
                    "Load Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }
    }

    /// <summary>
    /// ViewModel for content search
    /// </summary>
    public partial class ContentSearchViewModel : ObservableObject
    {
        private readonly ISearchService _searchService;
        private ObservableCollection<ContentSearchResult> _results = new();

        [ObservableProperty]
        private string _searchText = string.Empty;

        [ObservableProperty]
        private bool _useRegex;

        [ObservableProperty]
        private bool _caseSensitive;

        [ObservableProperty]
        private ObservableCollection<string> _fileTypes = new();

        [ObservableProperty]
        private bool _isSearching;

        public ObservableCollection<ContentSearchResult> Results
        {
            get => _results;
            set => SetProperty(ref _results, value);
        }

        public ContentSearchViewModel(ISearchService searchService)
        {
            _searchService = searchService;
            InitializeFileTypes();
        }

        private void InitializeFileTypes()
        {
            FileTypes.Add(".txt");
            FileTypes.Add(".cs");
            FileTypes.Add(".js");
            FileTypes.Add(".html");
            FileTypes.Add(".xml");
            FileTypes.Add(".json");
            FileTypes.Add(".md");
            FileTypes.Add(".log");
        }

        [RelayCommand]
        private async Task SearchInContentAsync()
        {
            if (string.IsNullOrWhiteSpace(SearchText)) return;

            IsSearching = true;

            try
            {
                var query = new ContentSearchQuery
                {
                    Text = SearchText,
                    UseRegex = UseRegex,
                    CaseSensitive = CaseSensitive,
                    FileTypes = FileTypes.ToList(),
                    MaxResults = 100
                };

                var results = await _searchService.SearchContentAsync(query);
                
                Results.Clear();
                foreach (var result in results)
                {
                    Results.Add(result);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Content search failed: {ex.Message}",
                    "Search Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
            finally
            {
                IsSearching = false;
            }
        }

        [RelayCommand]
        private async Task OpenResultAsync(ContentSearchResult? result)
        {
            if (result == null) return;

            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = result.FilePath,
                    UseShellExecute = true
                };
                System.Diagnostics.Process.Start(psi);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to open file at line {result.LineNumber}: {ex.Message}",
                    "Open Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
            await Task.CompletedTask;
        }
    }

    /// <summary>
    /// ViewModel for quick filter
    /// </summary>
    public partial class QuickFilterViewModel : ObservableObject
    {
        private readonly IQuickFilterService _quickFilterService;
        private string _filterText = string.Empty;
        private ObservableCollection<FileItem> _filteredItems = new();

        public string FilterText
        {
            get => _filterText;
            set
            {
                SetProperty(ref _filterText, value);
                ApplyFilter(value);
            }
        }

        public ObservableCollection<FileItem> FilteredItems
        {
            get => _filteredItems;
            set => SetProperty(ref _filteredItems, value);
        }

        public QuickFilterViewModel(IQuickFilterService quickFilterService)
        {
            _quickFilterService = quickFilterService;
        }

        private void ApplyFilter(string filter)
        {
            if (string.IsNullOrWhiteSpace(filter))
            {
                var allItems = _quickFilterService.GetAllItems();
                FilteredItems.Clear();
                foreach (var item in allItems)
                {
                    FilteredItems.Add(item);
                }
                return;
            }

            var filtered = _quickFilterService.ApplyFilter(filter);
            FilteredItems.Clear();
            foreach (var item in filtered)
            {
                FilteredItems.Add(item);
            }
        }

        [RelayCommand]
        private void ClearFilter()
        {
            FilterText = string.Empty;
        }

        [RelayCommand]
        private void SetFilterType(string type)
        {
            _quickFilterService.SetFilterType(type);
            ApplyFilter(FilterText);
        }
    }

    /// <summary>
    /// ViewModel for filter builder
    /// </summary>
    public partial class FilterBuilderViewModel : ObservableObject
    {
        private ObservableCollection<FilterRule> _rules = new();
        private ObservableCollection<FilterPreset> _presets = new();

        public ObservableCollection<FilterRule> Rules
        {
            get => _rules;
            set => SetProperty(ref _rules, value);
        }

        public ObservableCollection<FilterPreset> Presets
        {
            get => _presets;
            set => SetProperty(ref _presets, value);
        }

        public FilterBuilderViewModel()
        {
            InitializeDefaultRules();
        }

        private void InitializeDefaultRules()
        {
            Rules.Add(new FilterRule { Property = "Name", Operator = "Contains", Value = "" });
        }

        [RelayCommand]
        private void AddRule()
        {
            Rules.Add(new FilterRule { Property = "Name", Operator = "Contains", Value = "" });
        }

        [RelayCommand]
        private void RemoveRule(FilterRule? rule)
        {
            if (rule == null) return;
            Rules.Remove(rule);
        }

        [RelayCommand]
        private void ApplyFilter()
        {
            var filter = BuildFilterFromRules();
            System.Windows.MessageBox.Show(
                $"Filter applied: {filter}",
                "Filter Active",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
        }

        [RelayCommand]
        private async Task SaveAsPresetAsync(string presetName)
        {
            var preset = new FilterPreset
            {
                Name = presetName,
                Rules = Rules.ToList()
            };

            Presets.Add(preset);
            try
            {
                var json = System.Text.Json.JsonSerializer.Serialize(Presets,
                    new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                var presetPath = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Winhance", "FilterPresets.json");
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(presetPath)!);
                System.IO.File.WriteAllText(presetPath, json);
                System.Windows.MessageBox.Show(
                    "Preset saved successfully.",
                    "Save Complete",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to save preset: {ex.Message}",
                    "Save Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
            await Task.CompletedTask;
        }

        [RelayCommand]
        private void LoadPreset(FilterPreset? preset)
        {
            if (preset == null) return;

            Rules.Clear();
            foreach (var rule in preset.Rules)
            {
                Rules.Add(new FilterRule
                {
                    Property = rule.Property,
                    Operator = rule.Operator,
                    Value = rule.Value
                });
            }
        }

        private string BuildFilterFromRules()
        {
            if (!Rules.Any()) return "(No filters)";
            return string.Join(" AND ", Rules.Select(r => $"{r.Property} {r.Operator} '{r.Value}'"));
        }
    }

    /// <summary>
    /// ViewModel for duplicate finder
    /// </summary>
    public partial class DuplicateFinderViewModel : ObservableObject
    {
        private readonly IDuplicateFinderService _duplicateFinderService;
        private ObservableCollection<DuplicateGroup> _duplicateGroups = new();

        [ObservableProperty]
        private bool _isScanning;

        [ObservableProperty]
        private string _scanStatus = string.Empty;

        [ObservableProperty]
        private DuplicateScanOptions _scanOptions = new();

        public ObservableCollection<DuplicateGroup> DuplicateGroups
        {
            get => _duplicateGroups;
            set => SetProperty(ref _duplicateGroups, value);
        }

        public DuplicateFinderViewModel(IDuplicateFinderService duplicateFinderService)
        {
            _duplicateFinderService = duplicateFinderService;
        }

        [RelayCommand]
        private async Task StartScanAsync(string searchPath)
        {
            IsScanning = true;
            ScanStatus = "Scanning for duplicates...";

            try
            {
                var groups = await _duplicateFinderService.FindDuplicatesAsync(searchPath, ScanOptions);
                
                DuplicateGroups.Clear();
                foreach (var group in groups)
                {
                    DuplicateGroups.Add(group);
                }

                ScanStatus = $"Found {groups.Count} duplicate groups";
            }
            catch (Exception ex)
            {
                ScanStatus = $"Scan failed: {ex.Message}";
            }
            finally
            {
                IsScanning = false;
            }
        }

        [RelayCommand]
        private async Task DeleteDuplicatesAsync(DuplicateGroup? group, bool keepNewest)
        {
            if (group == null) return;

            try
            {
                await _duplicateFinderService.DeleteDuplicatesAsync(group, keepNewest);
                DuplicateGroups.Remove(group);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to delete duplicates: {ex.Message}",
                    "Delete Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void SelectAllInGroup(DuplicateGroup? group)
        {
            if (group == null) return;

            foreach (var file in group.Files)
            {
                file.IsSelected = true;
            }
        }
    }

    // Model classes
    public class SearchFilter
    {
        public string Name { get; set; } = string.Empty;
        public FilterType Type { get; set; }
        public object? Value { get; set; }
    }

    public class ContentSearchResult
    {
        public string FilePath { get; set; } = string.Empty;
        public int LineNumber { get; set; }
        public string LineContent { get; set; } = string.Empty;
        public string Context { get; set; } = string.Empty;
    }

    public class FilterRule
    {
        public string Property { get; set; } = string.Empty;
        public string Operator { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
    }

    public class FilterPreset
    {
        public string Name { get; set; } = string.Empty;
        public List<FilterRule> Rules { get; set; } = new();
    }

    public class DuplicateGroup
    {
        public string Hash { get; set; } = string.Empty;
        public long Size { get; set; }
        public ObservableCollection<DuplicateFile> Files { get; set; } = new();
    }

    public class DuplicateFile
    {
        public string Path { get; set; } = string.Empty;
        public DateTime ModifiedDate { get; set; }
        public bool IsSelected { get; set; }
    }

    public class DuplicateScanOptions
    {
        public bool IncludeFiles { get; set; } = true;
        public bool IncludeImages { get; set; } = true;
        public bool IncludeVideos { get; set; } = false;
        public long MinFileSize { get; set; } = 1024;
        public bool CompareContent { get; set; } = true;
    }
}
