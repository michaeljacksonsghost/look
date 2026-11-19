using Look.Models;
using Look.Services;
using Xunit;

namespace Look.Tests;

public sealed class BrowserDataStoreTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "look-tests", Guid.NewGuid().ToString("N"));
    private string FilePath => Path.Combine(_folder, "browser-data.json");

    [Fact]
    public async Task SessionAndActiveTabArePersisted()
    {
        var store = new BrowserDataStore(FilePath);
        await store.SaveSessionAsync(
            [new("https://example.org/", "Example"), new("https://openai.com/", "OpenAI")], 1);

        var loaded = await new BrowserDataStore(FilePath).LoadAsync();

        Assert.Equal(2, loaded.SessionTabs.Count);
        Assert.Equal(1, loaded.ActiveTabIndex);
        Assert.Equal("https://openai.com/", loaded.SessionTabs[1].Address);
    }

    [Fact]
    public async Task BookmarkCanBeAddedAndRemovedByToggling()
    {
        var store = new BrowserDataStore(FilePath);

        Assert.True(await store.ToggleBookmarkAsync("Example", "https://example.org/"));
        Assert.Single(await store.GetBookmarksAsync());

        Assert.False(await store.ToggleBookmarkAsync("Example", "https://example.org/"));
        Assert.Empty(await store.GetBookmarksAsync());
    }

    [Fact]
    public async Task HistoryDeduplicatesByAddressAndKeepsLatestVisit()
    {
        var store = new BrowserDataStore(FilePath);
        await store.AddHistoryAsync("First", "https://example.org/");
        await store.AddHistoryAsync("Second", "https://example.org/");

        var history = await store.GetHistoryAsync();

        var entry = Assert.Single(history);
        Assert.Equal("Second", entry.Title);
    }

    [Fact]
    public async Task RecentlyClosedTabsAndDownloadsArePersisted()
    {
        var store = new BrowserDataStore(FilePath);
        await store.AddRecentlyClosedAsync(new BrowserSessionTab("https://example.org/", "Example"));
        await store.AddDownloadAsync(new BrowserDownloadEntry(Guid.NewGuid(), "report.pdf",
            "https://example.org/report.pdf", Path.Combine(_folder, "report.pdf"), true, DateTimeOffset.UtcNow));

        var reopened = new BrowserDataStore(FilePath);
        Assert.Equal("https://example.org/", Assert.Single(await reopened.GetRecentlyClosedAsync()).Address);
        Assert.Equal("report.pdf", Assert.Single(await reopened.GetDownloadsAsync()).FileName);
    }

    [Fact]
    public async Task DomainBlockingSettingsNormalizeAndPersistHostnames()
    {
        var store = new BrowserDataStore(FilePath);
        await store.AddBlockedDomainAsync("https://ads.example.org/path");
        await store.AddAllowedDomainAsync("media.example.org");

        var settings = await new BrowserDataStore(FilePath).GetSettingsAsync();

        Assert.Contains("ads.example.org", settings.BlockedDomains);
        Assert.Contains("media.example.org", settings.AllowedDomains);
    }

    [Fact]
    public async Task InvalidDomainRulesAreRejected()
    {
        var store = new BrowserDataStore(FilePath);
        await Assert.ThrowsAsync<ArgumentException>(() => store.AddBlockedDomainAsync("javascript:alert(1)"));
    }

    [Fact]
    public async Task NonHttpAddressesAreNotStoredInHistoryOrBookmarks()
    {
        var store = new BrowserDataStore(FilePath);

        await store.AddHistoryAsync("Local file", "file:///C:/secret.txt");

        Assert.False(await store.ToggleBookmarkAsync("Local file", "file:///C:/secret.txt"));
        Assert.Empty(await store.GetHistoryAsync());
        Assert.Empty(await store.GetBookmarksAsync());
    }

    public void Dispose()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, recursive: true);
    }
}
