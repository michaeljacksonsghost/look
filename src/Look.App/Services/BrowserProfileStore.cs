using System.Text.Json;
using Look.Models;

namespace Look.Services;

public sealed class BrowserProfileStore
{
    private readonly string _filePath;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public BrowserProfileStore(string filePath) => _filePath = Path.GetFullPath(filePath);

    public async Task<IReadOnlyList<BrowserProfileInfo>> GetProfilesAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try { return (await LoadAsync(cancellationToken)).ToList(); }
        finally { _gate.Release(); }
    }

    public async Task<BrowserProfileInfo> CreateProfileAsync(string name, CancellationToken cancellationToken = default)
    {
        var normalizedName = name.Trim();
        if (normalizedName.Length is < 1 or > 32)
            throw new ArgumentException("Profile names must be between 1 and 32 characters.", nameof(name));

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var profiles = (await LoadAsync(cancellationToken)).ToList();
            if (profiles.Any(profile => string.Equals(profile.Name, normalizedName, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("A profile with that name already exists.");

            var profile = new BrowserProfileInfo(Guid.NewGuid().ToString("N"), normalizedName, DateTimeOffset.UtcNow);
            profiles.Add(profile);
            await SaveAsync(profiles, cancellationToken);
            return profile;
        }
        finally { _gate.Release(); }
    }

    private async Task<List<BrowserProfileInfo>> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_filePath)) return [];
        try
        {
            await using var stream = File.OpenRead(_filePath);
            return await JsonSerializer.DeserializeAsync<List<BrowserProfileInfo>>(stream, JsonOptions, cancellationToken) ?? [];
        }
        catch (JsonException)
        {
            var corruptPath = _filePath + ".corrupt-" + DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            File.Move(_filePath, corruptPath, overwrite: true);
            return [];
        }
    }

    private async Task SaveAsync(List<BrowserProfileInfo> profiles, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_filePath)!;
        Directory.CreateDirectory(directory);
        var temporaryPath = _filePath + ".tmp";
        await using (var stream = File.Create(temporaryPath))
            await JsonSerializer.SerializeAsync(stream, profiles, JsonOptions, cancellationToken);
        File.Move(temporaryPath, _filePath, overwrite: true);
    }
}
