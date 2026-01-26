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
    /// ViewModel for tree map visualization
    /// </summary>
    public partial class TreeMapViewModel : ObservableObject
    {
        private readonly ISpaceAnalyzerService _spaceAnalyzerService;
        private ObservableCollection<TreeMapItem> _treeMapItems = new();

        [ObservableProperty]
        private TreeMapItem? _selectedItem;

        [ObservableProperty]
        private string _currentPath = string.Empty;

        [ObservableProperty]
        private bool _isAnalyzing;

        [ObservableProperty]
        private TreeMapColorScheme _colorScheme = TreeMapColorScheme.ByType;

        public ObservableCollection<TreeMapItem> TreeMapItems
        {
            get => _treeMapItems;
            set => SetProperty(ref _treeMapItems, value);
        }

        public TreeMapViewModel(ISpaceAnalyzerService spaceAnalyzerService)
        {
            _spaceAnalyzerService = spaceAnalyzerService;
        }

        [RelayCommand]
        private async Task AnalyzePathAsync(string? path)
        {
            if (string.IsNullOrEmpty(path)) return;

            CurrentPath = path;
            IsAnalyzing = true;

            try
            {
                var analysis = await _spaceAnalyzerService.GetTreeMapDataAsync(path);
                
                TreeMapItems.Clear();
                foreach (var item in analysis)
                {
                    TreeMapItems.Add(item);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to analyze path: {ex.Message}",
                    "Analysis Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
            finally
            {
                IsAnalyzing = false;
            }
        }

        [RelayCommand]
        private void DrillDown(TreeMapItem? item)
        {
            if (item == null || !item.IsDirectory) return;

            _ = AnalyzePathAsync(item.FullPath);
        }

        [RelayCommand]
        private void NavigateUp()
        {
            var parent = System.IO.Directory.GetParent(CurrentPath);
            if (parent != null)
            {
                _ = AnalyzePathAsync(parent.FullName);
            }
        }

        [RelayCommand]
        private void ChangeColorScheme(TreeMapColorScheme scheme)
        {
            ColorScheme = scheme;
            UpdateColors();
        }

        private void UpdateColors()
        {
            foreach (var item in TreeMapItems)
            {
                item.Color = ColorScheme switch
                {
                    TreeMapColorScheme.ByType => GetColorByType(item),
                    TreeMapColorScheme.BySize => GetColorBySize(item),
                    TreeMapColorScheme.ByDate => GetColorByDate(item),
                    TreeMapColorScheme.ByDepth => GetColorByDepth(item),
                    _ => "#0078D4"
                };
            }
        }

        private static string GetColorByType(TreeMapItem item)
        {
            if (item.IsDirectory) return "#FFA500";
            var ext = System.IO.Path.GetExtension(item.FullPath).ToLowerInvariant();
            return ext switch
            {
                ".txt" or ".doc" or ".docx" => "#4A90E2",
                ".jpg" or ".png" or ".gif" => "#E24A90",
                ".mp4" or ".avi" or ".mkv" => "#90E24A",
                ".zip" or ".rar" or ".7z" => "#9C27B0",
                _ => "#607D8B"
            };
        }

        private static string GetColorBySize(TreeMapItem item)
        {
            return item.Size switch
            {
                < 1024 * 1024 => "#4CAF50",
                < 10 * 1024 * 1024 => "#FFC107",
                < 100 * 1024 * 1024 => "#FF9800",
                _ => "#F44336"
            };
        }

        private static string GetColorByDate(TreeMapItem item)
        {
            try
            {
                var info = new System.IO.FileInfo(item.FullPath);
                var age = (DateTime.Now - info.LastWriteTime).TotalDays;
                return age switch
                {
                    < 7 => "#4CAF50",
                    < 30 => "#8BC34A",
                    < 90 => "#FFC107",
                    _ => "#9E9E9E"
                };
            }
            catch
            {
                return "#9E9E9E";
            }
        }

        private static string GetColorByDepth(TreeMapItem item)
        {
            var depth = item.FullPath.Split('\\').Length;
            return depth switch
            {
                <= 3 => "#E3F2FD",
                4 => "#90CAF9",
                5 => "#42A5F5",
                6 => "#1E88E5",
                _ => "#1565C0"
            };
        }
    }

    /// <summary>
    /// ViewModel for disk usage visualization
    /// </summary>
    public partial class DiskUsageViewModel : ObservableObject
    {
        private readonly ISpaceAnalyzerService _spaceAnalyzerService;
        private ObservableCollection<DiskUsageChartItem> _chartItems = new();

        [ObservableProperty]
        private DiskDriveInfo? _selectedDrive;

        [ObservableProperty]
        private ChartType _chartType = ChartType.Pie;

        [ObservableProperty]
        private bool _showDetails = true;

        public ObservableCollection<DiskUsageChartItem> ChartItems
        {
            get => _chartItems;
            set => SetProperty(ref _chartItems, value);
        }

        public DiskUsageViewModel(ISpaceAnalyzerService spaceAnalyzerService)
        {
            _spaceAnalyzerService = spaceAnalyzerService;
        }

        [RelayCommand]
        private async Task LoadDriveUsageAsync(DiskDriveInfo? drive)
        {
            if (drive == null) return;

            SelectedDrive = drive;

            try
            {
                var usage = await _spaceAnalyzerService.GetDiskUsageChartAsync(drive.RootPath);
                
                ChartItems.Clear();
                foreach (var item in usage)
                {
                    ChartItems.Add(item);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to load disk usage: {ex.Message}",
                    "Load Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void ToggleChartType()
        {
            ChartType = ChartType == ChartType.Pie ? ChartType.Doughnut : ChartType.Pie;
        }

        [RelayCommand]
        private async void ExportChart()
        {
            try
            {
                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "PNG Image (*.png)|*.png|JPEG Image (*.jpg)|*.jpg|SVG Image (*.svg)|*.svg",
                    DefaultExt = ".png",
                    FileName = $"DiskUsageChart_{DateTime.Now:yyyyMMdd_HHmmss}"
                };

                if (dialog.ShowDialog() == true)
                {
                    var chartData = System.Text.Json.JsonSerializer.Serialize(ChartItems);
                    await System.IO.File.WriteAllTextAsync(
                        dialog.FileName + ".json",
                        chartData);

                    System.Windows.MessageBox.Show(
                        $"Chart data exported to {dialog.FileName}.json\n\nNote: Visual rendering requires additional library.",
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
    /// ViewModel for file type distribution
    /// </summary>
    public partial class FileTypeDistributionViewModel : ObservableObject
    {
        private readonly ISpaceAnalyzerService _spaceAnalyzerService;
        private ObservableCollection<FileTypeItem> _fileTypes = new();

        [ObservableProperty]
        private string _analysisPath = string.Empty;

        [ObservableProperty]
        private bool _isAnalyzing;

        [ObservableProperty]
        private DistributionView _viewMode = DistributionView.BySize;

        public ObservableCollection<FileTypeItem> FileTypes
        {
            get => _fileTypes;
            set => SetProperty(ref _fileTypes, value);
        }

        public FileTypeDistributionViewModel(ISpaceAnalyzerService spaceAnalyzerService)
        {
            _spaceAnalyzerService = spaceAnalyzerService;
        }

        [RelayCommand]
        private async Task AnalyzeFileTypesAsync()
        {
            if (string.IsNullOrEmpty(AnalysisPath)) return;

            IsAnalyzing = true;

            try
            {
                var distribution = await _spaceAnalyzerService.GetFileTypeDistributionAsync(AnalysisPath);
                
                FileTypes.Clear();
                foreach (var type in distribution)
                {
                    FileTypes.Add(type);
                }

                SortFileTypes();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to analyze file types: {ex.Message}",
                    "Analysis Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
            finally
            {
                IsAnalyzing = false;
            }
        }

        [RelayCommand]
        private void ChangeViewMode(DistributionView mode)
        {
            ViewMode = mode;
            SortFileTypes();
        }

        private void SortFileTypes()
        {
            var sorted = ViewMode switch
            {
                DistributionView.BySize => FileTypes.OrderByDescending(f => f.TotalSize),
                DistributionView.ByCount => FileTypes.OrderByDescending(f => f.FileCount),
                DistributionView.ByName => FileTypes.OrderBy(f => f.Extension),
                _ => FileTypes.OrderByDescending(f => f.TotalSize)
            };

            var sortedList = sorted.ToList();
            FileTypes.Clear();
            foreach (var item in sortedList)
            {
                FileTypes.Add(item);
            }
        }
    }

    /// <summary>
    /// ViewModel for timeline visualization
    /// </summary>
    public partial class TimelineViewModel : ObservableObject
    {
        private readonly ISpaceAnalyzerService _spaceAnalyzerService;
        private ObservableCollection<TimelineItem> _timelineItems = new();

        [ObservableProperty]
        private DateTime _startDate = DateTime.Today.AddMonths(-1);

        [ObservableProperty]
        private DateTime _endDate = DateTime.Today;

        [ObservableProperty]
        private TimelineGranularity _granularity = TimelineGranularity.Daily;

        [ObservableProperty]
        private string _analysisPath = string.Empty;

        public ObservableCollection<TimelineItem> TimelineItems
        {
            get => _timelineItems;
            set => SetProperty(ref _timelineItems, value);
        }

        public TimelineViewModel(ISpaceAnalyzerService spaceAnalyzerService)
        {
            _spaceAnalyzerService = spaceAnalyzerService;
        }

        [RelayCommand]
        private async Task GenerateTimelineAsync()
        {
            if (string.IsNullOrEmpty(AnalysisPath)) return;

            try
            {
                var timeline = await _spaceAnalyzerService.GetFileTimelineAsync(
                    AnalysisPath,
                    StartDate,
                    EndDate,
                    Granularity);
                
                TimelineItems.Clear();
                foreach (var item in timeline)
                {
                    TimelineItems.Add(item);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to generate timeline: {ex.Message}",
                    "Timeline Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void SetGranularity(TimelineGranularity granularity)
        {
            Granularity = granularity;
        }

        [RelayCommand]
        private async void ExportTimeline()
        {
            try
            {
                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "CSV File (*.csv)|*.csv|JSON File (*.json)|*.json|Excel File (*.xlsx)|*.xlsx",
                    DefaultExt = ".csv",
                    FileName = $"Timeline_{DateTime.Now:yyyyMMdd_HHmmss}"
                };

                if (dialog.ShowDialog() == true)
                {
                    if (dialog.FileName.EndsWith(".json"))
                    {
                        var json = System.Text.Json.JsonSerializer.Serialize(TimelineItems, 
                            new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                        await System.IO.File.WriteAllTextAsync(dialog.FileName, json);
                    }
                    else
                    {
                        var csv = new System.Text.StringBuilder();
                        csv.AppendLine("Date,FileCount,TotalSize,FilesCreated,FilesModified,FilesDeleted");
                        foreach (var item in TimelineItems)
                        {
                            csv.AppendLine($"{item.Date:yyyy-MM-dd},{item.FileCount},{item.TotalSize},{item.FilesCreated},{item.FilesModified},{item.FilesDeleted}");
                        }
                        await System.IO.File.WriteAllTextAsync(dialog.FileName, csv.ToString());
                    }

                    System.Windows.MessageBox.Show(
                        $"Timeline data exported to {dialog.FileName}",
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
    /// ViewModel for network drive map
    /// </summary>
    public partial class NetworkDriveMapViewModel : ObservableObject
    {
        private readonly ISpaceAnalyzerService _spaceAnalyzerService;
        private ObservableCollection<NetworkDrive> _networkDrives = new();
        private ObservableCollection<NetworkConnection> _connections = new();

        [ObservableProperty]
        private NetworkDrive? _selectedDrive;

        [ObservableProperty]
        private bool _isScanning;

        public ObservableCollection<NetworkDrive> NetworkDrives
        {
            get => _networkDrives;
            set => SetProperty(ref _networkDrives, value);
        }

        public ObservableCollection<NetworkConnection> Connections
        {
            get => _connections;
            set => SetProperty(ref _connections, value);
        }

        public NetworkDriveMapViewModel(ISpaceAnalyzerService spaceAnalyzerService)
        {
            _spaceAnalyzerService = spaceAnalyzerService;
        }

        [RelayCommand]
        private async Task ScanNetworkDrivesAsync()
        {
            IsScanning = true;

            try
            {
                var drives = await _spaceAnalyzerService.ScanNetworkDrivesAsync();
                
                NetworkDrives.Clear();
                foreach (var drive in drives)
                {
                    NetworkDrives.Add(drive);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to scan network drives: {ex.Message}",
                    "Scan Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
            finally
            {
                IsScanning = false;
            }
        }

        [RelayCommand]
        private async Task AnalyzeDriveAsync(NetworkDrive? drive)
        {
            if (drive == null) return;

            SelectedDrive = drive;

            try
            {
                var connections = await _spaceAnalyzerService.GetNetworkConnectionsAsync(drive.Path);
                
                Connections.Clear();
                foreach (var connection in connections)
                {
                    Connections.Add(connection);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to analyze drive: {ex.Message}",
                    "Analysis Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void RefreshNetworkMap()
        {
            _ = ScanNetworkDrivesAsync();
        }
    }

    /// <summary>
    /// ViewModel for flow diagrams
    /// </summary>
    public partial class FlowDiagramViewModel : ObservableObject
    {
        private readonly ISpaceAnalyzerService _spaceAnalyzerService;
        private ObservableCollection<FlowNode> _nodes = new();
        private ObservableCollection<FlowConnection> _connections = new();

        [ObservableProperty]
        private string _rootPath = string.Empty;

        [ObservableProperty]
        private FlowDiagramType _diagramType = FlowDiagramType.FileFlow;

        [ObservableProperty]
        private int _maxDepth = 3;

        public ObservableCollection<FlowNode> Nodes
        {
            get => _nodes;
            set => SetProperty(ref _nodes, value);
        }

        public ObservableCollection<FlowConnection> Connections
        {
            get => _connections;
            set => SetProperty(ref _connections, value);
        }

        public FlowDiagramViewModel(ISpaceAnalyzerService spaceAnalyzerService)
        {
            _spaceAnalyzerService = spaceAnalyzerService;
        }

        [RelayCommand]
        private async Task GenerateDiagramAsync()
        {
            if (string.IsNullOrEmpty(RootPath)) return;

            try
            {
                var diagram = await _spaceAnalyzerService.GenerateFlowDiagramAsync(
                    RootPath,
                    DiagramType,
                    MaxDepth);
                
                Nodes.Clear();
                foreach (var node in diagram.Nodes)
                {
                    Nodes.Add(node);
                }

                Connections.Clear();
                foreach (var connection in diagram.Connections)
                {
                    Connections.Add(connection);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to generate diagram: {ex.Message}",
                    "Diagram Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void ChangeDiagramType(FlowDiagramType type)
        {
            DiagramType = type;
        }

        [RelayCommand]
        private async void ExportDiagram()
        {
            try
            {
                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "JSON File (*.json)|*.json|GraphML (*.graphml)|*.graphml|DOT File (*.dot)|*.dot",
                    DefaultExt = ".json",
                    FileName = $"FlowDiagram_{DateTime.Now:yyyyMMdd_HHmmss}"
                };

                if (dialog.ShowDialog() == true)
                {
                    var diagram = new { Nodes, Connections, Type = DiagramType, MaxDepth };
                    var json = System.Text.Json.JsonSerializer.Serialize(diagram,
                        new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                    await System.IO.File.WriteAllTextAsync(dialog.FileName, json);

                    System.Windows.MessageBox.Show(
                        $"Diagram exported to {dialog.FileName}",
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
    /// ViewModel for interactive canvas
    /// </summary>
    public partial class InteractiveCanvasViewModel : ObservableObject
    {
        private ObservableCollection<CanvasElement> _elements = new();
        private ObservableCollection<CanvasConnection> _connections = new();

        [ObservableProperty]
        private CanvasElement? _selectedElement;

        [ObservableProperty]
        private bool _isPanning;

        [ObservableProperty]
        private double _zoomLevel = 1.0;

        [ObservableProperty]
        private double _offsetX;

        [ObservableProperty]
        private double _offsetY;

        public ObservableCollection<CanvasElement> Elements
        {
            get => _elements;
            set => SetProperty(ref _elements, value);
        }

        public ObservableCollection<CanvasConnection> Connections
        {
            get => _connections;
            set => SetProperty(ref _connections, value);
        }

        public double ZoomLevel
        {
            get => _zoomLevel;
            set => SetProperty(ref _zoomLevel, Math.Max(0.1, Math.Min(5.0, value)));
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
        private void ResetZoom()
        {
            ZoomLevel = 1.0;
            OffsetX = 0;
            OffsetY = 0;
        }

        [RelayCommand]
        private void FitToScreen()
        {
            if (!Elements.Any()) return;

            var minX = Elements.Min(e => e.X);
            var maxX = Elements.Max(e => e.X + e.Width);
            var minY = Elements.Min(e => e.Y);
            var maxY = Elements.Max(e => e.Y + e.Height);

            var contentWidth = maxX - minX;
            var contentHeight = maxY - minY;

            const double viewportWidth = 1000;
            const double viewportHeight = 800;
            const double padding = 50;

            var zoomX = (viewportWidth - 2 * padding) / contentWidth;
            var zoomY = (viewportHeight - 2 * padding) / contentHeight;
            ZoomLevel = Math.Min(zoomX, zoomY);

            OffsetX = -(minX * ZoomLevel) + padding;
            OffsetY = -(minY * ZoomLevel) + padding;
        }

        [RelayCommand]
        private void AddElement(string elementType)
        {
            var element = new CanvasElement
            {
                Type = elementType,
                X = OffsetX + 100,
                Y = OffsetY + 100,
                Width = 100,
                Height = 60
            };

            Elements.Add(element);
        }

        [RelayCommand]
        private void DeleteElement(CanvasElement? element)
        {
            if (element == null) return;

            Elements.Remove(element);
            
            // Remove connections
            var connectionsToRemove = Connections
                .Where(c => c.SourceId == element.Id || c.TargetId == element.Id)
                .ToList();
            
            foreach (var connection in connectionsToRemove)
            {
                Connections.Remove(connection);
            }
        }

        [RelayCommand]
        private void ConnectElements(CanvasElement? source, CanvasElement? target)
        {
            if (source == null || target == null) return;

            var connection = new CanvasConnection
            {
                SourceId = source.Id,
                TargetId = target.Id,
                Type = "Arrow"
            };

            Connections.Add(connection);
        }

        [RelayCommand]
        private async void ExportCanvas()
        {
            try
            {
                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "JSON File (*.json)|*.json|SVG Image (*.svg)|*.svg",
                    DefaultExt = ".json",
                    FileName = $"Canvas_{DateTime.Now:yyyyMMdd_HHmmss}"
                };

                if (dialog.ShowDialog() == true)
                {
                    var canvas = new 
                    { 
                        Elements, 
                        Connections, 
                        ZoomLevel, 
                        OffsetX, 
                        OffsetY 
                    };
                    var json = System.Text.Json.JsonSerializer.Serialize(canvas,
                        new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                    await System.IO.File.WriteAllTextAsync(dialog.FileName, json);

                    System.Windows.MessageBox.Show(
                        $"Canvas exported to {dialog.FileName}",
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
    public class TreeMapItem
    {
        public string Name { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public long Size { get; set; }
        public double Percentage { get; set; }
        public string Color { get; set; } = "#0078D4";
        public bool IsDirectory { get; set; }
        public int FileCount { get; set; }
        public int FolderCount { get; set; }
        public ObservableCollection<TreeMapItem> Children { get; set; } = new();
    }

    public class DiskUsageChartItem
    {
        public string Label { get; set; } = string.Empty;
        public double Value { get; set; }
        public double Percentage { get; set; }
        public string Color { get; set; } = "#0078D4";
        public string Description { get; set; } = string.Empty;
    }

    public class FileTypeItem
    {
        public string Extension { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public long TotalSize { get; set; }
        public int FileCount { get; set; }
        public double SizePercentage { get; set; }
        public string Color { get; set; } = "#0078D4";
        public string Icon { get; set; } = "📄";
    }

    public class TimelineItem
    {
        public DateTime Date { get; set; }
        public int FileCount { get; set; }
        public long TotalSize { get; set; }
        public int FilesCreated { get; set; }
        public int FilesModified { get; set; }
        public int FilesDeleted { get; set; }
    }

    public class NetworkDrive
    {
        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string Server { get; set; } = string.Empty;
        public long TotalSpace { get; set; }
        public long FreeSpace { get; set; }
        public bool IsConnected { get; set; }
        public DateTime? LastConnected { get; set; }
    }

    public class NetworkConnection
    {
        public string SourcePath { get; set; } = string.Empty;
        public string TargetPath { get; set; } = string.Empty;
        public ConnectionType Type { get; set; }
        public long DataTransferred { get; set; }
        public DateTime LastAccess { get; set; }
    }

    public class FlowNode
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public NodeType Type { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public long Size { get; set; }
        public int FileCount { get; set; }
        public int Level { get; set; }
    }

    public class FlowConnection
    {
        public string SourceId { get; set; } = string.Empty;
        public string TargetId { get; set; } = string.Empty;
        public ConnectionType Type { get; set; }
        public long DataFlow { get; set; }
    }

    public class CanvasElement
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Type { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public string Color { get; set; } = "#0078D4";
        public object? Data { get; set; }
    }

    public class CanvasConnection
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string SourceId { get; set; } = string.Empty;
        public string TargetId { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Color { get; set; } = "#666666";
    }

    // Enums
    public enum TreeMapColorScheme
    {
        ByType,
        BySize,
        ByDate,
        ByDepth
    }

    public enum ChartType
    {
        Pie,
        Doughnut,
        Bar,
        Column
    }

    public enum DistributionView
    {
        BySize,
        ByCount,
        ByName
    }

    public enum TimelineGranularity
    {
        Hourly,
        Daily,
        Weekly,
        Monthly
    }

    public enum FlowDiagramType
    {
        FileFlow,
        Dependency,
        Hierarchy,
        Network
    }

    public enum NodeType
    {
        File,
        Folder,
        Drive,
        Network,
        Cloud
    }

    public enum ConnectionType
    {
        Reference,
        Dependency,
        Flow,
        Link
    }
}
