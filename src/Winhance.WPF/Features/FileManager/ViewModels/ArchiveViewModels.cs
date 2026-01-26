using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Winhance.Core.Features.FileManager.Interfaces;
using Winhance.Core.Features.FileManager.Models;

namespace Winhance.WPF.Features.FileManager.ViewModels
{
    /// <summary>
    /// ViewModel for archive creation
    /// </summary>
    public partial class ArchiveCreationViewModel : ObservableObject
    {
        private readonly IArchiveService _archiveService;
        private ObservableCollection<FileItem> _selectedFiles = new();
        private ObservableCollection<ArchiveFormat> _supportedFormats = new();

        [ObservableProperty]
        private string _archivePath = string.Empty;

        [ObservableProperty]
        private ArchiveFormat _selectedFormat;

        [ObservableProperty]
        private int _compressionLevel = 5;

        [ObservableProperty]
        private string? _password;

        [ObservableProperty]
        private bool _isEncrypting;

        [ObservableProperty]
        private long _estimatedSize;

        [ObservableProperty]
        private bool _isCreating;

        [ObservableProperty]
        private string? _creationStatus;

        public ObservableCollection<FileItem> SelectedFiles
        {
            get => _selectedFiles;
            set => SetProperty(ref _selectedFiles, value);
        }

        public ObservableCollection<ArchiveFormat> SupportedFormats
        {
            get => _supportedFormats;
            set => SetProperty(ref _supportedFormats, value);
        }

        public ArchiveCreationViewModel(IArchiveService archiveService)
        {
            _archiveService = archiveService;
            InitializeFormats();
        }

        private void InitializeFormats()
        {
            SupportedFormats.Add(new ArchiveFormat { Name = "ZIP", Extension = ".zip", SupportsPassword = true });
            SupportedFormats.Add(new ArchiveFormat { Name = "7z", Extension = ".7z", SupportsPassword = true });
            SupportedFormats.Add(new ArchiveFormat { Name = "RAR", Extension = ".rar", SupportsPassword = true });
            SupportedFormats.Add(new ArchiveFormat { Name = "TAR", Extension = ".tar", SupportsPassword = false });
            SupportedFormats.Add(new ArchiveFormat { Name = "TAR.GZ", Extension = ".tar.gz", SupportsPassword = false });

            SelectedFormat = SupportedFormats.First();
        }

        [RelayCommand]
        private async Task CreateArchiveAsync()
        {
            if (SelectedFiles.Count == 0 || string.IsNullOrEmpty(ArchivePath)) return;

            IsCreating = true;
            CreationStatus = "Creating archive...";

            try
            {
                var options = new ArchiveCreationOptions
                {
                    Format = SelectedFormat.Name,
                    CompressionLevel = CompressionLevel,
                    Password = IsEncrypting ? Password : null,
                    SplitSize = 0 // No splitting by default
                };

                var result = await _archiveService.CreateArchiveAsync(
                    ArchivePath,
                    SelectedFiles.Select(f => f.FullPath),
                    options);

                CreationStatus = $"Archive created successfully: {result.ArchivePath} ({result.Size} bytes)";
            }
            catch (Exception ex)
            {
                CreationStatus = $"Failed to create archive: {ex.Message}";
            }
            finally
            {
                IsCreating = false;
            }
        }

        [RelayCommand]
        private void BrowseForLocation()
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "ZIP Archive (*.zip)|*.zip|7z Archive (*.7z)|*.7z|RAR Archive (*.rar)|*.rar|TAR Archive (*.tar)|*.tar|TAR.GZ Archive (*.tar.gz)|*.tar.gz",
                DefaultExt = SelectedFormat.Extension,
                FileName = "Archive" + SelectedFormat.Extension
            };

