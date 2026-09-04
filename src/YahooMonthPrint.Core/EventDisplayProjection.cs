using System.Globalization;

namespace YahooMonthPrint.Core;

public sealed record EventDisplayContent(
    string TimeText,
    string Title,
    IReadOnlyList<string> DescriptionLines,
    string Location);

public static class EventDisplayProjection
{
    public static EventDisplayContent Create(CalendarOccurrence occurrence, MonthViewState state)
    {
        ArgumentNullException.ThrowIfNull(occurrence);
        ArgumentNullException.ThrowIfNull(state);

        var timeText = occurrence.IsAllDay
            ? string.Empty
            : occurrence.Start.ToString("h:mm tt", CultureInfo.CurrentCulture);
        var descriptionLines = state.DetailLevel switch
        {
            DetailLevel.TitlesOnly => [],
            DetailLevel.Compact => Lines(occurrence.Description).Take(1).ToArray(),
            DetailLevel.Detailed => Lines(occurrence.Description)
                .Take(Math.Max(0, state.MaximumDescriptionLines))
                .ToArray(),
            _ => throw new ArgumentOutOfRangeException(nameof(state), "Unknown detail level."),
        };
        var location = state.DetailLevel == DetailLevel.Detailed && state.ShowLocations
            ? occurrence.Location
            : string.Empty;

        return new EventDisplayContent(timeText, occurrence.Title, descriptionLines, location);
    }

    private static string[] Lines(string value) => value.Split(
        ["\r\n", "\r", "\n"],
        StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
