using System.ComponentModel;
using System.Windows;
using YahooMonthPrint.App.Services;
using YahooMonthPrint.App.ViewModels;

namespace YahooMonthPrint.App;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel viewModel;
    private readonly ApplicationSettings printSettings;
    private readonly IAppLogger logger;
    private bool initialized;
    private bool closeIsPending;
    private bool mayClose;

    public MainWindow(
        MainWindowViewModel viewModel,
        ApplicationSettings? printSettings = null,
        IAppLogger? logger = null)
    {
        this.viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        this.printSettings = printSettings ?? new ApplicationSettings();
        this.logger = logger ?? new NullAppLogger();
        DataContext = viewModel;
        InitializeComponent();
        Loaded += OnLoaded;
        Closing += OnClosing;
    }

    public event EventHandler? SettingsRequested;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (initialized)
        {
            return;
        }

        initialized = true;
        _ = InitializeAsync();
    }

    private async void OnClosing(object? sender, CancelEventArgs e)
    {
        if (mayClose)
        {
            viewModel.Dispose();
            return;
        }

        e.Cancel = true;
        if (closeIsPending)
        {
            return;
        }

        closeIsPending = true;
        try
        {
            await viewModel.FlushPendingChangesAsync().WaitAsync(TimeSpan.FromSeconds(10));
        }
        catch (TimeoutException exception)
        {
            logger.Log("settings", "shutdown-flush-timeout", exception: exception);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            logger.Log("settings", "shutdown-flush-failed", exception: exception);
        }
        finally
        {
            mayClose = true;
            closeIsPending = false;
            Close();
        }
    }

    private void OnSettingsClicked(object sender, RoutedEventArgs e) =>
        SettingsRequested?.Invoke(this, EventArgs.Empty);

    private async void OnPrintClicked(object sender, RoutedEventArgs e)
    {
        try
        {
            await viewModel.PendingFilterUpdate;
            var options = PrintPreviewWindow.OptionsFromSettings(
                printSettings,
                viewModel.SelectedDetailLevel,
                viewModel.MaximumDescriptionLines,
                viewModel.ShowLocations);
            var preview = new PrintPreviewWindow(
                viewModel.DisplayedMonth,
                viewModel.VisibleOccurrences,
                options,
                logger)
            {
                Owner = this,
            };
            preview.ShowDialog();
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            logger.Log("printing", "preview-failed", exception: exception);
            MessageBox.Show(
                this,
                "The print preview could not be created. Try reducing the detail level and try again.",
                "Yahoo Month Print",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private async Task InitializeAsync()
    {
        try
        {
            await viewModel.InitializeAsync();
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            _ = exception;
        }
    }
}
