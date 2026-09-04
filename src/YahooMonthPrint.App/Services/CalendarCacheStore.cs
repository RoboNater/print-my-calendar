using System.IO;
using System.Text.Json;
using YahooMonthPrint.Core;

namespace YahooMonthPrint.App.Services;

public interface ICalendarCacheStore
{
    Task<CalendarLoadResult?> TryReadAsync(
        MonthGridRange range,
        IReadOnlyCollection<string> calendarIds,
        CancellationToken cancellationToken = default);

    Task WriteAsync(
        MonthGridRange range,
        IReadOnlyCollection<string> calendarIds,
        CalendarLoadResult result,
        CancellationToken cancellationToken = default);

    Task ClearAsync(CancellationToken cancellationToken = default);
}

public sealed class CalendarCacheStore : ICalendarCacheStore
{
    private const int CurrentVersion = 1;
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    private readonly string path;

    public CalendarCacheStore(string? applicationDataPath = null)
    {
        var directory = applicationDataPath ?? AppStoragePaths.Root;
        path = Path.Combine(directory, "calendar-cache.json");
    }

    public async Task<CalendarLoadResult?> TryReadAsync(
        MonthGridRange range,
        IReadOnlyCollection<string> calendarIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(range);
        ArgumentNullException.ThrowIfNull(calendarIds);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            await using var stream = File.OpenRead(path);
            var document = await JsonSerializer.DeserializeAsync<CacheDocument>(
                stream,
                SerializerOptions,
                cancellationToken);
            if (document is not { Version: CurrentVersion }
                || document.RangeStart != range.Start
                || document.RangeEndExclusive != range.EndExclusive
                || !SameCalendars(document.CalendarIds, calendarIds))
            {
                return null;
            }

            return new CalendarLoadResult(
                document.Occurrences.Select(item => item.ToOccurrence()).ToArray(),
                document.RefreshedAt,
                document.UnreadableResourceCount,
                isFromCache: true);
        }
        catch (Exception exception) when (IsUnreadable(exception))
        {
            QuarantineUnreadableCache();
            return null;
        }
    }

    public Task WriteAsync(
        MonthGridRange range,
        IReadOnlyCollection<string> calendarIds,
        CalendarLoadResult result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(range);
        ArgumentNullException.ThrowIfNull(calendarIds);
        ArgumentNullException.ThrowIfNull(result);
        var document = new CacheDocument(
            CurrentVersion,
            range.Start,
            range.EndExclusive,
            calendarIds.Order(StringComparer.Ordinal).ToArray(),
            result.RefreshedAt,
            result.UnreadableResourceCount,
            result.Occurrences.Select(CachedOccurrence.FromOccurrence).ToArray());
        return AtomicJsonFile.WriteAsync(path, document, SerializerOptions, cancellationToken);
    }

    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        File.Delete(path);
        return Task.CompletedTask;
    }

    private static bool SameCalendars(
        IReadOnlyCollection<string> cached,
        IReadOnlyCollection<string> requested) =>
        cached.Order(StringComparer.Ordinal).SequenceEqual(requested.Order(StringComparer.Ordinal));

    private void QuarantineUnreadableCache()
    {
        try
        {
            var directory = Path.GetDirectoryName(path)!;
            var quarantinePath = Path.Combine(
                directory,
                $"calendar-cache.corrupt-{DateTime.UtcNow:yyyyMMddHHmmssfff}.json");
            File.Move(path, quarantinePath, false);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static bool IsUnreadable(Exception exception) => exception is
        JsonException
        or IOException
        or UnauthorizedAccessException
        or ArgumentException;

    private sealed record CacheDocument(
        int Version,
        DateOnly RangeStart,
        DateOnly RangeEndExclusive,
        IReadOnlyList<string> CalendarIds,
        DateTimeOffset RefreshedAt,
        int UnreadableResourceCount,
        IReadOnlyList<CachedOccurrence> Occurrences);

    private sealed record CachedOccurrence(
        string CalendarId,
        string Uid,
        DateTimeOffset Start,
        DateTimeOffset End,
        bool IsAllDay,
        string Title,
        string Description,
        string Location,
        DateTimeOffset? RecurrenceId,
        string? SourceTimeZoneId,
        string? SourceResourceId)
    {
        public static CachedOccurrence FromOccurrence(CalendarOccurrence occurrence) => new(
            occurrence.CalendarId,
            occurrence.Uid,
            occurrence.Start,
            occurrence.End,
            occurrence.IsAllDay,
            occurrence.Title,
            occurrence.Description,
            occurrence.Location,
            occurrence.RecurrenceId,
            occurrence.SourceTimeZoneId,
            occurrence.SourceResourceId);

        public CalendarOccurrence ToOccurrence() => new(
            CalendarId,
            Uid,
            Start,
            End,
            IsAllDay,
            Title,
            Description,
            Location,
            RecurrenceId,
            SourceTimeZoneId,
            SourceResourceId);
    }
}
