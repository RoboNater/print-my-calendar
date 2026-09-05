using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace YahooMonthPrint.Printing;

public sealed record RenderedMonthDocument(FixedDocument Document, MonthPrintPlan Plan)
{
    public int PageCount => Document.Pages.Count;
}

public sealed class FixedDocumentRenderer(IPrintTextMeasurer? textMeasurer = null)
{
    private static readonly Brush GridBrush = new SolidColorBrush(Color.FromRgb(118, 118, 118));
    private static readonly Brush SecondaryBrush = new SolidColorBrush(Color.FromRgb(82, 82, 82));
    private static readonly Brush OutOfMonthBrush = new SolidColorBrush(Color.FromRgb(242, 242, 242));
    private readonly IPrintTextMeasurer textMeasurer = textMeasurer ?? new WpfPrintTextMeasurer();

    public RenderedMonthDocument Render(MonthPrintPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var document = new FixedDocument();
        document.DocumentPaginator.PageSize = new Size(
            plan.EffectiveOptions.Page.Width,
            plan.EffectiveOptions.Page.Height);
        AddPage(document, CreateMonthPage(plan));
        foreach (var detailsPage in CreateDetailsPages(plan))
        {
            AddPage(document, detailsPage);
        }

        return new RenderedMonthDocument(document, plan);
    }

    private static void AddPage(FixedDocument document, FixedPage page) =>
        document.Pages.Add(new PageContent { Child = page });

    private static FixedPage CreateMonthPage(MonthPrintPlan plan)
    {
        var options = plan.EffectiveOptions;
        var page = NewPage(options.Page);
        var contentWidth = options.Page.Width - options.Margins.Left - options.Margins.Right;
        var gridHeight = options.Page.Height
            - options.Margins.Top
            - options.Margins.Bottom
            - PrintLayoutMetrics.HeaderHeight
            - PrintLayoutMetrics.WeekdayHeight;
        var cellWidth = contentWidth / 7;
        var cellHeight = gridHeight / plan.Model.Grid.WeekCount;

        AddPositioned(
            page,
            new TextBlock
            {
                Text = plan.Model.DisplayedMonth.ToString("MMMM yyyy", CultureInfo.CurrentCulture),
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 22,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.Black,
            },
            options.Margins.Left,
            options.Margins.Top,
            contentWidth,
            PrintLayoutMetrics.HeaderHeight);

        var weekdays = CultureInfo.CurrentCulture.DateTimeFormat.AbbreviatedDayNames;
        for (var column = 0; column < 7; column++)
        {
            AddPositioned(
                page,
                new TextBlock
                {
                    Text = weekdays[column],
                    FontFamily = new FontFamily("Segoe UI"),
                    FontSize = 11,
                    FontWeight = FontWeights.SemiBold,
                    TextAlignment = TextAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = Brushes.Black,
                },
                options.Margins.Left + column * cellWidth,
                options.Margins.Top + PrintLayoutMetrics.HeaderHeight,
                cellWidth,
                PrintLayoutMetrics.WeekdayHeight);
        }

        for (var index = 0; index < plan.Days.Count; index++)
        {
            var row = index / 7;
            var column = index % 7;
            AddPositioned(
                page,
                CreateDayCell(plan.Days[index], plan),
                options.Margins.Left + column * cellWidth,
                options.Margins.Top
                    + PrintLayoutMetrics.HeaderHeight
                    + PrintLayoutMetrics.WeekdayHeight
                    + row * cellHeight,
                cellWidth,
                cellHeight);
        }

        return page;
    }

    private static Border CreateDayCell(PrintDayLayout dayLayout, MonthPrintPlan plan)
    {
        var fontSize = MonthPrintLayoutEngine.PointsToDips(
            plan.EffectiveOptions.BodyFontSizePoints);
        var panel = new StackPanel();
        panel.Children.Add(Text(
            dayLayout.Day.Date.Day.ToString(CultureInfo.CurrentCulture),
            fontSize,
            dayLayout.Day.IsInDisplayedMonth ? Brushes.Black : SecondaryBrush,
            FontWeights.SemiBold));
        foreach (var occurrence in dayLayout.MainPageOccurrences)
        {
            panel.Children.Add(CreateOccurrenceBlock(occurrence, plan));
        }

        if (dayLayout.OverflowCount > 0)
        {
            panel.Children.Add(new TextBlock
            {
                Margin = new Thickness(0, 1, 0, 0),
                Text = $"+{dayLayout.OverflowCount} on details page",
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = Math.Max(8, fontSize * 0.82),
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.Black,
                TextWrapping = TextWrapping.Wrap,
            });
        }

        return new Border
        {
            BorderBrush = GridBrush,
            BorderThickness = new Thickness(PrintLayoutMetrics.GridBorderThickness),
            Background = dayLayout.Day.IsInDisplayedMonth ? Brushes.White : OutOfMonthBrush,
            Padding = new Thickness(PrintLayoutMetrics.CellPadding),
            ClipToBounds = true,
            Child = panel,
        };
    }

