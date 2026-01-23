using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Winhance.Core.Features.FileManager.Interfaces
{
    /// <summary>
    /// Service interface for batch rename operations.
    /// </summary>
    public interface IBatchRenameService
    {
        /// <summary>
        /// Generates a preview of rename operations without executing them.
        /// </summary>
        Task<IEnumerable<RenamePreview>> PreviewRenameAsync(IEnumerable<string> files, IEnumerable<RenameRule> rules, CancellationToken cancellationToken = default);

        /// <summary>
        /// Executes the rename operation on the specified files.
        /// </summary>
        Task<RenameResult> ExecuteRenameAsync(IEnumerable<string> files, IEnumerable<RenameRule> rules, CancellationToken cancellationToken = default);

        /// <summary>
        /// Undoes a previous rename operation using its transaction ID.
        /// </summary>
        Task<RenameResult> UndoRenameAsync(string transactionId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets available rename presets.
        /// </summary>
        Task<IEnumerable<RenamePreset>> GetPresetsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Saves a rename preset.
        /// </summary>
        Task SavePresetAsync(RenamePreset preset, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes a rename preset.
        /// </summary>
        Task DeletePresetAsync(string presetName, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Represents a rename rule.
    /// </summary>
    public class RenameRule
    {
        public RenameRuleType Type { get; set; }
        public int Order { get; set; }
        public bool Enabled { get; set; } = true;
        
        // Find & Replace
        public string FindText { get; set; } = string.Empty;
        public string ReplaceText { get; set; } = string.Empty;
        public bool UseRegex { get; set; }
        public bool CaseSensitive { get; set; }
        
        // Add Text
        public string TextToAdd { get; set; } = string.Empty;
        public TextPosition Position { get; set; }
        public int PositionIndex { get; set; }
        
        // Remove Text
        public int RemoveFromIndex { get; set; }
        public int RemoveCount { get; set; }
        public string RemovePattern { get; set; } = string.Empty;
        
        // Counter
        public int CounterStart { get; set; } = 1;
        public int CounterStep { get; set; } = 1;
        public int CounterPadding { get; set; } = 3;
        public string CounterPrefix { get; set; } = string.Empty;
        public string CounterSuffix { get; set; } = string.Empty;
        
        // Case Change
        public CaseType CaseType { get; set; }
        
        // Extension
        public string NewExtension { get; set; } = string.Empty;
        
        // Date/Time
        public string DateFormat { get; set; } = "yyyy-MM-dd";
        public DateSource DateSource { get; set; }
    }

    /// <summary>
    /// Types of rename rules.
    /// </summary>
    public enum RenameRuleType
    {
        FindReplace,
        AddText,
        RemoveText,
        Counter,
        ChangeCase,
        ChangeExtension,
        DateTime,
        MetadataExtract,
        RegularExpression
    }

    /// <summary>
    /// Position for adding text.
    /// </summary>
    public enum TextPosition
    {
        Prefix,
        Suffix,
        AtIndex,
        BeforeExtension
    }

    /// <summary>
    /// Case types for case change rule.
    /// </summary>
    public enum CaseType
    {
        LowerCase,
        UpperCase,
        TitleCase,
        SentenceCase,
        ToggleCase
    }

    /// <summary>
    /// Source for date in rename.
    /// </summary>
    public enum DateSource
    {
        CurrentDate,
        FileCreated,
        FileModified,
        ExifTaken
    }

    /// <summary>
    /// Preview of a rename operation.
    /// </summary>
    public class RenamePreview
    {
        public string OriginalPath { get; set; } = string.Empty;
        public string OriginalName { get; set; } = string.Empty;
        public string NewName { get; set; } = string.Empty;
        public string NewPath { get; set; } = string.Empty;
        public bool HasConflict { get; set; }
        public string ConflictReason { get; set; } = string.Empty;
        public bool WillChange => OriginalName != NewName;
    }

    /// <summary>
    /// Result of a rename operation.
    /// </summary>
    public class RenameResult
    {
        public bool Success { get; set; }
        public string TransactionId { get; set; } = string.Empty;
        public int FilesRenamed { get; set; }
        public int FilesFailed { get; set; }
        public int FilesSkipped { get; set; }
        public IEnumerable<RenameError> Errors { get; set; } = Array.Empty<RenameError>();
    }

    /// <summary>
    /// Error information for a failed rename.
    /// </summary>
    public class RenameError
    {
        public string FilePath { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
    }

    /// <summary>
    /// A saved rename preset.
    /// </summary>
    public class RenamePreset
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public IEnumerable<RenameRule> Rules { get; set; } = Array.Empty<RenameRule>();
        public bool IsBuiltIn { get; set; }
        public DateTime CreatedDate { get; set; }
    }
}
