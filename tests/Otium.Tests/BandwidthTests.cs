using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Otium.Core.Accounts;
using Otium.Core.Bandwidth;
using Otium.Core.Platform;
using Otium.Core.Pools;
using Otium.Core.Providers;
using Otium.Providers.Honeygain;

namespace Otium.Tests;

/// <summary>The server's crediting of internet sharing, and its limits on self-reported usage.</summary>
public sealed class BandwidthServerTests : IDisposable
{
    private const long MB = 1024 * 1024;
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"otium-bw-{Guid.NewGuid():N}.db");
    private readonly ManualTime _time = new(new DateTimeOffset(2026, 10, 15, 12, 0, 0, TimeSpan.Zero));
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _http;

    public BandwidthServerTests() : this(enabled: true)
    {
    }

    private BandwidthServerTests(bool enabled)
    {
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Otium", $"Data Source={_dbPath};Pooling=False");
            builder.UseSetting("Custody:Enabled", "true");
            builder.UseSetting("DisableBackgroundJobs", "true");
            builder.UseSetting("Earnings:PoolWallets:0", TestAddresses.Generate());
            builder.UseSetting("Earnings:FeePercent", "10");
            builder.UseSetting("Bandwidth:Enabled", enabled ? "true" : "false");
            builder.UseSetting("Bandwidth:PartnerName", "Test partner");
            builder.UseSetting("Bandwidth:UsdPerGb", "1");
            builder.UseSetting("Bandwidth:UsdPerSharingHour", "0.01");
            builder.UseSetting("Bandwidth:DailyLimitBytes", (100 * MB).ToString(System.Globalization.CultureInfo.InvariantCulture));
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IMoneroPoolApi>();
                services.AddSingleton<IMoneroPoolApi>(new NoPool());
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(_time);
            });
        });
        _http = _factory.CreateClient();
    }

    private sealed class NoPool : IMoneroPoolApi
    {
        public Task<MoneroNetworkStats> GetNetworkStatsAsync(CancellationToken ct) => Task.FromResult(new MoneroNetworkStats(1, 1, 1, DateTimeOffset.UtcNow));

        public Task<PoolMinerStats?> GetMinerStatsAsync(string walletAddress, CancellationToken ct) => Task.FromResult<PoolMinerStats?>(null);

        public Task<AlgoProfits> GetAlgoProfitsAsync(CancellationToken ct) =>
            Task.FromResult(new AlgoProfits(new Dictionary<string, double>(), 1, DateTimeOffset.UtcNow));

        public Task<IReadOnlyDictionary<string, IReadOnlyList<HashratePoint>>> GetWorkerHistoryAsync(string walletAddress, CancellationToken ct) =>
            Task.FromResult<IReadOnlyDictionary<string, IReadOnlyList<HashratePoint>>>(new Dictionary<string, IReadOnlyList<HashratePoint>>());
    }

    private sealed record Device(string Id, string Secret);

    private async Task<Device> NewBalanceDevice(string? ownWallet = null)
    {
        var d = new Device(Guid.NewGuid().ToString("N"), Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32)));
        var response = await _http.PostAsJsonAsync("/api/v1/devices", new RegisterDeviceRequest(d.Id, d.Secret, ownWallet, null));
        response.EnsureSuccessStatusCode();
        return d;
    }

    private Task<BandwidthReportResult> Report(Device d, long seq, long bytes, double seconds = 300) =>
        new AccountClient(_http).ReportBandwidthAsync(d.Id, d.Secret,
            new BandwidthReport(seq, _time.GetUtcNow() - TimeSpan.FromSeconds(seconds), _time.GetUtcNow(), bytes, seconds, "simulated"),
            CancellationToken.None);

    private Task<AccountSummary> Summary(Device d) => new AccountClient(_http).GetSummaryAsync(d.Id, d.Secret, CancellationToken.None);

    [Fact]
    public async Task Summary_announces_the_terms_after_the_fee()
    {
        var bandwidth = (await Summary(await NewBalanceDevice())).Bandwidth!;

        Assert.True(bandwidth.Available);
        Assert.Equal(0.9, bandwidth.UserUsdPerGb, 6);
        Assert.Equal(0.009, bandwidth.UserUsdPerSharingHour, 6);
        Assert.Equal("Test partner", bandwidth.PartnerName);
    }

    [Fact]
    public async Task Own_wallet_accounts_are_not_offered_sharing()
    {
        var device = await NewBalanceDevice(TestAddresses.Generate());
        Assert.False((await Summary(device)).Bandwidth!.Available);
        await Assert.ThrowsAsync<AccountRequestException>(() => Report(device, 1, MB));
    }

    [Fact]
    public async Task Reported_usage_is_credited_after_the_fee_and_held()
    {
        var device = await NewBalanceDevice();

        var result = await Report(device, 1, 64 * MB, seconds: 600);

        // 1/16 GB × $1 + 1/6 h × $0.01, minus 10%.
        Assert.Equal(BandwidthReportStatuses.Credited, result.Status);
        Assert.Equal(64 * MB, result.CreditedBytes);
        Assert.Equal(0.9 * (0.0625 + 0.01 / 6), (double)result.CreditedUsd, 5);
        var balance = (await Summary(device)).Balance;
        Assert.Equal(0m, balance.AvailableUsd);
        Assert.Equal(0.05m, balance.PendingUsd);
    }

    [Fact]
    public async Task A_retried_report_is_credited_once()
    {
        var device = await NewBalanceDevice();

        var first = await Report(device, 7, 100 * MB / 2);
        var again = await Report(device, 7, 100 * MB / 2);

        Assert.Equal(first.CreditedUsd, again.CreditedUsd);
        var expected = AccountServiceUsd(first.CreditedUsd);
        Assert.Equal(expected, (await Summary(device)).Balance.PendingUsd);
    }

    [Fact]
    public async Task Only_one_computer_per_connection_is_paid()
    {
        // The test server sees every client at the same address, like two PCs behind one home router.
        var first = await NewBalanceDevice();
        var second = await NewBalanceDevice();

        Assert.Equal(BandwidthReportStatuses.Credited, (await Report(first, 1, MB)).Status);
        var refused = await Report(second, 1, MB);
        Assert.Equal(BandwidthReportStatuses.SharedConnection, refused.Status);
        Assert.Equal(0m, refused.CreditedUsd);
        Assert.Contains("Only one computer", refused.Note);

        // Once the first stops sharing for a while, the second can take over.
        _time.Advance(TimeSpan.FromMinutes(20));
        Assert.Equal(BandwidthReportStatuses.Credited, (await Report(second, 2, MB)).Status);
    }

    [Fact]
    public async Task The_daily_limit_caps_credited_bytes()
    {
        var device = await NewBalanceDevice();

        // Spread over 12 minutes so the speed cap (2 MB/s) doesn't apply first.
        var first = await Report(device, 1, 80 * MB, seconds: 720);
        var second = await Report(device, 2, 80 * MB, seconds: 720);

        Assert.Equal(80 * MB, first.CreditedBytes);
        Assert.Equal(BandwidthReportStatuses.DailyLimit, second.Status);
        Assert.Equal(20 * MB, second.CreditedBytes);
    }

    [Fact]
    public async Task Implausible_speed_and_long_periods_are_capped()
    {
        var device = await NewBalanceDevice();

        // 10 seconds can't carry 50 MB at 2 MB/s.
        var result = await Report(device, 1, 50 * MB, seconds: 10);

        Assert.Equal(20 * MB, result.CreditedBytes);
    }

    [Fact]
    public async Task Reports_far_in_the_future_are_rejected()
    {
        var device = await NewBalanceDevice();
        var future = new BandwidthReport(1, _time.GetUtcNow() + TimeSpan.FromHours(1), _time.GetUtcNow() + TimeSpan.FromHours(2), MB, 60, "simulated");

        var error = await Assert.ThrowsAsync<AccountRequestException>(() =>
            new AccountClient(_http).ReportBandwidthAsync(device.Id, device.Secret, future, CancellationToken.None));
        Assert.Equal(400, error.StatusCode);
    }

    [Fact]
    public async Task Reports_need_the_device_secret()
    {
        var device = await NewBalanceDevice();
        var error = await Assert.ThrowsAsync<AccountRequestException>(() =>
            new AccountClient(_http).ReportBandwidthAsync(device.Id, new string('0', 64),
                new BandwidthReport(1, _time.GetUtcNow().AddMinutes(-5), _time.GetUtcNow(), MB, 300, "simulated"), CancellationToken.None));
        Assert.NotEqual(200, error.StatusCode);
    }

    [Fact]
    public async Task Sharing_is_refused_while_the_operator_has_it_off()
    {
        using var disabled = new BandwidthServerTests(enabled: false);
        var device = await disabled.NewBalanceDevice();

        Assert.False((await disabled.Summary(device)).Bandwidth!.Available);
        var error = await Assert.ThrowsAsync<AccountRequestException>(() => disabled.Report(device, 1, MB));
        Assert.Equal((int)HttpStatusCode.Forbidden, error.StatusCode);
    }

    /// <summary>Balances are shown rounded down to cents.</summary>
    private static decimal AccountServiceUsd(decimal usd) => decimal.Round(usd, 2, MidpointRounding.ToZero);

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
}

