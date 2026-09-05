using YahooMonthPrint.Core;

namespace YahooMonthPrint.Printing.Tests;

public sealed class MonthPrintingTests
{
    [Theory]
    [InlineData(PrintPaperSize.Letter, 1056, 816)]
    [InlineData(PrintPaperSize.A4, 1122.52, 793.70)]
    public void LandscapePageGeometryUsesPhysicalPaperDimensions(
        PrintPaperSize paper,
        double expectedWidth,
        double expectedHeight)
    {
        var geometry = PrintPageGeometry.Create(paper);

        Assert.Equal(expectedWidth, geometry.Width, 1);
        Assert.Equal(expectedHeight, geometry.Height, 1);
        Assert.Equal(PrintPageOrientation.Landscape, geometry.Orientation);
        Assert.True(geometry.Width > geometry.Height);
    }

    [Theory]
    [InlineData(2026, 9, 5)]
    [InlineData(2026, 8, 6)]
    public void ModelPreservesFiveAndSixWeekGridGeometry(int year, int month, int weeks)
    {
        var model = MonthLayoutModelBuilder.Build(
            new DateOnly(year, month, 1),
            [],
            new MonthPrintOptions());

        Assert.Equal(weeks, model.Grid.WeekCount);
        Assert.Equal(weeks * 7, model.Days.Count);
        Assert.Equal(7, model.Days.Count / weeks);
    }

    [Fact]
    public void ModelContainsExactlyTheAuthoritativeVisibleOccurrenceSet()
    {
        var shown = Occurrence("shown", 14, 9);
        var hiddenByCaller = Occurrence("hidden", 14, 10);

        var model = MonthLayoutModelBuilder.Build(
            new DateOnly(2026, 9, 1),
            [shown],
            new MonthPrintOptions());

        Assert.Equal([shown.Key], model.VisibleOccurrenceKeys);
        var printedKeys = model.Days
            .SelectMany(day => day.Occurrences)
            .Select(item => item.Key)
            .Distinct()
            .ToArray();
        Assert.Equal([shown.Key], printedKeys);
        Assert.DoesNotContain(hiddenByCaller.Key, printedKeys);
    }

    [Fact]
    public void AllDayEventsSortBeforeTimedEventsAndUseExpectedTimeText()
    {
        var timed = Occurrence("Timed", 14, 9);
        var allDayStart = new DateTimeOffset(2026, 9, 14, 0, 0, 0, TimeSpan.Zero);
        var allDay = new CalendarOccurrence(
            "college",
            "all-day",
            allDayStart,
            allDayStart.AddDays(1),
            true,
            "All day");

        var day = MonthLayoutModelBuilder.Build(
            new DateOnly(2026, 9, 1),
            [timed, allDay],
            new MonthPrintOptions()).Days.Single(item => item.Date == new DateOnly(2026, 9, 14));

        Assert.Equal(["All day", "Timed"], day.Occurrences.Select(item => item.Title));
        Assert.Equal(string.Empty, day.Occurrences[0].TimeText);
        Assert.Equal("9:00 AM", day.Occurrences[1].TimeText);
    }

    [Fact]
    public void AutomaticReductionUsesSpecifiedOrderAndReportsEveryDecision()
    {
        var model = MonthLayoutModelBuilder.Build(
            new DateOnly(2026, 9, 1),
            [
                Occurrence("First", 14, 9, "description", "ROOM"),
                Occurrence("Second", 14, 10, "description", "ROOM"),
            ],
            new MonthPrintOptions { DescriptionLineLimit = 1, ShowLocations = true });
        var engine = new MonthPrintLayoutEngine(new ConstantTextMeasurer(20));

        var plan = engine.CreatePlan(model);

        Assert.False(plan.HasOverflow);
        Assert.False(plan.EffectiveOptions.ShowLocations);
        Assert.Equal(0, plan.EffectiveOptions.DescriptionLineLimit);
        Assert.Equal(
            [PrintReductionStep.RemoveLocation, PrintReductionStep.ReduceDescriptionLines],
            plan.Diagnostics.Select(item => item.Step));
    }

