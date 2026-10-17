using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Otium.Core.Accounts;
using Otium.Core.Bandwidth;

namespace Otium.Tests;

/// <summary>
/// Behind a reverse proxy, the server must see each client's own address (one paid sharer per internet connection),
/// and must not let anyone but the proxy choose it.
/// </summary>
public sealed class ReverseProxyTests : IDisposable
{
    private const long MB = 1024 * 1024;
    private const string Proxy = "172.30.0.10";
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"otium-proxy-{Guid.NewGuid():N}.db");
    private readonly ManualTime _time = new(new DateTimeOffset(2026, 10, 15, 12, 0, 0, TimeSpan.Zero));
    private WebApplicationFactory<Program>? _factory;

    /// <summary>Starts the server with every connection arriving from <paramref name="connectionFrom"/>.</summary>
    private void Start(bool reverseProxy, string connectionFrom = Proxy)
    {
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Otium", $"Data Source={_dbPath};Pooling=False");
            builder.UseSetting("Custody:Enabled", "true");
            builder.UseSetting("DisableBackgroundJobs", "true");
            builder.UseSetting("Earnings:PoolWallets:0", TestAddresses.Generate());
            builder.UseSetting("Bandwidth:Enabled", "true");
            builder.UseSetting("Bandwidth:UsdPerGb", "1");
            builder.UseSetting("ReverseProxy:Enabled", reverseProxy ? "true" : "false");
            builder.UseSetting("ReverseProxy:KnownProxies:0", Proxy);
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(_time);
                services.AddSingleton<IStartupFilter>(new ConnectionFrom(IPAddress.Parse(connectionFrom)));
            });
        });
    }

    /// <summary>The test server has no real connections; give them all a remote address, ahead of the app's middleware.</summary>
    private sealed class ConnectionFrom(IPAddress address) : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use((context, nextMiddleware) =>
            {
                context.Connection.RemoteIpAddress = address;
                return nextMiddleware(context);
            });
            next(app);
        };
    }

    /// <summary>Registers a computer whose requests carry the given X-Forwarded-For, and returns its first report's status.</summary>
    private async Task<string> RegisterAndReport(string forwardedFor)
    {
        using var http = _factory!.CreateClient();
        http.DefaultRequestHeaders.Add("X-Forwarded-For", forwardedFor);
        var (id, secret) = (Guid.NewGuid().ToString("N"), Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32)));
        (await http.PostAsJsonAsync("/api/v1/devices", new RegisterDeviceRequest(id, secret, null, null))).EnsureSuccessStatusCode();

        var report = new BandwidthReport(1, _time.GetUtcNow() - TimeSpan.FromMinutes(5), _time.GetUtcNow(), MB, 300, "simulated");
        return (await new AccountClient(http).ReportBandwidthAsync(id, secret, report, CancellationToken.None)).Status;
    }

    [Fact]
    public async Task Enabled_the_client_address_comes_from_the_proxy_header()
    {
        Start(reverseProxy: true);

        // Two homes behind the same proxy are both paid.
        Assert.Equal(BandwidthReportStatuses.Credited, await RegisterAndReport("203.0.113.1"));
        Assert.Equal(BandwidthReportStatuses.Credited, await RegisterAndReport("203.0.113.2"));
        // A second computer in the first home is not.
        Assert.Equal(BandwidthReportStatuses.SharedConnection, await RegisterAndReport("203.0.113.1"));
    }

    [Fact]
    public async Task Disabled_the_header_is_ignored()
    {
        Start(reverseProxy: false);

        Assert.Equal(BandwidthReportStatuses.Credited, await RegisterAndReport("203.0.113.1"));
        Assert.Equal(BandwidthReportStatuses.SharedConnection, await RegisterAndReport("203.0.113.2"));
    }

    [Fact]
    public async Task Enabled_the_header_is_ignored_unless_it_comes_from_the_proxy()
    {
        // A client reaching the server directly can't pick its address.
        Start(reverseProxy: true, connectionFrom: "198.51.100.7");

        Assert.Equal(BandwidthReportStatuses.Credited, await RegisterAndReport("203.0.113.1"));
        Assert.Equal(BandwidthReportStatuses.SharedConnection, await RegisterAndReport("203.0.113.2"));
    }

    public void Dispose()
    {
        _factory?.Dispose();
        try
        {
            File.Delete(_dbPath);
        }
        catch (IOException)
        {
        }
    }
}
