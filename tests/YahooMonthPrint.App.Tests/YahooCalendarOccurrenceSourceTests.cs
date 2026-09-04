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
        bool includePersonal = false)
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
            new NullAppLogger());
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

    private sealed class FakeClientFactory(FakeClient client) : IYahooCalDavClientFactory
    {
        public IYahooCalDavClient Create(string accountName, string appPassword) => client;
    }

    private sealed class FakeClient : IYahooCalDavClient
    {
        public CalendarQueryResult QueryResult { get; init; } = new([], []);

        public CalDavException? QueryException { get; init; }

        public IReadOnlyList<CalDavCalendar> DiscoveredCalendars { get; init; } = [];

        public int DiscoveryCount { get; private set; }

        public Task<IReadOnlyList<CalDavCalendar>> DiscoverCalendarsAsync(
            CancellationToken cancellationToken)
        {
            DiscoveryCount++;
            return Task.FromResult(DiscoveredCalendars);
        }

        public Task<CalendarQueryResult> QueryCalendarsAsync(
            IReadOnlyCollection<CalDavCalendar> calendars,
            MonthGridRange range,
            CancellationToken cancellationToken) => QueryException is null
                ? Task.FromResult(QueryResult)
                : Task.FromException<CalendarQueryResult>(QueryException);

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
}
