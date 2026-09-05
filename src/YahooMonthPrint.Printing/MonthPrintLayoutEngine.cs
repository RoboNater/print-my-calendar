using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace YahooMonthPrint.Printing;

public interface IPrintTextMeasurer
{
    double MeasureHeight(string text, double width, double fontSizeDips, bool bold = false);
}

public sealed class WpfPrintTextMeasurer : IPrintTextMeasurer
{
    public double MeasureHeight(string text, double width, double fontSizeDips, bool bold = false)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }

        var typeface = new Typeface(
            new FontFamily("Segoe UI"),
            FontStyles.Normal,
            bold ? FontWeights.SemiBold : FontWeights.Normal,
            FontStretches.Normal);
        var formatted = new FormattedText(
            text,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            typeface,
            fontSizeDips,
            Brushes.Black,
            1);
        formatted.MaxTextWidth = Math.Max(1, width);
        formatted.LineHeight = fontSizeDips * PrintLayoutMetrics.TextLineHeightMultiplier;
        return Math.Ceiling(formatted.Height);
    }
}

public sealed class MonthPrintLayoutEngine(IPrintTextMeasurer? textMeasurer = null)
{
    public const double MinimumBodyFontSizePoints = 7;
    public const double StandardEventSpacing = 4;
    public const double TightEventSpacing = 1;

    private readonly IPrintTextMeasurer textMeasurer = textMeasurer ?? new WpfPrintTextMeasurer();

    public MonthPrintPlan CreatePlan(MonthLayoutModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        model.RequestedOptions.Validate();

        var options = model.RequestedOptions;
        var spacing = StandardEventSpacing;
        var diagnostics = new List<PrintLayoutDiagnostic>();

        if (Fits(model, options, spacing))
        {
            return CompletePlan(model, options, spacing, diagnostics);
        }

        if (options.OverflowPolicy == PrintOverflowPolicy.ReduceDetailAutomatically)
        {
            if (options.ShowLocations && model.Days.Any(day => day.Occurrences.Any(item => item.Location.Length > 0)))
            {
                options = options with { ShowLocations = false };
                diagnostics.Add(new(
                    PrintReductionStep.RemoveLocation,
                    "Locations were removed from the month grid to make more room."));
                if (Fits(model, options, spacing))
                {
                    return CompletePlan(model, options, spacing, diagnostics);
                }
            }

            var originalDescriptionLineLimit = options.DescriptionLineLimit;
            var minimumDescriptionLineLimit = options.DetailLevel == YahooMonthPrint.Core.DetailLevel.TitlesOnly
                ? 0
                : 1;
            while (options.DescriptionLineLimit > minimumDescriptionLineLimit)
            {
                options = options with { DescriptionLineLimit = options.DescriptionLineLimit - 1 };
                if (Fits(model, options, spacing))
                {
                    diagnostics.Add(DescriptionReduction(options.DescriptionLineLimit));
                    return CompletePlan(model, options, spacing, diagnostics);
                }
            }

            if (options.DescriptionLineLimit != originalDescriptionLineLimit)
            {
                diagnostics.Add(DescriptionReduction(options.DescriptionLineLimit));
            }

            spacing = TightEventSpacing;
            diagnostics.Add(new(
                PrintReductionStep.TightenSpacing,
                "Spacing between events was tightened."));
            if (Fits(model, options, spacing))
            {
                return CompletePlan(model, options, spacing, diagnostics);
            }
        }

        if (options.OverflowPolicy is PrintOverflowPolicy.ReduceDetailAutomatically
            or PrintOverflowPolicy.UseSmallerText)
        {
            var originalFontSize = options.BodyFontSizePoints;
            while (options.BodyFontSizePoints > MinimumBodyFontSizePoints)
            {
                options = options with
                {
                    BodyFontSizePoints = Math.Max(
                        MinimumBodyFontSizePoints,
                        options.BodyFontSizePoints - 0.5),
                };
                if (Fits(model, options, spacing))
                {
                    diagnostics.Add(FontReduction(options.BodyFontSizePoints));
                    return CompletePlan(model, options, spacing, diagnostics);
                }
            }

            if (options.BodyFontSizePoints != originalFontSize)
            {
                diagnostics.Add(FontReduction(options.BodyFontSizePoints));
            }
        }

        if (model.RequestedOptions.OverflowPolicy == PrintOverflowPolicy.ReduceDetailAutomatically)
        {
            options = model.RequestedOptions;
            spacing = StandardEventSpacing;
            diagnostics.Clear();
        }

        diagnostics.Add(new(
            PrintReductionStep.OverflowDetailsPage,
            "Details that do not fit in the month grid will be printed on additional pages."));
        return OverflowPlan(model, options, spacing, diagnostics);
    }

    private static MonthPrintPlan CompletePlan(
        MonthLayoutModel model,
        MonthPrintOptions options,
        double spacing,
        IReadOnlyList<PrintLayoutDiagnostic> diagnostics) => new(
            model,
            options,
            spacing,
            model.Days.Select(day => new PrintDayLayout(day, day.Occurrences, 0)).ToArray(),
            [],
            diagnostics);

