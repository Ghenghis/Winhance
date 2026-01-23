using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Winhance.Core.Features.FileManager.Interfaces;

namespace Winhance.WPF.Features.FileManager.ViewModels
{
    /// <summary>
    /// ViewModel for the dual-pane file browser.
    /// </summary>
    public partial class DualPaneBrowserViewModel : ObservableObject
    {
        private readonly IFileManagerService? _fileManagerService;

        [ObservableProperty]
        private string _leftPanePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        [ObservableProperty]
        private string _rightPanePath = "C:\\";

        [ObservableProperty]
        private ObservableCollection<FileItemViewModel> _leftPaneItems = new();

        [ObservableProperty]
        private ObservableCollection<FileItemViewModel> _rightPaneItems = new();

        [ObservableProperty]
        private FileItemViewModel? _selectedLeftItem;

        [ObservableProperty]
        private FileItemViewModel? _selectedRightItem;

        [ObservableProperty]
        private ObservableCollection<FileItemViewModel> _selectedLeftItems = new();

        [ObservableProperty]
        private ObservableCollection<FileItemViewModel> _selectedRightItems = new();

        [ObservableProperty]
        private bool _isLeftPaneActive = true;

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private string _filterText = string.Empty;

        [ObservableProperty]
        private ObservableCollection<DriveItemViewModel> _drives = new();

        [ObservableProperty]
        private ObservableCollection<string> _leftPathHistory = new();

        [ObservableProperty]
        private ObservableCollection<string> _rightPathHistory = new();

        public DualPaneBrowserViewModel()
        {
            // Design-time constructor
            LoadDesignTimeData();
        }

        public DualPaneBrowserViewModel(IFileManagerService? fileManagerService)
        {
            _fileManagerService = fileManagerService;
            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            await LoadDrivesAsync();
            await RefreshAsync();
        }

        private void LoadDesignTimeData()
        {
            LeftPaneItems.Add(new FileItemViewModel { Name = "Documents", IsDirectory = true, Size = 0 });
            LeftPaneItems.Add(new FileItemViewModel { Name = "Downloads", IsDirectory = true, Size = 0 });
            LeftPaneItems.Add(new FileItemViewModel { Name = "report.pdf", IsDirectory = false, Size = 1024000 });
            
            RightPaneItems.Add(new FileItemViewModel { Name = "Program Files", IsDirectory = true, Size = 0 });
            RightPaneItems.Add(new FileItemViewModel { Name = "Windows", IsDirectory = true, Size = 0 });
        }

        [RelayCommand]
        private async Task LoadDrivesAsync()
        {
            Drives.Clear();
            
            if (_fileManagerService != null)
            {
                var drives = await _fileManagerService.GetDrivesAsync();
                foreach (var drive in drives.Where(d => d.IsReady))
                {
                    Drives.Add(new DriveItemViewModel
                    {
                        Name = drive.Name,
                        Label = string.IsNullOrEmpty(drive.Label) ? drive.Name : drive.Label,
                        TotalSize = drive.TotalSize,
                        FreeSpace = drive.FreeSpace,
                        DriveType = drive.DriveType
                    });
                }
            }
            else
            {
                // Fallback to System.IO
                foreach (var drive in System.IO.DriveInfo.GetDrives().Where(d => d.IsReady))
                {
                    Drives.Add(new DriveItemViewModel
                    {
                        Name = drive.Name,
                        Label = string.IsNullOrEmpty(drive.VolumeLabel) ? drive.Name : drive.VolumeLabel,
                        TotalSize = drive.TotalSize,
                        FreeSpace = drive.TotalFreeSpace,
                        DriveType = drive.DriveType.ToString()
                    });
                }
            }
        }

