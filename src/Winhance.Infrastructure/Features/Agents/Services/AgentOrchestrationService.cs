using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Winhance.Core.Features.Agents.Interfaces;
using Winhance.Core.Features.Agents.Models;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.Infrastructure.Features.Agents.Services
{
    /// <summary>
    /// Orchestrates agent tasks with real-time status updates and queue management.
    /// </summary>
    public class AgentOrchestrationService : IAgentOrchestrationService, IDisposable
    {
        private readonly ILogService _logService;
        private readonly ConcurrentDictionary<string, AgentTask> _tasks = new();
        private readonly ConcurrentQueue<string> _taskQueue = new();
        private readonly List<AgentTask> _completedTasks = new();
        private readonly ConcurrentDictionary<string, CancellationTokenSource> _cancellationTokens = new();
        private readonly System.Timers.Timer _statusUpdateTimer;
        private readonly object _lock = new();
        private const int MaxCompletedHistory = 50;

        public event EventHandler<AgentTaskEventArgs>? TaskUpdated;

        public event EventHandler<AgentTaskEventArgs>? TaskQueued;

        public event EventHandler<AgentTaskEventArgs>? TaskCompleted;

        public AgentOrchestrationService(ILogService logService)
        {
            _logService = logService;

            // Timer for periodic status updates (every 500ms)
            _statusUpdateTimer = new System.Timers.Timer(500);
            _statusUpdateTimer.Elapsed += OnStatusUpdateTimer;
            _statusUpdateTimer.AutoReset = true;
            _statusUpdateTimer.Start();
        }

        public IReadOnlyList<AgentTask> ActiveTasks =>
            _tasks.Values.Where(t => t.Status == AgentTaskStatus.Running).ToList();

        public IReadOnlyList<AgentTask> QueuedTasks =>
            _tasks.Values.Where(t => t.Status == AgentTaskStatus.Queued || t.Status == AgentTaskStatus.Pending).ToList();

        public IReadOnlyList<AgentTask> CompletedTasks
        {
            get
            {
                lock (_lock)
                {
                    return _completedTasks.ToList();
                }
            }
        }

        public AgentTask? CurrentTask => ActiveTasks.FirstOrDefault();

        public int QueueLength => _taskQueue.Count;

        public bool IsRunning => ActiveTasks.Any();

        public Task<string> QueueTaskAsync(AgentTask task)
        {
            task.Status = AgentTaskStatus.Queued;
            _tasks[task.Id] = task;
            _taskQueue.Enqueue(task.Id);
            _cancellationTokens[task.Id] = new CancellationTokenSource();

            _logService.Log(LogLevel.Info, $"Agent task queued: {task.AgentName} - {task.Description}");
            TaskQueued?.Invoke(this, new AgentTaskEventArgs(task));

            return Task.FromResult(task.Id);
        }

        public Task StartTaskAsync(string taskId, CancellationToken cancellationToken = default)
        {
            if (!_tasks.TryGetValue(taskId, out var task))
            {
                _logService.Log(LogLevel.Warning, $"Task not found: {taskId}");
                return Task.CompletedTask;
            }

            task.Status = AgentTaskStatus.Running;
            task.StartedAt = DateTime.UtcNow;

            _logService.Log(LogLevel.Info, $"Agent task started: {task.AgentName} - {task.Description}");
            TaskUpdated?.Invoke(this, new AgentTaskEventArgs(task, "Task started"));

            return Task.CompletedTask;
        }

        public void UpdateProgress(string taskId, int processedItems, string? currentAction = null)
        {
            if (!_tasks.TryGetValue(taskId, out var task))
            {
                return;
            }

            task.ProcessedItems = processedItems;
            if (!string.IsNullOrEmpty(currentAction))
            {
                task.CurrentAction = currentAction;
            }

            TaskUpdated?.Invoke(this, new AgentTaskEventArgs(task));
        }

        public void UpdateProgressBytes(string taskId, long processedBytes, string? currentAction = null)
        {
            if (!_tasks.TryGetValue(taskId, out var task))
            {
                return;
            }

            task.ProcessedBytes = processedBytes;
            if (!string.IsNullOrEmpty(currentAction))
            {
                task.CurrentAction = currentAction;
            }

            TaskUpdated?.Invoke(this, new AgentTaskEventArgs(task));
        }

        public Task PauseTaskAsync(string taskId)
        {
            if (!_tasks.TryGetValue(taskId, out var task))
            {
                return Task.CompletedTask;
            }

            if (task.Status == AgentTaskStatus.Running && task.CanPause)
            {
                task.Status = AgentTaskStatus.Paused;
                _logService.Log(LogLevel.Info, $"Agent task paused: {task.AgentName}");
                TaskUpdated?.Invoke(this, new AgentTaskEventArgs(task, "Task paused"));
            }

            return Task.CompletedTask;
        }

        public Task ResumeTaskAsync(string taskId)
        {
            if (!_tasks.TryGetValue(taskId, out var task))
            {
                return Task.CompletedTask;
            }

            if (task.Status == AgentTaskStatus.Paused)
            {
                task.Status = AgentTaskStatus.Running;
                _logService.Log(LogLevel.Info, $"Agent task resumed: {task.AgentName}");
                TaskUpdated?.Invoke(this, new AgentTaskEventArgs(task, "Task resumed"));
            }

            return Task.CompletedTask;
        }

        public Task CancelTaskAsync(string taskId, string? reason = null)
        {
            if (!_tasks.TryGetValue(taskId, out var task))
            {
                return Task.CompletedTask;
            }

            if (task.CanCancel && task.Status != AgentTaskStatus.Completed)
            {
                task.Status = AgentTaskStatus.Cancelled;
                task.CancellationReason = reason ?? "Cancelled by user";
                task.CompletedAt = DateTime.UtcNow;

                if (_cancellationTokens.TryGetValue(taskId, out var cts))
                {
                    cts.Cancel();
                }

                MoveToCompleted(task);
                _logService.Log(LogLevel.Info, $"Agent task cancelled: {task.AgentName} - {reason}");
                TaskCompleted?.Invoke(this, new AgentTaskEventArgs(task, "Task cancelled"));
            }

            return Task.CompletedTask;
        }

        public void CompleteTask(string taskId, bool success = true, string? message = null)
        {
            if (!_tasks.TryGetValue(taskId, out var task))
            {
                return;
            }

            task.Status = success ? AgentTaskStatus.Completed : AgentTaskStatus.Failed;
            task.CompletedAt = DateTime.UtcNow;

            MoveToCompleted(task);
            _logService.Log(
                LogLevel.Info,
                $"Agent task completed: {task.AgentName} - {task.ProcessedItems}/{task.TotalItems} items");
            TaskCompleted?.Invoke(this, new AgentTaskEventArgs(task, message ?? "Task completed"));
        }

        public void FailTask(string taskId, string errorMessage)
        {
            if (!_tasks.TryGetValue(taskId, out var task))
            {
                return;
            }

            task.Status = AgentTaskStatus.Failed;
            task.CompletedAt = DateTime.UtcNow;
            task.Errors.Add(errorMessage);

            MoveToCompleted(task);
            _logService.Log(LogLevel.Error, $"Agent task failed: {task.AgentName} - {errorMessage}");
            TaskCompleted?.Invoke(this, new AgentTaskEventArgs(task, errorMessage));
        }

        public AgentTask? GetTask(string taskId)
        {
            _tasks.TryGetValue(taskId, out var task);
            return task;
        }

        public void ClearHistory()
        {
            lock (_lock)
            {
                _completedTasks.Clear();
            }
        }

        private void MoveToCompleted(AgentTask task)
        {
            _tasks.TryRemove(task.Id, out _);
            _cancellationTokens.TryRemove(task.Id, out var cts);
            cts?.Dispose();

            lock (_lock)
            {
                _completedTasks.Insert(0, task);
                while (_completedTasks.Count > MaxCompletedHistory)
                {
                    _completedTasks.RemoveAt(_completedTasks.Count - 1);
                }
            }
        }

        private void OnStatusUpdateTimer(object? sender, ElapsedEventArgs e)
        {
            // Trigger updates for running tasks to refresh elapsed time
            foreach (var task in ActiveTasks)
            {
                TaskUpdated?.Invoke(this, new AgentTaskEventArgs(task));
            }
        }

        public void Dispose()
        {
            _statusUpdateTimer.Stop();
            _statusUpdateTimer.Dispose();

            foreach (var cts in _cancellationTokens.Values)
            {
                cts.Dispose();
            }

            _cancellationTokens.Clear();
        }
    }
}
