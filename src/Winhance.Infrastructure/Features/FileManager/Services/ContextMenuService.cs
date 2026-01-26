using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Win32;
using Winhance.Core.Features.FileManager.Interfaces;
using Winhance.Core.Features.FileManager.Models;

namespace Winhance.Infrastructure.Features.FileManager.Services
{
    /// <summary>
    /// Service for managing context menus in the file browser and Windows Shell integration.
    /// </summary>
    public class ContextMenuService : IContextMenuService
    {
        private readonly List<InternalContextMenuItem> _standardItems;
        private readonly List<InternalContextMenuItem> _backgroundItems;
        private readonly List<ContextMenuItemStatus> _registrationStatus;
        private bool _isRegistered;

        private const string RegistryBasePath = @"Software\Classes";
        private const string WinhanceKeyName = "Winhance";

        public ContextMenuService()
        {
            _standardItems = CreateStandardMenuItems();
            _backgroundItems = CreateBackgroundMenuItems();
            _registrationStatus = new List<ContextMenuItemStatus>();
            _isRegistered = CheckRegistration();
        }

        /// <inheritdoc />
        public bool IsRegistered => _isRegistered;

        /// <inheritdoc />
        public async Task<bool> RegisterContextMenuAsync(ContextMenuRegistration registration)
        {
            try
            {
                _registrationStatus.Clear();

                await Task.Run(() =>
                {
                    foreach (var item in registration.Items)
                    {
                        var status = new ContextMenuItemStatus
                        {
                            Id = item.Id,
                            Title = item.Title
                        };

                        try
                        {
                            RegisterMenuItem(item, registration);
                            status.IsRegistered = true;
                        }
                        catch (Exception ex)
                        {
                            status.IsRegistered = false;
                            status.Error = ex.Message;
                        }

                        _registrationStatus.Add(status);
                    }
                });

                _isRegistered = _registrationStatus.All(s => s.IsRegistered);
                return _isRegistered;
            }
            catch
            {
                return false;
            }
        }

        /// <inheritdoc />
        public async Task<bool> UnregisterContextMenuAsync()
        {
            try
            {
                await Task.Run(() =>
                {
                    // Remove from *\shell
                    RemoveRegistryKey($@"{RegistryBasePath}\*\shell\{WinhanceKeyName}");

                    // Remove from Directory\shell
                    RemoveRegistryKey($@"{RegistryBasePath}\Directory\shell\{WinhanceKeyName}");

                    // Remove from Directory\Background\shell
                    RemoveRegistryKey($@"{RegistryBasePath}\Directory\Background\shell\{WinhanceKeyName}");

                    // Remove from Drive\shell
                    RemoveRegistryKey($@"{RegistryBasePath}\Drive\shell\{WinhanceKeyName}");
                });

                _isRegistered = false;
                _registrationStatus.Clear();
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <inheritdoc />
        public IEnumerable<ContextMenuItemStatus> GetRegistrationStatus()
        {
            return _registrationStatus.AsReadOnly();
        }

        private bool CheckRegistration()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey($@"{RegistryBasePath}\*\shell\{WinhanceKeyName}");
                return key != null;
            }
            catch
            {
                return false;
            }
        }

