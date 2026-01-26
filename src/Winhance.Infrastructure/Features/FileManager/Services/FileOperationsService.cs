using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Winhance.Core.Features.FileManager.Interfaces;

namespace Winhance.Infrastructure.Features.FileManager.Services
{
    /// <summary>
    /// Implementation of basic file operations service
    /// </summary>
    public class FileOperationsService : IFileOperationsService
    {
        private readonly ILogger<FileOperationsService> _logger;

        public FileOperationsService(ILogger<FileOperationsService> logger)
        {
            _logger = logger;
        }

        public async Task<bool> CopyFileAsync(string sourcePath, string destinationPath, bool overwrite = false)
        {
            try
            {
                _logger.LogInformation($"Copying file from {sourcePath} to {destinationPath}");
                
                if (!File.Exists(sourcePath))
                {
                    _logger.LogError($"Source file not found: {sourcePath}");
                    return false;
                }

                // Ensure destination directory exists
                var destinationDir = Path.GetDirectoryName(destinationPath);
                if (!string.IsNullOrEmpty(destinationDir) && !Directory.Exists(destinationDir))
                {
                    Directory.CreateDirectory(destinationDir);
                }

                // Check if destination exists and overwrite is false
                if (File.Exists(destinationPath) && !overwrite)
                {
                    _logger.LogWarning($"Destination file exists and overwrite is false: {destinationPath}");
                    return false;
                }

                // Copy the file
                await Task.Run(() => File.Copy(sourcePath, destinationPath, overwrite));
                
                _logger.LogInformation($"File copied successfully to {destinationPath}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error copying file from {sourcePath} to {destinationPath}");
                return false;
            }
        }

        public async Task<bool> MoveFileAsync(string sourcePath, string destinationPath, bool overwrite = false)
        {
            try
            {
                _logger.LogInformation($"Moving file from {sourcePath} to {destinationPath}");
                
                if (!File.Exists(sourcePath))
                {
                    _logger.LogError($"Source file not found: {sourcePath}");
                    return false;
                }

                // Ensure destination directory exists
                var destinationDir = Path.GetDirectoryName(destinationPath);
                if (!string.IsNullOrEmpty(destinationDir) && !Directory.Exists(destinationDir))
                {
                    Directory.CreateDirectory(destinationDir);
                }

                // Check if destination exists and overwrite is false
                if (File.Exists(destinationPath) && !overwrite)
                {
                    _logger.LogWarning($"Destination file exists and overwrite is false: {destinationPath}");
                    return false;
                }

                // Move the file
                await Task.Run(() => File.Move(sourcePath, destinationPath, overwrite));
                
                _logger.LogInformation($"File moved successfully to {destinationPath}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error moving file from {sourcePath} to {destinationPath}");
                return false;
            }
        }

        public async Task<bool> DeleteFileAsync(string filePath, bool permanent = false)
        {
            try
            {
                _logger.LogInformation($"Deleting file: {filePath}");
                
                if (!File.Exists(filePath))
                {
                    _logger.LogWarning($"File not found: {filePath}");
                    return false;
                }

                if (permanent)
                {
                    await Task.Run(() => File.Delete(filePath));
                }
                else
                {
                    // Move to recycle bin (would need additional library for this)
                    // For now, just delete
                    await Task.Run(() => File.Delete(filePath));
                }
                
                _logger.LogInformation($"File deleted successfully: {filePath}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting file: {filePath}");
                return false;
            }
        }

        public async Task<bool> RenameFileAsync(string filePath, string newName)
        {
            try
            {
                _logger.LogInformation($"Renaming file {filePath} to {newName}");
                
                if (!File.Exists(filePath))
                {
                    _logger.LogError($"File not found: {filePath}");
                    return false;
                }

                var directory = Path.GetDirectoryName(filePath);
                var newPath = Path.Combine(directory ?? string.Empty, newName);

                if (File.Exists(newPath))
                {
                    _logger.LogWarning($"Target file already exists: {newPath}");
                    return false;
                }

                await Task.Run(() => File.Move(filePath, newPath));
                
                _logger.LogInformation($"File renamed successfully to {newPath}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error renaming file {filePath} to {newName}");
                return false;
            }
        }

