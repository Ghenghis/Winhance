using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using Winhance.Core.Features.FileManager.Interfaces;
using Winhance.Core.Features.FileManager.Models;

namespace Winhance.WPF.Features.FileManager.ViewModels
{
    /// <summary>
    /// ViewModel for disk space analysis functionality.
    /// </summary>
    public partial class SpaceAnalyzerViewModel : ObservableObject
    {
        private readonly ISpaceAnalyzerService _spaceAnalyzerService;
        private readonly IFileManagerService _fileManagerService;
        private CancellationTokenSource? _cancellationTokenSource;

        [ObservableProperty]
        private string _scanPath = string.Empty;

        [ObservableProperty]
        private bool _isScanning;

        [ObservableProperty]
        private double _scanProgress;

        [ObservableProperty]
        private string _scanStatus = "Ready to scan";

        [ObservableProperty]
        private long _totalSize;

        [ObservableProperty]
        private int _totalFiles;

        [ObservableProperty]
        private int _totalFolders;

        [ObservableProperty]
        private long _freeSpace;

        [ObservableProperty]
        private long _totalSpace;

        [ObservableProperty]
        private ObservableCollection<FolderSizeViewModel> _topFolders = new();

        [ObservableProperty]
        private ObservableCollection<FileSizeViewModel> _largestFiles = new();

        [ObservableProperty]
        private ObservableCollection<FileTypeViewModel> _fileTypes = new();

        [ObservableProperty]
        private ObservableCollection<TimeSeriesViewModel> _sizeHistory = new();

        [ObservableProperty]
        private FolderSizeViewModel? _selectedFolder;

        [ObservableProperty]
        private ViewMode _currentView = ViewMode.TreeMap;

        [ObservableProperty]
        private bool _showHiddenFiles;

        [ObservableProperty]
        private int _maxItems = 100;

        [ObservableProperty]
        private string _filterPattern = string.Empty;

        [ObservableProperty]
        private ISeries[] _pieSeries = Array.Empty<ISeries>();

        [ObservableProperty]
        private ISeries[] _barSeries = Array.Empty<ISeries>();

        [ObservableProperty]
        private Axis[] _xAxes = Array.Empty<Axis>();

        [ObservableProperty]
        private Axis[] _yAxes = Array.Empty<Axis>();

        public SpaceAnalyzerViewModel(
            ISpaceAnalyzerService spaceAnalyzerService,
            IFileManagerService fileManagerService)
        {
            _spaceAnalyzerService = spaceAnalyzerService;
            _fileManagerService = fileManagerService;

            // Set default scan path
            ScanPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }

        /// <summary>
        /// Starts scanning the specified path.
        /// </summary>
        [RelayCommand]
        public async Task StartScanAsync()
        {
            if (string.IsNullOrWhiteSpace(ScanPath) || !Directory.Exists(ScanPath))
            {
                ScanStatus = "Invalid path";
                return;
            }

            // Cancel previous scan
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource = new CancellationTokenSource();

            IsScanning = true;
            ScanProgress = 0;
            ScanStatus = "Initializing scan...";
            TopFolders.Clear();
            LargestFiles.Clear();
            FileTypes.Clear();

            try
            {
                // Get drive information
                var driveInfo = new DriveInfo(Path.GetPathRoot(ScanPath)!);
                TotalSpace = driveInfo.TotalSize;
                FreeSpace = driveInfo.AvailableFreeSpace;

                // Create progress reporter
                var progress = new Progress<ScanProgress>(p =>
                {
                    ScanProgress = p.ProgressPercentage;
                    ScanStatus = $"Scanning... {p.CurrentFolder}";
                    TotalFiles = p.FilesScanned;
                    TotalFolders = p.FoldersScanned;
                });

                // Start scan
                var result = await _spaceAnalyzerService.ScanAsync(ScanPath, 
                    new ScanOptions
                    {
                        IncludeHidden = ShowHiddenFiles,
                        MaxDepth = 10,
                        CancellationToken = _cancellationTokenSource.Token
                    }, progress);

                if (!_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    // Update results
                    TotalSize = result.TotalSize;
                    TotalFiles = result.FileCount;
                    TotalFolders = result.FolderCount;

                    // Process results
                    await ProcessScanResultsAsync(result);

                    ScanStatus = $"Scan complete: {FormatSize(TotalSize)} in {TotalFiles:N0} files, {TotalFolders:N0} folders";
                }
            }
            catch (OperationCanceledException)
            {
                ScanStatus = "Scan cancelled";
            }
            catch (UnauthorizedAccessException)
            {
                ScanStatus = "Access denied to some folders";
            }
            catch (Exception ex)
            {
                ScanStatus = $"Scan error: {ex.Message}";
            }
            finally
            {
                IsScanning = false;
            }
        }

