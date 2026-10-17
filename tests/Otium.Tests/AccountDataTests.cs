using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Otium.Core.Accounts;
using Otium.Core.Pools;
using Otium.Server.Accounts;
using Otium.Server.Data;

namespace Otium.Tests;

/// <summary>Exporting and deleting account data (the GDPR rights), through the real API.</summary>
public sealed class AccountDataTests : IDisposable
{
    private const string AdminToken = "test-admin-token";

    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"otium-data-{Guid.NewGuid():N}.db");
    private readonly ManualTime _time = new(DateTimeOffset.UtcNow);
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _http;
    private readonly AccountClient _client;

    public AccountDataTests()
    {
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Otium", $"Data Source={_dbPath};Pooling=False");
            builder.UseSetting("Custody:Enabled", "true");
            builder.UseSetting("DisableBackgroundJobs", "true");
            builder.UseSetting("Earnings:PoolWallets:0", TestAddresses.Generate());
            builder.UseSetting("Admin:Token", AdminToken);
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IMoneroPoolApi>();
                services.AddSingleton<IMoneroPoolApi>(new FakePool());
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(_time);
            });
        });
        _http = _factory.CreateClient();
        _client = new AccountClient(_http);
    }

    private sealed class FakePool : IMoneroPoolApi
    {
        public Task<MoneroNetworkStats> GetNetworkStatsAsync(CancellationToken ct) =>
            Task.FromResult(new MoneroNetworkStats(1, 1, 1, DateTimeOffset.UtcNow));

        public Task<PoolMinerStats?> GetMinerStatsAsync(string walletAddress, CancellationToken ct) =>
            Task.FromResult<PoolMinerStats?>(new PoolMinerStats(0, 0, 0, 0));

        public Task<AlgoProfits> GetAlgoProfitsAsync(CancellationToken ct) =>
            Task.FromResult(new AlgoProfits(new Dictionary<string, double>(), 1, DateTimeOffset.UtcNow));

        public Task<IReadOnlyDictionary<string, IReadOnlyList<HashratePoint>>> GetWorkerHistoryAsync(string walletAddress, CancellationToken ct) =>
            Task.FromResult<IReadOnlyDictionary<string, IReadOnlyList<HashratePoint>>>(new Dictionary<string, IReadOnlyList<HashratePoint>>());
    }

    private sealed record Device(string Id, string Secret);

    private OtiumStore Store => _factory.Services.GetRequiredService<OtiumStore>();

    private OutboxEmailSender Outbox => _factory.Services.GetRequiredService<OutboxEmailSender>();

    private DateTimeOffset Now => _time.GetUtcNow();

    private static Device NewDevice() => new(Guid.NewGuid().ToString("N"), Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32)));

    private async Task<AccountSummary> Register(Device d, string? code = null)
    {
        var response = await _http.PostAsJsonAsync("/api/v1/devices", new RegisterDeviceRequest(d.Id, d.Secret, null, code));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AccountSummary>())!;
    }

    private string LatestCode(string email) =>
        System.Text.RegularExpressions.Regex.Match(Outbox.Sent.Last(m => m.To == email).Body, @"\b(\d{6})\b").Groups[1].Value;

    private async Task SignIn(Device device, string email)
    {
        await _client.SendEmailCodeAsync(device.Id, device.Secret, new EmailCodeRequest(email), CancellationToken.None);
        await _client.VerifyEmailCodeAsync(device.Id, device.Secret, new EmailVerifyRequest(email, LatestCode(email)), CancellationToken.None);
    }

    private async Task<string> RequestDeleteCode(Device device, string email)
    {
        await _client.SendEmailCodeAsync(device.Id, device.Secret, new EmailCodeRequest(email, EmailCodePurposes.DeleteAccount), CancellationToken.None);
        return LatestCode(email);
    }

    private Task<DeleteAccountResult> Delete(Device device, string? code, bool giveUpBalance = false) =>
        _client.DeleteAccountAsync(device.Id, device.Secret, new DeleteAccountRequest(code, giveUpBalance), CancellationToken.None);

    private AccountRecord AccountOf(AccountSummary summary) => Store.FindAccountByWorker(summary.WorkerId)!;

    private void Fund(AccountRecord account, decimal usd) =>
        Store.AddLedger(account.AccountId, LedgerKinds.Earning, AccountService.ToMicros(usd), Now, Now, "test");

    private async Task<JsonElement> AdminSummary()
    {
        using var message = new HttpRequestMessage(HttpMethod.Get, "/admin/summary");
        message.Headers.Add("X-Admin-Token", AdminToken);
        using var response = await _http.SendAsync(message);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).Clone();
    }

    [Fact]
    public async Task Export_contains_the_account_devices_money_referrals_and_bandwidth()
    {
        var carol = NewDevice();
        var carolSummary = await Register(carol);
        var alice = NewDevice();
        var aliceSummary = await Register(alice, carolSummary.Code);
        await SignIn(alice, "alice@example.com");
        var bob = NewDevice();
        var bobSummary = await Register(bob, aliceSummary.Code);

        var account = AccountOf(aliceSummary);
        Fund(account, 6m);
        Store.AddLedger(account.AccountId, LedgerKinds.Referral, 250_000, Now, Now, $"ref:{bobSummary.WorkerId}@123");
        Store.DebitAndCreateCashout(account.AccountId, CashoutMethods.PayPal, "alice.pay@example.com", 0, CashoutStatuses.Paid, Now);
        Store.AddBandwidthReport(new BandwidthReportRecord(alice.Id, 1, account.AccountId, "203.0.113.7", Now.AddMinutes(-5), Now,
            1_000_000, 300, 1_000_000, 50_000, BandwidthReportStatuses.Credited, Now));

        using var message = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/devices/{alice.Id}/account/export");
        message.Headers.Add(AccountClient.SecretHeader, alice.Secret);
        using var response = await _http.SendAsync(message);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.StartsWith("otium-data-", response.Content.Headers.ContentDisposition?.FileNameStar ?? response.Content.Headers.ContentDisposition?.FileName);

        var text = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(text).RootElement;
        Assert.Equal(AccountDataService.ExportFormat, json.GetProperty("format").GetString());
        Assert.Equal("alice@example.com", json.GetProperty("account").GetProperty("email").GetString());
        Assert.Equal(aliceSummary.Code, json.GetProperty("account").GetProperty("referralCode").GetString());

        var device = Assert.Single(json.GetProperty("devices").EnumerateArray());
        Assert.Equal(alice.Id, device.GetProperty("deviceId").GetString());
        Assert.True(device.GetProperty("thisComputer").GetBoolean());

        var kinds = json.GetProperty("ledger").EnumerateArray().Select(e => e.GetProperty("kind").GetString()).ToList();
        Assert.Equal([LedgerKinds.Earning, LedgerKinds.Referral, LedgerKinds.Cashout], kinds);
        Assert.Equal(-6.25m, json.GetProperty("ledger")[2].GetProperty("amountUsd").GetDecimal());

        var cashout = Assert.Single(json.GetProperty("cashouts").EnumerateArray());
        Assert.Equal("alice.pay@example.com", cashout.GetProperty("destination").GetString()); // the owner sees it in full

        var referrals = json.GetProperty("referrals");
        Assert.True(referrals.GetProperty("joinedWithInvite").GetBoolean());
        Assert.Single(referrals.GetProperty("invitedFriends").EnumerateArray());

        var report = Assert.Single(json.GetProperty("bandwidthReports").EnumerateArray());
        Assert.Equal("203.0.113.7", report.GetProperty("ip").GetString());
        Assert.Equal(0.05m, report.GetProperty("creditedUsd").GetDecimal());

        // Nothing that identifies the people on either side of an invite.
        Assert.DoesNotContain(carolSummary.Code, text);
        Assert.DoesNotContain(carolSummary.WorkerId, text);
        Assert.DoesNotContain(bobSummary.Code, text);
        Assert.DoesNotContain(bobSummary.WorkerId, text);
    }

    [Fact]
    public async Task Export_needs_the_device_secret()
    {
        var alice = NewDevice();
        await Register(alice);
        var e = await Assert.ThrowsAsync<AccountRequestException>(() =>
            _client.ExportAccountDataAsync(alice.Id, new string('f', 64), CancellationToken.None));
        Assert.Equal(401, e.StatusCode);
    }

    [Fact]
    public async Task Deletion_is_refused_while_a_cashout_is_open()
    {
        var alice = NewDevice();
        var summary = await Register(alice);
        var account = AccountOf(summary);
        Fund(account, 6m);
        Store.DebitAndCreateCashout(account.AccountId, CashoutMethods.PayPal, "alice@example.com", 0, CashoutStatuses.Review, Now);

        var e = await Assert.ThrowsAsync<AccountRequestException>(() => Delete(alice, null, giveUpBalance: true));
        Assert.Equal(409, e.StatusCode);
        Assert.Contains("cash-out is still on its way", e.Message);
        Assert.Null(Store.FindAccount(account.AccountId)!.DeletedAt);
    }

    [Fact]
    public async Task Deletion_needs_a_fresh_code_sent_to_the_accounts_email()
    {
        var alice = NewDevice();
        var summary = await Register(alice);
        await SignIn(alice, "alice@example.com");

        var noCode = await Assert.ThrowsAsync<AccountRequestException>(() => Delete(alice, null));
        Assert.Equal(400, noCode.StatusCode);

        // A deletion code only goes to the account's own email.
        var otherEmail = await Assert.ThrowsAsync<AccountRequestException>(() =>
            _client.SendEmailCodeAsync(alice.Id, alice.Secret, new EmailCodeRequest("mallory@example.com", EmailCodePurposes.DeleteAccount), CancellationToken.None));
        Assert.Equal(400, otherEmail.StatusCode);

        var right = await RequestDeleteCode(alice, "alice@example.com");
        Assert.Contains("delete", Outbox.Sent.Last().Subject);
        var wrong = right == "000000" ? "111111" : "000000";
        var e = await Assert.ThrowsAsync<AccountRequestException>(() => Delete(alice, wrong));
        Assert.Equal(400, e.StatusCode);
        Assert.Contains("isn't right", e.Message);

        // Still there and usable.
        var account = AccountOf(summary);
        Assert.Null(account.DeletedAt);
        Assert.Equal("alice@example.com", account.Email);
        Assert.Equal("alice@example.com", (await _client.GetSummaryAsync(alice.Id, alice.Secret, CancellationToken.None)).Email);

        // An expired code doesn't work either.
        _time.Advance(AccountService.CodeLifetime + TimeSpan.FromMinutes(1));
        var expired = await Assert.ThrowsAsync<AccountRequestException>(() => Delete(alice, right));
        Assert.Contains("expired", expired.Message);
    }

    [Fact]
    public async Task Deletion_warns_about_a_remaining_balance()
    {
        var alice = NewDevice();
        var summary = await Register(alice);
        Fund(AccountOf(summary), 3m);

        var e = await Assert.ThrowsAsync<AccountRequestException>(() => Delete(alice, null));
        Assert.Equal(409, e.StatusCode);
        Assert.Contains("$3.00", e.Message);

        var result = await Delete(alice, null, giveUpBalance: true);
        Assert.Equal(3m, result.GivenUpUsd);
    }

    [Fact]
    public async Task Deletion_anonymizes_the_account_but_keeps_the_money_records()
    {
        var carol = NewDevice();
        var carolSummary = await Register(carol);
        var alice = NewDevice();
        var aliceSummary = await Register(alice, carolSummary.Code);
        await SignIn(alice, "alice@example.com");
        var bob = NewDevice();
        var bobSummary = await Register(bob, aliceSummary.Code);

        var account = AccountOf(aliceSummary);
        var carolAccount = AccountOf(carolSummary);
        Fund(account, 8m);
        Store.AddLedger(carolAccount.AccountId, LedgerKinds.Referral, 400_000, Now, Now, $"ref:{aliceSummary.WorkerId}@1");
        Store.DebitAndCreateCashout(account.AccountId, CashoutMethods.PayPal, "alice.pay@example.com", 0, CashoutStatuses.Paid, Now);
        Fund(account, 1.25m);
        Store.AddBandwidthReport(new BandwidthReportRecord(alice.Id, 1, account.AccountId, "203.0.113.7", Now.AddMinutes(-5), Now,
            1_000_000, 300, 1_000_000, 50_000, BandwidthReportStatuses.Credited, Now));
        var ledgerBefore = Store.LedgerFor(account.AccountId);
        var carolBalanceBefore = Store.Balance(carolAccount.AccountId, Now);
        var totalsBefore = await AdminSummary();

        var code = await RequestDeleteCode(alice, "alice@example.com");
        var result = await Delete(alice, code, giveUpBalance: true);
        Assert.Equal(1.25m, result.GivenUpUsd);

        // Nothing that points to Alice is left on her account row.
        var deleted = Store.FindAccount(account.AccountId)!;
        Assert.Equal(Now, deleted.DeletedAt);
        Assert.Null(deleted.Email);
        Assert.Null(deleted.PoolWallet);
        Assert.Null(deleted.ReferredByCode);
        Assert.Null(Store.FindAccountByEmail("alice@example.com"));
        Assert.Null(Store.FindAccountByWorker(aliceSummary.WorkerId));
        Assert.Null(Store.FindAccountByCode(aliceSummary.Code));
        Assert.Null(Store.FindDevice(alice.Id));
        var e = await Assert.ThrowsAsync<AccountRequestException>(() => _client.GetSummaryAsync(alice.Id, alice.Secret, CancellationToken.None));
        Assert.Equal(404, e.StatusCode);

        // Money rows stay for the books, with the destination masked.
        Assert.Equal(ledgerBefore, Store.LedgerFor(account.AccountId));
        var cashout = Assert.Single(Store.CashoutsFor(account.AccountId));
        Assert.DoesNotContain("alice", cashout.Destination);
        Assert.Equal("•••@•••.com", cashout.Destination);
        Assert.Equal(8_000_000, cashout.Micros);
        Assert.Equal(CashoutStatuses.Paid, cashout.Status);
        var report = Store.BandwidthReportsFor(account.AccountId).Single();
        Assert.Equal("", report.Ip);
        Assert.NotEqual(alice.Id, report.DeviceId);

        var totalsAfter = await AdminSummary();
        foreach (var total in new[] { "creditedToUsersUsd", "referralRewardsUsd", "paidOutUsd", "owedToUsersUsd", "bandwidthCreditedToUsersUsd" })
            Assert.Equal(totalsBefore.GetProperty(total).GetDecimal(), totalsAfter.GetProperty(total).GetDecimal());

        // Referrals are detached: the referrer keeps her rewards, the invited friend keeps his history.
        Assert.Equal(carolBalanceBefore, Store.Balance(carolAccount.AccountId, Now));
        Assert.Equal(0, (await _client.GetSummaryAsync(carol.Id, carol.Secret, CancellationToken.None)).InvitedCount);
        var bobAfter = AccountOf(bobSummary);
        Assert.Null(bobAfter.ReferredByCode);
        Assert.NotNull(bobAfter.ReferredAt);

        // The same computer can start over with a brand-new, empty account.
        var fresh = await Register(alice);
        Assert.NotEqual(aliceSummary.WorkerId, fresh.WorkerId);
        Assert.Null(fresh.Email);
        Assert.Equal(0m, fresh.Balance.AvailableUsd);
    }

    [Fact]
    public async Task Accounts_without_an_email_are_deleted_without_a_code()
    {
        var alice = NewDevice();
        var accountId = AccountOf(await Register(alice)).AccountId;

        var result = await Delete(alice, null);

        Assert.Equal(0m, result.GivenUpUsd);
        Assert.Equal(Now, Store.FindAccount(accountId)!.DeletedAt);
        Assert.Null(Store.FindDevice(alice.Id));
    }

    public void Dispose()
    {
        _http.Dispose();
        _factory.Dispose();
        if (File.Exists(_dbPath))
            File.Delete(_dbPath);
    }
}
