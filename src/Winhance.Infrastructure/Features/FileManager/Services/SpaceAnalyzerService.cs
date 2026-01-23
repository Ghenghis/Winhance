using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.FileManager.Interfaces;

namespace Winhance.Infrastructure.Features.FileManager.Services
{
    /// <summary>
    /// Disk space analyzer with treemap visualization support.
    /// Provides detailed breakdown of space usage by folder, file type, and age.
    /// </summary>
    public class SpaceAnalyzerService : ISpaceAnalyzerService
    {
        private readonly ILogService _logService;
        private static readonly Dictionary<string, (string Category, string Color)> FileTypeCategories = new()
        {
            // Documents
            { ".pdf", ("Documents", "#E74C3C") },
            { ".doc", ("Documents", "#E74C3C") },
            { ".docx", ("Documents", "#E74C3C") },
            { ".xls", ("Documents", "#E74C3C") },
            { ".xlsx", ("Documents", "#E74C3C") },
            { ".ppt", ("Documents", "#E74C3C") },
            { ".pptx", ("Documents", "#E74C3C") },
            { ".txt", ("Documents", "#E74C3C") },
            { ".rtf", ("Documents", "#E74C3C") },
            
            // Images
            { ".jpg", ("Images", "#3498DB") },
            { ".jpeg", ("Images", "#3498DB") },
            { ".png", ("Images", "#3498DB") },
            { ".gif", ("Images", "#3498DB") },
            { ".bmp", ("Images", "#3498DB") },
            { ".svg", ("Images", "#3498DB") },
            { ".webp", ("Images", "#3498DB") },
            { ".ico", ("Images", "#3498DB") },
            { ".raw", ("Images", "#3498DB") },
            
            // Video
            { ".mp4", ("Video", "#9B59B6") },
            { ".avi", ("Video", "#9B59B6") },
            { ".mkv", ("Video", "#9B59B6") },
            { ".mov", ("Video", "#9B59B6") },
            { ".wmv", ("Video", "#9B59B6") },
            { ".flv", ("Video", "#9B59B6") },
            { ".webm", ("Video", "#9B59B6") },
            
            // Audio
            { ".mp3", ("Audio", "#1ABC9C") },
            { ".wav", ("Audio", "#1ABC9C") },
            { ".flac", ("Audio", "#1ABC9C") },
            { ".aac", ("Audio", "#1ABC9C") },
            { ".ogg", ("Audio", "#1ABC9C") },
            { ".wma", ("Audio", "#1ABC9C") },
            
            // Archives
            { ".zip", ("Archives", "#F39C12") },
            { ".rar", ("Archives", "#F39C12") },
            { ".7z", ("Archives", "#F39C12") },
            { ".tar", ("Archives", "#F39C12") },
            { ".gz", ("Archives", "#F39C12") },
            
            // Code
            { ".cs", ("Code", "#2ECC71") },
            { ".js", ("Code", "#2ECC71") },
            { ".ts", ("Code", "#2ECC71") },
            { ".py", ("Code", "#2ECC71") },
            { ".java", ("Code", "#2ECC71") },
            { ".cpp", ("Code", "#2ECC71") },
            { ".c", ("Code", "#2ECC71") },
            { ".h", ("Code", "#2ECC71") },
            { ".rs", ("Code", "#2ECC71") },
            { ".go", ("Code", "#2ECC71") },
            
            // Executables
            { ".exe", ("Executables", "#E67E22") },
            { ".dll", ("Executables", "#E67E22") },
            { ".msi", ("Executables", "#E67E22") },
            { ".sys", ("Executables", "#E67E22") }
        };

        public SpaceAnalyzerService(ILogService logService)
        {
            _logService = logService;
        }