    private static PrintLayoutDiagnostic DescriptionReduction(int lineLimit) => new(
        PrintReductionStep.ReduceDescriptionLines,
        $"Description lines were reduced to {lineLimit}.");

    private static PrintLayoutDiagnostic FontReduction(double fontSizePoints) => new(
        PrintReductionStep.ReduceFontSize,
        $"Body text was reduced to {fontSizePoints:0.#} pt.");

    private MonthPrintPlan OverflowPlan(
        MonthLayoutModel model,
        MonthPrintOptions options,
        double spacing,
        IReadOnlyList<PrintLayoutDiagnostic> diagnostics)
    {
        var availableHeight = CellContentHeight(model, options);
        var width = CellTextWidth(options);
        var dateHeaderHeight = MeasureDateHeader(options, width);
        var overflowMarkerHeight = MeasureOverflowMarker(options, width);
        var days = new List<PrintDayLayout>(model.Days.Count);
        var overflow = new List<PrintOccurrenceModel>();
        foreach (var day in model.Days)
        {
            var used = dateHeaderHeight;
            var main = new List<PrintOccurrenceModel>();
            var dayOverflow = new List<PrintOccurrenceModel>();
            var measuredHeights = new List<double>();
            foreach (var occurrence in day.Occurrences)
            {
                var occurrenceHeight = MeasureOccurrence(occurrence, options, width) + spacing;
                if (used + occurrenceHeight <= availableHeight)
                {
                    main.Add(occurrence);
                    measuredHeights.Add(occurrenceHeight);
                    used += occurrenceHeight;
                }
                else
                {
                    dayOverflow.Add(occurrence);
                }
            }

            while (dayOverflow.Count > 0 && main.Count > 0 && used + overflowMarkerHeight > availableHeight)
            {
                var lastIndex = main.Count - 1;
                used -= measuredHeights[lastIndex];
                dayOverflow.Insert(0, main[lastIndex]);
                main.RemoveAt(lastIndex);
                measuredHeights.RemoveAt(lastIndex);
            }

            overflow.AddRange(dayOverflow);
            days.Add(new PrintDayLayout(day, main, dayOverflow.Count));
        }

        return new MonthPrintPlan(model, options, spacing, days, overflow, diagnostics);
    }

    private bool Fits(MonthLayoutModel model, MonthPrintOptions options, double spacing)
    {
        var availableHeight = CellContentHeight(model, options);
        var width = CellTextWidth(options);
        return model.Days.All(day =>
            MeasureDateHeader(options, width)
            + day.Occurrences.Sum(item => MeasureOccurrence(item, options, width) + spacing)
            <= availableHeight);
    }

    private double MeasureOccurrence(
        PrintOccurrenceModel occurrence,
        MonthPrintOptions options,
        double width)
    {
        var fontSize = PointsToDips(options.BodyFontSizePoints);
        var height = textMeasurer.MeasureHeight(occurrence.TimeText, width, fontSize * 0.9);
        height += textMeasurer.MeasureHeight(occurrence.Title, width, fontSize, bold: true);
        var description = string.Join(
            Environment.NewLine,
            occurrence.DescriptionLines.Take(options.DescriptionLineLimit));
        height += textMeasurer.MeasureHeight(description, width, fontSize * 0.92);
        if (options.ShowLocations)
        {
            height += textMeasurer.MeasureHeight(occurrence.Location, width, fontSize * 0.88);
        }

        return Math.Ceiling(height);
    }

    private double MeasureDateHeader(MonthPrintOptions options, double width)
    {
        var fontSize = PointsToDips(options.BodyFontSizePoints);
        return textMeasurer.MeasureHeight("30", width, fontSize, bold: true);
    }

    private double MeasureOverflowMarker(MonthPrintOptions options, double width)
    {
        var fontSize = PointsToDips(options.BodyFontSizePoints);
        return 1 + textMeasurer.MeasureHeight(
            "+99 on details page",
            width,
            Math.Max(8, fontSize * 0.82),
            bold: true);
    }

    private static double CellContentHeight(MonthLayoutModel model, MonthPrintOptions options) =>
        (options.Page.Height
            - options.Margins.Top
            - options.Margins.Bottom
            - PrintLayoutMetrics.HeaderHeight
            - PrintLayoutMetrics.WeekdayHeight)
        / model.Grid.WeekCount
        - PrintLayoutMetrics.CellPadding * 2
        - PrintLayoutMetrics.GridBorderThickness * 2;

    private static double CellTextWidth(MonthPrintOptions options) =>
        (options.Page.Width - options.Margins.Left - options.Margins.Right) / 7
        - PrintLayoutMetrics.CellPadding * 2
        - PrintLayoutMetrics.GridBorderThickness * 2;

    internal static double PointsToDips(double points) => points * 96 / 72;
}
