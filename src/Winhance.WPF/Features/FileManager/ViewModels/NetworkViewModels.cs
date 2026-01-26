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
    /// ViewModel for network locations
    /// </summary>
    public partial class NetworkLocationsViewModel : ObservableObject
    {
        private readonly INetworkService _networkService;
        private ObservableCollection<NetworkLocation> _networkLocations = new();
        private ObservableCollection<NetworkShare> _networkShares = new();

        [ObservableProperty]
        private NetworkLocation? _selectedLocation;

        [ObservableProperty]
        private NetworkShare? _selectedShare;

        [ObservableProperty]
        private bool _isConnecting;

        [ObservableProperty]
        private bool _isRefreshing;

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        [ObservableProperty]
        private string _serverAddress = string.Empty;

        [ObservableProperty]
        private string _username = string.Empty;

        [ObservableProperty]
        private string _password = string.Empty;

        [ObservableProperty]
        private bool _saveCredentials;

        public ObservableCollection<NetworkLocation> NetworkLocations
        {
            get => _networkLocations;
            set => SetProperty(ref _networkLocations, value);
        }

        public ObservableCollection<NetworkShare> NetworkShares
        {
            get => _networkShares;
            set => SetProperty(ref _networkShares, value);
        }

        public NetworkLocationsViewModel(INetworkService networkService)
        {
            _networkService = networkService;
            _ = LoadNetworkLocationsAsync();
        }

        private async Task LoadNetworkLocationsAsync()
        {
            IsRefreshing = true;

            try
            {
                var locations = await _networkService.GetNetworkLocationsAsync();
                NetworkLocations.Clear();
                foreach (var location in locations)
                {
                    NetworkLocations.Add(location);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading network locations: {ex.Message}";
            }
            finally
            {
                IsRefreshing = false;
            }
        }

        partial void OnSelectedLocationChanged(NetworkLocation? value)
        {
            if (value != null)
            {
                _ = LoadNetworkSharesAsync(value);
            }
        }

        private async Task LoadNetworkSharesAsync(NetworkLocation location)
        {
            try
            {
                var shares = await _networkService.GetNetworkSharesAsync(location.Address);
                NetworkShares.Clear();
                foreach (var share in shares)
                {
                    NetworkShares.Add(share);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading shares: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task ConnectToServerAsync()
        {
            if (string.IsNullOrEmpty(ServerAddress)) return;

            IsConnecting = true;
            StatusMessage = "Connecting to server...";

            try
            {
                var credentials = !string.IsNullOrEmpty(Username) 
                    ? new NetworkCredentials(Username, Password, SaveCredentials)
                    : null;

                var location = await _networkService.ConnectToServerAsync(ServerAddress, credentials);
                NetworkLocations.Insert(0, location);
                
                StatusMessage = "Connected successfully";
                ServerAddress = string.Empty;
                Username = string.Empty;
                Password = string.Empty;
                SaveCredentials = false;
            }
            catch (Exception ex)
            {
                StatusMessage = $"Connection failed: {ex.Message}";
            }
            finally
            {
                IsConnecting = false;
            }
        }

        [RelayCommand]
        private async Task DisconnectAsync(NetworkLocation? location)
        {
            if (location == null) return;

            try
            {
                await _networkService.DisconnectAsync(location.Id);
                NetworkLocations.Remove(location);
                
                if (SelectedLocation == location)
                {
                    SelectedLocation = null;
                    NetworkShares.Clear();
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Disconnect failed: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task RefreshLocationsAsync()
        {
            await LoadNetworkLocationsAsync();
        }

        [RelayCommand]
        private async Task RefreshSharesAsync()
        {
            if (SelectedLocation != null)
            {
                await LoadNetworkSharesAsync(SelectedLocation);
            }
        }

        [RelayCommand]
        private async Task MapNetworkDriveAsync(NetworkShare? share)
        {
            if (share == null) return;

            try
            {
                var driveLetter = Microsoft.VisualBasic.Interaction.InputBox(
                    $"Enter drive letter to map {share.Name}:",
                    "Map Network Drive",
                    "Z:");

                if (!string.IsNullOrEmpty(driveLetter))
                {
                    if (!driveLetter.EndsWith(":"))
                        driveLetter += ":";
                    
                    await _networkService.MapNetworkDriveAsync(driveLetter, share.Path);
                    StatusMessage = $"Mapped {share.Name} to {driveLetter}";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Failed to map drive: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task UnmapDriveAsync(string driveLetter)
        {
            try
            {
                await _networkService.UnmapNetworkDriveAsync(driveLetter);
                StatusMessage = $"Drive {driveLetter} unmapped successfully";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Failed to unmap drive: {ex.Message}";
            }
        }
    }

    /// <summary>
    /// ViewModel for cloud storage providers
    /// </summary>
    public partial class CloudStorageViewModel : ObservableObject
    {
        private readonly ICloudStorageService _cloudStorageService;
        private ObservableCollection<CloudProvider> _cloudProviders = new();
        private ObservableCollection<CloudFileItem> _cloudFiles = new();

        [ObservableProperty]
        private CloudProvider? _selectedProvider;

        [ObservableProperty]
        private string _currentPath = "/";

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private bool _isUploading;

        [ObservableProperty]
        private bool _isDownloading;

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        [ObservableProperty]
        private CloudFileItem? _selectedFile;

        [ObservableProperty]
        private string _uploadFilePath = string.Empty;

        public ObservableCollection<CloudProvider> CloudProviders
        {
            get => _cloudProviders;
            set => SetProperty(ref _cloudProviders, value);
        }

        public ObservableCollection<CloudFileItem> CloudFiles
        {
            get => _cloudFiles;
            set => SetProperty(ref _cloudFiles, value);
        }

        public CloudStorageViewModel(ICloudStorageService cloudStorageService)
        {
            _cloudStorageService = cloudStorageService;
            _ = LoadCloudProvidersAsync();
        }

        private async Task LoadCloudProvidersAsync()
        {
            try
            {
                var providers = await _cloudStorageService.GetCloudProvidersAsync();
                CloudProviders.Clear();
                foreach (var provider in providers)
                {
                    CloudProviders.Add(provider);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading cloud providers: {ex.Message}";
            }
        }

        partial void OnSelectedProviderChanged(CloudProvider? value)
        {
            if (value != null)
            {
                _ = LoadCloudFilesAsync();
            }
        }

        private async Task LoadCloudFilesAsync()
        {
            if (SelectedProvider == null) return;

            IsLoading = true;

            try
            {
                var files = await _cloudStorageService.GetFilesAsync(SelectedProvider.Id, CurrentPath);
                CloudFiles.Clear();
                foreach (var file in files.OrderBy(f => f.IsFolder ? 0 : 1).ThenBy(f => f.Name))
                {
                    CloudFiles.Add(file);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading files: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task ConnectProviderAsync(CloudProvider? provider)
        {
            if (provider == null) return;

            try
            {
                if (!provider.IsConnected)
                {
                    var result = System.Windows.MessageBox.Show(
                        $"Connect to {provider.Name}?\n\nYou will be redirected to authenticate with your account.",
                        "Cloud Provider Authentication",
                        System.Windows.MessageBoxButton.YesNo,
                        System.Windows.MessageBoxImage.Question);

                    if (result == System.Windows.MessageBoxResult.Yes)
                    {
                        await _cloudStorageService.ConnectAsync(provider.Id);
                        provider.IsConnected = true;
                        StatusMessage = $"Connected to {provider.Name}";
                    }
                }
                SelectedProvider = provider;
            }
            catch (Exception ex)
            {
                StatusMessage = $"Connection failed: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task DisconnectProviderAsync(CloudProvider? provider)
        {
            if (provider == null) return;

            try
            {
                await _cloudStorageService.DisconnectAsync(provider.Id);
                provider.IsConnected = false;
                
                if (SelectedProvider == provider)
                {
                    SelectedProvider = null;
                    CloudFiles.Clear();
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Disconnect failed: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task NavigateToFolderAsync(CloudFileItem? item)
        {
            if (item == null || !item.IsFolder) return;

            CurrentPath = item.Path;
            await LoadCloudFilesAsync();
        }

        [RelayCommand]
        private async Task NavigateUpAsync()
        {
            if (CurrentPath == "/") return;

            var parentPath = System.IO.Path.GetDirectoryName(CurrentPath.Trim('/'));
            CurrentPath = string.IsNullOrEmpty(parentPath) ? "/" : "/" + parentPath;
            await LoadCloudFilesAsync();
        }

        [RelayCommand]
        private async Task UploadFileAsync()
        {
            if (SelectedProvider == null || string.IsNullOrEmpty(UploadFilePath)) return;

            IsUploading = true;
            StatusMessage = "Uploading file...";

            try
            {
                await _cloudStorageService.UploadFileAsync(
                    SelectedProvider.Id, 
                    UploadFilePath, 
                    CurrentPath);
                
                StatusMessage = "File uploaded successfully";
                UploadFilePath = string.Empty;
                await LoadCloudFilesAsync();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Upload failed: {ex.Message}";
            }
            finally
            {
                IsUploading = false;
            }
        }

        [RelayCommand]
        private async Task DownloadFileAsync(CloudFileItem? item)
        {
            if (item == null || item.IsFolder) return;

            IsDownloading = true;
            StatusMessage = "Downloading file...";

            try
            {
                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    FileName = item.Name,
                    DefaultExt = System.IO.Path.GetExtension(item.Name),
                    Filter = "All Files (*.*)|*.*"
                };

                if (dialog.ShowDialog() == true)
                {
                    await _cloudStorageService.DownloadFileAsync(
                        SelectedProvider!.Id, 
                        item.Path, 
                        dialog.FileName);
                    
                    StatusMessage = $"Downloaded to {dialog.FileName}";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Download failed: {ex.Message}";
            }
            finally
            {
                IsDownloading = false;
            }
        }

        [RelayCommand]
        private async Task DeleteFileAsync(CloudFileItem? item)
        {
            if (item == null) return;

            try
            {
                await _cloudStorageService.DeleteFileAsync(SelectedProvider!.Id, item.Path);
                CloudFiles.Remove(item);
                StatusMessage = "File deleted successfully";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Delete failed: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task CreateFolderAsync()
        {
            var folderName = Microsoft.VisualBasic.Interaction.InputBox(
                "Enter folder name:",
                "Create Folder",
                "");

            if (!string.IsNullOrEmpty(folderName) && SelectedProvider != null)
            {
                try
                {
                    var newPath = CurrentPath.TrimEnd('/') + "/" + folderName;
                    await _cloudStorageService.CreateFolderAsync(SelectedProvider.Id, newPath);
                    await LoadCloudFilesAsync();
                    StatusMessage = $"Folder '{folderName}' created";
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Create folder failed: {ex.Message}";
                }
            }
        }

        [RelayCommand]
        private async Task RefreshAsync()
        {
            await LoadCloudFilesAsync();
        }

        [RelayCommand]
        private void BrowseUploadFile()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select File to Upload",
                Filter = "All Files (*.*)|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                UploadFilePath = dialog.FileName;
            }
        }
    }

    /// <summary>
    /// ViewModel for FTP/SFTP connections
    /// </summary>
    public partial class FtpViewModel : ObservableObject
    {
        private readonly IFtpService _ftpService;
        private ObservableCollection<FtpConnection> _ftpConnections = new();
        private ObservableCollection<FtpFileItem> _ftpFiles = new();

        [ObservableProperty]
        private FtpConnection? _selectedConnection;

        [ObservableProperty]
        private string _currentPath = "/";

        [ObservableProperty]
        private bool _isConnecting;

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        [ObservableProperty]
        private FtpFileItem? _selectedFile;

        [ObservableProperty]
        private string _host = string.Empty;

        [ObservableProperty]
        private int _port = 21;

        [ObservableProperty]
        private string _username = string.Empty;

        [ObservableProperty]
        private string _password = string.Empty;

        [ObservableProperty]
        private FtpProtocol _protocol = FtpProtocol.FTP;

        [ObservableProperty]
        private bool _usePassiveMode = true;

        [ObservableProperty]
        private bool _useSsl = false;

        public ObservableCollection<FtpConnection> FtpConnections
        {
            get => _ftpConnections;
            set => SetProperty(ref _ftpConnections, value);
        }

        public ObservableCollection<FtpFileItem> FtpFiles
        {
            get => _ftpFiles;
            set => SetProperty(ref _ftpFiles, value);
        }

        public FtpViewModel(IFtpService ftpService)
        {
            _ftpService = ftpService;
            _ = LoadFtpConnectionsAsync();
        }

        private async Task LoadFtpConnectionsAsync()
        {
            try
            {
                var connections = await _ftpService.GetSavedConnectionsAsync();
                FtpConnections.Clear();
                foreach (var connection in connections)
                {
                    FtpConnections.Add(connection);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading connections: {ex.Message}";
            }
        }

        partial void OnSelectedConnectionChanged(FtpConnection? value)
        {
            if (value != null)
            {
                Host = value.Host;
                Port = value.Port;
                Username = value.Username;
                Protocol = value.Protocol;
                UsePassiveMode = value.UsePassiveMode;
                UseSsl = value.UseSsl;
            }
        }

        [RelayCommand]
        private async Task ConnectAsync()
        {
            if (string.IsNullOrEmpty(Host) || string.IsNullOrEmpty(Username)) return;

            IsConnecting = true;
            StatusMessage = "Connecting to FTP server...";

            try
            {
                var connection = new FtpConnection
                {
                    Host = Host,
                    Port = Port,
                    Username = Username,
                    Password = Password,
                    Protocol = Protocol,
                    UsePassiveMode = UsePassiveMode,
                    UseSsl = UseSsl
                };

                await _ftpService.ConnectAsync(connection);
                SelectedConnection = connection;
                
                if (!FtpConnections.Any(c => c.Id == connection.Id))
                {
                    FtpConnections.Add(connection);
                }
                
                await LoadFtpFilesAsync();
                StatusMessage = "Connected successfully";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Connection failed: {ex.Message}";
            }
            finally
            {
                IsConnecting = false;
            }
        }

        [RelayCommand]
        private async Task DisconnectAsync()
        {
            if (SelectedConnection == null) return;

            try
            {
                await _ftpService.DisconnectAsync(SelectedConnection.Id);
                SelectedConnection = null;
                FtpFiles.Clear();
                CurrentPath = "/";
                StatusMessage = "Disconnected";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Disconnect failed: {ex.Message}";
            }
        }

        private async Task LoadFtpFilesAsync()
        {
            if (SelectedConnection == null) return;

            IsLoading = true;

            try
            {
                var files = await _ftpService.GetFilesAsync(SelectedConnection.Id, CurrentPath);
                FtpFiles.Clear();
                foreach (var file in files.OrderBy(f => f.IsFolder ? 0 : 1).ThenBy(f => f.Name))
                {
                    FtpFiles.Add(file);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading files: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task NavigateToFolderAsync(FtpFileItem? item)
        {
            if (item == null || !item.IsFolder) return;

            CurrentPath = item.Path;
            await LoadFtpFilesAsync();
        }

        [RelayCommand]
        private async Task NavigateUpAsync()
        {
            if (CurrentPath == "/") return;

            var parentPath = CurrentPath.TrimEnd('/');
            var lastSlash = parentPath.LastIndexOf('/');
            CurrentPath = lastSlash > 0 ? CurrentPath.Substring(0, lastSlash) : "/";
            await LoadFtpFilesAsync();
        }

        [RelayCommand]
        private async Task UploadFileAsync()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select File to Upload",
                Filter = "All Files (*.*)|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    await _ftpService.UploadFileAsync(SelectedConnection!.Id, dialog.FileName, CurrentPath);
                    await LoadFtpFilesAsync();
                    StatusMessage = "File uploaded successfully";
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Upload failed: {ex.Message}";
                }
            }
        }

        [RelayCommand]
        private async Task DownloadFileAsync(FtpFileItem? item)
        {
            if (item == null || item.IsFolder) return;

            try
            {
                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    FileName = item.Name,
                    DefaultExt = System.IO.Path.GetExtension(item.Name),
                    Filter = "All Files (*.*)|*.*"
                };

                if (dialog.ShowDialog() == true)
                {
                    await _ftpService.DownloadFileAsync(SelectedConnection!.Id, item.Path, dialog.FileName);
                    StatusMessage = $"Downloaded to {dialog.FileName}";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Download failed: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task DeleteFileAsync(FtpFileItem? item)
        {
            if (item == null) return;

            try
            {
                await _ftpService.DeleteFileAsync(SelectedConnection!.Id, item.Path);
                FtpFiles.Remove(item);
                StatusMessage = "File deleted successfully";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Delete failed: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task RefreshAsync()
        {
            await LoadFtpFilesAsync();
        }

        [RelayCommand]
        private void QuickConnect()
        {
            var serverUrl = Microsoft.VisualBasic.Interaction.InputBox(
                "Enter FTP server (format: ftp://server:port):",
                "Quick Connect",
                "ftp://");

            if (!string.IsNullOrEmpty(serverUrl) && serverUrl.StartsWith("ftp"))
            {
                try
                {
                    var uri = new Uri(serverUrl);
                    Host = uri.Host;
                    Port = uri.Port > 0 ? uri.Port : 21;
                    StatusMessage = $"Ready to connect to {Host}:{Port}";
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Invalid URL: {ex.Message}";
                }
            }
        }
    }

    // Model classes
    public class NetworkLocation
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public LocationType Type { get; set; }
        public bool IsConnected { get; set; }
        public DateTime ConnectedAt { get; set; }
        public string? Username { get; set; }
    }

    public class NetworkShare
    {
        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public ShareType Type { get; set; }
        public long FreeSpace { get; set; }
        public long TotalSpace { get; set; }
        public bool IsAvailable { get; set; }
    }

    public class NetworkCredentials
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public bool Save { get; set; }
    }

    public class CloudProvider
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public bool IsConnected { get; set; }
        public CloudProviderType Type { get; set; }
        public long UsedSpace { get; set; }
        public long TotalSpace { get; set; }
        public DateTime ConnectedAt { get; set; }
    }

    public class CloudFileItem
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public long Size { get; set; }
        public DateTime Modified { get; set; }
        public bool IsFolder { get; set; }
        public string MimeType { get; set; } = string.Empty;
        public string? ThumbnailUrl { get; set; }
        public bool IsShared { get; set; }
    }

    public class FtpConnection
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string Host { get; set; } = string.Empty;
        public int Port { get; set; } = 21;
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public FtpProtocol Protocol { get; set; }
        public bool UsePassiveMode { get; set; } = true;
        public bool UseSsl { get; set; }
        public bool IsConnected { get; set; }
        public DateTime LastConnected { get; set; }
    }

    public class FtpFileItem
    {
        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public long Size { get; set; }
        public DateTime Modified { get; set; }
        public string Permissions { get; set; } = string.Empty;
        public string Owner { get; set; } = string.Empty;
        public string Group { get; set; } = string.Empty;
        public bool IsFolder { get; set; }
        public bool IsExecutable { get; set; }
    }

    // Enums
    public enum LocationType
    {
        Workstation,
        Server,
        Domain,
        Cloud
    }

    public enum CloudProviderType
    {
        OneDrive,
        GoogleDrive,
        Dropbox,
        iCloud,
        Box,
        SharePoint,
        Nextcloud,
        OwnCloud
    }

    public enum FtpProtocol
    {
        FTP,
        SFTP,
        FTPS
    }
}
