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
    /// ViewModel for basic file operations
    /// </summary>
    public partial class BasicFileOperationsViewModel : ObservableObject
    {
        private readonly IFileManagerService _fileManagerService;
        private readonly IClipboardService _clipboardService;
        private readonly ISelectionService _selectionService;

        [ObservableProperty]
        private ObservableCollection<FileItem> _selectedItems = new();

        [ObservableProperty]
        private bool _isOperationInProgress;

        [ObservableProperty]
        private string? _operationStatus;

        public BasicFileOperationsViewModel(
            IFileManagerService fileManagerService,
            IClipboardService clipboardService,
            ISelectionService selectionService)
        {
            _fileManagerService = fileManagerService;
            _clipboardService = clipboardService;
            _selectionService = selectionService;
        }

        [RelayCommand]
        private async Task CopyAsync()
        {
            if (SelectedItems.Count == 0) return;

            IsOperationInProgress = true;
            OperationStatus = "Copying...";

            try
            {
                await _clipboardService.CopyToClipboardAsync(SelectedItems.ToList());
                OperationStatus = $"Copied {SelectedItems.Count} item(s) to clipboard";
            }
            catch (Exception ex)
            {
                OperationStatus = $"Copy failed: {ex.Message}";
            }
            finally
            {
                IsOperationInProgress = false;
            }
        }

        [RelayCommand]
        private async Task CutAsync()
        {
            if (SelectedItems.Count == 0) return;

            IsOperationInProgress = true;
            OperationStatus = "Cutting...";

            try
            {
                await _clipboardService.CutToClipboardAsync(SelectedItems.ToList());
                OperationStatus = $"Cut {SelectedItems.Count} item(s) to clipboard";
            }
            catch (Exception ex)
            {
                OperationStatus = $"Cut failed: {ex.Message}";
            }
            finally
            {
                IsOperationInProgress = false;
            }
        }

        [RelayCommand]
        private async Task PasteAsync(string destinationPath)
        {
            IsOperationInProgress = true;
            OperationStatus = "Pasting...";

            try
            {
                var result = await _clipboardService.PasteFromClipboardAsync(destinationPath);
                OperationStatus = $"Pasted {result.SuccessCount} item(s)";
            }
            catch (Exception ex)
            {
                OperationStatus = $"Paste failed: {ex.Message}";
            }
            finally
            {
                IsOperationInProgress = false;
            }
        }

        [RelayCommand]
        private async Task DeleteAsync()
        {
            if (SelectedItems.Count == 0) return;

            IsOperationInProgress = true;
            OperationStatus = "Deleting...";

            try
            {
                await _fileManagerService.DeleteAsync(SelectedItems.Select(i => i.FullPath));
                OperationStatus = $"Deleted {SelectedItems.Count} item(s)";
                SelectedItems.Clear();
            }
            catch (Exception ex)
            {
                OperationStatus = $"Delete failed: {ex.Message}";
            }
            finally
            {
                IsOperationInProgress = false;
            }
        }

        [RelayCommand]
        private async Task PermanentDeleteAsync()
        {
            if (SelectedItems.Count == 0) return;

            IsOperationInProgress = true;
            OperationStatus = "Permanently deleting...";

            try
            {
                await _fileManagerService.PermanentDeleteAsync(SelectedItems.Select(i => i.FullPath));
                OperationStatus = $"Permanently deleted {SelectedItems.Count} item(s)";
                SelectedItems.Clear();
            }
            catch (Exception ex)
            {
                OperationStatus = $"Delete failed: {ex.Message}";
            }
            finally
            {
                IsOperationInProgress = false;
            }
        }

        [RelayCommand]
        private async Task RenameAsync(FileItem? item)
        {
            if (item == null) return;

            var newName = Microsoft.VisualBasic.Interaction.InputBox(
                "Enter new name:",
                "Rename",
                System.IO.Path.GetFileName(item.FullPath));

            if (string.IsNullOrWhiteSpace(newName)) return;
            
            try
            {
                await _fileManagerService.RenameAsync(item.FullPath, newName);
                OperationStatus = $"Renamed to {newName}";
            }
            catch (Exception ex)
            {
                OperationStatus = $"Rename failed: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task CreateFolderAsync(string parentPath)
        {
            IsOperationInProgress = true;
            OperationStatus = "Creating folder...";

            try
            {
                var folderName = "New Folder";
                var folderPath = System.IO.Path.Combine(parentPath, folderName);
                
                // Ensure unique name
                int counter = 1;
                while (System.IO.Directory.Exists(folderPath))
                {
                    folderPath = System.IO.Path.Combine(parentPath, $"{folderName} ({counter})");
                    counter++;
                }

                await _fileManagerService.CreateDirectoryAsync(folderPath);
                OperationStatus = $"Created folder: {System.IO.Path.GetFileName(folderPath)}";
            }
            catch (Exception ex)
            {
                OperationStatus = $"Create folder failed: {ex.Message}";
            }
            finally
            {
                IsOperationInProgress = false;
            }
        }

        [RelayCommand]
        private async Task CreateFileAsync(string parentPath)
        {
            IsOperationInProgress = true;
            OperationStatus = "Creating file...";

            try
            {
                var fileName = "New File.txt";
                var filePath = System.IO.Path.Combine(parentPath, fileName);
                
                // Ensure unique name
                int counter = 1;
                while (System.IO.File.Exists(filePath))
                {
                    filePath = System.IO.Path.Combine(parentPath, $"New File ({counter}).txt");
                    counter++;
                }

                await _fileManagerService.CreateFileAsync(filePath);
                OperationStatus = $"Created file: {System.IO.Path.GetFileName(filePath)}";
            }
            catch (Exception ex)
            {
                OperationStatus = $"Create file failed: {ex.Message}";
            }
            finally
            {
                IsOperationInProgress = false;
            }
        }
    }

    /// <summary>
    /// ViewModel for advanced file operations
    /// </summary>
    public partial class AdvancedFileOperationsViewModel : ObservableObject
    {
        private readonly IAdvancedFileOperations _advancedOperations;

        public AdvancedFileOperationsViewModel(IAdvancedFileOperations advancedOperations)
        {
            _advancedOperations = advancedOperations;
        }

        [RelayCommand]
        private async Task CreateHardLinkAsync(string targetPath, string linkPath)
        {
            try
            {
                await _advancedOperations.CreateHardLinkAsync(targetPath, linkPath);
                System.Windows.MessageBox.Show(
                    "Hard link created successfully.",
                    "Success",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to create hard link: {ex.Message}",
                    "Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task CreateSymbolicLinkAsync(string targetPath, string linkPath)
        {
            try
            {
                await _advancedOperations.CreateSymbolicLinkAsync(targetPath, linkPath);
                System.Windows.MessageBox.Show(
                    "Symbolic link created successfully.",
                    "Success",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to create symbolic link: {ex.Message}",
                    "Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task CreateJunctionAsync(string targetPath, string junctionPath)
        {
            try
            {
                await _advancedOperations.CreateJunctionAsync(targetPath, junctionPath);
                System.Windows.MessageBox.Show(
                    "Junction created successfully.",
                    "Success",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to create junction: {ex.Message}",
                    "Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task EditFileAttributesAsync(string filePath)
        {
            try
            {
                var attributes = await _advancedOperations.GetFileAttributesAsync(filePath);
                var message = $"File Attributes:\n\n" +
                             $"Archive: {attributes.IsArchive}\n" +
                             $"Hidden: {attributes.IsHidden}\n" +
                             $"Read-Only: {attributes.IsReadOnly}\n" +
                             $"System: {attributes.IsSystem}";
                
                System.Windows.MessageBox.Show(message, "File Attributes",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to get file attributes: {ex.Message}",
                    "Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task EditTimestampsAsync(string filePath)
        {
            try
            {
                var timestamps = await _advancedOperations.GetFileTimestampsAsync(filePath);
                var message = $"File Timestamps:\n\n" +
                             $"Created: {timestamps.Created:g}\n" +
                             $"Modified: {timestamps.Modified:g}\n" +
                             $"Accessed: {timestamps.Accessed:g}";
                
                System.Windows.MessageBox.Show(message, "File Timestamps",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to get file timestamps: {ex.Message}",
                    "Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task SplitLargeFileAsync(string filePath, long chunkSize)
        {
            try
            {
                await _advancedOperations.SplitFileAsync(filePath, chunkSize);
                System.Windows.MessageBox.Show(
                    $"File split successfully into chunks of {chunkSize / (1024 * 1024)} MB.",
                    "Success",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to split file: {ex.Message}",
                    "Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task JoinSplitFilesAsync(string[] parts, string outputPath)
        {
            try
            {
                await _advancedOperations.JoinFilesAsync(parts, outputPath);
                System.Windows.MessageBox.Show(
                    $"Files joined successfully to {outputPath}",
                    "Success",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to join files: {ex.Message}",
                    "Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task CalculateChecksumAsync(string filePath, string algorithm)
        {
            try
            {
                var checksum = await _advancedOperations.CalculateChecksumAsync(filePath, algorithm);
                System.Windows.MessageBox.Show(
                    $"{algorithm} Checksum:\n\n{checksum}",
                    "Checksum Result",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to calculate checksum: {ex.Message}",
                    "Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task VerifyChecksumAsync(string filePath, string expectedChecksum, string algorithm)
        {
            try
            {
                var isValid = await _advancedOperations.VerifyChecksumAsync(filePath, expectedChecksum, algorithm);
                var message = isValid 
                    ? "Checksum verification passed!" 
                    : "Checksum verification failed! File may be corrupted.";
                var icon = isValid 
                    ? System.Windows.MessageBoxImage.Information 
                    : System.Windows.MessageBoxImage.Warning;
                
                System.Windows.MessageBox.Show(message, "Verification Result",
                    System.Windows.MessageBoxButton.OK, icon);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Verification failed: {ex.Message}",
                    "Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task SecureDeleteAsync(string filePath)
        {
            try
            {
                await _advancedOperations.SecureDeleteAsync(filePath);
                System.Windows.MessageBox.Show(
                    "File securely deleted (data overwritten).",
                    "Success",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Secure delete failed: {ex.Message}",
                    "Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task WipeFreeSpaceAsync(string drivePath)
        {
            try
            {
                await _advancedOperations.WipeFreeSpaceAsync(drivePath);
                System.Windows.MessageBox.Show(
                    "Free space wiped successfully.",
                    "Success",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Free space wipe failed: {ex.Message}",
                    "Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }
    }

    /// <summary>
    /// ViewModel for batch operations
    /// </summary>
    public partial class BatchOperationsViewModel : ObservableObject
    {
        private readonly IBatchRenameService _batchRenameService;
        private ObservableCollection<FileItem> _selectedItems = new();
        private ObservableCollection<RenamePattern> _renamePatterns = new();

        public ObservableCollection<FileItem> SelectedItems
        {
            get => _selectedItems;
            set => SetProperty(ref _selectedItems, value);
        }

        public ObservableCollection<RenamePattern> RenamePatterns
        {
            get => _renamePatterns;
            set => SetProperty(ref _renamePatterns, value);
        }

        public BatchOperationsViewModel(IBatchRenameService batchRenameService)
        {
            _batchRenameService = batchRenameService;
            LoadDefaultPatterns();
        }

        private void LoadDefaultPatterns()
        {
            RenamePatterns.Add(new RenamePattern { Name = "Numbered", Pattern = "{0} {1}", Description = "Base Name + Number" });
            RenamePatterns.Add(new RenamePattern { Name = "Date", Pattern = "{1:yyyy-MM-dd}", Description = "Date format" });
            RenamePatterns.Add(new RenamePattern { Name = "Extension", Pattern = "{1}_{0}", Description = "Extension + Name" });
        }

        [RelayCommand]
        private async Task PreviewBatchRenameAsync(RenamePattern? pattern)
        {
            if (pattern == null || SelectedItems.Count == 0) return;

            try
            {
                var preview = await _batchRenameService.PreviewRenameAsync(
                    SelectedItems.Select(i => i.FullPath),
                    pattern.Pattern);

                var message = string.Join("\n", preview.Select((p, i) => $"{i + 1}. {p.OldName} → {p.NewName}"));
                System.Windows.MessageBox.Show(
                    $"Rename Preview:\n\n{message}",
                    "Batch Rename Preview",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Preview failed: {ex.Message}",
                    "Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task ExecuteBatchRenameAsync(RenamePattern? pattern)
        {
            if (pattern == null || SelectedItems.Count == 0) return;

            try
            {
                await _batchRenameService.ExecuteRenameAsync(
                    SelectedItems.Select(i => i.FullPath),
                    pattern.Pattern);
                System.Windows.MessageBox.Show(
                    $"Successfully renamed {SelectedItems.Count} item(s).",
                    "Batch Rename Complete",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Batch rename failed: {ex.Message}",
                    "Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task SavePresetAsync(RenamePattern? pattern)
        {
            if (pattern == null) return;

            try
            {
                await _batchRenameService.SavePresetAsync(pattern);
                System.Windows.MessageBox.Show(
                    $"Preset '{pattern.Name}' saved successfully.",
                    "Preset Saved",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to save preset: {ex.Message}",
                    "Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task LoadPresetsAsync()
        {
            try
            {
                var presets = await _batchRenameService.GetPresetsAsync();
                foreach (var preset in presets)
                {
                    RenamePatterns.Add(preset);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to load presets: {ex.Message}",
                    "Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }
    }

    /// <summary>
    /// Rename pattern model
    /// </summary>
    public class RenamePattern
    {
        public string Name { get; set; } = string.Empty;
        public string Pattern { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    /// <summary>
    /// ViewModel for view controls
    /// </summary>
    public partial class ViewControlsViewModel : ObservableObject
    {
        private readonly IViewModeService _viewModeService;
        private readonly ISortingService _sortingService;
        private readonly IColumnService _columnService;

        [ObservableProperty]
        private ViewMode _currentViewMode = ViewMode.Details;

        [ObservableProperty]
        private SortColumn _currentSortColumn = SortColumn.Name;

        [ObservableProperty]
        private SortDirection _sortDirection = SortDirection.Ascending;

        public ViewControlsViewModel(
            IViewModeService viewModeService,
            ISortingService sortingService,
            IColumnService columnService)
        {
            _viewModeService = viewModeService;
            _sortingService = sortingService;
            _columnService = columnService;
        }

        [RelayCommand]
        private async Task SetViewModeAsync(ViewMode viewMode)
        {
            CurrentViewMode = viewMode;
            await _viewModeService.SetViewModeAsync(viewMode);
        }

        [RelayCommand]
        private async Task SortByColumnAsync(SortColumn column)
        {
            if (CurrentSortColumn == column)
            {
                SortDirection = SortDirection == SortDirection.Ascending 
                    ? SortDirection.Descending 
                    : SortDirection.Ascending;
            }
            else
            {
                CurrentSortColumn = column;
                SortDirection = SortDirection.Ascending;
            }

            await _sortingService.SetSortingAsync(column, SortDirection);
        }

        [RelayCommand]
        private async Task ToggleColumnAsync(string columnName)
        {
            var column = await _columnService.GetColumnAsync(columnName);
            if (column != null)
            {
                column.IsVisible = !column.IsVisible;
                await _columnService.UpdateColumnAsync(column);
            }
        }

        [RelayCommand]
        private async Task SaveViewPresetAsync(string presetName)
        {
            var preset = new ViewPreset
            {
                Name = presetName,
                ViewMode = CurrentViewMode,
                SortColumn = CurrentSortColumn,
                SortDirection = SortDirection
            };

            await _viewModeService.SavePresetAsync(preset);
        }
    }

    /// <summary>
    /// View preset model
    /// </summary>
    public class ViewPreset
    {
        public string Name { get; set; } = string.Empty;
        public ViewMode ViewMode { get; set; }
        public SortColumn SortColumn { get; set; }
        public SortDirection SortDirection { get; set; }
    }
}
