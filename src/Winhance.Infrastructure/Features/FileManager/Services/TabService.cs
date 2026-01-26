using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.FileManager.Interfaces;

namespace Winhance.Infrastructure.Features.FileManager.Services
{
    /// <summary>
    /// Service for managing file browser tabs with persistence and history.
    /// </summary>
    public class TabService : ITabService
    {
        private readonly ILogService _logService;
        private readonly ConcurrentQueue<FileTab> _closedTabs = new();
        private readonly string _sessionsPath;
        private const int MaxClosedTabs = 20;
        private const string SessionsFolder = "TabSessions";

        public TabService(ILogService logService)
        {
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
            _sessionsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Winhance",
                SessionsFolder);

            Directory.CreateDirectory(_sessionsPath);
        }

        public ObservableCollection<FileTab> Tabs { get; } = new();

        private FileTab? _activeTab;
        public FileTab? ActiveTab
        {
            get => _activeTab;
            set
            {
                if (_activeTab != value)
                {
                    if (_activeTab != null)
                    {
                        _activeTab.IsActive = false;
                    }

                    _activeTab = value;

                    if (_activeTab != null)
                    {
                        _activeTab.IsActive = true;
                        _activeTab.LastAccessed = DateTime.UtcNow;
                    }

                    ActiveTabChanged?.Invoke(this, _activeTab);
                }
            }
        }

        public event EventHandler<FileTab?>? ActiveTabChanged;

