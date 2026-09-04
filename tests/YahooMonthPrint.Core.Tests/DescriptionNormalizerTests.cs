using YahooMonthPrint.Core;

namespace YahooMonthPrint.Core.Tests;

public sealed class DescriptionNormalizerTests
{
    [Fact]
    public void NormalizeDecodesIcsEscapingAndPreservesParagraphs()
    {
        var result = DescriptionNormalizer.Normalize(
            "  Exam 2\\nChapters 5\\, 6\\; and 7.  \r\n\r\n\r\n  Bring   calculator.  ");

        Assert.Equal(
            $"Exam 2{Environment.NewLine}Chapters 5, 6; and 7.{Environment.NewLine}{Environment.NewLine}Bring calculator.",
            result);
    }

    [Fact]
    public void NormalizeLeavesHtmlLikeInputAsInertPlainText()
    {
        const string input = "<script>alert('no')</script> <b>Study</b>";

        Assert.Equal(input, DescriptionNormalizer.Normalize(input));
    }

    [Fact]
    public void NormalizeReturnsEmptyForWhitespace()
    {
        Assert.Empty(DescriptionNormalizer.Normalize(" \r\n\t "));
        Assert.Empty(DescriptionNormalizer.Normalize(null));
    }
}
