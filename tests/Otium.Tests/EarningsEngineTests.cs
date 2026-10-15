using Otium.Core.Engine;
using Otium.Core.Fees;
using Otium.Core.Platform;
using Otium.Core.Power;
using Otium.Core.Providers;

namespace Otium.Tests;

public class EarningsEngineTests
{
    private static readonly string User = TestAddresses.Generate();
    private static readonly string AppWallet = TestAddresses.Generate();

    private sealed class FakeProvider : IComputeProvider
    {
        public double Hashrate { get; set; } = 5000;
        public double UsdPerHashPerDay { get; set; } = 0.0001; // 5 kH/s -> $0.50/day
        public bool Installed { get; set; }
        public List<ProviderRunOptions> Starts { get; } = [];
        public int Stops { get; private set; }
        public bool Running { get; private set; }

        public string Id => "fake";
        public string DisplayName => "Fake";
        public DeviceKind Device => DeviceKind.Cpu;
        public string PayoutCurrency => "XMR";
        public bool IsSupportedOnThisPlatform => true;
        public bool IsInstalled => Installed;

        public Task InstallAsync(IProgress<InstallProgress>? progress, CancellationToken ct)
        {
            progress?.Report(new InstallProgress("Downloading", 0.5));
            Installed = true;
            return Task.CompletedTask;
        }

        public Task StartAsync(ProviderRunOptions options, CancellationToken ct)
        {
            Starts.Add(options);
            Running = true;
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken ct)
        {
            if (Running)
                Stops++;
            Running = false;
            return Task.CompletedTask;
        }

        public Task<ProviderStatus> GetStatusAsync(CancellationToken ct) =>
            Task.FromResult(new ProviderStatus(Running ? ProviderState.Running : ProviderState.Ready, Running ? Hashrate : 0, Detail: Detail, IsCalibrating: Calibrating));

        public Task<RevenueEstimate> EstimateRevenueAsync(double hashrateHps, CancellationToken ct) =>
            Task.FromResult(new RevenueEstimate(0, hashrateHps * UsdPerHashPerDay, "XMR"));

        public Task<PayoutBalance?> GetPayoutBalanceAsync(string walletAddress, CancellationToken ct) => Task.FromResult<PayoutBalance?>(null);

        public ProfitOffer? Offer { get; set; }
        public bool Calibrating { get; set; }
        public string? Detail { get; set; }

        public Task<ProfitOffer?> GetExpectedOfferAsync(CancellationToken ct) => Task.FromResult(Offer);

        public string? ValidatePayoutAddress(string address) => null;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FakeIdle : IIdleDetector
    {
        public TimeSpan? Idle { get; set; } = TimeSpan.FromHours(1);

        public TimeSpan? GetIdleTime() => Idle;
    }

    private sealed class Harness
    {
        public ManualTime Time { get; } = new(new DateTimeOffset(2026, 10, 15, 12, 0, 0, TimeSpan.Zero));
        public FakeProvider Provider { get; } = new();
        public FakeIdle Idle { get; } = new();
        public EarningsLedger Ledger { get; } = new();
        public double PricePerKwh { get; set; } = 0.15;
        public bool OnlyWhenIdle { get; set; }
        public int CpuPercent { get; set; } = 50;
        public EarningsEngine Engine { get; }

        public Harness()
        {
            // 100 W full load, 10 W idle: 45 W extra at 50% -> $0.162/day at $0.15/kWh.
            var meter = new EstimatedCpuPowerMeter(new CpuPowerModel(100, 10));
            Engine = new EarningsEngine(Provider, meter, Idle, Config, Ledger, _ => { }, Time);
        }

        private EngineConfig Config() => new(
            User, CpuPercent, OnlyWhenIdle, TimeSpan.FromMinutes(5), NeverRunAtLoss: true, PricePerKwh,
            new FeeSchedule(FeePolicy.Default, User, AppWallet, new ReferralStanding(null, null, null)), "test");

