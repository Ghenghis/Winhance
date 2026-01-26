using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Winhance.WPF.Features.FileManager.Models;

namespace Winhance.WPF.Features.FileManager.ViewModels;

public partial class SearchResultsPanelViewModel : ObservableObject
{
    [ObservableProperty]
    private ObservableCollection<FileItemModel> _results = new();

    [ObservableProperty]
    private FileItemModel? _selectedResult;

    [ObservableProperty]
    private int _resultCount;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private bool _isSearching;

    private CancellationTokenSource? _searchCts;

    public event EventHandler? CloseRequested;
    public event EventHandler<string>? NavigateToPathRequested;

    [RelayCommand]
    private void OpenSelected()
    {
        if (SelectedResult == null) return;

        try
        {
            if (SelectedResult.IsDirectory)
            {
                NavigateToPathRequested?.Invoke(this, SelectedResult.FullPath);
            }
            else
            {
                Process.Start(new ProcessStartInfo(SelectedResult.FullPath) { UseShellExecute = true });
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error opening: {ex.Message}";
        }
    }

    [RelayCommand]
    private void OpenFileLocation()
    {
        if (SelectedResult == null) return;

        try
        {
            var directory = SelectedResult.IsDirectory 
                ? SelectedResult.FullPath 
                : Path.GetDirectoryName(SelectedResult.FullPath);

            if (!string.IsNullOrEmpty(directory))
            {
                NavigateToPathRequested?.Invoke(this, directory);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
    }

    [RelayCommand]
    private void CopySelected()
    {
        var selected = Results.Where(r => r.IsSelected).ToList();
        if (selected.Count == 0 && SelectedResult != null)
        {
            selected.Add(SelectedResult);
        }

        if (selected.Count > 0)
        {
            var paths = selected.Select(s => s.FullPath).ToArray();
            var dataObject = new DataObject();
            dataObject.SetData(DataFormats.FileDrop, paths);
            Clipboard.SetDataObject(dataObject);
            StatusMessage = $"Copied {selected.Count} item(s) to clipboard";
        }
    }

    [RelayCommand]
    private void CopyPath()
    {
        if (SelectedResult != null)
        {
            Clipboard.SetText(SelectedResult.FullPath);
            StatusMessage = "Path copied to clipboard";
        }
    }

    [RelayCommand]
    private void SelectAll()
    {
        foreach (var result in Results)
        {
            result.IsSelected = true;
        }
    }

    [RelayCommand]
    private async Task ExportResults()
    {
        if (Results.Count == 0)
        {
            StatusMessage = "No results to export";
            return;
        }

        try
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "CSV Files (*.csv)|*.csv|Text Files (*.txt)|*.txt",
                DefaultExt = ".csv",
                FileName = $"SearchResults_{DateTime.Now:yyyyMMdd_HHmmss}"
            };

            if (dialog.ShowDialog() == true)
            {
                var lines = Results.Select(r => 
                    $"\"{r.Name}\",\"{r.FullPath}\",\"{r.SizeDisplay}\",\"{r.DateModified:yyyy-MM-dd HH:mm}\"");
                
                var header = "\"Name\",\"Path\",\"Size\",\"Modified\"";
                await File.WriteAllLinesAsync(dialog.FileName, new[] { header }.Concat(lines));
                
                StatusMessage = $"Exported {Results.Count} results to {Path.GetFileName(dialog.FileName)}";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Export failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private void Close()
    {
        CancelSearch();
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    public async Task SearchAsync(string searchPath, string pattern, bool includeSubfolders = true)
    {
        CancelSearch();
        _searchCts = new CancellationTokenSource();
        var token = _searchCts.Token;

        Results.Clear();
        ResultCount = 0;
        IsSearching = true;
        StatusMessage = "Searching...";

        try
        {
            var searchOption = includeSubfolders ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            var files = Directory.EnumerateFiles(searchPath, pattern, searchOption);
            var directories = Directory.EnumerateDirectories(searchPath, pattern, searchOption);

            int count = 0;
            foreach (var path in files.Concat(directories))
            {
                if (token.IsCancellationRequested) break;

                try
                {
                    var item = CreateFileItem(path);
                    if (item != null)
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            Results.Add(item);
                            count++;
                            if (count % 100 == 0)
                            {
                                StatusMessage = $"Found {count} items...";
                            }
                        });
                    }
                }
                catch
                {
                    // Skip inaccessible items
                }

                if (count >= 10000)
                {
                    StatusMessage = "Search limited to 10,000 results";
                    break;
                }
            }

            ResultCount = count;
            StatusMessage = $"Found {count} item(s)";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Search cancelled";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Search error: {ex.Message}";
        }
        finally
        {
            IsSearching = false;
        }
    }

    public void CancelSearch()
    {
        _searchCts?.Cancel();
        _searchCts?.Dispose();
        _searchCts = null;
    }

    private static FileItemModel? CreateFileItem(string path)
    {
        try
        {
            var isDirectory = Directory.Exists(path);
            var info = isDirectory ? (FileSystemInfo)new DirectoryInfo(path) : new FileInfo(path);

            return new FileItemModel
            {
                Name = info.Name,
                FullPath = info.FullName,
                IsDirectory = isDirectory,
                Size = isDirectory ? 0 : ((FileInfo)info).Length,
                DateModified = info.LastWriteTime,
                Icon = isDirectory ? "Folder" : GetFileIcon(info.Extension)
            };
        }
        catch
        {
            return null;
        }
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
        ".cs" or ".py" or ".js" => "FileCode",
        _ => "File"
    };
}
