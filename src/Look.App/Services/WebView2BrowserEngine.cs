using Look.Models;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using System.Windows;
using System.Text.Json;
using System.Collections.Concurrent;

namespace Look.Services;

/// <summary>
/// WebView2 implementation of the browser-engine boundary. One instance is created per tab;
/// all instances share the same persistent browser profile/environment.
/// </summary>
public sealed class WebView2BrowserEngine : IBrowserEngine
{
    private sealed class EnvironmentEntry(string userDataFolder)
    {
        public Lazy<Task<CoreWebView2Environment>> Environment { get; } = new(() =>
            CoreWebView2Environment.CreateAsync(userDataFolder: userDataFolder,
                options: new CoreWebView2EnvironmentOptions { AreBrowserExtensionsEnabled = true }));
        public int ReferenceCount { get; set; }
    }

    private static readonly object EnvironmentSync = new();
    private static readonly Dictionary<string, EnvironmentEntry> Environments =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly WebView2 _view = new();
    private string? _environmentKey;
    private EnvironmentEntry? _environmentEntry;
    private bool _initialized;

    public FrameworkElement View => _view;
    public bool IsMuted
    {
        get => _view.CoreWebView2?.IsMuted ?? false;
        set { if (_view.CoreWebView2 is not null) _view.CoreWebView2.IsMuted = value; }
    }

    public bool CanGoBack => _view.CoreWebView2?.CanGoBack ?? false;
    public bool CanGoForward => _view.CoreWebView2?.CanGoForward ?? false;

    public event EventHandler<string>? TitleChanged;
    public event EventHandler<Uri>? AddressChanged;
    public event EventHandler<bool>? LoadingChanged;
    public event EventHandler<bool>? NavigationFinished;
    public event EventHandler<BrowserDownloadRequest>? DownloadRequested;

