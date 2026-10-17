using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Otium.Core.Accounts;
using Otium.Core.Bandwidth;
using Otium.Core.Platform;
using Otium.Core.Pools;
using Otium.Core.Providers;
using Otium.Providers.Mysterium;
using Otium.Server.Bandwidth;

namespace Otium.Tests;

/// <summary>The Mysterium node's local API, as captured from node 1.39.7 in October 2026.</summary>
public class MysteriumNodeTests
{
    private static JsonElement Json(string json) => JsonDocument.Parse(json).RootElement.Clone();

    [Fact]
    public void Identity_reads_registration_channel_and_earnings()
    {
        var identity = TequilaClient.ParseIdentity(Json("""
            {"id":"0x8371fdd3293773e1082dc2616a432e557d1f712d","registration_status":"Unregistered",
             "channel_address":"0x40619B07eC4F7007697D7De77AB9b09516f324E6","balance":0,"earnings":0,"earnings_total":0,
             "balance_tokens":{"wei":"0","ether":"0","human":"0"},
             "earnings_tokens":{"wei":"420000000000000000","ether":"0.42","human":"0.42"},
             "earnings_total_tokens":{"wei":"1500000000000000000","ether":"1.5","human":"1.5"},"stake":0,
             "hermes_id":"0x80Ed28d84792d8b153bf2F25F0C4B7a1381dE4ab","earnings_per_hermes":{}}
            """));

        Assert.Equal("0x8371fdd3293773e1082dc2616a432e557d1f712d", identity.Id);
        Assert.False(identity.IsRegistered);
        Assert.Equal("0x40619B07eC4F7007697D7De77AB9b09516f324E6", identity.ChannelAddress);
        Assert.Equal(0.42m, identity.EarningsMyst);
        Assert.Equal(1.5m, identity.EarningsTotalMyst);
        Assert.Contains("1.5 MYST earned", MysteriumBandwidthProvider.EarningsText(identity));
        Assert.Contains("5 MYST", MysteriumBandwidthProvider.EarningsText(identity));
    }

    [Fact]
    public void Relayed_bytes_are_sent_plus_received()
    {
        Assert.Equal(3_000, TequilaClient.ParseRelayedBytes(Json("""
            {"stats":{"count":2,"count_consumers":1,"sum_bytes_received":1000,"sum_bytes_sent":2000,"sum_duration":60,"sum_tokens":0}}
            """)));
    }

    [Fact]
    public void The_node_runs_headless_with_its_api_on_loopback_only()
    {
        var args = MysteriumBandwidthProvider.DaemonArguments(@"C:\otium\engines\myst-data", 41234, "secret");

        Assert.Contains("--ui.enable=false", args);
        Assert.Contains("--tequilapi.address=127.0.0.1", args);
        Assert.Contains("--tequilapi.port=41234", args);
        Assert.Contains("--tequilapi.auth.password=secret", args);
        Assert.Contains("--usermode", args);
        Assert.Equal("daemon", args[^1]);
        Assert.DoesNotContain(args, a => a.Contains("agreed-terms", StringComparison.Ordinal)); // terms come from Otium's consent
        Assert.Equal(["scraping", "data_transfer", "dvpn"], MysteriumBandwidthProvider.B2BServices);
    }

    [Fact]
    public void Only_windows_x64_is_offered_for_now()
    {
        Assert.NotNull(MystRelease.For(System.Runtime.InteropServices.OSPlatform.Windows, System.Runtime.InteropServices.Architecture.X64));
        Assert.Null(MystRelease.For(System.Runtime.InteropServices.OSPlatform.Linux, System.Runtime.InteropServices.Architecture.X64));
        Assert.Equal(64, MystRelease.For(System.Runtime.InteropServices.OSPlatform.Windows, System.Runtime.InteropServices.Architecture.X64)!.Sha256.Length);
    }

