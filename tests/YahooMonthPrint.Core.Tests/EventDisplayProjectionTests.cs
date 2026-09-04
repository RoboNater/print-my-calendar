using YahooMonthPrint.Core;

namespace YahooMonthPrint.Core.Tests;

public sealed class EventDisplayProjectionTests
{
    private static readonly CalendarOccurrence Occurrence = new(
        "college",
        "exam",
        LocalAt(2026, 9, 14, 9),
        LocalAt(2026, 9, 14, 10),
        false,
        "Calculus II",
        "EXAM 2\nChapters 5-7\nBring calculator\nBring student ID",
        "Science 201");

    [Fact]
    public void TitlesOnlyContainsTimeAndTitleOnly()
    {
        var result = EventDisplayProjection.Create(
            Occurrence,
            new MonthViewState(new DateOnly(2026, 9, 1)) { DetailLevel = DetailLevel.TitlesOnly });

        Assert.Equal("Calculus II", result.Title);
        Assert.NotEmpty(result.TimeText);
        Assert.Empty(result.DescriptionLines);
        Assert.Empty(result.Location);
    }

    [Fact]
    public void CompactContainsFirstUsefulDescriptionLine()
    {
        var result = EventDisplayProjection.Create(
            Occurrence,
            new MonthViewState(new DateOnly(2026, 9, 1)) { DetailLevel = DetailLevel.Compact });

        Assert.Equal(["EXAM 2"], result.DescriptionLines);
        Assert.Empty(result.Location);
    }

    [Fact]
    public void DetailedHonorsDescriptionLimitAndLocationSetting()
    {
        var result = EventDisplayProjection.Create(
            Occurrence,
            new MonthViewState(new DateOnly(2026, 9, 1))
            {
                DetailLevel = DetailLevel.Detailed,
                MaximumDescriptionLines = 3,
                ShowLocations = true,
            });

        Assert.Equal(["EXAM 2", "Chapters 5-7", "Bring calculator"], result.DescriptionLines);
        Assert.Equal("Science 201", result.Location);
    }

    [Fact]
    public void AllDayOmitsTime()
    {
        var allDay = new CalendarOccurrence(
            "college",
            "deadline",
            Occurrence.Start.Date,
            Occurrence.Start.Date.AddDays(1),
            true,
            "Deadline");

        var result = EventDisplayProjection.Create(allDay, new MonthViewState(new DateOnly(2026, 9, 1)));

        Assert.Empty(result.TimeText);
    }

    private static DateTimeOffset LocalAt(int year, int month, int day, int hour)
    {
        var local = new DateTime(year, month, day, hour, 0, 0, DateTimeKind.Unspecified);
        return new DateTimeOffset(local, TimeZoneInfo.Local.GetUtcOffset(local));
    }
}
