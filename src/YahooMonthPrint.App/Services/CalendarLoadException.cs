namespace YahooMonthPrint.App.Services;

public sealed class CalendarLoadException : Exception
{
    public CalendarLoadException(
        CalendarLoadFailureKind kind,
        string message,
        string? technicalDetail = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        Kind = kind;
        TechnicalDetail = technicalDetail;
    }

    public CalendarLoadException(string message, Exception? innerException = null)
        : this(CalendarLoadFailureKind.Unknown, message, null, innerException)
    {
    }

    public CalendarLoadFailureKind Kind { get; }

    public string? TechnicalDetail { get; }
}

public enum CalendarLoadFailureKind
{
    Unknown,
    Authentication,
    Connectivity,
    Server,
    Protocol,
}
