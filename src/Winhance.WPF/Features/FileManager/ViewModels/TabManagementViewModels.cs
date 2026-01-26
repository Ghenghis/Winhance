using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Winhance.Core.Features.FileManager.Interfaces;
using Winhance.Core.Features.FileManager.Models;

namespace Winhance.WPF.Features.FileManager.ViewModels
{
    /// <summary>
    /// ViewModel for tab drag reordering
    /// </summary>
    public partial class TabDragReorderViewModel : ObservableObject
    {
        private readonly ITabService _tabService;
        private ObservableCollection<TabViewModel> _tabs = new();

        public ObservableCollection<TabViewModel> Tabs
        {
            get => _tabs;
            set => SetProperty(ref _tabs, value);
        }

        public TabDragReorderViewModel(ITabService tabService)
        {
            _tabService = tabService;
        }

        public void MoveTab(int fromIndex, int toIndex)
        {
            if (fromIndex < 0 || fromIndex >= Tabs.Count || toIndex < 0 || toIndex >= Tabs.Count)
                return;

            var tab = Tabs[fromIndex];
            Tabs.RemoveAt(fromIndex);
            Tabs.Insert(toIndex, tab);

            // Notify tab service of the change
            _tabService.ReorderTab(fromIndex, toIndex);
        }
    }

    /// <summary>
    /// ViewModel for tab drag between panels
    /// </summary>
    public partial class TabDragBetweenPanelsViewModel : ObservableObject
    {
        [ObservableProperty]
        private TabViewModel? _draggedTab;

        [ObservableProperty]
        private string? _sourcePanel;

        [RelayCommand]
        private void DropTabOnPanel(string targetPanel)
        {
            if (DraggedTab == null || string.IsNullOrEmpty(SourcePanel) || string.IsNullOrEmpty(targetPanel))
                return;

            try
            {
                await _tabService.MoveTabBetweenPanelsAsync(DraggedTab.Id, SourcePanel, targetPanel);
                System.Diagnostics.Debug.WriteLine($"Moved tab '{DraggedTab.Title}' from {SourcePanel} to {targetPanel}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error moving tab: {ex.Message}");
            }
            finally
            {
                DraggedTab = null;
                SourcePanel = null;
            }
        }
    }

    /// <summary>
    /// ViewModel for tab duplication
    /// </summary>
    public partial class TabDuplicationViewModel : ObservableObject
    {
        private readonly ITabService _tabService;

        public TabDuplicationViewModel(ITabService tabService)
        {
            _tabService = tabService;
        }

        [RelayCommand]
        private async Task DuplicateTabAsync(TabViewModel tab)
        {
            if (tab == null) return;

            var newTab = await _tabService.DuplicateTabAsync(tab.Id);
            
            TabDuplicated?.Invoke(this, new TabDuplicatedEventArgs { OriginalTab = tab, NewTab = newTab });
            System.Diagnostics.Debug.WriteLine($"Duplicated tab '{tab.Title}'");
        }
    }

    /// <summary>
    /// ViewModel for tab closing options
    /// </summary>
    public partial class TabCloseOptionsViewModel : ObservableObject
    {
        private readonly ITabService _tabService;

        public TabCloseOptionsViewModel(ITabService tabService)
        {
            _tabService = tabService;
        }

        [RelayCommand]
        private async Task CloseOtherTabsAsync(TabViewModel keepTab)
        {
            if (keepTab == null) return;

            var allTabs = await _tabService.GetAllTabsAsync();
            foreach (var tab in allTabs.Where(t => t.Id != keepTab.Id))
            {
                await _tabService.CloseTabAsync(tab.Id);
            }
        }

        [RelayCommand]
        private async Task CloseTabsToRightAsync(TabViewModel tab)
        {
            if (tab == null) return;

            var allTabs = await _tabService.GetAllTabsAsync();
            var tabIndex = allTabs.ToList().FindIndex(t => t.Id == tab.Id);
            
            for (int i = tabIndex + 1; i < allTabs.Count(); i++)
            {
                await _tabService.CloseTabAsync(allTabs.ElementAt(i).Id);
            }
        }
    }

    /// <summary>
    /// ViewModel for tab pinning
    /// </summary>
    public partial class TabPinningViewModel : ObservableObject
    {
        [ObservableProperty]
        private ObservableCollection<TabViewModel> _pinnedTabs = new();

        [ObservableProperty]
        private ObservableCollection<TabViewModel> _unpinnedTabs = new();

