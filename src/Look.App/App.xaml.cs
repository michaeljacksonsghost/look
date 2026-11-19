using System.Windows;
using Look.Services;

namespace Look;

public partial class App : Application
{
    public IBrowserEngineFactory BrowserEngineFactory { get; } = new WebView2BrowserEngineFactory();

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        AppPaths.Initialize(e.Args);
        var window = new Views.MainWindow(engineFactory: BrowserEngineFactory);
        MainWindow = window;
        window.Show();
    }
}
