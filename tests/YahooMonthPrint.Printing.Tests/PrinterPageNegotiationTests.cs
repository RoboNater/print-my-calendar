namespace YahooMonthPrint.Printing.Tests;

public sealed class PrinterPageNegotiationTests
{
    // Measured from a Canon TR4700 series at Letter via PrintQueue.GetPrintCapabilities.
    private const double LandscapeOriginWidth = 11.20;
    private const double LandscapeOriginHeight = 23.68;
    private const double LandscapeExtentWidth = 1025.76;
    private const double LandscapeExtentHeight = 768.00;
    private const double PortraitOriginWidth = 24.16;
    private const double PortraitOriginHeight = 11.20;
    private const double PortraitExtentWidth = 768.00;
    private const double PortraitExtentHeight = 1025.76;

    [Fact]
    public void ImageableAreaConvertsToTheMarginsItLeavesUnreachable()
    {
        var page = PrintPageGeometry.Create(PrintPaperSize.Letter, PrintPageOrientation.Landscape);

        var required = PrinterPageNegotiation.RequiredMargins(
            page,
            LandscapeOriginWidth,
            LandscapeOriginHeight,
            LandscapeExtentWidth,
            LandscapeExtentHeight);

        Assert.Equal(11.20, required.Left, 2);
        Assert.Equal(23.68, required.Top, 2);
        Assert.Equal(19.04, required.Right, 2);
        Assert.Equal(24.32, required.Bottom, 2);
    }

    [Theory]
    [InlineData(PrintPageOrientation.Landscape)]
    [InlineData(PrintPageOrientation.Portrait)]
    public void SubPixelDriverMarginsNeverCountAsAPageSetupTheUserMustReview(
        PrintPageOrientation orientation)
    {
        // Regression for issue #16: on a real printer the unprintable border misses a 0.25 in
        // request by hundredths of a DIP in both orientations. Treating that as a change made the
        // first Print always stop with "the printer changed the requested paper, orientation, or
        // margins" and forced a second attempt.
        var page = PrintPageGeometry.Create(PrintPaperSize.Letter, orientation);
        var landscape = orientation == PrintPageOrientation.Landscape;
        var required = PrinterPageNegotiation.RequiredMargins(
            page,
            landscape ? LandscapeOriginWidth : PortraitOriginWidth,
            landscape ? LandscapeOriginHeight : PortraitOriginHeight,
            landscape ? LandscapeExtentWidth : PortraitExtentWidth,
            landscape ? LandscapeExtentHeight : PortraitExtentHeight);
        var requested = new PrintMargins(24, 24, 24, 24);

        var adjusted = PrinterPageNegotiation.ApplyMinimum(requested, required);

        Assert.NotEqual(requested, adjusted);
        Assert.False(PrinterPageNegotiation.DiffersBeyondTolerance(adjusted, requested));
    }

    [Fact]
    public void MarginsAPrinterTrulyCannotHonourAreStillReported()
    {
        var page = PrintPageGeometry.Create(PrintPaperSize.Letter, PrintPageOrientation.Landscape);
        var required = PrinterPageNegotiation.RequiredMargins(page, 48, 48, 960, 720);
        var requested = new PrintMargins(24, 24, 24, 24);

        var adjusted = PrinterPageNegotiation.ApplyMinimum(requested, required);

        Assert.Equal(new PrintMargins(48, 48, 48, 48), adjusted);
        Assert.True(PrinterPageNegotiation.DiffersBeyondTolerance(adjusted, requested));
    }

    [Fact]
    public void RequiredMarginsAreOrientationSpecificSoTheyCannotBeReusedAcrossOrientations()
    {
        // The cached minimums used to survive an orientation change, applying the landscape
        // unprintable border to a portrait page and vice versa.
        var landscape = PrinterPageNegotiation.RequiredMargins(
            PrintPageGeometry.Create(PrintPaperSize.Letter, PrintPageOrientation.Landscape),
            LandscapeOriginWidth,
            LandscapeOriginHeight,
            LandscapeExtentWidth,
            LandscapeExtentHeight);
        var portrait = PrinterPageNegotiation.RequiredMargins(
            PrintPageGeometry.Create(PrintPaperSize.Letter, PrintPageOrientation.Portrait),
            PortraitOriginWidth,
            PortraitOriginHeight,
            PortraitExtentWidth,
            PortraitExtentHeight);

        Assert.NotEqual(landscape, portrait);
        Assert.True(PrinterPageNegotiation.DiffersBeyondTolerance(landscape, portrait));
    }

    [Fact]
    public void AnUnknownImageableAreaLeavesTheRequestedMarginsAlone()
    {
        var requested = new PrintMargins(24, 24, 24, 24);

        Assert.Equal(requested, PrinterPageNegotiation.ApplyMinimum(requested, minimum: null));
    }

    [Fact]
    public void AnImageableAreaLargerThanThePageNeverProducesNegativeMargins()
    {
        var page = PrintPageGeometry.Create(PrintPaperSize.Letter, PrintPageOrientation.Landscape);

        var required = PrinterPageNegotiation.RequiredMargins(page, 0, 0, page.Width + 8, page.Height + 8);

        Assert.Equal(0, required.Right);
        Assert.Equal(0, required.Bottom);
    }
}
