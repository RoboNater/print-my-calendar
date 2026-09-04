using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using YahooMonthPrint.App.Services;
using YahooMonthPrint.Core;

namespace YahooMonthPrint.App;

public partial class SettingsWindow : Window
{
    private readonly ISettingsStore settingsStore;
    private readonly ICalendarCacheStore cacheStore;
    private readonly ICredentialStore credentialStore;
    private readonly YahooAccountService accountService;
    private readonly YahooConnectionService connectionService;
    private readonly ObservableCollection<SettingsCalendarChoice> calendars;
    private ApplicationSettings settings;

    public SettingsWindow(
        ApplicationSettings settings,
        ISettingsStore settingsStore,
        ICalendarCacheStore cacheStore,
        ICredentialStore credentialStore,
        YahooAccountService accountService,
        YahooConnectionService connectionService)
    {
        this.settings = settings;
        this.settingsStore = settingsStore;
        this.cacheStore = cacheStore;
        this.credentialStore = credentialStore;
        this.accountService = accountService;
        this.connectionService = connectionService;
        calendars = new ObservableCollection<SettingsCalendarChoice>(
            settings.Calendars.Select(calendar => new SettingsCalendarChoice(calendar)));

        InitializeComponent();
        AccountText.Text = settings.YahooAccount ?? "Not connected";
        CalendarsList.ItemsSource = calendars;
        SelectComboItem(DetailLevelCombo, settings.DetailLevel.ToString());
        SelectComboItem(
            DescriptionLinesCombo,
            settings.MaximumDescriptionLines.ToString(System.Globalization.CultureInfo.InvariantCulture));
        SelectComboItem(PaperSizeCombo, settings.PaperSize);
        SelectComboItem(OrientationCombo, settings.Orientation);
        ShowLocationsCheckBox.IsChecked = settings.ShowLocations;
    }

    public bool WasDisconnected { get; private set; }

    private async void OnTestConnection(object sender, RoutedEventArgs e)
    {
        try
        {
            var account = settings.YahooAccount;
            var password = string.IsNullOrWhiteSpace(account) ? null : credentialStore.Read(account);
            if (string.IsNullOrWhiteSpace(account) || string.IsNullOrEmpty(password))
            {
                ConnectionStatusText.Text = "No saved Yahoo connection is available.";
                return;
            }

            ConnectionStatusText.Text = "Connecting…";
            var discovered = await connectionService.DiscoverAsync(account, password, CancellationToken.None);
            ConnectionStatusText.Text = $"Connected successfully. Yahoo returned {discovered.Count} calendar(s).";
        }
        catch (CalendarLoadException exception)
        {
            ConnectionStatusText.Text = exception.Message;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            ConnectionStatusText.Text = "The connection could not be tested. Try again.";
        }
    }

    private void OnChangePassword(object sender, RoutedEventArgs e)
    {
        var account = settings.YahooAccount;
        if (string.IsNullOrWhiteSpace(account) || NewPasswordBox.Password.Length == 0)
        {
            ConnectionStatusText.Text = "Enter a new Yahoo app password first.";
            return;
        }

        try
        {
            accountService.ChangePassword(account, NewPasswordBox.Password);
            NewPasswordBox.Clear();
            ConnectionStatusText.Text = "The new app password was saved securely.";
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            ConnectionStatusText.Text = "The app password could not be saved. Try again.";
        }
    }

    private async void OnDisconnect(object sender, RoutedEventArgs e)
    {
        var account = settings.YahooAccount;
        if (string.IsNullOrWhiteSpace(account))
        {
            return;
        }

        var choice = MessageBox.Show(
            this,
            "Disconnect this Yahoo account? The locally saved password, settings, and cache will be removed. The app password is not revoked at Yahoo.",
            "Disconnect Yahoo Account",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (choice != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            await accountService.DisconnectAsync(account, CancellationToken.None);
            WasDisconnected = true;
            DialogResult = true;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            ConnectionStatusText.Text = "The account could not be disconnected. Try again.";
        }
    }

    private async void OnClearCache(object sender, RoutedEventArgs e)
    {
        try
        {
            await cacheStore.ClearAsync(CancellationToken.None);
            PrivacyStatusText.Text = "Cached calendar data was cleared. Your Yahoo connection is still saved.";
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            PrivacyStatusText.Text = "Cached calendar data could not be cleared. Try again.";
        }
    }

    private async void OnSave(object sender, RoutedEventArgs e)
    {
        if (!calendars.Any(calendar => calendar.IsSelected))
        {
            MessageBox.Show(this, "Choose at least one calendar.", "Yahoo Month Print");
            return;
        }

        try
        {
            settings = settings with
            {
                Calendars = calendars.Select(calendar => calendar.Value with
                {
                    IsSelected = calendar.IsSelected,
                }).ToArray(),
                DetailLevel = Enum.Parse<DetailLevel>(SelectedValue(DetailLevelCombo)),
                MaximumDescriptionLines = int.Parse(
                    SelectedValue(DescriptionLinesCombo),
                    System.Globalization.CultureInfo.InvariantCulture),
                ShowLocations = ShowLocationsCheckBox.IsChecked == true,
                PaperSize = SelectedValue(PaperSizeCombo),
                Orientation = SelectedValue(OrientationCombo),
            };
            await settingsStore.SaveAsync(settings, CancellationToken.None);
            DialogResult = true;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            MessageBox.Show(
                this,
                "Settings could not be saved. Try again.",
                "Yahoo Month Print",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private static void SelectComboItem(ComboBox comboBox, string value)
    {
        comboBox.SelectedItem = comboBox.Items
            .OfType<ComboBoxItem>()
            .FirstOrDefault(item => string.Equals(
                item.Tag as string ?? item.Content?.ToString(),
                value,
                StringComparison.Ordinal));
        comboBox.SelectedIndex = comboBox.SelectedIndex < 0 ? 0 : comboBox.SelectedIndex;
    }

    private static string SelectedValue(ComboBox comboBox)
    {
        var selected = (ComboBoxItem)comboBox.SelectedItem;
        return selected.Tag as string ?? selected.Content?.ToString() ?? string.Empty;
    }
}

public sealed class SettingsCalendarChoice(SavedCalendar value)
{
    public SavedCalendar Value { get; } = value;

    public string DisplayName => Value.DisplayName;

    public bool IsSelected { get; set; } = value.IsSelected;
}
