using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

namespace YahooMonthPrint.YahooCalDav;

internal static class WebDavXmlParser
{
    private static readonly XNamespace Dav = "DAV:";
    private static readonly XNamespace CalDav = "urn:ietf:params:xml:ns:caldav";
    private static readonly XNamespace Apple = "http://apple.com/ns/ical/";

    public static Uri ParseSingleHref(string xml, XName propertyName, Uri responseUri)
    {
        var document = Parse(xml);
        var href = document
            .Descendants(propertyName)
            .Elements(Dav + "href")
            .Select(element => element.Value.Trim())
            .FirstOrDefault(value => value.Length > 0);

        if (href is null)
        {
            throw ProtocolFailure($"The WebDAV response did not contain {propertyName.LocalName}.");
        }

        return ResolveHref(responseUri, href);
    }

    public static IReadOnlyList<CalDavCalendar> ParseCalendars(string xml, Uri responseUri)
    {
        var document = Parse(xml);
        var calendars = new List<CalDavCalendar>();
        foreach (var response in document.Descendants(Dav + "response"))
        {
            var prop = SuccessfulProperties(response).FirstOrDefault();
            if (prop is null
                || !prop.Elements(Dav + "resourcetype")
                    .Elements(CalDav + "calendar")
                    .Any())
            {
                continue;
            }

            var href = response.Element(Dav + "href")?.Value.Trim();
            if (string.IsNullOrWhiteSpace(href))
            {
                continue;
            }

            var uri = ResolveHref(responseUri, href);
            var displayName = prop.Element(Dav + "displayname")?.Value.Trim();
            var color = prop.Element(Apple + "calendar-color")?.Value.Trim();
            calendars.Add(new CalDavCalendar(
                StableId(uri),
                string.IsNullOrWhiteSpace(displayName) ? Uri.UnescapeDataString(uri.Segments[^1].Trim('/')) : displayName,
                uri,
                string.IsNullOrWhiteSpace(color) ? null : color));
        }

        return calendars
            .GroupBy(calendar => calendar.Id, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(calendar => calendar.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static IReadOnlyList<(string ResourceId, string? CalendarData)> ParseCalendarData(
        string xml,
        Uri responseUri)
    {
        var document = Parse(xml);
        var resources = new List<(string ResourceId, string? CalendarData)>();
        foreach (var response in document.Descendants(Dav + "response"))
        {
            var href = response.Element(Dav + "href")?.Value.Trim();
            if (string.IsNullOrWhiteSpace(href))
            {
                continue;
            }

            var calendarData = SuccessfulProperties(response)
                .Elements(CalDav + "calendar-data")
                .Select(element => element.Value)
                .FirstOrDefault();
            resources.Add((
                ResolveHref(responseUri, href).AbsoluteUri,
                string.IsNullOrWhiteSpace(calendarData) ? null : calendarData));
        }

        return resources;
    }

    public static XName CurrentUserPrincipal => Dav + "current-user-principal";

    public static XName CalendarHomeSet => CalDav + "calendar-home-set";

    private static IEnumerable<XElement> SuccessfulProperties(XElement response) => response
        .Elements(Dav + "propstat")
        .Where(propstat => IsSuccessStatus(propstat.Element(Dav + "status")?.Value))
        .SelectMany(propstat => propstat.Elements(Dav + "prop"));

    private static bool IsSuccessStatus(string? status) =>
        status?.Contains(" 200 ", StringComparison.Ordinal) == true
        || status?.EndsWith(" 200", StringComparison.Ordinal) == true;

    private static XDocument Parse(string xml)
    {
        try
        {
            return XDocument.Parse(xml, LoadOptions.None);
        }
        catch (Exception exception) when (exception is System.Xml.XmlException or ArgumentException)
        {
            throw ProtocolFailure("Yahoo returned malformed WebDAV XML.", exception);
        }
    }

    private static Uri ResolveHref(Uri responseUri, string href)
    {
        if (!Uri.TryCreate(responseUri, href, out var resolved)
            || !string.Equals(resolved.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw ProtocolFailure("Yahoo returned an invalid or unsafe calendar URI.");
        }

        return resolved;
    }

    private static string StableId(Uri uri)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(uri.AbsoluteUri));
        return Convert.ToHexString(bytes[..12]).ToLowerInvariant();
    }

    private static CalDavException ProtocolFailure(string detail, Exception? innerException = null) =>
        new(
            CalDavFailureKind.Protocol,
            "Yahoo returned an unexpected calendar response.",
            detail,
            innerException);
}