    public async Task InitializeAsync(string userDataFolder, CancellationToken cancellationToken = default)
    {
        if (_initialized) return;

        var fullPath = Path.GetFullPath(userDataFolder);
        Directory.CreateDirectory(fullPath);
        EnvironmentEntry entry;
        lock (EnvironmentSync)
        {
            if (!Environments.TryGetValue(fullPath, out entry!))
            {
                entry = new EnvironmentEntry(fullPath);
                Environments.Add(fullPath, entry);
            }
            entry.ReferenceCount++;
        }

        try
        {
            var environment = await entry.Environment.Value.WaitAsync(cancellationToken);
            await _view.EnsureCoreWebView2Async(environment);
            _environmentKey = fullPath;
            _environmentEntry = entry;
        }
        catch
        {
            ReleaseEnvironment(fullPath, entry);
            throw;
        }

        var core = _view.CoreWebView2;
        core.Settings.AreDefaultContextMenusEnabled = true;
        core.Settings.IsStatusBarEnabled = false;
        core.Settings.AreDevToolsEnabled = false;
        core.Settings.IsZoomControlEnabled = true;

        core.DocumentTitleChanged += OnDocumentTitleChanged;
        core.SourceChanged += OnSourceChanged;
        core.NavigationStarting += OnNavigationStarting;
        core.NavigationCompleted += OnNavigationCompleted;
        core.DownloadStarting += OnDownloadStarting;
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

    public Task StopAsync()
    {
        if (_initialized) _view.CoreWebView2.Stop();
        return Task.CompletedTask;
    }

    public async Task FindInPageAsync(string query, bool backwards = false)
    {
        if (!_initialized || string.IsNullOrWhiteSpace(query)) return;
        var encodedQuery = JsonSerializer.Serialize(query);
        var backwardsLiteral = backwards ? "true" : "false";
        await _view.CoreWebView2.ExecuteScriptAsync($"window.find({encodedQuery}, false, {backwardsLiteral}, true, false, true, false);");
    }

    public async Task<IReadOnlyList<BrowserExtensionInfo>> GetExtensionsAsync()
    {
        EnsureInitialized();
        var extensions = await _view.CoreWebView2.Profile.GetBrowserExtensionsAsync();
        return extensions.Select(extension => new BrowserExtensionInfo(
            extension.Id, extension.Name, extension.IsEnabled)).ToList();
    }

    public async Task<BrowserExtensionInfo> InstallExtensionAsync(string folderPath)
    {
        EnsureInitialized();
        var extension = await _view.CoreWebView2.Profile.AddBrowserExtensionAsync(folderPath);
        return new BrowserExtensionInfo(extension.Id, extension.Name, extension.IsEnabled);
    }

    public async Task SetExtensionEnabledAsync(string extensionId, bool enabled)
    {
        EnsureInitialized();
        var extensions = await _view.CoreWebView2.Profile.GetBrowserExtensionsAsync();
        var extension = extensions.FirstOrDefault(item => item.Id == extensionId)
            ?? throw new InvalidOperationException("The selected extension is no longer installed.");
        await extension.EnableAsync(enabled);
    }

    public async Task RemoveExtensionAsync(string extensionId)
    {
        EnsureInitialized();
        var extensions = await _view.CoreWebView2.Profile.GetBrowserExtensionsAsync();
        var extension = extensions.FirstOrDefault(item => item.Id == extensionId)
            ?? throw new InvalidOperationException("The selected extension is no longer installed.");
        await extension.RemoveAsync();
    }

    private static void ReleaseEnvironment(string key, EnvironmentEntry entry)
    {
        lock (EnvironmentSync)
        {
            entry.ReferenceCount--;
            if (entry.ReferenceCount <= 0 && Environments.TryGetValue(key, out var current) &&
                ReferenceEquals(current, entry))
                Environments.Remove(key);
        }
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

    private void OnDownloadStarting(object? sender, CoreWebView2DownloadStartingEventArgs e)
    {
        var operation = e.DownloadOperation;
        var suggestedName = Path.GetFileName(e.ResultFilePath);
        e.Handled = true;
        if (DownloadRequested is null)
        {
            e.Cancel = true;
            return;
        }
        var request = new BrowserDownloadRequest(operation.Uri, suggestedName, destination =>
        {
            if (string.IsNullOrWhiteSpace(destination)) e.Cancel = true;
            else e.ResultFilePath = destination;
        });
        void OnStateChanged(object? stateSender, object stateArgs)
        {
            if (operation.State == CoreWebView2DownloadState.Completed ||
                operation.State == CoreWebView2DownloadState.Interrupted)
            {
                request.ReportFinished(operation.State == CoreWebView2DownloadState.Completed);
                operation.StateChanged -= OnStateChanged;
            }
        }
        operation.StateChanged += OnStateChanged;
        DownloadRequested.Invoke(this, request);
    }

    private void OnNavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e) =>
        LoadingChanged?.Invoke(this, true);

    private void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        LoadingChanged?.Invoke(this, false);
        NavigationFinished?.Invoke(this, e.IsSuccess);
    }

    public ValueTask DisposeAsync()
    {
        if (_view.CoreWebView2 is { } core)
        {
            core.DocumentTitleChanged -= OnDocumentTitleChanged;
            core.SourceChanged -= OnSourceChanged;
            core.NavigationStarting -= OnNavigationStarting;
            core.NavigationCompleted -= OnNavigationCompleted;
            core.DownloadStarting -= OnDownloadStarting;
            core.Stop();
        }

        TitleChanged = null;
        AddressChanged = null;
        LoadingChanged = null;
        NavigationFinished = null;
        DownloadRequested = null;
        ((IDisposable)_view).Dispose();
        if (_environmentKey is not null && _environmentEntry is not null)
            ReleaseEnvironment(_environmentKey, _environmentEntry);
        _environmentKey = null;
        _environmentEntry = null;
        return ValueTask.CompletedTask;
    }
}
