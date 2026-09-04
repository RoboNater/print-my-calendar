namespace YahooMonthPrint.YahooCalDav;

public sealed class ReadOnlyHttpsHandler(HttpMessageHandler innerHandler)
    : DelegatingHandler(innerHandler)
{
    private static readonly HashSet<HttpMethod> AllowedMethods =
    [
        HttpMethod.Get,
        new HttpMethod("PROPFIND"),
        new HttpMethod("REPORT"),
    ];

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (request.RequestUri is null
            || !string.Equals(request.RequestUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Yahoo calendar requests require an HTTPS endpoint.");
        }

        if (!AllowedMethods.Contains(request.Method))
        {
            throw new InvalidOperationException(
                $"HTTP method {request.Method.Method} is not permitted by the read-only calendar client.");
        }

        return base.SendAsync(request, cancellationToken);
    }
}
