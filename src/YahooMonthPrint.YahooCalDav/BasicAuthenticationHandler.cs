using System.Net.Http.Headers;
using System.Text;

namespace YahooMonthPrint.YahooCalDav;

public sealed class BasicAuthenticationHandler : DelegatingHandler
{
    private readonly string parameter;

    public BasicAuthenticationHandler(
        string userName,
        string appPassword,
        HttpMessageHandler innerHandler)
        : base(innerHandler)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userName);
        ArgumentException.ThrowIfNullOrWhiteSpace(appPassword);

        parameter = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{userName}:{appPassword}"));
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", parameter);
        return base.SendAsync(request, cancellationToken);
    }
}
