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
    /// ViewModel for disk tools
    /// </summary>
    public partial class DiskToolsViewModel : ObservableObject
    {
        private readonly IDiskToolsService _diskToolsService;
        private ObservableCollection<DiskDrive> _diskDrives = new();
        private ObservableCollection<DiskPartition> _partitions = new();

        [ObservableProperty]
        private DiskDrive? _selectedDrive;

        [ObservableProperty]
        private DiskPartition? _selectedPartition;

        [ObservableProperty]
        private bool _isScanning;

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        [ObservableProperty]
        private double _scanProgress;

        [ObservableProperty]
        private string _currentOperation = string.Empty;

        [ObservableProperty]
        private bool _quickFormat = true;

        [ObservableProperty]
        private string _volumeLabel = string.Empty;

        [ObservableProperty]
        private string _fileSystem = "NTFS";

        [ObservableProperty]
        private long _clusterSize = 4096;

        [ObservableProperty]
        private bool _enableCompression = false;

        public ObservableCollection<DiskDrive> DiskDrives
        {
            get => _diskDrives;
            set => SetProperty(ref _diskDrives, value);
        }

        public ObservableCollection<DiskPartition> Partitions
        {
            get => _partitions;
            set => SetProperty(ref _partitions, value);
        }

        public DiskToolsViewModel(IDiskToolsService diskToolsService)
        {
            _diskToolsService = diskToolsService;
            _ = LoadDiskDrivesAsync();
        }

        private async Task LoadDiskDrivesAsync()
        {
            try
            {
                var drives = await _diskToolsService.GetDiskDrivesAsync();
                DiskDrives.Clear();
                foreach (var drive in drives)
                {
                    DiskDrives.Add(drive);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading drives: {ex.Message}";
            }
        }

        partial void OnSelectedDriveChanged(DiskDrive? value)
        {
            if (value != null)
            {
                _ = LoadPartitionsAsync(value.Id);
            }
        }

        private async Task LoadPartitionsAsync(string driveId)
        {
            try
            {
                var partitions = await _diskToolsService.GetPartitionsAsync(driveId);
                Partitions.Clear();
                foreach (var partition in partitions)
                {
                    Partitions.Add(partition);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading partitions: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task RefreshDrivesAsync()
        {
            await LoadDiskDrivesAsync();
            StatusMessage = "Drives refreshed";
        }

        [RelayCommand]
        private async Task AnalyzeDiskAsync(DiskDrive? drive)
        {
            if (drive == null) return;

            IsScanning = true;
            StatusMessage = "Analyzing disk...";
            CurrentOperation = "Initializing scan";

            try
            {
                var progress = new Progress<DiskOperationProgress>(p =>
                {
                    ScanProgress = p.Percentage;
                    CurrentOperation = p.CurrentOperation;
                    StatusMessage = $"{p.CurrentOperation}... {p.Percentage:F1}%";
                });

                var result = await _diskToolsService.AnalyzeDiskAsync(drive.Id, progress);
                StatusMessage = $"Analysis complete. Health: {result.HealthStatus}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Analysis failed: {ex.Message}";
            }
            finally
            {
                IsScanning = false;
            }
        }

        [RelayCommand]
        private async Task DefragmentDiskAsync(DiskPartition? partition)
        {
            if (partition == null) return;

            IsScanning = true;
            StatusMessage = "Defragmenting...";

            try
            {
                var progress = new Progress<DiskOperationProgress>(p =>
                {
                    ScanProgress = p.Percentage;
                    CurrentOperation = p.CurrentOperation;
                });

                await _diskToolsService.DefragmentPartitionAsync(partition.Id, progress);
                StatusMessage = "Defragmentation complete";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Defragmentation failed: {ex.Message}";
            }
            finally
            {
                IsScanning = false;
            }
        }

        [RelayCommand]
        private async Task CheckDiskAsync(DiskPartition? partition)
        {
            if (partition == null) return;

            IsScanning = true;
            StatusMessage = "Checking disk...";

            try
            {
                var options = new CheckDiskOptions
                {
                    FixErrors = true,
                    RecoverBadSectors = true,
                    ForceDismount = false
                };

                var progress = new Progress<DiskOperationProgress>(p =>
                {
                    ScanProgress = p.Percentage;
                    CurrentOperation = p.CurrentOperation;
                });

                var result = await _diskToolsService.CheckDiskAsync(partition.Id, options, progress);
                StatusMessage = $"Check complete. Errors found: {result.ErrorsFound}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Disk check failed: {ex.Message}";
            }
            finally
            {
                IsScanning = false;
            }
        }

        [RelayCommand]
        private async Task FormatPartitionAsync(DiskPartition? partition)
        {
            if (partition == null) return;

            try
            {
                var options = new FormatOptions
                {
                    VolumeLabel = VolumeLabel,
                    FileSystem = FileSystem,
                    ClusterSize = ClusterSize,
                    QuickFormat = QuickFormat,
                    EnableCompression = EnableCompression
                };

                await _diskToolsService.FormatPartitionAsync(partition.Id, options);
                StatusMessage = "Partition formatted successfully";
                await LoadPartitionsAsync(SelectedDrive?.Id ?? string.Empty);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Format failed: {ex.Message}";
            }
        }

        [RelayCommand]
        private void CleanDrive()
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "cleanmgr.exe",
                    Arguments = $"/d {SelectedDrive?.Id?.Substring(0, 1) ?? "C"}",
                    UseShellExecute = true
                };
                System.Diagnostics.Process.Start(psi);
                StatusMessage = "Disk Cleanup utility launched";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Failed to launch Disk Cleanup: {ex.Message}";
            }
        }

        [RelayCommand]
        private void OptimizeDrive()
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "dfrgui.exe",
                    UseShellExecute = true
                };
                System.Diagnostics.Process.Start(psi);
                StatusMessage = "Optimize Drives utility launched";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Failed to launch Optimize Drives: {ex.Message}";
            }
        }

        [RelayCommand]
        private void ManageBitlocker(DiskPartition? partition)
        {
            if (partition == null) return;

            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "control.exe",
                    Arguments = "/name Microsoft.BitLockerDriveEncryption",
                    UseShellExecute = true
                };
                System.Diagnostics.Process.Start(psi);
                StatusMessage = $"BitLocker management opened for {partition.Letter}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Failed to open BitLocker: {ex.Message}";
            }
        }

        [RelayCommand]
        private void ViewDriveProperties(DiskDrive? drive)
        {
            if (drive == null) return;

            try
            {
                var message = $"Drive: {drive.Model}\n" +
                             $"Serial: {drive.SerialNumber}\n" +
                             $"Interface: {drive.Interface}\n" +
                             $"Size: {FormatSize(drive.TotalSize)}\n" +
                             $"Type: {drive.Type}\n" +
                             $"Status: {drive.Status}\n" +
                             $"Firmware: {drive.Firmware}\n" +
                             $"Temperature: {drive.Temperature}°C\n" +
                             $"Power On Hours: {drive.PowerOnHours}\n" +
                             $"Health: {drive.HealthStatus}";
                
                System.Windows.MessageBox.Show(message, "Drive Properties", 
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error showing properties: {ex.Message}";
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

        [RelayCommand]
        private async void EjectDrive(DiskDrive? drive)
        {
            if (drive == null || !drive.IsRemovable) return;

            try
            {
                StatusMessage = $"Ejecting {drive.Model}...";
                await _diskToolsService.EjectDriveAsync(drive.Id);
                StatusMessage = $"{drive.Model} ejected safely";
                await LoadDiskDrivesAsync();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Failed to eject drive: {ex.Message}";
            }
        }

        [RelayCommand]
        private async void UpdateFirmware(DiskDrive? drive)
        {
            if (drive == null) return;

            try
            {
                StatusMessage = $"Checking firmware for {drive.Model}...";
                var updateAvailable = await _diskToolsService.CheckFirmwareUpdateAsync(drive.Id);
                
                if (updateAvailable)
                {
                    var result = System.Windows.MessageBox.Show(
                        $"Firmware update available for {drive.Model}.\n\nWould you like to open the manufacturer's website?",
                        "Firmware Update Available",
                        System.Windows.MessageBoxButton.YesNo,
                        System.Windows.MessageBoxImage.Question);
                    
                    if (result == System.Windows.MessageBoxResult.Yes)
                    {
                        var url = await _diskToolsService.GetFirmwareUpdateUrlAsync(drive.Id);
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = url,
                            UseShellExecute = true
                        });
                    }
                    StatusMessage = "Firmware check complete - update available";
                }
                else
                {
                    StatusMessage = "Firmware is up to date";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Firmware check failed: {ex.Message}";
            }
        }
    }

    /// <summary>
    /// ViewModel for system cleanup
    /// </summary>
    public partial class SystemCleanupViewModel : ObservableObject
    {
        private readonly ISystemCleanupService _cleanupService;
        private ObservableCollection<CleanupCategory> _cleanupCategories = new();
        private ObservableCollection<CleanupItem> _cleanupItems = new();

        [ObservableProperty]
        private CleanupCategory? _selectedCategory;

        [ObservableProperty]
        private bool _isScanning;

        [ObservableProperty]
        private bool _isCleaning;

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        [ObservableProperty]
        private double _scanProgress;

        [ObservableProperty]
        private long _totalSpaceToFree;

        [ObservableProperty]
        private int _itemsToDelete;

        [ObservableProperty]
        private string _currentScanItem = string.Empty;

        [ObservableProperty]
        private bool _selectSafeItemsOnly = true;

        public ObservableCollection<CleanupCategory> CleanupCategories
        {
            get => _cleanupCategories;
            set => SetProperty(ref _cleanupCategories, value);
        }

        public ObservableCollection<CleanupItem> CleanupItems
        {
            get => _cleanupItems;
            set => SetProperty(ref _cleanupItems, value);
        }

        public SystemCleanupViewModel(ISystemCleanupService cleanupService)
        {
            _cleanupService = cleanupService;
            _ = LoadCleanupCategoriesAsync();
        }

        private async Task LoadCleanupCategoriesAsync()
        {
            try
            {
                var categories = await _cleanupService.GetCleanupCategoriesAsync();
                CleanupCategories.Clear();
                foreach (var category in categories)
                {
                    CleanupCategories.Add(category);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading categories: {ex.Message}";
            }
        }

        partial void OnSelectedCategoryChanged(CleanupCategory? value)
        {
            if (value != null)
            {
                _ = LoadCleanupItemsAsync(value.Id);
            }
        }

        private async Task LoadCleanupItemsAsync(string categoryId)
        {
            try
            {
                var items = await _cleanupService.GetCleanupItemsAsync(categoryId);
                CleanupItems.Clear();
                foreach (var item in items)
                {
                    item.IsSelected = SelectSafeItemsOnly && item.RiskLevel == RiskLevel.Safe;
                    CleanupItems.Add(item);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading items: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task ScanForCleanupAsync()
        {
            IsScanning = true;
            StatusMessage = "Scanning for cleanup items...";
            ScanProgress = 0;
            TotalSpaceToFree = 0;
            ItemsToDelete = 0;

            try
            {
                var progress = new Progress<CleanupScanProgress>(p =>
                {
                    ScanProgress = p.Percentage;
                    CurrentScanItem = p.CurrentItem;
                    TotalSpaceToFree = p.SpaceToFree;
                    ItemsToDelete = p.ItemCount;
                    StatusMessage = $"Scanning {p.CurrentItem}... {p.Percentage:F1}%";
                });

                await _cleanupService.ScanForCleanupAsync(progress);
                StatusMessage = $"Scan complete. Can free {FormatFileSize(TotalSpaceToFree)} from {ItemsToDelete} items";
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
        private async Task StartCleanupAsync()
        {
            var selectedItems = CleanupItems.Where(i => i.IsSelected).ToList();
            if (!selectedItems.Any())
            {
                StatusMessage = "No items selected for cleanup";
                return;
            }

            IsCleaning = true;
            StatusMessage = "Cleaning up...";

            try
            {
                var progress = new Progress<CleanupProgress>(p =>
                {
                    ScanProgress = p.Percentage;
                    CurrentScanItem = p.CurrentItem;
                    StatusMessage = $"Cleaning {p.CurrentItem}... {p.Percentage:F1}%";
                });

                var result = await _cleanupService.CleanupItemsAsync(selectedItems, progress);
                StatusMessage = $"Cleanup complete. Freed {FormatFileSize(result.SpaceFreed)}";
                
                // Remove cleaned items
                foreach (var item in result.CleanedItems)
                {
                    CleanupItems.Remove(item);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Cleanup failed: {ex.Message}";
            }
            finally
            {
                IsCleaning = false;
            }
        }

        [RelayCommand]
        private void SelectAllItems()
        {
            foreach (var item in CleanupItems)
            {
                item.IsSelected = true;
            }
        }

        [RelayCommand]
        private void DeselectAllItems()
        {
            foreach (var item in CleanupItems)
            {
                item.IsSelected = false;
            }
        }

        [RelayCommand]
        private void SelectSafeItems()
        {
            foreach (var item in CleanupItems)
            {
                item.IsSelected = item.RiskLevel == RiskLevel.Safe;
            }
        }

        [RelayCommand]
        private void SelectByRiskLevel(RiskLevel riskLevel)
        {
            foreach (var item in CleanupItems)
            {
                item.IsSelected = item.RiskLevel == riskLevel;
            }
        }

        [RelayCommand]
        private void ViewItemDetails(CleanupItem? item)
        {
            if (item == null) return;

            try
            {
                var details = $"Name: {item.Name}\n" +
                             $"Description: {item.Description}\n" +
                             $"Path: {item.Path}\n" +
                             $"Size: {FormatFileSize(item.Size)}\n" +
                             $"Files: {item.FilePaths.Count}\n" +
                             $"Last Accessed: {item.LastAccessed:g}\n" +
                             $"Created: {item.Created:g}\n" +
                             $"Risk Level: {item.RiskLevel}";
                
                System.Windows.MessageBox.Show(details, "Cleanup Item Details",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error showing details: {ex.Message}";
            }
        }

        [RelayCommand]
        private void ExcludeItem(CleanupItem? item)
        {
            if (item == null) return;
            CleanupItems.Remove(item);
        }

        [RelayCommand]
        private async void ConfigureCleanup()
        {
            try
            {
                var config = await _cleanupService.GetCleanupConfigurationAsync();
                var message = $"Current Cleanup Configuration:\n\n" +
                             $"Auto-cleanup enabled: {config.AutoCleanupEnabled}\n" +
                             $"Cleanup interval: {config.CleanupIntervalDays} days\n" +
                             $"Delete files older than: {config.DeleteFilesOlderThanDays} days\n" +
                             $"Max recycle bin size: {FormatFileSize(config.MaxRecycleBinSize)}\n" +
                             $"Include temp files: {config.IncludeTempFiles}\n" +
                             $"Include downloads: {config.IncludeDownloads}\n\n" +
                             $"Modify settings in application preferences.";
                
                System.Windows.MessageBox.Show(message, "Cleanup Configuration",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                StatusMessage = "Cleanup configuration displayed";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Failed to load configuration: {ex.Message}";
            }
        }

        [RelayCommand]
        private async void ScheduleCleanup()
        {
            try
            {
                var scheduleOptions = new CleanupScheduleOptions
                {
                    Enabled = true,
                    Frequency = CleanupFrequency.Weekly,
                    DayOfWeek = DayOfWeek.Sunday,
                    TimeOfDay = new TimeSpan(2, 0, 0),
                    NotifyBeforeCleanup = true
                };
                
                await _cleanupService.ScheduleCleanupAsync(scheduleOptions);
                
                StatusMessage = $"Cleanup scheduled for every {scheduleOptions.DayOfWeek} at {scheduleOptions.TimeOfDay}";
                
                System.Windows.MessageBox.Show(
                    $"Automatic cleanup has been scheduled.\n\n" +
                    $"Frequency: {scheduleOptions.Frequency}\n" +
                    $"Day: {scheduleOptions.DayOfWeek}\n" +
                    $"Time: {scheduleOptions.TimeOfDay}\n\n" +
                    $"You will be notified before cleanup runs.",
                    "Cleanup Scheduled",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Failed to schedule cleanup: {ex.Message}";
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

    /// <summary>
    /// ViewModel for system information
    /// </summary>
    public partial class SystemInfoViewModel : ObservableObject
    {
        private readonly ISystemInfoService _systemInfoService;
        private ObservableCollection<SystemCategory> _systemCategories = new();

        [ObservableProperty]
        private SystemCategory? _selectedCategory;

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        [ObservableProperty]
        private SystemInformation _systemInfo = new();

        [ObservableProperty]
        private bool _refreshing;

        [ObservableProperty]
        private DateTime _lastRefresh;

        [ObservableProperty]
        private TimeSpan _refreshInterval = TimeSpan.FromSeconds(5);

        public ObservableCollection<SystemCategory> SystemCategories
        {
            get => _systemCategories;
            set => SetProperty(ref _systemCategories, value);
        }

        public SystemInfoViewModel(ISystemInfoService systemInfoService)
        {
            _systemInfoService = systemInfoService;
            _ = LoadSystemCategoriesAsync();
            _ = LoadSystemInfoAsync();
        }

        private async Task LoadSystemCategoriesAsync()
        {
            try
            {
                var categories = await _systemInfoService.GetSystemCategoriesAsync();
                SystemCategories.Clear();
                foreach (var category in categories)
                {
                    SystemCategories.Add(category);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading categories: {ex.Message}";
            }
        }

        private async Task LoadSystemInfoAsync()
        {
            IsLoading = true;

            try
            {
                SystemInfo = await _systemInfoService.GetSystemInformationAsync();
                LastRefresh = DateTime.Now;
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading system info: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task RefreshAsync()
        {
            Refreshing = true;
            StatusMessage = "Refreshing...";

            try
            {
                if (SelectedCategory != null)
                {
                    await _systemInfoService.RefreshCategoryAsync(SelectedCategory.Id);
                }
                else
                {
                    await LoadSystemInfoAsync();
                }
                StatusMessage = "Information refreshed";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Refresh failed: {ex.Message}";
            }
            finally
            {
                Refreshing = false;
            }
        }

        [RelayCommand]
        private async void ExportSystemInfo()
        {
            try
            {
                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "Text Files (*.txt)|*.txt|JSON Files (*.json)|*.json|HTML Files (*.html)|*.html",
                    DefaultExt = ".txt",
                    FileName = $"SystemInfo_{DateTime.Now:yyyyMMdd_HHmmss}"
                };
                
                if (dialog.ShowDialog() == true)
                {
                    StatusMessage = "Exporting system information...";
                    await _systemInfoService.ExportSystemInfoAsync(SystemInfo, dialog.FileName);
                    StatusMessage = $"System information exported to {dialog.FileName}";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Export failed: {ex.Message}";
            }
        }

        [RelayCommand]
        private async void SaveReport()
        {
            try
            {
                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "PDF Files (*.pdf)|*.pdf|HTML Files (*.html)|*.html",
                    DefaultExt = ".pdf",
                    FileName = $"SystemReport_{DateTime.Now:yyyyMMdd_HHmmss}"
                };
                
                if (dialog.ShowDialog() == true)
                {
                    StatusMessage = "Generating system report...";
                    var report = await _systemInfoService.GenerateReportAsync(SystemInfo);
                    await System.IO.File.WriteAllTextAsync(dialog.FileName, report);
                    StatusMessage = $"System report saved to {dialog.FileName}";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Failed to save report: {ex.Message}";
            }
        }

        [RelayCommand]
        private async void RunDiagnostics()
        {
            try
            {
                IsLoading = true;
                StatusMessage = "Running system diagnostics...";
                
                var diagnostics = await _systemInfoService.RunDiagnosticsAsync();
                
                var result = $"System Diagnostics Results:\n\n" +
                            $"CPU Health: {diagnostics.CpuHealth}\n" +
                            $"Memory Health: {diagnostics.MemoryHealth}\n" +
                            $"Disk Health: {diagnostics.DiskHealth}\n" +
                            $"Network Health: {diagnostics.NetworkHealth}\n" +
                            $"Overall Status: {diagnostics.OverallStatus}\n\n" +
                            $"Issues Found: {diagnostics.IssuesFound}\n" +
                            $"Recommendations: {diagnostics.Recommendations.Count}";
                
                System.Windows.MessageBox.Show(result, "Diagnostics Complete",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                
                StatusMessage = $"Diagnostics complete - {diagnostics.OverallStatus}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Diagnostics failed: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private void ViewPerformanceMonitor()
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "perfmon.exe",
                    UseShellExecute = true
                };
                System.Diagnostics.Process.Start(psi);
                StatusMessage = "Performance Monitor launched";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Failed to launch Performance Monitor: {ex.Message}";
            }
        }

        [RelayCommand]
        private void ViewEventViewer()
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "eventvwr.exe",
                    UseShellExecute = true
                };
                System.Diagnostics.Process.Start(psi);
                StatusMessage = "Event Viewer launched";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Failed to launch Event Viewer: {ex.Message}";
            }
        }

        [RelayCommand]
        private void ViewDeviceManager()
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "devmgmt.msc",
                    UseShellExecute = true
                };
                System.Diagnostics.Process.Start(psi);
                StatusMessage = "Device Manager launched";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Failed to launch Device Manager: {ex.Message}";
            }
        }

        [RelayCommand]
        private void CheckForUpdates()
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "ms-settings:windowsupdate",
                    UseShellExecute = true
                };
                System.Diagnostics.Process.Start(psi);
                StatusMessage = "Windows Update opened";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Failed to open Windows Update: {ex.Message}";
            }
        }

        [RelayCommand]
        private async void GenerateHealthReport()
        {
            try
            {
                IsLoading = true;
                StatusMessage = "Generating system health report...";
                
                var healthReport = await _systemInfoService.GenerateHealthReportAsync();
                
                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "HTML Files (*.html)|*.html|PDF Files (*.pdf)|*.pdf",
                    DefaultExt = ".html",
                    FileName = $"HealthReport_{DateTime.Now:yyyyMMdd_HHmmss}"
                };
                
                if (dialog.ShowDialog() == true)
                {
                    await System.IO.File.WriteAllTextAsync(dialog.FileName, healthReport.HtmlContent);
                    StatusMessage = $"Health report saved to {dialog.FileName}";
                    
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = dialog.FileName,
                        UseShellExecute = true
                    });
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Failed to generate health report: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }
    }

    // Model classes
    public class DiskDrive
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Model { get; set; } = string.Empty;
        public string SerialNumber { get; set; } = string.Empty;
        public string Interface { get; set; } = string.Empty;
        public long TotalSize { get; set; }
        public DriveType Type { get; set; }
        public DriveStatus Status { get; set; }
        public string Firmware { get; set; } = string.Empty;
        public int Temperature { get; set; }
        public int PowerOnHours { get; set; }
        public HealthStatus HealthStatus { get; set; }
        public bool IsRemovable { get; set; }
        public bool IsSystem { get; set; }
        public DateTime LastScanned { get; set; }
    }

    public class DiskPartition
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string DriveId { get; set; } = string.Empty;
        public string Letter { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string FileSystem { get; set; } = string.Empty;
        public long TotalSize { get; set; }
        public long FreeSpace { get; set; }
        public long UsedSpace { get; set; }
        public PartitionType Type { get; set; }
        public bool IsActive { get; set; }
        public bool IsSystem { get; set; }
        public bool IsBoot { get; set; }
        public bool IsCompressed { get; set; }
        public int FragmentationPercentage { get; set; }
        public DateTime LastDefragmented { get; set; }
    }

    public class DiskOperationProgress
    {
        public double Percentage { get; set; }
        public string CurrentOperation { get; set; } = string.Empty;
        public string CurrentFile { get; set; } = string.Empty;
        public long BytesProcessed { get; set; }
        public long TotalBytes { get; set; }
        public TimeSpan EstimatedTimeRemaining { get; set; }
        public double Speed { get; set; }
    }

    public class FormatOptions
    {
        public string VolumeLabel { get; set; } = string.Empty;
        public string FileSystem { get; set; } = "NTFS";
        public long ClusterSize { get; set; } = 4096;
        public bool QuickFormat { get; set; } = true;
        public bool EnableCompression { get; set; } = false;
        public bool EnableIndexing { get; set; } = true;
    }

    public class CheckDiskOptions
    {
        public bool FixErrors { get; set; } = true;
        public bool RecoverBadSectors { get; set; } = true;
        public bool ForceDismount { get; set; } = false;
        public bool ScanForBadSectors { get; set; } = false;
    }

    public class CleanupCategory
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public long EstimatedSpace { get; set; }
        public int ItemCount { get; set; }
        public bool IsExpanded { get; set; }
        public RiskLevel DefaultRiskLevel { get; set; }
    }

    public class CleanupItem
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string CategoryId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public long Size { get; set; }
        public DateTime LastAccessed { get; set; }
        public DateTime Created { get; set; }
        public RiskLevel RiskLevel { get; set; }
        public bool IsSelected { get; set; }
        public bool CanPreview { get; set; }
        public List<string> FilePaths { get; set; } = new();
    }

    public class CleanupScanProgress
    {
        public double Percentage { get; set; }
        public string CurrentItem { get; set; } = string.Empty;
        public long SpaceToFree { get; set; }
        public int ItemCount { get; set; }
        public int ScannedCategories { get; set; }
        public int TotalCategories { get; set; }
    }

    public class CleanupProgress
    {
        public double Percentage { get; set; }
        public string CurrentItem { get; set; } = string.Empty;
        public long SpaceFreed { get; set; }
        public int ItemsProcessed { get; set; }
        public int TotalItems { get; set; }
        public TimeSpan EstimatedTimeRemaining { get; set; }
    }

    public class CleanupResult
    {
        public long SpaceFreed { get; set; }
        public int ItemsDeleted { get; set; }
        public List<CleanupItem> CleanedItems { get; set; } = new();
        public List<string> Errors { get; set; } = new();
        public TimeSpan Duration { get; set; }
    }

    public class SystemCategory
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public bool IsExpanded { get; set; }
        public int Order { get; set; }
    }

    public class SystemInformation
    {
        public SystemSummary Summary { get; set; } = new();
        public HardwareInfo Hardware { get; set; } = new();
        public SoftwareInfo Software { get; set; } = new();
        public NetworkInfo Network { get; set; } = new();
        public StorageInfo Storage { get; set; } = new();
        public PerformanceInfo Performance { get; set; } = new();
        public Dictionary<string, object> CustomInfo { get; set; } = new();
    }

    public class SystemSummary
    {
        public string ComputerName { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string OSName { get; set; } = string.Empty;
        public string OSVersion { get; set; } = string.Empty;
        public string Architecture { get; set; } = string.Empty;
        public DateTime BootTime { get; set; }
        public TimeSpan Uptime { get; set; }
        public int ProcessCount { get; set; }
        public int ThreadCount { get; set; }
        public long TotalMemory { get; set; }
        public long AvailableMemory { get; set; }
    }

    public class HardwareInfo
    {
        public string Processor { get; set; } = string.Empty;
        public int Cores { get; set; }
        public int LogicalProcessors { get; set; }
        public double MaxClockSpeed { get; set; }
        public double CurrentClockSpeed { get; set; }
        public string Motherboard { get; set; } = string.Empty;
        public string BIOS { get; set; } = string.Empty;
        public List<MemoryModule> MemoryModules { get; set; } = new();
        public List<GraphicsCard> GraphicsCards { get; set; } = new();
        public List<DiskDrive> Drives { get; set; } = new();
    }

    public class MemoryModule
    {
        public string Manufacturer { get; set; } = string.Empty;
        public string PartNumber { get; set; } = string.Empty;
        public long Capacity { get; set; }
        public string Type { get; set; } = string.Empty;
        public double Speed { get; set; }
        public string FormFactor { get; set; } = string.Empty;
    }

    public class GraphicsCard
    {
        public string Name { get; set; } = string.Empty;
        public string DriverVersion { get; set; } = string.Empty;
        public long DedicatedMemory { get; set; }
        public long SharedMemory { get; set; }
        public double CurrentClockSpeed { get; set; }
        public double Temperature { get; set; }
    }

    public class SoftwareInfo
    {
        public List<InstalledProgram> InstalledPrograms { get; set; } = new();
        public List<WindowsFeature> WindowsFeatures { get; set; } = new();
        public List<SystemService> Services { get; set; } = new();
        public List<StartupProgram> StartupPrograms { get; set; } = new();
        public List<WindowsUpdate> Updates { get; set; } = new();
    }

    public class InstalledProgram
    {
        public string Name { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string Publisher { get; set; } = string.Empty;
        public DateTime InstallDate { get; set; }
        public long Size { get; set; }
        public string InstallLocation { get; set; } = string.Empty;
    }

    public class NetworkInfo
    {
        public List<NetworkAdapter> Adapters { get; set; } = new();
        public List<NetworkConnection> Connections { get; set; } = new();
        public List<PortInfo> OpenPorts { get; set; } = new();
        public Dictionary<string, object> Statistics { get; set; } = new();
    }

    public class NetworkAdapter
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string IPAddress { get; set; } = string.Empty;
        public string SubnetMask { get; set; } = string.Empty;
        public string Gateway { get; set; } = string.Empty;
        public string DNSServers { get; set; } = string.Empty;
        public double Speed { get; set; }
        public long BytesSent { get; set; }
        public long BytesReceived { get; set; }
    }

    public class StorageInfo
    {
        public List<DiskDrive> Drives { get; set; } = new();
        public List<VolumeInfo> Volumes { get; set; } = new();
        public Dictionary<string, object> Quotas { get; set; } = new();
    }

    public class PerformanceInfo
    {
        public double CpuUsage { get; set; }
        public long MemoryUsage { get; set; }
        public double DiskUsage { get; set; }
        public double NetworkUsage { get; set; }
        public List<ProcessInfo> TopProcesses { get; set; } = new();
        public Dictionary<string, PerformanceCounter> Counters { get; set; } = new();
    }

    // Supporting classes
    public class VolumeInfo
    {
        public string Letter { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string FileSystem { get; set; } = string.Empty;
        public long TotalSize { get; set; }
        public long FreeSpace { get; set; }
        public bool IsSystem { get; set; }
        public bool IsCompressed { get; set; }
    }

    public class WindowsFeature
    {
        public string Name { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public bool IsEnabled { get; set; }
        public string Description { get; set; } = string.Empty;
    }

    public class SystemService
    {
        public string Name { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string StartType { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    public class StartupProgram
    {
        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public bool IsEnabled { get; set; }
        public string Publisher { get; set; } = string.Empty;
    }

    public class WindowsUpdate
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime InstalledDate { get; set; }
        public string KBNumber { get; set; } = string.Empty;
        public UpdateType Type { get; set; }
    }

    public class NetworkConnection
    {
        public string Protocol { get; set; } = string.Empty;
        public string LocalAddress { get; set; } = string.Empty;
        public string RemoteAddress { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public int ProcessId { get; set; }
        public string ProcessName { get; set; } = string.Empty;
    }

    public class PortInfo
    {
        public int Port { get; set; }
        public string Protocol { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string Process { get; set; } = string.Empty;
    }

    public class ProcessInfo
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public double CpuUsage { get; set; }
        public long MemoryUsage { get; set; }
        public string UserName { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
    }

    public class PerformanceCounter
    {
        public string Name { get; set; } = string.Empty;
        public double Value { get; set; }
        public string Unit { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
    }

    // Enums
    public enum DriveType
    {
        Unknown,
        NoRootDirectory,
        Removable,
        Fixed,
        Network,
        CDRom,
        Ram
    }

    public enum DriveStatus
    {
        Healthy,
        Warning,
        Error,
        Unknown
    }

    public enum HealthStatus
    {
        Good,
        Warning,
        Critical,
        Unknown
    }

    public enum PartitionType
    {
        Primary,
        Extended,
        Logical,
        Recovery,
        System,
        OEM
    }

    public enum RiskLevel
    {
        Safe,
        Low,
        Medium,
        High,
        Critical
    }

    public enum UpdateType
    {
        Security,
        Critical,
        Definition,
        Feature,
        Driver,
        Optional
    }
}