        public async Task<bool> CreateDirectoryAsync(string path)
        {
            try
            {
                _logger.LogInformation($"Creating directory: {path}");
                
                if (Directory.Exists(path))
                {
                    _logger.LogWarning($"Directory already exists: {path}");
                    return true;
                }

                await Task.Run(() => Directory.CreateDirectory(path));
                
                _logger.LogInformation($"Directory created successfully: {path}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error creating directory: {path}");
                return false;
            }
        }

        public async Task<bool> DeleteDirectoryAsync(string path, bool recursive = false)
        {
            try
            {
                _logger.LogInformation($"Deleting directory: {path}");

                if (!Directory.Exists(path))
                {
                    _logger.LogWarning($"Directory not found: {path}");
                    return false;
                }

                await Task.Run(() => Directory.Delete(path, recursive));

                _logger.LogInformation($"Directory deleted successfully: {path}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting directory: {path}");
                return false;
            }
        }

        public async Task<bool> MoveFilesAsync(string[] sourcePaths, string destinationDirectory, bool overwrite = false)
        {
            try
            {
                _logger.LogInformation($"Moving {sourcePaths.Length} files to {destinationDirectory}");

                // Ensure destination directory exists
                if (!Directory.Exists(destinationDirectory))
                {
                    Directory.CreateDirectory(destinationDirectory);
                }

                var allSuccess = true;
                foreach (var sourcePath in sourcePaths)
                {
                    var fileName = Path.GetFileName(sourcePath);
                    var destinationPath = Path.Combine(destinationDirectory, fileName);

                    var success = await MoveFileAsync(sourcePath, destinationPath, overwrite);
                    if (!success)
                    {
                        allSuccess = false;
                    }
                }

                return allSuccess;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error moving files to {destinationDirectory}");
                return false;
            }
        }

        public async Task<bool> MoveDirectoryAsync(string sourcePath, string destinationPath, bool overwrite = false)
        {
            try
            {
                _logger.LogInformation($"Moving directory from {sourcePath} to {destinationPath}");

                if (!Directory.Exists(sourcePath))
                {
                    _logger.LogError($"Source directory not found: {sourcePath}");
                    return false;
                }

                if (Directory.Exists(destinationPath))
                {
                    if (!overwrite)
                    {
                        _logger.LogWarning($"Destination directory exists and overwrite is false: {destinationPath}");
                        return false;
                    }

                    // Delete existing destination
                    Directory.Delete(destinationPath, true);
                }

                await Task.Run(() => Directory.Move(sourcePath, destinationPath));

                _logger.LogInformation($"Directory moved successfully to {destinationPath}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error moving directory from {sourcePath} to {destinationPath}");
                return false;
            }
        }

        public async Task<bool> CopyDirectoryAsync(string sourcePath, string destinationPath, bool overwrite = false)
        {
            try
            {
                _logger.LogInformation($"Copying directory from {sourcePath} to {destinationPath}");

                if (!Directory.Exists(sourcePath))
                {
                    _logger.LogError($"Source directory not found: {sourcePath}");
                    return false;
                }

                await Task.Run(() => CopyDirectoryRecursive(sourcePath, destinationPath, overwrite));

                _logger.LogInformation($"Directory copied successfully to {destinationPath}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error copying directory from {sourcePath} to {destinationPath}");
                return false;
            }
        }

        private void CopyDirectoryRecursive(string sourceDir, string destinationDir, bool overwrite)
        {
            // Create destination directory
            Directory.CreateDirectory(destinationDir);

            // Copy files
            foreach (var file in Directory.GetFiles(sourceDir))
            {
                var fileName = Path.GetFileName(file);
                var destFile = Path.Combine(destinationDir, fileName);
                File.Copy(file, destFile, overwrite);
            }

            // Recursively copy subdirectories
            foreach (var dir in Directory.GetDirectories(sourceDir))
            {
                var dirName = Path.GetFileName(dir);
                var destDir = Path.Combine(destinationDir, dirName);
                CopyDirectoryRecursive(dir, destDir, overwrite);
            }
        }
    }
}
