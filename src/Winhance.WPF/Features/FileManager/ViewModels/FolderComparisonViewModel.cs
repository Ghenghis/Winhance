using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Winhance.Core.Features.FileManager.Interfaces;

namespace Winhance.WPF.Features.FileManager.ViewModels
{
    /// <summary>
    /// ViewModel for comparing two folders and showing differences.
    /// </summary>
    public partial class FolderComparisonViewModel : ObservableObject
    {
        private readonly IFileManagerService _fileManagerService;
        private CancellationTokenSource? _cts;

        [ObservableProperty]
        private string _leftPath = string.Empty;

        [ObservableProperty]
        private string _rightPath = string.Empty;

        [ObservableProperty]
        private ObservableCollection<ComparisonItemViewModel> _comparisonItems = new();

        [ObservableProperty]
        private ObservableCollection<ComparisonItemViewModel> _selectedItems = new();

        [ObservableProperty]
        private bool _isComparing;

        [ObservableProperty]
        private int _totalFiles;

        [ObservableProperty]
        private int _identicalFiles;

        [ObservableProperty]
        private int _differentFiles;

        [ObservableProperty]
        private int _leftOnlyFiles;

        [ObservableProperty]
        private int _rightOnlyFiles;

        [ObservableProperty]
        private bool _compareContent = true;

        [ObservableProperty]
        private bool _compareDates = true;

        [ObservableProperty]
        private bool _compareSizes = true;

        [ObservableProperty]
        private bool _showIdentical = true;

        [ObservableProperty]
        private bool _showDifferent = true;

        [ObservableProperty]
        private bool _showLeftOnly = true;

        [ObservableProperty]
        private bool _showRightOnly = true;

        [ObservableProperty]
        private string _filterText = string.Empty;

        [ObservableProperty]
        private string _statusMessage = "Ready";

        public FolderComparisonViewModel(IFileManagerService fileManagerService)
        {
            _fileManagerService = fileManagerService;
        }

        [RelayCommand]
        private async Task CompareAsync()
        {
            if (string.IsNullOrEmpty(LeftPath) || string.IsNullOrEmpty(RightPath))
            {
                StatusMessage = "Please select both folders to compare";
                return;
            }

            if (!Directory.Exists(LeftPath) || !Directory.Exists(RightPath))
            {
                StatusMessage = "One or both folders do not exist";
                return;
            }

            _cts = new CancellationTokenSource();
            IsComparing = true;
            StatusMessage = "Comparing folders...";
            ComparisonItems.Clear();

            try
            {
                await Task.Run(() => PerformComparison(_cts.Token), _cts.Token);
                
                UpdateStatistics();
                ApplyFilters();
                StatusMessage = $"Comparison complete - {TotalFiles} files analyzed";
            }
            catch (OperationCanceledException)
            {
                StatusMessage = "Comparison cancelled";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
            }
            finally
            {
                IsComparing = false;
            }
        }

        private void PerformComparison(CancellationToken token)
        {
            var leftFiles = Directory.GetFiles(LeftPath, "*", SearchOption.AllDirectories)
                .Select(f => Path.GetRelativePath(LeftPath, f))
                .ToHashSet();

            var rightFiles = Directory.GetFiles(RightPath, "*", SearchOption.AllDirectories)
                .Select(f => Path.GetRelativePath(RightPath, f))
                .ToHashSet();

            var allFiles = leftFiles.Union(rightFiles).OrderBy(f => f);

            foreach (var relativePath in allFiles)
            {
                token.ThrowIfCancellationRequested();

                var leftFullPath = Path.Combine(LeftPath, relativePath);
                var rightFullPath = Path.Combine(RightPath, relativePath);

                var leftExists = File.Exists(leftFullPath);
                var rightExists = File.Exists(rightFullPath);

                var item = new ComparisonItemViewModel
                {
                    RelativePath = relativePath,
                    LeftExists = leftExists,
                    RightExists = rightExists
                };

                if (leftExists && rightExists)
                {
                    var leftInfo = new FileInfo(leftFullPath);
                    var rightInfo = new FileInfo(rightFullPath);

                    item.LeftSize = leftInfo.Length;
                    item.RightSize = rightInfo.Length;
                    item.LeftModified = leftInfo.LastWriteTime;
                    item.RightModified = rightInfo.LastWriteTime;

                    item.Status = DetermineStatus(leftInfo, rightInfo);
                }
                else if (leftExists)
                {
                    item.Status = ComparisonStatus.LeftOnly;
                    var leftInfo = new FileInfo(leftFullPath);
                    item.LeftSize = leftInfo.Length;
                    item.LeftModified = leftInfo.LastWriteTime;
                }
                else
                {
                    item.Status = ComparisonStatus.RightOnly;
                    var rightInfo = new FileInfo(rightFullPath);
                    item.RightSize = rightInfo.Length;
                    item.RightModified = rightInfo.LastWriteTime;
                }

                App.Current.Dispatcher.Invoke(() => ComparisonItems.Add(item));
            }
        }

