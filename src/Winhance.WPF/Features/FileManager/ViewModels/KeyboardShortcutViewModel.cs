using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Winhance.WPF.Features.FileManager.ViewModels;

namespace Winhance.WPF.Features.FileManager.ViewModels
{
    /// <summary>
    /// ViewModel for managing keyboard shortcuts (WS-KEY features).
    /// </summary>
    public partial class KeyboardShortcutViewModel : ObservableObject
    {
        private readonly DualPaneBrowserViewModel? _dualPaneViewModel;

        [ObservableProperty]
        private Dictionary<Key, List<ShortcutDefinition>> _shortcuts = new();

        [ObservableProperty]
        private string _shortcutInfo = string.Empty;

        public KeyboardShortcutViewModel(DualPaneBrowserViewModel? dualPaneViewModel)
        {
            _dualPaneViewModel = dualPaneViewModel;
            InitializeShortcuts();
        }

        private void InitializeShortcuts()
        {
            // Navigation shortcuts
            Shortcuts[Key.F3] = new List<ShortcutDefinition>
            {
                new() { Modifiers = ModifierKeys.None, Action = "Search", Description = "Open search dialog" }
            };

            Shortcuts[Key.F4] = new List<ShortcutDefinition>
            {
                new() { Modifiers = ModifierKeys.Alt, Action = "AddressBar", Description = "Focus address bar" }
            };

            Shortcuts[Key.F5] = new List<ShortcutDefinition>
            {
                new() { Modifiers = ModifierKeys.None, Action = "Refresh", Description = "Refresh current view" },
                new() { Modifiers = ModifierKeys.Control, Action = "RefreshAll", Description = "Refresh all panes" }
            };

            Shortcuts[Key.F6] = new List<ShortcutDefinition>
            {
                new() { Modifiers = ModifierKeys.None, Action = "TogglePane", Description = "Switch between panes" },
                new() { Modifiers = ModifierKeys.Control, Action = "ToggleQuickAccess", Description = "Toggle quick access sidebar" }
            };

            Shortcuts[Key.F7] = new List<ShortcutDefinition>
            {
                new() { Modifiers = ModifierKeys.None, Action = "NewFolder", Description = "Create new folder" }
            };

            Shortcuts[Key.F8] = new List<ShortcutDefinition>
            {
                new() { Modifiers = ModifierKeys.None, Action = "Rename", Description = "Rename selected item" }
            };

            Shortcuts[Key.F10] = new List<ShortcutDefinition>
            {
                new() { Modifiers = ModifierKeys.Shift, Action = "ContextMenu", Description = "Open context menu" }
            };

            Shortcuts[Key.F11] = new List<ShortcutDefinition>
            {
                new() { Modifiers = ModifierKeys.None, Action = "Fullscreen", Description = "Toggle fullscreen" }
            };

            Shortcuts[Key.F12] = new List<ShortcutDefinition>
            {
                new() { Modifiers = ModifierKeys.None, Action = "Properties", Description = "Show properties" }
            };

            // View mode shortcuts
            Shortcuts[Key.D1] = new List<ShortcutDefinition>
            {
                new() { Modifiers = ModifierKeys.Control, Action = "DetailsView", Description = "Details view" }
            };

            Shortcuts[Key.D2] = new List<ShortcutDefinition>
            {
                new() { Modifiers = ModifierKeys.Control, Action = "IconsView", Description = "Icons view" }
            };

            Shortcuts[Key.D3] = new List<ShortcutDefinition>
            {
                new() { Modifiers = ModifierKeys.Control, Action = "ListView", Description = "List view" }
            };

            Shortcuts[Key.D4] = new List<ShortcutDefinition>
            {
                new() { Modifiers = ModifierKeys.Control, Action = "TilesView", Description = "Tiles view" }
            };

            Shortcuts[Key.D5] = new List<ShortcutDefinition>
            {
                new() { Modifiers = ModifierKeys.Control, Action = "ThumbnailsView", Description = "Thumbnails view" }
            };

            // Panel management shortcuts
            Shortcuts[Key.Tab] = new List<ShortcutDefinition>
            {
                new() { Modifiers = ModifierKeys.Control, Action = "NextTab", Description = "Switch to next tab" },
                new() { Modifiers = ModifierKeys.Control | ModifierKeys.Shift, Action = "PreviousTab", Description = "Switch to previous tab" }
            };

            Shortcuts[Key.W] = new List<ShortcutDefinition>
            {
                new() { Modifiers = ModifierKeys.Control, Action = "CloseTab", Description = "Close current tab" },
                new() { Modifiers = ModifierKeys.Control | ModifierKeys.Shift, Action = "CloseAllTabs", Description = "Close all tabs" }
            };

            Shortcuts[Key.T] = new List<ShortcutDefinition>
            {
                new() { Modifiers = ModifierKeys.Control, Action = "NewTab", Description = "Create new tab" },
                new() { Modifiers = ModifierKeys.Control | ModifierKeys.Shift, Action = "CloneTab", Description = "Clone current tab" }
            };

            // Advanced operations shortcuts
            Shortcuts[Key.F] = new List<ShortcutDefinition>
            {
                new() { Modifiers = ModifierKeys.Control, Action = "Filter", Description = "Focus filter box" },
                new() { Modifiers = ModifierKeys.Control | ModifierKeys.Shift, Action = "AdvancedFilter", Description = "Advanced filter dialog" }
            };

            Shortcuts[Key.G] = new List<ShortcutDefinition>
            {
                new() { Modifiers = ModifierKeys.Control, Action = "Goto", Description = "Go to folder dialog" }
            };

            Shortcuts[Key.R] = new List<ShortcutDefinition>
            {
                new() { Modifiers = ModifierKeys.Control, Action = "CalculateSize", Description = "Calculate selected size" }
            };

            Shortcuts[Key.Space] = new List<ShortcutDefinition>
            {
                new() { Modifiers = ModifierKeys.Control, Action = "Preview", Description = "Toggle preview panel" },
                new() { Modifiers = ModifierKeys.Control | ModifierKeys.Shift, Action = "QuickView", Description = "Quick view selected file" }
            };

            Shortcuts[Key.OemPlus] = new List<ShortcutDefinition>
            {
                new() { Modifiers = ModifierKeys.Control, Action = "ZoomIn", Description = "Increase icon size" }
            };

            Shortcuts[Key.OemMinus] = new List<ShortcutDefinition>
            {
                new() { Modifiers = ModifierKeys.Control, Action = "ZoomOut", Description = "Decrease icon size" }
            };

            Shortcuts[Key.D0] = new List<ShortcutDefinition>
            {
                new() { Modifiers = ModifierKeys.Control, Action = "ZoomReset", Description = "Reset icon size" }
            };

            // Special shortcuts
            Shortcuts[Key.Pause] = new List<ShortcutDefinition>
            {
                new() { Modifiers = ModifierKeys.Control, Action = "PauseOperations", Description = "Pause background operations" }
            };

            Shortcuts[Key.Scroll] = new List<ShortcutDefinition>
            {
                new() { Modifiers = ModifierKeys.Control, Action = "LockLayout", Description = "Lock/unlock layout" }
            };

            Shortcuts[Key.NumPad0] = new List<ShortcutDefinition>
            {
                new() { Modifiers = ModifierKeys.Control, Action = "SelectSameExtension", Description = "Select files with same extension" }
            };

            Shortcuts[Key.NumPad1] = new List<ShortcutDefinition>
            {
                new() { Modifiers = ModifierKeys.Control, Action = "SelectSameName", Description = "Select files with same name" }
            };

            Shortcuts[Key.NumPad2] = new List<ShortcutDefinition>
            {
                new() { Modifiers = ModifierKeys.Control, Action = "SelectSameSize", Description = "Select files with same size" }
            };

            Shortcuts[Key.NumPad3] = new List<ShortcutDefinition>
            {
                new() { Modifiers = ModifierKeys.Control, Action = "SelectSameDate", Description = "Select files with same date" }
            };

            Shortcuts[Key.NumPad5] = new List<ShortcutDefinition>
            {
                new() { Modifiers = ModifierKeys.Control, Action = "CalculateFolderSize", Description = "Calculate folder sizes" }
            };

            Shortcuts[Key.NumPad8] = new List<ShortcutDefinition>
            {
                new() { Modifiers = ModifierKeys.Control, Action = "MarkCompare", Description = "Mark for comparison" }
            };

            Shortcuts[Key.NumPad9] = new List<ShortcutDefinition>
            {
                new() { Modifiers = ModifierKeys.Control, Action = "CompareMarked", Description = "Compare marked files" }
            };
        }

