using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Winhance.WPF.Features.FileManager.ViewModels
{
    /// <summary>
    /// ViewModel for managing browser tabs within the File Manager.
    /// Provides tab creation, closing, navigation, and session management.
    /// </summary>
    public partial class TabContainerViewModel : ObservableObject
    {
        [ObservableProperty]
        private ObservableCollection<BrowserTabViewModel> _tabs = new();

        [ObservableProperty]
        private BrowserTabViewModel? _activeTab;

        [ObservableProperty]
        private bool _canCloseTab = true;

        private readonly ObservableCollection<BrowserTabViewModel> _closedTabs = new();
        private const int MaxClosedTabs = 10;

        public TabContainerViewModel()
        {
            // Create initial tab
            CreateTab(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        }

        public event EventHandler<BrowserTabViewModel>? TabActivated;
        public event EventHandler<BrowserTabViewModel>? TabClosed;
        public event EventHandler<string>? NavigationRequested;
        public event EventHandler<string>? TabNavigationRequested;

        [RelayCommand]
        public void CreateTab(string path)
        {
            var tab = new BrowserTabViewModel
            {
                Id = Guid.NewGuid().ToString(),
                Title = GetFolderName(path),
                CurrentPath = path,
                CreatedAt = DateTime.UtcNow,
                LastAccessed = DateTime.UtcNow,
            };

            tab.CloseRequested += OnTabCloseRequested;
            tab.NavigationRequested += OnTabNavigationRequested;

            Tabs.Add(tab);
            ActiveTab = tab;
            UpdateCanCloseTab();
        }

        [RelayCommand]
        public void CloseTab(BrowserTabViewModel? tab)
        {
            if (tab == null || Tabs.Count <= 1)
            {
                return;
            }

            var index = Tabs.IndexOf(tab);
            tab.CloseRequested -= OnTabCloseRequested;
            tab.NavigationRequested -= OnTabNavigationRequested;

            // Store for reopen
            _closedTabs.Insert(0, tab);
            if (_closedTabs.Count > MaxClosedTabs)
            {
                _closedTabs.RemoveAt(_closedTabs.Count - 1);
            }

            Tabs.Remove(tab);
            TabClosed?.Invoke(this, tab);

            // Activate adjacent tab
            if (ActiveTab == tab)
            {
                ActiveTab = Tabs.ElementAtOrDefault(Math.Min(index, Tabs.Count - 1));
            }

            UpdateCanCloseTab();
        }

        [RelayCommand]
        public void CloseOtherTabs(BrowserTabViewModel? tab)
        {
            if (tab == null)
            {
                return;
            }

            var tabsToClose = Tabs.Where(t => t != tab && !t.IsPinned).ToList();
            foreach (var t in tabsToClose)
            {
                CloseTab(t);
            }
        }

        [RelayCommand]
        public void CloseTabsToRight(BrowserTabViewModel? tab)
        {
            if (tab == null)
            {
                return;
            }

            var index = Tabs.IndexOf(tab);
            var tabsToClose = Tabs.Skip(index + 1).Where(t => !t.IsPinned).ToList();
            foreach (var t in tabsToClose)
            {
                CloseTab(t);
            }
        }

        [RelayCommand]
        public void DuplicateTab(BrowserTabViewModel? tab)
        {
            if (tab == null)
            {
                return;
            }

            CreateTab(tab.CurrentPath);
        }

        [RelayCommand]
        public void TogglePin(BrowserTabViewModel? tab)
        {
            if (tab == null)
            {
                return;
            }

            tab.IsPinned = !tab.IsPinned;

            // Move pinned tabs to the front
            if (tab.IsPinned)
            {
                var index = Tabs.IndexOf(tab);
                var firstUnpinnedIndex = Tabs.TakeWhile(t => t.IsPinned).Count();
                if (index > firstUnpinnedIndex)
                {
                    Tabs.Move(index, firstUnpinnedIndex);
                }
            }
        }

        public BrowserTabViewModel? ReopenClosedTab()
        {
            if (_closedTabs.Count == 0)
            {
                return null;
            }

            var tab = _closedTabs[0];
            _closedTabs.RemoveAt(0);

            tab.CloseRequested += OnTabCloseRequested;
            tab.NavigationRequested += OnTabNavigationRequested;

            Tabs.Add(tab);
            ActiveTab = tab;
            UpdateCanCloseTab();

            return tab;
        }

        [RelayCommand]
        public void ActivateTab(BrowserTabViewModel? tab)
        {
            if (tab == null || !Tabs.Contains(tab))
            {
                return;
            }

            ActiveTab = tab;
            tab.LastAccessed = DateTime.UtcNow;
            TabActivated?.Invoke(this, tab);
        }

        [RelayCommand]
        public void NavigateTab(string path)
        {
            if (ActiveTab == null)
            {
                return;
            }

            // Save to history
            if (!string.IsNullOrEmpty(ActiveTab.CurrentPath))
            {
                ActiveTab.BackHistory.Push(ActiveTab.CurrentPath);
                ActiveTab.ForwardHistory.Clear();
            }

            ActiveTab.CurrentPath = path;
            ActiveTab.Title = GetFolderName(path);
            ActiveTab.LastAccessed = DateTime.UtcNow;

            NavigationRequested?.Invoke(this, path);
        }

        public bool GoBack()
        {
            if (ActiveTab == null || !ActiveTab.CanGoBack)
            {
                return false;
            }

            ActiveTab.ForwardHistory.Push(ActiveTab.CurrentPath);
            var path = ActiveTab.BackHistory.Pop();
            ActiveTab.CurrentPath = path;
            ActiveTab.Title = GetFolderName(path);

            NavigationRequested?.Invoke(this, path);
            return true;
        }

        public bool GoForward()
        {
            if (ActiveTab == null || !ActiveTab.CanGoForward)
            {
                return false;
            }

            ActiveTab.BackHistory.Push(ActiveTab.CurrentPath);
            var path = ActiveTab.ForwardHistory.Pop();
            ActiveTab.CurrentPath = path;
            ActiveTab.Title = GetFolderName(path);

            NavigationRequested?.Invoke(this, path);
            return true;
        }

        public void MoveTab(int fromIndex, int toIndex)
        {
            if (fromIndex < 0 || fromIndex >= Tabs.Count ||
                toIndex < 0 || toIndex >= Tabs.Count ||
                fromIndex == toIndex)
            {
                return;
            }

            Tabs.Move(fromIndex, toIndex);
        }

        private void OnTabCloseRequested(object? sender, EventArgs e)
        {
            if (sender is BrowserTabViewModel tab)
            {
                CloseTab(tab);
            }
        }

        private void OnTabNavigationRequested(object? sender, string path)
        {
            NavigateTab(path);
            TabNavigationRequested?.Invoke(this, path);
        }

        private void UpdateCanCloseTab()
        {
            CanCloseTab = Tabs.Count > 1;
        }

        private static string GetFolderName(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return "New Tab";
            }

            // Check for drive root
            if (path.Length <= 3 && path.EndsWith(":\\"))
            {
                return path;
            }

            var name = System.IO.Path.GetFileName(path.TrimEnd('\\', '/'));
            return string.IsNullOrEmpty(name) ? path : name;
        }

        partial void OnActiveTabChanged(BrowserTabViewModel? value)
        {
            if (value != null)
            {
                TabActivated?.Invoke(this, value);
            }
        }
    }

    /// <summary>
    /// Represents a single browser tab.
    /// </summary>
    public partial class BrowserTabViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _id = string.Empty;

        [ObservableProperty]
        private string _title = "New Tab";

        [ObservableProperty]
        private string _currentPath = string.Empty;

        [ObservableProperty]
        private bool _isPinned;

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private DateTime _createdAt = DateTime.UtcNow;

        [ObservableProperty]
        private DateTime _lastAccessed = DateTime.UtcNow;

        public System.Collections.Generic.Stack<string> BackHistory { get; } = new();
        public System.Collections.Generic.Stack<string> ForwardHistory { get; } = new();

        public bool CanGoBack => BackHistory.Count > 0;
        public bool CanGoForward => ForwardHistory.Count > 0;

        public event EventHandler? CloseRequested;
        public event EventHandler<string>? NavigationRequested;

        [RelayCommand]
        public void RequestClose()
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }

        [RelayCommand]
        public void Navigate(string path)
        {
            NavigationRequested?.Invoke(this, path);
        }
    }
}
