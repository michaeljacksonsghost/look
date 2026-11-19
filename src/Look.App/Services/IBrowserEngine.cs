using Look.Models;
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
    Task<bool> TrySuspendAsync();
    Task ResumeAsync();
    Task FindInPageAsync(string query, bool backwards = false);
    Task ConfigureContentBlockingAsync(BrowserSettings settings);
    Task<IReadOnlyList<BrowserExtensionInfo>> GetExtensionsAsync();
    Task<BrowserExtensionInfo> InstallExtensionAsync(string folderPath);
    Task SetExtensionEnabledAsync(string extensionId, bool enabled);
    Task RemoveExtensionAsync(string extensionId);
    bool IsMuted { get; set; }
    bool CanGoBack { get; }
    bool CanGoForward { get; }
    bool IsReaderMode { get; }
    Task SetReaderModeAsync(bool enabled);
    Task SetCustomCssAsync(string domain, string css);
    Task StartElementPickerAsync();
    event EventHandler<string>? TitleChanged;
    event EventHandler<Uri>? AddressChanged;
    event EventHandler<bool>? LoadingChanged;
    event EventHandler<bool>? NavigationFinished;
    event EventHandler<BrowserDownloadRequest>? DownloadRequested;
    event EventHandler<BrowserPermissionRequest>? PermissionRequested;
    event EventHandler<string>? ElementPicked;
}
