using System.Threading.Tasks;

namespace Winhance.Core.Features.FileManager.Interfaces
{
    /// <summary>
    /// Interface for file operations service
    /// </summary>
    public interface IFileOperationsService
    {
        Task<bool> CopyFileAsync(string sourcePath, string destinationPath, bool overwrite = false);
        Task<bool> MoveFileAsync(string sourcePath, string destinationPath, bool overwrite = false);
        Task<bool> DeleteFileAsync(string filePath, bool permanent = false);
        Task<bool> RenameFileAsync(string filePath, string newName);
        Task<bool> CreateDirectoryAsync(string path);
        Task<bool> DeleteDirectoryAsync(string path, bool recursive = false);
        Task<bool> MoveFilesAsync(string[] sourcePaths, string destinationDirectory, bool overwrite = false);
        Task<bool> MoveDirectoryAsync(string sourcePath, string destinationPath, bool overwrite = false);
        Task<bool> CopyDirectoryAsync(string sourcePath, string destinationPath, bool overwrite = false);
    }
}
