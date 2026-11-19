using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Data;
using Look.Models;

namespace Look.Views;

public partial class TabSwitcherWindow : Window
{
    private readonly ICollectionView _view;

    public BrowserTab? SelectedTab { get; private set; }

    public TabSwitcherWindow(IEnumerable<BrowserTab> tabs)
    {
        InitializeComponent();
        _view = CollectionViewSource.GetDefaultView(tabs);
        _view.Filter = FilterTab;
        TabsList.ItemsSource = _view;
        Loaded += (_, _) =>
        {
            SearchBox.Focus();
            if (_view.Cast<object>().FirstOrDefault() is BrowserTab first)
                TabsList.SelectedItem = first;
        };
    }

    private bool FilterTab(object item)
    {
        if (item is not BrowserTab tab) return false;
        var query = SearchBox?.Text?.Trim();
        if (string.IsNullOrWhiteSpace(query)) return true;
        return tab.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
               tab.Address.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _view.Refresh();
        TabsList.SelectedItem = _view.Cast<object>().FirstOrDefault();
    }

    private void SearchBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) SelectCurrent();
        else if (e.Key == Key.Down)
        {
            TabsList.Focus();
            if (TabsList.SelectedItem is null && TabsList.Items.Count > 0) TabsList.SelectedIndex = 0;
        }
    }

    private void TabsList_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) SelectCurrent();
    }

    private void TabsList_MouseDoubleClick(object sender, MouseButtonEventArgs e) => SelectCurrent();

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) DialogResult = false;
    }

    private void SelectCurrent()
    {
        if (TabsList.SelectedItem is not BrowserTab tab) return;
        SelectedTab = tab;
        DialogResult = true;
    }
}
