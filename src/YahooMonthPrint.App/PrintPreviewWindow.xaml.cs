using System.Globalization;
using System.Printing;
using System.Windows;
using System.Windows.Controls;
using YahooMonthPrint.App.Services;
using YahooMonthPrint.Core;
using YahooMonthPrint.Printing;

namespace YahooMonthPrint.App;

public partial class PrintPreviewWindow : Window
{
    private readonly DateOnly displayedMonth;
    private readonly IReadOnlyList<CalendarOccurrence> visibleOccurrences;
    private MonthPrintOptions options;
    private RenderedMonthDocument rendered = null!;
    private bool initialized;

    public PrintPreviewWindow(
        DateOnly displayedMonth,
        IReadOnlyList<CalendarOccurrence> visibleOccurrences,
        MonthPrintOptions options)
    {
        this.displayedMonth = displayedMonth;
        this.visibleOccurrences = visibleOccurrences
            ?? throw new ArgumentNullException(nameof(visibleOccurrences));
        this.options = options ?? throw new ArgumentNullException(nameof(options));

        InitializeComponent();
        LoadPrinters();
        SelectTag(PaperCombo, options.Page.PaperSize.ToString());
        SelectTag(OrientationCombo, options.Page.Orientation.ToString());
        SelectClosestMargin(options.Margins.Left / 96);
        SelectTag(DetailCombo, options.DetailLevel.ToString());
        SelectTag(
            DescriptionLinesCombo,
            options.DescriptionLineLimit.ToString(CultureInfo.InvariantCulture));
        SelectTag(
            FontSizeCombo,
            options.BodyFontSizePoints.ToString("0", CultureInfo.InvariantCulture));
        ShowLocationsCheckBox.IsChecked = options.ShowLocations;
        AutomaticRadio.IsChecked = options.OverflowPolicy == PrintOverflowPolicy.ReduceDetailAutomatically;
        SmallerTextRadio.IsChecked = options.OverflowPolicy == PrintOverflowPolicy.UseSmallerText;
        DetailsPagesRadio.IsChecked = options.OverflowPolicy == PrintOverflowPolicy.PrintDetailsPages;
        initialized = true;
        RebuildPreview();
    }

