using YahooMonthPrint.Core;

namespace YahooMonthPrint.Core.Tests;

public sealed class CalendarOccurrenceTests
{
    [Fact]
    public void KeyDistinguishesOccurrencesFromSameRecurringSeries()
    {
        var first = Occurrence("series", new DateTimeOffset(2026, 9, 7, 9, 0, 0, TimeSpan.FromHours(-4)));
        var second = Occurrence("series", new DateTimeOffset(2026, 9, 14, 9, 0, 0, TimeSpan.FromHours(-4)));

        Assert.NotEqual(first.Key, second.Key);
    }

    [Fact]
    public void KeyUsesRecurrenceIdForMovedOverride()
    {
        var recurrenceId = new DateTimeOffset(2026, 9, 14, 9, 0, 0, TimeSpan.FromHours(-4));
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
        var timed = Occurrence("timed", new DateTimeOffset(2026, 9, 7, 8, 0, 0, TimeSpan.Zero));
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

    private static CalendarOccurrence Occurrence(string uid, DateTimeOffset start) => new(
        "college",
        uid,
        start,
        start.AddHours(1),
        false,
        "Calculus II");
}
