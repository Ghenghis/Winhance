using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.FileManager.Interfaces;
using Winhance.Core.Features.FileManager.Models;

namespace Winhance.Infrastructure.Features.FileManager.Services
{
    /// <summary>
    /// High-level search service that combines Nexus indexed search with filesystem fallback.
    /// Provides comprehensive search capabilities including filtering, sorting, and streaming results.
    /// </summary>
    public class SearchService : ISearchService
    {
        private readonly INexusIndexerService _nexusIndexer;
        private readonly ILogService _logService;
        private readonly ConcurrentQueue<string> _recentSearches = new();
        private readonly ConcurrentDictionary<string, DriveIndexStatus> _driveStatuses = new();
        private CancellationTokenSource? _indexingCts;
        private bool _isIndexing;
        private const int MaxRecentSearches = 50;

        public SearchService(INexusIndexerService nexusIndexer, ILogService logService)
        {
            _nexusIndexer = nexusIndexer ?? throw new ArgumentNullException(nameof(nexusIndexer));
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        }

        /// <inheritdoc/>
        public async Task<IEnumerable<SearchResult>> SearchAsync(
            string query,
            SearchOptions options,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return Enumerable.Empty<SearchResult>();
            }

            var results = new List<SearchResult>();
            var sw = Stopwatch.StartNew();

            try
            {
                // Add to recent searches
                AddToRecentSearches(query);

                // Try indexed search first if available
                if (_nexusIndexer.IsAvailable)
                {
                    var indexedResults = await SearchIndexedAsync(query, options, ct);
                    results.AddRange(indexedResults);
                }
                else
                {
                    // Fall back to filesystem search
                    var fsResults = await SearchFileSystemAsync(query, options, ct);
                    results.AddRange(fsResults);
                }

                // Apply post-filters and sorting
                results = ApplyFiltersAndSort(results, options);

                // Limit results
                if (results.Count > options.MaxResults)
                {
                    results = results.Take(options.MaxResults).ToList();
                }

                sw.Stop();
                _logService.Log(LogLevel.Debug, $"Search '{query}' returned {results.Count} results in {sw.ElapsedMilliseconds}ms");
            }
            catch (OperationCanceledException)
            {
                _logService.Log(LogLevel.Debug, $"Search '{query}' was cancelled");
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Search error: {ex.Message}");
            }

            return results;
        }

        /// <inheritdoc/>
        public async Task<bool> IndexDriveAsync(
            string driveLetter,
            IProgress<IndexProgress>? progress = null,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(driveLetter))
            {
                return false;
            }

            // Normalize drive letter
            driveLetter = driveLetter.TrimEnd(':', '\\').ToUpperInvariant();
            var drivePath = $"{driveLetter}:\\";

            if (!Directory.Exists(drivePath))
            {
                _logService.Log(LogLevel.Warning, $"Drive {drivePath} does not exist");
                return false;
            }

            _indexingCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            _isIndexing = true;