        /// <summary>
        /// Cancels the current scan.
        /// </summary>
        [RelayCommand]
        public void CancelScan()
        {
            _cancellationTokenSource?.Cancel();
            ScanStatus = "Cancelling scan...";
        }

        /// <summary>
        /// Drills down into the selected folder.
        /// </summary>
        [RelayCommand]
        public async Task DrillDownAsync()
        {
            if (SelectedFolder?.FullPath == null) return;

            ScanPath = SelectedFolder.FullPath;
            await StartScanAsync();
        }

        /// <summary>
        /// Navigates to the parent folder.
        /// </summary>
        [RelayCommand]
        public async Task NavigateToParentAsync()
        {
            var parent = Path.GetDirectoryName(ScanPath);
            if (!string.IsNullOrEmpty(parent))
            {
                ScanPath = parent;
                await StartScanAsync();
            }
        }

        /// <summary>
        /// Refreshes the current scan.
        /// </summary>
        [RelayCommand]
        public async Task RefreshAsync()
        {
            await StartScanAsync();
        }

        /// <summary>
        /// Deletes selected items.
        /// </summary>
        [RelayCommand]
        public async Task DeleteSelectedAsync()
        {
            var itemsToDelete = new List<string>();
            
            // Add selected folder
            if (SelectedFolder?.FullPath != null)
            {
                itemsToDelete.Add(SelectedFolder.FullPath);
            }

            // Add selected files from the Files collection if available
            if (Files != null)
            {
                var selectedFiles = Files.Where(f => f.IsSelected).Select(f => f.FullPath);
                itemsToDelete.AddRange(selectedFiles);
            }

            if (itemsToDelete.Count == 0) return;

            try
            {
                var confirm = System.Windows.MessageBox.Show(
                    $"Are you sure you want to delete {itemsToDelete.Count} item(s)?",
                    "Confirm Delete",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Warning);

                if (confirm == System.Windows.MessageBoxResult.Yes)
                {
                    foreach (var item in itemsToDelete)
                    {
                        if (Directory.Exists(item))
                        {
                            await _fileManagerService.DeleteDirectoryAsync(item, true);
                        }
                        else if (File.Exists(item))
                        {
                            await _fileManagerService.DeleteFileAsync(item);
                        }
                    }

                    ScanStatus = $"Deleted {itemsToDelete.Count} item(s)";
                    await RefreshAsync();
                }
            }
            catch (Exception ex)
            {
                ScanStatus = $"Delete failed: {ex.Message}";
            }
        }

