using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Otium.Core.Accounts;
using Otium.Server;

namespace Otium.Tests;

/// <summary>
/// Limits per internet connection on what anyone can ask for and costs Otium something (new accounts, code emails,
/// problem reports), with IPv6 counted per /64 network.
/// </summary>
public sealed class ConnectionQuotaTests : IDisposable
{
    private const string Proxy = "172.30.0.10";
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"otium-quota-{Guid.NewGuid():N}.db");
    private readonly ManualTime _time = new(new DateTimeOffset(2026, 10, 20, 9, 0, 0, TimeSpan.Zero));
    private readonly WebApplicationFactory<Program> _factory;

    public ConnectionQuotaTests()
    {
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Otium", $"Data Source={_dbPath};Pooling=False");
            builder.UseSetting("DisableBackgroundJobs", "true");
            builder.UseSetting("ReverseProxy:Enabled", "true");
            builder.UseSetting("ReverseProxy:KnownProxies:0", Proxy);
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(_time);
                services.AddSingleton<IStartupFilter>(new ConnectionFromProxy());
            });
        });
    }

    private sealed class ConnectionFromProxy : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use((context, nextMiddleware) =>
            {
                context.Connection.RemoteIpAddress = IPAddress.Parse(Proxy);
                return nextMiddleware(context);
            });
            next(app);
        };
    }

    private HttpClient From(string address)
    {
        var http = _factory.CreateClient();
        http.DefaultRequestHeaders.Add("X-Forwarded-For", address);
        return http;
    }

    private static (string Id, string Secret) NewDevice() => (Guid.NewGuid().ToString("N"), Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32)));

    private async Task<HttpStatusCode> Register(string from, (string Id, string Secret) device)
    {
        using var response = await From(from).PostAsJsonAsync("/api/v1/devices", new RegisterDeviceRequest(device.Id, device.Secret, null, null));
        return response.StatusCode;
    }

    [Fact]
    public async Task New_installs_are_limited_per_connection_and_ipv6_counts_per_64()
    {
        var first = NewDevice();
        Assert.Equal(HttpStatusCode.OK, await Register("2001:db8:5:6::1", first));
        // The rest of the day's allowance, each from a different address in the same /64.
        for (var i = 2; i <= ConnectionQuotas.NewDevicesPerDay; i++)
            Assert.Equal(HttpStatusCode.OK, await Register($"2001:db8:5:6::{i:x}", NewDevice()));
        Assert.Equal(HttpStatusCode.TooManyRequests, await Register("2001:db8:5:6:ffff::9", NewDevice()));

        Assert.Equal(HttpStatusCode.OK, await Register("2001:db8:5:6::1", first)); // a known computer re-registers freely
        Assert.Equal(HttpStatusCode.OK, await Register("2001:db8:5:7::1", NewDevice())); // another network
        Assert.Equal(HttpStatusCode.OK, await Register("203.0.113.20", NewDevice()));

        _time.Advance(TimeSpan.FromDays(1));
        Assert.Equal(HttpStatusCode.OK, await Register("2001:db8:5:6::1", NewDevice())); // a new day
    }

    [Fact]
    public async Task Code_emails_are_limited_per_connection_across_devices()
    {
        // Devices are free, so the per-device limit alone wouldn't stop someone mailing codes to strangers.
        for (var i = 0; i < ConnectionQuotas.CodeEmailsPerHour; i++)
        {
            var device = NewDevice();
            await Register("198.51.100.30", device);
            using var ok = await From("198.51.100.30").PostAsJsonAsync($"/api/v1/devices/{device.Id}/email/code", new EmailCodeRequest($"person{i}@example.com"),
                CancellationToken.None, device.Secret);
            Assert.Equal(HttpStatusCode.NoContent, ok.StatusCode);
        }
        var last = NewDevice();
        await Register("198.51.100.30", last);
        using var refused = await From("198.51.100.30").PostAsJsonAsync($"/api/v1/devices/{last.Id}/email/code", new EmailCodeRequest("victim@example.com"),
            CancellationToken.None, last.Secret);
        Assert.Equal(HttpStatusCode.TooManyRequests, refused.StatusCode);
    }

    [Theory]
    [InlineData("203.0.113.5", "203.0.113.5")]
    [InlineData("::ffff:203.0.113.5", "203.0.113.5")]
    [InlineData("2001:db8:1:2:aaaa:bbbb:cccc:dddd", "20010db800010002/64")]
    [InlineData("2001:db8:1:2::1", "20010db800010002/64")]
    public void A_connection_is_an_ipv4_address_or_an_ipv6_64(string address, string key) =>
        Assert.Equal(key, ClientNetwork.Of(IPAddress.Parse(address)));

    [Fact]
    public void Quotas_reset_after_their_window_and_skip_unknown_addresses()
    {
        var quotas = new ConnectionQuotas(_time);
        var from = IPAddress.Parse("203.0.113.40");
        Assert.True(quotas.TryTake("x", from, 2, TimeSpan.FromHours(1)));
        Assert.True(quotas.TryTake("x", from, 2, TimeSpan.FromHours(1)));
        Assert.False(quotas.TryTake("x", from, 2, TimeSpan.FromHours(1)));
        Assert.True(quotas.TryTake("y", from, 2, TimeSpan.FromHours(1))); // another allowance
        _time.Advance(TimeSpan.FromHours(1));
        Assert.True(quotas.TryTake("x", from, 2, TimeSpan.FromHours(1)));
        Assert.True(quotas.TryTake("x", null, 0, TimeSpan.FromHours(1))); // no connection information: not limited
    }

    public void Dispose()
    {
        _factory.Dispose();
        foreach (var file in new[] { _dbPath, _dbPath + "-wal", _dbPath + "-shm" })
            File.Delete(file);
    }
}

internal static class SecretRequestExtensions
{
    /// <summary>POSTs JSON with the device secret header the device endpoints expect.</summary>
    public static Task<HttpResponseMessage> PostAsJsonAsync<T>(this HttpClient http, string url, T body, CancellationToken ct, string secret)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-Device-Secret", secret);
        return http.SendAsync(request, ct);
    }
}
