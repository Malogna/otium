using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Otium.Core.Accounts;
using Otium.Core.Engine;
using Otium.Core.Fees;
using Otium.Core.Gpu;
using Otium.Core.Platform;
using Otium.Core.Power;
using Otium.Core.Pools;
using Otium.Core.Projections;
using Otium.Core.Providers;
using Otium.Core.Settings;
using Otium.Core.Wallets;
using Otium.Server;
using Otium.Server.Gpu;
using Otium.Server.RealEarnings;

namespace Otium.Tests;

/// <summary>
/// Showing what idle time is worth: the "measure my PC" test, "while you were away", the monthly report, the opt-in
/// earnings reports behind otium.lol's real-numbers table, and pausing the processor miner instead of stopping it.
/// </summary>
public sealed class ProofOfEarningsTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 18, 2, 0, 0, TimeSpan.Zero);

    // ---------- while you were away ----------

    [Fact]
    public void A_stretch_of_earning_is_summed_up_when_it_ends()
    {
        var tracker = new AwaySessionTracker();
        var now = T0;
        double earned = 10, electricity = 2;
        Assert.Null(tracker.Observe(now, false, earned, electricity));
        for (var i = 0; i < 12 * 60; i++) // an hour of reports every 5 s
        {
            now += TimeSpan.FromSeconds(5);
            earned += 0.0001;
            electricity += 0.00002;
            Assert.Null(tracker.Observe(now, true, earned, electricity));
        }
        var summary = tracker.Observe(now + TimeSpan.FromSeconds(5), false, earned, electricity);

        Assert.NotNull(summary);
        // Counted from the first report while earning: 719 of the 720 increments.
        Assert.Equal(0.0719, summary.EarnedUsd, precision: 6);
        Assert.Equal(0.01438, summary.ElectricityUsd, precision: 6);
        Assert.InRange(summary.Earning.TotalMinutes, 59.9, 60);
        Assert.False(tracker.InSession);
    }

    [Fact]
    public void Short_stretches_and_sleep_gaps_dont_count()
    {
        var tracker = new AwaySessionTracker();
        Assert.Null(tracker.Observe(T0, true, 0, 0));
        Assert.Null(tracker.Observe(T0 + TimeSpan.FromMinutes(1), true, 0.01, 0));
        Assert.Null(tracker.Observe(T0 + TimeSpan.FromMinutes(2), false, 0.01, 0)); // two minutes: not worth a card

        // The computer slept for an hour in the middle: that hour isn't earning time.
        Assert.Null(tracker.Observe(T0, true, 0, 0));
        Assert.Null(tracker.Observe(T0 + TimeSpan.FromMinutes(1), true, 0.01, 0));
        Assert.Null(tracker.Observe(T0 + TimeSpan.FromMinutes(61), true, 0.02, 0));
        Assert.Null(tracker.Observe(T0 + TimeSpan.FromMinutes(62), false, 0.02, 0));
    }

    // ---------- monthly report ----------

    [Fact]
    public void History_keeps_month_totals_and_the_report_shows_early_in_the_next_month()
    {
        var history = new EarningsHistory();
        var october = new DateTimeOffset(2026, 10, 30, 22, 0, 0, TimeSpan.Zero);
        history.Record(october, 1.00, 0.10, TimeSpan.Zero);
        history.Record(october + TimeSpan.FromHours(1), 3.10, 0.30, TimeSpan.FromHours(1));
        history.Record(october + TimeSpan.FromDays(2), 3.50, 0.35, TimeSpan.FromMinutes(30)); // 1 November

        var oct = history.Month("2026-10")!;
        Assert.Equal(2.10, oct.EarnedUsd, precision: 6);
        Assert.Equal(3600, oct.EarningSeconds);
        Assert.Equal(0.40, history.Month("2026-11")!.EarnedUsd, precision: 6);

        var report = MonthlyReport.Due(history, new DateTimeOffset(2026, 11, 3, 9, 0, 0, TimeSpan.Zero), null);
        Assert.NotNull(report);
        Assert.Equal("October", report.MonthName);
        Assert.Equal(2.10, report.EarnedUsd, precision: 6);
        Assert.Equal(1, report.EarningHours, precision: 6);

        Assert.Null(MonthlyReport.Due(history, new DateTimeOffset(2026, 11, 3, 9, 0, 0, TimeSpan.Zero), "2026-10")); // dismissed
        Assert.Null(MonthlyReport.Due(history, new DateTimeOffset(2026, 11, 20, 9, 0, 0, TimeSpan.Zero), null)); // too late
        Assert.Null(MonthlyReport.Due(history, new DateTimeOffset(2026, 10, 3, 9, 0, 0, TimeSpan.Zero), null)); // nothing in September
    }

    // ---------- measure my PC ----------

    private static EngineSnapshot Earning(double hashrate, double user, double electricity, string? detail = null, bool calibrating = false) => new()
    {
        State = EngineState.Earning,
        HashrateHps = hashrate,
        UserUsdPerDay = user,
        ElectricityUsdPerDay = electricity,
        Detail = detail,
        IsCalibrating = calibrating,
    };

    [Fact]
    public void The_test_measures_for_two_minutes_and_keeps_each_devices_best_coin()
    {
        var run = new PcMeasurementRun(T0);
        var now = T0;
        run.Observe(now, [new(DeviceKind.Cpu, "Ryzen 9", new EngineSnapshot { State = EngineState.Installing })]);
        Assert.Equal(MeasurementPhase.Preparing, run.Phase);

        // Ten seconds of starting, then both devices earn; the card tries two coins while calibrating.
        for (var i = 0; i < 30; i++)
        {
            now += TimeSpan.FromSeconds(5);
            var cpu = i < 2 ? new EngineSnapshot { State = EngineState.Starting } : Earning(9000, 0.20, 0.05);
            var gpu = i < 2 ? new EngineSnapshot { State = EngineState.Starting }
                : i < 14 ? Earning(200e12, 1.50, 0.70, "Testing unmineable:pearlhash on your graphics card (1 of 2)", calibrating: true)
                : Earning(800e6, 2.40, 0.90, "Testing kryptex:quantus on your graphics card (2 of 2)", calibrating: true);
            run.Observe(now, [new(DeviceKind.Cpu, "Ryzen 9", cpu, "rx/0"), new(DeviceKind.Gpu, "RTX 4090", gpu)]);
            if (run.Phase == MeasurementPhase.Done)
                break;
        }

        Assert.Equal(MeasurementPhase.Done, run.Phase);
        var result = run.Result()!;
        var cpuResult = result.Devices.Single(d => d.Device == DeviceKind.Cpu);
        Assert.Equal("Monero", cpuResult.Coin);
        Assert.Equal(0.15, cpuResult.NetUsdPerDay, precision: 6);
        var gpuResult = result.Devices.Single(d => d.Device == DeviceKind.Gpu);
        Assert.Equal("Quantus", gpuResult.Coin); // $1.50 net beats Pearl's $0.80
        Assert.Equal(1.65, result.NetUsdPerDay, precision: 6);
        Assert.Equal(1.65 * 16 / 24 * 30, result.PerMonth(16), precision: 6);
    }

    [Fact]
    public void The_processor_is_left_off_only_next_to_a_card_that_earns_ten_times_as_much()
    {
        static PcMeasurement Of(double cpu, double gpu) => new(T0, [
            new DeviceMeasurement(DeviceKind.Cpu, "i7-8700K", "Monero", 2500, cpu, 0),
            new DeviceMeasurement(DeviceKind.Gpu, "RTX 3080", "Pearl", 100e12, gpu, 0),
        ]);
        Assert.True(Of(0.09, 3.23).ProcessorAddsLittle); // a real PC in October 2026: 34 times
        Assert.True(Of(0.10, 1.00).ProcessorAddsLittle);
        Assert.False(Of(0.15, 1.00).ProcessorAddsLittle); // under ten times: the processor still adds something
        Assert.False(new PcMeasurement(T0, [new DeviceMeasurement(DeviceKind.Cpu, "i5", "Monero", 1500, 0.05, 0)]).ProcessorAddsLittle);
        Assert.False(new PcMeasurement(T0, [new DeviceMeasurement(DeviceKind.Gpu, "RTX 3080", "Pearl", 1, 3, 0)]).ProcessorAddsLittle);
        Assert.Equal(3.23, Of(0.09, 3.23).WithoutProcessor().NetUsdPerDay, precision: 6);
    }

    [Fact]
    public void A_device_that_wouldnt_pay_counts_as_zero_and_doesnt_hold_the_test_up()
    {
        var run = new PcMeasurementRun(T0);
        var now = T0;
        var paused = new EngineSnapshot { State = EngineState.PausedUnprofitable, UserUsdPerDay = 0.10, ElectricityUsdPerDay = 0.30 };
        for (var i = 0; i < 40 && run.Phase != MeasurementPhase.Done; i++)
        {
            now += TimeSpan.FromSeconds(5);
            run.Observe(now, [new(DeviceKind.Cpu, "i5", paused, "rx/0"), new(DeviceKind.Gpu, "RTX 3060", Earning(41e12, 0.40, 0.30, "pearlhash"))]);
        }

        Assert.Equal(MeasurementPhase.Done, run.Phase);
        var result = run.Result()!;
        Assert.Equal(-0.20, result.Devices.Single(d => d.Device == DeviceKind.Cpu).NetUsdPerDay, precision: 6);
        Assert.Equal(0.10, result.NetUsdPerDay, precision: 6); // only the card
    }

    [Fact]
    public void The_test_gives_up_when_nothing_starts()
    {
        var run = new PcMeasurementRun(T0);
        run.Observe(T0 + PcMeasurementRun.MaximumPreparing, [new(DeviceKind.Cpu, "i5", new EngineSnapshot { State = EngineState.Installing })]);
        Assert.Equal(MeasurementPhase.Done, run.Phase);
        Assert.NotNull(run.Failure);
        Assert.Null(run.Result());
    }

    [Fact]
    public void Engines_still_off_right_after_start_dont_end_the_test()
    {
        // Each engine says "Off" until its first update after Start; the test used to end there with nothing measured.
        var run = new PcMeasurementRun(T0);
        var off = new EngineSnapshot { State = EngineState.Off };
        run.Observe(T0 + TimeSpan.FromSeconds(1), [new(DeviceKind.Cpu, "Ryzen 9", off), new(DeviceKind.Gpu, "RTX 3080", off)]);
        Assert.Equal(MeasurementPhase.Preparing, run.Phase);

        for (var i = 0; i < 16; i++)
            run.Observe(T0 + TimeSpan.FromSeconds(5 + i * 10), [new(DeviceKind.Cpu, "Ryzen 9", Earning(2000, 0.05, 0.01))]);
        Assert.Equal(MeasurementPhase.Done, run.Phase);
        Assert.NotNull(run.Result());

        // Still off well after Start: nothing will run, so the test ends.
        var stuck = new PcMeasurementRun(T0);
        stuck.Observe(T0 + PcMeasurementRun.OffGrace, [new(DeviceKind.Cpu, "Ryzen 9", off)]);
        Assert.Equal(MeasurementPhase.Done, stuck.Phase);
    }

    // ---------- the engine: run now, and pause instead of stop ----------

    private sealed class PausableProvider : IComputeProvider, IPausableProvider
    {
        public int Starts { get; private set; }
        public int Stops { get; private set; }
        public int Pauses { get; private set; }
        public int Resumes { get; private set; }
        public bool Running { get; private set; }
        public bool Paused { get; private set; }

        public string Id => "pausable";
        public string DisplayName => "Pausable";
        public DeviceKind Device => DeviceKind.Cpu;
        public string PayoutCurrency => "XMR";
        public bool IsSupportedOnThisPlatform => true;
        public bool IsInstalled => true;
        public Task InstallAsync(IProgress<InstallProgress>? progress, CancellationToken ct) => Task.CompletedTask;

        public Task StartAsync(ProviderRunOptions options, CancellationToken ct)
        {
            Starts++;
            Running = true;
            Paused = false;
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken ct)
        {
            if (Running)
                Stops++;
            Running = Paused = false;
            return Task.CompletedTask;
        }

        public Task<bool> PauseAsync(CancellationToken ct)
        {
            Pauses++;
            Paused = true;
            return Task.FromResult(Running);
        }

        public Task<bool> ResumeAsync(CancellationToken ct)
        {
            Resumes++;
            Paused = false;
            return Task.FromResult(Running);
        }

        public Task<ProviderStatus> GetStatusAsync(CancellationToken ct) =>
            Task.FromResult(new ProviderStatus(Running ? ProviderState.Running : ProviderState.Ready, Running && !Paused ? 5000 : 0));

        public Task<RevenueEstimate> EstimateRevenueAsync(double hashrateHps, CancellationToken ct) =>
            Task.FromResult(new RevenueEstimate(0, hashrateHps * 0.0001, "XMR"));

        public Task<ProfitOffer?> GetExpectedOfferAsync(CancellationToken ct) => Task.FromResult<ProfitOffer?>(null);
        public Task<PayoutBalance?> GetPayoutBalanceAsync(string walletAddress, CancellationToken ct) => Task.FromResult<PayoutBalance?>(null);
        public string? ValidatePayoutAddress(string address) => null;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class Idle : IIdleDetector
    {
        public TimeSpan? Value { get; set; } = TimeSpan.FromHours(1);

        public TimeSpan? GetIdleTime() => Value;
    }

    private sealed class Rig
    {
        public ManualTime Time { get; } = new(T0);
        public PausableProvider Provider { get; } = new();
        public Idle Idle { get; } = new();
        public EarningsLedger Ledger { get; } = new();
        public EarningsEngine Engine { get; }

        /// <summary>Whether the user agreed to keep the miner ready when they come back.</summary>
        public bool PauseWhenBack { get; set; } = true;

        public Rig()
        {
            var user = TestAddresses.Generate();
            Engine = new EarningsEngine(Provider, new EstimatedCpuPowerMeter(new CpuPowerModel(100, 10)), Idle,
                () => new EngineConfig(user, 50, OnlyWhenIdle: true, TimeSpan.FromMinutes(3), NeverRunAtLoss: false, 0.15,
                    new FeeSchedule(FeePolicy.Default with { FeePercent = 0 }, user, null, new ReferralStanding(null, null, null)), "test",
                    PauseWhenBack: PauseWhenBack),
                Ledger, _ => { }, Time);
        }

        public async Task Run(TimeSpan duration, Action? eachTick = null)
        {
            var end = Time.GetUtcNow() + duration;
            while (Time.GetUtcNow() < end)
            {
                Time.Advance(EarningsEngine.TickInterval);
                if (Idle.Value is { } idle)
                    Idle.Value = idle + EarningsEngine.TickInterval;
                eachTick?.Invoke();
                await Engine.TickAsync(CancellationToken.None);
            }
        }
    }

    [Fact]
    public async Task Coming_back_briefly_pauses_the_miner_and_leaving_again_resumes_it_without_a_restart()
    {
        var rig = new Rig();
        rig.Engine.Enable();
        await rig.Run(TimeSpan.FromMinutes(5));
        Assert.Equal(1, rig.Provider.Starts);
        var minedBefore = rig.Ledger.MiningTime;

        rig.Idle.Value = TimeSpan.Zero; // the user moves the mouse
        await rig.Engine.TickAsync(CancellationToken.None);
        Assert.Equal(1, rig.Provider.Pauses);
        Assert.Equal(0, rig.Provider.Stops);
        Assert.True(rig.Engine.IsSuspended);
        Assert.Equal(EngineState.WaitingForIdle, rig.Engine.Snapshot.State);

        await rig.Run(TimeSpan.FromMinutes(4)); // idle again for 3 minutes: earning resumes
        Assert.Equal(1, rig.Provider.Resumes);
        Assert.Equal(1, rig.Provider.Starts); // no restart, no warm-up
        Assert.False(rig.Engine.IsSuspended);
        // The paused minutes aren't counted as mining.
        Assert.InRange((rig.Ledger.MiningTime - minedBefore).TotalMinutes, 0.5, 1.5);
    }

    [Fact]
    public async Task Without_the_users_agreement_the_miner_stops_as_before()
    {
        var rig = new Rig { PauseWhenBack = false };
        rig.Engine.Enable();
        await rig.Run(TimeSpan.FromMinutes(5));

        rig.Idle.Value = TimeSpan.Zero;
        await rig.Engine.TickAsync(CancellationToken.None);
        Assert.Equal(0, rig.Provider.Pauses);
        Assert.Equal(1, rig.Provider.Stops);
    }

    [Fact]
    public async Task Turning_the_option_off_stops_a_paused_miner()
    {
        var rig = new Rig();
        rig.Engine.Enable();
        await rig.Run(TimeSpan.FromMinutes(5));
        rig.Idle.Value = TimeSpan.Zero;
        await rig.Engine.TickAsync(CancellationToken.None);
        Assert.True(rig.Engine.IsSuspended);

        rig.PauseWhenBack = false;
        await rig.Run(TimeSpan.FromSeconds(5), () => rig.Idle.Value = TimeSpan.Zero);
        Assert.False(rig.Engine.IsSuspended);
        Assert.Equal(1, rig.Provider.Stops);
    }

    [Fact]
    public async Task A_paused_miner_is_stopped_once_the_user_stays()
    {
        var rig = new Rig();
        rig.Engine.Enable();
        await rig.Run(TimeSpan.FromMinutes(5));

        rig.Idle.Value = TimeSpan.Zero;
        await rig.Run(EarningsEngine.PauseGrace + TimeSpan.FromSeconds(10), () => rig.Idle.Value = TimeSpan.Zero);
        Assert.Equal(1, rig.Provider.Pauses);
        Assert.Equal(1, rig.Provider.Stops);
        Assert.False(rig.Engine.IsSuspended);
    }

    [Fact]
    public async Task Run_now_earns_while_the_user_is_at_the_computer_until_it_ends()
    {
        var rig = new Rig();
        rig.Engine.Enable();
        rig.Idle.Value = TimeSpan.Zero;
        await rig.Run(TimeSpan.FromSeconds(30), () => rig.Idle.Value = TimeSpan.Zero);
        Assert.Equal(0, rig.Provider.Starts);

        rig.Engine.RunNow(TimeSpan.FromMinutes(5));
        await rig.Run(TimeSpan.FromMinutes(1), () => rig.Idle.Value = TimeSpan.Zero);
        Assert.Equal(1, rig.Provider.Starts);
        Assert.Equal(EngineState.Earning, rig.Engine.Snapshot.State);
        Assert.True(rig.Engine.Snapshot.RunningNow);

        rig.Engine.EndRunNow();
        await rig.Run(TimeSpan.FromSeconds(10), () => rig.Idle.Value = TimeSpan.Zero);
        Assert.Equal(EngineState.WaitingForIdle, rig.Engine.Snapshot.State);
        Assert.True(rig.Provider.Paused || !rig.Provider.Running);
    }

    // ---------- card ids, share messages ----------

    [Theory]
    [InlineData("NVIDIA GeForce RTX 4090", "rtx4090")]
    [InlineData("NVIDIA GeForce RTX 4070 Ti SUPER", "rtx4070tisuper")]
    [InlineData("NVIDIA GeForce RTX 3060 Ti", "rtx3060ti")]
    [InlineData("AMD Radeon RX 6700 XT", "rx6700xt")]
    [InlineData("AMD Radeon RX 7900 XTX", "rx7900xtx")]
    [InlineData("NVIDIA GeForce GTX 1660 SUPER", "gtx1660super")]
    [InlineData("NVIDIA GeForce RTX 4070 Laptop GPU", "rtx4070laptop")]
    [InlineData("Intel(R) UHD Graphics 770", null)]
    [InlineData(null, null)]
    public void Graphics_cards_get_short_ids(string? name, string? id) => Assert.Equal(id, CardIds.FromName(name));

    [Fact]
    public void Card_ids_read_back_as_names_on_the_site()
    {
        Assert.Equal("RTX 4090", SitePages.CardName("rtx4090"));
        Assert.Equal("RTX 4070 Laptop", SitePages.CardName("rtx4070laptop"));
        Assert.Equal("RX 7900 XTX", SitePages.CardName("rx7900xtx"));
    }

    [Fact]
    public void Shared_results_carry_the_invite_when_there_is_one()
    {
        var withCode = InviteMessages.Earned("My PC earned $4.21 in October", "7KQ2M9X", "https://otium.lol/r/7KQ2M9X", TimeSpan.FromDays(30));
        Assert.Equal("My PC earned $4.21 in October with Otium, which mines only while I'm away from the computer. " +
                     "Join with my code 7KQ2M9X and get 30 days fee-free: https://otium.lol/r/7KQ2M9X", withCode);
        var without = InviteMessages.Earned("My PC earned $0.84 while I was away", null, null, TimeSpan.FromDays(30));
        Assert.EndsWith("Try it: https://otium.lol", without);
    }

    // ---------- opt-in earnings reports (app side) ----------

    [Fact]
    public void Reports_send_only_what_was_earned_since_sharing_was_turned_on()
    {
        var state = new EarningsReportState();
        var now = T0;
        ReportedDevice[] Devices(double cpuHours, double cpuUsd, double gpuHours, double gpuUsd) =>
        [
            new("cpu", "AMD Ryzen 9 7950X", new EarningsTotals(TimeSpan.FromHours(cpuHours), cpuUsd, cpuUsd / 4)),
            new("gpu", "rtx4090", new EarningsTotals(TimeSpan.FromHours(gpuHours), gpuUsd, gpuUsd / 3)),
        ];

        EarningsReports.Reset(state, Devices(100, 5, 50, 40), now); // months of history before: never sent
        Assert.Null(EarningsReports.Next(state, Devices(102, 5.1, 52, 42), now + TimeSpan.FromHours(1))); // not due yet

        now += EarningsReports.Interval;
        var pending = EarningsReports.Next(state, Devices(108, 5.4, 58, 48), now)!;
        Assert.Equal(1, pending.Report.Sequence);
        var gpu = pending.Report.Devices.Single(d => d.Kind == "gpu");
        Assert.Equal("rtx4090", gpu.Model);
        Assert.Equal(8 * 3600, gpu.EarningSeconds, precision: 3);
        Assert.Equal(8, gpu.UserUsd, precision: 6);
        EarningsReports.Commit(state, pending);

        // Nothing mined since: no report, and the next one still starts from the last.
        now += EarningsReports.Interval;
        Assert.Null(EarningsReports.Next(state, Devices(108, 5.4, 58, 48), now));
        Assert.Equal(1, state.Sequence);
    }

    // ---------- the server: reports, real numbers, pool payouts ----------

    private sealed class FakeUnmineable : IUnmineableApi
    {
        public Dictionary<string, IReadOnlyList<UnmineableWorker>> Workers { get; } = [];

        public Task<UnmineableAddress> GetAddressAsync(PayoutTarget target, CancellationToken ct) =>
            Task.FromResult(new UnmineableAddress(target.Coin == PayoutCoins.Monero ? null : target.ToString(), 0.1m, 0.1m, target.Coin.Threshold, true, true, "TRX", [], null));

        public Task<UnmineableStats> GetStatsAsync(string uuid, CancellationToken ct) =>
            Task.FromResult(new UnmineableStats(uuid.Split(':')[0], 0.1m, 0, 0.1m, 0.2m, 1, 3, 6m, null, 1.5m));

        public Task<IReadOnlyList<UnmineableWorker>> GetWorkersAsync(string uuid, CancellationToken ct) =>
            Task.FromResult(Workers.GetValueOrDefault(uuid) ?? []);

        public Task<IReadOnlyList<UnmineablePayment>> GetPaymentsAsync(string uuid, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<UnmineablePayment>>([]);

        public Task<UnmineableReferrals> GetReferralsAsync(string uuid, CancellationToken ct) =>
            Task.FromResult(new UnmineableReferrals("ztjq-jr7s", 0));
    }

    /// <summary>MoneroOcean as the payout check sees it: per-worker history (who mines to an address) and what it paid.</summary>
    private sealed class FakeMoneroOcean : IMoneroPoolApi
    {
        private readonly GpuPolicyTests.FakePool _rest = new();

        public Dictionary<string, string[]> Workers { get; } = [];

        public Task<MoneroNetworkStats> GetNetworkStatsAsync(CancellationToken ct) => _rest.GetNetworkStatsAsync(ct);

        public Task<AlgoProfits> GetAlgoProfitsAsync(CancellationToken ct) => _rest.GetAlgoProfitsAsync(ct);

        public Task<PoolMinerStats?> GetMinerStatsAsync(string walletAddress, CancellationToken ct) =>
            Task.FromResult<PoolMinerStats?>(new PoolMinerStats(1e12, 0.001, 0.02, 9000)); // 0.02 XMR paid: $6 at $300

        public Task<IReadOnlyDictionary<string, IReadOnlyList<HashratePoint>>> GetWorkerHistoryAsync(string walletAddress, CancellationToken ct) =>
            Task.FromResult<IReadOnlyDictionary<string, IReadOnlyList<HashratePoint>>>(
                (Workers.GetValueOrDefault(walletAddress) ?? []).ToDictionary(w => w, IReadOnlyList<HashratePoint> (_) => [new HashratePoint(T0, 9000, 9000)]));
    }

    private sealed class FakePrices : ICoinPrices
    {
        public Task<IReadOnlyDictionary<string, double>> GetUsdAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyDictionary<string, double>>(new Dictionary<string, double> { ["USDT"] = 1, ["XMR"] = 300, ["MYST"] = 0.08, ["AIOZ"] = 0.25 });
    }

    private sealed class Server : IDisposable
    {
        private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"otium-real-{Guid.NewGuid():N}.db");
        public ManualTime Time { get; } = new(T0);
        public FakeUnmineable Unmineable { get; } = new();
        public FakeMoneroOcean MoneroOcean { get; } = new();
        public WebApplicationFactory<Program> Factory { get; }
        public HttpClient Http { get; }

        public Server(params (string Key, string Value)[] settings)
        {
            Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            {
                builder.UseSetting("ConnectionStrings:Otium", $"Data Source={_dbPath};Pooling=False");
                builder.UseSetting("DisableBackgroundJobs", "true");
                builder.UseSetting("RealEarnings:PoolRequestDelaySeconds", "0");
                foreach (var (key, value) in settings)
                    builder.UseSetting(key, value);
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<IMoneroPoolApi>();
                    services.AddSingleton<IMoneroPoolApi>(MoneroOcean);
                    services.RemoveAll<IUnmineableApi>();
                    services.AddSingleton<IUnmineableApi>(Unmineable);
                    services.RemoveAll<IKryptexApi>();
                    services.AddSingleton<IKryptexApi>(new GpuPolicyTests.FakeKryptex());
                    services.RemoveAll<IPearlPipFeed>();
                    services.AddSingleton<IPearlPipFeed>(new GpuPolicyTests.FakePip());
                    services.RemoveAll<IProfitFeed>();
                    services.AddSingleton<IProfitFeed>(new GpuPolicyTests.FakeFeed());
                    services.RemoveAll<ICoinPrices>();
                    services.AddSingleton<ICoinPrices>(new FakePrices());
                    services.RemoveAll<TimeProvider>();
                    services.AddSingleton<TimeProvider>(Time);
                });
            });
            Http = Factory.CreateClient();
        }

        public async Task<(string Device, string Secret, AccountSummary Summary)> Register(string payout)
        {
            var secret = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
            var device = Guid.NewGuid().ToString("N");
            var response = await Http.PostAsJsonAsync("/api/v1/devices", new RegisterDeviceRequest(device, secret, payout, null));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            return (device, secret, (await response.Content.ReadFromJsonAsync<AccountSummary>())!);
        }

        public Task Report(string device, string secret, EarningsReport report) =>
            new AccountClient(Http).ReportEarningsAsync(device, secret, report, CancellationToken.None);

        public Otium.Server.Data.OtiumStore Store => Factory.Services.GetRequiredService<Otium.Server.Data.OtiumStore>();

        /// <summary>Makes every account so far a few weeks old, as the site's figures only count established accounts.</summary>
        public void AgeAccounts() => Sql("UPDATE accounts SET created_at = '2026-09-01T00:00:00.0000000Z'");

        /// <summary>Runs SQL on the server's database directly, for what the API can no longer create (old rows).</summary>
        public void Sql(string sql)
        {
            using var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={_dbPath};Pooling=False");
            connection.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = sql;
            cmd.ExecuteNonQuery();
        }

        public void Dispose()
        {
            Http.Dispose();
            Factory.Dispose();
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            foreach (var file in new[] { _dbPath, _dbPath + "-wal", _dbPath + "-shm" })
                File.Delete(file);
        }
    }

    private static EarningsReport GpuReport(long seq, string model, double hours, double usd) =>
        new(seq, T0 - TimeSpan.FromDays(1), T0, [new DeviceEarnings("gpu", model, hours * 3600, usd, usd / 4), new DeviceEarnings("cpu", "Intel Core i7", hours * 3600, 0.05, 0.02)]);

    [Fact]
    public async Task The_site_shows_real_per_card_numbers_only_with_enough_computers()
    {
        // The section's own thresholds (users and dollars earned) are tested below; here only the per-card one counts.
        using var server = new Server(("RealEarnings:MinimumEarnedUsd", "0"), ("RealEarnings:MinimumUsers", "0"));
        var computers = new List<(string Device, string Secret)>();
        for (var i = 0; i < 3; i++)
        {
            var (device, secret, _) = await server.Register(TestAddresses.Usdt(i));
            computers.Add((device, secret));
        }
        server.AgeAccounts();

        // Two RTX 4090s so far: not enough to show the card.
        await server.Report(computers[0].Device, computers[0].Secret, GpuReport(1, "rtx4090", 12, 1.20));
        await server.Report(computers[1].Device, computers[1].Secret, GpuReport(1, "rtx4090", 8, 1.00));
        await server.Report(computers[1].Device, computers[1].Secret, GpuReport(1, "rtx4090", 8, 1.00)); // a retry, stored once
        var real = server.Factory.Services.GetRequiredService<RealEarningsService>();
        Assert.Empty(real.Compute(server.Time.GetUtcNow()).Cards);
        Assert.DoesNotContain("id=\"real\"", await server.Http.GetStringAsync("/"));

        await server.Report(computers[2].Device, computers[2].Secret, GpuReport(1, "rtx4090", 16, 1.60));
        var card = Assert.Single(real.Compute(server.Time.GetUtcNow()).Cards);
        Assert.Equal("rtx4090", card.Model);
        Assert.Equal(3, card.Computers);
        Assert.Equal(2.40, card.UsdPerEarningDay, precision: 6); // medians of $2.40, $3.00 and $2.40 a day of earning
        Assert.Equal(12.0 / 7, card.HoursPerDay, precision: 6);

        server.Time.Advance(TimeSpan.FromMinutes(11)); // past the page's cache
        var page = await server.Http.GetStringAsync("/");
        Assert.Contains("id=\"real\"", page);
        Assert.Contains("<th scope=\"row\">RTX 4090</th><td>3</td><td>$2.40</td>", page);
    }

    [Fact]
    public async Task Internet_sharing_figures_by_country_come_from_three_computers_and_reach_the_page()
    {
        using var server = new Server(("RealEarnings:MinimumEarnedUsd", "0"), ("RealEarnings:MinimumUsers", "0"));
        for (var i = 0; i < 3; i++)
        {
            var (device, secret, _) = await server.Register(TestAddresses.Usdt(i));
            // 24 hours of sharing in Norway earning $1, $2 and $3 (lower case is accepted and stored as "NO").
            await server.Report(device, secret, new EarningsReport(1, T0 - TimeSpan.FromDays(1), T0,
                [new DeviceEarnings("sharing", i == 0 ? "no" : "NO", 24 * 3600, i + 1, 0)]));
        }
        server.AgeAccounts();

        var real = server.Factory.Services.GetRequiredService<RealEarningsService>().Compute(server.Time.GetUtcNow());
        var norway = Assert.Single(real.SharingByCountry);
        Assert.Equal("NO", norway.Country);
        Assert.Equal(3, norway.Computers);
        Assert.Equal(2.0, norway.UsdPerSharingDay, precision: 6);
        Assert.Equal(2.0 / 7 * 30, norway.UsdPerMonth, precision: 6); // $2 in the 7-day window

        server.Time.Advance(TimeSpan.FromMinutes(11));
        var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.Add("CF-IPCountry", "NO");
        var page = await (await server.Http.SendAsync(request)).Content.ReadAsStringAsync();
        Assert.Contains("the median of 3 Otium computers in Norway", page);
        Assert.Contains("<th scope=\"row\">Norway</th><td>3</td><td>$2.00</td>", page);
    }

    [Fact]
    public async Task Mysterium_sharing_is_accepted_but_kept_out_of_proxybases_figures()
    {
        using var server = new Server(("RealEarnings:MinimumEarnedUsd", "0"), ("RealEarnings:MinimumUsers", "0"));
        for (var i = 0; i < 3; i++)
        {
            var (device, secret, _) = await server.Register(TestAddresses.Usdt(i));
            await server.Report(device, secret, new EarningsReport(1, T0 - TimeSpan.FromDays(1), T0,
                [new DeviceEarnings("sharing", "NO", 24 * 3600, 1, 0), new DeviceEarnings("sharing-mysterium", "no", 24 * 3600, 0.05, 0)]));
        }
        server.AgeAccounts();

        // The site's per-country figure is Proxybase's alone: $1 a day, not mixed with Mysterium's 5 cents.
        var norway = Assert.Single(server.Factory.Services.GetRequiredService<RealEarningsService>().Compute(server.Time.GetUtcNow()).SharingByCountry);
        Assert.Equal(1.0, norway.UsdPerSharingDay, precision: 6);

        // Stored like Proxybase's, country upper-cased, for the admin's share analytics.
        var kinds = server.Store.EarningsSince(T0 - TimeSpan.FromDays(2)).Select(r => (r.Kind, r.Model)).Distinct().ToList();
        Assert.Contains(("sharing-mysterium", "NO"), kinds);
    }

    [Fact]
    public async Task Account_summaries_carry_the_myst_price()
    {
        using var server = new Server();
        var (device, secret, _) = await server.Register(TestAddresses.Usdt(0)); // starts the hourly read in the background

        await server.Factory.Services.GetRequiredService<Otium.Server.Bandwidth.MystPrice>().RefreshAsync();
        var summary = await new AccountClient(server.Http).GetSummaryAsync(device, secret, CancellationToken.None);
        Assert.Equal(0.08, summary.MystUsd);
    }

    [Fact]
    public async Task Account_summaries_carry_the_aioz_price()
    {
        using var server = new Server();
        var (device, secret, _) = await server.Register(TestAddresses.Usdt(0));

        await server.Factory.Services.GetRequiredService<Otium.Server.Bandwidth.AiozPrice>().RefreshAsync();
        var summary = await new AccountClient(server.Http).GetSummaryAsync(device, secret, CancellationToken.None);
        Assert.Equal(0.25, summary.AiozUsd);
    }

    [Fact]
    public async Task Aioz_sharing_is_accepted_on_its_own_line()
    {
        using var server = new Server(("RealEarnings:MinimumEarnedUsd", "0"), ("RealEarnings:MinimumUsers", "0"));
        var (device, secret, _) = await server.Register(TestAddresses.Usdt(0));
        await server.Report(device, secret, new EarningsReport(1, T0 - TimeSpan.FromDays(1), T0,
            [new DeviceEarnings("sharing-aioz", "no", 24 * 3600, 0.02, 0)]));

        var kinds = server.Store.EarningsSince(T0 - TimeSpan.FromDays(2)).Select(r => (r.Kind, r.Model)).Distinct().ToList();
        Assert.Contains(("sharing-aioz", "NO"), kinds);
    }

    [Fact]
    public async Task Sharing_reports_must_name_a_country_and_stay_plausible()
    {
        using var server = new Server();
        var (device, secret, _) = await server.Register(TestAddresses.Usdt(0));
        var client = new AccountClient(server.Http);
        foreach (var bad in new[]
                 {
                     new DeviceEarnings("sharing", "Norway", 3600, 0.1, 0), // not a two-letter code
                     new DeviceEarnings("sharing", "NO", 3 * 3600, 20, 0), // $160 a day of sharing
                     new DeviceEarnings("sharing", "NO", 3600, 0.1, 0.05), // sharing doesn't count electricity
                 })
        {
            var e = await Assert.ThrowsAsync<AccountRequestException>(() =>
                client.ReportEarningsAsync(device, secret, new EarningsReport(1, T0 - TimeSpan.FromHours(6), T0, [bad]), CancellationToken.None));
            Assert.Equal(400, e.StatusCode);
        }
    }

    [Fact]
    public async Task Forged_or_broken_reports_are_refused()
    {
        using var server = new Server();
        var (device, secret, _) = await server.Register(TestAddresses.Usdt(0));
        var client = new AccountClient(server.Http);

        async Task Refused(EarningsReport report)
        {
            var e = await Assert.ThrowsAsync<AccountRequestException>(() => client.ReportEarningsAsync(device, secret, report, CancellationToken.None));
            Assert.Equal(400, e.StatusCode);
        }

        await Refused(new EarningsReport(1, T0 - TimeSpan.FromHours(6), T0, [new DeviceEarnings("gpu", "rtx4090", 6 * 3600, 500, 0)])); // $2,000 a day
        await Refused(new EarningsReport(1, T0 - TimeSpan.FromHours(6), T0, [new DeviceEarnings("gpu", "rtx4090", 9 * 3600, 1, 0)])); // more hours than the period
        await Refused(new EarningsReport(1, T0, T0 + TimeSpan.FromHours(6), [new DeviceEarnings("gpu", "rtx4090", 3600, 1, 0)])); // in the future
        await Refused(new EarningsReport(1, T0 - TimeSpan.FromHours(6), T0, [new DeviceEarnings("phone", "x", 3600, 0.1, 0)]));

        var wrongSecret = await Assert.ThrowsAsync<AccountRequestException>(() =>
            client.ReportEarningsAsync(device, new string('0', 64), GpuReport(1, "rtx4090", 1, 0.1), CancellationToken.None));
        Assert.Equal(401, wrongSecret.StatusCode);
    }

    [Fact]
    public async Task Reports_that_overlap_an_earlier_one_are_refused_and_retries_still_count_once()
    {
        using var server = new Server();
        var (device, secret, _) = await server.Register(TestAddresses.Usdt(0));
        var client = new AccountClient(server.Http);
        await server.Report(device, secret, GpuReport(1, "rtx4090", 12, 1.20));
        await server.Report(device, secret, GpuReport(1, "rtx4090", 12, 1.20)); // the same report again: a retry, no error

        // The same day again under new sequence numbers (whole, or half of it) would add its hours and dollars twice.
        foreach (var (start, end) in new[] { (T0 - TimeSpan.FromDays(1), T0), (T0 - TimeSpan.FromHours(12), T0), (T0 - TimeSpan.FromDays(2), T0 - TimeSpan.FromHours(23)) })
        {
            var e = await Assert.ThrowsAsync<AccountRequestException>(() => client.ReportEarningsAsync(device, secret,
                new EarningsReport(2, start, end, [new DeviceEarnings("gpu", "rtx4090", 3600, 0.1, 0)]), CancellationToken.None));
            Assert.Equal(400, e.StatusCode);
        }

        // The day before (ending where the first one starts) is fine, and so is another kind of device for the same day.
        await server.Report(device, secret, new EarningsReport(2, T0 - TimeSpan.FromDays(2), T0 - TimeSpan.FromDays(1), [new DeviceEarnings("gpu", "rtx4090", 3600, 0.1, 0)]));
        await server.Report(device, secret, new EarningsReport(3, T0 - TimeSpan.FromDays(1), T0, [new DeviceEarnings("sharing", "NO", 3600, 0.1, 0)]));
        Assert.Equal(4, server.Store.EarningsReportsFor(server.Store.FindDevice(device)!.AccountId).Count);
    }

    [Fact]
    public async Task A_cards_figures_need_three_established_accounts_and_each_account_counts_once()
    {
        using var server = new Server(("RealEarnings:MinimumEarnedUsd", "0"), ("RealEarnings:MinimumUsers", "0"));
        var real = server.Factory.Services.GetRequiredService<RealEarningsService>();

        // One account with three computers making $12 a day of earning, and two others at $2.40 and $3.00.
        var (big, bigSecret, _) = await server.Register(TestAddresses.Usdt(0));
        var bigAccount = server.Store.FindDevice(big)!.AccountId;
        var computers = new List<(string Device, string Secret)> { (big, bigSecret) };
        for (var i = 1; i < 3; i++)
        {
            var (device, secret, _) = await server.Register(TestAddresses.Usdt(i));
            server.Store.Upsert(server.Store.FindDevice(device)! with { AccountId = bigAccount }); // signed in to the same account
            computers.Add((device, secret));
        }
        foreach (var (device, secret) in computers)
            await server.Report(device, secret, GpuReport(1, "rtx4090", 10, 5.00));
        server.AgeAccounts();
        Assert.Empty(real.Compute(server.Time.GetUtcNow()).Cards); // three computers, but one account

        var (b, bSecret, _) = await server.Register(TestAddresses.Usdt(3));
        var (c, cSecret, _) = await server.Register(TestAddresses.Usdt(4));
        await server.Report(b, bSecret, GpuReport(1, "rtx4090", 10, 1.00));
        await server.Report(c, cSecret, GpuReport(1, "rtx4090", 10, 1.25));
        Assert.Empty(real.Compute(server.Time.GetUtcNow()).Cards); // the two new accounts are only minutes old

        server.AgeAccounts();
        var card = Assert.Single(real.Compute(server.Time.GetUtcNow()).Cards);
        Assert.Equal(5, card.Computers);
        // The median of the accounts ($12, $2.40 and $3.00 a day of earning), not of the five computers (which is $12).
        Assert.Equal(3.00, card.UsdPerEarningDay, precision: 6);
    }

    [Fact]
    public async Task No_computer_counts_more_hours_than_the_window_has()
    {
        using var server = new Server(("RealEarnings:MinimumEarnedUsd", "0"), ("RealEarnings:MinimumUsers", "0"));
        var devices = new List<string>();
        for (var i = 0; i < 3; i++)
        {
            var (device, secret, _) = await server.Register(TestAddresses.Usdt(i));
            devices.Add(device);
            // A fortnight's report: only its last week counts, with half its dollars ($7 of $14).
            await server.Report(device, secret, new EarningsReport(1, T0 - TimeSpan.FromDays(14), T0, [new DeviceEarnings("sharing", "NO", 14 * 12 * 3600, 14, 0)]));
        }
        server.AgeAccounts();
        var norway = Assert.Single(server.Factory.Services.GetRequiredService<RealEarningsService>().Compute(server.Time.GetUtcNow()).SharingByCountry);
        Assert.Equal(7.0 / 7 * 30, norway.UsdPerMonth, precision: 6);
        Assert.Equal(2.0, norway.UsdPerSharingDay, precision: 6);

        // Overlapping rows stored before they were refused: a computer claiming the window three times over is cut back
        // to the window, its rate unchanged.
        foreach (var seq in new[] { 2, 3, 4 })
            server.Sql($"""
                INSERT INTO earnings_reports (device_id, seq, kind, account_id, model, period_start, period_end, seconds, user_usd, electricity_usd, created_at)
                SELECT device_id, {seq}, kind, account_id, 'SE', '2026-10-11T02:00:00.0000000Z', '2026-10-18T02:00:00.0000000Z', 604800, 7, 0, created_at
                FROM earnings_reports WHERE device_id IN ('{devices[0]}', '{devices[1]}', '{devices[2]}') AND seq = 1
                """);
        var sweden = Assert.Single(server.Factory.Services.GetRequiredService<RealEarningsService>().Compute(server.Time.GetUtcNow()).SharingByCountry, s => s.Country == "SE");
        Assert.Equal(7.0 / 7 * 30, sweden.UsdPerMonth, precision: 6); // $7 a week, not $21
        Assert.Equal(1.0, sweden.UsdPerSharingDay, precision: 6);
    }

    [Fact]
    public async Task An_address_shared_by_several_accounts_counts_once()
    {
        using var server = new Server();
        for (var i = 0; i < 3; i++)
            await server.Register(TestAddresses.Usdt(0));
        server.Unmineable.Workers[TestAddresses.Usdt(0)] = [new UnmineableWorker("randomx", "ot1234abcd", true, 9000, 9000)];

        await server.Factory.Services.GetRequiredService<PoolPayoutChecker>().CheckAllAsync(CancellationToken.None);

        var summary = server.Factory.Services.GetRequiredService<RealEarningsService>().Compute(server.Time.GetUtcNow());
        Assert.Equal(6, summary.PaidUsd, precision: 6); // not $18
        Assert.Equal(1, summary.PaidUsers);
        Assert.Equal(3, summary.Rewarded30dUsd, precision: 6);
    }

    [Fact]
    public async Task Moneroocean_payouts_count_only_while_the_pool_lists_otium_workers_alone()
    {
        using var server = new Server();
        var (otium, mixed, unlisted) = (TestAddresses.Generate(), TestAddresses.Generate(), TestAddresses.Generate());
        foreach (var address in new[] { otium, mixed, unlisted })
            await server.Register($"XMR:{address}");
        server.MoneroOcean.Workers[otium] = ["ot1234abcd", "ot1234abcdg"];
        server.MoneroOcean.Workers[mixed] = ["ot5678abcd", "garage-rig"];
        var checker = server.Factory.Services.GetRequiredService<PoolPayoutChecker>();
        var real = server.Factory.Services.GetRequiredService<RealEarningsService>();

        await checker.CheckAllAsync(CancellationToken.None);
        var summary = real.Compute(server.Time.GetUtcNow());
        Assert.Equal(6, summary.PaidUsd, precision: 6); // 0.02 XMR at $300, from the Otium-only address alone
        Assert.Equal(1, summary.PaidUsers);

        // Once the pool stops listing its workers, the address drops out rather than keeping an unchecked figure.
        server.MoneroOcean.Workers.Remove(otium);
        await checker.CheckAllAsync(CancellationToken.None);
        Assert.Equal(0, real.Compute(server.Time.GetUtcNow()).PaidUsers);
    }

    [Fact]
    public async Task Pool_payouts_count_only_addresses_that_mine_with_otium_alone()
    {
        using var server = new Server();
        var (_, _, otiumOnly) = await server.Register(TestAddresses.Usdt(0));
        var (_, _, mixed) = await server.Register(TestAddresses.Usdt(1));
        server.Unmineable.Workers[TestAddresses.Usdt(0)] = [new UnmineableWorker("randomx", "ot1234abcd", true, 9000, 9000), new UnmineableWorker("pearlpow", "ot1234abcdg", true, 1, 1)];
        server.Unmineable.Workers[TestAddresses.Usdt(1)] = [new UnmineableWorker("randomx", "ot5678abcd", true, 9000, 9000), new UnmineableWorker("randomx", "my-rig", true, 9000, 9000)];

        await server.Factory.Services.GetRequiredService<PoolPayoutChecker>().CheckAllAsync(CancellationToken.None);

        var summary = server.Factory.Services.GetRequiredService<RealEarningsService>().Compute(server.Time.GetUtcNow());
        Assert.Equal(6, summary.PaidUsd, precision: 6); // one address, 6 USDT paid
        Assert.Equal(1, summary.PaidUsers);
        Assert.Equal(3, summary.Rewarded30dUsd, precision: 6);
        Assert.False(summary.ShowSection); // $6 from one user: far from $100 and 50 users
        Assert.NotNull(otiumOnly);
        Assert.NotNull(mixed);
    }

    [Theory]
    [InlineData(10, 1, false)] // $6 paid (and $3 in 30 days) is less than $10
    [InlineData(5, 2, false)]  // enough dollars, but only one user has earned
    [InlineData(5, 1, true)]
    public async Task Real_numbers_appear_only_once_enough_users_have_earned_enough(double minimumUsd, int minimumUsers, bool shown)
    {
        using var server = new Server(("RealEarnings:MinimumEarnedUsd", minimumUsd.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            ("RealEarnings:MinimumUsers", minimumUsers.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        await server.Register(TestAddresses.Usdt(0));
        server.Unmineable.Workers[TestAddresses.Usdt(0)] = [new UnmineableWorker("randomx", "ot1234abcd", true, 9000, 9000)];
        await server.Factory.Services.GetRequiredService<PoolPayoutChecker>().CheckAllAsync(CancellationToken.None);

        Assert.Equal(shown, server.Factory.Services.GetRequiredService<RealEarningsService>().Compute(server.Time.GetUtcNow()).ShowSection);
        server.Time.Advance(TimeSpan.FromMinutes(11)); // past the page's cache
        Assert.Equal(shown, (await server.Http.GetStringAsync("/")).Contains("id=\"real\""));
    }

    [Fact]
    public async Task Computers_that_mine_for_days_without_a_fee_slice_are_listed()
    {
        var fee = TestAddresses.Usdt(9);
        using var server = new Server(("Unmineable:OtiumTarget", fee));
        var (honest, _, _) = await server.Register(TestAddresses.Usdt(0));
        var (modified, _, _) = await server.Register(TestAddresses.Usdt(1));
        var (feeFree, _, _) = await server.Register(TestAddresses.Usdt(2));
        server.Unmineable.Workers[TestAddresses.Usdt(0)] = [new UnmineableWorker("pearlpow", $"ot{honest[..8]}g", true, 1, 1)];
        server.Unmineable.Workers[TestAddresses.Usdt(1)] = [new UnmineableWorker("pearlpow", $"ot{modified[..8]}g", true, 1, 1)];
        server.Unmineable.Workers[TestAddresses.Usdt(2)] = [new UnmineableWorker("pearlpow", $"ot{feeFree[..8]}g", true, 1, 1)];
        // Only the honest computer's fee slices reach Otium's address; the third account is still fee-free.
        server.Unmineable.Workers[fee] = [new UnmineableWorker("pearlpow", $"ot{honest[..8]}g", true, 1, 1)];
        var store = server.Factory.Services.GetRequiredService<Otium.Server.Data.OtiumStore>();
        var feeFreeAccount = store.FindDevice(feeFree)!.AccountId;
        store.Upsert(store.FindAccount(feeFreeAccount)! with { FeeFreeUntil = server.Time.GetUtcNow().AddDays(10) });

        var checker = server.Factory.Services.GetRequiredService<PoolPayoutChecker>();
        var watcher = server.Factory.Services.GetRequiredService<FeeSliceWatcher>();
        await checker.CheckAllAsync(CancellationToken.None);
        await watcher.CheckAsync(CancellationToken.None);
        Assert.Empty(store.FeeSliceGaps(server.Time.GetUtcNow(), TimeSpan.FromDays(7), TimeSpan.FromDays(2))); // too soon to tell

        server.Time.Advance(TimeSpan.FromDays(3));
        await checker.CheckAllAsync(CancellationToken.None);
        await watcher.CheckAsync(CancellationToken.None);
        var gap = Assert.Single(store.FeeSliceGaps(server.Time.GetUtcNow(), TimeSpan.FromDays(7), TimeSpan.FromDays(2)));
        Assert.Equal(modified, gap.DeviceId);
        Assert.Null(gap.FeeSeen);
    }

    [Theory]
    [InlineData("ot1234abcd", true)]
    [InlineData("ot1234abcdg", true)]
    [InlineData("otzzzzzzzz", false)]
    [InlineData("rig1", false)]
    [InlineData("ot1234abcdx", false)]
    public void Otium_workers_are_recognised_by_name(string name, bool otium) => Assert.Equal(otium, PoolPayoutChecker.IsOtiumWorker(name));

    [Fact]
    public async Task Deleting_the_account_removes_its_reports_and_exports_include_them()
    {
        using var server = new Server();
        var (device, secret, _) = await server.Register(TestAddresses.Usdt(0));
        await server.Report(device, secret, GpuReport(1, "rtx4090", 4, 0.40));

        var client = new AccountClient(server.Http);
        var export = System.Text.Json.JsonDocument.Parse(await client.ExportAccountDataAsync(device, secret, CancellationToken.None));
        Assert.Equal(2, export.RootElement.GetProperty("earningsReports").GetArrayLength());

        await client.DeleteAccountAsync(device, secret, new DeleteAccountRequest(null, false), CancellationToken.None);
        var store = server.Factory.Services.GetRequiredService<Otium.Server.Data.OtiumStore>();
        Assert.Empty(store.EarningsSince(T0 - TimeSpan.FromDays(30)));
    }
}
