using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Winhance.WPF.Features.FileManager.ViewModels;

public partial class CreateArchiveViewModel : ObservableObject
{
    private readonly List<string> _sourcePaths;
    private CancellationTokenSource? _cts;

    [ObservableProperty]
    private string _itemsDescription = "";

    [ObservableProperty]
    private string _totalSizeDisplay = "";

    [ObservableProperty]
    private string _archiveName = "";

    [ObservableProperty]
    private string _archiveFormat = ".zip";

    [ObservableProperty]
    private int _compressionLevel = 5;

    [ObservableProperty]
    private string _compressionLevelText = "Normal";

    [ObservableProperty]
    private int _compressionMethod;

    [ObservableProperty]
    private string _password = "";

    [ObservableProperty]
    private string _confirmPassword = "";

    [ObservableProperty]
    private bool _splitArchive;

    [ObservableProperty]
    private string _splitSize = "100";

    [ObservableProperty]
    private int _splitSizeUnit;

    [ObservableProperty]
    private bool _isCreating;

    [ObservableProperty]
    private string _currentFile = "";

    [ObservableProperty]
    private double _progress;

    [ObservableProperty]
    private string _progressText = "";

    [ObservableProperty]
    private bool _canCreate = true;

    public event EventHandler<string>? ArchiveCreated;
    public event EventHandler? CancelRequested;

    public CreateArchiveViewModel(IEnumerable<string> sourcePaths)
    {
        _sourcePaths = sourcePaths.ToList();
        Initialize();
    }

    private void Initialize()
    {
        var fileCount = 0;
        var folderCount = 0;
        long totalSize = 0;

        foreach (var path in _sourcePaths)
        {
            if (File.Exists(path))
            {
                fileCount++;
                totalSize += new FileInfo(path).Length;
            }
            else if (Directory.Exists(path))
            {
                folderCount++;
                try
                {
                    var dirInfo = new DirectoryInfo(path);
                    totalSize += dirInfo.EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length);
                }
                catch { }
            }
        }

        ItemsDescription = _sourcePaths.Count == 1 
            ? Path.GetFileName(_sourcePaths[0]) 
            : $"{fileCount} file(s), {folderCount} folder(s)";
        
        TotalSizeDisplay = FormatSize(totalSize);

        ArchiveName = _sourcePaths.Count == 1 
            ? Path.GetFileNameWithoutExtension(_sourcePaths[0]) 
            : "Archive";
    }

    partial void OnCompressionLevelChanged(int value)
    {
        CompressionLevelText = value switch
        {
            0 => "Store (fastest)",
            1 or 2 or 3 => "Fast",
            4 or 5 or 6 => "Normal",
            7 or 8 => "Maximum",
            9 => "Ultra (slowest)",
            _ => "Normal"
        };
    }

    [RelayCommand]
    private async Task Create()
    {
        if (string.IsNullOrWhiteSpace(ArchiveName))
        {
            return;
        }

        if (!string.IsNullOrEmpty(Password) && Password != ConfirmPassword)
        {
            ProgressText = "Passwords do not match";
            return;
        }

        _cts = new CancellationTokenSource();
        IsCreating = true;
        CanCreate = false;
        Progress = 0;

        try
        {
            var outputDir = Path.GetDirectoryName(_sourcePaths[0]) ?? "";
            var outputPath = Path.Combine(outputDir, ArchiveName + ArchiveFormat);

            if (File.Exists(outputPath))
            {
                outputPath = GetUniqueFileName(outputPath);
            }

            await Task.Run(async () =>
            {
                await CreateArchiveAsync(outputPath, _cts.Token);
            });

            ArchiveCreated?.Invoke(this, outputPath);
        }
        catch (OperationCanceledException)
        {
            ProgressText = "Creation cancelled";
        }
        catch (Exception ex)
        {
            ProgressText = $"Error: {ex.Message}";
        }
        finally
        {
            IsCreating = false;
            CanCreate = true;
        }
    }

    private async Task CreateArchiveAsync(string outputPath, CancellationToken token)
    {
        if (ArchiveFormat == ".zip")
        {
            await CreateZipAsync(outputPath, token);
        }
        else
        {
            throw new NotSupportedException($"Archive format '{ArchiveFormat}' is not supported. Only .zip is currently supported.");
        }
    }

    private async Task CreateZipAsync(string outputPath, CancellationToken token)
    {
        var allFiles = new List<(string SourcePath, string EntryName)>();

        foreach (var sourcePath in _sourcePaths)
        {
            if (File.Exists(sourcePath))
            {
                allFiles.Add((sourcePath, Path.GetFileName(sourcePath)));
            }
            else if (Directory.Exists(sourcePath))
            {
                var basePath = Path.GetDirectoryName(sourcePath) ?? "";
                foreach (var file in Directory.EnumerateFiles(sourcePath, "*", SearchOption.AllDirectories))
                {
                    var entryName = Path.GetRelativePath(basePath, file);
                    allFiles.Add((file, entryName));
                }
            }
        }

        var compressionLevel = CompressionLevel switch
        {
            0 => System.IO.Compression.CompressionLevel.NoCompression,
            1 or 2 or 3 => System.IO.Compression.CompressionLevel.Fastest,
            4 or 5 or 6 => System.IO.Compression.CompressionLevel.Optimal,
            _ => System.IO.Compression.CompressionLevel.SmallestSize
        };

        using var archive = ZipFile.Open(outputPath, ZipArchiveMode.Create);
        var total = allFiles.Count;
        var current = 0;

        foreach (var (sourcePath, entryName) in allFiles)
        {
            token.ThrowIfCancellationRequested();

            CurrentFile = Path.GetFileName(sourcePath);
            current++;
            Progress = (double)current / total * 100;
            ProgressText = $"{current} of {total} files";

            archive.CreateEntryFromFile(sourcePath, entryName, compressionLevel);
            await Task.Yield();
        }

        Progress = 100;
        ProgressText = "Archive created successfully";
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
