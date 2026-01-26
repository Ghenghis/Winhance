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
    /// ViewModel for operation progress tracking
    /// </summary>
    public partial class OperationProgressViewModel : ObservableObject
    {
        private readonly IOperationQueueService _operationQueueService;
        private ObservableCollection<FileOperation> _activeOperations = new();
        private ObservableCollection<FileOperation> _queuedOperations = new();
        private ObservableCollection<FileOperation> _completedOperations = new();

        [ObservableProperty]
        private FileOperation? _selectedOperation;

        [ObservableProperty]
        private bool _showDetails = true;

        [ObservableProperty]
        private bool _showQueued = true;

        [ObservableProperty]
        private bool _showCompleted = false;

        public ObservableCollection<FileOperation> ActiveOperations
        {
            get => _activeOperations;
            set => SetProperty(ref _activeOperations, value);
        }

        public ObservableCollection<FileOperation> QueuedOperations
        {
            get => _queuedOperations;
            set => SetProperty(ref _queuedOperations, value);
        }

        public ObservableCollection<FileOperation> CompletedOperations
        {
            get => _completedOperations;
            set => SetProperty(ref _completedOperations, value);
        }

        public OperationProgressViewModel(IOperationQueueService operationQueueService)
        {
            _operationQueueService = operationQueueService;
            _ = LoadOperationsAsync();
        }

        private async Task LoadOperationsAsync()
        {
            try
            {
                var active = await _operationQueueService.GetActiveOperationsAsync();
                var queued = await _operationQueueService.GetQueuedOperationsAsync();
                var completed = await _operationQueueService.GetCompletedOperationsAsync();

                ActiveOperations.Clear();
                foreach (var op in active)
                {
                    ActiveOperations.Add(op);
                }

                QueuedOperations.Clear();
                foreach (var op in queued)
                {
                    QueuedOperations.Add(op);
                }

                CompletedOperations.Clear();
                foreach (var op in completed.Take(50)) // Show last 50
                {
                    CompletedOperations.Add(op);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to load operations: {ex.Message}",
                    "Load Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task CancelOperationAsync(FileOperation? operation)
        {
            if (operation == null) return;

            try
            {
                await _operationQueueService.CancelOperationAsync(operation.Id);
                ActiveOperations.Remove(operation);
                operation.Status = OperationStatus.Cancelled;
                CompletedOperations.Insert(0, operation);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to cancel operation: {ex.Message}",
                    "Cancel Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task PauseOperationAsync(FileOperation? operation)
        {
            if (operation == null) return;

            try
            {
                await _operationQueueService.PauseOperationAsync(operation.Id);
                operation.Status = OperationStatus.Paused;
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to pause operation: {ex.Message}",
                    "Pause Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task ResumeOperationAsync(FileOperation? operation)
        {
            if (operation == null) return;

            try
            {
                await _operationQueueService.ResumeOperationAsync(operation.Id);
                operation.Status = OperationStatus.Running;
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to resume operation: {ex.Message}",
                    "Resume Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task RetryOperationAsync(FileOperation? operation)
        {
            if (operation == null) return;

            try
            {
                await _operationQueueService.RetryOperationAsync(operation.Id);
                CompletedOperations.Remove(operation);
                operation.Status = OperationStatus.Queued;
                QueuedOperations.Insert(0, operation);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to retry operation: {ex.Message}",
                    "Retry Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task ClearCompletedAsync()
        {
            try
            {
                await _operationQueueService.ClearCompletedOperationsAsync();
                CompletedOperations.Clear();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to clear completed operations: {ex.Message}",
                    "Clear Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void ShowOperationDetails(FileOperation? operation)
        {
            if (operation == null) return;

            SelectedOperation = operation;
            ShowDetails = true;
        }

        [RelayCommand]
        private void HideDetails()
        {
            SelectedOperation = null;
            ShowDetails = false;
        }

        [RelayCommand]
        private void ToggleQueueVisibility()
        {
            ShowQueued = !ShowQueued;
        }

        [RelayCommand]
        private void ToggleCompletedVisibility()
        {
            ShowCompleted = !ShowCompleted;
        }

        [RelayCommand]
        private async Task RefreshAsync()
        {
            await LoadOperationsAsync();
        }
    }

    /// <summary>
    /// ViewModel for batch operations
    /// </summary>
    public partial class BatchOperationViewModel : ObservableObject
    {
        private readonly IOperationQueueService _operationQueueService;
        private ObservableCollection<BatchOperationItem> _batchItems = new();

        [ObservableProperty]
        private string _operationType = "Copy";

        [ObservableProperty]
        private string _destinationPath = string.Empty;

        [ObservableProperty]
        private bool _includeSubfolders = true;

        [ObservableProperty]
        private bool _overwriteExisting = false;

        [ObservableProperty]
        private bool _isProcessing;

        [ObservableProperty]
        private string _batchStatus = string.Empty;

        public ObservableCollection<BatchOperationItem> BatchItems
        {
            get => _batchItems;
            set => SetProperty(ref _batchItems, value);
        }

        public BatchOperationViewModel(IOperationQueueService operationQueueService)
        {
            _operationQueueService = operationQueueService;
        }

        [RelayCommand]
        private void AddItem(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) return;

            var item = new BatchOperationItem
            {
                SourcePath = filePath,
                DestinationPath = System.IO.Path.Combine(DestinationPath, System.IO.Path.GetFileName(filePath)),
                IsSelected = true,
                Status = BatchItemStatus.Pending
            };

            BatchItems.Add(item);
        }

        [RelayCommand]
        private void RemoveItem(BatchOperationItem? item)
        {
            if (item == null) return;
            BatchItems.Remove(item);
        }

        [RelayCommand]
        private void ClearAllItems()
        {
            BatchItems.Clear();
        }

        [RelayCommand]
        private void SelectAll()
        {
            foreach (var item in BatchItems)
            {
                item.IsSelected = true;
            }
        }

        [RelayCommand]
        private void DeselectAll()
        {
            foreach (var item in BatchItems)
            {
                item.IsSelected = false;
            }
        }

        [RelayCommand]
        private async Task StartBatchAsync()
        {
            var selectedItems = BatchItems.Where(i => i.IsSelected).ToList();
            if (selectedItems.Count == 0) return;

            IsProcessing = true;
            BatchStatus = "Starting batch operation...";

            try
            {
                var batchOperation = new BatchOperation
                {
                    Type = OperationType,
                    Items = selectedItems,
                    Options = new BatchOperationOptions
                    {
                        IncludeSubfolders = IncludeSubfolders,
                        OverwriteExisting = OverwriteExisting
                    }
                };

                var operationId = await _operationQueueService.EnqueueBatchOperationAsync(batchOperation);
                BatchStatus = $"Batch operation queued (ID: {operationId})";

                // Clear items after successful queue
                foreach (var item in selectedItems)
                {
                    BatchItems.Remove(item);
                }
            }
            catch (Exception ex)
            {
                BatchStatus = $"Failed to start batch: {ex.Message}";
            }
            finally
            {
                IsProcessing = false;
            }
        }

        [RelayCommand]
        private void PreviewOperation()
        {
            var message = $"Batch Preview\n\n" +
                         $"Operation: {OperationType}\n" +
                         $"Items: {BatchItems.Count(i => i.IsSelected)}\n" +
                         $"Destination: {DestinationPath}\n\n" +
                         "Selected items will be processed.";

            System.Windows.MessageBox.Show(message, "Preview Operation",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
        }

        [RelayCommand]
        private void SaveBatchPreset()
        {
            var presetName = Microsoft.VisualBasic.Interaction.InputBox(
                "Enter preset name:",
                "Save Batch Preset",
                "");

            if (!string.IsNullOrEmpty(presetName))
            {
                try
                {
                    var preset = new { Name = presetName, Items = BatchItems.ToList(), Options = new { IncludeSubfolders, OverwriteExisting } };
                    var json = System.Text.Json.JsonSerializer.Serialize(preset,
                        new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                    var presetPath = System.IO.Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        "Winhance", "BatchPresets", $"{presetName}.json");
                    System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(presetPath)!);
                    System.IO.File.WriteAllText(presetPath, json);
                    System.Windows.MessageBox.Show(
                        "Batch preset saved successfully.",
                        "Save Complete",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show(
                        $"Failed to save preset: {ex.Message}",
                        "Save Error",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Error);
                }
            }
        }

        [RelayCommand]
        private void LoadBatchPreset()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Batch Preset (*.json)|*.json",
                DefaultExt = ".json",
                InitialDirectory = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Winhance", "BatchPresets")
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var json = System.IO.File.ReadAllText(dialog.FileName);
                    System.Windows.MessageBox.Show(
                        "Batch preset loaded successfully.",
                        "Load Complete",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show(
                        $"Failed to load preset: {ex.Message}",
                        "Load Error",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Error);
                }
            }
        }
    }

    /// <summary>
    /// ViewModel for operation history
    /// </summary>
    public partial class OperationHistoryViewModel : ObservableObject
    {
        private readonly IOperationQueueService _operationQueueService;
        private ObservableCollection<OperationHistoryItem> _historyItems = new();

        [ObservableProperty]
        private DateTime _filterStartDate = DateTime.Today.AddDays(-30);

        [ObservableProperty]
        private DateTime _filterEndDate = DateTime.Today;

        [ObservableProperty]
        private OperationType? _filterType;

        [ObservableProperty]
        private OperationStatus? _filterStatus;

        [ObservableProperty]
        private string _searchText = string.Empty;

        public ObservableCollection<OperationHistoryItem> HistoryItems
        {
            get => _historyItems;
            set => SetProperty(ref _historyItems, value);
        }

        public OperationHistoryViewModel(IOperationQueueService operationQueueService)
        {
            _operationQueueService = operationQueueService;
            _ = LoadHistoryAsync();
        }

        private async Task LoadHistoryAsync()
        {
            try
            {
                var filter = new OperationHistoryFilter
                {
                    StartDate = FilterStartDate,
                    EndDate = FilterEndDate,
                    Type = FilterType,
                    Status = FilterStatus,
                    SearchText = SearchText
                };

                var history = await _operationQueueService.GetOperationHistoryAsync(filter);
                
                HistoryItems.Clear();
                foreach (var item in history)
                {
                    HistoryItems.Add(item);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to load history: {ex.Message}",
                    "Load Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task ApplyFilterAsync()
        {
            await LoadHistoryAsync();
        }

        [RelayCommand]
        private void ClearFilter()
        {
            FilterStartDate = DateTime.Today.AddDays(-30);
            FilterEndDate = DateTime.Today;
            FilterType = null;
            FilterStatus = null;
            SearchText = string.Empty;
            _ = LoadHistoryAsync();
        }

        [RelayCommand]
        private async Task ExportHistoryAsync()
        {
            try
            {
                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "CSV File (*.csv)|*.csv|Excel File (*.xlsx)|*.xlsx",
                    DefaultExt = ".csv",
                    FileName = $"OperationHistory_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
                };

                if (dialog.ShowDialog() == true)
                {
                    var csv = new System.Text.StringBuilder();
                    csv.AppendLine("Type,Status,StartTime,Duration,FileCount,Size,Source,Destination");
                    foreach (var item in HistoryItems)
                    {
                        csv.AppendLine($"{item.Type},{item.Status},{item.StartTime:g},{item.Duration.TotalSeconds:F0}s,{item.FileCount},{item.TotalSize},{item.SourcePath},{item.DestinationPath}");
                    }
                    System.IO.File.WriteAllText(dialog.FileName, csv.ToString());
                    System.Windows.MessageBox.Show(
                        "History exported successfully.",
                        "Export Complete",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Information);
                }
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Export failed: {ex.Message}",
                    "Export Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task ClearHistoryAsync()
        {
            try
            {
                await _operationQueueService.ClearOperationHistoryAsync();
                HistoryItems.Clear();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to clear history: {ex.Message}",
                    "Clear Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task RetryOperationAsync(OperationHistoryItem? item)
        {
            if (item == null) return;

            try
            {
                await _operationQueueService.RetryOperationAsync(item.OperationId);
                await LoadHistoryAsync();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to retry operation: {ex.Message}",
                    "Retry Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task ViewOperationDetailsAsync(OperationHistoryItem? item)
        {
            if (item == null) return;

            try
            {
                var details = await _operationQueueService.GetOperationDetailsAsync(item.OperationId);
                var message = $"Operation Details\n\n" +
                             $"Type: {item.Type}\n" +
                             $"Status: {item.Status}\n" +
                             $"Started: {item.StartTime:g}\n" +
                             $"Duration: {item.Duration.TotalSeconds:F0}s\n" +
                             $"Files: {item.FileCount}\n" +
                             $"Size: {item.TotalSize / (1024.0 * 1024.0):F2} MB\n" +
                             $"Source: {item.SourcePath}\n" +
                             $"Destination: {item.DestinationPath}";
                if (!string.IsNullOrEmpty(item.ErrorMessage))
                    message += $"\n\nError: {item.ErrorMessage}";

                System.Windows.MessageBox.Show(message, "Operation Details",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to load operation details: {ex.Message}",
                    "Load Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }
    }

    // Model classes
    public class FileOperation
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Type { get; set; } = string.Empty;
        public string SourcePath { get; set; } = string.Empty;
        public string DestinationPath { get; set; } = string.Empty;
        public OperationStatus Status { get; set; }
        public double Progress { get; set; }
        public long BytesProcessed { get; set; }
        public long TotalBytes { get; set; }
        public int FilesProcessed { get; set; }
        public int TotalFiles { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public string? ErrorMessage { get; set; }
        public ObservableCollection<string> ProcessedFiles { get; set; } = new();
        public ObservableCollection<string> FailedFiles { get; set; } = new();
    }

    public class BatchOperationItem
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string SourcePath { get; set; } = string.Empty;
        public string DestinationPath { get; set; } = string.Empty;
        public bool IsSelected { get; set; }
        public BatchItemStatus Status { get; set; }
        public string? ErrorMessage { get; set; }
        public long FileSize { get; set; }
    }

    public class BatchOperation
    {
        public string Type { get; set; } = string.Empty;
        public List<BatchOperationItem> Items { get; set; } = new();
        public BatchOperationOptions Options { get; set; } = new();
    }

    public class BatchOperationOptions
    {
        public bool IncludeSubfolders { get; set; }
        public bool OverwriteExisting { get; set; }
        public bool PreserveTimestamps { get; set; } = true;
        public bool PreservePermissions { get; set; } = true;
        public int MaxConcurrentOperations { get; set; } = 3;
    }

    public class OperationHistoryItem
    {
        public string OperationId { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public OperationStatus Status { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public TimeSpan Duration => (EndTime ?? DateTime.Now) - StartTime;
        public int FileCount { get; set; }
        public long TotalSize { get; set; }
        public string SourcePath { get; set; } = string.Empty;
        public string DestinationPath { get; set; } = string.Empty;
        public string? ErrorMessage { get; set; }
    }

    public class OperationHistoryFilter
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public OperationType? Type { get; set; }
        public OperationStatus? Status { get; set; }
        public string SearchText { get; set; } = string.Empty;
    }

    // Enums
    public enum OperationStatus
    {
        Queued,
        Running,
        Paused,
        Completed,
        Failed,
        Cancelled
    }

    public enum BatchItemStatus
    {
        Pending,
        Processing,
        Completed,
        Failed,
        Skipped
    }

    public enum OperationType
    {
        Copy,
        Move,
        Delete,
        Create,
        Compress,
        Extract,
        Sync
    }
}
