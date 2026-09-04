namespace YahooMonthPrint.Core;

public sealed class OccurrenceComparer : IComparer<CalendarOccurrence>
{
    public static OccurrenceComparer Instance { get; } = new();

    private OccurrenceComparer()
    {
    }

    public int Compare(CalendarOccurrence? x, CalendarOccurrence? y)
    {
        if (ReferenceEquals(x, y))
        {
            return 0;
        }

        if (x is null)
        {
            return -1;
        }

        if (y is null)
        {
            return 1;
        }

        var dayComparison = DateOnly.FromDateTime(x.Start.LocalDateTime).CompareTo(
            DateOnly.FromDateTime(y.Start.LocalDateTime));
        if (dayComparison != 0)
        {
            return dayComparison;
        }

        var allDayComparison = y.IsAllDay.CompareTo(x.IsAllDay);
        if (allDayComparison != 0)
        {
            return allDayComparison;
        }

        var startComparison = x.Start.CompareTo(y.Start);
        return startComparison != 0
            ? startComparison
            : StringComparer.OrdinalIgnoreCase.Compare(x.Title, y.Title);
    }
}
