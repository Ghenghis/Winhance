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
    /// ViewModel for disk space usage overview
    /// </summary>
    public partial class DiskSpaceOverviewViewModel : ObservableObject
    {
        private readonly ISpaceAnalyzerService _spaceAnalyzerService;
        private ObservableCollection<DiskDriveInfo> _drives = new();
        private ObservableCollection<SpaceUsageItem> _usageItems = new();

        [ObservableProperty]
        private DiskDriveInfo? _selectedDrive;

        [ObservableProperty]
        private bool _isAnalyzing;

        [ObservableProperty]
        private string? _analysisStatus;

        public ObservableCollection<DiskDriveInfo> Drives
        {
            get => _drives;
            set => SetProperty(ref _drives, value);
        }

        public ObservableCollection<SpaceUsageItem> UsageItems
        {
            get => _usageItems;
            set => SetProperty(ref _usageItems, value);
        }

        public DiskSpaceOverviewViewModel(ISpaceAnalyzerService spaceAnalyzerService)
        {
            _spaceAnalyzerService = spaceAnalyzerService;
        }

        [RelayCommand]
        private async Task LoadDrivesAsync()
        {
            try
            {
                var drives = await _spaceAnalyzerService.GetDrivesAsync();
                Drives.Clear();
                foreach (var drive in drives)
                {
                    Drives.Add(drive);
                }
            }
            catch (Exception ex)
            {
                AnalysisStatus = $"Failed to load drives: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task AnalyzeDriveAsync(DiskDriveInfo? drive)
        {
            if (drive == null) return;

            SelectedDrive = drive;
            IsAnalyzing = true;
            AnalysisStatus = $"Analyzing {drive.Name}...";

            try
            {
                var usage = await _spaceAnalyzerService.AnalyzeDriveAsync(drive.RootPath);
                
                UsageItems.Clear();
                foreach (var item in usage)
                {
                    UsageItems.Add(item);
                }

                AnalysisStatus = $"Analysis complete: {drive.Name}";
            }
            catch (Exception ex)
            {
                AnalysisStatus = $"Analysis failed: {ex.Message}";
            }
            finally
            {
                IsAnalyzing = false;
            }
        }

        [RelayCommand]
        private async Task RefreshDriveAsync()
        {
            if (SelectedDrive == null) return;
            await AnalyzeDriveAsync(SelectedDrive);
        }

        [RelayCommand]
        private async Task ExportReportAsync()
        {
            if (SelectedDrive == null) return;

            try
            {
                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "HTML Report (*.html)|*.html|CSV File (*.csv)|*.csv",
                    DefaultExt = ".html",
                    FileName = $"DiskSpaceReport_{SelectedDrive.Name.Replace(":", "")}_{DateTime.Now:yyyyMMdd_HHmmss}.html"
                };

                if (dialog.ShowDialog() == true)
                {
                    var html = $"<html><head><title>Disk Space Report - {SelectedDrive.Name}</title></head><body>" +
                               $"<h1>Disk Space Report: {SelectedDrive.Name}</h1>" +
                               $"<p>Total Space: {SelectedDrive.TotalSpace / (1024.0 * 1024.0 * 1024.0):F2} GB</p>" +
                               $"<p>Free Space: {SelectedDrive.FreeSpace / (1024.0 * 1024.0 * 1024.0):F2} GB</p>" +
                               $"<p>Used Space: {SelectedDrive.UsedSpace / (1024.0 * 1024.0 * 1024.0):F2} GB</p>" +
                               "<h2>Top Usage Items</h2><ul>";
                    
                    foreach (var item in UsageItems.Take(20))
                    {
                        html += $"<li>{item.Name}: {item.Size / (1024.0 * 1024.0):F2} MB ({item.Percentage:F1}%)</li>";
                    }
                    
                    html += "</ul></body></html>";
                    await System.IO.File.WriteAllTextAsync(dialog.FileName, html);
                    AnalysisStatus = $"Report exported to {dialog.FileName}";
                }
            }
            catch (Exception ex)
            {
                AnalysisStatus = $"Export failed: {ex.Message}";
            }
        }
    }

    /// <summary>
    /// ViewModel for folder size analysis
    /// </summary>
    public partial class FolderSizeAnalysisViewModel : ObservableObject
    {
        private readonly ISpaceAnalyzerService _spaceAnalyzerService;
        private ObservableCollection<FolderSizeItem> _folders = new();
        private ObservableCollection<FileSizeItem> _files = new();

        [ObservableProperty]
        private string _currentPath = string.Empty;

        [ObservableProperty]
        private bool _isAnalyzing;

        [ObservableProperty]
        private string? _analysisStatus;

        [ObservableProperty]
        private SortCriteria _sortBy = SortCriteria.Size;

        [ObservableProperty]
        private SortDirection _sortDirection = SortDirection.Descending;

        public ObservableCollection<FolderSizeItem> Folders
        {
            get => _folders;
            set => SetProperty(ref _folders, value);
        }

        public ObservableCollection<FileSizeItem> Files
        {
            get => _files;
            set => SetProperty(ref _files, value);
        }

        public FolderSizeAnalysisViewModel(ISpaceAnalyzerService spaceAnalyzerService)
        {
            _spaceAnalyzerService = spaceAnalyzerService;
        }

        [RelayCommand]
        private async Task AnalyzeFolderAsync(string? folderPath)
        {
            if (string.IsNullOrEmpty(folderPath)) return;

            CurrentPath = folderPath;
            IsAnalyzing = true;
            AnalysisStatus = "Analyzing folder...";

            try
            {
                var analysis = await _spaceAnalyzerService.AnalyzeFolderAsync(folderPath);
                
                Folders.Clear();
                foreach (var folder in analysis.Folders)
                {
                    Folders.Add(folder);
                }

                Files.Clear();
                foreach (var file in analysis.Files)
                {
                    Files.Add(file);
                }

                SortResults();
                AnalysisStatus = $"Found {analysis.Folders.Count} folders, {analysis.Files.Count} files";
            }
            catch (Exception ex)
            {
                AnalysisStatus = $"Analysis failed: {ex.Message}";
            }
            finally
            {
                IsAnalyzing = false;
            }
        }

        [RelayCommand]
        private void SortBy(SortCriteria criteria)
        {
            if (SortBy == criteria)
            {
                SortDirection = SortDirection == SortDirection.Ascending 
                    ? SortDirection.Descending 
                    : SortDirection.Ascending;
            }
            else
            {
                SortBy = criteria;
                SortDirection = SortDirection.Descending;
            }

            SortResults();
        }

        private void SortResults()
        {
            var folderQuery = SortBy switch
            {
                SortCriteria.Name => SortDirection == SortDirection.Ascending 
                    ? Folders.OrderBy(f => f.Name) 
                    : Folders.OrderByDescending(f => f.Name),
                SortCriteria.Size => SortDirection == SortDirection.Ascending 
                    ? Folders.OrderBy(f => f.Size) 
                    : Folders.OrderByDescending(f => f.Size),
                SortCriteria.Files => SortDirection == SortDirection.Ascending 
                    ? Folders.OrderBy(f => f.FileCount) 
                    : Folders.OrderByDescending(f => f.FileCount),
                SortCriteria.Modified => SortDirection == SortDirection.Ascending 
                    ? Folders.OrderBy(f => f.LastModified) 
                    : Folders.OrderByDescending(f => f.LastModified),
                _ => Folders.OrderByDescending(f => f.Size)
            };

            var sortedFolders = folderQuery.ToList();
            Folders.Clear();
            foreach (var folder in sortedFolders)
            {
                Folders.Add(folder);
            }

            // Sort files
            var fileQuery = SortBy switch
            {
                SortCriteria.Name => SortDirection == SortDirection.Ascending 
                    ? Files.OrderBy(f => f.Name) 
                    : Files.OrderByDescending(f => f.Name),
                SortCriteria.Size => SortDirection == SortDirection.Ascending 
                    ? Files.OrderBy(f => f.Size) 
                    : Files.OrderByDescending(f => f.Size),
                _ => Files.OrderByDescending(f => f.Size)
            };

            var sortedFiles = fileQuery.ToList();
            Files.Clear();
            foreach (var file in sortedFiles)
            {
                Files.Add(file);
            }
        }

        [RelayCommand]
        private async Task NavigateToFolderAsync(FolderSizeItem? folder)
        {
            if (folder == null) return;
            await AnalyzeFolderAsync(folder.FullPath);
        }

        [RelayCommand]
        private void NavigateToParent()
        {
            var parent = System.IO.Directory.GetParent(CurrentPath);
            if (parent != null)
            {
                _ = AnalyzeFolderAsync(parent.FullName);
            }
        }
    }

    /// <summary>
    /// ViewModel for largest files finder
    /// </summary>
    public partial class LargestFilesViewModel : ObservableObject
    {
        private readonly ISpaceAnalyzerService _spaceAnalyzerService;
        private ObservableCollection<LargeFileItem> _largestFiles = new();

        [ObservableProperty]
        private string _searchPath = string.Empty;

        [ObservableProperty]
        private int _fileCount = 100;

        [ObservableProperty]
        private long _minFileSize = 1024 * 1024; // 1MB

        [ObservableProperty]
        private bool _includeSubfolders = true;

        [ObservableProperty]
        private bool _isSearching;

        [ObservableProperty]
        private string? _searchStatus;

        public ObservableCollection<LargeFileItem> LargestFiles
        {
            get => _largestFiles;
            set => SetProperty(ref _largestFiles, value);
        }

        public LargestFilesViewModel(ISpaceAnalyzerService spaceAnalyzerService)
        {
            _spaceAnalyzerService = spaceAnalyzerService;
        }

        [RelayCommand]
        private async Task FindLargestFilesAsync()
        {
            if (string.IsNullOrEmpty(SearchPath)) return;

            IsSearching = true;
            SearchStatus = "Searching for large files...";

            try
            {
                var options = new FileSearchOptions
                {
                    Path = SearchPath,
                    MinFileSize = MinFileSize,
                    MaxResults = FileCount,
                    IncludeSubfolders = IncludeSubfolders
                };

                var files = await _spaceAnalyzerService.FindLargestFilesAsync(options);
                
                LargestFiles.Clear();
                foreach (var file in files)
                {
                    LargestFiles.Add(file);
                }

                SearchStatus = $"Found {files.Count} large files";
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
        private void ClearResults()
        {
            LargestFiles.Clear();
            SearchStatus = null;
        }

        [RelayCommand]
        private void OpenFileLocation(LargeFileItem? file)
        {
            if (file == null) return;

            try
            {
                System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{file.FullPath}\"");
            }
            catch (Exception ex)
            {
                SearchStatus = $"Failed to open location: {ex.Message}";
            }
        }

        [RelayCommand]
        private void DeleteFile(LargeFileItem? file)
        {
            if (file == null) return;

            var result = System.Windows.MessageBox.Show(
                $"Are you sure you want to delete '{file.Name}'?\n\nSize: {file.Size / (1024.0 * 1024.0):F2} MB\nPath: {file.FullPath}\n\nThis action cannot be undone.",
                "Confirm Delete",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning);

            if (result == System.Windows.MessageBoxResult.Yes)
            {
                try
                {
                    System.IO.File.Delete(file.FullPath);
                    LargestFiles.Remove(file);
                    SearchStatus = $"Deleted {file.Name}";
                }
                catch (Exception ex)
                {
                    SearchStatus = $"Failed to delete file: {ex.Message}";
                }
            }
            catch (Exception ex)
            {
                SearchStatus = $"Failed to delete file: {ex.Message}";
            }
        }
    }

    /// <summary>
    /// ViewModel for duplicate space usage
    /// </summary>
    public partial class DuplicateSpaceViewModel : ObservableObject
    {
        private readonly ISpaceAnalyzerService _spaceAnalyzerService;
        private ObservableCollection<DuplicateSpaceGroup> _duplicateGroups = new();

        [ObservableProperty]
        private string _searchPath = string.Empty;

        [ObservableProperty]
        private bool _isScanning;

        [ObservableProperty]
        private string? _scanStatus;

        [ObservableProperty]
        private long _totalDuplicateSpace;

        public ObservableCollection<DuplicateSpaceGroup> DuplicateGroups
        {
            get => _duplicateGroups;
            set => SetProperty(ref _duplicateGroups, value);
        }

        public DuplicateSpaceViewModel(ISpaceAnalyzerService spaceAnalyzerService)
        {
            _spaceAnalyzerService = spaceAnalyzerService;
        }

        [RelayCommand]
        private async Task ScanForDuplicatesAsync()
        {
            if (string.IsNullOrEmpty(SearchPath)) return;

            IsScanning = true;
            ScanStatus = "Scanning for duplicates...";

            try
            {
                var groups = await _spaceAnalyzerService.FindDuplicateSpaceAsync(SearchPath);
                
                DuplicateGroups.Clear();
                TotalDuplicateSpace = 0;

                foreach (var group in groups)
                {
                    DuplicateGroups.Add(group);
                    TotalDuplicateSpace += group.WastedSpace;
                }

                ScanStatus = $"Found {groups.Count} duplicate groups wasting {FormatSize(TotalDuplicateSpace)}";
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
        private void SelectGroup(DuplicateSpaceGroup? group)
        {
            if (group == null) return;

            foreach (var file in group.Files)
            {
                file.IsSelected = true;
            }
        }

        [RelayCommand]
        private void DeleteSelected()
        {
            var selectedFiles = DuplicateGroups
                .SelectMany(g => g.Files)
                .Where(f => f.IsSelected)
                .ToList();

            if (selectedFiles.Count == 0)
            {
                ScanStatus = "No files selected for deletion";
                return;
            }

            var result = System.Windows.MessageBox.Show(
                $"Are you sure you want to delete {selectedFiles.Count} selected duplicate file(s)?\n\nThis action cannot be undone.",
                "Confirm Delete",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning);

            if (result == System.Windows.MessageBoxResult.Yes)
            {
                try
                {
                    foreach (var file in selectedFiles)
                    {
                        System.IO.File.Delete(file.FullPath);
                    }

                    ScanStatus = $"Deleted {selectedFiles.Count} duplicate files";
                    // Refresh results
                    _ = ScanForDuplicatesAsync();
                }
                catch (Exception ex)
                {
                    ScanStatus = $"Failed to delete files: {ex.Message}";
                }
            }
            catch (Exception ex)
            {
                ScanStatus = $"Failed to delete files: {ex.Message}";
            }
        }

        private string FormatSize(long bytes)
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
    /// ViewModel for cleanup suggestions
    /// </summary>
    public partial class CleanupSuggestionsViewModel : ObservableObject
    {
        private readonly ISpaceAnalyzerService _spaceAnalyzerService;
        private ObservableCollection<CleanupSuggestion> _suggestions = new();

        [ObservableProperty]
        private string _analysisPath = string.Empty;

        [ObservableProperty]
        private bool _isAnalyzing;

        [ObservableProperty]
        private string? _analysisStatus;

        [ObservableProperty]
        private long _totalSpaceToFree;

        public ObservableCollection<CleanupSuggestion> Suggestions
        {
            get => _suggestions;
            set => SetProperty(ref _suggestions, value);
        }

        public CleanupSuggestionsViewModel(ISpaceAnalyzerService spaceAnalyzerService)
        {
            _spaceAnalyzerService = spaceAnalyzerService;
        }

        [RelayCommand]
        private async Task AnalyzeForCleanupAsync()
        {
            if (string.IsNullOrEmpty(AnalysisPath)) return;

            IsAnalyzing = true;
            AnalysisStatus = "Analyzing for cleanup opportunities...";

            try
            {
                var suggestions = await _spaceAnalyzerService.GetCleanupSuggestionsAsync(AnalysisPath);
                
                Suggestions.Clear();
                TotalSpaceToFree = 0;

                foreach (var suggestion in suggestions)
                {
                    Suggestions.Add(suggestion);
                    TotalSpaceToFree += suggestion.SpaceToFree;
                }

                AnalysisStatus = $"Found {suggestions.Count} cleanup opportunities to free {FormatSize(TotalSpaceToFree)}";
            }
            catch (Exception ex)
            {
                AnalysisStatus = $"Analysis failed: {ex.Message}";
            }
            finally
            {
                IsAnalyzing = false;
            }
        }

        [RelayCommand]
        private async Task ExecuteSuggestionAsync(CleanupSuggestion? suggestion)
        {
            if (suggestion == null) return;

            try
            {
                await _spaceAnalyzerService.ExecuteCleanupSuggestionAsync(suggestion);
                suggestion.IsExecuted = true;
                AnalysisStatus = $"Executed: {suggestion.Description}";
            }
            catch (Exception ex)
            {
                AnalysisStatus = $"Execution failed: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task ExecuteAllSuggestionsAsync()
        {
            var executableSuggestions = Suggestions.Where(s => !s.IsExecuted).ToList();

            try
            {
                foreach (var suggestion in executableSuggestions)
                {
                    await _spaceAnalyzerService.ExecuteCleanupSuggestionAsync(suggestion);
                    suggestion.IsExecuted = true;
                }

                AnalysisStatus = $"Executed {executableSuggestions.Count} cleanup suggestions";
            }
            catch (Exception ex)
            {
                AnalysisStatus = $"Execution failed: {ex.Message}";
            }
        }

        private string FormatSize(long bytes)
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

    // Model classes
    public class DiskDriveInfo
    {
        public string Name { get; set; } = string.Empty;
        public string RootPath { get; set; } = string.Empty;
        public long TotalSpace { get; set; }
        public long FreeSpace { get; set; }
        public long UsedSpace => TotalSpace - FreeSpace;
        public string FileSystem { get; set; } = string.Empty;
        public DriveType DriveType { get; set; }
    }

    public class SpaceUsageItem
    {
        public string Name { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public long Size { get; set; }
        public double Percentage { get; set; }
        public ItemType Type { get; set; }
        public int FileCount { get; set; }
        public int FolderCount { get; set; }
    }

    public class FolderSizeItem
    {
        public string Name { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public long Size { get; set; }
        public int FileCount { get; set; }
        public int FolderCount { get; set; }
        public DateTime LastModified { get; set; }
        public double SizePercentage { get; set; }
    }

    public class FileSizeItem
    {
        public string Name { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public long Size { get; set; }
        public string Extension { get; set; } = string.Empty;
        public DateTime LastModified { get; set; }
    }

    public class LargeFileItem
    {
        public string Name { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public long Size { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime ModifiedDate { get; set; }
        public string Extension { get; set; } = string.Empty;
    }

    public class DuplicateSpaceGroup
    {
        public string Hash { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public int FileCount { get; set; }
        public long WastedSpace => FileSize * (FileCount - 1);
        public ObservableCollection<DuplicateFile> Files { get; set; } = new();
    }

    public class DuplicateFile
    {
        public string Name { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public DateTime ModifiedDate { get; set; }
        public bool IsSelected { get; set; }
    }

    public class CleanupSuggestion
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public SuggestionType Type { get; set; }
        public string Description { get; set; } = string.Empty;
        public string Details { get; set; } = string.Empty;
        public long SpaceToFree { get; set; }
        public RiskLevel RiskLevel { get; set; }
        public bool IsExecuted { get; set; }
        public List<string> FilesToDelete { get; set; } = new();
    }

    public class FileSearchOptions
    {
        public string Path { get; set; } = string.Empty;
        public long MinFileSize { get; set; }
        public int MaxResults { get; set; } = 100;
        public bool IncludeSubfolders { get; set; } = true;
        public string[] FileExtensions { get; set; } = Array.Empty<string>();
    }

    // Enums
    public enum ItemType
    {
        File,
        Folder,
        System
    }

    public enum SortCriteria
    {
        Name,
        Size,
        Files,
        Modified,
        Type
    }

    public enum SortDirection
    {
        Ascending,
        Descending
    }

    public enum SuggestionType
    {
        TemporaryFiles,
        RecycleBin,
        BrowserCache,
        SystemCache,
        LogFiles,
        DuplicateFiles,
        LargeFiles,
        OldFiles
    }

    public enum RiskLevel
    {
        Safe,
        Low,
        Medium,
        High
    }
}
