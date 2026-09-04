using YahooMonthPrint.Core;

namespace YahooMonthPrint.Core.Tests;

public sealed class MonthGridTests
{
    [Fact]
    public void CreateReturnsFiveWeeksForCompactMonth()
    {
        var range = MonthGrid.Create(2026, 2);

        Assert.Equal(new DateOnly(2026, 2, 1), range.Start);
        Assert.Equal(new DateOnly(2026, 3, 8), range.EndExclusive);
        Assert.Equal(5, range.WeekCount);
        Assert.Equal(35, range.Dates.Count);
    }

    [Fact]
    public void CreateReturnsSixWeeksWhenMonthSpansSixCalendarRows()
    {
        var range = MonthGrid.Create(2026, 8);

        Assert.Equal(new DateOnly(2026, 7, 26), range.Start);
        Assert.Equal(new DateOnly(2026, 9, 6), range.EndExclusive);
        Assert.Equal(6, range.WeekCount);
    }

    [Theory]
    [InlineData(2024, 2, 29)]
    [InlineData(2025, 2, 28)]
    public void CreateContainsEveryDayInFebruary(int year, int month, int lastDay)
    {
        var range = MonthGrid.Create(year, month);

        Assert.Contains(new DateOnly(year, month, lastDay), range.Dates);
    }

    [Fact]
    public void CreateIncludesLeadingAndTrailingDatesAcrossYearBoundary()
    {
        var range = MonthGrid.Create(2026, 12);

        Assert.Equal(new DateOnly(2026, 11, 29), range.Start);
        Assert.Equal(new DateOnly(2027, 1, 3), range.EndExclusive);
        Assert.True(range.Contains(new DateOnly(2027, 1, 2)));
        Assert.False(range.Contains(range.EndExclusive));
    }
}
