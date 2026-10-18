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
        Assert.Equal(["NVIDIA"], pearl.OnlyGpus); // unMineable's PearlPow is NVIDIA-only; Pearl's fork isn't active
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
