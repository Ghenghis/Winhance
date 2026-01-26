using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Winhance.Core.Features.FileManager.Interfaces;
using Winhance.WPF.Features.FileManager.ViewModels;

namespace Winhance.WPF.Features.FileManager.ViewModels
{
    /// <summary>
    /// ViewModel for dynamic context menus (WS-CTX features).
    /// </summary>
    public partial class ContextMenuViewModel : ObservableObject
    {
        private readonly IFileManagerService? _fileManagerService;
        private readonly IOperationQueueService? _operationQueueService;
        private readonly IServiceProvider? _serviceProvider;

        [ObservableProperty]
        private ObservableCollection<ContextMenuItemViewModel> _menuItems = new();

        [ObservableProperty]
        private ObservableCollection<FileItemViewModel> _selectedItems = new();

        [ObservableProperty]
        private FileItemViewModel? _rightClickedItem;

        [ObservableProperty]
        private bool _isRightPane;

        [ObservableProperty]
        private string _contextPath = string.Empty;

        public ContextMenuViewModel(IFileManagerService? fileManagerService, 
                                   IOperationQueueService? operationQueueService,
                                   IServiceProvider? serviceProvider)
        {
            _fileManagerService = fileManagerService;
            _operationQueueService = operationQueueService;
            _serviceProvider = serviceProvider;
        }

        /// <summary>
        /// Update context menu based on selection (WS-CTX-001).
        /// </summary>
        public void UpdateContextMenu(ObservableCollection<FileItemViewModel> selectedItems, 
                                    FileItemViewModel? rightClickedItem, 
                                    bool isRightPane, 
                                    string contextPath)
        {
            SelectedItems.Clear();
            foreach (var item in selectedItems)
            {
                SelectedItems.Add(item);
            }

            RightClickedItem = rightClickedItem;
            IsRightPane = isRightPane;
            ContextPath = contextPath;

            BuildMenuItems();
        }