        private void RegisterMenuItem(Winhance.Core.Features.FileManager.Interfaces.ContextMenuItem item, ContextMenuRegistration registration)
        {
            var targets = new List<string>();

            if (item.AppliesTo.HasFlag(ContextMenuTarget.Files) && registration.EnableFileMenu)
                targets.Add(@"*\shell");
            if (item.AppliesTo.HasFlag(ContextMenuTarget.Folders) && registration.EnableFolderMenu)
                targets.Add(@"Directory\shell");
            if (item.AppliesTo.HasFlag(ContextMenuTarget.Background) && registration.EnableBackgroundMenu)
                targets.Add(@"Directory\Background\shell");
            if (item.AppliesTo.HasFlag(ContextMenuTarget.Drives) && registration.EnableDriveMenu)
                targets.Add(@"Drive\shell");

            foreach (var target in targets)
            {
                var keyPath = registration.UseSubmenu
                    ? $@"{RegistryBasePath}\{target}\{WinhanceKeyName}\shell\{item.Id}"
                    : $@"{RegistryBasePath}\{target}\{item.Id}";

                using var key = Registry.CurrentUser.CreateSubKey(keyPath);
                if (key != null)
                {
                    key.SetValue("", item.Title);
                    key.SetValue("Icon", item.Icon);

                    using var commandKey = key.CreateSubKey("command");
                    commandKey?.SetValue("", $"\"{GetExecutablePath()}\" --command {item.Command} \"%1\"");
                }
            }

            // Create main submenu entry if using submenu
            if (registration.UseSubmenu)
            {
                foreach (var target in targets.Distinct())
                {
                    var mainKeyPath = $@"{RegistryBasePath}\{target}\{WinhanceKeyName}";
                    using var mainKey = Registry.CurrentUser.CreateSubKey(mainKeyPath);
                    if (mainKey != null)
                    {
                        mainKey.SetValue("MUIVerb", registration.SubmenuTitle);
                        mainKey.SetValue("SubCommands", "");
                    }
                }
            }
        }

        private void RemoveRegistryKey(string path)
        {
            try
            {
                Registry.CurrentUser.DeleteSubKeyTree(path, false);
            }
            catch
            {
                // Key might not exist
            }
        }

        private string GetExecutablePath()
        {
            return System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? "Winhance.exe";
        }

        /// <summary>
        /// Gets a context menu for the specified type and items.
        /// </summary>
        public async Task<IReadOnlyList<InternalContextMenuItem>> GetMenuAsync(InternalContextMenuType type, IEnumerable<FileItem>? selectedItems = null)
        {
            var items = type switch
            {
                InternalContextMenuType.File => await GetFileMenuAsync(selectedItems ?? Enumerable.Empty<FileItem>()),
                InternalContextMenuType.Folder => await GetFolderMenuAsync(selectedItems ?? Enumerable.Empty<FileItem>()),
                InternalContextMenuType.Multiple => await GetMultipleMenuAsync(selectedItems ?? Enumerable.Empty<FileItem>()),
                InternalContextMenuType.Background => _backgroundItems.AsReadOnly(),
                _ => new List<InternalContextMenuItem>().AsReadOnly()
            };

            return items;
        }

        /// <summary>
        /// Gets a context menu for the specified items.
        /// </summary>
        public async Task<IReadOnlyList<InternalContextMenuItem>> GetMenuForItemsAsync(IEnumerable<FileItem> items)
        {
            var itemList = items.ToList();

            if (!itemList.Any())
            {
                return _backgroundItems.AsReadOnly();
            }

            if (itemList.Count == 1)
            {
                return await GetMenuAsync(itemList[0].IsDirectory ? InternalContextMenuType.Folder : InternalContextMenuType.File, itemList);
            }

            return await GetMenuAsync(InternalContextMenuType.Multiple, itemList);
        }

        /// <summary>
        /// Executes a command on the specified items.
        /// </summary>
        public async Task<bool> ExecuteCommandAsync(string commandId, IEnumerable<FileItem>? items = null)
        {
            // TODO: Implement command execution
            await Task.CompletedTask;
            return true;
        }

        /// <summary>
        /// Adds a custom item to a context menu type.
        /// </summary>
        public async Task AddCustomItemAsync(InternalContextMenuItem item, InternalContextMenuType type)
        {
            // TODO: Implement custom item addition
            await Task.CompletedTask;
        }

        /// <summary>
        /// Removes a custom item by command ID.
        /// </summary>
        public async Task RemoveCustomItemAsync(string commandId)
        {
            // TODO: Implement custom item removal
            await Task.CompletedTask;
        }

