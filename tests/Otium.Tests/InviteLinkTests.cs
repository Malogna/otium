using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Otium.Core.Accounts;

namespace Otium.Tests;

/// <summary>
/// Invite links that apply themselves: a friend who opens otium.lol/r/CODE and installs Otium on the same internet
/// connection gets the code filled in, without typing it.
/// </summary>
public sealed class InviteLinkTests : IDisposable
{
    private const string Proxy = "172.30.0.10";
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"otium-invite-{Guid.NewGuid():N}.db");
    private readonly ManualTime _time = new(new DateTimeOffset(2026, 10, 19, 20, 0, 0, TimeSpan.Zero));
    private readonly WebApplicationFactory<Program> _factory;

    public InviteLinkTests()
    {
        // Behind the reverse proxy, as on otium.lol: each request's own address comes from X-Forwarded-For.
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

    private async Task<string> RegisterReferrer()
    {
        using var http = From("198.51.100.1");
        var (id, secret) = (Guid.NewGuid().ToString("N"), Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32)));
        var response = await http.PostAsJsonAsync("/api/v1/devices", new RegisterDeviceRequest(id, secret, null, null));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AccountSummary>())!.Code;
    }

    private Task<string?> PendingFor(string address) => new AccountClient(From(address)).GetInviteLinkCodeAsync(CancellationToken.None);

    [Fact]
    public async Task Opening_an_invite_link_fills_in_the_code_for_Otium_installed_on_the_same_connection()
    {
        var code = await RegisterReferrer();
        Assert.Null(await PendingFor("203.0.113.7")); // nothing opened yet

        (await From("203.0.113.7").GetAsync($"/r/{code.ToLowerInvariant()}")).EnsureSuccessStatusCode();
        Assert.Equal(code, await PendingFor("203.0.113.7"));
        Assert.Null(await PendingFor("203.0.113.8")); // another connection

        // IPv6: the same /64 network, although the device has picked a new address since.
        (await From("2001:db8:1:2::1").GetAsync($"/r/{code}")).EnsureSuccessStatusCode();
        Assert.Equal(code, await PendingFor("2001:db8:1:2:abcd:ef01:2345:6789"));
        Assert.Null(await PendingFor("2001:db8:1:3::1"));

        // Kept for a week, as long as a new install can use a code.
        _time.Advance(TimeSpan.FromDays(7) - TimeSpan.FromMinutes(1));
        Assert.Equal(code, await PendingFor("203.0.113.7"));
        _time.Advance(TimeSpan.FromMinutes(2));
        Assert.Null(await PendingFor("203.0.113.7"));
    }

    [Fact]
    public async Task Only_opening_the_page_counts_and_the_first_link_opened_wins()
    {
        var friend = await RegisterReferrer();
        var stranger = await RegisterReferrer();

        async Task Open(string code, string? dest, string? mode)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"/r/{code}");
            if (dest is not null)
                request.Headers.Add("Sec-Fetch-Dest", dest);
            if (mode is not null)
                request.Headers.Add("Sec-Fetch-Mode", mode);
            (await From("203.0.113.20").SendAsync(request)).EnsureSuccessStatusCode();
        }

        // Another site loading the link as an image, a script or a fetch: not someone opening it.
        await Open(stranger, "image", "no-cors");
        await Open(stranger, "empty", "cors");
        await Open(stranger, null, "no-cors");
        Assert.Null(await PendingFor("203.0.113.20"));

        await Open(friend, "document", "navigate");
        Assert.Equal(friend, await PendingFor("203.0.113.20"));

        // A link opened later on the same connection (a university's, say) doesn't replace it.
        await Open(stranger, "document", "navigate");
        await Open(stranger, null, null);
        Assert.Equal(friend, await PendingFor("203.0.113.20"));

        // After the week, the next link opened counts.
        _time.Advance(TimeSpan.FromDays(8));
        await Open(stranger, "document", "navigate");
        Assert.Equal(stranger, await PendingFor("203.0.113.20"));
    }

    [Fact]
    public async Task Unknown_codes_are_not_remembered_and_addresses_are_kept_only_as_keyed_hashes()
    {
        (await From("203.0.113.9").GetAsync("/r/ZZZZZZZ")).EnsureSuccessStatusCode(); // no such code
        Assert.Null(await PendingFor("203.0.113.9"));

        var code = await RegisterReferrer();
        (await From("203.0.113.9").GetAsync($"/r/{code}")).EnsureSuccessStatusCode();
        Assert.Equal(code, await PendingFor("203.0.113.9"));

        _factory.Dispose(); // close the database, so its files can be read
        var stored = string.Concat(new[] { _dbPath, _dbPath + "-wal" }.Where(File.Exists).Select(f => Encoding.Latin1.GetString(File.ReadAllBytes(f))));
        Assert.Contains(code, stored);
        Assert.DoesNotContain("203.0.113.9", stored);
    }

    [Fact]
    public async Task The_invite_page_says_the_invite_is_filled_in_for_you()
    {
        var code = await RegisterReferrer();
        var html = await From("203.0.113.10").GetStringAsync($"/r/{code}");
        Assert.Contains("the invite is filled in for you", html);
        Assert.Contains(code, html); // still shown, in case setup doesn't fill it in
    }

    public void Dispose()
    {
        _factory.Dispose();
        foreach (var file in new[] { _dbPath, _dbPath + "-wal", _dbPath + "-shm" })
            File.Delete(file);
    }
}
