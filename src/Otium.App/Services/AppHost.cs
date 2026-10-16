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
        _settingsStore = new JsonFileStore<AppSettings>(Paths.SettingsFile);
        Settings = _settingsStore.Load();
        EnsureDeviceSecrets();

        DeviceId = Environment.GetEnvironmentVariable("OTIUM_DEVICE_SEED") is { Length: > 0 } devSeed
            ? DeviceIdentity.FromSeed(devSeed)
            : DeviceIdentity.Compute(Settings.DeviceSeed);
        IsSimulated = SimulatedProvider.IsEnabled;
        FeeWallet = ValidWalletOrNull(AppIdentity.ResolveFeeWallet());

        _http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd($"{AppIdentity.Name}/0.1");
        Pool = new MoneroOceanClient(_http);
        var idle = IdleDetector.ForThisPlatform();
        _historyStore = new JsonFileStore<EarningsHistory>(Paths.HistoryFile);
        History = _historyStore.Load();
        Estimator = new EarningsEstimator(Pool);

        // CPU
        Provider = new XmrigProvider(_http, Pool, Paths.EnginesDir);
        PowerModel = CpuPowerModel.ForThisMachine(Settings.CpuFullLoadWatts);
        PowerMeter = RaplPowerMeter.IsAvailable() ? new RaplPowerMeter(() => PowerModel) : new EstimatedCpuPowerMeter(() => PowerModel);
        var cpuLedger = new JsonFileStore<EarningsLedger>(Paths.LedgerFile);
        _cpuConfig = BuildConfig(Settings.CpuUsagePercent, "");
        // Dev mode swaps the real miners for simulated ones that report to the local simulated pool.
        IComputeProvider cpuProvider = IsSimulated ? new SimulatedProvider(_http, Pool, DeviceKind.Cpu, "rx/0", 9_000, 1.0) : Provider;
        Engine = new EarningsEngine(cpuProvider, PowerMeter, idle, () => _cpuConfig, cpuLedger.Load(), cpuLedger.Save);

        // GPU (only when a supported card and miner build exist)
        _nvml = Nvml.TryOpen();
        Gpus = new GpuDetector(_nvml).Detect();
        GpuPowerMeter = new GpuPowerMeter(_nvml, () => Settings.GpuWatts, Gpus.Count);
        _gpuConfig = BuildConfig(100, "g");
        var gpuProvider = new SrbMinerGpuProvider(
            _http, Pool, Paths.EnginesDir, Paths.GpuBenchmarksFile, Gpus, GpuPowerMeter, () => Settings.ElectricityPricePerKwh,
            policy: () => EffectiveGpuPolicy);
        if (IsSimulated)
        {
            var gpuLedger = new JsonFileStore<EarningsLedger>(Paths.GpuLedgerFile);
            // kawpow at RTX 3080-like speed; the factor converts to the XMR-equivalent rate the simulated pool pays for.
            var simulatedGpu = new SimulatedProvider(_http, Pool, DeviceKind.Gpu, "kawpow", 45e6, 8.7e-5, policy: () => EffectiveGpuPolicy);
            GpuEngine = new EarningsEngine(simulatedGpu, new EstimatedCpuPowerMeter(new CpuPowerModel(200, 30)), idle, () => _gpuConfig, gpuLedger.Load(), gpuLedger.Save);
        }
        else if (gpuProvider.IsSupportedOnThisPlatform)
        {
            GpuProvider = gpuProvider;
            var gpuLedger = new JsonFileStore<EarningsLedger>(Paths.GpuLedgerFile);
            GpuEngine = new EarningsEngine(gpuProvider, GpuPowerMeter, idle, () => _gpuConfig, gpuLedger.Load(), gpuLedger.Save);
        }

        // Internet sharing (opt-in): usage is reported to the Otium server, which credits the balance.
        var bandwidthLedger = new JsonFileStore<BandwidthLedger>(Paths.BandwidthLedgerFile);
        _bandwidthConfig = BuildBandwidthConfig();
        IBandwidthProvider bandwidthProvider = IsSimulated
            ? new SimulatedBandwidthProvider()
            : new HoneygainBandwidthProvider(AppContext.BaseDirectory, AppIdentity.ResolveHoneygainApiKey());
        Bandwidth = new BandwidthSharer(bandwidthProvider, idle, () => _bandwidthConfig,
            (report, ct) => CreateClient().ReportBandwidthAsync(DeviceId, Settings.DeviceSecret, report, ct),
            bandwidthLedger.Load(), bandwidthLedger.Save);
    }

    public static AppHost Create() => new();

    public EarningsHistory History { get; }

    public EarningsEstimator Estimator { get; }

    /// <summary>Dev mode (OTIUM_SIMULATE=1): simulated miners instead of XMRig/SRBMiner.</summary>
    public bool IsSimulated { get; }

    public AppPaths Paths { get; }

    public AppSettings Settings { get; }

    public string DeviceId { get; }

    public string? FeeWallet { get; }

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
    /// The server's GPU policy as it applies to this user: Kryptex coins are mined to Otium's account, so own-wallet
    /// users only get the MoneroOcean algorithms. Null until fetched (the GPU miner then falls back to MoneroOcean).
    /// </summary>
    public GpuPolicy? EffectiveGpuPolicy => _gpuPolicy is { } policy
        ? Settings.PayoutMode == PayoutMode.Balance ? policy : policy.WithoutOperatorPools()
        : null;

    /// <summary>Shares unused internet bandwidth when the user has opted in.</summary>
    public BandwidthSharer Bandwidth { get; }

    /// <summary>The server pays for sharing and this computer can run the partner's component.</summary>
    public bool BandwidthOffered => Settings.PayoutMode == PayoutMode.Balance
                                    && Settings.Account.BandwidthAvailable
                                    && Bandwidth.Provider.UnavailableReason is null;

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
        PayoutMode.OwnWallet when ValidWalletOrNull(Settings.PayoutAddress) is null =>
            "Add your Monero wallet address in Advanced settings to earn with your own wallet.",
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
    public async Task StartFirstTimeAsync(string? inviteCode)
    {
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
        : FeePolicy.Default.FeePercent;

    /// <summary>What this computer could earn per 24 h of mining, from its hardware and today's payouts.</summary>
    public Task<EarningsEstimate> EstimateAsync(CancellationToken ct) =>
        Estimator.EstimateAsync(CpuInfo.Name, Environment.ProcessorCount, Settings.CpuUsagePercent, PowerModel,
            Settings.GpuEnabled ? Gpus : [], Settings.ElectricityPricePerKwh, EffectiveFeePercent, ct, EffectiveGpuPolicy);

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

    /// <summary>"Try again" after an antivirus block: re-enabling clears the engine's blocked/error state.</summary>
    public void ApplyDeviceSwitchesAfterBlock()
    {
        foreach (var engine in Engines.Where(e => e.Enabled))
            engine.Enable();
    }

    public void SaveSettings()
    {
        _settingsStore.Save(Settings);
        _cpuConfig = BuildConfig(Settings.CpuUsagePercent, "");
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
    private EngineConfig BuildConfig(int usagePercent, string workerSuffix)
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
                Settings.ElectricityPricePerKwh,
                new FeeSchedule(FeePolicy.Default with { FeePercent = 0 }, poolWallet, null, new ReferralStanding(null, null, null)),
                // CPU and GPU share one worker id so the pool reports their combined work for this account.
                Settings.Account.WorkerId ?? "");
        }

        var referral = Settings.Referral;
        var standing = new ReferralStanding(ValidWalletOrNull(referral.ReferrerWallet), referral.ReferredAt, referral.FeeFreeUntil);
        return new EngineConfig(
            Settings.PayoutAddress,
            usagePercent,
            Settings.OnlyWhenIdle,
            TimeSpan.FromMinutes(Math.Max(1, Settings.IdleMinutes)),
            Settings.NeverRunAtLoss,
            Settings.ElectricityPricePerKwh,
            new FeeSchedule(FeePolicy.Default, Settings.PayoutAddress, FeeWallet, standing),
            $"ot{DeviceId[..8]}{workerSuffix}");
    }

    private BandwidthConfig BuildBandwidthConfig()
    {
        var account = Settings.Account;
        var userLimit = (long)(Math.Clamp(Settings.BandwidthDailyLimitGb, 0.1, 100) * 1024 * 1024 * 1024);
        var serverLimit = account.BandwidthServerDailyLimitBytes > 0 ? account.BandwidthServerDailyLimitBytes : long.MaxValue;
        return new BandwidthConfig(
            Settings.BandwidthEnabled && Settings.BandwidthConsentAt is not null && BandwidthOfferedBySettings() && account.WorkerId is not null,
            Settings.BandwidthOnlyWhenIdle,
            TimeSpan.FromMinutes(Math.Max(1, Settings.IdleMinutes)),
            Math.Min(userLimit, serverLimit),
            account.BandwidthUserUsdPerGb,
            account.BandwidthUserUsdPerSharingHour,
            account.WorkerId ?? "");

        // Bandwidth isn't constructed yet on the first call, so check the provider separately in BandwidthOffered.
        bool BandwidthOfferedBySettings() => Settings.PayoutMode == PayoutMode.Balance && account.BandwidthAvailable;
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
            var ownWallet = Settings.PayoutMode == PayoutMode.OwnWallet ? ValidWalletOrNull(Settings.PayoutAddress) : null;
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
            Settings.Account.LastPayPalEmail = destination.Trim();
        else
            Settings.Account.LastCryptoAddress = destination.Trim();
        await SyncAccountAsync(CancellationToken.None);
        return cashout;
    }

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
        account.BandwidthUserUsdPerGb = summary.Bandwidth?.UserUsdPerGb ?? 0;
        account.BandwidthUserUsdPerSharingHour = summary.Bandwidth?.UserUsdPerSharingHour ?? 0;
        account.BandwidthServerDailyLimitBytes = summary.Bandwidth?.DailyLimitBytes ?? 0;
        account.BandwidthPartnerName = summary.Bandwidth?.PartnerName;
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
        _nvml?.Dispose();
        _settingsStore.Save(Settings);
        _http.Dispose();
    }
}
