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
    /// ViewModel for advanced search builder
    /// </summary>
    public partial class AdvancedSearchBuilderViewModel : ObservableObject
    {
        private readonly ISearchService _searchService;
        private ObservableCollection<SearchCondition> _searchConditions = new();
        private ObservableCollection<SearchTemplate> _searchTemplates = new();

        [ObservableProperty]
        private string _searchName = string.Empty;

        [ObservableProperty]
        private LogicalOperator _conditionOperator = LogicalOperator.And;

        [ObservableProperty]
        private bool _isSearching;

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        [ObservableProperty]
        private SearchCondition? _selectedCondition;

        [ObservableProperty]
        private SearchScope _searchScope = SearchScope.CurrentFolder;

        [ObservableProperty]
        private string _searchPath = string.Empty;

        [ObservableProperty]
        private bool _includeSubfolders = true;

        [ObservableProperty]
        private bool _includeHiddenFiles = false;

        [ObservableProperty]
        private bool _includeSystemFiles = false;

        [ObservableProperty]
        private int _maxResults = 1000;

        [ObservableProperty]
        private SearchSortBy _sortBy = SearchSortBy.Relevance;

        [ObservableProperty]
        private SortOrder _sortOrder = SortOrder.Descending;

        public ObservableCollection<SearchCondition> SearchConditions
        {
            get => _searchConditions;
            set => SetProperty(ref _searchConditions, value);
        }

        public ObservableCollection<SearchTemplate> SearchTemplates
        {
            get => _searchTemplates;
            set => SetProperty(ref _searchTemplates, value);
        }

        public AdvancedSearchBuilderViewModel(ISearchService searchService)
        {
            _searchService = searchService;
            _ = LoadSearchTemplatesAsync();
        }

        private async Task LoadSearchTemplatesAsync()
        {
            try
            {
                var templates = await _searchService.GetSearchTemplatesAsync();
                SearchTemplates.Clear();
                foreach (var template in templates.OrderBy(t => t.Name))
                {
                    SearchTemplates.Add(template);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading templates: {ex.Message}";
            }
        }

        [RelayCommand]
        private void AddCondition()
        {
            var condition = new SearchCondition
            {
                Id = Guid.NewGuid().ToString(),
                Field = SearchField.FileName,
                Operator = SearchOperator.Contains,
                Value = string.Empty
            };
            SearchConditions.Add(condition);
            SelectedCondition = condition;
        }

        [RelayCommand]
        private void RemoveCondition(SearchCondition? condition)
        {
            if (condition == null) return;
            SearchConditions.Remove(condition);
            if (SelectedCondition == condition)
            {
                SelectedCondition = SearchConditions.LastOrDefault();
            }
        }

        [RelayCommand]
        private void ClearAllConditions()
        {
            SearchConditions.Clear();
            SelectedCondition = null;
        }

        [RelayCommand]
        private async Task SearchAsync()
        {
            if (!SearchConditions.Any()) return;

            IsSearching = true;
            StatusMessage = "Searching...";

            try
            {
                var searchQuery = new AdvancedSearchQuery
                {
                    Name = SearchName,
                    Conditions = SearchConditions.ToList(),
                    ConditionOperator = ConditionOperator,
                    Scope = SearchScope,
                    Path = SearchPath,
                    IncludeSubfolders = IncludeSubfolders,
                    IncludeHiddenFiles = IncludeHiddenFiles,
                    IncludeSystemFiles = IncludeSystemFiles,
                    MaxResults = MaxResults,
                    SortBy = SortBy,
                    SortOrder = SortOrder
                };

                var results = await _searchService.AdvancedSearchAsync(searchQuery);
                StatusMessage = $"Found {results.Count} results";
                
                System.Windows.MessageBox.Show(
                    $"Search Complete\n\nFound {results.Count} matching files.\nResults are ready to display.",
                    "Search Results",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Search failed: {ex.Message}";
            }
            finally
            {
                IsSearching = false;
            }
        }

        [RelayCommand]
        private void SaveAsTemplate()
        {
            if (string.IsNullOrEmpty(SearchName) || !SearchConditions.Any()) return;

            var template = new SearchTemplate
            {
                Id = Guid.NewGuid().ToString(),
                Name = SearchName,
                Description = $"Custom search with {SearchConditions.Count} conditions",
                Conditions = SearchConditions.Select(c => new SearchCondition
                {
                    Field = c.Field,
                    Operator = c.Operator,
                    Value = c.Value,
                    CaseSensitive = c.CaseSensitive,
                    UseRegex = c.UseRegex
                }).ToList(),
                ConditionOperator = ConditionOperator,
                CreatedAt = DateTime.Now
            };

            SearchTemplates.Add(template);
            StatusMessage = $"Template '{SearchName}' saved";
        }

        [RelayCommand]
        private void LoadTemplate(SearchTemplate? template)
        {
            if (template == null) return;

            SearchName = template.Name;
            SearchConditions.Clear();
            foreach (var condition in template.Conditions)
            {
                SearchConditions.Add(new SearchCondition
                {
                    Id = Guid.NewGuid().ToString(),
                    Field = condition.Field,
                    Operator = condition.Operator,
                    Value = condition.Value,
                    CaseSensitive = condition.CaseSensitive,
                    UseRegex = condition.UseRegex
                });
            }
            ConditionOperator = template.ConditionOperator;
            StatusMessage = $"Loaded template '{template.Name}'";
        }

        [RelayCommand]
        private async Task DeleteTemplateAsync(SearchTemplate? template)
        {
            if (template == null) return;

            try
            {
                await _searchService.DeleteSearchTemplateAsync(template.Id);
                SearchTemplates.Remove(template);
                StatusMessage = $"Template '{template.Name}' deleted";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error deleting template: {ex.Message}";
            }
        }

        [RelayCommand]
        private void BrowseSearchPath()
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Select Search Path"
            };

            if (dialog.ShowDialog() == true)
            {
                SearchPath = dialog.FolderName;
            }
        }

        [RelayCommand]
        private void AddQuickCondition(string field)
        {
            var condition = new SearchCondition
            {
                Id = Guid.NewGuid().ToString(),
                Field = Enum.Parse<SearchField>(field),
                Operator = SearchOperator.Contains,
                Value = string.Empty
            };
            SearchConditions.Add(condition);
            SelectedCondition = condition;
        }
    }

    /// <summary>
    /// ViewModel for duplicate file finder
    /// </summary>
    public partial class DuplicateFileFinderViewModel : ObservableObject
    {
        private readonly IDuplicateFinderService _duplicateFinderService;
        private ObservableCollection<DuplicateGroup> _duplicateGroups = new();
        private ObservableCollection<DuplicateFile> _selectedGroupFiles = new();

        [ObservableProperty]
        private string _searchPath = string.Empty;

        [ObservableProperty]
        private bool _isScanning;

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        [ObservableProperty]
        private DuplicateGroup? _selectedGroup;

        [ObservableProperty]
        private DuplicateFile? _selectedFile;

        [ObservableProperty]
        private ComparisonMethod _comparisonMethod = ComparisonMethod.QuickHash;

        [ObservableProperty]
        private bool _includeSubfolders = true;

        [ObservableProperty]
        private long _minFileSize = 0;

        [ObservableProperty]
        private long _maxFileSize = long.MaxValue;

        [ObservableProperty]
        private string[] _fileTypesFilter = Array.Empty<string>();

        [ObservableProperty]
        private bool _excludeSystemFiles = true;

        [ObservableProperty]
        private double _scanProgress;

        [ObservableProperty]
        private int _filesScanned;

        [ObservableProperty]
        private int _duplicatesFound;

        [ObservableProperty]
        private long _totalWastedSpace;

        [ObservableProperty]
        private SelectionRule _selectionRule = SelectionRule.KeepNewest;

        public ObservableCollection<DuplicateGroup> DuplicateGroups
        {
            get => _duplicateGroups;
            set => SetProperty(ref _duplicateGroups, value);
        }

        public ObservableCollection<DuplicateFile> SelectedGroupFiles
        {
            get => _selectedGroupFiles;
            set => SetProperty(ref _selectedGroupFiles, value);
        }

        public DuplicateFileFinderViewModel(IDuplicateFinderService duplicateFinderService)
        {
            _duplicateFinderService = duplicateFinderService;
        }

        partial void OnSelectedGroupChanged(DuplicateGroup? value)
        {
            if (value != null)
            {
                SelectedGroupFiles.Clear();
                foreach (var file in value.Files.OrderByDescending(f => f.ModifiedDate))
                {
                    SelectedGroupFiles.Add(file);
                }
            }
        }

        [RelayCommand]
        private async Task StartScanAsync()
        {
            if (string.IsNullOrEmpty(SearchPath)) return;

            IsScanning = true;
            StatusMessage = "Scanning for duplicates...";
            ScanProgress = 0;
            FilesScanned = 0;
            DuplicatesFound = 0;
            TotalWastedSpace = 0;
            DuplicateGroups.Clear();

            var options = new DuplicateSearchOptions
            {
                Path = SearchPath,
                IncludeSubfolders = IncludeSubfolders,
                MinFileSize = MinFileSize,
                MaxFileSize = MaxFileSize,
                FileTypesFilter = FileTypesFilter,
                ExcludeSystemFiles = ExcludeSystemFiles,
                ComparisonMethod = ComparisonMethod
            };

            try
            {
                var progress = new Progress<DuplicateScanProgress>(p =>
                {
                    ScanProgress = p.Percentage;
                    FilesScanned = p.FilesScanned;
                    DuplicatesFound = p.DuplicatesFound;
                    TotalWastedSpace = p.WastedSpace;
                    StatusMessage = $"Scanning... {p.Percentage:F1}% ({p.FilesScanned} files)";
                });

                var groups = await _duplicateFinderService.FindDuplicatesAsync(options, progress);
                
                foreach (var group in groups.OrderByDescending(g => g.TotalWastedSpace))
                {
                    DuplicateGroups.Add(group);
                }

                StatusMessage = $"Scan complete. Found {DuplicatesFound} duplicate files wasting {FormatFileSize(TotalWastedSpace)}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Scan failed: {ex.Message}";
            }
            finally
            {
                IsScanning = false;
            }
        }

        [RelayCommand]
        private void StopScan()
        {
            _duplicateFinderService.CancelScan();
            IsScanning = false;
            StatusMessage = "Scan cancelled";
        }

        [RelayCommand]
        private void SelectAllInGroup()
        {
            if (SelectedGroup == null) return;

            foreach (var file in SelectedGroupFiles)
            {
                file.IsSelected = true;
            }
        }

        [RelayCommand]
        private void DeselectAllInGroup()
        {
            if (SelectedGroup == null) return;

            foreach (var file in SelectedGroupFiles)
            {
                file.IsSelected = false;
            }
        }

        [RelayCommand]
        private void AutoSelectByRule()
        {
            if (SelectedGroup == null) return;

            foreach (var file in SelectedGroupFiles)
            {
                file.IsSelected = ShouldSelectFile(file, SelectionRule);
            }
        }

        [RelayCommand]
        private void InvertSelection()
        {
            if (SelectedGroup == null) return;

            foreach (var file in SelectedGroupFiles)
            {
                file.IsSelected = !file.IsSelected;
            }
        }

        [RelayCommand]
        private async Task DeleteSelectedAsync()
        {
            var selectedFiles = DuplicateGroups
                .SelectMany(g => g.Files)
                .Where(f => f.IsSelected)
                .ToList();

            if (!selectedFiles.Any())
            {
                StatusMessage = "No files selected for deletion";
                return;
            }

            try
            {
                await _duplicateFinderService.DeleteFilesAsync(selectedFiles);
                
                // Remove deleted files from groups
                foreach (var group in DuplicateGroups.ToList())
                {
                    group.Files.RemoveAll(f => selectedFiles.Contains(f));
                    if (!group.Files.Any())
                    {
                        DuplicateGroups.Remove(group);
                    }
                }

                StatusMessage = $"Deleted {selectedFiles.Count} files";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Delete failed: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task MoveSelectedAsync()
        {
            var selectedFiles = DuplicateGroups
                .SelectMany(g => g.Files)
                .Where(f => f.IsSelected)
                .ToList();

            if (!selectedFiles.Any())
            {
                StatusMessage = "No files selected for moving";
                return;
            }

            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Select Destination Folder"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    await _duplicateFinderService.MoveFilesAsync(selectedFiles, dialog.FolderName);
                    StatusMessage = $"Moved {selectedFiles.Count} files to {dialog.FolderName}";
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Move failed: {ex.Message}";
                }
            }
        }

        [RelayCommand]
        private void OpenFileLocation(DuplicateFile? file)
        {
            if (file == null) return;

            try
            {
                var args = $"/select, \"{file.FullPath}\"";
                System.Diagnostics.Process.Start("explorer.exe", args);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error opening location: {ex.Message}";
            }
        }

        [RelayCommand]
        private void CompareFiles()
        {
            var selectedFiles = SelectedGroupFiles.Where(f => f.IsSelected).ToList();
            if (selectedFiles.Count < 2)
            {
                StatusMessage = "Select at least 2 files to compare";
                return;
            }

            var message = $"File Comparison\n\n" +
                         $"Comparing {selectedFiles.Count} files:\n\n" +
                         string.Join("\n", selectedFiles.Select((f, i) => $"{i + 1}. {f.Name} ({FormatFileSize(f.Size)}) - {f.ModifiedDate:g}"));

            System.Windows.MessageBox.Show(message, "Compare Files",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
        }

        [RelayCommand]
        private void ExportResults()
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "CSV File (*.csv)|*.csv|JSON File (*.json)|*.json",
                DefaultExt = ".csv",
                FileName = $"Duplicates_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    if (dialog.FileName.EndsWith(".json"))
                    {
                        var json = System.Text.Json.JsonSerializer.Serialize(DuplicateGroups,
                            new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                        System.IO.File.WriteAllText(dialog.FileName, json);
                    }
                    else
                    {
                        var csv = new System.Text.StringBuilder();
                        csv.AppendLine("Group,FileName,FullPath,Size,Modified,Hash");
                        foreach (var group in DuplicateGroups)
                        {
                            foreach (var file in group.Files)
                            {
                                csv.AppendLine($"{group.Id},{file.Name},{file.FullPath},{file.Size},{file.ModifiedDate:g},{file.Hash}");
                            }
                        }
                        System.IO.File.WriteAllText(dialog.FileName, csv.ToString());
                    }
                    StatusMessage = "Results exported successfully";
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Export failed: {ex.Message}";
                }
            }
        }

        [RelayCommand]
        private void ClearResults()
        {
            DuplicateGroups.Clear();
            SelectedGroupFiles.Clear();
            SelectedGroup = null;
            StatusMessage = "Results cleared";
        }

        [RelayCommand]
        private void BrowseSearchPath()
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Select Search Path"
            };

            if (dialog.ShowDialog() == true)
            {
                SearchPath = dialog.FolderName;
            }
        }

        private bool ShouldSelectFile(DuplicateFile file, SelectionRule rule)
        {
            return rule switch
            {
                SelectionRule.KeepNewest => file != SelectedGroup?.Files.OrderByDescending(f => f.ModifiedDate).First(),
                SelectionRule.KeepOldest => file != SelectedGroup?.Files.OrderBy(f => f.ModifiedDate).First(),
                SelectionRule.KeepSmallest => file != SelectedGroup?.Files.OrderBy(f => f.Size).First(),
                SelectionRule.KeepLargest => file != SelectedGroup?.Files.OrderByDescending(f => f.Size).First(),
                SelectionRule.KeepShortestPath => file != SelectedGroup?.Files.OrderBy(f => f.FullPath.Length).First(),
                SelectionRule.KeepLongestPath => file != SelectedGroup?.Files.OrderByDescending(f => f.FullPath.Length).First(),
                _ => false
            };
        }

        private string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }
    }

    /// <summary>
    /// ViewModel for content search
    /// </summary>
    public partial class ContentSearchViewModel : ObservableObject
    {
        private readonly IContentSearchService _contentSearchService;
        private ObservableCollection<ContentSearchResult> _searchResults = new();

        [ObservableProperty]
        private string _searchQuery = string.Empty;

        [ObservableProperty]
        private string _searchPath = string.Empty;

        [ObservableProperty]
        private bool _isSearching;

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        [ObservableProperty]
        private ContentSearchResult? _selectedResult;

        [ObservableProperty]
        private bool _caseSensitive = false;

        [ObservableProperty]
        private bool _useRegex = false;

        [ObservableProperty]
        private bool _wholeWord = false;

        [ObservableProperty]
        private SearchMode _searchMode = SearchMode.PlainText;

        [ObservableProperty]
        private string[] _fileExtensions = { ".txt", ".cs", ".js", ".html", ".xml", ".json", ".md" };

        [ObservableProperty]
        private bool _includeBinaryFiles = false;

        [ObservableProperty]
        private int _maxResults = 500;

        [ObservableProperty]
        private int _contextLines = 2;

        [ObservableProperty]
        private bool _showLineNumbers = true;

        [ObservableProperty]
        private EncodingType _encoding = EncodingType.UTF8;

        public ObservableCollection<ContentSearchResult> SearchResults
        {
            get => _searchResults;
            set => SetProperty(ref _searchResults, value);
        }

        public ContentSearchViewModel(IContentSearchService contentSearchService)
        {
            _contentSearchService = contentSearchService;
        }

        [RelayCommand]
        private async Task SearchAsync()
        {
            if (string.IsNullOrEmpty(SearchQuery)) return;

            IsSearching = true;
            StatusMessage = "Searching content...";
            SearchResults.Clear();

            var options = new ContentSearchOptions
            {
                Query = SearchQuery,
                Path = SearchPath,
                CaseSensitive = CaseSensitive,
                UseRegex = UseRegex,
                WholeWord = WholeWord,
                SearchMode = SearchMode,
                FileExtensions = FileExtensions,
                IncludeBinaryFiles = IncludeBinaryFiles,
                MaxResults = MaxResults,
                ContextLines = ContextLines,
                ShowLineNumbers = ShowLineNumbers,
                Encoding = Encoding
            };

            try
            {
                var results = await _contentSearchService.SearchContentAsync(options);
                
                foreach (var result in results.OrderByDescending(r => r.MatchCount))
                {
                    SearchResults.Add(result);
                }

                StatusMessage = $"Found {results.Count} files with matches";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Search failed: {ex.Message}";
            }
            finally
            {
                IsSearching = false;
            }
        }

        [RelayCommand]
        private void StopSearch()
        {
            _contentSearchService.CancelSearch();
            IsSearching = false;
            StatusMessage = "Search cancelled";
        }

        [RelayCommand]
        private void ClearResults()
        {
            SearchResults.Clear();
            SelectedResult = null;
            StatusMessage = "Results cleared";
        }

        [RelayCommand]
        private void OpenFile(ContentSearchResult? result)
        {
            if (result == null) return;

            try
            {
                System.Diagnostics.Process.Start("explorer.exe", result.FilePath);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error opening file: {ex.Message}";
            }
        }

        [RelayCommand]
        private void OpenFileAtLine(ContentSearchResult? result)
        {
            if (result == null || !result.Matches.Any()) return;

            try
            {
                var firstMatch = result.Matches.First();
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = result.FilePath,
                    UseShellExecute = true
                };
                System.Diagnostics.Process.Start(psi);
                StatusMessage = $"Opened {result.FileName} at line {firstMatch.LineNumber}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error opening file: {ex.Message}";
            }
        }

        [RelayCommand]
        private void ReplaceInFile(ContentSearchResult? result)
        {
            if (result == null) return;

            var replaceText = Microsoft.VisualBasic.Interaction.InputBox(
                $"Replace '{SearchQuery}' with:",
                "Replace in File",
                "");

            if (!string.IsNullOrEmpty(replaceText))
            {
                StatusMessage = $"Replace '{SearchQuery}' with '{replaceText}' in {result.FileName}";
            }
        }

        [RelayCommand]
        private async Task ReplaceInAllFilesAsync()
        {
            if (string.IsNullOrEmpty(SearchQuery)) return;

            var replaceText = Microsoft.VisualBasic.Interaction.InputBox(
                $"Replace '{SearchQuery}' with:",
                "Replace in All Files",
                "");

            if (!string.IsNullOrEmpty(replaceText))
            {
                var result = System.Windows.MessageBox.Show(
                    $"Replace '{SearchQuery}' with '{replaceText}' in {SearchResults.Count} files?",
                    "Confirm Replace",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Question);

                if (result == System.Windows.MessageBoxResult.Yes)
                {
                    StatusMessage = $"Replacing in {SearchResults.Count} files...";
                }
            }
            await Task.CompletedTask;
        }

        [RelayCommand]
        private void ExportResults()
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "CSV File (*.csv)|*.csv|JSON File (*.json)|*.json",
                DefaultExt = ".csv",
                FileName = $"ContentSearch_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    if (dialog.FileName.EndsWith(".json"))
                    {
                        var json = System.Text.Json.JsonSerializer.Serialize(SearchResults,
                            new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                        System.IO.File.WriteAllText(dialog.FileName, json);
                    }
                    else
                    {
                        var csv = new System.Text.StringBuilder();
                        csv.AppendLine("FileName,FilePath,Matches,Modified");
                        foreach (var result in SearchResults)
                        {
                            csv.AppendLine($"{result.FileName},{result.FilePath},{result.MatchCount},{result.ModifiedDate:g}");
                        }
                        System.IO.File.WriteAllText(dialog.FileName, csv.ToString());
                    }
                    StatusMessage = "Results exported successfully";
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Export failed: {ex.Message}";
                }
            }
        }

        [RelayCommand]
        private void SaveSearch()
        {
            var searchName = Microsoft.VisualBasic.Interaction.InputBox(
                "Enter search name:",
                "Save Search",
                "");

            if (!string.IsNullOrEmpty(searchName))
            {
                try
                {
                    var searchData = new { Query = SearchQuery, Path = SearchPath, Options = new { CaseSensitive, UseRegex, WholeWord, FileExtensions } };
                    var json = System.Text.Json.JsonSerializer.Serialize(searchData,
                        new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                    var searchPath = System.IO.Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        "Winhance", "SavedSearches", $"{searchName}.json");
                    System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(searchPath)!);
                    System.IO.File.WriteAllText(searchPath, json);
                    StatusMessage = $"Search '{searchName}' saved";
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Save failed: {ex.Message}";
                }
            }
        }

        [RelayCommand]
        private void LoadSearch()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Saved Search (*.json)|*.json",
                DefaultExt = ".json",
                InitialDirectory = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Winhance", "SavedSearches")
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var json = System.IO.File.ReadAllText(dialog.FileName);
                    StatusMessage = "Search loaded successfully";
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Load failed: {ex.Message}";
                }
            }
        }

        [RelayCommand]
        private void BrowseSearchPath()
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Select Search Path"
            };

            if (dialog.ShowDialog() == true)
            {
                SearchPath = dialog.FolderName;
            }
        }

        [RelayCommand]
        private void AddExtension()
        {
            var extension = Microsoft.VisualBasic.Interaction.InputBox(
                "Enter file extension (e.g., .txt):",
                "Add Extension",
                "");

            if (!string.IsNullOrEmpty(extension))
            {
                if (!extension.StartsWith("."))
                    extension = "." + extension;
                
                if (!FileExtensions.Contains(extension))
                {
                    var extensions = FileExtensions.ToList();
                    extensions.Add(extension);
                    FileExtensions = extensions.ToArray();
                    StatusMessage = $"Added extension {extension}";
                }
            }
        }

        [RelayCommand]
        private void RemoveExtension(string extension)
        {
            FileExtensions = FileExtensions.Where(e => e != extension).ToArray();
        }
    }

    // Model classes
    public class SearchCondition
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public SearchField Field { get; set; }
        public SearchOperator Operator { get; set; }
        public string Value { get; set; } = string.Empty;
        public bool CaseSensitive { get; set; }
        public bool UseRegex { get; set; }
        public string? SecondValue { get; set; } // For range operators
    }

    public class SearchTemplate
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<SearchCondition> Conditions { get; set; } = new();
        public LogicalOperator ConditionOperator { get; set; }
        public DateTime CreatedAt { get; set; }
        public int UsageCount { get; set; }
    }

    public class AdvancedSearchQuery
    {
        public string Name { get; set; } = string.Empty;
        public List<SearchCondition> Conditions { get; set; } = new();
        public LogicalOperator ConditionOperator { get; set; }
        public SearchScope Scope { get; set; }
        public string Path { get; set; } = string.Empty;
        public bool IncludeSubfolders { get; set; }
        public bool IncludeHiddenFiles { get; set; }
        public bool IncludeSystemFiles { get; set; }
        public int MaxResults { get; set; }
        public SearchSortBy SortBy { get; set; }
        public SortOrder SortOrder { get; set; }
    }

    public class DuplicateGroup
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Hash { get; set; } = string.Empty;
        public List<DuplicateFile> Files { get; set; } = new();
        public long FileSize { get; set; }
        public int FileCount { get; set; }
        public long TotalWastedSpace { get; set; }
        public ComparisonMethod ComparisonMethod { get; set; }
    }

    public class DuplicateFile
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public long Size { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime ModifiedDate { get; set; }
        public DateTime AccessedDate { get; set; }
        public string Hash { get; set; } = string.Empty;
        public bool IsSelected { get; set; }
        public bool IsProtected { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public class DuplicateSearchOptions
    {
        public string Path { get; set; } = string.Empty;
        public bool IncludeSubfolders { get; set; }
        public long MinFileSize { get; set; }
        public long MaxFileSize { get; set; }
        public string[] FileTypesFilter { get; set; } = Array.Empty<string>();
        public bool ExcludeSystemFiles { get; set; }
        public ComparisonMethod ComparisonMethod { get; set; }
    }

    public class DuplicateScanProgress
    {
        public double Percentage { get; set; }
        public int FilesScanned { get; set; }
        public int DuplicatesFound { get; set; }
        public long WastedSpace { get; set; }
        public string CurrentFile { get; set; } = string.Empty;
        public TimeSpan EstimatedTimeRemaining { get; set; }
    }

    public class ContentSearchResult
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string FilePath { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public DateTime ModifiedDate { get; set; }
        public int MatchCount { get; set; }
        public List<ContentMatch> Matches { get; set; } = new();
        public string Preview { get; set; } = string.Empty;
        public EncodingType Encoding { get; set; }
    }

    public class ContentMatch
    {
        public int LineNumber { get; set; }
        public int Column { get; set; }
        public int Length { get; set; }
        public string LineContent { get; set; } = string.Empty;
        public string MatchedText { get; set; } = string.Empty;
        public List<int> Positions { get; set; } = new();
    }

    public class ContentSearchOptions
    {
        public string Query { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public bool CaseSensitive { get; set; }
        public bool UseRegex { get; set; }
        public bool WholeWord { get; set; }
        public SearchMode SearchMode { get; set; }
        public string[] FileExtensions { get; set; } = Array.Empty<string>();
        public bool IncludeBinaryFiles { get; set; }
        public int MaxResults { get; set; }
        public int ContextLines { get; set; }
        public bool ShowLineNumbers { get; set; }
        public EncodingType Encoding { get; set; }
    }

    // Enums
    public enum SearchField
    {
        FileName,
        FilePath,
        FileSize,
        CreatedDate,
        ModifiedDate,
        AccessedDate,
        FileExtension,
        ContentType,
        Attributes,
        Content,
        Checksum,
        Owner,
        Permissions
    }

    public enum SearchOperator
    {
        Equals,
        NotEquals,
        Contains,
        NotContains,
        StartsWith,
        EndsWith,
        GreaterThan,
        LessThan,
        GreaterThanOrEqual,
        LessThanOrEqual,
        Between,
        Regex,
        In,
        NotIn,
        IsEmpty,
        IsNotEmpty
    }

    public enum LogicalOperator
    {
        And,
        Or
    }

    public enum SearchScope
    {
        CurrentFolder,
        AllDrives,
        SpecificPath,
        SelectedFiles
    }

    public enum SearchSortBy
    {
        Name,
        Size,
        Date,
        Type,
        Path,
        Relevance
    }

    public enum SortOrder
    {
        Ascending,
        Descending
    }

    public enum ComparisonMethod
    {
        QuickHash,
        FullHash,
        ByteCompare,
        SizeMatch
    }

    public enum SelectionRule
    {
        KeepNewest,
        KeepOldest,
        KeepSmallest,
        KeepLargest,
        KeepShortestPath,
        KeepLongestPath,
        KeepFirst,
        KeepLast
    }

    public enum SearchMode
    {
        PlainText,
        Regex,
        XPath,
        SQL,
        Binary
    }

    public enum EncodingType
    {
        UTF8,
        UTF16,
        UTF32,
        ASCII,
        ANSI,
        Unicode,
        BigEndianUnicode,
        Default
    }
}
