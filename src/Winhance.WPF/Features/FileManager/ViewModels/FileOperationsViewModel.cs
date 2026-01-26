using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Winhance.Core.Features.FileManager.Models;
using Winhance.Core.Features.FileManager.Interfaces;

namespace Winhance.WPF.Features.FileManager.ViewModels
{
    /// <summary>
    /// ViewModel for basic file operations
    /// </summary>
    public partial class FileOperationsViewModel : ObservableObject
    {
        private readonly IFileOperationsService _fileOperationsService;
        private readonly ILogger<FileOperationsViewModel> _logger;

        [ObservableProperty]
        private string _currentPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

        [ObservableProperty]
        private ObservableCollection<FileSystemItem> _items = new();

        [ObservableProperty]
        private FileSystemItem? _selectedItem;

        [ObservableProperty]
        private bool _isProcessing;

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        [ObservableProperty]
        private string _newItemName = string.Empty;

        [ObservableProperty]
        private ObservableCollection<FileSystemItem> _selectedItems = new();

        [ObservableProperty]
        private bool _isMultiSelect;

        public FileOperationsViewModel(IFileOperationsService fileOperationsService, ILogger<FileOperationsViewModel> logger)
        {
            _fileOperationsService = fileOperationsService;
            _logger = logger;
            _ = LoadDirectoryAsync();
        }

        private async Task LoadDirectoryAsync()
        {
            try
            {
                IsProcessing = true;
                StatusMessage = "Loading directory...";

                var items = await Task.Run(() =>
                {
                    var dirInfo = new DirectoryInfo(CurrentPath);
                    var list = new ObservableCollection<FileSystemItem>();

                    // Add directories
                    foreach (var dir in dirInfo.GetDirectories())
                    {
                        list.Add(new FileSystemItem
                        {
                            Name = dir.Name,
                            FullPath = dir.FullName,
                            IsDirectory = true,
                            Size = 0,
                            ModifiedDate = dir.LastWriteTime,
                            CreatedDate = dir.CreationTime
                        });
                    }

                    // Add files
                    foreach (var file in dirInfo.GetFiles())
                    {
                        list.Add(new FileSystemItem
                        {
                            Name = file.Name,
                            FullPath = file.FullName,
                            IsDirectory = false,
                            Size = file.Length,
                            ModifiedDate = file.LastWriteTime,
                            CreatedDate = file.CreationTime
                        });
                    }

                    return list;
                });

                Items = items;
                StatusMessage = $"Loaded {Items.Count} items";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading directory");
                StatusMessage = $"Error: {ex.Message}";
            }
            finally
            {
                IsProcessing = false;
            }
        }

