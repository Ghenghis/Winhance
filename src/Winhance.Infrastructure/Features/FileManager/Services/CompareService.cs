using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.FileManager.Interfaces;

namespace Winhance.Infrastructure.Features.FileManager.Services
{
    /// <summary>
    /// Service for comparing files and folders.
    /// </summary>
    public class CompareService : ICompareService
    {
        private const int BufferSize = 81920; // 80KB buffer for file comparison

        /// <inheritdoc />
        public async Task<FileCompareResult> CompareFilesAsync(
            string file1,
            string file2,
            FileCompareOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            options ??= new FileCompareOptions();

            var result = new FileCompareResult
            {
                File1 = file1,
                File2 = file2
            };

            if (!File.Exists(file1))
                throw new FileNotFoundException("First file not found", file1);
            if (!File.Exists(file2))
                throw new FileNotFoundException("Second file not found", file2);

            var info1 = new FileInfo(file1);
            var info2 = new FileInfo(file2);

            result.Size1 = info1.Length;
            result.Size2 = info2.Length;
            result.SizesMatch = info1.Length == info2.Length;
            result.ModifiedDate1 = info1.LastWriteTime;
            result.ModifiedDate2 = info2.LastWriteTime;

            if (info1.LastWriteTime > info2.LastWriteTime)
                result.NewerFile = CompareNewer.First;
            else if (info2.LastWriteTime > info1.LastWriteTime)
                result.NewerFile = CompareNewer.Second;
            else
                result.NewerFile = CompareNewer.Same;

            // Size comparison
            if (options.CompareSize && !result.SizesMatch)
            {
                result.Differences.Add($"Size differs: {result.Size1:N0} vs {result.Size2:N0} bytes");
            }

            // Date comparison
            if (options.CompareDates && result.NewerFile != CompareNewer.Same)
            {
                result.Differences.Add($"Date differs: {result.ModifiedDate1} vs {result.ModifiedDate2}");
            }

            // Attribute comparison
            if (options.CompareAttributes)
            {
                if (info1.Attributes != info2.Attributes)
                {
                    result.Differences.Add($"Attributes differ: {info1.Attributes} vs {info2.Attributes}");
                }
            }

            // Content comparison
            if (options.CompareContent)
            {
                if (!result.SizesMatch)
                {
                    result.HashesMatch = false;
                    result.Differences.Add("Content differs (different sizes)");
                }
                else if (options.UseHash)
                {
                    result.Hash1 = await ComputeHashAsync(file1, cancellationToken);
                    result.Hash2 = await ComputeHashAsync(file2, cancellationToken);
                    result.HashesMatch = result.Hash1 == result.Hash2;

                    if (!result.HashesMatch.Value)
                    {
                        result.Differences.Add("Content differs (different hashes)");
                    }
                }
                else if (options.ByteByByte)
                {
                    var bytesMatch = await CompareByteByByteAsync(file1, file2, cancellationToken);
                    result.HashesMatch = bytesMatch;

                    if (!bytesMatch)
                    {
                        result.Differences.Add("Content differs (byte comparison)");
                    }
                }
            }

            result.AreIdentical = result.Differences.Count == 0;
            return result;
        }

        /// <inheritdoc />
        public async Task<FolderCompareResult> CompareFoldersAsync(
            string folder1,
            string folder2,
            FolderCompareOptions? options = null,
            IProgress<CompareProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            options ??= new FolderCompareOptions();

            var result = new FolderCompareResult
            {
                Folder1 = folder1,
                Folder2 = folder2
            };

            if (!Directory.Exists(folder1))
                throw new DirectoryNotFoundException($"First folder not found: {folder1}");
            if (!Directory.Exists(folder2))
                throw new DirectoryNotFoundException($"Second folder not found: {folder2}");

            var searchOption = options.Recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

            // Get files from both folders
            var files1 = GetFilteredFiles(folder1, searchOption, options)
                .Select(f => GetRelativePath(folder1, f))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var files2 = GetFilteredFiles(folder2, searchOption, options)
                .Select(f => GetRelativePath(folder2, f))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var allFiles = files1.Union(files2).ToList();
            var processed = 0;

            foreach (var relativePath in allFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var fullPath1 = Path.Combine(folder1, relativePath);
                var fullPath2 = Path.Combine(folder2, relativePath);

                var exists1 = files1.Contains(relativePath);
                var exists2 = files2.Contains(relativePath);

                if (exists1 && !exists2)
                {
                    result.OnlyInFirst.Add(relativePath);
                }
                else if (!exists1 && exists2)
                {
                    result.OnlyInSecond.Add(relativePath);
                }
                else
                {
                    // Both exist - compare them
                    var diff = await CompareFileDetailsAsync(fullPath1, fullPath2, relativePath, options, cancellationToken);
                    if (diff != null)
                    {
                        result.Different.Add(diff);
                    }
                    else
                    {
                        result.Identical.Add(relativePath);
                    }
                }

                processed++;
                progress?.Report(new CompareProgress
                {
                    CurrentItem = relativePath,
                    TotalItems = allFiles.Count,
                    ProcessedItems = processed
                });
            }

            return result;
        }

