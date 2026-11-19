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
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "look", "browser-data.json");
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
        CancellationToken cancellationToken = default)
    {
        await MutateAsync(data =>
        {
            data.SessionTabs = tabs.ToList();
            data.ActiveTabIndex = tabs.Count == 0 ? 0 : Math.Clamp(activeIndex, 0, tabs.Count - 1);
        }, cancellationToken);
    }

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
        ActiveTabIndex = data.ActiveTabIndex,
        Bookmarks = data.Bookmarks.ToList(),
        History = data.History.ToList()
    };
}
