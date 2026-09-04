using YahooMonthPrint.Core;

namespace YahooMonthPrint.YahooCalDav.Tests;

public sealed class IcsOccurrenceParserTests
{
    private static readonly MonthGridRange March = MonthGrid.Create(2026, 3);
    private readonly IcsOccurrenceParser parser = new(TimeZoneInfo.Local);

    [Fact]
    public void ExpandsRulesRdateExdateOverridesAndCancellations()
    {
        const string ics = """
            BEGIN:VCALENDAR
            VERSION:2.0
            PRODID:-//Yahoo Month Print//Tests//EN
            BEGIN:VEVENT
            UID:calculus@example.test
            DTSTAMP:20260101T000000Z
            DTSTART;TZID=America/New_York:20260302T090000
            DTEND;TZID=America/New_York:20260302T100000
            RRULE:FREQ=WEEKLY;COUNT=4
            RDATE;TZID=America/New_York:20260331T090000
            EXDATE;TZID=America/New_York:20260309T090000
            SUMMARY:Calculus II
            DESCRIPTION:Normal class
            END:VEVENT
            BEGIN:VEVENT
            UID:calculus@example.test
            RECURRENCE-ID;TZID=America/New_York:20260316T090000
            DTSTAMP:20260101T000000Z
            DTSTART;TZID=America/New_York:20260316T110000
            DTEND;TZID=America/New_York:20260316T120000
            SUMMARY:Calculus II
            DESCRIPTION:EXAM 2
            END:VEVENT
            BEGIN:VEVENT
            UID:calculus@example.test
            RECURRENCE-ID;TZID=America/New_York:20260323T090000
            DTSTAMP:20260101T000000Z
            DTSTART;TZID=America/New_York:20260323T090000
            DTEND;TZID=America/New_York:20260323T100000
            STATUS:CANCELLED
            SUMMARY:Calculus II
            END:VEVENT
            END:VCALENDAR
            """;

        var occurrences = parser.Parse("college", "fixture.ics", ics, March);

        Assert.Equal(3, occurrences.Count);
        Assert.DoesNotContain(occurrences, item => item.Start.Day == 9);
        Assert.DoesNotContain(occurrences, item => item.Start.Day == 23);
        var exam = Assert.Single(occurrences, item => item.Description == "EXAM 2");
        Assert.Equal(16, exam.Start.Day);
        Assert.NotEqual(exam.Start, exam.RecurrenceId);
        Assert.Contains(occurrences, item => item.Start.Day == 31);
        Assert.Equal(occurrences.Count, occurrences.Select(item => item.Key).Distinct().Count());
    }

    [Fact]
    public void ConvertsTimezoneOccurrencesAcrossDstToViewerLocalTime()
    {
        const string ics = """
            BEGIN:VCALENDAR
            VERSION:2.0
            PRODID:-//Yahoo Month Print//Tests//EN
            BEGIN:VEVENT
            UID:dst@example.test
            DTSTAMP:20260101T000000Z
            DTSTART;TZID=America/New_York:20260301T090000
            DTEND;TZID=America/New_York:20260301T100000
            RRULE:FREQ=WEEKLY;COUNT=3
            SUMMARY:DST class
            END:VEVENT
            END:VCALENDAR
            """;

        var occurrences = parser.Parse("college", "dst.ics", ics, March);

        Assert.Equal(14, occurrences[0].Start.UtcDateTime.Hour);
        Assert.Equal(13, occurrences[1].Start.UtcDateTime.Hour);
        Assert.Equal(13, occurrences[2].Start.UtcDateTime.Hour);
    }

    [Fact]
    public void PreservesAllDayDatesAcrossTimezoneBoundaries()
    {
        const string ics = """
            BEGIN:VCALENDAR
            VERSION:2.0
            PRODID:-//Yahoo Month Print//Tests//EN
            BEGIN:VEVENT
            UID:all-day@example.test
            DTSTAMP:20260101T000000Z
            DTSTART;VALUE=DATE:20260308
            DTEND;VALUE=DATE:20260310
            SUMMARY:Spring break
            END:VEVENT
            END:VCALENDAR
            """;

        var occurrence = Assert.Single(parser.Parse("college", "all-day.ics", ics, March));

        Assert.True(occurrence.IsAllDay);
        Assert.Equal(new DateTime(2026, 3, 8), occurrence.Start.Date);
        Assert.Equal(new DateTime(2026, 3, 10), occurrence.End.Date);
    }

    [Fact]
    public void IncludesAnEventThatStartsBeforeTheGridAndOverlapsIt()
    {
        const string ics = """
            BEGIN:VCALENDAR
            VERSION:2.0
            PRODID:-//Yahoo Month Print//Tests//EN
            BEGIN:VEVENT
            UID:conference@example.test
            DTSTAMP:20260101T000000Z
            DTSTART;VALUE=DATE:20260220
            DTEND;VALUE=DATE:20260303
            SUMMARY:Conference
            END:VEVENT
            END:VCALENDAR
            """;

        var occurrence = Assert.Single(parser.Parse("college", "conference.ics", ics, March));

        Assert.Equal(20, occurrence.Start.Day);
        Assert.Equal(3, occurrence.End.Day);
    }
}