        /// <summary>
        /// Gets quick actions for the specified items.
        /// </summary>
        public async Task<IReadOnlyList<InternalContextMenuItem>> GetQuickActionsAsync(IEnumerable<FileItem> items)
        {
            var quickActions = new List<InternalContextMenuItem>();

            if (items.Any())
            {
                quickActions.Add(new InternalContextMenuItem
                {
                    Id = "quick-open",
                    Text = "Open",
                    Icon = "Open",
                    Shortcut = "Enter",
                    Command = "Open"
                });

                if (items.Count() == 1 && !items.First().IsDirectory)
                {
                    quickActions.Add(new InternalContextMenuItem
                    {
                        Id = "quick-preview",
                        Text = "Preview",
                        Icon = "Preview",
                        Shortcut = "Space",
                        Command = "Preview"
                    });
                }

                quickActions.Add(new InternalContextMenuItem
                {
                    Id = "quick-copy",
                    Text = "Copy",
                    Icon = "Copy",
                    Shortcut = "Ctrl+C",
                    Command = "Copy"
                });

                quickActions.Add(new InternalContextMenuItem
                {
                    Id = "quick-cut",
                    Text = "Cut",
                    Icon = "Cut",
                    Shortcut = "Ctrl+X",
                    Command = "Cut"
                });
            }

            return await Task.FromResult(quickActions.AsReadOnly());
        }

        private async Task<IReadOnlyList<InternalContextMenuItem>> GetFileMenuAsync(IEnumerable<FileItem> items)
        {
            var menu = new List<InternalContextMenuItem>(_standardItems);

            // Add file-specific items
            menu.InsertRange(0, new[]
            {
                new InternalContextMenuItem { Id = "open", Text = "Open", Icon = "Open", Shortcut = "Enter", Command = "Open" },
                new InternalContextMenuItem { Id = "open-with", Text = "Open with...", Icon = "OpenWith", Command = "OpenWith" },
                new InternalContextMenuItem { Id = "sep1", Text = "-", IsSeparator = true }
            });

            // Add preview if not directory
            var file = items.FirstOrDefault();
            if (file != null && !file.IsDirectory)
            {
                menu.Insert(3, new InternalContextMenuItem
                {
                    Id = "preview",
                    Text = "Preview",
                    Icon = "Preview",
                    Shortcut = "Space",
                    Command = "Preview"
                });
            }

            return await Task.FromResult(menu.AsReadOnly());
        }

        private async Task<IReadOnlyList<InternalContextMenuItem>> GetFolderMenuAsync(IEnumerable<FileItem> items)
        {
            var menu = new List<InternalContextMenuItem>(_standardItems);

            // Add folder-specific items
            menu.InsertRange(0, new[]
            {
                new InternalContextMenuItem { Id = "open", Text = "Open", Icon = "Open", Shortcut = "Enter", Command = "Open" },
                new InternalContextMenuItem { Id = "open-in-new-tab", Text = "Open in new tab", Icon = "NewTab", Command = "OpenInNewTab" },
                new InternalContextMenuItem { Id = "sep1", Text = "-", IsSeparator = true },
                new InternalContextMenuItem { Id = "explore", Text = "Explore", Icon = "Explore", Command = "Explore" },
                new InternalContextMenuItem { Id = "sep2", Text = "-", IsSeparator = true }
            });

            return await Task.FromResult(menu.AsReadOnly());
        }

        private async Task<IReadOnlyList<InternalContextMenuItem>> GetMultipleMenuAsync(IEnumerable<FileItem> items)
        {
            var menu = new List<InternalContextMenuItem>(_standardItems);

            // Remove items not applicable to multiple selection
            menu.RemoveAll(m => m.Id == "rename" || m.Id == "preview" || m.Id == "properties");

            // Add multi-select specific items
            menu.InsertRange(0, new[]
            {
                new InternalContextMenuItem { Id = "open-all", Text = "Open all", Icon = "Open", Command = "OpenAll" },
                new InternalContextMenuItem { Id = "sep1", Text = "-", IsSeparator = true }
            });

            return await Task.FromResult(menu.AsReadOnly());
        }