    public static MonthPrintOptions OptionsFromSettings(
        ApplicationSettings settings,
        DetailLevel detailLevel,
        int descriptionLines,
        bool showLocations)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var paper = Enum.TryParse<PrintPaperSize>(settings.PaperSize, ignoreCase: true, out var savedPaper)
            ? savedPaper
            : PrintPaperSize.Letter;
        var orientation = Enum.TryParse<PrintPageOrientation>(
            settings.Orientation,
            ignoreCase: true,
            out var savedOrientation)
            ? savedOrientation
            : PrintPageOrientation.Landscape;
        var overflow = settings.OverflowPolicy switch
        {
            "Use smaller text" => PrintOverflowPolicy.UseSmallerText,
            "Print overflow details on page 2" => PrintOverflowPolicy.PrintDetailsPages,
            _ => PrintOverflowPolicy.ReduceDetailAutomatically,
        };
        return new MonthPrintOptions
        {
            Page = PrintPageGeometry.Create(paper, orientation),
            DetailLevel = detailLevel,
            DescriptionLineLimit = descriptionLines,
            ShowLocations = showLocations,
            OverflowPolicy = overflow,
        };
    }

    private void LoadPrinters()
    {
        try
        {
            using var server = new LocalPrintServer();
            var queues = server.GetPrintQueues(
                [EnumeratedPrintQueueTypes.Local, EnumeratedPrintQueueTypes.Connections]);
            var names = queues.Select(queue => queue.FullName)
                .Order(StringComparer.CurrentCultureIgnoreCase)
                .ToArray();
            PrinterCombo.ItemsSource = names;
            var defaultName = server.DefaultPrintQueue?.FullName;
            PrinterCombo.SelectedItem = names.FirstOrDefault(name => string.Equals(
                name,
                defaultName,
                StringComparison.OrdinalIgnoreCase));
            PrinterCombo.SelectedIndex = PrinterCombo.SelectedIndex < 0 && names.Length > 0 ? 0 : PrinterCombo.SelectedIndex;
        }
        catch (PrintSystemException)
        {
            PrinterCombo.ItemsSource = new[] { "Choose in Windows print dialog" };
            PrinterCombo.SelectedIndex = 0;
            PrinterCombo.IsEnabled = false;
        }
    }

    private void OnOptionsChanged(object sender, RoutedEventArgs e)
    {
        if (initialized)
        {
            RebuildPreview();
        }
    }

    private void RebuildPreview()
    {
        var paper = Enum.Parse<PrintPaperSize>(SelectedTag(PaperCombo));
        var orientation = Enum.Parse<PrintPageOrientation>(SelectedTag(OrientationCombo));
        var margin = double.Parse(SelectedTag(MarginsCombo), CultureInfo.InvariantCulture) * 96;
        var detail = Enum.Parse<DetailLevel>(SelectedTag(DetailCombo));
        var lines = int.Parse(SelectedTag(DescriptionLinesCombo), CultureInfo.InvariantCulture);
        var fontSize = double.Parse(SelectedTag(FontSizeCombo), CultureInfo.InvariantCulture);
        var overflowPolicy = AutomaticRadio.IsChecked == true
            ? PrintOverflowPolicy.ReduceDetailAutomatically
            : SmallerTextRadio.IsChecked == true
                ? PrintOverflowPolicy.UseSmallerText
                : PrintOverflowPolicy.PrintDetailsPages;
        options = options with
        {
            Page = PrintPageGeometry.Create(paper, orientation),
            Margins = new PrintMargins(margin, margin, margin, margin),
            DetailLevel = detail,
            DescriptionLineLimit = lines,
            BodyFontSizePoints = fontSize,
            ShowLocations = ShowLocationsCheckBox.IsChecked == true,
            OverflowPolicy = overflowPolicy,
        };
        RenderCurrentOptions();
    }

    private void RenderCurrentOptions()
    {
        DescriptionLinesPanel.Visibility = options.DetailLevel == DetailLevel.Detailed
            ? Visibility.Visible
            : Visibility.Collapsed;
        var model = MonthLayoutModelBuilder.Build(displayedMonth, visibleOccurrences, options);
        var plan = new MonthPrintLayoutEngine().CreatePlan(model);
        rendered = new FixedDocumentRenderer().Render(plan);
        PreviewViewer.Document = rendered.Document;

        if (plan.Diagnostics.Count == 0)
        {
            OverflowWarning.Visibility = Visibility.Collapsed;
            return;
        }

        OverflowWarning.Visibility = Visibility.Visible;
        var heading = plan.HasOverflow
            ? "Some event details do not fit on one page. "
            : "The preview was adjusted to fit. ";
        OverflowWarningText.Text = heading + string.Join(
            " ",
            plan.Diagnostics.Select(item => item.Message));
    }

    private void OnPrint(object sender, RoutedEventArgs e)
    {
        try
        {
            var dialog = new PrintDialog();
            using var server = new LocalPrintServer();
            if (PrinterCombo.IsEnabled && PrinterCombo.SelectedItem is string printerName)
            {
                dialog.PrintQueue = server.GetPrintQueue(printerName);
            }

            dialog.PrintTicket.PageOrientation = options.Page.Orientation == PrintPageOrientation.Landscape
                ? PageOrientation.Landscape
                : PageOrientation.Portrait;
            dialog.PrintTicket.PageMediaSize = new PageMediaSize(
                options.Page.PaperSize == PrintPaperSize.A4
                    ? PageMediaSizeName.ISOA4
                    : PageMediaSizeName.NorthAmericaLetter);
            if (dialog.ShowDialog() != true)
            {
                return;
            }

            if (AdjustForPrinterTicket(dialog) || AdjustForPrinterImageableArea(dialog))
            {
                MessageBox.Show(
                    this,
                    "The printer changed the requested paper, orientation, or margins. The preview was updated; review it, then choose Print again.",
                    "Print Preview Updated",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            dialog.PrintDocument(
                rendered.Document.DocumentPaginator,
                $"Yahoo Month Print — {displayedMonth:MMMM yyyy}");
        }
        catch (PrintSystemException exception)
        {
            _ = exception;
            ShowPrintError();
        }
        catch (InvalidOperationException exception)
        {
            _ = exception;
            ShowPrintError();
        }
    }

    private bool AdjustForPrinterTicket(PrintDialog dialog)
    {
        var paper = dialog.PrintTicket.PageMediaSize?.PageMediaSizeName switch
        {
            PageMediaSizeName.ISOA4 => PrintPaperSize.A4,
            PageMediaSizeName.NorthAmericaLetter => PrintPaperSize.Letter,
            _ => options.Page.PaperSize,
        };
        var orientation = dialog.PrintTicket.PageOrientation switch
        {
            PageOrientation.Portrait => PrintPageOrientation.Portrait,
            PageOrientation.Landscape => PrintPageOrientation.Landscape,
            _ => options.Page.Orientation,
        };
        if (paper == options.Page.PaperSize && orientation == options.Page.Orientation)
        {
            return false;
        }

        options = options with { Page = PrintPageGeometry.Create(paper, orientation) };
        RenderCurrentOptions();
        return true;
    }

    private bool AdjustForPrinterImageableArea(PrintDialog dialog)
    {
        var area = dialog.PrintQueue.GetPrintCapabilities(dialog.PrintTicket).PageImageableArea;
        if (area is null)
        {
            return false;
        }

        var required = new PrintMargins(
            area.OriginWidth,
            area.OriginHeight,
            Math.Max(0, options.Page.Width - area.OriginWidth - area.ExtentWidth),
            Math.Max(0, options.Page.Height - area.OriginHeight - area.ExtentHeight));
        var adjusted = new PrintMargins(
            Math.Max(options.Margins.Left, required.Left),
            Math.Max(options.Margins.Top, required.Top),
            Math.Max(options.Margins.Right, required.Right),
            Math.Max(options.Margins.Bottom, required.Bottom));
        if (adjusted == options.Margins)
        {
            return false;
        }

        options = options with { Margins = adjusted };
        RenderCurrentOptions();
        return true;
    }

    private void ShowPrintError() => MessageBox.Show(
        this,
        "Windows could not print the calendar. Check that the printer is available, then try again.",
        "Printing Failed",
        MessageBoxButton.OK,
        MessageBoxImage.Error);

    private void SelectClosestMargin(double inches)
    {
        var selected = MarginsCombo.Items.OfType<ComboBoxItem>()
            .OrderBy(item => Math.Abs(
                double.Parse((string)item.Tag, CultureInfo.InvariantCulture) - inches))
            .First();
        MarginsCombo.SelectedItem = selected;
    }

    private static void SelectTag(ComboBox comboBox, string value)
    {
        comboBox.SelectedItem = comboBox.Items.OfType<ComboBoxItem>().FirstOrDefault(item =>
            string.Equals(item.Tag?.ToString(), value, StringComparison.OrdinalIgnoreCase));
        comboBox.SelectedIndex = comboBox.SelectedIndex < 0 ? 0 : comboBox.SelectedIndex;
    }

    private static string SelectedTag(ComboBox comboBox) =>
        ((ComboBoxItem)comboBox.SelectedItem).Tag?.ToString()
        ?? throw new InvalidOperationException("A print option is not selected.");
}
