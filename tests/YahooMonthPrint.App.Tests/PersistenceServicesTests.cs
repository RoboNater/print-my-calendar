using System.IO;
using YahooMonthPrint.App.Services;
using YahooMonthPrint.Core;
using YahooMonthPrint.YahooCalDav;

namespace YahooMonthPrint.App.Tests;

public sealed class PersistenceServicesTests : IDisposable
{
    private const string Secret = "never-write-this-app-password";
    private readonly string directory = Path.Combine(
        Path.GetTempPath(),
        "YahooMonthPrint.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task SettingsRoundTripContainsNoCredential()
    {
        var store = new JsonSettingsStore(directory);
        var settings = CreateSettings();

        await store.SaveAsync(settings, TestContext.Current.CancellationToken);
        var loaded = await store.LoadAsync(TestContext.Current.CancellationToken);
        var json = await File.ReadAllTextAsync(
            Path.Combine(directory, "settings.json"),
            TestContext.Current.CancellationToken);

        Assert.Equal(settings.YahooAccount, loaded.YahooAccount);
        Assert.Equal(settings.MaximumDescriptionLines, loaded.MaximumDescriptionLines);
        Assert.Equal(settings.Calendars, loaded.Calendars);
        Assert.DoesNotContain(Secret, json, StringComparison.Ordinal);
        Assert.DoesNotContain("password", json, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(Directory.GetFiles(directory, "*.tmp"));
    }

    [Fact]
    public async Task SerializedSettingsStoreMakesAConcurrentReadWaitForThePendingWrite()
    {
        var original = CreateSettings();
        var updated = original with { MaximumDescriptionLines = 8 };
        var inner = new BlockingSettingsStore(original);
        using var store = new SerializedSettingsStore(inner);

        var save = store.SaveAsync(updated, TestContext.Current.CancellationToken);
        await inner.SaveStarted.Task.WaitAsync(TestContext.Current.CancellationToken);
        var load = store.LoadAsync(TestContext.Current.CancellationToken);

        Assert.False(load.IsCompleted);
        inner.AllowSave.TrySetResult();
        await save;
        Assert.Equal(8, (await load).MaximumDescriptionLines);
    }

    [Fact]
    public async Task CacheRoundTripsAndRequiresExactRangeAndCalendarSet()
    {
        var store = new CalendarCacheStore(directory);
        var range = MonthGrid.Create(2026, 9);
        var occurrence = CreateOccurrence();
        var refreshedAt = DateTimeOffset.Now.AddMinutes(-15);
        var result = new CalendarLoadResult([occurrence], refreshedAt, 2);

        await store.WriteAsync(range, ["college"], result, TestContext.Current.CancellationToken);
        var loaded = await store.TryReadAsync(
            range,
            ["college"],
            TestContext.Current.CancellationToken);

        Assert.NotNull(loaded);
        Assert.True(loaded.IsFromCache);
        Assert.Equal(refreshedAt, loaded.RefreshedAt);
        Assert.Equal(2, loaded.UnreadableResourceCount);
        Assert.Equal(occurrence, Assert.Single(loaded.Occurrences));
        Assert.Null(await store.TryReadAsync(
            MonthGrid.Create(2026, 10),
            ["college"],
            TestContext.Current.CancellationToken));
        Assert.Null(await store.TryReadAsync(
            range,
            ["college", "personal"],
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CacheCanRestoreASelectedSubsetWithoutShowingDisabledCalendars()
    {
        var store = new CalendarCacheStore(directory);
        var range = MonthGrid.Create(2026, 9);
        var college = CreateOccurrence();
        var personal = new CalendarOccurrence(
            "personal",
            "personal-event",
            college.Start,
            college.End,
            false,
            "Personal event");
        await store.WriteAsync(
            range,
            ["college", "personal"],
            new CalendarLoadResult([college, personal], DateTimeOffset.Now),
            TestContext.Current.CancellationToken);

        var loaded = await store.TryReadAsync(
            range,
            ["college"],
            TestContext.Current.CancellationToken);

        Assert.Equal(college, Assert.Single(Assert.IsType<CalendarLoadResult>(loaded).Occurrences));
    }

    [Fact]
    public async Task CorruptCacheIsQuarantinedAndClearDoesNotTouchSettings()
    {
        Directory.CreateDirectory(directory);
        var cachePath = Path.Combine(directory, "calendar-cache.json");
        await File.WriteAllTextAsync(
            cachePath,
            "{ truncated",
            TestContext.Current.CancellationToken);
        var settingsStore = new JsonSettingsStore(directory);
        await settingsStore.SaveAsync(CreateSettings(), TestContext.Current.CancellationToken);
        var cache = new CalendarCacheStore(directory);

        var result = await cache.TryReadAsync(
            MonthGrid.Create(2026, 9),
            ["college"],
            TestContext.Current.CancellationToken);
        await cache.ClearAsync(TestContext.Current.CancellationToken);

        Assert.Null(result);
        Assert.False(File.Exists(cachePath));
        Assert.Single(Directory.GetFiles(directory, "calendar-cache.corrupt-*.json"));
        Assert.Equal(
            "student@example.test",
            (await settingsStore.LoadAsync(TestContext.Current.CancellationToken)).YahooAccount);
    }

    [Fact]
    public void LoggerRedactsAuthorizationAndNeverWritesExceptionMessages()
    {
        var logger = new RotatingFileAppLogger(directory);
        logger.Log(
            "calendar-query",
            $"failed Authorization: Basic {Secret}",
            "resource-1",
            new InvalidOperationException($"description and {Secret}"));

        var text = File.ReadAllText(Path.Combine(directory, "Logs", "YahooMonthPrint.log"));

        Assert.Contains("[redacted-header]", text, StringComparison.Ordinal);
        Assert.Contains(nameof(InvalidOperationException), text, StringComparison.Ordinal);
        Assert.DoesNotContain(Secret, text, StringComparison.Ordinal);
        Assert.DoesNotContain("description", text, StringComparison.Ordinal);
    }

    [Fact]
    public void WindowsCredentialManagerSupportsCreateReadReplaceDelete()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var store = new WindowsCredentialStore($"YahooMonthPrint.Tests:{Guid.NewGuid():N}");
        const string account = "disposable@example.test";
        try
        {
            store.Write(account, "first-disposable-secret");
            Assert.Equal("first-disposable-secret", store.Read(account));

            store.Write(account, "replacement-disposable-secret");
            Assert.Equal("replacement-disposable-secret", store.Read(account));
        }
        finally
        {
            store.Delete(account);
        }

        Assert.Null(store.Read(account));
    }

    [Fact]
    public async Task AccountLifecycleUsesCredentialTestDoubleAndSeparatesClearFromDisconnect()
    {
        const string account = "student@example.test";
        var credentials = new TrackingCredentialStore();
        var settings = new JsonSettingsStore(directory);
        var cache = new CalendarCacheStore(directory);
        var service = new YahooAccountService(credentials, settings, cache);
        var range = MonthGrid.Create(2026, 9);
        await cache.WriteAsync(
            range,
            ["college"],
            new CalendarLoadResult([CreateOccurrence()], DateTimeOffset.Now),
            TestContext.Current.CancellationToken);

        await service.SaveConnectionAsync(
            account,
            Secret,
            [
                new CalDavCalendar(
                    "college",
                    "College",
                    new Uri("https://calendar.example.test/college/")),
            ],
            new HashSet<string>(["college"], StringComparer.Ordinal),
            TestContext.Current.CancellationToken);
        service.ChangePassword(account, "replacement-app-password");

        Assert.Equal("replacement-app-password", credentials.Read(account));
        Assert.Equal(account, (await settings.LoadAsync(TestContext.Current.CancellationToken)).YahooAccount);

        await service.DisconnectAsync(account, TestContext.Current.CancellationToken);

        Assert.Null(credentials.Read(account));
        Assert.Null((await settings.LoadAsync(TestContext.Current.CancellationToken)).YahooAccount);
        Assert.Null(await cache.TryReadAsync(
            range,
            ["college"],
            TestContext.Current.CancellationToken));
    }

    public void Dispose()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static ApplicationSettings CreateSettings() => new()
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
        MaximumDescriptionLines = 4,
    };

    private static CalendarOccurrence CreateOccurrence()
    {
        var local = new DateTime(2026, 9, 14, 9, 0, 0, DateTimeKind.Unspecified);
        var start = new DateTimeOffset(local, TimeZoneInfo.Local.GetUtcOffset(local));
        return new CalendarOccurrence(
            "college",
            "calculus",
            start,
            start.AddHours(1),
            false,
            "Calculus II",
            "EXAM 2",
            "Science 201",
            start,
            TimeZoneInfo.Local.Id,
            "fixture.ics");
    }

    private sealed class TrackingCredentialStore : ICredentialStore
    {
        private readonly Dictionary<string, string> values = new(StringComparer.OrdinalIgnoreCase);

        public string? Read(string accountName) => values.GetValueOrDefault(accountName);

        public void Write(string accountName, string appPassword) => values[accountName] = appPassword;

        public void Delete(string accountName) => values.Remove(accountName);
    }

    private sealed class BlockingSettingsStore(ApplicationSettings settings) : ISettingsStore
    {
        private ApplicationSettings current = settings;

        public TaskCompletionSource SaveStarted { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource AllowSave { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<ApplicationSettings> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(current);

        public async Task SaveAsync(
            ApplicationSettings value,
            CancellationToken cancellationToken = default)
        {
            SaveStarted.TrySetResult();
            await AllowSave.Task.WaitAsync(cancellationToken);
            current = value;
        }

        public Task ClearAsync(CancellationToken cancellationToken = default)
        {
            current = new ApplicationSettings();
            return Task.CompletedTask;
        }
    }
}
