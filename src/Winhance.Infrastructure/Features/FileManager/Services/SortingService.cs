using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Winhance.Core.Features.FileManager.Interfaces;
using Winhance.Core.Features.FileManager.Models;

namespace Winhance.Infrastructure.Features.FileManager.Services
{
    /// <summary>
    /// Service for sorting and grouping files.
    /// </summary>
    public class SortingService : ISortingService
    {
        private readonly string _preferencesPath;
        private readonly Dictionary<string, FolderSortSettings> _folderSettings = new();

        private SortColumn _currentSortColumn = SortColumn.Name;
        private SortDirection _currentSortDirection = SortDirection.Ascending;
        private GroupBy _currentGrouping = GroupBy.None;
        private bool _foldersFirst = true;
        private bool _useNaturalSort = true;
        private bool _caseSensitive = false;
        private SortColumn? _secondarySortColumn;
        private SortDirection _secondarySortDirection = SortDirection.Ascending;

        /// <inheritdoc/>
        public SortColumn CurrentSortColumn
        {
            get => _currentSortColumn;
            set
            {
                if (_currentSortColumn != value)
                {
                    var old = _currentSortColumn;
                    _currentSortColumn = value;
                    SortChanged?.Invoke(this, new SortChangedEventArgs
                    {
                        OldColumn = old,
                        NewColumn = value,
                        OldDirection = _currentSortDirection,
                        NewDirection = _currentSortDirection
                    });
                }
            }
        }

        /// <inheritdoc/>
        public SortDirection CurrentSortDirection
        {
            get => _currentSortDirection;
            set
            {
                if (_currentSortDirection != value)
                {
                    var old = _currentSortDirection;
                    _currentSortDirection = value;
                    SortChanged?.Invoke(this, new SortChangedEventArgs
                    {
                        OldColumn = _currentSortColumn,
                        NewColumn = _currentSortColumn,
                        OldDirection = old,
                        NewDirection = value
                    });
                }
            }
        }

        /// <inheritdoc/>
        public GroupBy CurrentGrouping
        {
            get => _currentGrouping;
            set
            {
                if (_currentGrouping != value)
                {
                    var old = _currentGrouping;
                    _currentGrouping = value;
                    GroupChanged?.Invoke(this, new GroupChangedEventArgs
                    {
                        OldGrouping = old,
                        NewGrouping = value
                    });
                }
            }
        }

        /// <inheritdoc/>
        public bool FoldersFirst
        {
            get => _foldersFirst;
            set => _foldersFirst = value;
        }

        /// <inheritdoc/>
        public bool UseNaturalSort
        {
            get => _useNaturalSort;
            set => _useNaturalSort = value;
        }

        /// <inheritdoc/>
        public bool CaseSensitive
        {
            get => _caseSensitive;
            set => _caseSensitive = value;
        }

        /// <inheritdoc/>
        public SortColumn? SecondarySortColumn
        {
            get => _secondarySortColumn;
            set => _secondarySortColumn = value;
        }

        /// <inheritdoc/>
        public SortDirection SecondarySortDirection
        {
            get => _secondarySortDirection;
            set => _secondarySortDirection = value;
        }

        /// <inheritdoc/>
        public event EventHandler<SortChangedEventArgs>? SortChanged;

        /// <inheritdoc/>
        public event EventHandler<GroupChangedEventArgs>? GroupChanged;

        /// <summary>
        /// Initializes a new instance of the SortingService.
        /// </summary>
        public SortingService()
        {
            _preferencesPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Winhance", "FileManager", "sorting-preferences.json");
        }

        /// <inheritdoc/>
        public IEnumerable<FileSystemEntry> Sort(IEnumerable<FileSystemEntry> files)
        {
            var comparer = GetComparer();
            return files.OrderBy(f => f, comparer);
        }

        /// <inheritdoc/>
        public IEnumerable<FileGroup> Group(IEnumerable<FileSystemEntry> files)
        {
            if (_currentGrouping == GroupBy.None)
            {
                return new[] { new FileGroup { Key = "", DisplayName = "All Items", Files = files.ToList() } };
            }

            var groups = files
                .GroupBy(f => GetGroupKey(f))
                .Select(g => new FileGroup
                {
                    Key = g.Key,
                    DisplayName = GetGroupDisplayName(g.Key),
                    Files = g.ToList(),
                    TotalSize = g.Sum(f => f.Size)
                })
                .OrderBy(g => g.SortOrder)
                .ThenBy(g => g.DisplayName)
                .ToList();

            return groups;
        }

