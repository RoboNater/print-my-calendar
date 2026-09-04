using YahooMonthPrint.Core;

namespace YahooMonthPrint.YahooCalDav.Tests;

public sealed class RealYahooIntegrationTests
{
    [Fact]
    public async Task DiscoveryAndReadOnlyQueryAgainstOptInTestAccount()
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable("YMP_RUN_YAHOO_INTEGRATION"),
                "1",
                StringComparison.Ordinal))
        {
            Assert.Skip("Set YMP_RUN_YAHOO_INTEGRATION=1 to enable the real Yahoo read-only test.");
        }

        var account = Environment.GetEnvironmentVariable("YMP_TEST_YAHOO_USER");
        var password = Environment.GetEnvironmentVariable("YMP_TEST_YAHOO_APP_PASSWORD");
        if (string.IsNullOrWhiteSpace(account) || string.IsNullOrWhiteSpace(password))
        {
            Assert.Skip("Set both sanitized Yahoo integration-test credential environment variables.");
        }

        using var client = new YahooCalDavClient(YahooCalDavClient.CreateHttpClient(account, password));
        var calendars = await client.DiscoverCalendarsAsync(TestContext.Current.CancellationToken);
        Assert.NotEmpty(calendars);

        var today = DateOnly.FromDateTime(DateTime.Today);
        var range = MonthGrid.Create(today.Year, today.Month);
        _ = await client.QueryCalendarsAsync(
            calendars.Take(1).ToArray(),
            range,
            TestContext.Current.CancellationToken);
    }
}
