using Ical.Net;

namespace YahooMonthPrint.YahooCalDav;

/// <summary>
/// Provides a narrow Phase 1 proof that the pinned iCalendar parser can be loaded.
/// Production conversion and recurrence expansion are implemented in Phase 3.
/// </summary>
public static class ICalendarToolchainProbe
{
    public static Calendar Parse(string calendarText)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(calendarText);

        return Calendar.Load(calendarText)
            ?? throw new FormatException("The iCalendar payload did not contain a calendar.");
    }
}
