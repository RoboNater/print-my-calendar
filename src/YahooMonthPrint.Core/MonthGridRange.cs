namespace YahooMonthPrint.Core;

public sealed record MonthGridRange(
    DateOnly DisplayedMonth,
    DateOnly Start,
    DateOnly EndExclusive,
    int WeekCount)
{
    public IReadOnlyList<DateOnly> Dates => Enumerable.Range(0, WeekCount * 7)
        .Select(Start.AddDays)
        .ToArray();

    public bool Contains(DateOnly date) => date >= Start && date < EndExclusive;
}
