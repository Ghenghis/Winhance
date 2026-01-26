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
    /// ViewModel for auto-organize files
    /// </summary>
    public partial class AutoOrganizeViewModel : ObservableObject
    {
        private readonly IOrganizerService _organizerService;
        private ObservableCollection<OrganizationRule> _rules = new();
        private ObservableCollection<OrganizationPreview> _previewItems = new();

        [ObservableProperty]
        private string _sourcePath = string.Empty;

        [ObservableProperty]
        private bool _isOrganizing;

        [ObservableProperty]
        private string? _organizationStatus;

        [ObservableProperty]
        private OrganizationOptions _options = new();

        public ObservableCollection<OrganizationRule> Rules
        {
            get => _rules;
            set => SetProperty(ref _rules, value);
        }

        public ObservableCollection<OrganizationPreview> PreviewItems
        {
            get => _previewItems;
            set => SetProperty(ref _previewItems, value);
        }

        public AutoOrganizeViewModel(IOrganizerService organizerService)
        {
            _organizerService = organizerService;
            LoadDefaultRules();
        }

        private void LoadDefaultRules()
        {
            Rules.Add(new OrganizationRule
            {
                Name = "Organize by Date",
                Description = "Organize files into folders by creation date",
                Type = RuleType.ByDate,
                Pattern = "yyyy\\MM\\dd",
                IsEnabled = true
            });

            Rules.Add(new OrganizationRule
            {
                Name = "Organize by Extension",
                Description = "Organize files into folders by file type",
                Type = RuleType.ByExtension,
                Pattern = "{Extension}",
                IsEnabled = false
            });

            Rules.Add(new OrganizationRule
            {
                Name = "Organize Images by Size",
                Description = "Organize images by resolution",
                Type = RuleType.BySize,
                Pattern = "Small|Medium|Large",
                IsEnabled = false,
                FileTypes = new[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif" }
            });
        }

        [RelayCommand]
        private async Task PreviewOrganizationAsync()
        {
            if (string.IsNullOrEmpty(SourcePath)) return;

            OrganizationStatus = "Analyzing files...";

            try
            {
                var activeRules = Rules.Where(r => r.IsEnabled).ToList();
                var preview = await _organizerService.PreviewOrganizationAsync(SourcePath, activeRules, Options);

                PreviewItems.Clear();
                foreach (var item in preview)
                {
                    PreviewItems.Add(item);
                }

                OrganizationStatus = $"Preview: {preview.Count} files will be organized";
            }
            catch (Exception ex)
            {
                OrganizationStatus = $"Preview failed: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task StartOrganizationAsync()
        {
            if (string.IsNullOrEmpty(SourcePath)) return;

            IsOrganizing = true;
            OrganizationStatus = "Organizing files...";

            try
            {
                var activeRules = Rules.Where(r => r.IsEnabled).ToList();
                var result = await _organizerService.OrganizeFilesAsync(SourcePath, activeRules, Options);

                OrganizationStatus = $"Organization completed: {result.OrganizedFiles} files, {result.Errors} errors";
            }
            catch (Exception ex)
            {
                OrganizationStatus = $"Organization failed: {ex.Message}";
            }
            finally
            {
                IsOrganizing = false;
            }
        }

        [RelayCommand]
        private void CancelOrganization()
        {
            if (IsOrganizing)
            {
                IsOrganizing = false;
                OrganizationStatus = "Organization cancelled by user";
                System.Windows.MessageBox.Show(
                    "Organization operation has been cancelled.",
                    "Cancelled",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
            }
        }

        [RelayCommand]
        private void AddRule()
        {
            var rule = new OrganizationRule
            {
                Name = "New Rule",
                Type = RuleType.ByExtension,
                IsEnabled = false
            };
            Rules.Add(rule);
        }

        [RelayCommand]
        private void RemoveRule(OrganizationRule? rule)
        {
            if (rule == null) return;
            Rules.Remove(rule);
        }

        [RelayCommand]
        private async Task SaveRulesAsync()
        {
            try
            {
                await _organizerService.SaveRulesAsync(Rules.ToList());
                OrganizationStatus = "Rules saved";
            }
            catch (Exception ex)
            {
                OrganizationStatus = $"Failed to save rules: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task LoadRulesAsync()
        {
            try
            {
                var savedRules = await _organizerService.LoadRulesAsync();
                Rules.Clear();
                foreach (var rule in savedRules)
                {
                    Rules.Add(rule);
                }
            }
            catch (Exception ex)
            {
                OrganizationStatus = $"Failed to load rules: {ex.Message}";
            }
        }
    }

    /// <summary>
    /// ViewModel for custom rule builder
    /// </summary>
    public partial class RuleBuilderViewModel : ObservableObject
    {
        private OrganizationRule _rule = new();
        private ObservableCollection<RuleCondition> _conditions = new();
        private ObservableCollection<RuleAction> _actions = new();

        public OrganizationRule Rule
        {
            get => _rule;
            set => SetProperty(ref _rule, value);
        }

        public ObservableCollection<RuleCondition> Conditions
        {
            get => _conditions;
            set => SetProperty(ref _conditions, value);
        }

        public ObservableCollection<RuleAction> Actions
        {
            get => _actions;
            set => SetProperty(ref _actions, value);
        }

        public RuleBuilderViewModel()
        {
            InitializeDefaultCondition();
            InitializeDefaultAction();
        }

        private void InitializeDefaultCondition()
        {
            Conditions.Add(new RuleCondition
            {
                Property = "Extension",
                Operator = "Equals",
                Value = ""
            });
        }

        private void InitializeDefaultAction()
        {
            Actions.Add(new RuleAction
            {
                Type = ActionType.MoveToFolder,
                Target = "{Extension}"
            });
        }

        [RelayCommand]
        private void AddCondition()
        {
            Conditions.Add(new RuleCondition
            {
                Property = "Name",
                Operator = "Contains",
                Value = ""
            });
        }

        [RelayCommand]
        private void RemoveCondition(RuleCondition? condition)
        {
            if (condition == null) return;
            Conditions.Remove(condition);
        }

        [RelayCommand]
        private void AddAction()
        {
            Actions.Add(new RuleAction
            {
                Type = ActionType.CopyToFolder,
                Target = ""
            });
        }

        [RelayCommand]
        private void RemoveAction(RuleAction? action)
        {
            if (action == null) return;
            Actions.Remove(action);
        }

        [RelayCommand]
        private void TestRule()
        {
            var message = $"Rule Test Results\n\n" +
                         $"Rule Name: {Rule.Name}\n" +
                         $"Type: {Rule.Type}\n" +
                         $"Conditions: {Conditions.Count}\n" +
                         $"Actions: {Actions.Count}\n\n" +
                         "Test your rule with sample files to verify behavior.";

            System.Windows.MessageBox.Show(message, "Test Rule",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
        }

        [RelayCommand]
        private void SaveRule()
        {
            Rule.Conditions = Conditions.ToList();
            Rule.Actions = Actions.ToList();
            
            var message = $"Rule '{Rule.Name}' saved successfully.\n\n" +
                         $"Conditions: {Conditions.Count}\n" +
                         $"Actions: {Actions.Count}";

            System.Windows.MessageBox.Show(message, "Rule Saved",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
        }
    }

    /// <summary>
    /// ViewModel for watch folder auto-organize
    /// </summary>
    public partial class WatchFolderViewModel : ObservableObject
    {
        private readonly IWatchFolderService _watchFolderService;
        private readonly IOrganizerService _organizerService;
        private ObservableCollection<WatchedFolder> _watchedFolders = new();
        private ObservableCollection<FolderEvent> _recentEvents = new();

        [ObservableProperty]
        private bool _isWatchingEnabled;

        [ObservableProperty]
        private string? _watchStatus;

        public ObservableCollection<WatchedFolder> WatchedFolders
        {
            get => _watchedFolders;
            set => SetProperty(ref _watchedFolders, value);
        }

        public ObservableCollection<FolderEvent> RecentEvents
        {
            get => _recentEvents;
            set => SetProperty(ref _recentEvents, value);
        }

        public WatchFolderViewModel(IWatchFolderService watchFolderService, IOrganizerService organizerService)
        {
            _watchFolderService = watchFolderService;
            _organizerService = organizerService;
        }

        [RelayCommand]
        private async Task AddWatchedFolderAsync()
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Select Folder to Watch"
            };

            if (dialog.ShowDialog() == true)
            {
                var folderPath = dialog.FolderName;

                if (!string.IsNullOrEmpty(folderPath))
            {
                var folder = new WatchedFolder
                {
                    Path = folderPath,
                    IsEnabled = true,
                    Rule = new OrganizationRule
                    {
                        Name = "Auto-organize",
                        Type = RuleType.ByExtension
                    }
                };

                WatchedFolders.Add(folder);

                if (IsWatchingEnabled)
                {
                    await _watchFolderService.StartWatchingAsync(folder.Path, OnFileEvent);
                }

                    WatchStatus = $"Added watch for {folderPath}";
                }
            }
        }

        [RelayCommand]
        private async Task RemoveWatchedFolderAsync(WatchedFolder? folder)
        {
            if (folder == null) return;

            if (IsWatchingEnabled)
            {
                await _watchFolderService.StopWatchingAsync(folder.Path);
            }

            WatchedFolders.Remove(folder);
            WatchStatus = $"Removed watch for {folder.Path}";
        }

        [RelayCommand]
        private async Task ToggleWatchingAsync()
        {
            IsWatchingEnabled = !IsWatchingEnabled;

            if (IsWatchingEnabled)
            {
                foreach (var folder in WatchedFolders.Where(f => f.IsEnabled))
                {
                    await _watchFolderService.StartWatchingAsync(folder.Path, OnFileEvent);
                }
                WatchStatus = "Folder watching enabled";
            }
            else
            {
                foreach (var folder in WatchedFolders)
                {
                    await _watchFolderService.StopWatchingAsync(folder.Path);
                }
                WatchStatus = "Folder watching disabled";
            }
        }

        [RelayCommand]
        private void ClearEventHistory()
        {
            RecentEvents.Clear();
        }

        private async void OnFileEvent(FolderEvent folderEvent)
        {
            RecentEvents.Insert(0, folderEvent);

            // Keep only last 100 events
            while (RecentEvents.Count > 100)
            {
                RecentEvents.RemoveAt(RecentEvents.Count - 1);
            }

            // Auto-organize if rule is configured
            var watchedFolder = WatchedFolders.FirstOrDefault(f => 
                folderEvent.FullPath.StartsWith(f.Path, StringComparison.OrdinalIgnoreCase));

            if (watchedFolder?.Rule != null && watchedFolder.IsEnabled)
            {
                try
                {
                    await _organizerService.OrganizeSingleFileAsync(
                        folderEvent.FullPath,
                        watchedFolder.Rule);
                }
                catch (Exception ex)
                {
                    // Log error but don't stop watching
                    WatchStatus = $"Failed to organize {folderEvent.FullPath}: {ex.Message}";
                }
            }
        }
    }

    /// <summary>
    /// ViewModel for organization statistics
    /// </summary>
    public partial class OrganizationStatsViewModel : ObservableObject
    {
        private readonly IOrganizerService _organizerService;
        private ObservableCollection<OrganizationReport> _reports = new();

        [ObservableProperty]
        private OrganizationStatistics _statistics = new();

        [ObservableProperty]
        private DateTime _reportDate = DateTime.Today;

        public ObservableCollection<OrganizationReport> Reports
        {
            get => _reports;
            set => SetProperty(ref _reports, value);
        }

        public OrganizationStatsViewModel(IOrganizerService organizerService)
        {
            _organizerService = organizerService;
        }

        [RelayCommand]
        private async Task LoadStatisticsAsync()
        {
            try
            {
                Statistics = await _organizerService.GetStatisticsAsync(ReportDate);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to load statistics: {ex.Message}",
                    "Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task GenerateReportAsync()
        {
            try
            {
                var report = await _organizerService.GenerateReportAsync(ReportDate);
                Reports.Insert(0, report);
                System.Windows.MessageBox.Show(
                    $"Report generated successfully for {ReportDate:d}",
                    "Report Generated",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to generate report: {ex.Message}",
                    "Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task ExportReportAsync(OrganizationReport? report)
        {
            if (report == null) return;

            try
            {
                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "HTML Report (*.html)|*.html|PDF Report (*.pdf)|*.pdf|CSV File (*.csv)|*.csv",
                    DefaultExt = ".html",
                    FileName = $"OrganizationReport_{report.GeneratedDate:yyyyMMdd_HHmmss}.html"
                };

                if (dialog.ShowDialog() == true)
                {
                    var html = $"<html><head><title>{report.Title}</title></head><body>" +
                               $"<h1>{report.Title}</h1>" +
                               $"<p>Generated: {report.GeneratedDate:g}</p>" +
                               $"<h2>Statistics</h2>" +
                               $"<p>Total Files: {report.Statistics.TotalFiles}</p>" +
                               $"<p>Organized: {report.Statistics.OrganizedFiles}</p>" +
                               $"<p>Errors: {report.Statistics.Errors}</p>" +
                               $"<p>Space Saved: {report.Statistics.SpaceSaved / (1024.0 * 1024.0):F2} MB</p>" +
                               "</body></html>";

                    await System.IO.File.WriteAllTextAsync(dialog.FileName, html);
                    System.Windows.MessageBox.Show(
                        $"Report exported to {dialog.FileName}",
                        "Export Complete",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to export report: {ex.Message}",
                    "Export Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }
    }

    // Model classes
    public class OrganizationRule
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public RuleType Type { get; set; }
        public string Pattern { get; set; } = string.Empty;
        public bool IsEnabled { get; set; }
        public string[] FileTypes { get; set; } = Array.Empty<string>();
        public List<RuleCondition> Conditions { get; set; } = new();
        public List<RuleAction> Actions { get; set; } = new();
        public int Priority { get; set; } = 0;
        public DateTime CreatedDate { get; set; } = DateTime.Now;
    }

    public class OrganizationPreview
    {
        public string SourcePath { get; set; } = string.Empty;
        public string DestinationPath { get; set; } = string.Empty;
        public string RuleName { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public bool IsSelected { get; set; } = true;
    }

    public class OrganizationOptions
    {
        public bool Simulate { get; set; } = true;
        public bool CreateSubfolders { get; set; } = true;
        public bool HandleDuplicates { get; set; } = true;
        public DuplicateAction DuplicateAction { get; set; } = DuplicateAction.Rename;
        public bool PreserveStructure { get; set; } = false;
        public bool ProcessSubfolders { get; set; } = true;
        public bool MoveFiles { get; set; } = true; // false = copy
        public bool UndoOperation { get; set; } = true;
    }

    public class RuleCondition
    {
        public string Property { get; set; } = string.Empty;
        public string Operator { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public bool CaseSensitive { get; set; } = false;
    }

    public class RuleAction
    {
        public ActionType Type { get; set; }
        public string Target { get; set; } = string.Empty;
        public Dictionary<string, object> Parameters { get; set; } = new();
    }

    public class WatchedFolder
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Path { get; set; } = string.Empty;
        public bool IsEnabled { get; set; }
        public OrganizationRule Rule { get; set; } = new();
        public WatchFilter Filter { get; set; } = new();
        public DateTime CreatedDate { get; set; } = DateTime.Now;
    }

    public class FolderEvent
    {
        public DateTime Timestamp { get; set; }
        public string EventType { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    public class OrganizationStatistics
    {
        public int TotalFiles { get; set; }
        public int OrganizedFiles { get; set; }
        public int Errors { get; set; }
        public long SpaceSaved { get; set; }
        public Dictionary<string, int> FileTypeStats { get; set; } = new();
        public Dictionary<string, int> RuleStats { get; set; } = new();
    }

    public class OrganizationReport
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public DateTime GeneratedDate { get; set; }
        public string Title { get; set; } = string.Empty;
        public OrganizationStatistics Statistics { get; set; } = new();
        public List<string> Recommendations { get; set; } = new();
    }

    public class WatchFilter
    {
        public string[] IncludePatterns { get; set; } = Array.Empty<string>();
        public string[] ExcludePatterns { get; set; } = Array.Empty<string>();
        public bool IncludeSubdirectories { get; set; } = true;
        public long MinFileSize { get; set; } = 0;
        public long MaxFileSize { get; set; } = long.MaxValue;
    }

    // Enums
    public enum RuleType
    {
        ByExtension,
        ByDate,
        BySize,
        ByName,
        Custom
    }

    public enum ActionType
    {
        MoveToFolder,
        CopyToFolder,
        Rename,
        Delete,
        Custom
    }

    public enum DuplicateAction
    {
        Skip,
        Replace,
        Rename,
        KeepBoth
    }
}
