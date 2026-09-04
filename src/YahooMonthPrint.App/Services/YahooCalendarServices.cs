using System.IO;
using YahooMonthPrint.Core;
using YahooMonthPrint.YahooCalDav;

namespace YahooMonthPrint.App.Services;

public interface IYahooCalDavClientFactory
{
    IYahooCalDavClient Create(string accountName, string appPassword);
}

public sealed class YahooCalDavClientFactory : IYahooCalDavClientFactory
{
    public IYahooCalDavClient Create(string accountName, string appPassword) =>
        new YahooCalDavClient(YahooCalDavClient.CreateHttpClient(accountName, appPassword));
}

public sealed class YahooConnectionService(
    IYahooCalDavClientFactory clientFactory,
    IAppLogger logger)
{
    public async Task<IReadOnlyList<CalDavCalendar>> DiscoverAsync(
        string accountName,
        string appPassword,
        CancellationToken cancellationToken)
    {
        try
        {
            using var client = clientFactory.Create(accountName, appPassword);
            var calendars = await client.DiscoverCalendarsAsync(cancellationToken);
            logger.Log("discovery", "success");
            return calendars;
        }
        catch (CalDavException exception)
        {
            logger.Log("discovery", exception.Kind.ToString(), exception: exception);
            throw MapFailure(exception);
        }
    }

    internal static CalendarLoadException MapFailure(CalDavException exception) => new(
        exception.Kind switch
        {
            CalDavFailureKind.Authentication => CalendarLoadFailureKind.Authentication,
            CalDavFailureKind.Connectivity => CalendarLoadFailureKind.Connectivity,
            CalDavFailureKind.Server => CalendarLoadFailureKind.Server,
            _ => CalendarLoadFailureKind.Protocol,
        },
        exception.Message,
        exception.TechnicalDetail,
        exception);
}

public sealed class YahooCalendarOccurrenceSource : ICachedCalendarOccurrenceSource, ICalendarSelectionStore
{
    private readonly ICredentialStore credentialStore;
    private readonly ISettingsStore settingsStore;
    private readonly ICalendarCacheStore cacheStore;
    private readonly IYahooCalDavClientFactory clientFactory;
    private readonly IAppLogger logger;
    private ApplicationSettings settings;

    public YahooCalendarOccurrenceSource(
        ApplicationSettings settings,
        ICredentialStore credentialStore,
        ISettingsStore settingsStore,
        ICalendarCacheStore cacheStore,
        IYahooCalDavClientFactory clientFactory,
        IAppLogger logger)
    {
        this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        this.credentialStore = credentialStore ?? throw new ArgumentNullException(nameof(credentialStore));
        this.settingsStore = settingsStore ?? throw new ArgumentNullException(nameof(settingsStore));
        this.cacheStore = cacheStore ?? throw new ArgumentNullException(nameof(cacheStore));
        this.clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        Calendars = settings.Calendars.Select(calendar => calendar.ToCalendarSource()).ToArray();
    }

    public IReadOnlyList<CalendarSource> Calendars { get; private set; }

    public Task<CalendarLoadResult?> TryLoadCachedAsync(
        MonthGridRange range,
        CancellationToken cancellationToken)
    {
        var calendarIds = settings.Calendars
            .Where(calendar => calendar.IsSelected)
            .Select(calendar => calendar.Id)
            .ToArray();
        return cacheStore.TryReadAsync(range, calendarIds, cancellationToken);
    }

    public void SetCalendarEnabled(string calendarId, bool isEnabled)
    {
        var changed = false;
        settings = settings with
        {
            Calendars = settings.Calendars.Select(calendar =>
            {
                if (calendar.Id != calendarId || calendar.IsSelected == isEnabled)
                {
                    return calendar;
                }

                changed = true;
                return calendar with { IsSelected = isEnabled };
            }).ToArray(),
        };
        if (!changed)
        {
            return;
        }

        Calendars = settings.Calendars.Select(calendar => calendar.ToCalendarSource()).ToArray();
        _ = settingsStore.SaveAsync(settings).ContinueWith(
            task => logger.Log("settings", "calendar-selection-save-failed", exception: task.Exception),
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.Default);
    }

    public async Task<CalendarLoadResult> LoadAsync(
        MonthGridRange range,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(range);
        var accountName = settings.YahooAccount;
        if (string.IsNullOrWhiteSpace(accountName))
        {
            throw new CalendarLoadException(
                CalendarLoadFailureKind.Authentication,
                "Connect a Yahoo account before refreshing.");
        }

        var appPassword = credentialStore.Read(accountName);
        if (string.IsNullOrEmpty(appPassword))
        {
            throw new CalendarLoadException(
                CalendarLoadFailureKind.Authentication,
                "Yahoo did not accept the saved app password. Enter a new app password.");
        }

        using var client = clientFactory.Create(accountName, appPassword);
        CalDavCalendar[] selected;
        try
        {
            selected = SelectedCalendars();
        }
        catch (UriFormatException exception)
        {
            logger.Log("calendar-query", "invalid-saved-calendar", exception: exception);
            throw InvalidCalendarSettings(exception);
        }

        if (selected.Length == 0)
        {
            return new CalendarLoadResult([], DateTimeOffset.Now);
        }

        CalendarQueryResult queryResult;
        try
        {
            queryResult = await client.QueryCalendarsAsync(selected, range, cancellationToken);
        }
        catch (CalDavException exception) when (
            exception.Kind == CalDavFailureKind.CalendarCollectionRejected)
        {
            selected = await RediscoverAsync(client, cancellationToken);
            queryResult = await QueryAfterRediscoveryAsync(client, selected, range, cancellationToken);
        }
        catch (CalDavException exception)
        {
            logger.Log("calendar-query", exception.Kind.ToString(), exception: exception);
            throw YahooConnectionService.MapFailure(exception);
        }
        var result = new CalendarLoadResult(
            queryResult.Occurrences,
            DateTimeOffset.Now,
            queryResult.ResourceIssues.Count);
        try
        {
            await cacheStore.WriteAsync(
                range,
                selected.Select(calendar => calendar.Id).ToArray(),
                result,
                cancellationToken);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.Log("cache", "write-failed", exception: exception);
        }
        logger.Log(
            "calendar-query",
            queryResult.ResourceIssues.Count == 0
                ? "success"
                : $"success-with-{queryResult.ResourceIssues.Count}-resource-errors");
        foreach (var issue in queryResult.ResourceIssues)
        {
            logger.Log("resource-parse", issue.ExceptionType, issue.ResourceId);
        }

        return result;
    }

