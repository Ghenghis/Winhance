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
    /// ViewModel for file metadata management
    /// </summary>
    public partial class FileMetadataViewModel : ObservableObject
    {
        private readonly IMetadataService _metadataService;
        private ObservableCollection<MetadataCategory> _metadataCategories = new();
        private ObservableCollection<MetadataField> _metadataFields = new();

        [ObservableProperty]
        private string _filePath = string.Empty;

        [ObservableProperty]
        private FileMetadata? _fileMetadata;

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private bool _isEditing;

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        [ObservableProperty]
        private MetadataCategory? _selectedCategory;

        [ObservableProperty]
        private MetadataField? _selectedField;

        [ObservableProperty]
        private string _customFieldName = string.Empty;

        [ObservableProperty]
        private string _customFieldValue = string.Empty;

        [ObservableProperty]
        private MetadataType _customFieldType = MetadataType.Text;

        public ObservableCollection<MetadataCategory> MetadataCategories
        {
            get => _metadataCategories;
            set => SetProperty(ref _metadataCategories, value);
        }

        public ObservableCollection<MetadataField> MetadataFields
        {
            get => _metadataFields;
            set => SetProperty(ref _metadataFields, value);
        }

        public FileMetadataViewModel(IMetadataService metadataService)
        {
            _metadataService = metadataService;
            _ = LoadMetadataCategoriesAsync();
        }

        private async Task LoadMetadataCategoriesAsync()
        {
            try
            {
                var categories = await _metadataService.GetMetadataCategoriesAsync();
                MetadataCategories.Clear();
                foreach (var category in categories)
                {
                    MetadataCategories.Add(category);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading categories: {ex.Message}";
            }
        }

        partial void OnSelectedCategoryChanged(MetadataCategory? value)
        {
            if (value != null)
            {
                _ = LoadMetadataFieldsAsync(value.Id);
            }
        }

        private async Task LoadMetadataFieldsAsync(string categoryId)
        {
            try
            {
                var fields = await _metadataService.GetMetadataFieldsAsync(categoryId);
                MetadataFields.Clear();
                foreach (var field in fields)
                {
                    MetadataFields.Add(field);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading fields: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task LoadMetadataAsync()
        {
            if (string.IsNullOrEmpty(FilePath)) return;

            IsLoading = true;

            try
            {
                FileMetadata = await _metadataService.GetFileMetadataAsync(FilePath);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading metadata: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private void StartEdit()
        {
            IsEditing = true;
        }

        [RelayCommand]
        private void CancelEdit()
        {
            IsEditing = false;
            _ = LoadMetadataAsync();
        }

        [RelayCommand]
        private async Task SaveMetadataAsync()
        {
            if (FileMetadata == null) return;

            try
            {
                await _metadataService.SaveFileMetadataAsync(FilePath, FileMetadata);
                IsEditing = false;
                StatusMessage = "Metadata saved successfully";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error saving metadata: {ex.Message}";
            }
        }

        [RelayCommand]
        private void AddMetadataField()
        {
            if (SelectedField == null || FileMetadata == null) return;

            var field = new MetadataValue
            {
                FieldId = SelectedField.Id,
                FieldName = SelectedField.Name,
                FieldType = SelectedField.Type,
                Value = GetDefaultValue(SelectedField.Type)
            };

            FileMetadata.CustomFields.Add(field);
        }

        [RelayCommand]
        private void RemoveMetadataField(MetadataValue? field)
        {
            if (field == null || FileMetadata == null) return;
            FileMetadata.CustomFields.Remove(field);
        }

        [RelayCommand]
        private void AddCustomField()
        {
            if (string.IsNullOrEmpty(CustomFieldName) || FileMetadata == null) return;

            var field = new MetadataValue
            {
                FieldId = Guid.NewGuid().ToString(),
                FieldName = CustomFieldName,
                FieldType = CustomFieldType,
                Value = ConvertValue(CustomFieldValue, CustomFieldType)
            };

            FileMetadata.CustomFields.Add(field);
            CustomFieldName = string.Empty;
            CustomFieldValue = string.Empty;
        }

        [RelayCommand]
        private async Task ExtractMetadataAsync()
        {
            if (string.IsNullOrEmpty(FilePath)) return;

            IsLoading = true;
            StatusMessage = "Extracting metadata...";

            try
            {
                FileMetadata = await _metadataService.ExtractMetadataAsync(FilePath);
                StatusMessage = "Metadata extracted successfully";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error extracting metadata: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task ClearMetadataAsync()
        {
            if (string.IsNullOrEmpty(FilePath)) return;

            try
            {
                await _metadataService.ClearFileMetadataAsync(FilePath);
                FileMetadata = null;
                StatusMessage = "Metadata cleared";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error clearing metadata: {ex.Message}";
            }
        }

        [RelayCommand]
        private void ExportMetadata()
        {
            if (FileMetadata == null) return;

            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "JSON File (*.json)|*.json|XML File (*.xml)|*.xml",
                DefaultExt = ".json",
                FileName = $"Metadata_{System.IO.Path.GetFileNameWithoutExtension(FilePath)}_{DateTime.Now:yyyyMMdd_HHmmss}.json"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var json = System.Text.Json.JsonSerializer.Serialize(FileMetadata,
                        new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                    System.IO.File.WriteAllText(dialog.FileName, json);
                    StatusMessage = "Metadata exported successfully";
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Export failed: {ex.Message}";
                }
            }
        }

        [RelayCommand]
        private async Task ImportMetadataAsync()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "JSON File (*.json)|*.json|XML File (*.xml)|*.xml",
                DefaultExt = ".json"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var json = await System.IO.File.ReadAllTextAsync(dialog.FileName);
                    FileMetadata = System.Text.Json.JsonSerializer.Deserialize<FileMetadata>(json);
                    StatusMessage = "Metadata imported successfully";
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Import failed: {ex.Message}";
                }
            }
        }

        private object GetDefaultValue(MetadataType type)
        {
            return type switch
            {
                MetadataType.Text => string.Empty,
                MetadataType.Number => 0,
                MetadataType.Date => DateTime.Now,
                MetadataType.Boolean => false,
                MetadataType.List => new List<string>(),
                _ => string.Empty
            };
        }

        private object ConvertValue(string value, MetadataType type)
        {
            return type switch
            {
                MetadataType.Number => double.TryParse(value, out var num) ? num : 0,
                MetadataType.Date => DateTime.TryParse(value, out var date) ? date : DateTime.Now,
                MetadataType.Boolean => bool.TryParse(value, out var boolVal) ? boolVal : false,
                _ => value
            };
        }
    }

    /// <summary>
    /// ViewModel for file tagging
    /// </summary>
    public partial class FileTaggingViewModel : ObservableObject
    {
        private readonly ITaggingService _taggingService;
        private ObservableCollection<Tag> _availableTags = new();
        private ObservableCollection<TagCategory> _tagCategories = new();
        private ObservableCollection<FileTag> _fileTags = new();

        [ObservableProperty]
        private string _filePath = string.Empty;

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        [ObservableProperty]
        private Tag? _selectedTag;

        [ObservableProperty]
        private TagCategory? _selectedCategory;

        [ObservableProperty]
        private string _newTagName = string.Empty;

        [ObservableProperty]
        private string _newTagDescription = string.Empty;

        [ObservableProperty]
        private string _tagSearchText = string.Empty;

        [ObservableProperty]
        private ObservableCollection<Tag> _suggestedTags = new();

        [ObservableProperty]
        private bool _showAutoSuggested = true;

        public ObservableCollection<Tag> AvailableTags
        {
            get => _availableTags;
            set => SetProperty(ref _availableTags, value);
        }

        public ObservableCollection<TagCategory> TagCategories
        {
            get => _tagCategories;
            set => SetProperty(ref _tagCategories, value);
        }

        public ObservableCollection<FileTag> FileTags
        {
            get => _fileTags;
            set => SetProperty(ref _fileTags, value);
        }

        public ObservableCollection<Tag> SuggestedTags
        {
            get => _suggestedTags;
            set => SetProperty(ref _suggestedTags, value);
        }

        public FileTaggingViewModel(ITaggingService taggingService)
        {
            _taggingService = taggingService;
            _ = LoadTagsAsync();
            _ = LoadTagCategoriesAsync();
        }

        private async Task LoadTagsAsync()
        {
            try
            {
                var tags = await _taggingService.GetAllTagsAsync();
                AvailableTags.Clear();
                foreach (var tag in tags.OrderBy(t => t.Name))
                {
                    AvailableTags.Add(tag);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading tags: {ex.Message}";
            }
        }

        private async Task LoadTagCategoriesAsync()
        {
            try
            {
                var categories = await _taggingService.GetTagCategoriesAsync();
                TagCategories.Clear();
                foreach (var category in categories)
                {
                    TagCategories.Add(category);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading categories: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task LoadFileTagsAsync()
        {
            if (string.IsNullOrEmpty(FilePath)) return;

            IsLoading = true;

            try
            {
                var tags = await _taggingService.GetFileTagsAsync(FilePath);
                FileTags.Clear();
                foreach (var tag in tags)
                {
                    FileTags.Add(tag);
                }

                if (ShowAutoSuggested)
                {
                    await LoadSuggestedTagsAsync();
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading file tags: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task LoadSuggestedTagsAsync()
        {
            try
            {
                var suggestions = await _taggingService.GetSuggestedTagsAsync(FilePath);
                SuggestedTags.Clear();
                foreach (var tag in suggestions.Take(10))
                {
                    SuggestedTags.Add(tag);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading suggestions: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task AddTagAsync(Tag? tag)
        {
            if (tag == null || string.IsNullOrEmpty(FilePath)) return;

            try
            {
                var fileTag = new FileTag
                {
                    TagId = tag.Id,
                    TagName = tag.Name,
                    Category = tag.Category,
                    Color = tag.Color,
                    AppliedAt = DateTime.Now
                };

                await _taggingService.AddTagToFileAsync(FilePath, fileTag);
                FileTags.Add(fileTag);
                
                StatusMessage = $"Tag '{tag.Name}' added";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error adding tag: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task RemoveTagAsync(FileTag? fileTag)
        {
            if (fileTag == null || string.IsNullOrEmpty(FilePath)) return;

            try
            {
                await _taggingService.RemoveTagFromFileAsync(FilePath, fileTag.TagId);
                FileTags.Remove(fileTag);
                StatusMessage = $"Tag '{fileTag.TagName}' removed";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error removing tag: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task CreateTagAsync()
        {
            if (string.IsNullOrEmpty(NewTagName) || SelectedCategory == null) return;

            try
            {
                var tag = new Tag
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = NewTagName,
                    Description = NewTagDescription,
                    Category = SelectedCategory.Name,
                    CategoryId = SelectedCategory.Id,
                    Color = GenerateRandomColor(),
                    CreatedAt = DateTime.Now,
                    UsageCount = 0
                };

                await _taggingService.CreateTagAsync(tag);
                AvailableTags.Add(tag);
                
                NewTagName = string.Empty;
                NewTagDescription = string.Empty;
                StatusMessage = $"Tag '{tag.Name}' created";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error creating tag: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task DeleteTagAsync(Tag? tag)
        {
            if (tag == null) return;

            try
            {
                await _taggingService.DeleteTagAsync(tag.Id);
                AvailableTags.Remove(tag);
                StatusMessage = $"Tag '{tag.Name}' deleted";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error deleting tag: {ex.Message}";
            }
        }

        [RelayCommand]
        private void FilterTags()
        {
            if (string.IsNullOrEmpty(TagSearchText))
            {
                _ = LoadTagsAsync();
                return;
            }

            var filtered = AvailableTags.Where(t => 
                t.Name.Contains(TagSearchText, StringComparison.OrdinalIgnoreCase) ||
                t.Description.Contains(TagSearchText, StringComparison.OrdinalIgnoreCase));

            AvailableTags.Clear();
            foreach (var tag in filtered)
            {
                AvailableTags.Add(tag);
            }
        }

        [RelayCommand]
        private async Task ApplySuggestedTagAsync(Tag? tag)
        {
            if (tag == null) return;
            await AddTagAsync(tag);
            SuggestedTags.Remove(tag);
        }

        [RelayCommand]
        private async Task ApplyAllSuggestedTagsAsync()
        {
            if (!SuggestedTags.Any()) return;

            foreach (var tag in SuggestedTags.ToList())
            {
                await AddTagAsync(tag);
            }
            SuggestedTags.Clear();
            StatusMessage = "All suggested tags applied";
        }

        [RelayCommand]
        private void ClearAllTags()
        {
            FileTags.Clear();
            StatusMessage = "All tags cleared";
        }

        [RelayCommand]
        private void CreateCategory()
        {
            var categoryName = Microsoft.VisualBasic.Interaction.InputBox(
                "Enter category name:",
                "Create Category",
                "");

            if (!string.IsNullOrEmpty(categoryName))
            {
                var category = new TagCategory
                {
                    Name = categoryName,
                    Description = "",
                    Color = GenerateRandomColor(),
                    Order = TagCategories.Count
                };

                TagCategories.Add(category);
                StatusMessage = $"Category '{categoryName}' created";
            }
        }

        [RelayCommand]
        private void EditTag(Tag? tag)
        {
            if (tag == null) return;

            var message = $"Edit Tag\n\n" +
                         $"Name: {tag.Name}\n" +
                         $"Category: {tag.Category}\n" +
                         $"Description: {tag.Description}\n" +
                         $"Usage Count: {tag.UsageCount}\n" +
                         $"Created: {tag.CreatedAt:g}";

            System.Windows.MessageBox.Show(message, "Tag Details",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
        }

        [RelayCommand]
        private void ExportTags()
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "JSON File (*.json)|*.json|CSV File (*.csv)|*.csv",
                DefaultExt = ".json",
                FileName = $"Tags_{DateTime.Now:yyyyMMdd_HHmmss}.json"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    if (dialog.FileName.EndsWith(".json"))
                    {
                        var json = System.Text.Json.JsonSerializer.Serialize(AvailableTags,
                            new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                        System.IO.File.WriteAllText(dialog.FileName, json);
                    }
                    else
                    {
                        var csv = new System.Text.StringBuilder();
                        csv.AppendLine("Name,Category,Description,Color,UsageCount,Created");
                        foreach (var tag in AvailableTags)
                        {
                            csv.AppendLine($"{tag.Name},{tag.Category},{tag.Description},{tag.Color},{tag.UsageCount},{tag.CreatedAt:g}");
                        }
                        System.IO.File.WriteAllText(dialog.FileName, csv.ToString());
                    }
                    StatusMessage = "Tags exported successfully";
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Export failed: {ex.Message}";
                }
            }
        }

        [RelayCommand]
        private async Task ImportTagsAsync()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "JSON File (*.json)|*.json",
                DefaultExt = ".json"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var json = await System.IO.File.ReadAllTextAsync(dialog.FileName);
                    var importedTags = System.Text.Json.JsonSerializer.Deserialize<List<Tag>>(json);
                    
                    if (importedTags != null)
                    {
                        foreach (var tag in importedTags)
                        {
                            if (!AvailableTags.Any(t => t.Name == tag.Name))
                            {
                                AvailableTags.Add(tag);
                            }
                        }
                        StatusMessage = $"{importedTags.Count} tags imported successfully";
                    }
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Import failed: {ex.Message}";
                }
            }
        }

        private string GenerateRandomColor()
        {
            var colors = new[] { "#FF6B6B", "#4ECDC4", "#45B7D1", "#96CEB4", "#FFEAA7", "#DDA0DD", "#98D8C8", "#F7DC6F" };
            return colors[new Random().Next(colors.Length)];
        }
    }

    /// <summary>
    /// ViewModel for virtual folders
    /// </summary>
    public partial class VirtualFoldersViewModel : ObservableObject
    {
        private readonly IVirtualFolderService _virtualFolderService;
        private ObservableCollection<VirtualFolder> _virtualFolders = new();
        private ObservableCollection<VirtualFolderItem> _folderItems = new();

        [ObservableProperty]
        private VirtualFolder? _selectedFolder;

        [ObservableProperty]
        private bool _isCreatingFolder;

        [ObservableProperty]
        private string _folderName = string.Empty;

        [ObservableProperty]
        private string _folderDescription = string.Empty;

        [ObservableProperty]
        private FolderType _folderType = FolderType.Tags;

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        [ObservableProperty]
        private VirtualFolderItem? _selectedItem;

        [ObservableProperty]
        private string _filterText = string.Empty;

        [ObservableProperty]
        private SortBy _sortBy = SortBy.Name;

        [ObservableProperty]
        private SortOrder _sortOrder = SortOrder.Ascending;

        [ObservableProperty]
        private bool _includeSubfolders = true;

        [ObservableProperty]
        private string[] _selectedTags = Array.Empty<string>();

        [ObservableProperty]
        private string _searchQuery = string.Empty;

        [ObservableProperty]
        private DateTime? _dateFrom;

        [ObservableProperty]
        private DateTime? _dateTo;

        [ObservableProperty]
        private long _minSize;

        [ObservableProperty]
        private long _maxSize = long.MaxValue;

        public ObservableCollection<VirtualFolder> VirtualFolders
        {
            get => _virtualFolders;
            set => SetProperty(ref _virtualFolders, value);
        }

        public ObservableCollection<VirtualFolderItem> FolderItems
        {
            get => _folderItems;
            set => SetProperty(ref _folderItems, value);
        }

        public VirtualFoldersViewModel(IVirtualFolderService virtualFolderService)
        {
            _virtualFolderService = virtualFolderService;
            _ = LoadVirtualFoldersAsync();
        }

        private async Task LoadVirtualFoldersAsync()
        {
            try
            {
                var folders = await _virtualFolderService.GetVirtualFoldersAsync();
                VirtualFolders.Clear();
                foreach (var folder in folders.OrderBy(f => f.Name))
                {
                    VirtualFolders.Add(folder);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading folders: {ex.Message}";
            }
        }

        partial void OnSelectedFolderChanged(VirtualFolder? value)
        {
            if (value != null)
            {
                _ = LoadFolderItemsAsync(value.Id);
            }
        }

        private async Task LoadFolderItemsAsync(string folderId)
        {
            try
            {
                var items = await _virtualFolderService.GetFolderItemsAsync(folderId);
                ApplySortingAndFiltering(items);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading items: {ex.Message}";
            }
        }

        private void ApplySortingAndFiltering(IEnumerable<VirtualFolderItem> items)
        {
            var filtered = items;

            if (!string.IsNullOrEmpty(FilterText))
            {
                filtered = filtered.Where(i => 
                    i.Name.Contains(FilterText, StringComparison.OrdinalIgnoreCase) ||
                    i.Path.Contains(FilterText, StringComparison.OrdinalIgnoreCase));
            }

            var sorted = SortBy switch
            {
                SortBy.Name => SortOrder == SortOrder.Ascending 
                    ? filtered.OrderBy(i => i.Name)
                    : filtered.OrderByDescending(i => i.Name),
                SortBy.Size => SortOrder == SortOrder.Ascending 
                    ? filtered.OrderBy(i => i.Size)
                    : filtered.OrderByDescending(i => i.Size),
                SortBy.Date => SortOrder == SortOrder.Ascending 
                    ? filtered.OrderBy(i => i.ModifiedDate)
                    : filtered.OrderByDescending(i => i.ModifiedDate),
                SortBy.Type => SortOrder == SortOrder.Ascending 
                    ? filtered.OrderBy(i => i.Type)
                    : filtered.OrderByDescending(i => i.Type),
                _ => filtered
            };

            FolderItems.Clear();
            foreach (var item in sorted)
            {
                FolderItems.Add(item);
            }
        }

        [RelayCommand]
        private void StartCreateFolder()
        {
            IsCreatingFolder = true;
            FolderName = string.Empty;
            FolderDescription = string.Empty;
            FolderType = FolderType.Tags;
            SelectedTags = Array.Empty<string>();
            SearchQuery = string.Empty;
            DateFrom = null;
            DateTo = null;
            MinSize = 0;
            MaxSize = long.MaxValue;
        }

        [RelayCommand]
        private void CancelCreateFolder()
        {
            IsCreatingFolder = false;
            FolderName = string.Empty;
            FolderDescription = string.Empty;
        }

        [RelayCommand]
        private async Task CreateFolderAsync()
        {
            if (string.IsNullOrEmpty(FolderName)) return;

            try
            {
                var criteria = new FolderCriteria
                {
                    Type = FolderType,
                    Tags = SelectedTags,
                    SearchQuery = SearchQuery,
                    DateFrom = DateFrom,
                    DateTo = DateTo,
                    MinSize = MinSize,
                    MaxSize = MaxSize,
                    IncludeSubfolders = IncludeSubfolders
                };

                var folder = new VirtualFolder
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = FolderName,
                    Description = FolderDescription,
                    Type = FolderType,
                    Criteria = criteria,
                    CreatedAt = DateTime.Now,
                    ItemCount = 0,
                    IsDynamic = true
                };

                await _virtualFolderService.CreateVirtualFolderAsync(folder);
                VirtualFolders.Add(folder);
                
                CancelCreateFolder();
                StatusMessage = $"Virtual folder '{FolderName}' created";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error creating folder: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task DeleteFolderAsync(VirtualFolder? folder)
        {
            if (folder == null) return;

            try
            {
                await _virtualFolderService.DeleteVirtualFolderAsync(folder.Id);
                VirtualFolders.Remove(folder);
                StatusMessage = $"Virtual folder '{folder.Name}' deleted";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error deleting folder: {ex.Message}";
            }
        }

        [RelayCommand]
        private void RefreshFolder(VirtualFolder? folder)
        {
            if (folder == null) return;
            _ = LoadFolderItemsAsync(folder.Id);
        }

        [RelayCommand]
        private void OpenItem(VirtualFolderItem? item)
        {
            if (item == null) return;

            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = item.Path,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error opening item: {ex.Message}";
            }
        }

        [RelayCommand]
        private void OpenItemLocation(VirtualFolderItem? item)
        {
            if (item == null) return;

            try
            {
                var args = $"/select, \"{item.Path}\"";
                System.Diagnostics.Process.Start("explorer.exe", args);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error opening location: {ex.Message}";
            }
        }

        [RelayCommand]
        private void RemoveFromFolder(VirtualFolderItem? item)
        {
            if (item == null || SelectedFolder == null) return;

            var result = System.Windows.MessageBox.Show(
                $"Remove '{item.Name}' from virtual folder '{SelectedFolder.Name}'?\n\nNote: This only removes the item from the virtual folder, not from disk.",
                "Confirm Remove",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Question);

            if (result == System.Windows.MessageBoxResult.Yes)
            {
                FolderItems.Remove(item);
                if (SelectedFolder != null)
                {
                    SelectedFolder.ItemCount--;
                }
                StatusMessage = $"'{item.Name}' removed from folder";
            }
        }

        [RelayCommand]
        private void ExportFolder(VirtualFolder? folder)
        {
            if (folder == null) return;

            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "CSV File (*.csv)|*.csv|JSON File (*.json)|*.json",
                DefaultExt = ".csv",
                FileName = $"VirtualFolder_{folder.Name}_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    if (dialog.FileName.EndsWith(".json"))
                    {
                        var exportData = new { Folder = folder, Items = FolderItems };
                        var json = System.Text.Json.JsonSerializer.Serialize(exportData,
                            new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                        System.IO.File.WriteAllText(dialog.FileName, json);
                    }
                    else
                    {
                        var csv = new System.Text.StringBuilder();
                        csv.AppendLine("Name,Path,Type,Size,Modified,Tags");
                        foreach (var item in FolderItems)
                        {
                            csv.AppendLine($"{item.Name},{item.Path},{item.Type},{item.Size},{item.ModifiedDate:g},{string.Join(";", item.Tags)}");
                        }
                        System.IO.File.WriteAllText(dialog.FileName, csv.ToString());
                    }
                    StatusMessage = $"Virtual folder '{folder.Name}' exported successfully";
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Export failed: {ex.Message}";
                }
            }
        }

        [RelayCommand]
        private void AddTagToFilter(string tag)
        {
            var list = SelectedTags.ToList();
            list.Add(tag);
            SelectedTags = list.ToArray();
        }

        [RelayCommand]
        private void RemoveTagFromFilter(string tag)
        {
            var list = SelectedTags.ToList();
            list.Remove(tag);
            SelectedTags = list.ToArray();
        }

        [RelayCommand]
        private void ClearFilters()
        {
            FilterText = string.Empty;
            SelectedTags = Array.Empty<string>();
            SearchQuery = string.Empty;
            DateFrom = null;
            DateTo = null;
            MinSize = 0;
            MaxSize = long.MaxValue;
        }
    }

    // Model classes
    public class FileMetadata
    {
        public string FilePath { get; set; } = string.Empty;
        public Dictionary<string, object> BasicProperties { get; set; } = new();
        public Dictionary<string, object> ExtendedProperties { get; set; } = new();
        public List<MetadataValue> CustomFields { get; set; } = new();
        public DateTime ExtractedAt { get; set; }
        public string ExtractorVersion { get; set; } = string.Empty;
    }

    public class MetadataValue
    {
        public string FieldId { get; set; } = string.Empty;
        public string FieldName { get; set; } = string.Empty;
        public MetadataType FieldType { get; set; }
        public object Value { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsReadOnly { get; set; }
        public DateTime ModifiedAt { get; set; }
    }

    public class MetadataCategory
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public int Order { get; set; }
        public bool IsSystem { get; set; }
    }

    public class MetadataField
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string CategoryId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public MetadataType Type { get; set; }
        public object DefaultValue { get; set; } = string.Empty;
        public bool IsRequired { get; set; }
        public bool IsReadOnly { get; set; }
        public string[] ValidationRules { get; set; } = Array.Empty<string>();
    }

    public class Tag
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string CategoryId { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public int UsageCount { get; set; }
        public bool IsSystem { get; set; }
        public List<string> Aliases { get; set; } = new();
    }

    public class TagCategory
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;
        public int Order { get; set; }
        public bool IsReadOnly { get; set; }
    }

    public class FileTag
    {
        public string TagId { get; set; } = string.Empty;
        public string TagName { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;
        public DateTime AppliedAt { get; set; }
        public string? AppliedBy { get; set; }
        public int Confidence { get; set; }
    }

    public class VirtualFolder
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public FolderType Type { get; set; }
        public FolderCriteria Criteria { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
        public DateTime? LastUpdated { get; set; }
        public int ItemCount { get; set; }
        public bool IsDynamic { get; set; }
        public string? Icon { get; set; }
        public string Color { get; set; } = string.Empty;
    }

    public class VirtualFolderItem
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public long Size { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime ModifiedDate { get; set; }
        public DateTime AccessedDate { get; set; }
        public List<string> Tags { get; set; } = new();
        public Dictionary<string, object> Metadata { get; set; } = new();
        public bool IsFolder { get; set; }
        public string? Icon { get; set; }
    }

    public class FolderCriteria
    {
        public FolderType Type { get; set; }
        public string[] Tags { get; set; } = Array.Empty<string>();
        public string SearchQuery { get; set; } = string.Empty;
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }
        public long MinSize { get; set; }
        public long MaxSize { get; set; }
        public string[] FileTypes { get; set; } = Array.Empty<string>();
        public string[] Paths { get; set; } = Array.Empty<string>();
        public bool IncludeSubfolders { get; set; }
        public Dictionary<string, object> CustomCriteria { get; set; } = new();
    }

    // Enums
    public enum MetadataType
    {
        Text,
        Number,
        Date,
        Boolean,
        List,
        URL,
        Email,
        Phone,
        Color,
        Rating,
        Location,
        Custom
    }

    public enum FolderType
    {
        Tags,
        Search,
        DateRange,
        SizeRange,
        FileType,
        Path,
        Custom,
        Smart,
        Collection
    }

    public enum SortBy
    {
        Name,
        Size,
        Date,
        Type,
        Path
    }

    public enum SortOrder
    {
        Ascending,
        Descending
    }
}