        /// <inheritdoc />
        public async Task<TextDiffResult> GetTextDiffAsync(
            string file1,
            string file2,
            TextDiffOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            options ??= new TextDiffOptions();

            var result = new TextDiffResult();

            if (!File.Exists(file1))
                throw new FileNotFoundException("First file not found", file1);
            if (!File.Exists(file2))
                throw new FileNotFoundException("Second file not found", file2);

            var lines1 = await ReadLinesAsync(file1, options, cancellationToken);
            var lines2 = await ReadLinesAsync(file2, options, cancellationToken);

            result.LinesInFirst = lines1.Length;
            result.LinesInSecond = lines2.Length;

            // Use Longest Common Subsequence (LCS) algorithm for diff
            var lcs = ComputeLCS(lines1, lines2, options.IgnoreCase);
            var hunks = GenerateHunks(lines1, lines2, lcs, options.ContextLines);

            result.Hunks = hunks;
            result.AreIdentical = hunks.Count == 0;

            // Count changes
            foreach (var hunk in hunks)
            {
                foreach (var line in hunk.Lines)
                {
                    switch (line.Type)
                    {
                        case DiffLineType.Added:
                            result.LinesAdded++;
                            break;
                        case DiffLineType.Removed:
                            result.LinesRemoved++;
                            break;
                        case DiffLineType.Modified:
                            result.LinesModified++;
                            break;
                    }
                }
            }

            return result;
        }

        /// <inheritdoc />
        public async Task<bool> AreFilesIdenticalAsync(string file1, string file2, CancellationToken cancellationToken = default)
        {
            if (!File.Exists(file1) || !File.Exists(file2))
                return false;

            var info1 = new FileInfo(file1);
            var info2 = new FileInfo(file2);

            // Quick check: different sizes = different files
            if (info1.Length != info2.Length)
                return false;

            // Empty files are identical
            if (info1.Length == 0)
                return true;

            return await CompareByteByByteAsync(file1, file2, cancellationToken);
        }

        /// <inheritdoc />
        public async Task<MetadataCompareResult> CompareMetadataAsync(string file1, string file2)
        {
            var result = new MetadataCompareResult();

            if (!File.Exists(file1))
                throw new FileNotFoundException("First file not found", file1);
            if (!File.Exists(file2))
                throw new FileNotFoundException("Second file not found", file2);

            var info1 = new FileInfo(file1);
            var info2 = new FileInfo(file2);

            result.SizeMatches = info1.Length == info2.Length;
            result.DateMatches = info1.LastWriteTime == info2.LastWriteTime;
            result.AttributesMatch = info1.Attributes == info2.Attributes;

            if (!result.SizeMatches)
            {
                result.Differences["Size"] = new MetadataDifference
                {
                    Property = "Size",
                    Value1 = $"{info1.Length:N0} bytes",
                    Value2 = $"{info2.Length:N0} bytes"
                };
            }

            if (!result.DateMatches)
            {
                result.Differences["LastModified"] = new MetadataDifference
                {
                    Property = "Last Modified",
                    Value1 = info1.LastWriteTime.ToString("g"),
                    Value2 = info2.LastWriteTime.ToString("g")
                };
            }

            if (info1.CreationTime != info2.CreationTime)
            {
                result.Differences["Created"] = new MetadataDifference
                {
                    Property = "Created",
                    Value1 = info1.CreationTime.ToString("g"),
                    Value2 = info2.CreationTime.ToString("g")
                };
            }

            if (!result.AttributesMatch)
            {
                result.Differences["Attributes"] = new MetadataDifference
                {
                    Property = "Attributes",
                    Value1 = info1.Attributes.ToString(),
                    Value2 = info2.Attributes.ToString()
                };
            }

            return result;
        }

