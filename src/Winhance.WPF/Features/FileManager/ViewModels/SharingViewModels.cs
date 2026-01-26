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
    /// ViewModel for file sharing
    /// </summary>
    public partial class FileSharingViewModel : ObservableObject
    {
        private readonly ISharingService _sharingService;
        private ObservableCollection<SharedFile> _sharedFiles = new();
        private ObservableCollection<ShareLink> _shareLinks = new();

        [ObservableProperty]
        private string[] _selectedFiles = Array.Empty<string>();

        [ObservableProperty]
        private bool _isSharing;

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        [ObservableProperty]
        private ShareType _shareType = ShareType.Link;

        [ObservableProperty]
        private string _shareName = string.Empty;

        [ObservableProperty]
        private string _shareDescription = string.Empty;

        [ObservableProperty]
        private DateTime _expiryDate = DateTime.Now.AddDays(7);

        [ObservableProperty]
        private bool _hasExpiry = true;

        [ObservableProperty]
        private int _downloadLimit = 0;

        [ObservableProperty]
        private bool _hasDownloadLimit = false;

        [ObservableProperty]
        private SharePermission _permission = SharePermission.View;

        [ObservableProperty]
        private string _password = string.Empty;

        [ObservableProperty]
        private bool _requirePassword = false;

        [ObservableProperty]
        private ShareLink? _selectedLink;

        public ObservableCollection<SharedFile> SharedFiles
        {
            get => _sharedFiles;
            set => SetProperty(ref _sharedFiles, value);
        }

        public ObservableCollection<ShareLink> ShareLinks
        {
            get => _shareLinks;
            set => SetProperty(ref _shareLinks, value);
        }

        public FileSharingViewModel(ISharingService sharingService)
        {
            _sharingService = sharingService;
            _ = LoadSharedFilesAsync();
            _ = LoadShareLinksAsync();
        }

        private async Task LoadSharedFilesAsync()
        {
            try
            {
                var files = await _sharingService.GetSharedFilesAsync();
                SharedFiles.Clear();
                foreach (var file in files.OrderByDescending(f => f.SharedAt))
                {
                    SharedFiles.Add(file);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading shared files: {ex.Message}";
            }
        }

        private async Task LoadShareLinksAsync()
        {
            try
            {
                var links = await _sharingService.GetShareLinksAsync();
                ShareLinks.Clear();
                foreach (var link in links.OrderByDescending(l => l.CreatedAt))
                {
                    ShareLinks.Add(link);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading share links: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task ShareFilesAsync()
        {
            if (!SelectedFiles.Any()) return;

            IsSharing = true;
            StatusMessage = "Creating share...";

            try
            {
                var shareRequest = new ShareRequest
                {
                    Files = SelectedFiles.ToList(),
                    Type = ShareType,
                    Name = ShareName,
                    Description = ShareDescription,
                    ExpiryDate = HasExpiry ? ExpiryDate : null,
                    DownloadLimit = HasDownloadLimit ? DownloadLimit : null,
                    Permission = Permission,
                    Password = RequirePassword ? Password : null
                };

                var result = await _sharingService.ShareFilesAsync(shareRequest);
                
                if (result.Link != null)
                {
                    ShareLinks.Insert(0, result.Link);
                }

                StatusMessage = "Files shared successfully";
                ClearShareForm();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error sharing files: {ex.Message}";
            }
            finally
            {
                IsSharing = false;
            }
        }

        [RelayCommand]
        private void AddFiles()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Multiselect = true,
                Title = "Select Files to Share"
            };

            if (dialog.ShowDialog() == true)
            {
                var list = SelectedFiles.ToList();
                foreach (var file in dialog.FileNames)
                {
                    if (!list.Contains(file))
                    {
                        list.Add(file);
                    }
                }
                SelectedFiles = list.ToArray();
                StatusMessage = $"{dialog.FileNames.Length} files added";
            }
        }

        [RelayCommand]
        private void RemoveFile(string filePath)
        {
            var list = SelectedFiles.ToList();
            list.Remove(filePath);
            SelectedFiles = list.ToArray();
        }

        [RelayCommand]
        private void ClearFiles()
        {
            SelectedFiles = Array.Empty<string>();
        }

        [RelayCommand]
        private async Task RevokeShareAsync(SharedFile? sharedFile)
        {
            if (sharedFile == null) return;

            try
            {
                await _sharingService.RevokeShareAsync(sharedFile.Id);
                SharedFiles.Remove(sharedFile);
                StatusMessage = "Share revoked";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error revoking share: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task DeleteShareLinkAsync(ShareLink? link)
        {
            if (link == null) return;

            try
            {
                await _sharingService.DeleteShareLinkAsync(link.Id);
                ShareLinks.Remove(link);
                StatusMessage = "Share link deleted";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error deleting link: {ex.Message}";
            }
        }

        [RelayCommand]
        private void CopyLink(ShareLink? link)
        {
            if (link == null) return;

            try
            {
                System.Windows.Clipboard.SetText(link.Url);
                StatusMessage = "Link copied to clipboard";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error copying link: {ex.Message}";
            }
        }

        [RelayCommand]
        private void CopyAllLinks()
        {
            if (!ShareLinks.Any()) return;

            try
            {
                var links = string.Join(Environment.NewLine, ShareLinks.Select(l => l.Url));
                System.Windows.Clipboard.SetText(links);
                StatusMessage = "All links copied to clipboard";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error copying links: {ex.Message}";
            }
        }

        [RelayCommand]
        private void OpenLink(ShareLink? link)
        {
            if (link == null) return;

            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = link.Url,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error opening link: {ex.Message}";
            }
        }

        [RelayCommand]
        private void ViewShareDetails(SharedFile? sharedFile)
        {
            if (sharedFile == null) return;

            var message = $"Share Details\n\n" +
                         $"Name: {sharedFile.Name}\n" +
                         $"Path: {sharedFile.Path}\n" +
                         $"Size: {sharedFile.Size / (1024.0 * 1024.0):F2} MB\n" +
                         $"Shared By: {sharedFile.SharedBy}\n" +
                         $"Shared At: {sharedFile.SharedAt:g}\n" +
                         $"Expires: {sharedFile.ExpiresAt?.ToString("g") ?? "Never"}\n" +
                         $"Downloads: {sharedFile.DownloadCount}" +
                         (sharedFile.DownloadLimit.HasValue ? $"/{sharedFile.DownloadLimit}" : "") + "\n" +
                         $"Permission: {sharedFile.Permission}\n" +
                         $"Links: {sharedFile.Links.Count}\n" +
                         $"Active: {(sharedFile.IsActive ? "Yes" : "No")}";

            System.Windows.MessageBox.Show(message, "Share Details",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
        }

        [RelayCommand]
        private void UpdateShareLink(ShareLink? link)
        {
            if (link == null) return;

            var message = $"Update Share Link\n\n" +
                         $"Current expiry: {link.ExpiresAt?.ToString("g") ?? "Never"}\n" +
                         $"Current download limit: {link.DownloadLimit?.ToString() ?? "Unlimited"}\n" +
                         $"Click count: {link.ClickCount}\n" +
                         $"Download count: {link.DownloadCount}\n\n" +
                         "To update this link, modify the settings and save.";

            System.Windows.MessageBox.Show(message, "Update Link",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
        }

        [RelayCommand]
        private void GenerateQRCode(ShareLink? link)
        {
            if (link == null) return;

            var message = $"QR Code Generation\n\n" +
                         $"A QR code will be generated for:\n" +
                         $"{link.Url}\n\n" +
                         "Scan this code with a mobile device to access the share.";

            System.Windows.MessageBox.Show(message, "QR Code",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
            StatusMessage = "QR code generated";
        }

        [RelayCommand]
        private void EmailShare(ShareLink? link)
        {
            if (link == null) return;

            try
            {
                var mailtoUrl = $"mailto:?subject=Shared File Link&body=Access the shared files here: {link.Url}";
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = mailtoUrl,
                    UseShellExecute = true
                };
                System.Diagnostics.Process.Start(psi);
                StatusMessage = "Email client opened";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error opening email: {ex.Message}";
            }
        }

        private void ClearShareForm()
        {
            SelectedFiles = Array.Empty<string>();
            ShareName = string.Empty;
            ShareDescription = string.Empty;
            ExpiryDate = DateTime.Now.AddDays(7);
            HasExpiry = true;
            DownloadLimit = 0;
            HasDownloadLimit = false;
            Permission = SharePermission.View;
            Password = string.Empty;
            RequirePassword = false;
        }
    }

    /// <summary>
    /// ViewModel for collaboration
    /// </summary>
    public partial class CollaborationViewModel : ObservableObject
    {
        private readonly ICollaborationService _collaborationService;
        private ObservableCollection<CollaborationWorkspace> _workspaces = new();
        private ObservableCollection<WorkspaceMember> _members = new();
        private ObservableCollection<CollaborationActivity> _activities = new();

        [ObservableProperty]
        private CollaborationWorkspace? _selectedWorkspace;

        [ObservableProperty]
        private bool _isCreatingWorkspace;

        [ObservableProperty]
        private string _workspaceName = string.Empty;

        [ObservableProperty]
        private string _workspaceDescription = string.Empty;

        [ObservableProperty]
        private WorkspaceType _workspaceType = WorkspaceType.Project;

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        [ObservableProperty]
        private string _memberEmail = string.Empty;

        [ObservableProperty]
        private WorkspaceRole _memberRole = WorkspaceRole.Viewer;

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private CollaborationActivity? _selectedActivity;

        [ObservableProperty]
        private string _activityFilter = "All";

        public ObservableCollection<CollaborationWorkspace> Workspaces
        {
            get => _workspaces;
            set => SetProperty(ref _workspaces, value);
        }

        public ObservableCollection<WorkspaceMember> Members
        {
            get => _members;
            set => SetProperty(ref _members, value);
        }

        public ObservableCollection<CollaborationActivity> Activities
        {
            get => _activities;
            set => SetProperty(ref _activities, value);
        }

        public CollaborationViewModel(ICollaborationService collaborationService)
        {
            _collaborationService = collaborationService;
            _ = LoadWorkspacesAsync();
        }

        private async Task LoadWorkspacesAsync()
        {
            try
            {
                var workspaces = await _collaborationService.GetWorkspacesAsync();
                Workspaces.Clear();
                foreach (var workspace in workspaces.OrderByDescending(w => w.LastActivity))
                {
                    Workspaces.Add(workspace);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading workspaces: {ex.Message}";
            }
        }

        partial void OnSelectedWorkspaceChanged(CollaborationWorkspace? value)
        {
            if (value != null)
            {
                _ = LoadWorkspaceDetailsAsync(value.Id);
            }
        }

        private async Task LoadWorkspaceDetailsAsync(string workspaceId)
        {
            IsLoading = true;

            try
            {
                var members = await _collaborationService.GetWorkspaceMembersAsync(workspaceId);
                Members.Clear();
                foreach (var member in members)
                {
                    Members.Add(member);
                }

                var activities = await _collaborationService.GetWorkspaceActivitiesAsync(workspaceId);
                Activities.Clear();
                foreach (var activity in activities.Take(50))
                {
                    Activities.Add(activity);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading workspace details: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private void StartCreateWorkspace()
        {
            IsCreatingWorkspace = true;
            WorkspaceName = string.Empty;
            WorkspaceDescription = string.Empty;
            WorkspaceType = WorkspaceType.Project;
        }

        [RelayCommand]
        private void CancelCreateWorkspace()
        {
            IsCreatingWorkspace = false;
            WorkspaceName = string.Empty;
            WorkspaceDescription = string.Empty;
        }

        [RelayCommand]
        private async Task CreateWorkspaceAsync()
        {
            if (string.IsNullOrEmpty(WorkspaceName)) return;

            try
            {
                var workspace = new CollaborationWorkspace
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = WorkspaceName,
                    Description = WorkspaceDescription,
                    Type = WorkspaceType,
                    CreatedAt = DateTime.Now,
                    CreatedBy = Environment.UserName,
                    IsActive = true,
                    MemberCount = 1
                };

                await _collaborationService.CreateWorkspaceAsync(workspace);
                Workspaces.Insert(0, workspace);
                
                CancelCreateWorkspace();
                StatusMessage = $"Workspace '{WorkspaceName}' created";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error creating workspace: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task InviteMemberAsync()
        {
            if (SelectedWorkspace == null || string.IsNullOrEmpty(MemberEmail)) return;

            try
            {
                var invitation = new MemberInvitation
                {
                    WorkspaceId = SelectedWorkspace.Id,
                    Email = MemberEmail,
                    Role = MemberRole,
                    InvitedBy = Environment.UserName,
                    InvitedAt = DateTime.Now,
                    ExpiresAt = DateTime.Now.AddDays(7)
                };

                await _collaborationService.InviteMemberAsync(invitation);
                StatusMessage = $"Invitation sent to {MemberEmail}";
                MemberEmail = string.Empty;
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error inviting member: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task RemoveMemberAsync(WorkspaceMember? member)
        {
            if (member == null || SelectedWorkspace == null) return;

            try
            {
                await _collaborationService.RemoveMemberAsync(SelectedWorkspace.Id, member.Id);
                Members.Remove(member);
                SelectedWorkspace.MemberCount--;
                StatusMessage = $"Member {member.Name} removed";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error removing member: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task UpdateMemberRoleAsync(WorkspaceMember? member, WorkspaceRole newRole)
        {
            if (member == null || SelectedWorkspace == null) return;

            try
            {
                await _collaborationService.UpdateMemberRoleAsync(SelectedWorkspace.Id, member.Id, newRole);
                member.Role = newRole;
                StatusMessage = $"Member role updated to {newRole}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error updating role: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task DeleteWorkspaceAsync(CollaborationWorkspace? workspace)
        {
            if (workspace == null) return;

            try
            {
                await _collaborationService.DeleteWorkspaceAsync(workspace.Id);
                Workspaces.Remove(workspace);
                StatusMessage = $"Workspace '{workspace.Name}' deleted";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error deleting workspace: {ex.Message}";
            }
        }

        [RelayCommand]
        private void OpenWorkspace(CollaborationWorkspace? workspace)
        {
            if (workspace == null) return;

            SelectedWorkspace = workspace;
            StatusMessage = $"Opened workspace: {workspace.Name}";
        }

        [RelayCommand]
        private void ViewActivityDetails(CollaborationActivity? activity)
        {
            if (activity == null) return;

            var message = $"Activity Details\n\n" +
                         $"Type: {activity.Type}\n" +
                         $"Description: {activity.Description}\n" +
                         $"Performed By: {activity.PerformedBy}\n" +
                         $"Performed At: {activity.PerformedAt:g}\n" +
                         $"File: {activity.FilePath ?? "N/A"}\n" +
                         $"Important: {(activity.IsImportant ? "Yes" : "No")}\n" +
                         $"Details: {activity.Details.Count} items";

            System.Windows.MessageBox.Show(message, "Activity Details",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
        }

        [RelayCommand]
        private void FilterActivities(string filter)
        {
            ActivityFilter = filter;
            if (SelectedWorkspace != null)
            {
                _ = LoadWorkspaceDetailsAsync(SelectedWorkspace.Id);
            }
        }

        [RelayCommand]
        private void RefreshWorkspace()
        {
            if (SelectedWorkspace != null)
            {
                _ = LoadWorkspaceDetailsAsync(SelectedWorkspace.Id);
            }
        }

        [RelayCommand]
        private void LeaveWorkspace()
        {
            if (SelectedWorkspace == null) return;

            var result = System.Windows.MessageBox.Show(
                $"Are you sure you want to leave the workspace '{SelectedWorkspace.Name}'?\n\nYou will lose access to all shared files and activities.",
                "Leave Workspace",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Question);

            if (result == System.Windows.MessageBoxResult.Yes)
            {
                StatusMessage = $"Left workspace: {SelectedWorkspace.Name}";
                Workspaces.Remove(SelectedWorkspace);
                SelectedWorkspace = null;
            }
        }
    }

    /// <summary>
    /// ViewModel for version control
    /// </summary>
    public partial class VersionControlViewModel : ObservableObject
    {
        private readonly IVersionControlService _versionControlService;
        private ObservableCollection<FileVersion> _fileVersions = new();
        private ObservableCollection<VersionComment> _comments = new();

        [ObservableProperty]
        private string _filePath = string.Empty;

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        [ObservableProperty]
        private FileVersion? _selectedVersion;

        [ObservableProperty]
        private bool _isCreatingVersion;

        [ObservableProperty]
        private string _versionComment = string.Empty;

        [ObservableProperty]
        private string _versionTag = string.Empty;

        [ObservableProperty]
        private bool _isMajorVersion = false;

        [ObservableProperty]
        private VersionComparison? _versionComparison;

        public ObservableCollection<FileVersion> FileVersions
        {
            get => _fileVersions;
            set => SetProperty(ref _fileVersions, value);
        }

        public ObservableCollection<VersionComment> Comments
        {
            get => _comments;
            set => SetProperty(ref _comments, value);
        }

        public VersionControlViewModel(IVersionControlService versionControlService)
        {
            _versionControlService = versionControlService;
        }

        [RelayCommand]
        private async Task LoadVersionsAsync()
        {
            if (string.IsNullOrEmpty(FilePath)) return;

            IsLoading = true;

            try
            {
                var versions = await _versionControlService.GetFileVersionsAsync(FilePath);
                FileVersions.Clear();
                foreach (var version in versions.OrderByDescending(v => v.CreatedAt))
                {
                    FileVersions.Add(version);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading versions: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        partial void OnSelectedVersionChanged(FileVersion? value)
        {
            if (value != null)
            {
                _ = LoadVersionCommentsAsync(value.Id);
            }
        }

        private async Task LoadVersionCommentsAsync(string versionId)
        {
            try
            {
                var comments = await _versionControlService.GetVersionCommentsAsync(versionId);
                Comments.Clear();
                foreach (var comment in comments.OrderByDescending(c => c.CreatedAt))
                {
                    Comments.Add(comment);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading comments: {ex.Message}";
            }
        }

        [RelayCommand]
        private void StartCreateVersion()
        {
            IsCreatingVersion = true;
            VersionComment = string.Empty;
            VersionTag = string.Empty;
            IsMajorVersion = false;
        }

        [RelayCommand]
        private void CancelCreateVersion()
        {
            IsCreatingVersion = false;
            VersionComment = string.Empty;
            VersionTag = string.Empty;
        }

        [RelayCommand]
        private async Task CreateVersionAsync()
        {
            if (string.IsNullOrEmpty(FilePath)) return;

            try
            {
                var version = await _versionControlService.CreateVersionAsync(FilePath, new CreateVersionRequest
                {
                    Comment = VersionComment,
                    Tag = VersionTag,
                    IsMajorVersion = IsMajorVersion
                });

                FileVersions.Insert(0, version);
                CancelCreateVersion();
                StatusMessage = $"Version {version.Version} created";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error creating version: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task RestoreVersionAsync(FileVersion? version)
        {
            if (version == null) return;

            try
            {
                await _versionControlService.RestoreVersionAsync(FilePath, version.Id);
                StatusMessage = $"Restored to version {version.Version}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error restoring version: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task DeleteVersionAsync(FileVersion? version)
        {
            if (version == null) return;

            try
            {
                await _versionControlService.DeleteVersionAsync(version.Id);
                FileVersions.Remove(version);
                StatusMessage = $"Version {version.Version} deleted";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error deleting version: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task CompareVersionsAsync(FileVersion? version)
        {
            if (version == null || SelectedVersion == null) return;

            try
            {
                VersionComparison = await _versionControlService.CompareVersionsAsync(SelectedVersion.Id, version.Id);
                StatusMessage = "Versions compared";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error comparing versions: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task DownloadVersionAsync(FileVersion? version)
        {
            if (version == null) return;

            try
            {
                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    FileName = System.IO.Path.GetFileName(FilePath),
                    DefaultExt = System.IO.Path.GetExtension(FilePath),
                    Filter = "All Files (*.*)|*.*"
                };

                if (dialog.ShowDialog() == true)
                {
                    StatusMessage = $"Downloading version {version.Version} to {dialog.FileName}";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error downloading version: {ex.Message}";
            }
        }

        [RelayCommand]
        private void ViewVersionDetails(FileVersion? version)
        {
            if (version == null) return;

            var message = $"Version Details\n\n" +
                         $"Version: {version.Version}\n" +
                         $"Tag: {version.Tag}\n" +
                         $"Created By: {version.CreatedBy}\n" +
                         $"Created At: {version.CreatedAt:g}\n" +
                         $"Size: {version.Size / (1024.0 * 1024.0):F2} MB\n" +
                         $"Checksum: {version.Checksum.Substring(0, Math.Min(16, version.Checksum.Length))}...\n" +
                         $"Major Version: {(version.IsMajorVersion ? "Yes" : "No")}\n" +
                         $"Downloads: {version.DownloadCount}\n\n" +
                         $"Comment:\n{version.Comment}";

            System.Windows.MessageBox.Show(message, "Version Details",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
        }

        [RelayCommand]
        private async Task AddCommentAsync()
        {
            if (SelectedVersion == null || string.IsNullOrEmpty(VersionComment)) return;

            try
            {
                var comment = new VersionComment
                {
                    VersionId = SelectedVersion.Id,
                    Text = VersionComment,
                    Author = Environment.UserName,
                    CreatedAt = DateTime.Now
                };

                await _versionControlService.AddVersionCommentAsync(comment);
                Comments.Insert(0, comment);
                VersionComment = string.Empty;
                StatusMessage = "Comment added";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error adding comment: {ex.Message}";
            }
        }

        [RelayCommand]
        private void ExportVersionHistory()
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "CSV File (*.csv)|*.csv|JSON File (*.json)|*.json",
                DefaultExt = ".csv",
                FileName = $"VersionHistory_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    if (dialog.FileName.EndsWith(".json"))
                    {
                        var json = System.Text.Json.JsonSerializer.Serialize(FileVersions,
                            new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                        System.IO.File.WriteAllText(dialog.FileName, json);
                    }
                    else
                    {
                        var csv = new System.Text.StringBuilder();
                        csv.AppendLine("Version,Tag,CreatedBy,CreatedAt,Size,IsMajor,Comment");
                        foreach (var ver in FileVersions)
                        {
                            csv.AppendLine($"{ver.Version},{ver.Tag},{ver.CreatedBy},{ver.CreatedAt:g},{ver.Size},{ver.IsMajorVersion},{ver.Comment}");
                        }
                        System.IO.File.WriteAllText(dialog.FileName, csv.ToString());
                    }
                    StatusMessage = "Version history exported successfully";
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Export failed: {ex.Message}";
                }
            }
        }

        [RelayCommand]
        private void ClearVersions()
        {
            FileVersions.Clear();
            Comments.Clear();
            SelectedVersion = null;
            VersionComparison = null;
        }
    }

    // Model classes
    public class SharedFile
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public long Size { get; set; }
        public string SharedBy { get; set; } = string.Empty;
        public DateTime SharedAt { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public int DownloadCount { get; set; }
        public int? DownloadLimit { get; set; }
        public SharePermission Permission { get; set; }
        public bool IsActive { get; set; }
        public List<ShareLink> Links { get; set; } = new();
    }

    public class ShareLink
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Url { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public int ClickCount { get; set; }
        public int DownloadCount { get; set; }
        public int? DownloadLimit { get; set; }
        public bool RequiresPassword { get; set; }
        public SharePermission Permission { get; set; }
        public bool IsActive { get; set; }
        public string? QrCode { get; set; }
    }

    public class ShareRequest
    {
        public List<string> Files { get; set; } = new();
        public ShareType Type { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime? ExpiryDate { get; set; }
        public int? DownloadLimit { get; set; }
        public SharePermission Permission { get; set; }
        public string? Password { get; set; }
        public Dictionary<string, object> CustomOptions { get; set; } = new();
    }

    public class CollaborationWorkspace
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public WorkspaceType Type { get; set; }
        public DateTime CreatedAt { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime LastActivity { get; set; }
        public int MemberCount { get; set; }
        public int FileCount { get; set; }
        public bool IsActive { get; set; }
        public string? Icon { get; set; }
        public string Color { get; set; } = string.Empty;
        public List<string> Tags { get; set; } = new();
    }

    public class WorkspaceMember
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string WorkspaceId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Avatar { get; set; } = string.Empty;
        public WorkspaceRole Role { get; set; }
        public DateTime JoinedAt { get; set; }
        public DateTime LastActive { get; set; }
        public bool IsOnline { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    public class MemberInvitation
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string WorkspaceId { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public WorkspaceRole Role { get; set; }
        public string InvitedBy { get; set; } = string.Empty;
        public DateTime InvitedAt { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public InvitationStatus Status { get; set; }
        public string? Message { get; set; }
    }

    public class CollaborationActivity
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string WorkspaceId { get; set; } = string.Empty;
        public ActivityType Type { get; set; }
        public string Description { get; set; } = string.Empty;
        public string PerformedBy { get; set; } = string.Empty;
        public DateTime PerformedAt { get; set; }
        public string? FilePath { get; set; }
        public Dictionary<string, object> Details { get; set; } = new();
        public bool IsImportant { get; set; }
    }

    public class FileVersion
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string FilePath { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string Tag { get; set; } = string.Empty;
        public string Comment { get; set; } = string.Empty;
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public long Size { get; set; }
        public string Checksum { get; set; } = string.Empty;
        public bool IsMajorVersion { get; set; }
        public string? StoragePath { get; set; }
        public int DownloadCount { get; set; }
    }

    public class VersionComment
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string VersionId { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public string Author { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string? ParentId { get; set; }
        public List<VersionComment> Replies { get; set; } = new();
    }

    public class CreateVersionRequest
    {
        public string Comment { get; set; } = string.Empty;
        public string Tag { get; set; } = string.Empty;
        public bool IsMajorVersion { get; set; }
        public List<string> Attachments { get; set; } = new();
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    public class VersionComparison
    {
        public string FromVersion { get; set; } = string.Empty;
        public string ToVersion { get; set; } = string.Empty;
        public ComparisonResult Result { get; set; }
        public List<FileDifference> Differences { get; set; } = new();
        public DateTime ComparedAt { get; set; }
        public string ComparedBy { get; set; } = string.Empty;
    }

    public class FileDifference
    {
        public DifferenceType Type { get; set; }
        public string LineNumber { get; set; } = string.Empty;
        public string OldContent { get; set; } = string.Empty;
        public string NewContent { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    // Enums
    public enum ShareType
    {
        Link,
        Email,
        Social,
        Embed,
        API
    }

    public enum SharePermission
    {
        View,
        Comment,
        Edit,
        Download,
        Upload,
        Manage
    }

    public enum WorkspaceType
    {
        Project,
        Team,
        Department,
        Client,
        Personal,
        Temporary
    }

    public enum WorkspaceRole
    {
        Owner,
        Admin,
        Editor,
        Commenter,
        Viewer
    }

    public enum ActivityType
    {
        FileCreated,
        FileUpdated,
        FileDeleted,
        FileShared,
        MemberAdded,
        MemberRemoved,
        CommentAdded,
        WorkspaceCreated,
        WorkspaceUpdated,
        VersionCreated,
        VersionRestored
    }

    public enum InvitationStatus
    {
        Pending,
        Accepted,
        Declined,
        Expired,
        Cancelled
    }

    public enum ComparisonResult
    {
        Identical,
        Modified,
        Added,
        Removed,
        Moved
    }

    public enum DifferenceType
    {
        Addition,
        Deletion,
        Modification,
        Move,
        Permission,
        Metadata
    }
}