    private async Task<CalDavCalendar[]> RediscoverAsync(
        IYahooCalDavClient client,
        CancellationToken cancellationToken)
    {
        try
        {
            var discovered = await client.DiscoverCalendarsAsync(cancellationToken);
            var previouslySelected = settings.Calendars
                .Where(calendar => calendar.IsSelected)
                .ToArray();
            var selectedIds = previouslySelected
                .Select(calendar => calendar.Id)
                .ToHashSet(StringComparer.Ordinal);
            var recoveredEverySelection = previouslySelected.All(previous => discovered.Any(calendar =>
                string.Equals(calendar.Id, previous.Id, StringComparison.Ordinal)
                || string.Equals(
                    calendar.DisplayName,
                    previous.DisplayName,
                    StringComparison.OrdinalIgnoreCase)));
            if (!recoveredEverySelection)
            {
                throw new CalendarLoadException(
                    CalendarLoadFailureKind.Protocol,
                    "One or more selected Yahoo calendars could not be rediscovered. Review calendar settings and try again.",
                    "Rediscovery did not recover every previously selected calendar collection.");
            }

            settings = settings with
            {
                Calendars = discovered.Select(calendar => new SavedCalendar(
                    calendar.Id,
                    calendar.DisplayName,
                    calendar.Uri.AbsoluteUri,
                    calendar.Color,
                    selectedIds.Contains(calendar.Id)
                        || previouslySelected.Any(previous => string.Equals(
                            calendar.DisplayName,
                            previous.DisplayName,
                            StringComparison.OrdinalIgnoreCase))))
                    .ToArray(),
            };
            await settingsStore.SaveAsync(settings, cancellationToken);
            Calendars = settings.Calendars.Select(calendar => calendar.ToCalendarSource()).ToArray();
            return SelectedCalendars();
        }
        catch (CalDavException exception)
        {
            logger.Log("rediscovery", exception.Kind.ToString(), exception: exception);
            throw YahooConnectionService.MapFailure(exception);
        }
    }

    private async Task<CalendarQueryResult> QueryAfterRediscoveryAsync(
        IYahooCalDavClient client,
        IReadOnlyList<CalDavCalendar> selected,
        MonthGridRange range,
        CancellationToken cancellationToken)
    {
        try
        {
            return await client.QueryCalendarsAsync(selected, range, cancellationToken);
        }
        catch (CalDavException exception)
        {
            logger.Log("calendar-query", exception.Kind.ToString(), exception: exception);
            throw YahooConnectionService.MapFailure(exception);
        }
    }

    private CalDavCalendar[] SelectedCalendars() => settings.Calendars
        .Where(calendar => calendar.IsSelected)
        .Select(calendar => new CalDavCalendar(
            calendar.Id,
            calendar.DisplayName,
            ParseCalendarUri(calendar.Uri),
            calendar.Color))
        .ToArray();

    private static Uri ParseCalendarUri(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new UriFormatException("A saved calendar address is not an absolute HTTPS URI.");
        }

        return uri;
    }

    private static CalendarLoadException InvalidCalendarSettings(UriFormatException exception) => new(
        CalendarLoadFailureKind.Protocol,
        "A saved Yahoo calendar address is invalid. Reconnect the account and try again.",
        exception.GetType().Name,
        exception);
}

public sealed class YahooAccountService(
    ICredentialStore credentialStore,
    ISettingsStore settingsStore,
    ICalendarCacheStore cacheStore)
{
    public async Task SaveConnectionAsync(
        string accountName,
        string appPassword,
        IReadOnlyList<CalDavCalendar> calendars,
        IReadOnlySet<string> selectedCalendarIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountName);
        ArgumentException.ThrowIfNullOrWhiteSpace(appPassword);
        ArgumentNullException.ThrowIfNull(calendars);
        ArgumentNullException.ThrowIfNull(selectedCalendarIds);

        credentialStore.Write(accountName, appPassword);
        var settings = await settingsStore.LoadAsync(cancellationToken);
        settings = settings with
        {
            YahooAccount = accountName.Trim(),
            Calendars = calendars.Select(calendar => new SavedCalendar(
                calendar.Id,
                calendar.DisplayName,
                calendar.Uri.AbsoluteUri,
                calendar.Color,
                selectedCalendarIds.Contains(calendar.Id)))
                .ToArray(),
        };
        await settingsStore.SaveAsync(settings, cancellationToken);
    }

    public void ChangePassword(string accountName, string appPassword) =>
        credentialStore.Write(accountName, appPassword);

    public async Task DisconnectAsync(
        string accountName,
        CancellationToken cancellationToken = default)
    {
        credentialStore.Delete(accountName);
        await cacheStore.ClearAsync(cancellationToken);
        await settingsStore.ClearAsync(cancellationToken);
    }
}
