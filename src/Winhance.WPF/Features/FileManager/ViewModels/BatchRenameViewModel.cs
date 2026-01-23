using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Winhance.Core.Features.FileManager.Interfaces;

namespace Winhance.WPF.Features.FileManager.ViewModels
{
    /// <summary>
    /// ViewModel for the batch rename feature.
    /// </summary>
    public partial class BatchRenameViewModel : ObservableObject
    {
        private readonly IBatchRenameService? _batchRenameService;

        [ObservableProperty]
        private string _sourcePath = string.Empty;

        [ObservableProperty]
        private string _filterPattern = "*.*";

        [ObservableProperty]
        private bool _includeSubfolders;

        [ObservableProperty]
        private ObservableCollection<string> _selectedFiles = new();

        [ObservableProperty]
        private ObservableCollection<RenameRuleViewModel> _rules = new();

        [ObservableProperty]
        private ObservableCollection<RenamePreviewItemViewModel> _previewItems = new();

        [ObservableProperty]
        private ObservableCollection<RenamePresetViewModel> _presets = new();

        [ObservableProperty]
        private RenamePresetViewModel? _selectedPreset;

        [ObservableProperty]
        private int _totalFiles;

        [ObservableProperty]
        private int _conflictCount;

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        [ObservableProperty]
        private string? _lastTransactionId;

        [ObservableProperty]
        private bool _canUndo;

        public BatchRenameViewModel()
        {
            // Design-time constructor
            LoadDesignTimeData();
        }

        public BatchRenameViewModel(IBatchRenameService? batchRenameService)
        {
            _batchRenameService = batchRenameService;
            _ = LoadPresetsAsync();
        }

        private void LoadDesignTimeData()
        {
            PreviewItems.Add(new RenamePreviewItemViewModel 
            { 
                OriginalName = "IMG_20260115_001.jpg", 
                NewName = "Vacation_001.jpg",
                HasConflict = false
            });
            PreviewItems.Add(new RenamePreviewItemViewModel 
            { 
                OriginalName = "IMG_20260115_002.jpg", 
                NewName = "Vacation_002.jpg",
                HasConflict = false
            });

            Rules.Add(new RenameRuleViewModel { RuleType = RenameRuleType.FindReplace, FindText = "IMG_", ReplaceText = "Vacation_" });
        }

        [RelayCommand]
        private async Task LoadPresetsAsync()
        {
            if (_batchRenameService == null) return;

            Presets.Clear();
            var presets = await _batchRenameService.GetPresetsAsync();
            foreach (var preset in presets)
            {
                Presets.Add(new RenamePresetViewModel
                {
                    Name = preset.Name,
                    Description = preset.Description,
                    IsBuiltIn = preset.IsBuiltIn,
                    Rules = preset.Rules.Select(r => new RenameRuleViewModel
                    {
                        RuleType = r.Type,
                        FindText = r.FindText,
                        ReplaceText = r.ReplaceText,
                        UseRegex = r.UseRegex
                    }).ToList()
                });
            }
        }

        [RelayCommand]
        private void AddRule(RenameRuleType ruleType)
        {
            Rules.Add(new RenameRuleViewModel
            {
                RuleType = ruleType,
                Order = Rules.Count + 1,
                Enabled = true
            });
            _ = RefreshPreviewAsync();
        }

        [RelayCommand]
        private void RemoveRule(RenameRuleViewModel rule)
        {
            Rules.Remove(rule);
            ReorderRules();
            _ = RefreshPreviewAsync();
        }

        [RelayCommand]
        private void MoveRuleUp(RenameRuleViewModel rule)
        {
            var index = Rules.IndexOf(rule);
            if (index > 0)
            {
                Rules.Move(index, index - 1);
                ReorderRules();
                _ = RefreshPreviewAsync();
            }
        }

        [RelayCommand]
        private void MoveRuleDown(RenameRuleViewModel rule)
        {
            var index = Rules.IndexOf(rule);
            if (index < Rules.Count - 1)
            {
                Rules.Move(index, index + 1);
                ReorderRules();
                _ = RefreshPreviewAsync();
            }
        }

        private void ReorderRules()
        {
            for (int i = 0; i < Rules.Count; i++)
            {
                Rules[i].Order = i + 1;
            }
        }