            try
            {
                progress?.Report(new IndexProgress
                {
                    CurrentPath = drivePath,
                    Phase = "Initializing",
                    PercentComplete = 0,
                });

                if (_nexusIndexer.IsAvailable)
                {
                    // Use native MFT-based indexing
                    progress?.Report(new IndexProgress
                    {
                        CurrentPath = drivePath,
                        Phase = "MFT Reading",
                        PercentComplete = 10,
                    });

                    var count = await _nexusIndexer.IndexDirectoryAsync(drivePath, _indexingCts.Token);

                    _driveStatuses[driveLetter] = new DriveIndexStatus
                    {
                        DriveLetter = driveLetter,
                        IsIndexed = count >= 0,
                        LastIndexed = DateTime.UtcNow,
                        FilesIndexed = count >= 0 ? count : 0,
                        ErrorMessage = count < 0 ? _nexusIndexer.GetLastError() : null,
                    };

                    progress?.Report(new IndexProgress
                    {
                        CurrentPath = drivePath,
                        Phase = "Complete",
                        PercentComplete = 100,
                        FilesIndexed = count >= 0 ? count : 0,
                    });

                    return count >= 0;
                }
                else
                {
                    // Fallback: simple directory enumeration
                    long filesIndexed = 0;
                    var startTime = DateTime.UtcNow;

                    await Task.Run(
                        () =>
                    {
                        try
                        {
                            foreach (var file in Directory.EnumerateFiles(drivePath, "*", new EnumerationOptions
                            {
                                RecurseSubdirectories = true,
                                IgnoreInaccessible = true,
                                AttributesToSkip = FileAttributes.System,
                            }))
                            {
                                if (_indexingCts.Token.IsCancellationRequested)
                                {
                                    break;
                                }

                                filesIndexed++;

                                if (filesIndexed % 10000 == 0)
                                {
                                    var elapsed = (DateTime.UtcNow - startTime).TotalSeconds;
                                    progress?.Report(new IndexProgress
                                    {
                                        CurrentPath = file,
                                        FilesIndexed = filesIndexed,
                                        Phase = "Scanning",
                                        FilesPerSecond = filesIndexed / Math.Max(1, elapsed),
                                    });
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logService.Log(LogLevel.Warning, $"Indexing error: {ex.Message}");
                        }
                    }, _indexingCts.Token);

                    _driveStatuses[driveLetter] = new DriveIndexStatus
                    {
                        DriveLetter = driveLetter,
                        IsIndexed = true,
                        LastIndexed = DateTime.UtcNow,
                        FilesIndexed = filesIndexed,
                    };

                    progress?.Report(new IndexProgress
                    {
                        CurrentPath = drivePath,
                        Phase = "Complete",
                        PercentComplete = 100,
                        FilesIndexed = filesIndexed,
                    });

                    return true;
                }
            }
            catch (OperationCanceledException)
            {
                _logService.Log(LogLevel.Info, $"Indexing of {drivePath} was cancelled");
                return false;
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Failed to index {drivePath}: {ex.Message}");
                _driveStatuses[driveLetter] = new DriveIndexStatus
                {
                    DriveLetter = driveLetter,
                    IsIndexed = false,
                    ErrorMessage = ex.Message,
                };
                return false;
            }
            finally
            {
                _isIndexing = false;
                _indexingCts?.Dispose();
                _indexingCts = null;
            }
        }

        /// <inheritdoc/>
        public Task<IndexStatus> GetIndexStatusAsync(CancellationToken ct = default)
        {
            var stats = _nexusIndexer.Stats;

            var status = new IndexStatus
            {
                IsIndexing = _isIndexing,
                LastIndexed = stats.LastIndexTime != default ? stats.LastIndexTime : null,
                TotalFilesIndexed = stats.TotalFiles,
                DriveStatuses = _driveStatuses.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            };

            return Task.FromResult(status);
        }

        /// <inheritdoc/>
        public Task CancelIndexingAsync()
        {
            _indexingCts?.Cancel();
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public async Task<int> SearchStreamingAsync(
            string query,
            SearchOptions options,
            Action<SearchResult> onResult,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(query) || onResult == null)
            {
                return 0;
            }

            int count = 0;

            try
            {
                AddToRecentSearches(query);

                if (_nexusIndexer.IsAvailable)
                {
                    var results = await _nexusIndexer.SearchAsync(query, options.MaxResults, ct);
                    foreach (var entry in results)
                    {
                        if (ct.IsCancellationRequested)
                        {
                            break;
                        }

                        var result = ConvertNexusEntry(entry);
                        if (MatchesFilters(result, options))
                        {
                            onResult(result);
                            count++;
                        }
                    }
                }
                else
                {
                    // Filesystem streaming search
                    var searchPaths = options.SearchPaths ?? new[] { Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) };
                    var pattern = options.UseRegex ? query : $"*{query}*";

                    foreach (var searchPath in searchPaths)
                    {
                        if (!Directory.Exists(searchPath))
                        {
                            continue;
                        }

                        await Task.Run(
                            () =>
                        {
                            try
                            {
                                foreach (var file in Directory.EnumerateFileSystemEntries(searchPath, pattern, new EnumerationOptions
                                {
                                    RecurseSubdirectories = true,
                                    IgnoreInaccessible = true,
                                    MatchCasing = options.CaseSensitive ? MatchCasing.CaseSensitive : MatchCasing.CaseInsensitive,
                                }))
                                {
                                    if (ct.IsCancellationRequested || count >= options.MaxResults)
                                    {
                                        break;
                                    }

                                    var result = CreateSearchResult(file, query);
                                    if (MatchesFilters(result, options))
                                    {
                                        onResult(result);
                                        count++;
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                _logService.Log(LogLevel.Warning, $"Search error in {searchPath}: {ex.Message}");
                            }
                        }, ct);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logService.Log(LogLevel.Debug, $"Streaming search '{query}' was cancelled");
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Streaming search error: {ex.Message}");
            }

            return count;
        }

        /// <inheritdoc/>
        public async Task<IEnumerable<string>> GetSuggestionsAsync(
            string partialQuery,
            int maxSuggestions = 10,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(partialQuery))
            {
                return Enumerable.Empty<string>();
            }

            var suggestions = new List<string>();

            try
            {
                // First, check recent searches for matches
                var recentMatches = _recentSearches
                    .Where(s => s.StartsWith(partialQuery, StringComparison.OrdinalIgnoreCase))
                    .Take(maxSuggestions / 2);
                suggestions.AddRange(recentMatches);

                // Then, do a quick indexed search for filename suggestions
                if (_nexusIndexer.IsAvailable && suggestions.Count < maxSuggestions)
                {
                    var searchResults = await _nexusIndexer.SearchAsync(partialQuery, maxSuggestions - suggestions.Count, ct);
                    suggestions.AddRange(searchResults.Select(r => r.Name).Distinct());
                }

                return suggestions.Distinct().Take(maxSuggestions);
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Warning, $"Failed to get suggestions: {ex.Message}");
                return suggestions;
            }
        }

        /// <inheritdoc/>
        public IEnumerable<string> GetRecentSearches(int count = 10)
        {
            return _recentSearches.Take(Math.Min(count, _recentSearches.Count));
        }

        /// <inheritdoc/>
        public void ClearSearchHistory()
        {
            while (_recentSearches.TryDequeue(out _))
            {
            }
        }

        private async Task<List<SearchResult>> SearchIndexedAsync(
            string query,
            SearchOptions options,
            CancellationToken ct)
        {
            var results = new List<SearchResult>();

            var nexusResults = await _nexusIndexer.SearchAsync(query, options.MaxResults, ct);

            foreach (var entry in nexusResults)
            {
                var result = ConvertNexusEntry(entry);
                result.Score = CalculateRelevanceScore(result.Name, query);
                result.MatchHighlight = CreateHighlight(result.Name, query);
                results.Add(result);
            }

            return results;
        }

        private async Task<List<SearchResult>> SearchFileSystemAsync(
            string query,
            SearchOptions options,
            CancellationToken ct)
        {
            var results = new ConcurrentBag<SearchResult>();
            var searchPaths = options.SearchPaths ?? GetDefaultSearchPaths();

            var tasks = searchPaths.Select(path => Task.Run(() =>
            {
                if (!Directory.Exists(path))
                {
                    return;
                }

                try
                {
                    var pattern = options.UseRegex ? "*" : $"*{query}*";
                    Regex? regex = null;

                    if (options.UseRegex)
                    {
                        try
                        {
                            var regexOptions = options.CaseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase;
                            regex = new Regex(query, regexOptions);
                        }
                        catch
                        {
                            return;
                        }
                    }

                    foreach (var entry in Directory.EnumerateFileSystemEntries(path, pattern, new EnumerationOptions
                    {
                        RecurseSubdirectories = true,
                        IgnoreInaccessible = true,
                        MatchCasing = options.CaseSensitive ? MatchCasing.CaseSensitive : MatchCasing.CaseInsensitive,
                    }))
                    {
                        if (ct.IsCancellationRequested || results.Count >= options.MaxResults)
                        {
                            break;
                        }

                        var name = Path.GetFileName(entry);

                        // Apply regex filter if using regex
                        if (regex != null && !regex.IsMatch(name))
                        {
                            continue;
                        }

                        var result = CreateSearchResult(entry, query);
                        results.Add(result);
                    }
                }
                catch (Exception ex)
                {
                    _logService.Log(LogLevel.Warning, $"Search error in {path}: {ex.Message}");
                }
            }, ct));

            await Task.WhenAll(tasks);
            return results.ToList();
        }

        private static string[] GetDefaultSearchPaths()
        {
            var paths = new List<string>
            {
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            };

            // Add available fixed drives
            foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady && d.DriveType == System.IO.DriveType.Fixed))
            {
                if (!paths.Contains(drive.RootDirectory.FullName, StringComparer.OrdinalIgnoreCase))
                {
                    paths.Add(drive.RootDirectory.FullName);
                }
            }

            return paths.ToArray();
        }