        private async Task<string> ComputeHashAsync(string filePath, CancellationToken cancellationToken)
        {
            using var sha256 = SHA256.Create();
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, true);

            var hash = await sha256.ComputeHashAsync(stream, cancellationToken);
            return Convert.ToHexString(hash);
        }

        private async Task<bool> CompareByteByByteAsync(string file1, string file2, CancellationToken cancellationToken)
        {
            using var stream1 = new FileStream(file1, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, true);
            using var stream2 = new FileStream(file2, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, true);

            var buffer1 = new byte[BufferSize];
            var buffer2 = new byte[BufferSize];

            int bytesRead1, bytesRead2;

            do
            {
                cancellationToken.ThrowIfCancellationRequested();

                bytesRead1 = await stream1.ReadAsync(buffer1, cancellationToken);
                bytesRead2 = await stream2.ReadAsync(buffer2, cancellationToken);

                if (bytesRead1 != bytesRead2)
                    return false;

                for (int i = 0; i < bytesRead1; i++)
                {
                    if (buffer1[i] != buffer2[i])
                        return false;
                }
            } while (bytesRead1 > 0);

            return true;
        }

        private IEnumerable<string> GetFilteredFiles(string folder, SearchOption searchOption, FolderCompareOptions options)
        {
            var files = Directory.EnumerateFiles(folder, "*", searchOption);

            // Filter hidden files
            if (!options.IncludeHidden)
            {
                files = files.Where(f =>
                {
                    var info = new FileInfo(f);
                    return !info.Attributes.HasFlag(FileAttributes.Hidden);
                });
            }

            // Include patterns
            if (options.IncludePatterns != null && options.IncludePatterns.Length > 0)
            {
                files = files.Where(f => options.IncludePatterns.Any(p => MatchesPattern(f, p)));
            }

            // Exclude patterns
            if (options.ExcludePatterns != null && options.ExcludePatterns.Length > 0)
            {
                files = files.Where(f => !options.ExcludePatterns.Any(p => MatchesPattern(f, p)));
            }

            return files;
        }

        private bool MatchesPattern(string path, string pattern)
        {
            var fileName = Path.GetFileName(path);
            var regex = "^" + Regex.Escape(pattern).Replace("\\*", ".*").Replace("\\?", ".") + "$";
            return Regex.IsMatch(fileName, regex, RegexOptions.IgnoreCase);
        }

        private string GetRelativePath(string basePath, string fullPath)
        {
            return fullPath.Substring(basePath.Length).TrimStart('\\', '/');
        }

        private async Task<FileDifference?> CompareFileDetailsAsync(
            string file1, string file2, string relativePath,
            FolderCompareOptions options, CancellationToken cancellationToken)
        {
            var info1 = new FileInfo(file1);
            var info2 = new FileInfo(file2);

            var sizeDiff = options.CompareSizes && info1.Length != info2.Length;
            var dateDiff = options.CompareDates && info1.LastWriteTime != info2.LastWriteTime;
            var contentDiff = false;

            if (options.CompareContents && info1.Length == info2.Length)
            {
                contentDiff = !await AreFilesIdenticalAsync(file1, file2, cancellationToken);
            }

            if (!sizeDiff && !dateDiff && !contentDiff)
                return null;

            var diffType = DifferenceType.Content;
            if (sizeDiff && dateDiff)
                diffType = DifferenceType.Multiple;
            else if (sizeDiff)
                diffType = DifferenceType.Size;
            else if (dateDiff && !contentDiff)
                diffType = DifferenceType.Date;

            return new FileDifference
            {
                RelativePath = relativePath,
                Size1 = info1.Length,
                Size2 = info2.Length,
                Date1 = info1.LastWriteTime,
                Date2 = info2.LastWriteTime,
                Type = diffType
            };
        }

        private async Task<string[]> ReadLinesAsync(string filePath, TextDiffOptions options, CancellationToken cancellationToken)
        {
            var lines = await File.ReadAllLinesAsync(filePath, cancellationToken);

            if (options.IgnoreBlankLines)
            {
                lines = lines.Where(l => !string.IsNullOrWhiteSpace(l)).ToArray();
            }

            if (options.IgnoreWhitespace)
            {
                lines = lines.Select(l => l.Trim()).ToArray();
            }

            return lines;
        }

