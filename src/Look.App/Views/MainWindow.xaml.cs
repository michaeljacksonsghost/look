using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Look.Models;
using Look.Services;

namespace Look.Views;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<BrowserTab> _tabs = new();
    private readonly Dictionary<Guid, IBrowserEngine> _engines = new();
    private readonly string _profileFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "look", "WebView2");
    private BrowserTab? _active;

    public MainWindow()
    {
        InitializeComponent();
        TabsList.ItemsSource = _tabs;
        AddTab();
    }

    private void AddTab()
    {
        var tab = new BrowserTab();
        _tabs.Add(tab);
        SelectTab(tab);
    }

    private void SelectTab(BrowserTab tab)
    {
        _active = tab;
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
        if (_engines.TryGetValue(tab.Id, out var existing))
            return existing;

        var engine = new WebView2BrowserEngine();
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
            if (_active?.Id == tab.Id) StatusText.Text = loading ? "Loading" : "Ready";
        };

        try
        {
            await engine.InitializeAsync(_profileFolder);
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
        if (e.Key == Key.Enter)
        {
            _ = NavigateAsync(AddressBox.Text);
            e.Handled = true;
        }
    }

    private void WelcomeAddress_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            _ = NavigateAsync(WelcomeAddress.Text);
            e.Handled = true;
        }
    }

    private void NewTab_Click(object sender, RoutedEventArgs e)
    {
        AddTab();
        WelcomeAddress.Focus();
    }

    private void SelectTab_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is BrowserTab tab)
            SelectTab(tab);
    }

    private async void CloseTab_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not BrowserTab tab) return;

        var index = _tabs.IndexOf(tab);
        if (_active?.Id == tab.Id)
        {
            BrowserHost.Children.Clear();
            BrowserHost.Visibility = Visibility.Collapsed;
        }

        _tabs.Remove(tab);
        if (_engines.Remove(tab.Id, out var engine))
            await engine.DisposeAsync();

        if (_tabs.Count == 0)
            AddTab();
        else if (_active?.Id == tab.Id)
            SelectTab(_tabs[Math.Clamp(index - 1, 0, _tabs.Count - 1)]);
    }

    private async void Back_Click(object sender, RoutedEventArgs e)
    {
        if (_active is not null && _engines.TryGetValue(_active.Id, out var engine))
            await engine.GoBackAsync();
    }

    private async void Forward_Click(object sender, RoutedEventArgs e)
    {
        if (_active is not null && _engines.TryGetValue(_active.Id, out var engine))
            await engine.GoForwardAsync();
    }

    private async void Reload_Click(object sender, RoutedEventArgs e)
    {
        if (_active is not null && _engines.TryGetValue(_active.Id, out var engine))
            await engine.ReloadAsync();
    }

    private void Menu_Click(object sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu();
        var quit = new MenuItem { Header = "Quit look" };
        quit.Click += (_, _) => Close();
        menu.Items.Add(quit);
        menu.IsOpen = true;
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Control) == 0) return;

        if (e.Key == Key.L)
        {
            AddressBox.Focus();
            AddressBox.SelectAll();
            e.Handled = true;
        }
        else if (e.Key == Key.T)
        {
            AddTab();
            WelcomeAddress.Focus();
            e.Handled = true;
        }
        else if (e.Key == Key.W)
        {
            if (_active is not null)
                CloseTab_Click(new Button { Tag = _active }, new RoutedEventArgs());
            e.Handled = true;
        }
        else if (e.Key == Key.R)
        {
            Reload_Click(this, new RoutedEventArgs());
            e.Handled = true;
        }
        else if (e.Key == Key.Tab && _tabs.Count > 1)
        {
            var index = _active is null ? 0 : _tabs.IndexOf(_active);
            var backwards = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
            var next = (index + (backwards ? -1 : 1) + _tabs.Count) % _tabs.Count;
            SelectTab(_tabs[next]);
            e.Handled = true;
        }
    }

    private async void Window_Closed(object? sender, EventArgs e)
    {
        foreach (var engine in _engines.Values)
            await engine.DisposeAsync();
        _engines.Clear();
    }
}
