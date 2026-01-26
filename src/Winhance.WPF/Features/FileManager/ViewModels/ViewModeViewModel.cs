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
    /// ViewModel for managing view mode settings.
    /// </summary>
    public partial class ViewModeViewModel : ObservableObject
    {
        private readonly IViewModeService _viewModeService;

        [ObservableProperty]
        private string _currentViewMode = "Details";

        [ObservableProperty]
        private ObservableCollection<string> _availableViewModes = new()
        {
            "Details", "Icons", "Tiles", "List", "Thumbnail", "Content"
        };

        [ObservableProperty]
        private int _iconSize = 48;

        [ObservableProperty]
        private bool _showFileExtensions = true;

        [ObservableProperty]
        private bool _showHiddenFiles;

        [ObservableProperty]
        private bool _showSystemFiles;

        [ObservableProperty]
        private bool _showThumbnails = true;

        [ObservableProperty]
        private int _thumbnailSize = 96;

        [ObservableProperty]
        private bool _groupItems;

        [ObservableProperty]
        private string _groupBy = "None";

        public ViewModeViewModel(IViewModeService viewModeService)
        {
            _viewModeService = viewModeService;
            _ = LoadSettingsAsync();
        }

        private async Task LoadSettingsAsync()
        {
            var settings = await _viewModeService.GetCurrentSettingsAsync();
            
            CurrentViewMode = settings.ViewMode;
            IconSize = settings.IconSize;
            ShowFileExtensions = settings.ShowFileExtensions;
            ShowHiddenFiles = settings.ShowHiddenFiles;
            ShowSystemFiles = settings.ShowSystemFiles;
            ShowThumbnails = settings.ShowThumbnails;
            ThumbnailSize = settings.ThumbnailSize;
            GroupItems = settings.GroupItems;
            GroupBy = settings.GroupBy;
        }

        [RelayCommand]
        private async Task SetViewModeAsync(string mode)
        {
            if (string.IsNullOrEmpty(mode)) return;

            CurrentViewMode = mode;
            await _viewModeService.SetViewModeAsync(mode);
        }

        [RelayCommand]
        private async Task ToggleFileExtensionsAsync()
        {
            ShowFileExtensions = !ShowFileExtensions;
            await _viewModeService.SetShowFileExtensionsAsync(ShowFileExtensions);
        }

        [RelayCommand]
        private async Task ToggleHiddenFilesAsync()
        {
            ShowHiddenFiles = !ShowHiddenFiles;
            await _viewModeService.SetShowHiddenFilesAsync(ShowHiddenFiles);
        }

        [RelayCommand]
        private async Task ToggleSystemFilesAsync()
        {
            ShowSystemFiles = !ShowSystemFiles;
            await _viewModeService.SetShowSystemFilesAsync(ShowSystemFiles);
        }

        [RelayCommand]
        private async Task ToggleThumbnailsAsync()
        {
            ShowThumbnails = !ShowThumbnails;
            await _viewModeService.SetShowThumbnailsAsync(ShowThumbnails);
        }

        [RelayCommand]
        private async Task SetIconSizeAsync(int size)
        {
            IconSize = size;
            await _viewModeService.SetIconSizeAsync(size);
        }

        [RelayCommand]
        private async Task SetThumbnailSizeAsync(int size)
        {
            ThumbnailSize = size;
            await _viewModeService.SetThumbnailSizeAsync(size);
        }

        [RelayCommand]
        private async Task SetGroupByAsync(string groupBy)
        {
            GroupBy = groupBy;
            GroupItems = groupBy != "None";
            await _viewModeService.SetGroupByAsync(groupBy);
        }

        [RelayCommand]
        private async Task SaveAsDefaultAsync()
        {
            await _viewModeService.SaveAsDefaultAsync();
        }

        [RelayCommand]
        private async Task ResetToDefaultAsync()
        {
            await _viewModeService.ResetToDefaultAsync();
            await LoadSettingsAsync();
        }
    }
}
