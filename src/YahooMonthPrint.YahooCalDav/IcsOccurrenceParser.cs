using System.Runtime.Serialization;
using Ical.Net;
using Ical.Net.CalendarComponents;
using Ical.Net.DataTypes;
using Ical.Net.Evaluation;
using YahooMonthPrint.Core;

namespace YahooMonthPrint.YahooCalDav;

public sealed class IcsOccurrenceParser(TimeZoneInfo viewerTimeZone)
{
    public IReadOnlyList<CalendarOccurrence> Parse(
        string calendarId,
        string resourceId,
        string calendarData,
        MonthGridRange range)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(calendarId);
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(calendarData);
        ArgumentNullException.ThrowIfNull(range);

        try
        {
            var calendar = Calendar.Load(calendarData)
                ?? throw new FormatException("The calendar resource did not contain a VCALENDAR.");
            var rangeStartUtc = LocalTimeBoundary.ToUtc(range.Start, viewerTimeZone);
            var rangeEndUtc = LocalTimeBoundary.ToUtc(range.EndExclusive, viewerTimeZone);
            var longestDuration = calendar.Events
                .Select(GetDuration)
                .DefaultIfEmpty(TimeSpan.Zero)
                .Max();
            var evaluationStartUtc = rangeStartUtc - longestDuration;
            return calendar
                .GetOccurrences(new CalDateTime(evaluationStartUtc), new EvaluationOptions())
                .TakeWhile(occurrence => AsUtc(occurrence.Period.StartTime) < rangeEndUtc)
                .Select(occurrence => ConvertOccurrence(calendarId, resourceId, occurrence))
                .Where(occurrence => occurrence is not null)
                .Cast<CalendarOccurrence>()
                .Where(occurrence => Overlaps(occurrence, range))
                .DistinctBy(occurrence => occurrence.Key)
                .Order(OccurrenceComparer.Instance)
                .ToArray();
        }
        catch (Exception exception) when (IsMalformedCalendarData(exception))
        {
            throw new CalendarResourceException(exception);
        }
    }

    private CalendarOccurrence? ConvertOccurrence(
        string calendarId,
        string resourceId,
        Occurrence occurrence)
    {
        if (occurrence.Source is not CalendarEvent calendarEvent
            || !calendarEvent.IsActive
            || string.Equals(calendarEvent.Status, "CANCELLED", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var period = occurrence.Period;
        var isAllDay = calendarEvent.IsAllDay || !period.StartTime.HasTime;
        var start = ConvertDateTime(period.StartTime, isAllDay);
        var effectiveEnd = period.EffectiveEndTime ?? period.StartTime;
        var end = ConvertDateTime(effectiveEnd, isAllDay);
        var isRecurring = calendarEvent.RecurrenceIdentifier is not null
            || calendarEvent.RecurrenceRule is not null
            || calendarEvent.RecurrenceDates.GetAllDates().Any()
            || calendarEvent.RecurrenceDates.GetAllPeriods().Any();
        DateTimeOffset? recurrenceId = calendarEvent.RecurrenceIdentifier?.StartTime is { } overriddenId
            ? ConvertDateTime(overriddenId, isAllDay)
            : isRecurring ? start : null;
        if (string.IsNullOrWhiteSpace(calendarEvent.Uid))
        {
            throw new FormatException("A VEVENT did not contain a UID.");
        }

        return new CalendarOccurrence(
            calendarId,
            calendarEvent.Uid,
            start,
            end,
            isAllDay,
            calendarEvent.Summary ?? string.Empty,
            calendarEvent.Description,
            calendarEvent.Location,
            recurrenceId,
            period.StartTime.TzId,
            resourceId,
            viewerTimeZone);
    }

    private DateTimeOffset ConvertDateTime(CalDateTime value, bool isAllDay)
    {
        if (isAllDay || value.IsFloating)
        {
            var local = DateTime.SpecifyKind(value.Value, DateTimeKind.Unspecified);
            return new DateTimeOffset(local, viewerTimeZone.GetUtcOffset(local));
        }

        var utc = DateTime.SpecifyKind(value.AsUtc, DateTimeKind.Utc);
        return TimeZoneInfo.ConvertTime(new DateTimeOffset(utc), viewerTimeZone);
    }

    private DateTime AsUtc(CalDateTime value)
    {
        if (!value.IsFloating && value.HasTime)
        {
            return DateTime.SpecifyKind(value.AsUtc, DateTimeKind.Utc);
        }

        var local = DateTime.SpecifyKind(value.Value, DateTimeKind.Unspecified);
        while (viewerTimeZone.IsInvalidTime(local))
        {
            local = local.AddMinutes(1);
        }

        return new DateTimeOffset(local, viewerTimeZone.GetUtcOffset(local)).UtcDateTime;
    }

    private static TimeSpan GetDuration(CalendarEvent calendarEvent)
    {
        try
        {
            return calendarEvent.DtStart is null
                ? TimeSpan.Zero
                : calendarEvent.EffectiveDuration.ToTimeSpan(calendarEvent.DtStart).Duration();
        }
        catch (ArgumentException)
        {
            return TimeSpan.Zero;
        }
    }

    private static bool IsMalformedCalendarData(Exception exception) =>
        exception is SerializationException
            or FormatException
            or OverflowException
            or EvaluationException
        || exception is ArgumentException
            && exception.TargetSite?.DeclaringType?.Assembly == typeof(Calendar).Assembly;

    private static bool Overlaps(CalendarOccurrence occurrence, MonthGridRange range)
    {
        var start = range.Start.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        var end = range.EndExclusive.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        if (occurrence.Start == occurrence.End)
        {
            return occurrence.Start.DateTime >= start && occurrence.Start.DateTime < end;
        }

        return occurrence.Start.DateTime < end && occurrence.End.DateTime > start;
    }
}

internal sealed class CalendarResourceException : Exception
{
    public CalendarResourceException(Exception innerException)
        : base("The iCalendar resource could not be parsed.", innerException)
    {
        SourceExceptionType = innerException.GetType().Name;
    }

    public string SourceExceptionType { get; }
}
