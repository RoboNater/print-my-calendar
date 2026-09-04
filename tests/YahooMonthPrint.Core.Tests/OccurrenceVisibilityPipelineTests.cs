using YahooMonthPrint.Core;

namespace YahooMonthPrint.Core.Tests;

public sealed class OccurrenceVisibilityPipelineTests
{
    private static readonly DateOnly September = new(2026, 9, 1);

    [Theory]
    [InlineData("calculus", true)]
    [InlineData("EXAM 2", true)]
    [InlineData("science 201", true)]
    [InlineData("physics", false)]
    public void ShowOnlyMatchingSearchesAllFieldsCaseInsensitively(string text, bool visible)
    {
        var occurrence = Occurrence();
        var state = State() with
        {
            QuickFilterText = text,
            QuickFilterMode = QuickFilterMode.ShowOnlyMatching,
        };

        Assert.Equal(visible, OccurrenceVisibilityPipeline.Evaluate(occurrence, state).IsVisible);
    }

    [Fact]
    public void HideMatchingHidesMatchingOccurrence()
    {
        var result = OccurrenceVisibilityPipeline.Evaluate(
            Occurrence(),
            State() with { QuickFilterText = "exam", QuickFilterMode = QuickFilterMode.HideMatching });

        Assert.Equal(new VisibilityResult(false, HiddenReason.QuickFilterMatched), result);
    }

    [Fact]
    public void PipelineReportsFirstFailedConditionInSpecifiedOrder()
    {
        var occurrence = Occurrence();
        var state = State() with
        {
            EnabledCalendars = new HashSet<string>(),
            DisabledTitles = new HashSet<string>([occurrence.Title], StringComparer.Ordinal),
            QuickFilterText = "exam",
            HiddenOccurrences = new HashSet<OccurrenceKey> { occurrence.Key },
        };

        Assert.Equal(
            HiddenReason.CalendarDisabled,
            OccurrenceVisibilityPipeline.Evaluate(occurrence, state).HiddenReason);
    }

    [Fact]
    public void TitleFilterUsesExactOrdinalTitle()
    {
        var occurrence = Occurrence();

        Assert.False(OccurrenceVisibilityPipeline.Evaluate(
            occurrence,
            State() with { DisabledTitles = new HashSet<string>(["Calculus II"], StringComparer.Ordinal) }).IsVisible);
        Assert.True(OccurrenceVisibilityPipeline.Evaluate(
            occurrence,
            State() with { DisabledTitles = new HashSet<string>(["calculus ii"], StringComparer.Ordinal) }).IsVisible);
    }

    [Fact]
    public void HideRestoreAndRestoreAllDoNotMutateOriginalState()
    {
        var first = Occurrence();
        var second = Occurrence("second", first.Start.AddDays(7));
        var original = State();
        var hidden = original.Hide(first.Key).Hide(second.Key);
        var restored = hidden.Restore(first.Key);
        var restoredAll = restored.RestoreAll();

        Assert.Empty(original.HiddenOccurrences);
        Assert.Equal(2, hidden.HiddenOccurrences.Count);
        Assert.DoesNotContain(first.Key, restored.HiddenOccurrences);
        Assert.Contains(second.Key, restored.HiddenOccurrences);
        Assert.Empty(restoredAll.HiddenOccurrences);
    }

    [Fact]
    public void MonthOccurrenceSetRetainsRawDataAndProducesDeterministicCountsAndTitles()
    {
        var calculus = Occurrence();
        var physics = Occurrence("physics", calculus.Start.AddDays(2), "Physics");
        var october = Occurrence("october", calculus.Start.AddMonths(1), "October item");
        var set = new MonthOccurrenceSet([physics, calculus, october]);
        var state = State() with
        {
            DisabledTitles = new HashSet<string>(["Physics"], StringComparer.Ordinal),
        };

        Assert.Equal(3, set.RawOccurrences.Count);
        Assert.Equal(2, set.Visible(state).Count);
        Assert.Equal(["Calculus II", "Physics"], set.TitlesInDisplayedMonth(September));
    }

    private static MonthViewState State() => new(September)
    {
        EnabledCalendars = new HashSet<string>(["college"], StringComparer.Ordinal),
    };

    private static CalendarOccurrence Occurrence(
        string uid = "calculus",
        DateTimeOffset? start = null,
        string title = "Calculus II")
    {
        var actualStart = start ?? new DateTimeOffset(2026, 9, 14, 9, 0, 0, TimeSpan.Zero);
        return new CalendarOccurrence(
            "college",
            uid,
            actualStart,
            actualStart.AddHours(1),
            false,
            title,
            "EXAM 2",
            "Science 201");
    }
}
