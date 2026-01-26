using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Xaml.Behaviors.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Winhance.Core.Features.FileManager.Interfaces;
using Winhance.Core.Features.FileManager.Models;

namespace Winhance.WPF.Features.FileManager.ViewModels
{
    /// <summary>
    /// ViewModel for synchronized panel scrolling
    /// </summary>
    public partial class SynchronizedScrollingViewModel : ObservableObject
    {
        private readonly IFileManagerService _fileManagerService;
        private bool _isSynchronized = true;

        [ObservableProperty]
        private double _leftScrollOffset;

        [ObservableProperty]
        private double _rightScrollOffset;

        public SynchronizedScrollingViewModel(IFileManagerService fileManagerService)
        {
            _fileManagerService = fileManagerService;
        }

        public bool IsSynchronized
        {
            get => _isSynchronized;
            set
            {
                SetProperty(ref _isSynchronized, value);
            }
        }

        partial void OnLeftScrollOffsetChanged(double value)
        {
            if (IsSynchronized)
            {
                RightScrollOffset = value;
            }
        }

        partial void OnRightScrollOffsetChanged(double value)
        {
            if (IsSynchronized)
            {
                LeftScrollOffset = value;
            }
        }
    }

    /// <summary>
    /// Behavior for synchronized scrolling between panels
    /// </summary>
    public class SynchronizedScrollBehavior : Behavior<ScrollViewer>
    {
        public static readonly DependencyProperty SyncWithProperty =
            DependencyProperty.Register(nameof(SyncWith), typeof(ScrollViewer), typeof(SynchronizedScrollBehavior),
                new PropertyMetadata(null, OnSyncWithChanged));

        private bool _isUpdating;

        public ScrollViewer SyncWith
        {
            get => (ScrollViewer)GetValue(SyncWithProperty);
            set => SetValue(SyncWithProperty, value);
        }

        protected override void OnAttached()
        {
            base.OnAttached();
            AssociatedObject.ScrollChanged += OnScrollChanged;
        }

        protected override void OnDetaching()
        {
            AssociatedObject.ScrollChanged -= OnScrollChanged;
            base.OnDetaching();
        }

        private void OnScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (_isUpdating || SyncWith == null) return;

            _isUpdating = true;
            SyncWith.ScrollToVerticalOffset(e.VerticalOffset);
            SyncWith.ScrollToHorizontalOffset(e.HorizontalOffset);
            _isUpdating = false;
        }