        private static SearchResult ConvertNexusEntry(NexusFileEntry entry)
        {
            return new SearchResult
            {
                FullPath = entry.Path,
                Name = entry.Name,
                Directory = Path.GetDirectoryName(entry.Path) ?? string.Empty,
                Size = entry.Size,
                DateModified = entry.Modified ?? DateTime.MinValue,
                DateCreated = entry.Created ?? DateTime.MinValue,
                IsDirectory = entry.IsDirectory,
                Extension = entry.Extension ?? string.Empty,
            };
        }

        private static SearchResult CreateSearchResult(string path, string query)
        {
            var isDirectory = Directory.Exists(path);
            var name = Path.GetFileName(path);

            long size = 0;
            DateTime modified = DateTime.MinValue;
            DateTime created = DateTime.MinValue;

            try
            {
                if (isDirectory)
                {
                    var dirInfo = new DirectoryInfo(path);
                    modified = dirInfo.LastWriteTime;
                    created = dirInfo.CreationTime;
                }
                else
                {
                    var fileInfo = new FileInfo(path);
                    size = fileInfo.Length;
                    modified = fileInfo.LastWriteTime;
                    created = fileInfo.CreationTime;
                }
            }
            catch
            {
                // Ignore access errors
            }

            return new SearchResult
            {
                FullPath = path,
                Name = name,
                Directory = Path.GetDirectoryName(path) ?? string.Empty,
                Size = size,
                DateModified = modified,
                DateCreated = created,
                IsDirectory = isDirectory,
                Extension = isDirectory ? string.Empty : Path.GetExtension(path),
                Score = CalculateRelevanceScore(name, query),
                MatchHighlight = CreateHighlight(name, query),
            };
        }

