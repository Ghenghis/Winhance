using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Winhance.WPF.Features.FileManager.Models;

namespace Winhance.WPF.Features.FileManager.ViewModels;

public partial class AdvancedSearchDialogViewModel : ObservableObject
{
    [ObservableProperty]
    private string? _searchPath;

    [ObservableProperty]
    private string? _namePattern = "*";

    [ObservableProperty]
    private bool _useRegex;

    [ObservableProperty]
    private string? _contentSearch;

    [ObservableProperty]
    private string? _minSize;

    [ObservableProperty]
    private string _minSizeUnit = "KB";

    [ObservableProperty]
    private string? _maxSize;

    [ObservableProperty]
    private string _maxSizeUnit = "MB";

    [ObservableProperty]
    private DateTime? _minDate;

    [ObservableProperty]
    private DateTime? _maxDate;

    [ObservableProperty]
    private bool _filterImages;

    [ObservableProperty]
    private bool _filterVideos;

    [ObservableProperty]
    private bool _filterDocuments;

    [ObservableProperty]
    private bool _filterAudio;

    [ObservableProperty]
    private bool _filterArchives;

    [ObservableProperty]
    private bool _filterCode;

    [ObservableProperty]
    private bool _includeSubfolders = true;

    [ObservableProperty]
    private bool _includeHidden;

    [ObservableProperty]
    private bool _caseSensitive;

    [ObservableProperty]
    private ObservableCollection<FileItemModel> _previewResults = new();

    public event EventHandler<SearchCriteria>? SearchRequested;
    public event EventHandler? CancelRequested;

    [RelayCommand]
    private void BrowseFolder()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Select Search Location"
        };

        if (dialog.ShowDialog() == true)
        {
            SearchPath = dialog.FolderName;
        }
    }

    [RelayCommand]
    private void Search()
    {
        var criteria = new SearchCriteria
        {
            SearchPath = SearchPath ?? "",
            NamePattern = NamePattern ?? "*",
            UseRegex = UseRegex,
            ContentSearch = ContentSearch,
            MinSize = ParseSize(MinSize, MinSizeUnit),
            MaxSize = ParseSize(MaxSize, MaxSizeUnit),
            MinDate = MinDate,
            MaxDate = MaxDate,
            IncludeSubfolders = IncludeSubfolders,
            IncludeHidden = IncludeHidden,
            CaseSensitive = CaseSensitive,
            FilterImages = FilterImages,
            FilterVideos = FilterVideos,
            FilterDocuments = FilterDocuments,
            FilterAudio = FilterAudio,
            FilterArchives = FilterArchives,
            FilterCode = FilterCode
        };

        SearchRequested?.Invoke(this, criteria);
    }

    [RelayCommand]
    private void Clear()
    {
        NamePattern = "*";
        UseRegex = false;
        ContentSearch = null;
        MinSize = null;
        MaxSize = null;
        MinDate = null;
        MaxDate = null;
        FilterImages = false;
        FilterVideos = false;
        FilterDocuments = false;
        FilterAudio = false;
        FilterArchives = false;
        FilterCode = false;
        IncludeSubfolders = true;
        IncludeHidden = false;
        CaseSensitive = false;
        PreviewResults.Clear();
    }

    [RelayCommand]
    private void Cancel()
    {
        CancelRequested?.Invoke(this, EventArgs.Empty);
    }

    private static long? ParseSize(string? value, string unit)
    {
        if (string.IsNullOrWhiteSpace(value) || !long.TryParse(value, out var size))
            return null;

        return unit switch
        {
            "B" => size,
            "KB" => size * 1024,
            "MB" => size * 1024 * 1024,
            "GB" => size * 1024 * 1024 * 1024,
            _ => size
        };
    }
}

public class SearchCriteria
{
    public string SearchPath { get; set; } = "";
    public string NamePattern { get; set; } = "*";
    public bool UseRegex { get; set; }
    public string? ContentSearch { get; set; }
    public long? MinSize { get; set; }
    public long? MaxSize { get; set; }
    public DateTime? MinDate { get; set; }
    public DateTime? MaxDate { get; set; }
    public bool IncludeSubfolders { get; set; } = true;
    public bool IncludeHidden { get; set; }
    public bool CaseSensitive { get; set; }
    public bool FilterImages { get; set; }
    public bool FilterVideos { get; set; }
    public bool FilterDocuments { get; set; }
    public bool FilterAudio { get; set; }
    public bool FilterArchives { get; set; }
    public bool FilterCode { get; set; }
}