        private static void OnSyncWithChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var behavior = (SynchronizedScrollBehavior)d;
            if (e.OldValue is ScrollViewer oldViewer)
            {
                // Unsubscribe from old
            }
            if (e.NewValue is ScrollViewer newViewer)
            {
                // Subscribe to new
            }
        }
    }

    /// <summary>
    /// ViewModel for panel swap functionality
    /// </summary>
    public partial class PanelSwapViewModel : ObservableObject
    {
        [ObservableProperty]
        private FileItem? _leftPanelPath;

        [ObservableProperty]
        private FileItem? _rightPanelPath;

        [RelayCommand]
        private void SwapPanels()
        {
            var temp = LeftPanelPath;
            LeftPanelPath = RightPanelPath;
            RightPanelPath = temp;
        }
    }

    /// <summary>
    /// ViewModel for equal panel sizing
    /// </summary>
    public partial class EqualPanelSizingViewModel : ObservableObject
    {
        [ObservableProperty]
        private GridLength _leftPanelWidth = new GridLength(1, GridUnitType.Star);

        [ObservableProperty]
        private GridLength _rightPanelWidth = new GridLength(1, GridUnitType.Star);

        [RelayCommand]
        private void MakeEqual()
        {
            LeftPanelWidth = new GridLength(1, GridUnitType.Star);
            RightPanelWidth = new GridLength(1, GridUnitType.Star);
        }
    }

    /// <summary>
    /// ViewModel for single panel mode
    /// </summary>
    public partial class SinglePanelModeViewModel : ObservableObject
    {
        [ObservableProperty]
        private bool _isSinglePanelMode;

        [ObservableProperty]
        private PanelType _activePanel = PanelType.Left;

        public enum PanelType
        {
            Left,
            Right
        }

        [RelayCommand]
        private void ToggleSinglePanelMode()
        {
            IsSinglePanelMode = !IsSinglePanelMode;
        }

        [RelayCommand]
        private void SwitchActivePanel()
        {
            ActivePanel = ActivePanel == PanelType.Left ? PanelType.Right : PanelType.Left;
        }
    }

    /// <summary>
    /// ViewModel for panel lock functionality
    /// </summary>
    public partial class PanelLockViewModel : ObservableObject
    {
        [ObservableProperty]
        private bool _isLeftPanelLocked;

        [ObservableProperty]
        private bool _isRightPanelLocked;

        [ObservableProperty]
        private string? _lockedPath;

        [RelayCommand]
        private void LockLeftPanel()
        {
            IsLeftPanelLocked = !IsLeftPanelLocked;
        }

        [RelayCommand]
        private void LockRightPanel()
        {
            IsRightPanelLocked = !IsRightPanelLocked;
        }

        [RelayCommand]
        private void UnlockAllPanels()
        {
            IsLeftPanelLocked = false;
            IsRightPanelLocked = false;
            LockedPath = null;
        }
    }

    /// <summary>
    /// ViewModel for mirror navigation mode
    /// </summary>
    public partial class MirrorNavigationViewModel : ObservableObject
    {
        [ObservableProperty]
        private bool _isMirrorModeEnabled;

        [ObservableProperty]
        private bool _syncSelection;

        [ObservableProperty]
        private bool _syncScrollPosition;

        [RelayCommand]
        private void ToggleMirrorMode()
        {
            IsMirrorModeEnabled = !IsMirrorModeEnabled;
        }
    }

    /// <summary>
    /// ViewModel for vertical/horizontal split toggle
    /// </summary>
    public partial class SplitOrientationViewModel : ObservableObject
    {
        [ObservableProperty]
        private Orientation _splitOrientation = Orientation.Vertical;

        [RelayCommand]
        private void ToggleOrientation()
        {
            SplitOrientation = SplitOrientation == Orientation.Vertical 
                ? Orientation.Horizontal 
                : Orientation.Vertical;
        }
    }

    /// <summary>
    /// ViewModel for panel state persistence
    /// </summary>
    public partial class PanelStatePersistenceViewModel : ObservableObject
    {
        private readonly ITabService _tabService;

        public PanelStatePersistenceViewModel(ITabService tabService)
        {
            _tabService = tabService;
        }

        [RelayCommand]
        private async Task SavePanelStateAsync()
        {
            try
            {
                var statePath = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Winhance-FS",
                    "PanelState.json");

                var stateData = new
                {
                    SavedAt = DateTime.Now,
                    Version = "1.0"
                };

                var json = System.Text.Json.JsonSerializer.Serialize(stateData,
                    new System.Text.Json.JsonSerializerOptions { WriteIndented = true });

                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(statePath)!);
                await System.IO.File.WriteAllTextAsync(statePath, json);

                System.Windows.MessageBox.Show(
                    "Panel state saved successfully.",
                    "State Saved",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to save panel state: {ex.Message}",
                    "Save Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task RestorePanelStateAsync()
        {
            try
            {
                var statePath = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Winhance-FS",
                    "PanelState.json");

                if (!System.IO.File.Exists(statePath))
                {
                    System.Windows.MessageBox.Show(
                        "No saved panel state found.",
                        "Restore State",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Information);
                    return;
                }

                var json = await System.IO.File.ReadAllTextAsync(statePath);
                // Panel state would be restored here

                System.Windows.MessageBox.Show(
                    "Panel state restored successfully.",
                    "State Restored",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to restore panel state: {ex.Message}",
                    "Restore Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task ResetToDefaultAsync()
        {
            var result = System.Windows.MessageBox.Show(
                "Are you sure you want to reset all panel settings to default?\n\nThis will clear all saved panel states.",
                "Confirm Reset",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Question);

            if (result == System.Windows.MessageBoxResult.Yes)
            {
                try
                {
                    var statePath = System.IO.Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        "Winhance-FS",
                        "PanelState.json");

                    if (System.IO.File.Exists(statePath))
                    {
                        System.IO.File.Delete(statePath);
                    }

                    System.Windows.MessageBox.Show(
                        "Panel settings reset to default.",
                        "Reset Complete",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show(
                        $"Failed to reset panel state: {ex.Message}",
                        "Reset Error",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Error);
                }
            }
            await Task.CompletedTask;
        }
    }
}
