using System;
using System.Collections.Generic;
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
    /// ViewModel for managing file and folder selection.
    /// </summary>
    public partial class SelectionViewModel : ObservableObject
    {
        private readonly ISelectionService _selectionService;
        private readonly IFileManagerService _fileManagerService;

        [ObservableProperty]
        private ObservableCollection<FileItemViewModel> _selectedItems = new();

        [ObservableProperty]
        private ObservableCollection<FileItemViewModel> _allItems = new();

        [ObservableProperty]
        private int _selectedCount;

        [ObservableProperty]
        private long _selectedSize;

        [ObservableProperty]
        private long _selectedSizeOnDisk;

        [ObservableProperty]
        private string _selectedInfo = string.Empty;

        [ObservableProperty]
        private bool _hasSelection;

        [ObservableProperty]
        private bool _isMultiSelect;

        [ObservableProperty]
        private FileItemViewModel? _lastSelectedItem;

        [ObservableProperty]
        private SelectionMode _selectionMode = SelectionMode.Normal;

        [ObservableProperty]
        private string _selectionPattern = string.Empty;

        [ObservableProperty]
        private ObservableCollection<SelectionPreset> _selectionPresets = new();

        [ObservableProperty]
        private SelectionPreset? _selectedPreset;

        public SelectionViewModel(
            ISelectionService selectionService,
            IFileManagerService fileManagerService)
        {
            _selectionService = selectionService;
            _fileManagerService = fileManagerService;

            // Subscribe to selection service events
            _selectionService.SelectionChanged += OnSelectionChanged;
            _selectionService.SelectionModified += OnSelectionModified;

            // Load selection presets
            LoadSelectionPresets();
        }

        /// <summary>
        /// Sets the items available for selection.
        /// </summary>
        public void SetAvailableItems(IEnumerable<FileItemViewModel> items)
        {
            AllItems = new ObservableCollection<FileItemViewModel>(items);
        }

        /// <summary>
        /// Selects all items.
        /// </summary>
        [RelayCommand]
        public void SelectAll()
        {
            var paths = AllItems.Select(i => i.FullPath);
            _selectionService.SetSelection(paths);
        }

        /// <summary>
        /// Clears all selections.
        /// </summary>
        [RelayCommand]
        public void ClearSelection()
        {
            _selectionService.ClearSelection();
        }

        /// <summary>
        /// Inverts the current selection.
        /// </summary>
        [RelayCommand]
        public void InvertSelection()
        {
            var allPaths = AllItems.Select(i => i.FullPath);
            var selectedPaths = _selectionService.GetSelectedPaths();
            var toSelect = allPaths.Except(selectedPaths);
            _selectionService.SetSelection(toSelect);
        }

        /// <summary>
        /// Selects items by pattern.
        /// </summary>
        [RelayCommand]
        public void SelectByPattern()
        {
            if (string.IsNullOrWhiteSpace(SelectionPattern)) return;

            try
            {
                var options = new PatternOptions
                {
                    Pattern = SelectionPattern,
                    UseRegex = SelectionPattern.StartsWith("regex:"),
                    CaseSensitive = SelectionPattern.Any(c => char.IsUpper(c))
                };

                var matchingItems = _selectionService.SelectByPattern(AllItems.Select(i => i.FullPath), options);
                _selectionService.SetSelection(matchingItems);
            }
            catch (Exception ex)
            {
                // Show error to user
                System.Windows.MessageBox.Show($"Invalid pattern: {ex.Message}", "Pattern Error", 
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Selects items by type.
        /// </summary>
        [RelayCommand]
        public void SelectByType(string fileType)
        {
            var extensions = GetFileTypeExtensions(fileType);
            var items = AllItems.Where(i => 
                !i.IsDirectory && extensions.Contains(i.Extension.ToLower()));
            
            _selectionService.SetSelection(items.Select(i => i.FullPath));
        }

        /// <summary>
        /// Selects items by date range.
        /// </summary>
        [RelayCommand]
        public void SelectByDateRange(DateTime? from, DateTime? to)
        {
            var items = AllItems.Where(i =>
            {
                var date = i.DateModified;
                if (from.HasValue && date < from.Value) return false;
                if (to.HasValue && date > to.Value) return false;
                return true;
            });

            _selectionService.SetSelection(items.Select(i => i.FullPath));
        }

        /// <summary>
        /// Selects items by size range.
        /// </summary>
        [RelayCommand]
        public void SelectBySizeRange(long? minSize, long? maxSize)
        {
            var items = AllItems.Where(i =>
            {
                if (i.IsDirectory) return false;
                if (minSize.HasValue && i.Size < minSize.Value) return false;
                if (maxSize.HasValue && i.Size > maxSize.Value) return false;
                return true;
            });

            _selectionService.SetSelection(items.Select(i => i.FullPath));
        }

        /// <summary>
        /// Selects all folders.
        /// </summary>
        [RelayCommand]
        public void SelectAllFolders()
        {
            var folders = AllItems.Where(i => i.IsDirectory);
            _selectionService.SetSelection(folders.Select(i => i.FullPath));
        }

        /// <summary>
        /// Selects all files.
        /// </summary>
        [RelayCommand]
        public void SelectAllFiles()
        {
            var files = AllItems.Where(i => !i.IsDirectory);
            _selectionService.SetSelection(files.Select(i => i.FullPath));
        }

        /// <summary>
        /// Selects items with similar names.
        /// </summary>
        [RelayCommand]
        public void SelectSimilar()
        {
            if (LastSelectedItem == null) return;

            var baseName = Path.GetFileNameWithoutExtension(LastSelectedItem.Name);
            var similarItems = AllItems.Where(i =>
                Path.GetFileNameWithoutExtension(i.Name)
                .Equals(baseName, StringComparison.OrdinalIgnoreCase));

            _selectionService.SetSelection(similarItems.Select(i => i.FullPath));
        }

        /// <summary>
        /// Selects items in the same folder.
        /// </summary>
        [RelayCommand]
        public void SelectSameFolder()
        {
            if (LastSelectedItem == null) return;

            var folder = Path.GetDirectoryName(LastSelectedItem.FullPath);
            var items = AllItems.Where(i => 
                Path.GetDirectoryName(i.FullPath) == folder);

            _selectionService.SetSelection(items.Select(i => i.FullPath));
        }

        /// <summary>
        /// Saves the current selection as a preset.
        /// </summary>
        [RelayCommand]
        public async Task SaveSelectionAsPresetAsync(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return;

            var selectedPaths = _selectionService.GetSelectedPaths().ToArray();
            if (selectedPaths.Length == 0) return;

            try
            {
                var preset = new SelectionPreset
                {
                    Name = name,
                    Paths = selectedPaths,
                    CreatedAt = DateTime.Now
                };

                await _selectionService.SaveSelectionPresetAsync(preset);
                SelectionPresets.Insert(0, preset);
                SelectedPreset = preset;
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Cannot save preset: {ex.Message}", "Error",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Loads a selection preset.
        /// </summary>
        [RelayCommand]
        public async Task LoadPresetAsync(SelectionPreset? preset)
        {
            if (preset == null) return;

            try
            {
                // Filter paths that still exist
                var existingPaths = preset.Paths.Where(p => 
                    File.Exists(p) || Directory.Exists(p));

                _selectionService.SetSelection(existingPaths);
                SelectedInfo = $"Loaded preset: {preset.Name} ({existingPaths.Count()} items)";
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Cannot load preset: {ex.Message}", "Error",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Deletes a selection preset.
        /// </summary>
        [RelayCommand]
        public async Task DeletePresetAsync(SelectionPreset? preset)
        {
            if (preset == null) return;

            try
            {
                await _selectionService.DeleteSelectionPresetAsync(preset.Id);
                SelectionPresets.Remove(preset);
                
                if (SelectedPreset == preset)
                {
                    SelectedPreset = null;
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Cannot delete preset: {ex.Message}", "Error",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Extends the selection to include all items between the last selected and current item.
        /// </summary>
        public void ExtendSelection(FileItemViewModel currentItem)
        {
            if (LastSelectedItem == null)
            {
                SelectItem(currentItem, false);
                return;
            }

            var allItemsList = AllItems.ToList();
            var lastIndex = allItemsList.IndexOf(LastSelectedItem);
            var currentIndex = allItemsList.IndexOf(currentItem);

            if (lastIndex >= 0 && currentIndex >= 0)
            {
                var start = Math.Min(lastIndex, currentIndex);
                var end = Math.Max(lastIndex, currentIndex);
                
                var itemsToSelect = allItemsList.Skip(start).Take(end - start + 1);
                _selectionService.AddToSelection(itemsToSelect.Select(i => i.FullPath));
            }
        }

        /// <summary>
        /// Selects or deselects an item.
        /// </summary>
        public void SelectItem(FileItemViewModel item, bool isCtrlPressed, bool isShiftPressed)
        {
            if (isShiftPressed && IsMultiSelect)
            {
                ExtendSelection(item);
            }
            else if (isCtrlPressed && IsMultiSelect)
            {
                if (_selectionService.IsSelected(item.FullPath))
                {
                    _selectionService.RemoveFromSelection(item.FullPath);
                }
                else
                {
                    _selectionService.AddToSelection(item.FullPath);
                }
            }
            else
            {
                _selectionService.SetSelection(new[] { item.FullPath });
            }

            LastSelectedItem = item;
        }

        /// <summary>
        /// Handles selection changed events.
        /// </summary>
        private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            var selectedPaths = _selectionService.GetSelectedPaths();
            var selectedItems = AllItems.Where(i => selectedPaths.Contains(i.FullPath)).ToList();
            
            SelectedItems = new ObservableCollection<FileItemViewModel>(selectedItems);
            SelectedCount = selectedItems.Count;
            HasSelection = SelectedCount > 0;
            
            // Calculate sizes
            SelectedSize = selectedItems.Where(i => !i.IsDirectory).Sum(i => i.Size);
            SelectedSizeOnDisk = CalculateSizeOnDisk(selectedItems);
            
            // Update info text
            UpdateSelectedInfo();
        }

        /// <summary>
        /// Handles selection modified events.
        /// </summary>
        private void OnSelectionModified(object? sender, EventArgs e)
        {
            // Selection was modified, update UI
            OnSelectionChanged(sender, new SelectionChangedEventArgs());
        }

        /// <summary>
        /// Updates the selected info text.
        /// </summary>
        private void UpdateSelectedInfo()
        {
            if (SelectedCount == 0)
            {
                SelectedInfo = "No items selected";
            }
            else if (SelectedCount == 1)
            {
                var item = SelectedItems.FirstOrDefault();
                if (item != null)
                {
                    SelectedInfo = item.IsDirectory 
                        ? $"Selected: {item.Name}"
                        : $"Selected: {item.Name} ({FormatSize(item.Size)})";
                }
            }
            else
            {
                var fileCount = SelectedItems.Count(i => !i.IsDirectory);
                var folderCount = SelectedItems.Count(i => i.IsDirectory);
                
                if (fileCount > 0 && folderCount > 0)
                {
                    SelectedInfo = $"Selected: {fileCount} files, {folderCount} folders ({FormatSize(SelectedSize)})";
                }
                else if (fileCount > 0)
                {
                    SelectedInfo = $"Selected: {fileCount} files ({FormatSize(SelectedSize)})";
                }
                else
                {
                    SelectedInfo = $"Selected: {folderCount} folders";
                }
            }
        }

        /// <summary>
        /// Calculates the actual size on disk.
        /// </summary>
        private long CalculateSizeOnDisk(IEnumerable<FileItemViewModel> items)
        {
            // Simplified calculation - actual would need to account for cluster size
            return SelectedSize;
        }

        /// <summary>
        /// Gets file extensions for a file type.
        /// </summary>
        private static string[] GetFileTypeExtensions(string fileType)
        {
            return fileType.ToLowerInvariant() switch
            {
                "images" => new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".tiff", ".svg", ".webp" },
                "documents" => new[] { ".txt", ".doc", ".docx", ".pdf", ".rtf", ".odt" },
                "spreadsheets" => new[] { ".xls", ".xlsx", ".csv", ".ods" },
                "presentations" => new[] { ".ppt", ".pptx", ".odp" },
                "audio" => new[] { ".mp3", ".wav", ".flac", ".aac", ".ogg", ".wma" },
                "video" => new[] { ".mp4", ".avi", ".mkv", ".mov", ".wmv", ".flv", ".webm" },
                "archives" => new[] { ".zip", ".rar", ".7z", ".tar", ".gz", ".bz2" },
                "code" => new[] { ".cs", ".java", ".py", ".js", ".cpp", ".c", ".h", ".php", ".rb", ".go", ".rs" },
                _ => Array.Empty<string>()
            };
        }

        /// <summary>
        /// Loads selection presets.
        /// </summary>
        private async void LoadSelectionPresets()
        {
            try
            {
                var presets = await _selectionService.GetSelectionPresetsAsync();
                SelectionPresets = new ObservableCollection<SelectionPreset>(presets);
            }
            catch
            {
                SelectionPresets = new ObservableCollection<SelectionPreset>();
            }
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
    /// Selection mode enum.
    /// </summary>
    public enum SelectionMode
    {
        Normal,
        Extended,
        Multiple
    }

    /// <summary>
    /// Selection preset model.
    /// </summary>
    public class SelectionPreset
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string[] Paths { get; set; } = Array.Empty<string>();
        public DateTime CreatedAt { get; set; }
    }
}
