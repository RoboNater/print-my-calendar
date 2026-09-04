using System.Windows;
using YahooMonthPrint.App.Services;
using YahooMonthPrint.App.ViewModels;

namespace YahooMonthPrint.App;

public partial class App : Application
{
    private JsonSettingsStore settingsStore = null!;
    private ICalendarCacheStore cacheStore = null!;
    private WindowsCredentialStore credentialStore = null!;
    private IYahooCalDavClientFactory clientFactory = null!;
    private RotatingFileAppLogger logger = null!;
    private YahooConnectionService connectionService = null!;
    private YahooAccountService accountService = null!;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // This internal flag lets build validation exercise XAML startup without displaying a window.
        if (e.Args.Contains("--smoke-test", StringComparer.Ordinal))
        {
            MainWindow = CreateDemoWindow();
            _ = MainWindow.Dispatcher.InvokeAsync(() => Shutdown(0));
            return;
        }

        if (e.Args.Contains("--demo", StringComparer.Ordinal))
        {
            MainWindow = CreateDemoWindow();
            MainWindow.Show();
            return;
        }

        ConfigureServices();
        try
        {
            var settings = await settingsStore.LoadAsync();
            if (!IsConfigured(settings) && !ShowSetupWizard())
            {
                Shutdown(0);
                return;
            }

            settings = await settingsStore.LoadAsync();
            ShowMainWindow(settings);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            logger.Log("startup", "failed", exception: exception);
            MessageBox.Show(
                $"Yahoo Month Print could not start. {exception.Message}",
                "Yahoo Month Print",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    private void ConfigureServices()
    {
        settingsStore = new JsonSettingsStore();
        cacheStore = new CalendarCacheStore();
        credentialStore = new WindowsCredentialStore();
        clientFactory = new YahooCalDavClientFactory();
        logger = new RotatingFileAppLogger();
        connectionService = new YahooConnectionService(clientFactory, logger);
        accountService = new YahooAccountService(credentialStore, settingsStore, cacheStore);
    }

    private bool IsConfigured(ApplicationSettings settings) =>
        !string.IsNullOrWhiteSpace(settings.YahooAccount)
        && settings.Calendars.Count > 0
        && !string.IsNullOrEmpty(credentialStore.Read(settings.YahooAccount));

    private bool ShowSetupWizard()
    {
        var wizard = new SetupWizardWindow(connectionService, accountService);
        return wizard.ShowDialog() == true;
    }

    private void ShowMainWindow(ApplicationSettings settings)
    {
        var source = new YahooCalendarOccurrenceSource(
            settings,
            credentialStore,
            settingsStore,
            cacheStore,
            clientFactory,
            logger);
        var viewModel = new MainWindowViewModel(source)
        {
            SelectedDetailLevel = settings.DetailLevel,
            MaximumDescriptionLines = settings.MaximumDescriptionLines,
            ShowLocations = settings.ShowLocations,
        };
        var window = new MainWindow(viewModel);
        window.SettingsRequested += OnSettingsRequested;
        MainWindow = window;
        window.Show();
    }

    private async void OnSettingsRequested(object? sender, EventArgs e)
    {
        if (sender is not MainWindow currentWindow)
        {
            return;
        }

        var settings = await settingsStore.LoadAsync();
        var settingsWindow = new SettingsWindow(
            settings,
            settingsStore,
            cacheStore,
            credentialStore,
            accountService,
            connectionService)
        {
            Owner = currentWindow,
        };
        if (settingsWindow.ShowDialog() != true)
        {
            return;
        }

        if (settingsWindow.WasDisconnected && !ShowSetupWizard())
        {
            currentWindow.Close();
            Shutdown(0);
            return;
        }

        settings = await settingsStore.LoadAsync();
        ShowMainWindow(settings);
        currentWindow.Close();
    }

    private static MainWindow CreateDemoWindow()
    {
        var source = new FakeCalendarOccurrenceSource();
        var viewModel = new MainWindowViewModel(source);
        return new MainWindow(viewModel);
    }
}
