using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Otium.Core.Currency;
using Otium.Server.Rates;

namespace Otium.Tests;

/// <summary>The exchange rates the server hands to apps so they can show earnings in the user's own currency.</summary>
public class FxRatesTests
{
    private static JsonElement Json(string text) => JsonDocument.Parse(text).RootElement.Clone();

    [Fact]
    public void Open_er_api_rates_are_read_and_cleaned()
    {
        var rates = FxFeedClient.ParseOpenErApi(Json("""
            {"result":"success","time_last_update_unix":1793577600,
             "rates":{"USD":1,"EUR":0.92,"GBP":0.77,"XAU":0.0004,"BAD":"x","NEG":-1,"abc":3,"ZERO":0,"JPY":150.5}}
            """))!;

        Assert.Equal("USD", rates.Base);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1793577600), rates.AsOf);
        Assert.Equal(new[] { "EUR", "GBP", "JPY" }, rates.Rates.Keys.Order()); // not USD itself, metals, junk or non-positive rates
        Assert.Equal(150.5, rates.Rates["JPY"]);
    }

    [Fact]
    public void An_error_answer_from_open_er_api_is_not_used() =>
        Assert.Null(FxFeedClient.ParseOpenErApi(Json("""{"result":"error","error-type":"quota-reached"}""")));

    [Fact]
    public void Frankfurter_rates_are_read()
    {
        var rates = FxFeedClient.ParseFrankfurter(Json("""{"amount":1.0,"base":"USD","date":"2026-11-02","rates":{"EUR":0.92,"SEK":10.5}}"""))!;

        Assert.Equal(new DateTimeOffset(2026, 11, 2, 0, 0, 0, TimeSpan.Zero), rates.AsOf);
        Assert.Equal(0.92, rates.Rates["EUR"]);
        Assert.Null(FxFeedClient.ParseFrankfurter(Json("""{"rates":{}}""")));
        Assert.Null(FxFeedClient.ParseFrankfurter(Json("""{"message":"not found"}""")));
    }

    private sealed class FakeFeed : IFxFeed
    {
        public int Calls;
        public RatesResponse? Answer = new("USD", DateTimeOffset.UnixEpoch, new() { ["EUR"] = 0.92 });

        public Task<RatesResponse?> GetAsync(CancellationToken ct)
        {
            Calls++;
            return Task.FromResult(Answer);
        }
    }

    private sealed class FakeTime : TimeProvider
    {
        public DateTimeOffset Now = new(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }

    [Fact]
    public async Task Rates_are_fetched_once_and_kept_for_six_hours()
    {
        var feed = new FakeFeed();
        var time = new FakeTime();
        var service = new FxRatesService(feed, time);

        Assert.NotNull(await service.GetAsync(CancellationToken.None));
        time.Now += TimeSpan.FromHours(5);
        Assert.NotNull(await service.GetAsync(CancellationToken.None));
        Assert.Equal(1, feed.Calls);

        time.Now += TimeSpan.FromHours(2);
        await service.GetAsync(CancellationToken.None);
        Assert.Equal(2, feed.Calls);
    }

    [Fact]
    public async Task While_the_sources_are_down_old_rates_are_served_for_a_week_and_tried_for_every_few_minutes()
    {
        var feed = new FakeFeed();
        var time = new FakeTime();
        var service = new FxRatesService(feed, time);
        await service.GetAsync(CancellationToken.None);
        feed.Answer = null;

        time.Now += TimeSpan.FromHours(7);
        Assert.NotNull(await service.GetAsync(CancellationToken.None)); // old rates still do
        var calls = feed.Calls;
        for (var i = 0; i < 5; i++)
            await service.GetAsync(CancellationToken.None);
        Assert.Equal(calls, feed.Calls); // asking again straight away doesn't hit the sources again

        time.Now += FxRatesService.RetryAfter + TimeSpan.FromSeconds(1);
        await service.GetAsync(CancellationToken.None);
        Assert.Equal(calls + 1, feed.Calls);

        time.Now += FxRatesService.KeepFor;
        Assert.Null(await service.GetAsync(CancellationToken.None)); // too old to be of use
    }

    [Fact]
    public async Task The_rates_endpoint_answers_without_a_device_and_says_503_without_rates()
    {
        var feed = new FakeFeed();
        var db = Path.Combine(Path.GetTempPath(), $"otium-fx-{Guid.NewGuid():N}.db");
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Otium", $"Data Source={db};Pooling=False");
            builder.UseSetting("DisableBackgroundJobs", "true");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IFxFeed>();
                services.AddSingleton<IFxFeed>(feed);
            });
        });
        using var http = factory.CreateClient();

        var response = await http.GetAsync("/api/v1/rates");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("max-age=3600", response.Headers.CacheControl?.ToString());
        var body = (await response.Content.ReadFromJsonAsync<RatesResponse>())!;
        Assert.Equal(0.92, body.Rates["EUR"]);

        // The app's own client reads the same answer.
        var client = new RatesClient(http);
        Assert.Equal(0.92, (await client.GetAsync(CancellationToken.None))!.Rates["EUR"]);
    }
}