        /// <summary>
        /// Opens the selected item in file explorer.
        /// </summary>
        [RelayCommand]
        public void OpenItemLocation(string path)
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"/select,\"{path}\"",
                    UseShellExecute = true
                };
                System.Diagnostics.Process.Start(psi);
            }
            catch (Exception ex)
            {
                ScanStatus = $"Cannot open location: {ex.Message}";
            }
        }

        /// <summary>
        /// Exports the scan results.
        /// </summary>
        [RelayCommand]
        public async Task ExportResultsAsync()
        {
            try
            {
                var saveFileDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "CSV files (*.csv)|*.csv|JSON files (*.json)|*.json",
                    DefaultExt = ".csv",
                    FileName = $"SpaceAnalysis_{DateTime.Now:yyyyMMdd_HHmmss}"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    var extension = Path.GetExtension(saveFileDialog.FileName).ToLower();
                    
                    if (extension == ".csv")
                    {
                        await ExportToCsvAsync(saveFileDialog.FileName);
                    }
                    else if (extension == ".json")
                    {
                        await ExportToJsonAsync(saveFileDialog.FileName);
                    }
                    
                    ScanStatus = $"Results exported to {Path.GetFileName(saveFileDialog.FileName)}";
                }
            }
            catch (Exception ex)
            {
                ScanStatus = $"Export failed: {ex.Message}";
            }
        }

        /// <summary>
        /// Changes the view mode.
        /// </summary>
        [RelayCommand]
        public void SetViewMode(ViewMode mode)
        {
            CurrentView = mode;
            UpdateCharts();
        }

        /// <summary>
        /// Browses for a folder to scan.
        /// </summary>
        [RelayCommand]
        public void BrowseFolder()
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                SelectedPath = ScanPath,
                Description = "Select folder to analyze",
                ShowNewFolderButton = false
            };

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                ScanPath = dialog.SelectedPath;
            }
        }

        /// <summary>
        /// Processes scan results and populates collections.
        /// </summary>
        private async Task ProcessScanResultsAsync(ScanResult result)
        {
            await Task.Run(() =>
            {
                // Process top folders
                var topFolders = result.Folders
                    .Take(MaxItems)
                    .Select(f => new FolderSizeViewModel
                    {
                        FullPath = f.Path,
                        Name = Path.GetFileName(f.Path),
                        Size = f.Size,
                        FileCount = f.FileCount,
                        Percentage = TotalSize > 0 ? (double)f.Size / TotalSize * 100 : 0
                    })
                    .ToList();

                // Process largest files
                var largestFiles = result.Files
                    .Take(MaxItems)
                    .Select(f => new FileSizeViewModel
                    {
                        FullPath = f.Path,
                        Name = Path.GetFileName(f.Path),
                        Size = f.Size,
                        DateModified = f.ModifiedDate,
                        Percentage = TotalSize > 0 ? (double)f.Size / TotalSize * 100 : 0
                    })
                    .ToList();

                // Process file types
                var fileTypes = result.FileTypes
                    .Take(MaxItems)
                    .Select(t => new FileTypeViewModel
                    {
                        Extension = t.Extension,
                        Description = GetFileTypeDescription(t.Extension),
                        Count = t.Count,
                        Size = t.TotalSize,
                        Percentage = TotalSize > 0 ? (double)t.TotalSize / TotalSize * 100 : 0,
                        AverageSize = t.Count > 0 ? t.TotalSize / t.Count : 0
                    })
                    .ToList();

                // Update UI on dispatcher thread
                Application.Current.Dispatcher.Invoke(() =>
                {
                    TopFolders = new ObservableCollection<FolderSizeViewModel>(topFolders);
                    LargestFiles = new ObservableCollection<FileSizeViewModel>(largestFiles);
                    FileTypes = new ObservableCollection<FileTypeViewModel>(fileTypes);
                    
                    UpdateCharts();
                });
            });
        }

        /// <summary>
        /// Updates the chart data.
        /// </summary>
        private void UpdateCharts()
        {
            switch (CurrentView)
            {
                case ViewMode.PieChart:
                    UpdatePieChart();
                    break;
                case ViewMode.BarChart:
                    UpdateBarChart();
                    break;
                case ViewMode.TreeMap:
                    // TreeMap is handled by the view
                    break;
            }
        }

        /// <summary>
        /// Updates the pie chart.
        /// </summary>
        private void UpdatePieChart()
        {
            var data = FileTypes.Take(10).Select(t => new PieSeries<double>
            {
                Name = t.Description,
                Values = new double[] { t.Size / (1024.0 * 1024.0) }, // Convert to MB
                Fill = new SolidColorPaint(GetColorForType(t.Extension)),
                Stroke = null,
                InnerRadius = 0,
                MaxOuterRadius = 0.8f
            });

            PieSeries = data.ToArray();
        }

        /// <summary>
        /// Updates the bar chart.
        /// </summary>
        private void UpdateBarChart()
        {
            var data = TopFolders.Take(10).Select(t => new ColumnSeries<double>
            {
                Name = t.Name,
                Values = new double[] { t.Size / (1024.0 * 1024.0) }, // Convert to MB
                Stroke = null,
                Fill = new SolidColorPaint(SKColors.DodgerBlue)
            });

            BarSeries = data.ToArray();
            
            XAxes = new Axis[]
            {
                new Axis
                {
                    Labels = TopFolders.Take(10).Select(t => t.Name).ToArray(),
                    LabelsRotation = 45,
                    LabelsPadding = new Drawing.Padding(0, 0, 10, 0)
                }
            };
            
            YAxes = new Axis[]
            {
                new Axis
                {
                    LabelsRotation = 0,
                    Labeler = value => $"{value:F0} MB"
                }
            };
        }

        /// <summary>
        /// Gets color for file type.
        /// </summary>
        private static SKColor GetColorForType(string extension)
        {
            return extension.ToLowerInvariant() switch
            {
                ".txt" or ".log" => SKColors.LightBlue,
                ".pdf" => SKColors.Red,
                ".doc" or ".docx" => SKColors.Blue,
                ".xls" or ".xlsx" => SKColors.Green,
                ".jpg" or ".jpeg" or ".png" or ".gif" => SKColors.Orange,
                ".mp4" or ".avi" or ".mkv" => SKColors.Purple,
                ".mp3" or ".wav" or ".flac" => SKColors.Pink,
                ".zip" or ".rar" or ".7z" => SKColors.Gray,
                ".exe" or ".dll" or ".sys" => SKColors.DarkGray,
                _ => SKColors.LightGray
            };
        }

        /// <summary>
        /// Gets file type description.
        /// </summary>
        private static string GetFileTypeDescription(string extension)
        {
            return extension.ToLowerInvariant() switch
            {
                "" => "Unknown",
                ".txt" => "Text Files",
                ".pdf" => "PDF Documents",
                ".doc" or ".docx" => "Word Documents",
                ".xls" or ".xlsx" => "Excel Spreadsheets",
                ".jpg" or ".jpeg" => "JPEG Images",
                ".png" => "PNG Images",
                ".gif" => "GIF Images",
                ".mp4" => "MP4 Videos",
                ".avi" => "AVI Videos",
                ".mkv" => "MKV Videos",
                ".mp3" => "MP3 Audio",
                ".wav" => "WAV Audio",
                ".zip" => "ZIP Archives",
                ".rar" => "RAR Archives",
                ".exe" => "Executables",
                ".dll" => "Libraries",
                _ => $"{extension.ToUpper()} Files"
            };
        }

        /// <summary>
        /// Exports results to CSV.
        /// </summary>
        private async Task ExportToCsvAsync(string filePath)
        {
            await Task.Run(() =>
            {
                using var writer = new StreamWriter(filePath, false, System.Text.Encoding.UTF8);
                
                // Write summary
                await writer.WriteLineAsync("Space Analysis Summary");
                await writer.WriteLineAsync($"Scan Path,{ScanPath}");
                await writer.WriteLineAsync($"Total Size,{FormatSize(TotalSize)}");
                await writer.WriteLineAsync($"Total Files,{TotalFiles}");
                await writer.WriteLineAsync($"Total Folders,{TotalFolders}");
                await writer.WriteLineAsync();
                
                // Write top folders
                await writer.WriteLineAsync("Top Folders");
                await writer.WriteLineAsync("Folder,Size,Files,Percentage");
                foreach (var folder in TopFolders)
                {
                    await writer.WriteLineAsync($"\"{folder.Name}\",{folder.Size},{folder.FileCount},{folder.Percentage:F2}%");
                }
                await writer.WriteLineAsync();
                
                // Write largest files
                await writer.WriteLineAsync("Largest Files");
                await writer.WriteLineAsync("File,Size,Modified");
                foreach (var file in LargestFiles)
                {
                    await writer.WriteLineAsync($"\"{file.Name}\",{file.Size},{file.DateModified:yyyy-MM-dd HH:mm}");
                }
                await writer.WriteLineAsync();
                
                // Write file types
                await writer.WriteLineAsync("File Types");
                await writer.WriteLineAsync("Type,Count,Size,Average Size,Percentage");
                foreach (var type in FileTypes)
                {
                    await writer.WriteLineAsync($"\"{type.Description}\",{type.Count},{type.Size},{type.AverageSize},{type.Percentage:F2}%");
                }
            });
        }

        /// <summary>
        /// Exports results to JSON.
        /// </summary>
        private async Task ExportToJsonAsync(string filePath)
        {
            await Task.Run(() =>
            {
                var data = new
                {
                    ScanPath,
                    Timestamp = DateTime.Now,
                    Summary = new
                    {
                        TotalSize,
                        TotalFiles,
                        TotalFolders,
                        FreeSpace,
                        TotalSpace
                    },
                    TopFolders = TopFolders.Select(f => new
                    {
                        f.Name,
                        f.FullPath,
                        f.Size,
                        f.FileCount,
                        f.Percentage
                    }),
                    LargestFiles = LargestFiles.Select(f => new
                    {
                        f.Name,
                        f.FullPath,
                        f.Size,
                        f.DateModified,
                        f.Percentage
                    }),
                    FileTypes = FileTypes.Select(t => new
                    {
                        t.Extension,
                        t.Description,
                        t.Count,
                        t.Size,
                        t.AverageSize,
                        t.Percentage
                    })
                };

                var json = System.Text.Json.JsonSerializer.Serialize(data, new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true
                });

                File.WriteAllText(filePath, json);
            });
        }

        /// <summary>
        /// Formats file size in human readable format.
        /// </summary>
        private static string FormatSize(long bytes)
        {
            if (bytes == 0) return "0 B";
            
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
    /// View mode for space analyzer.
    /// </summary>
    public enum ViewMode
    {
        TreeMap,
        PieChart,
        BarChart,
        List
    }

    /// <summary>
    /// ViewModel for folder size information.
    /// </summary>
    public partial class FolderSizeViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _name = string.Empty;

        [ObservableProperty]
        private string _fullPath = string.Empty;

        [ObservableProperty]
        private long _size;

        [ObservableProperty]
        private int _fileCount;

        [ObservableProperty]
        private double _percentage;

        public string SizeFormatted => FormatSize(Size);
        public string PercentageFormatted => $"{Percentage:F1}%";

        private static string FormatSize(long bytes)
        {
            if (bytes == 0) return "0 B";
            
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
    /// ViewModel for file size information.
    /// </summary>
    public partial class FileSizeViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _name = string.Empty;

        [ObservableProperty]
        private string _fullPath = string.Empty;

        [ObservableProperty]
        private long _size;

        [ObservableProperty]
        private DateTime _dateModified;

        [ObservableProperty]
        private double _percentage;

        public string SizeFormatted => FormatSize(Size);
        public string PercentageFormatted => $"{Percentage:F2}%";

        private static string FormatSize(long bytes)
        {
            if (bytes == 0) return "0 B";
            
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
    /// ViewModel for file type statistics.
    /// </summary>
    public partial class FileTypeViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _extension = string.Empty;

        [ObservableProperty]
        private string _description = string.Empty;

        [ObservableProperty]
        private int _count;

        [ObservableProperty]
        private long _size;

        [ObservableProperty]
        private long _averageSize;

        [ObservableProperty]
        private double _percentage;

        public string SizeFormatted => FormatSize(Size);
        public string AverageSizeFormatted => FormatSize(AverageSize);
        public string PercentageFormatted => $"{Percentage:F1}%";

        private static string FormatSize(long bytes)
        {
            if (bytes == 0) return "0 B";
            
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
    /// ViewModel for time series data.
    /// </summary>
    public partial class TimeSeriesViewModel : ObservableObject
    {
        [ObservableProperty]
        private DateTime _timestamp;

        [ObservableProperty]
        private long _size;

        [ObservableProperty]
        private int _fileCount;
    }
}