        private static double CalculateRelevanceScore(string name, string query)
        {
            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(query))
            {
                return 0;
            }

            // Exact match = 100
            if (name.Equals(query, StringComparison.OrdinalIgnoreCase))
            {
                return 100;
            }

            // Starts with = 80
            if (name.StartsWith(query, StringComparison.OrdinalIgnoreCase))
            {
                return 80;
            }

            // Contains = 60
            if (name.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                return 60;
            }

            // Partial match based on character overlap
            var lowerName = name.ToLowerInvariant();
            var lowerQuery = query.ToLowerInvariant();
            int matchCount = lowerQuery.Count(c => lowerName.Contains(c));
            return (double)matchCount / lowerQuery.Length * 40;
        }

        private static string CreateHighlight(string name, string query)
        {
            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(query))
            {
                return name;
            }

            var index = name.IndexOf(query, StringComparison.OrdinalIgnoreCase);
            if (index >= 0)
            {
                return $"{name[..index]}**{name.Substring(index, query.Length)}**{name[(index + query.Length)..]}";
            }

            return name;
        }

        private static bool MatchesFilters(SearchResult result, SearchOptions options)
        {
            // File/Directory filter
            if (options.FilesOnly && result.IsDirectory)
            {
                return false;
            }

            if (options.DirectoriesOnly && !result.IsDirectory)
            {
                return false;
            }

            // Size filters
            if (options.MinSize.HasValue && result.Size < options.MinSize.Value)
            {
                return false;
            }

            if (options.MaxSize.HasValue && result.Size > options.MaxSize.Value)
            {
                return false;
            }

            // Date filters
            if (options.ModifiedAfter.HasValue && result.DateModified < options.ModifiedAfter.Value)
            {
                return false;
            }

            if (options.ModifiedBefore.HasValue && result.DateModified > options.ModifiedBefore.Value)
            {
                return false;
            }

            if (options.CreatedAfter.HasValue && result.DateCreated < options.CreatedAfter.Value)
            {
                return false;
            }

            if (options.CreatedBefore.HasValue && result.DateCreated > options.CreatedBefore.Value)
            {
                return false;
            }

            // Extension filters
            if (options.Extensions != null && options.Extensions.Length > 0)
            {
                if (!options.Extensions.Any(ext =>
                    result.Extension.Equals(ext, StringComparison.OrdinalIgnoreCase) ||
                    result.Extension.Equals($".{ext}", StringComparison.OrdinalIgnoreCase)))
                {
                    return false;
                }
            }

            if (options.ExcludeExtensions != null && options.ExcludeExtensions.Length > 0)
            {
                if (options.ExcludeExtensions.Any(ext =>
                    result.Extension.Equals(ext, StringComparison.OrdinalIgnoreCase) ||
                    result.Extension.Equals($".{ext}", StringComparison.OrdinalIgnoreCase)))
                {
                    return false;
                }
            }

            // Path exclusions
            if (options.ExcludePaths != null && options.ExcludePaths.Length > 0)
            {
                if (options.ExcludePaths.Any(p => result.FullPath.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
                {
                    return false;
                }
            }

            return true;
        }

        private static List<SearchResult> ApplyFiltersAndSort(List<SearchResult> results, SearchOptions options)
        {
            // Filter
            var filtered = results.Where(r => MatchesFilters(r, options));

            // Sort
            IOrderedEnumerable<SearchResult> sorted = options.SortBy switch
            {
                SearchSortBy.Name => options.SortDescending
                    ? filtered.OrderByDescending(r => r.Name)
                    : filtered.OrderBy(r => r.Name),
                SearchSortBy.Size => options.SortDescending
                    ? filtered.OrderByDescending(r => r.Size)
                    : filtered.OrderBy(r => r.Size),
                SearchSortBy.DateModified => options.SortDescending
                    ? filtered.OrderByDescending(r => r.DateModified)
                    : filtered.OrderBy(r => r.DateModified),
                SearchSortBy.DateCreated => options.SortDescending
                    ? filtered.OrderByDescending(r => r.DateCreated)
                    : filtered.OrderBy(r => r.DateCreated),
                SearchSortBy.Extension => options.SortDescending
                    ? filtered.OrderByDescending(r => r.Extension)
                    : filtered.OrderBy(r => r.Extension),
                SearchSortBy.Path => options.SortDescending
                    ? filtered.OrderByDescending(r => r.FullPath)
                    : filtered.OrderBy(r => r.FullPath),
                _ => options.SortDescending
                    ? filtered.OrderByDescending(r => r.Score)
                    : filtered.OrderBy(r => r.Score),
            };

            return sorted.ToList();
        }

        private void AddToRecentSearches(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return;
            }

            // Remove if already exists (to move to front)
            var existing = _recentSearches.ToList();
            existing.RemoveAll(s => s.Equals(query, StringComparison.OrdinalIgnoreCase));
            existing.Insert(0, query);

            // Keep only max items
            while (existing.Count > MaxRecentSearches)
            {
                existing.RemoveAt(existing.Count - 1);
            }

            // Rebuild queue
            while (_recentSearches.TryDequeue(out _))
            {
            }

            foreach (var item in existing)
            {
                _recentSearches.Enqueue(item);
            }
        }
    }
}