        /// <summary>
        /// Handle a key press and execute the corresponding action.
        /// </summary>
        public bool HandleKeyPress(Key key, ModifierKeys modifiers)
        {
            if (Shortcuts.TryGetValue(key, out var shortcutList))
            {
                var matchingShortcut = shortcutList.FirstOrDefault(s => s.Modifiers == modifiers);
                if (matchingShortcut != null)
                {
                    ExecuteShortcutAction(matchingShortcut.Action);
                    ShortcutInfo = $"{GetModifierString(modifiers)} + {key}: {matchingShortcut.Description}";
                    return true;
                }
            }

            // Handle special combinations
            if (key == Key.L && modifiers == ModifierKeys.Control)
            {
                ExecuteShortcutAction("AddressBar");
                ShortcutInfo = "Ctrl + L: Focus address bar";
                return true;
            }

            if (key == Key.E && modifiers == ModifierKeys.Control)
            {
                ExecuteShortcutAction("Edit");
                ShortcutInfo = "Ctrl + E: Edit file";
                return true;
            }

            if (key == Key.J && modifiers == ModifierKeys.Control)
            {
                ExecuteShortcutAction("JumpList");
                ShortcutInfo = "Ctrl + J: Show jump list";
                return true;
            }

            if (key == Key.K && modifiers == ModifierKeys.Control)
            {
                ExecuteShortcutAction("CommandPalette");
                ShortcutInfo = "Ctrl + K: Command palette";
                return true;
            }

            return false;
        }

