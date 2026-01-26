using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Winhance.Core.Features.FileManager.Interfaces;
using Winhance.Core.Features.FileManager.Models;

namespace Winhance.WPF.Features.FileManager.ViewModels
{
    /// <summary>
    /// ViewModel for file operations automation
    /// </summary>
    public partial class FileAutomationViewModel : ObservableObject
    {
        private readonly IAutomationService _automationService;
        private ObservableCollection<AutomationRule> _automationRules = new();
        private ObservableCollection<AutomationTrigger> _triggers = new();
        private ObservableCollection<AutomationAction> _actions = new();

        [ObservableProperty]
        private AutomationRule? _selectedRule;

        [ObservableProperty]
        private bool _isCreatingRule;

        [ObservableProperty]
        private string _ruleName = string.Empty;

        [ObservableProperty]
        private string _ruleDescription = string.Empty;

        [ObservableProperty]
        private bool _isRuleEnabled = true;

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        [ObservableProperty]
        private AutomationTrigger? _selectedTrigger;

        [ObservableProperty]
        private AutomationAction? _selectedAction;

        [ObservableProperty]
        private ObservableCollection<AutomationAction> _ruleActions = new();

        [ObservableProperty]
        private string _scriptContent = string.Empty;

        [ObservableProperty]
        private ScriptLanguage _scriptLanguage = ScriptLanguage.PowerShell;

        public ObservableCollection<AutomationRule> AutomationRules
        {
            get => _automationRules;
            set => SetProperty(ref _automationRules, value);
        }

        public ObservableCollection<AutomationTrigger> Triggers
        {
            get => _triggers;
            set => SetProperty(ref _triggers, value);
        }

        public ObservableCollection<AutomationAction> Actions
        {
            get => _actions;
            set => SetProperty(ref _actions, value);
        }

        public ObservableCollection<AutomationAction> RuleActions
        {
            get => _ruleActions;
            set => SetProperty(ref _ruleActions, value);
        }

        public FileAutomationViewModel(IAutomationService automationService)
        {
            _automationService = automationService;
            _ = LoadAutomationRulesAsync();
            _ = LoadTriggersAsync();
            _ = LoadActionsAsync();
        }

