using System.Collections.Specialized;
using YahooMonthPrint.App.Services;
using YahooMonthPrint.App.ViewModels;
using YahooMonthPrint.Core;

namespace YahooMonthPrint.App.Tests;

public sealed class MainWindowViewModelTests
{
    private static readonly DateOnly September = new(2026, 9, 1);

    [Fact]
    public async Task InitializeBuildsUsefulDeterministicOfflineMonth()
    {
        using var viewModel = CreateViewModel();

        await viewModel.RefreshAsync();

        Assert.Equal("September 2026", viewModel.DisplayedMonthLabel);
        Assert.Equal(35, viewModel.Days.Count);
        Assert.Equal(viewModel.RawOccurrenceCount, viewModel.VisibleOccurrenceCount);
        Assert.Equal(viewModel.VisibleOccurrences.Count, AllVisible(viewModel).Count);
        Assert.Contains(viewModel.TitleFilters, item => item.Name == "Calculus II");
        var exam = AllVisible(viewModel).Single(item => item.DescriptionText.Contains("EXAM 2", StringComparison.Ordinal));
        Assert.Contains("Bring calculator", exam.DescriptionText, StringComparison.Ordinal);
        Assert.Equal(3, exam.DescriptionText.Split(Environment.NewLine).Length);
    }

    [Fact]
    public async Task HideCommandHidesOnlyOneRecurringOccurrenceAndRestoreShowsIt()
    {
        using var viewModel = CreateViewModel();
        await viewModel.RefreshAsync();
        var calculus = AllVisible(viewModel).First(item => item.Title == "Calculus II");
        var originalCalculusCount = AllVisible(viewModel).Count(item => item.Title == "Calculus II");

        calculus.HideCommand.Execute(null);

        Assert.Single(viewModel.HiddenOccurrences);
        Assert.Equal(
            originalCalculusCount - 1,
            AllVisible(viewModel).Count(item => item.Title == "Calculus II"));

        viewModel.HiddenOccurrences[0].RestoreCommand.Execute(null);

        Assert.Empty(viewModel.HiddenOccurrences);
        Assert.Equal(originalCalculusCount, AllVisible(viewModel).Count(item => item.Title == "Calculus II"));
    }

