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
    /// ViewModel for viewing and sorting columns in the file list.
    /// </summary>
    public partial class ColumnConfigViewModel : ObservableObject
    {
        private readonly IColumnService _columnService;

        [ObservableProperty]
        private ObservableCollection<ColumnViewModel> _availableColumns = new();

        [ObservableProperty]
        private ObservableCollection<ColumnViewModel> _visibleColumns = new();

        [ObservableProperty]
        private ColumnViewModel? _selectedColumn;

        [ObservableProperty]
        private bool _isDialogOpen;

        public ColumnConfigViewModel(IColumnService columnService)
        {
            _columnService = columnService;
            _ = LoadColumnsAsync();
        }

        private async Task LoadColumnsAsync()
        {
            var columns = await _columnService.GetAvailableColumnsAsync();
            
            AvailableColumns.Clear();
            VisibleColumns.Clear();

            foreach (var column in columns)
            {
                var columnVm = new ColumnViewModel
                {
                    Name = column.Name,
                    DisplayName = column.DisplayName,
                    Width = column.Width,
                    IsVisible = column.IsVisible,
                    Order = column.Order,
                    Alignment = column.Alignment
                };

                AvailableColumns.Add(columnVm);
                if (column.IsVisible)
                {
                    VisibleColumns.Add(columnVm);
                }
            }
        }

        [RelayCommand]
        private void ShowDialog()
        {
            IsDialogOpen = true;
        }

        [RelayCommand]
        private void CloseDialog()
        {
            IsDialogOpen = false;
        }

        [RelayCommand]
        private async Task SaveConfigurationAsync()
        {
            var config = VisibleColumns.Select((col, index) => new
            {
                col.Name,
                col.Width,
                IsVisible = true,
                Order = index,
                col.Alignment
            }).ToList();

            await _columnService.SaveConfigurationAsync(config.Cast<object>().ToList());
            IsDialogOpen = false;
        }

        [RelayCommand]
        private void AddColumn(ColumnViewModel? column)
        {
            if (column == null || VisibleColumns.Contains(column)) return;

            column.IsVisible = true;
            VisibleColumns.Add(column);
        }

        [RelayCommand]
        private void RemoveColumn(ColumnViewModel? column)
        {
            if (column == null) return;

            column.IsVisible = false;
            VisibleColumns.Remove(column);
        }

        [RelayCommand]
        private void MoveColumnUp(ColumnViewModel? column)
        {
            if (column == null) return;

            var index = VisibleColumns.IndexOf(column);
            if (index > 0)
            {
                VisibleColumns.Move(index, index - 1);
            }
        }

        [RelayCommand]
        private void MoveColumnDown(ColumnViewModel? column)
        {
            if (column == null) return;

            var index = VisibleColumns.IndexOf(column);
            if (index < VisibleColumns.Count - 1)
            {
                VisibleColumns.Move(index, index + 1);
            }
        }

        [RelayCommand]
        private async Task ResetToDefaultAsync()
        {
            await _columnService.ResetToDefaultAsync();
            await LoadColumnsAsync();
        }

        [RelayCommand]
        private void AutoFitColumn(ColumnViewModel? column)
        {
            if (column == null) return;

            column.Width = -1; // Auto-fit marker
        }
    }

    public partial class ColumnViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _name = string.Empty;

        [ObservableProperty]
        private string _displayName = string.Empty;

        [ObservableProperty]
        private double _width = 100;

        [ObservableProperty]
        private bool _isVisible;

        [ObservableProperty]
        private int _order;

        [ObservableProperty]
        private string _alignment = "Left";
    }
}
