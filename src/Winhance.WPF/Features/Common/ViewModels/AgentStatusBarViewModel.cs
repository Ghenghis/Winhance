using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Winhance.Core.Features.Agents.Interfaces;
using Winhance.Core.Features.Agents.Models;

namespace Winhance.WPF.Features.Common.ViewModels
{
    /// <summary>
    /// ViewModel for the agent status bar showing real-time agent task progress.
    /// </summary>
    public partial class AgentStatusBarViewModel : ObservableObject
    {
        private readonly IAgentOrchestrationService _orchestrationService;
        private readonly Dispatcher _dispatcher;

        [ObservableProperty]
        private bool _isVisible;

        [ObservableProperty]
        private bool _hasActiveTask;

        [ObservableProperty]
        private string _currentAgentName = string.Empty;

        [ObservableProperty]
        private string _currentAction = string.Empty;

        [ObservableProperty]
        private double _progressPercentage;

        [ObservableProperty]
        private string _progressText = string.Empty;

        [ObservableProperty]
        private string _elapsedTime = "00:00";

        [ObservableProperty]
        private string _estimatedTimeRemaining = "--:--";

        [ObservableProperty]
        private int _queueCount;

        [ObservableProperty]
        private string _statusIcon = "CheckCircle";

        [ObservableProperty]
        private string _statusColor = "Green";

        [ObservableProperty]
        private bool _canPause;

        [ObservableProperty]
        private bool _canCancel;

        [ObservableProperty]
        private bool _isPaused;

        public ObservableCollection<AgentTaskItemViewModel> ActiveTasks { get; } = new();
        public ObservableCollection<AgentTaskItemViewModel> RecentTasks { get; } = new();

        public AgentStatusBarViewModel(IAgentOrchestrationService orchestrationService)
        {
            _orchestrationService = orchestrationService;
            _dispatcher = Dispatcher.CurrentDispatcher;

            _orchestrationService.TaskUpdated += OnTaskUpdated;
            _orchestrationService.TaskQueued += OnTaskQueued;
            _orchestrationService.TaskCompleted += OnTaskCompleted;

            RefreshState();
        }

        private void OnTaskUpdated(object? sender, AgentTaskEventArgs e)
        {
            _dispatcher.BeginInvoke(() => UpdateFromTask(e.Task));
        }

        private void OnTaskQueued(object? sender, AgentTaskEventArgs e)
        {
            _dispatcher.BeginInvoke(() =>
            {
                RefreshState();
                ActiveTasks.Add(new AgentTaskItemViewModel(e.Task));
            });
        }

        private void OnTaskCompleted(object? sender, AgentTaskEventArgs e)
        {
            _dispatcher.BeginInvoke(() =>
            {
                RefreshState();
                
                var existing = ActiveTasks.FirstOrDefault(t => t.TaskId == e.Task.Id);
                if (existing != null)
                    ActiveTasks.Remove(existing);

                RecentTasks.Insert(0, new AgentTaskItemViewModel(e.Task));
                while (RecentTasks.Count > 10)
                    RecentTasks.RemoveAt(RecentTasks.Count - 1);
            });
        }

        private void UpdateFromTask(AgentTask task)
        {
            if (task.Status == AgentTaskStatus.Running)
            {
                HasActiveTask = true;
                IsVisible = true;
                CurrentAgentName = task.AgentName;
                CurrentAction = task.CurrentAction;
                ProgressPercentage = task.ProgressPercentage;
                ProgressText = task.ProgressText;
                ElapsedTime = task.ElapsedTimeText;
                EstimatedTimeRemaining = task.EtaText;
                CanPause = task.CanPause;
                CanCancel = task.CanCancel;
                IsPaused = false;
                StatusIcon = "Play";
                StatusColor = "#3498db";

                var existing = ActiveTasks.FirstOrDefault(t => t.TaskId == task.Id);
                if (existing != null)
                {
                    existing.UpdateFrom(task);
                }
            }
            else if (task.Status == AgentTaskStatus.Paused)
            {
                IsPaused = true;
                StatusIcon = "Pause";
                StatusColor = "#f39c12";
            }
        }

        private void RefreshState()
        {
            var currentTask = _orchestrationService.CurrentTask;
            QueueCount = _orchestrationService.QueueLength;
            HasActiveTask = currentTask != null;
            IsVisible = HasActiveTask || QueueCount > 0;

            if (!HasActiveTask)
            {
                CurrentAgentName = string.Empty;
                CurrentAction = QueueCount > 0 ? $"{QueueCount} tasks queued" : "Idle";
                ProgressPercentage = 0;
                ProgressText = string.Empty;
                ElapsedTime = "00:00";
                EstimatedTimeRemaining = "--:--";
                StatusIcon = QueueCount > 0 ? "Clock" : "CheckCircle";
                StatusColor = QueueCount > 0 ? "#f39c12" : "#27ae60";
            }
        }

        [RelayCommand]
        private async Task PauseCurrentTask()
        {
            var task = _orchestrationService.CurrentTask;
            if (task != null)
            {
                if (IsPaused)
                    await _orchestrationService.ResumeTaskAsync(task.Id);
                else
                    await _orchestrationService.PauseTaskAsync(task.Id);
            }
        }

        [RelayCommand]
        private async Task CancelCurrentTask()
        {
            var task = _orchestrationService.CurrentTask;
            if (task != null)
            {
                await _orchestrationService.CancelTaskAsync(task.Id, "Cancelled by user");
            }
        }

        [RelayCommand]
        private void ToggleExpanded()
        {
            // Toggle expanded view of all active tasks
        }
    }

    public partial class AgentTaskItemViewModel : ObservableObject
    {
        public string TaskId { get; }

        [ObservableProperty]
        private string _agentName;

        [ObservableProperty]
        private string _description;

        [ObservableProperty]
        private string _currentAction;

        [ObservableProperty]
        private double _progress;

        [ObservableProperty]
        private string _progressText;

        [ObservableProperty]
        private string _elapsedTime;

        [ObservableProperty]
        private string _status;

        [ObservableProperty]
        private string _statusIcon;

        public AgentTaskItemViewModel(AgentTask task)
        {
            TaskId = task.Id;
            _agentName = task.AgentName;
            _description = task.Description;
            _currentAction = task.CurrentAction;
            _progress = task.ProgressPercentage;
            _progressText = task.ProgressText;
            _elapsedTime = task.ElapsedTimeText;
            _status = task.Status.ToString();
            _statusIcon = GetStatusIcon(task.Status);
        }

        public void UpdateFrom(AgentTask task)
        {
            CurrentAction = task.CurrentAction;
            Progress = task.ProgressPercentage;
            ProgressText = task.ProgressText;
            ElapsedTime = task.ElapsedTimeText;
            Status = task.Status.ToString();
            StatusIcon = GetStatusIcon(task.Status);
        }

        private static string GetStatusIcon(AgentTaskStatus status) => status switch
        {
            AgentTaskStatus.Running => "Play",
            AgentTaskStatus.Paused => "Pause",
            AgentTaskStatus.Completed => "CheckCircle",
            AgentTaskStatus.Failed => "AlertCircle",
            AgentTaskStatus.Cancelled => "XCircle",
            AgentTaskStatus.Queued => "Clock",
            _ => "Circle"
        };
    }
}
