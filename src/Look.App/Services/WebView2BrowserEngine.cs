using Look.Models;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using System.Windows;
using System.Text.Json;
using System.Net;
using System.Text;
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
    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);
    private CoreWebView2Environment? _environment;
    private HashSet<string> _blockedDomains = new(StringComparer.OrdinalIgnoreCase);
    private HashSet<string> _allowedDomains = new(StringComparer.OrdinalIgnoreCase);
    private bool _contentBlockingEnabled;
    private bool _resourceFilterAdded;
    private string? _environmentKey;
    private EnvironmentEntry? _environmentEntry;
    private bool _initialized;
    private bool _isSuspended;
    private string? _readerSourceUri;
    private string? _customCssScriptId;
    private string? _customCssDomain;

    public FrameworkElement View => _view;
    public bool IsMuted
    {
        get => _view.CoreWebView2?.IsMuted ?? false;
        set { if (_view.CoreWebView2 is not null) _view.CoreWebView2.IsMuted = value; }
    }

    public bool CanGoBack => _view.CoreWebView2?.CanGoBack ?? false;
    public bool CanGoForward => _view.CoreWebView2?.CanGoForward ?? false;
    public bool IsReaderMode => _readerSourceUri is not null;

    public event EventHandler<string>? TitleChanged;
    public event EventHandler<Uri>? AddressChanged;
    public event EventHandler<bool>? LoadingChanged;
    public event EventHandler<bool>? NavigationFinished;
    public event EventHandler<BrowserDownloadRequest>? DownloadRequested;
    public event EventHandler<BrowserPermissionRequest>? PermissionRequested;

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
            _environment = environment;
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
        core.PermissionRequested += OnPermissionRequested;
        _initialized = true;
    }

    public Task NavigateAsync(Uri uri, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureInitialized();
        _readerSourceUri = null;
        _view.CoreWebView2.Navigate(uri.AbsoluteUri);
        return Task.CompletedTask;
    }

    public async Task GoBackAsync()
    {
        if (IsReaderMode) await SetReaderModeAsync(false);
        else if (CanGoBack) _view.CoreWebView2!.GoBack();
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

    public async Task<bool> TrySuspendAsync()
    {
        await _lifecycleGate.WaitAsync();
        try
        {
            if (!_initialized || _isSuspended) return _isSuspended;
            try { _isSuspended = await _view.CoreWebView2.TrySuspendAsync(); }
            catch (InvalidOperationException) { _isSuspended = false; }
            return _isSuspended;
        }
        finally { _lifecycleGate.Release(); }
    }

    public async Task ResumeAsync()
    {
        await _lifecycleGate.WaitAsync();
        try
        {
            if (_initialized && _isSuspended)
            {
                _view.CoreWebView2.Resume();
                _isSuspended = false;
            }
        }
        finally { _lifecycleGate.Release(); }
    }

    public Task StopAsync()
    {
        if (_initialized) _view.CoreWebView2.Stop();
        return Task.CompletedTask;
    }

    public async Task FindInPageAsync(string query, bool backwards = false)
    {
        if (!_initialized) return;
        if (string.IsNullOrWhiteSpace(query))
        {
            await _view.CoreWebView2.ExecuteScriptAsync("window.getSelection()?.removeAllRanges();");
            return;
        }
        var encodedQuery = JsonSerializer.Serialize(query);
        var backwardsLiteral = backwards ? "true" : "false";
        await _view.CoreWebView2.ExecuteScriptAsync($"window.find({encodedQuery}, false, {backwardsLiteral}, true, false, true, false);");
    }

    private sealed record ReaderPayload(string Title, string Text);

    public async Task SetReaderModeAsync(bool enabled)
    {
        EnsureInitialized();
        if (enabled == IsReaderMode) return;

        if (!enabled)
        {
            var source = _readerSourceUri;
            _readerSourceUri = null;
            if (Uri.TryCreate(source, UriKind.Absolute, out var original))
                _view.CoreWebView2.Navigate(original.AbsoluteUri);
            return;
        }

        var sourceUri = _view.Source?.AbsoluteUri;
        if (!Uri.TryCreate(sourceUri, UriKind.Absolute, out var current) ||
            (current.Scheme != Uri.UriSchemeHttp && current.Scheme != Uri.UriSchemeHttps))
            throw new InvalidOperationException("Open a normal website before entering reader mode.");

        const string extractionScript = @"(() => {
            const root = document.querySelector('article') || document.querySelector('main') || document.body;
            const title = document.querySelector('h1')?.innerText || document.title || location.hostname;
            return JSON.stringify({ title, text: root?.innerText || '' });
        })()";
        var scriptResult = await _view.CoreWebView2.ExecuteScriptAsync(extractionScript);
        var payloadJson = JsonSerializer.Deserialize<string>(scriptResult);
        var payload = payloadJson is null ? null :
            JsonSerializer.Deserialize<ReaderPayload>(payloadJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (payload is null || string.IsNullOrWhiteSpace(payload.Text))
            throw new InvalidOperationException("look couldn't find readable text on this page.");

        var html = new StringBuilder();
        html.Append("<!doctype html><html><head><meta charset='utf-8'><meta name='viewport' content='width=device-width, initial-scale=1'>");
        html.Append("<title>").Append(WebUtility.HtmlEncode(payload.Title)).Append("</title>");
        html.Append("<style>html{background:#f4f1e9;color:#292820}body{margin:0;padding:56px 24px 96px;font-family:Georgia,serif;font-size:19px;line-height:1.8}main{max-width:740px;margin:0 auto}h1{font-size:2.2rem;line-height:1.2;font-weight:500;margin:0 0 2.2rem}p{margin:0 0 1.25rem;white-space:pre-wrap}::selection{background:#d8d3c6}</style>");
        html.Append("</head><body><main><h1>").Append(WebUtility.HtmlEncode(payload.Title)).Append("</h1>");
        foreach (var paragraph in payload.Text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = paragraph.Trim();
            if (trimmed.Length > 0)
                html.Append("<p>").Append(WebUtility.HtmlEncode(trimmed)).Append("</p>");
        }
        html.Append("</main></body></html>");

        _readerSourceUri = current.AbsoluteUri;
        _view.CoreWebView2.NavigateToString(html.ToString());
    }

    public async Task SetCustomCssAsync(string domain, string css)
    {
        EnsureInitialized();
        var core = _view.CoreWebView2;
        if (_customCssScriptId is not null)
        {
            await core.RemoveScriptToExecuteOnDocumentCreatedAsync(_customCssScriptId);
            _customCssScriptId = null;
        }
        _customCssDomain = null;
        await core.ExecuteScriptAsync("document.getElementById('look-custom-css')?.remove();");
        if (string.IsNullOrWhiteSpace(css)) return;

        var normalizedDomain = domain.Trim().TrimStart('.').ToLowerInvariant();
        var domainJson = JsonSerializer.Serialize(normalizedDomain);
        var cssJson = JsonSerializer.Serialize(css);
        var script = $"(() => {{ const d = {domainJson}; const h = location.hostname.toLowerCase(); if (h !== d && !h.endsWith('.' + d)) return; let s = document.getElementById('look-custom-css'); if (!s) {{ s = document.createElement('style'); s.id = 'look-custom-css'; (document.head || document.documentElement).appendChild(s); }} s.textContent = {cssJson}; }})();";

        _customCssScriptId = await core.AddScriptToExecuteOnDocumentCreatedAsync(script);
        _customCssDomain = normalizedDomain;
        await core.ExecuteScriptAsync(script);
    }

    public Task ConfigureContentBlockingAsync(BrowserSettings settings)
    {
        EnsureInitialized();
        _blockedDomains = new HashSet<string>(settings.BlockedDomains, StringComparer.OrdinalIgnoreCase);
        _allowedDomains = new HashSet<string>(settings.AllowedDomains, StringComparer.OrdinalIgnoreCase);
        _contentBlockingEnabled = settings.ContentBlockingEnabled;

        if (_contentBlockingEnabled && !_resourceFilterAdded)
        {
            _view.CoreWebView2.AddWebResourceRequestedFilter("*://*/*", CoreWebView2WebResourceContext.All, CoreWebView2WebResourceRequestSourceKinds.Document);
            _view.CoreWebView2.WebResourceRequested += OnWebResourceRequested;
            _resourceFilterAdded = true;
        }
        else if (!_contentBlockingEnabled && _resourceFilterAdded)
        {
            _view.CoreWebView2.WebResourceRequested -= OnWebResourceRequested;
            _view.CoreWebView2.RemoveWebResourceRequestedFilter("*://*/*", CoreWebView2WebResourceContext.All, CoreWebView2WebResourceRequestSourceKinds.Document);
            _resourceFilterAdded = false;
        }
        return Task.CompletedTask;
    }

    private void OnWebResourceRequested(object? sender, CoreWebView2WebResourceRequestedEventArgs e)
    {
        if (!_contentBlockingEnabled || _environment is null ||
            !Uri.TryCreate(e.Request.Uri, UriKind.Absolute, out var uri)) return;

        static bool MatchesDomain(string host, string domain) =>
            host.Equals(domain, StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith("." + domain, StringComparison.OrdinalIgnoreCase);

        if (_allowedDomains.Any(domain => MatchesDomain(uri.Host, domain))) return;
        if (!_blockedDomains.Any(domain => MatchesDomain(uri.Host, domain))) return;

        var body = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("Blocked by look"));
        e.Response = _environment.CreateWebResourceResponse(body, 403, "Blocked by look",
            "Content-Type: text/plain; charset=utf-8");
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
        if (IsReaderMode) return;
        if (Uri.TryCreate(_view.Source?.ToString(), UriKind.Absolute, out var uri))
            AddressChanged?.Invoke(this, uri);
    }

    private void OnPermissionRequested(object? sender, CoreWebView2PermissionRequestedEventArgs e)
    {
        e.Handled = true;
        e.SavesInProfile = false;
        e.State = CoreWebView2PermissionState.Deny;
        if (PermissionRequested is null) return;
        PermissionRequested.Invoke(this, new BrowserPermissionRequest(e.Uri, e.PermissionKind.ToString(), allow =>
        {
            e.State = allow ? CoreWebView2PermissionState.Allow : CoreWebView2PermissionState.Deny;
        }));
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
        if (!IsReaderMode) NavigationFinished?.Invoke(this, e.IsSuccess);
    }

    public async ValueTask DisposeAsync()
    {
        await _lifecycleGate.WaitAsync();
        try
        {
            if (_view.CoreWebView2 is { } core)
            {
                core.DocumentTitleChanged -= OnDocumentTitleChanged;
                core.SourceChanged -= OnSourceChanged;
                core.NavigationStarting -= OnNavigationStarting;
                core.NavigationCompleted -= OnNavigationCompleted;
                core.DownloadStarting -= OnDownloadStarting;
                core.PermissionRequested -= OnPermissionRequested;
                if (_resourceFilterAdded)
                {
                    core.WebResourceRequested -= OnWebResourceRequested;
                    core.RemoveWebResourceRequestedFilter("*://*/*", CoreWebView2WebResourceContext.All,
                        CoreWebView2WebResourceRequestSourceKinds.Document);
                    _resourceFilterAdded = false;
                }
                if (_customCssScriptId is not null)
                    await core.RemoveScriptToExecuteOnDocumentCreatedAsync(_customCssScriptId);
                core.Stop();
            }

            TitleChanged = null;
            AddressChanged = null;
            LoadingChanged = null;
            NavigationFinished = null;
            DownloadRequested = null;
            PermissionRequested = null;
            _readerSourceUri = null;
            _customCssScriptId = null;
            _customCssDomain = null;
            ((IDisposable)_view).Dispose();
            if (_environmentKey is not null && _environmentEntry is not null)
                ReleaseEnvironment(_environmentKey, _environmentEntry);
            _environmentKey = null;
            _environmentEntry = null;
        }
        finally
        {
            _lifecycleGate.Release();
            _lifecycleGate.Dispose();
        }
    }
}
