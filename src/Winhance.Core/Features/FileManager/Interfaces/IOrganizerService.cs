using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Winhance.Core.Features.FileManager.Interfaces
{
    /// <summary>
    /// Service interface for smart file organization operations.
    /// </summary>
    public interface IOrganizerService
    {
        /// <summary>
        /// Analyzes a folder and generates an organization plan.
        /// </summary>
        Task<OrganizationPlan> AnalyzeAsync(string path, OrganizationStrategy strategy, CancellationToken cancellationToken = default);

        /// <summary>
        /// Executes an organization plan.
        /// </summary>
        Task<OrganizationResult> ExecuteAsync(OrganizationPlan plan, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets space recovery opportunities for a drive.
        /// </summary>
        Task<SpaceRecoveryAnalysis> AnalyzeSpaceRecoveryAsync(string driveLetter, CancellationToken cancellationToken = default);

        /// <summary>
        /// Relocates AI models to another drive with symlink support.
        /// </summary>
        Task<OrganizationResult> RelocateModelsAsync(string sourcePath, string destinationPath, bool createSymlinks, CancellationToken cancellationToken = default);

        /// <summary>
        /// Finds duplicate files in a path.
        /// </summary>
        Task<IEnumerable<DuplicateGroup>> FindDuplicatesAsync(string path, DuplicateSearchMethod method, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets available organization rules.
        /// </summary>
        Task<IEnumerable<OrganizationRule>> GetRulesAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Saves an organization rule.
        /// </summary>
        Task SaveRuleAsync(OrganizationRule rule, CancellationToken cancellationToken = default);

        /// <summary>
        /// Undoes an organization operation.
        /// </summary>
        Task<OrganizationResult> UndoAsync(string transactionId, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Organization strategies.
    /// </summary>
    public enum OrganizationStrategy
    {
        ByType,
        ByDate,
        ByProject,
        BySize,
        ByAICategory,
        Custom
    }

    /// <summary>
    /// Methods for finding duplicates.
    /// </summary>
    public enum DuplicateSearchMethod
    {
        Hash,
        Name,
        Size,
        NameAndSize,
        SimilarImage
    }

    /// <summary>
    /// Represents an organization plan.
    /// </summary>
    public class OrganizationPlan
    {
        public string SourcePath { get; set; } = string.Empty;
        public string DestinationPath { get; set; } = string.Empty;
        public OrganizationStrategy Strategy { get; set; }
        public DateTime AnalysisDate { get; set; } = DateTime.UtcNow;
        public IEnumerable<OrganizationCategory> Categories { get; set; } = Array.Empty<OrganizationCategory>();
        public int TotalFiles { get; set; }
        public long TotalSize { get; set; }
        public int UnclassifiedFiles { get; set; }
    }

    /// <summary>
    /// A category in the organization plan.
    /// </summary>
    public class OrganizationCategory
    {
        public string Name { get; set; } = string.Empty;
        public string DestinationFolder { get; set; } = string.Empty;
        public int FileCount { get; set; }
        public long TotalSize { get; set; }
        public IEnumerable<OrganizationItem> Items { get; set; } = Array.Empty<OrganizationItem>();
        public IEnumerable<OrganizationCategory> SubCategories { get; set; } = Array.Empty<OrganizationCategory>();
    }

    /// <summary>
    /// An item to be organized.
    /// </summary>
    public class OrganizationItem
    {
        public string SourcePath { get; set; } = string.Empty;
        public string DestinationPath { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public long Size { get; set; }
        public string Category { get; set; } = string.Empty;
        public string Extension { get; set; } = string.Empty;
        public DateTime DateModified { get; set; }
        public double Confidence { get; set; }
    }

    /// <summary>
    /// Result of an organization operation.
    /// </summary>
    public class OrganizationResult
    {
        public bool Success { get; set; }
        public string TransactionId { get; set; } = string.Empty;
        public int FilesOrganized { get; set; }
        public int FilesFailed { get; set; }
        public int FilesSkipped { get; set; }
        public long BytesProcessed { get; set; }
        public IEnumerable<string> Errors { get; set; } = Array.Empty<string>();
        public string RollbackScriptPath { get; set; } = string.Empty;
    }

    /// <summary>
    /// Space recovery analysis result.
    /// </summary>
    public class SpaceRecoveryAnalysis
    {
        public string DriveLetter { get; set; } = string.Empty;
        public long TotalSpace { get; set; }
        public long FreeSpace { get; set; }
        public long RecoverableSpace { get; set; }
        public long TotalRecoverableSize { get; set; }
        public IEnumerable<RecoveryOpportunity> Opportunities { get; set; } = Array.Empty<RecoveryOpportunity>();
    }

    /// <summary>
    /// A space recovery opportunity.
    /// </summary>
    public class RecoveryOpportunity
    {
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public long Size { get; set; }
        public int ItemCount { get; set; }
        public RecoveryAction Action { get; set; }
        public RecoveryAction RecommendedAction { get; set; }
        public string Description { get; set; } = string.Empty;
        public string Priority { get; set; } = string.Empty;
        public bool IsSafeToClean { get; set; }
    }

    /// <summary>
    /// Recovery actions.
    /// </summary>
    public enum RecoveryAction
    {
        Delete,
        Relocate,
        Archive,
        Clean,
        Review
    }

    /// <summary>
    /// A group of duplicate files.
    /// </summary>
    public class DuplicateGroup
    {
        public string Key { get; set; } = string.Empty;
        public IEnumerable<DuplicateFile> Files { get; set; } = Array.Empty<DuplicateFile>();
        public long TotalSize { get; set; }
        public long WastedSize { get; set; }
        public int FileCount { get; set; }
    }

    /// <summary>
    /// A duplicate file entry.
    /// </summary>
    public class DuplicateFile
    {
        public string Path { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public long Size { get; set; }
        public DateTime DateModified { get; set; }
        public string? Hash { get; set; }
        public bool IsOriginal { get; set; }
        public bool MarkedForDeletion { get; set; }
    }

    /// <summary>
    /// A custom organization rule.
    /// </summary>
    public class OrganizationRule
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public int Priority { get; set; }
        public bool Enabled { get; set; } = true;
        public IEnumerable<RuleCondition> Conditions { get; set; } = Array.Empty<RuleCondition>();
        public RuleAction Action { get; set; } = new();
    }

    /// <summary>
    /// A condition for an organization rule.
    /// </summary>
    public class RuleCondition
    {
        public ConditionType Type { get; set; }
        public ConditionOperator Operator { get; set; }
        public string Value { get; set; } = string.Empty;
    }

    /// <summary>
    /// Condition types.
    /// </summary>
    public enum ConditionType
    {
        Name,
        Extension,
        Size,
        DateModified,
        DateCreated,
        DateAccessed,
        Location,
        Content
    }

    /// <summary>
    /// Condition operators.
    /// </summary>
    public enum ConditionOperator
    {
        Contains,
        NotContains,
        Equals,
        NotEquals,
        StartsWith,
        EndsWith,
        GreaterThan,
        LessThan,
        Between,
        Matches
    }

    /// <summary>
    /// Action for an organization rule.
    /// </summary>
    public class RuleAction
    {
        public RuleActionType Type { get; set; }
        public string Destination { get; set; } = string.Empty;
        public string RenamePattern { get; set; } = string.Empty;
        public bool CreateSymlink { get; set; }
    }

    /// <summary>
    /// Action types.
    /// </summary>
    public enum RuleActionType
    {
        Move,
        Copy,
        Delete,
        Rename,
        Tag,
        Compress,
        Notify
    }
}