        public FileTab CreateTab(string path, bool activate = true)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                path = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            }

            var tab = new FileTab
            {
                CurrentPath = path,
                Title = GetTabTitle(path)
            };

            Tabs.Add(tab);

            if (activate || Tabs.Count == 1)
            {
                ActiveTab = tab;
            }

            _logService.Log(LogLevel.Debug, $"Created new tab at {path}");
            return tab;
        }

        public void CloseTab(FileTab tab)
        {
            if (tab == null || !Tabs.Contains(tab))
            {
                return;
            }

            if (tab.IsPinned)
            {
                _logService.Log(LogLevel.Warning, "Attempted to close pinned tab");
                return;
            }

            // Add to closed tabs for reopening
            _closedTabs.Enqueue(tab);
            while (_closedTabs.Count > MaxClosedTabs)
            {
                _closedTabs.TryDequeue(out _);
            }

            // Remove from collection
            var tabIndex = Tabs.IndexOf(tab);
            Tabs.Remove(tab);

            // Activate another tab if this was active
            if (ActiveTab == tab)
            {
                if (Tabs.Count > 0)
                {
                    // Try to activate the tab to the right, or the last tab
                    if (tabIndex < Tabs.Count)
                    {
                        ActiveTab = Tabs[tabIndex];
                    }
                    else
                    {
                        ActiveTab = Tabs[^1];
                    }
                }
                else
                {
                    ActiveTab = null;
                }
            }

            _logService.Log(LogLevel.Debug, $"Closed tab {tab.Title}");
        }

        public void CloseOtherTabs(FileTab tab)
        {
            if (tab == null || !Tabs.Contains(tab))
            {
                return;
            }

            var tabsToClose = Tabs.Where(t => t != tab && !t.IsPinned).ToList();
            foreach (var t in tabsToClose)
            {
                CloseTab(t);
            }
        }

        public void CloseTabsToRight(FileTab tab)
        {
            if (tab == null || !Tabs.Contains(tab))
            {
                return;
            }

            var tabIndex = Tabs.IndexOf(tab);
            var tabsToClose = Tabs.Skip(tabIndex + 1).Where(t => !t.IsPinned).ToList();
            foreach (var t in tabsToClose)
            {
                CloseTab(t);
            }
        }

        public FileTab DuplicateTab(FileTab tab)
        {
            if (tab == null)
            {
                throw new ArgumentNullException(nameof(tab));
            }

            var clone = tab.Clone();
            clone.Title = $"{tab.Title} (Copy)";
            clone.IsActive = false;

            var insertIndex = Tabs.IndexOf(tab) + 1;
            Tabs.Insert(insertIndex, clone);

            _logService.Log(LogLevel.Debug, $"Duplicated tab {tab.Title}");
            return clone;
        }

        public void TogglePin(FileTab tab)
        {
            if (tab == null)
            {
                return;
            }

            tab.IsPinned = !tab.IsPinned;
            _logService.Log(LogLevel.Debug, $"Tab {tab.Title} pin state: {tab.IsPinned}");
        }

        public FileTab? ReopenClosedTab()
        {
            if (_closedTabs.TryDequeue(out var tab))
            {
                // Create a new tab with the same state
                var newTab = new FileTab
                {
                    CurrentPath = tab.CurrentPath,
                    Title = tab.Title,
                    BackHistory = new Stack<string>(tab.BackHistory.Reverse()),
                    ForwardHistory = new Stack<string>(tab.ForwardHistory.Reverse())
                };

                Tabs.Add(newTab);
                ActiveTab = newTab;

                _logService.Log(LogLevel.Debug, $"Reopened tab {tab.Title}");
                return newTab;
            }

            return null;
        }

        public void NavigateTo(FileTab tab, string path)
        {
            if (tab == null || string.IsNullOrEmpty(path))
            {
                return;
            }

            // Add current path to back history
            if (!string.IsNullOrEmpty(tab.CurrentPath) && tab.CurrentPath != path)
            {
                tab.BackHistory.Push(tab.CurrentPath);
            }

            // Clear forward history when navigating to new path
            tab.ForwardHistory.Clear();

            tab.CurrentPath = path;
            tab.Title = GetTabTitle(path);
            tab.LastAccessed = DateTime.UtcNow;

            // Notify property changes for history buttons
            tab.NotifyNavigationChanged();

            _logService.Log(LogLevel.Debug, $"Navigated tab to {path}");
        }

        public bool GoBack(FileTab tab)
        {
            if (tab == null || tab.BackHistory.Count == 0)
            {
                return false;
            }

            var currentPath = tab.CurrentPath;
            var previousPath = tab.BackHistory.Pop();

            // Add current path to forward history
            tab.ForwardHistory.Push(currentPath);

            tab.CurrentPath = previousPath;
            tab.Title = GetTabTitle(previousPath);
            tab.LastAccessed = DateTime.UtcNow;

            // Notify property changes
            tab.NotifyNavigationChanged();

            _logService.Log(LogLevel.Debug, $"Navigated back to {previousPath}");
            return true;
        }

        public bool GoForward(FileTab tab)
        {
            if (tab == null || tab.ForwardHistory.Count == 0)
            {
                return false;
            }

            var currentPath = tab.CurrentPath;
            var nextPath = tab.ForwardHistory.Pop();

            // Add current path to back history
            tab.BackHistory.Push(currentPath);

            tab.CurrentPath = nextPath;
            tab.Title = GetTabTitle(nextPath);
            tab.LastAccessed = DateTime.UtcNow;

            // Notify property changes
            tab.NotifyNavigationChanged();

            _logService.Log(LogLevel.Debug, $"Navigated forward to {nextPath}");
            return true;
        }

        public async Task SaveSessionAsync(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Session name cannot be empty", nameof(name));
            }

            try
            {
                var session = new TabSession
                {
                    Name = name,
                    ActiveTabIndex = ActiveTab != null ? Tabs.IndexOf(ActiveTab) : 0,
                    Tabs = Tabs.Select(tab => new SavedTab
                    {
                        Title = tab.Title,
                        CurrentPath = tab.CurrentPath,
                        IsPinned = tab.IsPinned,
                        BackHistory = tab.BackHistory.Reverse().ToList(),
                        ForwardHistory = tab.ForwardHistory.Reverse().ToList()
                    }).ToList()
                };

                var filePath = Path.Combine(_sessionsPath, $"{name}.json");
                var json = JsonSerializer.Serialize(session, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(filePath, json);

                _logService.Log(LogLevel.Info, $"Saved session '{name}' with {Tabs.Count} tabs");
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Failed to save session '{name}': {ex.Message}");
                throw;
            }
        }

        public async Task<bool> LoadSessionAsync(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            try
            {
                var filePath = Path.Combine(_sessionsPath, $"{name}.json");
                if (!File.Exists(filePath))
                {
                    return false;
                }

                var json = await File.ReadAllTextAsync(filePath);
                var session = JsonSerializer.Deserialize<TabSession>(json);

                if (session == null)
                {
                    return false;
                }

                // Clear current tabs
                Tabs.Clear();

                // Restore tabs
                foreach (var savedTab in session.Tabs)
                {
                    var tab = new FileTab
                    {
                        Title = savedTab.Title,
                        CurrentPath = savedTab.CurrentPath,
                        IsPinned = savedTab.IsPinned,
                        BackHistory = new Stack<string>(savedTab.BackHistory.AsEnumerable().Reverse()),
                        ForwardHistory = new Stack<string>(savedTab.ForwardHistory.AsEnumerable().Reverse())
                    };
                    Tabs.Add(tab);
                }

                // Set active tab
                if (session.ActiveTabIndex >= 0 && session.ActiveTabIndex < Tabs.Count)
                {
                    ActiveTab = Tabs[session.ActiveTabIndex];
                }
                else if (Tabs.Count > 0)
                {
                    ActiveTab = Tabs[0];
                }

                _logService.Log(LogLevel.Info, $"Loaded session '{name}' with {Tabs.Count} tabs");
                return true;
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Failed to load session '{name}': {ex.Message}");
                return false;
            }
        }

        public IEnumerable<string> GetSavedSessions()
        {
            try
            {
                return Directory.GetFiles(_sessionsPath, "*.json")
                    .Select(Path.GetFileNameWithoutExtension)
                    .Where(name => !string.IsNullOrEmpty(name))
                    .OrderBy(name => name);
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Warning, $"Failed to get saved sessions: {ex.Message}");
                return Enumerable.Empty<string>();
            }
        }

        public void DeleteSession(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return;
            }

            try
            {
                var filePath = Path.Combine(_sessionsPath, $"{name}.json");
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    _logService.Log(LogLevel.Info, $"Deleted session '{name}'");
                }
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Failed to delete session '{name}': {ex.Message}");
            }
        }

        public FileTab? GetNextTab(FileTab? current = null)
        {
            current ??= ActiveTab;
            if (current == null || Tabs.Count <= 1)
            {
                return null;
            }

            var currentIndex = Tabs.IndexOf(current);
            if (currentIndex < 0)
            {
                return Tabs[0];
            }

            var nextIndex = (currentIndex + 1) % Tabs.Count;
            return Tabs[nextIndex];
        }

        public FileTab? GetPreviousTab(FileTab? current = null)
        {
            current ??= ActiveTab;
            if (current == null || Tabs.Count <= 1)
            {
                return null;
            }

            var currentIndex = Tabs.IndexOf(current);
            if (currentIndex < 0)
            {
                return Tabs[^1];
            }

            var prevIndex = currentIndex == 0 ? Tabs.Count - 1 : currentIndex - 1;
            return Tabs[prevIndex];
        }

        public FileTab? GetTabByIndex(int index)
        {
            return index >= 0 && index < Tabs.Count ? Tabs[index] : null;
        }

        public int GetTabIndex(FileTab tab)
        {
            return Tabs.IndexOf(tab);
        }

        private static string GetTabTitle(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return "New Tab";
            }

            // Special folders
            if (path.Equals(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), StringComparison.OrdinalIgnoreCase))
            {
                return "Home";
            }

            if (path.Equals(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), StringComparison.OrdinalIgnoreCase))
            {
                return "Desktop";
            }

            if (path.Equals(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), StringComparison.OrdinalIgnoreCase))
            {
                return "Documents";
            }

            // Drive letters
            if (path.Length == 3 && path[1] == ':' && path[2] == '\\')
            {
                return path[0].ToString();
            }

            // Get folder name
            var folderName = Path.GetFileName(path.TrimEnd('\\'));
            return string.IsNullOrEmpty(folderName) ? path : folderName;
        }
    }
}