        private void BuildMenuItems()
        {
            MenuItems.Clear();

            // Basic operations (always visible)
            MenuItems.Add(new ContextMenuItemViewModel
            {
                Header = "Open",
                Icon = "FolderOpen",
                Command = new RelayCommand(ExecuteOpen),
                InputGestureText = "Enter",
                IsEnabled = SelectedItems.Count <= 1 && (RightClickedItem?.IsDirectory == true || SelectedItems.Count == 1)
            });

            MenuItems.Add(new ContextMenuItemViewModel { IsSeparator = true });

            // Clipboard operations
            MenuItems.Add(new ContextMenuItemViewModel
            {
                Header = "Copy",
                Icon = "ContentCopy",
                Command = new RelayCommand(ExecuteCopy),
                InputGestureText = "Ctrl+C",
                IsEnabled = SelectedItems.Count > 0 || RightClickedItem != null
            });

            MenuItems.Add(new ContextMenuItemViewModel
            {
                Header = "Cut",
                Icon = "ContentCut",
                Command = new RelayCommand(ExecuteCut),
                InputGestureText = "Ctrl+X",
                IsEnabled = SelectedItems.Count > 0 || RightClickedItem != null
            });

            MenuItems.Add(new ContextMenuItemViewModel
            {
                Header = "Paste",
                Icon = "ContentPaste",
                Command = new RelayCommand(ExecutePaste),
                InputGestureText = "Ctrl+V",
                IsEnabled = true // Always enabled for directories
            });

            MenuItems.Add(new ContextMenuItemViewModel { IsSeparator = true });

            // File operations
            MenuItems.Add(new ContextMenuItemViewModel
            {
                Header = "Delete",
                Icon = "Delete",
                Command = new RelayCommand(ExecuteDelete),
                InputGestureText = "Del",
                IsEnabled = SelectedItems.Count > 0 || RightClickedItem != null
            });

            MenuItems.Add(new ContextMenuItemViewModel
            {
                Header = "Rename",
                Icon = "RenameBox",
                Command = new RelayCommand(ExecuteRename),
                InputGestureText = "F2",
                IsEnabled = (SelectedItems.Count == 1 && SelectedItems[0] != null) || 
                           (RightClickedItem != null && !RightClickedItem.IsParentDirectory)
            });

            MenuItems.Add(new ContextMenuItemViewModel { IsSeparator = true });

            // Create operations
            MenuItems.Add(new ContextMenuItemViewModel
            {
                Header = "New Folder",
                Icon = "FolderPlus",
                Command = new RelayCommand(ExecuteNewFolder),
                InputGestureText = "Ctrl+Shift+N",
                IsEnabled = true
            });

            MenuItems.Add(new ContextMenuItemViewModel
            {
                Header = "New File",
                Icon = "FilePlus",
                Command = new RelayCommand(ExecuteNewFile),
                IsEnabled = true
            });

            // Dynamic submenu for "Create" (WS-CTX-005)
            var createSubmenu = new List<ContextMenuItemViewModel>
            {
                new() { Header = "Text Document", Command = new RelayCommand(() => CreateNewFile(".txt")) },
                new() { Header = "Word Document", Command = new RelayCommand(() => CreateNewFile(".docx")) },
                new() { Header = "Excel Spreadsheet", Command = new RelayCommand(() => CreateNewFile(".xlsx")) },
                new() { Header = "PowerPoint Presentation", Command = new RelayCommand(() => CreateNewFile(".pptx")) },
                new() { Header = "PDF Document", Command = new RelayCommand(() => CreateNewFile(".pdf")) },
                new() { Header = "Image File", Command = new RelayCommand(() => CreateNewFile(".png")) },
                new() { Header = "Zip Archive", Command = new RelayCommand(() => CreateNewFile(".zip")) }
            };

            MenuItems.Add(new ContextMenuItemViewModel
            {
                Header = "Create",
                Icon = "Plus",
                SubItems = createSubmenu
            });

            MenuItems.Add(new ContextMenuItemViewModel { IsSeparator = true });

            // Advanced operations (WS-CTX-010 to WS-CTX-015)
            if (SelectedItems.Count > 0)
            {
                MenuItems.Add(new ContextMenuItemViewModel
                {
                    Header = "Compress",
                    Icon = "ZipBox",
                    Command = new RelayCommand(ExecuteCompress),
                    IsEnabled = SelectedItems.Count > 0
                });

                MenuItems.Add(new ContextMenuItemViewModel
                {
                    Header = "Extract",
                    Icon = "ZipBoxOutline",
                    Command = new RelayCommand(ExecuteExtract),
                    IsEnabled = SelectedItems.Any(i => i.IsArchive)
                });

                MenuItems.Add(new ContextMenuItemViewModel
                {
                    Header = "Encrypt",
                    Icon = "Lock",
                    Command = new RelayCommand(ExecuteEncrypt),
                    IsEnabled = SelectedItems.Count > 0
                });

                MenuItems.Add(new ContextMenuItemViewModel { IsSeparator = true });

                // Selection operations submenu
                var selectionSubmenu = new List<ContextMenuItemViewModel>
                {
                    new() { Header = "Select All", Command = new RelayCommand(ExecuteSelectAll), InputGestureText = "Ctrl+A" },
                    new() { Header = "Invert Selection", Command = new RelayCommand(ExecuteInvertSelection), InputGestureText = "Ctrl+I" },
                    new() { Header = "Select Same Type", Command = new RelayCommand(ExecuteSelectSameType) },
                    new() { Header = "Select Same Size", Command = new RelayCommand(ExecuteSelectSameSize) },
                    new() { Header = "Select Same Date", Command = new RelayCommand(ExecuteSelectSameDate) }
                };

                MenuItems.Add(new ContextMenuItemViewModel
                {
                    Header = "Selection",
                    Icon = "CheckboxMultipleMarked",
                    SubItems = selectionSubmenu
                });
            }

            // Compare with other panel (WS-CTX-017)
            MenuItems.Add(new ContextMenuItemViewModel
            {
                Header = "Compare with Other Panel",
                Icon = "Compare",
                Command = new RelayCommand(ExecuteCompare),
                IsEnabled = SelectedItems.Count > 0
            });

            MenuItems.Add(new ContextMenuItemViewModel { IsSeparator = true });

            // System operations
            MenuItems.Add(new ContextMenuItemViewModel
            {
                Header = "Open in Terminal",
                Icon = "Console",
                Command = new RelayCommand(ExecuteOpenTerminal),
                IsEnabled = true
            });

            MenuItems.Add(new ContextMenuItemViewModel
            {
                Header = "Open in Explorer",
                Icon = "FolderOutline",
                Command = new RelayCommand(ExecuteOpenExplorer),
                IsEnabled = RightClickedItem != null || !string.IsNullOrEmpty(ContextPath)
            });

            MenuItems.Add(new ContextMenuItemViewModel
            {
                Header = "Copy Path",
                Icon = "ContentCopy",
                Command = new RelayCommand(ExecuteCopyPath),
                IsEnabled = RightClickedItem != null
            });

            MenuItems.Add(new ContextMenuItemViewModel { IsSeparator = true });

            // Properties
            MenuItems.Add(new ContextMenuItemViewModel
            {
                Header = "Properties",
                Icon = "InformationOutline",
                Command = new RelayCommand(ExecuteProperties),
                InputGestureText = "Alt+Enter",
                IsEnabled = RightClickedItem != null || SelectedItems.Count > 0
            });
        }

