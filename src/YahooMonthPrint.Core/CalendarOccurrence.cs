namespace YahooMonthPrint.Core;

public sealed record CalendarOccurrence
{
    public CalendarOccurrence(
        string calendarId,
        string uid,
        DateTimeOffset start,
        DateTimeOffset end,
        bool isAllDay,
        string title,
        string? description = null,
        string? location = null,
        DateTimeOffset? recurrenceId = null,
        string? sourceTimeZoneId = null,
        string? sourceResourceId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(calendarId);
        ArgumentException.ThrowIfNullOrWhiteSpace(uid);

        if (end < start)
        {
            throw new ArgumentOutOfRangeException(nameof(end), "An occurrence cannot end before it starts.");
        }

        CalendarId = calendarId;
        Uid = uid;
        Start = start;
        End = end;
        IsAllDay = isAllDay;
        Title = string.IsNullOrWhiteSpace(title) ? "(Untitled)" : title.Trim();
        Description = DescriptionNormalizer.Normalize(description);
        Location = string.IsNullOrWhiteSpace(location) ? string.Empty : location.Trim();
        RecurrenceId = recurrenceId;
        SourceTimeZoneId = sourceTimeZoneId;
        SourceResourceId = sourceResourceId;
    }

    public string CalendarId { get; }

    public string Uid { get; }

    public DateTimeOffset Start { get; }

    public DateTimeOffset End { get; }

    public bool IsAllDay { get; }

    public string Title { get; }

    public string Description { get; }

    public string Location { get; }

    public DateTimeOffset? RecurrenceId { get; }

    public string? SourceTimeZoneId { get; }

    public string? SourceResourceId { get; }

    public OccurrenceKey Key => new(CalendarId, Uid, RecurrenceId ?? Start);
}