    [Fact]
    public async Task Sharing_paid_by_the_partner_sends_no_reports_and_shows_its_earnings()
    {
        var time = new ManualTime(new DateTimeOffset(2026, 10, 17, 12, 0, 0, TimeSpan.Zero));
        var provider = new PayingProvider();
        var ledger = new BandwidthLedger();
        var config = new BandwidthConfig(true, false, TimeSpan.FromMinutes(3), 1024L * 1024 * 1024, 0, 0, "");
        await using var sharer = new BandwidthSharer(provider, new AlwaysAway(), () => config, report: null, ledger, _ => { }, time);
        sharer.Enable();

        await sharer.TickAsync(CancellationToken.None);
        provider.Bytes = 5_000_000;
        time.Advance(TimeSpan.FromMinutes(10));
        await sharer.TickAsync(CancellationToken.None);
        time.Advance(TimeSpan.FromMinutes(10));
        await sharer.TickAsync(CancellationToken.None);

        Assert.Equal(BandwidthState.Sharing, sharer.Snapshot.State);
        Assert.Equal(5_000_000, sharer.Snapshot.TotalBytes);
        Assert.Equal("0.3 MYST earned", sharer.Snapshot.Earnings);
        Assert.Null(ledger.Pending); // nothing waits to be reported
        Assert.Equal(1, ledger.NextSequence);
    }

    private sealed class PayingProvider : IBandwidthProvider
    {
        public long Bytes { get; set; }

        public string Id => "mysterium";

        public string DisplayName => "Paying partner";

        public string? UnavailableReason => null;

        public bool IsInstalled => true;

        public Task InstallAsync(IProgress<InstallProgress>? progress, CancellationToken ct) => Task.CompletedTask;

        public Task StartAsync(BandwidthRunOptions options, CancellationToken ct) => Task.CompletedTask;

        public Task StopAsync(CancellationToken ct) => Task.CompletedTask;

        public Task<BandwidthStatus> GetStatusAsync(CancellationToken ct) =>
            Task.FromResult(new BandwidthStatus(BandwidthProviderState.Online, Bytes, null, "0.3 MYST earned"));

        public Task OptInAsync(CancellationToken ct) => Task.CompletedTask;

        public Task OptOutAsync(CancellationToken ct) => Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class AlwaysAway : IIdleDetector
    {
        public TimeSpan? GetIdleTime() => TimeSpan.FromHours(1);
    }
}

/// <summary>Otium paying new Mysterium nodes' registration fee from its own wallet.</summary>
public sealed class MysteriumSponsorTests : IDisposable
{
    private const string Channel = "0x40619b07ec4f7007697d7de77ab9b09516f324e6";
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"otium-myst-{Guid.NewGuid():N}.db");
    private readonly FakeMystWallet _wallet = new();
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _http;

