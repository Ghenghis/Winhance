using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Winhance.Core.Features.FileManager.Interfaces
{
    /// <summary>
    /// Windows Shell context menu integration service.
    /// Adds powerful features missing from Windows Explorer right-click menu.
    /// </summary>
    public interface IContextMenuService
    {
        /// <summary>
        /// Register all Winhance context menu entries.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task<bool> RegisterContextMenuAsync(ContextMenuRegistration registration);

        /// <summary>
        /// Unregister all Winhance context menu entries.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task<bool> UnregisterContextMenuAsync();

        /// <summary>
        /// Gets a value indicating whether check if context menu is registered.
        /// </summary>
        bool IsRegistered { get; }

        /// <summary>
        /// Get current registration status for all menu items.
        /// </summary>
        /// <returns></returns>
        IEnumerable<ContextMenuItemStatus> GetRegistrationStatus();
    }

    /// <summary>
    /// Context menu registration configuration.
    /// </summary>
    public class ContextMenuRegistration
    {
        public bool EnableFileMenu { get; set; } = true;

        public bool EnableFolderMenu { get; set; } = true;

        public bool EnableDriveMenu { get; set; } = true;

        public bool EnableBackgroundMenu { get; set; } = true;

        public bool UseSubmenu { get; set; } = true; // Group under "Winhance" submenu

        public string SubmenuTitle { get; set; } = "Winhance";

        public List<ContextMenuItem> Items { get; set; } = GetDefaultItems();

        public static List<ContextMenuItem> GetDefaultItems() => new()
        {
            // ====================================================================
            // COPY OPERATIONS - What Windows desperately needs
            // ====================================================================
            new ContextMenuItem
            {
                Id = "copy_path",
                Title = "Copy Path",
                Icon = "📋",
                Command = "copy_path",
                AppliesTo = ContextMenuTarget.All,
                Group = "Copy",
            },
            new ContextMenuItem
            {
                Id = "copy_path_unix",
                Title = "Copy Path (Unix)",
                Icon = "🐧",
                Command = "copy_path_unix",
                AppliesTo = ContextMenuTarget.All,
                Group = "Copy",
            },
            new ContextMenuItem
            {
                Id = "copy_name",
                Title = "Copy Name",
                Icon = "📝",
                Command = "copy_name",
                AppliesTo = ContextMenuTarget.FilesAndFolders,
                Group = "Copy",
            },
            new ContextMenuItem
            {
                Id = "copy_contents",
                Title = "Copy File Contents",
                Icon = "📄",
                Command = "copy_contents",
                AppliesTo = ContextMenuTarget.Files,
                Group = "Copy",
            },

            // ====================================================================
            // TERMINAL OPERATIONS
            // ====================================================================
            new ContextMenuItem
            {
                Id = "open_powershell",
                Title = "Open PowerShell Here",
                Icon = "💻",
                Command = "open_terminal:powershell",
                AppliesTo = ContextMenuTarget.FoldersAndBackground,
                Group = "Terminal",
            },
            new ContextMenuItem
            {
                Id = "open_cmd",
                Title = "Open CMD Here",
                Icon = "⬛",
                Command = "open_terminal:cmd",
                AppliesTo = ContextMenuTarget.FoldersAndBackground,
                Group = "Terminal",
            },
            new ContextMenuItem
            {
                Id = "open_terminal_admin",
                Title = "Open Terminal (Admin)",
                Icon = "🔒",
                Command = "open_terminal_admin",
                AppliesTo = ContextMenuTarget.FoldersAndBackground,
                Group = "Terminal",
                RequiresElevation = true,
            },

            // ====================================================================
            // HASH & VERIFY
            // ====================================================================
            new ContextMenuItem
            {
                Id = "calculate_hash",
                Title = "Calculate Hash",
                Icon = "#️⃣",
                Command = "calculate_hash",
                AppliesTo = ContextMenuTarget.Files,
                Group = "Verify",
            },
            new ContextMenuItem
            {
                Id = "verify_checksum",
                Title = "Verify Checksum",
                Icon = "✅",
                Command = "verify_checksum",
                AppliesTo = ContextMenuTarget.Files,
                Group = "Verify",
            },
            new ContextMenuItem
            {
                Id = "compare_files",
                Title = "Compare Files",
                Icon = "🔍",
                Command = "compare_files",
                AppliesTo = ContextMenuTarget.Files,
                Group = "Verify",
            },

            // ====================================================================
            // ADVANCED OPERATIONS
            // ====================================================================
            new ContextMenuItem
            {
                Id = "find_duplicates",
                Title = "Find Duplicates",
                Icon = "👯",
                Command = "find_duplicates",
                AppliesTo = ContextMenuTarget.Folders,
                Group = "Analyze",
            },
            new ContextMenuItem
            {
                Id = "analyze_space",
                Title = "Analyze Space Usage",
                Icon = "📊",
                Command = "analyze_space",
                AppliesTo = ContextMenuTarget.FoldersAndDrives,
                Group = "Analyze",
            },
            new ContextMenuItem
            {
                Id = "show_folder_size",
                Title = "Show Folder Size",
                Icon = "📁",
                Command = "show_folder_size",
                AppliesTo = ContextMenuTarget.Folders,
                Group = "Analyze",
            },

            // ====================================================================
            // LINK OPERATIONS
            // ====================================================================
            new ContextMenuItem
            {
                Id = "create_symlink",
                Title = "Create Symbolic Link",
                Icon = "🔗",
                Command = "create_symlink",
                AppliesTo = ContextMenuTarget.FilesAndFolders,
                Group = "Links",
                RequiresElevation = true,
            },
            new ContextMenuItem
            {
                Id = "create_hardlink",
                Title = "Create Hard Link",
                Icon = "⛓️",
                Command = "create_hardlink",
                AppliesTo = ContextMenuTarget.Files,
                Group = "Links",
            },
            new ContextMenuItem
            {
                Id = "create_junction",
                Title = "Create Junction",
                Icon = "↪️",
                Command = "create_junction",
                AppliesTo = ContextMenuTarget.Folders,
                Group = "Links",
            },

            // ====================================================================
            // OWNERSHIP & PERMISSIONS
            // ====================================================================
            new ContextMenuItem
            {
                Id = "take_ownership",
                Title = "Take Ownership",
                Icon = "👤",
                Command = "take_ownership",
                AppliesTo = ContextMenuTarget.FilesAndFolders,
                Group = "Security",
                RequiresElevation = true,
            },
            new ContextMenuItem
            {
                Id = "grant_full_control",
                Title = "Grant Full Control",
                Icon = "🔓",
                Command = "grant_full_control",
                AppliesTo = ContextMenuTarget.FilesAndFolders,
                Group = "Security",
                RequiresElevation = true,
            },

            // ====================================================================
            // FILE OPERATIONS
            // ====================================================================
            new ContextMenuItem
            {
                Id = "split_file",
                Title = "Split File",
                Icon = "✂️",
                Command = "split_file",
                AppliesTo = ContextMenuTarget.Files,
                Group = "Operations",
            },
            new ContextMenuItem
            {
                Id = "join_files",
                Title = "Join Files",
                Icon = "🔗",
                Command = "join_files",
                AppliesTo = ContextMenuTarget.Files,
                Group = "Operations",
            },
            new ContextMenuItem
            {
                Id = "secure_delete",
                Title = "Secure Delete",
                Icon = "🗑️",
                Command = "secure_delete",
                AppliesTo = ContextMenuTarget.FilesAndFolders,
                Group = "Operations",
            },
            new ContextMenuItem
            {
                Id = "batch_rename",
                Title = "Batch Rename",
                Icon = "✏️",
                Command = "batch_rename",
                AppliesTo = ContextMenuTarget.FilesAndFolders,
                Group = "Operations",
            },

            // ====================================================================
            // ATTRIBUTES
            // ====================================================================
            new ContextMenuItem
            {
                Id = "toggle_hidden",
                Title = "Toggle Hidden",
                Icon = "👁️",
                Command = "toggle_hidden",
                AppliesTo = ContextMenuTarget.FilesAndFolders,
                Group = "Attributes",
            },
            new ContextMenuItem
            {
                Id = "toggle_readonly",
                Title = "Toggle Read-Only",
                Icon = "🔒",
                Command = "toggle_readonly",
                AppliesTo = ContextMenuTarget.FilesAndFolders,
                Group = "Attributes",
            },
            new ContextMenuItem
            {
                Id = "edit_timestamps",
                Title = "Edit Timestamps",
                Icon = "🕐",
                Command = "edit_timestamps",
                AppliesTo = ContextMenuTarget.FilesAndFolders,
                Group = "Attributes",
            },

            // ====================================================================
            // WINHANCE MAIN
            // ====================================================================
            new ContextMenuItem
            {
                Id = "open_in_winhance",
                Title = "Open in Winhance",
                Icon = "🚀",
                Command = "open_in_winhance",
                AppliesTo = ContextMenuTarget.All,
                Group = "Main",
                IsDefault = true,
            },
        };
    }

    public class ContextMenuItem
    {
        public string Id { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;

        public string Icon { get; set; } = string.Empty;

        public string Command { get; set; } = string.Empty;

        public ContextMenuTarget AppliesTo { get; set; }

        public string Group { get; set; } = string.Empty;

        public bool RequiresElevation { get; set; }

        public bool IsDefault { get; set; }

        public string? HotKey { get; set; }
    }

    [Flags]
    public enum ContextMenuTarget
    {
        None = 0,
        Files = 1,
        Folders = 2,
        Drives = 4,
        Background = 8,
        FilesAndFolders = Files | Folders,
        FoldersAndDrives = Folders | Drives,
        FoldersAndBackground = Folders | Background,
        All = Files | Folders | Drives | Background,
    }

    public class ContextMenuItemStatus
    {
        public string Id { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;

        public bool IsRegistered { get; set; }

        public string RegistryPath { get; set; } = string.Empty;

        public string? Error { get; set; }
    }
}
