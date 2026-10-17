using System.Globalization;
using System.Net;
using System.Text;
using Otium.Core.Power;

namespace Otium.Tests;

public class ElectricityPriceProviderTests
{
    private static readonly DateTimeOffset Noon = new(2026, 10, 16, 12, 30, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("NO1", "https://www.hvakosterstrommen.no/api/v1/prices/2026/10-16_NO1.json")]
    [InlineData("SE3", "https://www.elprisetjustnu.se/api/v1/prices/2026/10-16_SE3.json")]
    [InlineData("DK1", "https://www.elprisenligenu.dk/api/v1/prices/2026/10-16_DK1.json")]
    [InlineData("DK2", "https://www.elprisenligenu.dk/api/v1/prices/2026/10-16_DK2.json")]
    public void Nordic_zones_use_their_country_api(string code, string expected)
    {
        Assert.Equal(expected, SpotPriceZone.Find(code)!.UrlFor(new DateOnly(2026, 10, 16)).ToString());
    }

    [Theory]
    // Summer time (UTC+3): the Finnish day starts at 21:00 UTC the evening before.
    [InlineData(2026, 7, 1, "start=2026-06-30T21:00:00.000Z&end=2026-07-01T21:00:00.000Z")]
    // Winter time (UTC+2).
    [InlineData(2026, 12, 1, "start=2026-11-30T22:00:00.000Z&end=2026-12-01T22:00:00.000Z")]
    // Clocks go back on 25 October 2026, so that day is 25 hours long.
    [InlineData(2026, 10, 25, "start=2026-10-24T21:00:00.000Z&end=2026-10-25T22:00:00.000Z")]
    public void Finland_asks_for_exactly_the_Finnish_calendar_day(int year, int month, int day, string range)
    {
        var url = SpotPriceZone.Find("FI")!.UrlFor(new DateOnly(year, month, day));
        Assert.Equal($"https://sahkotin.fi/prices?{range}", url.ToString());
    }

    [Theory]
    [InlineData("NO", "NO1", 5)]
    [InlineData("SE", "SE3", 4)]
    [InlineData("DK", "DK1", 2)]
    [InlineData("FI", "FI", 1)]
    public void Every_live_country_has_a_default_zone(string country, string defaultZone, int zones)
    {
        Assert.Equal(defaultZone, SpotPriceZone.DefaultFor(country)!.Code);
        Assert.Equal(zones, SpotPriceZone.ForCountry(country).Count);
        Assert.All(SpotPriceZone.ForCountry(country), z => Assert.Equal(country, z.Country));
    }

    [Fact]
    public void Countries_without_a_source_have_no_zones()
    {
        Assert.Null(SpotPriceZone.DefaultFor("DE"));
        Assert.Empty(SpotPriceZone.ForCountry("DE"));
        Assert.Null(SpotPriceZone.Find("XX"));
        Assert.Null(SpotPriceZone.Find(null));
    }

    [Fact]
    public async Task Danish_prices_give_the_current_hour_relative_to_the_day()
    {
        // 23 hours at 0.10 EUR and the current hour (12:00) at 0.34: the average is 0.11, so now is ~3.09x average.
        var prices = Enumerable.Range(0, 24).Select(h => h == 12 ? 0.34 : 0.10).ToArray();
        var http = new StubHttp().Add("elprisenligenu.dk/api/v1/prices/2026/10-16_DK1.json", NordicJson(prices));
        var provider = new ElectricityPriceProvider(http.Client, Utc(Noon));

        await provider.RefreshAsync(SpotPriceZone.Find("DK1"), default);

        Assert.True(provider.HasLivePrices);
        Assert.Equal("DK1", provider.Zone!.Code);
        Assert.Equal(0.34 / 0.11, provider.CurrentMultiplier()!.Value, 6);
        Assert.Equal("209% above today's average right now", provider.Describe());
    }

