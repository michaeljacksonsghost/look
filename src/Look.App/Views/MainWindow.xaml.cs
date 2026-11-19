using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using System.Diagnostics;
using Look.Models;
using Look.Services;

namespace Look.Views;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<BrowserTab> _tabs = new();
    private readonly Dictionary<Guid, IBrowserEngine> _engines = new();
    private readonly IBrowserEngineFactory _engineFactory;
    private readonly BrowserDataStore _dataStore;
    private readonly string _profileFolder;
    private readonly bool _isPrivate;
    private readonly string? _privateDataRoot;
    private BrowserTab? _active;
    private bool _restoring;
    private Point _tabDragStart;
    private BrowserTab? _tabDragCandidate;

    public MainWindow(bool isPrivate = false, IBrowserEngineFactory? engineFactory = null)
    {
        _engineFactory = engineFactory ?? ((App)Application.Current).BrowserEngineFactory;
        _isPrivate = isPrivate;
        if (_isPrivate)
        {
            _privateDataRoot = Path.Combine(Path.GetTempPath(), "look-private", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_privateDataRoot);
            _dataStore = new BrowserDataStore(Path.Combine(_privateDataRoot, "browser-data.json"));
            _profileFolder = Path.Combine(_privateDataRoot, "WebView2");
        }
        else
        {
            _privateDataRoot = null;
            _dataStore = new BrowserDataStore();
            _profileFolder = Path.Combine(Look.AppPaths.DataRoot, "WebView2");
        }

        InitializeComponent();
        if (_isPrivate)
        {
            Title = "look — Private";
            ModeText.Text = " / private window";
            FooterLabel.Text = "LOOK · PRIVATE SESSION";
        }
        TabsList.ItemsSource = _tabs;
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        _restoring = true;
        try
        {
            var data = await _dataStore.LoadAsync();
            foreach (var saved in data.SessionTabs)
            {
                _tabs.Add(new BrowserTab
                {
                    Address = saved.Address,
                    Title = string.IsNullOrWhiteSpace(saved.Title) ? "New tab" : saved.Title,
                    IsPinned = saved.IsPinned
                });
            }

            if (_tabs.Count == 0) AddTab();
            else SelectTab(_tabs[Math.Clamp(data.ActiveTabIndex, 0, _tabs.Count - 1)]);
        }
        catch (Exception ex)
        {
            StatusText.Text = "Session could not be restored";
            AddTab();
            MessageBox.Show($"look could not restore the previous session.\n\n{ex.Message}",
                "look", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally { _restoring = false; }
    }

    private void AddTab()
    {
        var tab = new BrowserTab();
        _tabs.Add(tab);
        SelectTab(tab);
    }

    private void SelectTab(BrowserTab tab)
    {
        if (_active is not null) _active.IsActive = false;
        _active = tab;
        tab.IsActive = true;
        AddressBox.Text = tab.Address;
        BrowserHost.Children.Clear();

        if (_engines.TryGetValue(tab.Id, out var engine))
        {
            WelcomePanel.Visibility = Visibility.Collapsed;
            BrowserHost.Visibility = Visibility.Visible;
            BrowserHost.Children.Add(engine.View);
        }
        else
        {
            BrowserHost.Visibility = Visibility.Collapsed;
            WelcomePanel.Visibility = Visibility.Visible;
            if (!string.IsNullOrWhiteSpace(tab.Address))
                _ = NavigateAsync(tab.Address);
        }
    }

    private async Task<IBrowserEngine> GetOrCreateEngineAsync(BrowserTab tab)
    {
        if (_engines.TryGetValue(tab.Id, out var existing)) return existing;

        var engine = _engineFactory.Create();
        _engines.Add(tab.Id, engine);
        engine.TitleChanged += (_, title) => tab.Title = title;
        engine.AddressChanged += (_, uri) =>
        {
            tab.Address = uri.AbsoluteUri;
            if (_active?.Id == tab.Id) AddressBox.Text = tab.Address;
        };
        engine.LoadingChanged += (_, loading) =>
        {
            tab.IsLoading = loading;
            if (_active?.Id == tab.Id) StatusText.Text = loading ? "Loading…" : "Ready";
        };
        engine.DownloadRequested += (_, request) =>
        {
            var downloadsFolder = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            downloadsFolder = Path.Combine(downloadsFolder, "Downloads");
            Directory.CreateDirectory(downloadsFolder);
            var dialog = new SaveFileDialog
            {
                FileName = request.SuggestedFileName,
                InitialDirectory = downloadsFolder,
                Title = "Save download"
            };
            if (dialog.ShowDialog(this) != true)
            {
                request.Complete(null);
                return;
            }

            request.Finished += async (_, success) =>
            {
                try
                {
                    if (!_isPrivate) await _dataStore.AddDownloadAsync(new BrowserDownloadEntry(
                        Guid.NewGuid(), request.SuggestedFileName, request.SourceUri,
                        dialog.FileName, success, DateTimeOffset.UtcNow));
                    if (_active?.Id == tab.Id)
                        StatusText.Text = success ? "Download completed" : "Download interrupted";
                }
                catch (IOException) { if (_active?.Id == tab.Id) StatusText.Text = "Download record could not be saved"; }
            };
            request.Complete(dialog.FileName);
        };
        if (!_isPrivate) engine.NavigationFinished += async (_, success) =>
        {
            if (!success || string.IsNullOrWhiteSpace(tab.Address)) return;
            try { await _dataStore.AddHistoryAsync(tab.Title, tab.Address); }
            catch (IOException) { if (_active?.Id == tab.Id) StatusText.Text = "History could not be saved"; }
        };

        try
        {
            await engine.InitializeAsync(_profileFolder);
            engine.IsMuted = tab.IsMuted;
            return engine;
        }
        catch
        {
            _engines.Remove(tab.Id);
            await engine.DisposeAsync();
            throw;
        }
    }

    private async Task NavigateAsync(string input)
    {
        if (_active is null) return;
        try
        {
            var uri = AddressParser.Parse(input);
            var tab = _active;
            tab.Address = uri.AbsoluteUri;
            AddressBox.Text = tab.Address;
            WelcomePanel.Visibility = Visibility.Collapsed;
            StatusText.Text = "Starting tab…";

            var engine = await GetOrCreateEngineAsync(tab);
            if (_active?.Id != tab.Id) return;

            BrowserHost.Children.Clear();
            BrowserHost.Children.Add(engine.View);
            BrowserHost.Visibility = Visibility.Visible;
            await engine.NavigateAsync(uri);
        }
        catch (Exception ex)
        {
            StatusText.Text = "Could not open page";
            MessageBox.Show($"look could not start this page.\n\n{ex.Message}",
                "look", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void AddressBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) { _ = NavigateAsync(AddressBox.Text); e.Handled = true; }
    }

    private void WelcomeAddress_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) { _ = NavigateAsync(WelcomeAddress.Text); e.Handled = true; }
    }

    private void NewTab_Click(object sender, RoutedEventArgs e) { AddTab(); WelcomeAddress.Focus(); }

    private void Tab_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement element || element.Tag is not BrowserTab tab) return;
        _tabDragCandidate = tab;
        _tabDragStart = e.GetPosition(null);
    }

    private void Tab_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _tabDragCandidate is null) return;
        var current = e.GetPosition(null);
        if (Math.Abs(current.X - _tabDragStart.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(current.Y - _tabDragStart.Y) < SystemParameters.MinimumVerticalDragDistance) return;

        var candidate = _tabDragCandidate;
        _tabDragCandidate = null;
        DragDrop.DoDragDrop((DependencyObject)sender, candidate, DragDropEffects.Move);
    }

    private void Tab_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(typeof(BrowserTab)) ? DragDropEffects.Move : DragDropEffects.None;
        e.Handled = true;
    }

    private async void Tab_Drop(object sender, DragEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not BrowserTab target ||
            e.Data.GetData(typeof(BrowserTab)) is not BrowserTab dragged) return;
        var oldIndex = _tabs.IndexOf(dragged);
        var newIndex = _tabs.IndexOf(target);
        if (oldIndex < 0 || newIndex < 0 || oldIndex == newIndex) return;
        _tabs.Move(oldIndex, newIndex);
        await SaveSessionAsync();
    }

    private void SelectTab_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is BrowserTab tab) SelectTab(tab);
    }

    private async void CloseTab_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not BrowserTab tab) return;
        var index = _tabs.IndexOf(tab);
        var wasActive = _active?.Id == tab.Id;
        if (!_isPrivate && Uri.TryCreate(tab.Address, UriKind.Absolute, out var closedUri) &&
            (closedUri.Scheme == Uri.UriSchemeHttp || closedUri.Scheme == Uri.UriSchemeHttps))
            await _dataStore.AddRecentlyClosedAsync(new BrowserSessionTab(tab.Address, tab.Title, tab.IsPinned));
        if (wasActive) { BrowserHost.Children.Clear(); BrowserHost.Visibility = Visibility.Collapsed; }
        _tabs.Remove(tab);
        if (_engines.Remove(tab.Id, out var engine)) await engine.DisposeAsync();
        if (_tabs.Count == 0) AddTab();
        else if (wasActive) SelectTab(_tabs[Math.Clamp(index - 1, 0, _tabs.Count - 1)]);
        await SaveSessionAsync();
    }

    private static BrowserTab? GetContextTab(object sender)
    {
        if (sender is not MenuItem item) return null;
        var contextMenu = item.Parent as ContextMenu;
        return (contextMenu?.PlacementTarget as FrameworkElement)?.Tag as BrowserTab;
    }

    private async void PinTab_Click(object sender, RoutedEventArgs e)
    {
        var tab = GetContextTab(sender);
        if (tab is null) return;
        tab.IsPinned = !tab.IsPinned;
        await SaveSessionAsync();
    }

    private async void DuplicateTab_Click(object sender, RoutedEventArgs e)
    {
        var tab = GetContextTab(sender);
        if (tab is null) return;
        var duplicate = new BrowserTab
        {
            Address = tab.Address,
            Title = tab.Title,
            IsPinned = false,
            IsMuted = tab.IsMuted
        };
        _tabs.Add(duplicate);
        SelectTab(duplicate);
        await SaveSessionAsync();
    }

    private async void MuteTab_Click(object sender, RoutedEventArgs e)
    {
        var tab = GetContextTab(sender);
        if (tab is null) return;
        tab.IsMuted = !tab.IsMuted;
        if (_engines.TryGetValue(tab.Id, out var engine))
            engine.IsMuted = tab.IsMuted;
        StatusText.Text = tab.IsMuted ? "Tab muted" : "Tab unmuted";
        await SaveSessionAsync();
    }

    private void CloseTabFromMenu_Click(object sender, RoutedEventArgs e)
    {
        var tab = GetContextTab(sender);
        if (tab is not null)
            CloseTab_Click(new Button { Tag = tab }, new RoutedEventArgs());
    }

    private async void Bookmark_Click(object sender, RoutedEventArgs e)
    {
        if (_isPrivate) { StatusText.Text = "Bookmarks are not saved in private windows"; return; }
        if (_active is null || string.IsNullOrWhiteSpace(_active.Address))
        { StatusText.Text = "Open a page before bookmarking it"; return; }
        var added = await _dataStore.ToggleBookmarkAsync(_active.Title, _active.Address);
        StatusText.Text = added ? "Bookmark saved" : "Bookmark removed";
    }

    private async void Menu_Click(object sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu();
        var privateWindow = new MenuItem { Header = "New private window" };
        privateWindow.Click += (_, _) => new MainWindow(isPrivate: true, engineFactory: _engineFactory).Show();
        menu.Items.Add(privateWindow);
        var addBookmark = new MenuItem { Header = "Add / remove bookmark" };
        addBookmark.Click += Bookmark_Click;
        menu.Items.Add(addBookmark);
        menu.Items.Add(new Separator());

        var bookmarks = new MenuItem { Header = "Bookmarks" };
        foreach (var bookmark in await _dataStore.GetBookmarksAsync())
        {
            var item = new MenuItem { Header = bookmark.Title, Tag = bookmark.Address, ToolTip = bookmark.Address };
            item.Click += MenuNavigate_Click;
            bookmarks.Items.Add(item);
        }
        if (bookmarks.Items.Count == 0) bookmarks.Items.Add(new MenuItem { Header = "No bookmarks yet", IsEnabled = false });
        menu.Items.Add(bookmarks);

        var extensionsMenu = new MenuItem { Header = "Extensions" };
        var extensionNote = new MenuItem
        {
            Header = "Unpacked extensions only · popup buttons are not hosted yet",
            IsEnabled = false
        };
        extensionsMenu.Items.Add(extensionNote);
        extensionsMenu.Items.Add(new Separator());
        var installExtension = new MenuItem { Header = "Install unpacked extension…" };
        installExtension.Click += async (_, _) =>
        {
            if (_active is null || !_engines.TryGetValue(_active.Id, out var activeEngine))
            {
                MessageBox.Show("Open a webpage first, then manage extensions from this menu.",
                    "Extensions", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dialog = new OpenFolderDialog { Title = "Select an unpacked extension folder" };
            if (dialog.ShowDialog(this) != true) return;
            try
            {
                var installed = await activeEngine.InstallExtensionAsync(dialog.FolderName);
                StatusText.Text = $"Installed {installed.Name}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"The extension could not be installed.\n\n{ex.Message}",
                    "Extension installation", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        };
        extensionsMenu.Items.Add(installExtension);
        if (_active is not null && _engines.TryGetValue(_active.Id, out var extensionEngine))
        {
            foreach (var extension in await extensionEngine.GetExtensionsAsync())
            {
                var extensionItem = new MenuItem
                {
                    Header = $"{extension.Name} ({(extension.IsEnabled ? "enabled" : "disabled")})"
                };
                var toggle = new MenuItem
                {
                    Header = extension.IsEnabled ? "Disable" : "Enable",
                    Tag = (extensionEngine, extension)
                };
                toggle.Click += ExtensionToggle_Click;
                var remove = new MenuItem { Header = "Remove", Tag = (extensionEngine, extension) };
                remove.Click += ExtensionRemove_Click;
                extensionItem.Items.Add(toggle);
                extensionItem.Items.Add(remove);
                extensionsMenu.Items.Add(extensionItem);
            }
        }
        menu.Items.Add(extensionsMenu);

        var downloadsMenu = new MenuItem { Header = "Downloads" };
        foreach (var download in await _dataStore.GetDownloadsAsync(20))
        {
            var downloadItem = new MenuItem
            {
                Header = $"{download.FileName} ({(download.IsComplete ? "complete" : "interrupted")})",
                Tag = download.DestinationPath,
                ToolTip = download.DestinationPath
            };
            downloadItem.Click += OpenDownload_Click;
            downloadsMenu.Items.Add(downloadItem);
        }
        if (downloadsMenu.Items.Count == 0)
            downloadsMenu.Items.Add(new MenuItem { Header = "No downloads yet", IsEnabled = false });
        menu.Items.Add(downloadsMenu);

        var history = new MenuItem { Header = "Recent history" };
        foreach (var entry in await _dataStore.GetHistoryAsync(20))
        {
            var item = new MenuItem { Header = entry.Title, Tag = entry.Address, ToolTip = entry.Address };
            item.Click += MenuNavigate_Click;
            history.Items.Add(item);
        }
        if (history.Items.Count == 0) history.Items.Add(new MenuItem { Header = "No history yet", IsEnabled = false });
        history.Items.Add(new Separator());
        var clearHistory = new MenuItem { Header = "Clear browsing history…" };
        clearHistory.Click += async (_, _) =>
        {
            if (MessageBox.Show("Clear the browsing history saved by look?", "Clear history",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                await _dataStore.ClearHistoryAsync();
                StatusText.Text = "Browsing history cleared";
            }
        };
        history.Items.Add(clearHistory);
        menu.Items.Add(history);

        menu.Items.Add(new Separator());
        var quit = new MenuItem { Header = "Quit look" };
        quit.Click += (_, _) => Close();
        menu.Items.Add(quit);
        menu.IsOpen = true;
    }

    private async void ExtensionToggle_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as MenuItem)?.Tag is not ValueTuple<IBrowserEngine, BrowserExtensionInfo> data) return;
        try
        {
            await data.Item1.SetExtensionEnabledAsync(data.Item2.Id, !data.Item2.IsEnabled);
            StatusText.Text = data.Item2.IsEnabled ? "Extension disabled" : "Extension enabled";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not update the extension.\n\n{ex.Message}",
                "Extensions", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void ExtensionRemove_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as MenuItem)?.Tag is not ValueTuple<IBrowserEngine, BrowserExtensionInfo> data) return;
        if (MessageBox.Show($"Remove {data.Item2.Name} from look?", "Remove extension",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        try
        {
            await data.Item1.RemoveExtensionAsync(data.Item2.Id);
            StatusText.Text = $"Removed {data.Item2.Name}";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not remove the extension.\n\n{ex.Message}",
                "Extensions", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OpenDownload_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as MenuItem)?.Tag is not string path || !File.Exists(path))
        {
            StatusText.Text = "That downloaded file is no longer available";
            return;
        }
        try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); }
        catch (Exception ex)
        {
            MessageBox.Show($"The file could not be opened.\n\n{ex.Message}",
                "Downloads", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void MenuNavigate_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is string address) _ = NavigateAsync(address);
    }

    private async Task RestoreClosedTabAsync()
    {
        if (_isPrivate) { StatusText.Text = "Recently closed tabs are unavailable in private windows"; return; }
        var recentlyClosed = await _dataStore.GetRecentlyClosedAsync();
        if (recentlyClosed.Count == 0)
        {
            StatusText.Text = "No recently closed tabs";
            return;
        }

        var saved = recentlyClosed[0];
        await _dataStore.RemoveRecentlyClosedAsync(saved.Address);
        var tab = new BrowserTab { Address = saved.Address, Title = saved.Title, IsPinned = saved.IsPinned };
        _tabs.Add(tab);
        SelectTab(tab);
        StatusText.Text = "Tab restored";
    }

    private async Task SaveSessionAsync()
    {
        if (_restoring || _isPrivate) return;
        var snapshots = _tabs.Select(tab => new BrowserSessionTab(tab.Address, tab.Title, tab.IsPinned)).ToList();
        var activeIndex = _active is null ? 0 : Math.Max(0, _tabs.IndexOf(_active));
        await _dataStore.SaveSessionAsync(snapshots, activeIndex);
    }

    private async void Stop_Click(object sender, RoutedEventArgs e)
    {
        if (_active is not null && _engines.TryGetValue(_active.Id, out var engine)) await engine.StopAsync();
    }

    private async void Back_Click(object sender, RoutedEventArgs e)
    {
        if (_active is not null && _engines.TryGetValue(_active.Id, out var engine)) await engine.GoBackAsync();
    }

    private async void Forward_Click(object sender, RoutedEventArgs e)
    {
        if (_active is not null && _engines.TryGetValue(_active.Id, out var engine)) await engine.GoForwardAsync();
    }

    private async void Reload_Click(object sender, RoutedEventArgs e)
    {
        if (_active is not null && _engines.TryGetValue(_active.Id, out var engine)) await engine.ReloadAsync();
    }

    private void FindTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) { _ = FindActivePageAsync(backwards: (Keyboard.Modifiers & ModifierKeys.Shift) != 0); e.Handled = true; }
        else if (e.Key == Key.Escape) { CloseFind_Click(this, new RoutedEventArgs()); e.Handled = true; }
    }

    private async Task FindActivePageAsync(bool backwards = false)
    {
        if (_active is not null && _engines.TryGetValue(_active.Id, out var engine))
            await engine.FindInPageAsync(FindTextBox.Text, backwards);
    }

    private void FindNext_Click(object sender, RoutedEventArgs e) => _ = FindActivePageAsync();
    private void FindPrevious_Click(object sender, RoutedEventArgs e) => _ = FindActivePageAsync(backwards: true);
    private void CloseFind_Click(object sender, RoutedEventArgs e)
    {
        FindBar.Visibility = Visibility.Collapsed;
        if (_active is not null && _engines.TryGetValue(_active.Id, out var engine))
            _ = engine.FindInPageAsync(string.Empty);
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Control) == 0) return;
        if (e.Key == Key.F)
        {
            FindBar.Visibility = Visibility.Visible;
            FindTextBox.Focus();
            FindTextBox.SelectAll();
            e.Handled = true;
        }
        else if (e.Key == Key.L)
        {
            AddressBox.Focus(); AddressBox.SelectAll(); e.Handled = true;
        }
        else if (e.Key == Key.N && (Keyboard.Modifiers & ModifierKeys.Shift) != 0)
        {
            new MainWindow(isPrivate: true).Show(); e.Handled = true;
        }
        else if (e.Key == Key.T && (Keyboard.Modifiers & ModifierKeys.Shift) != 0)
        {
            _ = RestoreClosedTabAsync(); e.Handled = true;
        }
        else if (e.Key == Key.T)
        {
            AddTab(); WelcomeAddress.Focus(); e.Handled = true;
        }
        else if (e.Key == Key.W)
        {
            if (_active is not null) CloseTab_Click(new Button { Tag = _active }, new RoutedEventArgs());
            e.Handled = true;
        }
        else if (e.Key == Key.R)
        {
            Reload_Click(this, new RoutedEventArgs()); e.Handled = true;
        }
        else if (e.Key >= Key.D1 && e.Key <= Key.D8 && _tabs.Count > 0)
        {
            var targetIndex = (int)e.Key - (int)Key.D1;
            if (targetIndex < _tabs.Count) SelectTab(_tabs[targetIndex]);
            e.Handled = true;
        }
        else if (e.Key == Key.D9 && _tabs.Count > 0)
        {
            SelectTab(_tabs[^1]); e.Handled = true;
        }
        else if (e.Key == Key.Tab && _tabs.Count > 1)
        {
            var index = _active is null ? 0 : _tabs.IndexOf(_active);
            var backwards = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
            SelectTab(_tabs[(index + (backwards ? -1 : 1) + _tabs.Count) % _tabs.Count]);
            e.Handled = true;
        }
    }

    private async void Window_Closed(object? sender, EventArgs e)
    {
        try { await SaveSessionAsync(); }
        catch (IOException) { /* A failed session write must not prevent shutdown. */ }
        BrowserHost.Children.Clear();
        foreach (var engine in _engines.Values) await engine.DisposeAsync();
        _engines.Clear();
        if (_privateDataRoot is not null && Directory.Exists(_privateDataRoot))
        {
            try { Directory.Delete(_privateDataRoot, recursive: true); }
            catch (IOException) { /* Temporary private data is cleaned up on normal close where Windows permits it. */ }
            catch (UnauthorizedAccessException) { /* Do not block shutdown if the OS still holds a profile file. */ }
        }
    }
}
