using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Globalization;
using YahooMonthPrint.Core;

namespace YahooMonthPrint.YahooCalDav.Tests;

public sealed class YahooCalDavClientTests
{
    private static readonly MonthGridRange September = MonthGrid.Create(2026, 9);

    [Fact]
    public async Task DiscoveryResolvesNamespacesHrefsAndCalendarCollections()
    {
        var handler = new QueueHandler(
            XmlResponse("""
                <d:multistatus xmlns:d="DAV:">
                  <d:response><d:propstat><d:prop>
                    <d:current-user-principal><d:href>/dav/principals/users/student%40example.com/</d:href></d:current-user-principal>
                  </d:prop><d:status>HTTP/1.1 200 OK</d:status></d:propstat></d:response>
                </d:multistatus>
                """),
            XmlResponse("""
                <x:multistatus xmlns:x="DAV:" xmlns:c="urn:ietf:params:xml:ns:caldav">
                  <x:response><x:propstat><x:prop>
                    <c:calendar-home-set><x:href>../../../calendars/student%40example.com/</x:href></c:calendar-home-set>
                  </x:prop><x:status>HTTP/1.1 200 OK</x:status></x:propstat></x:response>
                </x:multistatus>
                """),
            XmlResponse("""
                <d:multistatus xmlns:d="DAV:" xmlns:c="urn:ietf:params:xml:ns:caldav" xmlns:a="http://apple.com/ns/ical/">
                  <d:response>
                    <d:href>/dav/calendars/student%40example.com/</d:href>
                    <d:propstat><d:prop><d:displayname>Home</d:displayname><d:resourcetype><d:collection /></d:resourcetype></d:prop><d:status>HTTP/1.1 200 OK</d:status></d:propstat>
                  </d:response>
                  <d:response>
                    <d:href>college%20schedule/</d:href>
                    <d:propstat><d:prop><d:displayname>College</d:displayname><d:resourcetype><d:collection/><c:calendar/></d:resourcetype><a:calendar-color>#325EA8FF</a:calendar-color></d:prop><d:status>HTTP/1.1 200 OK</d:status></d:propstat>
                  </d:response>
                  <d:response>
                    <d:href>missing-name/</d:href>
                    <d:propstat><d:prop><d:resourcetype><c:calendar/></d:resourcetype></d:prop><d:status>HTTP/1.1 200 OK</d:status></d:propstat>
                  </d:response>
                </d:multistatus>
                """));
        var client = new YahooCalDavClient(
            new HttpClient(handler),
            serviceUri: new Uri("https://calendar.example.test/dav/"));

        var calendars = await client.DiscoverCalendarsAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, calendars.Count);
        Assert.Equal("College", calendars[0].DisplayName);
        Assert.Equal("#325EA8FF", calendars[0].Color);
        Assert.Equal(
            "https://calendar.example.test/dav/calendars/student%40example.com/college%20schedule/",
            calendars[0].Uri.AbsoluteUri);
        Assert.Equal("missing-name", calendars[1].DisplayName);
        Assert.Equal(3, handler.Requests.Count);
        Assert.Equal("0", handler.Requests[0].Depth);
        Assert.Equal("1", handler.Requests[2].Depth);
    }

    [Fact]
    public async Task QueryUsesCompleteGridBoundariesAndParsesResourcesIndependently()
    {
        var handler = new QueueHandler(XmlResponse($"""
            <d:multistatus xmlns:d="DAV:" xmlns:c="urn:ietf:params:xml:ns:caldav">
              <d:response><d:href>good.ics</d:href><d:propstat><d:prop><c:calendar-data><![CDATA[{SingleEventIcs}]]></c:calendar-data></d:prop><d:status>HTTP/1.1 200 OK</d:status></d:propstat></d:response>
              <d:response><d:href>bad.ics</d:href><d:propstat><d:prop><c:calendar-data>not an ics file</c:calendar-data></d:prop><d:status>HTTP/1.1 200 OK</d:status></d:propstat></d:response>
              <d:response><d:href>unreadable.ics</d:href><d:status>HTTP/1.1 500 Server Error</d:status></d:response>
            </d:multistatus>
            """));
        var client = new YahooCalDavClient(new HttpClient(handler));
        var calendar = new CalDavCalendar(
            "college",
            "College",
            new Uri("https://calendar.example.test/college/"));

        var result = await client.QueryCalendarAsync(
            calendar,
            September,
            TestContext.Current.CancellationToken);

        Assert.Single(result.Occurrences);
        Assert.Equal(2, result.ResourceIssues.Count);
        Assert.Contains(
            result.ResourceIssues,
            issue => issue.ResourceId == "https://calendar.example.test/college/bad.ics");
        Assert.Contains(
            result.ResourceIssues,
            issue => issue.ExceptionType == "MissingCalendarData");
        var request = Assert.Single(handler.Requests);
        Assert.Equal("REPORT", request.Method);
        Assert.Contains(ToUtcStamp(September.Start), request.Body, StringComparison.Ordinal);
        Assert.Contains(ToUtcStamp(September.EndExclusive), request.Body, StringComparison.Ordinal);
        Assert.Contains("name=\"VEVENT\"", request.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FailedCalendarCollectionFailsTheWholeMultiCalendarLoad()
    {
        var handler = new QueueHandler(
            XmlResponse("<d:multistatus xmlns:d=\"DAV:\" />"),
            new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        var client = new YahooCalDavClient(new HttpClient(handler));
        var calendars = new[]
        {
            new CalDavCalendar("first", "First", new Uri("https://calendar.example.test/first/")),
            new CalDavCalendar("second", "Second", new Uri("https://calendar.example.test/second/")),
        };

        var exception = await Assert.ThrowsAsync<CalDavException>(() => client.QueryCalendarsAsync(
            calendars,
            September,
            TestContext.Current.CancellationToken));

        Assert.Equal(CalDavFailureKind.Server, exception.Kind);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Theory]
    [InlineData("http://calendar.example.test/calendar/", "REPORT")]
    [InlineData("https://calendar.example.test/calendar/", "PUT")]
    [InlineData("https://calendar.example.test/calendar/", "DELETE")]
    [InlineData("https://calendar.example.test/calendar/", "POST")]
    public async Task SafetyHandlerRejectsPlaintextAndMutationMethods(string uri, string method)
    {
        var inner = new QueueHandler(XmlResponse("<ok />"));
        using var client = new HttpClient(new ReadOnlyHttpsHandler(inner));
        using var request = new HttpRequestMessage(new HttpMethod(method), uri);

        await Assert.ThrowsAsync<InvalidOperationException>(() => client.SendAsync(
            request,
            TestContext.Current.CancellationToken));
        Assert.Empty(inner.Requests);
    }

    [Fact]
    public async Task AuthenticationIsAppliedWithoutLeakingTheSecretInErrors()
    {
        const string password = "unique-app-secret";
        var handler = new QueueHandler(new HttpResponseMessage(HttpStatusCode.Unauthorized));
        using var httpClient = YahooCalDavClient.CreateHttpClient("student@example.test", password, handler);
        var client = new YahooCalDavClient(
            httpClient,
            serviceUri: new Uri("https://calendar.example.test/"));

        var exception = await Assert.ThrowsAsync<CalDavException>(() =>
            client.DiscoverCalendarsAsync(TestContext.Current.CancellationToken));

        Assert.Equal(CalDavFailureKind.Authentication, exception.Kind);
        Assert.DoesNotContain(password, exception.ToString(), StringComparison.Ordinal);
        var request = Assert.Single(handler.Requests);
        Assert.Equal("Basic", request.Authorization?.Scheme);
        Assert.DoesNotContain(password, request.Authorization?.Parameter ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TruncatedCollectionXmlIsAProtocolFailureNotPartialSuccess()
    {
        var handler = new QueueHandler(XmlResponse("<d:multistatus xmlns:d=\"DAV:\"><d:response>"));
        var client = new YahooCalDavClient(new HttpClient(handler));

        var exception = await Assert.ThrowsAsync<CalDavException>(() => client.QueryCalendarAsync(
            new CalDavCalendar("college", "College", new Uri("https://calendar.example.test/college/")),
            September,
            TestContext.Current.CancellationToken));

        Assert.Equal(CalDavFailureKind.Protocol, exception.Kind);
    }

    private const string SingleEventIcs = """
        BEGIN:VCALENDAR
        VERSION:2.0
        PRODID:-//Yahoo Month Print//Tests//EN
        BEGIN:VEVENT
        UID:one@example.test
        DTSTAMP:20260801T000000Z
        DTSTART:20260908T130000Z
        DTEND:20260908T140000Z
        SUMMARY:Calculus II
        DESCRIPTION:Review chapters 1-3
        END:VEVENT
        END:VCALENDAR
        """;

    private static HttpResponseMessage XmlResponse(string xml) => new((HttpStatusCode)207)
    {
        Content = new StringContent(xml, Encoding.UTF8, "application/xml"),
    };

    private static string ToUtcStamp(DateOnly date)
    {
        var local = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(local, TimeZoneInfo.Local)
            .ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);
    }

    private sealed class QueueHandler(params HttpResponseMessage[] responses) : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> responses = new(responses);

        public List<CapturedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(new CapturedRequest(
                request.Method.Method,
                request.RequestUri,
                request.Headers.TryGetValues("Depth", out var depths) ? depths.Single() : null,
                request.Headers.Authorization,
                request.Content is null
                    ? string.Empty
                    : await request.Content.ReadAsStringAsync(cancellationToken)));
            return responses.Dequeue();
        }
    }

    private sealed record CapturedRequest(
        string Method,
        Uri? Uri,
        string? Depth,
        AuthenticationHeaderValue? Authorization,
        string Body);
}
