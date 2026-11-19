using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using System.Windows;

namespace Look.Services;

/// <summary>
/// WebView2 implementation of the browser-engine boundary. One instance is created per tab;
/// all instances share the same persistent browser profile/environment.
/// </summary>
public sealed class WebView2BrowserEngine : IBrowserEngine
{
    private static readonly SemaphoreSlim EnvironmentGate = new(1, 1);
    private static CoreWebView2Environment? SharedEnvironment;
    private static string? SharedUserDataFolder;

    private readonly WebView2 _view = new();
    private bool _initialized;

    public FrameworkElement View => _view;
    public bool CanGoBack => _view.CoreWebView2?.CanGoBack ?? false;
    public bool CanGoForward => _view.CoreWebView2?.CanGoForward ?? false;

    public event EventHandler<string>? TitleChanged;
    public event EventHandler<Uri>? AddressChanged;
    public event EventHandler<bool>? LoadingChanged;

    public async Task InitializeAsync(string userDataFolder, CancellationToken cancellationToken = default)
    {
        if (_initialized) return;

        await EnvironmentGate.WaitAsync(cancellationToken);
        try
        {
            if (SharedEnvironment is null)
            {
                SharedUserDataFolder = Path.GetFullPath(userDataFolder);
                Directory.CreateDirectory(SharedUserDataFolder);
                SharedEnvironment = await CoreWebView2Environment.CreateAsync(
                    userDataFolder: SharedUserDataFolder);
            }
            else if (!string.Equals(SharedUserDataFolder, Path.GetFullPath(userDataFolder),
                         StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("All tabs in a browser window must use the same WebView2 profile.");
            }
        }
        finally
        {
            EnvironmentGate.Release();
        }

        var environment = SharedEnvironment
            ?? throw new InvalidOperationException("The WebView2 environment could not be created.");
        await _view.EnsureCoreWebView2Async(environment);
        var core = _view.CoreWebView2;
        core.Settings.AreDefaultContextMenusEnabled = true;
        core.Settings.IsStatusBarEnabled = false;
        core.Settings.AreDevToolsEnabled = false;
        core.Settings.IsZoomControlEnabled = true;

        core.DocumentTitleChanged += OnDocumentTitleChanged;
        core.SourceChanged += OnSourceChanged;
        core.NavigationStarting += OnNavigationStarting;
        core.NavigationCompleted += OnNavigationCompleted;
        _initialized = true;
    }

    public Task NavigateAsync(Uri uri, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureInitialized();
        _view.CoreWebView2.Navigate(uri.AbsoluteUri);
        return Task.CompletedTask;
    }

    public Task GoBackAsync()
    {
        if (CanGoBack) _view.CoreWebView2!.GoBack();
        return Task.CompletedTask;
    }

    public Task GoForwardAsync()
    {
        if (CanGoForward) _view.CoreWebView2!.GoForward();
        return Task.CompletedTask;
    }

    public async Task ReloadAsync(bool bypassCache = false)
    {
        if (!_initialized) return;
        if (bypassCache)
            await _view.CoreWebView2.ExecuteScriptAsync("location.reload(true)");
        else
            _view.CoreWebView2.Reload();
    }

    private void EnsureInitialized()
    {
        if (!_initialized || _view.CoreWebView2 is null)
            throw new InvalidOperationException("The browser tab has not been initialized.");
    }

    private void OnDocumentTitleChanged(object? sender, object e) =>
        TitleChanged?.Invoke(this, _view.CoreWebView2?.DocumentTitle ?? "New tab");

    private void OnSourceChanged(object? sender, CoreWebView2SourceChangedEventArgs e)
    {
        if (Uri.TryCreate(_view.Source?.ToString(), UriKind.Absolute, out var uri))
            AddressChanged?.Invoke(this, uri);
    }

    private void OnNavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e) =>
        LoadingChanged?.Invoke(this, true);

    private void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e) =>
        LoadingChanged?.Invoke(this, false);

    public ValueTask DisposeAsync()
    {
        if (_view.CoreWebView2 is { } core)
        {
            core.DocumentTitleChanged -= OnDocumentTitleChanged;
            core.SourceChanged -= OnSourceChanged;
            core.NavigationStarting -= OnNavigationStarting;
            core.NavigationCompleted -= OnNavigationCompleted;
            core.Stop();
        }

        TitleChanged = null;
        AddressChanged = null;
        LoadingChanged = null;
        return ValueTask.CompletedTask;
    }
}
