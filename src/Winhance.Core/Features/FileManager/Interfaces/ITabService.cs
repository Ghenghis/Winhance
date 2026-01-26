using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Winhance.Core.Features.FileManager.Interfaces
{
    /// <summary>
    /// Service for managing file browser tabs with persistence and history.
    /// </summary>
    public interface ITabService
    {
        /// <summary>
        /// Observable collection of open tabs.
        /// </summary>
        ObservableCollection<FileTab> Tabs { get; }

        /// <summary>
        /// Currently active tab.
        /// </summary>
        FileTab? ActiveTab { get; set; }

        /// <summary>
        /// Event raised when active tab changes.
        /// </summary>
        event EventHandler<FileTab?>? ActiveTabChanged;

        /// <summary>
        /// Create a new tab at the specified path.
        /// </summary>
        /// <param name="path">Initial path for the tab.</param>
        /// <param name="activate">Whether to activate the new tab.</param>
        /// <returns>The created tab.</returns>
        FileTab CreateTab(string path, bool activate = true);

        /// <summary>
        /// Close a specific tab.
        /// </summary>
        /// <param name="tab">Tab to close.</param>
        void CloseTab(FileTab tab);

        /// <summary>
        /// Close all tabs except the specified one.
        /// </summary>
        /// <param name="tab">Tab to keep open.</param>
        void CloseOtherTabs(FileTab tab);

        /// <summary>
        /// Close tabs to the right of the specified tab.
        /// </summary>
        /// <param name="tab">Reference tab.</param>
        void CloseTabsToRight(FileTab tab);

        /// <summary>
        /// Duplicate a tab with its history.
        /// </summary>
        /// <param name="tab">Tab to duplicate.</param>
        /// <returns>The duplicated tab.</returns>
        FileTab DuplicateTab(FileTab tab);

        /// <summary>
        /// Pin/unpin a tab.
        /// </summary>
        /// <param name="tab">Tab to pin/unpin.</param>
        void TogglePin(FileTab tab);

        /// <summary>
        /// Reopen the last closed tab.
        /// </summary>
        /// <returns>The reopened tab or null if none available.</returns>
        FileTab? ReopenClosedTab();

        /// <summary>
        /// Navigate a tab to a new path.
        /// </summary>
        /// <param name="tab">Tab to navigate.</param>
        /// <param name="path">New path.</param>
        void NavigateTo(FileTab tab, string path);

        /// <summary>
        /// Go back in tab history.
        /// </summary>
        /// <param name="tab">Tab to navigate.</param>
        /// <returns>True if navigation succeeded.</returns>
        bool GoBack(FileTab tab);

        /// <summary>
        /// Go forward in tab history.
        /// </summary>
        /// <param name="tab">Tab to navigate.</param>
        /// <returns>True if navigation succeeded.</returns>
        bool GoForward(FileTab tab);

        /// <summary>
        /// Save current session.
        /// </summary>
        /// <param name="name">Session name.</param>
        Task SaveSessionAsync(string name);

        /// <summary>
        /// Load a saved session.
        /// </summary>
        /// <param name="name">Session name.</param>
        /// <returns>True if session loaded successfully.</returns>
        Task<bool> LoadSessionAsync(string name);

        /// <summary>
        /// Get list of saved sessions.
        /// </summary>
        /// <returns>Collection of session names.</returns>
        IEnumerable<string> GetSavedSessions();

        /// <summary>
        /// Delete a saved session.
        /// </summary>
        /// <param name="name">Session name to delete.</param>
        void DeleteSession(string name);

        /// <summary>
        /// Get the next tab in order.
        /// </summary>
        /// <param name="current">Current tab.</param>
        /// <returns>Next tab or null if at end.</returns>
        FileTab? GetNextTab(FileTab? current = null);

        /// <summary>
        /// Get the previous tab in order.
        /// </summary>
        /// <param name="current">Current tab.</param>
        /// <returns>Previous tab or null if at start.</returns>
        FileTab? GetPreviousTab(FileTab? current = null);

        /// <summary>
        /// Get tab by index.
        /// </summary>
        /// <param name="index">Tab index.</param>
        /// <returns>Tab at index or null if invalid.</returns>
        FileTab? GetTabByIndex(int index);

        /// <summary>
        /// Get the index of a tab.
        /// </summary>
        /// <param name="tab">Tab to find.</param>
        /// <returns>Tab index or -1 if not found.</returns>
        int GetTabIndex(FileTab tab);
    }

    /// <summary>
    /// Represents a file browser tab with navigation history.
    /// </summary>
    public class FileTab : ObservableObject
    {
        private string _title = string.Empty;
        private string _currentPath = string.Empty;
        private bool _isPinned;
        private bool _isActive;

        /// <summary>
        /// Unique identifier for the tab.
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Display title for the tab.
        /// </summary>
        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

        /// <summary>
        /// Current path the tab is showing.
        /// </summary>
        public string CurrentPath
        {
            get => _currentPath;
            set => SetProperty(ref _currentPath, value);
        }

        /// <summary>
        /// Navigation back history.
        /// </summary>
        public Stack<string> BackHistory { get; set; } = new();

        /// <summary>
        /// Navigation forward history.
        /// </summary>
        public Stack<string> ForwardHistory { get; set; } = new();

        /// <summary>
        /// Whether the tab is pinned (cannot be closed).
        /// </summary>
        public bool IsPinned
        {
            get => _isPinned;
            set => SetProperty(ref _isPinned, value);
        }

        /// <summary>
        /// Whether this is the currently active tab.
        /// </summary>
        public bool IsActive
        {
            get => _isActive;
            set => SetProperty(ref _isActive, value);
        }

        /// <summary>
        /// Whether the tab can navigate back.
        /// </summary>
        public bool CanGoBack => BackHistory.Count > 0;

        /// <summary>
        /// Whether the tab can navigate forward.
        /// </summary>
        public bool CanGoForward => ForwardHistory.Count > 0;

        /// <summary>
        /// Notify that navigation state has changed.
        /// </summary>
        public void NotifyNavigationChanged()
        {
            OnPropertyChanged(nameof(CanGoBack));
            OnPropertyChanged(nameof(CanGoForward));
        }

        /// <summary>
        /// When the tab was created.
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// When the tab was last accessed.
        /// </summary>
        public DateTime LastAccessed { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Custom data stored with the tab.
        /// </summary>
        public Dictionary<string, object> TagData { get; set; } = new();

        /// <summary>
        /// Clone the tab (including history).
        /// </summary>
        /// <returns>A new tab with copied state.</returns>
        public FileTab Clone()
        {
            var clone = new FileTab
            {
                Title = Title,
                CurrentPath = CurrentPath,
                IsPinned = IsPinned,
                BackHistory = new Stack<string>(BackHistory.Reverse()),
                ForwardHistory = new Stack<string>(ForwardHistory.Reverse()),
                TagData = new Dictionary<string, object>(TagData)
            };
            return clone;
        }
    }

    /// <summary>
    /// Represents a saved tab session.
    /// </summary>
    public class TabSession
    {
        /// <summary>
        /// Session name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// When the session was saved.
        /// </summary>
        public DateTime SavedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Collection of tabs in the session.
        /// </summary>
        public List<SavedTab> Tabs { get; set; } = new();

        /// <summary>
        /// Index of the active tab when saved.
        /// </summary>
        public int ActiveTabIndex { get; set; } = 0;
    }

    /// <summary>
    /// A saved tab state.
    /// </summary>
    public class SavedTab
    {
        /// <summary>
        /// Tab title.
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Current path.
        /// </summary>
        public string CurrentPath { get; set; } = string.Empty;

        /// <summary>
        /// Whether the tab was pinned.
        /// </summary>
        public bool IsPinned { get; set; }

        /// <summary>
        /// Back history paths.
        /// </summary>
        public List<string> BackHistory { get; set; } = new();

        /// <summary>
        /// Forward history paths.
        /// </summary>
        public List<string> ForwardHistory { get; set; } = new();
    }
}
