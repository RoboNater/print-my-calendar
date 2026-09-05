using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace YahooMonthPrint.Printing;

public static class FixedDocumentPngExporter
{
    public static IReadOnlyList<string> Export(
        RenderedMonthDocument rendered,
        string outputDirectory,
        string filePrefix)
    {
        ArgumentNullException.ThrowIfNull(rendered);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePrefix);
        Directory.CreateDirectory(outputDirectory);

        var paths = new List<string>(rendered.PageCount);
        for (var index = 0; index < rendered.Document.Pages.Count; index++)
        {
            var page = rendered.Document.Pages[index].Child;
            page.Measure(new Size(page.Width, page.Height));
            page.Arrange(new Rect(0, 0, page.Width, page.Height));
            page.UpdateLayout();
            var bitmap = new RenderTargetBitmap(
                (int)Math.Ceiling(page.Width),
                (int)Math.Ceiling(page.Height),
                96,
                96,
                PixelFormats.Pbgra32);
            bitmap.Render(page);

            var path = Path.Combine(outputDirectory, $"{filePrefix}-page-{index + 1}.png");
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream = File.Create(path);
            encoder.Save(stream);
            paths.Add(path);
        }

        return paths;
    }
}