        /// <inheritdoc/>
        public IEnumerable<FileGroup> SortAndGroup(IEnumerable<FileSystemEntry> files)
        {
            var sorted = Sort(files);
            return Group(sorted);
        }

        /// <inheritdoc/>
        public FolderSortSettings GetSortSettingsForFolder(string folderPath)
        {
            var normalized = NormalizePath(folderPath);
            if (_folderSettings.TryGetValue(normalized, out var settings))
                return settings;

            return new FolderSortSettings
            {
                SortColumn = _currentSortColumn,
                SortDirection = _currentSortDirection,
                GroupBy = _currentGrouping,
                FoldersFirst = _foldersFirst,
                SecondarySortColumn = _secondarySortColumn,
                SecondarySortDirection = _secondarySortDirection
            };
        }

        /// <inheritdoc/>
        public void SetSortSettingsForFolder(string folderPath, FolderSortSettings settings)
        {
            var normalized = NormalizePath(folderPath);
            _folderSettings[normalized] = settings;
        }

        /// <inheritdoc/>
        public void ClearSortSettingsForFolder(string folderPath)
        {
            var normalized = NormalizePath(folderPath);
            _folderSettings.Remove(normalized);
        }

        /// <inheritdoc/>
        public IComparer<FileSystemEntry> GetComparer()
        {
            return new FileSystemEntryComparer(this);
        }

        /// <inheritdoc/>
        public string GetGroupKey(FileSystemEntry file)
        {
            return _currentGrouping switch
            {
                GroupBy.Type => file.IsDirectory ? "Folder" : (string.IsNullOrEmpty(file.Extension) ? "No Extension" : file.Extension.ToUpperInvariant()),
                GroupBy.DateModified => GetDateGroupKey(file.DateModified),
                GroupBy.DateCreated => GetDateGroupKey(file.DateCreated),
                GroupBy.Size => GetSizeGroupKey(file.Size, file.IsDirectory),
                GroupBy.FirstLetter => GetFirstLetterGroupKey(file.Name),
                GroupBy.Kind => file.IsDirectory ? "Folders" : "Files",
                _ => ""
            };
        }

