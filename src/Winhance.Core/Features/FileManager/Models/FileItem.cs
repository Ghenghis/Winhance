using System;

namespace Winhance.Core.Features.FileManager.Models
{
    /// <summary>
    /// Base model for a file or directory item in the file browser.
    /// </summary>
    public class FileItem
    {
        /// <summary>
        /// Gets or sets the name of the item.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the full path of the item.
        /// </summary>
        public string FullPath { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the size in bytes.
        /// </summary>
        public long Size { get; set; }

        /// <summary>
        /// Gets or sets the last modified date.
        /// </summary>
        public DateTime LastModified { get; set; }

        /// <summary>
        /// Gets or sets the creation date.
        /// </summary>
        public DateTime Created { get; set; }

        /// <summary>
        /// Gets or sets the creation date (alias for Created).
        /// </summary>
        public DateTime CreatedDate => Created;

        /// <summary>
        /// Gets or sets the last accessed date.
        /// </summary>
        public DateTime AccessedDate { get; set; }

        /// <summary>
        /// Gets or sets the file extension.
        /// </summary>
        public string Extension { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the file attributes.
        /// </summary>
        public System.IO.FileAttributes Attributes { get; set; }

        /// <summary>
        /// Gets or sets whether this is a directory.
        /// </summary>
        public bool IsDirectory { get; set; }

        /// <summary>
        /// Gets or sets whether this is hidden.
        /// </summary>
        public bool IsHidden { get; set; }

        /// <summary>
        /// Gets or sets whether this is a system file.
        /// </summary>
        public bool IsSystem { get; set; }

        /// <summary>
        /// Gets or sets whether this is read-only.
        /// </summary>
        public bool IsReadOnly { get; set; }

        /// <summary>
        /// Gets or sets whether this is the parent directory entry ("..").
        /// </summary>
        public bool IsParentDirectory { get; set; }
    }
}
