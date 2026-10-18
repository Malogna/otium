using System.Net.Http;
using System.Security.Cryptography;
using Otium.Core;
using Otium.Core.Bandwidth;
using Otium.Core.Engine;
using Otium.Core.Fees;
using Otium.Core.Gpu;
using Otium.Core.Platform;
using Otium.Core.Pools;
using Otium.Core.Power;
using Otium.Core.Projections;
using Otium.Core.Providers;
using Otium.Core.Accounts;
using Otium.Core.Settings;
using Otium.Core.Switching;
using Otium.Core.Wallets;
using Otium.Providers.Honeygain;
using Otium.Providers.Mysterium;
using Otium.Providers.Proxybase;
using Otium.Providers.SrbMiner;
using Otium.Providers.XMRig;

namespace Otium.App.Services;

/// <summary>
/// Composition root: wires settings, providers, engines and the referral client together.
/// Each device (CPU, GPU) gets its own engine, ledger and fee cycle; the UI adds them up.
/// </summary>
public sealed class AppHost : IAsyncDisposable
{
    private readonly JsonFileStore<AppSettings> _settingsStore;
    private readonly HttpClient _http;
    private readonly Nvml? _nvml;
    private readonly JsonFileStore<EarningsHistory> _historyStore;
    private DateTimeOffset _lastHistorySample = DateTimeOffset.UtcNow;
    private volatile EngineConfig _cpuConfig;
    private volatile EngineConfig _gpuConfig;
    private volatile BandwidthConfig _bandwidthConfig;
    private volatile GpuPolicy? _gpuPolicy;

    private AppHost()
    {
        Paths = AppPaths.Default;
        Paths.EnsureCreated();
        // Miners an earlier Otium left running (crash, killed process) would mine on unseen; stop them first.
        MinerLifetime.StopOrphans(Paths.EnginesDir, Path.Combine(Paths.Root, "otium.lock"));
        _settingsStore = new JsonFileStore<AppSettings>(Paths.SettingsFile);
        Settings = _settingsStore.Load();
        EnsureDeviceSecrets();

        DeviceId = ComputeDeviceId();
        IsSimulated = SimulatedProvider.IsEnabled;
        FeeWallets = AppIdentity.ResolveFeeWallets();
        ReferralCode = AppIdentity.ResolveUnmineableReferral();

        _http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd($"{AppIdentity.Name}/0.1");
        Pool = new MoneroOceanClient(_http);
        var idle = IdleDetector.ForThisPlatform();
        _idle = idle;
        // The graphics card may also mine while the user does ordinary things at the computer, if they turned that on.
        var gpuIdle = new BrowsingIdleDetector(idle, () => Settings.EarnWhileBrowsing, ForegroundMonitor.ForThisPlatform(), PowerSource.OnBattery);
        _historyStore = new JsonFileStore<EarningsHistory>(Paths.HistoryFile);
        History = _historyStore.Load();
        Estimator = new EarningsEstimator(Pool);
        ElectricityPrices = new ElectricityPriceProvider(_http);

        // When a miner can't reach its pool, check whether something on this computer blocks it (antivirus web protection).
        var enginesDir = Paths.EnginesDir;
        PoolReachability = new PoolReachability(new TcpProbe(), async ct =>
            (await Antivirus.InspectAsync(enginesDir, ct))?.Products.Where(p => p.Enabled && !p.IsMicrosoftDefender).Select(p => p.Name).ToList() ?? []);

        // CPU
        Provider = new XmrigProvider(_http, Pool, Paths.EnginesDir, referralCode: ReferralCode, unitedStates: InUnitedStates,
            poolBlocked: PoolReachability.BlockedReasonAsync);
        PowerModel = CpuPowerModel.ForThisMachine(Settings.CpuFullLoadWatts);
        PowerMeter = RaplPowerMeter.IsAvailable() ? new RaplPowerMeter(() => PowerModel) : new EstimatedCpuPowerMeter(() => PowerModel);
        var cpuLedger = new JsonFileStore<EarningsLedger>(Paths.LedgerFile);
        _cpuConfig = BuildConfig(Settings.CpuUsagePercent, "", cpu: true);
        // Dev mode swaps the real miners for simulated ones that report to the local simulated pool.
        IComputeProvider cpuProvider = IsSimulated ? new SimulatedProvider(_http, Pool, DeviceKind.Cpu, "rx/0", 9_000, 1.0) : Provider;
        Engine = new EarningsEngine(cpuProvider, PowerMeter, idle, () => _cpuConfig, cpuLedger.Load(), cpuLedger.Save);

        // GPU (only when a supported card and miner build exist)
        _nvml = Nvml.TryOpen();
        Gpus = new GpuDetector(_nvml).Detect();
        GpuPowerMeter = new GpuPowerMeter(_nvml, () => Settings.GpuWatts, Gpus.Count);
        _gpuConfig = BuildConfig(100, "g");
        var gpuProvider = new SrbMinerGpuProvider(
            _http, Pool, Paths.EnginesDir, Paths.GpuBenchmarksFile, Gpus, GpuPowerMeter, EffectivePricePerKwh,
            policy: () => EffectiveGpuPolicy,
            freeMemoryGb: gpu => gpu.Vendor == GpuVendor.Nvidia ? _nvml?.ReadFreeMemoryGb(gpu.Index) : null,
            referralCode: ReferralCode,
            unitedStates: InUnitedStates,
            poolBlocked: PoolReachability.BlockedReasonAsync);
        if (IsSimulated)
        {
            var gpuLedger = new JsonFileStore<EarningsLedger>(Paths.GpuLedgerFile);
            // kawpow at RTX 3080-like speed; the factor converts to the XMR-equivalent rate the simulated pool pays for.
            var simulatedGpu = new SimulatedProvider(_http, Pool, DeviceKind.Gpu, "kawpow", 45e6, 8.7e-5, policy: () => EffectiveGpuPolicy);
            GpuEngine = new EarningsEngine(simulatedGpu, new EstimatedCpuPowerMeter(new CpuPowerModel(200, 30)), gpuIdle, () => _gpuConfig, gpuLedger.Load(), gpuLedger.Save);
        }
        else if (gpuProvider.IsSupportedOnThisPlatform)
        {
            GpuProvider = gpuProvider;
            var gpuLedger = new JsonFileStore<EarningsLedger>(Paths.GpuLedgerFile);
            GpuEngine = new EarningsEngine(gpuProvider, GpuPowerMeter, gpuIdle, () => _gpuConfig, gpuLedger.Load(), gpuLedger.Save);
        }

        // Internet sharing (opt-in). Proxybase's client earns per GB; the server routes each payout through a conversion
        // to the user's own payout address and coin;
        // builds with a Honeygain publisher key (custodial operators) report usage to the Otium server, which credits
        // the balance. (Mysterium's node, the earlier choice, stays in the code base but isn't offered.)
        var bandwidthLedger = new JsonFileStore<BandwidthLedger>(Paths.BandwidthLedgerFile);
        IBandwidthProvider bandwidthProvider = IsSimulated
            ? new SimulatedBandwidthProvider()
            : AppIdentity.ResolveHoneygainApiKey() is { } honeygainKey
                ? new HoneygainBandwidthProvider(AppContext.BaseDirectory, honeygainKey)
                : new ProxybaseBandwidthProvider(_http, Paths.EnginesDir,
                    (request, ct) => CreateClient().GetSharingRouteAsync(DeviceId, Settings.DeviceSecret, request, ct));
        UsesProxybase = bandwidthProvider is ProxybaseBandwidthProvider;
        _bandwidthConfig = BuildBandwidthConfig();
        Bandwidth = new BandwidthSharer(bandwidthProvider, idle, () => _bandwidthConfig,
            UsesProxybase ? null : (report, ct) => CreateClient().ReportBandwidthAsync(DeviceId, Settings.DeviceSecret, report, ct),
            bandwidthLedger.Load(), bandwidthLedger.Save);

        foreach (var engine in Engines)
        {
            engine.SnapshotChanged += _ =>
            {
                UpdateKeepAwake();
                UpdateGpuPowerCap();
                ObserveAway();
                ObserveMeasurement();
            };
        }
    }

