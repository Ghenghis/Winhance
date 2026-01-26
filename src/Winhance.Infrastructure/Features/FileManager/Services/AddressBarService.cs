using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.FileManager.Interfaces;

namespace Winhance.Infrastructure.Features.FileManager.Services
{
    /// <summary>
    /// Service for address bar functionality including breadcrumb navigation,
    /// path autocomplete, and special folder handling.
    /// </summary>
    public class AddressBarService : IAddressBarService
    {
        private string _currentPath = string.Empty;
        private List<BreadcrumbSegment> _breadcrumbs = new();
        private readonly List<string> _recentPaths = new();
        private readonly Dictionary<string, int> _frequentPaths = new();
        private readonly int _maxRecentPaths = 50;
        private readonly object _lock = new();

        // Special folder mappings
        private static readonly Dictionary<string, SpecialFolderType> SpecialFolders = new(StringComparer.OrdinalIgnoreCase)
        {
            { Environment.GetFolderPath(Environment.SpecialFolder.Desktop), SpecialFolderType.Desktop },
            { Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), SpecialFolderType.Documents },
            { GetDownloadsPath(), SpecialFolderType.Downloads },
            { Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), SpecialFolderType.Pictures },
            { Environment.GetFolderPath(Environment.SpecialFolder.MyMusic), SpecialFolderType.Music },
            { Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), SpecialFolderType.Videos },
            { Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), SpecialFolderType.UserProfile }
        };

        /// <inheritdoc />
        public string CurrentPath
        {
            get => _currentPath;
            set
            {
                if (_currentPath != value)
                {
                    var oldPath = _currentPath;
                    _currentPath = value;
                    UpdateBreadcrumbs();
                    PathChanged?.Invoke(this, new PathChangedEventArgs
                    {
                        OldPath = oldPath,
                        NewPath = value,
                        AddedToHistory = true
                    });
                }
            }
        }

        /// <inheritdoc />
        public IReadOnlyList<BreadcrumbSegment> Breadcrumbs => _breadcrumbs.AsReadOnly();

        /// <inheritdoc />
        public bool IsEditing { get; set; }

        /// <inheritdoc />
        public event EventHandler<PathChangedEventArgs>? PathChanged;

        /// <inheritdoc />
        public event EventHandler? BreadcrumbsChanged;

        /// <inheritdoc />
        public async Task<bool> NavigateAsync(string path, bool addToHistory = true)
        {
            var expanded = ExpandPath(path);
            var validation = ValidatePath(expanded);

            if (!validation.IsValid || !validation.Exists)
                return false;

            var oldPath = _currentPath;
            _currentPath = validation.NormalizedPath;
            UpdateBreadcrumbs();

            if (addToHistory)
            {
                AddToRecent(_currentPath);
                IncrementFrequency(_currentPath);
            }

            PathChanged?.Invoke(this, new PathChangedEventArgs
            {
                OldPath = oldPath,
                NewPath = _currentPath,
                AddedToHistory = addToHistory
            });

            return await Task.FromResult(true);
        }

        /// <inheritdoc />
        public async Task<bool> NavigateToBreadcrumbAsync(BreadcrumbSegment segment)
        {
            return await NavigateAsync(segment.FullPath, true);
        }

        /// <inheritdoc />
        public async Task<IEnumerable<PathSuggestion>> GetSuggestionsAsync(
            string input,
            int maxResults = 10,
            CancellationToken cancellationToken = default)
        {
            var suggestions = new List<PathSuggestion>();

            if (string.IsNullOrWhiteSpace(input))
            {
                // Return recent and frequent paths
                foreach (var path in _recentPaths.Take(5))
                {
                    suggestions.Add(new PathSuggestion
                    {
                        Type = SuggestionType.Recent,
                        DisplayText = GetDisplayName(path),
                        FullPath = path,
                        Icon = GetPathIcon(path),
                        Score = 90
                    });
                }
                return suggestions;
            }

            var expanded = ExpandPath(input);

            // Check for special folder shortcuts
            var specialSuggestions = GetSpecialFolderSuggestions(input);
            suggestions.AddRange(specialSuggestions);

            // Path completion
            await Task.Run(() =>
            {
                try
                {
                    string directory;
                    string searchPattern;

                    if (Directory.Exists(expanded))
                    {
                        directory = expanded;
                        searchPattern = "*";
                    }
                    else
                    {
                        directory = Path.GetDirectoryName(expanded) ?? string.Empty;
                        searchPattern = Path.GetFileName(expanded) + "*";
                    }

                    if (Directory.Exists(directory))
                    {
                        var entries = Directory.GetDirectories(directory, searchPattern)
                            .Take(maxResults - suggestions.Count);

                        foreach (var entry in entries)
                        {
                            cancellationToken.ThrowIfCancellationRequested();

                            var name = Path.GetFileName(entry);
                            var matchStart = name.IndexOf(
                                Path.GetFileName(input),
                                StringComparison.OrdinalIgnoreCase);

                            suggestions.Add(new PathSuggestion
                            {
                                Type = SuggestionType.Path,
                                DisplayText = name,
                                FullPath = entry,
                                Icon = GetPathIcon(entry),
                                SecondaryText = entry,
                                Score = 80,
                                MatchStart = matchStart >= 0 ? matchStart : 0,
                                MatchLength = Path.GetFileName(input).Length
                            });
                        }
                    }
                }
                catch { }
            }, cancellationToken);

            // Search recent paths
            foreach (var path in _recentPaths)
            {
                if (path.Contains(input, StringComparison.OrdinalIgnoreCase))
                {
                    suggestions.Add(new PathSuggestion
                    {
                        Type = SuggestionType.Recent,
                        DisplayText = GetDisplayName(path),
                        FullPath = path,
                        Icon = GetPathIcon(path),
                        Score = 70
                    });
                }
            }

            return suggestions
                .OrderByDescending(s => s.Score)
                .ThenBy(s => s.DisplayText)
                .Take(maxResults);
        }

        /// <inheritdoc />
        public async Task<IEnumerable<FolderItem>> GetSubfoldersAsync(
            BreadcrumbSegment segment,
            CancellationToken cancellationToken = default)
        {
            var folders = new List<FolderItem>();

            await Task.Run(() =>
            {
                try
                {
                    if (segment.SpecialFolder == SpecialFolderType.ThisPc)
                    {
                        // Return drives
                        foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady))
                        {
                            folders.Add(new FolderItem
                            {
                                Name = $"{drive.Name} ({drive.VolumeLabel})",
                                FullPath = drive.Name,
                                Icon = "Drive"
                            });
                        }
                    }
                    else if (Directory.Exists(segment.FullPath))
                    {
                        foreach (var dir in Directory.GetDirectories(segment.FullPath))
                        {
                            cancellationToken.ThrowIfCancellationRequested();

                            var di = new DirectoryInfo(dir);
                            folders.Add(new FolderItem
                            {
                                Name = di.Name,
                                FullPath = dir,
                                Icon = GetPathIcon(dir),
                                IsHidden = (di.Attributes & FileAttributes.Hidden) != 0,
                                IsSystem = (di.Attributes & FileAttributes.System) != 0
                            });
                        }
                    }
                }
                catch { }
            }, cancellationToken);

            return folders.OrderBy(f => f.Name);
        }

        /// <inheritdoc />
        public IEnumerable<BreadcrumbSegment> ParsePath(string path)
        {
            var segments = new List<BreadcrumbSegment>();

            if (string.IsNullOrEmpty(path)) return segments;

            // Add "This PC" as root
            segments.Add(new BreadcrumbSegment
            {
                DisplayName = "This PC",
                FullPath = "::{20D04FE0-3AEA-1069-A2D8-08002B30309D}",
                Icon = "Computer",
                IsSpecialFolder = true,
                SpecialFolder = SpecialFolderType.ThisPc,
                HasChildren = true,
                Index = 0
            });

            var parts = path.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
            var currentPath = string.Empty;

            for (int i = 0; i < parts.Length; i++)
            {
                var part = parts[i];

                if (i == 0 && part.EndsWith(":"))
                {
                    // Drive root
                    currentPath = part + Path.DirectorySeparatorChar;
                    var driveInfo = new DriveInfo(currentPath);

                    segments.Add(new BreadcrumbSegment
                    {
                        DisplayName = $"{part} ({driveInfo.VolumeLabel})",
                        FullPath = currentPath,
                        Icon = "Drive",
                        IsSpecialFolder = true,
                        SpecialFolder = SpecialFolderType.DriveRoot,
                        HasChildren = true,
                        Index = segments.Count
                    });
                }
                else
                {
                    currentPath = Path.Combine(currentPath, part);

                    var isSpecial = SpecialFolders.TryGetValue(currentPath, out var specialType);
                    var hasChildren = false;

                    try
                    {
                        hasChildren = Directory.Exists(currentPath) &&
                                     Directory.GetDirectories(currentPath).Length > 0;
                    }
                    catch { }

                    segments.Add(new BreadcrumbSegment
                    {
                        DisplayName = part,
                        FullPath = currentPath,
                        Icon = GetPathIcon(currentPath),
                        IsSpecialFolder = isSpecial,
                        SpecialFolder = isSpecial ? specialType : null,
                        HasChildren = hasChildren,
                        Index = segments.Count,
                        IsLast = i == parts.Length - 1
                    });
                }
            }

            if (segments.Count > 0)
                segments[segments.Count - 1].IsLast = true;

            return segments;
        }

        /// <inheritdoc />
        public PathValidationResult ValidatePath(string path)
        {
            var result = new PathValidationResult();

            if (string.IsNullOrWhiteSpace(path))
            {
                result.IsValid = false;
                result.ErrorMessage = "Path cannot be empty";
                return result;
            }

            // Check for invalid characters
            var invalidChars = Path.GetInvalidPathChars();
            var invalidPositions = new List<int>();

            for (int i = 0; i < path.Length; i++)
            {
                if (invalidChars.Contains(path[i]))
                {
                    invalidPositions.Add(i);
                }
            }

            if (invalidPositions.Any())
            {
                result.IsValid = false;
                result.ErrorMessage = "Path contains invalid characters";
                result.InvalidCharPositions = invalidPositions.ToArray();
                return result;
            }

            try
            {
                result.NormalizedPath = Path.GetFullPath(path);
                result.IsValid = true;
                result.Exists = Directory.Exists(result.NormalizedPath) || File.Exists(result.NormalizedPath);
            }
            catch (Exception ex)
            {
                result.IsValid = false;
                result.ErrorMessage = ex.Message;
            }

            return result;
        }

        /// <inheritdoc />
        public string ExpandPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return path;

            // Expand environment variables
            var expanded = Environment.ExpandEnvironmentVariables(path);

            // Handle special shortcuts
            expanded = expanded switch
            {
                "~" => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                var p when p.StartsWith("~/") => Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    p.Substring(2)),
                "::{20D04FE0-3AEA-1069-A2D8-08002B30309D}" => "::{20D04FE0-3AEA-1069-A2D8-08002B30309D}",
                _ => expanded
            };

            return expanded;
        }

        /// <inheritdoc />
        public string GetDisplayName(string path)
        {
            if (string.IsNullOrEmpty(path)) return string.Empty;

            // Check for special folders
            if (SpecialFolders.TryGetValue(path, out var specialType))
            {
                return specialType switch
                {
                    SpecialFolderType.Desktop => "Desktop",
                    SpecialFolderType.Documents => "Documents",
                    SpecialFolderType.Downloads => "Downloads",
                    SpecialFolderType.Pictures => "Pictures",
                    SpecialFolderType.Music => "Music",
                    SpecialFolderType.Videos => "Videos",
                    SpecialFolderType.UserProfile => Environment.UserName,
                    _ => Path.GetFileName(path)
                };
            }

            // Check for drive root
            if (path.Length == 3 && path.EndsWith(":\\"))
            {
                try
                {
                    var drive = new DriveInfo(path);
                    return $"{path[0]}: ({drive.VolumeLabel})";
                }
                catch
                {
                    return path;
                }
            }

            return Path.GetFileName(path);
        }

        /// <inheritdoc />
        public string GetPathIcon(string path)
        {
            if (string.IsNullOrEmpty(path)) return "Folder";

            // Special folder icons
            if (SpecialFolders.TryGetValue(path, out var specialType))
            {
                return specialType switch
                {
                    SpecialFolderType.Desktop => "Desktop",
                    SpecialFolderType.Documents => "Document",
                    SpecialFolderType.Downloads => "Download",
                    SpecialFolderType.Pictures => "Image",
                    SpecialFolderType.Music => "Music",
                    SpecialFolderType.Videos => "Video",
                    SpecialFolderType.UserProfile => "User",
                    _ => "Folder"
                };
            }

            // Drive icons
            if (path.Length == 3 && path.EndsWith(":\\"))
            {
                try
                {
                    var drive = new DriveInfo(path);
                    return drive.DriveType switch
                    {
                        DriveType.Fixed => "HardDrive",
                        DriveType.Removable => "UsbDrive",
                        DriveType.Network => "NetworkDrive",
                        DriveType.CDRom => "Disc",
                        _ => "Drive"
                    };
                }
                catch
                {
                    return "Drive";
                }
            }

            return "Folder";
        }

        /// <inheritdoc />
        public void CopyPathToClipboard(PathCopyFormat format = PathCopyFormat.FullPath)
        {
            var formatted = format switch
            {
                PathCopyFormat.FullPath => _currentPath,
                PathCopyFormat.NameOnly => Path.GetFileName(_currentPath),
                PathCopyFormat.Quoted => $"\"{_currentPath}\"",
                PathCopyFormat.UnixStyle => "/" + _currentPath.Replace(":", "").Replace("\\", "/"),
                PathCopyFormat.UncPath => @"\\?\" + _currentPath,
                PathCopyFormat.FileUrl => "file:///" + _currentPath.Replace("\\", "/").Replace(" ", "%20"),
                _ => _currentPath
            };

            try
            {
                System.Windows.Clipboard.SetText(formatted);
            }
            catch { }
        }

        private void UpdateBreadcrumbs()
        {
            _breadcrumbs = ParsePath(_currentPath).ToList();
            BreadcrumbsChanged?.Invoke(this, EventArgs.Empty);
        }

        private void AddToRecent(string path)
        {
            lock (_lock)
            {
                _recentPaths.Remove(path);
                _recentPaths.Insert(0, path);

                while (_recentPaths.Count > _maxRecentPaths)
                    _recentPaths.RemoveAt(_recentPaths.Count - 1);
            }
        }

        private void IncrementFrequency(string path)
        {
            lock (_lock)
            {
                _frequentPaths.TryGetValue(path, out var count);
                _frequentPaths[path] = count + 1;
            }
        }

        private IEnumerable<PathSuggestion> GetSpecialFolderSuggestions(string input)
        {
            var suggestions = new List<PathSuggestion>();
            var lowerInput = input.ToLowerInvariant();

            var shortcuts = new Dictionary<string, (string Path, string Icon)>
            {
                { "desktop", (Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Desktop") },
                { "documents", (Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Document") },
                { "downloads", (GetDownloadsPath(), "Download") },
                { "pictures", (Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Image") },
                { "music", (Environment.GetFolderPath(Environment.SpecialFolder.MyMusic), "Music") },
                { "videos", (Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "Video") },
                { "home", (Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "User") },
                { "appdata", (Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Folder") },
                { "temp", (Path.GetTempPath(), "Folder") }
            };

            foreach (var (key, value) in shortcuts)
            {
                if (key.StartsWith(lowerInput))
                {
                    suggestions.Add(new PathSuggestion
                    {
                        Type = SuggestionType.SpecialFolder,
                        DisplayText = char.ToUpper(key[0]) + key.Substring(1),
                        FullPath = value.Path,
                        Icon = value.Icon,
                        Score = 100
                    });
                }
            }

            return suggestions;
        }

        private static string GetDownloadsPath()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        }
    }
}
