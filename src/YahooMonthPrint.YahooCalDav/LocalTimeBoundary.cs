namespace YahooMonthPrint.YahooCalDav;

internal static class LocalTimeBoundary
{
    public static DateTime Resolve(DateOnly date, TimeZoneInfo timeZone)
    {
        var local = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        while (timeZone.IsInvalidTime(local))
        {
            local = local.AddMinutes(1);
        }

        return local;
    }

    public static DateTime ToUtc(DateOnly date, TimeZoneInfo timeZone)
    {
        var local = Resolve(date, timeZone);
        return new DateTimeOffset(local, timeZone.GetUtcOffset(local)).UtcDateTime;
    }
}
