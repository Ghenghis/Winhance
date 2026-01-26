using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Winhance.Core.Features.FileManager.Interfaces;
using Winhance.Core.Features.FileManager.Models;

namespace Winhance.Infrastructure.Features.FileManager.Services
{
    /// <summary>
    /// Service for advanced file selection operations.
    /// Supports pattern matching, range selection, type-based selection, and selection sets.
    /// </summary>
    public class SelectionService : ISelectionService
    {
        private readonly ObservableCollection<FileItem> _selectedItems = new();
        private readonly List<SelectionSet> _savedSets = new();
        private readonly Stack<List<FileItem>> _selectionHistory = new();
        private readonly object _lock = new();
        private List<FileItem> _previousSelection = new();

        // Clipboard abstraction - set by UI layer
        private Func<string>? _getClipboardText;
        private Action<string>? _setClipboardText;
        private Func<bool>? _hasClipboardText;

        /// <inheritdoc />
        public ObservableCollection<FileItem> SelectedItems => _selectedItems;

        /// <inheritdoc />
        public int SelectionCount => _selectedItems.Count;

        /// <inheritdoc />
        public long SelectionSize => _selectedItems.Sum(item => item.Size);

        /// <inheritdoc />
        public string SelectionSizeDisplay => FormatSize(SelectionSize);

        /// <inheritdoc />
        public bool CheckboxMode { get; set; }

        /// <inheritdoc />
        public FileItem? AnchorItem { get; set; }

        /// <inheritdoc />
        public event EventHandler<SelectionChangedEventArgs>? SelectionChanged;

        /// <summary>
        /// Configures clipboard functions from the UI layer.
        /// </summary>
        /// <param name="getText">Function to get text from clipboard.</param>
        /// <param name="setText">Action to set text to clipboard.</param>
        /// <param name="hasText">Function to check if clipboard has text.</param>
        public void ConfigureClipboard(
            Func<string>? getText,
            Action<string>? setText,
            Func<bool>? hasText)
        {
            _getClipboardText = getText;
            _setClipboardText = setText;
            _hasClipboardText = hasText;
        }

        /// <inheritdoc />
        public void Select(FileItem item, bool clearExisting = true)
        {
            ArgumentNullException.ThrowIfNull(item);

            lock (_lock)
            {
                var removed = new List<FileItem>();
                var added = new List<FileItem>();

                if (clearExisting && _selectedItems.Count > 0)
                {
                    removed.AddRange(_selectedItems);
                    _selectedItems.Clear();
                }

                if (!_selectedItems.Any(i => i.FullPath.Equals(item.FullPath, StringComparison.OrdinalIgnoreCase)))
                {
                    _selectedItems.Add(item);
                    added.Add(item);
                    AnchorItem = item;
                }

                if (added.Count > 0 || removed.Count > 0)
                {
                    OnSelectionChanged(added, removed);
                }
            }
        }

        /// <inheritdoc />
        public void SelectMany(IEnumerable<FileItem> items, bool clearExisting = true)
        {
            ArgumentNullException.ThrowIfNull(items);

            lock (_lock)
            {
                var removed = new List<FileItem>();
                var added = new List<FileItem>();

                if (clearExisting && _selectedItems.Count > 0)
                {
                    removed.AddRange(_selectedItems);
                    _selectedItems.Clear();
                }

                foreach (var item in items)
                {
                    if (!_selectedItems.Any(i => i.FullPath.Equals(item.FullPath, StringComparison.OrdinalIgnoreCase)))
                    {
                        _selectedItems.Add(item);
                        added.Add(item);
                    }
                }

                if (items.Any())
                {
                    AnchorItem = items.First();
                }

                if (added.Count > 0 || removed.Count > 0)
                {
                    OnSelectionChanged(added, removed);
                }
            }
        }

