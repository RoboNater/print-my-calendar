using YahooMonthPrint.Core;

namespace YahooMonthPrint.App.Services;

public sealed record ApplicationSettings
{
    public const int CurrentVersion = 1;

    public int Version { get; init; } = CurrentVersion;

    public string? YahooAccount { get; init; }

    public IReadOnlyList<SavedCalendar> Calendars { get; init; } = [];

    public DetailLevel DetailLevel { get; init; } = DetailLevel.Detailed;

    public int MaximumDescriptionLines { get; init; } = 3;

    public bool ShowLocations { get; init; } = true;

    public string PaperSize { get; init; } = "Printer default";

    public string Orientation { get; init; } = "Landscape";

    public string OverflowPolicy { get; init; } = "Reduce detail automatically";
}

public sealed record SavedCalendar(
    string Id,
    string DisplayName,
    string Uri,
    string? Color,
    bool IsSelected)
{
    public CalendarSource ToCalendarSource() =>
        new(Id, DisplayName, new Uri(Uri, UriKind.Absolute), Color, IsSelected);
}