    [Fact]
    public async Task Finnish_prices_are_read_from_sahkotin_in_eur_per_mwh()
    {
        // 50 EUR/MWh all day except 10 at the current hour: well below average.
        var prices = Enumerable.Range(0, 24).Select(h => h == 12 ? 10.0 : 50.0).ToArray();
        var day = new DateTimeOffset(2026, 10, 16, 0, 0, 0, TimeSpan.Zero);
        var json = "{\"prices\":[" + string.Join(",", prices.Select((p, h) => string.Create(CultureInfo.InvariantCulture,
            $"{{\"date\":\"{day.AddHours(h):yyyy-MM-ddTHH:mm:ss.fffZ}\",\"value\":{p}}}"))) + "]}";
        var http = new StubHttp().Add("sahkotin.fi/prices", json);
        var provider = new ElectricityPriceProvider(http.Client, Utc(Noon));

        await provider.RefreshAsync(SpotPriceZone.Find("FI"), default);

        var average = (23 * 50.0 + 10) / 24;
        Assert.Equal(10 / average, provider.CurrentMultiplier()!.Value, 6);
        Assert.StartsWith("79% below", provider.Describe());
    }

    [Fact]
    public async Task Quarter_hour_prices_pick_the_current_quarter()
    {
        // Sweden publishes 15-minute prices since the Nordic market moved to 15-minute periods.
        var quarters = Enumerable.Range(0, 96).Select(q => q == 50 ? 0.25 : 0.10).ToArray();
        var http = new StubHttp().Add("elprisetjustnu.se", NordicJson(quarters, TimeSpan.FromMinutes(15)));
        var provider = new ElectricityPriceProvider(http.Client, Utc(Noon)); // 12:30 is quarter 50

        await provider.RefreshAsync(SpotPriceZone.Find("SE3"), default);

        Assert.Equal(0.25 / ((95 * 0.10 + 0.25) / 96), provider.CurrentMultiplier()!.Value, 6);
    }

    [Theory]
    [InlineData(5.00, 4.0)]  // a price spike is capped
    [InlineData(-0.50, 0.0)] // negative prices count as free, not as being paid to mine
    public async Task Extreme_hours_are_clamped(double currentPrice, double expected)
    {
        var prices = Enumerable.Range(0, 24).Select(h => h == 12 ? currentPrice : 0.10).ToArray();
        var http = new StubHttp().Add("hvakosterstrommen.no", NordicJson(prices));
        var provider = new ElectricityPriceProvider(http.Client, Utc(Noon));

        await provider.RefreshAsync(SpotPriceZone.Find("NO1"), default);

        Assert.Equal(expected, provider.CurrentMultiplier());
    }

    [Fact]
    public async Task A_near_zero_average_falls_back_to_the_flat_price()
    {
        var http = new StubHttp().Add("hvakosterstrommen.no", NordicJson(new double[24]));
        var provider = new ElectricityPriceProvider(http.Client, Utc(Noon));

        await provider.RefreshAsync(SpotPriceZone.Find("NO1"), default);

        Assert.Null(provider.CurrentMultiplier());
        Assert.Null(provider.Describe());
    }

    [Fact]
    public async Task Prices_are_fetched_once_per_day()
    {
        var time = Utc(Noon);
        var http = new StubHttp().Add("elprisenligenu.dk", NordicJson(Flat(24)));
        var provider = new ElectricityPriceProvider(http.Client, time);
        var zone = SpotPriceZone.Find("DK2");

        await provider.RefreshAsync(zone, default);
        await provider.RefreshAsync(zone, default);
        Assert.Single(http.Requests);

        time.Advance(TimeSpan.FromDays(1));
        await provider.RefreshAsync(zone, default);
        Assert.Equal(2, http.Requests.Count);
        Assert.EndsWith("2026/10-17_DK2.json", http.Requests[1]);
    }

    [Fact]
    public async Task Switching_to_a_zone_without_prices_drops_the_old_shape()
    {
        var http = new StubHttp()
            .Add("_DK1.json", NordicJson(Flat(24)))
            .Add("sahkotin.fi", "{\"prices\":[]}");
        var provider = new ElectricityPriceProvider(http.Client, Utc(Noon));

        await provider.RefreshAsync(SpotPriceZone.Find("DK1"), default);
        Assert.True(provider.HasLivePrices);

        // Finland has no prices yet: no live data, rather than Denmark's shape under Finland's name.
        await provider.RefreshAsync(SpotPriceZone.Find("FI"), default);
        Assert.False(provider.HasLivePrices);
        Assert.Null(provider.CurrentMultiplier());
    }