        public async Task<SpaceAnalysisResult> AnalyzeAsync(string path, SpaceAnalysisOptions options,
            IProgress<SpaceAnalysisProgress>? progress = null, CancellationToken ct = default)
        {
            var result = new SpaceAnalysisResult { RootPath = path };
            var sw = Stopwatch.StartNew();

            progress?.Report(new SpaceAnalysisProgress { Phase = "Scanning", CurrentPath = path });

            // Build tree structure
            result.RootNode = await BuildTreeNodeAsync(path, options, 0, progress, ct);
            
            // Calculate totals
            CalculateTotals(result.RootNode, result);

            // Get largest files and folders
            var allFiles = new List<FileSpaceInfo>();
            var allFolders = new List<FolderSpaceInfo>();
            CollectFilesAndFolders(result.RootNode, allFiles, allFolders);

            result.LargestFiles = allFiles.OrderByDescending(f => f.Size).Take(100).ToList();
            result.LargestFolders = allFolders.OrderByDescending(f => f.Size).Take(100).ToList();

            // Calculate space by type
            result.SpaceByType = CalculateSpaceByType(allFiles, result.TotalSize);

            sw.Stop();
            result.AnalysisDuration = sw.Elapsed;

            _logService.Log(LogLevel.Info, 
                $"Space analysis complete: {result.FormattedSize} in {result.TotalFiles:N0} files");

            return result;
        }

        private async Task<TreemapNode> BuildTreeNodeAsync(string path, SpaceAnalysisOptions options, 
            int depth, IProgress<SpaceAnalysisProgress>? progress, CancellationToken ct)
        {
            var node = new TreemapNode
            {
                Name = Path.GetFileName(path),
                FullPath = path,
                IsFile = false
            };

            if (string.IsNullOrEmpty(node.Name))
                node.Name = path; // For drive roots

            if (depth > options.MaxDepth) return node;

            try
            {
                // Process files
                foreach (var file in Directory.EnumerateFiles(path))
                {
                    if (ct.IsCancellationRequested) break;

                    try
                    {
                        var fi = new FileInfo(file);
                        
                        if (!options.IncludeHiddenFiles && fi.Attributes.HasFlag(FileAttributes.Hidden))
                            continue;
                        if (!options.IncludeSystemFiles && fi.Attributes.HasFlag(FileAttributes.System))
                            continue;
                        if (fi.Length < options.MinFileSize)
                            continue;

                        var ext = fi.Extension.ToLowerInvariant();
                        var (category, color) = FileTypeCategories.GetValueOrDefault(ext, ("Other", "#95A5A6"));

                        node.Children.Add(new TreemapNode
                        {
                            Name = fi.Name,
                            FullPath = fi.FullName,
                            Size = fi.Length,
                            IsFile = true,
                            Color = color
                        });

                        node.Size += fi.Length;
                    }
                    catch (UnauthorizedAccessException) { /* Skip inaccessible files */ }
                    catch (IOException) { /* Skip inaccessible files */ }
                }

                // Process subdirectories
                foreach (var dir in Directory.EnumerateDirectories(path))
                {
                    if (ct.IsCancellationRequested) break;

                    try
                    {
                        var di = new DirectoryInfo(dir);
                        
                        if (!options.IncludeHiddenFiles && di.Attributes.HasFlag(FileAttributes.Hidden))
                            continue;
                        if (!options.IncludeSystemFiles && di.Attributes.HasFlag(FileAttributes.System))
                            continue;

                        progress?.Report(new SpaceAnalysisProgress
                        {
                            Phase = "Scanning",
                            CurrentPath = dir
                        });

                        var childNode = await BuildTreeNodeAsync(dir, options, depth + 1, progress, ct);
                        node.Children.Add(childNode);
                        node.Size += childNode.Size;
                    }
                    catch (UnauthorizedAccessException) { /* Skip inaccessible directories */ }
                    catch (IOException) { /* Skip inaccessible directories */ }
                }

                // Sort children by size descending
                node.Children = node.Children.OrderByDescending(c => c.Size).ToList();

                // Assign colors based on relative size
                if (!node.IsFile && node.Size > 0)
                {
                    foreach (var child in node.Children)
                    {
                        child.Percentage = (double)child.Size / node.Size * 100;
                    }
                }
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Warning, $"Error analyzing {path}: {ex.Message}");
            }

            return node;
        }