    // ---------- "While you were away" ----------

    private readonly object _sessionLock = new();
    private readonly IIdleDetector _idle;

    /// <summary>The user is away by Otium's own rule (or earning isn't limited to time away).</summary>
    private bool UserAway =>
        !Settings.OnlyWhenIdle || _idle.GetIdleTime() is not { } idle || idle >= TimeSpan.FromMinutes(Math.Max(1, Settings.IdleMinutes));
    private readonly AwaySessionTracker _away = new();

    /// <summary>The last stretch of earning that ended, until the user closes its card; null when there's none.</summary>
    public AwaySummary? LastAway { get; private set; }

    public event Action? AwayChanged;

    public void DismissAway()
    {
        LastAway = null;
        AwayChanged?.Invoke();
    }

    /// <summary>
    /// Watches the engines for a stretch of earning ending (the user came back) and keeps its summary. The "measure my
    /// PC" test isn't time away, so it doesn't count.
    /// </summary>
    private void ObserveAway()
    {
        AwaySummary? ended;
        lock (_sessionLock)
        {
            var engines = Engines.ToList();
            // Earning while the user browses (or runs the test) isn't time away.
            var active = engines.Any(e => e.Snapshot.State is EngineState.Earning or EngineState.Starting) && !engines.Any(e => e.IsRunningNow) && UserAway;
            var totals = engines.Select(e => e.Totals).ToList();
            ended = _away.Observe(DateTimeOffset.Now, active, totals.Sum(t => t.UserUsd), totals.Sum(t => t.ElectricityUsd));
            if (ended is not null)
                LastAway = ended;
        }
        if (ended is not null)
            AwayChanged?.Invoke();
    }

    // ---------- "Measure my PC" ----------

    /// <summary>The test under way or just finished; null before the first one this session.</summary>
    public PcMeasurementRun? MeasurementRun { get; private set; }

    public event Action? MeasurementChanged;

    /// <summary>Why the test can't run now, or null when it can.</summary>
    public string? MeasurementBlocker =>
        !Settings.OnboardingComplete ? "Finish setting up Otium first."
        : !Settings.EarningEnabled ? "Press Start first: the test earns for real, to your address."
        : MiningBlocker is { } blocker ? blocker
        : !Engines.Any(e => e.Enabled) ? "Turn on the processor or the graphics card in Settings first."
        : null;

    public bool IsMeasuring => MeasurementRun is { Phase: not MeasurementPhase.Done };

    /// <summary>
    /// Starts the two-minute test: the miners earn now (to the user's address, fee slices as usual) even though the user
    /// is at the computer, and their measured rates replace the hardware estimate. Returns why it can't, or null.
    /// </summary>
    public string? StartMeasurement()
    {
        lock (_sessionLock)
        {
            if (IsMeasuring)
                return null;
            if (MeasurementBlocker is { } blocker)
                return blocker;
            MeasurementRun = new PcMeasurementRun(DateTimeOffset.UtcNow);
            foreach (var engine in Engines.Where(e => e.Enabled))
                engine.RunNow(PcMeasurementRun.RunNowWindow);
        }
        MeasurementChanged?.Invoke();
        return null;
    }

    /// <summary>Ends the test now, keeping whatever it measured.</summary>
    public void StopMeasurement()
    {
        lock (_sessionLock)
        {
            if (MeasurementRun is not { Phase: not MeasurementPhase.Done } run)
                return;
            run.Stop();
            FinishMeasurement(run);
        }
        MeasurementChanged?.Invoke();
    }

    /// <summary>Feeds the test the engines' latest numbers; ends it when it has enough.</summary>
    public void ObserveMeasurement()
    {
        lock (_sessionLock)
        {
            if (MeasurementRun is not { Phase: not MeasurementPhase.Done } run)
                return;
            var devices = new List<MeasuredDevice> { new(DeviceKind.Cpu, CpuInfo.Name, Engine.Snapshot, "rx/0") };
            if (GpuEngine is not null && GpuEngine.Enabled)
                devices.Add(new MeasuredDevice(DeviceKind.Gpu, GpuProvider?.Gpus.FirstOrDefault()?.Name ?? "Graphics card", GpuEngine.Snapshot));
            if (!Engine.Enabled)
                devices.RemoveAt(0);
            run.Observe(DateTimeOffset.UtcNow, devices);
            if (run.Phase == MeasurementPhase.Done)
                FinishMeasurement(run);
        }
        MeasurementChanged?.Invoke();
    }

    private void FinishMeasurement(PcMeasurementRun run)
    {
        foreach (var engine in Engines)
            engine.EndRunNow(); // back to earning only while the user is away
        if (run.Result() is { } result)
        {
            Settings.Measurement = result;
            _settingsStore.Save(Settings);
        }
    }

    // ---------- Opt-in earnings reports for otium.lol ----------

    private int _reporting;

    /// <summary>The devices the reports cover, with their models and running totals.</summary>
    private List<ReportedDevice> ReportedDevices()
    {
        var devices = new List<ReportedDevice> { new("cpu", CpuInfo.Name, Engine.Totals) };
        if (GpuEngine is not null)
            devices.Add(new ReportedDevice("gpu", CardIds.FromName(GpuProvider?.Gpus.FirstOrDefault()?.Name) ?? GpuProvider?.Gpus.FirstOrDefault()?.Name, GpuEngine.Totals));
        return devices;
    }

    /// <summary>Turns the opt-in on or off. Turning it on counts from now: nothing earned before is sent.</summary>
    public void SetShareEarningsStats(bool on)
    {
        if (on && !Settings.ShareEarningsStats)
            EarningsReports.Reset(Settings.EarningsReports, ReportedDevices(), DateTimeOffset.UtcNow);
        Settings.ShareEarningsStats = on;
        _settingsStore.Save(Settings);
    }

