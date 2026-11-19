using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using Look.Models;
using Look.Services;

namespace Look.Views;

public partial class BrowserLibraryWindow : Window
{
    private enum EntryKind { Bookmark, History, Download }

    private sealed record LibraryItem(Guid Id, EntryKind Kind, string Title, string Address, string Metadata);

    private readonly BrowserDataStore _store;
    private readonly Action<string> _openAddress;
    private readonly List<LibraryItem> _allBookmarks = [];
    private readonly List<LibraryItem> _allHistory = [];
    private readonly List<LibraryItem> _allDownloads = [];
    private readonly ObservableCollection<LibraryItem> _visibleBookmarks = [];
    private readonly ObservableCollection<LibraryItem> _visibleHistory = [];
    private readonly ObservableCollection<LibraryItem> _visibleDownloads = [];

    public BrowserLibraryWindow(BrowserDataStore store, Action<string> openAddress)
    {
        _store = store;
        _openAddress = openAddress;
        InitializeComponent();
        BookmarksList.ItemsSource = _visibleBookmarks;
        HistoryList.ItemsSource = _visibleHistory;
        DownloadsList.ItemsSource = _visibleDownloads;
        Loaded += async (_, _) => await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        _allBookmarks.Clear();
        foreach (var item in await _store.GetBookmarksAsync())
            _allBookmarks.Add(new LibraryItem(item.Id, EntryKind.Bookmark, item.Title, item.Address,
                $"Saved {item.CreatedAt.ToLocalTime():g}"));

        _allHistory.Clear();
        foreach (var item in await _store.GetHistoryAsync(1000))
            _allHistory.Add(new LibraryItem(item.Id, EntryKind.History, item.Title, item.Address,
                $"Visited {item.VisitedAt.ToLocalTime():g}"));

        _allDownloads.Clear();
        foreach (var item in await _store.GetDownloadsAsync(200))
            _allDownloads.Add(new LibraryItem(item.Id, EntryKind.Download, item.FileName,
                item.SourceUri, $"{(item.IsComplete ? "Completed" : "Interrupted")} · {item.DestinationPath}"));

        ApplySearch();
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => ApplySearch();

    private void ApplySearch()
    {
        var query = SearchBox.Text.Trim();
        static bool Matches(LibraryItem item, string search) =>
            string.IsNullOrEmpty(search) ||
            item.Title.Contains(search, StringComparison.OrdinalIgnoreCase) ||
            item.Address.Contains(search, StringComparison.OrdinalIgnoreCase) ||
            item.Metadata.Contains(search, StringComparison.OrdinalIgnoreCase);

        ReplaceItems(_visibleBookmarks, _allBookmarks.Where(item => Matches(item, query)));
        ReplaceItems(_visibleHistory, _allHistory.Where(item => Matches(item, query)));
        ReplaceItems(_visibleDownloads, _allDownloads.Where(item => Matches(item, query)));
    }

    private static void ReplaceItems(ObservableCollection<LibraryItem> target, IEnumerable<LibraryItem> items)
    {
        target.Clear();
        foreach (var item in items) target.Add(item);
    }

    private void OpenEntry_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not LibraryItem item) return;
        if (item.Kind == EntryKind.Download)
        {
            if (File.Exists(item.Metadata.Split(" · ", 2).Last()))
            {
                var path = item.Metadata.Split(" · ", 2).Last();
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
            }
            else
                MessageBox.Show("The downloaded file is no longer available.", "Downloads",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        _openAddress(item.Address);
        Activate();
    }

    private async void RemoveEntry_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not LibraryItem item) return;
        switch (item.Kind)
        {
            case EntryKind.Bookmark:
                await _store.RemoveBookmarkAsync(item.Address);
                break;
            case EntryKind.History:
                await _store.RemoveHistoryAsync(item.Address);
                break;
            case EntryKind.Download:
                await _store.RemoveDownloadAsync(item.Id);
                break;
        }
        await RefreshAsync();
    }
}
