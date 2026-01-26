using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.FileManager.Interfaces;

namespace Winhance.Infrastructure.Features.FileManager.Services
{
    /// <summary>
    /// Robust implementation of batch rename operations with preview and rollback support.
    /// </summary>
    public class BatchRenameService : IBatchRenameService
    {
        private readonly ILogService _logService;
        private readonly string _transactionLogPath;
        private readonly string _presetsPath;
        private readonly List<RenamePreset> _builtInPresets;

        public BatchRenameService(ILogService logService)
        {
            _logService = logService;

            var appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Winhance", "FileManager");

            _transactionLogPath = Path.Combine(appDataPath, "rename-transactions");
            _presetsPath = Path.Combine(appDataPath, "rename-presets");

            Directory.CreateDirectory(_transactionLogPath);
            Directory.CreateDirectory(_presetsPath);

            _builtInPresets = InitializeBuiltInPresets();
        }

        public async Task<IEnumerable<RenamePreview>> PreviewRenameAsync(
            IEnumerable<string> files,
            IEnumerable<RenameRule> rules,
            CancellationToken cancellationToken = default)
        {
            var previews = new List<RenamePreview>();
            var rulesList = rules.Where(r => r.Enabled).OrderBy(r => r.Order).ToList();
            var counter = rulesList.FirstOrDefault(r => r.Type == RenameRuleType.Counter)?.CounterStart ?? 1;
            var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            await Task.Run(
                () =>
            {
                foreach (var filePath in files)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        var originalName = Path.GetFileName(filePath);
                        var directory = Path.GetDirectoryName(filePath) ?? string.Empty;
                        var nameWithoutExt = Path.GetFileNameWithoutExtension(filePath);
                        var extension = Path.GetExtension(filePath);

                        var newName = nameWithoutExt;

                        // Apply each rule in order
                        foreach (var rule in rulesList)
                        {
                            newName = ApplyRule(newName, extension, rule, ref counter);
                        }

                        // Restore extension (unless changed by a rule)
                        var extRule = rulesList.FirstOrDefault(r => r.Type == RenameRuleType.ChangeExtension);
                        if (extRule != null && !string.IsNullOrEmpty(extRule.NewExtension))
                        {
                            extension = extRule.NewExtension.StartsWith(".", StringComparison.Ordinal)
                                ? extRule.NewExtension
                                : "." + extRule.NewExtension;
                        }

                        var finalNewName = newName + extension;
                        var newPath = Path.Combine(directory, finalNewName);

                        // Check for conflicts
                        var hasConflict = false;
                        var conflictReason = string.Empty;

                        if (usedNames.Contains(finalNewName))
                        {
                            hasConflict = true;
                            conflictReason = "Duplicate name in batch";
                        }
                        else if (File.Exists(newPath) && !newPath.Equals(filePath, StringComparison.OrdinalIgnoreCase))
                        {
                            hasConflict = true;
                            conflictReason = "File already exists";
                        }
                        else if (string.IsNullOrWhiteSpace(newName))
                        {
                            hasConflict = true;
                            conflictReason = "Name cannot be empty";
                        }
                        else if (HasInvalidCharacters(finalNewName))
                        {
                            hasConflict = true;
                            conflictReason = "Invalid characters in name";
                        }

                        usedNames.Add(finalNewName);

                        previews.Add(new RenamePreview
                        {
                            OriginalPath = filePath,
                            OriginalName = originalName,
                            NewName = finalNewName,
                            NewPath = newPath,
                            HasConflict = hasConflict,
                            ConflictReason = conflictReason,
                        });
                    }
                    catch (Exception ex)
                    {
                        previews.Add(new RenamePreview
                        {
                            OriginalPath = filePath,
                            OriginalName = Path.GetFileName(filePath),
                            NewName = Path.GetFileName(filePath),
                            HasConflict = true,
                            ConflictReason = $"Error: {ex.Message}",
                        });
                    }
                }
            }, cancellationToken);

