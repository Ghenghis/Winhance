using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Winhance.WPF.Features.FileManager.ViewModels;

public partial class PreviewPanelViewModel : ObservableObject
{
    private static readonly string[] ImageExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".ico", ".tiff" };
    private static readonly string[] TextExtensions = { ".txt", ".md", ".json", ".xml", ".csv", ".log", ".ini", ".config", ".cs", ".xaml", ".html", ".css", ".js", ".ts", ".py", ".rs", ".toml", ".yaml", ".yml" };
    private static readonly string[] MediaExtensions = { ".mp4", ".avi", ".mkv", ".mov", ".wmv", ".mp3", ".wav", ".flac", ".ogg", ".m4a" };

    [ObservableProperty]
    private string? _filePath;

    [ObservableProperty]
    private string? _fileName;

    [ObservableProperty]
    private string? _fileSizeDisplay;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _noPreviewAvailable;

    [ObservableProperty]
    private string? _noPreviewReason;

    [ObservableProperty]
    private bool _isImagePreview;

    [ObservableProperty]
    private BitmapImage? _imageSource;

    [ObservableProperty]
    private double _zoomLevel = 1.0;

    [ObservableProperty]
    private bool _isTextPreview;

    [ObservableProperty]
    private string? _textContent;

    [ObservableProperty]
    private bool _isMediaPreview;

    [ObservableProperty]
    private Uri? _mediaSource;

    [ObservableProperty]
    private bool _isPlaying;

    [ObservableProperty]
    private bool _isMuted;

    [ObservableProperty]
    private double _mediaDuration;

    [ObservableProperty]
    private double _mediaPosition;

    [ObservableProperty]
    private string? _mediaTimeDisplay;

    [ObservableProperty]
    private bool _isHexPreview;

    [ObservableProperty]
    private string? _hexOffsets;

    [ObservableProperty]
    private string? _hexContent;

    [ObservableProperty]
    private string? _asciiContent;

    public async Task LoadPreviewAsync(string? path)
    {
        ResetPreview();

        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            NoPreviewAvailable = true;
            NoPreviewReason = "No file selected";
            return;
        }

        FilePath = path;
        FileName = Path.GetFileName(path);
        IsLoading = true;

        try
        {
            var fileInfo = new FileInfo(path);
            FileSizeDisplay = FormatFileSize(fileInfo.Length);
            var extension = Path.GetExtension(path).ToLowerInvariant();

            if (Array.Exists(ImageExtensions, e => e == extension))
            {
                await LoadImagePreviewAsync(path);
            }
            else if (Array.Exists(TextExtensions, e => e == extension))
            {
                await LoadTextPreviewAsync(path);
            }
            else if (Array.Exists(MediaExtensions, e => e == extension))
            {
                LoadMediaPreview(path);
            }
            else if (fileInfo.Length < 1024 * 1024)
            {
                await LoadHexPreviewAsync(path);
            }
            else
            {
                NoPreviewAvailable = true;
                NoPreviewReason = "File type not supported for preview";
            }
        }
        catch (Exception ex)
        {
            NoPreviewAvailable = true;
            NoPreviewReason = $"Error loading preview: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ResetPreview()
    {
        IsImagePreview = false;
        IsTextPreview = false;
        IsMediaPreview = false;
        IsHexPreview = false;
        NoPreviewAvailable = false;
        NoPreviewReason = null;
        ImageSource = null;
        TextContent = null;
        MediaSource = null;
        HexContent = null;
        HexOffsets = null;
        AsciiContent = null;
        ZoomLevel = 1.0;
        IsPlaying = false;
    }

    private async Task LoadImagePreviewAsync(string path)
    {
        await Task.Run(() =>
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(path);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();

            Application.Current.Dispatcher.Invoke(() =>
            {
                ImageSource = bitmap;
                IsImagePreview = true;
            });
        });
    }

    private async Task LoadTextPreviewAsync(string path)
    {
        var content = await File.ReadAllTextAsync(path);
        if (content.Length > 100000)
        {
            content = content[..100000] + "\n\n[Truncated - file too large]";
        }
        TextContent = content;
        IsTextPreview = true;
    }

    private void LoadMediaPreview(string path)
    {
        MediaSource = new Uri(path);
        IsMediaPreview = true;
        MediaTimeDisplay = "00:00 / 00:00";
    }

    private async Task LoadHexPreviewAsync(string path)
    {
        const int bytesPerLine = 16;
        const int maxBytes = 4096;

        var bytes = await File.ReadAllBytesAsync(path);
        var displayBytes = bytes.Length > maxBytes ? bytes[..maxBytes] : bytes;

        var offsets = new StringBuilder();
        var hex = new StringBuilder();
        var ascii = new StringBuilder();

        for (int i = 0; i < displayBytes.Length; i += bytesPerLine)
        {
            offsets.AppendLine($"{i:X8}");

            var lineBytes = Math.Min(bytesPerLine, displayBytes.Length - i);
            var hexLine = new StringBuilder();
            var asciiLine = new StringBuilder();

            for (int j = 0; j < bytesPerLine; j++)
            {
                if (j < lineBytes)
                {
                    var b = displayBytes[i + j];
                    hexLine.Append($"{b:X2} ");
                    asciiLine.Append(b >= 32 && b < 127 ? (char)b : '.');
                }
                else
                {
                    hexLine.Append("   ");
                }
            }

            hex.AppendLine(hexLine.ToString());
            ascii.AppendLine(asciiLine.ToString());
        }

        if (bytes.Length > maxBytes)
        {
            offsets.AppendLine("...");
            hex.AppendLine("[Truncated]");
            ascii.AppendLine("");
        }

        HexOffsets = offsets.ToString();
        HexContent = hex.ToString();
        AsciiContent = ascii.ToString();
        IsHexPreview = true;
    }

    public void OnMediaOpened(Duration duration)
    {
        if (duration.HasTimeSpan)
        {
            MediaDuration = duration.TimeSpan.TotalSeconds;
            UpdateMediaTimeDisplay();
        }
    }

    public void OnMediaEnded()
    {
        IsPlaying = false;
        MediaPosition = 0;
    }

    private void UpdateMediaTimeDisplay()
    {
        var current = TimeSpan.FromSeconds(MediaPosition);
        var total = TimeSpan.FromSeconds(MediaDuration);
        MediaTimeDisplay = $"{current:mm\\:ss} / {total:mm\\:ss}";
    }

    [RelayCommand]
    private void PlayPause()
    {
        IsPlaying = !IsPlaying;
    }

    [RelayCommand]
    private void ToggleMute()
    {
        IsMuted = !IsMuted;
    }

    [RelayCommand]
    private async Task RefreshPreview()
    {
        if (!string.IsNullOrEmpty(FilePath))
        {
            await LoadPreviewAsync(FilePath);
        }
    }

    [RelayCommand]
    private void ClosePreview()
    {
        ResetPreview();
        FilePath = null;
        FileName = null;
        FileSizeDisplay = null;
    }

    [RelayCommand]
    private void ZoomIn()
    {
        ZoomLevel = Math.Min(ZoomLevel * 1.25, 5.0);
    }

    [RelayCommand]
    private void ZoomOut()
    {
        ZoomLevel = Math.Max(ZoomLevel / 1.25, 0.1);
    }

    [RelayCommand]
    private void ZoomReset()
    {
        ZoomLevel = 1.0;
    }

    private static string FormatFileSize(long bytes)
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