/// <summary>The client-side engine: when it shares, and how usage reaches the server.</summary>
public sealed class BandwidthSharerTests
{
    private const long MB = 1024 * 1024;
    private readonly ManualTime _time = new(new DateTimeOffset(2026, 10, 15, 12, 0, 0, TimeSpan.Zero));
    private readonly FakeProvider _provider = new();
    private readonly FakeIdle _idle = new();
    private readonly List<BandwidthReport> _reports = [];
    private readonly BandwidthLedger _ledger = new();
    private BandwidthConfig _config = new(true, false, TimeSpan.FromMinutes(3), 1024 * MB, 1.0, 0.0, "worker1");
    private bool _serverDown;

    private sealed class FakeProvider : IBandwidthProvider
    {
        public bool Running { get; private set; }

        public long Bytes { get; set; }

        public string Id => "fake";

        public string DisplayName => "Fake";

        public string? UnavailableReason { get; set; }

        public bool IsInstalled => true;

        public Task InstallAsync(IProgress<InstallProgress>? progress, CancellationToken ct) => Task.CompletedTask;

        public Task StartAsync(BandwidthRunOptions options, CancellationToken ct)
        {
            Running = true;
            Bytes = 0;
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken ct)
        {
            Running = false;
            return Task.CompletedTask;
        }

