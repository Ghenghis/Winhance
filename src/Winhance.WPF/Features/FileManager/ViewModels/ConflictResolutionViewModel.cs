using System;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Winhance.WPF.Features.FileManager.ViewModels
{
    public partial class ConflictResolutionViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _message = "A file with the same name already exists in the destination folder.";

        [ObservableProperty]
        private string _existingFileName = string.Empty;

        [ObservableProperty]
        private string _existingFileSize = string.Empty;

        [ObservableProperty]
        private string _existingFileModified = string.Empty;

        [ObservableProperty]
        private string _existingFilePath = string.Empty;

        [ObservableProperty]
        private string _newFileName = string.Empty;

        [ObservableProperty]
        private string _newFileSize = string.Empty;

        [ObservableProperty]
        private string _newFileModified = string.Empty;

        [ObservableProperty]
        private string _newFilePath = string.Empty;

        [ObservableProperty]
        private bool _applyToAll;

        public ConflictResolutionAction SelectedAction { get; private set; }

        public ConflictResolutionViewModel()
        {
        }

        public void SetConflict(FileInfo existingFile, FileInfo newFile)
        {
            ExistingFileName = existingFile.Name;
            ExistingFileSize = FormatSize(existingFile.Length);
            ExistingFileModified = existingFile.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss");
            ExistingFilePath = existingFile.DirectoryName ?? string.Empty;

            NewFileName = newFile.Name;
            NewFileSize = FormatSize(newFile.Length);
            NewFileModified = newFile.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss");
            NewFilePath = newFile.DirectoryName ?? string.Empty;

            Message = $"The file '{existingFile.Name}' already exists in the destination folder.";
        }

        [RelayCommand]
        private void Replace()
        {
            SelectedAction = ConflictResolutionAction.Replace;
            CloseDialog();
        }

        [RelayCommand]
        private void Skip()
        {
            SelectedAction = ConflictResolutionAction.Skip;
            CloseDialog();
        }

        [RelayCommand]
        private void KeepBoth()
        {
            SelectedAction = ConflictResolutionAction.KeepBoth;
            CloseDialog();
        }

        [RelayCommand]
        private void Rename()
        {
            SelectedAction = ConflictResolutionAction.Rename;
            CloseDialog();
        }

        [RelayCommand]
        private void Cancel()
        {
            SelectedAction = ConflictResolutionAction.Cancel;
            CloseDialog();
        }

        private void CloseDialog()
        {
            OnRequestClose?.Invoke(this, EventArgs.Empty);
        }

        public event EventHandler? OnRequestClose;

        private static string FormatSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            int order = 0;
            double size = bytes;
            while (size >= 1024 && order < sizes.Length - 1)
            {
                order++;
                size /= 1024;
            }
            return $"{size:0.##} {sizes[order]}";
        }
    }

    public enum ConflictResolutionAction
    {
        Replace,
        Skip,
        KeepBoth,
        Rename,
        Cancel
    }
}
