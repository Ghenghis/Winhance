using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Winhance.WPF.Features.FileManager.ViewModels;

public partial class FolderTreeViewModel : ObservableObject
{
    [ObservableProperty]
    private ObservableCollection<FolderTreeNodeViewModel> _rootNodes = new();

    [ObservableProperty]
    private FolderTreeNodeViewModel? _selectedNode;

    public event EventHandler<string>? FolderSelected;
    public event EventHandler<string>? OpenInNewTabRequested;

    public FolderTreeViewModel()
    {
        LoadDrives();
    }

    private void LoadDrives()
    {
        RootNodes.Clear();
        foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady))
        {
            var node = new FolderTreeNodeViewModel(drive.RootDirectory.FullName, drive.Name, true)
            {
                Icon = GetDriveIcon(drive.DriveType)
            };
            node.ExpandRequested += OnNodeExpandRequested;
            node.Selected += OnNodeSelected;
            RootNodes.Add(node);
        }
    }

    private static string GetDriveIcon(DriveType driveType) => driveType switch
    {
        DriveType.Removable => "UsbFlashDrive",
        DriveType.Network => "NetworkOutline",
        DriveType.CDRom => "Disc",
        _ => "HarddiskPlus"
    };

    private void OnNodeExpandRequested(object? sender, FolderTreeNodeViewModel node)
    {
        if (!node.IsLoaded)
        {
            LoadChildren(node);
        }
    }

    private void OnNodeSelected(object? sender, FolderTreeNodeViewModel node)
    {
        SelectedNode = node;
        FolderSelected?.Invoke(this, node.Path);
    }

    private void LoadChildren(FolderTreeNodeViewModel node)
    {
        node.Children.Clear();
        try
        {
            var directories = Directory.GetDirectories(node.Path)
                .Where(d => !IsSystemOrHidden(d))
                .OrderBy(d => Path.GetFileName(d));

            foreach (var dir in directories)
            {
                var childNode = new FolderTreeNodeViewModel(dir, Path.GetFileName(dir), HasSubDirectories(dir))
                {
                    Icon = "Folder"
                };
                childNode.ExpandRequested += OnNodeExpandRequested;
                childNode.Selected += OnNodeSelected;
                node.Children.Add(childNode);
            }
            node.IsLoaded = true;
        }
        catch (UnauthorizedAccessException)
        {
            node.IsLoaded = true;
        }
        catch (IOException)
        {
            node.IsLoaded = true;
        }
    }

    private static bool IsSystemOrHidden(string path)
    {
        try
        {
            var attrs = File.GetAttributes(path);
            return (attrs & FileAttributes.Hidden) != 0 || (attrs & FileAttributes.System) != 0;
        }
        catch
        {
            return true;
        }
    }

    private static bool HasSubDirectories(string path)
    {
        try
        {
            return Directory.EnumerateDirectories(path).Any();
        }
        catch
        {
            return false;
        }
    }

    [RelayCommand]
    private void Refresh()
    {
        LoadDrives();
    }

    [RelayCommand]
    private void CollapseAll()
    {
        foreach (var node in RootNodes)
        {
            CollapseNode(node);
        }
    }

    private void CollapseNode(FolderTreeNodeViewModel node)
    {
        node.IsExpanded = false;
        foreach (var child in node.Children)
        {
            CollapseNode(child);
        }
    }

    [RelayCommand]
    private void ExpandAll()
    {
        if (SelectedNode != null)
        {
            ExpandNode(SelectedNode, 2);
        }
    }

    private void ExpandNode(FolderTreeNodeViewModel node, int depth)
    {
        if (depth <= 0) return;
        node.IsExpanded = true;
        foreach (var child in node.Children)
        {
            ExpandNode(child, depth - 1);
        }
    }

    [RelayCommand]
    private void OpenSelected()
    {
        if (SelectedNode != null)
        {
            FolderSelected?.Invoke(this, SelectedNode.Path);
        }
    }

    [RelayCommand]
    private void OpenInNewTab()
    {
        if (SelectedNode != null)
        {
            OpenInNewTabRequested?.Invoke(this, SelectedNode.Path);
        }
    }

    [RelayCommand]
    private void ShowProperties()
    {
        // Properties dialog will be handled by parent
    }

    public void NavigateToPath(string path)
    {
        var parts = path.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return;

        var driveLetter = parts[0] + Path.DirectorySeparatorChar;
        var driveNode = RootNodes.FirstOrDefault(n => n.Path.Equals(driveLetter, StringComparison.OrdinalIgnoreCase));
        if (driveNode == null) return;

        var currentNode = driveNode;
        currentNode.IsExpanded = true;

        for (int i = 1; i < parts.Length; i++)
        {
            var childNode = currentNode.Children.FirstOrDefault(n => 
                n.Name.Equals(parts[i], StringComparison.OrdinalIgnoreCase));
            if (childNode == null) break;
            childNode.IsExpanded = true;
            currentNode = childNode;
        }

        currentNode.IsSelected = true;
        SelectedNode = currentNode;
    }
}

public partial class FolderTreeNodeViewModel : ObservableObject
{
    public string Path { get; }
    public string Name { get; }

    [ObservableProperty]
    private string _icon = "Folder";

    [ObservableProperty]
    private bool _isExpanded;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isLoaded;

    public bool HasChildren { get; }

    public ObservableCollection<FolderTreeNodeViewModel> Children { get; } = new();

    public event EventHandler<FolderTreeNodeViewModel>? ExpandRequested;
    public event EventHandler<FolderTreeNodeViewModel>? Selected;

    public FolderTreeNodeViewModel(string path, string name, bool hasChildren)
    {
        Path = path;
        Name = name;
        HasChildren = hasChildren;

        if (hasChildren)
        {
            Children.Add(new FolderTreeNodeViewModel("", "Loading...", false));
        }
    }

    partial void OnIsExpandedChanged(bool value)
    {
        if (value && !IsLoaded)
        {
            ExpandRequested?.Invoke(this, this);
        }
    }

    partial void OnIsSelectedChanged(bool value)
    {
        if (value)
        {
            Selected?.Invoke(this, this);
        }
    }
}