    public MysteriumSponsorTests()
    {
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Otium", $"Data Source={_dbPath};Pooling=False");
            builder.UseSetting("DisableBackgroundJobs", "true");
            builder.UseSetting("Mysterium:Enabled", "true");
            builder.UseSetting("Mysterium:AmountMyst", "0.15");
            builder.UseSetting("Mysterium:DailyBudgetMyst", "0.5");
            builder.UseSetting("Mysterium:MaxPerAccount", "2");
            builder.UseSetting("Mysterium:MaxPerIpPerDay", "10");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IMoneroPoolApi>();
                services.AddSingleton<IMoneroPoolApi>(new GpuPolicyTests.FakePool());
                services.RemoveAll<IMystWallet>();
                services.AddSingleton<IMystWallet>(_wallet);
            });
        });
        _http = _factory.CreateClient();
    }

    private async Task<(string Device, string Secret, AccountSummary Summary)> Register()
    {
        var secret = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
        var device = Guid.NewGuid().ToString("N");
        var response = await _http.PostAsJsonAsync("/api/v1/devices", new RegisterDeviceRequest(device, secret, "USDT:TR7NHqjeKQxGTCi8q8ZY4pL8otSzgjLj6t", null));
        return (device, secret, (await response.Content.ReadFromJsonAsync<AccountSummary>())!);
    }

    private static string Identity(int n) => "0x" + n.ToString("x40", System.Globalization.CultureInfo.InvariantCulture);

    private Task<MystSponsorResult> Sponsor(string device, string secret, int identity, string channel = Channel) =>
        new AccountClient(_http).SponsorMysteriumAsync(device, secret, new MystSponsorRequest(Identity(identity), channel), CancellationToken.None);

    [Fact]
    public async Task A_new_node_gets_its_fee_once()
    {
        var (device, secret, summary) = await Register();
        Assert.True(summary.MysteriumSponsored);

        var first = await Sponsor(device, secret, 1);
        Assert.Equal(MystSponsorStatuses.Sent, first.Status);
        Assert.Equal(0.15m, first.AmountMyst);
        Assert.Equal((Channel, 0.15m), Assert.Single(_wallet.Sent));

        // The app asks again after a restart: the fee was already sent, so it just registers.
        var again = await Sponsor(device, secret, 1);
        Assert.Equal(MystSponsorStatuses.AlreadySent, again.Status);
        Assert.Equal(first.TransactionId, again.TransactionId);
        Assert.Single(_wallet.Sent);

        // A second identity from the same computer isn't paid for.
        var other = await Sponsor(device, secret, 2);
        Assert.Equal(MystSponsorStatuses.Unavailable, other.Status);
        Assert.Contains("this computer", other.Message);
    }

    [Fact]
    public async Task The_daily_budget_caps_spending()
    {
        for (var n = 1; n <= 3; n++)
        {
            var (device, secret, _) = await Register();
            var result = await Sponsor(device, secret, n);
            Assert.Equal(n <= 3 ? MystSponsorStatuses.Sent : MystSponsorStatuses.Unavailable, result.Status);
        }
        var (lastDevice, lastSecret, _) = await Register();
        var over = await Sponsor(lastDevice, lastSecret, 4); // 4 × 0.15 > 0.5 MYST
        Assert.Equal(MystSponsorStatuses.Unavailable, over.Status);
        Assert.Equal(3, _wallet.Sent.Count);
    }

    [Fact]
    public async Task Bad_addresses_are_refused()
    {
        var (device, secret, _) = await Register();
        var error = await Assert.ThrowsAsync<AccountRequestException>(() => Sponsor(device, secret, 1, "not-an-address"));
        Assert.Equal((int)HttpStatusCode.BadRequest, error.StatusCode);
        Assert.Empty(_wallet.Sent);
    }

    [Fact]
    public async Task Without_a_sponsor_wallet_sharing_isnt_offered()
    {
        _wallet.Configured = false;
        var (device, secret, summary) = await Register();
        Assert.False(summary.MysteriumSponsored);
        Assert.Equal(MystSponsorStatuses.Unavailable, (await Sponsor(device, secret, 1)).Status);
    }

    [Fact]
    public async Task A_failed_transfer_is_retried_later_not_recorded()
    {
        var (device, secret, _) = await Register();
        _wallet.Fail = true;
        Assert.Equal(MystSponsorStatuses.Unavailable, (await Sponsor(device, secret, 1)).Status);
        _wallet.Fail = false;
        Assert.Equal(MystSponsorStatuses.Sent, (await Sponsor(device, secret, 1)).Status);
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

    private sealed class FakeMystWallet : IMystWallet
    {
        public List<(string To, decimal Amount)> Sent { get; } = [];

        public bool Configured { get; set; } = true;

        public bool Fail { get; set; }

        public bool IsConfigured => Configured;

        public Task<string> SendAsync(string to, decimal amountMyst, CancellationToken ct)
        {
            if (Fail)
                throw new HttpRequestException("Polygon RPC down");
            Sent.Add((to, amountMyst));
            return Task.FromResult("0x" + Sent.Count.ToString("x64", System.Globalization.CultureInfo.InvariantCulture));
        }

        public Task<decimal> GetBalanceAsync(CancellationToken ct) => Task.FromResult(10m);
    }
}
