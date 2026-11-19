using System.IO;
using Look.Services;
using Xunit;

namespace Look.Tests;

public sealed class BrowserProfileStoreTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "look-profile-tests", Guid.NewGuid().ToString("N"));
    private string FilePath => Path.Combine(_folder, "profiles.json");

    [Fact]
    public async Task ProfilesAreCreatedAndPersisted()
    {
        var store = new BrowserProfileStore(FilePath);
        var profile = await store.CreateProfileAsync("Research");
        var reopened = await new BrowserProfileStore(FilePath).GetProfilesAsync();

        Assert.Equal("Research", Assert.Single(reopened).Name);
        Assert.Equal(profile.Id, reopened[0].Id);
    }

    [Fact]
    public async Task DuplicateProfileNamesAreRejectedIgnoringCase()
    {
        var store = new BrowserProfileStore(FilePath);
        await store.CreateProfileAsync("Personal");
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.CreateProfileAsync("personal"));
    }

    [Fact]
    public async Task ProfilesCanBeRenamedAndDeleted()
    {
        var store = new BrowserProfileStore(FilePath);
        var profile = await store.CreateProfileAsync("Research");
        var renamed = await store.RenameProfileAsync(profile.Id, "Work");
        Assert.Equal("Work", renamed.Name);
        Assert.True(await store.DeleteProfileAsync(profile.Id));
        Assert.Empty(await store.GetProfilesAsync());
    }

    [Fact]
    public async Task InvalidProfileNamesAreRejected()
    {
        var store = new BrowserProfileStore(FilePath);
        await Assert.ThrowsAsync<ArgumentException>(() => store.CreateProfileAsync(" "));
        await Assert.ThrowsAsync<ArgumentException>(() => store.CreateProfileAsync(new string('x', 33)));
    }

    public void Dispose()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, recursive: true);
    }
}