        private List<InternalContextMenuItem> CreateStandardMenuItems()
        {
            return new List<InternalContextMenuItem>
            {
                new InternalContextMenuItem { Id = "cut", Text = "Cut", Icon = "Cut", Shortcut = "Ctrl+X", Command = "Cut" },
                new InternalContextMenuItem { Id = "copy", Text = "Copy", Icon = "Copy", Shortcut = "Ctrl+C", Command = "Copy" },
                new InternalContextMenuItem { Id = "paste", Text = "Paste", Icon = "Paste", Shortcut = "Ctrl+V", Command = "Paste" },
                new InternalContextMenuItem { Id = "sep2", Text = "-", IsSeparator = true },
                new InternalContextMenuItem { Id = "delete", Text = "Delete", Icon = "Delete", Shortcut = "Del", Command = "Delete" },
                new InternalContextMenuItem { Id = "rename", Text = "Rename", Icon = "Rename", Shortcut = "F2", Command = "Rename" },
                new InternalContextMenuItem { Id = "sep3", Text = "-", IsSeparator = true },
                new InternalContextMenuItem { Id = "send-to", Text = "Send to", Icon = "SendTo", Command = "SendTo" },
                new InternalContextMenuItem { Id = "sep4", Text = "-", IsSeparator = true },
                new InternalContextMenuItem { Id = "properties", Text = "Properties", Icon = "Properties", Shortcut = "Alt+Enter", Command = "Properties" }
            };
        }

        private List<InternalContextMenuItem> CreateBackgroundMenuItems()
        {
            return new List<InternalContextMenuItem>
            {
                new InternalContextMenuItem { Id = "new-folder", Text = "New Folder", Icon = "NewFolder", Shortcut = "Ctrl+Shift+N", Command = "NewFolder" },
                new InternalContextMenuItem { Id = "new-file", Text = "New File", Icon = "NewFile", Command = "NewFile" },
                new InternalContextMenuItem { Id = "sep1", Text = "-", IsSeparator = true },
                new InternalContextMenuItem { Id = "paste", Text = "Paste", Icon = "Paste", Shortcut = "Ctrl+V", Command = "Paste" },
                new InternalContextMenuItem { Id = "paste-shortcut", Text = "Paste shortcut", Icon = "PasteShortcut", Command = "PasteShortcut" },
                new InternalContextMenuItem { Id = "sep2", Text = "-", IsSeparator = true },
                new InternalContextMenuItem { Id = "refresh", Text = "Refresh", Icon = "Refresh", Shortcut = "F5", Command = "Refresh" },
                new InternalContextMenuItem { Id = "sep3", Text = "-", IsSeparator = true },
                new InternalContextMenuItem { Id = "properties", Text = "Properties", Icon = "Properties", Command = "Properties" },
                new InternalContextMenuItem { Id = "sep4", Text = "-", IsSeparator = true },
                new InternalContextMenuItem { Id = "show-hidden", Text = "Show hidden files", Icon = "ShowHidden", Command = "ShowHidden", IsCheckable = true },
                new InternalContextMenuItem { Id = "show-sys", Text = "Show system files", Icon = "ShowSystem", Command = "ShowSystem", IsCheckable = true },
                new InternalContextMenuItem { Id = "sep5", Text = "-", IsSeparator = true },
                new InternalContextMenuItem { Id = "customize", Text = "Customize folder...", Icon = "Customize", Command = "Customize" }
            };
        }
    }

    /// <summary>
    /// Internal context menu item definition (for file browser use).
    /// </summary>
    public class InternalContextMenuItem
    {
        public string Id { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public string Shortcut { get; set; } = string.Empty;
        public string Command { get; set; } = string.Empty;
        public bool IsSeparator { get; set; }
        public bool IsCheckable { get; set; }
        public bool IsChecked { get; set; }
        public bool IsEnabled { get; set; } = true;
        public bool IsVisible { get; set; } = true;
        public List<InternalContextMenuItem> SubItems { get; set; } = new();
        public string Tooltip { get; set; } = string.Empty;
        public string Tag { get; set; } = string.Empty;
    }

    /// <summary>
    /// Internal context menu types (for file browser use).
    /// </summary>
    public enum InternalContextMenuType
    {
        File,
        Folder,
        Multiple,
        Background
    }
}