        /// <inheritdoc />
        public void Toggle(FileItem item)
        {
            ArgumentNullException.ThrowIfNull(item);

            lock (_lock)
            {
                var added = new List<FileItem>();
                var removed = new List<FileItem>();

                var existingItem = _selectedItems.FirstOrDefault(i =>
                    i.FullPath.Equals(item.FullPath, StringComparison.OrdinalIgnoreCase));

                if (existingItem != null)
                {
                    _selectedItems.Remove(existingItem);
                    removed.Add(existingItem);
                }
                else
                {
                    _selectedItems.Add(item);
                    added.Add(item);
                    AnchorItem = item;
                }

                OnSelectionChanged(added, removed);
            }
        }

        /// <inheritdoc />
        public void SelectRange(FileItem item, IList<FileItem> allItems)
        {
            ArgumentNullException.ThrowIfNull(item);
            ArgumentNullException.ThrowIfNull(allItems);

            lock (_lock)
            {
                var removed = _selectedItems.ToList();
                var added = new List<FileItem>();

                _selectedItems.Clear();

                if (AnchorItem != null)
                {
                    var anchorIndex = -1;
                    var itemIndex = -1;

                    for (int i = 0; i < allItems.Count; i++)
                    {
                        if (allItems[i].FullPath.Equals(AnchorItem.FullPath, StringComparison.OrdinalIgnoreCase))
                            anchorIndex = i;
                        if (allItems[i].FullPath.Equals(item.FullPath, StringComparison.OrdinalIgnoreCase))
                            itemIndex = i;
                    }

                    if (anchorIndex >= 0 && itemIndex >= 0)
                    {
                        var start = Math.Min(anchorIndex, itemIndex);
                        var end = Math.Max(anchorIndex, itemIndex);

                        for (int i = start; i <= end; i++)
                        {
                            _selectedItems.Add(allItems[i]);
                            added.Add(allItems[i]);
                        }
                    }
                }
                else
                {
                    _selectedItems.Add(item);
                    added.Add(item);
                    AnchorItem = item;
                }

                OnSelectionChanged(added, removed);
            }
        }

        /// <inheritdoc />
        public void Deselect(FileItem item)
        {
            ArgumentNullException.ThrowIfNull(item);

            lock (_lock)
            {
                var existingItem = _selectedItems.FirstOrDefault(i =>
                    i.FullPath.Equals(item.FullPath, StringComparison.OrdinalIgnoreCase));

                if (existingItem != null)
                {
                    _selectedItems.Remove(existingItem);
                    OnSelectionChanged(Array.Empty<FileItem>(), new[] { existingItem });
                }
            }
        }

        /// <inheritdoc />
        public void DeselectMany(IEnumerable<FileItem> items)
        {
            ArgumentNullException.ThrowIfNull(items);

            lock (_lock)
            {
                var removed = new List<FileItem>();

                foreach (var item in items)
                {
                    var existingItem = _selectedItems.FirstOrDefault(i =>
                        i.FullPath.Equals(item.FullPath, StringComparison.OrdinalIgnoreCase));

                    if (existingItem != null)
                    {
                        _selectedItems.Remove(existingItem);
                        removed.Add(existingItem);
                    }
                }

                if (removed.Count > 0)
                {
                    OnSelectionChanged(Array.Empty<FileItem>(), removed);
                }
            }
        }

        /// <inheritdoc />
        public void SelectAll(IEnumerable<FileItem> allItems)
        {
            ArgumentNullException.ThrowIfNull(allItems);

            lock (_lock)
            {
                var removed = _selectedItems.ToList();
                var added = new List<FileItem>();

                _selectedItems.Clear();

                foreach (var item in allItems)
                {
                    _selectedItems.Add(item);
                    added.Add(item);
                }

                OnSelectionChanged(added, removed);
            }
        }

        /// <inheritdoc />
        public void ClearSelection()
        {
            lock (_lock)
            {
                if (_selectedItems.Count > 0)
                {
                    var removed = _selectedItems.ToList();
                    _selectedItems.Clear();
                    AnchorItem = null;
                    OnSelectionChanged(Array.Empty<FileItem>(), removed);
                }
            }
        }

