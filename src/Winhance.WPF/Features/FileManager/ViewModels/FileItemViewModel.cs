using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Winhance.Core.Features.FileManager.Models;
using Winhance.Core.Features.FileManager.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Winhance.WPF.Features.FileManager.ViewModels
{
    /// <summary>
    /// ViewModel for a file or directory item in the file browser.
    /// </summary>
    public partial class FileItemViewModel : ObservableObject
    {
        private readonly FileItem _fileItem;
        private readonly IServiceProvider? _serviceProvider;
        private readonly ISelectionService? _selectionService;
        private readonly IFileManagerService? _fileManagerService;
        private readonly IPreviewService? _previewService;

        // Base properties from FileItem with ObservableProperty
        [ObservableProperty]
        private string _name = string.Empty;

        [ObservableProperty]
        private string _fullPath = string.Empty;

        [ObservableProperty]
        private long _size;

        [ObservableProperty]
        private DateTime _lastModified;

        [ObservableProperty]
        private DateTime _created;

        [ObservableProperty]
        private string _extension = string.Empty;

        [ObservableProperty]
        private FileAttributes _attributes;

        [ObservableProperty]
        private bool _isDirectory;

        [ObservableProperty]
        private bool _isHidden;

        [ObservableProperty]
        private bool _isSystem;

        [ObservableProperty]
        private bool _isReadOnly;

        [ObservableProperty]
        private bool _isParentDirectory;

        // UI-specific properties
        [ObservableProperty]
        private bool _isSelected;

        [ObservableProperty]
        private bool _isFocused;

        [ObservableProperty]
        private bool _isEditing;

        [ObservableProperty]
        private string _editName = string.Empty;

        [ObservableProperty]
        private bool _isRenaming;

        [ObservableProperty]
        private bool _isDragging;

        [ObservableProperty]
        private double _dragOpacity = 1.0;

        [ObservableProperty]
        private string _icon = "";

        [ObservableProperty]
        private string _typeDescription = string.Empty;

        [ObservableProperty]
        private string _sizeFormatted = string.Empty;

        [ObservableProperty]
        private string _dateFormatted = string.Empty;

        [ObservableProperty]
        private string _attributesFormatted = string.Empty;

        [ObservableProperty]
        private bool _hasPreview;

        [ObservableProperty]
        private ImageSource? _previewImage;

        [ObservableProperty]
        private string _searchMatch = string.Empty;

        [ObservableProperty]
        private bool _isSearchResult;

        public FileItemViewModel()
        {
            _fileItem = new FileItem();
        }

        public FileItemViewModel(IServiceProvider serviceProvider)
        {
            _fileItem = new FileItem();
            _serviceProvider = serviceProvider;
            _selectionService = serviceProvider.GetService<ISelectionService>();
            _fileManagerService = serviceProvider.GetService<IFileManagerService>();
            _previewService = serviceProvider.GetService<IPreviewService>();
        }

        public FileItemViewModel(FileInfo fileInfo, IServiceProvider? serviceProvider = null)
        {
            _serviceProvider = serviceProvider;
            _selectionService = serviceProvider?.GetService<ISelectionService>();
            _fileManagerService = serviceProvider?.GetService<IFileManagerService>();
            _previewService = serviceProvider?.GetService<IPreviewService>();

            _fileItem = new FileItem
            {
                Name = fileInfo.Name,
                FullPath = fileInfo.FullName,
                Size = fileInfo.Length,
                LastModified = fileInfo.LastWriteTime,
                Created = fileInfo.CreationTime,
                Extension = fileInfo.Extension,
                Attributes = fileInfo.Attributes,
                IsDirectory = false,
                IsHidden = (fileInfo.Attributes & FileAttributes.Hidden) != 0,
                IsSystem = (fileInfo.Attributes & FileAttributes.System) != 0,
                IsReadOnly = (fileInfo.Attributes & FileAttributes.ReadOnly) != 0,
                IsParentDirectory = false
            };

            InitializeProperties();
        }

        public FileItemViewModel(DirectoryInfo directoryInfo, bool isParentDirectory = false, IServiceProvider? serviceProvider = null)
        {
            _serviceProvider = serviceProvider;
            _selectionService = serviceProvider?.GetService<ISelectionService>();
            _fileManagerService = serviceProvider?.GetService<IFileManagerService>();
            _previewService = serviceProvider?.GetService<IPreviewService>();

            _fileItem = new FileItem
            {
                Name = isParentDirectory ? ".." : directoryInfo.Name,
                FullPath = isParentDirectory ? directoryInfo.Parent?.FullName ?? "" : directoryInfo.FullName,
                Size = 0,
                LastModified = directoryInfo.LastWriteTime,
                Created = directoryInfo.CreationTime,
                Extension = "",
                Attributes = directoryInfo.Attributes,
                IsDirectory = true,
                IsHidden = (directoryInfo.Attributes & FileAttributes.Hidden) != 0,
                IsSystem = (directoryInfo.Attributes & FileAttributes.System) != 0,
                IsReadOnly = (directoryInfo.Attributes & FileAttributes.ReadOnly) != 0,
                IsParentDirectory = isParentDirectory
            };

            InitializeProperties();
        }

        public FileItemViewModel(FileItem fileItem, IServiceProvider? serviceProvider = null)
        {
            _serviceProvider = serviceProvider;
            _selectionService = serviceProvider?.GetService<ISelectionService>();
            _fileManagerService = serviceProvider?.GetService<IFileManagerService>();
            _previewService = serviceProvider?.GetService<IPreviewService>();

            _fileItem = fileItem;
            InitializeProperties();
        }

        private void InitializeProperties()
        {
            _name = _fileItem.Name;
            _fullPath = _fileItem.FullPath;
            _size = _fileItem.Size;
            _lastModified = _fileItem.LastModified;
            _created = _fileItem.Created;
            _extension = _fileItem.Extension;
            _attributes = _fileItem.Attributes;
            _isDirectory = _fileItem.IsDirectory;
            _isHidden = _fileItem.IsHidden;
            _isSystem = _fileItem.IsSystem;
            _isReadOnly = _fileItem.IsReadOnly;
            _isParentDirectory = _fileItem.IsParentDirectory;

            _sizeFormatted = FormatSize(_size);
            _typeDescription = GetFileType(_extension);
            _dateFormatted = _lastModified.ToString("yyyy-MM-dd HH:mm:ss");
            _attributesFormatted = GetAttributesDescription(_attributes);
            _icon = GetIcon(_extension, _isDirectory);
            _hasPreview = _previewService?.HasPreview(_fileItem) ?? false;
            _previewImage = _previewService?.GetPreviewImage(_fileItem);
        }

        // Commands
        [RelayCommand]
        private void Select()
        {
            _selectionService?.AddToSelection(_fullPath);
        }

        [RelayCommand]
        private void Deselect()
        {
            _selectionService?.RemoveFromSelection(_fullPath);
        }

        [RelayCommand]
        private void BeginRename()
        {
            if (_isParentDirectory || _isReadOnly) return;
            
            IsRenaming = true;
            EditName = _name;
        }

        [RelayCommand]
        private async Task CommitRenameAsync()
        {
            if (!IsRenaming || string.IsNullOrWhiteSpace(EditName)) return;

            try
            {
                var newPath = Path.Combine(Path.GetDirectoryName(_fullPath) ?? "", EditName);
                
                if (_isDirectory)
                {
                    await _fileManagerService?.RenameDirectoryAsync(_fullPath, newPath)!;
                }
                else
                {
                    await _fileManagerService?.RenameFileAsync(_fullPath, newPath)!;
                }

                _name = EditName;
                _fullPath = newPath;
                _fileItem.Name = _name;
                _fileItem.FullPath = _fullPath;
            }
            catch (Exception ex)
            {
                // Show error to user
                System.Windows.MessageBox.Show($"Cannot rename: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsRenaming = false;
                EditName = string.Empty;
            }
        }

        [RelayCommand]
        private void CancelRename()
        {
            IsRenaming = false;
            EditName = string.Empty;
        }

        [RelayCommand]
        private async Task DeleteAsync()
        {
            if (_isParentDirectory) return;

            try
            {
                var result = System.Windows.MessageBox.Show(
                    $"Delete {_name}?",
                    "Confirm Delete",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    if (_isDirectory)
                    {
                        await _fileManagerService?.DeleteDirectoryAsync(_fullPath, false)!;
                    }
                    else
                    {
                        await _fileManagerService?.DeleteFileAsync(_fullPath)!;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Cannot delete: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void CopyPath()
        {
            try
            {
                System.Windows.Clipboard.SetText(_fullPath);
            }
            catch
            {
                // Handle clipboard error
            }
        }

        [RelayCommand]
        private void OpenInExplorer()
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"/select,\"{_fullPath}\"",
                    UseShellExecute = true
                };
                System.Diagnostics.Process.Start(psi);
            }
            catch
            {
                // Handle error
            }
        }

        [RelayCommand]
        private async Task OpenAsync()
        {
            try
            {
                if (_isDirectory)
                {
                    // Navigate to directory
                    await _fileManagerService?.NavigateToDirectoryAsync(_fullPath)!;
                }
                else
                {
                    // Open file with default application
                    var psi = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = _fullPath,
                        UseShellExecute = true
                    };
                    System.Diagnostics.Process.Start(psi);
                }
            }
            catch
                // Handle error
            }
        }

        [RelayCommand]
        private async Task ShowPropertiesAsync()
        {
            var properties = $"File Properties\n\n" +
                           $"Name: {Name}\n" +
                           $"Path: {FullPath}\n" +
                           $"Size: {FormattedSize}\n" +
                           $"Type: {Extension}\n" +
                           $"Created: {CreatedDate:g}\n" +
                           $"Modified: {ModifiedDate:g}\n" +
                           $"Accessed: {AccessedDate:g}";

            System.Windows.MessageBox.Show(
                properties,
                "Properties",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
            await Task.CompletedTask;
        }

        [RelayCommand]
        private async Task GeneratePreviewAsync()
        {
            if (!_hasPreview || _previewService == null) return;

            try
            {
                PreviewImage = await _previewService.GeneratePreviewAsync(_fileItem);
            }
            catch
            {
                // Handle preview generation error
            }
        }

        // Helper methods
        private static string GetIcon(string extension, bool isDirectory)
        {
            if (isDirectory)
            {
                return "📁";
            }

            return extension.ToLowerInvariant() switch
            {
                ".txt" => "📄",
                ".doc" or ".docx" => "📘",
                ".xls" or ".xlsx" => "📗",
                ".ppt" or ".pptx" => "📙",
                ".pdf" => "📕",
                ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" => "🖼️",
                ".mp4" or ".avi" or ".mkv" or ".mov" => "🎬",
                ".mp3" or ".wav" or ".flac" => "🎵",
                ".zip" or ".rar" or ".7z" => "📦",
                ".exe" => "⚙️",
                ".dll" or ".sys" => "🔧",
                ".ini" or ".cfg" or ".conf" => "⚙️",
                ".log" => "📋",
                _ => "📄"
            };
        }

        private static string GetAttributesDescription(FileAttributes attributes)
        {
            var attrs = new System.Text.StringBuilder();

            if ((attributes & FileAttributes.ReadOnly) != 0) attrs.Append("R ");
            if ((attributes & FileAttributes.Hidden) != 0) attrs.Append("H ");
            if ((attributes & FileAttributes.System) != 0) attrs.Append("S ");
            if ((attributes & FileAttributes.Archive) != 0) attrs.Append("A ");
            if ((attributes & FileAttributes.Compressed) != 0) attrs.Append("C ");
            if ((attributes & FileAttributes.Encrypted) != 0) attrs.Append("E ");

            return attrs.ToString().Trim();
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

        private static string GetFileType(string extension)
        {
            return extension.ToLower() switch
            {
                ".txt" => "Text File",
                ".doc" or ".docx" => "Word Document",
                ".xls" or ".xlsx" => "Excel Spreadsheet",
                ".ppt" or ".pptx" => "PowerPoint Presentation",
                ".pdf" => "PDF Document",
                ".jpg" or ".jpeg" => "JPEG Image",
                ".png" => "PNG Image",
                ".gif" => "GIF Image",
                ".bmp" => "Bitmap Image",
                ".mp4" => "MP4 Video",
                ".avi" => "AVI Video",
                ".mp3" => "MP3 Audio",
                ".wav" => "WAV Audio",
                ".zip" => "ZIP Archive",
                ".rar" => "RAR Archive",
                ".7z" => "7-Zip Archive",
                ".exe" => "Executable",
                ".dll" => "Dynamic Link Library",
                ".sys" => "System File",
                ".ini" or ".cfg" or ".conf" => "Configuration File",
                ".log" => "Log File",
                ".tmp" or ".temp" => "Temporary File",
                "" => "File",
                _ => $"{extension.ToUpper()} File"
            };
        }
    }
}
