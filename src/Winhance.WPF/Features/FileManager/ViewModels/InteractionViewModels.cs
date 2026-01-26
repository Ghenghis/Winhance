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
    /// ViewModel for context menu
    /// </summary>
    public partial class ContextMenuViewModel : ObservableObject
    {
        private readonly IContextMenuService _contextMenuService;
        private ObservableCollection<ContextMenuItem> _menuItems = new();
        private ObservableCollection<FileItem> _selectedFiles = new();

        [ObservableProperty]
        private bool _isVisible;

        [ObservableProperty]
        private double _xPosition;

        [ObservableProperty]
        private double _yPosition;

        public ObservableCollection<ContextMenuItem> MenuItems
        {
            get => _menuItems;
            set => SetProperty(ref _menuItems, value);
        }

        public ObservableCollection<FileItem> SelectedFiles
        {
            get => _selectedFiles;
            set => SetProperty(ref _selectedFiles, value);
        }

        public ContextMenuViewModel(IContextMenuService contextMenuService)
        {
            _contextMenuService = contextMenuService;
            InitializeMenuItems();
        }

        private void InitializeMenuItems()
        {
            // Standard operations
            MenuItems.Add(new ContextMenuItem
            {
                Header = "Open",
                Icon = "📂",
                Command = new AsyncRelayCommand<FileItem>(OpenFileAsync),
                IsEnabled = true
            });

            MenuItems.Add(new ContextMenuItem
            {
                Header = "Open with...",
                Icon = "🔧",
                Command = new AsyncRelayCommand<FileItem>(OpenWithAsync),
                IsEnabled = true
            });

            MenuItems.Add(new ContextMenuItem { IsSeparator = true });

            MenuItems.Add(new ContextMenuItem
            {
                Header = "Cut",
                Icon = "✂️",
                Command = new AsyncRelayCommand(CutAsync),
                Shortcut = "Ctrl+X",
                IsEnabled = true
            });

            MenuItems.Add(new ContextMenuItem
            {
                Header = "Copy",
                Icon = "📋",
                Command = new AsyncRelayCommand(CopyAsync),
                Shortcut = "Ctrl+C",
                IsEnabled = true
            });

            MenuItems.Add(new ContextMenuItem
            {
                Header = "Paste",
                Icon = "📌",
                Command = new AsyncRelayCommand(PasteAsync),
                Shortcut = "Ctrl+V",
                IsEnabled = true
            });

            MenuItems.Add(new ContextMenuItem { IsSeparator = true });

            MenuItems.Add(new ContextMenuItem
            {
                Header = "Delete",
                Icon = "🗑️",
                Command = new AsyncRelayCommand(DeleteAsync),
                Shortcut = "Del",
                IsEnabled = true
            });

            MenuItems.Add(new ContextMenuItem
            {
                Header = "Rename",
                Icon = "✏️",
                Command = new AsyncRelayCommand<FileItem>(RenameAsync),
                Shortcut = "F2",
                IsEnabled = true
            });

            MenuItems.Add(new ContextMenuItem { IsSeparator = true });

            // Advanced operations submenu
            var advancedMenu = new ContextMenuItem
            {
                Header = "Advanced",
                Icon = "⚙️",
                IsEnabled = true
            };

            advancedMenu.SubItems.Add(new ContextMenuItem
            {
                Header = "Create Hard Link",
                Command = new AsyncRelayCommand<FileItem>(CreateHardLinkAsync)
            });

            advancedMenu.SubItems.Add(new ContextMenuItem
            {
                Header = "Create Symbolic Link",
                Command = new AsyncRelayCommand<FileItem>(CreateSymbolicLinkAsync)
            });

            advancedMenu.SubItems.Add(new ContextMenuItem
            {
                Header = "Calculate Hash",
                Command = new AsyncRelayCommand<FileItem>(CalculateHashAsync)
            });

            MenuItems.Add(advancedMenu);
        }

        [RelayCommand]
        private async Task OpenFileAsync(FileItem? file)
        {
            if (file == null) return;

            try
            {
                await System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = file.FullPath,
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
        private async Task OpenWithAsync(FileItem? file)
        {
            if (file == null) return;

            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "rundll32.exe",
                    Arguments = $"shell32.dll,OpenAs_RunDLL {file.FullPath}",
                    UseShellExecute = true
                };
                System.Diagnostics.Process.Start(psi);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to show 'Open With' dialog: {ex.Message}",
                    "Open With Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
            await Task.CompletedTask;
        }

        [RelayCommand]
        private async Task CutAsync()
        {
            if (SelectedFiles.Count == 0) return;

            try
            {
                await _contextMenuService.CutFilesAsync(SelectedFiles.Select(f => f.FullPath).ToList());
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Cut operation failed: {ex.Message}",
                    "Cut Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task CopyAsync()
        {
            if (SelectedFiles.Count == 0) return;

            try
            {
                await _contextMenuService.CopyFilesAsync(SelectedFiles.Select(f => f.FullPath).ToList());
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Copy operation failed: {ex.Message}",
                    "Copy Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task PasteAsync()
        {
            try
            {
                var targetPath = SelectedFiles.FirstOrDefault()?.FullPath ?? Environment.CurrentDirectory;
                await _contextMenuService.PasteFilesAsync(targetPath);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Paste operation failed: {ex.Message}",
                    "Paste Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task DeleteAsync()
        {
            if (SelectedFiles.Count == 0) return;

            try
            {
                var result = System.Windows.MessageBox.Show(
                    $"Are you sure you want to delete {SelectedFiles.Count} item(s)?",
                    "Confirm Delete",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Warning);

                if (result == System.Windows.MessageBoxResult.Yes)
                {
                    await _contextMenuService.DeleteFilesAsync(SelectedFiles.Select(f => f.FullPath).ToList());
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Delete operation failed: {ex.Message}",
                    "Delete Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task RenameAsync(FileItem? file)
        {
            if (file == null) return;

            var newName = Microsoft.VisualBasic.Interaction.InputBox(
                "Enter new name:",
                "Rename",
                System.IO.Path.GetFileName(file.FullPath));

            if (!string.IsNullOrEmpty(newName))
            {
                try
                {
                    await _contextMenuService.RenameFileAsync(file.FullPath, newName);
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show(
                        $"Rename failed: {ex.Message}",
                        "Rename Error",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Error);
                }
            }
        }

        [RelayCommand]
        private async Task CreateHardLinkAsync(FileItem? file)
        {
            if (file == null) return;

            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Create Hard Link",
                FileName = System.IO.Path.GetFileName(file.FullPath)
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    await _contextMenuService.CreateHardLinkAsync(file.FullPath, dialog.FileName);
                    System.Windows.MessageBox.Show(
                        "Hard link created successfully.",
                        "Success",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show(
                        $"Failed to create hard link: {ex.Message}",
                        "Error",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Error);
                }
            }
        }

        [RelayCommand]
        private async Task CreateSymbolicLinkAsync(FileItem? file)
        {
            if (file == null) return;

            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Create Symbolic Link",
                FileName = System.IO.Path.GetFileName(file.FullPath)
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    await _contextMenuService.CreateSymbolicLinkAsync(file.FullPath, dialog.FileName);
                    System.Windows.MessageBox.Show(
                        "Symbolic link created successfully.",
                        "Success",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show(
                        $"Failed to create symbolic link: {ex.Message}",
                        "Error",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Error);
                }
            }
        }

        [RelayCommand]
        private async Task CalculateHashAsync(FileItem? file)
        {
            if (file == null) return;

            try
            {
                var md5 = await _contextMenuService.CalculateHashAsync(file.FullPath, "MD5");
                var sha256 = await _contextMenuService.CalculateHashAsync(file.FullPath, "SHA256");

                var message = $"File: {file.Name}\n\n" +
                             $"MD5: {md5}\n" +
                             $"SHA256: {sha256}";

                System.Windows.MessageBox.Show(message, "File Hashes",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to calculate hash: {ex.Message}",
                    "Hash Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        public void ShowMenu(double x, double y, ObservableCollection<FileItem> selectedFiles)
        {
            XPosition = x;
            YPosition = y;
            SelectedFiles = selectedFiles;
            IsVisible = true;

            // Update menu item states based on selection
            UpdateMenuStates();
        }

        public void HideMenu()
        {
            IsVisible = false;
        }

        private void UpdateMenuStates()
        {
            var hasSelection = SelectedFiles.Count > 0;
            var singleSelection = SelectedFiles.Count == 1;

            foreach (var item in MenuItems)
            {
                if (!item.IsSeparator)
                {
                    item.IsEnabled = hasSelection || item.Header == "Paste";
                }
            }
        }
    }

    /// <summary>
    /// ViewModel for keyboard shortcuts
    /// </summary>
    public partial class KeyboardShortcutsViewModel : ObservableObject
    {
        private readonly IFileManagerService _fileManagerService;
        private ObservableCollection<KeyboardShortcut> _shortcuts = new();

        public ObservableCollection<KeyboardShortcut> Shortcuts
        {
            get => _shortcuts;
            set => SetProperty(ref _shortcuts, value);
        }

        public KeyboardShortcutsViewModel(IFileManagerService fileManagerService)
        {
            _fileManagerService = fileManagerService;
            InitializeShortcuts();
        }

        private void InitializeShortcuts()
        {
            Shortcuts.Add(new KeyboardShortcut
            {
                Key = "Ctrl+C",
                Description = "Copy selected items",
                Action = CopyAsync
            });

            Shortcuts.Add(new KeyboardShortcut
            {
                Key = "Ctrl+X",
                Description = "Cut selected items",
                Action = CutAsync
            });

            Shortcuts.Add(new KeyboardShortcut
            {
                Key = "Ctrl+V",
                Description = "Paste items",
                Action = PasteAsync
            });

            Shortcuts.Add(new KeyboardShortcut
            {
                Key = "Delete",
                Description = "Delete selected items",
                Action = DeleteAsync
            });

            Shortcuts.Add(new KeyboardShortcut
            {
                Key = "F2",
                Description = "Rename selected item",
                Action = RenameAsync
            });

            Shortcuts.Add(new KeyboardShortcut
            {
                Key = "F5",
                Description = "Refresh current view",
                Action = RefreshAsync
            });

            Shortcuts.Add(new KeyboardShortcut
            {
                Key = "Ctrl+A",
                Description = "Select all items",
                Action = SelectAllAsync
            });

            Shortcuts.Add(new KeyboardShortcut
            {
                Key = "Ctrl+N",
                Description = "Create new folder",
                Action = NewFolderAsync
            });

            Shortcuts.Add(new KeyboardShortcut
            {
                Key = "Ctrl+Shift+N",
                Description = "Create new file",
                Action = NewFileAsync
            });

            Shortcuts.Add(new KeyboardShortcut
            {
                Key = "Ctrl+F",
                Description = "Focus search box",
                Action = FocusSearchAsync
            });

            Shortcuts.Add(new KeyboardShortcut
            {
                Key = "Ctrl+T",
                Description = "Open new tab",
                Action = NewTabAsync
            });

            Shortcuts.Add(new KeyboardShortcut
            {
                Key = "Ctrl+W",
                Description = "Close current tab",
                Action = CloseTabAsync
            });

            Shortcuts.Add(new KeyboardShortcut
            {
                Key = "Ctrl+Tab",
                Description = "Switch to next tab",
                Action = NextTabAsync
            });

            Shortcuts.Add(new KeyboardShortcut
            {
                Key = "Ctrl+Shift+Tab",
                Description = "Switch to previous tab",
                Action = PreviousTabAsync
            });

            Shortcuts.Add(new KeyboardShortcut
            {
                Key = "Backspace",
                Description = "Navigate to parent folder",
                Action = NavigateUpAsync
            });

            Shortcuts.Add(new KeyboardShortcut
            {
                Key = "Alt+Left",
                Description = "Navigate back",
                Action = NavigateBackAsync
            });

            Shortcuts.Add(new KeyboardShortcut
            {
                Key = "Alt+Right",
                Description = "Navigate forward",
                Action = NavigateForwardAsync
            });
        }

        [RelayCommand]
        private async Task CopyAsync()
        {
            await _fileManagerService.CopyToClipboardAsync();
        }

        [RelayCommand]
        private async Task CutAsync()
        {
            await _fileManagerService.CutToClipboardAsync();
        }

        [RelayCommand]
        private async Task PasteAsync()
        {
            await _fileManagerService.PasteFromClipboardAsync();
        }

        [RelayCommand]
        private async Task DeleteAsync()
        {
            await _fileManagerService.DeleteSelectedAsync();
        }

        [RelayCommand]
        private async Task RenameAsync()
        {
            await _fileManagerService.StartRenameAsync();
        }

        [RelayCommand]
        private async Task RefreshAsync()
        {
            await _fileManagerService.RefreshCurrentViewAsync();
        }

        [RelayCommand]
        private async Task SelectAllAsync()
        {
            await _fileManagerService.SelectAllItemsAsync();
        }

        [RelayCommand]
        private async Task NewFolderAsync()
        {
            await _fileManagerService.CreateNewFolderAsync("New Folder");
        }

        [RelayCommand]
        private async Task NewFileAsync()
        {
            await _fileManagerService.CreateNewFileAsync("New File.txt");
        }

        [RelayCommand]
        private async Task FocusSearchAsync()
        {
            await _fileManagerService.FocusSearchBoxAsync();
        }

        [RelayCommand]
        private async Task NewTabAsync()
        {
            await _fileManagerService.OpenNewTabAsync();
        }

        [RelayCommand]
        private async Task CloseTabAsync()
        {
            await _fileManagerService.CloseCurrentTabAsync();
        }

        [RelayCommand]
        private async Task NextTabAsync()
        {
            await _fileManagerService.SwitchToNextTabAsync();
        }

        [RelayCommand]
        private async Task PreviousTabAsync()
        {
            await _fileManagerService.SwitchToPreviousTabAsync();
        }

        [RelayCommand]
        private async Task NavigateUpAsync()
        {
            await _fileManagerService.NavigateToParentAsync();
        }

        [RelayCommand]
        private async Task NavigateBackAsync()
        {
            await _fileManagerService.NavigateBackAsync();
        }

        [RelayCommand]
        private async Task NavigateForwardAsync()
        {
            await _fileManagerService.NavigateForwardAsync();
        }
    }

    /// <summary>
    /// ViewModel for drag and drop
    /// </summary>
    public partial class DragDropViewModel : ObservableObject
    {
        private readonly IFileManagerService _fileManagerService;
        private ObservableCollection<FileItem> _draggedFiles = new();

        [ObservableProperty]
        private bool _isDragging;

        [ObservableProperty]
        private string? _dropTargetPath;

        [ObservableProperty]
        private DragDropEffect _dropEffect = DragDropEffect.Copy;

        public ObservableCollection<FileItem> DraggedFiles
        {
            get => _draggedFiles;
            set => SetProperty(ref _draggedFiles, value);
        }

        public DragDropViewModel(IFileManagerService fileManagerService)
        {
            _fileManagerService = fileManagerService;
        }

        public void StartDrag(ObservableCollection<FileItem> files)
        {
            DraggedFiles = new ObservableCollection<FileItem>(files);
            IsDragging = true;
        }

        public void EndDrag()
        {
            IsDragging = false;
            DraggedFiles.Clear();
            DropTargetPath = null;
        }

        [RelayCommand]
        private async Task DropAsync()
        {
            if (DraggedFiles.Count == 0 || string.IsNullOrEmpty(DropTargetPath)) return;

            try
            {
                switch (DropEffect)
                {
                    case DragDropEffect.Copy:
                        await _fileManagerService.CopyFilesAsync(
                            DraggedFiles.Select(f => f.FullPath),
                            DropTargetPath);
                        break;
                    case DragDropEffect.Move:
                        await _fileManagerService.MoveFilesAsync(
                            DraggedFiles.Select(f => f.FullPath),
                            DropTargetPath);
                        break;
                    case DragDropEffect.Link:
                        foreach (var file in DraggedFiles)
                        {
                            var linkPath = System.IO.Path.Combine(DropTargetPath, file.Name + ".lnk");
                            await _fileManagerService.CreateShortcutAsync(file.FullPath, linkPath);
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Drop operation failed: {ex.Message}",
                    "Drop Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
            finally
            {
                EndDrag();
            }
        }

        public void UpdateDropEffect(bool isCtrlPressed, bool isShiftPressed, bool isAltPressed)
        {
            if (isCtrlPressed && isShiftPressed)
            {
                DropEffect = DragDropEffect.Link;
            }
            else if (isCtrlPressed)
            {
                DropEffect = DragDropEffect.Copy;
            }
            else if (isShiftPressed)
            {
                DropEffect = DragDropEffect.Move;
            }
            else
            {
                DropEffect = DragDropEffect.Copy; // Default
            }
        }
    }

    // Model classes
    public class ContextMenuItem
    {
        public string Header { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public ICommand? Command { get; set; }
        public string Shortcut { get; set; } = string.Empty;
        public bool IsEnabled { get; set; } = true;
        public bool IsSeparator { get; set; }
        public ObservableCollection<ContextMenuItem> SubItems { get; set; } = new();
    }

    public class KeyboardShortcut
    {
        public string Key { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public ICommand? Action { get; set; }
    }

    public enum DragDropEffect
    {
        Copy,
        Move,
        Link
    }
}
