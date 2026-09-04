namespace YahooMonthPrint.App.Services;

public sealed class CalendarLoadException : Exception
{
    public CalendarLoadException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
