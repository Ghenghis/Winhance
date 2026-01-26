using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Winhance.Core.Features.FileManager.Interfaces;

namespace Winhance.WPF.Features.FileManager.ViewModels
{
    /// <summary>
    /// ViewModel for sorting files and directories.
    /// </summary>
    public partial class SortingViewModel : ObservableObject
    {
        private readonly ISortingService _sortingService;

        [ObservableProperty]
        private string _sortColumn = "Name";

        [ObservableProperty]
        private bool _sortAscending = true;

        [ObservableProperty]
        private bool _foldersFirst = true;

        [ObservableProperty]
        private bool _useNaturalSort = true;

        [ObservableProperty]
        private bool _caseSensitive;

        [ObservableProperty]
        private ObservableCollection<string> _availableSortColumns = new()
        {
            "Name", "Size", "Type", "Date Modified", "Date Created", "Extension"
        };

        [ObservableProperty]
        private ObservableCollection<SortPresetViewModel> _sortPresets = new();

        public SortingViewModel(ISortingService sortingService)
        {
            _sortingService = sortingService;
            _ = LoadPresetsAsync();
        }

        private async Task LoadPresetsAsync()
        {
            var presets = await _sortingService.GetPresetsAsync();
            
            SortPresets.Clear();
            foreach (var preset in presets)
            {
                SortPresets.Add(new SortPresetViewModel
                {
                    Name = preset.Name,
                    SortColumn = preset.SortColumn,
                    Ascending = preset.Ascending,
                    FoldersFirst = preset.FoldersFirst
                });
            }
        }

        [RelayCommand]
        private async Task SortByColumnAsync(string column)
        {
            if (SortColumn == column)
            {
                SortAscending = !SortAscending;
            }
            else
            {
                SortColumn = column;
                SortAscending = true;
            }

            await ApplySortingAsync();
        }

        [RelayCommand]
        private async Task ToggleSortOrderAsync()
        {
            SortAscending = !SortAscending;
            await ApplySortingAsync();
        }

        [RelayCommand]
        private async Task ToggleFoldersFirstAsync()
        {
            FoldersFirst = !FoldersFirst;
            await ApplySortingAsync();
        }

        [RelayCommand]
        private async Task ToggleNaturalSortAsync()
        {
            UseNaturalSort = !UseNaturalSort;
            await ApplySortingAsync();
        }

        [RelayCommand]
        private async Task ToggleCaseSensitiveAsync()
        {
            CaseSensitive = !CaseSensitive;
            await ApplySortingAsync();
        }

        private async Task ApplySortingAsync()
        {
            await _sortingService.ApplySortAsync(new
            {
                SortColumn,
                SortAscending,
                FoldersFirst,
                UseNaturalSort,
                CaseSensitive
            });
        }

        [RelayCommand]
        private async Task SavePresetAsync(string presetName)
        {
            if (string.IsNullOrWhiteSpace(presetName)) return;

            var preset = new SortPresetViewModel
            {
                Name = presetName,
                SortColumn = SortColumn,
                Ascending = SortAscending,
                FoldersFirst = FoldersFirst
            };

            await _sortingService.SavePresetAsync(preset);
            SortPresets.Add(preset);
        }

        [RelayCommand]
        private async Task LoadPresetAsync(SortPresetViewModel? preset)
        {
            if (preset == null) return;

            SortColumn = preset.SortColumn;
            SortAscending = preset.Ascending;
            FoldersFirst = preset.FoldersFirst;

            await ApplySortingAsync();
        }

        [RelayCommand]
        private async Task DeletePresetAsync(SortPresetViewModel? preset)
        {
            if (preset == null) return;

            await _sortingService.DeletePresetAsync(preset.Name);
            SortPresets.Remove(preset);
        }
    }

    public partial class SortPresetViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _name = string.Empty;

        [ObservableProperty]
        private string _sortColumn = "Name";

        [ObservableProperty]
        private bool _ascending = true;

        [ObservableProperty]
        private bool _foldersFirst = true;
    }
}
