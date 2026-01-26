using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Winhance.Core.Features.FileManager.Interfaces;
using Winhance.Core.Features.FileManager.Models;
using Winhance.WPF.Features.FileManager.Base;
using Winhance.Infrastructure.Features.SelfHealing;

namespace Winhance.WPF.Features.FileManager.ViewModels
{
    /// <summary>
    /// File Copy Feature with integrated self-healing
    /// Automatically detects and fixes errors at runtime
    /// </summary>
    public partial class FileCopyViewModel : SelfHealingViewModelBase
    {
        private readonly IFileOperationsService _fileOperationsService;
        private readonly ObservableCollection<string> _copyHistory;

        [ObservableProperty]
        private string _sourcePath = string.Empty;

        [ObservableProperty]
        private string _destinationPath = string.Empty;

        [ObservableProperty]
        private bool _overwriteExisting = false;

        [ObservableProperty]
        private bool _preserveTimestamps = true;

        [ObservableProperty]
        private int _progressPercentage = 0;

        [ObservableProperty]
        private string _statusMessage = "Ready to copy files";

        [ObservableProperty]
        private bool _isCopying = false;

        [ObservableProperty]
        private long _totalBytesToCopy = 0;

        [ObservableProperty]
        private long _bytesCopied = 0;

        [ObservableProperty]
        private ObservableCollection<FileSystemItem> _selectedFiles = new();

        public FileCopyViewModel(
            IFileOperationsService fileOperationsService,
            ILogger<FileCopyViewModel> logger,
            SelfHealingSystem healingSystem) 
            : base(logger, healingSystem)
        {
            _fileOperationsService = fileOperationsService;
            _copyHistory = new ObservableCollection<string>();
            
            // Initialize with self-healing
            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            await ExecuteCommandWithHealingAsync("Initialize", async () =>
            {
                // Load copy history
                await LoadCopyHistoryAsync();
                
                // Set default paths
                SourcePath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                DestinationPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Copies");
                
                StatusMessage = "Initialized successfully";
            }, 55);
        }

        [RelayCommand]
        private async Task CopyFilesAsync()
        {
            await ExecuteCommandWithHealingAsync("CopyFiles", async () =>
            {
                // Validate inputs with auto-fix suggestions
                ValidateStringParameter(SourcePath, nameof(SourcePath), 82);
                ValidateStringParameter(DestinationPath, nameof(DestinationPath), 83);

                // Check source exists with auto-healing
                if (!await CheckFileExistsAsync(SourcePath, 86))
                {
                    StatusMessage = "Source path does not exist. Please check the path.";
                    return;
                }

                // Ensure destination directory exists
                await EnsureDestinationDirectoryAsync();

                // Start copy operation
                IsCopying = true;
                StatusMessage = "Starting copy operation...";
                ProgressPercentage = 0;

                try
                {
                    if (SelectedFiles.Any())
                    {
                        await CopySelectedFilesAsync();
                    }
                    else if (Directory.Exists(SourcePath))
                    {
                        await CopyDirectoryAsync();
                    }
                    else
                    {
                        await CopySingleFileAsync();
                    }

                    StatusMessage = $"Copy completed successfully. {_bytesCopied:N0} bytes copied.";
                    await AddToCopyHistoryAsync();
                }
                finally
                {
                    IsCopying = false;
                    ProgressPercentage = 0;
                }
            }, 115);
        }

        [RelayCommand]
        private async Task BrowseSourceAsync()
        {
            await ExecuteCommandWithHealingAsync("BrowseSource", async () =>
            {
                // Implementation for folder browser dialog
                StatusMessage = "Source browser would open here";
            }, 130);
        }

        [RelayCommand]
        private async Task BrowseDestinationAsync()
        {
            await ExecuteCommandWithHealingAsync("BrowseDestination", async () =>
            {
                // Implementation for folder browser dialog
                StatusMessage = "Destination browser would open here";
            }, 137);
        }

        [RelayCommand]
        private async Task ClearSelectionAsync()
        {
            await ExecuteCommandWithHealingAsync("ClearSelection", async () =>
            {
                SelectedFiles.Clear();
                StatusMessage = "Selection cleared";
            }, 144);
        }

        [RelayCommand]
        private async Task LoadFilesFromSourceAsync()
        {
            await ExecuteCommandWithHealingAsync("LoadFilesFromSource", async () =>
            {
                ValidateStringParameter(SourcePath, nameof(SourcePath), 152);

                if (!await CheckFileExistsAsync(SourcePath, 154))
                {
                    StatusMessage = "Source path is invalid";
                    return;
                }

                // Load files from source directory
                await LoadFilesAsync();
                StatusMessage = $"Loaded {SelectedFiles.Count} files from source";
            }, 158);
        }

        private async Task EnsureDestinationDirectoryAsync()
        {
            await ExecuteWithHealingAsync("EnsureDestination", async () =>
            {
                if (!Directory.Exists(DestinationPath))
                {
                    _logger.LogInformation("Creating destination directory: {Path}", DestinationPath);
                    Directory.CreateDirectory(DestinationPath);
                    StatusMessage = "Created destination directory";
                }
            }, 171);
        }

        private async Task CopySingleFileAsync()
        {
            await ExecuteWithHealingAsync("CopySingleFile", async () =>
            {
                var fileInfo = new FileInfo(SourcePath);
                TotalBytesToCopy = fileInfo.Length;
                BytesCopied = 0;

                StatusMessage = $"Copying {fileInfo.Name}...";

                var success = await _fileOperationsService.CopyFileAsync(SourcePath, DestinationPath, OverwriteExisting);
                
                if (success)
                {
                    BytesCopied = fileInfo.Length;
                    ProgressPercentage = 100;
                    
                    if (PreserveTimestamps)
                    {
                        await PreserveFileTimestampsAsync(SourcePath, Path.Combine(DestinationPath, Path.GetFileName(SourcePath)));
                    }
                }
                else
                {
                    throw new InvalidOperationException("Failed to copy file");
                }
            }, 196);
        }

