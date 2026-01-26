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
    /// ViewModel for file system analysis
    /// </summary>
    public partial class FileSystemAnalysisViewModel : ObservableObject
    {
        private readonly IAnalysisService _analysisService;
        private ObservableCollection<AnalysisResult> _analysisResults = new();

        [ObservableProperty]
        private string _analysisPath = string.Empty;

        [ObservableProperty]
        private bool _isAnalyzing;

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        [ObservableProperty]
        private AnalysisType _analysisType = AnalysisType.Overview;

        [ObservableProperty]
        private AnalysisDepth _analysisDepth = AnalysisDepth.Deep;

        [ObservableProperty]
        private bool _includeHiddenFiles = false;

        [ObservableProperty]
        private bool _followSymlinks = false;

        [ObservableProperty]
        private double _analysisProgress;

        [ObservableProperty]
        private string _currentFile = string.Empty;

        [ObservableProperty]
        private DateTime _analysisStartTime;

        [ObservableProperty]
        private TimeSpan _elapsedTime;

        [ObservableProperty]
        private AnalysisResult? _selectedResult;

        public ObservableCollection<AnalysisResult> AnalysisResults
        {
            get => _analysisResults;
            set => SetProperty(ref _analysisResults, value);
        }

        public FileSystemAnalysisViewModel(IAnalysisService analysisService)
        {
            _analysisService = analysisService;
        }

        [RelayCommand]
        private async Task StartAnalysisAsync()
        {
            if (string.IsNullOrEmpty(AnalysisPath)) return;

            IsAnalyzing = true;
            StatusMessage = "Starting analysis...";
            AnalysisProgress = 0;
            AnalysisStartTime = DateTime.Now;
            AnalysisResults.Clear();

            var options = new AnalysisOptions
            {
                Path = AnalysisPath,
                Type = AnalysisType,
                Depth = AnalysisDepth,
                IncludeHiddenFiles = IncludeHiddenFiles,
                FollowSymlinks = FollowSymlinks
            };

            try
            {
                var progress = new Progress<AnalysisProgress>(p =>
                {
                    AnalysisProgress = p.Percentage;
                    CurrentFile = p.CurrentFile;
                    ElapsedTime = DateTime.Now - AnalysisStartTime;
                    StatusMessage = $"Analyzing... {p.FilesProcessed:N0} files, {p.FoldersProcessed:N0} folders";
                });

                var result = await _analysisService.AnalyzeAsync(options, progress);
                AnalysisResults.Add(result);
                
                StatusMessage = $"Analysis complete. Found {result.TotalFiles:N0} files, {result.TotalFolders:N0} folders";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Analysis failed: {ex.Message}";
            }
            finally
            {
                IsAnalyzing = false;
            }
        }

        [RelayCommand]
        private void StopAnalysis()
        {
            _analysisService.CancelAnalysis();
            IsAnalyzing = false;
            StatusMessage = "Analysis cancelled";
        }

        [RelayCommand]
        private void ExportResults()
        {
            if (!AnalysisResults.Any()) return;

            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "JSON File (*.json)|*.json|CSV File (*.csv)|*.csv",
                DefaultExt = ".json",
                FileName = $"Analysis_{DateTime.Now:yyyyMMdd_HHmmss}.json"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var json = System.Text.Json.JsonSerializer.Serialize(AnalysisResults,
                        new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                    System.IO.File.WriteAllText(dialog.FileName, json);
                    StatusMessage = $"Results exported to {dialog.FileName}";
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Export failed: {ex.Message}";
                }
            }
        }

        [RelayCommand]
        private void GenerateReport()
        {
            if (!AnalysisResults.Any()) return;

            var report = new System.Text.StringBuilder();
            report.AppendLine("File System Analysis Report");
            report.AppendLine($"Generated: {DateTime.Now:g}");
            report.AppendLine(new string('=', 50));
            
            foreach (var result in AnalysisResults)
            {
                report.AppendLine($"\nPath: {result.Path}");
                report.AppendLine($"Files: {result.TotalFiles:N0}");
                report.AppendLine($"Folders: {result.TotalFolders:N0}");
                report.AppendLine($"Total Size: {result.TotalSize / (1024.0 * 1024.0 * 1024.0):F2} GB");
                report.AppendLine($"Duration: {result.Duration.TotalSeconds:F2}s");
            }

            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Text File (*.txt)|*.txt",
                DefaultExt = ".txt",
                FileName = $"AnalysisReport_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
            };

            if (dialog.ShowDialog() == true)
            {
                System.IO.File.WriteAllText(dialog.FileName, report.ToString());
                StatusMessage = $"Report generated: {dialog.FileName}";
            }
        }

        [RelayCommand]
        private void ClearResults()
        {
            AnalysisResults.Clear();
            SelectedResult = null;
            StatusMessage = "Results cleared";
        }

        [RelayCommand]
        private void BrowseAnalysisPath()
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Select Folder to Analyze"
            };

            if (dialog.ShowDialog() == true)
            {
                AnalysisPath = dialog.FolderName;
            }
        }

        [RelayCommand]
        private void ViewDetails(AnalysisResult? result)
        {
            if (result == null) return;

            var message = $"Analysis Details\n\n" +
                         $"Path: {result.Path}\n" +
                         $"Type: {result.Type}\n" +
                         $"Duration: {result.Duration.TotalSeconds:F2}s\n\n" +
                         $"Files: {result.TotalFiles:N0}\n" +
                         $"Folders: {result.TotalFolders:N0}\n" +
                         $"Total Size: {result.TotalSize / (1024.0 * 1024.0 * 1024.0):F2} GB\n" +
                         $"Used Space: {result.UsedSpace / (1024.0 * 1024.0 * 1024.0):F2} GB\n" +
                         $"Free Space: {result.FreeSpace / (1024.0 * 1024.0 * 1024.0):F2} GB";

            if (result.Warnings.Any())
                message += $"\n\nWarnings: {result.Warnings.Count}";
            if (result.Errors.Any())
                message += $"\nErrors: {result.Errors.Count}";

            System.Windows.MessageBox.Show(message, "Analysis Details",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
        }
    }

    /// <summary>
    /// ViewModel for disk usage reports
    /// </summary>
    public partial class DiskUsageReportViewModel : ObservableObject
    {
        private readonly IReportService _reportService;
        private ObservableCollection<DiskUsageReport> _reports = new();

        [ObservableProperty]
        private DiskUsageReport? _selectedReport;

        [ObservableProperty]
        private bool _isGenerating;

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        [ObservableProperty]
        private ReportType _reportType = ReportType.Summary;

        [ObservableProperty]
        private string _reportPath = string.Empty;

        [ObservableProperty]
        private DateTime _startDate = DateTime.Today.AddDays(-30);

        [ObservableProperty]
        private DateTime _endDate = DateTime.Now;

        [ObservableProperty]
        private string[] _includeDrives = Array.Empty<string>();

        [ObservableProperty]
        private ReportFormat _outputFormat = ReportFormat.HTML;

        [ObservableProperty]
        private bool _includeCharts = true;

        [ObservableProperty]
        private bool _includeRecommendations = true;

        [ObservableProperty]
        private bool _includeTrends = true;

        public ObservableCollection<DiskUsageReport> Reports
        {
            get => _reports;
            set => SetProperty(ref _reports, value);
        }

        public DiskUsageReportViewModel(IReportService reportService)
        {
            _reportService = reportService;
            _ = LoadReportsAsync();
        }

        private async Task LoadReportsAsync()
        {
            try
            {
                var reports = await _reportService.GetDiskUsageReportsAsync();
                Reports.Clear();
                foreach (var report in reports.OrderByDescending(r => r.GeneratedAt))
                {
                    Reports.Add(report);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading reports: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task GenerateReportAsync()
        {
            if (string.IsNullOrEmpty(ReportPath)) return;

            IsGenerating = true;
            StatusMessage = "Generating report...";

            var options = new DiskUsageReportOptions
            {
                Type = ReportType,
                StartDate = StartDate,
                EndDate = EndDate,
                IncludeDrives = IncludeDrives,
                OutputFormat = OutputFormat,
                IncludeCharts = IncludeCharts,
                IncludeRecommendations = IncludeRecommendations,
                IncludeTrends = IncludeTrends
            };

            try
            {
                var report = await _reportService.GenerateDiskUsageReportAsync(ReportPath, options);
                Reports.Insert(0, report);
                StatusMessage = $"Report generated: {ReportPath}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Report generation failed: {ex.Message}";
            }
            finally
            {
                IsGenerating = false;
            }
        }

        [RelayCommand]
        private async Task DeleteReportAsync(DiskUsageReport? report)
        {
            if (report == null) return;

            try
            {
                await _reportService.DeleteReportAsync(report.Id);
                Reports.Remove(report);
                StatusMessage = $"Report deleted: {report.Name}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error deleting report: {ex.Message}";
            }
        }

        [RelayCommand]
        private void OpenReport(DiskUsageReport? report)
        {
            if (report == null) return;

            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = report.FilePath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error opening report: {ex.Message}";
            }
        }

        [RelayCommand]
        private void ViewReportSummary(DiskUsageReport? report)
        {
            if (report == null) return;

            var message = $"Report Summary\n\n" +
                         $"Name: {report.Name}\n" +
                         $"Type: {report.Type}\n" +
                         $"Format: {report.Format}\n" +
                         $"Generated: {report.GeneratedAt:g}\n" +
                         $"Period: {report.PeriodStart:d} - {report.PeriodEnd:d}\n" +
                         $"Size: {report.FileSize / 1024.0:F2} KB\n" +
                         $"Drives: {string.Join(", ", report.Drives)}";

            System.Windows.MessageBox.Show(message, "Report Summary",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
        }

        [RelayCommand]
        private void EmailReport(DiskUsageReport? report)
        {
            if (report == null) return;

            var mailto = $"mailto:?subject=Disk Usage Report - {report.Name}&body=Please find attached the disk usage report.";
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = mailto,
                    UseShellExecute = true
                };
                System.Diagnostics.Process.Start(psi);
                StatusMessage = $"Email client opened for report: {report.Name}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Failed to open email client: {ex.Message}";
            }
        }

        [RelayCommand]
        private void ScheduleReport()
        {
            var message = "Schedule Report\n\n" +
                         "Configure scheduled reports using Windows Task Scheduler.\n" +
                         "You can create a task that runs the report generation command.";

            System.Windows.MessageBox.Show(message, "Schedule Report",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
        }

        [RelayCommand]
        private void BrowseReportPath()
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "HTML File (*.html)|*.html|PDF File (*.pdf)|*.pdf|Excel File (*.xlsx)|*.xlsx",
                DefaultExt = ".html",
                FileName = $"DiskUsageReport_{DateTime.Now:yyyyMMdd_HHmmss}.html"
            };

            if (dialog.ShowDialog() == true)
            {
                ReportPath = dialog.FileName;
            }
        }
    }

    /// <summary>
    /// ViewModel for file type analysis
    /// </summary>
    public partial class FileTypeAnalysisViewModel : ObservableObject
    {
        private readonly IFileTypeAnalysisService _fileTypeAnalysisService;
        private ObservableCollection<FileTypeStatistics> _fileTypeStats = new();
        private ObservableCollection<FileTypeCategory> _categories = new();

        [ObservableProperty]
        private string _analysisPath = string.Empty;

        [ObservableProperty]
        private bool _isAnalyzing;

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        [ObservableProperty]
        private FileTypeCategory? _selectedCategory;

        [ObservableProperty]
        private FileTypeStatistics? _selectedType;

        [ObservableProperty]
        private ChartType _chartType = ChartType.Pie;

        [ObservableProperty]
        private bool _showUnknownTypes = true;

        [ObservableProperty]
        private long _minSizeThreshold = 0;

        [ObservableProperty]
        private GroupBy _groupBy = GroupBy.Extension;

        [ObservableProperty]
        private SortBy _sortBy = SortBy.Size;

        [ObservableProperty]
        private SortOrder _sortOrder = SortOrder.Descending;

        public ObservableCollection<FileTypeStatistics> FileTypeStats
        {
            get => _fileTypeStats;
            set => SetProperty(ref _fileTypeStats, value);
        }

        public ObservableCollection<FileTypeCategory> Categories
        {
            get => _categories;
            set => SetProperty(ref _categories, value);
        }

        public FileTypeAnalysisViewModel(IFileTypeAnalysisService fileTypeAnalysisService)
        {
            _fileTypeAnalysisService = fileTypeAnalysisService;
            _ = LoadCategoriesAsync();
        }

        private async Task LoadCategoriesAsync()
        {
            try
            {
                var categories = await _fileTypeAnalysisService.GetCategoriesAsync();
                Categories.Clear();
                foreach (var category in categories)
                {
                    Categories.Add(category);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading categories: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task AnalyzeAsync()
        {
            if (string.IsNullOrEmpty(AnalysisPath)) return;

            IsAnalyzing = true;
            StatusMessage = "Analyzing file types...";
            FileTypeStats.Clear();

            var options = new FileTypeAnalysisOptions
            {
                Path = AnalysisPath,
                GroupBy = GroupBy,
                MinSizeThreshold = MinSizeThreshold,
                ShowUnknownTypes = ShowUnknownTypes
            };

            try
            {
                var stats = await _fileTypeAnalysisService.AnalyzeFileTypesAsync(options);
                
                var sortedStats = SortBy switch
                {
                    SortBy.Name => SortOrder == SortOrder.Ascending 
                        ? stats.OrderBy(s => s.TypeName)
                        : stats.OrderByDescending(s => s.TypeName),
                    SortBy.Size => SortOrder == SortOrder.Ascending 
                        ? stats.OrderBy(s => s.TotalSize)
                        : stats.OrderByDescending(s => s.TotalSize),
                    SortBy.Count => SortOrder == SortOrder.Ascending 
                        ? stats.OrderBy(s => s.FileCount)
                        : stats.OrderByDescending(s => s.FileCount),
                    SortBy.AverageSize => SortOrder == SortOrder.Ascending 
                        ? stats.OrderBy(s => s.AverageSize)
                        : stats.OrderByDescending(s => s.AverageSize),
                    _ => stats
                };

                foreach (var stat in sortedStats)
                {
                    FileTypeStats.Add(stat);
                }

                StatusMessage = $"Analysis complete. Found {stats.Count} different file types";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Analysis failed: {ex.Message}";
            }
            finally
            {
                IsAnalyzing = false;
            }
        }

        [RelayCommand]
        private void FilterByCategory(FileTypeCategory? category)
        {
            if (category == null)
            {
                _ = AnalyzeAsync();
                return;
            }

            var filtered = FileTypeStats.Where(s => s.CategoryId == category.Id).ToList();
            FileTypeStats.Clear();
            foreach (var stat in filtered)
            {
                FileTypeStats.Add(stat);
            }
        }

        [RelayCommand]
        private void ViewFilesOfType(FileTypeStatistics? stat)
        {
            if (stat == null) return;

            var message = $"Files of Type: {stat.TypeName}\n\n" +
                         $"Extension: {stat.Extension}\n" +
                         $"Count: {stat.FileCount:N0}\n" +
                         $"Total Size: {stat.TotalSize / (1024.0 * 1024.0):F2} MB\n" +
                         $"Average Size: {stat.AverageSize / 1024.0:F2} KB\n" +
                         $"Percentage: {stat.Percentage:F2}%";

            System.Windows.MessageBox.Show(message, "File Type Details",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
        }

        [RelayCommand]
        private void ExportStatistics()
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "CSV File (*.csv)|*.csv|Excel File (*.xlsx)|*.xlsx",
                DefaultExt = ".csv",
                FileName = $"FileTypeStats_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var csv = new System.Text.StringBuilder();
                    csv.AppendLine("Type,Extension,Count,TotalSize,Percentage,AvgSize");
                    foreach (var stat in FileTypeStats)
                    {
                        csv.AppendLine($"{stat.TypeName},{stat.Extension},{stat.FileCount},{stat.TotalSize},{stat.Percentage:F2},{stat.AverageSize}");
                    }
                    System.IO.File.WriteAllText(dialog.FileName, csv.ToString());
                    StatusMessage = $"Statistics exported to {dialog.FileName}";
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Export failed: {ex.Message}";
                }
            }
        }

        [RelayCommand]
        private void GenerateChart()
        {
            var message = $"Chart Generation\n\n" +
                         $"Chart Type: {ChartType}\n" +
                         $"Data Points: {FileTypeStats.Count}\n\n" +
                         "Chart will be generated and displayed in a separate window.";

            System.Windows.MessageBox.Show(message, "Generate Chart",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
            StatusMessage = "Chart generation feature available in full version";
        }

        [RelayCommand]
        private void RefreshAnalysis()
        {
            _ = AnalyzeAsync();
        }

        [RelayCommand]
        private void BrowseAnalysisPath()
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Select Folder to Analyze"
            };

            if (dialog.ShowDialog() == true)
            {
                AnalysisPath = dialog.FolderName;
            }
        }

        [RelayCommand]
        private void CustomizeCategories()
        {
            var message = "Category Customization\n\n" +
                         "Define custom file type categories and assign extensions.\n" +
                         "Categories can be used to group related file types.";

            System.Windows.MessageBox.Show(message, "Customize Categories",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
        }
    }

    /// <summary>
    /// ViewModel for storage optimization
    /// </summary>
    public partial class StorageOptimizationViewModel : ObservableObject
    {
        private readonly IOptimizationService _optimizationService;
        private ObservableCollection<OptimizationSuggestion> _suggestions = new();
        private ObservableCollection<OptimizationRule> _rules = new();

        [ObservableProperty]
        private string _scanPath = string.Empty;

        [ObservableProperty]
        private bool _isScanning;

        [ObservableProperty]
        private bool _isOptimizing;

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        [ObservableProperty]
        private OptimizationSuggestion? _selectedSuggestion;

        [ObservableProperty]
        private long _potentialSavings;

        [ObservableProperty]
        private int _issuesFound;

        [ObservableProperty]
        private OptimizationLevel _optimizationLevel = OptimizationLevel.Safe;

        [ObservableProperty]
        private bool _includeSystemFiles = false;

        [ObservableProperty]
        private bool _createBackup = true;

        [ObservableProperty]
        private TimeSpan _scanTime;

        public ObservableCollection<OptimizationSuggestion> Suggestions
        {
            get => _suggestions;
            set => SetProperty(ref _suggestions, value);
        }

        public ObservableCollection<OptimizationRule> Rules
        {
            get => _rules;
            set => SetProperty(ref _rules, value);
        }

        public StorageOptimizationViewModel(IOptimizationService optimizationService)
        {
            _optimizationService = optimizationService;
            _ = LoadRulesAsync();
        }

        private async Task LoadRulesAsync()
        {
            try
            {
                var rules = await _optimizationService.GetOptimizationRulesAsync();
                Rules.Clear();
                foreach (var rule in rules)
                {
                    Rules.Add(rule);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading rules: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task ScanForOptimizationsAsync()
        {
            if (string.IsNullOrEmpty(ScanPath)) return;

            IsScanning = true;
            StatusMessage = "Scanning for optimization opportunities...";
            Suggestions.Clear();
            PotentialSavings = 0;
            IssuesFound = 0;

            var startTime = DateTime.Now;

            var options = new ScanOptions
            {
                Path = ScanPath,
                Level = OptimizationLevel,
                IncludeSystemFiles = IncludeSystemFiles,
                Rules = Rules.Where(r => r.IsEnabled).ToList()
            };

            try
            {
                var suggestions = await _optimizationService.ScanForOptimizationsAsync(options);
                
                foreach (var suggestion in suggestions.OrderByDescending(s => s.PotentialSavings))
                {
                    Suggestions.Add(suggestion);
                    PotentialSavings += suggestion.PotentialSavings;
                    IssuesFound++;
                }

                ScanTime = DateTime.Now - startTime;
                StatusMessage = $"Scan complete. Found {IssuesFound} optimization opportunities";
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
        private async Task ApplyOptimizationsAsync()
        {
            var selectedSuggestions = Suggestions.Where(s => s.IsSelected).ToList();
            if (!selectedSuggestions.Any())
            {
                StatusMessage = "No optimizations selected";
                return;
            }

            IsOptimizing = true;
            StatusMessage = "Applying optimizations...";

            var options = new ApplyOptions
            {
                CreateBackup = CreateBackup,
                Level = OptimizationLevel
            };

            try
            {
                var results = await _optimizationService.ApplyOptimizationsAsync(selectedSuggestions, options);
                
                long totalSavings = 0;
                int successful = 0;
                int failed = 0;

                foreach (var result in results)
                {
                    if (result.Success)
                    {
                        totalSavings += result.SpaceSaved;
                        successful++;
                    }
                    else
                    {
                        failed++;
                    }
                }

                StatusMessage = $"Optimization complete. {successful} successful, {failed} failed. Saved {FormatFileSize(totalSavings)}";
                
                // Remove applied suggestions
                foreach (var suggestion in selectedSuggestions.Where(s => results.Any(r => r.SuggestionId == s.Id && r.Success)))
                {
                    Suggestions.Remove(suggestion);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Optimization failed: {ex.Message}";
            }
            finally
            {
                IsOptimizing = false;
            }
        }

        [RelayCommand]
        private void SelectAllSuggestions()
        {
            foreach (var suggestion in Suggestions)
            {
                suggestion.IsSelected = true;
            }
        }

        [RelayCommand]
        private void DeselectAllSuggestions()
        {
            foreach (var suggestion in Suggestions)
            {
                suggestion.IsSelected = false;
            }
        }

        [RelayCommand]
        private void SelectByType(OptimizationType type)
        {
            foreach (var suggestion in Suggestions.Where(s => s.Type == type))
            {
                suggestion.IsSelected = true;
            }
        }

        [RelayCommand]
        private void ViewSuggestionDetails(OptimizationSuggestion? suggestion)
        {
            if (suggestion == null) return;

            var message = $"Optimization Details\n\n" +
                         $"Title: {suggestion.Title}\n" +
                         $"Type: {suggestion.Type}\n" +
                         $"Risk: {suggestion.Risk}\n\n" +
                         $"Potential Savings: {FormatFileSize(suggestion.PotentialSavings)}\n" +
                         $"Affected Items: {suggestion.AffectedItems:N0}\n" +
                         $"Path: {suggestion.Path}\n\n" +
                         $"Description:\n{suggestion.Description}";

            System.Windows.MessageBox.Show(message, "Optimization Details",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
        }

        [RelayCommand]
        private void PreviewOptimization(OptimizationSuggestion? suggestion)
        {
            if (suggestion == null) return;

            var message = $"Preview: {suggestion.Title}\n\n" +
                         $"The following items will be affected:\n" +
                         $"- {suggestion.AffectedItems:N0} items\n" +
                         $"- Potential savings: {FormatFileSize(suggestion.PotentialSavings)}\n\n" +
                         "This is a preview only. No changes will be made.";

            if (suggestion.AffectedFiles.Any())
            {
                message += $"\n\nFirst 5 files:\n";
                message += string.Join("\n", suggestion.AffectedFiles.Take(5));
                if (suggestion.AffectedFiles.Count > 5)
                    message += $"\n... and {suggestion.AffectedFiles.Count - 5} more";
            }

            System.Windows.MessageBox.Show(message, "Preview Optimization",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
        }

        [RelayCommand]
        private void ExcludeSuggestion(OptimizationSuggestion? suggestion)
        {
            if (suggestion == null) return;

            Suggestions.Remove(suggestion);
        }

        [RelayCommand]
        private void CustomizeRules()
        {
            var message = $"Rules Customization\n\n" +
                         $"Active Rules: {Rules.Count(r => r.IsEnabled)}/{Rules.Count}\n\n" +
                         "Configure optimization rules to customize scanning behavior.\n" +
                         "Enable or disable rules based on your requirements.";

            System.Windows.MessageBox.Show(message, "Customize Rules",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
        }

        [RelayCommand]
        private void ExportSuggestions()
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "JSON File (*.json)|*.json|CSV File (*.csv)|*.csv",
                DefaultExt = ".json",
                FileName = $"OptimizationSuggestions_{DateTime.Now:yyyyMMdd_HHmmss}.json"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var json = System.Text.Json.JsonSerializer.Serialize(Suggestions,
                        new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                    System.IO.File.WriteAllText(dialog.FileName, json);
                    StatusMessage = $"Suggestions exported to {dialog.FileName}";
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
            Suggestions.Clear();
            PotentialSavings = 0;
            IssuesFound = 0;
            StatusMessage = "Results cleared";
        }

        [RelayCommand]
        private void BrowseScanPath()
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Select Folder to Scan"
            };

            if (dialog.ShowDialog() == true)
            {
                ScanPath = dialog.FolderName;
            }
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

    // Model classes
    public class AnalysisResult
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Path { get; set; } = string.Empty;
        public AnalysisType Type { get; set; }
        public DateTime AnalyzedAt { get; set; }
        public TimeSpan Duration { get; set; }
        public long TotalFiles { get; set; }
        public long TotalFolders { get; set; }
        public long TotalSize { get; set; }
        public long UsedSpace { get; set; }
        public long FreeSpace { get; set; }
        public Dictionary<string, object> Details { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        public List<string> Errors { get; set; } = new();
    }

    public class AnalysisOptions
    {
        public string Path { get; set; } = string.Empty;
        public AnalysisType Type { get; set; }
        public AnalysisDepth Depth { get; set; }
        public bool IncludeHiddenFiles { get; set; }
        public bool FollowSymlinks { get; set; }
        public string[] ExcludePatterns { get; set; } = Array.Empty<string>();
        public string[] IncludePatterns { get; set; } = Array.Empty<string>();
    }

    public class AnalysisProgress
    {
        public double Percentage { get; set; }
        public int FilesProcessed { get; set; }
        public int FoldersProcessed { get; set; }
        public string CurrentFile { get; set; } = string.Empty;
        public long BytesProcessed { get; set; }
        public long TotalBytes { get; set; }
        public TimeSpan EstimatedTimeRemaining { get; set; }
    }

    public class DiskUsageReport
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public ReportType Type { get; set; }
        public ReportFormat Format { get; set; }
        public DateTime GeneratedAt { get; set; }
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }
        public long FileSize { get; set; }
        public bool HasCharts { get; set; }
        public bool HasRecommendations { get; set; }
        public string[] Drives { get; set; } = Array.Empty<string>();
    }

    public class DiskUsageReportOptions
    {
        public ReportType Type { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string[] IncludeDrives { get; set; } = Array.Empty<string>();
        public ReportFormat OutputFormat { get; set; }
        public bool IncludeCharts { get; set; }
        public bool IncludeRecommendations { get; set; }
        public bool IncludeTrends { get; set; }
        public Dictionary<string, object> CustomOptions { get; set; } = new();
    }

    public class FileTypeStatistics
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string TypeName { get; set; } = string.Empty;
        public string Extension { get; set; } = string.Empty;
        public string CategoryId { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public int FileCount { get; set; }
        public long TotalSize { get; set; }
        public double Percentage { get; set; }
        public long AverageSize { get; set; }
        public long MinSize { get; set; }
        public long MaxSize { get; set; }
        public DateTime OldestDate { get; set; }
        public DateTime NewestDate { get; set; }
        public string? Icon { get; set; }
    }

    public class FileTypeCategory
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public string[] Extensions { get; set; } = Array.Empty<string>();
        public bool IsCustom { get; set; }
    }

    public class FileTypeAnalysisOptions
    {
        public string Path { get; set; } = string.Empty;
        public GroupBy GroupBy { get; set; }
        public long MinSizeThreshold { get; set; }
        public bool ShowUnknownTypes { get; set; }
        public string[] IncludeExtensions { get; set; } = Array.Empty<string>();
        public string[] ExcludeExtensions { get; set; } = Array.Empty<string>();
    }

    public class OptimizationSuggestion
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public OptimizationType Type { get; set; }
        public string Path { get; set; } = string.Empty;
        public long PotentialSavings { get; set; }
        public int AffectedItems { get; set; }
        public OptimizationRisk Risk { get; set; }
        public string RuleId { get; set; } = string.Empty;
        public bool IsSelected { get; set; }
        public bool IsApplied { get; set; }
        public List<string> AffectedFiles { get; set; } = new();
        public Dictionary<string, object> Details { get; set; } = new();
    }

    public class OptimizationRule
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public OptimizationType Type { get; set; }
        public bool IsEnabled { get; set; }
        public OptimizationLevel Level { get; set; }
        public string Pattern { get; set; } = string.Empty;
        public Dictionary<string, object> Parameters { get; set; } = new();
    }

    public class OptimizationResult
    {
        public string SuggestionId { get; set; } = string.Empty;
        public bool Success { get; set; }
        public long SpaceSaved { get; set; }
        public int ItemsProcessed { get; set; }
        public string? ErrorMessage { get; set; }
        public List<string> ProcessedFiles { get; set; } = new();
    }

    public class ScanOptions
    {
        public string Path { get; set; } = string.Empty;
        public OptimizationLevel Level { get; set; }
        public bool IncludeSystemFiles { get; set; }
        public List<OptimizationRule> Rules { get; set; } = new();
        public string[] ExcludePaths { get; set; } = Array.Empty<string>();
    }

    public class ApplyOptions
    {
        public bool CreateBackup { get; set; }
        public OptimizationLevel Level { get; set; }
        public string BackupPath { get; set; } = string.Empty;
        public bool ConfirmBeforeApply { get; set; }
    }

    // Enums
    public enum AnalysisType
    {
        Overview,
        Detailed,
        Performance,
        Security,
        Usage,
        Trends,
        Recommendations
    }

    public enum AnalysisDepth
    {
        Quick,
        Normal,
        Deep,
        Comprehensive
    }

    public enum ReportType
    {
        Summary,
        Detailed,
        Trend,
        Comparison,
        Forecast,
        Custom
    }

    public enum ReportFormat
    {
        HTML,
        PDF,
        Excel,
        CSV,
        JSON,
        XML
    }

    public enum ChartType
    {
        Pie,
        Doughnut,
        Bar,
        Column,
        Line,
        Area,
        Treemap,
        Sunburst
    }

    public enum GroupBy
    {
        Extension,
        Category,
        Size,
        Date,
        Type
    }

    public enum SortBy
    {
        Name,
        Size,
        Count,
        AverageSize,
        Percentage
    }

    public enum OptimizationType
    {
        RemoveDuplicates,
        CompressFiles,
        ArchiveOldFiles,
        RemoveTempFiles,
        CleanCache,
        RemoveEmptyFolders,
        Deduplicate,
        CompressImages,
        OptimizeStorage,
        RemoveJunk,
        CleanRegistry,
        Defragment
    }

    public enum OptimizationLevel
    {
        Safe,
        Moderate,
        Aggressive,
        Extreme
    }

    public enum OptimizationRisk
    {
        None,
        Low,
        Medium,
        High,
        Critical
    }
}
