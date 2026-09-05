namespace YahooMonthPrint.Printing;

/// <summary>
/// Reconciles the page setup the preview asked for with what a printer can physically deliver.
/// The maths is kept free of <c>System.Printing</c> types so it can be tested without a printer.
/// </summary>
public static class PrinterPageNegotiation
{
    /// <summary>
    /// Printer imageable areas arrive as device units converted to DIPs, so they routinely land a
    /// fraction of a DIP away from a requested whole-fraction margin. A Canon TR4700 at Letter, for
    /// example, reports a 24.32 DIP bottom margin against a requested 24 DIP (0.25 in) margin.
    /// Differences below this threshold are measurement noise, not a page setup the printer changed.
    /// </summary>
    public const double MarginToleranceDips = 1;

    /// <summary>
    /// Converts a printer's imageable area into the margins that area implies for <paramref name="page"/>.
    /// The origin and extent are expressed in the page's own orientation, so the caller must pass the
    /// geometry that produced the capabilities, not the portrait sheet.
    /// </summary>
    public static PrintMargins RequiredMargins(
        PrintPageGeometry page,
        double originWidth,
        double originHeight,
        double extentWidth,
        double extentHeight)
    {
        ArgumentNullException.ThrowIfNull(page);
        return new PrintMargins(
            Math.Max(0, originWidth),
            Math.Max(0, originHeight),
            Math.Max(0, page.Width - originWidth - extentWidth),
            Math.Max(0, page.Height - originHeight - extentHeight));
    }

    /// <summary>Widens <paramref name="requested"/> on any edge the printer cannot reach.</summary>
    public static PrintMargins ApplyMinimum(PrintMargins requested, PrintMargins? minimum)
    {
        ArgumentNullException.ThrowIfNull(requested);
        return minimum is null
            ? requested
            : new PrintMargins(
                Math.Max(requested.Left, minimum.Left),
                Math.Max(requested.Top, minimum.Top),
                Math.Max(requested.Right, minimum.Right),
                Math.Max(requested.Bottom, minimum.Bottom));
    }

    /// <summary>
    /// Reports whether two margin sets differ by enough to be worth re-rendering or telling the user
    /// about. Exact equality is the wrong test: driver noise below <see cref="MarginToleranceDips"/>
    /// is invisible on paper and must not force the user to press Print a second time.
    /// </summary>
    public static bool DiffersBeyondTolerance(PrintMargins left, PrintMargins right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        return Exceeds(left.Left, right.Left)
            || Exceeds(left.Top, right.Top)
            || Exceeds(left.Right, right.Right)
            || Exceeds(left.Bottom, right.Bottom);
    }

    private static bool Exceeds(double left, double right) =>
        Math.Abs(left - right) > MarginToleranceDips;
}
