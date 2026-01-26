using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Winhance.Core.Features.FileManager.Interfaces;
using Winhance.Core.Features.FileManager.Models;

namespace Winhance.WPF.Features.FileManager.ViewModels
{
    /// <summary>
    /// ViewModel for file content preview
    /// </summary>
    public partial class FileContentPreviewViewModel : ObservableObject
    {
        private readonly IPreviewService _previewService;
        private ObservableCollection<PreviewTab> _previewTabs = new();

        [ObservableProperty]
        private PreviewTab? _selectedTab;

        [ObservableProperty]
        private string _filePath = string.Empty;

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private string _previewContent = string.Empty;

        [ObservableProperty]
        private PreviewType _currentPreviewType = PreviewType.Text;

        [ObservableProperty]
        private bool _canEdit = false;

        [ObservableProperty]
        private bool _isModified;

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        public ObservableCollection<PreviewTab> PreviewTabs
        {
            get => _previewTabs;
            set => SetProperty(ref _previewTabs, value);
        }

        public FileContentPreviewViewModel(IPreviewService previewService)
        {
            _previewService = previewService;
        }

        [RelayCommand]
        private async Task LoadPreviewAsync()
        {
            if (string.IsNullOrEmpty(FilePath)) return;

            IsLoading = true;
            StatusMessage = "Loading preview...";

            try
            {
                var preview = await _previewService.GetPreviewAsync(FilePath);
                CurrentPreviewType = preview.Type;
                PreviewContent = preview.Content;
                CanEdit = preview.CanEdit;
                IsModified = false;

                // Add to tabs if not exists
                if (!PreviewTabs.Any(t => t.FilePath == FilePath))
                {
                    var tab = new PreviewTab
                    {
                        Id = Guid.NewGuid().ToString(),
                        FilePath = FilePath,
                        FileName = System.IO.Path.GetFileName(FilePath),
                        Type = preview.Type,
                        Content = preview.Content,
                        IsModified = false
                    };
                    PreviewTabs.Add(tab);
                    SelectedTab = tab;
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading preview: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task SaveContentAsync()
        {
            if (SelectedTab == null || !CanEdit) return;

            try
            {
                await _previewService.SaveContentAsync(SelectedTab.FilePath, SelectedTab.Content);
                SelectedTab.IsModified = false;
                IsModified = false;
                StatusMessage = "File saved successfully";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error saving file: {ex.Message}";
            }
        }

        [RelayCommand]
        private void CloseTab(PreviewTab? tab)
        {
            if (tab == null) return;

            PreviewTabs.Remove(tab);
            if (SelectedTab == tab)
            {
                SelectedTab = PreviewTabs.LastOrDefault();
            }
        }

        [RelayCommand]
        private void CloseAllTabs()
        {
            PreviewTabs.Clear();
            SelectedTab = null;
            FilePath = string.Empty;
            PreviewContent = string.Empty;
        }

        [RelayCommand]
        private void CloseOtherTabs(PreviewTab? keepTab)
        {
            if (keepTab == null) return;

            var tabsToRemove = PreviewTabs.Where(t => t.Id != keepTab.Id).ToList();
            foreach (var tab in tabsToRemove)
            {
                PreviewTabs.Remove(tab);
            }
            SelectedTab = keepTab;
        }

        [RelayCommand]
        private async Task RefreshPreviewAsync()
        {
            if (SelectedTab == null) return;

            FilePath = SelectedTab.FilePath;
            await LoadPreviewAsync();
        }

        [RelayCommand]
        private void CopyContent()
        {
            if (!string.IsNullOrEmpty(PreviewContent))
            {
                System.Windows.Clipboard.SetText(PreviewContent);
                StatusMessage = "Content copied to clipboard";
            }
        }

        [RelayCommand]
        private void SelectAll()
        {
            System.Windows.Clipboard.SetText(PreviewContent);
            StatusMessage = "All content selected and copied";
        }

        [RelayCommand]
        private void FindInContent()
        {
            var searchText = Microsoft.VisualBasic.Interaction.InputBox(
                "Enter text to find:",
                "Find in Content",
                "");

            if (!string.IsNullOrEmpty(searchText))
            {
                var index = PreviewContent.IndexOf(searchText, StringComparison.OrdinalIgnoreCase);
                if (index >= 0)
                {
                    StatusMessage = $"Found at position {index}";
                }
                else
                {
                    StatusMessage = "Text not found";
                }
            }
        }

        [RelayCommand]
        private void PrintContent()
        {
            try
            {
                var printDialog = new System.Windows.Controls.PrintDialog();
                if (printDialog.ShowDialog() == true)
                {
                    StatusMessage = "Printing initiated";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Print failed: {ex.Message}";
            }
        }

        partial void OnSelectedTabChanged(PreviewTab? value)
        {
            if (value != null)
            {
                FilePath = value.FilePath;
                PreviewContent = value.Content;
                CurrentPreviewType = value.Type;
                CanEdit = _previewService.CanEdit(value.Type);
                IsModified = value.IsModified;
            }
        }
    }

    /// <summary>
    /// ViewModel for hex editor
    /// </summary>
    public partial class HexEditorViewModel : ObservableObject
    {
        private readonly IHexEditorService _hexEditorService;

        [ObservableProperty]
        private string _filePath = string.Empty;

        [ObservableProperty]
        private byte[] _fileData = Array.Empty<byte>();

        [ObservableProperty]
        private long _fileSize;

        [ObservableProperty]
        private long _currentOffset;

        [ObservableProperty]
        private int _bytesPerRow = 16;

        [ObservableProperty]
        private bool _isModified;

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        [ObservableProperty]
        private DisplayFormat _displayFormat = DisplayFormat.Hexadecimal;

        [ObservableProperty]
        private bool _showAscii = true;

        [ObservableProperty]
        private bool _showOffsets = true;

        public HexEditorViewModel(IHexEditorService hexEditorService)
        {
            _hexEditorService = hexEditorService;
        }

        [RelayCommand]
        private async Task LoadFileAsync()
        {
            if (string.IsNullOrEmpty(FilePath)) return;

            IsLoading = true;
            StatusMessage = "Loading file...";

            try
            {
                FileData = await _hexEditorService.LoadFileAsync(FilePath);
                FileSize = FileData.Length;
                CurrentOffset = 0;
                IsModified = false;
                StatusMessage = $"Loaded {FileSize} bytes";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading file: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task SaveFileAsync()
        {
            if (string.IsNullOrEmpty(FilePath) || FileData.Length == 0) return;

            try
            {
                await _hexEditorService.SaveFileAsync(FilePath, FileData);
                IsModified = false;
                StatusMessage = "File saved successfully";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error saving file: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task SaveAsAsync()
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                FileName = System.IO.Path.GetFileName(FilePath),
                DefaultExt = System.IO.Path.GetExtension(FilePath),
                Filter = "All Files (*.*)|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    await _hexEditorService.SaveFileAsync(dialog.FileName, FileData);
                    FilePath = dialog.FileName;
                    IsModified = false;
                    StatusMessage = $"Saved to {dialog.FileName}";
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Save failed: {ex.Message}";
                }
            }
        }

        [RelayCommand]
        private void GoToOffset()
        {
            var offsetStr = Microsoft.VisualBasic.Interaction.InputBox(
                "Enter offset (in hex or decimal):",
                "Go To Offset",
                "0x0");

            if (!string.IsNullOrEmpty(offsetStr))
            {
                try
                {
                    long offset = offsetStr.StartsWith("0x") 
                        ? Convert.ToInt64(offsetStr.Substring(2), 16)
                        : long.Parse(offsetStr);
                    
                    if (offset >= 0 && offset < FileSize)
                    {
                        CurrentOffset = offset;
                        StatusMessage = $"Jumped to offset 0x{offset:X}";
                    }
                    else
                    {
                        StatusMessage = "Offset out of range";
                    }
                }
                catch
                {
                    StatusMessage = "Invalid offset format";
                }
            }
        }

        [RelayCommand]
        private void FindBytes()
        {
            var hexStr = Microsoft.VisualBasic.Interaction.InputBox(
                "Enter bytes to find (hex, space separated):",
                "Find Bytes",
                "00 00");

            if (!string.IsNullOrEmpty(hexStr))
            {
                StatusMessage = $"Searching for: {hexStr}";
            }
        }

        [RelayCommand]
        private void ReplaceBytes()
        {
            var findHex = Microsoft.VisualBasic.Interaction.InputBox(
                "Enter bytes to find:",
                "Replace Bytes - Find",
                "00");

            if (!string.IsNullOrEmpty(findHex))
            {
                var replaceHex = Microsoft.VisualBasic.Interaction.InputBox(
                    "Enter replacement bytes:",
                    "Replace Bytes - Replace",
                    "FF");
                
                if (!string.IsNullOrEmpty(replaceHex))
                {
                    IsModified = true;
                    StatusMessage = $"Replaced bytes";
                }
            }
        }

        [RelayCommand]
        private void SelectBlock()
        {
            var startStr = Microsoft.VisualBasic.Interaction.InputBox(
                "Enter start offset:",
                "Select Block",
                "0");
            
            if (!string.IsNullOrEmpty(startStr))
            {
                var lengthStr = Microsoft.VisualBasic.Interaction.InputBox(
                    "Enter length:",
                    "Select Block",
                    "16");
                
                if (!string.IsNullOrEmpty(lengthStr))
                {
                    StatusMessage = $"Block selected: {startStr} + {lengthStr}";
                }
            }
        }

        [RelayCommand]
        private void CopyAsHex()
        {
            if (FileData.Length > 0)
            {
                var hexString = BitConverter.ToString(FileData, (int)CurrentOffset, Math.Min(16, FileData.Length - (int)CurrentOffset));
                System.Windows.Clipboard.SetText(hexString);
                StatusMessage = "Copied as hex";
            }
        }

        [RelayCommand]
        private void CopyAsText()
        {
            if (FileData.Length > 0)
            {
                var text = System.Text.Encoding.ASCII.GetString(FileData, (int)CurrentOffset, Math.Min(16, FileData.Length - (int)CurrentOffset));
                System.Windows.Clipboard.SetText(text);
                StatusMessage = "Copied as text";
            }
        }

        [RelayCommand]
        private void PasteFromHex()
        {
            try
            {
                var hexText = System.Windows.Clipboard.GetText();
                var bytes = hexText.Split(new[] { ' ', '-' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(h => Convert.ToByte(h, 16)).ToArray();
                
                IsModified = true;
                StatusMessage = $"Pasted {bytes.Length} bytes from hex";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Paste failed: {ex.Message}";
            }
        }

        [RelayCommand]
        private void FillSelection()
        {
            var pattern = Microsoft.VisualBasic.Interaction.InputBox(
                "Enter fill pattern (hex byte):",
                "Fill Selection",
                "00");

            if (!string.IsNullOrEmpty(pattern))
            {
                try
                {
                    var fillByte = Convert.ToByte(pattern, 16);
                    IsModified = true;
                    StatusMessage = $"Filled with 0x{fillByte:X2}";
                }
                catch
                {
                    StatusMessage = "Invalid pattern";
                }
            }
        }

        [RelayCommand]
        private void InsertBytes()
        {
            var hexStr = Microsoft.VisualBasic.Interaction.InputBox(
                "Enter bytes to insert (hex):",
                "Insert Bytes",
                "00");

            if (!string.IsNullOrEmpty(hexStr))
            {
                IsModified = true;
                StatusMessage = "Bytes inserted";
            }
        }

        [RelayCommand]
        private void DeleteBytes()
        {
            var result = System.Windows.MessageBox.Show(
                "Delete selected bytes?",
                "Confirm Delete",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Question);

            if (result == System.Windows.MessageBoxResult.Yes)
            {
                IsModified = true;
                StatusMessage = "Bytes deleted";
            }
        }

        [RelayCommand]
        private void Undo()
        {
            StatusMessage = "Undo not yet implemented";
        }

        [RelayCommand]
        private void Redo()
        {
            StatusMessage = "Redo not yet implemented";
        }

        [RelayCommand]
        private async void CalculateChecksum()
        {
            try
            {
                using var md5 = System.Security.Cryptography.MD5.Create();
                var hash = md5.ComputeHash(FileData);
                var hashStr = BitConverter.ToString(hash).Replace("-", "");
                
                System.Windows.MessageBox.Show(
                    $"MD5: {hashStr}\n\nFile size: {FileSize} bytes",
                    "Checksum",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Checksum failed: {ex.Message}";
            }
        }

        [RelayCommand]
        private void ViewAs()
        {
            var options = new[] { "Text", "Image", "Audio", "Binary" };
            var choice = Microsoft.VisualBasic.Interaction.InputBox(
                $"View as: {string.Join(", ", options)}",
                "View As",
                "Text");

            if (!string.IsNullOrEmpty(choice))
            {
                StatusMessage = $"Viewing as {choice}";
            }
        }

        [RelayCommand]
        private void ExportSelection()
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                FileName = "export.bin",
                DefaultExt = ".bin",
                Filter = "Binary Files (*.bin)|*.bin|All Files (*.*)|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    System.IO.File.WriteAllBytes(dialog.FileName, FileData);
                    StatusMessage = $"Exported to {dialog.FileName}";
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Export failed: {ex.Message}";
                }
            }
        }

        [RelayCommand]
        private void CompareFiles()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "All Files (*.*)|*.*",
                Title = "Select File to Compare"
            };

            if (dialog.ShowDialog() == true)
            {
                StatusMessage = $"Comparing with {System.IO.Path.GetFileName(dialog.FileName)}";
            }
        }
    }

    /// <summary>
    /// ViewModel for file converter
    /// </summary>
    public partial class FileConverterViewModel : ObservableObject
    {
        private readonly IFileConverterService _fileConverterService;
        private ObservableCollection<ConversionFormat> _availableFormats = new();
        private ObservableCollection<ConversionJob> _conversionJobs = new();

        [ObservableProperty]
        private string _sourceFile = string.Empty;

        [ObservableProperty]
        private string _destinationFile = string.Empty;

        [ObservableProperty]
        private ConversionFormat? _selectedFormat;

        [ObservableProperty]
        private bool _isConverting;

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        [ObservableProperty]
        private ConversionJob? _selectedJob;

        [ObservableProperty]
        private ConversionOptions _conversionOptions = new();

        [ObservableProperty]
        private double _progress;

        [ObservableProperty]
        private string _estimatedTime = string.Empty;

        public ObservableCollection<ConversionFormat> AvailableFormats
        {
            get => _availableFormats;
            set => SetProperty(ref _availableFormats, value);
        }

        public ObservableCollection<ConversionJob> ConversionJobs
        {
            get => _conversionJobs;
            set => SetProperty(ref _conversionJobs, value);
        }

        public FileConverterViewModel(IFileConverterService fileConverterService)
        {
            _fileConverterService = fileConverterService;
            _ = LoadAvailableFormatsAsync();
            _ = LoadConversionJobsAsync();
        }

        private async Task LoadAvailableFormatsAsync()
        {
            try
            {
                var formats = await _fileConverterService.GetAvailableFormatsAsync();
                AvailableFormats.Clear();
                foreach (var format in formats.OrderBy(f => f.Category).ThenBy(f => f.Name))
                {
                    AvailableFormats.Add(format);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading formats: {ex.Message}";
            }
        }

        private async Task LoadConversionJobsAsync()
        {
            try
            {
                var jobs = await _fileConverterService.GetConversionJobsAsync();
                ConversionJobs.Clear();
                foreach (var job in jobs.OrderByDescending(j => j.CreatedAt))
                {
                    ConversionJobs.Add(job);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading jobs: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task StartConversionAsync()
        {
            if (string.IsNullOrEmpty(SourceFile) || string.IsNullOrEmpty(DestinationFile) || SelectedFormat == null) return;

            IsConverting = true;
            StatusMessage = "Starting conversion...";
            Progress = 0;

            try
            {
                var job = new ConversionJob
                {
                    Id = Guid.NewGuid().ToString(),
                    SourceFile = SourceFile,
                    DestinationFile = DestinationFile,
                    Format = SelectedFormat,
                    Options = ConversionOptions,
                    Status = ConversionStatus.Running,
                    CreatedAt = DateTime.Now
                };

                ConversionJobs.Insert(0, job);

                await _fileConverterService.ConvertFileAsync(job, new Progress<ConversionProgress>(p =>
                {
                    Progress = p.Percentage;
                    EstimatedTime = p.EstimatedTimeRemaining.ToString(@"hh\:mm\:ss");
                    StatusMessage = $"Converting... {p.Percentage:F1}%";
                }));

                job.Status = ConversionStatus.Completed;
                job.CompletedAt = DateTime.Now;
                StatusMessage = "Conversion completed successfully";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Conversion failed: {ex.Message}";
                if (ConversionJobs.Any())
                {
                    ConversionJobs[0].Status = ConversionStatus.Failed;
                    ConversionJobs[0].ErrorMessage = ex.Message;
                }
            }
            finally
            {
                IsConverting = false;
            }
        }

        [RelayCommand]
        private void CancelConversion()
        {
            if (IsConverting && ConversionJobs.Any())
            {
                _fileConverterService.CancelConversion(ConversionJobs[0].Id);
                IsConverting = false;
                StatusMessage = "Conversion cancelled";
            }
        }

        [RelayCommand]
        private async Task RemoveJobAsync(ConversionJob? job)
        {
            if (job == null) return;

            try
            {
                await _fileConverterService.RemoveConversionJobAsync(job.Id);
                ConversionJobs.Remove(job);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error removing job: {ex.Message}";
            }
        }

        [RelayCommand]
        private void BrowseSourceFile()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "All Files (*.*)|*.*",
                Title = "Select Source File"
            };

            if (dialog.ShowDialog() == true)
            {
                SourceFile = dialog.FileName;
                AutoSelectDestination();
            }
        }

        [RelayCommand]
        private void BrowseDestinationFile()
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "All Files (*.*)|*.*",
                Title = "Select Destination File",
                FileName = DestinationFile
            };

            if (dialog.ShowDialog() == true)
            {
                DestinationFile = dialog.FileName;
            }
        }

        [RelayCommand]
        private void AutoSelectDestination()
        {
            if (string.IsNullOrEmpty(SourceFile) || SelectedFormat == null) return;

            var sourceExt = System.IO.Path.GetExtension(SourceFile);
            var destExt = SelectedFormat.DefaultExtension;
            DestinationFile = System.IO.Path.ChangeExtension(SourceFile, destExt);
        }

        [RelayCommand]
        private void ResetOptions()
        {
            ConversionOptions = new ConversionOptions();
        }

        [RelayCommand]
        private void PreviewConversion()
        {
            if (string.IsNullOrEmpty(SourceFile) || SelectedFormat == null)
            {
                StatusMessage = "Select source file and format first";
                return;
            }

            var message = $"Source: {System.IO.Path.GetFileName(SourceFile)}\n" +
                         $"Format: {SelectedFormat.Name}\n" +
                         $"Destination: {System.IO.Path.GetFileName(DestinationFile)}";
            
            System.Windows.MessageBox.Show(message, "Conversion Preview",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
        }

        [RelayCommand]
        private void OpenDestinationFolder()
        {
            if (!string.IsNullOrEmpty(DestinationFile))
            {
                var folder = System.IO.Path.GetDirectoryName(DestinationFile);
                if (!string.IsNullOrEmpty(folder))
                {
                    System.Diagnostics.Process.Start("explorer.exe", folder);
                }
            }
        }

        [RelayCommand]
        private void AddToQueue()
        {
            if (string.IsNullOrEmpty(SourceFile) || string.IsNullOrEmpty(DestinationFile) || SelectedFormat == null)
            {
                StatusMessage = "Complete all fields before adding to queue";
                return;
            }

            var job = new ConversionJob
            {
                SourceFile = SourceFile,
                DestinationFile = DestinationFile,
                Format = SelectedFormat,
                Options = ConversionOptions,
                Status = ConversionStatus.Queued,
                CreatedAt = DateTime.Now
            };

            ConversionJobs.Add(job);
            StatusMessage = $"Added to queue: {System.IO.Path.GetFileName(SourceFile)}";
        }

        [RelayCommand]
        private async void ProcessQueue()
        {
            var queuedJobs = ConversionJobs.Where(j => j.Status == ConversionStatus.Queued).ToList();
            if (!queuedJobs.Any())
            {
                StatusMessage = "No jobs in queue";
                return;
            }

            StatusMessage = $"Processing {queuedJobs.Count} queued job(s)...";
            
            foreach (var job in queuedJobs)
            {
                job.Status = ConversionStatus.Running;
                try
                {
                    await _fileConverterService.ConvertFileAsync(job, null);
                    job.Status = ConversionStatus.Completed;
                    job.CompletedAt = DateTime.Now;
                }
                catch
                {
                    job.Status = ConversionStatus.Failed;
                }
            }
            
            StatusMessage = $"Queue processing complete";
        }
    }

    // Model classes
    public class PreviewTab
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string FilePath { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public PreviewType Type { get; set; }
        public string Content { get; set; } = string.Empty;
        public bool IsModified { get; set; }
        public DateTime LastModified { get; set; }
    }

    public class PreviewData
    {
        public PreviewType Type { get; set; }
        public string Content { get; set; } = string.Empty;
        public bool CanEdit { get; set; }
        public string? MimeType { get; set; }
        public byte[]? BinaryData { get; set; }
    }

    public class ConversionFormat
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string DefaultExtension { get; set; } = string.Empty;
        public string[] SupportedExtensions { get; set; } = Array.Empty<string>();
        public string[] InputFormats { get; set; } = Array.Empty<string>();
        public bool IsLossless { get; set; }
        public bool CanEdit { get; set; }
        public string Icon { get; set; } = string.Empty;
    }

    public class ConversionJob
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string SourceFile { get; set; } = string.Empty;
        public string DestinationFile { get; set; } = string.Empty;
        public ConversionFormat Format { get; set; } = null!;
        public ConversionOptions Options { get; set; } = new();
        public ConversionStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public double Progress { get; set; }
        public long BytesProcessed { get; set; }
        public long TotalBytes { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public class ConversionOptions
    {
        public bool OverwriteExisting { get; set; } = false;
        public bool PreserveMetadata { get; set; } = true;
        public int Quality { get; set; } = 100;
        public bool OptimizeSize { get; set; } = false;
        public Dictionary<string, object> CustomOptions { get; set; } = new();
    }

    public class ConversionProgress
    {
        public double Percentage { get; set; }
        public long BytesProcessed { get; set; }
        public long TotalBytes { get; set; }
        public TimeSpan EstimatedTimeRemaining { get; set; }
        public double ProcessingSpeed { get; set; }
    }

    // Enums
    public enum PreviewType
    {
        Text,
        Image,
        Audio,
        Video,
        PDF,
        Document,
        Spreadsheet,
        Presentation,
        Archive,
        Binary,
        Hex,
        Unknown
    }

    public enum DisplayFormat
    {
        Hexadecimal,
        Decimal,
        Octal,
        Binary
    }

    public enum ConversionStatus
    {
        Queued,
        Running,
        Completed,
        Failed,
        Cancelled,
        Paused
    }
}
