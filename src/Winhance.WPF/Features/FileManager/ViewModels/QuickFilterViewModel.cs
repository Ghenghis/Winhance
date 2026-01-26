using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Winhance.WPF.Features.FileManager.ViewModels;

public partial class QuickFilterViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _showDocuments;

    [ObservableProperty]
    private bool _showImages;

    [ObservableProperty]
    private bool _showVideos;

    [ObservableProperty]
    private bool _showAudio;

    [ObservableProperty]
    private bool _showArchives;

    [ObservableProperty]
    private bool _showCode;

    [ObservableProperty]
    private bool _filterToday;

    [ObservableProperty]
    private bool _filterThisWeek;

    [ObservableProperty]
    private bool _filterThisMonth;

    [ObservableProperty]
    private bool _filterThisYear;

    [ObservableProperty]
    private bool _filterTiny;

    [ObservableProperty]
    private bool _filterSmall;

    [ObservableProperty]
    private bool _filterMedium;

    [ObservableProperty]
    private bool _filterLarge;

    [ObservableProperty]
    private bool _filterHuge;

    [ObservableProperty]
    private int _activeFilterCount;

    [ObservableProperty]
    private bool _hasActiveFilters;

    public event EventHandler? FiltersChanged;

    private static readonly Dictionary<string, string[]> TypeExtensions = new()
    {
        ["Documents"] = new[] { ".doc", ".docx", ".pdf", ".txt", ".rtf", ".odt", ".xls", ".xlsx", ".ppt", ".pptx" },
        ["Images"] = new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".svg", ".ico", ".tiff" },
        ["Videos"] = new[] { ".mp4", ".avi", ".mkv", ".mov", ".wmv", ".flv", ".webm", ".m4v" },
        ["Audio"] = new[] { ".mp3", ".wav", ".flac", ".aac", ".ogg", ".wma", ".m4a" },
        ["Archives"] = new[] { ".zip", ".rar", ".7z", ".tar", ".gz", ".bz2", ".xz" },
        ["Code"] = new[] { ".cs", ".xaml", ".py", ".js", ".ts", ".html", ".css", ".json", ".xml", ".rs", ".cpp", ".h" }
    };

    partial void OnShowDocumentsChanged(bool value) => OnFilterChanged();
    partial void OnShowImagesChanged(bool value) => OnFilterChanged();
    partial void OnShowVideosChanged(bool value) => OnFilterChanged();
    partial void OnShowAudioChanged(bool value) => OnFilterChanged();
    partial void OnShowArchivesChanged(bool value) => OnFilterChanged();
    partial void OnShowCodeChanged(bool value) => OnFilterChanged();
    partial void OnFilterTodayChanged(bool value) => OnFilterChanged();
    partial void OnFilterThisWeekChanged(bool value) => OnFilterChanged();
    partial void OnFilterThisMonthChanged(bool value) => OnFilterChanged();
    partial void OnFilterThisYearChanged(bool value) => OnFilterChanged();
    partial void OnFilterTinyChanged(bool value) => OnFilterChanged();
    partial void OnFilterSmallChanged(bool value) => OnFilterChanged();
    partial void OnFilterMediumChanged(bool value) => OnFilterChanged();
    partial void OnFilterLargeChanged(bool value) => OnFilterChanged();
    partial void OnFilterHugeChanged(bool value) => OnFilterChanged();

    private void OnFilterChanged()
    {
        UpdateFilterCount();
        FiltersChanged?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateFilterCount()
    {
        int count = 0;
        if (ShowDocuments) count++;
        if (ShowImages) count++;
        if (ShowVideos) count++;
        if (ShowAudio) count++;
        if (ShowArchives) count++;
        if (ShowCode) count++;
        if (FilterToday) count++;
        if (FilterThisWeek) count++;
        if (FilterThisMonth) count++;
        if (FilterThisYear) count++;
        if (FilterTiny) count++;
        if (FilterSmall) count++;
        if (FilterMedium) count++;
        if (FilterLarge) count++;
        if (FilterHuge) count++;

        ActiveFilterCount = count;
        HasActiveFilters = count > 0;
    }

    [RelayCommand]
    private void ClearAllFilters()
    {
        ShowDocuments = false;
        ShowImages = false;
        ShowVideos = false;
        ShowAudio = false;
        ShowArchives = false;
        ShowCode = false;
        FilterToday = false;
        FilterThisWeek = false;
        FilterThisMonth = false;
        FilterThisYear = false;
        FilterTiny = false;
        FilterSmall = false;
        FilterMedium = false;
        FilterLarge = false;
        FilterHuge = false;
    }

    public bool MatchesFilter(string fileName, long fileSize, DateTime modifiedDate)
    {
        if (!HasActiveFilters) return true;

        bool matchesType = MatchesTypeFilter(fileName);
        bool matchesDate = MatchesDateFilter(modifiedDate);
        bool matchesSize = MatchesSizeFilter(fileSize);

        bool anyTypeFilter = ShowDocuments || ShowImages || ShowVideos || ShowAudio || ShowArchives || ShowCode;
        bool anyDateFilter = FilterToday || FilterThisWeek || FilterThisMonth || FilterThisYear;
        bool anySizeFilter = FilterTiny || FilterSmall || FilterMedium || FilterLarge || FilterHuge;

        return (!anyTypeFilter || matchesType) && (!anyDateFilter || matchesDate) && (!anySizeFilter || matchesSize);
    }

    private bool MatchesTypeFilter(string fileName)
    {
        var ext = System.IO.Path.GetExtension(fileName).ToLowerInvariant();

        if (ShowDocuments && Array.Exists(TypeExtensions["Documents"], e => e == ext)) return true;
        if (ShowImages && Array.Exists(TypeExtensions["Images"], e => e == ext)) return true;
        if (ShowVideos && Array.Exists(TypeExtensions["Videos"], e => e == ext)) return true;
        if (ShowAudio && Array.Exists(TypeExtensions["Audio"], e => e == ext)) return true;
        if (ShowArchives && Array.Exists(TypeExtensions["Archives"], e => e == ext)) return true;
        if (ShowCode && Array.Exists(TypeExtensions["Code"], e => e == ext)) return true;

        return false;
    }

    private bool MatchesDateFilter(DateTime modifiedDate)
    {
        var now = DateTime.Now;

        if (FilterToday && modifiedDate.Date == now.Date) return true;
        if (FilterThisWeek && modifiedDate >= now.AddDays(-7)) return true;
        if (FilterThisMonth && modifiedDate >= now.AddMonths(-1)) return true;
        if (FilterThisYear && modifiedDate >= now.AddYears(-1)) return true;

        return false;
    }

    private bool MatchesSizeFilter(long fileSize)
    {
        const long KB = 1024;
        const long MB = 1024 * KB;
        const long GB = 1024 * MB;

        if (FilterTiny && fileSize < KB) return true;
        if (FilterSmall && fileSize >= KB && fileSize < MB) return true;
        if (FilterMedium && fileSize >= MB && fileSize < 100 * MB) return true;
        if (FilterLarge && fileSize >= 100 * MB && fileSize < GB) return true;
        if (FilterHuge && fileSize >= GB) return true;

        return false;
    }

    public IEnumerable<string> GetActiveTypeExtensions()
    {
        var extensions = new List<string>();

        if (ShowDocuments) extensions.AddRange(TypeExtensions["Documents"]);
        if (ShowImages) extensions.AddRange(TypeExtensions["Images"]);
        if (ShowVideos) extensions.AddRange(TypeExtensions["Videos"]);
        if (ShowAudio) extensions.AddRange(TypeExtensions["Audio"]);
        if (ShowArchives) extensions.AddRange(TypeExtensions["Archives"]);
        if (ShowCode) extensions.AddRange(TypeExtensions["Code"]);

        return extensions;
    }
}
