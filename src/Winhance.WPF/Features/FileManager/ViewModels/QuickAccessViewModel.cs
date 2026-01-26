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
    /// ViewModel for quick access panel.
    /// </summary>
    public partial class QuickAccessViewModel : ObservableObject
    {
        private readonly IFavoritesService _favoritesService;
        private readonly IAddressBarService _addressBarService;

        [ObservableProperty]
        private ObservableCollection<QuickAccessItemViewModel> _recentLocations = new();

        [ObservableProperty]
        private ObservableCollection<QuickAccessItemViewModel> _frequentLocations = new();

        [ObservableProperty]
        private ObservableCollection<QuickAccessItemViewModel> _specialFolders = new();

        [ObservableProperty]
        private QuickAccessItemViewModel? _selectedItem;

        [ObservableProperty]
        private bool _showRecent = true;

        [ObservableProperty]
        private bool _showFrequent = true;

        [ObservableProperty]
        private bool _showSpecial = true;

        public QuickAccessViewModel(IFavoritesService favoritesService, IAddressBarService addressBarService)
        {
            _favoritesService = favoritesService;
            _addressBarService = addressBarService;

            _ = LoadQuickAccessItemsAsync();
        }

        [RelayCommand]
        public async Task LoadQuickAccessItemsAsync()
        {
            await Task.WhenAll(
                LoadRecentLocationsAsync(),
                LoadFrequentLocationsAsync(),
                LoadSpecialFoldersAsync()
            );
        }

        private async Task LoadRecentLocationsAsync()
        {
            try
            {
                var recent = await _favoritesService.GetRecentLocationsAsync(10);
                RecentLocations = new ObservableCollection<QuickAccessItemViewModel>(
                    recent.Select(r => new QuickAccessItemViewModel
                    {
                        Name = Path.GetFileName(r) ?? r,
                        FullPath = r,
                        Icon = "🕐",
                        Type = QuickAccessType.Recent
                    }));
            }
            catch
            {
                RecentLocations = new ObservableCollection<QuickAccessItemViewModel>();
            }
        }

        private async Task LoadFrequentLocationsAsync()
        {
            try
            {
                var frequent = await _favoritesService.GetFrequentLocationsAsync(10);
                FrequentLocations = new ObservableCollection<QuickAccessItemViewModel>(
                    frequent.Select(f => new QuickAccessItemViewModel
                    {
                        Name = Path.GetFileName(f) ?? f,
                        FullPath = f,
                        Icon = "⭐",
                        Type = QuickAccessType.Frequent
                    }));
            }
            catch
            {
                FrequentLocations = new ObservableCollection<QuickAccessItemViewModel>();
            }
        }

        private async Task LoadSpecialFoldersAsync()
        {
            await Task.Run(() =>
            {
                var folders = new[]
                {
                    (Environment.SpecialFolder.Desktop, "Desktop", "🖥️"),
                    (Environment.SpecialFolder.Documents, "Documents", "📄"),
                    (Environment.SpecialFolder.Downloads, "Downloads", "⬇️"),
                    (Environment.SpecialFolder.Pictures, "Pictures", "🖼️"),
                    (Environment.SpecialFolder.Music, "Music", "🎵"),
                    (Environment.SpecialFolder.Videos, "Videos", "🎬")
                };

                var items = new ObservableCollection<QuickAccessItemViewModel>();
                foreach (var (folder, name, icon) in folders)
                {
                    var path = Environment.GetFolderPath(folder);
                    if (Directory.Exists(path))
                    {
                        items.Add(new QuickAccessItemViewModel
                        {
                            Name = name,
                            FullPath = path,
                            Icon = icon,
                            Type = QuickAccessType.Special
                        });
                    }
                }

                SpecialFolders = items;
            });
        }

        [RelayCommand]
        public async Task NavigateToItemAsync(QuickAccessItemViewModel? item)
        {
            if (item?.FullPath == null) return;

            if (!Directory.Exists(item.FullPath))
            {
                await _addressBarService.ShowErrorAsync("Folder does not exist");
                return;
            }

            await _addressBarService.NavigateAsync(item.FullPath);
            await _favoritesService.UpdateAccessStatsAsync(item.FullPath);
        }

        [RelayCommand]
        public async Task RemoveRecentAsync(QuickAccessItemViewModel? item)
        {
            if (item == null) return;

            try
            {
                await _favoritesService.RemoveFromRecentAsync(item.FullPath);
                RecentLocations.Remove(item);
            }
            catch (Exception ex)
            {
                await _addressBarService.ShowErrorAsync($"Cannot remove: {ex.Message}");
            }
        }

        [RelayCommand]
        public async Task ClearRecentAsync()
        {
            try
            {
                await _favoritesService.ClearRecentAsync();
                RecentLocations.Clear();
            }
            catch (Exception ex)
            {
                await _addressBarService.ShowErrorAsync($"Cannot clear recent: {ex.Message}");
            }
        }

        [RelayCommand]
        public async Task RefreshAsync()
        {
            await LoadQuickAccessItemsAsync();
        }
    }

    public partial class QuickAccessItemViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _name = string.Empty;

        [ObservableProperty]
        private string _fullPath = string.Empty;

        [ObservableProperty]
        private string _icon = "📁";

        [ObservableProperty]
        private QuickAccessType _type;

        [ObservableProperty]
        private int _accessCount;

        [ObservableProperty]
        private DateTime _lastAccessed = DateTime.Now;
    }

    public enum QuickAccessType
    {
        Recent,
        Frequent,
        Special
    }
}
