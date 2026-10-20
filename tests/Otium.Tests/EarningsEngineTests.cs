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
            Task.FromResult(Problem is not null
                ? new ProviderStatus(ProviderState.Error, 0, Problem, Problem: Problem)
                : new ProviderStatus(Running ? ProviderState.Running : ProviderState.Ready, Running ? Hashrate : 0, Detail: Detail, IsCalibrating: Calibrating));

        /// <summary>When set, the provider reports this plain-words problem as an error.</summary>
        public string? Problem { get; set; }

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
        public bool RunsWhileBrowsing { get; set; }
        public bool FullSpeedWhenAway { get; set; }
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
            new FeeSchedule(FeePolicy.Default, User, AppWallet, new ReferralStanding(null, null, null)), "test",
            RunsWhileBrowsing: RunsWhileBrowsing, FullSpeedWhenAway: FullSpeedWhenAway);

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
    public async Task A_problem_the_provider_explains_is_shown_as_is_and_retried_later()
    {
        var h = new Harness();
        h.Engine.Enable();
        await h.TickFor(TimeSpan.FromMinutes(1));

        const string problem = "Graphics card isn't mining: its dataset doesn't fit in the graphics card's free memory.";
        h.Provider.Problem = problem;
        await h.TickFor(EarningsEngine.TickInterval);
        var s = h.Engine.Snapshot;
        Assert.Equal(EngineState.Error, s.State);
        Assert.Equal(problem, s.StatusText); // no "Something went wrong:" in front
        Assert.Equal(problem, s.Problem);
        Assert.False(h.Provider.Running);

        var starts = h.Provider.Starts.Count;
        await h.TickFor(TimeSpan.FromSeconds(30));
        Assert.Equal(starts, h.Provider.Starts.Count); // waits before retrying, still explaining why
        Assert.Equal(problem, h.Engine.Snapshot.Problem);

        h.Provider.Problem = null;
        await h.TickFor(EarningsEngine.ErrorRetry);
        Assert.Equal(starts + 1, h.Provider.Starts.Count);
        Assert.Null(h.Engine.Snapshot.Problem);
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

    [Theory]
    [InlineData(true, false, false)] // away: normal priority, so nothing holds it back
    [InlineData(false, false, true)] // mines while the user works: lowest priority
    [InlineData(true, true, true)] // the graphics card may mine while they browse: lowest priority
    public async Task Mines_at_normal_priority_only_while_the_user_is_away(bool onlyWhenAway, bool whileBrowsing, bool lowPriority)
    {
        var h = new Harness { OnlyWhenIdle = onlyWhenAway, RunsWhileBrowsing = whileBrowsing };
        h.Engine.Enable();
        await h.TickFor(TimeSpan.FromSeconds(20));

        Assert.Equal(lowPriority, Assert.Single(h.Provider.Starts).LowPriority);
    }

    [Fact]
    public async Task The_test_the_user_starts_at_the_computer_mines_at_the_lowest_priority()
    {
        var h = new Harness { OnlyWhenIdle = true };
        h.Idle.Idle = TimeSpan.FromSeconds(3); // at the computer
        h.Engine.RunNow(TimeSpan.FromMinutes(2));
        h.Engine.Enable();
        await h.TickFor(TimeSpan.FromSeconds(20));

        Assert.True(Assert.Single(h.Provider.Starts).LowPriority);
    }

    [Fact]
    public async Task The_processor_uses_every_thread_while_the_user_is_away()
    {
        var h = new Harness { OnlyWhenIdle = true, FullSpeedWhenAway = true, CpuPercent = 50 };
        h.Engine.Enable();
        await h.TickFor(TimeSpan.FromSeconds(20));

        var start = Assert.Single(h.Provider.Starts);
        Assert.Equal(100, start.UsagePercent);
        Assert.False(start.LowPriority);
    }

    [Fact]
    public async Task Always_on_it_goes_full_speed_when_the_user_leaves_and_back_to_their_share_when_they_return()
    {
        var h = new Harness { OnlyWhenIdle = false, FullSpeedWhenAway = true, CpuPercent = 50 };
        h.Idle.Idle = TimeSpan.FromSeconds(3); // at the computer
        h.Engine.Enable();
        await h.TickFor(TimeSpan.FromSeconds(20));
        Assert.Equal((50, true), (h.Provider.Starts[^1].UsagePercent, h.Provider.Starts[^1].LowPriority));

        h.Idle.Idle = TimeSpan.FromMinutes(6); // away past the 5-minute threshold
        await h.TickFor(TimeSpan.FromSeconds(10));
        Assert.Equal((100, false), (h.Provider.Starts[^1].UsagePercent, h.Provider.Starts[^1].LowPriority));

        h.Idle.Idle = TimeSpan.FromSeconds(1); // back
        await h.TickFor(TimeSpan.FromSeconds(10));
        Assert.Equal((50, true), (h.Provider.Starts[^1].UsagePercent, h.Provider.Starts[^1].LowPriority));
        Assert.Equal(3, h.Provider.Starts.Count);
    }

    [Fact]
    public async Task The_test_the_user_watches_measures_every_thread_at_the_lowest_priority()
    {
        var h = new Harness { OnlyWhenIdle = true, FullSpeedWhenAway = true, CpuPercent = 50 };
        h.Idle.Idle = TimeSpan.FromSeconds(3);
        h.Engine.RunNow(TimeSpan.FromMinutes(2));
        h.Engine.Enable();
        await h.TickFor(TimeSpan.FromSeconds(20));

        var start = Assert.Single(h.Provider.Starts);
        Assert.Equal((100, true), (start.UsagePercent, start.LowPriority));
    }

    [Fact]
    public async Task The_graphics_card_keeps_one_setting_whether_or_not_the_user_is_there()
    {
        // Without FullSpeedWhenAway (the graphics card), always-on mining doesn't restart as the user comes and goes.
        var h = new Harness { OnlyWhenIdle = false, CpuPercent = 100 };
        h.Engine.Enable();
        await h.TickFor(TimeSpan.FromSeconds(20));
        h.Idle.Idle = TimeSpan.FromSeconds(1);
        await h.TickFor(TimeSpan.FromSeconds(20));

        var start = Assert.Single(h.Provider.Starts);
        Assert.True(start.LowPriority);
    }

    [Fact]
    public async Task Turning_the_clock_back_while_mining_never_moves_the_fee_cycle_back()
    {
        var h = new Harness();
        h.Engine.Enable();
        await h.TickFor(TimeSpan.FromMinutes(10));
        var before = h.Ledger.MiningTime;

        h.Time.SetWallClock(-TimeSpan.FromDays(30)); // the user sets the system clock a month back
        await h.TickFor(TimeSpan.FromMinutes(1));

        Assert.Equal(before + TimeSpan.FromMinutes(1), h.Ledger.MiningTime); // measured on the monotonic clock
    }

    [Fact]
    public async Task A_long_gap_between_ticks_counts_as_at_most_a_few_tick_intervals()
    {
        var h = new Harness();
        h.Engine.Enable();
        await h.TickFor(TimeSpan.FromMinutes(1));
        var before = h.Ledger.MiningTime;

        h.Time.Advance(TimeSpan.FromHours(2)); // e.g. the computer slept, and the miner with it
        await h.Engine.TickAsync(CancellationToken.None);

        Assert.Equal(before + EarningsEngine.MaxTickElapsed, h.Ledger.MiningTime);
        Assert.Equal(TimeSpan.Zero, EarningsEngine.Clamp(TimeSpan.FromHours(-5)));
    }

    [Fact]
    public async Task Only_time_the_miner_hashes_moves_the_fee_cycle()
    {
        // Warming up, or a miner suspended so its API stops answering: it reports no hashrate, so no mining time passes,
        // and suspending it through Otium's slice doesn't run the cycle past the slice.
        var h = new Harness();
        h.Provider.Hashrate = 0;
        h.Engine.Enable();
        await h.TickFor(TimeSpan.FromMinutes(10));
        Assert.Equal(TimeSpan.Zero, h.Ledger.MiningTime);
        Assert.True(h.Ledger.EnergyKwh > 0); // the electricity it used still counts
        Assert.Equal(EngineState.Starting, h.Engine.Snapshot.State);

        h.Provider.Hashrate = 5000;
        await h.TickFor(TimeSpan.FromMinutes(1));
        Assert.Equal(TimeSpan.FromMinutes(1), h.Ledger.MiningTime);
    }

    [Fact]
    public async Task The_mining_time_the_server_has_on_record_raises_the_ledger_but_never_lowers_it()
    {
        // ledger.json deleted near the end of a cycle: the server's floor puts the cycle back where it was, in Otium's slice.
        var h = new Harness();
        h.Engine.Enable();
        await h.TickFor(TimeSpan.FromMinutes(1));
        Assert.Equal(PayoutRole.User, h.Provider.Starts[^1].Target.Role);

        var floor = FeePolicy.Default.CycleLength * 0.99;
        h.Engine.RaiseMiningTimeTo(floor);
        await h.TickFor(TimeSpan.FromSeconds(10));
        Assert.True(h.Ledger.MiningTime >= floor);
        Assert.Equal(PayoutRole.App, h.Provider.Starts[^1].Target.Role);

        var now = h.Ledger.MiningTime;
        h.Engine.RaiseMiningTimeTo(TimeSpan.FromMinutes(5));
        await h.TickFor(EarningsEngine.TickInterval);
        Assert.True(h.Ledger.MiningTime >= now);
    }
}
