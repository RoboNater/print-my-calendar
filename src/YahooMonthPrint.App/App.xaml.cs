using System.IO;
using System.Windows;
using YahooMonthPrint.App.Services;
using YahooMonthPrint.App.ViewModels;
using YahooMonthPrint.Core;
using YahooMonthPrint.Printing;

namespace YahooMonthPrint.App;

public partial class App : Application, IDisposable
{
    private SerializedSettingsStore settingsStore = null!;
    private CalendarCacheStore cacheStore = null!;
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

        var sampleArgument = Array.IndexOf(e.Args, "--render-print-samples");
        if (sampleArgument >= 0)
        {
            var outputDirectory = sampleArgument + 1 < e.Args.Length
                ? Path.GetFullPath(e.Args[sampleArgument + 1])
                : Path.GetFullPath("artifacts/print-samples");
            var source = new FakeCalendarOccurrenceSource();
            var displayedMonth = new DateOnly(2026, 9, 1);
            var result = await source.LoadAsync(
                MonthGrid.Create(displayedMonth.Year, displayedMonth.Month),
                CancellationToken.None);
            foreach (var paper in new[] { PrintPaperSize.Letter, PrintPaperSize.A4 })
            {
                var options = new MonthPrintOptions
                {
                    Page = PrintPageGeometry.Create(paper),
                    DetailLevel = DetailLevel.Detailed,
                    DescriptionLineLimit = 3,
                };
                var model = MonthLayoutModelBuilder.Build(displayedMonth, result.Occurrences, options);
                var plan = new MonthPrintLayoutEngine().CreatePlan(model);
                var document = new FixedDocumentRenderer().Render(plan);
                FixedDocumentPngExporter.Export(
                    document,
                    outputDirectory,
                    $"september-2026-{paper.ToString().ToLowerInvariant()}");
            }

            Shutdown(0);
            return;
        }

        if (e.Args.Contains("--uninstall-cleanup", StringComparer.Ordinal))
        {
            try
            {
                ConfigureServices();
                var settings = await settingsStore.LoadAsync();
                if (!string.IsNullOrWhiteSpace(settings.YahooAccount))
                {
                    credentialStore.Delete(settings.YahooAccount);
                }

                await cacheStore.ClearAsync();
                await settingsStore.ClearAsync();
                RemoveLocalApplicationData();
                Shutdown(0);
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                logger?.Log("uninstall", "cleanup-failed", exception: exception);
                Shutdown(1);
            }

            return;
        }

        if (e.Args.Contains("--demo", StringComparer.Ordinal))
        {
            MainWindow = CreateDemoWindow();
            MainWindow.Show();
            return;
        }

        ConfigureServices();
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
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
                "Yahoo Month Print could not start. Restart the application and try again.",
                "Yahoo Month Print",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Dispose();
        base.OnExit(e);
    }

    public void Dispose()
    {
        settingsStore?.Dispose();
        GC.SuppressFinalize(this);
    }

    private void ConfigureServices()
    {
        settingsStore = new SerializedSettingsStore(new JsonSettingsStore());
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
        var window = new MainWindow(viewModel, settings, logger);
        window.SettingsRequested += OnSettingsRequested;
        MainWindow = window;
        window.Show();
        ShutdownMode = ShutdownMode.OnLastWindowClose;
    }

    private void OnSettingsRequested(object? sender, EventArgs e)
    {
        if (sender is MainWindow currentWindow)
        {
            _ = ShowSettingsAsync(currentWindow);
        }
    }

    private async Task ShowSettingsAsync(MainWindow currentWindow)
    {
        try
        {
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
                ShutdownMode = ShutdownMode.OnExplicitShutdown;
                await CloseWindowAsync(currentWindow);
                Shutdown(0);
                return;
            }

            settings = await settingsStore.LoadAsync();
            ShowMainWindow(settings);
            await CloseWindowAsync(currentWindow);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            logger.Log("settings", "failed", exception: exception);
            MessageBox.Show(
                currentWindow,
                "Settings could not be updated. Try again.",
                "Yahoo Month Print",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private static MainWindow CreateDemoWindow()
    {
        var source = new FakeCalendarOccurrenceSource();
        var viewModel = new MainWindowViewModel(source);
        return new MainWindow(viewModel);
    }

    private static Task CloseWindowAsync(Window window)
    {
        var completion = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        void OnClosed(object? sender, EventArgs e)
        {
            window.Closed -= OnClosed;
            completion.TrySetResult();
        }

        window.Closed += OnClosed;
        try
        {
            window.Close();
        }
        catch
        {
            window.Closed -= OnClosed;
            throw;
        }

        return completion.Task;
    }

    private static void RemoveLocalApplicationData()
    {
        var localApplicationData = Path.GetFullPath(Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData));
        var applicationData = Path.GetFullPath(AppStoragePaths.Root);
        if (!string.Equals(
                Path.GetDirectoryName(applicationData),
                localApplicationData,
                StringComparison.OrdinalIgnoreCase)
            || !string.Equals(
                Path.GetFileName(applicationData),
                "YahooMonthPrint",
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The local application data path is unexpected.");
        }

        if (Directory.Exists(applicationData))
        {
            Directory.Delete(applicationData, recursive: true);
        }
    }
}