        // Command implementations
        private void ExecuteOpen() => _serviceProvider?.GetService<DualPaneBrowserViewModel>()?.OpenItemCommand.Execute(RightClickedItem);
        private void ExecuteCopy() => _serviceProvider?.GetService<DualPaneBrowserViewModel>()?.CopyToClipboardCommand.Execute(null);
        private void ExecuteCut() => _serviceProvider?.GetService<DualPaneBrowserViewModel>()?.CutToClipboardCommand.Execute(null);
        private void ExecutePaste() => _serviceProvider?.GetService<DualPaneBrowserViewModel>()?.PasteFromClipboardCommand.Execute(null);
        private void ExecuteDelete() => _serviceProvider?.GetService<DualPaneBrowserViewModel>()?.DeleteCommand.Execute(null);
        private void ExecuteRename() => _serviceProvider?.GetService<DualPaneBrowserViewModel>()?.StartRenameCommand.Execute(null);
        private void ExecuteNewFolder() => _serviceProvider?.GetService<DualPaneBrowserViewModel>()?.NewFolderCommand.Execute(null);
        private void ExecuteNewFile() => CreateNewFile(".txt");
        private void ExecuteCompress() => _operationQueueService?.QueueCompressOperationAsync(GetSelectedPaths());
        private void ExecuteExtract() => _operationQueueService?.QueueExtractOperationAsync(GetSelectedPaths());
        private void ExecuteEncrypt() => _operationQueueService?.QueueEncryptOperationAsync(GetSelectedPaths());
        private void ExecuteSelectAll() => _serviceProvider?.GetService<DualPaneBrowserViewModel>()?.SelectAllCommand.Execute(null);
        private void ExecuteInvertSelection() => _serviceProvider?.GetService<DualPaneBrowserViewModel>()?.InvertSelectionCommand.Execute(null);
        private void ExecuteSelectSameType() => SelectByType();
        private void ExecuteSelectSameSize() => SelectBySize();
        private void ExecuteSelectSameDate() => SelectByDate();
        private void ExecuteCompare() => _serviceProvider?.GetService<DualPaneBrowserViewModel>()?.CompareWithOtherPanelCommand.Execute(null);
        private void ExecuteOpenTerminal() => OpenTerminalAtPath();
        private void ExecuteOpenExplorer() => _serviceProvider?.GetService<DualPaneBrowserViewModel>()?.OpenInExplorerCommand.Execute(null);
        private void ExecuteCopyPath() => CopyPathToClipboard();
        private void ExecuteProperties() => _serviceProvider?.GetService<DualPaneBrowserViewModel>()?.ShowPropertiesCommand.Execute(null);

        private void CreateNewFile(string extension)
        {
            var fileName = $"New File{extension}";
            var fullPath = System.IO.Path.Combine(ContextPath, fileName);
            System.IO.File.WriteAllText(fullPath, string.Empty);
            _serviceProvider?.GetService<DualPaneBrowserViewModel>()?.RefreshCommand.Execute(null);
        }

        private List<string> GetSelectedPaths()
        {
            var items = RightClickedItem != null ? new[] { RightClickedItem } : SelectedItems.ToArray();
            return items.Where(i => i != null && !i.IsParentDirectory).Select(i => i.FullPath).ToList();
        }

        private void SelectByType()
        {
            // Implementation for selecting files of same type
        }

        private void SelectBySize()
        {
            // Implementation for selecting files of same size
        }

        private void SelectByDate()
        {
            // Implementation for selecting files with same date
        }

        private void OpenTerminalAtPath()
        {
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/k cd /d \"{ContextPath}\"",
                UseShellExecute = true
            };
            System.Diagnostics.Process.Start(startInfo);
        }

        private void CopyPathToClipboard()
        {
            if (RightClickedItem != null)
            {
                Clipboard.SetText(RightClickedItem.FullPath);
            }
        }
    }

    /// <summary>
    /// ViewModel for a context menu item.
    /// </summary>
    public partial class ContextMenuItemViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _header = string.Empty;

        [ObservableProperty]
        private string _icon = string.Empty;

        [ObservableProperty]
        private ICommand? _command;

        [ObservableProperty]
        private string _inputGestureText = string.Empty;

        [ObservableProperty]
        private bool _isEnabled = true;

        [ObservableProperty]
        private bool _isSeparator;

        [ObservableProperty]
        private List<ContextMenuItemViewModel>? _subItems;
    }

    /// <summary>
    /// Extension methods for FileItemViewModel.
    /// </summary>
    public static class FileItemViewModelExtensions
    {
        public static bool IsArchive(this FileItemViewModel item)
        {
            var extension = item.Extension?.ToLowerInvariant();
            return extension == ".zip" || extension == ".rar" || extension == ".7z" || 
                   extension == ".tar" || extension == ".gz" || extension == ".bz2";
        }
    }
}