        [RelayCommand]
        private void ClearAllRules()
        {
            Rules.Clear();
            PreviewItems.Clear();
            ConflictCount = 0;
        }

        [RelayCommand]
        private void LoadPreset(RenamePresetViewModel preset)
        {
            Rules.Clear();
            foreach (var rule in preset.Rules)
            {
                Rules.Add(new RenameRuleViewModel
                {
                    RuleType = rule.RuleType,
                    Order = Rules.Count + 1,
                    Enabled = rule.Enabled,
                    FindText = rule.FindText,
                    ReplaceText = rule.ReplaceText,
                    UseRegex = rule.UseRegex,
                    TextToAdd = rule.TextToAdd,
                    Position = rule.Position,
                    CaseType = rule.CaseType,
                    CounterStart = rule.CounterStart,
                    CounterStep = rule.CounterStep,
                    CounterPadding = rule.CounterPadding
                });
            }
            _ = RefreshPreviewAsync();
        }

        [RelayCommand]
        public async Task RefreshPreviewAsync()
        {
            if (_batchRenameService == null || SelectedFiles.Count == 0 || Rules.Count == 0)
            {
                return;
            }

            IsLoading = true;
            StatusMessage = "Generating preview...";

            try
            {
                var rules = Rules.Where(r => r.Enabled).Select(r => new RenameRule
                {
                    Type = r.RuleType,
                    Order = r.Order,
                    Enabled = r.Enabled,
                    FindText = r.FindText,
                    ReplaceText = r.ReplaceText,
                    UseRegex = r.UseRegex,
                    TextToAdd = r.TextToAdd,
                    Position = r.Position,
                    CaseType = r.CaseType,
                    CounterStart = r.CounterStart,
                    CounterStep = r.CounterStep,
                    CounterPadding = r.CounterPadding
                }).ToList();

                var previews = await _batchRenameService.PreviewRenameAsync(SelectedFiles, rules);

                PreviewItems.Clear();
                ConflictCount = 0;

                foreach (var preview in previews)
                {
                    var item = new RenamePreviewItemViewModel
                    {
                        OriginalName = preview.OriginalName,
                        OriginalPath = preview.OriginalPath,
                        NewName = preview.NewName,
                        NewPath = preview.NewPath,
                        HasConflict = preview.HasConflict,
                        ConflictReason = preview.ConflictReason,
                        WillChange = preview.WillChange
                    };
                    PreviewItems.Add(item);

                    if (preview.HasConflict)
                        ConflictCount++;
                }

                TotalFiles = PreviewItems.Count;
                StatusMessage = ConflictCount > 0 
                    ? $"{ConflictCount} conflicts detected" 
                    : $"{TotalFiles} files ready to rename";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task ApplyRenameAsync()
        {
            if (_batchRenameService == null || SelectedFiles.Count == 0 || Rules.Count == 0)
            {
                return;
            }

            if (ConflictCount > 0)
            {
                StatusMessage = "Cannot rename: conflicts exist";
                return;
            }

            IsLoading = true;
            StatusMessage = "Renaming files...";

            try
            {
                var rules = Rules.Where(r => r.Enabled).Select(r => new RenameRule
                {
                    Type = r.RuleType,
                    Order = r.Order,
                    Enabled = r.Enabled,
                    FindText = r.FindText,
                    ReplaceText = r.ReplaceText,
                    UseRegex = r.UseRegex,
                    TextToAdd = r.TextToAdd,
                    Position = r.Position,
                    CaseType = r.CaseType,
                    CounterStart = r.CounterStart,
                    CounterStep = r.CounterStep,
                    CounterPadding = r.CounterPadding
                }).ToList();

                var result = await _batchRenameService.ExecuteRenameAsync(SelectedFiles, rules);

                if (result.Success)
                {
                    LastTransactionId = result.TransactionId;
                    CanUndo = true;
                    StatusMessage = $"Successfully renamed {result.FilesRenamed} files";
                    SelectedFiles.Clear();
                    PreviewItems.Clear();
                }
                else
                {
                    StatusMessage = $"Renamed {result.FilesRenamed}, failed {result.FilesFailed}";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task UndoLastBatchAsync()
        {
            if (_batchRenameService == null || string.IsNullOrEmpty(LastTransactionId))
            {
                return;
            }

            IsLoading = true;
            StatusMessage = "Undoing rename...";

            try
            {
                var result = await _batchRenameService.UndoRenameAsync(LastTransactionId);

                if (result.Success)
                {
                    StatusMessage = $"Successfully restored {result.FilesRenamed} files";
                    LastTransactionId = null;
                    CanUndo = false;
                }
                else
                {
                    StatusMessage = $"Undo failed: restored {result.FilesRenamed}, failed {result.FilesFailed}";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private void AddFiles(System.Collections.IList files)
        {
            foreach (string file in files)
            {
                if (!SelectedFiles.Contains(file))
                {
                    SelectedFiles.Add(file);
                }
            }
            TotalFiles = SelectedFiles.Count;
            _ = RefreshPreviewAsync();
        }

        [RelayCommand]
        private void ClearFiles()
        {
            SelectedFiles.Clear();
            PreviewItems.Clear();
            TotalFiles = 0;
            ConflictCount = 0;
        }
    }

    /// <summary>
    /// ViewModel for a rename rule.
    /// </summary>
    public partial class RenameRuleViewModel : ObservableObject
    {
        [ObservableProperty]
        private RenameRuleType _ruleType;

        [ObservableProperty]
        private int _order;

        [ObservableProperty]
        private bool _enabled = true;

        [ObservableProperty]
        private string _findText = string.Empty;

        [ObservableProperty]
        private string _replaceText = string.Empty;

        [ObservableProperty]
        private bool _useRegex;

        [ObservableProperty]
        private bool _caseSensitive;

        [ObservableProperty]
        private string _textToAdd = string.Empty;

        [ObservableProperty]
        private TextPosition _position;

        [ObservableProperty]
        private int _positionIndex;

        [ObservableProperty]
        private CaseType _caseType;

        [ObservableProperty]
        private int _counterStart = 1;

        [ObservableProperty]
        private int _counterStep = 1;

        [ObservableProperty]
        private int _counterPadding = 3;

        [ObservableProperty]
        private string _counterPrefix = string.Empty;

        [ObservableProperty]
        private string _counterSuffix = string.Empty;

        [ObservableProperty]
        private string _newExtension = string.Empty;

        [ObservableProperty]
        private string _dateFormat = "yyyy-MM-dd";

        public string RuleTypeDisplay => RuleType switch
        {
            RenameRuleType.FindReplace => "Find & Replace",
            RenameRuleType.AddText => "Add Text",
            RenameRuleType.RemoveText => "Remove Text",
            RenameRuleType.Counter => "Counter",
            RenameRuleType.ChangeCase => "Change Case",
            RenameRuleType.ChangeExtension => "Change Extension",
            RenameRuleType.DateTime => "Date/Time",
            RenameRuleType.MetadataExtract => "Metadata",
            RenameRuleType.RegularExpression => "Regex",
            _ => "Unknown"
        };
    }

    /// <summary>
    /// ViewModel for a rename preview item.
    /// </summary>
    public partial class RenamePreviewItemViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _originalName = string.Empty;

        [ObservableProperty]
        private string _originalPath = string.Empty;

        [ObservableProperty]
        private string _newName = string.Empty;

        [ObservableProperty]
        private string _newPath = string.Empty;

        [ObservableProperty]
        private bool _hasConflict;

        [ObservableProperty]
        private string _conflictReason = string.Empty;

        [ObservableProperty]
        private bool _willChange;

        public string StatusIcon => HasConflict ? "AlertCircle" : (WillChange ? "Check" : "Minus");
        public string StatusColor => HasConflict ? "#FF6B6B" : (WillChange ? "#4ECDC4" : "#888888");
    }

    /// <summary>
    /// ViewModel for a rename preset.
    /// </summary>
    public partial class RenamePresetViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _name = string.Empty;

        [ObservableProperty]
        private string _description = string.Empty;

        [ObservableProperty]
        private bool _isBuiltIn;

        public System.Collections.Generic.List<RenameRuleViewModel> Rules { get; set; } = new();
    }
}
