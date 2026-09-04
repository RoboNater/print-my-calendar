using System.Net;
using System.Net.Http.Headers;
using System.Text;
using YahooMonthPrint.Core;

namespace YahooMonthPrint.YahooCalDav;

public sealed class YahooCalDavClient
{
    public static readonly Uri DefaultServiceUri = new("https://caldav.calendar.yahoo.com/");

    private const string PrincipalRequest = """
        <?xml version="1.0" encoding="utf-8"?>
        <d:propfind xmlns:d="DAV:"><d:prop><d:current-user-principal /></d:prop></d:propfind>
        """;
    private const string HomeRequest = """
        <?xml version="1.0" encoding="utf-8"?>
        <d:propfind xmlns:d="DAV:" xmlns:c="urn:ietf:params:xml:ns:caldav">
          <d:prop><c:calendar-home-set /></d:prop>
        </d:propfind>
        """;
    private const string CalendarsRequest = """
        <?xml version="1.0" encoding="utf-8"?>
        <d:propfind xmlns:d="DAV:" xmlns:c="urn:ietf:params:xml:ns:caldav" xmlns:a="http://apple.com/ns/ical/">
          <d:prop><d:displayname /><d:resourcetype /><a:calendar-color /></d:prop>
        </d:propfind>
        """;

    private readonly HttpClient httpClient;
    private readonly IcsOccurrenceParser occurrenceParser;
    private readonly Uri serviceUri;

    public YahooCalDavClient(
        HttpClient httpClient,
        TimeZoneInfo? viewerTimeZone = null,
        Uri? serviceUri = null)
    {
        this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        this.serviceUri = serviceUri ?? DefaultServiceUri;
        EnsureHttps(this.serviceUri);
        occurrenceParser = new IcsOccurrenceParser(viewerTimeZone ?? TimeZoneInfo.Local);
        ViewerTimeZone = viewerTimeZone ?? TimeZoneInfo.Local;
    }

    public TimeZoneInfo ViewerTimeZone { get; }

    public static HttpClient CreateHttpClient(
        string userName,
        string appPassword,
        HttpMessageHandler? transport = null)
    {
        transport ??= new HttpClientHandler
        {
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.All,
        };
        var safety = new ReadOnlyHttpsHandler(transport);
        return new HttpClient(new BasicAuthenticationHandler(userName, appPassword, safety));
    }

    public async Task<IReadOnlyList<CalDavCalendar>> DiscoverCalendarsAsync(
        CancellationToken cancellationToken)
    {
        var principalXml = await SendXmlAsync(
            serviceUri,
            "PROPFIND",
            "0",
            PrincipalRequest,
            cancellationToken);
        var principalUri = WebDavXmlParser.ParseSingleHref(
            principalXml,
            WebDavXmlParser.CurrentUserPrincipal,
            serviceUri);
        var homeXml = await SendXmlAsync(
            principalUri,
            "PROPFIND",
            "0",
            HomeRequest,
            cancellationToken);
        var homeUri = WebDavXmlParser.ParseSingleHref(
            homeXml,
            WebDavXmlParser.CalendarHomeSet,
            principalUri);
        var calendarsXml = await SendXmlAsync(
            homeUri,
            "PROPFIND",
            "1",
            CalendarsRequest,
            cancellationToken);
        return WebDavXmlParser.ParseCalendars(calendarsXml, homeUri);
    }

    public async Task<CalendarQueryResult> QueryCalendarsAsync(
        IReadOnlyCollection<CalDavCalendar> calendars,
        MonthGridRange range,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(calendars);
        ArgumentNullException.ThrowIfNull(range);

        var occurrences = new List<CalendarOccurrence>();
        var issues = new List<CalDavResourceIssue>();
        foreach (var calendar in calendars)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await QueryCalendarAsync(calendar, range, cancellationToken);
            occurrences.AddRange(result.Occurrences);
            issues.AddRange(result.ResourceIssues);
        }

