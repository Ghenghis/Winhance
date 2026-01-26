using System;

namespace Winhance.Core.Features.FileManager.Models
{
    /// <summary>
    /// Represents a file system item (file or directory)
    /// </summary>
    public class FileSystemItem
    {
        public string Name { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public bool IsDirectory { get; set; }
        public long Size { get; set; }
        public DateTime ModifiedDate { get; set; }
        public DateTime CreatedDate { get; set; }
        public string Icon => IsDirectory ? "📁" : GetFileIcon();
        public string DisplaySize => IsDirectory ? "<Folder>" : FormatFileSize(Size);

        private string GetFileIcon()
        {
            var ext = System.IO.Path.GetExtension(Name).ToLower();
            return ext switch
            {
                ".txt" => "📄",
                ".pdf" => "📕",
                ".doc" or ".docx" => "📘",
                ".xls" or ".xlsx" => "📗",
                ".jpg" or ".jpeg" or ".png" or ".gif" => "🖼️",
                ".mp4" or ".avi" or ".mkv" => "🎬",
                ".mp3" or ".wav" or ".flac" => "🎵",
                ".zip" or ".rar" or ".7z" => "📦",
                _ => "📄"
            };
        }

        private string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }
    }
}
