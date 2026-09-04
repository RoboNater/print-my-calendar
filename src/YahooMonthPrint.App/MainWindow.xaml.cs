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

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (initialized)
        {
            return;
        }

        initialized = true;
        await viewModel.InitializeAsync();
    }

    private void OnClosed(object? sender, EventArgs e) => viewModel.Dispose();
}
