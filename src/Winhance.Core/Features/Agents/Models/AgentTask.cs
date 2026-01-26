using System;
using System.Collections.Generic;

namespace Winhance.Core.Features.Agents.Models
{
    /// <summary>
    /// Represents a task being executed by an agent with real-time tracking.
    /// </summary>
    public class AgentTask
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();

        public string AgentName { get; set; } = string.Empty;

        public AgentType AgentType { get; set; }

        public string Description { get; set; } = string.Empty;

        public string CurrentAction { get; set; } = string.Empty;

        public AgentTaskStatus Status { get; set; } = AgentTaskStatus.Pending;

        public AgentTaskPriority Priority { get; set; } = AgentTaskPriority.Normal;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? StartedAt { get; set; }

        public DateTime? CompletedAt { get; set; }

        public int TotalItems { get; set; }

        public int ProcessedItems { get; set; }

        public int FailedItems { get; set; }

        public long TotalBytes { get; set; }

        public long ProcessedBytes { get; set; }

        public double ProgressPercentage => TotalItems > 0
            ? Math.Round((double)ProcessedItems / TotalItems * 100, 1)
            : 0;

        public TimeSpan ElapsedTime => StartedAt.HasValue
            ? (CompletedAt ?? DateTime.UtcNow) - StartedAt.Value
            : TimeSpan.Zero;

        public TimeSpan? EstimatedTimeRemaining
        {
            get
            {
                if (!StartedAt.HasValue || ProcessedItems == 0 || TotalItems == 0)
                {
                    return null;
                }

                var elapsed = ElapsedTime.TotalSeconds;
                var itemsPerSecond = ProcessedItems / elapsed;
                var remainingItems = TotalItems - ProcessedItems;

                if (itemsPerSecond <= 0)
                {
                    return null;
                }

                return TimeSpan.FromSeconds(remainingItems / itemsPerSecond);
            }
        }

        public string ProgressText => $"{ProcessedItems:N0} / {TotalItems:N0}";

        public string ElapsedTimeText => FormatTimeSpan(ElapsedTime);

        public string EtaText => EstimatedTimeRemaining.HasValue
            ? FormatTimeSpan(EstimatedTimeRemaining.Value)
            : "--:--";

        public List<string> Errors { get; set; } = new();

        public Dictionary<string, object> Metadata { get; set; } = new();

        public string? CancellationReason { get; set; }

        public bool CanCancel { get; set; } = true;

        public bool CanPause { get; set; } = true;

        private static string FormatTimeSpan(TimeSpan ts)
        {
            if (ts.TotalHours >= 1)
            {
                return $"{(int)ts.TotalHours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}";
            }

            return $"{ts.Minutes:D2}:{ts.Seconds:D2}";
        }
    }

    public enum AgentType
    {
        FileDiscovery,
        Classification,
        Organization,
        Cleanup,
        Search,
        Monitor,
        BatchRename,
        Duplicate,
        SpaceRecovery,
        Backup,
        Restore,
    }

    public enum AgentTaskStatus
    {
        Pending,
        Queued,
        Running,
        Paused,
        Completed,
        Failed,
        Cancelled,
    }

    public enum AgentTaskPriority
    {
        Low,
        Normal,
        High,
        Critical,
    }
}
