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
using Otium.Providers.Aioz;
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
    private volatile BandwidthConfig _mysteriumConfig;
    private volatile BandwidthConfig _aiozConfig;
    private volatile GpuPolicy? _gpuPolicy;
    private readonly JsonFileStore<CachedGpuPolicy> _gpuPolicyStore;
    private bool _gpuPolicyFetched;

    private AppHost()
    {
        Paths = AppPaths.Default;
        Paths.EnsureCreated();
        // Wallet keys set aside by an uninstall (with earnings that may still be waiting): back before the networks start.
        try
        {
            WalletKeys.Restore(WalletKeys.KeepDir(Paths.Root), Paths.EnginesDir);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Left where they are, with their note; tried again at the next start.
        }
        // Miners an earlier Otium left running (crash, killed process) would mine on unseen; stop them first.
        MinerLifetime.StopOrphans(Paths.EnginesDir, Path.Combine(Paths.Root, "otium.lock"));
        _settingsStore = new JsonFileStore<AppSettings>(Paths.SettingsFile);
        Settings = _settingsStore.Load();
        _gpuPolicyStore = new JsonFileStore<CachedGpuPolicy>(Paths.GpuPolicyFile);
        _gpuPolicy = _gpuPolicyStore.Load().UsableAt(DateTimeOffset.UtcNow);
        UsPersonAnswer = Settings.UsPersonAnswered ? Settings.UsPerson : Settings.UsPerson ? true : null;
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
        Provider = new XmrigProvider(_http, Pool, Paths.EnginesDir, referralCode: ReferralCode, isUnitedStates: () => InUnitedStates,
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
        GpuPowerMeter = new GpuPowerMeter(_nvml, () => Settings.GpuWatts, Gpus);
        Hardware = new HardwareMonitor(_nvml);
        _gpuConfig = BuildConfig(100, "g");
        var gpuProvider = new SrbMinerGpuProvider(
            _http, Pool, Paths.EnginesDir, Paths.GpuBenchmarksFile, Gpus, GpuPowerMeter, EffectivePricePerKwh,
            policy: () => EffectiveGpuPolicy,
            freeMemoryGb: gpu => gpu.Vendor == GpuVendor.Nvidia ? _nvml?.ReadFreeMemoryGb(gpu.Index) : null,
            referralCode: ReferralCode,
            isUnitedStates: () => InUnitedStates,
            poolBlocked: PoolReachability.BlockedReasonAsync,
            optimize: () => Settings.GpuOptimize,
            powerCap: SetGpuPowerCap,
            stats: gpu => Hardware.ReadGpus([gpu]).FirstOrDefault());
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
        ApplyMiningFloors();

        // Internet sharing (opt-in). Proxybase's client earns per GB; the server routes each payout through a conversion
        // to the user's own payout address and coin;
        // builds with a Honeygain publisher key (custodial operators) report usage to the Otium server, which credits
        // the balance. Next to Proxybase, users can also share through a Mysterium node (below), which pays MYST
        // straight to their own Polygon address.
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
        _mysteriumConfig = BuildMysteriumConfig();
        _aiozConfig = BuildAiozConfig();
        if (UsesProxybase)
        {
            var mysteriumLedger = new JsonFileStore<BandwidthLedger>(Paths.MysteriumLedgerFile);
            Mysterium = new BandwidthSharer(
                new MysteriumBandwidthProvider(_http, Paths.EnginesDir, () => MysteriumBeneficiary,
                    (request, ct) => CreateClient().SponsorMysteriumAsync(DeviceId, Settings.DeviceSecret, request, ct),
                    mystUsd: () => Settings.Account.MystUsd),
                idle, () => _mysteriumConfig, null, mysteriumLedger.Load(), mysteriumLedger.Save);

            // And through an AIOZ DePIN node, which stores and delivers files for AIOZ's network and pays AIOZ tokens.
            var aiozLedger = new JsonFileStore<BandwidthLedger>(Paths.AiozLedgerFile);
            Aioz = new BandwidthSharer(
                new AiozBandwidthProvider(_http, Paths.EnginesDir, () => AiozPayoutAddress, () => Settings.AiozStorageLimitGb,
                    aiozUsd: () => Settings.Account.AiozUsd,
                    feeAddress: () => Settings.Account.SharingFeeAddress,
                    feePercent: () => Settings.Account.SharingFeeBps / 100.0),
                idle, () => _aiozConfig, null, aiozLedger.Load(), aiozLedger.Save);
        }

        // Each start caps at the usual percentage; quiet mode lowers it again only if the card runs loud.
        if (GpuEfficiencySupported)
            GpuEfficiency.WriteCap(Paths.Root, GpuEfficiency.CapPercent);
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
                devices.Add(new MeasuredDevice(DeviceKind.Gpu, GpuInfo.NamesOf(GpuProvider?.Gpus ?? []) ?? "Graphics card", GpuEngine.Snapshot));
            if (!Engine.Enabled)
                devices.RemoveAt(0);
            run.Observe(DateTimeOffset.UtcNow, devices);
            if (run.Phase == MeasurementPhase.Done)
                FinishMeasurement(run);
        }
        MeasurementChanged?.Invoke();
    }

    /// <summary>The first test switched the processor off in this session; the result card says why.</summary>
    public bool CpuLeftOffByTest { get; private set; }

    private void FinishMeasurement(PcMeasurementRun run)
    {
        foreach (var engine in Engines)
            engine.EndRunNow(); // back to earning only while the user is away
        if (run.Result() is { } result)
        {
            // After the first test only, so someone who turns the processor back on keeps it: next to a graphics card
            // that earns ten times as much, it mostly adds heat and noise.
            if (Settings.Measurement is null && Settings.CpuEnabled && result.ProcessorAddsLittle)
            {
                Settings.CpuEnabled = false;
                CpuLeftOffByTest = true;
                ApplyDeviceSwitches();
            }
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
        {
            // A computer with several cards reports "multi:N": its earnings are the whole computer's, so they stay out of
            // the website's per-card figures.
            var model = GpuInfo.ReportedModel(GpuProvider?.Gpus ?? []);
            devices.Add(new ReportedDevice("gpu", model is { } m && !m.StartsWith("multi:", StringComparison.Ordinal) ? CardIds.FromName(m) ?? m : model, GpuEngine.Totals));
        }
        // Internet sharing pays by country, so its line carries the country instead of a model: hours online and the
        // user's share earned, for the per-country figures on otium.lol.
        var sharing = Bandwidth.Snapshot;
        if (sharing.TotalSharingTime > TimeSpan.Zero && CountryCode() is { } country)
            devices.Add(new ReportedDevice("sharing", country, new EarningsTotals(sharing.TotalSharingTime, sharing.TotalUserUsd, 0)));
        // Mysterium on its own line, so the two networks' figures never mix.
        if (Mysterium?.Snapshot is { TotalSharingTime.Ticks: > 0 } myst && CountryCode() is { } mystCountry)
            devices.Add(new ReportedDevice("sharing-mysterium", mystCountry, new EarningsTotals(myst.TotalSharingTime, myst.TotalUserUsd, 0)));
        if (Aioz?.Snapshot is { TotalSharingTime.Ticks: > 0 } aioz && CountryCode() is { } aiozCountry)
            devices.Add(new ReportedDevice("sharing-aioz", aiozCountry, new EarningsTotals(aioz.TotalSharingTime, aioz.TotalUserUsd, 0)));
        return devices;
    }

    private static string? CountryCode()
    {
        try
        {
            return System.Globalization.RegionInfo.CurrentRegion.TwoLetterISORegionName is { Length: 2 } code ? code : null;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    /// <summary>Turns the opt-in on or off. Turning it on counts from now: nothing earned before is sent.</summary>
    public void SetShareEarningsStats(bool on)
    {
        var wasOn = Settings.ShareEarningsStats;
        if (on && !wasOn)
            EarningsReports.Reset(Settings.EarningsReports, ReportedDevices(), DateTimeOffset.UtcNow);
        Settings.ShareEarningsStats = on;
        Settings.ShareEarningsAnsweredAt = DateTimeOffset.UtcNow; // when the user said yes or no, for the record
        _settingsStore.Save(Settings);
        if (wasOn && !on)
            _ = DeleteSharedEarningsAsync();
    }

    /// <summary>Withdrawing consent deletes what this computer shared. Without a connection it's retried at the next sync.</summary>
    private async Task DeleteSharedEarningsAsync()
    {
        Settings.DeleteSharedEarnings = true;
        SaveSettings();
        if (Settings.Account.WorkerId is null)
            return;
        try
        {
            await CreateClient().DeleteEarningsReportsAsync(DeviceId, Settings.DeviceSecret, CancellationToken.None);
            Settings.DeleteSharedEarnings = false;
            SaveSettings();
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or AccountRequestException or InvalidOperationException)
        {
            // Retried after the next successful sync.
        }
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
            ? $"While it mines, your graphics card runs at {GpuEfficiency.CapPercent}% of its usual power limit, and back at full power the moment it stops."
            : "Your graphics card mines at its usual power limit.";
    }

    /// <summary>
    /// "Optimize my graphics card" on or off. Turning it on also sets up the lower power limit when Windows hasn't
    /// allowed it yet (one administrator prompt); declining keeps the rest of it on. Returns what to tell the user.
    /// </summary>
    public async Task<string?> SetGpuOptimizeAsync(bool on)
    {
        Settings.GpuOptimize = on;
        SaveSettings();
        UpdateGpuPowerCap();
        if (!on)
            return null;
        if (GpuEfficiencySupported && !Settings.GpuEfficiencyMode)
            return await SetGpuEfficiencyAsync(true);
        return null;
    }

    /// <summary>
    /// Quiet mode's lower power limit for coins whose miner setting doesn't change the load (Pearl): the percentage the
    /// cap applies from now on, applied at once while capped. False when the power part isn't on (not an NVIDIA card on
    /// Windows, "Optimize my graphics card" off, or Windows never allowed it): quiet mode can't ease those coins off.
    /// </summary>
    public bool SetGpuPowerCap(int percent)
    {
        if (!GpuEfficiencySupported || !Settings.GpuOptimize || !Settings.GpuEfficiencyMode || !GpuEfficiency.WriteCap(Paths.Root, percent))
            return false;
        if (Volatile.Read(ref _capApplied) == 1)
            _ = Task.Run(() => GpuEfficiency.Trigger(cap: true));
        return true;
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
        var want = Settings.GpuOptimize && Settings.GpuEfficiencyMode && mining ? 1 : 0;
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
    /// <summary>Processor and graphics card stats (load, temperature, fan…) for Details.</summary>
    public HardwareMonitor Hardware { get; }

    public string? FeeWallet => PayoutRouting.FeeTargetFor(OwnPayoutTarget, FeeWallets, InUnitedStates);

    /// <summary>Otium's unMineable referral code, added to every unMineable login.</summary>
    public string? ReferralCode { get; }

    /// <summary>The user's own payout target in canonical form ("USDT:T…", "XMR:4…"), or null while it isn't valid.</summary>
    public string? OwnPayoutTarget => PayoutTarget.ParseOrNull(Settings.PayoutAddress)?.ToString();

    /// <summary>
    /// Always null: everything Otium earns is paid to the user's one payout address, so the graphics card mines Quantus
    /// only on unMineable (paid in the user's coin), not on Kryptex, which pays only a separate Quantus wallet. A Quantus
    /// wallet saved by an earlier version is ignored. The Kryptex address-login code stays in the code base, unused.
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
    public FeePolicy Fees => AppIdentity.IsReleaseBuild
        ? FeeGrant is { } grant
            ? (FeePolicy.Default with { FeePercent = grant.FeePercent, ReferrerShareOfFee = grant.ReferrerShareOfFee }).WithinBuiltInLimits()
            : FeePolicy.Default
        : FeePolicy.FromTerms(Settings.Account.Fees);

    /// <summary>
    /// The server's signed fee grant for this computer, while it's valid. Release builds take the fee, fee-free days, the
    /// inviter's slice and balance mode only from it, so an edited settings file, another server or a computer cut off
    /// from otium.lol for over a week pays the full built-in fee. Development builds also accept an unsigned summary.
    /// </summary>
    public FeeGrant? FeeGrant =>
        FeeGrants.Verify(Settings.Referral.FeeGrant, Settings.Referral.FeeGrantSignature, AppIdentity.FeeGrantKeys, DeviceId, DateTimeOffset.UtcNow);

    /// <summary>
    /// Release builds: each engine's mining time is at least what the server has on record for this computer (the signed
    /// grant's floors), so deleting or editing ledger.json is undone at the next check-in instead of skipping Otium's fee
    /// slice. Development builds run without a server and keep their own ledgers.
    /// </summary>
    private void ApplyMiningFloors()
    {
        if (!AppIdentity.IsReleaseBuild || FeeGrant is not { } grant)
            return;
        Engine.RaiseMiningTimeTo(TimeSpan.FromSeconds(grant.CpuMiningFloor));
        GpuEngine?.RaiseMiningTimeTo(TimeSpan.FromSeconds(grant.GpuMiningFloor));
    }

    /// <summary>Fee-free time no further out than the most the built-in terms allow at once.</summary>
    private static DateTimeOffset? Capped(DateTimeOffset? feeFreeUntil) =>
        feeFreeUntil is { } until && until > DateTimeOffset.UtcNow + FeePolicy.Default.MaxFeeHoliday
            ? DateTimeOffset.UtcNow + FeePolicy.Default.MaxFeeHoliday
            : feeFreeUntil;

    /// <summary>Balance mode's pool wallet: in release builds only one the server signed for a custodial account.</summary>
    private string? BalancePoolWallet => AppIdentity.IsReleaseBuild
        ? FeeGrant is { Custodial: true } grant ? ValidWalletOrNull(grant.PoolWallet) : null
        : ValidWalletOrNull(Settings.Account.PoolWallet);

    /// <summary>
    /// unMineable's terms exclude US persons, so US users are paid in Monero by MoneroOcean instead. The user's own answer
    /// wins; until they've given one, Windows' region decides (and the question shows, ticked, so they can say otherwise).
    /// </summary>
    public static bool InUnitedStates => UsPersonAnswer ?? WindowsRegionIsUs;

    /// <summary>Windows is set to the United States region: only the default answer, since many installs elsewhere are too.</summary>
    public static bool WindowsRegionIsUs => System.Globalization.RegionInfo.CurrentRegion.TwoLetterISORegionName == "US";

    /// <summary>The user answered the US question themselves.</summary>
    public static bool UsPersonAnswered => UsPersonAnswer is not null;

    /// <summary>The user's answer to "I'm a US citizen or live in the US" (onboarding or Settings), or null before they answered.</summary>
    private static bool? UsPersonAnswer { get; set; }

    /// <summary>Records whether the user is a US citizen or resident, which decides their pool and payout coin.</summary>
    public void SetUsPerson(bool usPerson)
    {
        Settings.UsPerson = usPerson;
        Settings.UsPersonAnswered = true;
        UsPersonAnswer = usPerson;
        SaveSettings();
    }

    /// <summary>Why Otium can't run here: the Windows region, or the server's check of where the computer connects from.</summary>
    public string? UnavailableHere => Core.Availability.BlockedHere() ?? Settings.UnavailableReason;

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
            // Only to the pools' own hosts, so the server can't send the user's card to someone else's proxy.
            return PayoutRouting.PoolFor(Settings.PayoutAddress, InUnitedStates) == PayoutPool.Unmineable
                ? (_gpuPolicy ?? GpuPolicy.NotYetFetched).OnlyRoutes([GpuRoutes.Unmineable, .. quantus]).OnlyTrustedHosts()
                : _gpuPolicy?.OnlyRoutes([GpuRoutes.MoneroOcean, .. quantus]).OnlyTrustedHosts();
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

    /// <summary>Internet sharing runs Proxybase's client (own-wallet builds), not a custodial operator's partner.</summary>
    public bool UsesProxybase { get; }

    /// <summary>
    /// Sharing through Mysterium, next to Proxybase: its node pays MYST to the user's own Polygon address. Null in builds
    /// that don't share through Proxybase (simulated, custodial operators).
    /// </summary>
    public BandwidthSharer? Mysterium { get; }

    /// <summary>
    /// Sharing through AIOZ: its node stores and delivers files for AIOZ's network, and Otium withdraws the AIOZ it earns
    /// to the user's own 0x address. Null in builds that don't share through Proxybase.
    /// </summary>
    public BandwidthSharer? Aioz { get; }

    /// <summary>Every kind of sharing this build has, for totals and switches that cover every one.</summary>
    public IEnumerable<BandwidthSharer> Sharers => new[] { Bandwidth, Mysterium, Aioz }.OfType<BandwidthSharer>();

    /// <summary>This computer can run AIOZ's node and the user is paid to their own address.</summary>
    /// <summary>Otium's share of AIOZ sharing in percent, for the explanations: the server's, or the mining fee until it names one.</summary>
    public double AiozFeePercent => Settings.Account.SharingFeeBps > 0 ? Settings.Account.SharingFeeBps / 100.0 : Fees.FeePercent;

    public bool AiozOffered =>Aioz is not null && Settings.PayoutMode == PayoutMode.OwnWallet && AiozRelease.ForCurrentPlatform() is not null;

    /// <summary>
    /// The 0x address AIOZ is withdrawn to: the one the user gave for it, their payout address when it's an 0x address,
    /// or the wallet they gave Mysterium (the same key works on the AIOZ network). Null until there is one: AIOZ then
    /// waits in the node on this computer.
    /// </summary>
    public string? AiozPayoutAddress =>
        CryptoAddresses.TryValidate(AddressFormat.Evm, Settings.AiozPayoutAddress, out _)
            ? Settings.AiozPayoutAddress!.Trim().ToLowerInvariant()
            : PayoutTarget.ParseOrNull(Settings.PayoutAddress) is { Coin.Format: AddressFormat.Evm } evm ? evm.Address
            : CryptoAddresses.TryValidate(AddressFormat.Evm, Settings.MysteriumPayoutAddress, out _) ? Settings.MysteriumPayoutAddress!.Trim().ToLowerInvariant()
            : null;

    /// <summary>The server pays new nodes' registration and this computer can run Mysterium's node.</summary>
    public bool MysteriumOffered => Mysterium is not null && Settings.PayoutMode == PayoutMode.OwnWallet && Settings.Account.MysteriumSponsored
                                    && MystRelease.ForCurrentPlatform() is not null;

    /// <summary>
    /// The Polygon address Mysterium pays: the one the user gave for it, or their payout address when it's an 0x
    /// address (the same key works on every EVM network). Null until there is one.
    /// </summary>
    public string? MysteriumBeneficiary =>
        CryptoAddresses.TryValidate(AddressFormat.Evm, Settings.MysteriumPayoutAddress, out _)
            ? Settings.MysteriumPayoutAddress!.Trim().ToLowerInvariant()
            : PayoutTarget.ParseOrNull(Settings.PayoutAddress) is { Coin.Format: AddressFormat.Evm } evm ? evm.Address : null;

    /// <summary>
    /// The 0x address internet sharing pays (Proxybase: USDC on Tempo): the one the user gave for sharing, or their
    /// payout address when it's an 0x address (the same key works on every EVM network). Null until there is one.
    /// </summary>
    public string? BandwidthBeneficiary =>
        CryptoAddresses.TryValidate(AddressFormat.Evm, Settings.BandwidthPayoutAddress, out _)
            ? Settings.BandwidthPayoutAddress!.Trim().ToLowerInvariant()
            : PayoutTarget.ParseOrNull(Settings.PayoutAddress) is { Coin.Format: AddressFormat.Evm } evm ? evm.Address : null;

    public FeeSchedule FeeSchedule => _cpuConfig.FeeSchedule;

    /// <summary>
    /// The main Start/Pause switch, for the tray menu and the dashboard's button. It covers mining and sharing alike: a
    /// computer that only shares (processor and graphics card switched off) is still earning, and Pause stops it.
    /// </summary>
    public bool IsEarning => Settings.EarningEnabled;

    public UpdateService Updates { get; } = UpdateService.Create();

    /// <summary>Last error talking to the Otium server, for display; null when the last sync worked.</summary>
    public string? LastServerError { get; private set; }

    /// <summary>The most recent account summary from the server (balance, cash-outs, referrals).</summary>
    public AccountSummary? Account { get; private set; }

    public event Action? AccountChanged;

    /// <summary>Why mining can't start yet, or null when it can.</summary>
    public string? MiningBlocker => UnavailableHere ?? Settings.PayoutMode switch
    {
        PayoutMode.OwnWallet when OwnPayoutTarget is null =>
            "Add the address you want to be paid at in Advanced settings.",
        PayoutMode.OwnWallet when InUnitedStates && PayoutRouting.PoolFor(Settings.PayoutAddress, unitedStates: true) == PayoutPool.Unmineable =>
            "unMineable, which pays out in your chosen coin, doesn't serve US residents. Choose Monero (XMR) as your payout coin in Advanced settings.",
        PayoutMode.Balance when Settings.Account.WorkerId is null || BalancePoolWallet is null =>
            LastServerError is null ? "Connecting to Otium…" : "Can't reach Otium right now. Retrying automatically.",
        _ => null,
    };

    public void StartEngines()
    {
        foreach (var engine in Engines)
            engine.Start();
        foreach (var sharer in Sharers)
            sharer.Start();
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

    /// <summary>
    /// Opts in to (or out of) sharing through Mysterium, on its own consent screen. Turning it on records the consent
    /// with the node too (it agrees to Mysterium's provider terms when it starts); turning it off withdraws it.
    /// </summary>
    public async Task SetMysteriumSharingAsync(bool on)
    {
        if (Mysterium is null)
            return;
        var provider = Mysterium.Provider;
        if (on && provider.UnavailableReason is null)
            await provider.OptInAsync(CancellationToken.None);

        Settings.MysteriumEnabled = on;
        Settings.MysteriumConsentAt = on ? Settings.MysteriumConsentAt ?? DateTimeOffset.UtcNow : null;
        SaveSettings();
        ApplyDeviceSwitches();

        if (!on && provider.UnavailableReason is null)
            await provider.OptOutAsync(CancellationToken.None);
    }

    /// <summary>Opts in to (or out of) sharing through AIOZ, on its own consent screen. Turning it off stops the node.</summary>
    public async Task SetAiozSharingAsync(bool on)
    {
        if (Aioz is null)
            return;
        Settings.AiozEnabled = on;
        Settings.AiozConsentAt = on ? Settings.AiozConsentAt ?? DateTimeOffset.UtcNow : null;
        SaveSettings();
        ApplyDeviceSwitches();

        if (!on)
            await Aioz.Provider.OptOutAsync(CancellationToken.None);
    }

    /// <summary>The main switch: turns earning on for every device the user has enabled, or off for all.</summary>
    public void SetEarning(bool on)
    {
        Settings.EarningEnabled = on;
        ApplyDeviceSwitches();
        SaveSettings();
        EarningSwitched?.Invoke();
    }

    /// <summary>The main switch changed (from the overview, the dashboard or the tray), so every label of it can follow.</summary>
    public event Action? EarningSwitched;

    /// <summary>
    /// The one-click start: accepts the terms shown on the welcome screen, turns on start-at-sign-in and
    /// earning, then connects to the server (mining begins as soon as the account is ready).
    /// </summary>
    /// <param name="payoutTarget">The address chosen during onboarding ("USDT:T…"), paid by the pool directly.</param>
    /// <param name="shareEarnings">The user answered yes to "Share analytics" (see <see cref="SetShareEarningsStats"/>).</param>
    /// <param name="keepMinerReady">The user agreed to keep the processor miner paused, not stopped, when they come back.</param>
    /// <param name="shareInternet">The user agreed to internet sharing on the welcome screen (Quick start, or ticked).</param>
    /// <param name="shareInternet">Share through Proxybase, agreed on the welcome screen.</param>
    /// <param name="shareMysterium">Also share through Mysterium, agreed on the same screen.</param>
    /// <param name="mysteriumAddress">The Polygon (0x…) address Mysterium pays, when the payout address isn't an 0x address.</param>
    /// <param name="shareAioz">Also share through AIOZ, agreed on the same screen; it's withdrawn to the same 0x address.</param>
    public async Task StartFirstTimeAsync(string? inviteCode, string? payoutTarget = null, bool shareEarnings = false, bool keepMinerReady = false,
        bool shareInternet = false, bool shareMysterium = false, string? mysteriumAddress = null, bool shareAioz = false)
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
        if (shareMysterium)
        {
            if (CryptoAddresses.TryValidate(AddressFormat.Evm, mysteriumAddress, out _))
                Settings.MysteriumPayoutAddress = mysteriumAddress!.Trim().ToLowerInvariant();
            if (MysteriumOffered && Mysterium?.Provider.UnavailableReason is null)
            {
                try
                {
                    await SetMysteriumSharingAsync(true);
                }
                catch (InvalidOperationException)
                {
                    // Not fatal: the overview offers it again.
                }
            }
            else
            {
                Settings.MysteriumPromptDismissed = false; // the overview finishes it (e.g. once the server sponsors it)
                SaveSettings();
            }
        }
        if (shareAioz && AiozOffered)
        {
            if (CryptoAddresses.TryValidate(AddressFormat.Evm, mysteriumAddress, out _))
                Settings.AiozPayoutAddress = mysteriumAddress!.Trim().ToLowerInvariant();
            await SetAiozSharingAsync(true);
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
        if (on && Settings.MysteriumEnabled && Settings.MysteriumConsentAt is not null)
            Mysterium?.Enable();
        else
            Mysterium?.Disable();
        if (on && Settings.AiozEnabled && Settings.AiozConsentAt is not null)
            Aioz?.Enable();
        else
            Aioz?.Disable();

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
            snapshots.Sum(s => s.TotalUserUsd) + Sharers.Sum(s => s.Snapshot.TotalUserUsd),
            snapshots.Sum(s => s.TotalElectricityUsd),
            earning ? now - _lastHistorySample : TimeSpan.Zero);
        _lastHistorySample = now;
        _historyStore.Save(History);
        HistoryChanged?.Invoke();
    }

    public event Action? HistoryChanged;

    /// <summary>How long the user has been away from keyboard and mouse after which a ready update installs itself.</summary>
    public static readonly TimeSpan InstallUpdateAfterAway = TimeSpan.FromMinutes(10);

    /// <summary>
    /// A downloaded update installs itself when the user allows it, has been away a while, the window is closed and no
    /// test of the PC is running. Checked every minute.
    /// </summary>
    public async Task InstallUpdateIfAwayAsync()
    {
        if (_installingUpdate || !Settings.InstallUpdatesWhileAway || Updates.ReadyVersion is null || IsWindowOpen() || IsMeasuring)
            return;
        if (_idle.GetIdleTime() is not { } away || away < InstallUpdateAfterAway)
            return;
        _installingUpdate = true; // once: the restart takes a moment, and the timer keeps ticking meanwhile
        await ApplyUpdateAndRestartAsync();
    }

    private bool _installingUpdate;

    /// <summary>Whether the main window is open (shown and not minimised); the app sets it once the window exists.</summary>
    public Func<bool> IsWindowOpen { get; set; } = () => false;

    /// <summary>
    /// Stops every miner (so none is left orphaned), then lets Velopack install the update and relaunch: with the window
    /// open again if it was open, otherwise in the tray.
    /// </summary>
    public async Task ApplyUpdateAndRestartAsync()
    {
        var showWindow = IsWindowOpen();
        foreach (var engine in Engines)
            await engine.DisposeAsync();
        await DisposeSharersAsync();
        await Provider.DisposeAsync();
        if (GpuProvider is not null)
            await GpuProvider.DisposeAsync();
        _settingsStore.Save(Settings);
        Updates.ApplyAndRestart(showWindow);
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
        foreach (var sharer in Sharers.Where(s => s.Enabled))
            sharer.Enable();
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
        _mysteriumConfig = BuildMysteriumConfig();
        _aiozConfig = BuildAiozConfig();
        foreach (var engine in Engines)
            engine.Nudge();
        foreach (var sharer in Sharers)
            sharer.Nudge();
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
            var poolWallet = BalancePoolWallet ?? "";
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
                cpu && Settings.KeepMinerReadyWhenBack,
                RunsWhileBrowsing: !cpu && Settings.EarnWhileBrowsing,
                FullSpeedWhenAway: cpu);
        }

        // Fee and referral slices mine on the user's own pool, so a referrer paid on the other pool gets no slice.
        var referral = Settings.Referral;
        var user = OwnPayoutTarget ?? Settings.PayoutAddress;
        // Release builds: who invited the user and their fee-free days come only from the signed grant.
        var grant = FeeGrant;
        var (referrerWallet, referredAt, feeFreeUntil) = AppIdentity.IsReleaseBuild
            ? (grant?.ReferrerWallet, grant?.ReferredAt, grant?.FeeFreeUntil)
            : (referral.ReferrerWallet, referral.ReferredAt, referral.FeeFreeUntil);
        var referrer = PayoutRouting.SamePool(user, referrerWallet, InUnitedStates) ? PayoutTarget.Normalize(referrerWallet!) : null;
        var standing = new ReferralStanding(referrer, referredAt, Capped(feeFreeUntil));
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
            PauseWhenBack: cpu && Settings.KeepMinerReadyWhenBack,
            // The graphics card may mine while the user browses: then it stays at the lowest priority.
            RunsWhileBrowsing: !cpu && Settings.EarnWhileBrowsing,
            // The processor uses every thread while the user is away; their setting applies while they're at the computer.
            FullSpeedWhenAway: cpu);
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

    /// <summary>Mysterium's node: the user's own switch, idle choice and daily limit; Mysterium pays it, not the server.</summary>
    private BandwidthConfig BuildMysteriumConfig() => new(
        Settings.MysteriumEnabled && Settings.MysteriumConsentAt is not null && Settings.PayoutMode == PayoutMode.OwnWallet,
        Settings.MysteriumOnlyWhenIdle,
        TimeSpan.FromMinutes(Math.Max(1, Settings.IdleMinutes)),
        (long)(Math.Clamp(Settings.MysteriumDailyLimitGb, 0.1, 100) * 1024 * 1024 * 1024),
        0,
        0,
        Settings.Account.WorkerId ?? "");

    /// <summary>
    /// AIOZ's node: the user's own switch and idle choice; AIOZ pays it, not the server. Its limit is on storage (the
    /// provider enforces it), and the node doesn't report traffic, so there's no daily data limit.
    /// </summary>
    private BandwidthConfig BuildAiozConfig() => new(
        Settings.AiozEnabled && Settings.AiozConsentAt is not null && Settings.PayoutMode == PayoutMode.OwnWallet,
        Settings.AiozOnlyWhenIdle,
        TimeSpan.FromMinutes(Math.Max(1, Settings.IdleMinutes)),
        long.MaxValue,
        0,
        0,
        Settings.Account.WorkerId ?? "");

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
                ? await client.RegisterAsync(new RegisterDeviceRequest(DeviceId, Settings.DeviceSecret, ownWallet, referral.CodeUsed, HardwareId()), ct)
                : await client.GetSummaryAsync(DeviceId, Settings.DeviceSecret, ct,
                    new MiningTimeReport(Engine.Totals.MiningTime, GpuEngine?.Totals.MiningTime ?? TimeSpan.Zero,
                        EngineStateTags.For(Engine.Snapshot, Settings.CpuEnabled, Settings.EarningEnabled, MiningBlocker is not null),
                        EngineStateTags.For(GpuEngine?.Snapshot, Settings.GpuEnabled, Settings.EarningEnabled, MiningBlocker is not null)));

            Apply(summary);
            ApplyMiningFloors();
            referral.RegisteredPayoutAddress = ownWallet;
            LastServerError = null;
            Settings.UnavailableReason = null;
            if (Settings.DeleteSharedEarnings && !Settings.ShareEarningsStats)
                await DeleteSharedEarningsAsync();
            await RefreshGpuPolicyAsync(client, ct);
            await ReportReferralShareAsync(client, ct);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or UriFormatException or InvalidOperationException or AccountRequestException)
        {
            LastServerError = e is AccountRequestException rejected ? rejected.Message : "Can't reach Otium right now.";
            if (e is AccountRequestException { StatusCode: 451 } unavailable)
                Settings.UnavailableReason = unavailable.Message; // stops mining and sharing until a check from elsewhere passes
            if (e is AccountRequestException { StatusCode: 401 } && account.WorkerId is null && !Settings.InstallDeviceId)
            {
                // The server knows this computer from an earlier install, whose secret went with it. Nobody can prove
                // they own that account now, so this install registers as a new computer instead (signing in by email
                // brings the old account's invites back).
                Settings.InstallDeviceId = true;
                DeviceId = ComputeDeviceId();
                SaveSettings();
                await SyncAccountAsync(ct);
                return;
            }
        }

        SaveSettings();
        ApplyDeviceSwitches();
        AccountChanged?.Invoke();
        // The pool pays own-address users directly, so its balance doesn't depend on reaching the Otium server.
        await RefreshPoolBalanceAsync(force: false, ct);
    }

    /// <summary>
    /// The code of a friend's invite link (otium.lol/r/CODE) opened on this internet connection in the last week, so a
    /// friend who downloaded Otium from it doesn't have to type the code. Asked once: after the server answers, never
    /// again, so a code the user cleared stays cleared. Null when there's none or the server can't be reached. Only the
    /// welcome screen asks, and shows the code for the user to keep or clear: a link's code is never used unseen (anyone
    /// on a shared connection, such as a university's, could have opened it).
    /// </summary>
    public async Task<string?> CheckInviteLinkAsync(CancellationToken ct)
    {
        if (Settings.Referral.InviteLinkChecked)
            return null;
        try
        {
            var code = await CreateClient().GetInviteLinkCodeAsync(ct);
            Settings.Referral.InviteLinkChecked = true;
            _settingsStore.Save(Settings);
            return code is not null && ReferralCodes.IsWellFormed(code) && code != Settings.Referral.MyCode ? code : null;
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or UriFormatException or AccountRequestException
                                       or System.Text.Json.JsonException)
        {
            return null; // not marked as asked: the welcome screen asks again next time it opens
        }
    }

    private double _referralShareSent = -1;
    private DateTimeOffset _referralShareSentAt = DateTimeOffset.MinValue;

    /// <summary>
    /// For a user who joined through a friend's invite: tells the server how much of Otium's fee this computer has mined
    /// for that friend (an estimate from its own records), so the friend can see what inviting earns. At most hourly,
    /// and only when it has grown.
    /// </summary>
    private async Task ReportReferralShareAsync(AccountClient client, CancellationToken ct)
    {
        if (Settings.Referral.ReferredAt is null)
            return;
        var total = Engines.Sum(e => e.Totals.ReferrerUsd);
        var now = DateTimeOffset.UtcNow;
        if (total <= _referralShareSent + 0.0001 || now - _referralShareSentAt < TimeSpan.FromHours(1))
            return;
        try
        {
            await client.ReportReferralShareAsync(DeviceId, Settings.DeviceSecret, new ReferralShareReport(total), ct);
            _referralShareSent = total;
            _referralShareSentAt = now;
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or AccountRequestException)
        {
            // Sent again at the next check-in.
        }
    }

    /// <summary>Fetches the GPU policy (it needs no account, so the welcome screen's estimate can use it).</summary>
    public Task RefreshGpuPolicyAsync(CancellationToken ct) => RefreshGpuPolicyAsync(CreateClient(), ct);

    /// <summary>Fetches the GPU policy and keeps it on disk; on failure the last one stays in effect.</summary>
    private async Task RefreshGpuPolicyAsync(AccountClient client, CancellationToken ct)
    {
        try
        {
            _gpuPolicy = await client.GetGpuPolicyAsync(ct);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or AccountRequestException or System.Text.Json.JsonException)
        {
            return;
        }
        try
        {
            _gpuPolicyStore.Save(new CachedGpuPolicy { Policy = _gpuPolicy, FetchedAt = DateTimeOffset.UtcNow });
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Not kept: the next start waits for the server, as before.
        }
        // The first policy of this run: a graphics card that found nothing to mine without it starts now instead of
        // after its one-minute retry.
        if (!_gpuPolicyFetched)
        {
            _gpuPolicyFetched = true;
            if (GpuEngine is { Enabled: true } gpu)
                gpu.Enable();
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

    /// <summary>
    /// The hardware id the server remembers invites by (see <see cref="DeviceIdentity.HardwareId"/>); in dev mode, one per
    /// simulated computer.
    /// </summary>
    private static string? HardwareId() =>
        AppIdentity.DevelopmentOverride("OTIUM_DEVICE_SEED") is { Length: > 0 } devSeed ? DeviceIdentity.HardwareIdFromSeed(devSeed)
        : DeviceIdentity.HardwareId();

    private string ComputeDeviceId() =>
        AppIdentity.DevelopmentOverride("OTIUM_DEVICE_SEED") is { Length: > 0 } devSeed ? DeviceIdentity.FromSeed(devSeed)
        : Settings.InstallDeviceId ? DeviceIdentity.FromSeed("install:" + Settings.DeviceSeed)
        : DeviceIdentity.Compute(Settings.DeviceSeed);

    /// <summary>
    /// Sends a problem report (see <see cref="ProblemReportBuilder"/>). Returns the reference to quote; throws
    /// <see cref="AccountRequestException"/> or a network error with the reason when it can't be sent.
    /// </summary>
    public async Task<string> SendProblemReportAsync(string? note)
    {
        if (Settings.Account.WorkerId is null)
            await SyncAccountAsync(CancellationToken.None); // the server only takes reports from computers it knows
        var report = ProblemReportBuilder.Build(this, note);
        var result = await CreateClient().SendProblemReportAsync(DeviceId, Settings.DeviceSecret, report, CancellationToken.None);
        return result.Reference;
    }

    private AccountClient CreateClient()
    {
        var http = new HttpClient
        {
            BaseAddress = new Uri(AppIdentity.ResolveServerUrl().TrimEnd('/') + "/"),
            Timeout = TimeSpan.FromSeconds(20),
        };
        // The server can require a minimum version (older ones trusted their settings file for the fee).
        if (AppVersions.Of(typeof(AppHost).Assembly) is { } version)
            http.DefaultRequestHeaders.TryAddWithoutValidation(AppVersions.Header, version);
        return new(http);
    }

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
        referral.EarnedFromFriendsUsd = summary.ReferralEarnedUsd;
        referral.LastSyncedAt = DateTimeOffset.UtcNow;
        referral.FeeGrant = summary.FeeGrant;
        referral.FeeGrantSignature = summary.FeeGrantSignature;
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
        account.MystUsd = summary.MystUsd ?? account.MystUsd;
        account.AiozUsd = summary.AiozUsd ?? account.AiozUsd;
        account.SharingFeeAddress = summary.SharingFeeAddress;
        account.SharingFeeBps = summary.SharingFeeBps ?? 0;
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

    /// <summary>Stops every sharer and its partner component (each node or client process ends with it).</summary>
    private async Task DisposeSharersAsync()
    {
        foreach (var sharer in Sharers)
        {
            await sharer.DisposeAsync();
            await sharer.Provider.DisposeAsync();
        }
    }

    private static string? ValidWalletOrNull(string? wallet) =>
        wallet is not null && MoneroAddress.TryValidate(wallet, out _, out _) ? wallet.Trim() : null;

    public async ValueTask DisposeAsync()
    {
        foreach (var engine in Engines)
            await engine.DisposeAsync();
        await DisposeSharersAsync();
        await Provider.DisposeAsync();
        if (GpuProvider is not null)
            await GpuProvider.DisposeAsync();
        _sleep.Dispose();
        if (_capApplied == 1)
            GpuEfficiency.Trigger(cap: false); // full power again once Otium is closed
        _nvml?.Dispose();
        _settingsStore.Save(Settings);
        _http.Dispose();
    }
}
