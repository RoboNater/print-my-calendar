using YahooMonthPrint.Core;

namespace YahooMonthPrint.App.ViewModels;

public sealed record CalendarDayViewModel(
    DateOnly Date,
    bool IsInDisplayedMonth,
    bool IsToday,
    IReadOnlyList<OccurrenceViewModel> Occurrences)
{
    public string DayNumber => Date.Day.ToString(System.Globalization.CultureInfo.CurrentCulture);

    public string AccessibleName => Date.ToString("D", System.Globalization.CultureInfo.CurrentCulture);
}

public sealed record OccurrenceViewModel(
    CalendarOccurrence Occurrence,
    string CalendarName,
    string CalendarColor,
    string TimeText,
    string Title,
    string DescriptionText,
    string Location,
    RelayCommand HideCommand)
{
    public string AccessibleName => string.IsNullOrEmpty(TimeText)
        ? $"{Title}, all day, {CalendarName} calendar"
        : $"{Title}, {TimeText}, {CalendarName} calendar";
}

public sealed record HiddenOccurrenceViewModel(
    CalendarOccurrence Occurrence,
    string Label,
    RelayCommand RestoreCommand);

public sealed record DetailLevelOption(DetailLevel Value, string Label);

public sealed record DescriptionLineOption(int Value, string Label);
