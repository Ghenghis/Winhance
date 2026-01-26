using System;
using System.IO;

namespace Winhance.Core.Features.FileManager.Models
{
    /// <summary>
    /// Represents a file system entry (file or directory).
    /// </summary>
    public class FileSystemEntry
    {
        /// <summary>
        /// Gets or sets the name of the entry.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the full path of the entry.
        /// </summary>
        public string FullPath { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets whether this is a directory.
        /// </summary>
        public bool IsDirectory { get; set; }

        /// <summary>
        /// Gets or sets the size in bytes.
        /// </summary>
        public long Size { get; set; }

        /// <summary>
        /// Gets or sets the date modified.
        /// </summary>
        public DateTime DateModified { get; set; }

        /// <summary>
        /// Gets or sets the date created.
        /// </summary>
        public DateTime DateCreated { get; set; }

        /// <summary>
        /// Gets or sets the date last accessed.
        /// </summary>
        public DateTime DateAccessed { get; set; }

        /// <summary>
        /// Gets or sets the file extension.
        /// </summary>
        public string Extension { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the file attributes.
        /// </summary>
        public FileAttributes Attributes { get; set; }

        /// <summary>
        /// Gets or sets the icon path.
        /// </summary>
        public string IconPath { get; set; } = string.Empty;

        /// <summary>
        /// Gets whether this is hidden.
        /// </summary>
        public bool IsHidden => (Attributes & FileAttributes.Hidden) != 0;

        /// <summary>
        /// Gets whether this is a system file.
        /// </summary>
        public bool IsSystem => (Attributes & FileAttributes.System) != 0;

        /// <summary>
        /// Gets whether this is read-only.
        /// </summary>
        public bool IsReadOnly => (Attributes & FileAttributes.ReadOnly) != 0;
    }

    /// <summary>
    /// Drive type enumeration.
    /// </summary>
    public enum DriveType
    {
        Unknown,
        NoRootDirectory,
        Removable,
        Fixed,
        Network,
        CDRom,
        Ram
    }
}
