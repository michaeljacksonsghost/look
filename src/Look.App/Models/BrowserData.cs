namespace Look.Models;

public sealed record BrowserSessionTab(string Address, string Title, bool IsPinned = false, string SpaceId = "default");

public sealed record BrowserBookmark(Guid Id, string Title, string Address, DateTimeOffset CreatedAt);

public sealed record BrowserHistoryEntry(Guid Id, string Title, string Address, DateTimeOffset VisitedAt);

public sealed class BrowserData
{
    public int SchemaVersion { get; set; } = 1;
    public List<BrowserSessionTab> SessionTabs { get; set; } = [];
    public List<BrowserSpace> Spaces { get; set; } = [new BrowserSpace("default", "Default", DateTimeOffset.UtcNow)];
    public string ActiveSpaceId { get; set; } = "default";
    public List<BrowserSessionTab> RecentlyClosedTabs { get; set; } = [];
    public int ActiveTabIndex { get; set; }
    public List<BrowserBookmark> Bookmarks { get; set; } = [];
    public List<BrowserHistoryEntry> History { get; set; } = [];
    public List<BrowserDownloadEntry> Downloads { get; set; } = [];
    public BrowserSettings Settings { get; set; } = new();
}
