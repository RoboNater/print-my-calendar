using Ical.Net;
using Ical.Net.DataTypes;
using Ical.Net.Evaluation;

namespace YahooMonthPrint.YahooCalDav.Tests;

public sealed class ICalendarToolchainSmokeTests
{
    private const string CalendarText = """
        BEGIN:VCALENDAR
        VERSION:2.0
        PRODID:-//Yahoo Month Print//Toolchain Smoke//EN
        BEGIN:VEVENT
        UID:phase-1@example.test
        DTSTAMP:20260101T000000Z
        DTSTART;TZID=America/New_York:20260302T090000
        DTEND;TZID=America/New_York:20260302T100000
        RRULE:FREQ=WEEKLY;COUNT=3
        EXDATE;TZID=America/New_York:20260309T090000
        SUMMARY:Calculus II
        END:VEVENT
        END:VCALENDAR
        """;

    [Fact]
    public void PinnedIcalendarLibraryParsesTimezoneRecurrenceAndExceptionData()
    {
        var calendar = ICalendarToolchainProbe.Parse(CalendarText);
        var calendarEvent = Assert.Single(calendar.Events);
        var eventStart = Assert.IsType<CalDateTime>(calendarEvent.DtStart);
        var occurrences = calendar
            .GetOccurrences(new CalDateTime(2026, 3, 1), new EvaluationOptions())
            .TakeWhileBefore(new CalDateTime(2026, 4, 1))
            .ToArray();

        Assert.Equal("Calculus II", calendarEvent.Summary);
        Assert.Equal("America/New_York", eventStart.TzId);
        Assert.NotNull(calendarEvent.RecurrenceRule);
        Assert.Equal(2, occurrences.Length);
    }
}