        /// <inheritdoc />
        public void InvertSelection(IEnumerable<FileItem> allItems)
        {
            ArgumentNullException.ThrowIfNull(allItems);

            lock (_lock)
            {
                var previousSelection = _selectedItems.ToList();
                var added = new List<FileItem>();

                _selectedItems.Clear();

                foreach (var item in allItems)
                {
                    if (!previousSelection.Any(i => i.FullPath.Equals(item.FullPath, StringComparison.OrdinalIgnoreCase)))
                    {
                        _selectedItems.Add(item);
                        added.Add(item);
                    }
                }

                OnSelectionChanged(added, previousSelection);
            }
        }

        /// <inheritdoc />
        public void SelectByPattern(string pattern, IEnumerable<FileItem> allItems, bool addToSelection = false)
        {
            ArgumentException.ThrowIfNullOrEmpty(pattern);
            ArgumentNullException.ThrowIfNull(allItems);

            lock (_lock)
            {
                var removed = new List<FileItem>();
                var added = new List<FileItem>();

                if (!addToSelection && _selectedItems.Count > 0)
                {
                    removed.AddRange(_selectedItems);
                    _selectedItems.Clear();
                }

                var regexPattern = Regex.Escape(pattern)
                    .Replace(@"\*", ".*")
                    .Replace(@"\?", ".");

                var regex = new Regex($"^{regexPattern}$", RegexOptions.IgnoreCase);

                foreach (var item in allItems.Where(i => regex.IsMatch(i.Name)))
                {
                    if (!_selectedItems.Any(i => i.FullPath.Equals(item.FullPath, StringComparison.OrdinalIgnoreCase)))
                    {
                        _selectedItems.Add(item);
                        added.Add(item);
                    }
                }

                if (added.Count > 0 || removed.Count > 0)
                {
                    OnSelectionChanged(added, removed);
                }
            }
        }

        /// <inheritdoc />
        public void SelectByRegex(string regexPattern, IEnumerable<FileItem> allItems, bool addToSelection = false)
        {
            ArgumentException.ThrowIfNullOrEmpty(regexPattern);
            ArgumentNullException.ThrowIfNull(allItems);

            lock (_lock)
            {
                var removed = new List<FileItem>();
                var added = new List<FileItem>();

                if (!addToSelection && _selectedItems.Count > 0)
                {
                    removed.AddRange(_selectedItems);
                    _selectedItems.Clear();
                }

                try
                {
                    var regex = new Regex(regexPattern, RegexOptions.IgnoreCase);
                    foreach (var item in allItems.Where(i => regex.IsMatch(i.Name)))
                    {
                        if (!_selectedItems.Any(i => i.FullPath.Equals(item.FullPath, StringComparison.OrdinalIgnoreCase)))
                        {
                            _selectedItems.Add(item);
                            added.Add(item);
                        }
                    }
                }
                catch (RegexParseException)
                {
                    // Invalid regex, do nothing
                }

                if (added.Count > 0 || removed.Count > 0)
                {
                    OnSelectionChanged(added, removed);
                }
            }
        }

        /// <inheritdoc />
        public void SelectByExtension(IEnumerable<string> extensions, IEnumerable<FileItem> allItems, bool addToSelection = false)
        {
            ArgumentNullException.ThrowIfNull(extensions);
            ArgumentNullException.ThrowIfNull(allItems);

            var extensionSet = new HashSet<string>(
                extensions.Select(e => e.StartsWith('.') ? e.ToLowerInvariant() : $".{e.ToLowerInvariant()}"),
                StringComparer.OrdinalIgnoreCase);

            lock (_lock)
            {
                var removed = new List<FileItem>();
                var added = new List<FileItem>();

                if (!addToSelection && _selectedItems.Count > 0)
                {
                    removed.AddRange(_selectedItems);
                    _selectedItems.Clear();
                }

                foreach (var item in allItems)
                {
                    var ext = Path.GetExtension(item.Name);
                    if (!string.IsNullOrEmpty(ext) && extensionSet.Contains(ext.ToLowerInvariant()))
                    {
                        if (!_selectedItems.Any(i => i.FullPath.Equals(item.FullPath, StringComparison.OrdinalIgnoreCase)))
                        {
                            _selectedItems.Add(item);
                            added.Add(item);
                        }
                    }
                }

                if (added.Count > 0 || removed.Count > 0)
                {
                    OnSelectionChanged(added, removed);
                }
            }
        }