        public async Task TickFor(TimeSpan duration)
        {
            var end = Time.GetUtcNow() + duration;
            while (Time.GetUtcNow() < end)
            {
                Time.Advance(EarningsEngine.TickInterval);
                await Engine.TickAsync(CancellationToken.None);
            }
        }
    }

    [Fact]
    public async Task Does_nothing_until_enabled()
    {
        var h = new Harness();
        await h.Engine.TickAsync(CancellationToken.None);
        Assert.Equal(EngineState.Off, h.Engine.Snapshot.State);
        Assert.Empty(h.Provider.Starts);
    }

    [Fact]
    public async Task Installs_then_mines_for_the_user_and_records_earnings()
    {
        var h = new Harness();
        h.Engine.Enable();
        await h.Engine.TickAsync(CancellationToken.None);

        Assert.True(h.Provider.Installed);
        var start = Assert.Single(h.Provider.Starts);
        Assert.Equal(PayoutRole.User, start.Target.Role);
        Assert.Equal(User, start.Target.WalletAddress);
        Assert.Equal(50, start.UsagePercent);

        await h.TickFor(TimeSpan.FromHours(1));
        var s = h.Engine.Snapshot;
        Assert.Equal(EngineState.Earning, s.State);
        Assert.Equal(5000, s.HashrateHps);
        Assert.Equal(0.50, s.GrossUsdPerDay, precision: 6);
        Assert.Equal(0.49, s.UserUsdPerDay, precision: 6); // 2% fee
        Assert.Equal(45, s.ExtraWatts, precision: 6);
        Assert.Equal(0.162, s.ElectricityUsdPerDay, precision: 6);
        Assert.True(s.NetUsdPerDay > 0);
        Assert.Equal(TimeSpan.FromHours(1), h.Ledger.MiningTime);
        Assert.Equal(0.50 / 24, h.Ledger.EstimatedUserUsd, precision: 4);
        Assert.Equal(0.045, h.Ledger.EnergyKwh, precision: 4);
    }

    [Fact]
    public async Task Switches_to_the_fee_slice_at_the_end_of_each_cycle_and_back()
    {
        var h = new Harness();
        h.Engine.Enable();
        await h.Engine.TickAsync(CancellationToken.None);

        var cycle = FeePolicy.Default.CycleLength;
        await h.TickFor(cycle * 0.98 + TimeSpan.FromSeconds(10));
        Assert.Equal(PayoutRole.App, h.Provider.Starts[^1].Target.Role);
        Assert.Equal(AppWallet, h.Provider.Starts[^1].Target.WalletAddress);

        await h.TickFor(cycle * 0.02 + TimeSpan.FromSeconds(10));
        Assert.Equal(PayoutRole.User, h.Provider.Starts[^1].Target.Role);
        Assert.Equal(3, h.Provider.Starts.Count);
        Assert.True(h.Ledger.EstimatedAppFeeUsd > 0);
    }

    [Fact]
    public async Task Changing_cpu_usage_restarts_the_engine_with_the_new_limit()
    {
        var h = new Harness();
        h.Engine.Enable();
        await h.TickFor(TimeSpan.FromMinutes(1));
        h.CpuPercent = 80;
        await h.TickFor(TimeSpan.FromSeconds(10));
        Assert.Equal(80, h.Provider.Starts[^1].UsagePercent);
    }