        private void ExecuteShortcutAction(string action)
        {
            if (_dualPaneViewModel == null) return;

            switch (action)
            {
                case "Search":
                    _dualPaneViewModel.ShowSearchCommand.Execute(null);
                    break;
                case "AddressBar":
                    // Focus address bar implementation
                    break;
                case "Refresh":
                    _dualPaneViewModel.RefreshCommand.Execute(null);
                    break;
                case "RefreshAll":
                    _dualPaneViewModel.RefreshAllCommand.Execute(null);
                    break;
                case "TogglePane":
                    _dualPaneViewModel.ToggleActivePaneCommand.Execute(null);
                    break;
                case "ToggleQuickAccess":
                    _dualPaneViewModel.ToggleQuickAccessCommand.Execute(null);
                    break;
                case "NewFolder":
                    _dualPaneViewModel.NewFolderCommand.Execute(null);
                    break;
                case "Rename":
                    _dualPaneViewModel.StartRenameCommand.Execute(null);
                    break;
                case "ContextMenu":
                    // Open context menu implementation
                    break;
                case "Fullscreen":
                    // Toggle fullscreen implementation
                    break;
                case "Properties":
                    _dualPaneViewModel.ShowPropertiesCommand.Execute(null);
                    break;
                case "DetailsView":
                    _dualPaneViewModel.SetDetailsViewCommand.Execute(null);
                    break;
                case "IconsView":
                    _dualPaneViewModel.SetIconsViewCommand.Execute(null);
                    break;
                case "ListView":
                    _dualPaneViewModel.SetListViewCommand.Execute(null);
                    break;
                case "TilesView":
                    _dualPaneViewModel.SetTilesViewCommand.Execute(null);
                    break;
                case "ThumbnailsView":
                    _dualPaneViewModel.SetThumbnailsViewCommand.Execute(null);
                    break;
                case "NextTab":
                    _dualPaneViewModel.NextTabCommand.Execute(null);
                    break;
                case "PreviousTab":
                    _dualPaneViewModel.PreviousTabCommand.Execute(null);
                    break;
                case "CloseTab":
                    _dualPaneViewModel.CloseTabCommand.Execute(null);
                    break;
                case "NewTab":
                    _dualPaneViewModel.NewTabCommand.Execute(null);
                    break;
                case "Filter":
                    // Focus filter implementation
                    break;
                case "Goto":
                    // Go to folder dialog implementation
                    break;
                case "CalculateSize":
                    // Calculate size implementation
                    break;
                case "Preview":
                    // Toggle preview panel implementation
                    break;
                case "ZoomIn":
                    // Increase icon size implementation
                    break;
                case "ZoomOut":
                    // Decrease icon size implementation
                    break;
                case "ZoomReset":
                    // Reset icon size implementation
                    break;
                case "SelectSameExtension":
                    // Select same extension implementation
                    break;
                case "CommandPalette":
                    // Show command palette implementation
                    break;
            }
        }

        private static string GetModifierString(ModifierKeys modifiers)
        {
            var parts = new List<string>();

            if (modifiers.HasFlag(ModifierKeys.Control))
                parts.Add("Ctrl");
            if (modifiers.HasFlag(ModifierKeys.Alt))
                parts.Add("Alt");
            if (modifiers.HasFlag(ModifierKeys.Shift))
                parts.Add("Shift");
            if (modifiers.HasFlag(ModifierKeys.Windows))
                parts.Add("Win");

            return string.Join(" + ", parts);
        }

        /// <summary>
        /// Get all shortcuts as a formatted list.
        /// </summary>
        public List<string> GetAllShortcuts()
        {
            var allShortcuts = new List<string>();

            foreach (var kvp in Shortcuts.OrderBy(s => s.Key))
            {
                foreach (var shortcut in kvp.Value)
                {
                    var keyString = GetModifierString(shortcut.Modifiers);
                    if (!string.IsNullOrEmpty(keyString))
                        keyString += " + ";
                    keyString += kvp.Key;

                    allShortcuts.Add($"{keyString,-20} - {shortcut.Action,-20} ({shortcut.Description})");
                }
            }

            return allShortcuts;
        }
    }

    /// <summary>
    /// Represents a keyboard shortcut definition.
    /// </summary>
    public class ShortcutDefinition
    {
        public ModifierKeys Modifiers { get; set; }
        public string Action { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }
}
