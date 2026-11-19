using Microsoft.Web.WebView2.Core;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using Look.Models;
using Look.Services;
namespace Look.Views;
public partial class MainWindow : Window
{
 private readonly ObservableCollection<BrowserTab> _tabs=new(); private BrowserTab? _active; private bool _engineReady;
 public MainWindow(){InitializeComponent();TabsList.ItemsSource=_tabs;AddTab();Loaded+=async(_,_)=>await InitializeEngineAsync();}
 private void AddTab(){var tab=new BrowserTab();_tabs.Add(tab);SelectTab(tab);}
 private void SelectTab(BrowserTab tab){_active=tab;AddressBox.Text=tab.Address;WelcomePanel.Visibility=string.IsNullOrEmpty(tab.Address)?Visibility.Visible:Visibility.Collapsed;if(!string.IsNullOrEmpty(tab.Address)&&_engineReady&&Uri.TryCreate(tab.Address,UriKind.Absolute,out var uri)){BrowserView.Visibility=Visibility.Visible;BrowserView.Source=uri;}else BrowserView.Visibility=Visibility.Collapsed;}
 private async Task InitializeEngineAsync(){try{var folder=System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"look","WebView2");var env=await CoreWebView2Environment.CreateAsync(userDataFolder:folder);await BrowserView.EnsureCoreWebView2Async(env);_engineReady=true;BrowserView.CoreWebView2.DocumentTitleChanged+=(_,_)=>{if(_active!=null)_active.Title=BrowserView.CoreWebView2.DocumentTitle;};BrowserView.CoreWebView2.SourceChanged+=(_,_)=>{if(_active!=null&&BrowserView.Source!=null){_active.Address=BrowserView.Source.ToString();AddressBox.Text=_active.Address;}};BrowserView.CoreWebView2.NavigationStarting+=(_,_)=>StatusText.Text="Loading";BrowserView.CoreWebView2.NavigationCompleted+=(_,e)=>StatusText.Text=e.IsSuccess?"Ready":"Page could not be loaded";BrowserView.CoreWebView2.Settings.AreDefaultContextMenusEnabled=true;BrowserView.CoreWebView2.Settings.IsStatusBarEnabled=false;BrowserView.CoreWebView2.Settings.AreDevToolsEnabled=true;}catch(Exception ex){StatusText.Text="WebView2 runtime unavailable";MessageBox.Show("look needs the Microsoft Edge WebView2 Runtime to display websites.\n\n"+ex.Message,"look",MessageBoxButton.OK,MessageBoxImage.Warning);}}
 private void Navigate(string text){var uri=AddressParser.Parse(text);if(_active!=null)_active.Address=uri.ToString();AddressBox.Text=uri.ToString();WelcomePanel.Visibility=Visibility.Collapsed;if(_engineReady){BrowserView.Visibility=Visibility.Visible;BrowserView.Source=uri;}else StatusText.Text="Waiting for browser engine";}
 private void AddressBox_KeyDown(object sender,KeyEventArgs e){if(e.Key==Key.Enter){Navigate(AddressBox.Text);e.Handled=true;}}
 private void WelcomeAddress_KeyDown(object sender,KeyEventArgs e){if(e.Key==Key.Enter){Navigate(WelcomeAddress.Text);e.Handled=true;}}
 private void NewTab_Click(object sender,RoutedEventArgs e){AddTab();BrowserView.Visibility=Visibility.Collapsed;WelcomePanel.Visibility=Visibility.Visible;WelcomeAddress.Focus();}
 private void SelectTab_Click(object sender,RoutedEventArgs e){if((sender as FrameworkElement)?.Tag is BrowserTab tab)SelectTab(tab);}
 private void CloseTab_Click(object sender,RoutedEventArgs e){if((sender as FrameworkElement)?.Tag is BrowserTab tab){var index=_tabs.IndexOf(tab);_tabs.Remove(tab);if(_tabs.Count==0)AddTab();else SelectTab(_tabs[Math.Clamp(index-1,0,_tabs.Count-1)]);}}
 private void Back_Click(object sender,RoutedEventArgs e){if(_engineReady&&BrowserView.CoreWebView2.CanGoBack)BrowserView.CoreWebView2.GoBack();}
 private void Forward_Click(object sender,RoutedEventArgs e){if(_engineReady&&BrowserView.CoreWebView2.CanGoForward)BrowserView.CoreWebView2.GoForward();}
 private void Reload_Click(object sender,RoutedEventArgs e){if(_engineReady)BrowserView.CoreWebView2.Reload();}
 private void Menu_Click(object sender,RoutedEventArgs e){var menu=new System.Windows.Controls.ContextMenu();foreach(var item in new[]{ "History","Downloads","Extensions","Settings" })menu.Items.Add(new System.Windows.Controls.MenuItem{Header=item,IsEnabled=false});menu.Items.Add(new System.Windows.Controls.Separator());var quit=new System.Windows.Controls.MenuItem{Header="Quit look"};quit.Click+=(_,_)=>Close();menu.Items.Add(quit);menu.IsOpen=true;}
 private void Window_PreviewKeyDown(object sender,KeyEventArgs e){if((Keyboard.Modifiers&ModifierKeys.Control)!=0){if(e.Key==Key.L){AddressBox.Focus();AddressBox.SelectAll();e.Handled=true;}else if(e.Key==Key.T){AddTab();BrowserView.Visibility=Visibility.Collapsed;WelcomePanel.Visibility=Visibility.Visible;e.Handled=true;}else if(e.Key==Key.W){if(_active!=null)CloseTab_Click(new FrameworkElement{Tag=_active},new RoutedEventArgs());e.Handled=true;}else if(e.Key==Key.R){Reload_Click(this,new RoutedEventArgs());e.Handled=true;}}}
}
