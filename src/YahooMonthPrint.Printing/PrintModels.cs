using YahooMonthPrint.Core;

namespace YahooMonthPrint.Printing;

public enum PrintPaperSize
{
    Letter,
    A4,
}

public enum PrintPageOrientation
{
    Landscape,
    Portrait,
}

public enum PrintOverflowPolicy
{
    ReduceDetailAutomatically,
    UseSmallerText,
    PrintDetailsPages,
}

public enum PrintReductionStep
{
    RemoveLocation,
    ReduceDescriptionLines,
    TightenSpacing,
    ReduceFontSize,
    OverflowDetailsPage,
}

public sealed record PrintMargins(double Left, double Top, double Right, double Bottom)
{
    public static PrintMargins QuarterInch { get; } = new(24, 24, 24, 24);

    public void Validate()
    {
        ArgumentOutOfRangeException.ThrowIfNegative(Left);
        ArgumentOutOfRangeException.ThrowIfNegative(Top);
        ArgumentOutOfRangeException.ThrowIfNegative(Right);
        ArgumentOutOfRangeException.ThrowIfNegative(Bottom);
    }
}

public sealed record PrintPageGeometry(
    double Width,
    double Height,
    PrintPaperSize PaperSize,
    PrintPageOrientation Orientation)
{
    private const double DipsPerInch = 96;

    public static PrintPageGeometry Create(
        PrintPaperSize paperSize,
        PrintPageOrientation orientation = PrintPageOrientation.Landscape)
    {
        var portrait = paperSize switch
        {
            PrintPaperSize.Letter => (Width: 8.5 * DipsPerInch, Height: 11 * DipsPerInch),
            PrintPaperSize.A4 => (Width: 210d / 25.4 * DipsPerInch, Height: 297d / 25.4 * DipsPerInch),
            _ => throw new ArgumentOutOfRangeException(nameof(paperSize)),
        };
        return orientation == PrintPageOrientation.Landscape
            ? new PrintPageGeometry(portrait.Height, portrait.Width, paperSize, orientation)
            : new PrintPageGeometry(portrait.Width, portrait.Height, paperSize, orientation);
    }
}

public sealed record MonthPrintOptions
{
    public PrintPageGeometry Page { get; init; } = PrintPageGeometry.Create(PrintPaperSize.Letter);

    public PrintMargins Margins { get; init; } = PrintMargins.QuarterInch;

    public DetailLevel DetailLevel { get; init; } = DetailLevel.Detailed;

    public int DescriptionLineLimit { get; init; } = 3;

    public bool ShowLocations { get; init; } = true;

    public double BodyFontSizePoints { get; init; } = 9;

    public PrintOverflowPolicy OverflowPolicy { get; init; } = PrintOverflowPolicy.ReduceDetailAutomatically;

    public void Validate()
    {
        Margins.Validate();
        ArgumentOutOfRangeException.ThrowIfNegative(DescriptionLineLimit);
        if (BodyFontSizePoints is < 7 or > 14)
        {
            throw new ArgumentOutOfRangeException(
                nameof(BodyFontSizePoints),
                "Print body text must be between 7 and 14 points.");
        }

        if (Margins.Left + Margins.Right >= Page.Width
            || Margins.Top + Margins.Bottom >= Page.Height)
        {
            throw new ArgumentOutOfRangeException(nameof(Margins), "Margins leave no printable page area.");
        }
    }
}

public sealed record PrintOccurrenceModel(
    OccurrenceKey Key,
    DateOnly Date,
    bool IsAllDay,
    string TimeText,
    string Title,
    IReadOnlyList<string> DescriptionLines,
    IReadOnlyList<string> FullDescriptionLines,
    string Location);

public sealed record PrintDayModel(
    DateOnly Date,
    bool IsInDisplayedMonth,
    IReadOnlyList<PrintOccurrenceModel> Occurrences);

public sealed record MonthLayoutModel(
    DateOnly DisplayedMonth,
    MonthGridRange Grid,
    IReadOnlyList<PrintDayModel> Days,
    IReadOnlyList<OccurrenceKey> VisibleOccurrenceKeys,
    MonthPrintOptions RequestedOptions);

public sealed record PrintDayLayout(
    PrintDayModel Day,
    IReadOnlyList<PrintOccurrenceModel> MainPageOccurrences,
    int OverflowCount);

public sealed record PrintLayoutDiagnostic(PrintReductionStep Step, string Message);

public sealed record MonthPrintPlan(
    MonthLayoutModel Model,
    MonthPrintOptions EffectiveOptions,
    double EventSpacing,
    IReadOnlyList<PrintDayLayout> Days,
    IReadOnlyList<PrintOccurrenceModel> OverflowOccurrences,
    IReadOnlyList<PrintLayoutDiagnostic> Diagnostics)
{
    public bool HasOverflow => OverflowOccurrences.Count > 0;

    public int MinimumPageCount => HasOverflow ? 2 : 1;
}
