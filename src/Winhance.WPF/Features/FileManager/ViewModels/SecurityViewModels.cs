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
    /// ViewModel for file security and permissions
    /// </summary>
    public partial class FileSecurityViewModel : ObservableObject
    {
        private readonly ISecurityService _securityService;
        private ObservableCollection<FilePermission> _permissions = new();
        private ObservableCollection<FileAuditEntry> _auditEntries = new();

        [ObservableProperty]
        private string _filePath = string.Empty;

        [ObservableProperty]
        private FileSecurityInfo? _securityInfo;

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private bool _isEditing;

        [ObservableProperty]
        private string? _selectedOwner;

        [ObservableProperty]
        private bool _inheritFromParent = true;

        public ObservableCollection<FilePermission> Permissions
        {
            get => _permissions;
            set => SetProperty(ref _permissions, value);
        }

        public ObservableCollection<FileAuditEntry> AuditEntries
        {
            get => _auditEntries;
            set => SetProperty(ref _auditEntries, value);
        }

        public FileSecurityViewModel(ISecurityService securityService)
        {
            _securityService = securityService;
        }

        [RelayCommand]
        private async Task LoadSecurityInfoAsync()
        {
            if (string.IsNullOrEmpty(FilePath)) return;

            IsLoading = true;

            try
            {
                SecurityInfo = await _securityService.GetFileSecurityInfoAsync(FilePath);
                SelectedOwner = SecurityInfo.Owner;
                InheritFromParent = SecurityInfo.InheritFromParent;

                Permissions.Clear();
                foreach (var permission in SecurityInfo.Permissions)
                {
                    Permissions.Add(permission);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to load security info: {ex.Message}",
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
        private void CancelEdit()
        {
            IsEditing = false;
            _ = LoadSecurityInfoAsync();
        }

        [RelayCommand]
        private async Task SaveChangesAsync()
        {
            if (SecurityInfo == null) return;

            try
            {
                SecurityInfo.Owner = SelectedOwner;
                SecurityInfo.InheritFromParent = InheritFromParent;
                SecurityInfo.Permissions = Permissions.ToList();

                await _securityService.UpdateFileSecurityAsync(FilePath, SecurityInfo);
                IsEditing = false;
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to save security changes: {ex.Message}",
                    "Save Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task AddPermissionAsync()
        {
            var userOrGroup = Microsoft.VisualBasic.Interaction.InputBox(
                "Enter user or group name:",
                "Add Permission",
                "");

            if (!string.IsNullOrEmpty(userOrGroup))
            {
                var permission = new FilePermission
                {
                    UserOrGroup = userOrGroup,
                    Rights = FileSystemRights.Read,
                    Type = AccessControlType.Allow
                };
                Permissions.Add(permission);
            }
            await Task.CompletedTask;
        }

        [RelayCommand]
        private async Task RemovePermissionAsync(FilePermission? permission)
        {
            if (permission == null) return;

            Permissions.Remove(permission);
        }

        [RelayCommand]
        private async Task EditPermissionAsync(FilePermission? permission)
        {
            if (permission == null) return;

            var message = $"Permission for: {permission.UserOrGroup}\n\n" +
                         $"Rights: {permission.Rights}\n" +
                         $"Type: {permission.Type}\n" +
                         $"Inherited: {permission.IsInherited}";

            System.Windows.MessageBox.Show(message, "Permission Details",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
            await Task.CompletedTask;
        }

        [RelayCommand]
        private async Task DisableInheritanceAsync()
        {
            if (SecurityInfo == null) return;

            try
            {
                await _securityService.DisableInheritanceAsync(FilePath);
                InheritFromParent = false;
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to disable inheritance: {ex.Message}",
                    "Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task EnableInheritanceAsync()
        {
            if (SecurityInfo == null) return;

            try
            {
                await _securityService.EnableInheritanceAsync(FilePath);
                InheritFromParent = true;
                await LoadSecurityInfoAsync();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to enable inheritance: {ex.Message}",
                    "Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task TakeOwnershipAsync()
        {
            if (string.IsNullOrEmpty(FilePath)) return;

            try
            {
                await _securityService.TakeOwnershipAsync(FilePath);
                await LoadSecurityInfoAsync();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to take ownership: {ex.Message}",
                    "Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task LoadAuditEntriesAsync()
        {
            if (string.IsNullOrEmpty(FilePath)) return;

            try
            {
                var entries = await _securityService.GetAuditEntriesAsync(FilePath);
                AuditEntries.Clear();
                foreach (var entry in entries.Take(100)) // Last 100 entries
                {
                    AuditEntries.Add(entry);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to load audit entries: {ex.Message}",
                    "Load Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void ExportSecurityInfo()
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "JSON File (*.json)|*.json|Text File (*.txt)|*.txt",
                DefaultExt = ".json",
                FileName = $"SecurityInfo_{DateTime.Now:yyyyMMdd_HHmmss}.json"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var json = System.Text.Json.JsonSerializer.Serialize(SecurityInfo,
                        new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                    System.IO.File.WriteAllText(dialog.FileName, json);
                    System.Windows.MessageBox.Show(
                        "Security information exported successfully.",
                        "Export Complete",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Information);
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
    }

    /// <summary>
    /// ViewModel for file encryption
    /// </summary>
    public partial class FileEncryptionViewModel : ObservableObject
    {
        private readonly IEncryptionService _encryptionService;

        [ObservableProperty]
        private string _filePath = string.Empty;

        [ObservableProperty]
        private bool _isEncrypted;

        [ObservableProperty]
        private bool _isProcessing;

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        [ObservableProperty]
        private EncryptionType _encryptionType = EncryptionType.AES256;

        [ObservableProperty]
        private string _password = string.Empty;

        [ObservableProperty]
        private string _confirmPassword = string.Empty;

        [ObservableProperty]
        private ObservableCollection<string> _encryptedFiles = new();

        public FileEncryptionViewModel(IEncryptionService encryptionService)
        {
            _encryptionService = encryptionService;
        }

        [RelayCommand]
        private async Task CheckEncryptionStatusAsync()
        {
            if (string.IsNullOrEmpty(FilePath)) return;

            try
            {
                IsEncrypted = await _encryptionService.IsFileEncryptedAsync(FilePath);
                StatusMessage = IsEncrypted ? "File is encrypted" : "File is not encrypted";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task EncryptFileAsync()
        {
            if (string.IsNullOrEmpty(FilePath) || string.IsNullOrEmpty(Password)) return;

            if (Password != ConfirmPassword)
            {
                StatusMessage = "Passwords do not match";
                return;
            }

            IsProcessing = true;
            StatusMessage = "Encrypting file...";

            try
            {
                await _encryptionService.EncryptFileAsync(FilePath, Password, EncryptionType);
                IsEncrypted = true;
                StatusMessage = "File encrypted successfully";
                EncryptedFiles.Add(FilePath);
                Password = string.Empty;
                ConfirmPassword = string.Empty;
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
        private async Task DecryptFileAsync()
        {
            if (string.IsNullOrEmpty(FilePath) || string.IsNullOrEmpty(Password)) return;

            IsProcessing = true;
            StatusMessage = "Decrypting file...";

            try
            {
                await _encryptionService.DecryptFileAsync(FilePath, Password);
                IsEncrypted = false;
                StatusMessage = "File decrypted successfully";
                EncryptedFiles.Remove(FilePath);
                Password = string.Empty;
                ConfirmPassword = string.Empty;
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
        private async Task EncryptFolderAsync()
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Select Folder to Encrypt"
            };

            if (dialog.ShowDialog() == true)
            {
                StatusMessage = $"Folder encryption for {dialog.FolderName} will be implemented";
            }
            await Task.CompletedTask;
        }

        [RelayCommand]
        private async Task CreateEncryptedContainerAsync()
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Encrypted Container (*.ecnt)|*.ecnt",
                DefaultExt = ".ecnt",
                FileName = "EncryptedContainer.ecnt"
            };

            if (dialog.ShowDialog() == true)
            {
                StatusMessage = $"Encrypted container creation at {dialog.FileName} will be implemented";
            }
            await Task.CompletedTask;
        }

        [RelayCommand]
        private void ClearPassword()
        {
            Password = string.Empty;
            ConfirmPassword = string.Empty;
        }

        [RelayCommand]
        private void GeneratePassword()
        {
            var password = _encryptionService.GenerateSecurePassword(16);
            Password = password;
            ConfirmPassword = password;
        }
    }

    /// <summary>
    /// ViewModel for file backup and recovery
    /// </summary>
    public partial class FileBackupViewModel : ObservableObject
    {
        private readonly IBackupService _backupService;
        private ObservableCollection<BackupJob> _backupJobs = new();
        private ObservableCollection<BackupSet> _backupSets = new();

        [ObservableProperty]
        private string _sourcePath = string.Empty;

        [ObservableProperty]
        private string _destinationPath = string.Empty;

        [ObservableProperty]
        private BackupType _backupType = BackupType.Full;

        [ObservableProperty]
        private bool _isCreatingBackup;

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        [ObservableProperty]
        private BackupJob? _selectedJob;

        [ObservableProperty]
        private bool _includeHiddenFiles = true;

        [ObservableProperty]
        private bool _compressBackup = true;

        [ObservableProperty]
        private bool _encryptBackup = false;

        public ObservableCollection<BackupJob> BackupJobs
        {
            get => _backupJobs;
            set => SetProperty(ref _backupJobs, value);
        }

        public ObservableCollection<BackupSet> BackupSets
        {
            get => _backupSets;
            set => SetProperty(ref _backupSets, value);
        }

        public FileBackupViewModel(IBackupService backupService)
        {
            _backupService = backupService;
            _ = LoadBackupJobsAsync();
            _ = LoadBackupSetsAsync();
        }

        private async Task LoadBackupJobsAsync()
        {
            try
            {
                var jobs = await _backupService.GetBackupJobsAsync();
                BackupJobs.Clear();
                foreach (var job in jobs.OrderByDescending(j => j.CreatedDate))
                {
                    BackupJobs.Add(job);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to load backup jobs: {ex.Message}",
                    "Load Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        private async Task LoadBackupSetsAsync()
        {
            try
            {
                var sets = await _backupService.GetBackupSetsAsync();
                BackupSets.Clear();
                foreach (var set in sets.OrderByDescending(s => s.CreatedDate))
                {
                    BackupSets.Add(set);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to load backup sets: {ex.Message}",
                    "Load Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task CreateBackupAsync()
        {
            if (string.IsNullOrEmpty(SourcePath) || string.IsNullOrEmpty(DestinationPath)) return;

            IsCreatingBackup = true;
            StatusMessage = "Creating backup...";

            try
            {
                var options = new BackupOptions
                {
                    IncludeHiddenFiles = IncludeHiddenFiles,
                    Compress = CompressBackup,
                    Encrypt = EncryptBackup,
                    Type = BackupType
                };

                var job = await _backupService.CreateBackupAsync(SourcePath, DestinationPath, options);
                BackupJobs.Insert(0, job);
                
                StatusMessage = "Backup created successfully";
                SourcePath = string.Empty;
                DestinationPath = string.Empty;
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
            }
            finally
            {
                IsCreatingBackup = false;
            }
        }

        [RelayCommand]
        private async Task RestoreBackupAsync(BackupSet? backupSet)
        {
            if (backupSet == null) return;

            try
            {
                await _backupService.RestoreBackupAsync(backupSet.Id, SourcePath);
                StatusMessage = "Backup restored successfully";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task DeleteBackupJobAsync(BackupJob? job)
        {
            if (job == null) return;

            try
            {
                await _backupService.DeleteBackupJobAsync(job.Id);
                BackupJobs.Remove(job);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to delete backup job: {ex.Message}",
                    "Delete Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task DeleteBackupSetAsync(BackupSet? backupSet)
        {
            if (backupSet == null) return;

            try
            {
                await _backupService.DeleteBackupSetAsync(backupSet.Id);
                BackupSets.Remove(backupSet);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to delete backup set: {ex.Message}",
                    "Delete Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task ScheduleBackupAsync()
        {
            var message = "Schedule Backup\n\n" +
                         "Configure backup schedule using Windows Task Scheduler.\n" +
                         "You can create a task that runs the backup command at specified intervals.";

            System.Windows.MessageBox.Show(message, "Schedule Backup",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
            await Task.CompletedTask;
        }

        [RelayCommand]
        private async Task VerifyBackupAsync(BackupSet? backupSet)
        {
            if (backupSet == null) return;

            try
            {
                var isValid = await _backupService.VerifyBackupAsync(backupSet.Id);
                StatusMessage = isValid ? "Backup is valid" : "Backup verification failed";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
            }
        }

        [RelayCommand]
        private void BrowseSource()
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Select Source Folder"
            };

            if (dialog.ShowDialog() == true)
            {
                SourcePath = dialog.FolderName;
            }
        }

        [RelayCommand]
        private void BrowseDestination()
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Select Destination Folder"
            };

            if (dialog.ShowDialog() == true)
            {
                DestinationPath = dialog.FolderName;
            }
        }

        [RelayCommand]
        private async Task RefreshAsync()
        {
            await LoadBackupJobsAsync();
            await LoadBackupSetsAsync();
        }
    }

    // Model classes
    public class FileSecurityInfo
    {
        public string Owner { get; set; } = string.Empty;
        public bool InheritFromParent { get; set; }
        public List<FilePermission> Permissions { get; set; } = new();
        public string? ShareName { get; set; }
        public ShareType ShareType { get; set; }
        public DateTime LastModified { get; set; }
    }

    public class FilePermission
    {
        public string UserOrGroup { get; set; } = string.Empty;
        public FileSystemRights Rights { get; set; }
        public AccessControlType Type { get; set; }
        public InheritanceFlags Inheritance { get; set; }
        public PropagationFlags Propagation { get; set; }
        public bool IsInherited { get; set; }
    }

    public class FileAuditEntry
    {
        public DateTime Timestamp { get; set; }
        public string User { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string Details { get; set; } = string.Empty;
        public bool Success { get; set; }
    }

    public class BackupJob
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string SourcePath { get; set; } = string.Empty;
        public string DestinationPath { get; set; } = string.Empty;
        public BackupType Type { get; set; }
        public BackupStatus Status { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime? CompletedDate { get; set; }
        public long TotalFiles { get; set; }
        public long ProcessedFiles { get; set; }
        public long TotalBytes { get; set; }
        public long ProcessedBytes { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public class BackupSet
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string OriginalPath { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; }
        public long Size { get; set; }
        public int FileCount { get; set; }
        public BackupType Type { get; set; }
        public bool IsCompressed { get; set; }
        public bool IsEncrypted { get; set; }
        public string? Checksum { get; set; }
    }

    public class BackupOptions
    {
        public bool IncludeHiddenFiles { get; set; }
        public bool Compress { get; set; }
        public bool Encrypt { get; set; }
        public BackupType Type { get; set; }
        public string? Password { get; set; }
        public List<string> ExcludePatterns { get; set; } = new();
        public List<string> IncludePatterns { get; set; } = new();
    }

    // Enums
    public enum EncryptionType
    {
        AES128,
        AES256,
        AES512,
        DES3,
        Blowfish
    }

    public enum BackupType
    {
        Full,
        Incremental,
        Differential,
        Mirror
    }

    public enum BackupStatus
    {
        Pending,
        Running,
        Completed,
        Failed,
        Cancelled
    }

    public enum ShareType
    {
        None,
        Read,
        Write,
        FullControl
    }
}