    private static StackPanel CreateOccurrenceBlock(
        PrintOccurrenceModel occurrence,
        MonthPrintPlan plan)
    {
        var options = plan.EffectiveOptions;
        var fontSize = MonthPrintLayoutEngine.PointsToDips(options.BodyFontSizePoints);
        var panel = new StackPanel { Margin = new Thickness(0, plan.EventSpacing, 0, 0) };
        if (occurrence.TimeText.Length > 0)
        {
            panel.Children.Add(Text(occurrence.TimeText, fontSize * 0.9, SecondaryBrush));
        }

        panel.Children.Add(Text(occurrence.Title, fontSize, Brushes.Black, FontWeights.SemiBold));
        var description = string.Join(
            Environment.NewLine,
            occurrence.DescriptionLines.Take(options.DescriptionLineLimit));
        if (description.Length > 0)
        {
            panel.Children.Add(Text(description, fontSize * 0.92, Brushes.Black));
        }

        if (options.ShowLocations && occurrence.Location.Length > 0)
        {
            panel.Children.Add(Text(
                occurrence.Location,
                fontSize * 0.88,
                SecondaryBrush,
                FontWeights.Normal,
                FontStyles.Italic));
        }

        return panel;
    }

    private IEnumerable<FixedPage> CreateDetailsPages(MonthPrintPlan plan)
    {
        if (!plan.HasOverflow)
        {
            yield break;
        }

        var options = plan.EffectiveOptions;
        var width = options.Page.Width - options.Margins.Left - options.Margins.Right;
        var availableHeight = options.Page.Height
            - options.Margins.Top
            - options.Margins.Bottom
            - PrintLayoutMetrics.HeaderHeight;
        var fontSize = Math.Max(
            MonthPrintLayoutEngine.PointsToDips(options.BodyFontSizePoints),
            MonthPrintLayoutEngine.PointsToDips(8));
        var chunks = plan.OverflowOccurrences.SelectMany(item =>
            SplitDetailsText(
                DetailsText(item),
                width,
                fontSize,
                availableHeight - PrintLayoutMetrics.DetailsSpacing)).ToArray();

        var page = NewDetailsPage(plan);
        var top = options.Margins.Top + PrintLayoutMetrics.HeaderHeight;
        foreach (var chunk in chunks)
        {
            var height = textMeasurer.MeasureHeight(chunk, width, fontSize)
                + PrintLayoutMetrics.DetailsSpacing;
            if (top + height > options.Page.Height - options.Margins.Bottom
                && top > options.Margins.Top + PrintLayoutMetrics.HeaderHeight)
            {
                yield return page;
                page = NewDetailsPage(plan);
                top = options.Margins.Top + PrintLayoutMetrics.HeaderHeight;
            }

            AddPositioned(
                page,
                Text(chunk, fontSize, Brushes.Black),
                options.Margins.Left,
                top,
                width,
                height);
            top += height;
        }

        yield return page;
    }

    private IEnumerable<string> SplitDetailsText(
        string text,
        double width,
        double fontSize,
        double maximumHeight)
    {
        var remaining = text;
        var continuation = false;
        while (remaining.Length > 0)
        {
            var prefix = continuation ? "(continued) " : string.Empty;
            if (textMeasurer.MeasureHeight(prefix + remaining, width, fontSize) <= maximumHeight)
            {
                yield return prefix + remaining;
                yield break;
            }

            var low = 1;
            var high = remaining.Length;
            while (low < high)
            {
                var middle = (low + high + 1) / 2;
                if (textMeasurer.MeasureHeight(prefix + remaining[..middle], width, fontSize) <= maximumHeight)
                {
                    low = middle;
                }
                else
                {
                    high = middle - 1;
                }
            }

            var split = Math.Max(1, low);
            yield return prefix + remaining[..split];
            remaining = remaining[split..];
            continuation = true;
        }
    }

    private static string DetailsText(PrintOccurrenceModel occurrence)
    {
        var heading = occurrence.IsAllDay
            ? $"{occurrence.Date:MMMM d, yyyy}\n{occurrence.Title}"
            : $"{occurrence.Date:MMMM d, yyyy}\n{occurrence.Title} — {occurrence.TimeText}";
        var parts = new List<string> { heading };
        parts.AddRange(occurrence.FullDescriptionLines);
        if (occurrence.Location.Length > 0)
        {
            parts.Add($"Location: {occurrence.Location}");
        }

        return string.Join(Environment.NewLine, parts);
    }

    private static FixedPage NewDetailsPage(MonthPrintPlan plan)
    {
        var page = NewPage(plan.EffectiveOptions.Page);
        AddPositioned(
            page,
            new TextBlock
            {
                Text = $"{plan.Model.DisplayedMonth:MMMM yyyy} — Additional Event Details",
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 18,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.Black,
            },
            plan.EffectiveOptions.Margins.Left,
            plan.EffectiveOptions.Margins.Top,
            plan.EffectiveOptions.Page.Width
                - plan.EffectiveOptions.Margins.Left
                - plan.EffectiveOptions.Margins.Right,
            PrintLayoutMetrics.HeaderHeight);
        return page;
    }

    private static FixedPage NewPage(PrintPageGeometry geometry) => new()
    {
        Width = geometry.Width,
        Height = geometry.Height,
        Background = Brushes.White,
    };

    private static TextBlock Text(
        string value,
        double fontSize,
        Brush brush,
        FontWeight? weight = null,
        FontStyle? style = null) => new()
        {
            Text = value,
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = fontSize,
            FontWeight = weight ?? FontWeights.Normal,
            FontStyle = style ?? FontStyles.Normal,
            Foreground = brush,
            TextWrapping = TextWrapping.Wrap,
            LineStackingStrategy = LineStackingStrategy.BlockLineHeight,
            LineHeight = fontSize * PrintLayoutMetrics.TextLineHeightMultiplier,
        };

    private static void AddPositioned(
        FixedPage page,
        FrameworkElement element,
        double left,
        double top,
        double width,
        double height)
    {
        element.Width = width;
        element.Height = height;
        FixedPage.SetLeft(element, left);
        FixedPage.SetTop(element, top);
        page.Children.Add(element);
    }
}
