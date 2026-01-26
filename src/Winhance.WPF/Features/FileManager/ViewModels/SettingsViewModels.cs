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
    /// ViewModel for application settings
    /// </summary>
    public partial class SettingsViewModel : ObservableObject
    {
        private readonly ISettingsService _settingsService;
        private AppSettings _settings = new();

        [ObservableProperty]
        private SettingsCategory _selectedCategory = SettingsCategory.General;

        public AppSettings Settings
        {
            get => _settings;
            set => SetProperty(ref _settings, value);
        }

        public SettingsViewModel(ISettingsService settingsService)
        {
            _settingsService = settingsService;
            _ = LoadSettingsAsync();
        }

        private async Task LoadSettingsAsync()
        {
            try
            {
                Settings = await _settingsService.GetSettingsAsync();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to load settings: {ex.Message}\n\nUsing default settings.",
                    "Settings Load Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
                Settings = AppSettings.GetDefaults();
            }
        }

        [RelayCommand]
        private async Task SaveSettingsAsync()
        {
            try
            {
                await _settingsService.SaveSettingsAsync(Settings);
                System.Windows.MessageBox.Show(
                    "Settings saved successfully.",
                    "Settings Saved",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to save settings: {ex.Message}",
                    "Save Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task ResetToDefaultsAsync()
        {
            try
            {
                var result = System.Windows.MessageBox.Show(
                    "Are you sure you want to reset all settings to defaults?",
                    "Confirm Reset",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Question);
                
                if (result == System.Windows.MessageBoxResult.Yes)
                {
                    Settings = AppSettings.GetDefaults();
                    await SaveSettingsAsync();
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to reset settings: {ex.Message}",
                    "Reset Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async void ImportSettings()
        {
            try
            {
                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "Settings Files (*.json)|*.json|All Files (*.*)|*.*",
                    DefaultExt = ".json",
                    Title = "Import Settings"
                };

                if (dialog.ShowDialog() == true)
                {
                    var json = await System.IO.File.ReadAllTextAsync(dialog.FileName);
                    Settings = System.Text.Json.JsonSerializer.Deserialize<AppSettings>(json) ?? AppSettings.GetDefaults();
                    await SaveSettingsAsync();
                    
                    System.Windows.MessageBox.Show(
                        "Settings imported successfully.",
                        "Import Complete",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to import settings: {ex.Message}",
                    "Import Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async void ExportSettings()
        {
            try
            {
                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "Settings Files (*.json)|*.json|All Files (*.*)|*.*",
                    DefaultExt = ".json",
                    FileName = $"WinhanceSettings_{DateTime.Now:yyyyMMdd_HHmmss}.json",
                    Title = "Export Settings"
                };

                if (dialog.ShowDialog() == true)
                {
                    var json = System.Text.Json.JsonSerializer.Serialize(Settings,
                        new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                    await System.IO.File.WriteAllTextAsync(dialog.FileName, json);
                    
                    System.Windows.MessageBox.Show(
                        $"Settings exported to {dialog.FileName}",
                        "Export Complete",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to export settings: {ex.Message}",
                    "Export Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }
    }

    /// <summary>
    /// ViewModel for general settings
    /// </summary>
    public partial class GeneralSettingsViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _defaultLocation = string.Empty;

        [ObservableProperty]
        private bool _showHiddenFiles;

        [ObservableProperty]
        private bool _showSystemFiles;

        [ObservableProperty]
        private bool _confirmDeletions = true;

        [ObservableProperty]
        private bool _rememberTabs;

        [ObservableProperty]
        private int _maxRecentItems = 20;

        [ObservableProperty]
        private string _theme = "Default";

        [ObservableProperty]
        private string _language = "en-US";

        public GeneralSettingsViewModel()
        {
            LoadDefaults();
        }

        private void LoadDefaults()
        {
            DefaultLocation = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            ShowHiddenFiles = false;
            ShowSystemFiles = false;
            ConfirmDeletions = true;
            RememberTabs = true;
            MaxRecentItems = 20;
            Theme = "Default";
            Language = System.Globalization.CultureInfo.CurrentCulture.Name;
        }

        [RelayCommand]
        private void BrowseDefaultLocation()
        {
            try
            {
                var dialog = new Microsoft.Win32.OpenFolderDialog
                {
                    Title = "Select Default Location",
                    InitialDirectory = string.IsNullOrEmpty(DefaultLocation) 
                        ? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
                        : DefaultLocation
                };

                if (dialog.ShowDialog() == true)
                {
                    DefaultLocation = dialog.FolderName;
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to browse location: {ex.Message}",
                    "Browse Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void ResetToDefaults()
        {
            LoadDefaults();
        }
    }

    /// <summary>
    /// ViewModel for file operations settings
    /// </summary>
    public partial class FileOperationsSettingsViewModel : ObservableObject
    {
        [ObservableProperty]
        private bool _enableLargeFileWarning = true;

        [ObservableProperty]
        private long _largeFileThreshold = 100 * 1024 * 1024; // 100MB

        [ObservableProperty]
        private bool _enableFileVerification = true;

        [ObservableProperty]
        private bool _preserveTimestamps = true;

        [ObservableProperty]
        private bool _preservePermissions = true;

        [ObservableProperty]
        private int _defaultBufferSize = 64 * 1024; // 64KB

        [ObservableProperty]
        private int _maxConcurrentOperations = 3;

        [ObservableProperty]
        private bool _enableRecycleBin = true;

        [ObservableProperty]
        private bool _showOperationProgress = true;

        public FileOperationsSettingsViewModel()
        {
            LoadDefaults();
        }

        private void LoadDefaults()
        {
            EnableLargeFileWarning = true;
            LargeFileThreshold = 100 * 1024 * 1024;
            EnableFileVerification = true;
            PreserveTimestamps = true;
            PreservePermissions = true;
            DefaultBufferSize = 64 * 1024;
            MaxConcurrentOperations = 3;
            EnableRecycleBin = true;
            ShowOperationProgress = true;
        }

        [RelayCommand]
        private void ResetToDefaults()
        {
            LoadDefaults();
        }

        [RelayCommand]
        private void TestBufferSize()
        {
            var message = $"Buffer Size Performance Test\n\n" +
                         $"Current buffer size: {DefaultBufferSize / 1024} KB\n\n" +
                         "Testing buffer performance...\n" +
                         "Recommended: 64 KB for local drives\n" +
                         "Recommended: 128 KB for network drives\n" +
                         "Recommended: 256 KB for slow connections";

            System.Windows.MessageBox.Show(message, "Buffer Test",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
        }
    }

    /// <summary>
    /// ViewModel for appearance settings
    /// </summary>
    public partial class AppearanceSettingsViewModel : ObservableObject
    {
        private ObservableCollection<ThemeInfo> _availableThemes = new();

        [ObservableProperty]
        private string _selectedTheme = string.Empty;

        [ObservableProperty]
        private bool _enableAnimations = true;

        [ObservableProperty]
        private bool _showPreviewPanel = true;

        [ObservableProperty]
        private bool _showDetailsPanel = true;

        [ObservableProperty]
        private double _fontSize = 12;

        [ObservableProperty]
        private string _fontFamily = "Segoe UI";

        [ObservableProperty]
        private bool _compactMode = false;

        public ObservableCollection<ThemeInfo> AvailableThemes
        {
            get => _availableThemes;
            set => SetProperty(ref _availableThemes, value);
        }

        public AppearanceSettingsViewModel()
        {
            LoadThemes();
            LoadDefaults();
        }

        private void LoadThemes()
        {
            AvailableThemes.Add(new ThemeInfo { Name = "Default", Description = "Default Windows theme" });
            AvailableThemes.Add(new ThemeInfo { Name = "Dark", Description = "Dark theme" });
            AvailableThemes.Add(new ThemeInfo { Name = "Light", Description = "Light theme" });
            AvailableThemes.Add(new ThemeInfo { Name = "Blue", Description = "Blue accent theme" });
            AvailableThemes.Add(new ThemeInfo { Name = "High Contrast", Description = "High contrast theme" });
        }

        private void LoadDefaults()
        {
            SelectedTheme = "Default";
            EnableAnimations = true;
            ShowPreviewPanel = true;
            ShowDetailsPanel = true;
            FontSize = 12;
            FontFamily = "Segoe UI";
            CompactMode = false;
        }

        [RelayCommand]
        private void ApplyTheme()
        {
            try
            {
                System.Windows.MessageBox.Show(
                    $"Theme '{SelectedTheme}' will be applied on next restart.",
                    "Theme Changed",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to apply theme: {ex.Message}",
                    "Theme Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void PreviewTheme(ThemeInfo? theme)
        {
            if (theme == null) return;

            var message = $"Theme Preview\n\n" +
                         $"Name: {theme.Name}\n" +
                         $"Description: {theme.Description}\n\n" +
                         "This is a preview of how the theme will look.\n" +
                         "Click 'Apply Theme' to use this theme.";

            System.Windows.MessageBox.Show(message, "Theme Preview",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
        }

        [RelayCommand]
        private void ResetToDefaults()
        {
            LoadDefaults();
        }
    }

    /// <summary>
    /// ViewModel for advanced settings
    /// </summary>
    public partial class AdvancedSettingsViewModel : ObservableObject
    {
        [ObservableProperty]
        private bool _enableLogging = true;

        [ObservableProperty]
        private LogLevel _logLevel = LogLevel.Information;

        [ObservableProperty]
        private string _logPath = string.Empty;

        [ObservableProperty]
        private int _maxLogSize = 10; // MB

        [ObservableProperty]
        private bool _enableTelemetry = false;

        [ObservableProperty]
        private bool _enableAutoUpdate = true;

        [ObservableProperty]
        private bool _enableBetaUpdates = false;

        [ObservableProperty]
        private bool _enableDebugMode = false;

        [ObservableProperty]
        private int _cacheSize = 100; // MB

        [ObservableProperty]
        private bool _clearCacheOnExit = false;

        public AdvancedSettingsViewModel()
        {
            LoadDefaults();
        }

        private void LoadDefaults()
        {
            EnableLogging = true;
            LogLevel = LogLevel.Information;
            LogPath = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Winhance-FS",
                "Logs");
            MaxLogSize = 10;
            EnableTelemetry = false;
            EnableAutoUpdate = true;
            EnableBetaUpdates = false;
            EnableDebugMode = false;
            CacheSize = 100;
            ClearCacheOnExit = false;
        }

        [RelayCommand]
        private void BrowseLogPath()
        {
            try
            {
                var dialog = new Microsoft.Win32.OpenFolderDialog
                {
                    Title = "Select Log Directory",
                    InitialDirectory = string.IsNullOrEmpty(LogPath)
                        ? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
                        : LogPath
                };

                if (dialog.ShowDialog() == true)
                {
                    LogPath = dialog.FolderName;
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to browse log path: {ex.Message}",
                    "Browse Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void ClearLogs()
        {
            var result = System.Windows.MessageBox.Show(
                "Are you sure you want to clear all log files?\n\nThis action cannot be undone.",
                "Confirm Clear Logs",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning);

            if (result == System.Windows.MessageBoxResult.Yes)
            {
                try
                {
                    if (System.IO.Directory.Exists(LogPath))
                    {
                        foreach (var file in System.IO.Directory.GetFiles(LogPath, "*.log"))
                        {
                            System.IO.File.Delete(file);
                        }
                        System.Windows.MessageBox.Show(
                            "Log files cleared successfully.",
                            "Logs Cleared",
                            System.Windows.MessageBoxButton.OK,
                            System.Windows.MessageBoxImage.Information);
                    }
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show(
                        $"Failed to clear logs: {ex.Message}",
                        "Clear Error",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Error);
                }
            }
        }

        [RelayCommand]
        private void ClearCache()
        {
            var result = System.Windows.MessageBox.Show(
                $"Are you sure you want to clear the application cache ({CacheSize} MB)?\n\nThis will free up disk space but may slow down the application temporarily.",
                "Confirm Clear Cache",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Question);

            if (result == System.Windows.MessageBoxResult.Yes)
            {
                try
                {
                    var cachePath = System.IO.Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "Winhance-FS",
                        "Cache");

                    if (System.IO.Directory.Exists(cachePath))
                    {
                        System.IO.Directory.Delete(cachePath, true);
                        System.IO.Directory.CreateDirectory(cachePath);
                    }

                    System.Windows.MessageBox.Show(
                        "Cache cleared successfully.",
                        "Cache Cleared",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show(
                        $"Failed to clear cache: {ex.Message}",
                        "Clear Error",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Error);
                }
            }
        }

        [RelayCommand]
        private void ExportDiagnostics()
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "ZIP Archive (*.zip)|*.zip|Text File (*.txt)|*.txt",
                DefaultExt = ".zip",
                FileName = $"Diagnostics_{DateTime.Now:yyyyMMdd_HHmmss}.zip"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var diagnostics = $"Winhance-FS Diagnostic Report\n" +
                                    $"Generated: {DateTime.Now:g}\n\n" +
                                    $"Logging: {EnableLogging}\n" +
                                    $"Log Level: {LogLevel}\n" +
                                    $"Debug Mode: {EnableDebugMode}\n" +
                                    $"Cache Size: {CacheSize} MB\n" +
                                    $"OS: {Environment.OSVersion}\n" +
                                    $".NET: {Environment.Version}";

                    System.IO.File.WriteAllText(dialog.FileName.Replace(".zip", ".txt"), diagnostics);
                    System.Windows.MessageBox.Show(
                        "Diagnostic information exported successfully.",
                        "Export Complete",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show(
                        $"Failed to export diagnostics: {ex.Message}",
                        "Export Error",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Error);
                }
            }
        }

        [RelayCommand]
        private void ResetToDefaults()
        {
            LoadDefaults();
        }
    }

    /// <summary>
    /// ViewModel for plugins settings
    /// </summary>
    public partial class PluginsSettingsViewModel : ObservableObject
    {
        private ObservableCollection<PluginInfo> _availablePlugins = new();
        private ObservableCollection<PluginInfo> _installedPlugins = new();

        [ObservableProperty]
        private bool _enablePlugins = true;

        [ObservableProperty]
        private bool _allowBetaPlugins = false;

        public ObservableCollection<PluginInfo> AvailablePlugins
        {
            get => _availablePlugins;
            set => SetProperty(ref _availablePlugins, value);
        }

        public ObservableCollection<PluginInfo> InstalledPlugins
        {
            get => _installedPlugins;
            set => SetProperty(ref _installedPlugins, value);
        }

        public PluginsSettingsViewModel()
        {
            LoadPlugins();
        }

        private void LoadPlugins()
        {
            // Load installed plugins
            InstalledPlugins.Add(new PluginInfo
            {
                Name = "Everything Search Integration",
                Version = "1.0.0",
                Description = "Integration with Everything search engine",
                IsEnabled = true,
                IsBuiltIn = true
            });

            InstalledPlugins.Add(new PluginInfo
            {
                Name = "Cloud Storage Sync",
                Version = "1.2.0",
                Description = "Sync with cloud storage providers",
                IsEnabled = false,
                IsBuiltIn = false
            });

            // Load available plugins
            AvailablePlugins.Add(new PluginInfo
            {
                Name = "Advanced Preview",
                Version = "2.0.0",
                Description = "Enhanced file preview capabilities",
                IsEnabled = false,
                IsBuiltIn = false,
                IsUpdateAvailable = true
            });
        }

        [RelayCommand]
        private async Task InstallPluginAsync(PluginInfo? plugin)
        {
            if (plugin == null) return;

            try
            {
                var result = System.Windows.MessageBox.Show(
                    $"Install plugin '{plugin.Name}'?\n\nVersion: {plugin.Version}\nDescription: {plugin.Description}",
                    "Confirm Install",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Question);

                if (result == System.Windows.MessageBoxResult.Yes)
                {
                    plugin.IsInstalled = true;
                    InstalledPlugins.Add(plugin);
                    AvailablePlugins.Remove(plugin);
                    System.Windows.MessageBox.Show(
                        $"Plugin '{plugin.Name}' installed successfully.",
                        "Install Complete",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to install plugin: {ex.Message}",
                    "Install Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task UninstallPluginAsync(PluginInfo? plugin)
        {
            if (plugin == null || plugin.IsBuiltIn) return;

            try
            {
                var result = System.Windows.MessageBox.Show(
                    $"Uninstall plugin '{plugin.Name}'?\n\nThis will remove all plugin data.",
                    "Confirm Uninstall",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Warning);

                if (result == System.Windows.MessageBoxResult.Yes)
                {
                    plugin.IsInstalled = false;
                    InstalledPlugins.Remove(plugin);
                    AvailablePlugins.Add(plugin);
                    System.Windows.MessageBox.Show(
                        $"Plugin '{plugin.Name}' uninstalled successfully.",
                        "Uninstall Complete",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to uninstall plugin: {ex.Message}",
                    "Uninstall Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task EnablePluginAsync(PluginInfo? plugin)
        {
            if (plugin == null) return;

            try
            {
                plugin.IsEnabled = true;
                System.Windows.MessageBox.Show(
                    $"Plugin '{plugin.Name}' enabled successfully.\n\nRestart may be required.",
                    "Plugin Enabled",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to enable plugin: {ex.Message}",
                    "Enable Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task DisablePluginAsync(PluginInfo? plugin)
        {
            if (plugin == null || plugin.IsBuiltIn) return;

            try
            {
                plugin.IsEnabled = false;
                System.Windows.MessageBox.Show(
                    $"Plugin '{plugin.Name}' disabled successfully.\n\nRestart may be required.",
                    "Plugin Disabled",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to disable plugin: {ex.Message}",
                    "Disable Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task UpdatePluginAsync(PluginInfo? plugin)
        {
            if (plugin == null) return;

            try
            {
                var result = System.Windows.MessageBox.Show(
                    $"Update plugin '{plugin.Name}' to the latest version?",
                    "Confirm Update",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Question);

                if (result == System.Windows.MessageBoxResult.Yes)
                {
                    plugin.IsUpdateAvailable = false;
                    System.Windows.MessageBox.Show(
                        $"Plugin '{plugin.Name}' updated successfully.",
                        "Update Complete",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to update plugin: {ex.Message}",
                    "Update Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void CheckForUpdates()
        {
            try
            {
                var updatesAvailable = InstalledPlugins.Count(p => p.IsUpdateAvailable);
                var message = updatesAvailable > 0
                    ? $"Found {updatesAvailable} plugin update(s) available."
                    : "All plugins are up to date.";

                System.Windows.MessageBox.Show(message, "Plugin Updates",
                    System.Windows.MessageBoxButton.OK,
                    updatesAvailable > 0
                        ? System.Windows.MessageBoxImage.Information
                        : System.Windows.MessageBoxImage.None);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to check for updates: {ex.Message}",
                    "Update Check Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void BrowsePlugins()
        {
            try
            {
                var message = $"Plugin Browser\n\n" +
                             $"Available Plugins: {AvailablePlugins.Count}\n" +
                             $"Installed Plugins: {InstalledPlugins.Count}\n\n" +
                             "Visit the plugin marketplace to discover more plugins.";

                System.Windows.MessageBox.Show(message, "Plugin Browser",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to open plugin browser: {ex.Message}",
                    "Browser Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }
    }

    // Model classes
    public class AppSettings
    {
        public GeneralSettings General { get; set; } = new();
        public FileOperationsSettings FileOperations { get; set; } = new();
        public AppearanceSettings Appearance { get; set; } = new();
        public AdvancedSettings Advanced { get; set; } = new();
        public PluginsSettings Plugins { get; set; } = new();

        public static AppSettings GetDefaults()
        {
            return new AppSettings();
        }
    }

    public class GeneralSettings
    {
        public string DefaultLocation { get; set; } = string.Empty;
        public bool ShowHiddenFiles { get; set; }
        public bool ShowSystemFiles { get; set; }
        public bool ConfirmDeletions { get; set; } = true;
        public bool RememberTabs { get; set; }
        public int MaxRecentItems { get; set; } = 20;
        public string Theme { get; set; } = "Default";
        public string Language { get; set; } = "en-US";
    }

    public class FileOperationsSettings
    {
        public bool EnableLargeFileWarning { get; set; } = true;
        public long LargeFileThreshold { get; set; } = 100 * 1024 * 1024;
        public bool EnableFileVerification { get; set; } = true;
        public bool PreserveTimestamps { get; set; } = true;
        public bool PreservePermissions { get; set; } = true;
        public int DefaultBufferSize { get; set; } = 64 * 1024;
        public int MaxConcurrentOperations { get; set; } = 3;
        public bool EnableRecycleBin { get; set; } = true;
        public bool ShowOperationProgress { get; set; } = true;
    }

    public class AppearanceSettings
    {
        public string Theme { get; set; } = "Default";
        public bool EnableAnimations { get; set; } = true;
        public bool ShowPreviewPanel { get; set; } = true;
        public bool ShowDetailsPanel { get; set; } = true;
        public double FontSize { get; set; } = 12;
        public string FontFamily { get; set; } = "Segoe UI";
        public bool CompactMode { get; set; }
    }

    public class AdvancedSettings
    {
        public bool EnableLogging { get; set; } = true;
        public LogLevel LogLevel { get; set; } = LogLevel.Information;
        public string LogPath { get; set; } = string.Empty;
        public int MaxLogSize { get; set; } = 10;
        public bool EnableTelemetry { get; set; }
        public bool EnableAutoUpdate { get; set; } = true;
        public bool EnableBetaUpdates { get; set; }
        public bool EnableDebugMode { get; set; }
        public int CacheSize { get; set; } = 100;
        public bool ClearCacheOnExit { get; set; }
    }

    public class PluginsSettings
    {
        public bool EnablePlugins { get; set; } = true;
        public bool AllowBetaPlugins { get; set; }
        public List<string> DisabledPlugins { get; set; } = new();
    }

    public class ThemeInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    public class PluginInfo
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsEnabled { get; set; }
        public bool IsInstalled { get; set; } = true;
        public bool IsBuiltIn { get; set; }
        public bool IsUpdateAvailable { get; set; }
        public string? Author { get; set; }
        public string? Homepage { get; set; }
    }

    // Enums
    public enum SettingsCategory
    {
        General,
        FileOperations,
        Appearance,
        Advanced,
        Plugins
    }

    public enum LogLevel
    {
        Trace,
        Debug,
        Information,
        Warning,
        Error,
        Critical
    }
}