        [RelayCommand]
        private async Task CopyItemAsync()
        {
            if (SelectedItem == null) return;

            try
            {
                IsProcessing = true;
                StatusMessage = $"Copying {SelectedItem.Name}...";

                var destination = Path.Combine(CurrentPath, $"Copy of {SelectedItem.Name}");
                
                if (SelectedItem.IsDirectory)
                {
                    // For directories, would need recursive copy
                    StatusMessage = "Directory copy not implemented yet";
                }
                else
                {
                    var result = await _fileOperationsService.CopyFileAsync(SelectedItem.FullPath, destination);
                    StatusMessage = result ? "File copied successfully" : "Failed to copy file";
                }

                if (StatusMessage.Contains("successfully"))
                {
                    await LoadDirectoryAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error copying item");
                StatusMessage = $"Error: {ex.Message}";
            }
            finally
            {
                IsProcessing = false;
            }
        }

        [RelayCommand]
        private async Task MoveItemAsync()
        {
            if (SelectedItem == null || string.IsNullOrEmpty(NewItemName)) return;

            try
            {
                IsProcessing = true;
                StatusMessage = $"Moving {SelectedItem.Name}...";

                var destination = Path.Combine(CurrentPath, NewItemName);
                
                var result = SelectedItem.IsDirectory
                    ? await _fileOperationsService.MoveDirectoryAsync(SelectedItem.FullPath, destination)
                    : await _fileOperationsService.MoveFileAsync(SelectedItem.FullPath, destination);

                StatusMessage = result ? "Item moved successfully" : "Failed to move item";

                if (result)
                {
                    NewItemName = string.Empty;
                    await LoadDirectoryAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error moving item");
                StatusMessage = $"Error: {ex.Message}";
            }
            finally
            {
                IsProcessing = false;
            }
        }

        [RelayCommand]
        private async Task MoveSelectedItemsAsync(string destinationPath)
        {
            if (!SelectedItems.Any()) return;

            try
            {
                IsProcessing = true;
                StatusMessage = $"Moving {SelectedItems.Count} items...";

                var sourcePaths = SelectedItems.Select(i => i.FullPath).ToArray();
                var result = await _fileOperationsService.MoveFilesAsync(sourcePaths, destinationPath);

                StatusMessage = result ? "Items moved successfully" : "Failed to move items";

                if (result)
                {
                    SelectedItems.Clear();
                    await LoadDirectoryAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error moving items");
                StatusMessage = $"Error: {ex.Message}";
            }
            finally
            {
                IsProcessing = false;
            }
        }

        [RelayCommand]
        private async Task DeleteItemAsync()
        {
            if (SelectedItem == null) return;

            try
            {
                IsProcessing = true;
                StatusMessage = $"Deleting {SelectedItem.Name}...";

                var result = SelectedItem.IsDirectory
                    ? await _fileOperationsService.DeleteDirectoryAsync(SelectedItem.FullPath, true)
                    : await _fileOperationsService.DeleteFileAsync(SelectedItem.FullPath);

                StatusMessage = result ? "Item deleted successfully" : "Failed to delete item";

                if (result)
                {
                    await LoadDirectoryAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting item");
                StatusMessage = $"Error: {ex.Message}";
            }
            finally
            {
                IsProcessing = false;
            }
        }

        [RelayCommand]
        private async Task RenameItemAsync()
        {
            if (SelectedItem == null || string.IsNullOrEmpty(NewItemName)) return;

            try
            {
                IsProcessing = true;
                StatusMessage = $"Renaming {SelectedItem.Name}...";

                var result = await _fileOperationsService.RenameFileAsync(SelectedItem.FullPath, NewItemName);
                StatusMessage = result ? "Item renamed successfully" : "Failed to rename item";

                if (result)
                {
                    NewItemName = string.Empty;
                    await LoadDirectoryAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error renaming item");
                StatusMessage = $"Error: {ex.Message}";
            }
            finally
            {
                IsProcessing = false;
            }
        }

        [RelayCommand]
        private async Task CreateFolderAsync()
        {
            if (string.IsNullOrEmpty(NewItemName)) return;

            try
            {
                IsProcessing = true;
                StatusMessage = "Creating folder...";

                var newPath = Path.Combine(CurrentPath, NewItemName);
                var result = await _fileOperationsService.CreateDirectoryAsync(newPath);
                StatusMessage = result ? "Folder created successfully" : "Failed to create folder";

                if (result)
                {
                    NewItemName = string.Empty;
                    await LoadDirectoryAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating folder");
                StatusMessage = $"Error: {ex.Message}";
            }
            finally
            {
                IsProcessing = false;
            }
        }

        [RelayCommand]
        private async Task NavigateToParentAsync()
        {
            try
            {
                var parent = Directory.GetParent(CurrentPath);
                if (parent != null)
                {
                    CurrentPath = parent.FullName;
                    await LoadDirectoryAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error navigating to parent");
                StatusMessage = $"Error: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task NavigateIntoAsync(FileSystemItem? item)
        {
            if (item == null || !item.IsDirectory) return;

            try
            {
                CurrentPath = item.FullPath;
                await LoadDirectoryAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error navigating into directory");
                StatusMessage = $"Error: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task RefreshAsync()
        {
            await LoadDirectoryAsync();
        }
    }
}
