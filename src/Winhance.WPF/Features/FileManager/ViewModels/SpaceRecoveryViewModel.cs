using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Winhance.Core.Features.FileManager.Interfaces;
using Winhance.WPF.Features.FileManager.Controls;

namespace Winhance.WPF.Features.FileManager.ViewModels
{
    /// <summary>
    /// ViewModel for the Space Recovery feature.
    /// Analyzes drives and finds opportunities to recover disk space.
    /// </summary>
    public partial class SpaceRecoveryViewModel : ObservableObject
    {
        private readonly IOrganizerService? _organizerService;

        [ObservableProperty]
        private string _selectedDrive = "C:";

        [ObservableProperty]
        private ObservableCollection<string> _availableDrives = new() { "C:", "D:", "E:", "F:" };

        [ObservableProperty]
        private ObservableCollection<RecoveryOpportunityViewModel> _recoveryOpportunities = new();

        [ObservableProperty]
        private long _recoverableSpace;

        public string RecoverableSpaceDisplay => $"{FormatSize(RecoverableSpace)} Recoverable";

        partial void OnRecoverableSpaceChanged(long value)
        {
            OnPropertyChanged(nameof(RecoverableSpaceDisplay));
        }

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private string _statusMessage = "Select a drive and click Analyze to find recovery opportunities";

        [ObservableProperty]
        private double _analyzeProgress;

        [ObservableProperty]
        private ObservableCollection<TreeMapItem> _treeMapItems = new();

        public SpaceRecoveryViewModel()
        {
            // Design-time constructor
            LoadDesignTimeData();
        }

        public SpaceRecoveryViewModel(IOrganizerService? organizerService)
        {
            _organizerService = organizerService;
            _ = DetectAvailableDrivesAsync();
        }

        private void LoadDesignTimeData()
        {
            RecoveryOpportunities.Add(new RecoveryOpportunityViewModel
            {
                Category = "Temporary Files",
                Size = 2500000000,
                ItemCount = 1250,
                RecommendedAction = RecoveryAction.Clean,
                Description = "Windows temporary files",
                IsSafeToClean = true,
            });
            RecoveryOpportunities.Add(new RecoveryOpportunityViewModel
            {
                Category = "AI Models (.lmstudio)",
                Size = 337000000000,
                ItemCount = 428,
                RecommendedAction = RecoveryAction.Relocate,
                Description = "Relocate to D: drive",
                IsSafeToClean = false,
            });
            RecoverableSpace = 339500000000;
        }

        private async Task DetectAvailableDrivesAsync()
        {
            await Task.Run(() =>
            {
                try
                {
                    var drives = System.IO.DriveInfo.GetDrives()
                        .Where(d => d.IsReady && d.DriveType == System.IO.DriveType.Fixed)
                        .Select(d => d.Name.TrimEnd('\\'))
                        .ToList();

                    System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                    {
                        AvailableDrives.Clear();
                        foreach (var drive in drives)
                        {
                            AvailableDrives.Add(drive);
                        }
                        if (AvailableDrives.Count > 0 && !AvailableDrives.Contains(SelectedDrive))
                        {
                            SelectedDrive = AvailableDrives[0];
                        }
                    });
                }
                catch
                {
                    // Keep defaults
                }
            });
        }

