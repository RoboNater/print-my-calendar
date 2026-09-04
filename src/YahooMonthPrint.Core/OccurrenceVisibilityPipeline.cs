namespace YahooMonthPrint.Core;

public static class OccurrenceVisibilityPipeline
{
    public static VisibilityResult Evaluate(CalendarOccurrence occurrence, MonthViewState state)
    {
        ArgumentNullException.ThrowIfNull(occurrence);
        ArgumentNullException.ThrowIfNull(state);

        if (!state.EnabledCalendars.Contains(occurrence.CalendarId))
        {
            return new(false, HiddenReason.CalendarDisabled);
        }

        if (state.DisabledTitles.Contains(occurrence.Title))
        {
            return new(false, HiddenReason.TitleDisabled);
        }

        var filterText = state.QuickFilterText.Trim();
        if (filterText.Length > 0)
        {
            var matches = Contains(occurrence.Title, filterText)
                || Contains(occurrence.Description, filterText)
                || Contains(occurrence.Location, filterText);

            if (state.QuickFilterMode == QuickFilterMode.ShowOnlyMatching && !matches)
            {
                return new(false, HiddenReason.QuickFilterNotMatched);
            }

            if (state.QuickFilterMode == QuickFilterMode.HideMatching && matches)
            {
                return new(false, HiddenReason.QuickFilterMatched);
            }
        }

        if (state.HiddenOccurrences.Contains(occurrence.Key))
        {
            return new(false, HiddenReason.ManuallyHidden);
        }

        return new(true);
    }

    public static IReadOnlyList<CalendarOccurrence> Apply(
        IEnumerable<CalendarOccurrence> occurrences,
        MonthViewState state) => occurrences
        .Where(occurrence => Evaluate(occurrence, state).IsVisible)
        .Order(OccurrenceComparer.Instance)
        .ToArray();

    private static bool Contains(string value, string filterText) =>
        value.Contains(filterText, StringComparison.OrdinalIgnoreCase);
}
