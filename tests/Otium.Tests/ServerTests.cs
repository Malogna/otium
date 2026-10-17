using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Otium.Core.Pools;
using Otium.Core.Accounts;
using Otium.Server;
using Otium.Server.Accounts;
using Otium.Server.Data;

namespace Otium.Tests;

public sealed class ServerTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"otium-test-{Guid.NewGuid():N}.db");
    private readonly FakePool _pool = new();
    private readonly string _poolWallet = TestAddresses.Generate();
    private readonly ManualTime _time = new(DateTimeOffset.UtcNow);
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _http;

    private const string AdminToken = "test-admin-token";

    public ServerTests()
    {
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Otium", $"Data Source={_dbPath};Pooling=False");
            builder.UseSetting("DisableBackgroundJobs", "true");
            builder.UseSetting("Earnings:PoolWallets:0", _poolWallet);
            builder.UseSetting("Referral:PublicBaseUrl", "https://otium.example");
            builder.UseSetting("Admin:Token", AdminToken);
            // PayPal stays enabled here because these tests exercise the generic cash-out flow through it.
            string[] methods = ["btc", "eth", "bnb", "sol", "paypal", "xmr"];
            for (var i = 0; i < methods.Length; i++)
                builder.UseSetting($"Cashouts:Methods:{i}", methods[i]);
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IMoneroPoolApi>();
                services.AddSingleton<IMoneroPoolApi>(_pool);
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(_time);
            });
        });
        _http = _factory.CreateClient();
    }

    private sealed class FakePool : IMoneroPoolApi
    {
        public Dictionary<string, double> Hashes { get; } = [];

        /// <summary>Simulates the pool API being unreachable.</summary>
        public bool Down { get; set; }

        public Task<MoneroNetworkStats> GetNetworkStatsAsync(CancellationToken ct) =>
            Down ? throw new HttpRequestException("pool down") : Task.FromResult(new MoneroNetworkStats(1, 1, 1, DateTimeOffset.UtcNow));

        public Task<PoolMinerStats?> GetMinerStatsAsync(string walletAddress, CancellationToken ct) =>
            Down ? throw new HttpRequestException("pool down") : Task.FromResult<PoolMinerStats?>(new PoolMinerStats(Hashes.GetValueOrDefault(walletAddress), 0, 0, 0));

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
    public async Task Cashout_needs_an_email_and_then_enough_balance()
    {
        var device = NewDevice();
        await RegisterBalance(device);
        var client = new AccountClient(_http);
        var noEmail = await Assert.ThrowsAsync<AccountRequestException>(() =>
            client.RequestCashoutAsync(device.Id, device.Secret, new CashoutRequest(CashoutMethods.PayPal, "me@example.com"), CancellationToken.None));
        Assert.Equal(403, noEmail.StatusCode);
        Assert.Contains("Add your email", noEmail.Message);

        await SignIn(device, "me@example.com");
        var noMoney = await Assert.ThrowsAsync<AccountRequestException>(() =>
            client.RequestCashoutAsync(device.Id, device.Secret, new CashoutRequest(CashoutMethods.PayPal, "me@example.com"), CancellationToken.None));
        Assert.Equal(400, noMoney.StatusCode);
        Assert.Contains("at least $5.00", noMoney.Message);
    }

    // ---------- email sign-in ----------

    private OutboxEmailSender Outbox => _factory.Services.GetRequiredService<OutboxEmailSender>();

    private OtiumStore Store => _factory.Services.GetRequiredService<OtiumStore>();

    private string LatestCode(string email) =>
        System.Text.RegularExpressions.Regex.Match(Outbox.Sent.Last(m => m.To == email).Body, @"\b(\d{6})\b").Groups[1].Value;

    private async Task<AccountSummary> SignIn(Device device, string email)
    {
        var client = new AccountClient(_http);
        await client.SendEmailCodeAsync(device.Id, device.Secret, new EmailCodeRequest(email), CancellationToken.None);
        return await client.VerifyEmailCodeAsync(device.Id, device.Secret, new EmailVerifyRequest(email, LatestCode(email)), CancellationToken.None);
    }

    [Fact]
    public async Task Adding_an_email_secures_the_account()
    {
        var device = NewDevice();
        var before = await RegisterBalance(device);
        var after = await SignIn(device, "Alex@Example.com");
        Assert.Equal("alex@example.com", after.Email);
        Assert.Equal(before.WorkerId, after.WorkerId);
        Assert.Equal(before.Code, after.Code);
        Assert.Equal(1, after.DeviceCount);
    }

    [Fact]
    public async Task Signing_in_on_a_second_computer_joins_the_account_and_keeps_its_earnings()
    {
        var laptop = NewDevice();
        var account = await RegisterBalance(laptop);
        await SignIn(laptop, "sam@example.com");

        // The new PC mined a little on its own anonymous account before signing in.
        var desktop = NewDevice();
        var anonymous = await RegisterBalance(desktop);
        var anonymousId = Store.FindAccountByWorker(anonymous.WorkerId)!.AccountId;
        Store.AddLedger(anonymousId, LedgerKinds.Earning, 1_500_000, _time.GetUtcNow(), _time.GetUtcNow(), "test");

        var joined = await SignIn(desktop, "sam@example.com");

        Assert.Equal(account.WorkerId, joined.WorkerId); // both PCs now mine for the same account
        Assert.Equal(2, joined.DeviceCount);
        Assert.Equal(1.50m, joined.Balance.AvailableUsd);
        Assert.Equal(1.50m, (await Summary(laptop)).Balance.AvailableUsd);
        // The old worker id still credits the surviving account.
        Assert.Equal(Store.FindAccountByWorker(account.WorkerId)!.AccountId, Store.Resolve(Store.FindAccountByWorker(anonymous.WorkerId))!.AccountId);
    }

    [Fact]
    public async Task Wrong_codes_are_rejected_and_lock_after_five_tries()
    {
        var device = NewDevice();
        await RegisterBalance(device);
        var client = new AccountClient(_http);
        await client.SendEmailCodeAsync(device.Id, device.Secret, new EmailCodeRequest("kim@example.com"), CancellationToken.None);
        var right = LatestCode("kim@example.com");
        var wrong = right == "000000" ? "111111" : "000000";

        for (var i = 0; i < AccountService.MaxCodeAttempts; i++)
        {
            var e = await Assert.ThrowsAsync<AccountRequestException>(() =>
                client.VerifyEmailCodeAsync(device.Id, device.Secret, new EmailVerifyRequest("kim@example.com", wrong), CancellationToken.None));
            Assert.Contains("isn't right", e.Message);
        }
        var locked = await Assert.ThrowsAsync<AccountRequestException>(() =>
            client.VerifyEmailCodeAsync(device.Id, device.Secret, new EmailVerifyRequest("kim@example.com", right), CancellationToken.None));
        Assert.Contains("Too many wrong tries", locked.Message);
    }

    [Fact]
    public async Task Codes_expire_and_cannot_be_reused()
    {
        var device = NewDevice();
        await RegisterBalance(device);
        var client = new AccountClient(_http);
        await client.SendEmailCodeAsync(device.Id, device.Secret, new EmailCodeRequest("lee@example.com"), CancellationToken.None);
        var code = LatestCode("lee@example.com");
        _time.Advance(AccountService.CodeLifetime + TimeSpan.FromMinutes(1));
        var expired = await Assert.ThrowsAsync<AccountRequestException>(() =>
            client.VerifyEmailCodeAsync(device.Id, device.Secret, new EmailVerifyRequest("lee@example.com", code), CancellationToken.None));
        Assert.Contains("expired", expired.Message);

        await SignIn(device, "lee@example.com");
        var reused = await Assert.ThrowsAsync<AccountRequestException>(() =>
            client.VerifyEmailCodeAsync(device.Id, device.Secret, new EmailVerifyRequest("lee@example.com", LatestCode("lee@example.com")), CancellationToken.None));
        Assert.Contains("expired", reused.Message);
    }

    [Fact]
    public async Task Code_requests_are_rate_limited_per_email()
    {
        var device = NewDevice();
        await RegisterBalance(device);
        var client = new AccountClient(_http);
        for (var i = 0; i < AccountService.MaxCodesPerEmailPerHour; i++)
            await client.SendEmailCodeAsync(device.Id, device.Secret, new EmailCodeRequest("pat@example.com"), CancellationToken.None);
        var limited = await Assert.ThrowsAsync<AccountRequestException>(() =>
            client.SendEmailCodeAsync(device.Id, device.Secret, new EmailCodeRequest("pat@example.com"), CancellationToken.None));
        Assert.Equal(429, limited.StatusCode);
    }

    // ---------- invite leaderboard ----------

    /// <summary>Registers a friend through <paramref name="code"/>; verified friends have really started earning.</summary>
    private async Task Invite(string code, bool verified = true, DateTimeOffset? referredAt = null)
    {
        var friend = await RegisterBalance(NewDevice(), code);
        var account = Store.FindAccountByCode(friend.Code)!;
        Store.Upsert(account with { Verified = verified, ReferredAt = referredAt ?? account.ReferredAt });
    }

    private Task<InviteLeaderboard> Leaderboard(Device d) =>
        new AccountClient(_http).GetInviteLeaderboardAsync(d.Id, d.Secret, CancellationToken.None);

    [Fact]
    public async Task The_leaderboard_ranks_inviters_by_friends_who_started_earning()
    {
        var alice = NewDevice();
        var bob = NewDevice();
        var carol = NewDevice();
        var aliceCode = (await RegisterBalance(alice)).Code;
        var bobCode = (await RegisterBalance(bob)).Code;
        var carolCode = (await RegisterBalance(carol)).Code;
        await Invite(aliceCode);
        await Invite(aliceCode);
        await Invite(aliceCode, verified: false); // signed up but never earned: doesn't count
        await Invite(bobCode);
        await Invite(carolCode);

        var board = await Leaderboard(bob);

        Assert.Equal(new DateTimeOffset(_time.GetUtcNow().Year, _time.GetUtcNow().Month, 1, 0, 0, 0, TimeSpan.Zero), board.PeriodStart);
        Assert.Equal(3, board.Top.Count);
        Assert.Equal(new InviteLeaderboardEntry(1, ReferralCodes.Mask(aliceCode), 2, false), board.Top[0]);
        Assert.All(board.Top.Skip(1), e => Assert.Equal(2, e.Rank)); // bob and carol tie
        Assert.Contains(board.Top, e => e is { IsYou: true, Name: "You", Friends: 1 });
        Assert.Equal(2, board.YourRank);
        Assert.Equal(1, board.YourFriends);
        Assert.DoesNotContain(board.Top, e => e.Name.Contains(aliceCode, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Last_months_friends_and_newcomers_without_friends_are_not_ranked()
    {
        var alice = NewDevice();
        var aliceCode = (await RegisterBalance(alice)).Code;
        await Invite(aliceCode, referredAt: _time.GetUtcNow().AddMonths(-1).AddDays(-1));
        var newcomer = NewDevice();
        await RegisterBalance(newcomer);

        Assert.DoesNotContain((await Leaderboard(alice)).Top, e => e.IsYou);
        var board = await Leaderboard(newcomer);
        Assert.Null(board.YourRank);
        Assert.Equal(0, board.YourFriends);
    }

    [Fact]
    public async Task The_leaderboard_needs_the_device_secret()
    {
        var alice = NewDevice();
        await RegisterBalance(alice);
        var error = await Assert.ThrowsAsync<AccountRequestException>(() => Leaderboard(alice with { Secret = "wrong" }));
        Assert.Equal(401, error.StatusCode);
    }

    [Fact]
    public async Task Admin_endpoints_need_the_token()
    {
        var response = await _http.GetAsync("/admin/summary");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task The_app_is_told_each_payout_method_once_with_its_minimum()
    {
        var device = NewDevice();
        await RegisterBalance(device);

        var methods = (await Summary(device)).Balance.Methods!;

        Assert.Equal(["btc", "eth", "bnb", "sol", "paypal", "xmr"], methods.Select(m => m.Method));
        Assert.Equal(5m, methods.Single(m => m.Method == "btc").MinUsd);
        Assert.Equal(8m, methods.Single(m => m.Method == "eth").MinUsd);
        Assert.Null(methods.Single(m => m.Method == "btc").FeeNote);
        Assert.Contains("THORChain", methods.Single(m => m.Method == "bnb").FeeNote);
        Assert.Equal(5m, (await Summary(device)).Balance.MinCryptoUsd);
    }

    // ---------- operator dashboard ----------

    private async Task<HttpResponseMessage> Admin(HttpMethod method, string path, object? body = null)
    {
        using var request = new HttpRequestMessage(method, path) { Content = body is null ? null : JsonContent.Create(body) };
        request.Headers.Add("X-Admin-Token", AdminToken);
        return await _http.SendAsync(request);
    }

    /// <summary>A signed-in account with a cash-out waiting for review (new accounts always wait).</summary>
    private async Task<(Device Device, CashoutInfo Cashout)> CashoutInReview(string email, long micros = 20_000_000)
    {
        var device = NewDevice();
        await RegisterBalance(device);
        var account = await SignIn(device, email);
        Store.AddLedger(Store.FindAccountByCode(account.Code)!.AccountId, LedgerKinds.Earning, micros, _time.GetUtcNow(), _time.GetUtcNow(), "test");
        var cashout = await new AccountClient(_http).RequestCashoutAsync(device.Id, device.Secret,
            new CashoutRequest(CashoutMethods.PayPal, email), CancellationToken.None);
        return (device, cashout);
    }

    [Fact]
    public async Task The_dashboard_page_holds_no_data_and_locks_itself_down()
    {
        var page = await _http.GetAsync("/admin");

        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Equal("text/html", page.Content.Headers.ContentType!.MediaType);
        var csp = page.Headers.GetValues("Content-Security-Policy").Single();
        Assert.Contains("script-src 'self'", csp);
        Assert.Contains("frame-ancestors 'none'", csp);
        Assert.DoesNotContain("unsafe-inline", csp);
        Assert.Equal("no-store", page.Headers.CacheControl!.ToString());
        var html = await page.Content.ReadAsStringAsync();
        Assert.Contains("Otium operator", html);
        Assert.DoesNotContain("<script>", html); // only the external script, which the policy allows
        Assert.DoesNotContain(AdminToken, html);

        Assert.Equal(HttpStatusCode.OK, (await _http.GetAsync("/admin/dashboard.js")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _http.GetAsync("/admin/dashboard.css")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _http.GetAsync("/admin/cashouts")).StatusCode);
    }

    [Fact]
    public async Task Without_an_admin_token_there_is_no_dashboard()
    {
        using var factory = _factory.WithWebHostBuilder(b => b.UseSetting("Admin:Token", ""));
        using var http = factory.CreateClient();

        Assert.Equal(HttpStatusCode.NotFound, (await http.GetAsync("/admin")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await http.GetAsync("/admin/dashboard.js")).StatusCode);
    }

    [Fact]
    public async Task The_review_queue_shows_what_a_reviewer_needs_and_approving_queues_it()
    {
        var (_, cashout) = await CashoutInReview("rev@example.com");

        var queue = await (await Admin(HttpMethod.Get, "/admin/cashouts?status=review")).Content.ReadFromJsonAsync<JsonElement>();
        var entry = queue.EnumerateArray().Single(e => e.GetProperty("id").GetString() == cashout.Id);
        Assert.Equal("rev@example.com", entry.GetProperty("destination").GetString()); // full destination for the operator
        Assert.Equal("rev@example.com", entry.GetProperty("accountEmail").GetString());
        Assert.Equal(20m, entry.GetProperty("amountUsd").GetDecimal());
        Assert.Equal(20_000_000, entry.GetProperty("micros").GetInt64());
        Assert.Equal(20m, entry.GetProperty("lifetimeEarnedUsd").GetDecimal());
        Assert.Equal(0, entry.GetProperty("otherAccountsUsingDestination").GetInt64());
        Assert.False(string.IsNullOrEmpty(entry.GetProperty("note").GetString()));

        Assert.Equal(HttpStatusCode.OK, (await Admin(HttpMethod.Post, $"/admin/cashouts/{cashout.Id}/approve")).StatusCode);
        Assert.Equal(CashoutStatuses.Queued, Store.FindCashout(cashout.Id)!.Status);
        // Approving twice does nothing: it's no longer in review.
        Assert.Equal(HttpStatusCode.NotFound, (await Admin(HttpMethod.Post, $"/admin/cashouts/{cashout.Id}/approve")).StatusCode);
    }

    [Fact]
    public async Task Rejecting_returns_the_money_and_tells_the_user_why()
    {
        var (device, cashout) = await CashoutInReview("rej@example.com");

        var response = await Admin(HttpMethod.Post, $"/admin/cashouts/{cashout.Id}/reject", new { note = "please use your own PayPal" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var balance = (await Summary(device)).Balance;
        Assert.Equal(20m, balance.AvailableUsd);
        var rejected = Assert.Single(balance.RecentCashouts);
        Assert.Equal(CashoutStatuses.Failed, rejected.Status);
        Assert.Equal("please use your own PayPal", rejected.Note);
    }

    [Fact]
    public async Task The_summary_still_shows_the_ledger_when_the_pool_is_down()
    {
        await CashoutInReview("sum@example.com", micros: 7_500_000);
        _pool.Down = true;

        var response = await Admin(HttpMethod.Get, "/admin/summary");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var summary = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(7.5m, summary.GetProperty("creditedToUsersUsd").GetDecimal());
        Assert.Equal(7.5m, summary.GetProperty("moneroOceanCreditedToUsersUsd").GetDecimal());
        Assert.Equal(1, summary.GetProperty("reviewQueue").GetInt32());
        Assert.Equal(0m, summary.GetProperty("owedToUsersUsd").GetDecimal());
        Assert.Equal(7.5m, summary.GetProperty("inOpenCashoutsUsd").GetDecimal());
        Assert.False(string.IsNullOrEmpty(summary.GetProperty("poolError").GetString()));
    }

    [Fact]
    public async Task Marking_a_cashout_paid_by_hand_emails_a_receipt_and_shows_in_the_app()
    {
        var device = NewDevice();
        await RegisterBalance(device);
        var account = await SignIn(device, "sam@example.com");
        var accountId = Store.FindAccountByCode(account.Code)!.AccountId;
        Store.AddLedger(accountId, LedgerKinds.Earning, 20_000_000, _time.GetUtcNow(), _time.GetUtcNow(), "test");
        var client = new AccountClient(_http);
        // A brand-new account, so it waits for review rather than being sent automatically.
        var cashout = await client.RequestCashoutAsync(device.Id, device.Secret, new CashoutRequest(CashoutMethods.PayPal, "sam@example.com"), CancellationToken.None);
        Assert.Equal(CashoutStatuses.Review, cashout.Status);
        Assert.Null(cashout.Note); // the review reason is for the operator only

        using var request = new HttpRequestMessage(HttpMethod.Post, $"/admin/cashouts/{cashout.Id}/mark-paid")
        {
            Content = JsonContent.Create(new { note = "PayPal txn 9XY" }),
        };
        request.Headers.Add("X-Admin-Token", AdminToken);
        Assert.Equal(HttpStatusCode.OK, (await _http.SendAsync(request)).StatusCode);

        var receipt = Outbox.Sent.Last();
        Assert.Equal("sam@example.com", receipt.To);
        Assert.Equal("Your Otium cash-out of $20.00 was sent", receipt.Subject);
        var balance = (await Summary(device)).Balance;
        Assert.Equal(20m, balance.PaidOutUsd);
        var paid = Assert.Single(balance.RecentCashouts);
        Assert.Equal(CashoutStatuses.Paid, paid.Status);
        Assert.NotNull(paid.UpdatedAt);
    }

    public void Dispose()
    {
        _http.Dispose();
        _factory.Dispose();
        if (File.Exists(_dbPath))
            File.Delete(_dbPath);
    }
}
