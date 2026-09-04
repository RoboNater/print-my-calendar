using System.Windows;
using YahooMonthPrint.App.ViewModels;

namespace YahooMonthPrint.App;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel viewModel;
    private bool initialized;

    public MainWindow(MainWindowViewModel viewModel)
    {
        this.viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
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

    private void OnClosed(object? sender, EventArgs e) => viewModel.Dispose();

    private void OnSettingsClicked(object sender, RoutedEventArgs e) =>
        SettingsRequested?.Invoke(this, EventArgs.Empty);

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
