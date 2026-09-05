using System.ComponentModel;
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
    private const string PrinterMarginToolTip =
        "The selected printer's imageable area requires wider effective margins.";

    private readonly DateOnly displayedMonth;
    private readonly IReadOnlyList<CalendarOccurrence> visibleOccurrences;
    private readonly IAppLogger logger;
    private MonthPrintOptions options;
    private RenderedMonthDocument rendered = null!;
    private readonly Dictionary<string, PrintMargins?> printerMinimumMargins = new(StringComparer.Ordinal);
    private bool initialized;

    public PrintPreviewWindow(
        DateOnly displayedMonth,
        IReadOnlyList<CalendarOccurrence> visibleOccurrences,
        MonthPrintOptions options,
        IAppLogger? logger = null)
    {
        this.displayedMonth = displayedMonth;
        this.visibleOccurrences = visibleOccurrences
            ?? throw new ArgumentNullException(nameof(visibleOccurrences));
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        this.logger = logger ?? new NullAppLogger();

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
        return new MonthPrintOptions
        {
            Page = PrintPageGeometry.Create(paper, orientation),
            DetailLevel = detailLevel,
            DescriptionLineLimit = descriptionLines,
            ShowLocations = showLocations,
            OverflowPolicy = settings.OverflowPolicy,
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
            if (PrinterCombo.SelectedIndex < 0 && names.Length > 0)
            {
                PrinterCombo.SelectedIndex = 0;
            }
        }
        catch (Exception exception) when (IsPrinterDiscoveryFailure(exception))
        {
            logger.Log("printing", "printer-discovery-failed", exception: exception);
            PrinterCombo.ItemsSource = new[] { "Choose in Windows print dialog" };
            PrinterCombo.SelectedIndex = 0;
            PrinterCombo.IsEnabled = false;
        }
    }

    private void OnPrinterChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!initialized)
        {
            return;
        }

        printerMinimumMargins.Clear();
        MarginsCombo.ToolTip = null;
        RebuildPreview();
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
        var page = PrintPageGeometry.Create(paper, orientation);
        var requestedMargins = new PrintMargins(margin, margin, margin, margin);
        var effectiveMargins = PrinterPageNegotiation.ApplyMinimum(
            requestedMargins,
            TryGetPrinterMinimumMargins(page));
        MarginsCombo.ToolTip =
            PrinterPageNegotiation.DiffersBeyondTolerance(effectiveMargins, requestedMargins)
                ? PrinterMarginToolTip
                : null;
        options = options with
        {
            Page = page,
            Margins = effectiveMargins,
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

            dialog.PrintTicket = CreatePageTicket(dialog.PrintQueue, options.Page);
            var previewedPageCount = rendered.PageCount;
            if (dialog.ShowDialog() != true)
            {
                return;
            }

            // Whatever the user settled on in the Windows dialog and the driver's own preferences is
            // their most recent choice, so it wins over the page setup the preview was built with.
            AdoptDialogPageSetup(dialog);
            AdoptPrinterImageableArea(dialog);

            // The ticket a driver hands back can be incomplete, and an incomplete ticket is one a
            // driver may discard in favour of its saved page setup. Re-assert the page setup just
            // agreed as a validated ticket so the job really carries it.
            var ticket = CreatePageTicket(dialog.PrintQueue, options.Page);
            if (!Honours(ticket, options.Page))
            {
                logger.Log("printing", "printer-rejected-page-setup", dialog.PrintQueue?.FullName);
                ShowUnsupportedPageSetup();
                return;
            }

            dialog.PrintTicket = ticket;
            if (!ConfirmAdditionalPages(previewedPageCount))
            {
                return;
            }

            dialog.PrintDocument(
                rendered.Document.DocumentPaginator,
                $"Yahoo Month Print \u2014 {displayedMonth:MMMM yyyy}");
        }
        catch (PrintSystemException exception)
        {
            logger.Log("printing", "print-failed", exception: exception);
            ShowPrintError();
        }
        catch (InvalidOperationException exception)
        {
            logger.Log("printing", "print-failed", exception: exception);
            ShowPrintError();
        }
        catch (Win32Exception exception)
        {
            logger.Log("printing", "print-failed", exception: exception);
            ShowPrintError();
        }
    }

    /// <summary>
    /// Brings the destination, paper, and orientation the user left the Windows print dialog with
    /// back into the preview, so the preview keeps showing what is about to come out of the printer.
    /// </summary>
    private void AdoptDialogPageSetup(PrintDialog dialog)
    {
        var paper = dialog.PrintTicket.PageMediaSize?.PageMediaSizeName switch
        {
            PageMediaSizeName.ISOA4 => PrintPaperSize.A4,
            PageMediaSizeName.NorthAmericaLetter => PrintPaperSize.Letter,
            _ => options.Page.PaperSize,
        };
        var orientation = dialog.PrintTicket.PageOrientation switch
        {
            PageOrientation.Portrait or PageOrientation.ReversePortrait => PrintPageOrientation.Portrait,
            PageOrientation.Landscape or PageOrientation.ReverseLandscape => PrintPageOrientation.Landscape,
            _ => options.Page.Orientation,
        };
        var queueName = dialog.PrintQueue?.FullName;
        var printerChanged = queueName is not null
            && PrinterCombo.IsEnabled
            && !string.Equals(
                PrinterCombo.SelectedItem as string,
                queueName,
                StringComparison.OrdinalIgnoreCase)
            && PrinterCombo.Items.OfType<string>().Contains(queueName, StringComparer.OrdinalIgnoreCase);
        if (paper == options.Page.PaperSize
            && orientation == options.Page.Orientation
            && !printerChanged)
        {
            return;
        }

        logger.Log("printing", "adopted-dialog-page-setup", queueName);
        initialized = false;
        try
        {
            SelectTag(PaperCombo, paper.ToString());
            SelectTag(OrientationCombo, orientation.ToString());
            if (printerChanged)
            {
                PrinterCombo.SelectedItem = queueName;
                printerMinimumMargins.Clear();
            }
        }
        finally
        {
            initialized = true;
        }

        // Rebuilding rather than re-rendering re-reads the printer's unprintable border for the page
        // setup just adopted; that border is orientation specific.
        RebuildPreview();
    }

    /// <summary>
    /// Final safety net for the margins, using the queue and ticket the dialog actually settled on.
    /// It normally finds nothing, because the preview already applied this printer's border.
    /// </summary>
    private void AdoptPrinterImageableArea(PrintDialog dialog)
    {
        var area = dialog.PrintQueue.GetPrintCapabilities(dialog.PrintTicket).PageImageableArea;
        if (area is null)
        {
            return;
        }

        var required = PrinterPageNegotiation.RequiredMargins(
            options.Page,
            area.OriginWidth,
            area.OriginHeight,
            area.ExtentWidth,
            area.ExtentHeight);
        var adjusted = PrinterPageNegotiation.ApplyMinimum(options.Margins, required);
        if (!PrinterPageNegotiation.DiffersBeyondTolerance(adjusted, options.Margins))
        {
            return;
        }

        printerMinimumMargins[MinimumMarginKey(dialog.PrintQueue.FullName, options.Page)] = required;
        MarginsCombo.ToolTip = PrinterMarginToolTip;
        options = options with { Margins = adjusted };
        RenderCurrentOptions();
    }

    /// <summary>
    /// The adopted page setup can re-flow the calendar onto more sheets than the preview showed.
    /// Extra paper is the one consequence worth stopping for; anything else prints straight away.
    /// </summary>
    private bool ConfirmAdditionalPages(int previewedPageCount)
    {
        if (rendered.PageCount <= previewedPageCount)
        {
            return true;
        }

        return MessageBox.Show(
            this,
            $"The printer's page setup re-flows the calendar onto {rendered.PageCount} pages instead of {previewedPageCount}. Print all {rendered.PageCount} pages?",
            "More Pages Than the Preview Showed",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question) == MessageBoxResult.Yes;
    }

    /// <summary>Confirms a validated ticket really carries the paper and orientation asked for.</summary>
    private static bool Honours(PrintTicket ticket, PrintPageGeometry page)
    {
        var expectedOrientation = page.Orientation == PrintPageOrientation.Landscape
            ? PageOrientation.Landscape
            : PageOrientation.Portrait;
        var expectedMedia = page.PaperSize == PrintPaperSize.A4
            ? PageMediaSizeName.ISOA4
            : PageMediaSizeName.NorthAmericaLetter;
        return ticket.PageOrientation == expectedOrientation
            && ticket.PageMediaSize?.PageMediaSizeName == expectedMedia;
    }

    private void ShowUnsupportedPageSetup() => MessageBox.Show(
        this,
        "The selected printer cannot print this paper size in this orientation. Choose a different paper size or orientation, then try again.",
        "Page Setup Not Supported",
        MessageBoxButton.OK,
        MessageBoxImage.Warning);

    private void ShowPrintError() => MessageBox.Show(
        this,
        "Windows could not print the calendar. Check that the printer is available, then try again.",
        "Printing Failed",
        MessageBoxButton.OK,
        MessageBoxImage.Error);

    /// <summary>
    /// Asks the selected printer what it can actually reach for this exact paper and orientation.
    /// The unprintable border is orientation specific, so the answer is cached per page geometry and
    /// resolved while the preview is built rather than after the print dialog has already been shown.
    /// </summary>
    private PrintMargins? TryGetPrinterMinimumMargins(PrintPageGeometry page)
    {
        if (!PrinterCombo.IsEnabled || PrinterCombo.SelectedItem is not string printerName)
        {
            return null;
        }

        var key = MinimumMarginKey(printerName, page);
        if (printerMinimumMargins.TryGetValue(key, out var cached))
        {
            return cached;
        }

        PrintMargins? required = null;
        try
        {
            using var server = new LocalPrintServer();
            using var queue = server.GetPrintQueue(printerName);
            var area = queue.GetPrintCapabilities(CreatePageTicket(queue, page)).PageImageableArea;
            if (area is not null)
            {
                required = PrinterPageNegotiation.RequiredMargins(
                    page,
                    area.OriginWidth,
                    area.OriginHeight,
                    area.ExtentWidth,
                    area.ExtentHeight);
            }
        }
        catch (Exception exception) when (IsPrinterDiscoveryFailure(exception))
        {
            logger.Log("printing", "printer-capabilities-unavailable", exception: exception);
        }

        printerMinimumMargins[key] = required;
        return required;
    }

    private static string MinimumMarginKey(string printerName, PrintPageGeometry page) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{printerName}|{page.PaperSize}|{page.Orientation}");

    /// <summary>
    /// Builds a device-valid ticket for the requested page geometry. Merging against the queue's own
    /// ticket matters: a ticket carrying nothing but an orientation and a dimensionless media name is
    /// incomplete, and a driver is free to discard it and substitute its own saved page setup, which
    /// is how a requested landscape job used to come back as portrait.
    /// </summary>
    private static PrintTicket CreatePageTicket(PrintQueue? queue, PrintPageGeometry page)
    {
        var requested = new PrintTicket
        {
            PageOrientation = page.Orientation == PrintPageOrientation.Landscape
                ? PageOrientation.Landscape
                : PageOrientation.Portrait,
            PageMediaSize = new PageMediaSize(
                page.PaperSize == PrintPaperSize.A4
                    ? PageMediaSizeName.ISOA4
                    : PageMediaSizeName.NorthAmericaLetter),
        };
        if (queue is null)
        {
            return requested;
        }

        return queue.MergeAndValidatePrintTicket(queue.UserPrintTicket, requested).ValidatedPrintTicket;
    }

    private static bool IsPrinterDiscoveryFailure(Exception exception) => exception is
        PrintSystemException
        or Win32Exception
        or InvalidOperationException
        or UnauthorizedAccessException;

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
