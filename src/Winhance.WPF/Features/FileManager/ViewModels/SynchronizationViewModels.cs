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
    /// ViewModel for folder synchronization
    /// </summary>
    public partial class FolderSyncViewModel : ObservableObject
    {
        private readonly ISyncService _syncService;
        private ObservableCollection<SyncConflict> _conflicts = new();
        private ObservableCollection<SyncPreviewItem> _previewItems = new();

        [ObservableProperty]
        private string _sourcePath = string.Empty;

        [ObservableProperty]
        private string _destinationPath = string.Empty;

        [ObservableProperty]
        private SyncMode _syncMode = SyncMode.TwoWay;

        [ObservableProperty]
        private bool _isSyncing;

        [ObservableProperty]
        private string? _syncStatus;

        [ObservableProperty]
        private double _syncProgress;

        [ObservableProperty]
        private SyncOptions _options = new();

        public ObservableCollection<SyncConflict> Conflicts
        {
            get => _conflicts;
            set => SetProperty(ref _conflicts, value);
        }

        public ObservableCollection<SyncPreviewItem> PreviewItems
        {
            get => _previewItems;
            set => SetProperty(ref _previewItems, value);
        }

        public FolderSyncViewModel(ISyncService syncService)
        {
            _syncService = syncService;
        }

        [RelayCommand]
        private async Task PreviewSyncAsync()
        {
            if (string.IsNullOrEmpty(SourcePath) || string.IsNullOrEmpty(DestinationPath)) return;

            SyncStatus = "Analyzing folders...";

            try
            {
                var preview = await _syncService.PreviewSyncAsync(SourcePath, DestinationPath, SyncMode, Options);
                
                PreviewItems.Clear();
                foreach (var item in preview.Items)
                {
                    PreviewItems.Add(item);
                }

                Conflicts.Clear();
                foreach (var conflict in preview.Conflicts)
                {
                    Conflicts.Add(conflict);
                }

                SyncStatus = $"Preview: {preview.Items.Count} changes, {preview.Conflicts.Count} conflicts";
            }
            catch (Exception ex)
            {
                SyncStatus = $"Preview failed: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task StartSyncAsync()
        {
            if (string.IsNullOrEmpty(SourcePath) || string.IsNullOrEmpty(DestinationPath)) return;

            IsSyncing = true;
            SyncStatus = "Synchronizing...";
            SyncProgress = 0;

            try
            {
                var progress = new Progress<SyncProgress>(p =>
                {
                    SyncProgress = p.Percentage;
                    SyncStatus = $"{p.Status} ({p.ProcessedFiles}/{p.TotalFiles})";
                });

                var result = await _syncService.SynchronizeAsync(
                    SourcePath,
                    DestinationPath,
                    SyncMode,
                    Options,
                    progress);

                SyncStatus = $"Sync completed: {result.SynchronizedFiles} files, {result.Errors} errors";
            }
            catch (Exception ex)
            {
                SyncStatus = $"Sync failed: {ex.Message}";
            }
            finally
            {
                IsSyncing = false;
                SyncProgress = 0;
            }
        }

        [RelayCommand]
        private async void CancelSync()
        {
            try
            {
                await _syncService.CancelSyncAsync();
                SyncStatus = "Synchronization cancelled by user";
                IsSyncing = false;
                SyncProgress = 0;
            }
            catch (Exception ex)
            {
                SyncStatus = $"Cancel failed: {ex.Message}";
            }
        }

        [RelayCommand]
        private void ResolveConflict(SyncConflict? conflict, ConflictResolution resolution)
        {
            if (conflict == null) return;

            conflict.Resolution = resolution;
            Conflicts.Remove(conflict);
        }

        [RelayCommand]
        private void ResolveAllConflicts(ConflictResolution resolution)
        {
            foreach (var conflict in Conflicts.ToList())
            {
                conflict.Resolution = resolution;
                Conflicts.Remove(conflict);
            }
        }

        [RelayCommand]
        private void BrowseSourceFolder()
        {
            try
            {
                var dialog = new Microsoft.Win32.OpenFolderDialog
                {
                    Title = "Select Source Folder",
                    InitialDirectory = string.IsNullOrEmpty(SourcePath) 
                        ? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
                        : SourcePath
                };

                if (dialog.ShowDialog() == true)
                {
                    SourcePath = dialog.FolderName;
                    SyncStatus = $"Source folder selected: {SourcePath}";
                }
            }
            catch (Exception ex)
            {
                SyncStatus = $"Failed to browse: {ex.Message}";
            }
        }

        [RelayCommand]
        private void BrowseDestinationFolder()
        {
            try
            {
                var dialog = new Microsoft.Win32.OpenFolderDialog
                {
                    Title = "Select Destination Folder",
                    InitialDirectory = string.IsNullOrEmpty(DestinationPath) 
                        ? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
                        : DestinationPath
                };

                if (dialog.ShowDialog() == true)
                {
                    DestinationPath = dialog.FolderName;
                    SyncStatus = $"Destination folder selected: {DestinationPath}";
                }
            }
            catch (Exception ex)
            {
                SyncStatus = $"Failed to browse: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task SaveProfileAsync(string profileName)
        {
            try
            {
                var profile = new SyncProfile
                {
                    Name = profileName,
                    SourcePath = SourcePath,
                    DestinationPath = DestinationPath,
                    Mode = SyncMode,
                    Options = Options
                };

                await _syncService.SaveSyncProfileAsync(profile);
                SyncStatus = $"Profile saved: {profileName}";
            }
            catch (Exception ex)
            {
                SyncStatus = $"Failed to save profile: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task LoadProfileAsync(SyncProfile? profile)
        {
            if (profile == null) return;

            SourcePath = profile.SourcePath;
            DestinationPath = profile.DestinationPath;
            SyncMode = profile.Mode;
            Options = profile.Options;
        }
    }

    /// <summary>
    /// ViewModel for real-time sync
    /// </summary>
    public partial class RealTimeSyncViewModel : ObservableObject
    {
        private readonly ISyncService _syncService;
        private ObservableCollection<MonitoredFolder> _monitoredFolders = new();
        private ObservableCollection<SyncEvent> _recentEvents = new();

        [ObservableProperty]
        private bool _isRealTimeSyncEnabled;

        [ObservableProperty]
        private int _syncDelaySeconds = 5;

        [ObservableProperty]
        private string? _syncStatus;

        public ObservableCollection<MonitoredFolder> MonitoredFolders
        {
            get => _monitoredFolders;
            set => SetProperty(ref _monitoredFolders, value);
        }

        public ObservableCollection<SyncEvent> RecentEvents
        {
            get => _recentEvents;
            set => SetProperty(ref _recentEvents, value);
        }

        public RealTimeSyncViewModel(ISyncService syncService)
        {
            _syncService = syncService;
        }

        [RelayCommand]
        private async Task AddMonitoredFolderAsync()
        {
            var sourceDialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Select Source Folder to Monitor"
            };

            if (sourceDialog.ShowDialog() != true) return;
            var sourcePath = sourceDialog.FolderName;

            var destDialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Select Destination Folder"
            };

            if (destDialog.ShowDialog() != true) return;
            var destinationPath = destDialog.FolderName;

            if (!string.IsNullOrEmpty(sourcePath) && !string.IsNullOrEmpty(destinationPath))
            {
                var folder = new MonitoredFolder
                {
                    SourcePath = sourcePath,
                    DestinationPath = destinationPath,
                    IsEnabled = true,
                    SyncMode = SyncMode.Mirror
                };

                MonitoredFolders.Add(folder);

                if (IsRealTimeSyncEnabled)
                {
                    await _syncService.StartMonitoringAsync(folder);
                }

                SyncStatus = $"Added monitoring for {sourcePath}";
            }
        }

        [RelayCommand]
        private async Task RemoveMonitoredFolderAsync(MonitoredFolder? folder)
        {
            if (folder == null) return;

            if (IsRealTimeSyncEnabled)
            {
                await _syncService.StopMonitoringAsync(folder);
            }

            MonitoredFolders.Remove(folder);
            SyncStatus = $"Removed monitoring for {folder.SourcePath}";
        }

        [RelayCommand]
        private async Task ToggleRealTimeSyncAsync()
        {
            IsRealTimeSyncEnabled = !IsRealTimeSyncEnabled;

            if (IsRealTimeSyncEnabled)
            {
                foreach (var folder in MonitoredFolders.Where(f => f.IsEnabled))
                {
                    await _syncService.StartMonitoringAsync(folder);
                }
                SyncStatus = "Real-time sync enabled";
            }
            else
            {
                foreach (var folder in MonitoredFolders)
                {
                    await _syncService.StopMonitoringAsync(folder);
                }
                SyncStatus = "Real-time sync disabled";
            }
        }

        [RelayCommand]
        private void ClearEventHistory()
        {
            RecentEvents.Clear();
        }

        [RelayCommand]
        private async Task SyncNowAsync(MonitoredFolder? folder)
        {
            if (folder == null) return;

            try
            {
                SyncStatus = $"Syncing {folder.SourcePath}...";
                await _syncService.SynchronizeAsync(
                    folder.SourcePath,
                    folder.DestinationPath,
                    folder.SyncMode,
                    new SyncOptions());

                SyncStatus = "Sync completed";
            }
            catch (Exception ex)
            {
                SyncStatus = $"Sync failed: {ex.Message}";
            }
        }

        private void OnSyncEvent(SyncEvent syncEvent)
        {
            RecentEvents.Insert(0, syncEvent);
            
            // Keep only last 100 events
            while (RecentEvents.Count > 100)
            {
                RecentEvents.RemoveAt(RecentEvents.Count - 1);
            }
        }
    }

    /// <summary>
    /// ViewModel for scheduled sync
    /// </summary>
    public partial class ScheduledSyncViewModel : ObservableObject
    {
        private readonly ISyncService _syncService;
        private ObservableCollection<SyncTask> _scheduledTasks = new();

        [ObservableProperty]
        private string? _taskStatus;

        public ObservableCollection<SyncTask> ScheduledTasks
        {
            get => _scheduledTasks;
            set => SetProperty(ref _scheduledTasks, value);
        }

        public ScheduledSyncViewModel(ISyncService syncService)
        {
            _syncService = syncService;
        }

        [RelayCommand]
        private async Task CreateScheduledTaskAsync()
        {
            var sourceDialog = new Microsoft.Win32.OpenFolderDialog { Title = "Select Source Folder" };
            if (sourceDialog.ShowDialog() != true) return;

            var destDialog = new Microsoft.Win32.OpenFolderDialog { Title = "Select Destination Folder" };
            if (destDialog.ShowDialog() != true) return;

            var task = new SyncTask
            {
                Name = $"Sync_{System.IO.Path.GetFileName(sourceDialog.FolderName)}",
                SourcePath = sourceDialog.FolderName,
                DestinationPath = destDialog.FolderName,
                Schedule = new SyncSchedule
                {
                    Type = ScheduleType.Daily,
                    Time = new TimeSpan(2, 0, 0) // 2:00 AM
                },
                IsEnabled = true
            };

            ScheduledTasks.Add(task);
            await _syncService.SaveScheduledTaskAsync(task);
            TaskStatus = $"Created task: {task.Name}";
        }

        [RelayCommand]
        private async Task EditTaskAsync(SyncTask? task)
        {
            if (task == null) return;

            var message = $"Edit Task: {task.Name}\n\n" +
                         $"Source: {task.SourcePath}\n" +
                         $"Destination: {task.DestinationPath}\n" +
                         $"Schedule: {task.Schedule.Type}\n" +
                         $"Enabled: {task.IsEnabled}\n\n" +
                         $"Use the scheduled tasks interface to modify settings.";
            
            System.Windows.MessageBox.Show(message, "Task Details",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            
            await _syncService.SaveScheduledTaskAsync(task);
            TaskStatus = $"Updated task: {task.Name}";
        }

        [RelayCommand]
        private async Task DeleteTaskAsync(SyncTask? task)
        {
            if (task == null) return;

            await _syncService.DeleteScheduledTaskAsync(task.Id);
            ScheduledTasks.Remove(task);
            TaskStatus = $"Deleted task: {task.Name}";
        }

        [RelayCommand]
        private async Task ToggleTaskAsync(SyncTask? task)
        {
            if (task == null) return;

            task.IsEnabled = !task.IsEnabled;
            await _syncService.SaveScheduledTaskAsync(task);
            TaskStatus = task.IsEnabled ? $"Enabled task: {task.Name}" : $"Disabled task: {task.Name}";
        }

        [RelayCommand]
        private async Task RunTaskNowAsync(SyncTask? task)
        {
            if (task == null) return;

            try
            {
                TaskStatus = $"Running task: {task.Name}";
                await _syncService.RunScheduledTaskAsync(task.Id);
                TaskStatus = $"Task completed: {task.Name}";
            }
            catch (Exception ex)
            {
                TaskStatus = $"Task failed: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task LoadTasksAsync()
        {
            try
            {
                var tasks = await _syncService.GetScheduledTasksAsync();
                ScheduledTasks.Clear();
                foreach (var task in tasks)
                {
                    ScheduledTasks.Add(task);
                }
            }
            catch (Exception ex)
            {
                TaskStatus = $"Failed to load tasks: {ex.Message}";
            }
        }
    }

    // Model classes
    public class SyncOptions
    {
        public bool IncludeSubfolders { get; set; } = true;
        public bool IncludeHiddenFiles { get; set; } = false;
        public bool IncludeSystemFiles { get; set; } = false;
        public bool PreserveTimestamps { get; set; } = true;
        public bool PreservePermissions { get; set; } = true;
        public bool DeleteExtraFiles { get; set; } = false;
        public bool UseCompression { get; set; } = false;
        public int BandwidthLimit { get; set; } = 0; // 0 = unlimited
        public string[] ExcludePatterns { get; set; } = Array.Empty<string>();
        public string[] IncludePatterns { get; set; } = Array.Empty<string>();
    }

    public class SyncPreviewItem
    {
        public string RelativePath { get; set; } = string.Empty;
        public SyncAction Action { get; set; }
        public long SourceSize { get; set; }
        public long DestinationSize { get; set; }
        public DateTime SourceModified { get; set; }
        public DateTime DestinationModified { get; set; }
        public bool IsSelected { get; set; } = true;
    }

    public class SyncConflict
    {
        public string RelativePath { get; set; } = string.Empty;
        public ConflictType Type { get; set; }
        public string SourcePath { get; set; } = string.Empty;
        public string DestinationPath { get; set; } = string.Empty;
        public DateTime SourceModified { get; set; }
        public DateTime DestinationModified { get; set; }
        public ConflictResolution Resolution { get; set; }
    }

    public class MonitoredFolder
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string SourcePath { get; set; } = string.Empty;
        public string DestinationPath { get; set; } = string.Empty;
        public SyncMode SyncMode { get; set; }
        public bool IsEnabled { get; set; }
        public SyncOptions Options { get; set; } = new();
    }

    public class SyncEvent
    {
        public DateTime Timestamp { get; set; }
        public string EventType { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsSuccess { get; set; }
    }

    public class SyncTask
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string SourcePath { get; set; } = string.Empty;
        public string DestinationPath { get; set; } = string.Empty;
        public SyncMode SyncMode { get; set; }
        public SyncSchedule Schedule { get; set; } = new();
        public SyncOptions Options { get; set; } = new();
        public bool IsEnabled { get; set; }
        public DateTime? LastRun { get; set; }
        public DateTime? NextRun { get; set; }
        public TaskStatus Status { get; set; }
    }

    public class SyncSchedule
    {
        public ScheduleType Type { get; set; }
        public TimeSpan Time { get; set; }
        public DayOfWeek[] DaysOfWeek { get; set; } = Array.Empty<DayOfWeek>();
        public int DayOfMonth { get; set; } = 1;
        public int IntervalMinutes { get; set; } = 60;
    }

    public enum SyncMode
    {
        TwoWay,
        Mirror,
        Update,
        Contribute
    }

    public enum SyncAction
    {
        Copy,
        Update,
        Delete,
        None
    }

    public enum ConflictType
    {
        ModifiedBoth,
        NewerInSource,
        NewerInDestination,
        TypeMismatch
    }

    public enum ConflictResolution
    {
        KeepSource,
        KeepDestination,
        KeepBoth,
        Skip
    }

    public enum ScheduleType
    {
        Once,
        Daily,
        Weekly,
        Monthly,
        Interval
    }

    public enum TaskStatus
    {
        Pending,
        Running,
        Completed,
        Failed,
        Cancelled
    }
}
