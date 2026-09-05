using YahooMonthPrint.Core;

namespace YahooMonthPrint.Printing;

public static class MonthLayoutModelBuilder
{
    public static MonthLayoutModel Build(
        DateOnly displayedMonth,
        IReadOnlyList<CalendarOccurrence> visibleOccurrences,
        MonthPrintOptions options)
    {
        ArgumentNullException.ThrowIfNull(visibleOccurrences);
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();

        displayedMonth = new DateOnly(displayedMonth.Year, displayedMonth.Month, 1);
        var grid = MonthGrid.Create(displayedMonth.Year, displayedMonth.Month);
        var projectionState = new MonthViewState(displayedMonth)
        {
            DetailLevel = options.DetailLevel,
            MaximumDescriptionLines = options.DescriptionLineLimit,
            ShowLocations = options.ShowLocations,
        };
        var ordered = visibleOccurrences.Order(OccurrenceComparer.Instance).ToArray();
        var days = grid.Dates.Select(date => new PrintDayModel(
            date,
            date.Year == displayedMonth.Year && date.Month == displayedMonth.Month,
            ordered
                .Where(occurrence => OccurrenceDateRange.OccursOnDate(occurrence, date))
                .Select(occurrence => CreateOccurrence(occurrence, date, projectionState))
                .ToArray()))
            .ToArray();

        return new MonthLayoutModel(
            displayedMonth,
            grid,
            days,
            ordered.Select(occurrence => occurrence.Key).Distinct().ToArray(),
            options);
    }

    private static PrintOccurrenceModel CreateOccurrence(
        CalendarOccurrence occurrence,
        DateOnly date,
        MonthViewState projectionState)
    {
        var projection = EventDisplayProjection.Create(occurrence, projectionState);
        return new PrintOccurrenceModel(
            occurrence.Key,
            date,
            occurrence.IsAllDay,
            projection.TimeText,
            projection.Title,
            projection.DescriptionLines,
            Lines(occurrence.Description),
            projection.Location);
    }

    private static string[] Lines(string value) => value.Split(
        ["\r\n", "\r", "\n"],
        StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