        [RelayCommand]
        private async Task AnalyzeAsync()
        {
            if (_organizerService == null)
            {
                // Use fallback analysis when service not available
                await AnalyzeFallbackAsync();
                return;
            }

            IsLoading = true;
            StatusMessage = $"Analyzing {SelectedDrive} for recovery opportunities...";
            AnalyzeProgress = 0;

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
                StatusMessage = $"Found {FormatSize(RecoverableSpace)} recoverable space in {RecoveryOpportunities.Count} categories";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
                AnalyzeProgress = 100;
            }
        }

        private async Task AnalyzeFallbackAsync()
        {
            IsLoading = true;
            StatusMessage = $"Analyzing {SelectedDrive}...";
            AnalyzeProgress = 0;

            await Task.Run(async () =>
            {
                var opportunities = new System.Collections.Generic.List<RecoveryOpportunityViewModel>();
                int progressStep = 0;
                int totalSteps = 12;

                void UpdateProgress(string message)
                {
                    progressStep++;
                    var progress = (double)progressStep / totalSteps * 100;
                    System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                    {
                        AnalyzeProgress = progress;
                        StatusMessage = message;
                    });
                }

                try
                {
                    var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                    var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

                    // 1. User Temp folder
                    UpdateProgress("Scanning user temp files...");
                    var tempPath = System.IO.Path.GetTempPath();
                    await AddDirectoryOpportunityAsync(opportunities, tempPath, "User Temp Files",
                        "Temporary files created by applications - safe to clean", true, true);

                    // 2. Windows Temp
                    UpdateProgress("Scanning Windows temp files...");
                    var windowsTemp = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp");
                    await AddDirectoryOpportunityAsync(opportunities, windowsTemp, "Windows Temp",
                        "Windows system temporary files", true, true);

                    // 3. Windows Prefetch
                    UpdateProgress("Scanning prefetch cache...");
                    var prefetch = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Prefetch");
                    await AddDirectoryOpportunityAsync(opportunities, prefetch, "Prefetch Cache",
                        "Windows prefetch files - can slow app starts if cleaned", false, false);

                    // 4. Windows Update Cache
                    UpdateProgress("Scanning Windows Update cache...");
                    var updateCache = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "SoftwareDistribution", "Download");
                    await AddDirectoryOpportunityAsync(opportunities, updateCache, "Windows Update Cache",
                        "Downloaded Windows updates - safe after updates complete", true, false);

                    // 5. Browser Caches - Chrome
                    UpdateProgress("Scanning Chrome cache...");
                    var chromeCache = System.IO.Path.Combine(localAppData, "Google", "Chrome", "User Data", "Default", "Cache");
                    await AddDirectoryOpportunityAsync(opportunities, chromeCache, "Chrome Cache",
                        "Google Chrome browser cache", true, true);

                    // 6. Browser Caches - Edge
                    UpdateProgress("Scanning Edge cache...");
                    var edgeCache = System.IO.Path.Combine(localAppData, "Microsoft", "Edge", "User Data", "Default", "Cache");
                    await AddDirectoryOpportunityAsync(opportunities, edgeCache, "Edge Cache",
                        "Microsoft Edge browser cache", true, true);

                    // 7. Browser Caches - Firefox
                    UpdateProgress("Scanning Firefox cache...");
                    var firefoxPath = System.IO.Path.Combine(localAppData, "Mozilla", "Firefox", "Profiles");
                    if (System.IO.Directory.Exists(firefoxPath))
                    {
                        foreach (var profile in System.IO.Directory.GetDirectories(firefoxPath))
                        {
                            var ffCache = System.IO.Path.Combine(profile, "cache2");
                            await AddDirectoryOpportunityAsync(opportunities, ffCache, "Firefox Cache",
                                "Mozilla Firefox browser cache", true, true);
                        }
                    }

                    // 8. Thumbnails Cache
                    UpdateProgress("Scanning thumbnail cache...");
                    var thumbCache = System.IO.Path.Combine(localAppData, "Microsoft", "Windows", "Explorer");
                    long thumbSize = 0;
                    int thumbCount = 0;
                    if (System.IO.Directory.Exists(thumbCache))
                    {
                        foreach (var file in System.IO.Directory.GetFiles(thumbCache, "thumbcache_*.db"))
                        {
                            try
                            {
                                thumbSize += new System.IO.FileInfo(file).Length;
                                thumbCount++;
                            }
                            catch { }
                        }
                    }
                    if (thumbSize > 1024 * 1024) // Only show if > 1MB
                    {
                        opportunities.Add(new RecoveryOpportunityViewModel
                        {
                            Category = "Thumbnail Cache",
                            Path = thumbCache,
                            Size = thumbSize,
                            ItemCount = thumbCount,
                            RecommendedAction = RecoveryAction.Clean,
                            Description = "Windows thumbnail cache - will be rebuilt as needed",
                            IsSafeToClean = true,
                            IsSelected = false,
                        });
                    }

                    // 9. Windows Error Reports
                    UpdateProgress("Scanning error reports...");
                    var wer = System.IO.Path.Combine(localAppData, "Microsoft", "Windows", "WER", "ReportQueue");
                    await AddDirectoryOpportunityAsync(opportunities, wer, "Error Reports",
                        "Windows Error Reporting files", true, true);

                    // 10. Delivery Optimization Cache
                    UpdateProgress("Scanning Delivery Optimization cache...");
                    var deliveryOpt = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "ServiceProfiles", "NetworkService", "AppData", "Local", "Microsoft", "Windows", "DeliveryOptimization", "Cache");
                    await AddDirectoryOpportunityAsync(opportunities, deliveryOpt, "Delivery Optimization",
                        "Windows Update delivery optimization cache", true, false);

                    // 11. NPM Cache (if exists)
                    UpdateProgress("Scanning developer caches...");
                    var npmCache = System.IO.Path.Combine(userProfile, ".npm", "_cacache");
                    await AddDirectoryOpportunityAsync(opportunities, npmCache, "NPM Cache",
                        "Node.js package manager cache", true, false);

                    // 12. Large folders on selected drive
                    UpdateProgress("Scanning for large folders...");
                    await ScanForLargeFoldersAsync(opportunities, SelectedDrive);

                    System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                    {
                        RecoveryOpportunities.Clear();
                        foreach (var opp in opportunities.OrderByDescending(o => o.Size))
                        {
                            RecoveryOpportunities.Add(opp);
                        }
                        RecoverableSpace = opportunities.Sum(o => o.Size);
                        StatusMessage = opportunities.Count > 0
                            ? $"Found {FormatSize(RecoverableSpace)} recoverable in {opportunities.Count} categories"
                            : "No significant recovery opportunities found";
                        AnalyzeProgress = 100;
                    });
                }
                catch (Exception ex)
                {
                    System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                    {
                        StatusMessage = $"Analysis error: {ex.Message}";
                        AnalyzeProgress = 100;
                    });
                }
            });

            IsLoading = false;
        }

        private static async Task AddDirectoryOpportunityAsync(
            System.Collections.Generic.List<RecoveryOpportunityViewModel> opportunities,
            string path, string category, string description, bool safeToClean, bool autoSelect)
        {
            if (!System.IO.Directory.Exists(path))
            {
                return;
            }

            var (size, count) = await GetDirectorySizeAndCountAsync(path);
            if (size < 1024 * 1024) // Skip if less than 1MB
            {
                return;
            }

            opportunities.Add(new RecoveryOpportunityViewModel
            {
                Category = category,
                Path = path,
                Size = size,
                ItemCount = count,
                RecommendedAction = RecoveryAction.Clean,
                Description = description,
                IsSafeToClean = safeToClean,
                IsSelected = autoSelect && safeToClean,
            });
        }

        private static async Task ScanForLargeFoldersAsync(
            System.Collections.Generic.List<RecoveryOpportunityViewModel> opportunities, string drive)
        {
            await Task.Run(() =>
            {
                try
                {
                    var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

                    // Check common large folder locations
                    var largeFolderPaths = new[]
                    {
                        System.IO.Path.Combine(userProfile, ".lmstudio"),
                        System.IO.Path.Combine(userProfile, ".ollama"),
                        System.IO.Path.Combine(userProfile, ".cache"),
                        System.IO.Path.Combine(userProfile, "node_modules"),
                        System.IO.Path.Combine(userProfile, ".nuget"),
                        System.IO.Path.Combine(userProfile, ".gradle"),
                        System.IO.Path.Combine(userProfile, ".m2"),
                        System.IO.Path.Combine(userProfile, "AppData", "Local", "Docker"),
                        System.IO.Path.Combine(userProfile, "AppData", "Local", "Packages"),
                    };

                    foreach (var folderPath in largeFolderPaths)
                    {
                        if (!System.IO.Directory.Exists(folderPath))
                        {
                            continue;
                        }

                        try
                        {
                            var dirInfo = new System.IO.DirectoryInfo(folderPath);
                            long size = 0;
                            int count = 0;

                            // Quick size estimate from top-level files
                            foreach (var file in dirInfo.EnumerateFiles("*", System.IO.SearchOption.TopDirectoryOnly))
                            {
                                try
                                {
                                    size += file.Length;
                                    count++;
                                }
                                catch { }
                            }

                            // Add subdirectory count
                            count += dirInfo.GetDirectories().Length;

                            // Estimate: multiply by depth factor for nested content
                            size *= 10; // Rough estimate

                            if (size > 500 * 1024 * 1024) // > 500MB estimated
                            {
                                var folderName = System.IO.Path.GetFileName(folderPath);
                                var action = folderName.StartsWith(".lm") || folderName.StartsWith(".ol")
                                    ? RecoveryAction.Relocate
                                    : RecoveryAction.Review;

                                opportunities.Add(new RecoveryOpportunityViewModel
                                {
                                    Category = $"Large Folder: {folderName}",
                                    Path = folderPath,
                                    Size = size,
                                    ItemCount = count,
                                    RecommendedAction = action,
                                    Description = action == RecoveryAction.Relocate
                                        ? "Consider relocating to another drive"
                                        : "Review contents and clean if not needed",
                                    IsSafeToClean = false,
                                    IsSelected = false,
                                });
                            }
                        }
                        catch { }
                    }
                }
                catch { }
            });
        }

        private static async Task<(long size, int count)> GetDirectorySizeAndCountAsync(string path)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var dirInfo = new System.IO.DirectoryInfo(path);
                    long totalSize = 0;
                    int fileCount = 0;

                    foreach (var file in dirInfo.EnumerateFiles("*", System.IO.SearchOption.AllDirectories))
                    {
                        try
                        {
                            totalSize += file.Length;
                            fileCount++;
                        }
                        catch { }
                    }

                    return (totalSize, fileCount);
                }
                catch
                {
                    return (0, 0);
                }
            });
        }

        private static async Task<long> GetDirectorySizeAsync(string path)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var dirInfo = new System.IO.DirectoryInfo(path);
                    return dirInfo.EnumerateFiles("*", System.IO.SearchOption.TopDirectoryOnly)
                        .Sum(f => f.Length);
                }
                catch
                {
                    return 0;
                }
            });
        }

        [RelayCommand]
        private async Task ExecuteRecoveryAsync()
        {
            var selectedItems = RecoveryOpportunities.Where(o => o.IsSelected).ToList();
            if (selectedItems.Count == 0)
            {
                StatusMessage = "No items selected for recovery";
                return;
            }

            IsLoading = true;
            long totalRecovered = 0;

            try
            {
                foreach (var item in selectedItems)
                {
                    StatusMessage = $"Processing {item.Category}...";

                    if (item.RecommendedAction == RecoveryAction.Clean && item.IsSafeToClean)
                    {
                        // For temp files, try to clean
                        if (System.IO.Directory.Exists(item.Path) && item.Path.Contains("Temp", StringComparison.OrdinalIgnoreCase))
                        {
                            try
                            {
                                var files = System.IO.Directory.GetFiles(item.Path);
                                foreach (var file in files)
                                {
                                    try
                                    {
                                        var fileInfo = new System.IO.FileInfo(file);
                                        var size = fileInfo.Length;
                                        fileInfo.Delete();
                                        totalRecovered += size;
                                    }
                                    catch
                                    {
                                        // Skip files in use
                                    }
                                }
                            }
                            catch
                            {
                                // Skip inaccessible directories
                            }
                        }
                    }
                }

                StatusMessage = totalRecovered > 0
                    ? $"Recovered {FormatSize(totalRecovered)}"
                    : "Recovery complete - some files may be in use";

                // Refresh analysis
                await AnalyzeAsync();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error during recovery: {ex.Message}";
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
}
