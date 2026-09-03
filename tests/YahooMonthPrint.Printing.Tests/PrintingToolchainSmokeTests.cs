namespace YahooMonthPrint.Printing.Tests;

public sealed class PrintingToolchainSmokeTests
{
    [Fact]
    public void FixedDocumentCanBeCreatedForLandscapeLetterPage()
    {
        Exception? testFailure = null;
        var staThread = new Thread(() =>
        {
            try
            {
                var document = PrintingToolchainProbe.CreateEmptyDocument(1_056, 816);

                var page = Assert.Single(document.Pages);
                Assert.Equal(1_056, page.Child.Width);
                Assert.Equal(816, page.Child.Height);
            }
            catch (Exception exception)
            {
                testFailure = exception;
            }
        });

        staThread.SetApartmentState(ApartmentState.STA);
        staThread.Start();
        staThread.Join();

        Assert.Null(testFailure);
    }
}