        [RelayCommand]
        public async Task RefreshAsync()
        {
            IsLoading = true;
            try
            {
                await LoadLeftPaneAsync();
                await LoadRightPaneAsync();
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task LoadLeftPaneAsync()
        {
            await LoadPaneAsync(LeftPanePath, LeftPaneItems, true);
        }

        [RelayCommand]
        private async Task LoadRightPaneAsync()
        {
            await LoadPaneAsync(RightPanePath, RightPaneItems, false);
        }

        private async Task LoadPaneAsync(string path, ObservableCollection<FileItemViewModel> items, bool isLeftPane)
        {
            items.Clear();

            try
            {
                if (_fileManagerService != null)
                {
                    var entries = await _fileManagerService.GetDirectoryContentsAsync(path);
                    foreach (var entry in entries.OrderByDescending(e => e.IsDirectory).ThenBy(e => e.Name))
                    {
                        items.Add(new FileItemViewModel
                        {
                            Name = entry.Name,
                            FullPath = entry.FullPath,
                            IsDirectory = entry.IsDirectory,
                            Size = entry.Size,
                            DateModified = entry.DateModified,
                            Extension = entry.Extension
                        });
                    }
                }
                else
                {
                    // Fallback to System.IO
                    var dirInfo = new DirectoryInfo(path);
                    
                    // Add parent directory entry
                    if (dirInfo.Parent != null)
                    {
                        items.Add(new FileItemViewModel
                        {
                            Name = "..",
                            FullPath = dirInfo.Parent.FullName,
                            IsDirectory = true,
                            IsParentDirectory = true
                        });
                    }

                    foreach (var dir in dirInfo.GetDirectories().OrderBy(d => d.Name))
                    {
                        try
                        {
                            items.Add(new FileItemViewModel
                            {
                                Name = dir.Name,
                                FullPath = dir.FullName,
                                IsDirectory = true,
                                DateModified = dir.LastWriteTime
                            });
                        }
                        catch (Exception) { /* Skip inaccessible directories */ }
                    }

                    foreach (var file in dirInfo.GetFiles().OrderBy(f => f.Name))
                    {
                        try
                        {
                            items.Add(new FileItemViewModel
                            {
                                Name = file.Name,
                                FullPath = file.FullName,
                                IsDirectory = false,
                                Size = file.Length,
                                DateModified = file.LastWriteTime,
                                Extension = file.Extension
                            });
                        }
                        catch (Exception) { /* Skip inaccessible files */ }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading {path}: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task NavigateLeftAsync(string path)
        {
            LeftPathHistory.Add(LeftPanePath);
            LeftPanePath = path;
            await LoadLeftPaneAsync();
        }

        [RelayCommand]
        private async Task NavigateRightAsync(string path)
        {
            RightPathHistory.Add(RightPanePath);
            RightPanePath = path;
            await LoadRightPaneAsync();
        }

        [RelayCommand]
        private async Task NavigateUpLeftAsync()
        {
            var parent = Directory.GetParent(LeftPanePath);
            if (parent != null)
            {
                await NavigateLeftAsync(parent.FullName);
            }
        }

        [RelayCommand]
        private async Task NavigateUpRightAsync()
        {
            var parent = Directory.GetParent(RightPanePath);
            if (parent != null)
            {
                await NavigateRightAsync(parent.FullName);
            }
        }

        [RelayCommand]
        private async Task GoBackLeftAsync()
        {
            if (LeftPathHistory.Count > 0)
            {
                var previousPath = LeftPathHistory.Last();
                LeftPathHistory.RemoveAt(LeftPathHistory.Count - 1);
                LeftPanePath = previousPath;
                await LoadLeftPaneAsync();
            }
        }

        [RelayCommand]
        private async Task GoBackRightAsync()
        {
            if (RightPathHistory.Count > 0)
            {
                var previousPath = RightPathHistory.Last();
                RightPathHistory.RemoveAt(RightPathHistory.Count - 1);
                RightPanePath = previousPath;
                await LoadRightPaneAsync();
            }
        }

        [RelayCommand]
        private async Task CopyToOtherPaneAsync()
        {
            var sourceItems = IsLeftPaneActive ? SelectedLeftItems : SelectedRightItems;
            var destPath = IsLeftPaneActive ? RightPanePath : LeftPanePath;

            if (sourceItems.Count == 0 || _fileManagerService == null) return;

            var sourcePaths = sourceItems.Select(i => i.FullPath).ToList();
            await _fileManagerService.CopyFilesAsync(sourcePaths, destPath);
            await RefreshAsync();
        }

        [RelayCommand]
        private async Task MoveToOtherPaneAsync()
        {
            var sourceItems = IsLeftPaneActive ? SelectedLeftItems : SelectedRightItems;
            var destPath = IsLeftPaneActive ? RightPanePath : LeftPanePath;

            if (sourceItems.Count == 0 || _fileManagerService == null) return;

            var sourcePaths = sourceItems.Select(i => i.FullPath).ToList();
            await _fileManagerService.MoveFilesAsync(sourcePaths, destPath);
            await RefreshAsync();
        }

        [RelayCommand]
        private async Task DeleteSelectedAsync()
        {
            var sourceItems = IsLeftPaneActive ? SelectedLeftItems : SelectedRightItems;
            if (sourceItems.Count == 0 || _fileManagerService == null) return;

            var sourcePaths = sourceItems.Select(i => i.FullPath).ToList();
            await _fileManagerService.DeleteFilesAsync(sourcePaths, permanent: false);
            await RefreshAsync();
        }

        [RelayCommand]
        private async Task CreateNewFolderAsync()
        {
            var currentPath = IsLeftPaneActive ? LeftPanePath : RightPanePath;
            if (_fileManagerService == null) return;

            var newFolderPath = Path.Combine(currentPath, "New Folder");
            await _fileManagerService.CreateDirectoryAsync(newFolderPath);
            await RefreshAsync();
        }

        partial void OnFilterTextChanged(string value)
        {
            // TODO: Implement filtering
        }
    }

    /// <summary>
    /// ViewModel for a file/folder item.
    /// </summary>
    public partial class FileItemViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _name = string.Empty;

        [ObservableProperty]
        private string _fullPath = string.Empty;

        [ObservableProperty]
        private bool _isDirectory;

        [ObservableProperty]
        private bool _isParentDirectory;

        [ObservableProperty]
        private long _size;

        [ObservableProperty]
        private DateTime _dateModified;

        [ObservableProperty]
        private string _extension = string.Empty;

        [ObservableProperty]
        private bool _isSelected;

        public string SizeDisplay => IsDirectory ? "" : FormatSize(Size);

        public string Icon => IsParentDirectory ? "ArrowUp" : (IsDirectory ? "Folder" : GetFileIcon(Extension));

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

        private static string GetFileIcon(string extension)
        {
            return extension.ToLower() switch
            {
                ".pdf" => "FilePdfBox",
                ".doc" or ".docx" => "FileWordBox",
                ".xls" or ".xlsx" => "FileExcelBox",
                ".ppt" or ".pptx" => "FilePowerpointBox",
                ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" => "FileImageBox",
                ".mp3" or ".wav" or ".flac" => "FileMusicBox",
                ".mp4" or ".mkv" or ".avi" => "FileVideoBox",
                ".zip" or ".rar" or ".7z" => "FolderZip",
                ".exe" => "Application",
                ".py" => "LanguagePython",
                ".cs" => "LanguageCsharp",
                ".js" or ".ts" => "LanguageJavascript",
                ".rs" => "LanguageRust",
                _ => "File"
            };
        }
    }

    /// <summary>
    /// ViewModel for a drive item.
    /// </summary>
    public partial class DriveItemViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _name = string.Empty;

        [ObservableProperty]
        private string _label = string.Empty;

        [ObservableProperty]
        private long _totalSize;

        [ObservableProperty]
        private long _freeSpace;

        [ObservableProperty]
        private string _driveType = string.Empty;

        public long UsedSpace => TotalSize - FreeSpace;
        public double UsedPercentage => TotalSize > 0 ? (double)UsedSpace / TotalSize * 100 : 0;
        public string SpaceDisplay => $"{FormatSize(FreeSpace)} free of {FormatSize(TotalSize)}";

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
