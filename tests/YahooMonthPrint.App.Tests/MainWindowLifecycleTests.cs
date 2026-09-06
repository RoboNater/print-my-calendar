using System.Windows.Threading;
using YahooMonthPrint.App.Services;
using YahooMonthPrint.App.ViewModels;
using YahooMonthPrint.Core;

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
}
