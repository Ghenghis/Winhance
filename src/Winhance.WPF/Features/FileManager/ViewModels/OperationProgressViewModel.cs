using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Winhance.WPF.Features.FileManager.ViewModels
{
    public partial class OperationProgressViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _title = "File Operation";

        [ObservableProperty]
        private string _currentOperation = "Copying files...";

        [ObservableProperty]
        private string _currentFile = string.Empty;

        [ObservableProperty]
        private double _progressPercentage;

        [ObservableProperty]
        private int _processedCount;

        [ObservableProperty]
        private int _totalCount;

        [ObservableProperty]
        private string _details = string.Empty;

        [ObservableProperty]
        private bool _showDetails;

        [ObservableProperty]
        private bool _canPause = true;

        [ObservableProperty]
        private bool _canCancel = true;

        [ObservableProperty]
        private bool _isCompleted;

        [ObservableProperty]
        private bool _isPaused;

        public ObservableCollection<string> OperationLog { get; } = new();

        public OperationProgressViewModel()
        {
        }

        [RelayCommand]
        private void Pause()
        {
            IsPaused = !IsPaused;
            if (IsPaused)
            {
                CurrentOperation = "Operation paused";
                CanCancel = true;
            }
            else
            {
                CurrentOperation = "Resuming operation...";
                ProgressPercentage = Math.Min(ProgressPercentage + 5, 100);
            }
        }

        [RelayCommand]
        private void Cancel()
        {
            CurrentOperation = "Cancelling operation...";
            CanPause = false;
            CanCancel = false;
            ProgressPercentage = 0;
            IsPaused = false;
            System.Windows.MessageBox.Show(
                "Operation cancelled by user.",
                "Cancelled",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
        }

        [RelayCommand]
        private void Close()
        {
            if (IsCompleted || IsPaused)
            {
                // Close the dialog
                OnRequestClose?.Invoke(this, EventArgs.Empty);
            }
        }

        public event EventHandler? OnRequestClose;

        public void UpdateProgress(string fileName, int processed, int total, double percentage)
        {
            CurrentFile = fileName;
            ProcessedCount = processed;
            TotalCount = total;
            ProgressPercentage = percentage;

            if (!string.IsNullOrEmpty(fileName))
            {
                OperationLog.Add($"{DateTime.Now:HH:mm:ss} - {fileName}");
                if (OperationLog.Count > 100)
                {
                    OperationLog.RemoveAt(0);
                }
            }

            if (percentage >= 100)
            {
                Complete();
            }
        }

        public void Complete()
        {
            IsCompleted = true;
            CurrentOperation = "Operation completed successfully";
            CanPause = false;
            CanCancel = false;
        }

        public void SetError(string error)
        {
            CurrentOperation = $"Error: {error}";
            IsCompleted = true;
            CanPause = false;
            CanCancel = false;
        }
    }
}