            return previews;
        }

        public async Task<RenameResult> ExecuteRenameAsync(
            IEnumerable<string> files,
            IEnumerable<RenameRule> rules,
            CancellationToken cancellationToken = default)
        {
            var result = new RenameResult { Success = true };
            var errors = new List<RenameError>();
            var transactionId = Guid.NewGuid().ToString();
            var transactionLog = new List<RenameTransaction>();

            try
            {
                // Generate previews first
                var previews = await PreviewRenameAsync(files, rules, cancellationToken);
                var previewList = previews.ToList();

                // Check for conflicts
                if (previewList.Any(p => p.HasConflict))
                {
                    result.Success = false;
                    result.FilesSkipped = previewList.Count(p => p.HasConflict);
                    errors.AddRange(previewList.Where(p => p.HasConflict).Select(p => new RenameError
                    {
                        FilePath = p.OriginalPath,
                        ErrorMessage = p.ConflictReason,
                    }));
                }

                // Execute renames for non-conflicting files
                foreach (var preview in previewList.Where(p => !p.HasConflict && p.WillChange))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        await Task.Run(() => File.Move(preview.OriginalPath, preview.NewPath), cancellationToken);

                        transactionLog.Add(new RenameTransaction
                        {
                            OriginalPath = preview.OriginalPath,
                            NewPath = preview.NewPath,
                            Timestamp = DateTime.UtcNow,
                        });

                        result.FilesRenamed++;
                        _logService.Log(LogLevel.Info, $"Renamed: {preview.OriginalName} -> {preview.NewName}");
                    }
                    catch (Exception ex)
                    {
                        errors.Add(new RenameError
                        {
                            FilePath = preview.OriginalPath,
                            ErrorMessage = ex.Message,
                        });
                        result.FilesFailed++;
                        _logService.Log(LogLevel.Error, $"Failed to rename {preview.OriginalName}: {ex.Message}");
                    }
                }

                // Save transaction log for undo
                await SaveTransactionAsync(transactionId, transactionLog);
                result.TransactionId = transactionId;
            }
            catch (OperationCanceledException)
            {
                result.Success = false;
                throw;
            }
            catch (Exception ex)
            {
                result.Success = false;
                _logService.Log(LogLevel.Error, $"Rename operation failed: {ex.Message}");
            }

            result.Errors = errors;
            result.Success = result.FilesFailed == 0 && result.FilesSkipped == 0;

            return result;
        }

        public async Task<RenameResult> UndoRenameAsync(
            string transactionId,
            CancellationToken cancellationToken = default)
        {
            var result = new RenameResult { Success = true };
            var errors = new List<RenameError>();

            try
            {
                var transactionPath = Path.Combine(_transactionLogPath, $"{transactionId}.json");

                if (!File.Exists(transactionPath))
                {
                    result.Success = false;
                    errors.Add(new RenameError { ErrorMessage = "Transaction not found" });
                    result.Errors = errors;
                    return result;
                }

                var json = await File.ReadAllTextAsync(transactionPath, cancellationToken);
                var transactions = JsonSerializer.Deserialize<List<RenameTransaction>>(json) ?? new List<RenameTransaction>();

                // Undo in reverse order
                foreach (var transaction in transactions.AsEnumerable().Reverse())
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        if (File.Exists(transaction.NewPath))
                        {
                            await Task.Run(() => File.Move(transaction.NewPath, transaction.OriginalPath), cancellationToken);
                            result.FilesRenamed++;
                            _logService.Log(LogLevel.Info, $"Undone: {transaction.NewPath} -> {transaction.OriginalPath}");
                        }
                        else
                        {
                            result.FilesSkipped++;
                        }
                    }
                    catch (Exception ex)
                    {
                        errors.Add(new RenameError
                        {
                            FilePath = transaction.NewPath,
                            ErrorMessage = ex.Message,
                        });
                        result.FilesFailed++;
                    }
                }

                // Delete transaction log after successful undo
                if (result.FilesFailed == 0)
                {
                    File.Delete(transactionPath);
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                _logService.Log(LogLevel.Error, $"Undo operation failed: {ex.Message}");
            }

            result.Errors = errors;
            result.Success = result.FilesFailed == 0;

            return result;
        }

        public async Task<IEnumerable<RenamePreset>> GetPresetsAsync(CancellationToken cancellationToken = default)
        {
            var presets = new List<RenamePreset>(_builtInPresets);

            await Task.Run(
                () =>
            {
                try
                {
                    foreach (var file in Directory.GetFiles(_presetsPath, "*.json"))
                    {
                        try
                        {
                            var json = File.ReadAllText(file);
                            var preset = JsonSerializer.Deserialize<RenamePreset>(json);
                            if (preset != null)
                            {
                                preset.IsBuiltIn = false;
                                presets.Add(preset);
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Failed to load preset: {ex.Message}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to enumerate preset files: {ex.Message}");
                }
            }, cancellationToken);

            return presets;
        }

        public async Task SavePresetAsync(RenamePreset preset, CancellationToken cancellationToken = default)
        {
            var fileName = SanitizeFileName(preset.Name) + ".json";
            var filePath = Path.Combine(_presetsPath, fileName);

            preset.CreatedDate = DateTime.UtcNow;
            preset.IsBuiltIn = false;

            var json = JsonSerializer.Serialize(preset, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(filePath, json, cancellationToken);

            _logService.Log(LogLevel.Info, $"Saved preset: {preset.Name}");
        }

        public async Task DeletePresetAsync(string presetName, CancellationToken cancellationToken = default)
        {
            var fileName = SanitizeFileName(presetName) + ".json";
            var filePath = Path.Combine(_presetsPath, fileName);

            if (File.Exists(filePath))
            {
                await Task.Run(() => File.Delete(filePath), cancellationToken);
                _logService.Log(LogLevel.Info, $"Deleted preset: {presetName}");
            }
        }

        private string ApplyRule(string name, string extension, RenameRule rule, ref int counter)
        {
            return rule.Type switch
            {
                RenameRuleType.FindReplace => ApplyFindReplace(name, rule),
                RenameRuleType.AddText => ApplyAddText(name, rule),
                RenameRuleType.RemoveText => ApplyRemoveText(name, rule),
                RenameRuleType.Counter => ApplyCounter(name, rule, ref counter),
                RenameRuleType.ChangeCase => ApplyChangeCase(name, rule),
                RenameRuleType.DateTime => ApplyDateTime(name, rule),
                RenameRuleType.RegularExpression => ApplyRegex(name, rule),
                _ => name,
            };
        }

        private string ApplyFindReplace(string name, RenameRule rule)
        {
            if (string.IsNullOrEmpty(rule.FindText))
            {
                return name;
            }

            if (rule.UseRegex)
            {
                try
                {
                    var options = rule.CaseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase;
                    return Regex.Replace(name, rule.FindText, rule.ReplaceText ?? string.Empty, options);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Regex find/replace failed: {ex.Message}");
                    return name;
                }
            }

            var comparison = rule.CaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            return name.Replace(rule.FindText, rule.ReplaceText ?? string.Empty, comparison);
        }

        private string ApplyAddText(string name, RenameRule rule)
        {
            if (string.IsNullOrEmpty(rule.TextToAdd))
            {
                return name;
            }

            return rule.Position switch
            {
                TextPosition.Prefix => rule.TextToAdd + name,
                TextPosition.Suffix => name + rule.TextToAdd,
                TextPosition.AtIndex => name.Insert(Math.Min(rule.PositionIndex, name.Length), rule.TextToAdd),
                TextPosition.BeforeExtension => name + rule.TextToAdd,
                _ => name,
            };
        }

        private string ApplyRemoveText(string name, RenameRule rule)
        {
            if (!string.IsNullOrEmpty(rule.RemovePattern))
            {
                try
                {
                    return Regex.Replace(name, rule.RemovePattern, string.Empty);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Regex remove failed: {ex.Message}");
                    return name;
                }
            }

            if (rule.RemoveFromIndex >= 0 && rule.RemoveCount > 0 && rule.RemoveFromIndex < name.Length)
            {
                var count = Math.Min(rule.RemoveCount, name.Length - rule.RemoveFromIndex);
                return name.Remove(rule.RemoveFromIndex, count);
            }

            return name;
        }

        private string ApplyCounter(string name, RenameRule rule, ref int counter)
        {
            var counterStr = counter.ToString().PadLeft(rule.CounterPadding, '0');
            var counterText = $"{rule.CounterPrefix}{counterStr}{rule.CounterSuffix}";
            counter += rule.CounterStep;

            return rule.Position switch
            {
                TextPosition.Prefix => counterText + name,
                TextPosition.Suffix => name + counterText,
                TextPosition.BeforeExtension => name + counterText,
                _ => name + counterText,
            };
        }

        private string ApplyChangeCase(string name, RenameRule rule)
        {
            return rule.CaseType switch
            {
                CaseType.LowerCase => name.ToLowerInvariant(),
                CaseType.UpperCase => name.ToUpperInvariant(),
                CaseType.TitleCase => CultureInfo.CurrentCulture.TextInfo.ToTitleCase(name.ToLower()),
                CaseType.SentenceCase => char.ToUpper(name[0]) + name.Substring(1).ToLower(),
                CaseType.ToggleCase => new string(name.Select(c => char.IsUpper(c) ? char.ToLower(c) : char.ToUpper(c)).ToArray()),
                _ => name,
            };
        }

        private string ApplyDateTime(string name, RenameRule rule)
        {
            var dateTime = rule.DateSource switch
            {
                DateSource.CurrentDate => DateTime.Now,
                _ => DateTime.Now,
            };

            try
            {
                var dateStr = dateTime.ToString(rule.DateFormat);
                return rule.Position switch
                {
                    TextPosition.Prefix => dateStr + "_" + name,
                    TextPosition.Suffix => name + "_" + dateStr,
                    _ => name + "_" + dateStr,
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DateTime format failed: {ex.Message}");
                return name;
            }
        }

        private string ApplyRegex(string name, RenameRule rule)
        {
            if (string.IsNullOrEmpty(rule.FindText))
            {
                return name;
            }

            try
            {
                var options = rule.CaseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase;
                return Regex.Replace(name, rule.FindText, rule.ReplaceText ?? string.Empty, options);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Regex apply failed: {ex.Message}");
                return name;
            }
        }

        private bool HasInvalidCharacters(string name)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            return name.Any(c => invalidChars.Contains(c));
        }

        private string SanitizeFileName(string name)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            return new string(name.Where(c => !invalidChars.Contains(c)).ToArray());
        }

        private async Task SaveTransactionAsync(string transactionId, List<RenameTransaction> transactions)
        {
            var filePath = Path.Combine(_transactionLogPath, $"{transactionId}.json");
            var json = JsonSerializer.Serialize(transactions, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(filePath, json);
        }

        private List<RenamePreset> InitializeBuiltInPresets()
        {
            return new List<RenamePreset>
            {
                new RenamePreset
                {
                    Name = "Photo Date Prefix",
                    Description = "Add date prefix to photos (YYYY-MM-DD_)",
                    IsBuiltIn = true,
                    Rules = new[]
                    {
                        new RenameRule
                        {
                            Type = RenameRuleType.DateTime,
                            Order = 1,
                            Enabled = true,
                            DateFormat = "yyyy-MM-dd",
                            Position = TextPosition.Prefix,
                        },
                    },
                },
                new RenamePreset
                {
                    Name = "Lowercase All",
                    Description = "Convert all filenames to lowercase",
                    IsBuiltIn = true,
                    Rules = new[]
                    {
                        new RenameRule
                        {
                            Type = RenameRuleType.ChangeCase,
                            Order = 1,
                            Enabled = true,
                            CaseType = CaseType.LowerCase,
                        },
                    },
                },
                new RenamePreset
                {
                    Name = "Remove Spaces",
                    Description = "Replace spaces with underscores",
                    IsBuiltIn = true,
                    Rules = new[]
                    {
                        new RenameRule
                        {
                            Type = RenameRuleType.FindReplace,
                            Order = 1,
                            Enabled = true,
                            FindText = " ",
                            ReplaceText = "_",
                        },
                    },
                },
                new RenamePreset
                {
                    Name = "Sequential Numbering",
                    Description = "Add sequential numbers (001, 002, ...)",
                    IsBuiltIn = true,
                    Rules = new[]
                    {
                        new RenameRule
                        {
                            Type = RenameRuleType.Counter,
                            Order = 1,
                            Enabled = true,
                            CounterStart = 1,
                            CounterStep = 1,
                            CounterPadding = 3,
                            CounterSuffix = "_",
                            Position = TextPosition.Prefix,
                        },
                    },
                },
            };
        }

        private class RenameTransaction
        {
            public string OriginalPath { get; set; } = string.Empty;

            public string NewPath { get; set; } = string.Empty;

            public DateTime Timestamp { get; set; }
        }
    }
}
