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
    /// ViewModel for advanced file operations
    /// </summary>
    public partial class AdvancedFileOperationsViewModel : ObservableObject
    {
        private readonly IFileManagerService _fileManagerService;
        private ObservableCollection<FileOperation> _pendingOperations = new();

        [ObservableProperty]
        private string _operationType = "HardLink";

        [ObservableProperty]
        private string _sourcePath = string.Empty;

        [ObservableProperty]
        private string _destinationPath = string.Empty;

        [ObservableProperty]
        private bool _isProcessing;

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        public ObservableCollection<FileOperation> PendingOperations
        {
            get => _pendingOperations;
            set => SetProperty(ref _pendingOperations, value);
        }

        public AdvancedFileOperationsViewModel(IFileManagerService fileManagerService)
        {
            _fileManagerService = fileManagerService;
        }

        [RelayCommand]
        private async Task CreateHardLinkAsync()
        {
            if (string.IsNullOrEmpty(SourcePath) || string.IsNullOrEmpty(DestinationPath)) return;

            IsProcessing = true;
            StatusMessage = "Creating hard link...";

            try
            {
                await _fileManagerService.CreateHardLinkAsync(SourcePath, DestinationPath);
                StatusMessage = "Hard link created successfully";
                SourcePath = string.Empty;
                DestinationPath = string.Empty;
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
            }
            finally
            {
                IsProcessing = false;
            }
        }

        [RelayCommand]
        private async Task CreateSymbolicLinkAsync()
        {
            if (string.IsNullOrEmpty(SourcePath) || string.IsNullOrEmpty(DestinationPath)) return;

            IsProcessing = true;
            StatusMessage = "Creating symbolic link...";

            try
            {
                await _fileManagerService.CreateSymbolicLinkAsync(SourcePath, DestinationPath);
                StatusMessage = "Symbolic link created successfully";
                SourcePath = string.Empty;
                DestinationPath = string.Empty;
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
            }
            finally
            {
                IsProcessing = false;
            }
        }

        [RelayCommand]
        private async Task CreateJunctionAsync()
        {
            if (string.IsNullOrEmpty(SourcePath) || string.IsNullOrEmpty(DestinationPath)) return;

            IsProcessing = true;
            StatusMessage = "Creating junction...";

            try
            {
                await _fileManagerService.CreateJunctionAsync(SourcePath, DestinationPath);
                StatusMessage = "Junction created successfully";
                SourcePath = string.Empty;
                DestinationPath = string.Empty;
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
            }
            finally
            {
                IsProcessing = false;
            }
        }

        [RelayCommand]
        private async Task EditFileAttributesAsync()
        {
            if (string.IsNullOrEmpty(SourcePath)) return;

            try
            {
                var attributes = await _fileManagerService.GetFileAttributesAsync(SourcePath);
                
                var message = $"File Attributes:\n\n" +
                             $"Archive: {attributes.IsArchive}\n" +
                             $"Hidden: {attributes.IsHidden}\n" +
                             $"Read-Only: {attributes.IsReadOnly}\n" +
                             $"System: {attributes.IsSystem}\n" +
                             $"Compressed: {attributes.IsCompressed}\n" +
                             $"Encrypted: {attributes.IsEncrypted}";
                
                System.Windows.MessageBox.Show(message, "File Attributes",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                
                StatusMessage = "File attributes displayed";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task EditTimestampsAsync()
        {
            if (string.IsNullOrEmpty(SourcePath)) return;

            try
            {
                var timestamps = await _fileManagerService.GetFileTimestampsAsync(SourcePath);
                
                var message = $"File Timestamps:\n\n" +
                             $"Created: {timestamps.Created:g}\n" +
                             $"Modified: {timestamps.Modified:g}\n" +
                             $"Accessed: {timestamps.Accessed:g}";
                
                System.Windows.MessageBox.Show(message, "File Timestamps",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                
                StatusMessage = "File timestamps displayed";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task SplitFileAsync()
        {
            if (string.IsNullOrEmpty(SourcePath)) return;

            IsProcessing = true;
            StatusMessage = "Splitting file...";

            try
            {
                await _fileManagerService.SplitFileAsync(SourcePath, 1024 * 1024 * 100); // 100MB chunks
                StatusMessage = "File split successfully";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
            }
            finally
            {
                IsProcessing = false;
            }
        }

        [RelayCommand]
        private async Task JoinFilesAsync()
        {
            try
            {
                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "Split Files (*.part*)|*.part*|All Files (*.*)|*.*",
                    Multiselect = true,
                    Title = "Select Split Files to Join"
                };

                if (dialog.ShowDialog() == true && dialog.FileNames.Length > 0)
                {
                    IsProcessing = true;
                    StatusMessage = "Joining files...";
                    
                    var outputPath = dialog.FileNames[0].Replace(".part1", "");
                    await _fileManagerService.JoinFilesAsync(dialog.FileNames, outputPath);
                    
                    StatusMessage = $"Files joined successfully: {outputPath}";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Join failed: {ex.Message}";
            }
            finally
            {
                IsProcessing = false;
            }
        }

        [RelayCommand]
        private async Task CalculateChecksumAsync()
        {
            if (string.IsNullOrEmpty(SourcePath)) return;

            IsProcessing = true;
            StatusMessage = "Calculating checksum...";

            try
            {
                var checksum = await _fileManagerService.CalculateChecksumAsync(SourcePath, "SHA256");
                StatusMessage = $"SHA256: {checksum}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
            }
            finally
            {
                IsProcessing = false;
            }
        }

        [RelayCommand]
        private async Task SecureDeleteAsync()
        {
            if (string.IsNullOrEmpty(SourcePath)) return;

            IsProcessing = true;
            StatusMessage = "Securely deleting...";

            try
            {
                await _fileManagerService.SecureDeleteAsync(SourcePath, 3); // 3 passes
                StatusMessage = "File securely deleted";
                SourcePath = string.Empty;
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
            }
            finally
            {
                IsProcessing = false;
            }
        }

        [RelayCommand]
        private async Task WipeFreeSpaceAsync()
        {
            if (string.IsNullOrEmpty(SourcePath)) return;

            IsProcessing = true;
            StatusMessage = "Wiping free space...";

            try
            {
                await _fileManagerService.WipeFreeSpaceAsync(SourcePath);
                StatusMessage = "Free space wiped successfully";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
            }
            finally
            {
                IsProcessing = false;
            }
        }
    }

    /// <summary>
    /// ViewModel for file comparison
    /// </summary>
    public partial class FileComparisonViewModel : ObservableObject
    {
        private readonly IFileManagerService _fileManagerService;
        private ObservableCollection<ComparisonResult> _comparisonResults = new();

        [ObservableProperty]
        private string _leftFilePath = string.Empty;

        [ObservableProperty]
        private string _rightFilePath = string.Empty;

        [ObservableProperty]
        private bool _isComparing;

        [ObservableProperty]
        private ComparisonMode _comparisonMode = ComparisonMode.Content;

        [ObservableProperty]
        private ComparisonResult? _selectedResult;

        public ObservableCollection<ComparisonResult> ComparisonResults
        {
            get => _comparisonResults;
            set => SetProperty(ref _comparisonResults, value);
        }

        public FileComparisonViewModel(IFileManagerService fileManagerService)
        {
            _fileManagerService = fileManagerService;
        }

        [RelayCommand]
        private async Task CompareFilesAsync()
        {
            if (string.IsNullOrEmpty(LeftFilePath) || string.IsNullOrEmpty(RightFilePath)) return;

            IsComparing = true;
            ComparisonResults.Clear();

            try
            {
                var result = await _fileManagerService.CompareFilesAsync(LeftFilePath, RightFilePath, ComparisonMode);
                ComparisonResults.Add(result);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Comparison failed: {ex.Message}",
                    "Comparison Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
            finally
            {
                IsComparing = false;
            }
        }

        [RelayCommand]
        private void ClearComparison()
        {
            LeftFilePath = string.Empty;
            RightFilePath = string.Empty;
            ComparisonResults.Clear();
            SelectedResult = null;
        }

        [RelayCommand]
        private async void ExportResults()
        {
            try
            {
                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "CSV File (*.csv)|*.csv|JSON File (*.json)|*.json|Text File (*.txt)|*.txt",
                    DefaultExt = ".csv",
                    FileName = $"ComparisonResults_{DateTime.Now:yyyyMMdd_HHmmss}"
                };

                if (dialog.ShowDialog() == true)
                {
                    if (dialog.FileName.EndsWith(".json"))
                    {
                        var json = System.Text.Json.JsonSerializer.Serialize(ComparisonResults,
                            new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                        await System.IO.File.WriteAllTextAsync(dialog.FileName, json);
                    }
                    else if (dialog.FileName.EndsWith(".csv"))
                    {
                        var csv = new System.Text.StringBuilder();
                        csv.AppendLine("LeftFile,RightFile,AreEqual,DifferenceCount,ComparisonTime");
                        foreach (var result in ComparisonResults)
                        {
                            csv.AppendLine($"{result.LeftFile},{result.RightFile},{result.AreEqual},{result.Differences.Count},{result.ComparisonTime:g}");
                        }
                        await System.IO.File.WriteAllTextAsync(dialog.FileName, csv.ToString());
                    }
                    else
                    {
                        var text = new System.Text.StringBuilder();
                        foreach (var result in ComparisonResults)
                        {
                            text.AppendLine($"Comparison: {result.LeftFile} vs {result.RightFile}");
                            text.AppendLine($"Equal: {result.AreEqual}");
                            text.AppendLine($"Differences: {result.Differences.Count}");
                            text.AppendLine($"Time: {result.ComparisonTime:g}");
                            text.AppendLine();
                        }
                        await System.IO.File.WriteAllTextAsync(dialog.FileName, text.ToString());
                    }

                    System.Windows.MessageBox.Show(
                        $"Results exported to {dialog.FileName}",
                        "Export Complete",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Export failed: {ex.Message}",
                    "Export Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }
    }

    /// <summary>
    /// ViewModel for file properties
    /// </summary>
    public partial class FilePropertiesViewModel : ObservableObject
    {
        private readonly IFileManagerService _fileManagerService;

        [ObservableProperty]
        private string _filePath = string.Empty;

        [ObservableProperty]
        private FileProperties? _fileProperties;

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private bool _isEditing;

        public FilePropertiesViewModel(IFileManagerService fileManagerService)
        {
            _fileManagerService = fileManagerService;
        }

        [RelayCommand]
        private async Task LoadPropertiesAsync()
        {
            if (string.IsNullOrEmpty(FilePath)) return;

            IsLoading = true;

            try
            {
                FileProperties = await _fileManagerService.GetFilePropertiesAsync(FilePath);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to load properties: {ex.Message}",
                    "Load Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private void StartEdit()
        {
            IsEditing = true;
        }

        [RelayCommand]
        private async Task SaveChangesAsync()
        {
            if (FileProperties == null) return;

            try
            {
                await _fileManagerService.UpdateFilePropertiesAsync(FilePath, FileProperties);
                IsEditing = false;
                System.Windows.MessageBox.Show(
                    "Properties updated successfully.",
                    "Update Complete",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to update properties: {ex.Message}",
                    "Update Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void CancelEdit()
        {
            IsEditing = false;
            _ = LoadPropertiesAsync();
        }

        [RelayCommand]
        private async Task CalculateChecksumsAsync()
        {
            if (string.IsNullOrEmpty(FilePath) || FileProperties == null) return;

            try
            {
                FileProperties.MD5 = await _fileManagerService.CalculateChecksumAsync(FilePath, "MD5");
                FileProperties.SHA1 = await _fileManagerService.CalculateChecksumAsync(FilePath, "SHA1");
                FileProperties.SHA256 = await _fileManagerService.CalculateChecksumAsync(FilePath, "SHA256");
                System.Windows.MessageBox.Show(
                    "Checksums calculated successfully.",
                    "Calculation Complete",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to calculate checksums: {ex.Message}",
                    "Calculation Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }
    }

    /// <summary>
    /// ViewModel for file verification
    /// </summary>
    public partial class FileVerificationViewModel : ObservableObject
    {
        private readonly IFileManagerService _fileManagerService;
        private ObservableCollection<VerificationResult> _verificationResults = new();

        [ObservableProperty]
        private string _filePath = string.Empty;

        [ObservableProperty]
        private string _expectedChecksum = string.Empty;

        [ObservableProperty]
        private ChecksumType _checksumType = ChecksumType.SHA256;

        [ObservableProperty]
        private bool _isVerifying;

        public ObservableCollection<VerificationResult> VerificationResults
        {
            get => _verificationResults;
            set => SetProperty(ref _verificationResults, value);
        }

        public FileVerificationViewModel(IFileManagerService fileManagerService)
        {
            _fileManagerService = fileManagerService;
        }

        [RelayCommand]
        private async Task VerifyFileAsync()
        {
            if (string.IsNullOrEmpty(FilePath)) return;

            IsVerifying = true;

            try
            {
                var actualChecksum = await _fileManagerService.CalculateChecksumAsync(FilePath, ChecksumType.ToString());
                var isValid = string.Equals(actualChecksum, ExpectedChecksum, StringComparison.OrdinalIgnoreCase);

                var result = new VerificationResult
                {
                    FilePath = FilePath,
                    ExpectedChecksum = ExpectedChecksum,
                    ActualChecksum = actualChecksum,
                    ChecksumType = ChecksumType,
                    IsValid = isValid,
                    VerificationTime = DateTime.Now
                };

                VerificationResults.Insert(0, result);
                
                var message = isValid 
                    ? "File verification passed. Checksum matches."
                    : "File verification failed. Checksum mismatch!";
                var icon = isValid 
                    ? System.Windows.MessageBoxImage.Information
                    : System.Windows.MessageBoxImage.Warning;
                
                System.Windows.MessageBox.Show(message, "Verification Result",
                    System.Windows.MessageBoxButton.OK, icon);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Verification failed: {ex.Message}",
                    "Verification Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
            finally
            {
                IsVerifying = false;
            }
        }

        [RelayCommand]
        private void ClearResults()
        {
            VerificationResults.Clear();
        }

        [RelayCommand]
        private async void ExportResults()
        {
            try
            {
                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "CSV File (*.csv)|*.csv|JSON File (*.json)|*.json",
                    DefaultExt = ".csv",
                    FileName = $"VerificationResults_{DateTime.Now:yyyyMMdd_HHmmss}"
                };

                if (dialog.ShowDialog() == true)
                {
                    if (dialog.FileName.EndsWith(".json"))
                    {
                        var json = System.Text.Json.JsonSerializer.Serialize(VerificationResults,
                            new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                        await System.IO.File.WriteAllTextAsync(dialog.FileName, json);
                    }
                    else
                    {
                        var csv = new System.Text.StringBuilder();
                        csv.AppendLine("FilePath,ExpectedChecksum,ActualChecksum,ChecksumType,IsValid,VerificationTime");
                        foreach (var result in VerificationResults)
                        {
                            csv.AppendLine($"{result.FilePath},{result.ExpectedChecksum},{result.ActualChecksum},{result.ChecksumType},{result.IsValid},{result.VerificationTime:g}");
                        }
                        await System.IO.File.WriteAllTextAsync(dialog.FileName, csv.ToString());
                    }

                    System.Windows.MessageBox.Show(
                        $"Verification results exported to {dialog.FileName}",
                        "Export Complete",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Export failed: {ex.Message}",
                    "Export Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }
    }

    // Model classes
    public class FileOperation
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Type { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public string Destination { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public OperationStatus Status { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public class ComparisonResult
    {
        public string LeftFile { get; set; } = string.Empty;
        public string RightFile { get; set; } = string.Empty;
        public bool AreEqual { get; set; }
        public List<string> Differences { get; set; } = new();
        public DateTime ComparisonTime { get; set; }
    }

    public class FileProperties
    {
        public string Name { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public long Size { get; set; }
        public DateTime Created { get; set; }
        public DateTime Modified { get; set; }
        public DateTime Accessed { get; set; }
        public FileAttributes Attributes { get; set; }
        public string? MD5 { get; set; }
        public string? SHA1 { get; set; }
        public string? SHA256 { get; set; }
        public string? Version { get; set; }
        public string? Company { get; set; }
        public string? Description { get; set; }
    }

    public class VerificationResult
    {
        public string FilePath { get; set; } = string.Empty;
        public string ExpectedChecksum { get; set; } = string.Empty;
        public string ActualChecksum { get; set; } = string.Empty;
        public ChecksumType ChecksumType { get; set; }
        public bool IsValid { get; set; }
        public DateTime VerificationTime { get; set; }
    }

    // Enums
    public enum ComparisonMode
    {
        Content,
        Size,
        Checksum,
        Attributes,
        Timestamps
    }

    public enum ChecksumType
    {
        MD5,
        SHA1,
        SHA256,
        SHA512
    }

    public enum OperationStatus
    {
        Pending,
        Running,
        Completed,
        Failed
    }
}
