using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Winhance.Core.Features.FileManager.Interfaces;
using Winhance.Core.Features.FileManager.Models;

namespace Winhance.Infrastructure.Features.FileManager.Services
{
    /// <summary>
    /// Service for managing column configuration in Details view.
    /// </summary>
    public class ColumnService : IColumnService
    {
        private readonly string _preferencesPath;
        private readonly Dictionary<string, List<ColumnDefinition>> _folderColumns = new();
        private List<ColumnDefinition> _currentColumns;
        private List<ColumnDefinition> _availableColumns;

        /// <inheritdoc />
        public IReadOnlyList<ColumnDefinition> CurrentColumns => _currentColumns.AsReadOnly();

        /// <inheritdoc />
        public IReadOnlyList<ColumnDefinition> AvailableColumns => _availableColumns.AsReadOnly();

        /// <inheritdoc />
        public event EventHandler<ColumnsChangedEventArgs>? ColumnsChanged;

        public ColumnService()
        {
            _preferencesPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Winhance", "ColumnPreferences.json");

            _availableColumns = CreateDefaultColumns();
            _currentColumns = _availableColumns.Where(c => c.IsVisible).ToList();
        }

        private List<ColumnDefinition> CreateDefaultColumns()
        {
            return new List<ColumnDefinition>
            {
                new() { Id = "name", DisplayName = "Name", Width = 300, IsVisible = true, DisplayIndex = 0, SortColumn = SortColumn.Name, CanHide = false, ColumnType = ColumnType.Text, BindingPath = "Name" },
                new() { Id = "size", DisplayName = "Size", Width = 100, IsVisible = true, DisplayIndex = 1, SortColumn = SortColumn.Size, Alignment = ColumnAlignment.Right, ColumnType = ColumnType.FileSize, BindingPath = "Size" },
                new() { Id = "dateModified", DisplayName = "Date Modified", Width = 150, IsVisible = true, DisplayIndex = 2, SortColumn = SortColumn.DateModified, ColumnType = ColumnType.DateTime, BindingPath = "DateModified" },
                new() { Id = "type", DisplayName = "Type", Width = 120, IsVisible = true, DisplayIndex = 3, SortColumn = SortColumn.Type, ColumnType = ColumnType.Text, BindingPath = "Extension" },
                new() { Id = "dateCreated", DisplayName = "Date Created", Width = 150, IsVisible = false, DisplayIndex = 4, SortColumn = SortColumn.DateCreated, ColumnType = ColumnType.DateTime, BindingPath = "DateCreated" },
                new() { Id = "dateAccessed", DisplayName = "Date Accessed", Width = 150, IsVisible = false, DisplayIndex = 5, SortColumn = SortColumn.DateModified, ColumnType = ColumnType.DateTime, BindingPath = "DateAccessed" },
                new() { Id = "attributes", DisplayName = "Attributes", Width = 80, IsVisible = false, DisplayIndex = 6, ColumnType = ColumnType.Text, BindingPath = "Attributes" },
                new() { Id = "path", DisplayName = "Path", Width = 300, IsVisible = false, DisplayIndex = 7, ColumnType = ColumnType.Text, BindingPath = "FullPath" }
            };
        }

        /// <inheritdoc />
        public void ShowColumn(string columnId)
        {
            var column = _availableColumns.FirstOrDefault(c => c.Id == columnId);
            if (column != null && !column.IsVisible)
            {
                column.IsVisible = true;
                if (!_currentColumns.Contains(column))
                {
                    _currentColumns.Add(column);
                    ReorderByDisplayIndex();
                }
                RaiseColumnsChanged(ColumnChangeType.VisibilityChanged, columnId);
            }
        }

        /// <inheritdoc />
        public void HideColumn(string columnId)
        {
            var column = _availableColumns.FirstOrDefault(c => c.Id == columnId);
            if (column != null && column.IsVisible && column.CanHide)
            {
                column.IsVisible = false;
                _currentColumns.Remove(column);
                RaiseColumnsChanged(ColumnChangeType.VisibilityChanged, columnId);
            }
        }

        /// <inheritdoc />
        public void ToggleColumn(string columnId)
        {
            var column = _availableColumns.FirstOrDefault(c => c.Id == columnId);
            if (column != null)
            {
                if (column.IsVisible)
                    HideColumn(columnId);
                else
                    ShowColumn(columnId);
            }
        }

        /// <inheritdoc />
        public void SetColumnWidth(string columnId, double width)
        {
            var column = _availableColumns.FirstOrDefault(c => c.Id == columnId);
            if (column != null)
            {
                width = Math.Max(column.MinWidth, Math.Min(column.MaxWidth, width));
                column.Width = width;
                column.IsAutoWidth = false;
                RaiseColumnsChanged(ColumnChangeType.WidthChanged, columnId);
            }
        }

        /// <inheritdoc />
        public void MoveColumn(string columnId, int newIndex)
        {
            var column = _currentColumns.FirstOrDefault(c => c.Id == columnId);
            if (column != null)
            {
                _currentColumns.Remove(column);
                newIndex = Math.Max(0, Math.Min(newIndex, _currentColumns.Count));
                _currentColumns.Insert(newIndex, column);

                // Update display indices
                for (int i = 0; i < _currentColumns.Count; i++)
                {
                    _currentColumns[i].DisplayIndex = i;
                }

                RaiseColumnsChanged(ColumnChangeType.OrderChanged, columnId);
            }
        }

