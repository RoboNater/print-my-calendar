using YahooMonthPrint.Core;

namespace YahooMonthPrint.App.Services;

public interface ICalendarOccurrenceSource
{
    IReadOnlyList<CalendarSource> Calendars { get; }

    Task<IReadOnlyList<CalendarOccurrence>> LoadAsync(
        MonthGridRange range,
        CancellationToken cancellationToken);
}
