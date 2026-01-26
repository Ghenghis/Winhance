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
    /// ViewModel for folder comparison
    /// </summary>
    public partial class FolderComparisonViewModel : ObservableObject
    {
        private readonly IFolderComparisonService _comparisonService;
        private ObservableCollection<ComparisonItem> _comparisonResults = new();
        private ObservableCollection<ComparisonDifference> _differences = new();

        [ObservableProperty]
        private string _leftPath = string.Empty;

        [ObservableProperty]
        private string _rightPath = string.Empty;

        [ObservableProperty]
        private bool _isComparing;

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        [ObservableProperty]
        private ComparisonMethod _comparisonMethod = ComparisonMethod.SizeAndDate;

        [ObservableProperty]
        private bool _includeSubfolders = true;

        [ObservableProperty]
        private bool _ignoreWhitespace = false;

        [ObservableProperty]
        private bool _ignoreCase = false;

        [ObservableProperty]
        private DifferenceFilter _filter = DifferenceFilter.All;

        [ObservableProperty]
        private ComparisonItem? _selectedItem;

        [ObservableProperty]
        private ComparisonDifference? _selectedDifference;

        [ObservableProperty]
        private int _totalFiles;

        [ObservableProperty]
        private int _identicalFiles;

        [ObservableProperty]
        private int _differentFiles;

        [ObservableProperty]
        private int _uniqueLeftFiles;

        [ObservableProperty]
        private int _uniqueRightFiles;

        public ObservableCollection<ComparisonItem> ComparisonResults
        {
            get => _comparisonResults;
            set => SetProperty(ref _comparisonResults, value);
        }

        public ObservableCollection<ComparisonDifference> Differences
        {
            get => _differences;
            set => SetProperty(ref _differences, value);
        }

        public FolderComparisonViewModel(IFolderComparisonService comparisonService)
        {
            _comparisonService = comparisonService;
        }

        [RelayCommand]
        private async Task CompareFoldersAsync()
        {
            if (string.IsNullOrEmpty(LeftPath) || string.IsNullOrEmpty(RightPath)) return;

            IsComparing = true;
            StatusMessage = "Comparing folders...";
            ComparisonResults.Clear();
            Differences.Clear();

            var options = new ComparisonOptions
            {
                LeftPath = LeftPath,
                RightPath = RightPath,
                Method = ComparisonMethod,
                IncludeSubfolders = IncludeSubfolders,
                IgnoreWhitespace = IgnoreWhitespace,
                IgnoreCase = IgnoreCase
            };

            try
            {
                var results = await _comparisonService.CompareFoldersAsync(options);
                
                TotalFiles = results.TotalFiles;
                IdenticalFiles = results.IdenticalFiles;
                DifferentFiles = results.DifferentFiles;
                UniqueLeftFiles = results.UniqueLeftFiles;
                UniqueRightFiles = results.UniqueRightFiles;

                foreach (var item in results.Items)
                {
                    ComparisonResults.Add(item);
                }

                foreach (var diff in results.Differences)
                {
                    Differences.Add(diff);
                }

                StatusMessage = $"Comparison complete. {TotalFiles} files compared";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Comparison failed: {ex.Message}";
            }
            finally
            {
                IsComparing = false;
            }
        }

        [RelayCommand]
        private void FilterResults(DifferenceFilter filter)
        {
            Filter = filter;
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            var filtered = Filter switch
            {
                DifferenceFilter.All => ComparisonResults,
                DifferenceFilter.Identical => ComparisonResults.Where(i => i.Status == ComparisonStatus.Identical),
                DifferenceFilter.Different => ComparisonResults.Where(i => i.Status == ComparisonStatus.Different),
                DifferenceFilter.UniqueLeft => ComparisonResults.Where(i => i.Status == ComparisonStatus.UniqueLeft),
                DifferenceFilter.UniqueRight => ComparisonResults.Where(i => i.Status == ComparisonStatus.UniqueRight),
                _ => ComparisonResults
            };

            ComparisonResults.Clear();
            foreach (var item in filtered)
            {
                ComparisonResults.Add(item);
            }
        }

        [RelayCommand]
        private void CopyToLeft(ComparisonItem? item)
        {
            if (item == null || item.Status == ComparisonStatus.UniqueLeft) return;

            try
            {
                System.IO.File.Copy(item.RightPath, item.LeftPath, true);
                StatusMessage = $"Copied {item.Name} from right to left";
                _ = CompareFoldersAsync();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Copy failed: {ex.Message}";
            }
        }

        [RelayCommand]
        private void CopyToRight(ComparisonItem? item)
        {
            if (item == null || item.Status == ComparisonStatus.UniqueRight) return;

            try
            {
                System.IO.File.Copy(item.LeftPath, item.RightPath, true);
                StatusMessage = $"Copied {item.Name} from left to right";
                _ = CompareFoldersAsync();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Copy failed: {ex.Message}";
            }
        }

        [RelayCommand]
        private void DeleteLeft(ComparisonItem? item)
        {
            if (item == null || item.Status == ComparisonStatus.UniqueRight) return;

            var result = System.Windows.MessageBox.Show(
                $"Delete {item.Name} from left folder?",
                "Confirm Delete",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning);

            if (result == System.Windows.MessageBoxResult.Yes)
            {
                try
                {
                    System.IO.File.Delete(item.LeftPath);
                    StatusMessage = $"Deleted {item.Name} from left";
                    _ = CompareFoldersAsync();
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Delete failed: {ex.Message}";
                }
            }
        }

        [RelayCommand]
        private void DeleteRight(ComparisonItem? item)
        {
            if (item == null || item.Status == ComparisonStatus.UniqueLeft) return;

            var result = System.Windows.MessageBox.Show(
                $"Delete {item.Name} from right folder?",
                "Confirm Delete",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning);

            if (result == System.Windows.MessageBoxResult.Yes)
            {
                try
                {
                    System.IO.File.Delete(item.RightPath);
                    StatusMessage = $"Deleted {item.Name} from right";
                    _ = CompareFoldersAsync();
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Delete failed: {ex.Message}";
                }
            }
        }

        [RelayCommand]
        private void OpenLeftFile(ComparisonItem? item)
        {
            if (item == null || string.IsNullOrEmpty(item.LeftPath)) return;

            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = item.LeftPath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error opening file: {ex.Message}";
            }
        }

        [RelayCommand]
        private void OpenRightFile(ComparisonItem? item)
        {
            if (item == null || string.IsNullOrEmpty(item.RightPath)) return;

            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = item.RightPath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error opening file: {ex.Message}";
            }
        }

        [RelayCommand]
        private void CompareFiles(ComparisonItem? item)
        {
            if (item == null || item.Status != ComparisonStatus.Different) return;

            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "fc.exe",
                    Arguments = $"/N \"{item.LeftPath}\" \"{item.RightPath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                System.Diagnostics.Process.Start(psi);
                StatusMessage = $"Comparing {item.Name}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Comparison failed: {ex.Message}";
            }
        }

        [RelayCommand]
        private void ViewDifferences(ComparisonItem? item)
        {
            if (item == null) return;

            var diffs = item.Differences.Count;
            var message = $"File: {item.Name}\n\n" +
                         $"Status: {item.Status}\n" +
                         $"Differences: {diffs}\n\n";
            
            if (diffs > 0)
            {
                message += string.Join("\n", item.Differences.Take(10).Select(d => 
                    $"- {d.Type}: {d.Description}"));
                if (diffs > 10) message += $"\n... and {diffs - 10} more";
            }

            System.Windows.MessageBox.Show(message, "File Differences",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
        }

        [RelayCommand]
        private async Task SynchronizeAsync()
        {
            var result = System.Windows.MessageBox.Show(
                $"Synchronize {LeftPath} with {RightPath}?\n\n" +
                $"This will copy different/unique files.",
                "Confirm Synchronization",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Question);

            if (result == System.Windows.MessageBoxResult.Yes)
            {
                try
                {
                    foreach (var item in ComparisonResults.Where(i => i.Status != ComparisonStatus.Identical))
                    {
                        if (item.Status == ComparisonStatus.UniqueLeft && !string.IsNullOrEmpty(item.LeftPath))
                            System.IO.File.Copy(item.LeftPath, System.IO.Path.Combine(RightPath, item.RelativePath), true);
                        else if (item.Status == ComparisonStatus.UniqueRight && !string.IsNullOrEmpty(item.RightPath))
                            System.IO.File.Copy(item.RightPath, System.IO.Path.Combine(LeftPath, item.RelativePath), true);
                    }
                    StatusMessage = "Synchronization complete";
                    _ = CompareFoldersAsync();
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Sync failed: {ex.Message}";
                }
            }
        }

        [RelayCommand]
        private void ExportResults()
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "CSV File (*.csv)|*.csv|JSON File (*.json)|*.json",
                DefaultExt = ".csv",
                FileName = $"Comparison_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    if (dialog.FileName.EndsWith(".json"))
                    {
                        var json = System.Text.Json.JsonSerializer.Serialize(ComparisonResults,
                            new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                        System.IO.File.WriteAllText(dialog.FileName, json);
                    }
                    else
                    {
                        var csv = new System.Text.StringBuilder();
                        csv.AppendLine("Name,Status,LeftSize,RightSize,LeftModified,RightModified");
                        foreach (var item in ComparisonResults)
                        {
                            csv.AppendLine($"{item.Name},{item.Status},{item.LeftSize},{item.RightSize},{item.LeftModified:g},{item.RightModified:g}");
                        }
                        System.IO.File.WriteAllText(dialog.FileName, csv.ToString());
                    }
                    StatusMessage = $"Results exported to {dialog.FileName}";
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
            ComparisonResults.Clear();
            Differences.Clear();
            TotalFiles = 0;
            IdenticalFiles = 0;
            DifferentFiles = 0;
            UniqueLeftFiles = 0;
            UniqueRightFiles = 0;
            StatusMessage = "Results cleared";
        }

        [RelayCommand]
        private void BrowseLeftPath()
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Select Left Folder"
            };

            if (dialog.ShowDialog() == true)
            {
                LeftPath = dialog.FolderName;
            }
        }

        [RelayCommand]
        private void BrowseRightPath()
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Select Right Folder"
            };

            if (dialog.ShowDialog() == true)
            {
                RightPath = dialog.FolderName;
            }
        }

        [RelayCommand]
        private void SwapPaths()
        {
            var temp = LeftPath;
            LeftPath = RightPath;
            RightPath = temp;
        }
    }

    /// <summary>
    /// ViewModel for file synchronization
    /// </summary>
    public partial class FileSynchronizationViewModel : ObservableObject
    {
        private readonly ISynchronizationService _syncService;
        private ObservableCollection<SyncProfile> _syncProfiles = new();
        private ObservableCollection<SyncConflict> _conflicts = new();
        private ObservableCollection<SyncOperation> _operations = new();

        [ObservableProperty]
        private SyncProfile? _selectedProfile;

        [ObservableProperty]
        private bool _isSyncing;

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        [ObservableProperty]
        private SyncDirection _direction = SyncDirection.Bidirectional;

        [ObservableProperty]
        private ConflictResolution _conflictResolution = ConflictResolution.Prompt;

        [ObservableProperty]
        private bool _previewMode = true;

        [ObservableProperty]
        private double _syncProgress;

        [ObservableProperty]
        private int _filesProcessed;

        [ObservableProperty]
        private int _totalFiles;

        [ObservableProperty]
        private long _bytesTransferred;

        [ObservableProperty]
        private TimeSpan _estimatedTimeRemaining;

        [ObservableProperty]
        private bool _isCreatingProfile;

        [ObservableProperty]
        private string _profileName = string.Empty;

        [ObservableProperty]
        private string _sourcePath = string.Empty;

        [ObservableProperty]
        private string _destinationPath = string.Empty;

        [ObservableProperty]
        private string[] _includePatterns = Array.Empty<string>();

        [ObservableProperty]
        private string[] _excludePatterns = Array.Empty<string>();

        public ObservableCollection<SyncProfile> SyncProfiles
        {
            get => _syncProfiles;
            set => SetProperty(ref _syncProfiles, value);
        }

        public ObservableCollection<SyncConflict> Conflicts
        {
            get => _conflicts;
            set => SetProperty(ref _conflicts, value);
        }

        public ObservableCollection<SyncOperation> Operations
        {
            get => _operations;
            set => SetProperty(ref _operations, value);
        }

        public FileSynchronizationViewModel(ISynchronizationService syncService)
        {
            _syncService = syncService;
            _ = LoadSyncProfilesAsync();
        }

        private async Task LoadSyncProfilesAsync()
        {
            try
            {
                var profiles = await _syncService.GetSyncProfilesAsync();
                SyncProfiles.Clear();
                foreach (var profile in profiles.OrderByDescending(p => p.LastSync))
                {
                    SyncProfiles.Add(profile);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading profiles: {ex.Message}";
            }
        }

        partial void OnSelectedProfileChanged(SyncProfile? value)
        {
            if (value != null)
            {
                Direction = value.Direction;
                ConflictResolution = value.ConflictResolution;
                IncludePatterns = value.IncludePatterns;
                ExcludePatterns = value.ExcludePatterns;
            }
        }

        [RelayCommand]
        private void StartCreateProfile()
        {
            IsCreatingProfile = true;
            ProfileName = string.Empty;
            SourcePath = string.Empty;
            DestinationPath = string.Empty;
            IncludePatterns = Array.Empty<string>();
            ExcludePatterns = Array.Empty<string>();
        }

        [RelayCommand]
        private void CancelCreateProfile()
        {
            IsCreatingProfile = false;
            ProfileName = string.Empty;
            SourcePath = string.Empty;
            DestinationPath = string.Empty;
        }

        [RelayCommand]
        private async Task CreateProfileAsync()
        {
            if (string.IsNullOrEmpty(ProfileName) || string.IsNullOrEmpty(SourcePath) || string.IsNullOrEmpty(DestinationPath)) return;

            try
            {
                var profile = new SyncProfile
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = ProfileName,
                    SourcePath = SourcePath,
                    DestinationPath = DestinationPath,
                    Direction = Direction,
                    ConflictResolution = ConflictResolution,
                    IncludePatterns = IncludePatterns,
                    ExcludePatterns = ExcludePatterns,
                    CreatedAt = DateTime.Now,
                    LastSync = null,
                    IsEnabled = true
                };

                await _syncService.CreateSyncProfileAsync(profile);
                SyncProfiles.Insert(0, profile);
                
                CancelCreateProfile();
                StatusMessage = $"Profile '{ProfileName}' created";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error creating profile: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task PreviewSyncAsync()
        {
            if (SelectedProfile == null) return;

            PreviewMode = true;
            await StartSyncAsync();
        }

        [RelayCommand]
        private async Task StartSyncAsync()
        {
            if (SelectedProfile == null) return;

            IsSyncing = true;
            StatusMessage = PreviewMode ? "Previewing synchronization..." : "Starting synchronization...";
            Operations.Clear();
            Conflicts.Clear();
            SyncProgress = 0;
            FilesProcessed = 0;
            TotalFiles = 0;
            BytesTransferred = 0;

            var options = new SyncOptions
            {
                ProfileId = SelectedProfile.Id,
                Direction = Direction,
                ConflictResolution = ConflictResolution,
                PreviewMode = PreviewMode,
                IncludePatterns = IncludePatterns,
                ExcludePatterns = ExcludePatterns
            };

            try
            {
                var progress = new Progress<SyncProgress>(p =>
                {
                    SyncProgress = p.Percentage;
                    FilesProcessed = p.FilesProcessed;
                    TotalFiles = p.TotalFiles;
                    BytesTransferred = p.BytesTransferred;
                    EstimatedTimeRemaining = p.EstimatedTimeRemaining;
                    StatusMessage = PreviewMode 
                        ? $"Previewing... {p.FilesProcessed:N0} files"
                        : $"Syncing... {p.FilesProcessed:N0}/{p.TotalFiles:N0} files";
                });

                var result = await _syncService.SynchronizeAsync(options, progress);

                foreach (var operation in result.Operations)
                {
                    Operations.Add(operation);
                }

                foreach (var conflict in result.Conflicts)
                {
                    Conflicts.Add(conflict);
                }

                if (!PreviewMode)
                {
                    SelectedProfile.LastSync = DateTime.Now;
                    await _syncService.UpdateSyncProfileAsync(SelectedProfile);
                }

                StatusMessage = PreviewMode 
                    ? $"Preview complete. {Operations.Count} operations planned"
                    : $"Sync complete. {result.SuccessfulOperations} successful, {result.FailedOperations} failed";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Sync failed: {ex.Message}";
            }
            finally
            {
                IsSyncing = false;
                PreviewMode = false;
            }
        }

        [RelayCommand]
        private void CancelSync()
        {
            _syncService.CancelSync();
            IsSyncing = false;
            StatusMessage = "Sync cancelled";
        }

        [RelayCommand]
        private async Task ResolveConflictAsync(SyncConflict? conflict, ConflictResolution resolution)
        {
            if (conflict == null) return;

            try
            {
                await _syncService.ResolveConflictAsync(conflict.Id, resolution);
                Conflicts.Remove(conflict);
                StatusMessage = "Conflict resolved";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error resolving conflict: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task DeleteProfileAsync(SyncProfile? profile)
        {
            if (profile == null) return;

            try
            {
                await _syncService.DeleteSyncProfileAsync(profile.Id);
                SyncProfiles.Remove(profile);
                StatusMessage = $"Profile '{profile.Name}' deleted";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error deleting profile: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task ToggleProfileAsync(SyncProfile? profile)
        {
            if (profile == null) return;

            try
            {
                profile.IsEnabled = !profile.IsEnabled;
                await _syncService.UpdateSyncProfileAsync(profile);
                StatusMessage = profile.IsEnabled 
                    ? $"Profile '{profile.Name}' enabled"
                    : $"Profile '{profile.Name}' disabled";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error toggling profile: {ex.Message}";
            }
        }

        [RelayCommand]
        private void ViewSyncHistory(SyncProfile? profile)
        {
            if (profile == null) return;

            var message = $"Sync History for: {profile.Name}\n\n" +
                         $"Total Syncs: {profile.Statistics.TotalSyncs}\n" +
                         $"Successful: {profile.Statistics.SuccessfulSyncs}\n" +
                         $"Failed: {profile.Statistics.FailedSyncs}\n" +
                         $"Last Sync: {profile.LastSync:g}\n" +
                         $"Total Data: {profile.Statistics.TotalBytesTransferred / (1024.0 * 1024.0):F2} MB";

            System.Windows.MessageBox.Show(message, "Sync History",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
        }

        [RelayCommand]
        private void ScheduleSync(SyncProfile? profile)
        {
            if (profile == null) return;

            var message = $"Schedule sync for: {profile.Name}\n\n" +
                         $"This feature requires Windows Task Scheduler integration.\n" +
                         $"Configure schedule in Task Scheduler or use sync profiles.";

            System.Windows.MessageBox.Show(message, "Schedule Sync",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
        }

        [RelayCommand]
        private void ExportResults()
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "JSON File (*.json)|*.json|CSV File (*.csv)|*.csv",
                DefaultExt = ".json",
                FileName = $"SyncResults_{DateTime.Now:yyyyMMdd_HHmmss}.json"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var json = System.Text.Json.JsonSerializer.Serialize(SyncProfiles,
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
        private void BrowseSourcePath()
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Select Source Folder"
            };

            if (dialog.ShowDialog() == true)
            {
                SourcePath = dialog.FolderName;
            }
        }

        [RelayCommand]
        private void BrowseDestinationPath()
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Select Destination Folder"
            };

            if (dialog.ShowDialog() == true)
            {
                DestinationPath = dialog.FolderName;
            }
        }

        [RelayCommand]
        private void AddIncludePattern()
        {
            var pattern = Microsoft.VisualBasic.Interaction.InputBox(
                "Enter file pattern to include (e.g., *.txt, *.doc):",
                "Add Include Pattern",
                "*.*");

            if (!string.IsNullOrEmpty(pattern))
            {
                StatusMessage = $"Include pattern added: {pattern}";
            }
        }

        [RelayCommand]
        private void AddExcludePattern()
        {
            var pattern = Microsoft.VisualBasic.Interaction.InputBox(
                "Enter file pattern to exclude (e.g., *.tmp, *.bak):",
                "Add Exclude Pattern",
                "*.tmp");

            if (!string.IsNullOrEmpty(pattern))
            {
                StatusMessage = $"Exclude pattern added: {pattern}";
            }
        }
    }

    /// <summary>
    /// ViewModel for real-time sync monitoring
    /// </summary>
    public partial class RealTimeSyncViewModel : ObservableObject
    {
        private readonly IRealTimeSyncService _realTimeSyncService;
        private ObservableCollection<SyncEvent> _syncEvents = new();
        private ObservableCollection<MonitoredFolder> _monitoredFolders = new();

        [ObservableProperty]
        private bool _isMonitoring;

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        [ObservableProperty]
        private SyncEvent? _selectedEvent;

        [ObservableProperty]
        private int _eventsPerSecond;

        [ObservableProperty]
        private long _totalBytesSynced;

        [ObservableProperty]
        private int _totalFilesSynced;

        [ObservableProperty]
        private DateTime _startTime;

        [ObservableProperty]
        private bool _autoStart = true;

        [ObservableProperty]
        private int _maxEvents = 1000;

        [ObservableProperty]
        private EventType _eventFilter = EventType.All;

        public ObservableCollection<SyncEvent> SyncEvents
        {
            get => _syncEvents;
            set => SetProperty(ref _syncEvents, value);
        }

        public ObservableCollection<MonitoredFolder> MonitoredFolders
        {
            get => _monitoredFolders;
            set => SetProperty(ref _monitoredFolders, value);
        }

        public RealTimeSyncViewModel(IRealTimeSyncService realTimeSyncService)
        {
            _realTimeSyncService = realTimeSyncService;
            _realTimeSyncService.SyncEvent += OnSyncEvent;
            _ = LoadMonitoredFoldersAsync();
        }

        private async Task LoadMonitoredFoldersAsync()
        {
            try
            {
                var folders = await _realTimeSyncService.GetMonitoredFoldersAsync();
                MonitoredFolders.Clear();
                foreach (var folder in folders)
                {
                    MonitoredFolders.Add(folder);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading folders: {ex.Message}";
            }
        }

        private void OnSyncEvent(object? sender, SyncEvent e)
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                if (EventFilter != EventType.All && e.Type != EventFilter)
                    return;

                SyncEvents.Insert(0, e);
                
                while (SyncEvents.Count > MaxEvents)
                {
                    SyncEvents.RemoveAt(SyncEvents.Count - 1);
                }

                UpdateStatistics();
            });
        }

        private void UpdateStatistics()
        {
            var now = DateTime.Now;
            var recentEvents = SyncEvents.Where(e => (now - e.Timestamp).TotalSeconds <= 1).Count();
            EventsPerSecond = recentEvents;
            TotalFilesSynced = SyncEvents.Count(e => e.Type == EventType.FileSynced);
            TotalBytesSynced = SyncEvents.Where(e => e.Type == EventType.FileSynced).Sum(e => e.FileSize);
        }

        [RelayCommand]
        private async Task StartMonitoringAsync()
        {
            try
            {
                await _realTimeSyncService.StartMonitoringAsync();
                IsMonitoring = true;
                StartTime = DateTime.Now;
                StatusMessage = "Real-time sync monitoring started";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error starting monitoring: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task StopMonitoringAsync()
        {
            try
            {
                await _realTimeSyncService.StopMonitoringAsync();
                IsMonitoring = false;
                StatusMessage = "Real-time sync monitoring stopped";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error stopping monitoring: {ex.Message}";
            }
        }

        [RelayCommand]
        private void ClearEvents()
        {
            SyncEvents.Clear();
            EventsPerSecond = 0;
            TotalFilesSynced = 0;
            TotalBytesSynced = 0;
            StatusMessage = "Events cleared";
        }

        [RelayCommand]
        private void FilterEvents(EventType filter)
        {
            EventFilter = filter;
        }

        [RelayCommand]
        private async Task AddMonitoredFolderAsync()
        {
            var sourceDialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Select Folder to Monitor"
            };

            if (sourceDialog.ShowDialog() == true)
            {
                var destDialog = new Microsoft.Win32.OpenFolderDialog
                {
                    Title = "Select Destination Folder"
                };

                if (destDialog.ShowDialog() == true)
                {
                    try
                    {
                        var folder = new MonitoredFolder
                        {
                            Path = sourceDialog.FolderName,
                            DestinationPath = destDialog.FolderName,
                            IsActive = true,
                            CreatedAt = DateTime.Now
                        };

                        await _realTimeSyncService.AddMonitoredFolderAsync(folder);
                        MonitoredFolders.Add(folder);
                        StatusMessage = $"Added monitoring for: {folder.Path}";
                    }
                    catch (Exception ex)
                    {
                        StatusMessage = $"Failed to add folder: {ex.Message}";
                    }
                }
            }
        }

        [RelayCommand]
        private async Task RemoveMonitoredFolderAsync(MonitoredFolder? folder)
        {
            if (folder == null) return;

            try
            {
                await _realTimeSyncService.RemoveMonitoredFolderAsync(folder.Id);
                MonitoredFolders.Remove(folder);
                StatusMessage = $"Removed monitoring for: {folder.Path}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error removing folder: {ex.Message}";
            }
        }

        [RelayCommand]
        private void ExportEvents()
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "CSV File (*.csv)|*.csv|JSON File (*.json)|*.json",
                DefaultExt = ".csv",
                FileName = $"SyncEvents_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    if (dialog.FileName.EndsWith(".json"))
                    {
                        var json = System.Text.Json.JsonSerializer.Serialize(SyncEvents,
                            new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                        System.IO.File.WriteAllText(dialog.FileName, json);
                    }
                    else
                    {
                        var csv = new System.Text.StringBuilder();
                        csv.AppendLine("Timestamp,Type,FilePath,Status,Duration");
                        foreach (var evt in SyncEvents)
                        {
                            csv.AppendLine($"{evt.Timestamp:g},{evt.Type},{evt.FilePath},{evt.Status},{evt.Duration.TotalSeconds:F2}s");
                        }
                        System.IO.File.WriteAllText(dialog.FileName, csv.ToString());
                    }
                    StatusMessage = $"Events exported to {dialog.FileName}";
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Export failed: {ex.Message}";
                }
            }
        }

        [RelayCommand]
        private void PauseMonitoring()
        {
            try
            {
                await _realTimeSyncService.PauseMonitoringAsync();
                StatusMessage = "Monitoring paused";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Pause failed: {ex.Message}";
            }
        }

        [RelayCommand]
        private void ResumeMonitoring()
        {
            try
            {
                await _realTimeSyncService.ResumeMonitoringAsync();
                StatusMessage = "Monitoring resumed";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Resume failed: {ex.Message}";
            }
        }
    }

    // Model classes
    public class ComparisonItem
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string RelativePath { get; set; } = string.Empty;
        public string LeftPath { get; set; } = string.Empty;
        public string RightPath { get; set; } = string.Empty;
        public ComparisonStatus Status { get; set; }
        public long LeftSize { get; set; }
        public long RightSize { get; set; }
        public DateTime LeftModified { get; set; }
        public DateTime RightModified { get; set; }
        public string LeftHash { get; set; } = string.Empty;
        public string RightHash { get; set; } = string.Empty;
        public List<ComparisonDifference> Differences { get; set; } = new();
    }

    public class ComparisonDifference
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public DifferenceType Type { get; set; }
        public string Description { get; set; } = string.Empty;
        public string LeftValue { get; set; } = string.Empty;
        public string RightValue { get; set; } = string.Empty;
        public int LineNumber { get; set; }
        public int Column { get; set; }
        public int Length { get; set; }
    }

    public class ComparisonOptions
    {
        public string LeftPath { get; set; } = string.Empty;
        public string RightPath { get; set; } = string.Empty;
        public ComparisonMethod Method { get; set; }
        public bool IncludeSubfolders { get; set; }
        public bool IgnoreWhitespace { get; set; }
        public bool IgnoreCase { get; set; }
        public string[] IncludePatterns { get; set; } = Array.Empty<string>();
        public string[] ExcludePatterns { get; set; } = Array.Empty<string>();
    }

    public class ComparisonResult
    {
        public int TotalFiles { get; set; }
        public int IdenticalFiles { get; set; }
        public int DifferentFiles { get; set; }
        public int UniqueLeftFiles { get; set; }
        public int UniqueRightFiles { get; set; }
        public List<ComparisonItem> Items { get; set; } = new();
        public List<ComparisonDifference> Differences { get; set; } = new();
    }

    public class SyncProfile
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string SourcePath { get; set; } = string.Empty;
        public string DestinationPath { get; set; } = string.Empty;
        public SyncDirection Direction { get; set; }
        public ConflictResolution ConflictResolution { get; set; }
        public string[] IncludePatterns { get; set; } = Array.Empty<string>();
        public string[] ExcludePatterns { get; set; } = Array.Empty<string>();
        public DateTime CreatedAt { get; set; }
        public DateTime? LastSync { get; set; }
        public bool IsEnabled { get; set; }
        public SyncStatistics Statistics { get; set; } = new();
    }

    public class SyncConflict
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string FilePath { get; set; } = string.Empty;
        public ConflictType Type { get; set; }
        public string Description { get; set; } = string.Empty;
        public DateTime LeftModified { get; set; }
        public DateTime RightModified { get; set; }
        public long LeftSize { get; set; }
        public long RightSize { get; set; }
        public ConflictResolution Resolution { get; set; }
        public bool IsResolved { get; set; }
    }

    public class SyncOperation
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public SyncOperationType Type { get; set; }
        public string SourcePath { get; set; } = string.Empty;
        public string DestinationPath { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public OperationStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public class SyncOptions
    {
        public string ProfileId { get; set; } = string.Empty;
        public SyncDirection Direction { get; set; }
        public ConflictResolution ConflictResolution { get; set; }
        public bool PreviewMode { get; set; }
        public string[] IncludePatterns { get; set; } = Array.Empty<string>();
        public string[] ExcludePatterns { get; set; } = Array.Empty<string>();
        public bool DeleteOrphans { get; set; }
        public bool PreserveTimestamps { get; set; }
    }

    public class SyncProgress
    {
        public double Percentage { get; set; }
        public int FilesProcessed { get; set; }
        public int TotalFiles { get; set; }
        public long BytesTransferred { get; set; }
        public long TotalBytes { get; set; }
        public TimeSpan EstimatedTimeRemaining { get; set; }
        public string CurrentFile { get; set; } = string.Empty;
        public double TransferSpeed { get; set; }
    }

    public class SyncResult
    {
        public List<SyncOperation> Operations { get; set; } = new();
        public List<SyncConflict> Conflicts { get; set; } = new();
        public int SuccessfulOperations { get; set; }
        public int FailedOperations { get; set; }
        public long BytesTransferred { get; set; }
        public TimeSpan Duration { get; set; }
    }

    public class SyncEvent
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public EventType Type { get; set; }
        public string FilePath { get; set; } = string.Empty;
        public string DestinationPath { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public long FileSize { get; set; }
        public EventStatus Status { get; set; }
        public string? ErrorMessage { get; set; }
        public TimeSpan Duration { get; set; }
    }

    public class MonitoredFolder
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Path { get; set; } = string.Empty;
        public string DestinationPath { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime LastActivity { get; set; }
        public int EventsCount { get; set; }
        public FilterSettings Filters { get; set; } = new();
    }

    public class SyncStatistics
    {
        public int TotalSyncs { get; set; }
        public int SuccessfulSyncs { get; set; }
        public int FailedSyncs { get; set; }
        public long TotalBytesTransferred { get; set; }
        public TimeSpan TotalSyncTime { get; set; }
        public DateTime LastSync { get; set; }
        public double AverageSpeed { get; set; }
    }

    public class FilterSettings
    {
        public string[] IncludePatterns { get; set; } = Array.Empty<string>();
        public string[] ExcludePatterns { get; set; } = Array.Empty<string>();
        public bool IncludeHiddenFiles { get; set; }
        public bool IncludeSystemFiles { get; set; }
        public long MinFileSize { get; set; }
        public long MaxFileSize { get; set; }
    }

    // Enums
    public enum ComparisonMethod
    {
        SizeAndDate,
        Content,
        Hash,
        SizeOnly,
        DateOnly
    }

    public enum ComparisonStatus
    {
        Identical,
        Different,
        UniqueLeft,
        UniqueRight,
        Error
    }

    public enum DifferenceType
    {
        Content,
        Size,
        Date,
        Attributes,
        Permissions,
        Hash
    }

    public enum DifferenceFilter
    {
        All,
        Identical,
        Different,
        UniqueLeft,
        UniqueRight
    }

    public enum SyncDirection
    {
        Bidirectional,
        LeftToRight,
        RightToLeft,
        Mirror
    }

    public enum ConflictResolution
    {
        Prompt,
        Skip,
        KeepLeft,
        KeepRight,
        KeepNewer,
        KeepLarger,
        Merge
    }

    public enum SyncOperationType
    {
        Copy,
        Move,
        Delete,
        Update,
        Create,
        Merge
    }

    public enum ConflictType
    {
        BothModified,
        SizeMismatch,
        TypeMismatch,
        PermissionConflict,
        NameConflict
    }

    public enum EventType
    {
        All,
        FileCreated,
        FileDeleted,
        FileModified,
        FileMoved,
        FileSynced,
        FolderCreated,
        FolderDeleted,
        Error
    }

    public enum EventStatus
    {
        Pending,
        Processing,
        Completed,
        Failed,
        Skipped
    }
}