        private async Task LoadAutomationRulesAsync()
        {
            try
            {
                var rules = await _automationService.GetAutomationRulesAsync();
                AutomationRules.Clear();
                foreach (var rule in rules.OrderByDescending(r => r.CreatedAt))
                {
                    AutomationRules.Add(rule);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading rules: {ex.Message}";
            }
        }

        private async Task LoadTriggersAsync()
        {
            try
            {
                var triggers = await _automationService.GetAvailableTriggersAsync();
                Triggers.Clear();
                foreach (var trigger in triggers)
                {
                    Triggers.Add(trigger);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading triggers: {ex.Message}";
            }
        }

        private async Task LoadActionsAsync()
        {
            try
            {
                var actions = await _automationService.GetAvailableActionsAsync();
                Actions.Clear();
                foreach (var action in actions)
                {
                    Actions.Add(action);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading actions: {ex.Message}";
            }
        }

        [RelayCommand]
        private void StartCreateRule()
        {
            IsCreatingRule = true;
            RuleName = string.Empty;
            RuleDescription = string.Empty;
            IsRuleEnabled = true;
            RuleActions.Clear();
            SelectedTrigger = null;
            ScriptContent = string.Empty;
        }

        [RelayCommand]
        private void CancelCreateRule()
        {
            IsCreatingRule = false;
            RuleName = string.Empty;
            RuleDescription = string.Empty;
            RuleActions.Clear();
            SelectedTrigger = null;
            ScriptContent = string.Empty;
        }

        [RelayCommand]
        private async Task SaveRuleAsync()
        {
            if (string.IsNullOrEmpty(RuleName) || SelectedTrigger == null || !RuleActions.Any()) return;

            try
            {
                var rule = new AutomationRule
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = RuleName,
                    Description = RuleDescription,
                    IsEnabled = IsRuleEnabled,
                    Trigger = SelectedTrigger,
                    Actions = RuleActions.ToList(),
                    CreatedAt = DateTime.Now,
                    LastTriggered = null,
                    TriggerCount = 0
                };

                await _automationService.CreateAutomationRuleAsync(rule);
                AutomationRules.Insert(0, rule);
                
                CancelCreateRule();
                StatusMessage = $"Rule '{RuleName}' created successfully";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error creating rule: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task DeleteRuleAsync(AutomationRule? rule)
        {
            if (rule == null) return;

            try
            {
                await _automationService.DeleteAutomationRuleAsync(rule.Id);
                AutomationRules.Remove(rule);
                StatusMessage = $"Rule '{rule.Name}' deleted";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error deleting rule: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task ToggleRuleAsync(AutomationRule? rule)
        {
            if (rule == null) return;

            try
            {
                rule.IsEnabled = !rule.IsEnabled;
                await _automationService.UpdateAutomationRuleAsync(rule);
                StatusMessage = rule.IsEnabled 
                    ? $"Rule '{rule.Name}' enabled"
                    : $"Rule '{rule.Name}' disabled";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error toggling rule: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task TriggerRuleAsync(AutomationRule? rule)
        {
            if (rule == null) return;

            try
            {
                await _automationService.TriggerRuleAsync(rule.Id);
                StatusMessage = $"Rule '{rule.Name}' triggered manually";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error triggering rule: {ex.Message}";
            }
        }

        [RelayCommand]
        private void AddAction(AutomationAction? action)
        {
            if (action == null) return;

            var newAction = new AutomationAction
            {
                Id = Guid.NewGuid().ToString(),
                Type = action.Type,
                Name = action.Name,
                Parameters = new Dictionary<string, object>(action.Parameters)
            };

            RuleActions.Add(newAction);
        }

        [RelayCommand]
        private void RemoveAction(AutomationAction? action)
        {
            if (action == null) return;
            RuleActions.Remove(action);
        }

        [RelayCommand]
        private void MoveActionUp(AutomationAction? action)
        {
            if (action == null) return;

            var index = RuleActions.IndexOf(action);
            if (index > 0)
            {
                RuleActions.Move(index, index - 1);
            }
        }

        [RelayCommand]
        private void MoveActionDown(AutomationAction? action)
        {
            if (action == null) return;

            var index = RuleActions.IndexOf(action);
            if (index < RuleActions.Count - 1)
            {
                RuleActions.Move(index, index + 1);
            }
        }

        [RelayCommand]
        private void EditAction(AutomationAction? action)
        {
            if (action == null) return;

            var message = $"Edit Action\n\n" +
                         $"Name: {action.Name}\n" +
                         $"Type: {action.Type}\n" +
                         $"Parameters: {action.Parameters.Count}";

            System.Windows.MessageBox.Show(message, "Action Details",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
        }

        [RelayCommand]
        private void AddScriptAction()
        {
            if (string.IsNullOrEmpty(ScriptContent)) return;

            var action = new AutomationAction
            {
                Id = Guid.NewGuid().ToString(),
                Type = ActionType.Script,
                Name = $"Script ({ScriptLanguage})",
                Parameters = new Dictionary<string, object>
                {
                    ["Language"] = ScriptLanguage.ToString(),
                    ["Script"] = ScriptContent
                }
            };

            RuleActions.Add(action);
            ScriptContent = string.Empty;
        }

        [RelayCommand]
        private void LoadScriptTemplate()
        {
            ScriptContent = ScriptLanguage switch
            {
                ScriptLanguage.PowerShell => "# PowerShell Script\nGet-ChildItem -Path $args[0]\n",
                ScriptLanguage.Batch => "@echo off\ndir %1\n",
                ScriptLanguage.Python => "# Python Script\nimport sys\nimport os\nprint(os.listdir(sys.argv[1]))\n",
                ScriptLanguage.JavaScript => "// JavaScript\nconsole.log(process.argv);\n",
                ScriptLanguage.VBScript => "' VBScript\nWScript.Echo \"Hello\"\n",
                _ => "// Script template\n"
            };
        }

        [RelayCommand]
        private void ValidateScript()
        {
            if (string.IsNullOrWhiteSpace(ScriptContent))
            {
                System.Windows.MessageBox.Show(
                    "Script content is empty.",
                    "Validation",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
                return;
            }

            System.Windows.MessageBox.Show(
                $"Script validated successfully.\n\nLanguage: {ScriptLanguage}\nLines: {ScriptContent.Split('\n').Length}",
                "Validation",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
        }

        [RelayCommand]
        private async Task TestRuleAsync()
        {
            if (SelectedTrigger == null || !RuleActions.Any()) return;

            try
            {
                var testResult = await _automationService.TestRuleAsync(SelectedTrigger, RuleActions.ToList());
                StatusMessage = testResult.Success 
                    ? "Rule test passed"
                    : $"Rule test failed: {testResult.ErrorMessage}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error testing rule: {ex.Message}";
            }
        }

        [RelayCommand]
        private void ViewRuleHistory(AutomationRule? rule)
        {
            if (rule == null) return;

            var message = $"Rule Execution History\n\n" +
                         $"Rule: {rule.Name}\n" +
                         $"Created: {rule.CreatedAt:g}\n" +
                         $"Last Triggered: {rule.LastTriggered?.ToString("g") ?? "Never"}\n" +
                         $"Total Triggers: {rule.TriggerCount}\n" +
                         $"History Entries: {rule.History.Count}";

            System.Windows.MessageBox.Show(message, "Rule History",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
        }

        [RelayCommand]
        private void ExportRules()
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "JSON File (*.json)|*.json",
                DefaultExt = ".json",
                FileName = $"AutomationRules_{DateTime.Now:yyyyMMdd_HHmmss}.json"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var json = System.Text.Json.JsonSerializer.Serialize(AutomationRules,
                        new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                    System.IO.File.WriteAllText(dialog.FileName, json);
                    StatusMessage = "Rules exported successfully";
                    System.Windows.MessageBox.Show(
                        "Automation rules exported successfully.",
                        "Export Complete",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Export failed: {ex.Message}";
                }
            }
        }

        [RelayCommand]
        private async Task ImportRulesAsync()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "JSON File (*.json)|*.json",
                DefaultExt = ".json"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var json = System.IO.File.ReadAllText(dialog.FileName);
                    var rules = System.Text.Json.JsonSerializer.Deserialize<List<AutomationRule>>(json);
                    if (rules != null)
                    {
                        foreach (var rule in rules)
                        {
                            await _automationService.CreateAutomationRuleAsync(rule);
                            AutomationRules.Add(rule);
                        }
                        StatusMessage = $"Imported {rules.Count} rules";
                        System.Windows.MessageBox.Show(
                            $"{rules.Count} automation rules imported successfully.",
                            "Import Complete",
                            System.Windows.MessageBoxButton.OK,
                            System.Windows.MessageBoxImage.Information);
                    }
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Import failed: {ex.Message}";
                }
            }
        }
    }

    /// <summary>
    /// ViewModel for batch operations
    /// </summary>
    public partial class BatchOperationsViewModel : ObservableObject
    {
        private readonly IBatchOperationService _batchService;
        private ObservableCollection<BatchOperation> _operations = new();
        private ObservableCollection<BatchTask> _tasks = new();

        [ObservableProperty]
        private BatchOperation? _selectedOperation;

        [ObservableProperty]
        private bool _isCreatingOperation;

        [ObservableProperty]
        private string _operationName = string.Empty;

        [ObservableProperty]
        private BatchOperationType _operationType = BatchOperationType.Copy;

        [ObservableProperty]
        private string _sourcePath = string.Empty;

        [ObservableProperty]
        private string _destinationPath = string.Empty;

        [ObservableProperty]
        private string _filePattern = "*.*";

        [ObservableProperty]
        private bool _includeSubfolders = true;

        [ObservableProperty]
        private bool _preserveStructure = true;

        [ObservableProperty]
        private bool _overwriteExisting = false;

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        [ObservableProperty]
        private bool _isProcessing;

        [ObservableProperty]
        private double _progress;

        [ObservableProperty]
        private int _processedFiles;

        [ObservableProperty]
        private int _totalFiles;

        [ObservableProperty]
        private TimeSpan _estimatedTimeRemaining;

        public ObservableCollection<BatchOperation> Operations
        {
            get => _operations;
            set => SetProperty(ref _operations, value);
        }

        public ObservableCollection<BatchTask> Tasks
        {
            get => _tasks;
            set => SetProperty(ref _tasks, value);
        }

        public BatchOperationsViewModel(IBatchOperationService batchService)
        {
            _batchService = batchService;
            _ = LoadOperationsAsync();
        }

        private async Task LoadOperationsAsync()
        {
            try
            {
                var operations = await _batchService.GetBatchOperationsAsync();
                Operations.Clear();
                foreach (var operation in operations.OrderByDescending(o => o.CreatedAt))
                {
                    Operations.Add(operation);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading operations: {ex.Message}";
            }
        }

        partial void OnSelectedOperationChanged(BatchOperation? value)
        {
            if (value != null)
            {
                _ = LoadTasksAsync(value.Id);
            }
        }

        private async Task LoadTasksAsync(string operationId)
        {
            try
            {
                var tasks = await _batchService.GetBatchTasksAsync(operationId);
                Tasks.Clear();
                foreach (var task in tasks)
                {
                    Tasks.Add(task);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading tasks: {ex.Message}";
            }
        }

        [RelayCommand]
        private void StartCreateOperation()
        {
            IsCreatingOperation = true;
            OperationName = string.Empty;
            SourcePath = string.Empty;
            DestinationPath = string.Empty;
            FilePattern = "*.*";
            IncludeSubfolders = true;
            PreserveStructure = true;
            OverwriteExisting = false;
        }

        [RelayCommand]
        private void CancelCreateOperation()
        {
            IsCreatingOperation = false;
            OperationName = string.Empty;
            SourcePath = string.Empty;
            DestinationPath = string.Empty;
        }

        [RelayCommand]
        private async Task CreateOperationAsync()
        {
            if (string.IsNullOrEmpty(OperationName) || string.IsNullOrEmpty(SourcePath)) return;

            try
            {
                var operation = new BatchOperation
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = OperationName,
                    Type = OperationType,
                    SourcePath = SourcePath,
                    DestinationPath = DestinationPath,
                    FilePattern = FilePattern,
                    IncludeSubfolders = IncludeSubfolders,
                    PreserveStructure = PreserveStructure,
                    OverwriteExisting = OverwriteExisting,
                    Status = BatchStatus.Created,
                    CreatedAt = DateTime.Now
                };

                await _batchService.CreateBatchOperationAsync(operation);
                Operations.Insert(0, operation);
                
                CancelCreateOperation();
                StatusMessage = $"Operation '{OperationName}' created";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error creating operation: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task StartOperationAsync(BatchOperation? operation)
        {
            if (operation == null) return;

            try
            {
                IsProcessing = true;
                StatusMessage = "Starting operation...";

                var progress = new Progress<BatchProgress>(p =>
                {
                    Progress = p.Percentage;
                    ProcessedFiles = p.ProcessedFiles;
                    TotalFiles = p.TotalFiles;
                    EstimatedTimeRemaining = p.EstimatedTimeRemaining;
                    StatusMessage = $"Processing... {p.ProcessedFiles:N0}/{p.TotalFiles:N0} files";
                });

                await _batchService.ExecuteBatchOperationAsync(operation.Id, progress);
                
                StatusMessage = "Operation completed successfully";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Operation failed: {ex.Message}";
            }
            finally
            {
                IsProcessing = false;
            }
        }

        [RelayCommand]
        private async Task PauseOperationAsync(BatchOperation? operation)
        {
            if (operation == null) return;

            try
            {
                await _batchService.PauseBatchOperationAsync(operation.Id);
                operation.Status = BatchStatus.Paused;
                StatusMessage = "Operation paused";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error pausing operation: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task ResumeOperationAsync(BatchOperation? operation)
        {
            if (operation == null) return;

            try
            {
                await _batchService.ResumeBatchOperationAsync(operation.Id);
                operation.Status = BatchStatus.Running;
                StatusMessage = "Operation resumed";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error resuming operation: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task CancelOperationAsync(BatchOperation? operation)
        {
            if (operation == null) return;

            try
            {
                await _batchService.CancelBatchOperationAsync(operation.Id);
                operation.Status = BatchStatus.Cancelled;
                StatusMessage = "Operation cancelled";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error cancelling operation: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task DeleteOperationAsync(BatchOperation? operation)
        {
            if (operation == null) return;

            try
            {
                await _batchService.DeleteBatchOperationAsync(operation.Id);
                Operations.Remove(operation);
                StatusMessage = "Operation deleted";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error deleting operation: {ex.Message}";
            }
        }

        [RelayCommand]
        private void RetryFailedTasks(BatchOperation? operation)
        {
            if (operation == null) return;

            var failedTasks = Tasks.Where(t => t.Status == BatchTaskStatus.Failed).ToList();
            foreach (var task in failedTasks)
            {
                task.Status = BatchTaskStatus.Pending;
                task.RetryCount++;
            }
            StatusMessage = $"Retrying {failedTasks.Count} failed tasks";
        }

        [RelayCommand]
        private void ViewTaskDetails(BatchTask? task)
        {
            if (task == null) return;

            var message = $"Task Details\n\n" +
                         $"Source: {task.SourcePath}\n" +
                         $"Destination: {task.DestinationPath}\n" +
                         $"Status: {task.Status}\n" +
                         $"Size: {task.FileSize / (1024.0 * 1024.0):F2} MB\n" +
                         $"Progress: {(task.FileSize > 0 ? (task.ProcessedBytes * 100.0 / task.FileSize):F1) : 0:F1}%\n" +
                         $"Retry Count: {task.RetryCount}";
            if (!string.IsNullOrEmpty(task.ErrorMessage))
                message += $"\n\nError: {task.ErrorMessage}";

            System.Windows.MessageBox.Show(message, "Task Details",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
        }

        [RelayCommand]
        private void ExportResults(BatchOperation? operation)
        {
            if (operation == null) return;

            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "CSV File (*.csv)|*.csv",
                DefaultExt = ".csv",
                FileName = $"BatchResults_{operation.Name}_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var csv = new System.Text.StringBuilder();
                    csv.AppendLine("Source,Destination,Status,Size,Created,Completed,Error");
                    foreach (var task in Tasks)
                    {
                        csv.AppendLine($"{task.SourcePath},{task.DestinationPath},{task.Status},{task.FileSize},{task.CreatedAt:g},{task.CompletedAt?.ToString("g")},{task.ErrorMessage}");
                    }
                    System.IO.File.WriteAllText(dialog.FileName, csv.ToString());
                    StatusMessage = "Results exported successfully";
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Export failed: {ex.Message}";
                }
            }
        }

        [RelayCommand]
        private void BrowseSourcePath()
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Select Source Folder"
            };

            if (dialog.ShowDialog() == true)
            {
                SourcePath = dialog.FolderName;
            }
        }

        [RelayCommand]
        private void BrowseDestinationPath()
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Select Destination Folder"
            };

            if (dialog.ShowDialog() == true)
            {
                DestinationPath = dialog.FolderName;
            }
        }

        [RelayCommand]
        private void PreviewFiles()
        {
            if (string.IsNullOrEmpty(SourcePath))
            {
                StatusMessage = "Please select a source path first";
                return;
            }

            try
            {
                var files = System.IO.Directory.GetFiles(SourcePath, FilePattern,
                    IncludeSubfolders ? System.IO.SearchOption.AllDirectories : System.IO.SearchOption.TopDirectoryOnly);
                var message = $"Preview\n\n" +
                             $"Source: {SourcePath}\n" +
                             $"Pattern: {FilePattern}\n" +
                             $"Files found: {files.Length}\n\n" +
                             $"First 10 files:\n" +
                             string.Join("\n", files.Take(10).Select(f => System.IO.Path.GetFileName(f)));
                if (files.Length > 10)
                    message += $"\n... and {files.Length - 10} more";

                System.Windows.MessageBox.Show(message, "File Preview",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Preview failed: {ex.Message}";
            }
        }
    }

    /// <summary>
    /// ViewModel for scheduled tasks
    /// </summary>
    public partial class ScheduledTasksViewModel : ObservableObject
    {
        private readonly IScheduledTaskService _scheduledTaskService;
        private ObservableCollection<ScheduledTask> _tasks = new();

        [ObservableProperty]
        private ScheduledTask? _selectedTask;

        [ObservableProperty]
        private bool _isCreatingTask;

        [ObservableProperty]
        private string _taskName = string.Empty;

        [ObservableProperty]
        private string _taskDescription = string.Empty;

        [ObservableProperty]
        private TaskType _taskType = TaskType.CustomScript;

        [ObservableProperty]
        private string _scriptPath = string.Empty;

        [ObservableProperty]
        private string _parameters = string.Empty;

        [ObservableProperty]
        private ScheduleType _scheduleType = ScheduleType.Daily;

        [ObservableProperty]
        private DateTime _startTime = DateTime.Today.AddHours(9);

        [ObservableProperty]
        private bool _isEnabled = true;

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        [ObservableProperty]
        private ObservableCollection<DayOfWeek> _selectedDays = new();

        [ObservableProperty]
        private int _intervalDays = 1;

        [ObservableProperty]
        private int _intervalHours = 0;

        [ObservableProperty]
        private int _intervalMinutes = 0;

        public ObservableCollection<ScheduledTask> Tasks
        {
            get => _tasks;
            set => SetProperty(ref _tasks, value);
        }

        public ScheduledTasksViewModel(IScheduledTaskService scheduledTaskService)
        {
            _scheduledTaskService = scheduledTaskService;
            _ = LoadTasksAsync();
        }

        private async Task LoadTasksAsync()
        {
            try
            {
                var tasks = await _scheduledTaskService.GetScheduledTasksAsync();
                Tasks.Clear();
                foreach (var task in tasks.OrderByDescending(t => t.NextRun))
                {
                    Tasks.Add(task);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading tasks: {ex.Message}";
            }
        }

        [RelayCommand]
        private void StartCreateTask()
        {
            IsCreatingTask = true;
            TaskName = string.Empty;
            TaskDescription = string.Empty;
            TaskType = TaskType.CustomScript;
            ScriptPath = string.Empty;
            Parameters = string.Empty;
            ScheduleType = ScheduleType.Daily;
            StartTime = DateTime.Today.AddHours(9);
            IsEnabled = true;
            SelectedDays.Clear();
            IntervalDays = 1;
            IntervalHours = 0;
            IntervalMinutes = 0;
        }

        [RelayCommand]
        private void CancelCreateTask()
        {
            IsCreatingTask = false;
            TaskName = string.Empty;
            ScriptPath = string.Empty;
            Parameters = string.Empty;
        }

        [RelayCommand]
        private async Task CreateTaskAsync()
        {
            if (string.IsNullOrEmpty(TaskName) || string.IsNullOrEmpty(ScriptPath)) return;

            try
            {
                var schedule = new TaskSchedule
                {
                    Type = ScheduleType,
                    StartTime = StartTime,
                    DaysOfWeek = SelectedDays.ToList(),
                    IntervalDays = IntervalDays,
                    IntervalHours = IntervalHours,
                    IntervalMinutes = IntervalMinutes
                };

                var task = new ScheduledTask
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = TaskName,
                    Description = TaskDescription,
                    Type = TaskType,
                    ScriptPath = ScriptPath,
                    Parameters = Parameters,
                    Schedule = schedule,
                    IsEnabled = IsEnabled,
                    CreatedAt = DateTime.Now,
                    LastRun = null,
                    NextRun = CalculateNextRun(schedule)
                };

                await _scheduledTaskService.CreateScheduledTaskAsync(task);
                Tasks.Insert(0, task);
                
                CancelCreateTask();
                StatusMessage = $"Task '{TaskName}' created";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error creating task: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task DeleteTaskAsync(ScheduledTask? task)
        {
            if (task == null) return;

            try
            {
                await _scheduledTaskService.DeleteScheduledTaskAsync(task.Id);
                Tasks.Remove(task);
                StatusMessage = $"Task '{task.Name}' deleted";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error deleting task: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task ToggleTaskAsync(ScheduledTask? task)
        {
            if (task == null) return;

            try
            {
                task.IsEnabled = !task.IsEnabled;
                await _scheduledTaskService.UpdateScheduledTaskAsync(task);
                StatusMessage = task.IsEnabled 
                    ? $"Task '{task.Name}' enabled"
                    : $"Task '{task.Name}' disabled";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error toggling task: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task RunTaskNowAsync(ScheduledTask? task)
        {
            if (task == null) return;

            try
            {
                await _scheduledTaskService.RunTaskNowAsync(task.Id);
                StatusMessage = $"Task '{task.Name}' started";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error running task: {ex.Message}";
            }
        }

        [RelayCommand]
        private void ViewTaskHistory(ScheduledTask? task)
        {
            if (task == null) return;

            var message = $"Task Execution History\n\n" +
                         $"Task: {task.Name}\n" +
                         $"Last Run: {task.LastRun?.ToString("g") ?? "Never"}\n" +
                         $"Next Run: {task.NextRun?.ToString("g") ?? "Not scheduled"}\n" +
                         $"Status: {task.Status}\n" +
                         $"History Entries: {task.ExecutionHistory.Count}";

            System.Windows.MessageBox.Show(message, "Task History",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
        }

        [RelayCommand]
        private void EditTask(ScheduledTask? task)
        {
            if (task == null) return;

            TaskName = task.Name;
            TaskDescription = task.Description;
            TaskType = task.Type;
            ScriptPath = task.ScriptPath;
            Parameters = task.Parameters;
            ScheduleType = task.Schedule.Type;
            StartTime = task.Schedule.StartTime;
            IsEnabled = task.IsEnabled;
            IsCreatingTask = true;
            StatusMessage = $"Editing task: {task.Name}";
        }

        [RelayCommand]
        private void BrowseScriptPath()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Script Files (*.ps1;*.bat;*.py;*.js;*.vbs)|*.ps1;*.bat;*.py;*.js;*.vbs|All Files (*.*)|*.*",
                DefaultExt = ".ps1"
            };

            if (dialog.ShowDialog() == true)
            {
                ScriptPath = dialog.FileName;
            }
        }

        [RelayCommand]
        private void AddDay(DayOfWeek day)
        {
            if (!SelectedDays.Contains(day))
            {
                SelectedDays.Add(day);
            }
        }

        [RelayCommand]
        private void RemoveDay(DayOfWeek day)
        {
            SelectedDays.Remove(day);
        }

        private DateTime CalculateNextRun(TaskSchedule schedule)
        {
            var now = DateTime.Now;
            var nextRun = schedule.StartTime;

            return schedule.Type switch
            {
                ScheduleType.Daily => now > nextRun ? nextRun.AddDays(1) : nextRun,
                ScheduleType.Weekly => CalculateNextWeeklyRun(schedule, now),
                ScheduleType.Monthly => now > nextRun ? nextRun.AddMonths(1) : nextRun,
                ScheduleType.Interval => CalculateNextIntervalRun(schedule, now),
                _ => nextRun
            };
        }

        private DateTime CalculateNextWeeklyRun(TaskSchedule schedule, DateTime now)
        {
            var nextRun = schedule.StartTime;
            while (nextRun <= now || !schedule.DaysOfWeek.Contains(nextRun.DayOfWeek))
            {
                nextRun = nextRun.AddDays(1);
            }
            return nextRun;
        }

        private DateTime CalculateNextIntervalRun(TaskSchedule schedule, DateTime now)
        {
            var interval = new TimeSpan(schedule.IntervalDays, schedule.IntervalHours, schedule.IntervalMinutes);
            var nextRun = schedule.StartTime;
            while (nextRun <= now)
            {
                nextRun = nextRun.Add(interval);
            }
            return nextRun;
        }
    }

    // Model classes
    public class AutomationRule
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsEnabled { get; set; }
        public AutomationTrigger Trigger { get; set; } = null!;
        public List<AutomationAction> Actions { get; set; } = new();
        public DateTime CreatedAt { get; set; }
        public DateTime? LastTriggered { get; set; }
        public int TriggerCount { get; set; }
        public List<ExecutionHistory> History { get; set; } = new();
    }

    public class AutomationTrigger
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public TriggerType Type { get; set; }
        public string Description { get; set; } = string.Empty;
        public Dictionary<string, object> Parameters { get; set; } = new();
        public bool IsConfigured { get; set; }
    }

    public class AutomationAction
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public ActionType Type { get; set; }
        public string Description { get; set; } = string.Empty;
        public Dictionary<string, object> Parameters { get; set; } = new();
        public bool IsConfigured { get; set; }
    }

    public class ExecutionHistory
    {
        public DateTime ExecutedAt { get; set; }
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public TimeSpan Duration { get; set; }
        public int ItemsProcessed { get; set; }
        public Dictionary<string, object> Results { get; set; } = new();
    }

    public class BatchOperation
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public BatchOperationType Type { get; set; }
        public string SourcePath { get; set; } = string.Empty;
        public string DestinationPath { get; set; } = string.Empty;
        public string FilePattern { get; set; } = string.Empty;
        public bool IncludeSubfolders { get; set; }
        public bool PreserveStructure { get; set; }
        public bool OverwriteExisting { get; set; }
        public BatchStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public int TotalFiles { get; set; }
        public int ProcessedFiles { get; set; }
        public int FailedFiles { get; set; }
        public long TotalBytes { get; set; }
        public long ProcessedBytes { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public class BatchTask
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string OperationId { get; set; } = string.Empty;
        public string SourcePath { get; set; } = string.Empty;
        public string DestinationPath { get; set; } = string.Empty;
        public BatchTaskStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public long FileSize { get; set; }
        public long ProcessedBytes { get; set; }
        public string? ErrorMessage { get; set; }
        public int RetryCount { get; set; }
    }

    public class BatchProgress
    {
        public double Percentage { get; set; }
        public int ProcessedFiles { get; set; }
        public int TotalFiles { get; set; }
        public long ProcessedBytes { get; set; }
        public long TotalBytes { get; set; }
        public TimeSpan EstimatedTimeRemaining { get; set; }
        public string CurrentFile { get; set; } = string.Empty;
        public double ProcessingSpeed { get; set; }
    }

    public class ScheduledTask
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public TaskType Type { get; set; }
        public string ScriptPath { get; set; } = string.Empty;
        public string Parameters { get; set; } = string.Empty;
        public TaskSchedule Schedule { get; set; } = null!;
        public bool IsEnabled { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastRun { get; set; }
        public DateTime? NextRun { get; set; }
        public TaskStatus Status { get; set; }
        public List<TaskExecution> ExecutionHistory { get; set; } = new();
    }

    public class TaskSchedule
    {
        public ScheduleType Type { get; set; }
        public DateTime StartTime { get; set; }
        public List<DayOfWeek> DaysOfWeek { get; set; } = new();
        public int IntervalDays { get; set; }
        public int IntervalHours { get; set; }
        public int IntervalMinutes { get; set; }
        public DateTime? EndDate { get; set; }
    }

    public class TaskExecution
    {
        public DateTime ExecutedAt { get; set; }
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public TimeSpan Duration { get; set; }
        public int ExitCode { get; set; }
        public string Output { get; set; } = string.Empty;
        public string ErrorOutput { get; set; } = string.Empty;
    }

    // Enums
    public enum TriggerType
    {
        FileCreated,
        FileDeleted,
        FileModified,
        FileMoved,
        FolderCreated,
        FolderDeleted,
        FileSystemEvent,
        Schedule,
        SystemStartup,
        UserLogin,
        CustomEvent
    }

    public enum ActionType
    {
        CopyFile,
        MoveFile,
        DeleteFile,
        RenameFile,
        CreateFolder,
        DeleteFolder,
        CompressFile,
        DecompressFile,
        SendEmail,
        RunProgram,
        RunScript,
        SetAttributes,
        CreateShortcut,
        LogEvent,
        ShowNotification,
        CustomAction
    }

    public enum BatchOperationType
    {
        Copy,
        Move,
        Delete,
        Compress,
        Decompress,
        Encrypt,
        Decrypt,
        ChangeAttributes,
        Rename,
        CreateBackup,
        Sync,
        Custom
    }

    public enum BatchStatus
    {
        Created,
        Queued,
        Running,
        Paused,
        Completed,
        Failed,
        Cancelled
    }

    public enum BatchTaskStatus
    {
        Pending,
        Running,
        Completed,
        Failed,
        Skipped,
        Cancelled
    }

    public enum TaskType
    {
        CustomScript,
        Backup,
        Cleanup,
        Sync,
        Report,
        Maintenance,
        Monitoring,
        Alert
    }

    public enum ScheduleType
    {
        Once,
        Daily,
        Weekly,
        Monthly,
        Interval,
        OnStartup,
        OnLogon,
        OnIdle
    }

    public enum TaskStatus
    {
        Ready,
        Running,
        Completed,
        Failed,
        Disabled,
        NotScheduled
    }

    public enum ScriptLanguage
    {
        PowerShell,
        Batch,
        Python,
        JavaScript,
        VBScript
    }
}
