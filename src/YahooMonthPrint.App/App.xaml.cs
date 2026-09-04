using System.Windows;

namespace YahooMonthPrint.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        MainWindow = CreateMainWindow();
        // This internal flag lets build validation exercise XAML startup without displaying a window.
        if (e.Args.Contains("--smoke-test", StringComparer.Ordinal))
        {
            MainWindow.Dispatcher.InvokeAsync(() => Shutdown(0));
            return;
        }

        MainWindow.Show();
    }

    private static MainWindow CreateMainWindow()
    {
        // Keep object construction here. Later phases can replace concrete services
        // with deterministic fakes without introducing a service-locator pattern.
        return new MainWindow();
    }
}