            if (dialog.ShowDialog() == true)
            {
                ArchivePath = dialog.FileName;
            }
        }

        [RelayCommand]
        private void AddFiles()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Multiselect = true,
                Title = "Select Files to Add to Archive"
            };

            if (dialog.ShowDialog() == true)
            {
                foreach (var filePath in dialog.FileNames)
                {
                    var fileInfo = new FileInfo(filePath);
                    SelectedFiles.Add(new FileItem
                    {
                        Name = fileInfo.Name,
                        FullPath = fileInfo.FullName,
                        Size = fileInfo.Length,
                        ModifiedDate = fileInfo.LastWriteTime
                    });
                }
                UpdateEstimatedSize();
                CreationStatus = $"Added {dialog.FileNames.Length} files";
            }
        }

        [RelayCommand]
        private void RemoveFile(FileItem? file)
        {
            if (file == null) return;
            SelectedFiles.Remove(file);
            UpdateEstimatedSize();
        }

        [RelayCommand]
        private void ClearAllFiles()
        {
            SelectedFiles.Clear();
            UpdateEstimatedSize();
        }

        private void UpdateEstimatedSize()
        {
            EstimatedSize = SelectedFiles.Sum(f => f.Size);
        }
    }

    /// <summary>
    /// ViewModel for archive extraction
    /// </summary>
    public partial class ArchiveExtractionViewModel : ObservableObject
    {
        private readonly IArchiveService _archiveService;
        private ObservableCollection<ArchiveEntry> _archiveEntries = new();

        [ObservableProperty]
        private string _archivePath = string.Empty;

        [ObservableProperty]
        private string _extractPath = string.Empty;

        [ObservableProperty]
        private string? _password;

        [ObservableProperty]
        private bool _isExtracting;

        [ObservableProperty]
        private string? _extractionStatus;

        [ObservableProperty]
        private ArchiveInfo _archiveInfo = new();

        public ObservableCollection<ArchiveEntry> ArchiveEntries
        {
            get => _archiveEntries;
            set => SetProperty(ref _archiveEntries, value);
        }

        public ArchiveExtractionViewModel(IArchiveService archiveService)
        {
            _archiveService = archiveService;
        }

        [RelayCommand]
        private async Task LoadArchiveAsync(string? archivePath)
        {
            if (string.IsNullOrEmpty(archivePath)) return;

            ArchivePath = archivePath;

            try
            {
                var info = await _archiveService.GetArchiveInfoAsync(archivePath);
                ArchiveInfo = info;

                ArchiveEntries.Clear();
                foreach (var entry in info.Entries)
                {
                    ArchiveEntries.Add(entry);
                }
            }
            catch (Exception ex)
            {
                ExtractionStatus = $"Failed to load archive: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task ExtractAllAsync()
        {
            if (string.IsNullOrEmpty(ArchivePath) || string.IsNullOrEmpty(ExtractPath)) return;

            IsExtracting = true;
            ExtractionStatus = "Extracting...";

            try
            {
                var options = new ArchiveExtractionOptions
                {
                    Password = string.IsNullOrEmpty(Password) ? null : Password,
                    OverwriteExisting = true
                };

                var result = await _archiveService.ExtractArchiveAsync(
                    ArchivePath,
                    ExtractPath,
                    options);

                ExtractionStatus = $"Extracted {result.ExtractedCount} files to {ExtractPath}";
            }
            catch (Exception ex)
            {
                ExtractionStatus = $"Extraction failed: {ex.Message}";
            }
            finally
            {
                IsExtracting = false;
            }
        }

        [RelayCommand]
        private async Task ExtractSelectedAsync()
        {
            var selectedEntries = ArchiveEntries.Where(e => e.IsSelected).ToList();
            if (selectedEntries.Count == 0) return;

            IsExtracting = true;
            ExtractionStatus = "Extracting selected files...";

            try
            {
                var options = new ArchiveExtractionOptions
                {
                    Password = string.IsNullOrEmpty(Password) ? null : Password,
                    OverwriteExisting = true,
                    SelectedFiles = selectedEntries.Select(e => e.FullPath).ToList()
                };

                var result = await _archiveService.ExtractArchiveAsync(
                    ArchivePath,
                    ExtractPath,
                    options);

                ExtractionStatus = $"Extracted {result.ExtractedCount} files to {ExtractPath}";
            }
            catch (Exception ex)
            {
                ExtractionStatus = $"Extraction failed: {ex.Message}";
            }
            finally
            {
                IsExtracting = false;
            }
        }

        [RelayCommand]
        private void BrowseForArchive()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Archive Files (*.zip;*.7z;*.rar;*.tar;*.tar.gz)|*.zip;*.7z;*.rar;*.tar;*.tar.gz|All Files (*.*)|*.*",
                Title = "Select Archive to Extract"
            };

            if (dialog.ShowDialog() == true)
            {
                _ = LoadArchiveAsync(dialog.FileName);
            }
        }

        [RelayCommand]
        private void BrowseForExtractLocation()
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Select Extract Location"
            };

            if (dialog.ShowDialog() == true)
            {
                ExtractPath = dialog.FolderName;
            }
        }

        [RelayCommand]
        private void SelectAll()
        {
            foreach (var entry in ArchiveEntries)
            {
                entry.IsSelected = true;
            }
        }

        [RelayCommand]
        private void DeselectAll()
        {
            foreach (var entry in ArchiveEntries)
            {
                entry.IsSelected = false;
            }
        }
    }

    /// <summary>
    /// ViewModel for archive management
    /// </summary>
    public partial class ArchiveManagementViewModel : ObservableObject
    {
        private readonly IArchiveService _archiveService;
        private ObservableCollection<ArchiveEntry> _archiveEntries = new();

        [ObservableProperty]
        private string _archivePath = string.Empty;

        [ObservableProperty]
        private bool _isModified;

        [ObservableProperty]
        private string? _operationStatus;

        public ObservableCollection<ArchiveEntry> ArchiveEntries
        {
            get => _archiveEntries;
            set => SetProperty(ref _archiveEntries, value);
        }

        public ArchiveManagementViewModel(IArchiveService archiveService)
        {
            _archiveService = archiveService;
        }

        [RelayCommand]
        private async Task LoadArchiveAsync(string? archivePath)
        {
            if (string.IsNullOrEmpty(archivePath)) return;

            ArchivePath = archivePath;
            IsModified = false;

            try
            {
                var info = await _archiveService.GetArchiveInfoAsync(archivePath);
                
                ArchiveEntries.Clear();
                foreach (var entry in info.Entries)
                {
                    ArchiveEntries.Add(entry);
                }
            }
            catch (Exception ex)
            {
                OperationStatus = $"Failed to load archive: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task AddFilesAsync(string[] filePaths)
        {
            if (filePaths.Length == 0) return;

            try
            {
                await _archiveService.AddFilesAsync(ArchivePath, filePaths);
                await LoadArchiveAsync(ArchivePath); // Reload
                IsModified = true;
                OperationStatus = $"Added {filePaths.Length} files to archive";
            }
            catch (Exception ex)
            {
                OperationStatus = $"Failed to add files: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task RemoveSelectedAsync()
        {
            var selectedEntries = ArchiveEntries.Where(e => e.IsSelected).ToList();
            if (selectedEntries.Count == 0) return;

            try
            {
                await _archiveService.RemoveFilesAsync(ArchivePath, selectedEntries.Select(e => e.FullPath));
                await LoadArchiveAsync(ArchivePath); // Reload
                IsModified = true;
                OperationStatus = $"Removed {selectedEntries.Count} files from archive";
            }
            catch (Exception ex)
            {
                OperationStatus = $"Failed to remove files: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task TestArchiveAsync()
        {
            try
            {
                var result = await _archiveService.TestArchiveAsync(ArchivePath);
                OperationStatus = result.IsValid ? "Archive is valid" : $"Archive is corrupted: {result.ErrorMessage}";
            }
            catch (Exception ex)
            {
                OperationStatus = $"Test failed: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task RepairArchiveAsync()
        {
            try
            {
                var repairedPath = await _archiveService.RepairArchiveAsync(ArchivePath);
                OperationStatus = $"Archive repaired: {repairedPath}";
            }
            catch (Exception ex)
            {
                OperationStatus = $"Repair failed: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task ConvertFormatAsync(string newFormat)
        {
            try
            {
                var newPath = Path.ChangeExtension(ArchivePath, newFormat.ToLower());
                await _archiveService.ConvertArchiveAsync(ArchivePath, newPath, newFormat);
                OperationStatus = $"Archive converted to {newFormat}: {newPath}";
            }
            catch (Exception ex)
            {
                OperationStatus = $"Conversion failed: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task SaveChangesAsync()
        {
            try
            {
                // Archives are typically saved immediately
                IsModified = false;
                OperationStatus = "Archive saved";
            }
            catch (Exception ex)
            {
                OperationStatus = $"Save failed: {ex.Message}";
            }
        }
    }

    // Model classes
    public class ArchiveFormat
    {
        public string Name { get; set; } = string.Empty;
        public string Extension { get; set; } = string.Empty;
        public bool SupportsPassword { get; set; }
        public int MaxCompressionLevel { get; set; } = 9;
    }

    public class ArchiveEntry
    {
        public string Name { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public long Size { get; set; }
        public long CompressedSize { get; set; }
        public DateTime ModifiedDate { get; set; }
        public bool IsDirectory { get; set; }
        public bool IsSelected { get; set; }
        public string CompressionRatio => Size > 0 ? $"{(1.0 - (double)CompressedSize / Size) * 100:F1}%" : "0%";
    }

    public class ArchiveInfo
    {
        public string Format { get; set; } = string.Empty;
        public long TotalSize { get; set; }
        public long CompressedSize { get; set; }
        public int FileCount { get; set; }
        public int DirectoryCount { get; set; }
        public bool IsEncrypted { get; set; }
        public bool HasPassword { get; set; }
        public string? Comment { get; set; }
        public ObservableCollection<ArchiveEntry> Entries { get; set; } = new();
    }

    public class ArchiveCreationOptions
    {
        public string Format { get; set; } = string.Empty;
        public int CompressionLevel { get; set; } = 5;
        public string? Password { get; set; }
        public long SplitSize { get; set; }
        public bool IncludeHiddenFiles { get; set; } = true;
        public bool PreserveAttributes { get; set; } = true;
        public bool FollowSymlinks { get; set; } = false;
    }

    public class ArchiveExtractionOptions
    {
        public string? Password { get; set; }
        public bool OverwriteExisting { get; set; } = false;
        public bool PreservePermissions { get; set; } = true;
        public List<string>? SelectedFiles { get; set; }
        public string? ExtractToPath { get; set; }
    }

    public class ArchiveOperationResult
    {
        public bool Success { get; set; }
        public string? ArchivePath { get; set; }
        public long Size { get; set; }
        public int ExtractedCount { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public class ArchiveTestResult
    {
        public bool IsValid { get; set; }
        public string? ErrorMessage { get; set; }
        public List<string> CorruptedFiles { get; set; } = new();
    }
}
