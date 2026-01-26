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
    /// ViewModel for managing the operation queue.
    /// </summary>
    public partial class OperationQueueViewModel : ObservableObject
    {
        private readonly IOperationQueueService _operationQueueService;

        [ObservableProperty]
        private ObservableCollection<QueuedOperationViewModel> _queuedOperations = new();

        [ObservableProperty]
        private ObservableCollection<QueuedOperationViewModel> _activeOperations = new();

        [ObservableProperty]
        private ObservableCollection<QueuedOperationViewModel> _completedOperations = new();

        [ObservableProperty]
        private QueuedOperationViewModel? _selectedOperation;

        [ObservableProperty]
        private bool _isPaused;

        [ObservableProperty]
        private bool _showCompleted = true;

        [ObservableProperty]
        private int _totalQueued;

        [ObservableProperty]
        private int _totalActive;

        [ObservableProperty]
        private int _totalCompleted;

        [ObservableProperty]
        private string _statusSummary = "No operations";

        public OperationQueueViewModel(IOperationQueueService operationQueueService)
        {
            _operationQueueService = operationQueueService;

            _operationQueueService.OperationQueued += OnOperationQueued;
            _operationQueueService.OperationStarted += OnOperationStarted;
            _operationQueueService.OperationCompleted += OnOperationCompleted;
            _operationQueueService.OperationProgress += OnOperationProgress;
            _operationQueueService.OperationFailed += OnOperationFailed;
            _operationQueueService.QueuePaused += OnQueuePaused;
            _operationQueueService.QueueResumed += OnQueueResumed;

            _ = LoadOperationsAsync();
        }

        [RelayCommand]
        private async Task LoadOperationsAsync()
        {
            try
            {
                var queued = await _operationQueueService.GetQueuedOperationsAsync();
                QueuedOperations = new ObservableCollection<QueuedOperationViewModel>(
                    queued.Select(o => new QueuedOperationViewModel(o)));

                var active = await _operationQueueService.GetActiveOperationsAsync();
                ActiveOperations = new ObservableCollection<QueuedOperationViewModel>(
                    active.Select(o => new QueuedOperationViewModel(o)));

                var completed = await _operationQueueService.GetCompletedOperationsAsync(50);
                CompletedOperations = new ObservableCollection<QueuedOperationViewModel>(
                    completed.Select(o => new QueuedOperationViewModel(o)));

                UpdateSummary();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading operations: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task PauseQueueAsync()
        {
            await _operationQueueService.PauseQueueAsync();
            IsPaused = true;
        }

        [RelayCommand]
        private async Task ResumeQueueAsync()
        {
            await _operationQueueService.ResumeQueueAsync();
            IsPaused = false;
        }

        [RelayCommand]
        private async Task CancelOperationAsync(QueuedOperationViewModel? operation)
        {
            if (operation?.Id == null) return;

            await _operationQueueService.CancelOperationAsync(operation.Id);
        }

        [RelayCommand]
        private async Task RetryOperationAsync(QueuedOperationViewModel? operation)
        {
            if (operation?.Id == null) return;

            await _operationQueueService.RetryOperationAsync(operation.Id);
        }

        [RelayCommand]
        private async Task ClearCompletedAsync()
        {
            await _operationQueueService.ClearCompletedOperationsAsync();
            CompletedOperations.Clear();
            UpdateSummary();
        }

        [RelayCommand]
        private async Task ClearAllAsync()
        {
            await _operationQueueService.ClearQueueAsync();
            QueuedOperations.Clear();
            UpdateSummary();
        }

        private void OnOperationQueued(object? sender, OperationQueuedEventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                QueuedOperations.Insert(0, new QueuedOperationViewModel(e.Operation));
                UpdateSummary();
            });
        }

        private void OnOperationStarted(object? sender, OperationStartedEventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var operation = QueuedOperations.FirstOrDefault(o => o.Id == e.OperationId);
                if (operation != null)
                {
                    QueuedOperations.Remove(operation);
                    operation.Status = OperationStatus.Running;
                    ActiveOperations.Insert(0, operation);
                    UpdateSummary();
                }
            });
        }

        private void OnOperationCompleted(object? sender, OperationCompletedEventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var operation = ActiveOperations.FirstOrDefault(o => o.Id == e.OperationId);
                if (operation != null)
                {
                    ActiveOperations.Remove(operation);
                    operation.Status = e.Success ? OperationStatus.Completed : OperationStatus.Failed;
                    operation.Progress = 100;
                    CompletedOperations.Insert(0, operation);
                    UpdateSummary();
                }
            });
        }

        private void OnOperationProgress(object? sender, OperationProgressEventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var operation = ActiveOperations.FirstOrDefault(o => o.Id == e.OperationId);
                if (operation != null)
                {
                    operation.Progress = e.ProgressPercentage;
                    operation.CurrentFile = e.CurrentFile;
                    operation.Speed = e.BytesPerSecond;
                }
            });
        }

        private void OnOperationFailed(object? sender, OperationFailedEventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var operation = ActiveOperations.FirstOrDefault(o => o.Id == e.OperationId);
                if (operation != null)
                {
                    operation.Status = OperationStatus.Failed;
                    operation.ErrorMessage = e.ErrorMessage;
                }
            });
        }

        private void OnQueuePaused(object? sender, EventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                IsPaused = true;
                StatusSummary = "Queue paused";
            });
        }

        private void OnQueueResumed(object? sender, EventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                IsPaused = false;
                UpdateSummary();
            });
        }

        private void UpdateSummary()
        {
            TotalQueued = QueuedOperations.Count;
            TotalActive = ActiveOperations.Count;
            TotalCompleted = CompletedOperations.Count;

            if (TotalActive > 0)
            {
                StatusSummary = $"{TotalActive} running, {TotalQueued} queued";
            }
            else if (TotalQueued > 0)
            {
                StatusSummary = $"{TotalQueued} queued";
            }
            else if (TotalCompleted > 0)
            {
                StatusSummary = $"{TotalCompleted} completed";
            }
            else
            {
                StatusSummary = "No operations";
            }
        }

        public void Dispose()
        {
            _operationQueueService.OperationQueued -= OnOperationQueued;
            _operationQueueService.OperationStarted -= OnOperationStarted;
            _operationQueueService.OperationCompleted -= OnOperationCompleted;
            _operationQueueService.OperationProgress -= OnOperationProgress;
            _operationQueueService.OperationFailed -= OnOperationFailed;
            _operationQueueService.QueuePaused -= OnQueuePaused;
            _operationQueueService.QueueResumed -= OnQueueResumed;
        }
    }
}
