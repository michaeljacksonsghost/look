using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Data;
using System.ComponentModel;
using Microsoft.Win32;
using System.Diagnostics;
using Look.Models;
using Look.Services;

namespace Look.Views;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<BrowserTab> _tabs = new();
    private readonly ObservableCollection<BrowserSpace> _spaces = new();
    private ICollectionView _tabView = null!;
    private string _activeSpaceId = "default";
    private readonly Dictionary<Guid, IBrowserEngine> _engines = new();
    private readonly Dictionary<Guid, Task> _engineInitializations = new();
    private readonly Dictionary<Guid, int> _navigationVersions = new();
    private readonly IBrowserEngineFactory _engineFactory;
    private readonly BrowserDataStore _dataStore;
    private readonly string _profileFolder;
    private readonly bool _isPrivate;
    private readonly string? _privateDataRoot;
    private FileStream? _privateLock;
    private BrowserTab? _active;
    private BrowserTab? _splitTab;
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
            _privateLock = new FileStream(Path.Combine(_privateDataRoot, "private.lock"),
                FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
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
        _tabView = CollectionViewSource.GetDefaultView(_tabs);
        _tabView.Filter = item => item is BrowserTab tab && tab.SpaceId == _activeSpaceId;
        TabsList.ItemsSource = _tabView;
        SpaceSelector.ItemsSource = _spaces;
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        _restoring = true;
        try
        {
            var data = await _dataStore.LoadAsync();
            _spaces.Clear();
            foreach (var space in data.Spaces ?? [])
                _spaces.Add(space);
            if (_spaces.Count == 0)
                _spaces.Add(new BrowserSpace("default", "Default", DateTimeOffset.UtcNow));

            _activeSpaceId = _spaces.Any(space => space.Id == data.ActiveSpaceId)
                ? data.ActiveSpaceId
                : _spaces[0].Id;
            SpaceSelector.SelectedValue = _activeSpaceId;

            foreach (var saved in data.SessionTabs)
            {
                var spaceId = _spaces.Any(space => space.Id == saved.SpaceId) ? saved.SpaceId : _spaces[0].Id;
                _tabs.Add(new BrowserTab
                {
                    Address = saved.Address,
                    Title = string.IsNullOrWhiteSpace(saved.Title) ? "New tab" : saved.Title,
                    IsPinned = saved.IsPinned,
                    SpaceId = spaceId
                });
            }
            _tabView.Refresh();

            var currentSpaceTabs = _tabs.Where(tab => tab.SpaceId == _activeSpaceId).ToList();
            if (currentSpaceTabs.Count == 0)
                AddTab();
            else
            {
                var desired = data.ActiveTabIndex >= 0 && data.ActiveTabIndex < _tabs.Count
                    ? _tabs[data.ActiveTabIndex]
                    : currentSpaceTabs[0];
                SelectTab(desired.SpaceId == _activeSpaceId ? desired : currentSpaceTabs[0]);
            }
        }
        catch (Exception ex)
        {
            StatusText.Text = "Session could not be restored";
            if (_spaces.Count == 0)
            {
                _spaces.Add(new BrowserSpace("default", "Default", DateTimeOffset.UtcNow));
                _activeSpaceId = "default";
                SpaceSelector.SelectedValue = _activeSpaceId;
            }
            AddTab();
            MessageBox.Show($"look could not restore the previous session.\n\n{ex.Message}",
                "look", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally { _restoring = false; }
    }

    private void AddTab()
    {
        var tab = new BrowserTab { SpaceId = _activeSpaceId };
        _tabs.Add(tab);
        SelectTab(tab);
    }

    private void SelectTab(BrowserTab tab)
    {
        if (tab.SpaceId != _activeSpaceId)
        {
            if (_splitTab is not null)
            {
                _splitTab.IsSplit = false;
                if (_engines.TryGetValue(_splitTab.Id, out var oldSplitEngine)) _ = oldSplitEngine.TrySuspendAsync();
                _splitTab = null;
            }
            _activeSpaceId = tab.SpaceId;
            SpaceSelector.SelectedValue = _activeSpaceId;
            _tabView.Refresh();
        }

        var previous = _active;
        if (_splitTab?.Id == tab.Id && previous is not null && previous.Id != tab.Id)
        {
            tab.IsSplit = false;
            _splitTab = previous;
            _splitTab.IsSplit = true;
        }

        if (previous is not null) previous.IsActive = false;
        BrowserHost.Children.Clear();
        if (previous is not null && previous.Id != tab.Id && previous.Id != _splitTab?.Id &&
            _engines.TryGetValue(previous.Id, out var previousEngine))
            _ = previousEngine.TrySuspendAsync();

        _active = tab;
        tab.IsActive = true;
        if (_splitTab?.Id == tab.Id)
        {
            _splitTab.IsSplit = false;
            _splitTab = null;
        }
        if (_splitTab is not null) _splitTab.IsSplit = true;
        AddressBox.Text = tab.Address;

        if (_engines.TryGetValue(tab.Id, out var engine))
            _ = engine.ResumeAsync();
        if (_splitTab is not null && _engines.TryGetValue(_splitTab.Id, out var splitEngine))
            _ = splitEngine.ResumeAsync();

        RenderBrowserHost();
        if (!_engines.ContainsKey(tab.Id) && !string.IsNullOrWhiteSpace(tab.Address))
            _ = NavigateAsync(tab.Address);
        _ = SaveSessionQuietlyAsync();
    }

    private void RenderBrowserHost()
    {
        BrowserHost.Children.Clear();
        if (_active is null || !_engines.TryGetValue(_active.Id, out var activeEngine))
        {
            BrowserHost.Visibility = Visibility.Collapsed;
            WelcomePanel.Visibility = Visibility.Visible;
            return;
        }

        WelcomePanel.Visibility = Visibility.Collapsed;
        BrowserHost.Visibility = Visibility.Visible;

        if (_splitTab is not null && _engines.TryGetValue(_splitTab.Id, out var splitEngine))
        {
            var splitGrid = new Grid();
            splitGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 180 });
            splitGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            splitGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 180 });

            var leftPane = new Border
            {
                Child = activeEngine.View,
                BorderBrush = (System.Windows.Media.Brush)FindResource("LineBrush"),
                BorderThickness = new Thickness(0, 0, 1, 0)
            };
            var splitter = new GridSplitter
            {
                Width = 6,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                ResizeDirection = GridResizeDirection.Columns,
                ResizeBehavior = GridResizeBehavior.PreviousAndNext,
                ShowsPreview = true,
                Background = (System.Windows.Media.Brush)FindResource("LineBrush")
            };
            var rightPane = new Border
            {
                Child = splitEngine.View,
                BorderBrush = (System.Windows.Media.Brush)FindResource("LineBrush"),
                BorderThickness = new Thickness(1, 0, 0, 0)
            };

            Grid.SetColumn(leftPane, 0);
            Grid.SetColumn(splitter, 1);
            Grid.SetColumn(rightPane, 2);
            splitGrid.Children.Add(leftPane);
            splitGrid.Children.Add(splitter);
            splitGrid.Children.Add(rightPane);
            BrowserHost.Children.Add(splitGrid);
        }
        else
        {
            BrowserHost.Children.Add(activeEngine.View);
        }
    }

    private async Task OpenSplitViewAsync(BrowserTab secondary)
    {
        if (_active is null || secondary.Id == _active.Id) return;
        if (secondary.SpaceId != _activeSpaceId)
        {
            StatusText.Text = "Split view is limited to tabs in the current space";
            return;
        }

        var previousSplit = _splitTab;
        if (previousSplit is not null && previousSplit.Id != secondary.Id)
        {
            BrowserHost.Children.Clear();
            previousSplit.IsSplit = false;
            if (_engines.TryGetValue(previousSplit.Id, out var previousEngine))
                _ = previousEngine.TrySuspendAsync();
        }
        _splitTab = secondary;
        secondary.IsSplit = true;

        try
        {
            var alreadyCreated = _engines.ContainsKey(secondary.Id);
            var engine = await GetOrCreateEngineAsync(secondary);
            if (!alreadyCreated)
            {
                var address = string.IsNullOrWhiteSpace(secondary.Address)
                    ? new Uri("https://www.google.com/")
                    : AddressParser.Parse(secondary.Address);
                secondary.Address = address.AbsoluteUri;
                await engine.NavigateAsync(address);
            }

            await engine.ResumeAsync();
            RenderBrowserHost();
            StatusText.Text = "Split view enabled";
        }
        catch (Exception ex)
        {
            secondary.IsSplit = false;
            _splitTab = previousSplit;
            if (_splitTab is not null)
            {
                _splitTab.IsSplit = true;
                if (_engines.TryGetValue(_splitTab.Id, out var restoreEngine)) _ = restoreEngine.ResumeAsync();
            }
            RenderBrowserHost();
            StatusText.Text = "Could not open split view";
            MessageBox.Show($"look could not open the second pane.\n\n{ex.Message}",
                "Split view", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void CloseSplitView()
    {
        if (_splitTab is null) return;
        _splitTab.IsSplit = false;
        var secondary = _splitTab;
        _splitTab = null;
        BrowserHost.Children.Clear();
        if (_engines.TryGetValue(secondary.Id, out var engine))
            _ = engine.TrySuspendAsync();
        RenderBrowserHost();
        StatusText.Text = "Split view closed";
    }

    private async Task<IBrowserEngine> GetOrCreateEngineAsync(BrowserTab tab)
    {
        if (_engines.TryGetValue(tab.Id, out var existing))
        {
            if (_engineInitializations.TryGetValue(tab.Id, out var pending))
                await pending;
            return existing;
        }

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
        engine.PermissionRequested += (_, request) =>
        {
            var decision = MessageBox.Show(
                $"{request.Origin} wants permission to use {request.PermissionKind}.\n\nAllow this request once?",
                "Site permission", MessageBoxButton.YesNo, MessageBoxImage.Question);
            var allowed = decision == MessageBoxResult.Yes;
            request.Respond(allowed);
            if (_active?.Id == tab.Id)
                StatusText.Text = allowed ? $"Allowed {request.PermissionKind} once" : $"Denied {request.PermissionKind}";
        };
        engine.DownloadRequested += (_, request) =>
        {
            var extension = Path.GetExtension(request.SuggestedFileName).ToLowerInvariant();
            var riskyExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".exe", ".msi", ".bat", ".cmd", ".ps1", ".scr", ".com", ".reg", ".vbs", ".js", ".jar", ".lnk"
            };
            if (riskyExtensions.Contains(extension) &&
                MessageBox.Show($"This file type ({extension}) can run code or change your system. Only save it if you trust the source. Continue?",
                    "Potentially unsafe download", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            {
                request.Complete(null);
                return;
            }

            var downloadsFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
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
        engine.NavigationFinished += async (_, success) =>
        {
            if (success && !_isPrivate && !string.IsNullOrWhiteSpace(tab.Address))
            {
                try { await _dataStore.AddHistoryAsync(tab.Title, tab.Address); }
                catch (IOException) { if (_active?.Id == tab.Id) StatusText.Text = "History could not be saved"; }
            }
            if (_active?.Id != tab.Id && _splitTab?.Id != tab.Id)
                await engine.TrySuspendAsync();
            else
                await SaveSessionQuietlyAsync();
        };

        var initialization = InitializeEngineAsync(engine, tab);
        _engineInitializations[tab.Id] = initialization;
        try
        {
            await initialization;
            return engine;
        }
        catch
        {
            _engines.Remove(tab.Id);
            await engine.DisposeAsync();
            throw;
        }
        finally
        {
            _engineInitializations.Remove(tab.Id);
        }
    }

    private async Task InitializeEngineAsync(IBrowserEngine engine, BrowserTab tab)
    {
        await engine.InitializeAsync(_profileFolder);
        engine.IsMuted = tab.IsMuted;
        await engine.ConfigureContentBlockingAsync(await _dataStore.GetSettingsAsync());
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

            var version = _navigationVersions.TryGetValue(tab.Id, out var currentVersion) ? currentVersion + 1 : 1;
            _navigationVersions[tab.Id] = version;
            var engine = await GetOrCreateEngineAsync(tab);
            if (!_navigationVersions.TryGetValue(tab.Id, out var latestVersion) || latestVersion != version)
                return;

            await engine.NavigateAsync(uri);
            if (_active?.Id != tab.Id && _splitTab?.Id != tab.Id) return;
            RenderBrowserHost();
        }
        catch (Exception ex)
        {
            StatusText.Text = "Could not open page";
            MessageBox.Show($"look could not start this page.\n\n{ex.Message}\n\nIf the WebView2 Runtime is missing, install the Microsoft Edge WebView2 Evergreen Runtime.",
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

    private void SpaceSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_restoring || SpaceSelector.SelectedValue is not string selectedId ||
            string.Equals(selectedId, _activeSpaceId, StringComparison.Ordinal)) return;

        var previous = _active;
        if (previous is not null) previous.IsActive = false;
        if (_splitTab is not null)
        {
            _splitTab.IsSplit = false;
            if (_engines.TryGetValue(_splitTab.Id, out var splitEngine)) _ = splitEngine.TrySuspendAsync();
            _splitTab = null;
        }
        BrowserHost.Children.Clear();
        BrowserHost.Visibility = Visibility.Collapsed;
        if (previous is not null && _engines.TryGetValue(previous.Id, out var previousEngine))
            _ = previousEngine.TrySuspendAsync();

        _active = null;
        _activeSpaceId = selectedId;
        _tabView.Refresh();
        var firstTab = _tabs.FirstOrDefault(tab => tab.SpaceId == _activeSpaceId);
        if (firstTab is not null) SelectTab(firstTab);
        else AddTab();
        _ = SaveSessionQuietlyAsync();
    }

    private void NewSpace_Click(object sender, RoutedEventArgs e)
    {
        var name = PromptForSpaceName();
        if (string.IsNullOrWhiteSpace(name)) return;
        var existing = _spaces.FirstOrDefault(space =>
            string.Equals(space.Name, name, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            SpaceSelector.SelectedValue = existing.Id;
            return;
        }

        var space = new BrowserSpace(Guid.NewGuid().ToString("N"), name, DateTimeOffset.UtcNow);
        _spaces.Add(space);
        SpaceSelector.SelectedValue = space.Id;
        _ = SaveSessionQuietlyAsync();
    }

    private string? PromptForSpaceName()
    {
        var dialog = new Window
        {
            Title = "Create a space",
            Width = 360,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this,
            ResizeMode = ResizeMode.NoResize,
            Background = (System.Windows.Media.Brush)FindResource("PanelBrush"),
            Foreground = (System.Windows.Media.Brush)FindResource("InkBrush")
        };
        var layout = new StackPanel { Margin = new Thickness(20) };
        layout.Children.Add(new TextBlock { Text = "Give this space a name", Margin = new Thickness(0, 0, 0, 10) });
        var input = new TextBox
        {
            MinWidth = 280,
            Padding = new Thickness(9, 6, 9, 6),
            Background = (System.Windows.Media.Brush)FindResource("CanvasBrush"),
            Foreground = (System.Windows.Media.Brush)FindResource("InkBrush"),
            BorderBrush = (System.Windows.Media.Brush)FindResource("LineBrush")
        };
        layout.Children.Add(input);
        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 16, 0, 0)
        };
        var cancel = new Button { Content = "Cancel", Style = (Style)FindResource("QuietButton"), Margin = new Thickness(0, 0, 8, 0) };
        cancel.Click += (_, _) => dialog.DialogResult = false;
        var create = new Button { Content = "Create", Style = (Style)FindResource("QuietButton"), IsDefault = true };
        create.Click += (_, _) => dialog.DialogResult = true;
        buttons.Children.Add(cancel);
        buttons.Children.Add(create);
        layout.Children.Add(buttons);
        dialog.Content = layout;
        dialog.Loaded += (_, _) => input.Focus();
        return dialog.ShowDialog() == true ? input.Text.Trim() : null;
    }

    private void TabContextMenu_Opened(object sender, RoutedEventArgs e)
    {
        if (sender is not ContextMenu menu ||
            (menu.PlacementTarget as FrameworkElement)?.Tag is not BrowserTab tab) return;

        var moveMenu = menu.Items.OfType<MenuItem>()
            .FirstOrDefault(item => string.Equals(item.Header?.ToString(), "Move to space", StringComparison.Ordinal));
        if (moveMenu is null) return;
        moveMenu.Items.Clear();

        foreach (var space in _spaces.Where(space => space.Id != tab.SpaceId))
        {
            var item = new MenuItem { Header = space.Name, Tag = (tab, space.Id) };
            item.Click += MoveTabToSpace_Click;
            moveMenu.Items.Add(item);
        }
        if (moveMenu.Items.Count == 0)
            moveMenu.Items.Add(new MenuItem { Header = "No other spaces", IsEnabled = false });
    }

    private async void MoveTabToSpace_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as MenuItem)?.Tag is not ValueTuple<BrowserTab, string> move) return;
        var tab = move.Item1;
        var wasActive = _active?.Id == tab.Id;
        var wasSplit = _splitTab?.Id == tab.Id;
        if (wasSplit)
        {
            _splitTab!.IsSplit = false;
            _splitTab = null;
            BrowserHost.Children.Clear();
            if (_engines.TryGetValue(tab.Id, out var splitEngine)) await splitEngine.TrySuspendAsync();
        }
        if (wasActive)
        {
            tab.IsActive = false;
            _active = null;
            BrowserHost.Children.Clear();
            BrowserHost.Visibility = Visibility.Collapsed;
            if (_engines.TryGetValue(tab.Id, out var engine))
                await engine.TrySuspendAsync();
        }

        tab.SpaceId = move.Item2;
        _tabView.Refresh();
        if (wasActive)
        {
            var next = _tabs.FirstOrDefault(item => item.SpaceId == _activeSpaceId);
            if (next is not null) SelectTab(next);
            else AddTab();
        }
        else if (wasSplit) RenderBrowserHost();
        await SaveSessionAsync();
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
        var index = _tabs.Where(item => item.SpaceId == _activeSpaceId).ToList().IndexOf(tab);
        var wasActive = _active?.Id == tab.Id;
        var wasSplit = _splitTab?.Id == tab.Id;
        var promotedSplit = wasActive ? _splitTab : null;
        if (wasSplit || promotedSplit is not null)
        {
            if (_splitTab is not null) _splitTab.IsSplit = false;
            _splitTab = null;
            if (wasSplit) BrowserHost.Children.Clear();
        }
        if (!_isPrivate && Uri.TryCreate(tab.Address, UriKind.Absolute, out var closedUri) &&
            (closedUri.Scheme == Uri.UriSchemeHttp || closedUri.Scheme == Uri.UriSchemeHttps))
            await _dataStore.AddRecentlyClosedAsync(new BrowserSessionTab(tab.Address, tab.Title, tab.IsPinned, tab.SpaceId));
        if (wasActive) { BrowserHost.Children.Clear(); BrowserHost.Visibility = Visibility.Collapsed; }
        _navigationVersions.Remove(tab.Id);
        _tabs.Remove(tab);
        if (_engineInitializations.TryGetValue(tab.Id, out var pendingInitialization))
        {
            try { await pendingInitialization; }
            catch { /* The tab is being closed; initialization failure needs no second dialog. */ }
        }
        if (_engines.Remove(tab.Id, out var engine)) await engine.DisposeAsync();
        _engineInitializations.Remove(tab.Id);
        var remainingInSpace = _tabs.Where(item => item.SpaceId == _activeSpaceId).ToList();
        if (wasActive && promotedSplit is not null && _tabs.Contains(promotedSplit))
            SelectTab(promotedSplit);
        else if (remainingInSpace.Count == 0) AddTab();
        else if (wasActive) SelectTab(remainingInSpace[Math.Clamp(index - 1, 0, remainingInSpace.Count - 1)]);
        else if (wasSplit) RenderBrowserHost();
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
            IsMuted = tab.IsMuted,
            SpaceId = tab.SpaceId
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
        var library = new MenuItem { Header = "Open library…" };
        library.Click += (_, _) =>
        {
            var window = new BrowserLibraryWindow(_dataStore, address => _ = NavigateAsync(address)) { Owner = this };
            window.Show();
        };
        menu.Items.Add(library);
        menu.Items.Add(new Separator());
        var privateWindow = new MenuItem { Header = "New private window" };
        privateWindow.Click += (_, _) => new MainWindow(isPrivate: true, engineFactory: _engineFactory).Show();
        menu.Items.Add(privateWindow);
        var splitMenu = new MenuItem { Header = _splitTab is null ? "Open split view" : "Change split pane" };
        foreach (var candidate in _tabs.Where(tab => tab.SpaceId == _activeSpaceId && tab.Id != _active?.Id))
        {
            var splitItem = new MenuItem
            {
                Header = string.IsNullOrWhiteSpace(candidate.Title) ? candidate.Address : candidate.Title,
                Tag = candidate,
                ToolTip = candidate.Address
            };
            splitItem.Click += async (_, _) => await OpenSplitViewAsync(candidate);
            splitMenu.Items.Add(splitItem);
        }
        if (splitMenu.Items.Count == 0)
            splitMenu.Items.Add(new MenuItem { Header = "Open another tab in this space first", IsEnabled = false });
        if (_splitTab is not null)
        {
            splitMenu.Items.Add(new Separator());
            var closeSplit = new MenuItem { Header = "Close split view" };
            closeSplit.Click += (_, _) => CloseSplitView();
            splitMenu.Items.Add(closeSplit);
        }
        menu.Items.Add(splitMenu);
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

        var blockingMenu = new MenuItem { Header = "Content blocking" };
        var blockingSettings = await _dataStore.GetSettingsAsync();
        var blockingToggle = new MenuItem
        {
            Header = "Enable domain blocking",
            IsCheckable = true,
            IsChecked = blockingSettings.ContentBlockingEnabled
        };
        blockingToggle.Click += async (_, _) =>
        {
            blockingSettings.ContentBlockingEnabled = blockingToggle.IsChecked;
            await _dataStore.SaveSettingsAsync(blockingSettings);
            await ApplyContentBlockingSettingsAsync();
            StatusText.Text = blockingToggle.IsChecked ? "Domain blocking enabled" : "Domain blocking disabled";
        };
        blockingMenu.Items.Add(blockingToggle);

        var blockCurrent = new MenuItem { Header = "Block current website" };
        blockCurrent.Click += async (_, _) =>
        {
            if (!TryGetActiveHost(out var host)) { StatusText.Text = "Open a website first"; return; }
            try
            {
                await _dataStore.AddBlockedDomainAsync(host);
                await ApplyContentBlockingSettingsAsync();
                await ReloadActivePageAsync();
                StatusText.Text = $"Blocked {host}";
            }
            catch (ArgumentException ex) { MessageBox.Show(ex.Message, "Content blocking"); }
        };
        blockingMenu.Items.Add(blockCurrent);

        var allowCurrent = new MenuItem { Header = "Allow current website" };
        allowCurrent.Click += async (_, _) =>
        {
            if (!TryGetActiveHost(out var host)) { StatusText.Text = "Open a website first"; return; }
            try
            {
                await _dataStore.AddAllowedDomainAsync(host);
                await ApplyContentBlockingSettingsAsync();
                await ReloadActivePageAsync();
                StatusText.Text = $"Added exception for {host}";
            }
            catch (ArgumentException ex) { MessageBox.Show(ex.Message, "Content blocking"); }
        };
        blockingMenu.Items.Add(allowCurrent);

        if (blockingSettings.BlockedDomains.Count > 0)
        {
            var blockedList = new MenuItem { Header = "Blocked domains" };
            foreach (var domain in blockingSettings.BlockedDomains)
            {
                var remove = new MenuItem { Header = $"Unblock {domain}", Tag = domain };
                remove.Click += async (_, _) =>
                {
                    await _dataStore.RemoveBlockedDomainAsync(domain);
                    await ApplyContentBlockingSettingsAsync();
                    await ReloadActivePageAsync();
                    StatusText.Text = $"Unblocked {domain}";
                };
                blockedList.Items.Add(remove);
            }
            blockingMenu.Items.Add(blockedList);
        }
        if (blockingSettings.AllowedDomains.Count > 0)
        {
            var exceptions = new MenuItem { Header = "Allowed exceptions" };
            foreach (var domain in blockingSettings.AllowedDomains)
            {
                var remove = new MenuItem { Header = $"Remove exception for {domain}", Tag = domain };
                remove.Click += async (_, _) =>
                {
                    await _dataStore.RemoveAllowedDomainAsync(domain);
                    await ApplyContentBlockingSettingsAsync();
                    await ReloadActivePageAsync();
                    StatusText.Text = $"Removed exception for {domain}";
                };
                exceptions.Items.Add(remove);
            }
            blockingMenu.Items.Add(exceptions);
        }
        menu.Items.Add(blockingMenu);

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

        var recentlyClosedMenu = new MenuItem { Header = "Recently closed tabs" };
        if (!_isPrivate)
        {
            foreach (var closedTab in await _dataStore.GetRecentlyClosedAsync())
            {
                var closedItem = new MenuItem
                {
                    Header = string.IsNullOrWhiteSpace(closedTab.Title) ? closedTab.Address : closedTab.Title,
                    Tag = closedTab.Address,
                    ToolTip = closedTab.Address
                };
                closedItem.Click += async (_, _) => await RestoreClosedTabAsync(closedTab.Address);
                recentlyClosedMenu.Items.Add(closedItem);
            }
        }
        if (recentlyClosedMenu.Items.Count == 0)
            recentlyClosedMenu.Items.Add(new MenuItem { Header = "No recently closed tabs", IsEnabled = false });
        menu.Items.Add(recentlyClosedMenu);

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

    private bool TryGetActiveHost(out string host)
    {
        host = string.Empty;
        return _active is not null &&
               Uri.TryCreate(_active.Address, UriKind.Absolute, out var uri) &&
               (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps) &&
               (host = uri.Host).Length > 0;
    }

    private async Task ReloadActivePageAsync()
    {
        if (_active is not null && _engines.TryGetValue(_active.Id, out var engine))
            await engine.ReloadAsync();
    }

    private async Task ApplyContentBlockingSettingsAsync()
    {
        var settings = await _dataStore.GetSettingsAsync();
        foreach (var engine in _engines.Values)
            await engine.ConfigureContentBlockingAsync(settings);
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

    private async Task RestoreClosedTabAsync(string? targetAddress = null)
    {
        if (_isPrivate) { StatusText.Text = "Recently closed tabs are unavailable in private windows"; return; }
        var recentlyClosed = await _dataStore.GetRecentlyClosedAsync();
        if (recentlyClosed.Count == 0)
        {
            StatusText.Text = "No recently closed tabs";
            return;
        }

        var saved = targetAddress is null
            ? recentlyClosed[0]
            : recentlyClosed.FirstOrDefault(item => item.Address == targetAddress);
        if (saved is null) return;
        await _dataStore.RemoveRecentlyClosedAsync(saved.Address);
        var restoreSpaceId = _spaces.Any(space => space.Id == saved.SpaceId) ? saved.SpaceId : _activeSpaceId;
        if (restoreSpaceId != _activeSpaceId)
        {
            _activeSpaceId = restoreSpaceId;
            SpaceSelector.SelectedValue = _activeSpaceId;
            _tabView.Refresh();
        }
        var tab = new BrowserTab { Address = saved.Address, Title = saved.Title, IsPinned = saved.IsPinned, SpaceId = restoreSpaceId };
        _tabs.Add(tab);
        SelectTab(tab);
        StatusText.Text = "Tab restored";
    }

    private async Task SaveSessionQuietlyAsync()
    {
        try { await SaveSessionAsync(); }
        catch (IOException) { if (!_isPrivate) StatusText.Text = "Session could not be saved"; }
    }

    private async Task SaveSessionAsync()
    {
        if (_restoring || _isPrivate) return;
        var snapshots = _tabs.Select(tab => new BrowserSessionTab(tab.Address, tab.Title, tab.IsPinned, tab.SpaceId)).ToList();
        var activeIndex = _active is null ? 0 : Math.Max(0, _tabs.IndexOf(_active));
        await _dataStore.SaveSessionAsync(snapshots, activeIndex, _spaces.ToList(), _activeSpaceId);
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
        else if (e.Key >= Key.D1 && e.Key <= Key.D8)
        {
            var visibleTabs = _tabs.Where(tab => tab.SpaceId == _activeSpaceId).ToList();
            var targetIndex = (int)e.Key - (int)Key.D1;
            if (targetIndex < visibleTabs.Count) SelectTab(visibleTabs[targetIndex]);
            e.Handled = true;
        }
        else if (e.Key == Key.D9)
        {
            var visibleTabs = _tabs.Where(tab => tab.SpaceId == _activeSpaceId).ToList();
            if (visibleTabs.Count > 0) SelectTab(visibleTabs[^1]);
            e.Handled = true;
        }
        else if (e.Key == Key.Tab)
        {
            var visibleTabs = _tabs.Where(tab => tab.SpaceId == _activeSpaceId).ToList();
            if (visibleTabs.Count > 1)
            {
                var index = _active is null ? 0 : visibleTabs.IndexOf(_active);
                var backwards = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
                SelectTab(visibleTabs[(index + (backwards ? -1 : 1) + visibleTabs.Count) % visibleTabs.Count]);
            }
            e.Handled = true;
        }
    }

    private async void Window_Closed(object? sender, EventArgs e)
    {
        try { await SaveSessionAsync(); }
        catch (IOException) { /* A failed session write must not prevent shutdown. */ }
        BrowserHost.Children.Clear();
        foreach (var pending in _engineInitializations.Values.ToList())
        {
            try { await pending; }
            catch { /* Continue shutdown after an individual tab initialization failure. */ }
        }
        foreach (var engine in _engines.Values) await engine.DisposeAsync();
        _engines.Clear();
        _privateLock?.Dispose();
        _privateLock = null;
        if (_privateDataRoot is not null && Directory.Exists(_privateDataRoot))
        {
            try { Directory.Delete(_privateDataRoot, recursive: true); }
            catch (IOException) { /* Temporary private data is cleaned up on normal close where Windows permits it. */ }
            catch (UnauthorizedAccessException) { /* Do not block shutdown if the OS still holds a profile file. */ }
        }
    }
}
