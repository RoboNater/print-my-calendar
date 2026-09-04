using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using YahooMonthPrint.App.Services;
using YahooMonthPrint.YahooCalDav;

namespace YahooMonthPrint.App;

public partial class SetupWizardWindow : Window
{
    private static readonly Uri YahooSecurityUri = new("https://login.yahoo.com/account/security");
    private readonly YahooConnectionService connectionService;
    private readonly YahooAccountService accountService;
    private readonly ObservableCollection<SetupCalendarChoice> calendars = [];
    private int step;
    private bool connectionSucceeded;

    public SetupWizardWindow(
        YahooConnectionService connectionService,
        YahooAccountService accountService)
    {
        this.connectionService = connectionService;
        this.accountService = accountService;
        InitializeComponent();
        CalendarsList.ItemsSource = calendars;
    }

    private void OnPrimary(object sender, RoutedEventArgs e)
    {
        switch (step)
        {
            case 0:
                ShowStep(1);
                break;
            case 1:
                _ = ConnectAsync();
                break;
            case 2 when connectionSucceeded:
                ShowStep(3);
                break;
            case 2:
                ShowStep(1);
                break;
            case 3:
                _ = FinishAsync();
                break;
        }
    }

    private void OnBack(object sender, RoutedEventArgs e)
    {
        if (step == 2)
        {
            ShowStep(1);
        }
        else if (step == 3)
        {
            ShowStep(2);
        }
        else
        {
            ShowStep(Math.Max(0, step - 1));
        }
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        AppPasswordBox.Clear();
        DialogResult = false;
    }

    private void OnOpenYahooSecurity(object sender, RoutedEventArgs e) =>
        Process.Start(new ProcessStartInfo(YahooSecurityUri.AbsoluteUri) { UseShellExecute = true });

    private async Task ConnectAsync()
    {
        var account = AccountTextBox.Text.Trim();
        var password = AppPasswordBox.Password;
        if (account.Length == 0 || password.Length == 0)
        {
            AccountValidationText.Text = "Enter your Yahoo account name and a Yahoo app password.";
            return;
        }

        AccountValidationText.Text = string.Empty;
        ShowStep(2);
        SetBusy(true);
        ConnectionStatusText.Text = "Connecting…";
        ConnectionTechnicalText.Text = string.Empty;
        connectionSucceeded = false;
        try
        {
            var discovered = await connectionService.DiscoverAsync(
                account,
                password,
                CancellationToken.None);
            calendars.Clear();
            foreach (var calendar in discovered)
            {
                calendars.Add(new SetupCalendarChoice(calendar));
            }

            connectionSucceeded = true;
            ConnectionStatusText.Text = discovered.Count == 0
                ? "Connected, but Yahoo returned no calendar collections."
                : "Connected successfully.";
            ConnectionTechnicalText.Text = discovered.Count == 0
                ? "Try again after confirming that the account has at least one Yahoo calendar."
                : string.Empty;
        }
        catch (CalendarLoadException exception)
        {
            ConnectionStatusText.Text = exception.Message;
            ConnectionTechnicalText.Text = exception.TechnicalDetail ?? string.Empty;
        }
        finally
        {
            SetBusy(false);
            UpdateButtons();
        }
    }

    private async Task FinishAsync()
    {
        var selected = calendars
            .Where(calendar => calendar.IsSelected)
            .Select(calendar => calendar.Calendar.Id)
            .ToHashSet(StringComparer.Ordinal);
        if (selected.Count == 0)
        {
            CalendarValidationText.Text = "Choose at least one calendar.";
            return;
        }

        SetBusy(true);
        try
        {
            await accountService.SaveConnectionAsync(
                AccountTextBox.Text.Trim(),
                AppPasswordBox.Password,
                calendars.Select(calendar => calendar.Calendar).ToArray(),
                selected,
                CancellationToken.None);
            AppPasswordBox.Clear();
            DialogResult = true;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            CalendarValidationText.Text = $"The account could not be saved: {exception.Message}";
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void ShowStep(int value)
    {
        step = value;
        WelcomePanel.Visibility = value == 0 ? Visibility.Visible : Visibility.Collapsed;
        AccountPanel.Visibility = value == 1 ? Visibility.Visible : Visibility.Collapsed;
        ConnectionPanel.Visibility = value == 2 ? Visibility.Visible : Visibility.Collapsed;
        CalendarsPanel.Visibility = value == 3 ? Visibility.Visible : Visibility.Collapsed;
        UpdateButtons();
    }

    private void UpdateButtons()
    {
        BackButton.Visibility = step == 0 ? Visibility.Collapsed : Visibility.Visible;
        PrimaryButton.Content = step switch
        {
            0 => "Get Started",
            1 => "Connect to Yahoo",
            2 when connectionSucceeded => "Continue",
            2 => "Try Again",
            _ => "Finish",
        };
        PrimaryButton.IsEnabled = step != 2 || connectionSucceeded || ConnectionStatusText.Text != "Connecting…";
    }

    private void SetBusy(bool isBusy)
    {
        PrimaryButton.IsEnabled = !isBusy;
        BackButton.IsEnabled = !isBusy;
        CancelButton.IsEnabled = !isBusy;
    }
}

public sealed class SetupCalendarChoice : INotifyPropertyChanged
{
    private bool isSelected = true;

    public SetupCalendarChoice(CalDavCalendar calendar)
    {
        Calendar = calendar;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public CalDavCalendar Calendar { get; }

    public string DisplayName => Calendar.DisplayName;

    public bool IsSelected
    {
        get => isSelected;
        set
        {
            if (isSelected == value)
            {
                return;
            }

            isSelected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
        }
    }
}
