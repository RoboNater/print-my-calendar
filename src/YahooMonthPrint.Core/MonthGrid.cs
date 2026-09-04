namespace YahooMonthPrint.Core;

public static class MonthGrid
{
    public static MonthGridRange Create(int year, int month)
    {
        var firstOfMonth = new DateOnly(year, month, 1);
        var leadingDays = (int)firstOfMonth.DayOfWeek;
        var start = firstOfMonth.AddDays(-leadingDays);
        var lastOfMonth = firstOfMonth.AddMonths(1).AddDays(-1);
        var occupiedDays = leadingDays + lastOfMonth.Day;
        var weekCount = Math.Max(5, (int)Math.Ceiling(occupiedDays / 7d));
        var endExclusive = start.AddDays(weekCount * 7);

        return new MonthGridRange(firstOfMonth, start, endExclusive, weekCount);
    }
}
