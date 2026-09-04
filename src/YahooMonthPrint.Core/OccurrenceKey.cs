using System.Globalization;

namespace YahooMonthPrint.Core;

public readonly record struct OccurrenceKey(
    string CalendarId,
    string Uid,
    DateTimeOffset InstanceStart)
{
    public override string ToString() => string.Create(
        CultureInfo.InvariantCulture,
        $"{CalendarId}|{Uid}|{InstanceStart:O}");
}