        /// <inheritdoc />
        public void SelectBySizeRange(long? minSize, long? maxSize, IEnumerable<FileItem> allItems, bool addToSelection = false)
        {
            ArgumentNullException.ThrowIfNull(allItems);

            lock (_lock)
            {
                var removed = new List<FileItem>();
                var added = new List<FileItem>();

                if (!addToSelection && _selectedItems.Count > 0)
                {
                    removed.AddRange(_selectedItems);
                    _selectedItems.Clear();
                }

                foreach (var item in allItems)
                {
                    if ((!minSize.HasValue || item.Size >= minSize.Value) &&
                        (!maxSize.HasValue || item.Size <= maxSize.Value))
                    {
                        if (!_selectedItems.Any(i => i.FullPath.Equals(item.FullPath, StringComparison.OrdinalIgnoreCase)))
                        {
                            _selectedItems.Add(item);
                            added.Add(item);
                        }
                    }
                }

                if (added.Count > 0 || removed.Count > 0)
                {
                    OnSelectionChanged(added, removed);
                }
            }
        }

        /// <inheritdoc />
        public void SelectByDateRange(DateTime? startDate, DateTime? endDate, DateType dateType, IEnumerable<FileItem> allItems, bool addToSelection = false)
        {
            ArgumentNullException.ThrowIfNull(allItems);

            lock (_lock)
            {
                var removed = new List<FileItem>();
                var added = new List<FileItem>();

                if (!addToSelection && _selectedItems.Count > 0)
                {
                    removed.AddRange(_selectedItems);
                    _selectedItems.Clear();
                }

                foreach (var item in allItems)
                {
                    // DateType from interface: Modified, Created, Accessed
                    DateTime itemDate = dateType switch
                    {
                        DateType.Created => item.CreatedDate,
                        DateType.Accessed => item.AccessedDate,
                        DateType.Modified => item.LastModified,
                        _ => item.LastModified
                    };

                    if ((!startDate.HasValue || itemDate >= startDate.Value) &&
                        (!endDate.HasValue || itemDate <= endDate.Value))
                    {
                        if (!_selectedItems.Any(i => i.FullPath.Equals(item.FullPath, StringComparison.OrdinalIgnoreCase)))
                        {
                            _selectedItems.Add(item);
                            added.Add(item);
                        }
                    }
                }

                if (added.Count > 0 || removed.Count > 0)
                {
                    OnSelectionChanged(added, removed);
                }
            }
        }

        /// <inheritdoc />
        public void SelectFilesOnly(IEnumerable<FileItem> allItems)
        {
            SelectFilesOnly(allItems, addToSelection: false);
        }

        /// <summary>
        /// Selects only files (excludes folders).
        /// </summary>
        /// <param name="allItems">All items to filter.</param>
        /// <param name="addToSelection">Whether to add to existing selection.</param>
        public void SelectFilesOnly(IEnumerable<FileItem> allItems, bool addToSelection)
        {
            ArgumentNullException.ThrowIfNull(allItems);

            lock (_lock)
            {
                var removed = new List<FileItem>();
                var added = new List<FileItem>();

                if (!addToSelection && _selectedItems.Count > 0)
                {
                    removed.AddRange(_selectedItems);
                    _selectedItems.Clear();
                }

                foreach (var item in allItems.Where(i => !i.IsDirectory))
                {
                    if (!_selectedItems.Any(i => i.FullPath.Equals(item.FullPath, StringComparison.OrdinalIgnoreCase)))
                    {
                        _selectedItems.Add(item);
                        added.Add(item);
                    }
                }

                if (added.Count > 0 || removed.Count > 0)
                {
                    OnSelectionChanged(added, removed);
                }
            }
        }

        /// <inheritdoc />
        public void SelectFoldersOnly(IEnumerable<FileItem> allItems)
        {
            SelectFoldersOnly(allItems, addToSelection: false);
        }

