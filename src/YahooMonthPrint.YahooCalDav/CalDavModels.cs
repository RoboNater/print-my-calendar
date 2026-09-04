using YahooMonthPrint.Core;

namespace YahooMonthPrint.YahooCalDav;

public sealed record CalDavCalendar(
    string Id,
    string DisplayName,
    Uri Uri,
    string? Color = null)
{
    public CalendarSource ToCalendarSource(bool isEnabled = true) =>
        new(Id, DisplayName, Uri, Color, isEnabled);
}

public sealed record CalDavResourceIssue(
    string CalendarId,
    string ResourceId,
    string ExceptionType);

public sealed record CalendarQueryResult(
    IReadOnlyList<CalendarOccurrence> Occurrences,
    IReadOnlyList<CalDavResourceIssue> ResourceIssues);

public enum CalDavFailureKind
{
    Authentication,
    Connectivity,
    Server,
    Protocol,
    CalendarCollectionRejected,
}

public sealed class CalDavException : Exception
{
    public CalDavException(
        CalDavFailureKind kind,
        string friendlyMessage,
        string technicalDetail,
        Exception? innerException = null)
        : base(friendlyMessage, innerException)
    {
        Kind = kind;
        TechnicalDetail = technicalDetail;
    }

    public CalDavFailureKind Kind { get; }

    public string TechnicalDetail { get; }
}