    /// <summary>When the user agreed and a report is due, sends what was earned since the last one. Failures retry later.</summary>
    public async Task ReportEarningsAsync(CancellationToken ct)
    {
        if (!Settings.ShareEarningsStats || !Settings.OnboardingComplete || Settings.Account.WorkerId is null)
            return;
        if (Interlocked.Exchange(ref _reporting, 1) == 1)
            return;
        try
        {
            if (EarningsReports.Next(Settings.EarningsReports, ReportedDevices(), DateTimeOffset.UtcNow) is not { } pending)
            {
                _settingsStore.Save(Settings); // the first call sets the baselines
                return;
            }
            await CreateClient().ReportEarningsAsync(DeviceId, Settings.DeviceSecret, pending.Report, ct);
            EarningsReports.Commit(Settings.EarningsReports, pending);
            _settingsStore.Save(Settings);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or AccountRequestException or UriFormatException)
        {
            // Not important enough to bother the user: the same period goes out with the next attempt.
        }
        finally
        {
            Interlocked.Exchange(ref _reporting, 0);
        }
    }

    private readonly ISleepInhibitor _sleep = SleepInhibitor.ForThisPlatform();

    // ---------- Graphics card efficiency mode ----------

    private int _capApplied = -1; // -1 unknown (restore once at start), 0 restored, 1 capped

    /// <summary>NVIDIA cards on Windows can use efficiency mode.</summary>
    public bool GpuEfficiencySupported => GpuEfficiency.IsSupported && !IsSimulated && Gpus.Any(g => g.Vendor == GpuVendor.Nvidia);