        /// <summary>
        /// Selects only folders (excludes files).
        /// </summary>
        /// <param name="allItems">All items to filter.</param>
        /// <param name="addToSelection">Whether to add to existing selection.</param>
        public void SelectFoldersOnly(IEnumerable<FileItem> allItems, bool addToSelection)
        {
            ArgumentNullException.ThrowIfNull(allItems);

            lock (_lock)
            {
                var removed = new List<FileItem>();
                var added = new List<FileItem>();

                if (!addToSelection && _selectedItems.Count > 0)
                {
                    removed.AddRange(_selectedItems);
                    _selectedItems.Clear();
                }

                foreach (var item in allItems.Where(i => i.IsDirectory))
                {
                    if (!_selectedItems.Any(i => i.FullPath.Equals(item.FullPath, StringComparison.OrdinalIgnoreCase)))
                    {
                        _selectedItems.Add(item);
                        added.Add(item);
                    }
                }

                if (added.Count > 0 || removed.Count > 0)
                {
                    OnSelectionChanged(added, removed);
                }
            }
        }

        /// <inheritdoc />
        public bool IsSelected(FileItem item)
        {
            ArgumentNullException.ThrowIfNull(item);

            lock (_lock)
            {
                return _selectedItems.Any(i => i.FullPath.Equals(item.FullPath, StringComparison.OrdinalIgnoreCase));
            }
        }

