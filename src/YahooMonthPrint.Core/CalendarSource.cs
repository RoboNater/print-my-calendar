namespace YahooMonthPrint.Core;

public sealed record CalendarSource(
    string Id,
    string DisplayName,
    Uri? CalDavUri = null,
    string? Color = null,
    bool IsEnabled = true);
