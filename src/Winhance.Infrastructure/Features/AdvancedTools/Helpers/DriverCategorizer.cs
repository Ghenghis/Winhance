using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.Infrastructure.Features.AdvancedTools.Helpers
{
    public static class DriverCategorizer
    {
        private static readonly HashSet<string> StorageClasses = new(StringComparer.OrdinalIgnoreCase)
        {
            "SCSIAdapter",
            "hdc",
            "HDC",
        };

        private static readonly HashSet<string> StorageFileNameKeywords = new(StringComparer.OrdinalIgnoreCase)
        {
            "iaahci",
            "iastor",
            "iastorac",
            "iastora",
            "iastorv",
            "vmd",
            "irst",
            "rst",
        };

        /// <summary>
        /// Validates a directory path for safe file operations.
        /// Prevents path traversal attacks and symlink-based attacks.
        /// </summary>
        private static string ValidateDirectoryPath(string path, string parameterName, ILogService? logService = null)
        {
            ArgumentNullException.ThrowIfNull(path, parameterName);

            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException($"{parameterName} cannot be empty", parameterName);
            }

            // Get the full path to normalize it
            var fullPath = Path.GetFullPath(path);

            // Check for path traversal patterns
            if (path.Contains("..", StringComparison.Ordinal))
            {
                logService?.LogWarning($"Path traversal attempt detected in {parameterName}: {path}");
                throw new ArgumentException($"Path traversal not allowed: {parameterName}", parameterName);
            }

            // Check if the path is a reparse point (symlink/junction)
            if (Directory.Exists(fullPath))
            {
                var dirInfo = new DirectoryInfo(fullPath);
                if (dirInfo.Attributes.HasFlag(FileAttributes.ReparsePoint))
                {
                    logService?.LogWarning($"Symlink/junction detected at {fullPath}, skipping for security");
                    throw new ArgumentException($"Symbolic links and junctions are not allowed: {parameterName}", parameterName);
                }
            }

            return fullPath;
        }

        /// <summary>
        /// Validates a file path for safe file operations.
        /// </summary>
        private static string ValidateFilePath(string path, string parameterName)
        {
            ArgumentNullException.ThrowIfNull(path, parameterName);

            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException($"{parameterName} cannot be empty", parameterName);
            }

            var fullPath = Path.GetFullPath(path);

            if (path.Contains("..", StringComparison.Ordinal))
            {
                throw new ArgumentException($"Path traversal not allowed: {parameterName}", parameterName);
            }

            return fullPath;
        }

        public static bool IsStorageDriver(string infPath, ILogService logService)
        {
            try
            {
                var fileName = Path.GetFileName(infPath).ToLowerInvariant();

                if (StorageFileNameKeywords.Any(keyword => fileName.Contains(keyword)))
                {
                    logService.LogInformation($"Storage driver detected (filename): {Path.GetFileName(infPath)}");
                    return true;
                }

                string fileContent;
                try
                {
                    fileContent = File.ReadAllText(infPath, Encoding.Unicode);
                }
                catch (Exception)
                {
                    fileContent = File.ReadAllText(infPath, Encoding.UTF8);
                }

                using var reader = new StringReader(fileContent);
                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    var trimmedLine = line.Trim();

                    if (trimmedLine.StartsWith("Class", StringComparison.OrdinalIgnoreCase) && trimmedLine.Contains("=", StringComparison.Ordinal))
                    {
                        var parts = trimmedLine.Split('=');
                        if (parts.Length >= 2)
                        {
                            var className = parts[1].Trim();
                            if (StorageClasses.Contains(className))
                            {
                                logService.LogInformation($"Storage driver detected (class={className}): {Path.GetFileName(infPath)}");
                                return true;
                            }
                        }
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                logService.LogWarning($"Could not categorize driver {Path.GetFileName(infPath)}: {ex.Message}");
                return false;
            }
        }

        public static int CategorizeAndCopyDrivers(
            string sourceDirectory,
            string winpeDriverPath,
            string oemDriverPath,
            ILogService logService,
            string? workingDirectoryToExclude = null)
        {
            // Validate input paths
            var validatedSourceDir = ValidateDirectoryPath(sourceDirectory, nameof(sourceDirectory), logService);
            var validatedWinpeDir = ValidateDirectoryPath(winpeDriverPath, nameof(winpeDriverPath), logService);
            var validatedOemDir = ValidateDirectoryPath(oemDriverPath, nameof(oemDriverPath), logService);

            var infFiles = Directory.GetFiles(validatedSourceDir, "*.inf", SearchOption.AllDirectories);

            if (infFiles.Length == 0)
            {
                logService.LogWarning($"No .inf files found in: {sourceDirectory}");
                return 0;
            }

            var validInfFiles = infFiles;

            if (!string.IsNullOrEmpty(workingDirectoryToExclude))
            {
                validInfFiles = infFiles
                    .Where(inf => !inf.StartsWith(workingDirectoryToExclude, StringComparison.OrdinalIgnoreCase))
                    .ToArray();

                int excludedCount = infFiles.Length - validInfFiles.Length;
                if (excludedCount > 0)
                {
                    logService.LogInformation($"Excluded {excludedCount} driver(s) from working directory");
                }
            }

            if (validInfFiles.Length == 0)
            {
                logService.LogWarning("No valid drivers found after filtering");
                return 0;
            }

            logService.LogInformation($"Found {validInfFiles.Length} driver(s) to categorize");
            int copiedCount = 0;
            var processedFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var infFile in validInfFiles)
            {
                try
                {
                    var sourceDir = Path.GetDirectoryName(infFile);
                    if (string.IsNullOrEmpty(sourceDir))
                    {
                        continue;
                    }

                    if (processedFolders.Contains(sourceDir))
                    {
                        continue;
                    }

                    processedFolders.Add(sourceDir);

                    var isStorage = IsStorageDriver(infFile, logService);
                    var targetBase = isStorage ? validatedWinpeDir : validatedOemDir;

                    var folderName = Path.GetFileName(sourceDir);

                    // Sanitize folder name to prevent path injection
                    if (string.IsNullOrWhiteSpace(folderName) || folderName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                    {
                        logService.LogWarning($"Invalid folder name: {folderName}, skipping");
                        continue;
                    }

                    var targetDirectory = Path.Combine(targetBase, folderName);

                    // Verify the combined path is still under the target base
                    var normalizedTarget = Path.GetFullPath(targetDirectory);
                    if (!normalizedTarget.StartsWith(targetBase, StringComparison.OrdinalIgnoreCase))
                    {
                        logService.LogWarning($"Path escape attempt detected: {targetDirectory}");
                        continue;
                    }

                    int counter = 1;
                    while (Directory.Exists(targetDirectory) && counter < 100)
                    {
                        targetDirectory = Path.Combine(targetBase, $"{folderName}_{counter}");
                        counter++;
                    }

                    Directory.CreateDirectory(targetDirectory);

                    foreach (var file in Directory.GetFiles(sourceDir))
                    {
                        var targetFile = Path.Combine(targetDirectory, Path.GetFileName(file));
                        File.Copy(file, targetFile, overwrite: true);
                    }

                    copiedCount++;
                    logService.LogInformation($"Copied driver: {folderName}");
                }
                catch (Exception ex)
                {
                    logService.LogError($"Failed to copy driver {Path.GetFileName(infFile)}: {ex.Message}", ex);
                }
            }

            return copiedCount;
        }

        public static void MergeDriverDirectory(string sourceDirectory, string targetDirectory, ILogService logService)
        {
            // Validate input paths
            var validatedSource = ValidateDirectoryPath(sourceDirectory, nameof(sourceDirectory), logService);
            var validatedTarget = ValidateDirectoryPath(targetDirectory, nameof(targetDirectory), logService);

            if (!Directory.Exists(validatedSource))
            {
                logService.LogWarning($"Source directory does not exist: {validatedSource}");
                return;
            }

            Directory.CreateDirectory(validatedTarget);

            foreach (var file in Directory.GetFiles(validatedSource))
            {
                var fileName = Path.GetFileName(file);

                // Validate file name doesn't contain path separators
                if (string.IsNullOrWhiteSpace(fileName) || fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                {
                    logService.LogWarning($"Invalid file name: {fileName}, skipping");
                    continue;
                }

                var targetFile = Path.Combine(validatedTarget, fileName);

                // Verify target is still under validated directory
                var normalizedTarget = Path.GetFullPath(targetFile);
                if (!normalizedTarget.StartsWith(validatedTarget, StringComparison.OrdinalIgnoreCase))
                {
                    logService.LogWarning($"Path escape attempt in file: {fileName}");
                    continue;
                }

                if (File.Exists(targetFile))
                {
                    logService.LogInformation($"File already exists, skipping: {fileName}");
                    continue;
                }

                File.Copy(file, targetFile, overwrite: false);
            }

            foreach (var dir in Directory.GetDirectories(validatedSource))
            {
                var dirName = Path.GetFileName(dir);

                // Validate directory name
                if (string.IsNullOrWhiteSpace(dirName) || dirName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                {
                    logService.LogWarning($"Invalid directory name: {dirName}, skipping");
                    continue;
                }

                var targetSubDir = Path.Combine(validatedTarget, dirName);
                MergeDriverDirectory(dir, targetSubDir, logService);
            }
        }
    }
}