        return new CalendarQueryResult(
            occurrences.Order(OccurrenceComparer.Instance).ToArray(),
            issues.ToArray());
    }

    public async Task<CalendarQueryResult> QueryCalendarAsync(
        CalDavCalendar calendar,
        MonthGridRange range,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(calendar);
        ArgumentNullException.ThrowIfNull(range);
        EnsureHttps(calendar.Uri);

        var xml = await SendXmlAsync(
            calendar.Uri,
            "REPORT",
            "1",
            CalendarQueryBuilder.Build(range, ViewerTimeZone),
            cancellationToken,
            isCalendarCollection: true);
        var resources = WebDavXmlParser.ParseCalendarData(xml, calendar.Uri);
        var occurrences = new List<CalendarOccurrence>();
        var issues = new List<CalDavResourceIssue>();
        foreach (var (resourceId, calendarData) in resources)
        {
            if (calendarData is null)
            {
                issues.Add(new CalDavResourceIssue(
                    calendar.Id,
                    resourceId,
                    "MissingCalendarData"));
                continue;
            }

            try
            {
                occurrences.AddRange(occurrenceParser.Parse(
                    calendar.Id,
                    resourceId,
                    calendarData,
                    range));
            }
            catch (Exception exception) when (IsResourceParseFailure(exception))
            {
                issues.Add(new CalDavResourceIssue(
                    calendar.Id,
                    resourceId,
                    exception.GetType().Name));
            }
        }

        return new CalendarQueryResult(occurrences, issues);
    }

    private async Task<string> SendXmlAsync(
        Uri uri,
        string method,
        string depth,
        string body,
        CancellationToken cancellationToken,
        bool isCalendarCollection = false)
    {
        EnsureHttps(uri);
        using var request = new HttpRequestMessage(new HttpMethod(method), uri);
        request.Headers.TryAddWithoutValidation("Depth", depth);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/xml"));
        request.Content = new StringContent(body, Encoding.UTF8, "application/xml");

        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (HttpRequestException exception)
        {
            throw new CalDavException(
                CalDavFailureKind.Connectivity,
                "Yahoo Calendar could not be reached. Check your Internet connection and try again.",
                $"{method} request failed with {exception.GetType().Name}.",
                exception);
        }

        using (response)
        {
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                throw new CalDavException(
                    CalDavFailureKind.Authentication,
                    "Yahoo did not accept the account name or app password.",
                    $"{method} returned HTTP {(int)response.StatusCode}.");
            }

            if (isCalendarCollection && response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone)
            {
                throw new CalDavException(
                    CalDavFailureKind.CalendarCollectionRejected,
                    "A saved Yahoo calendar could not be found. Calendar discovery must be refreshed.",
                    $"{method} returned HTTP {(int)response.StatusCode} for a saved calendar collection.");
            }

            if ((int)response.StatusCode >= 500)
            {
                throw new CalDavException(
                    CalDavFailureKind.Server,
                    "Yahoo Calendar is temporarily unavailable. Try again later.",
                    $"{method} returned HTTP {(int)response.StatusCode}.");
            }

            if (!response.IsSuccessStatusCode && response.StatusCode != (HttpStatusCode)207)
            {
                throw new CalDavException(
                    CalDavFailureKind.Protocol,
                    "Yahoo returned an unexpected calendar response.",
                    $"{method} returned HTTP {(int)response.StatusCode}.");
            }

            return await response.Content.ReadAsStringAsync(cancellationToken);
        }
    }

    private static void EnsureHttps(Uri uri)
    {
        if (!uri.IsAbsoluteUri
            || !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("CalDAV endpoints must be absolute HTTPS URIs.", nameof(uri));
        }
    }

    private static bool IsResourceParseFailure(Exception exception) => exception is not
        OutOfMemoryException
        and not AccessViolationException
        and not StackOverflowException;
}
