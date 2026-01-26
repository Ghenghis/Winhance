using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Winhance.Core.Features.FileManager.Interfaces;
using Winhance.WPF.Features.FileManager.Controls;

namespace Winhance.WPF.Features.FileManager.ViewModels
{
    public partial class TreeMapViewModel : ObservableObject
    {
        private readonly IFileManagerService _fileManagerService;

        [ObservableProperty]
        private ObservableCollection<TreeMapItem> _treeMapItems = new();

        [ObservableProperty]
        private string _currentPath = string.Empty;

        [ObservableProperty]
        private string _totalSize = "0 B";

        [ObservableProperty]
        private int _itemCount;

        [ObservableProperty]
        private string _statusMessage = "Ready to analyze";

        [ObservableProperty]
        private bool _isAnalyzing;

        [ObservableProperty]
        private double _analysisProgress;

        [ObservableProperty]
        private string _largestItemSize = "0 B";

        [ObservableProperty]
        private int _folderCount;

        [ObservableProperty]
        private int _fileCount;

        [ObservableProperty]
        private string? _selectedDrive;

        [ObservableProperty]
        private bool _showFiles = true;

        [ObservableProperty]
        private bool _scanSubfolders = true;

        [ObservableProperty]
        private bool _showHidden = false;

        [ObservableProperty]
        private int _selectedDepth = 3;

        public List<string> Drives { get; } = new();
        public List<int> DepthOptions { get; } = new() { 1, 2, 3, 4, 5, 10 };

        public TreeMapViewModel(IFileManagerService fileManagerService)
        {
            _fileManagerService = fileManagerService;
            LoadDrives();
        }

        private void LoadDrives()
        {
            var drives = DriveInfo.GetDrives()
                .Where(d => d.IsReady && d.DriveType == DriveType.Fixed)
                .Select(d => d.Name.Substring(0, 2))
                .ToList();

            Drives.Clear();
            foreach (var drive in drives)
            {
                Drives.Add(drive);
            }

            if (Drives.Count > 0)
            {
                SelectedDrive = Drives[0];
            }
        }

        [RelayCommand]
        private async Task AnalyzeAsync()
        {
            if (string.IsNullOrEmpty(SelectedDrive))
            {
                StatusMessage = "Please select a drive to analyze";
                return;
            }

            IsAnalyzing = true;
            StatusMessage = "Analyzing...";
            AnalysisProgress = 0;
            TreeMapItems.Clear();

            try
            {
                var rootPath = SelectedDrive + "\\";
                CurrentPath = rootPath;

                var items = await AnalyzeDirectoryAsync(rootPath);
                TreeMapItems = new ObservableCollection<TreeMapItem>(items);

                TotalSize = FormatSize(items.Sum(i => i.Size));
                ItemCount = items.Count;
                FolderCount = items.Count(i => i.IsDirectory);
                FileCount = items.Count(i => !i.IsDirectory);

                var largest = items.OrderByDescending(i => i.Size).FirstOrDefault();
                LargestItemSize = largest != null ? FormatSize(largest.Size) : "0 B";

                StatusMessage = $"Analysis complete - {items.Count} items";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
                Debug.WriteLine($"TreeMap analysis error: {ex}");
            }
            finally
            {
                IsAnalyzing = false;
            }
        }

        private async Task<List<TreeMapItem>> AnalyzeDirectoryAsync(string path, int currentDepth = 0)
        {
            var items = new List<TreeMapItem>();

            try
            {
                var dirInfo = new DirectoryInfo(path);
                if (!dirInfo.Exists) return items;

                // Get directories
                foreach (var dir in dirInfo.GetDirectories())
                {
                    if (!ShowHidden && dir.Attributes.HasFlag(FileAttributes.Hidden))
                        continue;

                    if (currentDepth < SelectedDepth && ScanSubfolders)
                    {
                        var subItems = await AnalyzeDirectoryAsync(dir.FullName, currentDepth + 1);
                        var totalSize = subItems.Sum(i => i.Size);
                        var itemCount = subItems.Count + 1; // +1 for the folder itself

                        items.Add(new TreeMapItem
                        {
                            Name = dir.Name,
                            Path = dir.FullName,
                            Size = totalSize,
                            ItemCount = itemCount,
                            Category = GetCategory(dir.Extension),
                            IsDirectory = true
                        });
                    }
                    else
                    {
                        items.Add(new TreeMapItem
                        {
                            Name = dir.Name,
                            Path = dir.FullName,
                            Size = await GetDirectorySizeAsync(dir),
                            ItemCount = 1,
                            Category = GetCategory(dir.Extension),
                            IsDirectory = true
                        });
                    }
                }

                // Get files if enabled
                if (ShowFiles)
                {
                    foreach (var file in dirInfo.GetFiles())
                    {
                        if (!ShowHidden && file.Attributes.HasFlag(FileAttributes.Hidden))
                            continue;

                        items.Add(new TreeMapItem
                        {
                            Name = file.Name,
                            Path = file.FullName,
                            Size = file.Length,
                            ItemCount = 1,
                            Category = GetCategory(file.Extension),
                            IsDirectory = false
                        });
                    }
                }

                // Update progress
                AnalysisProgress = Math.Min(100, (currentDepth + 1) * 20);
            }
            catch (UnauthorizedAccessException)
            {
                // Skip directories we can't access
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error analyzing {path}: {ex}");
            }

            return items.OrderByDescending(i => i.Size).ToList();
        }

