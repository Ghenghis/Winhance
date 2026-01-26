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
    /// Service for quick filtering of files and folders with presets and real-time filtering.
    /// </summary>
    public class QuickFilterService : IQuickFilterService
    {
        private readonly string _presetPath;
        private readonly string _historyPath;
        private readonly object _lock = new();
        private readonly List<FilterPreset> _presets = new();
        private readonly List<string> _filterHistory = new();
        private const int MaxHistoryItems = 50;

        // Extension preset definitions
        private static readonly Dictionary<ExtensionPreset, string[]> ExtensionPresets = new()
        {
            [ExtensionPreset.All] = Array.Empty<string>(),
            [ExtensionPreset.Images] = new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".svg", ".ico", ".tiff", ".tif", ".raw", ".heic", ".heif" },
            [ExtensionPreset.Videos] = new[] { ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".flv", ".webm", ".m4v", ".mpeg", ".mpg", ".3gp" },
            [ExtensionPreset.Audio] = new[] { ".mp3", ".wav", ".flac", ".aac", ".ogg", ".wma", ".m4a", ".opus", ".aiff", ".ape" },
            [ExtensionPreset.Documents] = new[] { ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".txt", ".rtf", ".odt", ".ods", ".odp", ".epub", ".md" },
            [ExtensionPreset.Archives] = new[] { ".zip", ".rar", ".7z", ".tar", ".gz", ".bz2", ".xz", ".iso", ".cab" },
            [ExtensionPreset.Code] = new[] { ".cs", ".js", ".ts", ".py", ".java", ".cpp", ".c", ".h", ".rs", ".go", ".html", ".css", ".scss", ".json", ".xml", ".yaml", ".yml", ".sql", ".sh", ".ps1", ".bat", ".cmd" },
            [ExtensionPreset.Executables] = new[] { ".exe", ".msi", ".bat", ".cmd", ".ps1", ".sh", ".com", ".scr", ".dll" }
        };

        private string _filterText = string.Empty;
        private FilterMode _mode = FilterMode.Contains;
        private bool _caseSensitive;
        private string[]? _extensionFilter;
        private SizeFilter? _sizeFilter;
        private DateFilter? _dateFilter;
        private AttributeFilter? _attributeFilter;

        public QuickFilterService()
        {
            var appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Winhance");
            _presetPath = Path.Combine(appDataPath, "filter_presets.json");
            _historyPath = Path.Combine(appDataPath, "filter_history.json");

            LoadPresets();
            LoadHistory();
        }

        /// <inheritdoc />
        public string FilterText
        {
            get => _filterText;
            set
            {
                if (_filterText != value)
                {
                    _filterText = value ?? string.Empty;
                    OnFilterChanged(FilterChangeType.TextChanged);
                }
            }
        }

        /// <inheritdoc />
        public bool IsFiltering => !string.IsNullOrEmpty(_filterText) ||
                                   _extensionFilter?.Length > 0 ||
                                   _sizeFilter != null ||
                                   _dateFilter != null ||
                                   _attributeFilter != null;

        /// <inheritdoc />
        public FilterMode Mode
        {
            get => _mode;
            set
            {
                if (_mode != value)
                {
                    _mode = value;
                    OnFilterChanged(FilterChangeType.TextChanged);
                }
            }
        }

        /// <inheritdoc />
        public bool CaseSensitive
        {
            get => _caseSensitive;
            set
            {
                if (_caseSensitive != value)
                {
                    _caseSensitive = value;
                    OnFilterChanged(FilterChangeType.TextChanged);
                }
            }
        }

        /// <inheritdoc />
        public string[]? ExtensionFilter
        {
            get => _extensionFilter;
            set
            {
                _extensionFilter = value?.Select(e => e.StartsWith(".") ? e : "." + e).ToArray();
                OnFilterChanged(FilterChangeType.ExtensionChanged);
            }
        }

        /// <inheritdoc />
        public SizeFilter? SizeFilter
        {
            get => _sizeFilter;
            set
            {
                _sizeFilter = value;
                OnFilterChanged(FilterChangeType.SizeChanged);
            }
        }

        /// <inheritdoc />
        public DateFilter? DateFilter
        {
            get => _dateFilter;
            set
            {
                _dateFilter = value;
                OnFilterChanged(FilterChangeType.DateChanged);
            }
        }

        /// <inheritdoc />
        public AttributeFilter? AttributeFilter
        {
            get => _attributeFilter;
            set
            {
                _attributeFilter = value;
                OnFilterChanged(FilterChangeType.AttributeChanged);
            }
        }

        /// <inheritdoc />
        public event EventHandler<FilterChangedEventArgs>? FilterChanged;

        /// <inheritdoc />
        public IEnumerable<FileSystemEntry> Filter(IEnumerable<FileSystemEntry> files)
        {
            return files.Where(Matches);
        }

        /// <inheritdoc />
        public bool Matches(FileSystemEntry file)
        {
            if (file == null) return false;

            // Text filter
            if (!string.IsNullOrEmpty(_filterText))
            {
                if (!MatchesTextFilter(file.Name))
                    return false;
            }

            // Extension filter
            if (_extensionFilter?.Length > 0 && !file.IsDirectory)
            {
                var extension = file.Extension?.ToLowerInvariant() ?? string.Empty;
                var comparison = _caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
                if (!_extensionFilter.Any(e => string.Equals(e, extension, comparison)))
                    return false;
            }

            // Size filter
            if (_sizeFilter != null && !file.IsDirectory)
            {
                if (!MatchesSizeFilter(file.Size))
                    return false;
            }

            // Date filter
            if (_dateFilter != null)
            {
                if (!MatchesDateFilter(file))
                    return false;
            }

            // Attribute filter
            if (_attributeFilter != null)
            {
                if (!MatchesAttributeFilter(file))
                    return false;
            }

            return true;
        }

        private bool MatchesTextFilter(string fileName)
        {
            var comparison = _caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

            return _mode switch
            {
                FilterMode.Contains => fileName.Contains(_filterText, comparison),
                FilterMode.Exact => string.Equals(fileName, _filterText, comparison),
                FilterMode.Wildcard => MatchesWildcard(fileName, _filterText, !_caseSensitive),
                FilterMode.Regex => MatchesRegex(fileName),
                _ => fileName.Contains(_filterText, comparison)
            };
        }

        private bool MatchesWildcard(string input, string pattern, bool ignoreCase)
        {
            try
            {
                var regexPattern = "^" + Regex.Escape(pattern)
                    .Replace("\\*", ".*")
                    .Replace("\\?", ".") + "$";
                var options = ignoreCase ? RegexOptions.IgnoreCase : RegexOptions.None;
                return Regex.IsMatch(input, regexPattern, options);
            }
            catch
            {
                return false;
            }
        }

        private bool MatchesRegex(string input)
        {
            try
            {
                var options = _caseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase;
                return Regex.IsMatch(input, _filterText, options);
            }
            catch
            {
                return false;
            }
        }

        private bool MatchesSizeFilter(long size)
        {
            if (_sizeFilter == null) return true;

            // Handle category preset
            if (_sizeFilter.Category.HasValue)
            {
                return _sizeFilter.Category.Value switch
                {
                    SizeCategory.Empty => size == 0,
                    SizeCategory.Tiny => size > 0 && size < 16 * 1024,
                    SizeCategory.Small => size >= 16 * 1024 && size < 1024 * 1024,
                    SizeCategory.Medium => size >= 1024 * 1024 && size < 128 * 1024 * 1024,
                    SizeCategory.Large => size >= 128 * 1024 * 1024 && size < 1024L * 1024 * 1024,
                    SizeCategory.Huge => size >= 1024L * 1024 * 1024,
                    _ => true
                };
            }

            // Handle min/max
            if (_sizeFilter.MinSize.HasValue && size < _sizeFilter.MinSize.Value)
                return false;
            if (_sizeFilter.MaxSize.HasValue && size > _sizeFilter.MaxSize.Value)
                return false;

            return true;
        }

        private bool MatchesDateFilter(FileSystemEntry file)
        {
            if (_dateFilter == null) return true;

            var dateToCheck = _dateFilter.DateType switch
            {
                DateType.Created => file.DateCreated,
                DateType.Accessed => file.DateAccessed,
                _ => file.DateModified
            };

            // Handle preset
            if (_dateFilter.Preset.HasValue)
            {
                var today = DateTime.Today;
                return _dateFilter.Preset.Value switch
                {
                    DatePreset.Today => dateToCheck.Date == today,
                    DatePreset.Yesterday => dateToCheck.Date == today.AddDays(-1),
                    DatePreset.ThisWeek => dateToCheck >= today.AddDays(-(int)today.DayOfWeek),
                    DatePreset.LastWeek => dateToCheck >= today.AddDays(-7 - (int)today.DayOfWeek) &&
                                           dateToCheck < today.AddDays(-(int)today.DayOfWeek),
                    DatePreset.ThisMonth => dateToCheck.Year == today.Year && dateToCheck.Month == today.Month,
                    DatePreset.LastMonth => dateToCheck >= new DateTime(today.Year, today.Month, 1).AddMonths(-1) &&
                                            dateToCheck < new DateTime(today.Year, today.Month, 1),
                    DatePreset.ThisYear => dateToCheck.Year == today.Year,
                    DatePreset.LastYear => dateToCheck.Year == today.Year - 1,
                    DatePreset.OlderThanYear => dateToCheck < today.AddYears(-1),
                    _ => true
                };
            }

            // Handle start/end dates
            if (_dateFilter.StartDate.HasValue && dateToCheck < _dateFilter.StartDate.Value)
                return false;
            if (_dateFilter.EndDate.HasValue && dateToCheck > _dateFilter.EndDate.Value)
                return false;

            return true;
        }

        private bool MatchesAttributeFilter(FileSystemEntry file)
        {
            if (_attributeFilter == null) return true;

            // Directories only
            if (_attributeFilter.DirectoriesOnly == true && !file.IsDirectory)
                return false;

            // Files only
            if (_attributeFilter.FilesOnly == true && file.IsDirectory)
                return false;

            // Hidden files
            if (_attributeFilter.IncludeHidden == false && file.IsHidden)
                return false;

            // System files
            if (_attributeFilter.IncludeSystem == false && file.IsSystem)
                return false;

            // Read-only files
            if (_attributeFilter.IncludeReadOnly == false && file.IsReadOnly)
                return false;

            return true;
        }

        /// <inheritdoc />
        public void ClearAll()
        {
            _filterText = string.Empty;
            _mode = FilterMode.Contains;
            _caseSensitive = false;
            _extensionFilter = null;
            _sizeFilter = null;
            _dateFilter = null;
            _attributeFilter = null;
            OnFilterChanged(FilterChangeType.Cleared);
        }

        /// <inheritdoc />
        public void ClearTextFilter()
        {
            _filterText = string.Empty;
            OnFilterChanged(FilterChangeType.TextChanged);
        }

        /// <inheritdoc />
        public void ClearExtensionFilter()
        {
            _extensionFilter = null;
            OnFilterChanged(FilterChangeType.ExtensionChanged);
        }

        /// <inheritdoc />
        public void ClearSizeFilter()
        {
            _sizeFilter = null;
            OnFilterChanged(FilterChangeType.SizeChanged);
        }

        /// <inheritdoc />
        public void ClearDateFilter()
        {
            _dateFilter = null;
            OnFilterChanged(FilterChangeType.DateChanged);
        }

        /// <inheritdoc />
        public void ClearAttributeFilter()
        {
            _attributeFilter = null;
            OnFilterChanged(FilterChangeType.AttributeChanged);
        }

        /// <inheritdoc />
        public void SetExtensionPreset(ExtensionPreset preset)
        {
            if (preset == ExtensionPreset.All)
            {
                _extensionFilter = null;
            }
            else if (ExtensionPresets.TryGetValue(preset, out var extensions))
            {
                _extensionFilter = extensions;
            }
            OnFilterChanged(FilterChangeType.ExtensionChanged);
        }

        /// <inheritdoc />
        public IEnumerable<string> GetFilterHistory(int maxItems = 10)
        {
            lock (_lock)
            {
                return _filterHistory.Take(Math.Min(maxItems, MaxHistoryItems)).ToList();
            }
        }

        /// <inheritdoc />
        public void AddToHistory(string filter)
        {
            if (string.IsNullOrWhiteSpace(filter)) return;

            lock (_lock)
            {
                _filterHistory.Remove(filter);
                _filterHistory.Insert(0, filter);

                if (_filterHistory.Count > MaxHistoryItems)
                {
                    _filterHistory.RemoveRange(MaxHistoryItems, _filterHistory.Count - MaxHistoryItems);
                }

                SaveHistory();
            }
        }

        /// <inheritdoc />
        public void ClearHistory()
        {
            lock (_lock)
            {
                _filterHistory.Clear();
                SaveHistory();
            }
        }

        /// <inheritdoc />
        public async Task SavePresetAsync(string name, FilterPreset preset)
        {
            if (string.IsNullOrWhiteSpace(name)) return;

            lock (_lock)
            {
                _presets.RemoveAll(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
                preset.Name = name;
                preset.CreatedAt = DateTime.UtcNow;
                _presets.Add(preset);
            }

            await Task.Run(() => SavePresets());
        }

        /// <inheritdoc />
        public async Task LoadPresetAsync(string name)
        {
            FilterPreset? preset;
            lock (_lock)
            {
                preset = _presets.FirstOrDefault(p =>
                    string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
            }

            if (preset == null) return;

            await Task.Run(() =>
            {
                _filterText = preset.FilterText ?? string.Empty;
                _mode = preset.Mode;
                _caseSensitive = preset.CaseSensitive;
                _extensionFilter = preset.Extensions;
                _sizeFilter = preset.SizeFilter;
                _dateFilter = preset.DateFilter;
                _attributeFilter = preset.AttributeFilter;
            });

            OnFilterChanged(FilterChangeType.PresetLoaded);
        }

        /// <inheritdoc />
        public IEnumerable<FilterPreset> GetPresets()
        {
            lock (_lock)
            {
                return _presets.ToList();
            }
        }

        /// <inheritdoc />
        public void DeletePreset(string name)
        {
            lock (_lock)
            {
                _presets.RemoveAll(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
                SavePresets();
            }
        }

        /// <inheritdoc />
        public FilterPreset GetCurrentAsPreset()
        {
            return new FilterPreset
            {
                Name = string.Empty,
                FilterText = _filterText,
                Mode = _mode,
                CaseSensitive = _caseSensitive,
                Extensions = _extensionFilter,
                SizeFilter = _sizeFilter,
                DateFilter = _dateFilter,
                AttributeFilter = _attributeFilter,
                CreatedAt = DateTime.UtcNow
            };
        }

        /// <inheritdoc />
        public FilterStatistics GetStatistics(IEnumerable<FileSystemEntry> allFiles, IEnumerable<FileSystemEntry> filteredFiles)
        {
            var allList = allFiles.ToList();
            var filteredList = filteredFiles.ToList();

            return new FilterStatistics
            {
                TotalFiles = allList.Count,
                ShownFiles = filteredList.Count,
                HiddenFiles = allList.Count - filteredList.Count,
                TotalSize = allList.Where(f => !f.IsDirectory).Sum(f => f.Size),
                ShownSize = filteredList.Where(f => !f.IsDirectory).Sum(f => f.Size)
            };
        }

        private void OnFilterChanged(FilterChangeType changeType)
        {
            FilterChanged?.Invoke(this, new FilterChangedEventArgs
            {
                IsActive = IsFiltering,
                FilterText = _filterText,
                ChangeType = changeType
            });
        }

        private void LoadPresets()
        {
            try
            {
                if (File.Exists(_presetPath))
                {
                    var json = File.ReadAllText(_presetPath);
                    var loaded = JsonSerializer.Deserialize<List<FilterPreset>>(json);
                    if (loaded != null)
                    {
                        lock (_lock)
                        {
                            _presets.Clear();
                            _presets.AddRange(loaded);
                        }
                    }
                }
            }
            catch
            {
                // Ignore load errors
            }
        }

        private void SavePresets()
        {
            try
            {
                var dir = Path.GetDirectoryName(_presetPath);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);

                List<FilterPreset> presetsToSave;
                lock (_lock)
                {
                    presetsToSave = _presets.ToList();
                }

                var json = JsonSerializer.Serialize(presetsToSave, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_presetPath, json);
            }
            catch
            {
                // Ignore save errors
            }
        }

        private void LoadHistory()
        {
            try
            {
                if (File.Exists(_historyPath))
                {
                    var json = File.ReadAllText(_historyPath);
                    var loaded = JsonSerializer.Deserialize<List<string>>(json);
                    if (loaded != null)
                    {
                        lock (_lock)
                        {
                            _filterHistory.Clear();
                            _filterHistory.AddRange(loaded);
                        }
                    }
                }
            }
            catch
            {
                // Ignore load errors
            }
        }

        private void SaveHistory()
        {
            try
            {
                var dir = Path.GetDirectoryName(_historyPath);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);

                List<string> historyToSave;
                lock (_lock)
                {
                    historyToSave = _filterHistory.ToList();
                }

                var json = JsonSerializer.Serialize(historyToSave, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_historyPath, json);
            }
            catch
            {
                // Ignore save errors
            }
        }
    }
}
