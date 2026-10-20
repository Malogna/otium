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
using Otium.Server.Data;

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
    private const string ThirdFriend = "BNB:0xdbF03B407c01E7cD3CBea99509d93f8DDDC8C6FB";
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"otium-referral-{Guid.NewGuid():N}.db");
    private readonly ManualTime _time = new(new DateTimeOffset(2026, 10, 20, 12, 0, 0, TimeSpan.Zero));
    private readonly NonCustodialServerTests.FakeUnmineable _unmineable = new();
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
                services.AddSingleton<IUnmineableApi>(_unmineable);
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

    /// <param name="Hardware">The app's hardware id: the same for every install on one computer.</param>
    private sealed record Computer(string Id, string Secret, string Hardware);

    private OtiumStore Store => _factory.Services.GetRequiredService<OtiumStore>();

    private HttpClient From(string address)
    {
        var http = _factory.CreateClient();
        http.DefaultRequestHeaders.Add("X-Forwarded-For", address);
        return http;
    }

    private static Computer NewComputer() =>
        new(Guid.NewGuid().ToString("N"), Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32)), Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32)));

    /// <summary>Another install on the same computer (a reinstall, or a second user account on it).</summary>
    private static Computer Reinstall(Computer computer) => NewComputer() with { Hardware = computer.Hardware };

    private async Task<HttpResponseMessage> Register(string from, Computer computer, string? payout, string? code = null) =>
        await From(from).PostAsJsonAsync("/api/v1/devices", new RegisterDeviceRequest(computer.Id, computer.Secret, payout, code, computer.Hardware));

    private async Task<AccountSummary> RegisterOk(string from, Computer computer, string? payout, string? code = null)
    {
        using var response = await Register(from, computer, payout, code);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AccountSummary>())!;
    }

    private async Task<AccountSummary> Summary(string from, Computer computer) =>
        await new AccountClient(From(from)).GetSummaryAsync(computer.Id, computer.Secret, CancellationToken.None);

    /// <summary>The pool shows the friend's address credited in two daily accountings, mined by these workers.</summary>
    private void Mined(string payout, params string[] workers)
    {
        var target = Otium.Core.Wallets.PayoutTarget.ParseOrNull(payout)!.ToString();
        _unmineable.Rewarded[target] = 0.0002m;
        _unmineable.CreditedBefore[target] = 0.0003m;
        _unmineable.Workers[target] = workers;
    }

    private static string WorkerOf(Computer computer) => $"ot{computer.Id[..8]}";

    private string AccountIdOf(Computer computer) => Store.FindDevice(computer.Id)!.AccountId;

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
    public async Task The_same_address_under_another_coin_is_the_same_wallet()
    {
        var inviterComputer = NewComputer();
        var inviter = await RegisterOk("203.0.113.71", inviterComputer, Inviter);

        // USDT and TRX both pay TRON addresses; BNB and ETH both pay 0x addresses.
        var self = await RegisterOk("198.51.100.31", NewComputer(), "TRX:TR7NHqjeKQxGTCi8q8ZY4pL8otSzgjLj6t", inviter.Code);
        Assert.Equal("You can't invite a device that pays out to your own wallet.", self.ReferralCodeError);

        var friendComputer = NewComputer();
        await RegisterOk("198.51.100.32", friendComputer, Friend, inviter.Code);
        using (var refused = await Register("198.51.100.32", friendComputer, "TRX:TR7NHqjeKQxGTCi8q8ZY4pL8otSzgjLj6t"))
            Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        using (var refused = await Register("203.0.113.71", inviterComputer, "ETH:0x5aAeb6053F3E94C9b9A09f33669435E7Ef1BeAed"))
            Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);

        var again = await RegisterOk("198.51.100.33", NewComputer(), "ETH:0x5aAeb6053F3E94C9b9A09f33669435E7Ef1BeAed", inviter.Code);
        Assert.Equal("This wallet has already joined through an invite.", again.ReferralCodeError);
    }

    [Fact]
    public async Task Deleting_the_account_and_joining_again_doesnt_repeat_the_invite()
    {
        var inviter = await RegisterOk("203.0.113.72", NewComputer(), Inviter);
        var friendComputer = NewComputer();
        Assert.Null((await RegisterOk("198.51.100.34", friendComputer, Friend, inviter.Code)).ReferralCodeError);
        await new AccountClient(From("198.51.100.34")).DeleteAccountAsync(friendComputer.Id, friendComputer.Secret,
            new DeleteAccountRequest(null), CancellationToken.None);

        // The same computer, reinstalled with a new address; the same address on another computer (or under another coin).
        var sameComputer = await RegisterOk("198.51.100.34", Reinstall(friendComputer), OtherFriend, inviter.Code);
        Assert.Equal("This computer has already joined through an invite.", sameComputer.ReferralCodeError);
        Assert.Null(sameComputer.FeeFreeUntil);
        var sameWallet = await RegisterOk("198.51.100.35", NewComputer(), "ETH:0x5aAeb6053F3E94C9b9A09f33669435E7Ef1BeAed", inviter.Code);
        Assert.Equal("This wallet has already joined through an invite.", sameWallet.ReferralCodeError);

        // Two households can't take turns inviting each other either: each joins through an invite once.
        var other = await RegisterOk("198.51.100.36", NewComputer(), ThirdFriend);
        var back = await RegisterOk("198.51.100.37", Reinstall(friendComputer), TestAddresses.Generate(), other.Code);
        Assert.Equal("This computer has already joined through an invite.", back.ReferralCodeError);

        // Someone new on another computer is welcome.
        Assert.Null((await RegisterOk("198.51.100.38", NewComputer(), OtherFriend, inviter.Code)).ReferralCodeError);
    }

    [Fact]
    public async Task A_code_waits_for_a_payout_address_and_a_later_address_cant_be_one_that_already_joined()
    {
        var inviter = await RegisterOk("203.0.113.73", NewComputer(), Inviter);

        var noAddress = await RegisterOk("198.51.100.40", NewComputer(), null, inviter.Code);
        Assert.Equal("Add your payout address to use an invite code.", noAddress.ReferralCodeError);
        Assert.Null(noAddress.ReferredAt);

        // Joined with a spare address, then switched to one that already joined through an invite: the fee-free days end.
        await RegisterOk("198.51.100.41", NewComputer(), Friend, inviter.Code);
        var switcher = NewComputer();
        var joined = await RegisterOk("198.51.100.42", switcher, TestAddresses.Generate(), inviter.Code);
        Assert.Null(joined.ReferralCodeError);
        _time.Advance(TimeSpan.FromDays(2));
        var switched = await RegisterOk("198.51.100.42", switcher, Friend);
        Assert.Equal(_time.GetUtcNow(), switched.FeeFreeUntil);

        // Moving to a new address of their own is fine.
        var mover = NewComputer();
        var moverJoined = await RegisterOk("198.51.100.43", mover, TestAddresses.Generate(), inviter.Code);
        var moved = await RegisterOk("198.51.100.43", mover, ThirdFriend);
        Assert.Equal(moverJoined.FeeFreeUntil, moved.FeeFreeUntil);
    }

    [Fact]
    public async Task A_computer_signing_in_with_an_address_that_already_joined_ends_the_accounts_invite_days()
    {
        var inviter = await RegisterOk("203.0.113.74", NewComputer(), Inviter);
        await RegisterOk("198.51.100.44", NewComputer(), Friend, inviter.Code);

        // An invited account with an email, and another computer (its own account, paying the address that already joined).
        var invited = NewComputer();
        Assert.NotNull((await RegisterOk("198.51.100.45", invited, TestAddresses.Generate(), inviter.Code)).FeeFreeUntil);
        await SignIn("198.51.100.45", invited, "pat@example.com");
        var other = NewComputer();
        await RegisterOk("198.51.100.46", other, Friend);

        var signedIn = await SignIn("198.51.100.46", other, "pat@example.com");
        Assert.Equal(_time.GetUtcNow(), signedIn.FeeFreeUntil);
    }

    private async Task<AccountSummary> SignIn(string from, Computer computer, string email)
    {
        var client = new AccountClient(From(from));
        await client.SendEmailCodeAsync(computer.Id, computer.Secret, new EmailCodeRequest(email), CancellationToken.None);
        var outbox = _factory.Services.GetRequiredService<OutboxEmailSender>();
        var code = System.Text.RegularExpressions.Regex.Match(outbox.Sent.Last(m => m.To == email).Body, @"\b(\d{6})\b").Groups[1].Value;
        return await client.VerifyEmailCodeAsync(computer.Id, computer.Secret, new EmailVerifyRequest(email, code), CancellationToken.None);
    }

    [Fact]
    public async Task Friends_count_once_the_pool_shows_their_own_Otium_computer_mining()
    {
        var inviterComputer = NewComputer();
        var inviter = await RegisterOk("203.0.113.75", inviterComputer, Inviter);
        var friendComputer = NewComputer();
        await RegisterOk("198.51.100.47", friendComputer, Friend, inviter.Code);

        // Credited, but by workers that aren't the friend's Otium (another miner, or the inviter's own computer).
        Mined(Friend, "rig1", WorkerOf(inviterComputer));
        Assert.Equal(0, (await Summary("203.0.113.75", inviterComputer)).VerifiedCount);

        Mined(Friend, WorkerOf(friendComputer) + "g");
        _time.Advance(TimeSpan.FromHours(2)); // the pool is asked again at most hourly
        var summary = await Summary("203.0.113.75", inviterComputer);
        Assert.Equal(1, summary.VerifiedCount);
        Assert.Equal(_time.GetUtcNow() + TimeSpan.FromDays(30), summary.FeeFreeUntil);
    }

    [Fact]
    public async Task A_friend_whose_computer_used_the_inviters_connection_isnt_verified_until_a_month_later()
    {
        var inviterComputer = NewComputer();
        var inviter = await RegisterOk("203.0.113.76", inviterComputer, Inviter);
        var friendComputer = NewComputer();
        await RegisterOk("198.51.100.48", friendComputer, Friend, inviter.Code);
        Mined(Friend, WorkerOf(friendComputer));

        // Joined from elsewhere, then mined at the inviter's: most likely the inviter's own second wallet.
        await Summary("203.0.113.76", friendComputer);
        Assert.Equal(0, (await Summary("203.0.113.76", inviterComputer)).VerifiedCount);

        _time.Advance(AccountService.ConnectionMemory + TimeSpan.FromHours(1));
        Assert.Equal(1, (await Summary("203.0.113.76", inviterComputer)).VerifiedCount);
    }

    [Fact]
    public async Task Check_ins_arriving_together_reward_the_inviter_once()
    {
        var inviterComputer = NewComputer();
        var inviter = await RegisterOk("203.0.113.77", inviterComputer, Inviter);
        var friendComputer = NewComputer();
        await RegisterOk("198.51.100.49", friendComputer, Friend, inviter.Code);
        Mined(Friend, WorkerOf(friendComputer));
        _unmineable.Delay = TimeSpan.FromMilliseconds(300); // every check-in sees the friend unverified before any marks them

        var summaries = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Summary("203.0.113.77", inviterComputer)));
        Assert.All(summaries, s => Assert.Equal(1, s.VerifiedCount));
        Assert.Equal(_time.GetUtcNow() + TimeSpan.FromDays(30), Store.FindAccount(AccountIdOf(inviterComputer))!.FeeFreeUntil);
    }

    [Fact]
    public async Task Only_a_few_friends_pools_are_asked_per_check_in()
    {
        var inviterComputer = NewComputer();
        var inviter = await RegisterOk("203.0.113.78", inviterComputer, Inviter);
        for (var i = 0; i < AccountService.MaxPoolLookupsPerRefresh + 2; i++)
            await RegisterOk($"198.51.100.{100 + i}", NewComputer(), TestAddresses.Generate(), inviter.Code);

        await Summary("203.0.113.78", inviterComputer);
        Assert.Equal(AccountService.MaxPoolLookupsPerRefresh, _unmineable.AddressLookups);
        await Summary("203.0.113.78", inviterComputer); // the rest on the next check-in
        Assert.Equal(AccountService.MaxPoolLookupsPerRefresh + 2, _unmineable.AddressLookups);
    }

    [Fact]
    public async Task Friends_invited_before_an_email_sign_in_merged_the_account_are_still_verified()
    {
        // The inviter's laptop had its own account and invited a friend; then it signed in to the inviter's main account.
        var laptop = NewComputer();
        var laptopAccount = await RegisterOk("203.0.113.79", laptop, Inviter);
        var friendComputer = NewComputer();
        await RegisterOk("198.51.100.50", friendComputer, Friend, laptopAccount.Code);
        var desktop = NewComputer();
        await RegisterOk("203.0.113.79", desktop, Inviter);
        await SignIn("203.0.113.79", desktop, "sam@example.com");
        await SignIn("203.0.113.79", laptop, "sam@example.com");

        Mined(Friend, WorkerOf(friendComputer));
        var summary = await Summary("203.0.113.79", desktop);
        Assert.Equal(1, summary.InvitedCount);
        Assert.Equal(1, summary.VerifiedCount);
        Assert.NotNull(summary.FeeFreeUntil);
    }

    [Fact]
    public async Task What_friends_mined_for_the_inviter_is_summed_and_cant_be_inflated()
    {
        var inviterComputer = NewComputer();
        var inviter = await RegisterOk("203.0.113.80", inviterComputer, Inviter);
        var friendA = NewComputer();
        var friendB = NewComputer();
        var unverified = NewComputer();
        await RegisterOk("198.51.100.40", friendA, Friend, inviter.Code);
        await RegisterOk("198.51.100.41", friendB, OtherFriend, inviter.Code);
        await RegisterOk("198.51.100.43", unverified, ThirdFriend, inviter.Code);
        Store.MarkReferralVerified(AccountIdOf(friendA), _time.GetUtcNow());
        Store.MarkReferralVerified(AccountIdOf(friendB), _time.GetUtcNow());
        var stranger = NewComputer();
        await RegisterOk("198.51.100.42", stranger, "USDT:TJCnKsPa7y5okkXvQAidZBzqx3QyQ6sxMW");

        async Task Report(Computer c, string from, double usd) =>
            await new AccountClient(From(from)).ReportReferralShareAsync(c.Id, c.Secret, new ReferralShareReport(usd), CancellationToken.None);
        async Task<double> Earned() => (await Summary("203.0.113.80", inviterComputer)).ReferralEarnedUsd;

        _time.Advance(TimeSpan.FromDays(4));
        await Report(friendA, "198.51.100.40", 0.12);
        await Report(friendB, "198.51.100.41", 0.08);
        Assert.Equal(0.20, await Earned(), precision: 6);

        await Report(friendA, "198.51.100.40", 0.05); // an older, lower total doesn't take anything away
        Assert.Equal(0.20, await Earned(), precision: 6);

        // Nobody's computer mines more than $0.50 a day for its inviter: bigger claims are cut to that.
        await Report(friendB, "198.51.100.41", 1_000);
        var expected = 0.12 + 4 * AccountService.MaxReferralShareUsdPerDay;
        Assert.Equal(expected, await Earned(), precision: 6);

        await Report(stranger, "198.51.100.42", 5); // not invited by anyone: ignored
        await Report(unverified, "198.51.100.43", 0.3); // invited, but the pool hasn't shown them mining: not counted
        Assert.Equal(expected, await Earned(), precision: 6);

        // The inviter's share lasts 6 months: reports after that are for nobody.
        _time.Advance(TimeSpan.FromDays(180));
        await Report(friendA, "198.51.100.40", 50);
        Assert.Equal(expected, await Earned(), precision: 6);
    }

    public void Dispose()
    {
        _factory.Dispose();
        foreach (var file in new[] { _dbPath, _dbPath + "-wal", _dbPath + "-shm" })
            File.Delete(file);
    }
}
