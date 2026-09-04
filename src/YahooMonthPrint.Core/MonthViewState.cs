namespace YahooMonthPrint.Core;

public sealed record MonthViewState
{
    public MonthViewState(DateOnly displayedMonth)
    {
        DisplayedMonth = new DateOnly(displayedMonth.Year, displayedMonth.Month, 1);
    }

    public DateOnly DisplayedMonth { get; init; }

    public DetailLevel DetailLevel { get; init; } = DetailLevel.Detailed;

    public int MaximumDescriptionLines { get; init; } = 3;

    public bool ShowLocations { get; init; } = true;

    public string QuickFilterText { get; init; } = string.Empty;

    public QuickFilterMode QuickFilterMode { get; init; } = QuickFilterMode.HideMatching;

    public IReadOnlySet<string> EnabledCalendars { get; init; } = new HashSet<string>(StringComparer.Ordinal);

    public IReadOnlySet<string> DisabledTitles { get; init; } = new HashSet<string>(StringComparer.Ordinal);

    public IReadOnlySet<OccurrenceKey> HiddenOccurrences { get; init; } = new HashSet<OccurrenceKey>();

    public MonthViewState Hide(OccurrenceKey key) => this with
    {
        HiddenOccurrences = HiddenOccurrences.Append(key).ToHashSet(),
    };

    public MonthViewState Restore(OccurrenceKey key) => this with
    {
        HiddenOccurrences = HiddenOccurrences.Where(item => item != key).ToHashSet(),
    };

    public MonthViewState RestoreAll() => this with
    {
        HiddenOccurrences = new HashSet<OccurrenceKey>(),
    };
}