    [Fact]
    public async Task DebouncedFilterUsesLatestTextAndBothModes()
    {
        using var viewModel = CreateViewModel();
        await viewModel.RefreshAsync();
        viewModel.IsShowOnlyMatching = true;

        viewModel.FilterText = "office";
        viewModel.FilterText = "exam";
        await viewModel.PendingFilterUpdate;

        Assert.Single(AllVisible(viewModel));
        Assert.Contains("EXAM 2", AllVisible(viewModel)[0].DescriptionText, StringComparison.Ordinal);

        viewModel.IsHideMatching = true;

        Assert.DoesNotContain(
            AllVisible(viewModel),
            item => item.DescriptionText.Contains("EXAM 2", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CalendarAndTitleOptionsUpdateAuthoritativeVisibleSet()
    {
        using var viewModel = CreateViewModel();
        await viewModel.RefreshAsync();
        var originalCount = viewModel.VisibleOccurrenceCount;

        viewModel.CalendarFilters.Single(item => item.Name == "Personal").IsEnabled = false;

        Assert.True(viewModel.VisibleOccurrenceCount < originalCount);
        Assert.DoesNotContain(AllVisible(viewModel), item => item.CalendarName == "Personal");

        viewModel.TitleFilters.Single(item => item.Name == "Physics").IsEnabled = false;

        Assert.DoesNotContain(AllVisible(viewModel), item => item.Title == "Physics");
    }

    [Fact]
    public async Task NavigationCrossesYearBoundaryAndRefreshesGrid()
    {
        using var viewModel = new MainWindowViewModel(
            new FakeCalendarOccurrenceSource(),
            new DateOnly(2026, 12, 1),
            TimeSpan.FromMilliseconds(1));
        await viewModel.RefreshAsync();

        await viewModel.NavigateAsync(1);

        Assert.Equal("January 2027", viewModel.DisplayedMonthLabel);
        Assert.Contains(viewModel.Days, day => day.Date.Year == 2026);
        Assert.Contains(viewModel.Days, day => day.Date.Year == 2027);
        Assert.True(viewModel.VisibleOccurrenceCount > 0);
    }

    [Fact]
    public async Task LaterNavigationCancelsAndSupersedesEarlierLoad()
    {
        var source = new ControllableSource();
        using var viewModel = new MainWindowViewModel(source, September);
        var firstLoad = viewModel.RefreshAsync();
        await source.FirstRequestStarted.Task;

        var navigation = viewModel.NavigateAsync(1);
        await Task.WhenAll(firstLoad, navigation);

        Assert.True(source.FirstRequestWasCancelled);
        Assert.Equal("October 2026", viewModel.DisplayedMonthLabel);
        Assert.Equal("October result", AllVisible(viewModel).Single().Title);
    }

    [Fact]
    public async Task UnexpectedSourceFailureProducesFriendlyStatusAndClearsLoading()
    {
        using var viewModel = new MainWindowViewModel(
            new ThrowingSource(),
            September,
            TimeSpan.FromMilliseconds(1));

        await viewModel.RefreshAsync();

        Assert.False(viewModel.IsLoading);
        Assert.Contains("unexpected error", viewModel.StatusText, StringComparison.OrdinalIgnoreCase);
        Assert.IsType<InvalidOperationException>(viewModel.LastTechnicalError);
    }

    [Fact]
    public async Task MultiDayOccurrencesRenderOnEveryOverlappingGridDate()
    {
        var beforeGrid = new CalendarOccurrence(
            "college",
            "conference",
            new DateTimeOffset(2026, 8, 28, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 9, 2, 0, 0, 0, TimeSpan.Zero),
            true,
            "Conference");
        var inMonth = new CalendarOccurrence(
            "college",
            "retreat",
            new DateTimeOffset(2026, 9, 20, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 9, 23, 0, 0, 0, TimeSpan.Zero),
            true,
            "Retreat");
        using var viewModel = new MainWindowViewModel(
            new FixedSource([beforeGrid, inMonth]),
            September);

        await viewModel.RefreshAsync();

        Assert.Equal(2, viewModel.VisibleOccurrenceCount);
        Assert.Equal(3, CountDaysContaining(viewModel, "Conference"));
        Assert.Equal(3, CountDaysContaining(viewModel, "Retreat"));
    }

    [Fact]
    public async Task HiddenItemsRemainDiscoverableAcrossMonthNavigation()
    {
        using var viewModel = CreateViewModel();
        await viewModel.RefreshAsync();
        AllVisible(viewModel).Single(item => item.Title == "Doctor appointment").HideCommand.Execute(null);

        await viewModel.NavigateAsync(2);

        Assert.Single(viewModel.HiddenOccurrences);
        Assert.Equal("Hidden items (1)", viewModel.HiddenCountLabel);
        Assert.Contains("2026", viewModel.HiddenOccurrences[0].Label, StringComparison.Ordinal);
        Assert.True(viewModel.RestoreAllCommand.CanExecute(null));

        viewModel.RestoreAllCommand.Execute(null);

        Assert.Empty(viewModel.HiddenOccurrences);
        Assert.False(viewModel.RestoreAllCommand.CanExecute(null));
    }

    [Fact]
    public async Task HideAllTitlesRebuildsTheCalendarOnlyOnce()
    {
        using var viewModel = CreateViewModel();
        await viewModel.RefreshAsync();
        var resetCount = 0;
        viewModel.Days.CollectionChanged += (_, eventArgs) =>
        {
            if (eventArgs.Action == NotifyCollectionChangedAction.Reset)
            {
                resetCount++;
            }
        };

        viewModel.HideAllTitlesCommand.Execute(null);

        Assert.Equal(1, resetCount);
        Assert.Equal(0, viewModel.VisibleOccurrenceCount);
    }

    [Fact]
    public async Task SupersededFailureCannotOverwriteNewerSuccessfulStatus()
    {
        var source = new LateFailingSource();
        using var viewModel = new MainWindowViewModel(source, September);
        var oldLoad = viewModel.RefreshAsync();
        await source.FirstRequestStarted.Task;

        await viewModel.NavigateAsync(1);
        var successfulStatus = viewModel.StatusText;
        source.ReleaseFirstFailure.SetResult();
        await oldLoad;

        Assert.Equal("October 2026", viewModel.DisplayedMonthLabel);
        Assert.Equal(successfulStatus, viewModel.StatusText);
        Assert.Null(viewModel.LastTechnicalError);
    }

    [Fact]
    public async Task RefreshUpdatesRetainedHiddenOccurrenceDetails()
    {
        var recurrenceId = new DateTimeOffset(2026, 9, 18, 9, 0, 0, TimeSpan.Zero);
        var source = new MutableSource(CreateMutableOccurrence("Original title", recurrenceId, recurrenceId));
        using var viewModel = new MainWindowViewModel(source, September);
        await viewModel.RefreshAsync();
        AllVisible(viewModel).Single().HideCommand.Execute(null);
        source.Occurrence = CreateMutableOccurrence("Updated title", recurrenceId.AddHours(2), recurrenceId);

        await viewModel.RefreshAsync();

        Assert.Single(viewModel.HiddenOccurrences);
        Assert.Contains("Updated title", viewModel.HiddenOccurrences[0].Label, StringComparison.Ordinal);
        Assert.Contains("11:00 AM", viewModel.HiddenOccurrences[0].Label, StringComparison.Ordinal);
        Assert.Contains("2026", viewModel.HiddenOccurrences[0].Label, StringComparison.Ordinal);
    }

    private static MainWindowViewModel CreateViewModel() => new(
        new FakeCalendarOccurrenceSource(),
        September,
        TimeSpan.FromMilliseconds(1));

    private static List<OccurrenceViewModel> AllVisible(MainWindowViewModel viewModel) =>
        viewModel.Days.SelectMany(day => day.Occurrences).ToList();

    private static int CountDaysContaining(MainWindowViewModel viewModel, string title) =>
        viewModel.Days.Count(day => day.Occurrences.Any(item => item.Title == title));

    private static CalendarOccurrence CreateMutableOccurrence(
        string title,
        DateTimeOffset start,
        DateTimeOffset recurrenceId) => new(
            "college",
            "mutable-series",
            start,
            start.AddHours(1),
            false,
            title,
            recurrenceId: recurrenceId);

    private sealed class FixedSource(IReadOnlyList<CalendarOccurrence> occurrences)
        : ICalendarOccurrenceSource
    {
        public IReadOnlyList<CalendarSource> Calendars { get; } = [new("college", "College")];

        public Task<IReadOnlyList<CalendarOccurrence>> LoadAsync(
            MonthGridRange range,
            CancellationToken cancellationToken)
        {
            _ = range;
            return Task.FromResult(occurrences);
        }
    }

    private sealed class ThrowingSource : ICalendarOccurrenceSource
    {
        public IReadOnlyList<CalendarSource> Calendars { get; } = [new("college", "College")];

        public Task<IReadOnlyList<CalendarOccurrence>> LoadAsync(
            MonthGridRange range,
            CancellationToken cancellationToken)
        {
            _ = range;
            throw new InvalidOperationException("Synthetic unexpected failure");
        }
    }

    private sealed class MutableSource(CalendarOccurrence occurrence) : ICalendarOccurrenceSource
    {
        public IReadOnlyList<CalendarSource> Calendars { get; } = [new("college", "College")];

        public CalendarOccurrence Occurrence { get; set; } = occurrence;

        public Task<IReadOnlyList<CalendarOccurrence>> LoadAsync(
            MonthGridRange range,
            CancellationToken cancellationToken)
        {
            _ = range;
            return Task.FromResult<IReadOnlyList<CalendarOccurrence>>([Occurrence]);
        }
    }

    private sealed class LateFailingSource : ICalendarOccurrenceSource
    {
        private int requests;

        public IReadOnlyList<CalendarSource> Calendars { get; } = [new("college", "College")];

        public TaskCompletionSource FirstRequestStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource ReleaseFirstFailure { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<IReadOnlyList<CalendarOccurrence>> LoadAsync(
            MonthGridRange range,
            CancellationToken cancellationToken)
        {
            _ = cancellationToken;
            if (Interlocked.Increment(ref requests) == 1)
            {
                FirstRequestStarted.SetResult();
                await ReleaseFirstFailure.Task;
                throw new InvalidOperationException("Late synthetic failure");
            }

            var start = new DateTimeOffset(
                range.DisplayedMonth.Year,
                range.DisplayedMonth.Month,
                1,
                9,
                0,
                0,
                TimeSpan.Zero);
            return
            [
                new CalendarOccurrence(
                    "college",
                    "newer-success",
                    start,
                    start.AddHours(1),
                    false,
                    "Newer successful result"),
            ];
        }
    }

    private sealed class ControllableSource : ICalendarOccurrenceSource
    {
        private int requests;

        public IReadOnlyList<CalendarSource> Calendars { get; } = [new("college", "College")];

        public TaskCompletionSource FirstRequestStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool FirstRequestWasCancelled { get; private set; }

        public async Task<IReadOnlyList<CalendarOccurrence>> LoadAsync(
            MonthGridRange range,
            CancellationToken cancellationToken)
        {
            var request = Interlocked.Increment(ref requests);
            if (request == 1)
            {
                FirstRequestStarted.SetResult();
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    FirstRequestWasCancelled = true;
                    throw;
                }
            }

            var start = new DateTimeOffset(
                range.DisplayedMonth.Year,
                range.DisplayedMonth.Month,
                1,
                9,
                0,
                0,
                TimeSpan.Zero);
            return
            [
                new CalendarOccurrence(
                    "college",
                    $"result-{range.DisplayedMonth:yyyy-MM}",
                    start,
                    start.AddHours(1),
                    false,
                    $"{range.DisplayedMonth:MMMM} result"),
            ];
        }
    }
}
