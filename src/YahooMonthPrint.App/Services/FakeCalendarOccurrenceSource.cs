using YahooMonthPrint.Core;

namespace YahooMonthPrint.App.Services;

public sealed class FakeCalendarOccurrenceSource : ICalendarOccurrenceSource
{
    private static readonly IReadOnlyList<CalendarSource> Sources =
    [
        new("college", "College", Color: "#325EA8"),
        new("personal", "Personal", Color: "#8A4F7D"),
        new("campus", "Campus", Color: "#567A38"),
    ];

    public IReadOnlyList<CalendarSource> Calendars => Sources;

    public async Task<CalendarLoadResult> LoadAsync(
        MonthGridRange range,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(range);
        await Task.Delay(TimeSpan.FromMilliseconds(120), cancellationToken);

        var occurrences = new List<CalendarOccurrence>();
        AddWeeklyClasses(range, occurrences);
        AddMonthSpecificEvents(range.DisplayedMonth, occurrences);
        return new CalendarLoadResult(
            occurrences.Order(OccurrenceComparer.Instance).ToArray(),
            DateTimeOffset.Now);
    }

    private static void AddWeeklyClasses(
        MonthGridRange range,
        List<CalendarOccurrence> occurrences)
    {
        var mondays = range.Dates.Where(date => date.DayOfWeek == DayOfWeek.Monday).ToArray();
        var examDate = mondays.FirstOrDefault(date => date.Month == range.DisplayedMonth.Month)
            .AddDays(7);

        foreach (var date in range.Dates)
        {
            if (date.DayOfWeek == DayOfWeek.Monday)
            {
                var isExam = date == examDate;
                occurrences.Add(Timed(
                    "college",
                    "calculus-series",
                    date,
                    9,
                    0,
                    50,
                    "Calculus II",
                    isExam
                        ? "EXAM 2 — Chapters 5–7.\nBring calculator.\nReview session follows."
                        : "Normal class meeting.\nReview the assigned problems before class.",
                    "Science 201",
                    recurrenceId: At(date, 9, 0)));
            }

            if (date.DayOfWeek == DayOfWeek.Wednesday)
            {
                occurrences.Add(Timed(
                    "college",
                    "physics-series",
                    date,
                    11,
                    0,
                    75,
                    "Physics",
                    "Normal lecture.\nLab preparation notes are in the course portal.",
                    "Engineering 110",
                    recurrenceId: At(date, 11, 0)));
            }

            if (date.DayOfWeek == DayOfWeek.Friday)
            {
                occurrences.Add(Timed(
                    "campus",
                    "office-hours-series",
                    date,
                    15,
                    0,
                    60,
                    "Office Hours",
                    "Optional drop-in help.",
                    "Library 2B",
                    recurrenceId: At(date, 15, 0)));
            }
        }
    }

    private static void AddMonthSpecificEvents(
        DateOnly displayedMonth,
        List<CalendarOccurrence> occurrences)
    {
        var deadline = SafeDate(displayedMonth, 18);
        occurrences.Add(new CalendarOccurrence(
            "college",
            $"project-deadline-{displayedMonth:yyyy-MM}",
            At(deadline, 0, 0),
            At(deadline.AddDays(1), 0, 0),
            true,
            "Research project due",
            "Submit the final report and bibliography.\nConfirm the upload receipt.",
            sourceTimeZoneId: TimeZoneInfo.Local.Id));

        var appointment = SafeDate(displayedMonth, 22);
        occurrences.Add(Timed(
            "personal",
            $"appointment-{displayedMonth:yyyy-MM}",
            appointment,
            14,
            30,
            45,
            "Doctor appointment",
            "Bring insurance card and current medication list.",
            "Health Center"));

        var denseDay = SafeDate(displayedMonth, 24);
        var denseItems = new[]
        {
            (8, "Study Group", "Practice problems for next week's quiz."),
            (10, "Chemistry Lab", "Titration lab; wear closed-toe shoes."),
            (13, "Advising", "Review spring registration options."),
            (16, "Club Meeting", "Budget vote and volunteer sign-up."),
            (19, "Reading Due", "Finish chapters 8–10 before discussion."),
        };

        foreach (var (hour, title, description) in denseItems)
        {
            occurrences.Add(Timed(
                hour == 19 ? "personal" : "college",
                $"dense-{displayedMonth:yyyy-MM}-{hour}",
                denseDay,
                hour,
                0,
                45,
                title,
                description,
                hour == 19 ? string.Empty : "Campus"));
        }
    }

    private static CalendarOccurrence Timed(
        string calendarId,
        string uid,
        DateOnly date,
        int hour,
        int minute,
        int durationMinutes,
        string title,
        string description,
        string location,
        DateTimeOffset? recurrenceId = null)
    {
        var start = At(date, hour, minute);
        return new CalendarOccurrence(
            calendarId,
            uid,
            start,
            start.AddMinutes(durationMinutes),
            false,
            title,
            description,
            location,
            recurrenceId,
            TimeZoneInfo.Local.Id);
    }

    private static DateOnly SafeDate(DateOnly month, int desiredDay) =>
        new(month.Year, month.Month, Math.Min(desiredDay, DateTime.DaysInMonth(month.Year, month.Month)));

    private static DateTimeOffset At(DateOnly date, int hour, int minute)
    {
        var local = date.ToDateTime(new TimeOnly(hour, minute), DateTimeKind.Unspecified);
        return new DateTimeOffset(local, TimeZoneInfo.Local.GetUtcOffset(local));
    }
}