        private async Task CopyDirectoryAsync()
        {
            await ExecuteWithHealingAsync("CopyDirectory", async () =>
            {
                var sourceDir = new DirectoryInfo(SourcePath);
                var files = sourceDir.GetFiles("*.*", SearchOption.AllDirectories);
                TotalBytesToCopy = files.Sum(f => f.Length);
                BytesCopied = 0;

                StatusMessage = $"Copying directory with {files.Length} files...";

                foreach (var file in files)
                {
                    var relativePath = Path.GetRelativePath(SourcePath, file.FullName);
                    var destPath = Path.Combine(DestinationPath, relativePath);
                    
                    var destDir = Path.GetDirectoryName(destPath);
                    if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
                    {
                        Directory.CreateDirectory(destDir);
                    }

                    var success = await _fileOperationsService.CopyFileAsync(file.FullName, destPath, OverwriteExisting);
                    
                    if (success)
                    {
                        BytesCopied += file.Length;
                        ProgressPercentage = (int)((BytesCopied * 100) / TotalBytesToCopy);
                        
                        if (PreserveTimestamps)
                        {
                            await PreserveFileTimestampsAsync(file.FullName, destPath);
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Failed to copy file: {File}", file.FullName);
                    }
                }
            }, 235);
        }

        private async Task CopySelectedFilesAsync()
        {
            await ExecuteWithHealingAsync("CopySelectedFiles", async () =>
            {
                TotalBytesToCopy = SelectedFiles.Sum(f => f.Size);
                BytesCopied = 0;

                StatusMessage = $"Copying {SelectedFiles.Count} selected files...";

                foreach (var file in SelectedFiles.Where(f => !f.IsDirectory))
                {
                    var fileName = Path.GetFileName(file.FullPath);
                    var destPath = Path.Combine(DestinationPath, fileName);

                    // Handle duplicate names
                    var counter = 1;
                    while (File.Exists(destPath) && !OverwriteExisting)
                    {
                        var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
                        var extension = Path.GetExtension(fileName);
                        destPath = Path.Combine(DestinationPath, $"{nameWithoutExt} ({counter}){extension}");
                        counter++;
                    }

                    var success = await _fileOperationsService.CopyFileAsync(file.FullPath, destPath, OverwriteExisting);
                    
                    if (success)
                    {
                        BytesCopied += file.Size;
                        ProgressPercentage = (int)((BytesCopied * 100) / TotalBytesToCopy);
                        
                        if (PreserveTimestamps)
                        {
                            await PreserveFileTimestampsAsync(file.FullPath, destPath);
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Failed to copy file: {File}", file.FullPath);
                    }
                }
            }, 275);
        }

        private async Task PreserveFileTimestampsAsync(string sourcePath, string destPath)
        {
            await ExecuteWithHealingAsync("PreserveTimestamps", async () =>
            {
                var sourceInfo = new FileInfo(sourcePath);
                var destInfo = new FileInfo(destPath);
                
                destInfo.CreationTime = sourceInfo.CreationTime;
                destInfo.LastWriteTime = sourceInfo.LastWriteTime;
                destInfo.LastAccessTime = sourceInfo.LastAccessTime;
            }, 298);
        }

        private async Task LoadFilesAsync()
        {
            await ExecuteWithHealingAsync("LoadFiles", async () =>
            {
                SelectedFiles.Clear();
                
                if (Directory.Exists(SourcePath))
                {
                    var dirInfo = new DirectoryInfo(SourcePath);
                    
                    foreach (var file in dirInfo.GetFiles())
                    {
                        SelectedFiles.Add(new FileSystemItem
                        {
                            Name = file.Name,
                            FullPath = file.FullName,
                            IsDirectory = false,
                            Size = file.Length,
                            ModifiedDate = file.LastWriteTime,
                            CreatedDate = file.CreationTime
                        });
                    }
                }
                else if (File.Exists(SourcePath))
                {
                    var fileInfo = new FileInfo(SourcePath);
                    SelectedFiles.Add(new FileSystemItem
                    {
                        Name = fileInfo.Name,
                        FullPath = fileInfo.FullName,
                        IsDirectory = false,
                        Size = fileInfo.Length,
                        ModifiedDate = fileInfo.LastWriteTime,
                        CreatedDate = fileInfo.CreationTime
                    });
                }
            }, 330);
        }

        private async Task LoadCopyHistoryAsync()
        {
            await ExecuteWithHealingAsync("LoadCopyHistory", async () =>
            {
                // Load copy history from settings
                _copyHistory.Clear();
                // Implementation would load from persistent storage
            }, 350);
        }

        private async Task AddToCopyHistoryAsync()
        {
            await ExecuteWithHealingAsync("AddToCopyHistory", async () =>
            {
                var historyEntry = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - {SourcePath} -> {DestinationPath}";
                _copyHistory.Insert(0, historyEntry);
                
                // Keep only last 50 entries
                while (_copyHistory.Count > 50)
                {
                    _copyHistory.RemoveAt(_copyHistory.Count - 1);
                }
                
                // Save to persistent storage
                // Implementation would save to settings
            }, 367);
        }

        protected override void Dispose()
        {
            // Save copy history before disposing
            _ = Task.Run(async () =>
            {
                try
                {
                    // Save history to persistent storage
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error saving copy history");
                }
            });
            
            base.Dispose();
        }
    }
}