        private ComparisonStatus DetermineStatus(FileInfo left, FileInfo right)
        {
            if (CompareSizes && left.Length != right.Length)
                return ComparisonStatus.Different;

            if (CompareDates && Math.Abs((left.LastWriteTime - right.LastWriteTime).TotalSeconds) > 2)
                return ComparisonStatus.Different;

            if (CompareContent)
            {
                var leftHash = ComputeFileHash(left.FullName);
                var rightHash = ComputeFileHash(right.FullName);
                
                if (leftHash != rightHash)
                    return ComparisonStatus.Different;
            }

            return ComparisonStatus.Identical;
        }

        private static string ComputeFileHash(string filePath)
        {
            using var stream = File.OpenRead(filePath);
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var hash = sha256.ComputeHash(stream);
            return BitConverter.ToString(hash).Replace("-", "");
        }

        private void UpdateStatistics()
        {
            TotalFiles = ComparisonItems.Count;
            IdenticalFiles = ComparisonItems.Count(i => i.Status == ComparisonStatus.Identical);
            DifferentFiles = ComparisonItems.Count(i => i.Status == ComparisonStatus.Different);
            LeftOnlyFiles = ComparisonItems.Count(i => i.Status == ComparisonStatus.LeftOnly);
            RightOnlyFiles = ComparisonItems.Count(i => i.Status == ComparisonStatus.RightOnly);
        }

        [RelayCommand]
        private void ApplyFilters()
        {
            // Filter logic would be applied to a filtered collection
            // For now, just update visibility flags
            OnPropertyChanged(nameof(ShowIdentical));
            OnPropertyChanged(nameof(ShowDifferent));
            OnPropertyChanged(nameof(ShowLeftOnly));
            OnPropertyChanged(nameof(ShowRightOnly));
        }

        [RelayCommand]
        private async Task SyncLeftToRightAsync()
        {
            var itemsToSync = SelectedItems.Any() ? SelectedItems : ComparisonItems;
            await SyncFilesAsync(itemsToSync, LeftPath, RightPath);
        }

        [RelayCommand]
        private async Task SyncRightToLeftAsync()
        {
            var itemsToSync = SelectedItems.Any() ? SelectedItems : ComparisonItems;
            await SyncFilesAsync(itemsToSync, RightPath, LeftPath);
        }

        private async Task SyncFilesAsync(IEnumerable<ComparisonItemViewModel> items, string sourcePath, string destPath)
        {
            IsComparing = true;
            StatusMessage = "Syncing files...";

            try
            {
                foreach (var item in items)
                {
                    var sourceFile = Path.Combine(sourcePath, item.RelativePath);
                    var destFile = Path.Combine(destPath, item.RelativePath);

                    if (File.Exists(sourceFile))
                    {
                        var destDir = Path.GetDirectoryName(destFile);
                        if (!string.IsNullOrEmpty(destDir))
                        {
                            Directory.CreateDirectory(destDir);
                        }

                        File.Copy(sourceFile, destFile, true);
                    }
                }

                StatusMessage = "Sync complete";
                await CompareAsync();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Sync error: {ex.Message}";
            }
            finally
            {
                IsComparing = false;
            }
        }

        [RelayCommand]
        private void Cancel()
        {
            _cts?.Cancel();
        }

        [RelayCommand]
        private void SwapPaths()
        {
            (LeftPath, RightPath) = (RightPath, LeftPath);
        }
    }

    public partial class ComparisonItemViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _relativePath = string.Empty;

        [ObservableProperty]
        private bool _leftExists;

        [ObservableProperty]
        private bool _rightExists;

        [ObservableProperty]
        private long _leftSize;

        [ObservableProperty]
        private long _rightSize;

        [ObservableProperty]
        private DateTime _leftModified;

        [ObservableProperty]
        private DateTime _rightModified;

        [ObservableProperty]
        private ComparisonStatus _status;

        public string StatusText => Status switch
        {
            ComparisonStatus.Identical => "Identical",
            ComparisonStatus.Different => "Different",
            ComparisonStatus.LeftOnly => "Left Only",
            ComparisonStatus.RightOnly => "Right Only",
            _ => "Unknown"
        };
    }

    public enum ComparisonStatus
    {
        Identical,
        Different,
        LeftOnly,
        RightOnly
    }
}