        [RelayCommand]
        private void PinTab(TabViewModel tab)
        {
            if (tab == null) return;

            tab.IsPinned = true;
            UnpinnedTabs.Remove(tab);
            PinnedTabs.Insert(0, tab); // Insert at beginning of pinned tabs
        }

        [RelayCommand]
        private void UnpinTab(TabViewModel tab)
        {
            if (tab == null) return;

            tab.IsPinned = false;
            PinnedTabs.Remove(tab);
            UnpinnedTabs.Add(tab);
        }
    }

    /// <summary>
    /// ViewModel for tab color coding
    /// </summary>
    public partial class TabColorCodingViewModel : ObservableObject
    {
        [RelayCommand]
        private void SetTabColor(TabViewModel tab, string color)
        {
            if (tab == null) return;

            tab.Color = color;
        }

        [RelayCommand]
        private void ClearTabColor(TabViewModel tab)
        {
            if (tab == null) return;

            tab.Color = null;
        }
    }

    /// <summary>
    /// ViewModel for tab icon display
    /// </summary>
    public partial class TabIconViewModel : ObservableObject
    {
        [RelayCommand]
        private string GetTabIcon(TabViewModel tab)
        {
            if (tab == null) return "📁";

            return tab.Path?.ToLowerInvariant() switch
            {
                var path when path.Contains("desktop") => "🖥️",
                var path when path.Contains("documents") => "📄",
                var path when path.Contains("downloads") => "⬇️",
                var path when path.Contains("pictures") => "🖼️",
                var path when path.Contains("music") => "🎵",
                var path when path.Contains("videos") => "🎬",
                _ => "📁"
            };
        }
    }

    /// <summary>
    /// ViewModel for tab overflow scrolling
    /// </summary>
    public partial class TabOverflowViewModel : ObservableObject
    {
        [ObservableProperty]
        private bool _hasOverflow;

        [ObservableProperty]
        private double _tabContainerWidth;

        [ObservableProperty]
        private double _totalTabsWidth;

        [RelayCommand]
        private void ScrollTabsLeft()
        {
            const double scrollAmount = 100;
            HorizontalOffset = Math.Max(0, HorizontalOffset - scrollAmount);
            System.Diagnostics.Debug.WriteLine($"Scrolling tabs left: offset={HorizontalOffset}");
        }

        [RelayCommand]
        private void ScrollTabsRight()
        {
            const double scrollAmount = 100;
            var maxOffset = Math.Max(0, TotalTabsWidth - ViewportWidth);
            HorizontalOffset = Math.Min(maxOffset, HorizontalOffset + scrollAmount);
            System.Diagnostics.Debug.WriteLine($"Scrolling tabs right: offset={HorizontalOffset}");
        }

        partial void OnTotalTabsWidthChanged(double value)
        {
            HasOverflow = value > TabContainerWidth;
        }
    }

    /// <summary>
    /// ViewModel for tab session restore
    /// </summary>
    public partial class TabSessionRestoreViewModel : ObservableObject
    {
        private readonly ITabService _tabService;

        public TabSessionRestoreViewModel(ITabService tabService)
        {
            _tabService = tabService;
        }

        [RelayCommand]
        private async Task SaveSessionAsync()
        {
            await _tabService.SaveSessionAsync();
        }

        [RelayCommand]
        private async Task RestoreSessionAsync()
        {
            await _tabService.RestoreSessionAsync();
        }

        [RelayCommand]
        private async Task ClearSessionAsync()
        {
            await _tabService.ClearSessionAsync();
        }
    }

    /// <summary>
    /// ViewModel for tab keyboard shortcuts
    /// </summary>
    public partial class TabKeyboardShortcutsViewModel : ObservableObject
    {
        private readonly ITabService _tabService;

        public TabKeyboardShortcutsViewModel(ITabService tabService)
        {
            _tabService = tabService;
        }

        [RelayCommand]
        private async Task SelectTabByNumberAsync(int number)
        {
            if (number < 1 || number > 9) return;

            var tabs = await _tabService.GetAllTabsAsync();
            if (number <= tabs.Count())
            {
                await _tabService.SetActiveTabAsync(tabs.ElementAt(number - 1).Id);
            }
        }

        [RelayCommand]
        private async Task NextTabAsync()
        {
            await _tabService.NextTabAsync();
        }

        [RelayCommand]
        private async Task PreviousTabAsync()
        {
            await _tabService.PreviousTabAsync();
        }
    }
}
