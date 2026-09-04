namespace YahooMonthPrint.Core;

public enum HiddenReason
{
    CalendarDisabled,
    TitleDisabled,
    QuickFilterNotMatched,
    QuickFilterMatched,
    ManuallyHidden,
}

public sealed record VisibilityResult(bool IsVisible, HiddenReason? HiddenReason = null);
