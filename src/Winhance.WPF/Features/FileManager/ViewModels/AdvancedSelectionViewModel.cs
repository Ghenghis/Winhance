using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Winhance.Core.Features.FileManager.Interfaces;

namespace Winhance.WPF.Features.FileManager.ViewModels
{
    /// <summary>
    /// ViewModel for advanced selection features (WS-SEL features).
    /// </summary>
    public partial class AdvancedSelectionViewModel : ObservableObject
    {
        private readonly ISelectionService? _selectionService;
        private readonly IFileManagerService? _fileManagerService;

        [ObservableProperty]
        private string _selectionPattern = "*.txt";

        [ObservableProperty]
        private ObservableCollection<FileItemViewModel> _currentItems = new();

        [ObservableProperty]
        private ObservableCollection<FileItemViewModel> _selectedItems = new();

        [ObservableProperty]
        private string _statusMessage = "Ready";

        [ObservableProperty]
        private bool _caseSensitive = false;

        [ObservableProperty]
        private bool _useRegex = false;

        [ObservableProperty]
        private DateTime _dateFrom = DateTime.Today.AddDays(-30);

        [ObservableProperty]
        private DateTime _dateTo = DateTime.Now;

        [ObservableProperty]
        private long _sizeFrom = 0;

        [ObservableProperty]
        private long _sizeTo = 1024 * 1024 * 1024; // 1GB

        [ObservableProperty]
        private string _fileTypeFilter = "All Files";

        public List<string> FileTypeOptions { get; } = new()
        {
            "All Files",
            "Images",
            "Videos",
            "Audio",
            "Documents",
            "Archives",
            "Code Files"
        };

        public AdvancedSelectionViewModel(ISelectionService? selectionService, IFileManagerService? fileManagerService)
        {
            _selectionService = selectionService;
            _fileManagerService = fileManagerService;
        }

        /// <summary>
        /// Select items by pattern (WS-SEL-007).
        /// </summary>
        [RelayCommand]
        private void SelectByPattern()
        {
            try
            {
                var pattern = SelectionPattern;
                var selected = new List<FileItemViewModel>();

                if (UseRegex)
                {
                    var regex = new Regex(pattern, CaseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase);
                    selected = CurrentItems.Where(i => 
                        !i.IsParentDirectory && 
                        regex.IsMatch(i.Name)).ToList();
                }
                else
                {
                    var options = CaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
                    selected = CurrentItems.Where(i => 
                        !i.IsParentDirectory && 
                        LikeOperator(i.Name, pattern, options)).ToList();
                }

                UpdateSelection(selected);
                StatusMessage = $"Selected {selected.Count} items matching pattern";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
            }
        }

        /// <summary>
        /// Select items by extension (WS-SEL-008).
        /// </summary>
        [RelayCommand]
        private void SelectByExtension()
        {
            var extension = SelectionPattern.TrimStart('*').TrimStart('.');
            if (string.IsNullOrEmpty(extension))
            {
                StatusMessage = "Please enter a valid extension";
                return;
            }

            var selected = CurrentItems.Where(i => 
                !i.IsParentDirectory && 
                i.Extension.Equals($".{extension}", StringComparison.OrdinalIgnoreCase)).ToList();

            UpdateSelection(selected);
            StatusMessage = $"Selected {selected.Count} files with .{extension} extension";
        }

        /// <summary>
        /// Select items by type (WS-SEL-009).
        /// </summary>
        [RelayCommand]
        private void SelectByType()
        {
            var selected = FileTypeFilter switch
            {
                "Images" => CurrentItems.Where(i => IsImageType(i.Extension)),
                "Videos" => CurrentItems.Where(i => IsVideoType(i.Extension)),
                "Audio" => CurrentItems.Where(i => IsAudioType(i.Extension)),
                "Documents" => CurrentItems.Where(i => IsDocumentType(i.Extension)),
                "Archives" => CurrentItems.Where(i => IsArchiveType(i.Extension)),
                "Code Files" => CurrentItems.Where(i => IsCodeType(i.Extension)),
                _ => CurrentItems.Where(i => !i.IsParentDirectory)
            };

            var selectedList = selected.ToList();
            UpdateSelection(selectedList);
            StatusMessage = $"Selected {selectedList.Count} {FileTypeFilter.ToLower()}";
        }

        /// <summary>
        /// Select items by date range (WS-SEL-010).
        /// </summary>
        [RelayCommand]
        private void SelectByDateRange()
        {
            var selected = CurrentItems.Where(i => 
                !i.IsParentDirectory && 
                i.DateModified >= DateFrom && 
                i.DateModified <= DateTo).ToList();

            UpdateSelection(selected);
            StatusMessage = $"Selected {selected.Count} items modified between {DateFrom:yyyy-MM-dd} and {DateTo:yyyy-MM-dd}";
        }