    [Fact]
    public void DenseDayNeverDropsAnEventAndRespectsMinimumFontSize()
    {
        var occurrences = Enumerable.Range(0, 30)
            .Select(index => Occurrence($"Event {index}", 14, 8 + index % 12))
            .ToArray();
        var model = MonthLayoutModelBuilder.Build(
            new DateOnly(2026, 9, 1),
            occurrences,
            new MonthPrintOptions
            {
                DetailLevel = DetailLevel.TitlesOnly,
                ShowLocations = false,
            });
        var plan = new MonthPrintLayoutEngine(new ConstantTextMeasurer(12)).CreatePlan(model);
        var day = plan.Days.Single(item => item.Day.Date == new DateOnly(2026, 9, 14));

        Assert.True(plan.HasOverflow);
        Assert.Equal(occurrences.Length, day.MainPageOccurrences.Count + day.OverflowCount);
        Assert.Equal(day.OverflowCount, plan.OverflowOccurrences.Count);
        Assert.True(plan.EffectiveOptions.BodyFontSizePoints >= MonthPrintLayoutEngine.MinimumBodyFontSizePoints);
        Assert.Equal(PrintReductionStep.OverflowDetailsPage, plan.Diagnostics[^1].Step);
    }

    [Fact]
    public void LongUnbrokenAndMultilineDetailsRemainAvailableOnDetailsPage()
    {
        var description = $"first line{Environment.NewLine}{new string('x', 8000)}";
        var occurrences = Enumerable.Range(0, 20)
            .Select(index => Occurrence($"Dense {index}", 14, 9, description, "Science 201"))
            .ToArray();
        var model = MonthLayoutModelBuilder.Build(
            new DateOnly(2026, 9, 1),
            occurrences,
            new MonthPrintOptions { OverflowPolicy = PrintOverflowPolicy.PrintDetailsPages });
        var plan = new MonthPrintLayoutEngine(new ConstantTextMeasurer(18)).CreatePlan(model);

        Assert.True(plan.HasOverflow);
        Assert.All(plan.OverflowOccurrences, item => Assert.Equal(description.Length, string.Join(Environment.NewLine, item.FullDescriptionLines).Length));
    }

    [Fact]
    public void RendererUsesPlanGeometryAndAddsDetailsPagesForOverflow()
    {
        var model = MonthLayoutModelBuilder.Build(
            new DateOnly(2026, 9, 1),
            Enumerable.Range(0, 30)
                .Select(index => Occurrence($"Event {index}", 14, 8 + index % 12))
                .ToArray(),
            new MonthPrintOptions
            {
                Page = PrintPageGeometry.Create(PrintPaperSize.A4),
                DetailLevel = DetailLevel.TitlesOnly,
                OverflowPolicy = PrintOverflowPolicy.PrintDetailsPages,
            });
        var plan = new MonthPrintLayoutEngine(new ConstantTextMeasurer(18)).CreatePlan(model);

        var rendered = RunSta(() =>
        {
            var document = new FixedDocumentRenderer().Render(plan);
            return new RenderFacts(
                document.PageCount,
                document.Document.DocumentPaginator.PageSize.Width,
                document.Document.DocumentPaginator.PageSize.Height,
                document.Document.Pages.Select(page => page.Child.Width).ToArray());
        });

        Assert.True(rendered.PageCount >= 2);
        Assert.Equal(model.RequestedOptions.Page.Width, rendered.PageWidth);
        Assert.Equal(model.RequestedOptions.Page.Height, rendered.PageHeight);
        Assert.All(
            rendered.PageWidths,
            width => Assert.Equal(model.RequestedOptions.Page.Width, width));
    }

    private static T RunSta<T>(Func<T> action)
    {
        T? result = default;
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                result = action();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        Assert.Null(failure);
        return Assert.IsType<T>(result);
    }

    private static CalendarOccurrence Occurrence(
        string title,
        int day,
        int hour,
        string? description = null,
        string? location = null)
    {
        var local = new DateTime(2026, 9, day, hour, 0, 0, DateTimeKind.Unspecified);
        var start = new DateTimeOffset(local, TimeZoneInfo.Local.GetUtcOffset(local));
        return new CalendarOccurrence(
            "college",
            title,
            start,
            start.AddHours(1),
            false,
            title,
            description,
            location);
    }

    private sealed class ConstantTextMeasurer(double height) : IPrintTextMeasurer
    {
        public double MeasureHeight(string text, double width, double fontSizeDips, bool bold = false)
        {
            _ = width;
            _ = fontSizeDips;
            _ = bold;
            return string.IsNullOrEmpty(text) ? 0 : height;
        }
    }

    private sealed record RenderFacts(
        int PageCount,
        double PageWidth,
        double PageHeight,
        IReadOnlyList<double> PageWidths);
}