        private async Task<long> GetDirectorySizeAsync(DirectoryInfo dir)
        {
            try
            {
                return await Task.Run(() =>
                {
                    long size = 0;
                    try
                    {
                        size += dir.EnumerateFiles().Sum(f => f.Length);
                        if (ScanSubfolders)
                        {
                            size += dir.EnumerateDirectories().Sum(d => GetDirectorySizeAsync(d).Result);
                        }
                    }
                    catch
                    {
                        // Ignore access errors
                    }
                    return size;
                });
            }
            catch
            {
                return 0;
            }
        }

        private string GetCategory(string extension)
        {
            extension = extension.ToLowerInvariant();
            return extension switch
            {
                ".doc" or ".docx" or ".pdf" or ".txt" or ".rtf" => "Documents",
                ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".tiff" => "Images",
                ".mp4" or ".avi" or ".mkv" or ".mov" or ".wmv" or ".flv" => "Videos",
                ".mp3" or ".wav" or ".flac" or ".aac" or ".ogg" => "Audio",
                ".zip" or ".rar" or ".7z" or ".tar" or ".gz" => "Archives",
                ".cs" or ".cpp" or ".h" or ".py" or ".js" or ".html" or ".css" => "Code",
                ".exe" or ".dll" or ".msi" or ".app" => "Applications",
                _ => "Other"
            };
        }

        private static string FormatSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            int order = 0;
            double size = bytes;
            while (size >= 1024 && order < sizes.Length - 1)
            {
                order++;
                size /= 1024;
            }
            return $"{size:0.##} {sizes[order]}";
        }

        [RelayCommand]
        private void Export()
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "HTML Report (*.html)|*.html|CSV File (*.csv)|*.csv",
                DefaultExt = ".html",
                FileName = $"TreeMap_{SelectedDrive?.Replace(":", "")}_{DateTime.Now:yyyyMMdd_HHmmss}.html"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var html = $"<html><head><title>TreeMap Analysis</title></head><body>" +
                               $"<h1>TreeMap Analysis: {CurrentPath}</h1>" +
                               $"<p>Total Size: {TotalSize}</p>" +
                               $"<p>Items: {ItemCount} ({FolderCount} folders, {FileCount} files)</p>" +
                               $"<p>Largest Item: {LargestItemSize}</p>" +
                               "<h2>Items</h2><ul>";
                    
                    foreach (var item in TreeMapItems.Take(50))
                    {
                        html += $"<li>{item.Name}: {FormatSize(item.Size)}</li>";
                    }
                    
                    html += "</ul></body></html>";
                    File.WriteAllText(dialog.FileName, html);
                    StatusMessage = $"Exported to {dialog.FileName}";
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Export failed: {ex.Message}";
                }
            }
        }

        public void OnItemClicked(TreeMapItem item)
        {
            StatusMessage = $"Selected: {item.Name} - {FormatSize(item.Size)}";
        }

        public void OnItemDoubleClicked(TreeMapItem item)
        {
            if (item.IsDirectory)
            {
                // Drill down into directory
                CurrentPath = item.Path;
                StatusMessage = $"Drilling into: {item.Name}";
                _ = AnalyzeAsync();
            }
            else
            {
                // Open file
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = item.Path,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Error opening file: {ex.Message}";
                }
            }
        }
    }
}
