namespace YahooMonthPrint.Core;

public static class OccurrenceDateRange
{
    public static bool OccursOnDate(CalendarOccurrence occurrence, DateOnly date)
    {
        ArgumentNullException.ThrowIfNull(occurrence);

        var localStart = occurrence.Start.DateTime;
        var localEnd = occurrence.End.DateTime;
        var dayStart = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        var dayEnd = dayStart.AddDays(1);

        if (localStart == localEnd)
        {
            return localStart >= dayStart && localStart < dayEnd;
        }

        return localStart < dayEnd && localEnd > dayStart;
    }
}
