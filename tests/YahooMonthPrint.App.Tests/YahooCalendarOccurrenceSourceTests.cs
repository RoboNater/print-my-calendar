using System.IO;
using YahooMonthPrint.App.Services;
using YahooMonthPrint.Core;
using YahooMonthPrint.YahooCalDav;

namespace YahooMonthPrint.App.Tests;

public sealed class YahooCalendarOccurrenceSourceTests : IDisposable
{
    private readonly string directory = Path.Combine(
        Path.GetTempPath(),
        "YahooMonthPrint.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task CompleteRefreshIsCachedWithKnownResourceWarnings()
    {
        var range = MonthGrid.Create(2026, 9);
        var occurrence = CreateOccurrence();
        var client = new FakeClient
        {
            QueryResult = new CalendarQueryResult(
                [occurrence],
                [new CalDavResourceIssue("college", "bad.ics", "FormatException")]),
        };
        var source = CreateSource(client);

        var result = await source.LoadAsync(range, TestContext.Current.CancellationToken);
        var cached = await source.TryLoadCachedAsync(range, TestContext.Current.CancellationToken);

        Assert.Equal(1, result.UnreadableResourceCount);
        Assert.Equal(occurrence, Assert.Single(result.Occurrences));
        Assert.NotNull(cached);
        Assert.Equal(1, cached.UnreadableResourceCount);
    }

    [Fact]
    public async Task FailedCollectionDoesNotReplacePreviouslyCompleteCache()
    {
        var range = MonthGrid.Create(2026, 9);
        var cache = new CalendarCacheStore(directory);
        var oldOccurrence = CreateOccurrence("old");
        await cache.WriteAsync(
            range,
            ["college", "personal"],
            new CalendarLoadResult([oldOccurrence], DateTimeOffset.Now.AddHours(-1)),
            TestContext.Current.CancellationToken);
        var client = new FakeClient
        {
            QueryException = new CalDavException(
                CalDavFailureKind.Server,
                "Yahoo Calendar is temporarily unavailable.",
                "Second selected calendar returned HTTP 503."),
        };
        var source = CreateSource(client, cache, includePersonal: true);

        var exception = await Assert.ThrowsAsync<CalendarLoadException>(() =>
            source.LoadAsync(range, TestContext.Current.CancellationToken));
        var cached = await source.TryLoadCachedAsync(range, TestContext.Current.CancellationToken);

        Assert.Equal(CalendarLoadFailureKind.Server, exception.Kind);
        Assert.Equal(oldOccurrence, Assert.Single(Assert.IsType<CalendarLoadResult>(cached).Occurrences));
    }

    [Fact]
    public async Task RejectedSavedUrlRediscoveryMustRecoverEverySelectedCalendar()
    {
        var client = new FakeClient
        {
            QueryException = new CalDavException(
                CalDavFailureKind.CalendarCollectionRejected,
                "Saved collection rejected.",
                "HTTP 404"),
            DiscoveredCalendars =
            [
                new CalDavCalendar(
                    "new-college",
                    "College",
                    new Uri("https://calendar.example.test/new-college/")),
            ],
        };
        var source = CreateSource(client, includePersonal: true);

        var exception = await Assert.ThrowsAsync<CalendarLoadException>(() => source.LoadAsync(
            MonthGrid.Create(2026, 9),
            TestContext.Current.CancellationToken));

        Assert.Equal(CalendarLoadFailureKind.Protocol, exception.Kind);
        Assert.Contains("one or more", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, client.DiscoveryCount);
    }

    [Fact]
    public async Task RediscoveryAllowsMultipleCollectionsMatchingOneSelectedDisplayName()
    {
        var logger = new TrackingLogger();
        var client = new FakeClient
        {
            QueryException = new CalDavException(
                CalDavFailureKind.CalendarCollectionRejected,
                "Saved collection rejected.",
                "HTTP 404"),
            RejectFirstQueryOnly = true,
            DiscoveredCalendars =
            [
                new CalDavCalendar(
                    "new-personal-1",
                    "College",
                    new Uri("https://calendar.example.test/new-personal-1/")),
                new CalDavCalendar(
                    "new-personal-2",
                    "College",
                    new Uri("https://calendar.example.test/new-personal-2/")),
            ],
        };
        var source = CreateSource(client, logger: logger);

        await source.LoadAsync(
            MonthGrid.Create(2026, 9),
            TestContext.Current.CancellationToken);

        Assert.Equal(2, client.QueryCount);
        Assert.Equal(2, client.LastQueriedCalendars.Count);
        Assert.All(client.LastQueriedCalendars, calendar => Assert.Equal("College", calendar.DisplayName));
        Assert.Contains(
            logger.Entries,
            entry => entry.Status == "ambiguous-name-selection"
                && entry.ResourceId is "new-personal-1" or "new-personal-2");
    }

    [Fact]
    public async Task CacheProbeUsesSelectedIdsWithoutParsingSavedAddresses()
    {
        var range = MonthGrid.Create(2026, 9);
        var cache = new CalendarCacheStore(directory);
        var occurrence = CreateOccurrence("cached");
        await cache.WriteAsync(
            range,
            ["college"],
            new CalendarLoadResult([occurrence], DateTimeOffset.Now),
            TestContext.Current.CancellationToken);
        var settings = new ApplicationSettings
        {
            YahooAccount = "student@example.test",
            Calendars = [new SavedCalendar("college", "College", "not a URI", null, true)],
        };
        var source = new YahooCalendarOccurrenceSource(
            settings,
            new FakeCredentialStore("disposable-app-password"),
            new JsonSettingsStore(directory),
            cache,
            new FakeClientFactory(new FakeClient()),
            new NullAppLogger());

        var result = await source.TryLoadCachedAsync(range, TestContext.Current.CancellationToken);

        Assert.Equal(occurrence, Assert.Single(Assert.IsType<CalendarLoadResult>(result).Occurrences));
    }

    [Fact]
    public async Task InvalidSavedAddressLogsTheCalendarIdentityWithoutTheAddress()
    {
        var logger = new TrackingLogger();
        var settings = new ApplicationSettings
        {
            YahooAccount = "student@example.test",
            Calendars = [new SavedCalendar("college", "College", "not a URI", null, true)],
        };
        var source = new YahooCalendarOccurrenceSource(
            settings,
            new FakeCredentialStore("disposable-app-password"),
            new JsonSettingsStore(directory),
            new CalendarCacheStore(directory),
            new FakeClientFactory(new FakeClient()),
            logger);

        var exception = await Assert.ThrowsAsync<CalendarLoadException>(() => source.LoadAsync(
            MonthGrid.Create(2026, 9),
            TestContext.Current.CancellationToken));

        Assert.Equal(CalendarLoadFailureKind.Protocol, exception.Kind);
        var entry = Assert.Single(logger.Entries);
        Assert.Equal("invalid-saved-calendar", entry.Status);
        Assert.Equal("college:College", entry.ResourceId);
    }

    [Fact]
    public async Task InitializeDisplaysCacheThenKeepsItWhenRefreshFails()
    {
        var range = MonthGrid.Create(2026, 9);
        var cached = new CalendarLoadResult(
            [CreateOccurrence("cached")],
            DateTimeOffset.Now.AddHours(-2),
            isFromCache: true);
        var source = new CachedFailingSource(cached);
        using var viewModel = new ViewModels.MainWindowViewModel(
            source,
            new DateOnly(2026, 9, 1),
            TimeSpan.FromMilliseconds(1));

        await viewModel.InitializeAsync();

        Assert.Equal("cached", Assert.Single(viewModel.VisibleOccurrences).Title);
        Assert.Contains("cached calendar data", viewModel.StatusText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("last updated", viewModel.StatusText, StringComparison.OrdinalIgnoreCase);
        Assert.IsType<CalendarLoadException>(viewModel.LastTechnicalError);
        Assert.Equal(range.Start, source.CachedRange?.Start);
    }

    [Fact]
    public async Task PendingCalendarSelectionWriteCanBeFlushedBeforeShutdown()
    {
        var settings = new ApplicationSettings
        {
            YahooAccount = "student@example.test",
            Calendars =
            [
                new SavedCalendar(
                    "college",
                    "College",
                    "https://calendar.example.test/college/",
                    null,
                    true),
            ],
        };
        var store = new BlockingSelectionSettingsStore();
        var source = new YahooCalendarOccurrenceSource(
            settings,
            new FakeCredentialStore("disposable-app-password"),
            store,
            new CalendarCacheStore(directory),
            new FakeClientFactory(new FakeClient()),
            new NullAppLogger());

        source.SetCalendarEnabled("college", false);
        await store.SaveStarted.Task.WaitAsync(TestContext.Current.CancellationToken);
        var flush = source.FlushPendingChangesAsync();

        Assert.False(flush.IsCompleted);
        store.AllowSave.TrySetResult();
        await flush;
        Assert.False(Assert.Single(Assert.IsType<ApplicationSettings>(store.Saved).Calendars).IsSelected);
    }

    public void Dispose()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private YahooCalendarOccurrenceSource CreateSource(
        FakeClient client,
        ICalendarCacheStore? cache = null,
        bool includePersonal = false,
        IAppLogger? logger = null)
    {
        var calendars = new List<SavedCalendar>
        {
            new(
                "college",
                "College",
                "https://calendar.example.test/college/",
                null,
                true),
        };
        if (includePersonal)
        {
            calendars.Add(new SavedCalendar(
                "personal",
                "Personal",
                "https://calendar.example.test/personal/",
                null,
                true));
        }

        var settings = new ApplicationSettings
        {
            YahooAccount = "student@example.test",
            Calendars = calendars,
        };
        return new YahooCalendarOccurrenceSource(
            settings,
            new FakeCredentialStore("disposable-app-password"),
            new JsonSettingsStore(directory),
            cache ?? new CalendarCacheStore(directory),
            new FakeClientFactory(client),
            logger ?? new NullAppLogger());
    }

    private static CalendarOccurrence CreateOccurrence(string title = "Calculus II")
    {
        var local = new DateTime(2026, 9, 14, 9, 0, 0, DateTimeKind.Unspecified);
        var start = new DateTimeOffset(local, TimeZoneInfo.Local.GetUtcOffset(local));
        return new CalendarOccurrence(
            "college",
            "calculus",
            start,
            start.AddHours(1),
            false,
            title);
    }

    private sealed class FakeCredentialStore(string? password) : ICredentialStore
    {
        public string? Read(string accountName) => password;

        public void Write(string accountName, string appPassword)
        {
        }

        public void Delete(string accountName)
        {
        }
    }

    private sealed class TrackingLogger : IAppLogger
    {
        public List<(string Status, string? ResourceId)> Entries { get; } = [];

        public void Log(
            string category,
            string status,
            string? resourceId = null,
            Exception? exception = null) => Entries.Add((status, resourceId));
    }

    private sealed class FakeClientFactory(FakeClient client) : IYahooCalDavClientFactory
    {
        public IYahooCalDavClient Create(string accountName, string appPassword) => client;
    }

    private sealed class FakeClient : IYahooCalDavClient
    {
        public CalendarQueryResult QueryResult { get; init; } = new([], []);

        public CalDavException? QueryException { get; init; }

        public bool RejectFirstQueryOnly { get; init; }

        public IReadOnlyList<CalDavCalendar> DiscoveredCalendars { get; init; } = [];

        public int DiscoveryCount { get; private set; }

        public int QueryCount { get; private set; }

        public IReadOnlyCollection<CalDavCalendar> LastQueriedCalendars { get; private set; } = [];

        public Task<IReadOnlyList<CalDavCalendar>> DiscoverCalendarsAsync(
            CancellationToken cancellationToken)
        {
            DiscoveryCount++;
            return Task.FromResult(DiscoveredCalendars);
        }

        public Task<CalendarQueryResult> QueryCalendarsAsync(
            IReadOnlyCollection<CalDavCalendar> calendars,
            MonthGridRange range,
            CancellationToken cancellationToken)
        {
            QueryCount++;
            LastQueriedCalendars = calendars;
            return QueryException is null || RejectFirstQueryOnly && QueryCount > 1
                ? Task.FromResult(QueryResult)
                : Task.FromException<CalendarQueryResult>(QueryException);
        }

        public void Dispose()
        {
        }
    }

    private sealed class CachedFailingSource(CalendarLoadResult cached)
        : ICachedCalendarOccurrenceSource
    {
        public IReadOnlyList<CalendarSource> Calendars { get; } = [new("college", "College")];

        public MonthGridRange? CachedRange { get; private set; }

        public Task<CalendarLoadResult?> TryLoadCachedAsync(
            MonthGridRange range,
            CancellationToken cancellationToken)
        {
            CachedRange = range;
            return Task.FromResult<CalendarLoadResult?>(cached);
        }

        public Task<CalendarLoadResult> LoadAsync(
            MonthGridRange range,
            CancellationToken cancellationToken) => Task.FromException<CalendarLoadResult>(
                new CalendarLoadException(
                    CalendarLoadFailureKind.Connectivity,
                    "Yahoo Calendar could not be reached."));
    }

    private sealed class BlockingSelectionSettingsStore : ISettingsStore
    {
        public TaskCompletionSource SaveStarted { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource AllowSave { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public ApplicationSettings? Saved { get; private set; }

        public Task<ApplicationSettings> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new ApplicationSettings());

        public async Task SaveAsync(
            ApplicationSettings settings,
            CancellationToken cancellationToken = default)
        {
            SaveStarted.TrySetResult();
            await AllowSave.Task.WaitAsync(cancellationToken);
            Saved = settings;
        }

        public Task ClearAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