    [Fact]
    public async Task A_failed_fetch_keeps_yesterdays_shape_for_the_same_zone()
    {
        var time = Utc(Noon);
        var prices = Enumerable.Range(0, 24).Select(h => h == 12 ? 0.2 : 0.1).ToArray();
        var http = new StubHttp().Add("10-16_DK1.json", NordicJson(prices));
        var provider = new ElectricityPriceProvider(http.Client, time);
        var zone = SpotPriceZone.Find("DK1");
        await provider.RefreshAsync(zone, default);
        var before = provider.CurrentMultiplier();

        time.Advance(TimeSpan.FromDays(1)); // the 17th isn't stubbed, so this fetch fails with a 404
        await provider.RefreshAsync(zone, default);

        Assert.Equal(2, http.Requests.Count);
        Assert.True(provider.HasLivePrices);
        Assert.NotNull(before);
        Assert.NotNull(provider.CurrentMultiplier());
    }

    [Fact]
    public async Task A_failed_fetch_for_a_new_zone_leaves_no_live_prices()
    {
        var http = new StubHttp().Add("_NO1.json", NordicJson(Flat(24)));
        var provider = new ElectricityPriceProvider(http.Client, Utc(Noon));
        await provider.RefreshAsync(SpotPriceZone.Find("NO1"), default);

        await provider.RefreshAsync(SpotPriceZone.Find("DK1"), default); // 404

        Assert.False(provider.HasLivePrices);
        Assert.Null(provider.Zone);
        Assert.Null(provider.CurrentMultiplier());
    }

    [Fact]
    public async Task Malformed_json_leaves_no_live_prices()
    {
        var http = new StubHttp().Add("elprisenligenu.dk", "<html>maintenance</html>");
        var provider = new ElectricityPriceProvider(http.Client, Utc(Noon));

        await provider.RefreshAsync(SpotPriceZone.Find("DK1"), default);

        Assert.False(provider.HasLivePrices);
    }

    [Fact]
    public async Task No_zone_clears_live_prices()
    {
        var http = new StubHttp().Add("elprisenligenu.dk", NordicJson(Flat(24)));
        var provider = new ElectricityPriceProvider(http.Client, Utc(Noon));
        await provider.RefreshAsync(SpotPriceZone.Find("DK1"), default);

        await provider.RefreshAsync(null, default);

        Assert.False(provider.HasLivePrices);
        Assert.Null(provider.Zone);
        Assert.Null(provider.CurrentMultiplier());
    }

    private static ManualTime Utc(DateTimeOffset now) => new(now, TimeZoneInfo.Utc);

    private static double[] Flat(int count) => Enumerable.Repeat(0.1, count).ToArray();

    /// <summary>The hvakosterstrommen.no / elprisetjustnu.se / elprisenligenu.dk array, starting at midnight UTC.</summary>
    private static string NordicJson(double[] prices, TimeSpan? step = null)
    {
        var day = new DateTimeOffset(2026, 10, 16, 0, 0, 0, TimeSpan.Zero);
        var period = step ?? TimeSpan.FromHours(1);
        var sb = new StringBuilder("[");
        for (var i = 0; i < prices.Length; i++)
        {
            var start = day + period * i;
            sb.Append(i == 0 ? "" : ",").Append(string.Create(CultureInfo.InvariantCulture,
                $"{{\"EUR_per_kWh\":{prices[i]},\"EXR\":7.46,\"time_start\":\"{start:yyyy-MM-ddTHH:mm:sszzz}\",\"time_end\":\"{start + period:yyyy-MM-ddTHH:mm:sszzz}\"}}"));
        }
        return sb.Append(']').ToString();
    }

    /// <summary>Answers requests whose URL contains a registered fragment; anything else is a 404.</summary>
    private sealed class StubHttp : HttpMessageHandler
    {
        private readonly List<(string Fragment, string Body)> _routes = [];

        public List<string> Requests { get; } = [];

        public HttpClient Client => new(this, disposeHandler: false);

        public StubHttp Add(string urlFragment, string body)
        {
            _routes.Add((urlFragment, body));
            return this;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var url = request.RequestUri!.ToString();
            Requests.Add(url);
            var route = _routes.FirstOrDefault(r => url.Contains(r.Fragment, StringComparison.Ordinal));
            return Task.FromResult(route.Body is null
                ? new HttpResponseMessage(HttpStatusCode.NotFound)
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(route.Body, Encoding.UTF8, "application/json") });
        }
    }
}