        /// <inheritdoc />
        public void ResetToDefaults()
        {
            _availableColumns = CreateDefaultColumns();
            _currentColumns = _availableColumns.Where(c => c.IsVisible).ToList();
            _folderColumns.Clear();
            RaiseColumnsChanged(ColumnChangeType.Reset, null);
        }

        /// <inheritdoc />
        public IReadOnlyList<ColumnDefinition> GetColumnsForFolder(string folderPath)
        {
            if (_folderColumns.TryGetValue(folderPath, out var columns))
            {
                return columns.AsReadOnly();
            }
            return _currentColumns.AsReadOnly();
        }

        /// <inheritdoc />
        public void SetColumnsForFolder(string folderPath, IEnumerable<ColumnDefinition> columns)
        {
            _folderColumns[folderPath] = columns.ToList();
        }

        /// <inheritdoc />
        public void ClearColumnsForFolder(string folderPath)
        {
            _folderColumns.Remove(folderPath);
        }

        /// <inheritdoc />
        public void AutoSizeColumn(string columnId)
        {
            var column = _availableColumns.FirstOrDefault(c => c.Id == columnId);
            if (column != null)
            {
                column.IsAutoWidth = true;
                // Actual auto-sizing would need to be done by the UI layer
                RaiseColumnsChanged(ColumnChangeType.WidthChanged, columnId);
            }
        }

        /// <inheritdoc />
        public void AutoSizeAllColumns()
        {
            foreach (var column in _currentColumns)
            {
                column.IsAutoWidth = true;
            }
            RaiseColumnsChanged(ColumnChangeType.WidthChanged, null);
        }

        /// <inheritdoc />
        public async Task SavePreferencesAsync()
        {
            try
            {
                var dir = Path.GetDirectoryName(_preferencesPath);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);

                var data = new ColumnPreferencesData
                {
                    AvailableColumns = _availableColumns,
                    FolderColumns = _folderColumns
                };

                var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(_preferencesPath, json);
            }
            catch
            {
                // Ignore save errors
            }
        }

        /// <inheritdoc />
        public async Task LoadPreferencesAsync()
        {
            try
            {
                if (File.Exists(_preferencesPath))
                {
                    var json = await File.ReadAllTextAsync(_preferencesPath);
                    var data = JsonSerializer.Deserialize<ColumnPreferencesData>(json);

                    if (data?.AvailableColumns != null)
                    {
                        _availableColumns = data.AvailableColumns;
                        _currentColumns = _availableColumns.Where(c => c.IsVisible).OrderBy(c => c.DisplayIndex).ToList();
                    }

                    if (data?.FolderColumns != null)
                    {
                        _folderColumns.Clear();
                        foreach (var kvp in data.FolderColumns)
                        {
                            _folderColumns[kvp.Key] = kvp.Value;
                        }
                    }

                    RaiseColumnsChanged(ColumnChangeType.Loaded, null);
                }
            }
            catch
            {
                // Ignore load errors, use defaults
            }
        }

        /// <inheritdoc />
        public Func<FileSystemEntry, object?> GetValueExtractor(string columnId)
        {
            return columnId switch
            {
                "name" => entry => entry.Name,
                "size" => entry => entry.IsDirectory ? null : (object?)entry.Size,
                "dateModified" => entry => entry.DateModified,
                "dateCreated" => entry => entry.DateCreated,
                "dateAccessed" => entry => entry.DateAccessed,
                "type" => entry => entry.IsDirectory ? "Folder" : entry.Extension,
                "attributes" => entry => entry.Attributes.ToString(),
                "path" => entry => entry.FullPath,
                _ => entry => null
            };
        }

        /// <inheritdoc />
        public Func<object?, string> GetDisplayFormatter(string columnId)
        {
            return columnId switch
            {
                "size" => value => value is long size ? FormatFileSize(size) : "",
                "dateModified" or "dateCreated" or "dateAccessed" => value => value is DateTime dt ? dt.ToString("g") : "",
                _ => value => value?.ToString() ?? ""
            };
        }

        private void ReorderByDisplayIndex()
        {
            _currentColumns = _currentColumns.OrderBy(c => c.DisplayIndex).ToList();
        }

        private void RaiseColumnsChanged(ColumnChangeType changeType, string? columnId)
        {
            ColumnsChanged?.Invoke(this, new ColumnsChangedEventArgs
            {
                ChangeType = changeType,
                ColumnId = columnId,
                Columns = _currentColumns.AsReadOnly()
            });
        }

        private static string FormatFileSize(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
            int suffixIndex = 0;
            double size = bytes;

            while (size >= 1024 && suffixIndex < suffixes.Length - 1)
            {
                size /= 1024;
                suffixIndex++;
            }

            return $"{size:0.##} {suffixes[suffixIndex]}";
        }

        private class ColumnPreferencesData
        {
            public List<ColumnDefinition> AvailableColumns { get; set; } = new();
            public Dictionary<string, List<ColumnDefinition>> FolderColumns { get; set; } = new();
        }
    }
}