        /// <summary>
        /// Select items by size range (WS-SEL-011).
        /// </summary>
        [RelayCommand]
        private void SelectBySizeRange()
        {
            var selected = CurrentItems.Where(i => 
                !i.IsParentDirectory && 
                i.Size >= SizeFrom && 
                i.Size <= SizeTo).ToList();

            UpdateSelection(selected);
            StatusMessage = $"Selected {selected.Count} items between {FormatSize(SizeFrom)} and {FormatSize(SizeTo)}";
        }

        /// <summary>
        /// Select files only (WS-SEL-012).
        /// </summary>
        [RelayCommand]
        private void SelectFilesOnly()
        {
            var selected = CurrentItems.Where(i => !i.IsDirectory && !i.IsParentDirectory).ToList();
            UpdateSelection(selected);
            StatusMessage = $"Selected {selected.Count} files";
        }

        /// <summary>
        /// Select folders only (WS-SEL-013).
        /// </summary>
        [RelayCommand]
        private void SelectFoldersOnly()
        {
            var selected = CurrentItems.Where(i => i.IsDirectory && !i.IsParentDirectory).ToList();
            UpdateSelection(selected);
            StatusMessage = $"Selected {selected.Count} folders";
        }

        /// <summary>
        /// Select modified today (WS-SEL-015).
        /// </summary>
        [RelayCommand]
        private void SelectModifiedToday()
        {
            var today = DateTime.Today;
            var selected = CurrentItems.Where(i => 
                !i.IsParentDirectory && 
                i.DateModified.Date == today).ToList();

            UpdateSelection(selected);
            StatusMessage = $"Selected {selected.Count} items modified today";
        }

        /// <summary>
        /// Compare to other panel selection (WS-SEL-014).
        /// </summary>
        [RelayCommand]
        private void SelectSameInOtherPanel(ObservableCollection<FileItemViewModel> otherPanelItems)
        {
            var selectedNames = SelectedItems.Select(i => i.Name).ToHashSet();
            var matching = otherPanelItems.Where(i => selectedNames.Contains(i.Name)).ToList();
            
            StatusMessage = $"Found {matching.Count} items with same names in other panel";
        }

        /// <summary>
        /// Select newer files vs other panel (WS-SEL-013).
        /// </summary>
        [RelayCommand]
        private void SelectNewerThanOtherPanel(ObservableCollection<FileItemViewModel> otherPanelItems)
        {
            var otherDict = otherPanelItems.Where(i => !i.IsParentDirectory)
                .ToDictionary(i => i.Name, i => i.DateModified);

            var selected = CurrentItems.Where(i => 
                !i.IsParentDirectory && 
                otherDict.TryGetValue(i.Name, out var otherDate) && 
                i.DateModified > otherDate).ToList();

            UpdateSelection(selected);
            StatusMessage = $"Selected {selected.Count} items newer than other panel";
        }

        /// <summary>
        /// Select duplicate files (WS-SEL-014).
        /// </summary>
        [RelayCommand]
        private async Task SelectDuplicatesAsync()
        {
            var groups = CurrentItems.Where(i => !i.IsParentDirectory)
                .GroupBy(i => new { i.Name, i.Size })
                .Where(g => g.Count() > 1)
                .SelectMany(g => g)
                .ToList();

            UpdateSelection(groups);
            StatusMessage = $"Selected {groups.Count} duplicate files";
            await Task.CompletedTask;
        }

        private void UpdateSelection(List<FileItemViewModel> selected)
        {
            SelectedItems.Clear();
            foreach (var item in selected)
            {
                SelectedItems.Add(item);
            }
        }

        private static bool LikeOperator(string input, string pattern, StringComparison comparison)
        {
            var regexPattern = "^" + Regex.Escape(pattern).Replace(@"\*", ".*").Replace(@"\?", ".") + "$";
            return Regex.IsMatch(input, regexPattern, comparison == StringComparison.OrdinalIgnoreCase ? RegexOptions.IgnoreCase : RegexOptions.None);
        }

        private static bool IsImageType(string extension) => 
            extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".png", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".gif", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".bmp", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".tiff", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".webp", StringComparison.OrdinalIgnoreCase);

        private static bool IsVideoType(string extension) => 
            extension.Equals(".mp4", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".avi", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".mkv", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".mov", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".wmv", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".flv", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".webm", StringComparison.OrdinalIgnoreCase);

        private static bool IsAudioType(string extension) => 
            extension.Equals(".mp3", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".wav", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".flac", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".aac", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".ogg", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".wma", StringComparison.OrdinalIgnoreCase);

        private static bool IsDocumentType(string extension) => 
            extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".doc", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".docx", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".xls", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".xlsx", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".ppt", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".pptx", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".txt", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".rtf", StringComparison.OrdinalIgnoreCase);

        private static bool IsArchiveType(string extension) => 
            extension.Equals(".zip", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".rar", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".7z", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".tar", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".gz", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".bz2", StringComparison.OrdinalIgnoreCase);

        private static bool IsCodeType(string extension) => 
            extension.Equals(".cs", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".vb", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".cpp", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".h", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".js", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".ts", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".py", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".java", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".html", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".css", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".xml", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".json", StringComparison.OrdinalIgnoreCase);

        private static string FormatSize(long bytes)
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
}
