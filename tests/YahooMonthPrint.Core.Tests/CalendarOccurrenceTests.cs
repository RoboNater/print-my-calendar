using YahooMonthPrint.Core;

namespace YahooMonthPrint.Core.Tests;

public sealed class CalendarOccurrenceTests
{
    [Fact]
    public void KeyDistinguishesOccurrencesFromSameRecurringSeries()
    {
        var first = Occurrence("series", LocalAt(2026, 9, 7, 9));
        var second = Occurrence("series", LocalAt(2026, 9, 14, 9));

        Assert.NotEqual(first.Key, second.Key);
    }

    [Fact]
    public void KeyUsesRecurrenceIdForMovedOverride()
    {
        var recurrenceId = LocalAt(2026, 9, 14, 9);
        var moved = new CalendarOccurrence(
            "college",
            "series",
            recurrenceId.AddHours(2),
            recurrenceId.AddHours(3),
            false,
            "Calculus II",
            recurrenceId: recurrenceId);

        Assert.Equal(recurrenceId, moved.Key.InstanceStart);
    }

    [Fact]
    public void ConstructorRetainsTimezoneMetadataAndAllDaySemantics()
    {
        var occurrence = new CalendarOccurrence(
            "college",
            "deadline",
            new DateTimeOffset(2026, 9, 18, 0, 0, 0, TimeSpan.FromHours(-4)),
            new DateTimeOffset(2026, 9, 19, 0, 0, 0, TimeSpan.FromHours(-4)),
            true,
            "Project due",
            sourceTimeZoneId: "America/New_York");

        Assert.True(occurrence.IsAllDay);
        Assert.Equal("America/New_York", occurrence.SourceTimeZoneId);
    }

    [Fact]
    public void ComparerPutsAllDayBeforeTimedOccurrences()
    {
        var timed = Occurrence("timed", LocalAt(2026, 9, 7, 8));
        var allDay = new CalendarOccurrence(
            "college",
            "all-day",
            timed.Start.Date,
            timed.Start.Date.AddDays(1),
            true,
            "Deadline");

        var ordered = new[] { timed, allDay }.Order(OccurrenceComparer.Instance).ToArray();

        Assert.True(ordered[0].IsAllDay);
    }

    [Fact]
    public void DateRangeIncludesEveryOverlappingDayWithExclusiveEnd()
    {
        var occurrence = new CalendarOccurrence(
            "college",
            "conference",
            new DateTimeOffset(2026, 9, 20, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 9, 23, 0, 0, 0, TimeSpan.Zero),
            true,
            "Conference");

        Assert.True(OccurrenceDateRange.OccursOnDate(occurrence, new DateOnly(2026, 9, 20)));
        Assert.True(OccurrenceDateRange.OccursOnDate(occurrence, new DateOnly(2026, 9, 21)));
        Assert.True(OccurrenceDateRange.OccursOnDate(occurrence, new DateOnly(2026, 9, 22)));
        Assert.False(OccurrenceDateRange.OccursOnDate(occurrence, new DateOnly(2026, 9, 23)));
    }

    [Fact]
    public void DateRangeIncludesZeroDurationOccurrenceOnItsStartDate()
    {
        var start = LocalAt(2026, 9, 20, 9);
        var occurrence = new CalendarOccurrence(
            "college",
            "reminder",
            start,
            start,
            false,
            "Reminder");

        Assert.True(OccurrenceDateRange.OccursOnDate(occurrence, new DateOnly(2026, 9, 20)));
        Assert.False(OccurrenceDateRange.OccursOnDate(occurrence, new DateOnly(2026, 9, 21)));
    }

    [Fact]
    public void ConstructorRejectsTimedOccurrenceOutsideViewerLocalTimezone()
    {
        var local = LocalAt(2026, 9, 20, 9);
        var nonLocalOffset = local.Offset == TimeSpan.FromHours(14)
            ? TimeSpan.FromHours(-14)
            : TimeSpan.FromHours(14);
        var nonLocal = new DateTimeOffset(local.DateTime, nonLocalOffset);

        var exception = Assert.Throws<ArgumentException>(() => new CalendarOccurrence(
            "college",
            "not-normalized",
            nonLocal,
            nonLocal.AddHours(1),
            false,
            "Wrong zone"));

        Assert.Equal("start", exception.ParamName);
    }

    private static CalendarOccurrence Occurrence(string uid, DateTimeOffset start) => new(
        "college",
        uid,
        start,
        start.AddHours(1),
        false,
        "Calculus II");

    private static DateTimeOffset LocalAt(int year, int month, int day, int hour)
    {
        var local = new DateTime(year, month, day, hour, 0, 0, DateTimeKind.Unspecified);
        return new DateTimeOffset(local, TimeZoneInfo.Local.GetUtcOffset(local));
    }
}
