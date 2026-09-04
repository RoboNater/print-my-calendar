using System.Collections;
using YahooMonthPrint.Core;

namespace YahooMonthPrint.App.Services;

public sealed class CalendarLoadResult : IReadOnlyList<CalendarOccurrence>
{
    public CalendarLoadResult(
        IReadOnlyList<CalendarOccurrence> occurrences,
        DateTimeOffset refreshedAt,
        int unreadableResourceCount = 0,
        bool isFromCache = false)
    {
        Occurrences = occurrences ?? throw new ArgumentNullException(nameof(occurrences));
        RefreshedAt = refreshedAt;
        UnreadableResourceCount = unreadableResourceCount;
        IsFromCache = isFromCache;
    }

    public IReadOnlyList<CalendarOccurrence> Occurrences { get; }

    public DateTimeOffset RefreshedAt { get; }

    public int UnreadableResourceCount { get; }

    public bool IsFromCache { get; }

    public int Count => Occurrences.Count;

    public CalendarOccurrence this[int index] => Occurrences[index];

    public IEnumerator<CalendarOccurrence> GetEnumerator() => Occurrences.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
