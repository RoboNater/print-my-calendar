using System.Windows.Documents;

namespace YahooMonthPrint.Printing;

/// <summary>
/// Provides a narrow Phase 1 proof that FixedDocument APIs are available.
/// Production print layout is implemented in Phase 4.
/// </summary>
public static class PrintingToolchainProbe
{
    public static FixedDocument CreateEmptyDocument(double pageWidth, double pageHeight)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageHeight);

        var page = new FixedPage
        {
            Width = pageWidth,
            Height = pageHeight,
        };

        var content = new PageContent
        {
            Child = page,
        };

        var document = new FixedDocument();
        document.Pages.Add(content);
        return document;
    }
}