        private void CalculateTotals(TreemapNode node, SpaceAnalysisResult result)
        {
            if (node.IsFile)
            {
                result.TotalFiles++;
                result.TotalSize += node.Size;
            }
            else
            {
                result.TotalFolders++;
                foreach (var child in node.Children)
                {
                    CalculateTotals(child, result);
                }
            }
        }

        private void CollectFilesAndFolders(TreemapNode node, List<FileSpaceInfo> files, List<FolderSpaceInfo> folders)
        {
            if (node.IsFile)
            {
                files.Add(new FileSpaceInfo
                {
                    Path = node.FullPath,
                    Name = node.Name,
                    Extension = Path.GetExtension(node.Name),
                    Size = node.Size
                });
            }
            else
            {
                long fileCount = 0, folderCount = 0;
                CountItems(node, ref fileCount, ref folderCount);

                folders.Add(new FolderSpaceInfo
                {
                    Path = node.FullPath,
                    Name = node.Name,
                    Size = node.Size,
                    FileCount = fileCount,
                    FolderCount = folderCount,
                    Percentage = node.Percentage
                });

                foreach (var child in node.Children)
                {
                    CollectFilesAndFolders(child, files, folders);
                }
            }
        }

        private void CountItems(TreemapNode node, ref long files, ref long folders)
        {
            if (node.IsFile) files++;
            else
            {
                folders++;
                foreach (var child in node.Children)
                    CountItems(child, ref files, ref folders);
            }
        }

        private List<FileTypeSpaceInfo> CalculateSpaceByType(List<FileSpaceInfo> files, long totalSize)
        {
            return files
                .GroupBy(f => f.Extension.ToLowerInvariant())
                .Select(g =>
                {
                    var ext = g.Key;
                    var (category, color) = FileTypeCategories.GetValueOrDefault(ext, ("Other", "#95A5A6"));
                    var size = g.Sum(f => f.Size);
                    
                    return new FileTypeSpaceInfo
                    {
                        Extension = ext,
                        Category = category,
                        TotalSize = size,
                        FileCount = g.Count(),
                        Percentage = totalSize > 0 ? (double)size / totalSize * 100 : 0,
                        Color = color
                    };
                })
                .OrderByDescending(t => t.TotalSize)
                .ToList();
        }

        public async Task<IEnumerable<FileSpaceInfo>> GetLargestFilesAsync(string path, int count = 100,
            CancellationToken ct = default)
        {
            var files = new List<FileSpaceInfo>();

            await Task.Run(() =>
            {
                try
                {
                    foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                    {
                        if (ct.IsCancellationRequested) break;

                        try
                        {
                            var fi = new FileInfo(file);
                            files.Add(new FileSpaceInfo
                            {
                                Path = fi.FullName,
                                Name = fi.Name,
                                Extension = fi.Extension,
                                Size = fi.Length,
                                ModifiedTime = fi.LastWriteTime
                            });
                        }
                        catch (UnauthorizedAccessException) { /* Skip inaccessible files */ }
                    catch (IOException) { /* Skip inaccessible files */ }
                    }
                }
                catch (UnauthorizedAccessException) { /* Skip inaccessible paths */ }
                catch (IOException) { /* Skip inaccessible paths */ }
            }, ct);

            return files.OrderByDescending(f => f.Size).Take(count);
        }

        public async Task<IEnumerable<FolderSpaceInfo>> GetLargestFoldersAsync(string path, int count = 100,
            CancellationToken ct = default)
        {
            var folders = new List<FolderSpaceInfo>();

            await Task.Run(() =>
            {
                try
                {
                    foreach (var dir in Directory.EnumerateDirectories(path, "*", SearchOption.AllDirectories))
                    {
                        if (ct.IsCancellationRequested) break;

                        try
                        {
                            var size = GetDirectorySize(dir);
                            folders.Add(new FolderSpaceInfo
                            {
                                Path = dir,
                                Name = Path.GetFileName(dir),
                                Size = size
                            });
                        }
                        catch (UnauthorizedAccessException) { /* Skip inaccessible directories */ }
                    catch (IOException) { /* Skip inaccessible directories */ }
                    }
                }
                catch (UnauthorizedAccessException) { /* Skip inaccessible paths */ }
                catch (IOException) { /* Skip inaccessible paths */ }
            }, ct);

            return folders.OrderByDescending(f => f.Size).Take(count);
        }

