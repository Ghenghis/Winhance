using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Winhance.WPF.Features.FileManager.ViewModels;

public partial class PropertiesDialogViewModel : ObservableObject
{
    private readonly string _path;
    private readonly bool _isFile;

    [ObservableProperty]
    private string _windowTitle = "Properties";

    [ObservableProperty]
    private string _name = "";

    [ObservableProperty]
    private string _icon = "File";

    [ObservableProperty]
    private string _fileType = "";

    [ObservableProperty]
    private string _location = "";

    [ObservableProperty]
    private string _sizeDisplay = "";

    [ObservableProperty]
    private string _sizeOnDiskDisplay = "";

    [ObservableProperty]
    private string _containsDisplay = "";

    [ObservableProperty]
    private DateTime _createdDate;

    [ObservableProperty]
    private DateTime _modifiedDate;

    [ObservableProperty]
    private DateTime _accessedDate;

    [ObservableProperty]
    private bool _isReadOnly;

    [ObservableProperty]
    private bool _isHidden;

    [ObservableProperty]
    private bool _isSystem;

    [ObservableProperty]
    private bool _isArchive;

    [ObservableProperty]
    private bool _isFile;

    [ObservableProperty]
    private bool _isFolder;

    [ObservableProperty]
    private string? _md5Hash;

    [ObservableProperty]
    private string? _sha256Hash;

    [ObservableProperty]
    private bool _hashesNotCalculated = true;

    [ObservableProperty]
    private bool _isCalculatingHashes;

    public event EventHandler? CloseRequested;
    public event EventHandler? ApplyRequested;

    public PropertiesDialogViewModel(string path)
    {
        _path = path;
        _isFile = File.Exists(path);
        LoadProperties();
    }

    private void LoadProperties()
    {
        if (_isFile)
        {
            LoadFileProperties();
        }
        else if (Directory.Exists(_path))
        {
            LoadFolderProperties();
        }
    }

    private void LoadFileProperties()
    {
        var info = new FileInfo(_path);
        
        Name = info.Name;
        WindowTitle = $"{info.Name} Properties";
        Icon = GetFileIcon(info.Extension);
        FileType = $"{info.Extension.TrimStart('.').ToUpperInvariant()} File";
        Location = info.DirectoryName ?? "";
        SizeDisplay = FormatSize(info.Length);
        SizeOnDiskDisplay = FormatSize(GetSizeOnDisk(info));
        CreatedDate = info.CreationTime;
        ModifiedDate = info.LastWriteTime;
        AccessedDate = info.LastAccessTime;
        
        IsFile = true;
        IsFolder = false;

        var attrs = info.Attributes;
        IsReadOnly = (attrs & FileAttributes.ReadOnly) != 0;
        IsHidden = (attrs & FileAttributes.Hidden) != 0;
        IsSystem = (attrs & FileAttributes.System) != 0;
        IsArchive = (attrs & FileAttributes.Archive) != 0;
    }

    private void LoadFolderProperties()
    {
        var info = new DirectoryInfo(_path);
        
        Name = info.Name;
        WindowTitle = $"{info.Name} Properties";
        Icon = "Folder";
        FileType = "Folder";
        Location = info.Parent?.FullName ?? "";
        CreatedDate = info.CreationTime;
        ModifiedDate = info.LastWriteTime;
        AccessedDate = info.LastAccessTime;
        
        IsFile = false;
        IsFolder = true;

        var attrs = info.Attributes;
        IsReadOnly = (attrs & FileAttributes.ReadOnly) != 0;
        IsHidden = (attrs & FileAttributes.Hidden) != 0;
        IsSystem = (attrs & FileAttributes.System) != 0;

        Task.Run(() => CalculateFolderSize(info));
    }

    private async Task CalculateFolderSize(DirectoryInfo info)
    {
        try
        {
            long size = 0;
            int fileCount = 0;
            int folderCount = 0;

            foreach (var file in info.EnumerateFiles("*", SearchOption.AllDirectories))
            {
                try
                {
                    size += file.Length;
                    fileCount++;
                }
                catch { }
            }

            foreach (var dir in info.EnumerateDirectories("*", SearchOption.AllDirectories))
            {
                folderCount++;
            }

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                SizeDisplay = FormatSize(size);
                ContainsDisplay = $"{fileCount:N0} files, {folderCount:N0} folders";
            });
        }
        catch { }
    }

    [RelayCommand]
    private async Task CalculateHashes()
    {
        if (!_isFile) return;

        IsCalculatingHashes = true;
        HashesNotCalculated = false;

        try
        {
            await Task.Run(async () =>
            {
                using var stream = File.OpenRead(_path);
                
                using var md5 = MD5.Create();
                var md5Bytes = await md5.ComputeHashAsync(stream);
                var md5Hash = BitConverter.ToString(md5Bytes).Replace("-", "").ToLowerInvariant();
                
                stream.Position = 0;
                
                using var sha256 = SHA256.Create();
                var sha256Bytes = await sha256.ComputeHashAsync(stream);
                var sha256Hash = BitConverter.ToString(sha256Bytes).Replace("-", "").ToLowerInvariant();

                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    Md5Hash = md5Hash;
                    Sha256Hash = sha256Hash;
                });
            });
        }
        catch (Exception ex)
        {
            Md5Hash = $"Error: {ex.Message}";
            Sha256Hash = $"Error: {ex.Message}";
        }
        finally
        {
            IsCalculatingHashes = false;
        }
    }

    [RelayCommand]
    private void CopyMd5()
    {
        if (!string.IsNullOrEmpty(Md5Hash))
        {
            Clipboard.SetText(Md5Hash);
        }
    }

    [RelayCommand]
    private void CopySha256()
    {
        if (!string.IsNullOrEmpty(Sha256Hash))
        {
            Clipboard.SetText(Sha256Hash);
        }
    }

    [RelayCommand]
    private void Apply()
    {
        try
        {
            var attrs = FileAttributes.Normal;
            if (IsReadOnly) attrs |= FileAttributes.ReadOnly;
            if (IsHidden) attrs |= FileAttributes.Hidden;
            if (IsArchive && _isFile) attrs |= FileAttributes.Archive;

            if (_isFile)
            {
                File.SetAttributes(_path, attrs);
                
                var oldPath = _path;
                var newPath = Path.Combine(Path.GetDirectoryName(_path)!, Name);
                if (oldPath != newPath)
                {
                    File.Move(oldPath, newPath);
                }
            }
            else
            {
                File.SetAttributes(_path, attrs);
            }

            ApplyRequested?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error applying changes: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void Ok()
    {
        Apply();
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void Cancel()
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private static string GetFileIcon(string extension) => extension.ToLowerInvariant() switch
    {
        ".txt" or ".log" or ".md" => "FileDocumentOutline",
        ".doc" or ".docx" => "FileWord",
        ".xls" or ".xlsx" => "FileExcel",
        ".pdf" => "FilePdfBox",
        ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" => "FileImage",
        ".mp3" or ".wav" or ".flac" => "FileMusic",
        ".mp4" or ".avi" or ".mkv" => "FileVideo",
        ".zip" or ".rar" or ".7z" => "FolderZip",
        ".exe" => "Application",
        _ => "File"
    };

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
        return $"{size:0.##} {sizes[order]} ({bytes:N0} bytes)";
    }

    private static long GetSizeOnDisk(FileInfo info)
    {
        var clusterSize = 4096L;
        return ((info.Length + clusterSize - 1) / clusterSize) * clusterSize;
    }
}
