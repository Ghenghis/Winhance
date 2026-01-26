using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Winhance.Core.Features.FileManager.Interfaces;

namespace Winhance.WPF.Features.FileManager.ViewModels
{
    /// <summary>
    /// ViewModel for the smart file organizer.
    /// </summary>
    public partial class OrganizerViewModel : ObservableObject
    {
        private readonly IOrganizerService? _organizerService;

        [ObservableProperty]
        private string _sourcePath = string.Empty;

        [ObservableProperty]
        private string _destinationPath = string.Empty;

        [ObservableProperty]
        private bool _moveToSameFolder = true;

        [ObservableProperty]
        private OrganizationStrategy _selectedStrategy = OrganizationStrategy.ByType;

        [ObservableProperty]
        private ObservableCollection<OrganizationCategoryViewModel> _categories = new();

        [ObservableProperty]
        private int _totalFiles;

        [ObservableProperty]
        private long _totalSize;

        [ObservableProperty]
        private int _unclassifiedFiles;

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        [ObservableProperty]
        private string? _lastTransactionId;

        [ObservableProperty]
        private bool _canUndo;

        // Space Recovery
        [ObservableProperty]
        private ObservableCollection<RecoveryOpportunityViewModel> _recoveryOpportunities = new();

        [ObservableProperty]
        private long _recoverableSpace;

        [ObservableProperty]
        private string _selectedDrive = "C:";

        [ObservableProperty]
        private ObservableCollection<string> _availableDrives = new() { "C:", "D:", "E:", "F:", "G:" };

        // Duplicates
        [ObservableProperty]
        private ObservableCollection<DuplicateGroupViewModel> _duplicateGroups = new();

        [ObservableProperty]
        private long _duplicateWastedSpace;

        public OrganizerViewModel()
        {
            // Design-time constructor
            LoadDesignTimeData();
        }

        public OrganizerViewModel(IOrganizerService? organizerService)
        {
            _organizerService = organizerService;
        }

        private void LoadDesignTimeData()
        {
            Categories.Add(new OrganizationCategoryViewModel
            {
                Name = "Documents",
                FileCount = 124,
                TotalSize = 234000000,
                DestinationFolder = "D:\\Organized\\Documents",
            });
            Categories.Add(new OrganizationCategoryViewModel
            {
                Name = "Images",
                FileCount = 312,
                TotalSize = 2300000000,
                DestinationFolder = "D:\\Organized\\Images",
            });

            RecoveryOpportunities.Add(new RecoveryOpportunityViewModel
            {
                Category = "AI Models (.lmstudio)",
                Size = 337000000000,
                ItemCount = 428,
                RecommendedAction = RecoveryAction.Relocate,
                Description = "Relocate to D: drive",
            });
        }

        [RelayCommand]
        private void BrowseSource()
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Select Source Folder",
                InitialDirectory = string.IsNullOrEmpty(SourcePath)
                    ? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
                    : SourcePath,
            };