        private long GetDirectorySize(string path)
        {
            long size = 0;
            try
            {
                foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                {
                    try { size += new FileInfo(file).Length; }
                    catch (UnauthorizedAccessException) { /* Skip inaccessible */ }
                    catch (IOException) { /* Skip inaccessible */ }
                }
            }
            catch (UnauthorizedAccessException) { /* Skip inaccessible */ }
                    catch (IOException) { /* Skip inaccessible */ }
            return size;
        }

        public async Task<IEnumerable<FileTypeSpaceInfo>> GetSpaceByFileTypeAsync(string path,
            CancellationToken ct = default)
        {
            var files = await GetLargestFilesAsync(path, int.MaxValue, ct);
            var totalSize = files.Sum(f => f.Size);
            return CalculateSpaceByType(files.ToList(), totalSize);
        }

        public async Task<IEnumerable<AgeGroupSpaceInfo>> GetSpaceByAgeAsync(string path,
            CancellationToken ct = default)
        {
            var ageGroups = new Dictionary<string, (long Size, long Count, DateTime Oldest, DateTime Newest)>
            {
                { "< 1 week", (0, 0, DateTime.MaxValue, DateTime.MinValue) },
                { "1 week - 1 month", (0, 0, DateTime.MaxValue, DateTime.MinValue) },
                { "1 - 3 months", (0, 0, DateTime.MaxValue, DateTime.MinValue) },
                { "3 - 6 months", (0, 0, DateTime.MaxValue, DateTime.MinValue) },
                { "6 months - 1 year", (0, 0, DateTime.MaxValue, DateTime.MinValue) },
                { "1 - 2 years", (0, 0, DateTime.MaxValue, DateTime.MinValue) },
                { "> 2 years", (0, 0, DateTime.MaxValue, DateTime.MinValue) }
            };

            var now = DateTime.Now;

            await Task.Run(() =>
            {
                try
                {
                    foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                    {
                        if (ct.IsCancellationRequested) break;

                        try
                        {
                            var fi = new FileInfo(file);
                            var age = now - fi.LastWriteTime;
                            var group = age.TotalDays switch
                            {
                                < 7 => "< 1 week",
                                < 30 => "1 week - 1 month",
                                < 90 => "1 - 3 months",
                                < 180 => "3 - 6 months",
                                < 365 => "6 months - 1 year",
                                < 730 => "1 - 2 years",
                                _ => "> 2 years"
                            };

                            var current = ageGroups[group];
                            ageGroups[group] = (
                                current.Size + fi.Length,
                                current.Count + 1,
                                fi.LastWriteTime < current.Oldest ? fi.LastWriteTime : current.Oldest,
                                fi.LastWriteTime > current.Newest ? fi.LastWriteTime : current.Newest
                            );
                        }
                        catch (UnauthorizedAccessException) { /* Skip inaccessible */ }
                    catch (IOException) { /* Skip inaccessible */ }
                    }
                }
                catch (UnauthorizedAccessException) { /* Skip inaccessible */ }
                    catch (IOException) { /* Skip inaccessible */ }
            }, ct);

            return ageGroups.Select(g => new AgeGroupSpaceInfo
            {
                AgeGroup = g.Key,
                TotalSize = g.Value.Size,
                FileCount = g.Value.Count,
                OldestFile = g.Value.Oldest == DateTime.MaxValue ? DateTime.MinValue : g.Value.Oldest,
                NewestFile = g.Value.Newest == DateTime.MinValue ? DateTime.MaxValue : g.Value.Newest
            }).Where(g => g.FileCount > 0);
        }