        private int[,] ComputeLCS(string[] lines1, string[] lines2, bool ignoreCase)
        {
            var comparison = ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            var m = lines1.Length;
            var n = lines2.Length;
            var dp = new int[m + 1, n + 1];

            for (int i = 1; i <= m; i++)
            {
                for (int j = 1; j <= n; j++)
                {
                    if (lines1[i - 1].Equals(lines2[j - 1], comparison))
                    {
                        dp[i, j] = dp[i - 1, j - 1] + 1;
                    }
                    else
                    {
                        dp[i, j] = Math.Max(dp[i - 1, j], dp[i, j - 1]);
                    }
                }
            }

            return dp;
        }

        private List<DiffHunk> GenerateHunks(string[] lines1, string[] lines2, int[,] lcs, int contextLines)
        {
            var hunks = new List<DiffHunk>();
            var diffLines = new List<DiffLine>();

            // Backtrack through LCS to find differences
            int i = lines1.Length, j = lines2.Length;
            var changes = new List<(DiffLineType type, int line1, int line2, string content)>();

            while (i > 0 || j > 0)
            {
                if (i > 0 && j > 0 && lines1[i - 1] == lines2[j - 1])
                {
                    changes.Add((DiffLineType.Context, i - 1, j - 1, lines1[i - 1]));
                    i--;
                    j--;
                }
                else if (j > 0 && (i == 0 || lcs[i, j - 1] >= lcs[i - 1, j]))
                {
                    changes.Add((DiffLineType.Added, -1, j - 1, lines2[j - 1]));
                    j--;
                }
                else
                {
                    changes.Add((DiffLineType.Removed, i - 1, -1, lines1[i - 1]));
                    i--;
                }
            }

            changes.Reverse();

            // Group changes into hunks
            DiffHunk? currentHunk = null;
            int lastChangeIndex = -contextLines - 1;

            for (int idx = 0; idx < changes.Count; idx++)
            {
                var (type, line1, line2, content) = changes[idx];

                if (type != DiffLineType.Context)
                {
                    // Start new hunk or extend current one
                    if (currentHunk == null || idx - lastChangeIndex > contextLines * 2)
                    {
                        if (currentHunk != null)
                        {
                            hunks.Add(currentHunk);
                        }

                        currentHunk = new DiffHunk
                        {
                            StartLine1 = line1 >= 0 ? line1 + 1 : (line2 + 1),
                            StartLine2 = line2 >= 0 ? line2 + 1 : (line1 + 1)
                        };

                        // Add leading context
                        for (int c = Math.Max(0, idx - contextLines); c < idx; c++)
                        {
                            var (cType, cLine1, cLine2, cContent) = changes[c];
                            currentHunk.Lines.Add(new DiffLine
                            {
                                Type = DiffLineType.Context,
                                Content = cContent,
                                LineNumber1 = cLine1 >= 0 ? cLine1 + 1 : null,
                                LineNumber2 = cLine2 >= 0 ? cLine2 + 1 : null
                            });
                        }
                    }

                    currentHunk.Lines.Add(new DiffLine
                    {
                        Type = type,
                        Content = content,
                        LineNumber1 = line1 >= 0 ? line1 + 1 : null,
                        LineNumber2 = line2 >= 0 ? line2 + 1 : null
                    });

                    lastChangeIndex = idx;
                }
                else if (currentHunk != null && idx - lastChangeIndex <= contextLines)
                {
                    // Add trailing context
                    currentHunk.Lines.Add(new DiffLine
                    {
                        Type = DiffLineType.Context,
                        Content = content,
                        LineNumber1 = line1 + 1,
                        LineNumber2 = line2 + 1
                    });
                }
            }

            if (currentHunk != null)
            {
                hunks.Add(currentHunk);
            }

            // Set line counts
            foreach (var hunk in hunks)
            {
                hunk.LineCount1 = hunk.Lines.Count(l => l.Type == DiffLineType.Context || l.Type == DiffLineType.Removed);
                hunk.LineCount2 = hunk.Lines.Count(l => l.Type == DiffLineType.Context || l.Type == DiffLineType.Added);
            }

            return hunks;
        }
    }
}
