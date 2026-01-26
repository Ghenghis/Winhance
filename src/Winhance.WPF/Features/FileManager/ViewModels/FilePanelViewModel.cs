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
    /// ViewModel for a single file panel (wraps FileListViewModel).
    /// </summary>
    public partial class FilePanelViewModel : ObservableObject
    {
        private readonly IFileManagerService _fileManagerService;
        private readonly ITabService _tabService;
        private readonly IAddressBarService _addressBarService;
        
        [ObservableProperty]
        private FileListViewModel? _fileListViewModel;

        [ObservableProperty]
        private TabContainerViewModel? _tabContainerViewModel;

        [ObservableProperty]
        private AddressBarViewModel? _addressBarViewModel;

        [ObservableProperty]
        private string _currentPath = string.Empty;

        [ObservableProperty]
        private bool _isActive;

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private string _panelId = Guid.NewGuid().ToString();

        [ObservableProperty]
        private string _panelTitle = "Panel";

        public FilePanelViewModel(
            IFileManagerService fileManagerService,
            ITabService tabService,
            IAddressBarService addressBarService)
        {
            _fileManagerService = fileManagerService;
            _tabService = tabService;
            _addressBarService = addressBarService;
        }

        [RelayCommand]
        public async Task InitializeAsync(string? initialPath = null)
        {
            IsLoading = true;
            try
            {
                CurrentPath = initialPath ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                
                // Initialize child ViewModels would happen here
                // This is a wrapper/container ViewModel
                
                await LoadDirectoryAsync(CurrentPath);
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        public async Task LoadDirectoryAsync(string path)
        {
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
                return;

            IsLoading = true;
            try
            {
                CurrentPath = path;
                PanelTitle = Path.GetFileName(path) ?? path;
                
                // Notify address bar
                await _addressBarService.NavigateAsync(path);
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        public async Task NavigateToParentAsync()
        {
            var parent = Directory.GetParent(CurrentPath);
            if (parent != null)
            {
                await LoadDirectoryAsync(parent.FullName);
            }
        }

        [RelayCommand]
        public async Task RefreshAsync()
        {
            await LoadDirectoryAsync(CurrentPath);
        }

        [RelayCommand]
        public void Activate()
        {
            IsActive = true;
        }

        [RelayCommand]
        public void Deactivate()
        {
            IsActive = false;
        }
    }
}
