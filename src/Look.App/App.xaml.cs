using System.Windows;

namespace Look;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        AppPaths.Initialize(e.Args);
        var window = new Views.MainWindow();
        MainWindow = window;
        window.Show();
    }
}
