using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Otium.Core.Accounts;
using Otium.Core.Pools;
using Otium.Server.Accounts;

namespace Otium.Tests;

/// <summary>
/// The invite rewards can't be collected by one person inviting themselves: not from their own internet connection, not
/// by switching to the inviter's wallet afterwards, and not with a few minutes of mining (see the unMineable test in
/// NonCustodialServerTests). What invites earned is shown to the inviter, and friends' apps can't inflate it.
/// </summary>
public sealed class ReferralAbuseTests : IDisposable
{
    private const string Proxy = "172.30.0.10";
    private const string Inviter = "USDT:TR7NHqjeKQxGTCi8q8ZY4pL8otSzgjLj6t";
    private const string Friend = "BNB:0x5aAeb6053F3E94C9b9A09f33669435E7Ef1BeAed";
    private const string OtherFriend = "BNB:0xfB6916095ca1df60bB79Ce92cE3Ea74c37c5d359";
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"otium-referral-{Guid.NewGuid():N}.db");
    private readonly ManualTime _time = new(new DateTimeOffset(2026, 10, 20, 12, 0, 0, TimeSpan.Zero));
    private readonly WebApplicationFactory<Program> _factory;

    public ReferralAbuseTests()
    {
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Otium", $"Data Source={_dbPath};Pooling=False");
            builder.UseSetting("DisableBackgroundJobs", "true");
            builder.UseSetting("ReverseProxy:Enabled", "true");
            builder.UseSetting("ReverseProxy:KnownProxies:0", Proxy);
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IUnmineableApi>();
                services.AddSingleton<IUnmineableApi>(new NonCustodialServerTests.FakeUnmineable());
                services.RemoveAll<IMoneroPoolApi>();
                services.AddSingleton<IMoneroPoolApi>(new GpuPolicyTests.FakePool());
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

    private sealed record Computer(string Id, string Secret);

    private HttpClient From(string address)
    {
        var http = _factory.CreateClient();
        http.DefaultRequestHeaders.Add("X-Forwarded-For", address);
        return http;
    }

    private static Computer NewComputer() => new(Guid.NewGuid().ToString("N"), Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32)));

    private async Task<HttpResponseMessage> Register(string from, Computer computer, string payout, string? code = null) =>
        await From(from).PostAsJsonAsync("/api/v1/devices", new RegisterDeviceRequest(computer.Id, computer.Secret, payout, code));

    private async Task<AccountSummary> RegisterOk(string from, Computer computer, string payout, string? code = null)
    {
        using var response = await Register(from, computer, payout, code);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AccountSummary>())!;
    }

    [Fact]
    public async Task An_invite_from_the_inviters_own_internet_connection_doesnt_count()
    {
        var inviter = await RegisterOk("203.0.113.50", NewComputer(), Inviter);

        // A second computer and a second wallet, on the same connection: the same person, most likely.
        var self = await RegisterOk("203.0.113.50", NewComputer(), Friend, inviter.Code);
        Assert.Equal("Invites only count for friends on a different internet connection.", self.ReferralCodeError);
        Assert.Null(self.ReferrerWallet);
        Assert.Null(self.FeeFreeUntil);

        // IPv6 counts per /64: a new address in the inviter's network is the same connection.
        var inviter6 = await RegisterOk("2001:db8:7:8::1", NewComputer(), "USDT:TJCnKsPa7y5okkXvQAidZBzqx3QyQ6sxMW");
        var self6 = await RegisterOk("2001:db8:7:8:ffff::2", NewComputer(), OtherFriend, inviter6.Code);
        Assert.NotNull(self6.ReferralCodeError);

        // A friend elsewhere is welcome.
        var friend = await RegisterOk("198.51.100.20", NewComputer(), OtherFriend, inviter.Code);
        Assert.Null(friend.ReferralCodeError);
        Assert.Equal(Inviter, friend.ReferrerWallet);
        Assert.NotNull(friend.FeeFreeUntil);
    }

    [Fact]
    public async Task The_inviters_connection_counts_for_30_days_after_their_last_check_in()
    {
        var inviterComputer = NewComputer();
        var inviter = await RegisterOk("203.0.113.60", inviterComputer, Inviter);
        // The inviter's laptop checks in from a café: that connection is theirs too, for a while.
        var check = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/devices/{inviterComputer.Id}/account");
        check.Headers.Add(AccountClient.SecretHeader, inviterComputer.Secret);
        (await From("192.0.2.77").SendAsync(check)).EnsureSuccessStatusCode();

        Assert.NotNull((await RegisterOk("192.0.2.77", NewComputer(), Friend, inviter.Code)).ReferralCodeError);

        _time.Advance(AccountService.ConnectionMemory + TimeSpan.FromHours(1));
        Assert.Null((await RegisterOk("192.0.2.77", NewComputer(), OtherFriend, inviter.Code)).ReferralCodeError);
    }

    [Fact]
    public async Task A_friend_cant_switch_to_the_inviters_wallet_nor_the_inviter_to_a_friends()
    {
        var inviterComputer = NewComputer();
        var inviter = await RegisterOk("203.0.113.70", inviterComputer, Inviter);
        var friendComputer = NewComputer();
        await RegisterOk("198.51.100.30", friendComputer, Friend, inviter.Code);

        using (var refused = await Register("198.51.100.30", friendComputer, Inviter))
        {
            Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
            Assert.Contains("payout address of the friend who invited you", await refused.Content.ReadAsStringAsync());
        }
        using (var refused = await Register("203.0.113.70", inviterComputer, Friend))
        {
            Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
            Assert.Contains("payout address of a friend you invited", await refused.Content.ReadAsStringAsync());
        }
        // Any other address of their own is fine.
        await RegisterOk("198.51.100.30", friendComputer, OtherFriend);
    }

    [Fact]
    public async Task What_friends_mined_for_the_inviter_is_summed_and_cant_be_inflated()
    {
        var inviterComputer = NewComputer();
        var inviter = await RegisterOk("203.0.113.80", inviterComputer, Inviter);
        var friendA = NewComputer();
        var friendB = NewComputer();
        await RegisterOk("198.51.100.40", friendA, Friend, inviter.Code);
        await RegisterOk("198.51.100.41", friendB, OtherFriend, inviter.Code);
        var stranger = NewComputer();
        await RegisterOk("198.51.100.42", stranger, "USDT:TJCnKsPa7y5okkXvQAidZBzqx3QyQ6sxMW");

        async Task Report(Computer c, string from, double usd) =>
            await new AccountClient(From(from)).ReportReferralShareAsync(c.Id, c.Secret, new ReferralShareReport(usd), CancellationToken.None);
        async Task<double> Earned() =>
            (await new AccountClient(From("203.0.113.80")).GetSummaryAsync(inviterComputer.Id, inviterComputer.Secret, CancellationToken.None)).ReferralEarnedUsd;

        _time.Advance(TimeSpan.FromDays(4));
        await Report(friendA, "198.51.100.40", 0.12);
        await Report(friendB, "198.51.100.41", 0.08);
        Assert.Equal(0.20, await Earned(), precision: 6);

        await Report(friendA, "198.51.100.40", 0.05); // an older, lower total doesn't take anything away
        Assert.Equal(0.20, await Earned(), precision: 6);

        // Nobody's computer mines more than $0.50 a day for its inviter: bigger claims are cut to that.
        await Report(friendB, "198.51.100.41", 1_000);
        Assert.Equal(0.12 + 4 * AccountService.MaxReferralShareUsdPerDay, await Earned(), precision: 6);

        await Report(stranger, "198.51.100.42", 5); // not invited by anyone: ignored
        Assert.Equal(0.12 + 4 * AccountService.MaxReferralShareUsdPerDay, await Earned(), precision: 6);
    }

    public void Dispose()
    {
        _factory.Dispose();
        foreach (var file in new[] { _dbPath, _dbPath + "-wal", _dbPath + "-shm" })
            File.Delete(file);
    }
}