        /// <inheritdoc />
        public async Task SaveSelectionSetAsync(string name)
        {
            ArgumentException.ThrowIfNullOrEmpty(name);

            await Task.Run(() =>
            {
                lock (_lock)
                {
                    var existingSet = _savedSets.FirstOrDefault(s =>
                        s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

                    if (existingSet != null)
                    {
                        _savedSets.Remove(existingSet);
                    }

                    _savedSets.Add(new SelectionSet
                    {
                        Name = name,
                        Paths = _selectedItems.Select(i => i.FullPath).ToList(),
                        CreatedAt = DateTime.UtcNow
                    });
                }
            });
        }

        /// <inheritdoc />
        public async Task LoadSelectionSetAsync(string name, IEnumerable<FileItem> availableItems)
        {
            ArgumentException.ThrowIfNullOrEmpty(name);
            ArgumentNullException.ThrowIfNull(availableItems);

            await Task.Run(() =>
            {
                lock (_lock)
                {
                    var set = _savedSets.FirstOrDefault(s =>
                        s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

                    if (set != null)
                    {
                        var removed = _selectedItems.ToList();
                        var added = new List<FileItem>();

                        _selectedItems.Clear();
                        var availableDict = availableItems.ToDictionary(
                            i => i.FullPath, i => i, StringComparer.OrdinalIgnoreCase);

                        foreach (var path in set.Paths)
                        {
                            if (availableDict.TryGetValue(path, out var item))
                            {
                                _selectedItems.Add(item);
                                added.Add(item);
                            }
                        }

                        OnSelectionChanged(added, removed);
                    }
                }
            });
        }

        /// <inheritdoc />
        public IEnumerable<SelectionSet> GetSelectionSets()
        {
            lock (_lock)
            {
                return _savedSets.ToList();
            }
        }

        /// <inheritdoc />
        public void DeleteSelectionSet(string name)
        {
            ArgumentException.ThrowIfNullOrEmpty(name);

            lock (_lock)
            {
                var set = _savedSets.FirstOrDefault(s =>
                    s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

                if (set != null)
                {
                    _savedSets.Remove(set);
                }
            }
        }

        /// <summary>
        /// Saves the current selection to history for undo.
        /// </summary>
        public void SaveSelectionToHistory()
        {
            lock (_lock)
            {
                _selectionHistory.Push(_selectedItems.ToList());

                // Keep only last 20 selections
                while (_selectionHistory.Count > 20)
                {
                    // Pop excess items (from the "bottom" by converting to list and back)
                    var historyList = _selectionHistory.ToList();
                    historyList.RemoveAt(historyList.Count - 1);
                    _selectionHistory.Clear();
                    foreach (var item in historyList.AsEnumerable().Reverse())
                    {
                        _selectionHistory.Push(item);
                    }
                }
            }
        }

        /// <summary>
        /// Restores the previous selection from history.
        /// </summary>
        public void RestoreSelectionFromHistory()
        {
            lock (_lock)
            {
                if (_selectionHistory.Count > 0)
                {
                    var removed = _selectedItems.ToList();
                    var previousSelection = _selectionHistory.Pop();

                    _selectedItems.Clear();
                    foreach (var item in previousSelection)
                    {
                        _selectedItems.Add(item);
                    }

                    OnSelectionChanged(previousSelection, removed);
                }
            }
        }

        /// <inheritdoc />
        public void CopySelectionToClipboard()
        {
            if (_setClipboardText == null)
            {
                // If clipboard not configured, silently fail
                return;
            }

            var paths = string.Join(Environment.NewLine, _selectedItems.Select(i => i.FullPath));
            _setClipboardText(paths);
        }

        /// <inheritdoc />
        public void SelectFromClipboard(IEnumerable<FileItem> availableItems)
        {
            ArgumentNullException.ThrowIfNull(availableItems);

            if (_hasClipboardText == null || _getClipboardText == null)
            {
                // If clipboard not configured, silently fail
                return;
            }

            if (_hasClipboardText())
            {
                var clipboardText = _getClipboardText();
                var paths = clipboardText.Split(new[] { Environment.NewLine, "\n", "\r" },
                    StringSplitOptions.RemoveEmptyEntries);

                lock (_lock)
                {
                    var removed = _selectedItems.ToList();
                    var added = new List<FileItem>();

                    _selectedItems.Clear();
                    var availableDict = availableItems.ToDictionary(
                        i => i.FullPath, i => i, StringComparer.OrdinalIgnoreCase);

                    foreach (var path in paths)
                    {
                        var trimmedPath = path.Trim();
                        if (availableDict.TryGetValue(trimmedPath, out var item))
                        {
                            _selectedItems.Add(item);
                            added.Add(item);
                        }
                    }

                    if (added.Count > 0 || removed.Count > 0)
                    {
                        OnSelectionChanged(added, removed);
                    }
                }
            }
        }

        /// <summary>
        /// Gets the selected paths as a newline-separated string (for clipboard).
        /// </summary>
        /// <returns>Selected paths as text.</returns>
        public string GetSelectionAsText()
        {
            lock (_lock)
            {
                return string.Join(Environment.NewLine, _selectedItems.Select(i => i.FullPath));
            }
        }

        /// <summary>
        /// Selects items from a newline-separated path string.
        /// </summary>
        /// <param name="pathsText">Newline-separated paths.</param>
        /// <param name="availableItems">All available items.</param>
        public void SelectFromText(string pathsText, IEnumerable<FileItem> availableItems)
        {
            ArgumentNullException.ThrowIfNull(availableItems);

            if (string.IsNullOrWhiteSpace(pathsText))
                return;

            var paths = pathsText.Split(new[] { Environment.NewLine, "\n", "\r" },
                StringSplitOptions.RemoveEmptyEntries);

            lock (_lock)
            {
                var removed = _selectedItems.ToList();
                var added = new List<FileItem>();

                _selectedItems.Clear();
                var availableDict = availableItems.ToDictionary(
                    i => i.FullPath, i => i, StringComparer.OrdinalIgnoreCase);

                foreach (var path in paths)
                {
                    var trimmedPath = path.Trim();
                    if (availableDict.TryGetValue(trimmedPath, out var item))
                    {
                        _selectedItems.Add(item);
                        added.Add(item);
                    }
                }

                if (added.Count > 0 || removed.Count > 0)
                {
                    OnSelectionChanged(added, removed);
                }
            }
        }

        private void OnSelectionChanged(IReadOnlyList<FileItem> added, IReadOnlyList<FileItem> removed)
        {
            SelectionChanged?.Invoke(this, new SelectionChangedEventArgs
            {
                AddedItems = added,
                RemovedItems = removed,
                SelectionCount = _selectedItems.Count,
                SelectionSize = SelectionSize
            });
        }

        private static string FormatSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }
    }
}