        public Task<BandwidthStatus> GetStatusAsync(CancellationToken ct) =>
            Task.FromResult(new BandwidthStatus(Running ? BandwidthProviderState.Online : BandwidthProviderState.Stopped, Bytes));

        public Task OptInAsync(CancellationToken ct) => Task.CompletedTask;

        public Task OptOutAsync(CancellationToken ct) => Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FakeIdle : IIdleDetector
    {
        public TimeSpan? Idle { get; set; } = TimeSpan.FromHours(1);

        public TimeSpan? GetIdleTime() => Idle;
    }

    private BandwidthSharer CreateSharer() => new(_provider, _idle, () => _config, (report, _) =>
    {
        if (_serverDown)
            throw new HttpRequestException("offline");
        _reports.Add(report);
        return Task.FromResult(new BandwidthReportResult(BandwidthReportStatuses.Credited, report.Bytes, report.Bytes / (decimal)(1024 * MB), null));
    }, _ledger, _ => { }, _time);

    private async Task Run(BandwidthSharer sharer, TimeSpan duration, long bytesPerTick = 0)
    {
        for (var t = TimeSpan.Zero; t < duration; t += BandwidthSharer.TickInterval)
        {
            _time.Advance(BandwidthSharer.TickInterval);
            if (_provider.Running)
                _provider.Bytes += bytesPerTick;
            await sharer.TickAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task Shares_once_enabled_and_reports_every_few_minutes()
    {
        var sharer = CreateSharer();
        sharer.Enable();

        await Run(sharer, TimeSpan.FromMinutes(11), bytesPerTick: MB);

        Assert.True(_provider.Running);
        Assert.Equal(BandwidthState.Sharing, sharer.Snapshot.State);
        Assert.Equal(2, _reports.Count);
        Assert.Equal(new[] { 1L, 2L }, _reports.Select(r => r.Sequence));
        Assert.All(_reports, r => Assert.InRange(r.SharingSeconds, 290, 305));
        Assert.True(sharer.Snapshot.TotalUserUsd > 0);
    }

    [Fact]
    public async Task Does_nothing_without_opt_in()
    {
        _config = _config with { Allowed = false };
        var sharer = CreateSharer();
        sharer.Enable();

        await Run(sharer, TimeSpan.FromMinutes(1));

        Assert.False(_provider.Running);
        Assert.Equal(BandwidthState.Off, sharer.Snapshot.State);
    }

    [Fact]
    public async Task Waits_for_the_user_to_step_away_when_asked_to()
    {
        _config = _config with { OnlyWhenIdle = true };
        _idle.Idle = TimeSpan.FromSeconds(10);
        var sharer = CreateSharer();
        sharer.Enable();

        await Run(sharer, TimeSpan.FromSeconds(30));
        Assert.Equal(BandwidthState.WaitingForIdle, sharer.Snapshot.State);
        Assert.False(_provider.Running);

        _idle.Idle = TimeSpan.FromMinutes(5);
        await Run(sharer, TimeSpan.FromSeconds(10));
        Assert.True(_provider.Running);
    }

    [Fact]
    public async Task Stops_at_the_daily_limit()
    {
        _config = _config with { DailyLimitBytes = 10 * MB };
        var sharer = CreateSharer();
        sharer.Enable();

        await Run(sharer, TimeSpan.FromMinutes(2), bytesPerTick: MB);

        Assert.Equal(BandwidthState.LimitReached, sharer.Snapshot.State);
        Assert.False(_provider.Running);
    }

    [Fact]
    public async Task A_failed_report_is_retried_unchanged()
    {
        var sharer = CreateSharer();
        sharer.Enable();
        _serverDown = true;
        await Run(sharer, TimeSpan.FromMinutes(6), bytesPerTick: MB);
        Assert.Empty(_reports);
        var pending = _ledger.Pending;
        Assert.NotNull(pending);

        _serverDown = false;
        await Run(sharer, TimeSpan.FromMinutes(2), bytesPerTick: MB);

        Assert.Equal(pending, _reports[0]);
        Assert.Null(_ledger.Pending);
    }

    [Fact]
    public async Task Unavailable_providers_never_start()
    {
        _provider.UnavailableReason = "Not in this build";
        var sharer = CreateSharer();
        sharer.Enable();

        await Run(sharer, TimeSpan.FromSeconds(20));

        Assert.Equal(BandwidthState.Unavailable, sharer.Snapshot.State);
        Assert.False(_provider.Running);
    }
}

/// <summary>The Honeygain SDK adapter, against a fake of the native library.</summary>
public sealed class HoneygainProviderTests
{
    private sealed class FakeSdk : IHoneygainSdk
    {
        public List<string> Calls { get; } = [];

        public bool OptedIn { get; set; }

        public bool Running { get; set; }

        public int Init(string apiKey)
        {
            Calls.Add($"init:{apiKey}");
            return 0;
        }

        public int OptIn()
        {
            Calls.Add("opt-in");
            OptedIn = true;
            return 0;
        }

        public int OptOut()
        {
            Calls.Add("opt-out");
            OptedIn = false;
            Running = false;
            return 0;
        }

        public int Start(out bool started)
        {
            Calls.Add("start");
            started = OptedIn;
            Running = OptedIn;
            return 0;
        }

        public int Stop()
        {
            Calls.Add("stop");
            Running = false;
            return 0;
        }

        public int IsRunning(out bool running)
        {
            running = Running;
            return 0;
        }

        public string? Identify() => "hg-device-1";

        public int Mute() => 0;

        public void Dispose()
        {
        }
    }

    [Fact]
    public void Without_a_key_or_library_it_is_unavailable()
    {
        Assert.NotNull(new HoneygainBandwidthProvider((IHoneygainSdk?)null, "key").UnavailableReason);
        Assert.NotNull(new HoneygainBandwidthProvider(new FakeSdk(), null).UnavailableReason);
        Assert.Null(new HoneygainBandwidthProvider(new FakeSdk(), "key").UnavailableReason);
    }

    [Fact]
    public async Task Won_t_share_before_consent()
    {
        var sdk = new FakeSdk();
        var provider = new HoneygainBandwidthProvider(sdk, "key");

        await Assert.ThrowsAsync<InvalidOperationException>(() => provider.StartAsync(new BandwidthRunOptions("w"), CancellationToken.None));
        Assert.False(sdk.Running);
    }

    [Fact]
    public async Task Initializes_once_then_shares_and_reports_online()
    {
        var sdk = new FakeSdk();
        var provider = new HoneygainBandwidthProvider(sdk, "key");

        await provider.OptInAsync(CancellationToken.None);
        await provider.StartAsync(new BandwidthRunOptions("w"), CancellationToken.None);
        var status = await provider.GetStatusAsync(CancellationToken.None);

        Assert.Equal(BandwidthProviderState.Online, status.State);
        Assert.Equal(new[] { "init:key", "opt-in", "start" }, sdk.Calls);
        Assert.Equal("hg-device-1", provider.PartnerDeviceId);
    }

    [Fact]
    public async Task Turning_off_withdraws_consent()
    {
        var sdk = new FakeSdk();
        var provider = new HoneygainBandwidthProvider(sdk, "key");
        await provider.OptInAsync(CancellationToken.None);
        await provider.StartAsync(new BandwidthRunOptions("w"), CancellationToken.None);

        await provider.OptOutAsync(CancellationToken.None);

        Assert.False(sdk.OptedIn);
        Assert.False(sdk.Running);
        Assert.Equal(BandwidthProviderState.Stopped, (await provider.GetStatusAsync(CancellationToken.None)).State);
    }

    [Fact]
    public async Task A_component_that_never_comes_up_is_an_error_after_a_grace_period()
    {
        var sdk = new FakeSdk();
        var time = new ManualTime(DateTimeOffset.UtcNow);
        var provider = new HoneygainBandwidthProvider(sdk, "key", time);
        await provider.OptInAsync(CancellationToken.None);
        await provider.StartAsync(new BandwidthRunOptions("w"), CancellationToken.None);
        sdk.Running = false;

        Assert.Equal(BandwidthProviderState.Starting, (await provider.GetStatusAsync(CancellationToken.None)).State);
        time.Advance(TimeSpan.FromMinutes(1));
        Assert.Equal(BandwidthProviderState.Error, (await provider.GetStatusAsync(CancellationToken.None)).State);
    }
}