        /// <inheritdoc/>
        public async Task SavePreferencesAsync()
        {
            try
            {
                var dir = Path.GetDirectoryName(_preferencesPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var preferences = new SortingPreferences
                {
                    CurrentSortColumn = _currentSortColumn,
                    CurrentSortDirection = _currentSortDirection,
                    CurrentGrouping = _currentGrouping,
                    FoldersFirst = _foldersFirst,
                    UseNaturalSort = _useNaturalSort,
                    CaseSensitive = _caseSensitive,
                    SecondarySortColumn = _secondarySortColumn,
                    SecondarySortDirection = _secondarySortDirection,
                    FolderSettings = new Dictionary<string, FolderSortSettings>(_folderSettings)
                };

                var json = JsonSerializer.Serialize(preferences, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(_preferencesPath, json);
            }
            catch
            {
                // Log error
            }
        }

        /// <inheritdoc/>
        public async Task LoadPreferencesAsync()
        {
            try
            {
                if (!File.Exists(_preferencesPath))
                    return;

                var json = await File.ReadAllTextAsync(_preferencesPath);
                var preferences = JsonSerializer.Deserialize<SortingPreferences>(json);

                if (preferences != null)
                {
                    _currentSortColumn = preferences.CurrentSortColumn;
                    _currentSortDirection = preferences.CurrentSortDirection;
                    _currentGrouping = preferences.CurrentGrouping;
                    _foldersFirst = preferences.FoldersFirst;
                    _useNaturalSort = preferences.UseNaturalSort;
                    _caseSensitive = preferences.CaseSensitive;
                    _secondarySortColumn = preferences.SecondarySortColumn;
                    _secondarySortDirection = preferences.SecondarySortDirection;

                    _folderSettings.Clear();
                    if (preferences.FolderSettings != null)
                    {
                        foreach (var kvp in preferences.FolderSettings)
                            _folderSettings[kvp.Key] = kvp.Value;
                    }
                }
            }
            catch
            {
                // Log error
            }
        }

        private static string GetDateGroupKey(DateTime date)
        {
            var today = DateTime.Today;
            var diff = (today - date.Date).Days;

            return diff switch
            {
                0 => "Today",
                1 => "Yesterday",
                <= 7 => "Earlier this week",
                <= 14 => "Last week",
                <= 30 => "Earlier this month",
                <= 60 => "Last month",
                <= 365 => "Earlier this year",
                _ => $"{date.Year}"
            };
        }

        private static string GetSizeGroupKey(long size, bool isDirectory)
        {
            if (isDirectory) return "Folders";

            return size switch
            {
                0 => "Empty (0 KB)",
                < 16 * 1024 => "Tiny (0 - 16 KB)",
                < 1024 * 1024 => "Small (16 KB - 1 MB)",
                < 128 * 1024 * 1024 => "Medium (1 - 128 MB)",
                < 1024L * 1024 * 1024 => "Large (128 MB - 1 GB)",
                _ => "Huge (> 1 GB)"
            };
        }

        private static string GetFirstLetterGroupKey(string name)
        {
            if (string.IsNullOrEmpty(name)) return "#";
            var first = char.ToUpperInvariant(name[0]);
            return char.IsLetter(first) ? first.ToString() : "#";
        }

        private static string GetGroupDisplayName(string key)
        {
            return key;
        }

        private static string NormalizePath(string path)
        {
            return path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).ToLowerInvariant();
        }

        private class FileSystemEntryComparer : IComparer<FileSystemEntry>
        {
            private readonly SortingService _service;
            private static readonly Regex NaturalSortRegex = new(@"(\d+)", RegexOptions.Compiled);

            public FileSystemEntryComparer(SortingService service)
            {
                _service = service;
            }

            public int Compare(FileSystemEntry? x, FileSystemEntry? y)
            {
                if (x == null && y == null) return 0;
                if (x == null) return -1;
                if (y == null) return 1;

                // Folders first
                if (_service._foldersFirst)
                {
                    if (x.IsDirectory && !y.IsDirectory) return -1;
                    if (!x.IsDirectory && y.IsDirectory) return 1;
                }

                var result = CompareByColumn(x, y, _service._currentSortColumn);
                if (result == 0 && _service._secondarySortColumn.HasValue)
                {
                    result = CompareByColumn(x, y, _service._secondarySortColumn.Value);
                    if (_service._secondarySortDirection == SortDirection.Descending)
                        result = -result;
                }

                if (_service._currentSortDirection == SortDirection.Descending)
                    result = -result;

                return result;
            }

            private int CompareByColumn(FileSystemEntry x, FileSystemEntry y, SortColumn column)
            {
                return column switch
                {
                    SortColumn.Name => CompareNames(x.Name, y.Name),
                    SortColumn.Size => x.Size.CompareTo(y.Size),
                    SortColumn.Type => string.Compare(x.Extension, y.Extension, StringComparison.OrdinalIgnoreCase),
                    SortColumn.DateModified => x.DateModified.CompareTo(y.DateModified),
                    SortColumn.DateCreated => x.DateCreated.CompareTo(y.DateCreated),
                    SortColumn.Path => string.Compare(x.FullPath, y.FullPath, StringComparison.OrdinalIgnoreCase),
                    _ => 0
                };
            }

            private int CompareNames(string name1, string name2)
            {
                if (_service._useNaturalSort)
                    return NaturalCompare(name1, name2);

                var comparison = _service._caseSensitive
                    ? StringComparison.Ordinal
                    : StringComparison.OrdinalIgnoreCase;

                return string.Compare(name1, name2, comparison);
            }

            private static int NaturalCompare(string s1, string s2)
            {
                var parts1 = NaturalSortRegex.Split(s1);
                var parts2 = NaturalSortRegex.Split(s2);

                for (int i = 0; i < Math.Min(parts1.Length, parts2.Length); i++)
                {
                    int result;
                    if (long.TryParse(parts1[i], out var num1) && long.TryParse(parts2[i], out var num2))
                    {
                        result = num1.CompareTo(num2);
                    }
                    else
                    {
                        result = string.Compare(parts1[i], parts2[i], StringComparison.OrdinalIgnoreCase);
                    }

                    if (result != 0) return result;
                }

                return parts1.Length.CompareTo(parts2.Length);
            }
        }

        private class SortingPreferences
        {
            public SortColumn CurrentSortColumn { get; set; }
            public SortDirection CurrentSortDirection { get; set; }
            public GroupBy CurrentGrouping { get; set; }
            public bool FoldersFirst { get; set; }
            public bool UseNaturalSort { get; set; }
            public bool CaseSensitive { get; set; }
            public SortColumn? SecondarySortColumn { get; set; }
            public SortDirection SecondarySortDirection { get; set; }
            public Dictionary<string, FolderSortSettings>? FolderSettings { get; set; }
        }
    }
}
