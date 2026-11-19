using System.IO;
using System.Text.Json;
using Look.Models;

namespace Look.Services;

/// <summary>
/// Local-only browser data store. Writes are atomic and the file is private to the current Windows user.
/// </summary>
public sealed class BrowserDataStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string _filePath;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private BrowserData? _cache;

    public BrowserDataStore(string? filePath = null)
    {
        _filePath = filePath ?? Path.Combine(
            Look.AppPaths.DataRoot, "browser-data.json");
    }

    public async Task<BrowserData> LoadAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_cache is not null) return Clone(_cache);
            if (!File.Exists(_filePath))
                _cache = new BrowserData();
            else
            {
                try
                {
                    await using var stream = File.OpenRead(_filePath);
                    _cache = await JsonSerializer.DeserializeAsync<BrowserData>(stream, JsonOptions, cancellationToken)
                        ?? new BrowserData();
                }
                catch (JsonException)
                {
                    var corruptPath = _filePath + ".corrupt-" + DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmss");
                    File.Move(_filePath, corruptPath, overwrite: true);
                    _cache = new BrowserData();
                }
            }

            return Clone(_cache);
        }
        finally { _gate.Release(); }
    }

    public async Task SaveSessionAsync(IReadOnlyCollection<BrowserSessionTab> tabs, int activeIndex,
        IReadOnlyCollection<BrowserSpace>? spaces = null, string? activeSpaceId = null,
        CancellationToken cancellationToken = default)
    {
        await MutateAsync(data =>
        {
            data.SessionTabs = tabs.ToList();
            data.ActiveTabIndex = tabs.Count == 0 ? 0 : Math.Clamp(activeIndex, 0, tabs.Count - 1);
            if (spaces is not null) data.Spaces = spaces.ToList();
            if (activeSpaceId is not null) data.ActiveSpaceId = activeSpaceId;
        }, cancellationToken);
    }

    public async Task AddRecentlyClosedAsync(BrowserSessionTab tab, CancellationToken cancellationToken = default) =>
        await MutateAsync(data =>
        {
            if (!string.IsNullOrWhiteSpace(tab.Address))
            {
                data.RecentlyClosedTabs.Insert(0, tab);
                data.RecentlyClosedTabs = data.RecentlyClosedTabs.Take(20).ToList();
            }
        }, cancellationToken);

    public async Task<IReadOnlyList<BrowserSessionTab>> GetRecentlyClosedAsync(CancellationToken cancellationToken = default) =>
        (await LoadAsync(cancellationToken)).RecentlyClosedTabs;

    public async Task RemoveRecentlyClosedAsync(string address, CancellationToken cancellationToken = default) =>
        await MutateAsync(data =>
        {
            var index = data.RecentlyClosedTabs.FindIndex(tab => tab.Address == address);
            if (index >= 0) data.RecentlyClosedTabs.RemoveAt(index);
        }, cancellationToken);

    public async Task AddHistoryAsync(string title, string address, CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(address, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            return;

        await MutateAsync(data =>
        {
            data.History.Insert(0, new BrowserHistoryEntry(Guid.NewGuid(),
                string.IsNullOrWhiteSpace(title) ? uri.Host : title, uri.AbsoluteUri, DateTimeOffset.UtcNow));
            data.History = data.History
                .GroupBy(item => item.Address, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .Take(1000)
                .ToList();
        }, cancellationToken);
    }

    public async Task RemoveBookmarkAsync(string address, CancellationToken cancellationToken = default) =>
        await MutateAsync(data => data.Bookmarks.RemoveAll(item =>
            string.Equals(item.Address, address, StringComparison.OrdinalIgnoreCase)), cancellationToken);

    public async Task RemoveHistoryAsync(string address, CancellationToken cancellationToken = default) =>
        await MutateAsync(data => data.History.RemoveAll(item =>
            string.Equals(item.Address, address, StringComparison.OrdinalIgnoreCase)), cancellationToken);

    public async Task RemoveDownloadAsync(Guid id, CancellationToken cancellationToken = default) =>
        await MutateAsync(data => data.Downloads.RemoveAll(item => item.Id == id), cancellationToken);

    public async Task<IReadOnlyList<BrowserBookmark>> GetBookmarksAsync(CancellationToken cancellationToken = default) =>
        (await LoadAsync(cancellationToken)).Bookmarks;

    public async Task<IReadOnlyList<BrowserHistoryEntry>> GetHistoryAsync(int limit = 100,
        CancellationToken cancellationToken = default) =>
        (await LoadAsync(cancellationToken)).History.Take(Math.Clamp(limit, 1, 1000)).ToList();

    public async Task<bool> ToggleBookmarkAsync(string title, string address,
        CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(address, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            return false;

        var added = false;
        await MutateAsync(data =>
        {
            var existing = data.Bookmarks.FirstOrDefault(item =>
                string.Equals(item.Address, uri.AbsoluteUri, StringComparison.OrdinalIgnoreCase));
            if (existing is not null)
                data.Bookmarks.Remove(existing);
            else
            {
                data.Bookmarks.Insert(0, new BrowserBookmark(Guid.NewGuid(),
                    string.IsNullOrWhiteSpace(title) ? uri.Host : title,
                    uri.AbsoluteUri, DateTimeOffset.UtcNow));
                added = true;
            }
        }, cancellationToken);
        return added;
    }

    public async Task AddDownloadAsync(BrowserDownloadEntry download, CancellationToken cancellationToken = default) =>
        await MutateAsync(data =>
        {
            data.Downloads.Insert(0, download);
            data.Downloads = data.Downloads.Take(200).ToList();
        }, cancellationToken);

    public async Task<IReadOnlyList<BrowserDownloadEntry>> GetDownloadsAsync(int limit = 100,
        CancellationToken cancellationToken = default) =>
        (await LoadAsync(cancellationToken)).Downloads.Take(Math.Clamp(limit, 1, 200)).ToList();

    public async Task<BrowserSettings> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        var settings = (await LoadAsync(cancellationToken)).Settings;
        return new BrowserSettings
        {
            ContentBlockingEnabled = settings.ContentBlockingEnabled,
            BlockedDomains = settings.BlockedDomains.ToList(),
            AllowedDomains = settings.AllowedDomains.ToList(),
            CustomCssByDomain = new Dictionary<string, string>(settings.CustomCssByDomain, StringComparer.OrdinalIgnoreCase)
        };
    }

    public async Task SaveSettingsAsync(BrowserSettings settings, CancellationToken cancellationToken = default) =>
        await MutateAsync(data => data.Settings = new BrowserSettings
        {
            ContentBlockingEnabled = settings.ContentBlockingEnabled,
            BlockedDomains = settings.BlockedDomains.Select(NormalizeDomain).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            AllowedDomains = settings.AllowedDomains.Select(NormalizeDomain).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            CustomCssByDomain = settings.CustomCssByDomain.ToDictionary(pair => NormalizeDomain(pair.Key), pair => pair.Value, StringComparer.OrdinalIgnoreCase)
        }, cancellationToken);

    public async Task AddBlockedDomainAsync(string domain, CancellationToken cancellationToken = default) =>
        await MutateAsync(data =>
        {
            var normalized = NormalizeDomain(domain);
            data.Settings.BlockedDomains = data.Settings.BlockedDomains
                .Append(normalized).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }, cancellationToken);

    public async Task RemoveBlockedDomainAsync(string domain, CancellationToken cancellationToken = default) =>
        await MutateAsync(data =>
        {
            var normalized = NormalizeDomain(domain);
            data.Settings.BlockedDomains.RemoveAll(item => string.Equals(item, normalized, StringComparison.OrdinalIgnoreCase));
        }, cancellationToken);

    public async Task AddAllowedDomainAsync(string domain, CancellationToken cancellationToken = default) =>
        await MutateAsync(data =>
        {
            var normalized = NormalizeDomain(domain);
            data.Settings.AllowedDomains = data.Settings.AllowedDomains
                .Append(normalized).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }, cancellationToken);

    public async Task RemoveAllowedDomainAsync(string domain, CancellationToken cancellationToken = default) =>
        await MutateAsync(data =>
        {
            var normalized = NormalizeDomain(domain);
            data.Settings.AllowedDomains.RemoveAll(item => string.Equals(item, normalized, StringComparison.OrdinalIgnoreCase));
        }, cancellationToken);

    private static string NormalizeDomain(string value)
    {
        var input = value.Trim();
        if (!input.Contains("://", StringComparison.Ordinal)) input = "https://" + input;
        if (!Uri.TryCreate(input, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
            Uri.CheckHostName(uri.Host) == UriHostNameType.Unknown)
            throw new ArgumentException("Enter a valid website domain.", nameof(value));
        return uri.Host.TrimEnd('.').ToLowerInvariant();
    }

    public async Task<string> GetSiteCssAsync(string domain, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeDomain(domain);
        var settings = await GetSettingsAsync(cancellationToken);
        return settings.CustomCssByDomain.TryGetValue(normalized, out var css) ? css : string.Empty;
    }

    public async Task SaveSiteCssAsync(string domain, string css, CancellationToken cancellationToken = default) =>
        await MutateAsync(data =>
        {
            var normalized = NormalizeDomain(domain);
            if (string.IsNullOrWhiteSpace(css)) data.Settings.CustomCssByDomain.Remove(normalized);
            else data.Settings.CustomCssByDomain[normalized] = css;
        }, cancellationToken);

    public async Task ClearHistoryAsync(CancellationToken cancellationToken = default) =>
        await MutateAsync(data => data.History.Clear(), cancellationToken);

    private async Task MutateAsync(Action<BrowserData> mutation, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            _cache ??= await ReadFromDiskAsync(cancellationToken);
            mutation(_cache);
            Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
            var temporaryPath = _filePath + ".tmp";
            await using (var stream = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write,
                             FileShare.None, 4096, FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(stream, _cache, JsonOptions, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }
            File.Move(temporaryPath, _filePath, overwrite: true);
        }
        finally { _gate.Release(); }
    }

    private async Task<BrowserData> ReadFromDiskAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_filePath)) return new BrowserData();
        await using var stream = File.OpenRead(_filePath);
        return await JsonSerializer.DeserializeAsync<BrowserData>(stream, JsonOptions, cancellationToken)
               ?? new BrowserData();
    }

    private static BrowserData Clone(BrowserData data) => new()
    {
        SchemaVersion = data.SchemaVersion,
        SessionTabs = data.SessionTabs.ToList(),
        Spaces = data.Spaces.ToList(),
        ActiveSpaceId = data.ActiveSpaceId,
        RecentlyClosedTabs = data.RecentlyClosedTabs.ToList(),
        ActiveTabIndex = data.ActiveTabIndex,
        Bookmarks = data.Bookmarks.ToList(),
        History = data.History.ToList(),
        Downloads = data.Downloads.ToList(),
        Settings = new BrowserSettings
        {
            ContentBlockingEnabled = data.Settings.ContentBlockingEnabled,
            BlockedDomains = data.Settings.BlockedDomains.ToList(),
            AllowedDomains = data.Settings.AllowedDomains.ToList(),
            CustomCssByDomain = new Dictionary<string, string>(data.Settings.CustomCssByDomain, StringComparer.OrdinalIgnoreCase)
        }
    };
}
