using System.Globalization;
using System.Security;
using YahooMonthPrint.Core;

namespace YahooMonthPrint.YahooCalDav;

public static class CalendarQueryBuilder
{
    public static string Build(MonthGridRange range, TimeZoneInfo viewerTimeZone)
    {
        ArgumentNullException.ThrowIfNull(range);
        ArgumentNullException.ThrowIfNull(viewerTimeZone);

        var localStart = range.Start.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        var localEnd = range.EndExclusive.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        var utcStart = TimeZoneInfo.ConvertTimeToUtc(localStart, viewerTimeZone);
        var utcEnd = TimeZoneInfo.ConvertTimeToUtc(localEnd, viewerTimeZone);
        var start = utcStart.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);
        var end = utcEnd.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);

        return $"""
            <?xml version="1.0" encoding="utf-8"?>
            <c:calendar-query xmlns:d="DAV:" xmlns:c="urn:ietf:params:xml:ns:caldav">
              <d:prop>
                <d:getetag />
                <c:calendar-data />
              </d:prop>
              <c:filter>
                <c:comp-filter name="VCALENDAR">
                  <c:comp-filter name="VEVENT">
                    <c:time-range start="{SecurityElement.Escape(start)}" end="{SecurityElement.Escape(end)}" />
                  </c:comp-filter>
                </c:comp-filter>
              </c:filter>
            </c:calendar-query>
            """;
    }
}
