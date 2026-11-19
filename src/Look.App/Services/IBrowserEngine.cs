namespace Look.Services;
/// <summary>Engine boundary. UI and browser state must not depend directly on a vendor engine.</summary>
public interface IBrowserEngine : IAsyncDisposable
{
 Task InitializeAsync(string userDataFolder, CancellationToken cancellationToken = default);
 Task NavigateAsync(Uri uri, CancellationToken cancellationToken = default);
 Task GoBackAsync(); Task GoForwardAsync(); Task ReloadAsync(bool bypassCache = false);
 bool CanGoBack { get; } bool CanGoForward { get; }
 event EventHandler<string>? TitleChanged; event EventHandler<Uri>? AddressChanged; event EventHandler<bool>? LoadingChanged;
}
