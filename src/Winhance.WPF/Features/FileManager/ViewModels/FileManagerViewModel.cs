using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Winhance.Core.Features.FileManager.Interfaces;

namespace Winhance.WPF.Features.FileManager.ViewModels
{
    /// <summary>
    /// Main ViewModel for the File Manager dashboard.
    /// Integrates with Rust nexus_core for ultra-fast file operations.
    /// </summary>
    public partial class FileManagerViewModel : ObservableObject
    {
        private readonly IFileManagerService? _fileManagerService;
        private readonly IBatchRenameService? _batchRenameService;
        private readonly IOrganizerService? _organizerService;
        private readonly INexusIndexerService? _nexusIndexer;

        [ObservableProperty]
        private bool _isBrowserTabSelected = true;

        [ObservableProperty]
        private bool _isBatchRenameTabSelected;

        [ObservableProperty]
        private bool _isOrganizerTabSelected;

        [ObservableProperty]
        private bool _isSpaceRecoveryTabSelected;

        [ObservableProperty]
        private string _searchText = string.Empty;

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private string _statusMessage = "Ready";

        [ObservableProperty]
        private object? _currentView;

        [ObservableProperty]
        private bool _isIndexing;

        [ObservableProperty]
        private long _indexedFileCount;

        [ObservableProperty]
        private ObservableCollection<NexusFileEntry> _searchResults = new();

        public DualPaneBrowserViewModel? BrowserViewModel { get; private set; }
        public BatchRenameViewModel? BatchRenameViewModel { get; private set; }
        public OrganizerViewModel? OrganizerViewModel { get; private set; }
        public bool IsNexusAvailable => _nexusIndexer?.IsAvailable ?? false;

        public FileManagerViewModel()
        {
            // Design-time constructor
            InitializeViewModels();
        }

        public FileManagerViewModel(
            IFileManagerService fileManagerService,
            IBatchRenameService batchRenameService,
            IOrganizerService organizerService,
            INexusIndexerService nexusIndexer)
        {
            _fileManagerService = fileManagerService;
            _batchRenameService = batchRenameService;
            _organizerService = organizerService;
            _nexusIndexer = nexusIndexer;
            
            InitializeViewModels();
        }

        private void InitializeViewModels()
        {
            BrowserViewModel = new DualPaneBrowserViewModel(_fileManagerService);
            BatchRenameViewModel = new BatchRenameViewModel(_batchRenameService);
            OrganizerViewModel = new OrganizerViewModel(_organizerService);
            
            CurrentView = BrowserViewModel;
        }

        [RelayCommand]
        private void SelectBrowserTab()
        {
            IsBrowserTabSelected = true;
            IsBatchRenameTabSelected = false;
            IsOrganizerTabSelected = false;
            IsSpaceRecoveryTabSelected = false;
            CurrentView = BrowserViewModel;
        }

        [RelayCommand]
        private void SelectBatchRenameTab()
        {
            IsBrowserTabSelected = false;
            IsBatchRenameTabSelected = true;
            IsOrganizerTabSelected = false;
            IsSpaceRecoveryTabSelected = false;
            CurrentView = BatchRenameViewModel;
        }

        [RelayCommand]
        private void SelectOrganizerTab()
        {
            IsBrowserTabSelected = false;
            IsBatchRenameTabSelected = false;
            IsOrganizerTabSelected = true;
            IsSpaceRecoveryTabSelected = false;
            CurrentView = OrganizerViewModel;
        }

        [RelayCommand]
        private void SelectSpaceRecoveryTab()
        {
            IsBrowserTabSelected = false;
            IsBatchRenameTabSelected = false;
            IsOrganizerTabSelected = false;
            IsSpaceRecoveryTabSelected = true;
        }

        [RelayCommand]
        private async Task RefreshAsync()
        {
            IsLoading = true;
            StatusMessage = "Refreshing...";

            try
            {
                if (IsBrowserTabSelected && BrowserViewModel != null)
                {
                    await BrowserViewModel.RefreshAsync();
                }
                else if (IsBatchRenameTabSelected && BatchRenameViewModel != null)
                {
                    await BatchRenameViewModel.RefreshPreviewAsync();
                }
                else if (IsOrganizerTabSelected && OrganizerViewModel != null)
                {
                    await OrganizerViewModel.AnalyzeAsync();
                }

                StatusMessage = "Ready";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private void ShowHelp()
        {
            // TODO: Show help dialog
        }

        /// <summary>
        /// Index all drives using Rust MFT reader (ultra-fast).
        /// </summary>
        [RelayCommand]
        private async Task IndexAllDrivesAsync()
        {
            if (_nexusIndexer == null || !_nexusIndexer.IsAvailable)
            {
                StatusMessage = "Nexus indexer not available";
                return;
            }

            IsIndexing = true;
            StatusMessage = "Indexing drives (MFT scan)...";

            try
            {
                var count = await _nexusIndexer.IndexAllDrivesAsync();
                IndexedFileCount = count;
                StatusMessage = count >= 0 
                    ? $"Indexed {count:N0} files" 
                    : $"Indexing failed: {_nexusIndexer.GetLastError()}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Index error: {ex.Message}";
            }
            finally
            {
                IsIndexing = false;
            }
        }

        /// <summary>
        /// Search indexed files using Rust backend.
        /// </summary>
        [RelayCommand]
        private async Task SearchNexusAsync()
        {
            if (_nexusIndexer == null || !_nexusIndexer.IsAvailable || string.IsNullOrWhiteSpace(SearchText))
            {
                return;
            }

            IsLoading = true;
            StatusMessage = $"Searching '{SearchText}'...";

            try
            {
                var results = await _nexusIndexer.SearchAsync(SearchText, 500);
                SearchResults.Clear();
                foreach (var result in results)
                {
                    SearchResults.Add(result);
                }
                StatusMessage = $"Found {SearchResults.Count} results";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Search error: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        partial void OnSearchTextChanged(string value)
        {
            // Filter current view based on search text
            if (IsBrowserTabSelected && BrowserViewModel != null)
            {
                BrowserViewModel.FilterText = value;
            }
            
            // Trigger Nexus search if available and text is substantial
            if (_nexusIndexer?.IsAvailable == true && value.Length >= 2)
            {
                _ = SearchNexusAsync();
            }
        }
    }
}