    /// <summary>
    /// Turns efficiency mode on (setting up its tasks with one administrator prompt, the first time) or off. Returns
    /// what to tell the user.
    /// </summary>
    public async Task<string> SetGpuEfficiencyAsync(bool on)
    {
        if (on && !GpuEfficiency.IsSetUp())
        {
            if (Environment.ProcessPath is not { } self)
                return "Couldn't find Otium's program file to ask Windows for permission.";
            try
            {
                using var helper = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(self, [GpuEfficiency.SetupArgument])
                {
                    UseShellExecute = true,
                    Verb = "runas", // the Windows administrator prompt
                    WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden,
                });
                if (helper is null)
                    return "Couldn't ask Windows for permission. Try again.";
                await helper.WaitForExitAsync();
                if (helper.ExitCode != 0)
                    return $"Windows didn't set it up (error {helper.ExitCode}). Your graphics card mines at full power as before.";
            }
            catch (System.ComponentModel.Win32Exception e) when (e.NativeErrorCode == 1223)
            {
                return "Nothing changed, because the Windows permission prompt was cancelled.";
            }
        }
        Settings.GpuEfficiencyMode = on;
        SaveSettings();
        UpdateGpuPowerCap();
        return on
            ? $"On. While it mines, your graphics card runs at {GpuEfficiency.CapPercent}% of its usual power limit, and back at full power the moment it stops."
            : "Off. Your graphics card mines at its usual power limit.";
    }

    /// <summary>
    /// Caps the power limit while the graphics card is mining with efficiency mode on, and restores it as soon as it
    /// isn't (and once at start, in case Otium closed while capped). Called on every graphics card engine change.
    /// </summary>
    public void UpdateGpuPowerCap()
    {
        if (!GpuEfficiencySupported || GpuEngine is null)
            return;
        var mining = GpuEngine.Snapshot.State is EngineState.Starting or EngineState.Earning;
        var want = Settings.GpuEfficiencyMode && mining ? 1 : 0;
        if (Interlocked.Exchange(ref _capApplied, want) == want)
            return;
        if (want == 0 && !GpuEfficiency.IsSetUp())
            return; // never set up: nothing to restore
        _ = Task.Run(() => GpuEfficiency.Trigger(cap: want == 1));
    }

    /// <summary>
    /// "Speed up processor mining": runs Otium once with administrator rights (Windows asks) to give this account the
    /// "Lock pages in memory" right, which the miner needs for large memory pages. Returns what to tell the user.
    /// </summary>
    public async Task<string> GrantLargePagesAsync()
    {
        if (!OperatingSystem.IsWindows())
            return "Large memory pages are set up by your system administrator on this operating system.";
        if (Environment.ProcessPath is not { } self)
            return "Couldn't find Otium's program file to ask Windows for permission.";
        try
        {
            using var helper = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(self, [LargePages.GrantArgument, LargePages.CurrentUserSid()])
            {
                UseShellExecute = true,
                Verb = "runas", // the Windows administrator prompt
                WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden,
            });
            if (helper is null)
                return "Couldn't ask Windows for permission. Try again.";
            await helper.WaitForExitAsync();
            return helper.ExitCode == 0
                ? "Done. Sign out of Windows and back in (or restart) to finish; processor mining is faster from then on."
                : $"Windows didn't allow it (error {helper.ExitCode}). Processor mining keeps working as before.";
        }
        catch (System.ComponentModel.Win32Exception e) when (e.NativeErrorCode == 1223)
        {
            return "Nothing changed, because the Windows permission prompt was cancelled. Processor mining keeps working as before.";
        }
    }

    /// <summary>Whether Otium is currently keeping the computer awake because it's earning.</summary>
    public bool KeepingAwake => _sleep.IsHeld;

    /// <summary>
    /// Holds off system sleep while a miner is starting or earning on mains power, and lets the computer sleep again
    /// as soon as nothing is (paused, waiting for you to leave, or on battery). Called on every engine change and once
    /// a minute, which also notices the charger being unplugged.
    /// </summary>
    public void UpdateKeepAwake()
    {
        if (KeepAwakePolicy.ShouldHold(Settings.KeepAwakeWhileEarning, Engines.Select(e => e.Snapshot.State), PowerSource.OnBattery()))
            _sleep.Hold();
        else
            _sleep.Release();
    }

    public static AppHost Create() => new();

    /// <summary>Tells a blocked pool apart from a dead internet connection, naming the security product to look at.</summary>
    public PoolReachability PoolReachability { get; }

    public EarningsHistory History { get; }

    public EarningsEstimator Estimator { get; }

    /// <summary>Live day-ahead spot shape that varies the electricity price through the day.</summary>
    public ElectricityPriceProvider ElectricityPrices { get; }

    /// <summary>
    /// The electricity price to charge mining with right now: zero when the user doesn't pay for power, otherwise
    /// their retail rate scaled by today's live hourly spot shape (so cheap hours cost less, expensive hours more).
    /// </summary>
    public double EffectivePricePerKwh()
    {
        if (!Settings.PayForElectricity)
            return 0;
        var shape = ElectricityPrices.CurrentMultiplier() ?? 1.0;
        return Math.Max(0, Settings.ElectricityPricePerKwh * shape);
    }

    /// <summary>The day's average price (zero when the user doesn't pay), for projections that span a whole day.</summary>
    public double AveragePricePerKwh() => Settings.PayForElectricity ? Settings.ElectricityPricePerKwh : 0;

    /// <summary>Fetches today's live prices for the user's zone. Non-fatal; the flat price is used if it fails.</summary>
    public async Task RefreshElectricityAsync(CancellationToken ct)
    {
        await ElectricityPrices.RefreshAsync(SpotPriceZone.Find(Settings.ElectricityZone), ct);
        RebuildEngineConfigs();
    }

    /// <summary>Dev mode (OTIUM_SIMULATE=1): simulated miners instead of XMRig/SRBMiner.</summary>
    public bool IsSimulated { get; }

    public AppPaths Paths { get; }

    public AppSettings Settings { get; }

    public string DeviceId { get; private set; }

    /// <summary>Otium's fee targets from the build, one per pool (e.g. "USDT:T…" for unMineable, "XMR:4…" for MoneroOcean).</summary>
    public IReadOnlyList<string> FeeWallets { get; }

    /// <summary>The fee target on the user's pool, or null when the build has none for it (the fee then stays with the user).</summary>
    public string? FeeWallet => PayoutRouting.FeeTargetFor(OwnPayoutTarget, FeeWallets, InUnitedStates);

    /// <summary>Otium's unMineable referral code, added to every unMineable login.</summary>
    public string? ReferralCode { get; }

    /// <summary>The user's own payout target in canonical form ("USDT:T…", "XMR:4…"), or null while it isn't valid.</summary>
    public string? OwnPayoutTarget => PayoutTarget.ParseOrNull(Settings.PayoutAddress)?.ToString();

    /// <summary>
    /// Always null: everything Otium earns is paid to the user's one payout address, so the graphics card no longer
    /// mines Quantus (which Kryptex pays only to a separate Quantus wallet). A Quantus wallet saved by an earlier
    /// version is ignored. The Quantus code stays in the code base, unused.
    /// </summary>
    public string? QuantusTarget => null;

    /// <summary>Otium's Quantus fee target from the build ("QTC:qz…"), or null (Quantus mined in the fee slice then stays with the user).</summary>
    public string? QuantusFeeWallet => FeeWallets.Select(PayoutRouting.QuantusTarget).FirstOrDefault(t => t is not null);

    /// <summary>Always null now (see <see cref="QuantusTarget"/>).</summary>
    public PayoutBalance? QuantusBalance => null;

    /// <summary>
    /// Whether the server holds balances (custodial). Until it has answered, assume not: the app then offers only
    /// payouts to the user's own address, which work with any server.
    /// </summary>
    public bool ServerIsCustodial => Account?.Custodial ?? false;

    /// <summary>What the pool holds for the user's own address, refreshed every few minutes; null until known.</summary>
    public PayoutBalance? PoolBalance { get; private set; }

    /// <summary>Why the pool balance couldn't be read last time, or null.</summary>
    public string? PoolBalanceError { get; private set; }

    private DateTimeOffset _poolBalanceCheckedAt;
    private string? _poolBalanceTarget;

    /// <summary>Reads the pool balance for the user's own address (at most every 5 minutes unless forced).</summary>
    public async Task RefreshPoolBalanceAsync(bool force, CancellationToken ct)
    {
        var target = Settings.PayoutMode == PayoutMode.OwnWallet ? OwnPayoutTarget : null;
        if (target is null)
        {
            PoolBalance = null;
            PoolBalanceError = null;
            return;
        }
        var now = DateTimeOffset.UtcNow;
        var key = $"{target}|{QuantusTarget}";
        if (!force && key == _poolBalanceTarget && now - _poolBalanceCheckedAt < TimeSpan.FromMinutes(5))
            return;
        _poolBalanceCheckedAt = now;
        if (key != _poolBalanceTarget)
        {
            PoolBalance = null; // a different address: don't show the old one's balance meanwhile
        }
        _poolBalanceTarget = key;
        try
        {
            IComputeProvider provider = IsSimulated ? Engine.Provider : Provider;
            PoolBalance = await provider.GetPayoutBalanceAsync(target, ct);
            PoolBalanceError = null;
            // Processor and graphics card share one pool now, so this only applies if they ever mine on different ones.
            GpuPoolBalance = GpuPayoutPool == PayoutPool.Unmineable && PayoutRouting.PoolFor(target, InUnitedStates) != PayoutPool.Unmineable && GpuEngine is not null
                ? await UnmineableClient.GetPayoutBalanceAsync(new UnmineableClient(_http), PayoutTarget.ParseOrNull(target)!, ct)
                : null;
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or InvalidOperationException or System.Text.Json.JsonException && !ct.IsCancellationRequested)
        {
            PoolBalanceError = "Couldn't reach the pool to check your balance. Retrying in a few minutes.";
        }
        AccountChanged?.Invoke();
    }

    /// <summary>The fee and invite terms in force: the server's, clamped, or the built-in defaults until it has answered.</summary>
    public FeePolicy Fees => FeePolicy.FromTerms(Settings.Account.Fees);

    /// <summary>unMineable's terms exclude US persons, so US users are paid in Monero by MoneroOcean instead.</summary>
    public static bool InUnitedStates => System.Globalization.RegionInfo.CurrentRegion.TwoLetterISORegionName == "US";

    public IMoneroPoolApi Pool { get; }

    public XmrigProvider Provider { get; }

    public CpuPowerModel PowerModel { get; private set; }

    public IPowerMeter PowerMeter { get; }

    /// <summary>The CPU engine.</summary>
    public EarningsEngine Engine { get; }

    /// <summary>All detected GPUs, including unsupported ones (shown in Settings).</summary>
    public IReadOnlyList<GpuInfo> Gpus { get; }

    public GpuPowerMeter GpuPowerMeter { get; }

    /// <summary>Null when there's no supported GPU or no miner build for this OS.</summary>
    public SrbMinerGpuProvider? GpuProvider { get; }

    public EarningsEngine? GpuEngine { get; }

    public IEnumerable<EarningsEngine> Engines => GpuEngine is null ? [Engine] : [Engine, GpuEngine];

    /// <summary>
    /// The server's GPU policy as it applies to this user. Balance accounts get all of it. Own-wallet users get the
    /// routes of the pool that pays their coin: MoneroOcean for Monero (null until fetched, when the GPU miner falls
    /// back to MoneroOcean's own prices), unMineable for every other coin (nothing until fetched, since unMineable's
    /// prices come from the server).
    /// </summary>
    public GpuPolicy? EffectiveGpuPolicy
    {
        get
        {
            if (Settings.PayoutMode == PayoutMode.Balance)
                return _gpuPolicy;
            string[] quantus = QuantusTarget is null ? [] : [GpuRoutes.KryptexAddress];
            // The graphics card mines on the same pool as the processor: unMineable, or MoneroOcean for US users' Monero.
            return PayoutRouting.PoolFor(Settings.PayoutAddress, InUnitedStates) == PayoutPool.Unmineable
                ? (_gpuPolicy ?? new GpuPolicy(DateTimeOffset.MinValue, [], [])).OnlyRoutes([GpuRoutes.Unmineable, .. quantus])
                : _gpuPolicy?.OnlyRoutes([GpuRoutes.MoneroOcean, .. quantus]);
        }
    }

    /// <summary>The pool the graphics card mines on for the user's payout coin: the processor's (see <see cref="PayoutRouting.PoolFor"/>).</summary>
    public PayoutPool? GpuPayoutPool => PayoutRouting.PoolFor(OwnPayoutTarget, InUnitedStates);

    /// <summary>
    /// For Monero users whose graphics card mines on unMineable: what unMineable holds for their address (paid out
    /// separately from MoneroOcean, at its own minimum). Null otherwise or until read.
    /// </summary>
    public PayoutBalance? GpuPoolBalance { get; private set; }

    /// <summary>Shares unused internet bandwidth when the user has opted in.</summary>
    public BandwidthSharer Bandwidth { get; }

    /// <summary>The server pays for sharing and this computer can run the partner's component.</summary>
    public bool BandwidthOffered => UsesProxybase
        ? Settings.PayoutMode == PayoutMode.OwnWallet && !Settings.Account.ProxybaseDisabled && ProxybaseRelease.ForCurrentPlatform() is not null
        : Settings.PayoutMode == PayoutMode.Balance && Settings.Account.BandwidthAvailable && Bandwidth.Provider.UnavailableReason is null;

    /// <summary>Internet sharing runs a Mysterium node that pays the user directly (not a partner paying Otium).</summary>
    public bool UsesProxybase { get; }

    /// <summary>
    /// The 0x address internet sharing pays (Proxybase: USDC on Tempo): the one the user gave for sharing, or their
    /// payout address when it's an 0x address (the same key works on every EVM network). Null until there is one.
    /// </summary>
    public string? BandwidthBeneficiary =>
        CryptoAddresses.TryValidate(AddressFormat.Evm, Settings.BandwidthPayoutAddress, out _)
            ? Settings.BandwidthPayoutAddress!.Trim().ToLowerInvariant()
            : PayoutTarget.ParseOrNull(Settings.PayoutAddress) is { Coin.Format: AddressFormat.Evm } evm ? evm.Address : null;

    public FeeSchedule FeeSchedule => _cpuConfig.FeeSchedule;

    public bool IsEarning => Engines.Any(e => e.Enabled);

    public UpdateService Updates { get; } = UpdateService.Create();

    /// <summary>Last error talking to the Otium server, for display; null when the last sync worked.</summary>
    public string? LastServerError { get; private set; }

    /// <summary>The most recent account summary from the server (balance, cash-outs, referrals).</summary>
    public AccountSummary? Account { get; private set; }

    public event Action? AccountChanged;

    /// <summary>Why mining can't start yet, or null when it can.</summary>
    public string? MiningBlocker => Settings.PayoutMode switch
    {
        PayoutMode.OwnWallet when OwnPayoutTarget is null =>
            "Add the address you want to be paid at in Advanced settings.",
        PayoutMode.OwnWallet when InUnitedStates && PayoutRouting.PoolFor(Settings.PayoutAddress, unitedStates: true) == PayoutPool.Unmineable =>
            "unMineable, which pays out in your chosen coin, doesn't serve US residents. Choose Monero (XMR) as your payout coin in Advanced settings.",
        PayoutMode.Balance when Settings.Account.WorkerId is null || ValidWalletOrNull(Settings.Account.PoolWallet) is null =>
            LastServerError is null ? "Connecting to Otium…" : "Can't reach Otium right now. Retrying automatically.",
        _ => null,
    };

    public void StartEngines()
    {
        foreach (var engine in Engines)
            engine.Start();
        Bandwidth.Start();
    }

    /// <summary>
    /// Opts in to (or out of) internet sharing. Turning it on records the user's consent (given on Otium's explanation
    /// screen) with the partner component too; turning it off withdraws it there as well.
    /// </summary>
    public async Task SetBandwidthSharingAsync(bool on)
    {
        var provider = Bandwidth.Provider;
        if (on && provider.UnavailableReason is null)
            await provider.OptInAsync(CancellationToken.None); // before starting: the partner won't share without it

        Settings.BandwidthEnabled = on;
        Settings.BandwidthConsentAt = on ? Settings.BandwidthConsentAt ?? DateTimeOffset.UtcNow : null;
        SaveSettings();
        ApplyDeviceSwitches();

        if (!on && provider.UnavailableReason is null)
            await provider.OptOutAsync(CancellationToken.None);
    }

    /// <summary>The main switch: turns earning on for every device the user has enabled, or off for all.</summary>
    public void SetEarning(bool on)
    {
        Settings.EarningEnabled = on;
        ApplyDeviceSwitches();
        SaveSettings();
    }

    /// <summary>
    /// The one-click start: accepts the terms shown on the welcome screen, turns on start-at-sign-in and
    /// earning, then connects to the server (mining begins as soon as the account is ready).
    /// </summary>
    /// <param name="payoutTarget">The address chosen during onboarding ("USDT:T…"), paid by the pool directly.</param>
    /// <param name="shareEarnings">"Help show what PCs earn" stayed ticked (see <see cref="SetShareEarningsStats"/>).</param>
    /// <param name="keepMinerReady">The user agreed to keep the processor miner paused, not stopped, when they come back.</param>
    /// <param name="shareInternet">The user agreed to internet sharing on the welcome screen (Quick start, or ticked).</param>
    public async Task StartFirstTimeAsync(string? inviteCode, string? payoutTarget = null, bool shareEarnings = false, bool keepMinerReady = false,
        bool shareInternet = false)
    {
        SetShareEarningsStats(shareEarnings);
        Settings.KeepMinerReadyWhenBack = keepMinerReady;
        if (PayoutTarget.ParseOrNull(payoutTarget) is { } target)
        {
            Settings.PayoutMode = PayoutMode.OwnWallet;
            Settings.PayoutAddress = target.ToString();
        }
        Settings.AcceptedTerms = true;
        Settings.OnboardingComplete = true;
        Settings.Referral.CodeUsed = string.IsNullOrWhiteSpace(inviteCode) ? null : ReferralCodes.Normalize(inviteCode);
        try
        {
            AutoStart.Set(true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            // Not fatal: the user can still start Otium by hand.
        }
        SetEarning(true);
        await SyncAccountAsync(CancellationToken.None);
        // Internet sharing, agreed to on the welcome screen. It starts once the account (and the server's switch) allows it.
        if (shareInternet && Bandwidth.Provider.UnavailableReason is null)
        {
            try
            {
                await SetBandwidthSharingAsync(true);
            }
            catch (InvalidOperationException)
            {
                // Not fatal: Settings can turn it on later.
            }
        }
        else if (shareInternet)
        {
            // Not possible yet (e.g. no address it can pay): remember the agreement so the overview can finish it.
            Settings.BandwidthPromptDismissed = false;
        }

        // Measure this PC right away (the user is watching), so the overview shows its own numbers, not a table's.
        if (Settings.Measurement is null && !Settings.MeasurementSkipped)
            StartMeasurement();
    }

    /// <summary>Re-applies the per-device toggles after the main switch or settings change.</summary>
    public void ApplyDeviceSwitches()
    {
        var on = Settings.EarningEnabled && Settings.OnboardingComplete && MiningBlocker is null;
        Toggle(Engine, on && Settings.CpuEnabled);
        if (GpuEngine is not null)
            Toggle(GpuEngine, on && Settings.GpuEnabled);
        if (on && Settings.BandwidthEnabled && Settings.BandwidthConsentAt is not null)
            Bandwidth.Enable();
        else
            Bandwidth.Disable();

        static void Toggle(EarningsEngine engine, bool enabled)
        {
            if (enabled && !engine.Enabled)
                engine.Enable();
            else if (!enabled && engine.Enabled)
                engine.Disable();
        }
    }

    /// <summary>Fee shown in estimates: the server's fee for balance accounts, the mined fee for own-wallet mode.</summary>
    public double EffectiveFeePercent => Settings.PayoutMode == PayoutMode.Balance
        ? (Account?.Balance.FeePercent ?? (Settings.Account.FeePercent > 0 ? Settings.Account.FeePercent : 10))
        : Fees.FeePercent;

    /// <summary>What this computer could earn per 24 h of mining, from its hardware and today's payouts.</summary>
    public Task<EarningsEstimate> EstimateAsync(CancellationToken ct) =>
        Estimator.EstimateAsync(CpuInfo.Name, Environment.ProcessorCount, Settings.CpuUsagePercent, PowerModel,
            Settings.GpuEnabled ? Gpus : [], AveragePricePerKwh(), EffectiveFeePercent, ct, EffectiveGpuPolicy);

    /// <summary>Samples running totals into the hour-by-hour history (call about once a minute).</summary>
    public void RecordHistory()
    {
        var now = DateTimeOffset.Now;
        var snapshots = Engines.Select(e => e.Snapshot).ToList();
        var earning = snapshots.Any(s => s.State == EngineState.Earning);
        // Earning hours count mining only: they scale the mining projection. Sharing is projected separately.
        History.Record(now,
            snapshots.Sum(s => s.TotalUserUsd) + Bandwidth.Snapshot.TotalUserUsd,
            snapshots.Sum(s => s.TotalElectricityUsd),
            earning ? now - _lastHistorySample : TimeSpan.Zero);
        _lastHistorySample = now;
        _historyStore.Save(History);
        HistoryChanged?.Invoke();
    }

    public event Action? HistoryChanged;

    /// <summary>Stops every miner (so none is left orphaned), then lets Velopack install the update and relaunch.</summary>
    public async Task ApplyUpdateAndRestartAsync()
    {
        foreach (var engine in Engines)
            await engine.DisposeAsync();
        await Bandwidth.DisposeAsync();
        await Provider.DisposeAsync();
        if (GpuProvider is not null)
            await GpuProvider.DisposeAsync();
        await Bandwidth.Provider.DisposeAsync();
        _settingsStore.Save(Settings);
        Updates.ApplyAndRestart();
    }

    /// <summary>
    /// One-click fix for an antivirus block on Windows: after the user's OK at the Windows prompt, allows only the
    /// engines folder in Microsoft Defender, then retries (which downloads fresh copies of the removed engines).
    /// Returns null on success, or a user-facing reason.
    /// </summary>
    public async Task<string?> AllowEnginesInWindowsSecurityAsync()
    {
        var problem = await WindowsSecurity.AllowFolderAsync(Paths.EnginesDir);
        if (problem is null)
            ApplyDeviceSwitchesAfterBlock();
        return problem;
    }

    /// <summary>What's guarding this computer and what it removed from the engines folder; null until checked.</summary>
    public AntivirusReport? AntivirusReport { get; private set; }

    public event Action? AntivirusChanged;

    private DateTimeOffset _antivirusCheckedAt;

    /// <summary>Re-reads the antivirus report (at most once a minute), after an engine was blocked.</summary>
    public async Task RefreshAntivirusAsync()
    {
        var now = DateTimeOffset.UtcNow;
        if (now - _antivirusCheckedAt < TimeSpan.FromMinutes(1))
            return;
        _antivirusCheckedAt = now;
        AntivirusReport = await Antivirus.InspectAsync(Paths.EnginesDir, CancellationToken.None);
        AntivirusChanged?.Invoke();
    }

    /// <summary>"Try again" after an antivirus block: re-enabling clears the engine's blocked/error state.</summary>
    public void ApplyDeviceSwitchesAfterBlock()
    {
        foreach (var engine in Engines.Where(e => e.Enabled))
            engine.Enable();
    }

    public void SaveSettings()
    {
        _settingsStore.Save(Settings);
        RebuildEngineConfigs();
    }

    /// <summary>Rebuilds each engine's config (including the current electricity price) and wakes them to re-apply it.</summary>
    private void RebuildEngineConfigs()
    {
        _cpuConfig = BuildConfig(Settings.CpuUsagePercent, "", cpu: true);
        _gpuConfig = BuildConfig(100, "g");
        _bandwidthConfig = BuildBandwidthConfig();
        foreach (var engine in Engines)
            engine.Nudge();
        Bandwidth.Nudge();
    }

    /// <summary>Settings affecting the power estimate need the model rebuilt.</summary>
    public void RefreshPowerModel()
    {
        PowerModel = CpuPowerModel.ForThisMachine(Settings.CpuFullLoadWatts);
    }

    /// <summary>Forget GPU benchmarks and, if the GPU is mining, restart it so testing begins now.</summary>
    public void RetestGpu()
    {
        if (GpuProvider is null || GpuEngine is null)
            return;
        var wasEnabled = GpuEngine.Enabled;
        GpuEngine.Disable();
        GpuProvider.ResetBenchmarks();
        if (wasEnabled)
            GpuEngine.Enable();
    }

    /// <summary>
    /// Balance mode: mine to the operator's pool wallet under this account's worker id; the server applies the
    /// fee and referral rewards when crediting, so no fee slices are mined client-side.
    /// Own-wallet mode: mine to the user's wallet with the time-sliced fee and referral schedule.
    /// </summary>
    private EngineConfig BuildConfig(int usagePercent, string workerSuffix, bool cpu = false)
    {
        if (Settings.PayoutMode == PayoutMode.Balance)
        {
            var poolWallet = ValidWalletOrNull(Settings.Account.PoolWallet) ?? "";
            return new EngineConfig(
                poolWallet,
                usagePercent,
                Settings.OnlyWhenIdle,
                TimeSpan.FromMinutes(Math.Max(1, Settings.IdleMinutes)),
                Settings.NeverRunAtLoss,
                EffectivePricePerKwh(),
                new FeeSchedule(FeePolicy.Default with { FeePercent = 0 }, poolWallet, null, new ReferralStanding(null, null, null)),
                // CPU and GPU share one worker id so the pool reports their combined work for this account.
                Settings.Account.WorkerId ?? "",
                // The CPU mines wherever the server routes this account (MoneroOcean or Kryptex XMR).
                cpu ? Settings.Account.CpuPool : null,
                cpu && Settings.KeepMinerReadyWhenBack);
        }

        // Fee and referral slices mine on the user's own pool, so a referrer paid on the other pool gets no slice.
        var referral = Settings.Referral;
        var user = OwnPayoutTarget ?? Settings.PayoutAddress;
        var referrer = PayoutRouting.SamePool(user, referral.ReferrerWallet, InUnitedStates) ? PayoutTarget.Normalize(referral.ReferrerWallet!) : null;
        var standing = new ReferralStanding(referrer, referral.ReferredAt, referral.FeeFreeUntil);
        return new EngineConfig(
            user,
            usagePercent,
            Settings.OnlyWhenIdle,
            TimeSpan.FromMinutes(Math.Max(1, Settings.IdleMinutes)),
            Settings.NeverRunAtLoss,
            EffectivePricePerKwh(),
            // Only the graphics card mines Quantus, so only its slices carry the Quantus targets.
            new FeeSchedule(Fees, user, FeeWallet, standing, cpu ? null : QuantusTarget, QuantusFeeWallet),
            $"ot{DeviceId[..8]}{workerSuffix}",
            PauseWhenBack: cpu && Settings.KeepMinerReadyWhenBack);
    }

    private BandwidthConfig BuildBandwidthConfig()
    {
        var account = Settings.Account;
        var userLimit = (long)(Math.Clamp(Settings.BandwidthDailyLimitGb, 0.1, 100) * 1024 * 1024 * 1024);
        var serverLimit = account.BandwidthServerDailyLimitBytes > 0 ? account.BandwidthServerDailyLimitBytes : long.MaxValue;
        return new BandwidthConfig(
            Settings.BandwidthEnabled && Settings.BandwidthConsentAt is not null && BandwidthOfferedBySettings(),
            Settings.BandwidthOnlyWhenIdle,
            TimeSpan.FromMinutes(Math.Max(1, Settings.IdleMinutes)),
            Math.Min(userLimit, serverLimit),
            account.BandwidthUserUsdPerGb,
            account.BandwidthUserUsdPerSharingHour,
            account.WorkerId ?? "");

        // Bandwidth isn't constructed yet on the first call, so check the provider separately in BandwidthOffered.
        bool BandwidthOfferedBySettings() => UsesProxybase
            ? Settings.PayoutMode == PayoutMode.OwnWallet && !account.ProxybaseDisabled
            : Settings.PayoutMode == PayoutMode.Balance && account.BandwidthAvailable && account.WorkerId is not null;
    }

    /// <summary>
    /// Registers this device with the Otium server (creating its account and redeeming a pending invite code)
    /// or refreshes balance, cash-outs and referral stats. Failure is non-fatal and retried by the caller's timer.
    /// </summary>
    public async Task SyncAccountAsync(CancellationToken ct)
    {
        var referral = Settings.Referral;
        var account = Settings.Account;
        try
        {
            var client = CreateClient();
            // Monero targets register as the bare address, which servers before the non-custodial switch expect.
            var ownWallet = Settings.PayoutMode == PayoutMode.OwnWallet && OwnPayoutTarget is { } own
                ? PayoutRouting.PoolFor(own, InUnitedStates) == PayoutPool.MoneroOcean ? PayoutRouting.MoneroOceanWallet(own) : own
                : null;
            var needsRegister = account.WorkerId is null
                                || referral.MyCode is null
                                || referral.RegisteredPayoutAddress != ownWallet
                                || (referral.CodeUsed is not null && referral.ReferredAt is null);
            var summary = needsRegister
                ? await client.RegisterAsync(new RegisterDeviceRequest(DeviceId, Settings.DeviceSecret, ownWallet, referral.CodeUsed), ct)
                : await client.GetSummaryAsync(DeviceId, Settings.DeviceSecret, ct);

            Apply(summary);
            referral.RegisteredPayoutAddress = ownWallet;
            LastServerError = null;
            await RefreshGpuPolicyAsync(client, ct);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or UriFormatException or InvalidOperationException or AccountRequestException)
        {
            LastServerError = e is AccountRequestException rejected ? rejected.Message : "Can't reach Otium right now.";
        }

        SaveSettings();
        ApplyDeviceSwitches();
        AccountChanged?.Invoke();
        // The pool pays own-address users directly, so its balance doesn't depend on reaching the Otium server.
        await RefreshPoolBalanceAsync(force: false, ct);
    }

    /// <summary>Fetches the GPU policy (it needs no account, so the welcome screen's estimate can use it).</summary>
    public Task RefreshGpuPolicyAsync(CancellationToken ct) => RefreshGpuPolicyAsync(CreateClient(), ct);

    /// <summary>Fetches the GPU policy; on failure the last one stays in effect.</summary>
    private async Task RefreshGpuPolicyAsync(AccountClient client, CancellationToken ct)
    {
        try
        {
            _gpuPolicy = await client.GetGpuPolicyAsync(ct);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or AccountRequestException or System.Text.Json.JsonException)
        {
        }
    }

    /// <summary>Asks the server to pay out the available balance. Throws <see cref="AccountRequestException"/> with a user-facing reason.</summary>
    public async Task<CashoutInfo> RequestCashoutAsync(string method, string destination)
    {
        var cashout = await CreateClient().RequestCashoutAsync(DeviceId, Settings.DeviceSecret, new CashoutRequest(method, destination.Trim()), CancellationToken.None);
        if (method == CashoutMethods.PayPal)
        {
            Settings.Account.LastPayPalEmail = destination.Trim();
        }
        else
        {
            Settings.Account.LastAddresses[method] = destination.Trim();
            Settings.Account.LastCashoutMethod = method;
        }
        await SyncAccountAsync(CancellationToken.None);
        return cashout;
    }

    /// <summary>This month's top inviters. Throws on network errors; the invites page shows it as unavailable.</summary>
    public Task<InviteLeaderboard> GetInviteLeaderboardAsync(CancellationToken ct) =>
        CreateClient().GetInviteLeaderboardAsync(DeviceId, Settings.DeviceSecret, ct);

    /// <summary>Emails a 6-digit code. Registers this computer first if it never connected.</summary>
    public async Task SendEmailCodeAsync(string email)
    {
        if (Settings.Account.WorkerId is null)
            await SyncAccountAsync(CancellationToken.None);
        await CreateClient().SendEmailCodeAsync(DeviceId, Settings.DeviceSecret, new EmailCodeRequest(email.Trim()), CancellationToken.None);
    }

    /// <summary>
    /// Confirms the code. Either secures this computer's account or signs this computer in to the account that
    /// already uses the email (bringing its balance, invite code and worker id here).
    /// </summary>
    public async Task VerifyEmailCodeAsync(string email, string code)
    {
        var summary = await CreateClient().VerifyEmailCodeAsync(DeviceId, Settings.DeviceSecret, new EmailVerifyRequest(email.Trim(), code.Trim()), CancellationToken.None);
        Apply(summary);
        SaveSettings();          // new worker id / pool address take effect on the next engine tick
        ApplyDeviceSwitches();
        AccountChanged?.Invoke();
    }

    /// <summary>A copy of everything the Otium server stores about this account, as a JSON document.</summary>
    public async Task<byte[]> ExportAccountDataAsync()
    {
        if (Settings.Account.WorkerId is null)
            await SyncAccountAsync(CancellationToken.None);
        return await CreateClient().ExportAccountDataAsync(DeviceId, Settings.DeviceSecret, CancellationToken.None);
    }

    /// <summary>Emails the code that confirms deleting the account (only for accounts with an email).</summary>
    public Task SendDeleteAccountCodeAsync() =>
        CreateClient().SendEmailCodeAsync(DeviceId, Settings.DeviceSecret,
            new EmailCodeRequest(Settings.Account.Email ?? "", EmailCodePurposes.DeleteAccount), CancellationToken.None);

    /// <summary>
    /// Deletes the account on the server, then resets this computer to a fresh install: earning and internet sharing
    /// stop, the account, invite and device secret are forgotten, and the welcome screen shows again. Machine
    /// settings (CPU use, electricity) and local earnings stats stay. Throws <see cref="AccountRequestException"/>
    /// with a user-facing reason when the server refuses.
    /// </summary>
    public async Task<DeleteAccountResult> DeleteAccountAsync(string? code, bool giveUpBalance)
    {
        var result = await CreateClient().DeleteAccountAsync(DeviceId, Settings.DeviceSecret,
            new DeleteAccountRequest(string.IsNullOrWhiteSpace(code) ? null : code.Trim(), giveUpBalance), CancellationToken.None);

        try
        {
            if (Settings.BandwidthEnabled)
                await SetBandwidthSharingAsync(false);
        }
        catch (InvalidOperationException)
        {
            // The account is already gone; finish resetting even if the partner component couldn't be told.
            Settings.BandwidthEnabled = false;
        }
        Settings.EarningEnabled = false;
        Settings.OnboardingComplete = false; // also stops the background account sync
        Settings.AcceptedTerms = false;
        Settings.BandwidthConsentAt = null;
        Settings.BandwidthPromptDismissed = false;
        Settings.Account = new AccountState();
        Settings.Referral = new ReferralState();
        Settings.DeviceSecret = "";
        Settings.DeviceSeed = "";
        EnsureDeviceSecrets();
        DeviceId = ComputeDeviceId();
        Account = null;
        LastServerError = null;
        try
        {
            AutoStart.Set(false);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
        }

        SaveSettings();
        ApplyDeviceSwitches();
        AccountChanged?.Invoke();
        return result;
    }

    private string ComputeDeviceId() =>
        Environment.GetEnvironmentVariable("OTIUM_DEVICE_SEED") is { Length: > 0 } devSeed
            ? DeviceIdentity.FromSeed(devSeed)
            : DeviceIdentity.Compute(Settings.DeviceSeed);

    private AccountClient CreateClient() =>
        new(new HttpClient
        {
            BaseAddress = new Uri(AppIdentity.ResolveServerUrl(Settings.ServerUrl).TrimEnd('/') + "/"),
            Timeout = TimeSpan.FromSeconds(20),
        });

    private void Apply(AccountSummary summary)
    {
        Account = summary;
        var referral = Settings.Referral;
        referral.MyCode = summary.Code;
        referral.ShareUrl = summary.ShareUrl;
        referral.ReferrerWallet = summary.ReferrerWallet;
        referral.ReferredAt = summary.ReferredAt;
        referral.FeeFreeUntil = summary.FeeFreeUntil;
        referral.InvitedCount = summary.InvitedCount;
        referral.VerifiedCount = summary.VerifiedCount;
        referral.LastSyncedAt = DateTimeOffset.UtcNow;
        if (summary.ReferralCodeError is not null)
        {
            referral.CodeError = summary.ReferralCodeError;
            referral.CodeUsed = null; // don't retry a rejected code forever
        }

        var account = Settings.Account;
        account.WorkerId = summary.WorkerId;
        account.Email = summary.Email;
        account.DeviceCount = summary.DeviceCount;
        account.PoolWallet = ValidWalletOrNull(summary.PoolWallet);
        account.AvailableUsd = summary.Balance.AvailableUsd;
        account.PendingUsd = summary.Balance.PendingUsd;
        account.LifetimeUsd = summary.Balance.LifetimeUsd;
        account.ReferralUsd = summary.Balance.ReferralUsd;
        account.MinPayPalUsd = summary.Balance.MinPayPalUsd;
        account.MinCryptoUsd = summary.Balance.MinCryptoUsd;
        account.FeePercent = summary.Balance.FeePercent;
        account.BandwidthAvailable = summary.Bandwidth?.Available ?? false;
        account.MysteriumSponsored = summary.MysteriumSponsored;
        account.ProxybaseDisabled = summary.ProxybaseDisabled;
        if (summary.Fees is not null && summary.Fees != account.Fees)
        {
            account.Fees = summary.Fees;
            RebuildEngineConfigs(); // the new fee or referral terms apply from the next slice
        }
        account.BandwidthUserUsdPerGb = summary.Bandwidth?.UserUsdPerGb ?? 0;
        account.BandwidthUserUsdPerSharingHour = summary.Bandwidth?.UserUsdPerSharingHour ?? 0;
        account.BandwidthServerDailyLimitBytes = summary.Bandwidth?.DailyLimitBytes ?? 0;
        account.BandwidthPartnerName = summary.Bandwidth?.PartnerName;
        account.CpuPool = summary.CpuPool;
        account.LastSyncedAt = DateTimeOffset.UtcNow;
    }

    private void EnsureDeviceSecrets()
    {
        var changed = false;
        if (string.IsNullOrEmpty(Settings.DeviceSecret))
        {
            Settings.DeviceSecret = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
            changed = true;
        }
        if (string.IsNullOrEmpty(Settings.DeviceSeed))
        {
            Settings.DeviceSeed = Guid.NewGuid().ToString("N");
            changed = true;
        }
        if (changed)
            _settingsStore.Save(Settings);
    }

    private static string? ValidWalletOrNull(string? wallet) =>
        wallet is not null && MoneroAddress.TryValidate(wallet, out _, out _) ? wallet.Trim() : null;

    public async ValueTask DisposeAsync()
    {
        foreach (var engine in Engines)
            await engine.DisposeAsync();
        await Bandwidth.DisposeAsync();
        await Provider.DisposeAsync();
        if (GpuProvider is not null)
            await GpuProvider.DisposeAsync();
        await Bandwidth.Provider.DisposeAsync();
        _sleep.Dispose();
        if (_capApplied == 1)
            GpuEfficiency.Trigger(cap: false); // full power again once Otium is closed
        _nvml?.Dispose();
        _settingsStore.Save(Settings);
        _http.Dispose();
    }
}
