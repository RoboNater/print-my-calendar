using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using YahooMonthPrint.App.Services;
using YahooMonthPrint.App.ViewModels;
using YahooMonthPrint.Core;
using YahooMonthPrint.Printing;
using YahooMonthPrint.YahooCalDav;

namespace YahooMonthPrint.App.Tests;

public sealed class MainWindowLifecycleTests
{
    [Fact]
    public void MainWindowClosesCleanlyWhenFlushCompletesSynchronously()
    {
        RunInSta(() =>
        {
            var source = new FakeCalendarOccurrenceSource();
            using var viewModel = new MainWindowViewModel(source);
            var window = new MainWindow(viewModel);
            var closed = false;
            window.Closed += (_, _) => closed = true;

            window.Show();
            window.Close();
            Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);

            Assert.True(closed);
        });
    }

    [Fact]
    public void MainWindowDefersCloseUntilPendingFlushCompletes()
    {
        RunInSta(() =>
        {
            var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var source = new TestPendingChangesOccurrenceSource(tcs.Task);
            using var viewModel = new MainWindowViewModel(source);
            var window = new MainWindow(viewModel);
            var closed = false;
            window.Closed += (_, _) => closed = true;

            window.Show();
            window.Close();

            Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
            Assert.False(closed);

            tcs.SetResult();
            PumpDispatcher(() => closed);

            Assert.True(closed);
        });
    }

    [Fact]
    public void SettingsSaveFlowReplacesMainWindowWithoutCrash()
    {
        RunInSta(() =>
        {
            var source1 = new FakeCalendarOccurrenceSource();
            using var viewModel1 = new MainWindowViewModel(source1);
            var window1 = new MainWindow(viewModel1);
            window1.Show();

            var source2 = new FakeCalendarOccurrenceSource();
            using var viewModel2 = new MainWindowViewModel(source2);
            var window2 = new MainWindow(viewModel2);
            window2.Show();

            var closed1 = false;
            window1.Closed += (_, _) => closed1 = true;
            window1.Close();

            Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);

            Assert.True(closed1);
            Assert.True(window2.IsLoaded);

            window2.Close();
            Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
        });
    }

    [Fact]
    public void SavingSettingsFromPrintingTabReplacesMainWindowCleanly()
    {
        RunInSta(() =>
        {
            var initialSettings = new ApplicationSettings
            {
                YahooAccount = "student@example.test",
                Calendars =
                [
                    new SavedCalendar(
                        "college",
                        "College",
                        "https://calendar.example.test/college/",
                        "#325EA8",
                        true),
                ],
                PaperSize = "Letter",
                Orientation = "Landscape",
                OverflowPolicy = PrintOverflowPolicy.ReduceDetailAutomatically,
            };
            var settingsStore = new InMemorySettingsStore(initialSettings);
            var credentialStore = new InMemoryCredentialStore();
            credentialStore.Write("student@example.test", "secret-app-password");
            var cacheStore = new InMemoryCacheStore();
            var accountService = new YahooAccountService(credentialStore, settingsStore, cacheStore);
            var connectionService = new YahooConnectionService(new YahooCalDavClientFactory(), new NullAppLogger());

            var source1 = new FakeCalendarOccurrenceSource();
            using var viewModel1 = new MainWindowViewModel(source1);
            var mainWindow = new MainWindow(viewModel1, initialSettings);
            mainWindow.Show();

            var settingsWindow = new SettingsWindow(
                initialSettings,
                settingsStore,
                cacheStore,
                credentialStore,
                accountService,
                connectionService)
            {
                Owner = mainWindow,
            };

            settingsWindow.Loaded += (_, _) =>
            {
                // Switch to Print tab (Tab 3: "Printing")
                var grid = Assert.IsType<Grid>(settingsWindow.Content);
                var tabControl = Assert.IsType<TabControl>(grid.Children[0]);
                tabControl.SelectedIndex = 3;

                var selectedTab = Assert.IsType<TabItem>(tabControl.SelectedItem);
                Assert.Equal("Printing", selectedTab.Header);

                // Find and click the Save button
                var buttonPanel = Assert.IsType<StackPanel>(grid.Children[1]);
                var saveButton = buttonPanel.Children.OfType<Button>().Single(b => (string)b.Content == "Save");

                saveButton.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            };

            var dialogResult = settingsWindow.ShowDialog();
            Assert.True(dialogResult);

            // Replicate App.ShowSettingsAsync sequence:
            var updatedSettings = settingsStore.LoadAsync().GetAwaiter().GetResult();
            Assert.Equal(initialSettings.PaperSize, updatedSettings.PaperSize);
            Assert.Equal(initialSettings.Orientation, updatedSettings.Orientation);

            var source2 = new FakeCalendarOccurrenceSource();
            using var viewModel2 = new MainWindowViewModel(source2);
            var newMainWindow = new MainWindow(viewModel2, updatedSettings);
            newMainWindow.Show();

            // Close the previous main window (this is what crashed prior to the fix)
            var oldWindowClosed = false;
            mainWindow.Closed += (_, _) => oldWindowClosed = true;
            mainWindow.Close();

            PumpDispatcher(() => oldWindowClosed);

            Assert.True(oldWindowClosed);
            Assert.True(newMainWindow.IsLoaded);

            newMainWindow.Close();
            PumpDispatcher();
        });
    }

    private static void RunInSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure is not null)
        {
            throw new InvalidOperationException("STA thread test failed.", failure);
        }
    }

    private static void PumpDispatcher(Func<bool>? condition = null)
    {
        var start = DateTime.UtcNow;
        while ((condition is null || !condition()) && DateTime.UtcNow - start < TimeSpan.FromSeconds(2))
        {
            var frame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(
                DispatcherPriority.Background,
                (DispatcherOperationCallback)(f =>
                {
                    ((DispatcherFrame)f).Continue = false;
                    return null;
                }),
                frame);
            Dispatcher.PushFrame(frame);
            if (condition is null)
            {
                break;
            }
        }
    }

    private sealed class TestPendingChangesOccurrenceSource(Task flushTask)
        : ICalendarOccurrenceSource, IPendingCalendarChanges
    {
        public IReadOnlyList<CalendarSource> Calendars => [];

        public Task<CalendarLoadResult> LoadAsync(
            MonthGridRange range,
            CancellationToken cancellationToken) =>
            Task.FromResult(new CalendarLoadResult([], DateTimeOffset.Now));

        public Task FlushPendingChangesAsync() => flushTask;
    }

    private sealed class InMemorySettingsStore(ApplicationSettings settings) : ISettingsStore
    {
        private ApplicationSettings current = settings;

        public Task<ApplicationSettings> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(current);

        public Task SaveAsync(ApplicationSettings value, CancellationToken cancellationToken = default)
        {
            current = value;
            return Task.CompletedTask;
        }

        public Task ClearAsync(CancellationToken cancellationToken = default)
        {
            current = new ApplicationSettings();
            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryCredentialStore : ICredentialStore
    {
        private readonly Dictionary<string, string> values = new(StringComparer.OrdinalIgnoreCase);

        public string? Read(string accountName) => values.GetValueOrDefault(accountName);

        public void Write(string accountName, string appPassword) => values[accountName] = appPassword;

        public void Delete(string accountName) => values.Remove(accountName);
    }

    private sealed class InMemoryCacheStore : ICalendarCacheStore
    {
        public Task ClearAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<CalendarLoadResult?> TryReadAsync(
            MonthGridRange range,
            IReadOnlyCollection<string> calendarIds,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<CalendarLoadResult?>(null);

        public Task WriteAsync(
            MonthGridRange range,
            IReadOnlyCollection<string> calendarIds,
            CalendarLoadResult result,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
