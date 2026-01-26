using System.Collections.Generic;
using Winhance.Core.Features.Common.Enums;

namespace Winhance.Core.Features.Common.Models
{
    public class TaskProgressDetail
    {
        public double? Progress { get; set; }

        public string StatusText { get; set; } = string.Empty;

        public string DetailedMessage { get; set; } = string.Empty;

        public LogLevel LogLevel { get; set; } = LogLevel.Info;

        public bool IsIndeterminate { get; set; }

        public Dictionary<string, string> AdditionalInfo { get; set; } = new Dictionary<string, string>();

        public string TerminalOutput { get; set; } = string.Empty;

        public bool IsActive { get; set; }
    }
}
