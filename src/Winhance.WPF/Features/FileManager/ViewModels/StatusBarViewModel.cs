using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Winhance.Core.Features.FileManager.Interfaces;

namespace Winhance.WPF.Features.FileManager.ViewModels
{
    /// <summary>
    /// ViewModel for status bar display and information.
    /// </summary>
    public partial class StatusBarViewModel : ObservableObject
    {
        private readonly ISelectionService _selectionService;

        [ObservableProperty]
        private string _statusMessage = "Ready";

        [ObservableProperty]
        private int _totalItems;

        [ObservableProperty]
        private int _selectedItems;

        [ObservableProperty]
        private long _totalSize;

        [ObservableProperty]
        private long _selectedSize;

        [ObservableProperty]
        private int _foldersCount;

        [ObservableProperty]
        private int _filesCount;

        [ObservableProperty]
        private bool _isOperationInProgress;

        [ObservableProperty]
        private int _operationProgress;

        [ObservableProperty]
        private string _operationStatus = string.Empty;

        [ObservableProperty]
        private bool _showDetailedStats = true;

        public string TotalSizeFormatted => FormatSize(TotalSize);
        public string SelectedSizeFormatted => FormatSize(SelectedSize);
        public string ItemsText => $"{TotalItems} items ({FoldersCount} folders, {FilesCount} files)";
        public string SelectionText => SelectedItems > 0 
            ? $"{SelectedItems} selected ({SelectedSizeFormatted})" 
            : string.Empty;

        public StatusBarViewModel(ISelectionService selectionService)
        {
            _selectionService = selectionService;
            
            _selectionService.SelectionChanged += OnSelectionChanged;
        }

        private void OnSelectionChanged(object? sender, EventArgs e)
        {
            UpdateSelectionStats();
        }

        public void UpdateStats(int total, int folders, int files, long totalSize)
        {
            TotalItems = total;
            FoldersCount = folders;
            FilesCount = files;
            TotalSize = totalSize;

            OnPropertyChanged(nameof(TotalSizeFormatted));
            OnPropertyChanged(nameof(ItemsText));
        }

        private void UpdateSelectionStats()
        {
            var selection = _selectionService.GetSelectedItems();
            SelectedItems = selection.Count;
            SelectedSize = selection.Sum(item => item.Size);

            OnPropertyChanged(nameof(SelectedSizeFormatted));
            OnPropertyChanged(nameof(SelectionText));
        }

        [RelayCommand]
        private void ToggleDetailedStats()
        {
            ShowDetailedStats = !ShowDetailedStats;
        }

        public void UpdateOperationProgress(int progress, string status)
        {
            IsOperationInProgress = progress < 100;
            OperationProgress = progress;
            OperationStatus = status;
        }

        public void SetMessage(string message)
        {
            StatusMessage = message;
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

        public void Dispose()
        {
            _selectionService.SelectionChanged -= OnSelectionChanged;
        }
    }
}
