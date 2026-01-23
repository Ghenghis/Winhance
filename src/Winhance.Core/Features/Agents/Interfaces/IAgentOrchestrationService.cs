using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.Agents.Models;

namespace Winhance.Core.Features.Agents.Interfaces
{
    /// <summary>
    /// Service for orchestrating agent tasks with real-time status tracking.
    /// </summary>
    public interface IAgentOrchestrationService
    {
        /// <summary>
        /// Event raised when any agent task is updated.
        /// </summary>
        event EventHandler<AgentTaskEventArgs>? TaskUpdated;

        /// <summary>
        /// Event raised when a new task is queued.
        /// </summary>
        event EventHandler<AgentTaskEventArgs>? TaskQueued;

        /// <summary>
        /// Event raised when a task completes.
        /// </summary>
        event EventHandler<AgentTaskEventArgs>? TaskCompleted;

        /// <summary>
        /// Gets all currently active tasks.
        /// </summary>
        IReadOnlyList<AgentTask> ActiveTasks { get; }

        /// <summary>
        /// Gets all queued tasks waiting to run.
        /// </summary>
        IReadOnlyList<AgentTask> QueuedTasks { get; }

        /// <summary>
        /// Gets recently completed tasks.
        /// </summary>
        IReadOnlyList<AgentTask> CompletedTasks { get; }

        /// <summary>
        /// Gets the currently running task, if any.
        /// </summary>
        AgentTask? CurrentTask { get; }

        /// <summary>
        /// Gets the total number of tasks in queue.
        /// </summary>
        int QueueLength { get; }

        /// <summary>
        /// Gets whether any agent is currently running.
        /// </summary>
        bool IsRunning { get; }

        /// <summary>
        /// Queues a new agent task for execution.
        /// </summary>
        Task<string> QueueTaskAsync(AgentTask task);

        /// <summary>
        /// Starts a task immediately.
        /// </summary>
        Task StartTaskAsync(string taskId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates task progress.
        /// </summary>
        void UpdateProgress(string taskId, int processedItems, string? currentAction = null);

        /// <summary>
        /// Updates task progress with bytes.
        /// </summary>
        void UpdateProgressBytes(string taskId, long processedBytes, string? currentAction = null);

        /// <summary>
        /// Pauses a running task.
        /// </summary>
        Task PauseTaskAsync(string taskId);

        /// <summary>
        /// Resumes a paused task.
        /// </summary>
        Task ResumeTaskAsync(string taskId);

        /// <summary>
        /// Cancels a task.
        /// </summary>
        Task CancelTaskAsync(string taskId, string? reason = null);

        /// <summary>
        /// Marks a task as completed.
        /// </summary>
        void CompleteTask(string taskId, bool success = true, string? message = null);

        /// <summary>
        /// Marks a task as failed.
        /// </summary>
        void FailTask(string taskId, string errorMessage);

        /// <summary>
        /// Gets a task by ID.
        /// </summary>
        AgentTask? GetTask(string taskId);

        /// <summary>
        /// Clears completed task history.
        /// </summary>
        void ClearHistory();
    }

    public class AgentTaskEventArgs : EventArgs
    {
        public AgentTask Task { get; }
        public string? Message { get; }

        public AgentTaskEventArgs(AgentTask task, string? message = null)
        {
            Task = task;
            Message = message;
        }
    }
}
