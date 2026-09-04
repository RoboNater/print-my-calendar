namespace YahooMonthPrint.Core;

public sealed class MonthOccurrenceSet
{
    private readonly IReadOnlyList<CalendarOccurrence> occurrences;

    public MonthOccurrenceSet(IEnumerable<CalendarOccurrence> occurrences)
    {
        ArgumentNullException.ThrowIfNull(occurrences);
        this.occurrences = occurrences.ToArray();
    }

    public IReadOnlyList<CalendarOccurrence> RawOccurrences => occurrences;

    public IReadOnlyList<CalendarOccurrence> Visible(MonthViewState state) =>
        OccurrenceVisibilityPipeline.Apply(occurrences, state);

    public IReadOnlyList<string> TitlesInDisplayedMonth(DateOnly displayedMonth) => occurrences
        .Where(occurrence => IntersectsMonth(occurrence, displayedMonth))
        .Select(occurrence => occurrence.Title)
        .Distinct(StringComparer.Ordinal)
        .Order(StringComparer.OrdinalIgnoreCase)
        .ToArray();

    private static bool IntersectsMonth(CalendarOccurrence occurrence, DateOnly displayedMonth)
    {
        var start = new DateTimeOffset(
            displayedMonth.Year,
            displayedMonth.Month,
            1,
            0,
            0,
            0,
            occurrence.Start.Offset);
        var end = start.AddMonths(1);
        return occurrence.Start < end && occurrence.End > start;
    }
}