        public async Task<IEnumerable<string>> FindEmptyFoldersAsync(string path, bool recursive = true,
            CancellationToken ct = default)
        {
            var emptyFolders = new List<string>();

            await Task.Run(() =>
            {
                var option = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                try
                {
                    foreach (var dir in Directory.EnumerateDirectories(path, "*", option))
                    {
                        if (ct.IsCancellationRequested) break;

                        try
                        {
                            if (!Directory.EnumerateFileSystemEntries(dir).Any())
                                emptyFolders.Add(dir);
                        }
                        catch (UnauthorizedAccessException) { /* Skip inaccessible */ }
                    catch (IOException) { /* Skip inaccessible */ }
                    }
                }
                catch (UnauthorizedAccessException) { /* Skip inaccessible */ }
                    catch (IOException) { /* Skip inaccessible */ }
            }, ct);

            return emptyFolders;
        }

        public async Task<CleanupSuggestions> GetCleanupSuggestionsAsync(string? path = null,
            CancellationToken ct = default)
        {
            var suggestions = new CleanupSuggestions();
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);

            await Task.Run(() =>
            {
                // Temp files
                AddCleanupItems(suggestions.TempFiles, Path.GetTempPath(), "*", "Temporary files", CleanupRisk.Safe);
                AddCleanupItems(suggestions.TempFiles, Path.Combine(windows, "Temp"), "*", "Windows Temp", CleanupRisk.Safe);

                // Browser caches
                var chromeCachePath = Path.Combine(localAppData, "Google", "Chrome", "User Data", "Default", "Cache");
                AddCleanupItems(suggestions.BrowserCache, chromeCachePath, "*", "Chrome Cache", CleanupRisk.Safe);

                var edgeCachePath = Path.Combine(localAppData, "Microsoft", "Edge", "User Data", "Default", "Cache");
                AddCleanupItems(suggestions.BrowserCache, edgeCachePath, "*", "Edge Cache", CleanupRisk.Safe);

                // Windows Update
                var softwareDistribution = Path.Combine(windows, "SoftwareDistribution", "Download");
                AddCleanupItems(suggestions.WindowsUpdate, softwareDistribution, "*", "Windows Update Cache", CleanupRisk.Low);

                // Thumbnails
                var thumbnailCache = Path.Combine(localAppData, "Microsoft", "Windows", "Explorer");
                AddCleanupItems(suggestions.Thumbnails, thumbnailCache, "thumbcache*.db", "Thumbnail Cache", CleanupRisk.Safe);

                // Old downloads (files older than 30 days)
                var downloads = Path.Combine(userProfile, "Downloads");
                if (Directory.Exists(downloads))
                {
                    try
                    {
                        foreach (var file in Directory.EnumerateFiles(downloads))
                        {
                            var fi = new FileInfo(file);
                            if ((DateTime.Now - fi.LastWriteTime).TotalDays > 30)
                            {
                                suggestions.OldDownloads.Add(new CleanupItem
                                {
                                    Path = file,
                                    Description = $"Downloaded > 30 days ago",
                                    Size = fi.Length,
                                    Risk = CleanupRisk.Medium
                                });
                            }
                        }
                    }
                    catch (UnauthorizedAccessException) { /* Skip inaccessible */ }
                    catch (IOException) { /* Skip inaccessible */ }
                }
            }, ct);

            return suggestions;
        }

        private void AddCleanupItems(List<CleanupItem> items, string path, string pattern, 
            string description, CleanupRisk risk)
        {
            if (!Directory.Exists(path)) return;

            try
            {
                foreach (var file in Directory.EnumerateFiles(path, pattern, SearchOption.AllDirectories))
                {
                    try
                    {
                        var fi = new FileInfo(file);
                        items.Add(new CleanupItem
                        {
                            Path = file,
                            Description = description,
                            Size = fi.Length,
                            Risk = risk
                        });
                    }
                    catch (UnauthorizedAccessException) { /* Skip inaccessible */ }
                    catch (IOException) { /* Skip inaccessible */ }
                }
            }
            catch (UnauthorizedAccessException) { /* Skip inaccessible */ }
                    catch (IOException) { /* Skip inaccessible */ }
        }
    }
}
