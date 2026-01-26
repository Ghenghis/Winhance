using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Winhance.WPF.Features.FileManager.ViewModels;

public partial class ExtractArchiveViewModel : ObservableObject
{
    private CancellationTokenSource? _cts;

    [ObservableProperty]
    private string _archivePath = "";

    [ObservableProperty]
    private string _archiveName = "";

    [ObservableProperty]
    private string _archiveInfo = "";

    [ObservableProperty]
    private string _destinationPath = "";

    [ObservableProperty]
    private bool _extractToSubfolder = true;

    [ObservableProperty]
    private int _overwriteMode;

    [ObservableProperty]
    private bool _deleteAfterExtract;

    [ObservableProperty]
    private string _password = "";

    [ObservableProperty]
    private bool _isExtracting;

    [ObservableProperty]
    private string _currentFile = "";

    [ObservableProperty]
    private double _progress;

    [ObservableProperty]
    private string _progressText = "";

    [ObservableProperty]
    private bool _canExtract = true;

    public event EventHandler? ExtractCompleted;
    public event EventHandler? CancelRequested;

    public ExtractArchiveViewModel(string archivePath)
    {
        ArchivePath = archivePath;
        ArchiveName = Path.GetFileName(archivePath);
        DestinationPath = Path.GetDirectoryName(archivePath) ?? "";
        
        var info = new FileInfo(archivePath);
        ArchiveInfo = $"{FormatSize(info.Length)} - {info.Extension.TrimStart('.').ToUpperInvariant()} archive";
    }

    [RelayCommand]
    private void BrowseDestination()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Select Destination Folder",
            InitialDirectory = DestinationPath
        };

        if (dialog.ShowDialog() == true)
        {
            DestinationPath = dialog.FolderName;
        }
    }

    [RelayCommand]
    private async Task Extract()
    {
        if (string.IsNullOrEmpty(DestinationPath))
        {
            return;
        }

        _cts = new CancellationTokenSource();
        IsExtracting = true;
        CanExtract = false;
        Progress = 0;

        try
        {
            var targetPath = ExtractToSubfolder 
                ? Path.Combine(DestinationPath, Path.GetFileNameWithoutExtension(ArchiveName))
                : DestinationPath;

            Directory.CreateDirectory(targetPath);

            await Task.Run(async () =>
            {
                await ExtractArchiveAsync(targetPath, _cts.Token);
            });

            if (DeleteAfterExtract && !_cts.Token.IsCancellationRequested)
            {
                File.Delete(ArchivePath);
            }

            ExtractCompleted?.Invoke(this, EventArgs.Empty);
        }
        catch (OperationCanceledException)
        {
            ProgressText = "Extraction cancelled";
        }
        catch (Exception ex)
        {
            ProgressText = $"Error: {ex.Message}";
        }
        finally
        {
            IsExtracting = false;
            CanExtract = true;
        }
    }

    private async Task ExtractArchiveAsync(string targetPath, CancellationToken token)
    {
        var extension = Path.GetExtension(ArchivePath).ToLowerInvariant();
        
        if (extension == ".zip")
        {
            await ExtractZipAsync(targetPath, token);
        }
        else
        {
            throw new NotSupportedException($"Archive format '{extension}' is not supported. Only .zip is currently supported.");
        }
    }

    private async Task ExtractZipAsync(string targetPath, CancellationToken token)
    {
        using var archive = System.IO.Compression.ZipFile.OpenRead(ArchivePath);
        var total = archive.Entries.Count;
        var current = 0;

        foreach (var entry in archive.Entries)
        {
            token.ThrowIfCancellationRequested();

            CurrentFile = entry.FullName;
            current++;
            Progress = (double)current / total * 100;
            ProgressText = $"{current} of {total} files";

            var destinationPath = Path.Combine(targetPath, entry.FullName);
            var destinationDir = Path.GetDirectoryName(destinationPath);
            
            if (!string.IsNullOrEmpty(destinationDir))
            {
                Directory.CreateDirectory(destinationDir);
            }

            if (string.IsNullOrEmpty(entry.Name))
            {
                continue;
            }

            if (File.Exists(destinationPath))
            {
                switch (OverwriteMode)
                {
                    case 1:
                        break;
                    case 2:
                        continue;
                    case 3:
                        destinationPath = GetUniqueFileName(destinationPath);
                        break;
                    default:
                        break;
                }
            }

            entry.ExtractToFile(destinationPath, true);
            await Task.Yield();
        }

        Progress = 100;
        ProgressText = "Extraction complete";
    }

    private static string GetUniqueFileName(string path)
    {
        var dir = Path.GetDirectoryName(path) ?? "";
        var name = Path.GetFileNameWithoutExtension(path);
        var ext = Path.GetExtension(path);
        var counter = 1;

        while (File.Exists(path))
        {
            path = Path.Combine(dir, $"{name} ({counter}){ext}");
            counter++;
        }

        return path;
    }

    [RelayCommand]
    private void Cancel()
    {
        _cts?.Cancel();
        CancelRequested?.Invoke(this, EventArgs.Empty);
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
}
