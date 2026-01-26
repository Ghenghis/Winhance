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
    /// ViewModel for file system monitoring
    /// </summary>
    public partial class FileMonitorViewModel : ObservableObject
    {
        private readonly IFileMonitorService _fileMonitorService;
        private ObservableCollection<MonitoredPath> _monitoredPaths = new();
        private ObservableCollection<FileSystemEvent> _recentEvents = new();

        [ObservableProperty]
        private MonitoredPath? _selectedPath;

        [ObservableProperty]
        private bool _isMonitoring;

        [ObservableProperty]
        private string _newPathToMonitor = string.Empty;

        [ObservableProperty]
        private bool _includeSubdirectories = true;

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        [ObservableProperty]
        private FileSystemEvent? _selectedEvent;

        [ObservableProperty]
        private MonitorFilter _eventFilter = MonitorFilter.All;

        [ObservableProperty]
        private int _maxEventsToKeep = 1000;

        public ObservableCollection<MonitoredPath> MonitoredPaths
        {
            get => _monitoredPaths;
            set => SetProperty(ref _monitoredPaths, value);
        }

        public ObservableCollection<FileSystemEvent> RecentEvents
        {
            get => _recentEvents;
            set => SetProperty(ref _recentEvents, value);
        }

        public FileMonitorViewModel(IFileMonitorService fileMonitorService)
        {
            _fileMonitorService = fileMonitorService;
            _fileMonitorService.FileSystemEvent += OnFileSystemEvent;
            _ = LoadMonitoredPathsAsync();
        }

        private async Task LoadMonitoredPathsAsync()
        {
            try
            {
                var paths = await _fileMonitorService.GetMonitoredPathsAsync();
                MonitoredPaths.Clear();
                foreach (var path in paths)
                {
                    MonitoredPaths.Add(path);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading monitored paths: {ex.Message}";
            }
        }

        private void OnFileSystemEvent(object? sender, FileSystemEvent e)
        {
            // Filter events based on selected filter
            if (EventFilter != MonitorFilter.All && e.Type != EventFilter)
                return;

            // Add to recent events
            App.Current.Dispatcher.Invoke(() =>
            {
                RecentEvents.Insert(0, e);
                
                // Keep only the maximum number of events
                while (RecentEvents.Count > MaxEventsToKeep)
                {
                    RecentEvents.RemoveAt(RecentEvents.Count - 1);
                }
            });
        }

        [RelayCommand]
        private async Task AddMonitoredPathAsync()
        {
            if (string.IsNullOrEmpty(NewPathToMonitor)) return;

            try
            {
                var monitoredPath = new MonitoredPath
                {
                    Path = NewPathToMonitor,
                    IncludeSubdirectories = IncludeSubdirectories,
                    IsActive = true,
                    CreatedAt = DateTime.Now
                };

                await _fileMonitorService.AddMonitoredPathAsync(monitoredPath);
                MonitoredPaths.Add(monitoredPath);
                
                StatusMessage = $"Added monitoring for: {NewPathToMonitor}";
                NewPathToMonitor = string.Empty;
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error adding path: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task RemoveMonitoredPathAsync(MonitoredPath? path)
        {
            if (path == null) return;

            try
            {
                await _fileMonitorService.RemoveMonitoredPathAsync(path.Id);
                MonitoredPaths.Remove(path);
                
                StatusMessage = $"Removed monitoring for: {path.Path}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error removing path: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task ToggleMonitoringAsync(MonitoredPath? path)
        {
            if (path == null) return;

            try
            {
                path.IsActive = !path.IsActive;
                await _fileMonitorService.UpdateMonitoredPathAsync(path);
                
                StatusMessage = path.IsActive 
                    ? $"Resumed monitoring for: {path.Path}"
                    : $"Paused monitoring for: {path.Path}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error toggling monitoring: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task StartAllMonitoringAsync()
        {
            try
            {
                foreach (var path in MonitoredPaths.Where(p => !p.IsActive))
                {
                    path.IsActive = true;
                    await _fileMonitorService.UpdateMonitoredPathAsync(path);
                }
                
                IsMonitoring = true;
                StatusMessage = "Started monitoring all paths";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error starting monitoring: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task StopAllMonitoringAsync()
        {
            try
            {
                foreach (var path in MonitoredPaths.Where(p => p.IsActive))
                {
                    path.IsActive = false;
                    await _fileMonitorService.UpdateMonitoredPathAsync(path);
                }
                
                IsMonitoring = false;
                StatusMessage = "Stopped monitoring all paths";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error stopping monitoring: {ex.Message}";
            }
        }

        [RelayCommand]
        private void ClearEvents()
        {
            RecentEvents.Clear();
            StatusMessage = "Cleared all events";
        }

        [RelayCommand]
        private void ExportEvents()
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "CSV File (*.csv)|*.csv|JSON File (*.json)|*.json",
                DefaultExt = ".csv",
                FileName = $"FileSystemEvents_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    if (dialog.FileName.EndsWith(".json"))
                    {
                        var json = System.Text.Json.JsonSerializer.Serialize(RecentEvents,
                            new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                        System.IO.File.WriteAllText(dialog.FileName, json);
                    }
                    else
                    {
                        var csv = new System.Text.StringBuilder();
                        csv.AppendLine("Timestamp,Type,FullPath,OldPath,UserName,ProcessName,FileSize");
                        foreach (var evt in RecentEvents)
                        {
                            csv.AppendLine($"{evt.Timestamp:g},{evt.Type},{evt.FullPath},{evt.OldPath},{evt.UserName},{evt.ProcessName},{evt.FileSize}");
                        }
                        System.IO.File.WriteAllText(dialog.FileName, csv.ToString());
                    }
                    StatusMessage = "Events exported successfully";
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Export failed: {ex.Message}";
                }
            }
        }

        [RelayCommand]
        private void BrowsePath()
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Select Folder to Monitor"
            };

            if (dialog.ShowDialog() == true)
            {
                NewPathToMonitor = dialog.FolderName;
            }
        }

        partial void OnEventFilterChanged(MonitorFilter value)
        {
            // Filter existing events
            var filteredEvents = value == MonitorFilter.All 
                ? _fileMonitorService.GetAllEvents()
                : _fileMonitorService.GetEventsByType(value);
            
            RecentEvents.Clear();
            foreach (var evt in filteredEvents.Take(MaxEventsToKeep))
            {
                RecentEvents.Add(evt);
            }
        }
    }

    /// <summary>
    /// ViewModel for file operations logging
    /// </summary>
    public partial class FileOperationsLogViewModel : ObservableObject
    {
        private readonly IOperationsLogService _operationsLogService;
        private ObservableCollection<OperationLogEntry> _logEntries = new();

        [ObservableProperty]
        private DateTime _startDate = DateTime.Today.AddDays(-7);

        [ObservableProperty]
        private DateTime _endDate = DateTime.Now;

        [ObservableProperty]
        private OperationType _operationFilter = OperationType.All;

        [ObservableProperty]
        private string _userFilter = string.Empty;

        [ObservableProperty]
        private string _pathFilter = string.Empty;

        [ObservableProperty]
        private bool _showSuccessful = true;

        [ObservableProperty]
        private bool _showFailed = true;

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private OperationLogEntry? _selectedEntry;

        [ObservableProperty]
        private int _totalEntries;

        [ObservableProperty]
        private int _successfulOperations;

        [ObservableProperty]
        private int _failedOperations;

        public ObservableCollection<OperationLogEntry> LogEntries
        {
            get => _logEntries;
            set => SetProperty(ref _logEntries, value);
        }

        public FileOperationsLogViewModel(IOperationsLogService operationsLogService)
        {
            _operationsLogService = operationsLogService;
            _ = LoadLogEntriesAsync();
        }

        private async Task LoadLogEntriesAsync()
        {
            IsLoading = true;

            try
            {
                var filter = new OperationLogFilter
                {
                    StartDate = StartDate,
                    EndDate = EndDate,
                    OperationType = OperationFilter,
                    User = UserFilter,
                    Path = PathFilter,
                    IncludeSuccessful = ShowSuccessful,
                    IncludeFailed = ShowFailed
                };

                var entries = await _operationsLogService.GetLogEntriesAsync(filter);
                LogEntries.Clear();
                foreach (var entry in entries.OrderByDescending(e => e.Timestamp))
                {
                    LogEntries.Add(entry);
                }

                // Update statistics
                TotalEntries = LogEntries.Count;
                SuccessfulOperations = LogEntries.Count(e => e.IsSuccess);
                FailedOperations = LogEntries.Count(e => !e.IsSuccess);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to load log entries: {ex.Message}",
                    "Load Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task RefreshLogsAsync()
        {
            await LoadLogEntriesAsync();
        }

        [RelayCommand]
        private async Task ApplyFiltersAsync()
        {
            await LoadLogEntriesAsync();
        }

        [RelayCommand]
        private void ClearFilters()
        {
            StartDate = DateTime.Today.AddDays(-7);
            EndDate = DateTime.Now;
            OperationFilter = OperationType.All;
            UserFilter = string.Empty;
            PathFilter = string.Empty;
            ShowSuccessful = true;
            ShowFailed = true;
        }

        [RelayCommand]
        private async Task ExportLogsAsync()
        {
            try
            {
                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "CSV File (*.csv)|*.csv|JSON File (*.json)|*.json",
                    DefaultExt = ".csv",
                    FileName = $"OperationsLog_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
                };

                if (dialog.ShowDialog() == true)
                {
                    await _operationsLogService.ExportLogsAsync(LogEntries.ToList(), dialog.FileName);
                    System.Windows.MessageBox.Show(
                        "Logs exported successfully.",
                        "Export Complete",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to export logs: {ex.Message}",
                    "Export Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task ClearLogsAsync()
        {
            try
            {
                await _operationsLogService.ClearLogsAsync();
                LogEntries.Clear();
                TotalEntries = 0;
                SuccessfulOperations = 0;
                FailedOperations = 0;
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to clear logs: {ex.Message}",
                    "Clear Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void ViewDetails(OperationLogEntry? entry)
        {
            if (entry == null) return;

            var message = $"Operation Details\n\n" +
                         $"Type: {entry.OperationType}\n" +
                         $"Timestamp: {entry.Timestamp:g}\n" +
                         $"User: {entry.UserName}\n" +
                         $"Source: {entry.SourcePath}\n" +
                         $"Destination: {entry.DestinationPath}\n" +
                         $"Duration: {entry.Duration.TotalSeconds:F2}s\n" +
                         $"Files: {entry.FilesCount}\n" +
                         $"Bytes: {entry.BytesTransferred:N0}\n" +
                         $"Success: {entry.IsSuccess}";
            if (!string.IsNullOrEmpty(entry.ErrorMessage))
                message += $"\n\nError: {entry.ErrorMessage}";

            System.Windows.MessageBox.Show(message, "Operation Details",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
        }

        [RelayCommand]
        private async Task RetryOperationAsync(OperationLogEntry? entry)
        {
            if (entry == null || entry.IsSuccess) return;

            try
            {
                await _operationsLogService.RetryOperationAsync(entry.Id);
                await LoadLogEntriesAsync();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to retry operation: {ex.Message}",
                    "Retry Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void ViewStackTrace(OperationLogEntry? entry)
        {
            if (entry == null || string.IsNullOrEmpty(entry.ErrorDetails)) return;

            var message = $"Stack Trace\n\nOperation: {entry.OperationType}\n" +
                         $"File: {entry.SourcePath}\n" +
                         $"Error: {entry.ErrorMessage}\n\n" +
                         $"Details:\n{entry.ErrorDetails}";

            System.Windows.MessageBox.Show(message, "Error Stack Trace",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// ViewModel for performance monitoring
    /// </summary>
    public partial class PerformanceMonitorViewModel : ObservableObject
    {
        private readonly IPerformanceMonitorService _performanceService;
        private ObservableCollection<PerformanceMetric> _metrics = new();
        private ObservableCollection<PerformanceAlert> _alerts = new();

        [ObservableProperty]
        private bool _isMonitoring;

        [ObservableProperty]
        private TimeSpan _updateInterval = TimeSpan.FromSeconds(5);

        [ObservableProperty]
        private PerformanceMetric? _selectedMetric;

        [ObservableProperty]
        private double _cpuUsage;

        [ObservableProperty]
        private long _memoryUsage;

        [ObservableProperty]
        private double _diskReadRate;

        [ObservableProperty]
        private double _diskWriteRate;

        [ObservableProperty]
        private int _activeOperations;

        [ObservableProperty]
        private DateTime _lastUpdate;

        public ObservableCollection<PerformanceMetric> Metrics
        {
            get => _metrics;
            set => SetProperty(ref _metrics, value);
        }

        public ObservableCollection<PerformanceAlert> Alerts
        {
            get => _alerts;
            set => SetProperty(ref _alerts, value);
        }

        public PerformanceMonitorViewModel(IPerformanceMonitorService performanceService)
        {
            _performanceService = performanceService;
            _performanceService.PerformanceUpdated += OnPerformanceUpdated;
            _performanceService.AlertRaised += OnAlertRaised;
        }

        private void OnPerformanceUpdated(object? sender, PerformanceMetrics e)
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                CpuUsage = e.CpuUsage;
                MemoryUsage = e.MemoryUsage;
                DiskReadRate = e.DiskReadRate;
                DiskWriteRate = e.DiskWriteRate;
                ActiveOperations = e.ActiveOperations;
                LastUpdate = DateTime.Now;

                // Update metrics collection
                var metric = new PerformanceMetric
                {
                    Timestamp = DateTime.Now,
                    CpuUsage = e.CpuUsage,
                    MemoryUsage = e.MemoryUsage,
                    DiskReadRate = e.DiskReadRate,
                    DiskWriteRate = e.DiskWriteRate,
                    ActiveOperations = e.ActiveOperations
                };

                Metrics.Insert(0, metric);
                
                // Keep only last 100 metrics
                while (Metrics.Count > 100)
                {
                    Metrics.RemoveAt(Metrics.Count - 1);
                }
            });
        }

        private void OnAlertRaised(object? sender, PerformanceAlert e)
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                Alerts.Insert(0, e);
                
                // Keep only last 50 alerts
                while (Alerts.Count > 50)
                {
                    Alerts.RemoveAt(Alerts.Count - 1);
                }
            });
        }

        [RelayCommand]
        private async Task StartMonitoringAsync()
        {
            try
            {
                await _performanceService.StartMonitoringAsync(UpdateInterval);
                IsMonitoring = true;
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to start monitoring: {ex.Message}",
                    "Start Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task StopMonitoringAsync()
        {
            try
            {
                await _performanceService.StopMonitoringAsync();
                IsMonitoring = false;
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to stop monitoring: {ex.Message}",
                    "Stop Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void ClearMetrics()
        {
            Metrics.Clear();
        }

        [RelayCommand]
        private void ClearAlerts()
        {
            Alerts.Clear();
        }

        [RelayCommand]
        private void ExportMetrics()
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "CSV File (*.csv)|*.csv",
                DefaultExt = ".csv",
                FileName = $"PerformanceMetrics_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var csv = new System.Text.StringBuilder();
                    csv.AppendLine("Timestamp,CPU%,Memory(MB),DiskRead(MB/s),DiskWrite(MB/s),ActiveOps");
                    foreach (var metric in Metrics)
                    {
                        csv.AppendLine($"{metric.Timestamp:g},{metric.CpuUsage:F2},{metric.MemoryUsage / (1024.0 * 1024.0):F2}," +
                                      $"{metric.DiskReadRate:F2},{metric.DiskWriteRate:F2},{metric.ActiveOperations}");
                    }
                    System.IO.File.WriteAllText(dialog.FileName, csv.ToString());
                    System.Windows.MessageBox.Show(
                        "Metrics exported successfully.",
                        "Export Complete",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show(
                        $"Export failed: {ex.Message}",
                        "Export Error",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Error);
                }
            }
        }

        [RelayCommand]
        private void ConfigureAlerts()
        {
            var message = "Alert Configuration\n\n" +
                         "Configure thresholds for performance alerts:\n\n" +
                         "• High CPU Usage (%)\n" +
                         "• High Memory Usage (MB)\n" +
                         "• Low Disk Space (GB)\n" +
                         "• High Disk Activity (MB/s)\n" +
                         "• Too Many Operations (count)\n" +
                         "• Slow Operation (seconds)";

            System.Windows.MessageBox.Show(message, "Alert Configuration",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
        }

        [RelayCommand]
        private void AcknowledgeAlert(PerformanceAlert? alert)
        {
            if (alert == null) return;

            alert.IsAcknowledged = true;
            alert.AcknowledgedAt = DateTime.Now;
        }
    }

    // Model classes
    public class MonitoredPath
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Path { get; set; } = string.Empty;
        public bool IncludeSubdirectories { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastActivity { get; set; }
        public long EventsCount { get; set; }
        public WatcherChangeTypes NotifyFilter { get; set; } = WatcherChangeTypes.All;
    }

    public class FileSystemEvent
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public DateTime Timestamp { get; set; }
        public MonitorFilter Type { get; set; }
        public string FullPath { get; set; } = string.Empty;
        public string? OldPath { get; set; } // For rename operations
        public string? SourcePath { get; set; } // For copy/move operations
        public string? DestinationPath { get; set; } // For copy/move operations
        public string UserName { get; set; } = string.Empty;
        public string ProcessName { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public FileAttributes Attributes { get; set; }
    }

    public class OperationLogEntry
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public DateTime Timestamp { get; set; }
        public OperationType OperationType { get; set; }
        public string SourcePath { get; set; } = string.Empty;
        public string? DestinationPath { get; set; }
        public string UserName { get; set; } = string.Empty;
        public bool IsSuccess { get; set; }
        public string? ErrorMessage { get; set; }
        public string? ErrorDetails { get; set; }
        public TimeSpan Duration { get; set; }
        public long BytesTransferred { get; set; }
        public int FilesCount { get; set; }
        public string? ProcessId { get; set; }
        public string? MachineName { get; set; }
    }

    public class OperationLogFilter
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public OperationType OperationType { get; set; }
        public string User { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public bool IncludeSuccessful { get; set; }
        public bool IncludeFailed { get; set; }
    }

    public class PerformanceMetric
    {
        public DateTime Timestamp { get; set; }
        public double CpuUsage { get; set; }
        public long MemoryUsage { get; set; }
        public double DiskReadRate { get; set; }
        public double DiskWriteRate { get; set; }
        public int ActiveOperations { get; set; }
        public int ThreadCount { get; set; }
        public double NetworkUsage { get; set; }
    }

    public class PerformanceMetrics
    {
        public double CpuUsage { get; set; }
        public long MemoryUsage { get; set; }
        public double DiskReadRate { get; set; }
        public double DiskWriteRate { get; set; }
        public int ActiveOperations { get; set; }
        public int ThreadCount { get; set; }
        public double NetworkUsage { get; set; }
    }

    public class PerformanceAlert
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public DateTime Timestamp { get; set; }
        public AlertType Type { get; set; }
        public string Message { get; set; } = string.Empty;
        public double Threshold { get; set; }
        public double ActualValue { get; set; }
        public bool IsAcknowledged { get; set; }
        public DateTime? AcknowledgedAt { get; set; }
        public string? AcknowledgedBy { get; set; }
        public AlertSeverity Severity { get; set; }
    }

    // Enums
    public enum MonitorFilter
    {
        All,
        Created,
        Deleted,
        Changed,
        Renamed,
        Copied,
        Moved
    }

    public enum OperationType
    {
        All,
        Copy,
        Move,
        Delete,
        Create,
        Rename,
        Compress,
        Decompress,
        Encrypt,
        Decrypt
    }

    public enum AlertType
    {
        HighCpuUsage,
        HighMemoryUsage,
        LowDiskSpace,
        HighDiskActivity,
        TooManyOperations,
        SlowOperation
    }

    public enum AlertSeverity
    {
        Info,
        Warning,
        Error,
        Critical
    }
}