            if (dialog.ShowDialog() == true)
            {
                SourcePath = dialog.FolderName;
            }
        }

        [RelayCommand]
        private void BrowseDestination()
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Select Destination Folder",
                InitialDirectory = string.IsNullOrEmpty(DestinationPath)
                    ? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
                    : DestinationPath,
            };

            if (dialog.ShowDialog() == true)
            {
                DestinationPath = dialog.FolderName;
            }
        }

        [RelayCommand]
        public async Task AnalyzeAsync()
        {
            if (string.IsNullOrEmpty(SourcePath))
            {
                StatusMessage = "Please select a source folder";
                return;
            }

            if (!System.IO.Directory.Exists(SourcePath))
            {
                StatusMessage = "Source folder does not exist";
                return;
            }

            IsLoading = true;
            StatusMessage = "Analyzing folder...";

            try
            {
                if (_organizerService != null)
                {
                    var plan = await _organizerService.AnalyzeAsync(SourcePath, SelectedStrategy);

                    Categories.Clear();
                    foreach (var category in plan.Categories)
                    {
                        Categories.Add(new OrganizationCategoryViewModel
                        {
                            Name = category.Name,
                            FileCount = category.FileCount,
                            TotalSize = category.TotalSize,
                            DestinationFolder = category.DestinationFolder,
                            IsSelected = true,
                        });
                    }

                    TotalFiles = plan.TotalFiles;
                    TotalSize = plan.TotalSize;
                    UnclassifiedFiles = plan.UnclassifiedFiles;
                }
                else
                {
                    await AnalyzeFallbackAsync();
                }

                StatusMessage = $"Found {TotalFiles} files in {Categories.Count} categories";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task AnalyzeFallbackAsync()
        {
            await Task.Run(() =>
            {
                var categoryData = new System.Collections.Generic.Dictionary<string, (int count, long size, System.Collections.Generic.List<string> files)>();
                int totalFiles = 0;
                long totalSize = 0;
                int unclassified = 0;

                var files = System.IO.Directory.GetFiles(SourcePath, "*.*", System.IO.SearchOption.TopDirectoryOnly);

                foreach (var file in files)
                {
                    try
                    {
                        var fileInfo = new System.IO.FileInfo(file);
                        var extension = fileInfo.Extension.ToLowerInvariant();
                        var categoryName = GetCategoryForExtension(extension);

                        totalFiles++;
                        totalSize += fileInfo.Length;

                        if (!categoryData.ContainsKey(categoryName))
                        {
                            categoryData[categoryName] = (0, 0, new System.Collections.Generic.List<string>());
                        }

                        var data = categoryData[categoryName];
                        data.count++;
                        data.size += fileInfo.Length;
                        data.files.Add(file);
                        categoryData[categoryName] = data;

                        if (categoryName == "Other")
                        {
                            unclassified++;
                        }
                    }
                    catch { }
                }

                var destBase = MoveToSameFolder ? SourcePath : DestinationPath;
                if (string.IsNullOrEmpty(destBase))
                {
                    destBase = SourcePath;
                }

                System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                {
                    Categories.Clear();
                    foreach (var kvp in categoryData.OrderByDescending(k => k.Value.size))
                    {
                        Categories.Add(new OrganizationCategoryViewModel
                        {
                            Name = kvp.Key,
                            FileCount = kvp.Value.count,
                            TotalSize = kvp.Value.size,
                            DestinationFolder = System.IO.Path.Combine(destBase, kvp.Key),
                            IsSelected = true,
                            Files = kvp.Value.files,
                        });
                    }

                    TotalFiles = totalFiles;
                    TotalSize = totalSize;
                    UnclassifiedFiles = unclassified;
                });
            });
        }

        private static string GetCategoryForExtension(string extension)
        {
            return extension switch
            {
                ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".webp" or ".svg" or ".ico" or ".tiff" => "Images",
                ".mp4" or ".mkv" or ".avi" or ".mov" or ".wmv" or ".flv" or ".webm" => "Videos",
                ".mp3" or ".wav" or ".flac" or ".aac" or ".ogg" or ".wma" or ".m4a" => "Music",
                ".pdf" or ".doc" or ".docx" or ".txt" or ".rtf" or ".odt" => "Documents",
                ".xls" or ".xlsx" or ".csv" or ".ods" => "Spreadsheets",
                ".ppt" or ".pptx" or ".odp" => "Presentations",
                ".zip" or ".rar" or ".7z" or ".tar" or ".gz" => "Archives",
                ".exe" or ".msi" or ".appx" => "Programs",
                ".dll" or ".sys" or ".drv" => "System",
                ".py" or ".js" or ".ts" or ".cs" or ".java" or ".cpp" or ".c" or ".h" or ".rs" => "Code",
                ".html" or ".htm" or ".css" or ".scss" or ".less" => "Web",
                ".json" or ".xml" or ".yaml" or ".yml" or ".toml" => "Data",
                ".sql" or ".db" or ".sqlite" => "Database",
                ".psd" or ".ai" or ".sketch" or ".fig" or ".xd" => "Design",
                ".ttf" or ".otf" or ".woff" or ".woff2" => "Fonts",
                _ => "Other",
            };
        }

        [RelayCommand]
        private async Task ApplyOrganizationAsync()
        {
            if (Categories.Count == 0)
            {
                StatusMessage = "No categories to organize";
                return;
            }

            IsLoading = true;
            StatusMessage = "Organizing files...";

            if (_organizerService == null)
            {
                await ApplyOrganizationFallbackAsync();
                return;
            }

            try
            {
                var plan = new OrganizationPlan
                {
                    SourcePath = SourcePath,
                    DestinationPath = MoveToSameFolder ? SourcePath : DestinationPath,
                    Strategy = SelectedStrategy,
                    Categories = Categories.Where(c => c.IsSelected).Select(c => new OrganizationCategory
                    {
                        Name = c.Name,
                        DestinationFolder = c.DestinationFolder,
                        FileCount = c.FileCount,
                        TotalSize = c.TotalSize,
                    }).ToList(),
                };

                var result = await _organizerService.ExecuteAsync(plan);

                if (result.Success)
                {
                    LastTransactionId = result.TransactionId;
                    CanUndo = true;
                    StatusMessage = $"Successfully organized {result.FilesOrganized} files";
                }
                else
                {
                    StatusMessage = $"Organized {result.FilesOrganized}, failed {result.FilesFailed}";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private System.Collections.Generic.List<(string source, string dest)>? _undoLog;

        private async Task ApplyOrganizationFallbackAsync()
        {
            int organized = 0;
            int failed = 0;
            var undoLog = new System.Collections.Generic.List<(string source, string dest)>();

            try
            {
                await Task.Run(() =>
                {
                    foreach (var category in Categories.Where(c => c.IsSelected))
                    {
                        if (!System.IO.Directory.Exists(category.DestinationFolder))
                        {
                            System.IO.Directory.CreateDirectory(category.DestinationFolder);
                        }

                        foreach (var file in category.Files)
                        {
                            try
                            {
                                var fileName = System.IO.Path.GetFileName(file);
                                var destPath = System.IO.Path.Combine(category.DestinationFolder, fileName);

                                // Handle name conflicts
                                if (System.IO.File.Exists(destPath))
                                {
                                    var nameWithoutExt = System.IO.Path.GetFileNameWithoutExtension(fileName);
                                    var ext = System.IO.Path.GetExtension(fileName);
                                    int counter = 1;
                                    while (System.IO.File.Exists(destPath))
                                    {
                                        destPath = System.IO.Path.Combine(category.DestinationFolder, $"{nameWithoutExt}_{counter++}{ext}");
                                    }
                                }

                                System.IO.File.Move(file, destPath);
                                undoLog.Add((destPath, file));
                                organized++;
                            }
                            catch
                            {
                                failed++;
                            }
                        }
                    }
                });

                if (organized > 0)
                {
                    _undoLog = undoLog;
                    CanUndo = true;
                    LastTransactionId = Guid.NewGuid().ToString();
                }

                StatusMessage = failed == 0
                    ? $"Successfully organized {organized} files"
                    : $"Organized {organized}, failed {failed}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task UndoOrganizationAsync()
        {
            // Try fallback undo first
            if (_undoLog != null && _undoLog.Count > 0)
            {
                await UndoOrganizationFallbackAsync();
                return;
            }

            if (_organizerService == null || string.IsNullOrEmpty(LastTransactionId))
            {
                return;
            }

            IsLoading = true;
            StatusMessage = "Undoing organization...";

            try
            {
                var result = await _organizerService.UndoAsync(LastTransactionId);

                if (result.Success)
                {
                    StatusMessage = $"Successfully restored {result.FilesOrganized} files";
                    LastTransactionId = null;
                    CanUndo = false;
                }
                else
                {
                    StatusMessage = $"Undo failed";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task UndoOrganizationFallbackAsync()
        {
            IsLoading = true;
            StatusMessage = "Undoing organization...";
            int restored = 0;
            int failed = 0;

            try
            {
                await Task.Run(() =>
                {
                    if (_undoLog != null)
                    {
                        foreach (var (currentPath, originalPath) in _undoLog)
                        {
                            try
                            {
                                if (System.IO.File.Exists(currentPath))
                                {
                                    System.IO.File.Move(currentPath, originalPath);
                                    restored++;
                                }
                            }
                            catch
                            {
                                failed++;
                            }
                        }
                    }
                });

                StatusMessage = failed == 0
                    ? $"Successfully restored {restored} files"
                    : $"Restored {restored}, failed {failed}";

                _undoLog = null;
                LastTransactionId = null;
                CanUndo = false;
            }
            catch (Exception ex)
            {
                StatusMessage = $"Undo error: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task AnalyzeSpaceRecoveryAsync()
        {
            if (_organizerService == null)
            {
                return;
            }

            IsLoading = true;
            StatusMessage = $"Analyzing {SelectedDrive} for recovery opportunities...";

            try
            {
                var analysis = await _organizerService.AnalyzeSpaceRecoveryAsync(SelectedDrive);

                RecoveryOpportunities.Clear();
                foreach (var opportunity in analysis.Opportunities)
                {
                    RecoveryOpportunities.Add(new RecoveryOpportunityViewModel
                    {
                        Category = opportunity.Category,
                        Path = opportunity.Path,
                        Size = opportunity.Size,
                        ItemCount = opportunity.ItemCount,
                        RecommendedAction = opportunity.RecommendedAction,
                        Description = opportunity.Description,
                        IsSafeToClean = opportunity.IsSafeToClean,
                        IsSelected = opportunity.IsSafeToClean,
                    });
                }

                RecoverableSpace = analysis.RecoverableSpace;
                StatusMessage = $"Found {FormatSize(RecoverableSpace)} recoverable";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task ExecuteRecoveryAsync()
        {
            if (_organizerService == null)
            {
                return;
            }

            var selectedOpportunities = RecoveryOpportunities.Where(o => o.IsSelected).ToList();
            if (selectedOpportunities.Count == 0)
            {
                StatusMessage = "No items selected for recovery";
                return;
            }

            IsLoading = true;
            StatusMessage = "Executing recovery...";

            try
            {
                // For now, handle model relocation specifically
                foreach (var opportunity in selectedOpportunities)
                {
                    if (opportunity.RecommendedAction == RecoveryAction.Relocate)
                    {
                        var result = await _organizerService.RelocateModelsAsync(
                            opportunity.Path,
                            $"D:\\AI-Models\\{opportunity.Category}",
                            createSymlinks: true);

                        if (result.Success)
                        {
                            StatusMessage = $"Relocated {FormatSize(result.BytesProcessed)}";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task FindDuplicatesAsync()
        {
            if (_organizerService == null || string.IsNullOrEmpty(SourcePath))
            {
                return;
            }

            IsLoading = true;
            StatusMessage = "Scanning for duplicates...";

            try
            {
                var duplicates = await _organizerService.FindDuplicatesAsync(
                    SourcePath,
                    DuplicateSearchMethod.Hash);

                DuplicateGroups.Clear();
                DuplicateWastedSpace = 0;

                foreach (var group in duplicates)
                {
                    var vm = new DuplicateGroupViewModel
                    {
                        Hash = group.Key,
                        FileSize = group.TotalSize,
                        DuplicateCount = group.FileCount - 1,
                        WastedSpace = group.WastedSize,
                    };

                    foreach (var file in group.Files)
                    {
                        vm.Files.Add(new DuplicateFileViewModel
                        {
                            Path = file.Path,
                            Name = file.Name,
                            DateModified = file.DateModified,
                            IsOriginal = file.IsOriginal,
                            IsSelected = !file.IsOriginal,
                        });
                    }

                    DuplicateGroups.Add(vm);
                    DuplicateWastedSpace += group.WastedSize;
                }

                StatusMessage = $"Found {DuplicateGroups.Count} duplicate groups, {FormatSize(DuplicateWastedSpace)} wasted";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private static string FormatSize(long bytes)
        {
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
    /// ViewModel for an organization category.
    /// </summary>
    public partial class OrganizationCategoryViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _name = string.Empty;

        [ObservableProperty]
        private string _destinationFolder = string.Empty;

        [ObservableProperty]
        private int _fileCount;

        [ObservableProperty]
        private long _totalSize;

        [ObservableProperty]
        private bool _isSelected = true;

        [ObservableProperty]
        private bool _isExpanded;

        public System.Collections.Generic.List<string> Files { get; set; } = new();

        public string SizeDisplay => FormatSize(TotalSize);

        private static string FormatSize(long bytes)
        {
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
    /// ViewModel for a recovery opportunity.
    /// </summary>
    public partial class RecoveryOpportunityViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _category = string.Empty;

        [ObservableProperty]
        private string _path = string.Empty;

        [ObservableProperty]
        private long _size;

        [ObservableProperty]
        private int _itemCount;

        [ObservableProperty]
        private RecoveryAction _recommendedAction;

        [ObservableProperty]
        private string _description = string.Empty;

        [ObservableProperty]
        private bool _isSafeToClean;

        [ObservableProperty]
        private bool _isSelected;

        public string SizeDisplay => FormatSize(Size);

        public string ActionDisplay => RecommendedAction.ToString();

        private static string FormatSize(long bytes)
        {
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
    /// ViewModel for a duplicate group.
    /// </summary>
    public partial class DuplicateGroupViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _hash = string.Empty;

        [ObservableProperty]
        private long _fileSize;

        [ObservableProperty]
        private int _duplicateCount;

        [ObservableProperty]
        private long _wastedSpace;

        public ObservableCollection<DuplicateFileViewModel> Files { get; } = new();

        public string SizeDisplay => FormatSize(FileSize);

        public string WastedDisplay => FormatSize(WastedSpace);

        private static string FormatSize(long bytes)
        {
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
    /// ViewModel for a duplicate file.
    /// </summary>
    public partial class DuplicateFileViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _path = string.Empty;

        [ObservableProperty]
        private string _name = string.Empty;

        [ObservableProperty]
        private DateTime _dateModified;

        [ObservableProperty]
        private bool _isOriginal;

        [ObservableProperty]
        private bool _isSelected;
    }
}
