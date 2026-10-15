using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Otium.Core.Pools;
using Otium.Core.Accounts;
using Otium.Server;

namespace Otium.Tests;

public sealed class ServerTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"otium-test-{Guid.NewGuid():N}.db");
    private readonly FakePool _pool = new();
    private readonly string _poolWallet = TestAddresses.Generate();
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _http;

    public ServerTests()
    {
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Otium", $"Data Source={_dbPath};Pooling=False");
            builder.UseSetting("DisableBackgroundJobs", "true");
            builder.UseSetting("Earnings:PoolWallets:0", _poolWallet);
            builder.UseSetting("Referral:PublicBaseUrl", "https://otium.example");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IMoneroPoolApi>();
                services.AddSingleton<IMoneroPoolApi>(_pool);
            });
        });
        _http = _factory.CreateClient();
    }

    private sealed class FakePool : IMoneroPoolApi
    {
        public Dictionary<string, double> Hashes { get; } = [];

        public Task<MoneroNetworkStats> GetNetworkStatsAsync(CancellationToken ct) =>
            Task.FromResult(new MoneroNetworkStats(1, 1, 1, DateTimeOffset.UtcNow));

        public Task<PoolMinerStats?> GetMinerStatsAsync(string walletAddress, CancellationToken ct) =>
            Task.FromResult<PoolMinerStats?>(new PoolMinerStats(Hashes.GetValueOrDefault(walletAddress), 0, 0, 0));

        public Task<AlgoProfits> GetAlgoProfitsAsync(CancellationToken ct) =>
            Task.FromResult(new AlgoProfits(new Dictionary<string, double>(), 1, DateTimeOffset.UtcNow));

        public Task<IReadOnlyDictionary<string, IReadOnlyList<HashratePoint>>> GetWorkerHistoryAsync(string walletAddress, CancellationToken ct) =>
            Task.FromResult<IReadOnlyDictionary<string, IReadOnlyList<HashratePoint>>>(new Dictionary<string, IReadOnlyList<HashratePoint>>());
    }

    private sealed record Device(string Id, string Secret, string Wallet);

    private static Device NewDevice() =>
        new(Guid.NewGuid().ToString("N"), Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32)), TestAddresses.Generate());

    private async Task<AccountSummary> Register(Device d, string? code = null)
    {
        var response = await _http.PostAsJsonAsync("/api/v1/devices", new RegisterDeviceRequest(d.Id, d.Secret, d.Wallet, code));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AccountSummary>())!;
    }

    private Task<AccountSummary> Summary(Device d) =>
        new AccountClient(_http).GetSummaryAsync(d.Id, d.Secret, CancellationToken.None);

    [Fact]
    public async Task Registering_issues_a_shareable_code()
    {
        var summary = await Register(NewDevice());
        Assert.True(ReferralCodes.IsWellFormed(summary.Code));
        Assert.Equal($"https://otium.example/r/{summary.Code}", summary.ShareUrl);
        Assert.Null(summary.ReferrerWallet);
    }

    [Fact]
    public async Task Re_registering_keeps_the_same_code_and_requires_the_secret()
    {
        var device = NewDevice();
        var first = await Register(device);
        var second = await Register(device);
        Assert.Equal(first.Code, second.Code);

        var stolen = await _http.PostAsJsonAsync("/api/v1/devices", new RegisterDeviceRequest(device.Id, new string('0', 64), device.Wallet, null));
        Assert.Equal(HttpStatusCode.Unauthorized, stolen.StatusCode);
    }

    [Fact]
    public async Task Invalid_payout_address_is_rejected()
    {
        var device = NewDevice() with { Wallet = "not-a-wallet" };
        var response = await _http.PostAsJsonAsync("/api/v1/devices", new RegisterDeviceRequest(device.Id, device.Secret, device.Wallet, null));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Friend_joining_with_a_code_is_linked_and_gets_thirty_days_fee_free()
    {
        var alice = NewDevice();
        var aliceCode = (await Register(alice)).Code;

        var bob = NewDevice();
        var bobSummary = await Register(bob, aliceCode.ToLowerInvariant()); // codes are case-insensitive

        Assert.Null(bobSummary.ReferralCodeError);
        Assert.Equal(alice.Wallet, bobSummary.ReferrerWallet);
        Assert.NotNull(bobSummary.FeeFreeUntil);
        Assert.InRange(bobSummary.FeeFreeUntil!.Value - DateTimeOffset.UtcNow, TimeSpan.FromDays(29.9), TimeSpan.FromDays(30.1));

        var aliceSummary = await Summary(alice);
        Assert.Equal(1, aliceSummary.InvitedCount);
        Assert.Equal(0, aliceSummary.VerifiedCount);
        Assert.Null(aliceSummary.FeeFreeUntil);
    }

    [Fact]
    public async Task Referrer_is_rewarded_only_once_the_friend_has_really_mined()
    {
        var alice = NewDevice();
        var aliceCode = (await Register(alice)).Code;
        var bob = NewDevice();
        await Register(bob, aliceCode);

        _pool.Hashes[bob.Wallet] = 10_000; // a few seconds of mining: not enough
        Assert.Equal(0, (await Summary(alice)).VerifiedCount);

        // Recheck is rate-limited per friend; a fresh friend avoids waiting an hour in this test.
        var carol = NewDevice();
        await Register(carol, aliceCode);
        _pool.Hashes[carol.Wallet] = 600_000_000;
        var summary = await Summary(alice);
        Assert.Equal(2, summary.InvitedCount);
        Assert.Equal(1, summary.VerifiedCount);
        Assert.NotNull(summary.FeeFreeUntil);
        Assert.InRange(summary.FeeFreeUntil!.Value - DateTimeOffset.UtcNow, TimeSpan.FromDays(29.9), TimeSpan.FromDays(30.1));
    }

    [Fact]
    public async Task Own_code_and_own_wallet_cannot_be_used()
    {
        var alice = NewDevice();
        var code = (await Register(alice)).Code;

        var self = await Register(alice, code);
        Assert.NotNull(self.ReferralCodeError);
        Assert.Null(self.ReferrerWallet);

        var aliceLaptop = NewDevice() with { Wallet = alice.Wallet };
        var sameWallet = await Register(aliceLaptop, code);
        Assert.NotNull(sameWallet.ReferralCodeError);
        Assert.Null(sameWallet.ReferrerWallet);
    }

    [Fact]
    public async Task Two_people_cannot_invite_each_other()
    {
        var alice = NewDevice();
        var bob = NewDevice();
        var aliceCode = (await Register(alice)).Code;
        var bobCode = (await Register(bob, aliceCode)).Code;

        var back = await Register(alice, bobCode);
        Assert.NotNull(back.ReferralCodeError);
        Assert.Null(back.ReferrerWallet);
    }

    [Fact]
    public async Task A_wallet_can_only_join_through_one_invite()
    {
        var alice = NewDevice();
        var aliceCode = (await Register(alice)).Code;
        var bob = NewDevice();
        await Register(bob, aliceCode);

        // Same wallet on a "new device" (e.g. a VM) trying to be counted again.
        var bobClone = NewDevice() with { Wallet = bob.Wallet };
        var clone = await Register(bobClone, aliceCode);
        Assert.NotNull(clone.ReferralCodeError);
        Assert.Equal(1, (await Summary(alice)).InvitedCount);
    }

    [Fact]
    public async Task Unknown_code_reports_an_error_but_registration_succeeds()
    {
        var summary = await Register(NewDevice(), "ZZZZZZZ");
        Assert.NotNull(summary.ReferralCodeError);
        Assert.True(ReferralCodes.IsWellFormed(summary.Code));
    }

    [Fact]
    public async Task Summary_needs_the_device_secret()
    {
        var alice = NewDevice();
        await Register(alice);
        await Assert.ThrowsAsync<AccountRequestException>(() =>
            new AccountClient(_http).GetSummaryAsync(alice.Id, new string('f', 64), CancellationToken.None));
    }

    [Fact]
    public async Task Landing_page_shows_the_invite_code()
    {
        var code = (await Register(NewDevice())).Code;
        var html = await _http.GetStringAsync($"/r/{code}");
        Assert.Contains(code, html);
        Assert.Contains("30 days", html);

        var unknown = await _http.GetStringAsync("/r/<script>");
        Assert.DoesNotContain("<script>", unknown);
    }

    private async Task<AccountSummary> RegisterBalance(Device d, string? code = null)
    {
        var response = await _http.PostAsJsonAsync("/api/v1/devices", new RegisterDeviceRequest(d.Id, d.Secret, null, code));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AccountSummary>())!;
    }

    [Fact]
    public async Task Balance_account_gets_a_worker_id_and_a_pool_address_but_no_wallet_is_needed()
    {
        var summary = await RegisterBalance(NewDevice());
        Assert.StartsWith("ot", summary.WorkerId);
        Assert.Equal(14, summary.WorkerId.Length);
        Assert.Equal(_poolWallet, summary.PoolWallet);
        Assert.Equal(0m, summary.Balance.AvailableUsd);
        Assert.Equal(10, summary.Balance.FeePercent);
        Assert.Equal(5, summary.Balance.ReferralPercent);
        Assert.Equal(5m, summary.Balance.MinPayPalUsd);
    }

    [Fact]
    public async Task Own_wallet_accounts_are_not_given_a_pool_address()
    {
        var summary = await Register(NewDevice());
        Assert.Null(summary.PoolWallet);
    }

    [Fact]
    public async Task Balance_accounts_can_invite_each_other_without_wallets()
    {
        var alice = NewDevice();
        var code = (await RegisterBalance(alice)).Code;
        var bob = await RegisterBalance(NewDevice(), code);
        Assert.Null(bob.ReferralCodeError);
        Assert.NotNull(bob.ReferredAt);
        Assert.Null(bob.ReferrerWallet); // balance referrers are rewarded server-side, not by mining slices
        Assert.Equal(1, (await Summary(alice)).InvitedCount);
    }

    [Fact]
    public async Task Cashout_with_no_balance_is_refused_with_a_readable_message()
    {
        var device = NewDevice();
        await RegisterBalance(device);
        var error = await Assert.ThrowsAsync<AccountRequestException>(() => new AccountClient(_http)
            .RequestCashoutAsync(device.Id, device.Secret, new CashoutRequest(CashoutMethods.PayPal, "me@example.com"), CancellationToken.None));
        Assert.Equal(400, error.StatusCode);
        Assert.Contains("at least $5.00", error.Message);
    }

    [Fact]
    public async Task Admin_endpoints_need_the_token()
    {
        var response = await _http.GetAsync("/admin/summary");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    public void Dispose()
    {
        _http.Dispose();
        _factory.Dispose();
        if (File.Exists(_dbPath))
            File.Delete(_dbPath);
    }
}
