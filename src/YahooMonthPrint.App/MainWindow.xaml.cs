using System.Windows;
using YahooMonthPrint.App.Services;
using YahooMonthPrint.App.ViewModels;

namespace YahooMonthPrint.App;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel viewModel;
    private readonly ApplicationSettings printSettings;
    private bool initialized;

    public MainWindow(MainWindowViewModel viewModel, ApplicationSettings? printSettings = null)
    {
        this.viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        this.printSettings = printSettings ?? new ApplicationSettings();
        DataContext = viewModel;
        InitializeComponent();
        Loaded += OnLoaded;
        Closed += OnClosed;
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

    private void OnClosed(object? sender, EventArgs e)
    {
        viewModel.FlushPendingChangesAsync().GetAwaiter().GetResult();
        viewModel.Dispose();
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
                options)
            {
                Owner = this,
            };
            preview.ShowDialog();
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
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
