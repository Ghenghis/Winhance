using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Winhance.Core.Features.FileManager.Interfaces;
using Winhance.Core.Features.FileManager.Models;

namespace Winhance.WPF.Features.FileManager.ViewModels
{
    /// <summary>
    /// ViewModel for image preview
    /// </summary>
    public partial class ImagePreviewViewModel : ObservableObject
    {
        private readonly IPreviewService _previewService;
        private string? _currentImagePath;
        private double _zoomLevel = 1.0;
        private double _rotation = 0.0;
        private bool _isLoading;

        [ObservableProperty]
        private object? _imageSource;

        [ObservableProperty]
        private ImageMetadata _metadata = new();

        public double ZoomLevel
        {
            get => _zoomLevel;
            set => SetProperty(ref _zoomLevel, Math.Max(0.1, Math.Min(10.0, value)));
        }

        public double Rotation
        {
            get => _rotation;
            set => SetProperty(ref _rotation, value % 360);
        }

        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public ImagePreviewViewModel(IPreviewService previewService)
        {
            _previewService = previewService;
        }

        [RelayCommand]
        private async Task LoadImageAsync(string? imagePath)
        {
            if (string.IsNullOrEmpty(imagePath)) return;

            CurrentImagePath = imagePath;
            IsLoading = true;

            try
            {
                var preview = await _previewService.GetImagePreviewAsync(imagePath);
                ImageSource = preview.ImageSource;
                Metadata = preview.Metadata;
                ZoomLevel = 1.0;
                Rotation = 0.0;
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to load image: {ex.Message}",
                    "Load Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
                ImageSource = null;
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private void ZoomIn()
        {
            ZoomLevel *= 1.2;
        }

        [RelayCommand]
        private void ZoomOut()
        {
            ZoomLevel /= 1.2;
        }

        [RelayCommand]
        private void ZoomToFit()
        {
            ZoomLevel = 1.0;
            Rotation = 0.0;
        }

        [RelayCommand]
        private void ZoomActual()
        {
            ZoomLevel = 1.0;
        }

        [RelayCommand]
        private void RotateLeft()
        {
            Rotation -= 90;
        }

        [RelayCommand]
        private void RotateRight()
        {
            Rotation += 90;
        }

        [RelayCommand]
        private void FlipHorizontal()
        {
            Rotation = (Rotation + 180) % 360;
        }

        [RelayCommand]
        private void FlipVertical()
        {
            Rotation = (360 - Rotation) % 360;
        }

        [RelayCommand]
        private async Task SaveImageAsync()
        {
            if (string.IsNullOrEmpty(CurrentImagePath)) return;

            try
            {
                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    FileName = System.IO.Path.GetFileName(CurrentImagePath),
                    DefaultExt = System.IO.Path.GetExtension(CurrentImagePath),
                    Filter = "Image Files (*.png;*.jpg;*.bmp)|*.png;*.jpg;*.bmp|All Files (*.*)|*.*"
                };

                if (dialog.ShowDialog() == true)
                {
                    System.IO.File.Copy(CurrentImagePath, dialog.FileName, true);
                    System.Windows.MessageBox.Show(
                        "Image saved successfully.",
                        "Save Complete",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to save image: {ex.Message}",
                    "Save Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }
    }

    /// <summary>
    /// ViewModel for document preview
    /// </summary>
    public partial class DocumentPreviewViewModel : ObservableObject
    {
        private readonly IPreviewService _previewService;
        private int _currentPage = 1;
        private int _totalPages = 1;
        private bool _isLoading;

        [ObservableProperty]
        private object? _documentContent;

        [ObservableProperty]
        private DocumentMetadata _metadata = new();

        public int CurrentPage
        {
            get => _currentPage;
            set
            {
                if (SetProperty(ref _currentPage, value))
                {
                    _ = LoadPageAsync(value);
                }
            }
        }

        public int TotalPages
        {
            get => _totalPages;
            set => SetProperty(ref _totalPages, value);
        }

        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public DocumentPreviewViewModel(IPreviewService previewService)
        {
            _previewService = previewService;
        }

        [RelayCommand]
        private async Task LoadDocumentAsync(string? documentPath)
        {
            if (string.IsNullOrEmpty(documentPath)) return;

            IsLoading = true;

            try
            {
                var preview = await _previewService.GetDocumentPreviewAsync(documentPath);
                DocumentContent = preview.Content;
                Metadata = preview.Metadata;
                TotalPages = preview.PageCount;
                CurrentPage = 1;
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to load document: {ex.Message}",
                    "Load Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
                DocumentContent = null;
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task LoadPageAsync(int pageNumber)
        {
            if (pageNumber < 1 || pageNumber > TotalPages) return;

            IsLoading = true;

            try
            {
                var pageContent = await _previewService.GetDocumentPageAsync(Metadata.FilePath, pageNumber);
                DocumentContent = pageContent;
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to load page: {ex.Message}",
                    "Page Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private void NextPage()
        {
            if (CurrentPage < TotalPages)
            {
                CurrentPage++;
            }
        }

        [RelayCommand]
        private void PreviousPage()
        {
            if (CurrentPage > 1)
            {
                CurrentPage--;
            }
        }

        [RelayCommand]
        private void FirstPage()
        {
            CurrentPage = 1;
        }

        [RelayCommand]
        private void LastPage()
        {
            CurrentPage = TotalPages;
        }

        [RelayCommand]
        private async Task PrintDocumentAsync()
        {
            try
            {
                await _previewService.PrintDocumentAsync(Metadata.FilePath);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to print document: {ex.Message}",
                    "Print Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }
    }

    /// <summary>
    /// ViewModel for media preview
    /// </summary>
    public partial class MediaPreviewViewModel : ObservableObject
    {
        private readonly IPreviewService _previewService;
        private bool _isPlaying;
        private double _volume = 1.0;
        private TimeSpan _duration;
        private TimeSpan _position;

        [ObservableProperty]
        private object? _mediaSource;

        [ObservableProperty]
        private MediaMetadata _metadata = new();

        public bool IsPlaying
        {
            get => _isPlaying;
            set => SetProperty(ref _isPlaying, value);
        }

        public double Volume
        {
            get => _volume;
            set => SetProperty(ref _volume, Math.Max(0.0, Math.Min(1.0, value)));
        }

        public TimeSpan Duration
        {
            get => _duration;
            set => SetProperty(ref _duration, value);
        }

        public TimeSpan Position
        {
            get => _position;
            set => SetProperty(ref _position, value);
        }

        public MediaPreviewViewModel(IPreviewService previewService)
        {
            _previewService = previewService;
        }

        [RelayCommand]
        private async Task LoadMediaAsync(string? mediaPath)
        {
            if (string.IsNullOrEmpty(mediaPath)) return;

            try
            {
                var preview = await _previewService.GetMediaPreviewAsync(mediaPath);
                MediaSource = preview.MediaSource;
                Metadata = preview.Metadata;
                Duration = preview.Duration;
                Position = TimeSpan.Zero;
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to load media: {ex.Message}",
                    "Load Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
                MediaSource = null;
            }
        }

        [RelayCommand]
        private void PlayPause()
        {
            IsPlaying = !IsPlaying;
        }

        [RelayCommand]
        private void Stop()
        {
            IsPlaying = false;
            Position = TimeSpan.Zero;
        }

        [RelayCommand]
        private void SeekForward()
        {
            Position = TimeSpan.FromSeconds(Math.Min(Duration.TotalSeconds, Position.TotalSeconds + 10));
        }

        [RelayCommand]
        private void SeekBackward()
        {
            Position = TimeSpan.FromSeconds(Math.Max(0, Position.TotalSeconds - 10));
        }

        [RelayCommand]
        private void MuteUnmute()
        {
            Volume = Volume > 0 ? 0 : 1.0;
        }

        [RelayCommand]
        private void ToggleFullscreen()
        {
            System.Windows.MessageBox.Show(
                "Fullscreen mode toggle\n\nPress F11 to toggle fullscreen in the media player.",
                "Fullscreen",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
        }
    }

    /// <summary>
    /// ViewModel for Quick Look (Spacebar preview)
    /// </summary>
    public partial class QuickLookViewModel : ObservableObject
    {
        private readonly IQuickLookService _quickLookService;
        private FileItem? _currentFile;
        private bool _isVisible;

        [ObservableProperty]
        private object? _previewContent;

        [ObservableProperty]
        private string? _previewType;

        public FileItem? CurrentFile
        {
            get => _currentFile;
            set => SetProperty(ref _currentFile, value);
        }

        public bool IsVisible
        {
            get => _isVisible;
            set => SetProperty(ref _isVisible, value);
        }

        public QuickLookViewModel(IQuickLookService quickLookService)
        {
            _quickLookService = quickLookService;
        }

        [RelayCommand]
        private async Task ShowQuickLookAsync(FileItem? file)
        {
            if (file == null) return;

            CurrentFile = file;
            IsVisible = true;

            try
            {
                var preview = await _quickLookService.GetQuickLookAsync(file.FullPath);
                PreviewContent = preview.Content;
                PreviewType = preview.Type;
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to load quick look preview: {ex.Message}",
                    "Preview Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
                PreviewContent = null;
                PreviewType = null;
            }
        }

        [RelayCommand]
        private void HideQuickLook()
        {
            IsVisible = false;
            PreviewContent = null;
            PreviewType = null;
            CurrentFile = null;
        }

        [RelayCommand]
        private async Task OpenFileAsync()
        {
            if (CurrentFile == null) return;

            try
            {
                await System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = CurrentFile.FullPath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to open file: {ex.Message}",
                    "Open Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void OpenContainingFolder()
        {
            if (CurrentFile == null) return;

            try
            {
                System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{CurrentFile.FullPath}\"");
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to open containing folder: {ex.Message}",
                    "Explorer Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }
    }

    /// <summary>
    /// ViewModel for hex viewer
    /// </summary>
    public partial class HexViewerViewModel : ObservableObject
    {
        private readonly IPreviewService _previewService;
        private long _fileSize;
        private long _currentOffset;
        private int _bytesPerLine = 16;

        [ObservableProperty]
        private ObservableCollection<HexLine> _hexLines = new();

        [ObservableProperty]
        private string? _currentPath;

        public long FileSize
        {
            get => _fileSize;
            set => SetProperty(ref _fileSize, value);
        }

        public long CurrentOffset
        {
            get => _currentOffset;
            set
            {
                if (SetProperty(ref _currentOffset, value))
                {
                    _ = LoadHexViewAsync(value);
                }
            }
        }

        public int BytesPerLine
        {
            get => _bytesPerLine;
            set
            {
                if (SetProperty(ref _bytesPerLine, value))
                {
                    _ = LoadHexViewAsync(CurrentOffset);
                }
            }
        }

        public HexViewerViewModel(IPreviewService previewService)
        {
            _previewService = previewService;
        }

        [RelayCommand]
        private async Task LoadFileAsync(string? filePath)
        {
            if (string.IsNullOrEmpty(filePath)) return;

            CurrentPath = filePath;
            CurrentOffset = 0;

            try
            {
                var fileInfo = new FileInfo(filePath);
                FileSize = fileInfo.Length;
                await LoadHexViewAsync(0);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to load file for hex view: {ex.Message}",
                    "Load Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
                HexLines.Clear();
            }
        }

        private async Task LoadHexViewAsync(long offset)
        {
            if (string.IsNullOrEmpty(CurrentPath)) return;

            try
            {
                var hexData = await _previewService.GetHexViewAsync(CurrentPath, offset, BytesPerLine * 50);
                
                HexLines.Clear();
                for (int i = 0; i < hexData.Bytes.Length; i += BytesPerLine)
                {
                    var line = new HexLine
                    {
                        Offset = offset + i,
                        Bytes = hexData.Bytes.Skip(i).Take(BytesPerLine).ToArray(),
                        Text = System.Text.Encoding.ASCII.GetString(hexData.Bytes, i, Math.Min(BytesPerLine, hexData.Bytes.Length - i))
                    };
                    HexLines.Add(line);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to load hex data: {ex.Message}",
                    "Hex View Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void ScrollUp()
        {
            CurrentOffset = Math.Max(0, CurrentOffset - BytesPerLine * 10);
        }

        [RelayCommand]
        private void ScrollDown()
        {
            CurrentOffset = Math.Min(FileSize - BytesPerLine, CurrentOffset + BytesPerLine * 10);
        }

        [RelayCommand]
        private void GoToStart()
        {
            CurrentOffset = 0;
        }

        [RelayCommand]
        private void GoToEnd()
        {
            CurrentOffset = Math.Max(0, FileSize - BytesPerLine * 50);
        }
    }

    // Model classes
    public class ImageMetadata
    {
        public string FilePath { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime ModifiedDate { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public string Format { get; set; } = string.Empty;
        public string Camera { get; set; } = string.Empty;
        public DateTime? DateTaken { get; set; }
        public string? ExifData { get; set; }
    }

    public class DocumentMetadata
    {
        public string FilePath { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime ModifiedDate { get; set; }
        public int PageCount { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Author { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
    }

    public class MediaMetadata
    {
        public string FilePath { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime ModifiedDate { get; set; }
        public TimeSpan Duration { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Artist { get; set; } = string.Empty;
        public string Album { get; set; } = string.Empty;
        public int Bitrate { get; set; }
        public string Codec { get; set; } = string.Empty;
        public int Width { get; set; }
        public int Height { get; set; }
        public double FrameRate { get; set; }
    }

    public class HexLine
    {
        public long Offset { get; set; }
        public byte[] Bytes { get; set; } = Array.Empty<byte>();
        public string Text { get; set; } = string.Empty;
        public string HexString => BitConverter.ToString(Bytes).Replace("-", " ").PadRight(48);
    }
}
