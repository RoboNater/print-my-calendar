using System.Windows;
using YahooMonthPrint.App.Services;
using YahooMonthPrint.App.ViewModels;

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
        var source = new FakeCalendarOccurrenceSource();
        var viewModel = new MainWindowViewModel(source);
        return new MainWindow(viewModel);
    }
}
