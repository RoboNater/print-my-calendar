using YahooMonthPrint.Core;

namespace YahooMonthPrint.App.Services;

public interface ICalendarOccurrenceSource
{
    IReadOnlyList<CalendarSource> Calendars { get; }

    /// <summary>
    /// Loads the complete occurrence set for every enabled calendar and the requested range.
    /// Implementations must throw <see cref="CalendarLoadException"/> if any calendar collection
    /// cannot be loaded; returning an unmarked partial collection is forbidden. A malformed
    /// individual resource may be isolated only when it is counted in the returned result.
    /// </summary>
    Task<CalendarLoadResult> LoadAsync(
        MonthGridRange range,
        CancellationToken cancellationToken);
}

public interface ICachedCalendarOccurrenceSource : ICalendarOccurrenceSource
{
    Task<CalendarLoadResult?> TryLoadCachedAsync(
        MonthGridRange range,
        CancellationToken cancellationToken);
}

public interface ICalendarSelectionStore
{
    void SetCalendarEnabled(string calendarId, bool isEnabled);
}

public interface IPendingCalendarChanges
{
    Task FlushPendingChangesAsync();
}
