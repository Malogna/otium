using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Otium.Core.Accounts;
using Otium.Core.Pools;
using Otium.Core.Switching;
using Otium.Core.Wallets;
using Otium.Server.Gpu;

namespace Otium.Tests;

/// <summary>The server with custody off (the default): accounts and referrals only, no balances or cash-outs.</summary>
public sealed class NonCustodialServerTests : IDisposable
{
    private const string AdminToken = "test-admin-token-0123456789";
    private const string Usdt = "USDT:TR7NHqjeKQxGTCi8q8ZY4pL8otSzgjLj6t";
    private const string OtiumUsdt = "USDT:TB2xNFAjGarYPykmt1dZQz8hJb2Rhry4wJ";
    private const string BnbMixedCase = "BNB:0x5aAeb6053F3E94C9b9A09f33669435E7Ef1BeAed";

    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"otium-nc-{Guid.NewGuid():N}.db");
    private readonly ManualTime _time = new(new DateTimeOffset(2026, 10, 17, 12, 0, 0, TimeSpan.Zero));
    private readonly FakeUnmineable _unmineable = new();
    private readonly GpuPolicyTests.FakeFeed _feed = new();
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _http;

    public NonCustodialServerTests()
    {
        _feed.Coins[469] = new CoinRevenue(3.4e-14, false, _time.GetUtcNow());
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Otium", $"Data Source={_dbPath};Pooling=False");
            builder.UseSetting("DisableBackgroundJobs", "true");
            builder.UseSetting("Admin:Token", AdminToken);
            builder.UseSetting("Unmineable:OtiumTarget", OtiumUsdt);
            builder.UseSetting("Fees:FeePercent", "3");
            // Configured, but a non-custodial server never routes anyone to Otium's Kryptex account.
            builder.UseSetting("Kryptex:Enabled", "true");
            builder.UseSetting("Kryptex:Account", "otium");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IMoneroPoolApi>();
                services.AddSingleton<IMoneroPoolApi>(new GpuPolicyTests.FakePool());
                services.RemoveAll<IUnmineableApi>();
                services.AddSingleton<IUnmineableApi>(_unmineable);
                services.RemoveAll<IKryptexApi>();
                services.AddSingleton<IKryptexApi>(new GpuPolicyTests.FakeKryptex());
                services.RemoveAll<IPearlPipFeed>();
                services.AddSingleton<IPearlPipFeed>(new GpuPolicyTests.FakePip());
                services.RemoveAll<IProfitFeed>();
                services.AddSingleton<IProfitFeed>(_feed);
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(_time);
            });
        });
        _http = _factory.CreateClient();
    }

    private async Task<(string Device, string Secret, HttpResponseMessage Response)> Register(string? payout, string? code = null)
    {
        var secret = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
        var device = Guid.NewGuid().ToString("N");
        var response = await _http.PostAsJsonAsync("/api/v1/devices", new RegisterDeviceRequest(device, secret, payout, code));
        return (device, secret, response);
    }

    private async Task<AccountSummary> RegisterOk(string? payout, string? code = null)
    {
        var (_, _, response) = await Register(payout, code);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<AccountSummary>())!;
    }

    [Fact]
    public async Task Accounts_have_no_balance_pool_wallet_or_cash_outs()
    {
        var summary = await RegisterOk(Usdt);

        Assert.False(summary.Custodial);
        Assert.Equal(new FeeTerms(3, 0.25, 180, 30, 30, 90), summary.Fees); // the operator's fee, default invite terms
        Assert.Null(summary.PoolWallet);
        Assert.Null(summary.CpuPool);
        Assert.Null(summary.Bandwidth);
        Assert.Empty(summary.Balance.Methods ?? []);

        // An app that hasn't chosen an address yet can still register (and redeem an invite in its first week).
        var later = await RegisterOk(null);
        Assert.Null(later.PoolWallet);
    }

    [Fact]
    public async Task Custodial_routes_do_not_exist()
    {
        var (device, secret, _) = await Register(Usdt);
        var cashout = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/devices/{device}/cashouts")
        {
            Content = JsonContent.Create(new CashoutRequest(CashoutMethods.Bitcoin, "bc1qw508d6qejxtdg4y5r3zarvary0c5xw7kv8f3t4")),
        };
        cashout.Headers.Add(AccountClient.SecretHeader, secret);
        Assert.Equal(HttpStatusCode.NotFound, (await _http.SendAsync(cashout)).StatusCode);

        var admin = new HttpRequestMessage(HttpMethod.Get, "/admin/cashouts");
        admin.Headers.Add("X-Admin-Token", AdminToken);
        Assert.Equal(HttpStatusCode.NotFound, (await _http.SendAsync(admin)).StatusCode);
    }

    [Theory]
    [InlineData("USDT:0x5aAeb6053F3E94C9b9A09f33669435E7Ef1BeAed", "TRON")]
    [InlineData("DOGE:DH5yaieqoZN36fDVciNyRueRGvGLR3mr7L", "can't pay out in DOGE")]
    public async Task Bad_payout_targets_are_refused_with_a_reason(string payout, string reason)
    {
        var (_, _, response) = await Register(payout);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(reason, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Referrers_get_their_payout_target_and_verification_comes_from_unmineable()
    {
        var referrer = await Register(Usdt);
        var referrerSummary = (await referrer.Response.Content.ReadFromJsonAsync<AccountSummary>())!;
        var friend = await RegisterOk(BnbMixedCase, referrerSummary.Code);

        Assert.Null(friend.ReferralCodeError);
        Assert.Equal(Usdt, friend.ReferrerWallet);

        // unMineable has rewarded the friend's address in a daily accounting: that's real mining.
        _unmineable.Rewarded["BNB:0x5aaeb6053f3e94c9b9a09f33669435e7ef1beaed"] = 0.0002m;
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/devices/{referrer.Device}/account");
        request.Headers.Add(AccountClient.SecretHeader, referrer.Secret);
        var refreshed = (await (await _http.SendAsync(request)).Content.ReadFromJsonAsync<AccountSummary>())!;

        Assert.Equal(1, refreshed.InvitedCount);
        Assert.Equal(1, refreshed.VerifiedCount);
        Assert.NotNull(refreshed.FeeFreeUntil);
    }

    [Fact]
    public async Task The_same_address_typed_differently_can_not_invite_itself()
    {
        var referrer = await RegisterOk(BnbMixedCase);
        var self = await RegisterOk("bnb:0x5aaeb6053f3e94c9b9a09f33669435e7ef1beaed", referrer.Code);
        Assert.Contains("your own wallet", self.ReferralCodeError);
        Assert.Null(self.ReferrerWallet);
    }

    [Fact]
    public async Task Gpu_policy_offers_unmineable_instead_of_kryptex()
    {
        var policy = await new AccountClient(_http).GetGpuPolicyAsync(CancellationToken.None);

        var pearl = policy.For("pearlhash")!;
        var route = policy.Route(pearl.Route)!;
        Assert.Equal(GpuRoutes.Unmineable, route.Kind);
        Assert.Equal("pearlpow.unmineable.com", route.Host);
        Assert.True(route.Tls);
        Assert.Null(route.User);
        Assert.Equal(GpuTiers.Primary, pearl.Tier);
        Assert.Equal(3.4e-14, pearl.UsdPerHashPerSecondDay);
        Assert.Equal(0.75, pearl.PoolFeePercent);
        Assert.DoesNotContain(policy.Routes, r => r.Kind == GpuRoutes.Kryptex);

        // No feed for Xelis or FishHash in this test: they're listed but switched off until their data is back.
        Assert.Equal(GpuTiers.Disabled, policy.For("xelishash")!.Tier);

        // Quantus would need a second (Quantus) wallet; everything is paid to the one payout address, so it isn't offered.
        Assert.Null(policy.For("quantus"));
        Assert.DoesNotContain(policy.Routes, r => r.Kind == GpuRoutes.KryptexAddress);
        Assert.Null(pearl.OnlyGpus); // AMD cards try Pearl too (failover if unMineable rejects them); the fork isn't active
    }

    [Fact]
    public async Task Admin_summary_shows_otiums_unmineable_earnings()
    {
        await RegisterOk(Usdt);
        await RegisterOk(null);
        var request = new HttpRequestMessage(HttpMethod.Get, "/admin/summary");
        request.Headers.Add("X-Admin-Token", AdminToken);
        using var doc = JsonDocument.Parse(await (await _http.SendAsync(request)).Content.ReadAsStringAsync());
        var root = doc.RootElement;

        Assert.False(root.GetProperty("custodial").GetBoolean());
        var otium = root.GetProperty("otium");
        Assert.Equal("ztjq-jr7s", otium.GetProperty("referralCode").GetString());
        Assert.Equal(3, otium.GetProperty("referredMiners").GetInt32());
        Assert.Equal(0.5m, otium.GetProperty("referralBalance").GetDecimal());
        var coins = root.GetProperty("accountsByCoin").EnumerateArray().ToDictionary(c => c.GetProperty("coin").GetString()!, c => c.GetProperty("accounts").GetInt64());
        Assert.Equal(1, coins["USDT"]);
        Assert.Equal(1, coins["none"]);

        // Activity: both new accounts and computers today, in a 14-day series ending today.
        var activity = root.GetProperty("activity");
        Assert.Equal(2, activity.GetProperty("accounts").GetInt64());
        Assert.Equal(2, activity.GetProperty("accountsNew24h").GetInt64());
        Assert.Equal(2, activity.GetProperty("devices").GetInt64());
        Assert.Equal(2, activity.GetProperty("devicesActiveNow").GetInt64());
        var days = activity.GetProperty("newDevicesPerDay").EnumerateArray().ToList();
        Assert.Equal(14, days.Count);
        Assert.Equal(2, days[^1].GetProperty("devices").GetInt64());
        Assert.Equal(0, days[0].GetProperty("devices").GetInt64());
        Assert.Equal(0, root.GetProperty("poolPayouts").GetProperty("paidAccounts").GetInt64());
        Assert.Equal(0, root.GetProperty("analytics").GetProperty("devices").GetInt32());
    }

    public void Dispose()
    {
        _http.Dispose();
        _factory.Dispose();
        try
        {
            File.Delete(_dbPath);
        }
        catch (IOException)
        {
        }
    }

    [Fact]
    public async Task Problem_reports_are_stored_with_a_reference_and_limited_per_day()
    {
        var (device, secret, response) = await Register(Usdt);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var client = new AccountClient(_http);

        var sent = await client.SendProblemReportAsync(device, secret, new ProblemReport("0.1.0-rc.1", "Windows 11 X64", "GPU never starts", "XMRig: mining"), CancellationToken.None);
        Assert.Matches("^[A-HJKMNP-Z2-9]{8}$", sent.Reference);

        var admin = new HttpRequestMessage(HttpMethod.Get, $"/admin/problem-reports/{sent.Reference.ToLowerInvariant()}");
        admin.Headers.Add("X-Admin-Token", AdminToken);
        var stored = await _http.SendAsync(admin);
        Assert.Equal(HttpStatusCode.OK, stored.StatusCode);
        var json = await stored.Content.ReadAsStringAsync();
        Assert.Contains("GPU never starts", json);
        Assert.Contains("XMRig: mining", json);

        for (var i = 1; i < ProblemReportServiceLimit; i++)
            await client.SendProblemReportAsync(device, secret, new ProblemReport("0.1.0", "Windows", null, "again"), CancellationToken.None);
        var tooMany = await Assert.ThrowsAsync<AccountRequestException>(() =>
            client.SendProblemReportAsync(device, secret, new ProblemReport("0.1.0", "Windows", null, "again"), CancellationToken.None));
        Assert.Equal(429, tooMany.StatusCode);

        _time.Advance(TimeSpan.FromDays(1.1));
        await client.SendProblemReportAsync(device, secret, new ProblemReport("0.1.0", "Windows", null, "next day"), CancellationToken.None);
    }

    [Fact]
    public async Task Problem_reports_need_the_device_secret_and_a_sane_size()
    {
        var (device, secret, _) = await Register(Usdt);
        var client = new AccountClient(_http);

        var wrongSecret = await Assert.ThrowsAsync<AccountRequestException>(() => client.SendProblemReportAsync(device,
            Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32)), new ProblemReport("0.1.0", "Windows", null, "x"), CancellationToken.None));
        Assert.Equal(401, wrongSecret.StatusCode);

        var tooLong = await Assert.ThrowsAsync<AccountRequestException>(() => client.SendProblemReportAsync(device, secret,
            new ProblemReport("0.1.0", "Windows", null, new string('x', Otium.Server.Support.ProblemReportService.MaxDetailsLength + 1)), CancellationToken.None));
        Assert.Equal(400, tooLong.StatusCode);
    }

    private const int ProblemReportServiceLimit = Otium.Server.Support.ProblemReportService.MaxPerDevicePerDay;

    [Theory]
    [InlineData("IR", "sanctions")]
    [InlineData("CN", "mining")]
    public async Task Countries_otium_doesnt_run_in_get_no_account_but_keep_their_data_rights(string country, string why)
    {
        var (device, secret, ok) = await Register(Usdt);
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);

        var register = new HttpRequestMessage(HttpMethod.Post, "/api/v1/devices")
        {
            Content = JsonContent.Create(new RegisterDeviceRequest(Guid.NewGuid().ToString("N"), Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32)), Usdt, null)),
        };
        register.Headers.Add("CF-IPCountry", country);
        var refused = await _http.SendAsync(register);
        Assert.Equal(HttpStatusCode.UnavailableForLegalReasons, refused.StatusCode);
        Assert.Contains(why, await refused.Content.ReadAsStringAsync());

        // An existing account seen from there: no summary (the app stops earning), but export still works.
        var summary = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/devices/{device}/account");
        summary.Headers.Add(AccountClient.SecretHeader, secret);
        summary.Headers.Add("CF-IPCountry", country);
        Assert.Equal(HttpStatusCode.UnavailableForLegalReasons, (await _http.SendAsync(summary)).StatusCode);
        var export = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/devices/{device}/account/export");
        export.Headers.Add(AccountClient.SecretHeader, secret);
        export.Headers.Add("CF-IPCountry", country);
        Assert.Equal(HttpStatusCode.OK, (await _http.SendAsync(export)).StatusCode);
    }

    [Fact]
    public async Task Stopping_earnings_sharing_deletes_what_the_computer_shared()
    {
        var (device, secret, _) = await Register(Usdt);
        var client = new AccountClient(_http);
        var now = _time.GetUtcNow();
        await client.ReportEarningsAsync(device, secret,
            new EarningsReport(1, now - TimeSpan.FromHours(6), now, [new DeviceEarnings("gpu", "rtx4090", 3600 * 5, 0.4, 0.1)]), CancellationToken.None);
        var store = _factory.Services.GetRequiredService<Otium.Server.Data.OtiumStore>();
        var accountId = store.FindDevice(device)!.AccountId;
        Assert.NotEmpty(store.EarningsReportsFor(accountId));

        await client.DeleteEarningsReportsAsync(device, secret, CancellationToken.None);
        Assert.Empty(store.EarningsReportsFor(accountId));
        await Assert.ThrowsAsync<AccountRequestException>(() => client.DeleteEarningsReportsAsync(device, "wrong", CancellationToken.None));
    }

    private sealed class RefusingEmail : Otium.Server.Accounts.IEmailSender
    {
        public Task SendAsync(string to, string subject, string body, CancellationToken ct) =>
            throw new System.Net.Mail.SmtpException("Message rejected: Email address is not verified.");
    }

    [Fact]
    public async Task A_refused_email_gets_a_plain_message_not_a_bare_server_error()
    {
        using var factory = _factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<Otium.Server.Accounts.IEmailSender>();
            services.AddSingleton<Otium.Server.Accounts.IEmailSender>(new RefusingEmail());
        }));
        var http = factory.CreateClient();
        var device = Guid.NewGuid().ToString("N");
        var secret = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
        (await http.PostAsJsonAsync("/api/v1/devices", new RegisterDeviceRequest(device, secret, Usdt, null))).EnsureSuccessStatusCode();

        var e = await Assert.ThrowsAsync<AccountRequestException>(() =>
            new AccountClient(http).SendEmailCodeAsync(device, secret, new EmailCodeRequest("someone@example.com"), CancellationToken.None));
        Assert.Equal(503, e.StatusCode);
        Assert.Contains("couldn't send the email", e.Message);
    }

    [Fact]
    public void Outgoing_requests_say_who_they_are()
    {
        // CoinGecko (coin prices for the site's totals) refuses requests without a User-Agent.
        var client = _factory.Services.GetRequiredService<IHttpClientFactory>().CreateClient();
        Assert.StartsWith("Otium-Server/", client.DefaultRequestHeaders.UserAgent.ToString());
    }

    [Fact]
    public void The_country_list_names_sanctioned_and_mining_ban_countries_only()
    {
        Assert.Contains("sanctions", Otium.Core.Availability.BlockedReason("ru"));
        Assert.Contains("mining", Otium.Core.Availability.BlockedReason("DZ"));
        foreach (var open in new[] { "NO", "SE", "US", "GB", "DE", "", null, "XX" })
            Assert.Null(Otium.Core.Availability.BlockedReason(open));
    }

    /// <summary>unMineable as seen through its API: every address has an account, rewarded as set per target.</summary>
    private sealed class FakeUnmineable : IUnmineableApi
    {
        public Dictionary<string, decimal> Rewarded { get; } = [];

        public Task<UnmineableAddress> GetAddressAsync(PayoutTarget target, CancellationToken ct) =>
            Task.FromResult(new UnmineableAddress(target.ToString(), 0.1m, 0.1m, target.Coin.Threshold, true, true, "TRX", [], null));

        public Task<UnmineableStats> GetStatsAsync(string uuid, CancellationToken ct)
        {
            var rewarded = Rewarded.GetValueOrDefault(uuid);
            var coin = uuid.Split(':')[0];
            return Task.FromResult(new UnmineableStats(coin, 0.1m, 0.5m, 0.6m, rewarded, rewarded, rewarded, 0, null, 1.5m));
        }

        public Task<IReadOnlyList<UnmineableWorker>> GetWorkersAsync(string uuid, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<UnmineableWorker>>([]);

        public Task<IReadOnlyList<UnmineablePayment>> GetPaymentsAsync(string uuid, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<UnmineablePayment>>([]);

        public Task<UnmineableReferrals> GetReferralsAsync(string uuid, CancellationToken ct) =>
            Task.FromResult(new UnmineableReferrals("ztjq-jr7s", 3));
    }
}