    [Fact]
    public async Task Pauses_when_electricity_costs_more_than_mining_earns()
    {
        var h = new Harness { PricePerKwh = 1.00 }; // 45 W -> $1.08/day vs $0.49/day earned
        h.Engine.Enable();
        await h.TickFor(EarningsEngine.Warmup + EarningsEngine.LossTolerance + TimeSpan.FromMinutes(1));

        Assert.Equal(EngineState.PausedUnprofitable, h.Engine.Snapshot.State);
        Assert.False(h.Provider.Running);

        // Still unprofitable 30 minutes later: stays paused using the remembered hashrate, without mining to check.
        var starts = h.Provider.Starts.Count;
        await h.TickFor(EarningsEngine.UnprofitableRecheck + TimeSpan.FromMinutes(1));
        Assert.Equal(starts, h.Provider.Starts.Count);
        Assert.Equal(EngineState.PausedUnprofitable, h.Engine.Snapshot.State);

        // Electricity gets cheaper: resumes at the next recheck.
        h.PricePerKwh = 0.10;
        await h.TickFor(EarningsEngine.UnprofitableRecheck + TimeSpan.FromMinutes(1));
        Assert.True(h.Provider.Running);
    }

    [Fact]
    public async Task Waits_while_the_user_is_active_when_idle_only_is_on()
    {
        var h = new Harness { OnlyWhenIdle = true };
        h.Idle.Idle = TimeSpan.FromSeconds(30);
        h.Engine.Enable();
        await h.Engine.TickAsync(CancellationToken.None);

        Assert.Equal(EngineState.WaitingForIdle, h.Engine.Snapshot.State);
        Assert.Empty(h.Provider.Starts);

        h.Idle.Idle = TimeSpan.FromMinutes(6);
        await h.Engine.TickAsync(CancellationToken.None);
        Assert.Single(h.Provider.Starts);

        // User comes back: mining stops immediately.
        h.Idle.Idle = TimeSpan.FromSeconds(2);
        await h.Engine.TickAsync(CancellationToken.None);
        Assert.False(h.Provider.Running);
    }

    [Fact]
    public async Task Disabling_stops_the_engine()
    {
        var h = new Harness();
        h.Engine.Enable();
        await h.TickFor(TimeSpan.FromMinutes(1));
        h.Engine.Disable();
        await h.Engine.TickAsync(CancellationToken.None);
        Assert.False(h.Provider.Running);
        Assert.Equal(EngineState.Off, h.Engine.Snapshot.State);
    }

    [Fact]
    public async Task Never_pauses_for_loss_while_the_provider_is_calibrating()
    {
        var h = new Harness { PricePerKwh = 1.00 };
        h.Provider.Calibrating = true;
        h.Engine.Enable();
        await h.TickFor(EarningsEngine.Warmup + EarningsEngine.LossTolerance + TimeSpan.FromMinutes(5));
        Assert.True(h.Provider.Running);
        Assert.Equal(0, h.Ledger.LastHashrateHps); // calibration speeds aren't remembered

        h.Provider.Calibrating = false;
        await h.TickFor(EarningsEngine.Warmup + EarningsEngine.LossTolerance + TimeSpan.FromMinutes(1));
        Assert.Equal(EngineState.PausedUnprofitable, h.Engine.Snapshot.State);
    }

    [Fact]
    public async Task Provider_offer_with_measured_watts_decides_whether_to_start()
    {
        // Offer: $0.50/day gross but 200 W -> $0.72/day at $0.15/kWh. Not worth starting.
        var h = new Harness();
        h.Provider.Offer = new ProfitOffer(0.50, 200, "kawpow");
        h.Engine.Enable();
        await h.Engine.TickAsync(CancellationToken.None);
        Assert.Empty(h.Provider.Starts);
        Assert.Equal(EngineState.PausedUnprofitable, h.Engine.Snapshot.State);

        // A better offer later: starts at the next recheck.
        h.Provider.Offer = new ProfitOffer(2.00, 200, "autolykos2");
        await h.TickFor(EarningsEngine.UnprofitableRecheck + TimeSpan.FromSeconds(10));
        Assert.Single(h.Provider.Starts);
    }

    [Fact]
    public async Task Snapshot_carries_provider_detail()
    {
        var h = new Harness();
        h.Provider.Detail = "kawpow";
        h.Engine.Enable();
        await h.TickFor(TimeSpan.FromSeconds(20));
        Assert.Equal("kawpow", h.Engine.Snapshot.Detail);
    }
}
