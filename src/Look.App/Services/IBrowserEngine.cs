using System.Windows;

namespace Look.Services;

/// <summary>Engine boundary. UI and browser state must not depend directly on a vendor engine.</summary>
public interface IBrowserEngine : IAsyncDisposable
{
    FrameworkElement View { get; }
    Task InitializeAsync(string userDataFolder, CancellationToken cancellationToken = default);
    Task NavigateAsync(Uri uri, CancellationToken cancellationToken = default);
    Task GoBackAsync();
    Task GoForwardAsync();
    Task ReloadAsync(bool bypassCache = false);
    Task StopAsync();
    Task FindInPageAsync(string query, bool backwards = false);
    bool IsMuted { get; set; }
    bool CanGoBack { get; }
    bool CanGoForward { get; }
    event EventHandler<string>? TitleChanged;
    event EventHandler<Uri>? AddressChanged;
    event EventHandler<bool>? LoadingChanged;
    event EventHandler<bool>? NavigationFinished;
}
