using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Otium.App.Services;
using Otium.Core.Accounts;
using Otium.Core.Fees;
using Otium.Core.Gpu;
using Otium.Core.Platform;
using Otium.Core.Power;
using Otium.Core.Settings;
using Otium.Providers.SrbMiner;
using Otium.Providers.XMRig;

namespace Otium.App.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly AppHost _host;
    private readonly Action _accountDeleted;
    private readonly DispatcherTimer _saveDebounce;
    private bool _loading = true;

    public SettingsViewModel(AppHost host, Action? accountDeleted = null)
    {
        _host = host;
        _accountDeleted = accountDeleted ?? (() => { });
        // Saving restarts the engine when CPU usage changes, so wait for the user to stop dragging.
        _saveDebounce = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _saveDebounce.Tick += (_, _) =>
        {
            _saveDebounce.Stop();
            Apply();
        };

        var s = host.Settings;
        CpuUsagePercent = s.CpuUsagePercent;
        OnlyWhenIdle = s.OnlyWhenIdle;
        KeepAwakeWhileEarning = s.KeepAwakeWhileEarning;
        ShareEarningsStats = s.ShareEarningsStats;
        GpuEfficiencyMode = s.GpuEfficiencyMode;
        EarnWhileBrowsing = s.EarnWhileBrowsing;
        NotificationsEnabled = s.NotificationsEnabled;
        KeepMinerReadyWhenBack = s.KeepMinerReadyWhenBack;
        InstallUpdatesWhileAway = s.InstallUpdatesWhileAway;
        IdleMinutes = s.IdleMinutes;
        ElectricityPrice = (decimal)s.ElectricityPricePerKwh;
        CpuFullLoadWatts = s.CpuFullLoadWatts is { } w ? (decimal)w : null;
        NeverRunAtLoss = s.NeverRunAtLoss;
        PayForElectricity = s.PayForElectricity;
        var country = SpotPriceZone.Find(s.ElectricityZone)?.Country ?? RegionCountry();
        Zones = SpotPriceZone.ForCountry(country);
        SelectedZone = SpotPriceZone.Find(s.ElectricityZone);
        Payout = new PayoutTargetEditor(s.PayoutAddress, s.UsPerson, host.SetUsPerson);
        Payout.Changed += () => SavePayoutCommand.NotifyCanExecuteChanged();
        ServerUrl = Core.AppIdentity.ResolveServerUrl(s.ServerUrl);
        StartWithSystem = AutoStart.IsEnabled();
        UseBalance = s.PayoutMode == PayoutMode.Balance;
        UseCpu = s.CpuEnabled;
        UseGpu = s.GpuEnabled;
        GpuWatts = s.GpuWatts is { } gw ? (decimal)gw : null;
        _loading = false;
        _ = RefreshBenchmarksAsync();
        _ = RefreshLivePriceAsync();
        UpdateStatus = host.Updates.Status;
        host.Updates.Changed += () => Dispatcher.UIThread.Post(() => UpdateStatus = _host.Updates.Status);
        host.AccountChanged += () => Dispatcher.UIThread.Post(() =>
        {
            OnPropertyChanged(nameof(OffersBalance));
            OnPropertyChanged(nameof(CostText));
            OnPropertyChanged(nameof(FeeInfo));
        });
    }

    public InternetSharingViewModel InternetSharing => _internetSharing ??= new InternetSharingViewModel(_host);

    private InternetSharingViewModel? _internetSharing;

    public EmailSignInViewModel SignIn => _signIn ??= new EmailSignInViewModel(_host, () => OnPropertyChanged(nameof(AccountText)));

    private EmailSignInViewModel? _signIn;

    public YourDataViewModel YourData => _yourData ??= new YourDataViewModel(_host, _accountDeleted);

    private YourDataViewModel? _yourData;

    public string AccountText => _host.Settings.Account.Email is { } email
        ? $"Signed in as {email}" + (_host.Settings.Account.DeviceCount > 1
            ? $" · {_host.Settings.Account.DeviceCount} computers share this {(_host.ServerIsCustodial ? "balance" : "account")}"
            : "")
        : _host.ServerIsCustodial
            ? "Not signed in. Add your email so your balance can't get lost, or sign in to an account you use on another computer."
            : "Not signed in. Add your email to keep your invite code and invited friends when you reinstall or use another computer.";

    public string VersionText => $"Version {_host.Updates.CurrentVersion}";

    public bool UpdatesEnabled => _host.Updates.IsEnabled;

    [ObservableProperty] public partial string UpdateStatus { get; set; } = "";

    [RelayCommand]
    private Task CheckForUpdates() => _host.Updates.CheckAsync();

    public bool HasGpu => _host.GpuProvider is not null;

    public string GpuSummary =>
        _host.GpuProvider is { } gpu
            ? string.Join(", ", gpu.Gpus.Select(g => g.Describe()))
            : _host.Gpus.Count > 0
                ? $"Found {string.Join(", ", _host.Gpus.Select(g => g.Describe()))}, but GPU mining isn't available on this system yet (it needs Windows or Linux on x64)."
                : "No supported NVIDIA or AMD graphics card found.";

    public bool GpuPowerMeasured => _host.GpuPowerMeter.CanMeasure;

    public string GpuPowerNote => GpuPowerMeasured
        ? "Your graphics card reports its own power draw, so electricity is measured."
        : $"Your graphics card can't report its power draw. Enter roughly how many extra watts it uses while mining (default {GpuPowerMeter.DefaultEstimatedWatts:0} W).";

    public ObservableCollection<BenchmarkRow> Benchmarks { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UseOwnWallet), nameof(OffersBalance))]
    public partial bool UseBalance { get; set; }

    public bool UseOwnWallet
    {
        get => !UseBalance;
        set => UseBalance = !value;
    }

    /// <summary>Price zones with live hourly prices in the user's country (empty where none are available yet).</summary>
    public IReadOnlyList<SpotPriceZone> Zones { get; }

    public bool HasZones => Zones.Count > 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LivePriceNote))]
    public partial bool PayForElectricity { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LivePriceNote))]
    public partial SpotPriceZone? SelectedZone { get; set; }

    public string LivePriceNote
    {
        get
        {
            if (!PayForElectricity)
                return "Electricity isn't counted, so Otium earns whenever your computer is free.";
            if (!HasZones)
                return "Live hourly prices aren't available in your country yet, so Otium uses your price all day.";
            var prices = _host.ElectricityPrices;
            return prices.HasLivePrices && prices.Describe() is { } now
                ? $"Following today's hourly prices in {prices.Zone!.Code}: {now}. Otium pauses in expensive hours and earns in cheap ones."
                : "Live hourly prices will load shortly.";
        }
    }

    partial void OnPayForElectricityChanged(bool value)
    {
        if (_loading)
            return;
        _host.Settings.PayForElectricity = value;
        _host.SaveSettings();
    }

    partial void OnSelectedZoneChanged(SpotPriceZone? value)
    {
        if (_loading || value is null)
            return;
        _host.Settings.ElectricityZone = value.Code;
        _host.SaveSettings();
        _ = RefreshLivePriceAsync();
    }

    private async Task RefreshLivePriceAsync()
    {
        await _host.RefreshElectricityAsync(CancellationToken.None);
        OnPropertyChanged(nameof(LivePriceNote));
    }

    private static string RegionCountry()
    {
        try
        {
            return System.Globalization.RegionInfo.CurrentRegion.TwoLetterISORegionName;
        }
        catch (ArgumentException)
        {
            return "";
        }
    }

    public string ElectricityNote => _host.Settings.ElectricityPriceIsEstimate
        ? "This is an estimate for your country. Enter the price from your bill for more accurate numbers."
        : "Your own price is used for every profit calculation.";

    [ObservableProperty] public partial bool UseCpu { get; set; }
    [ObservableProperty] public partial bool UseGpu { get; set; }
    [ObservableProperty] public partial decimal? GpuWatts { get; set; }
    [ObservableProperty] public partial string BenchmarkNote { get; set; } = "";
    [ObservableProperty] public partial bool HasBenchmarks { get; set; }

    partial void OnUseBalanceChanged(bool value)
    {
        if (_loading)
            return;
        _host.Settings.PayoutMode = value ? PayoutMode.Balance : PayoutMode.OwnWallet;
        _host.SaveSettings();
        _host.ApplyDeviceSwitches();
        _ = _host.SyncAccountAsync(CancellationToken.None);
    }

    partial void OnUseCpuChanged(bool value) => ApplyDevices();
    partial void OnUseGpuChanged(bool value)
    {
        OnPropertyChanged(nameof(CostText));
        ApplyDevices();
    }
    partial void OnGpuWattsChanged(decimal? value) => QueueSave();

    private void ApplyDevices()
    {
        if (_loading)
            return;
        _host.Settings.CpuEnabled = UseCpu;
        _host.Settings.GpuEnabled = UseGpu;
        _host.ApplyDeviceSwitches();
        _host.SaveSettings();
    }

    [RelayCommand]
    private async Task RefreshBenchmarksAsync()
    {
        if (_host.GpuProvider is not { } gpu)
            return;

        Benchmarks.Clear();
        HasBenchmarks = false;
        var results = gpu.Benchmarks;
        if (results.Count == 0)
        {
            BenchmarkNote = "Not tested yet. When the graphics card starts earning, Otium spends about 2½ minutes on each algorithm (still earning for you) to find the best one.";
            return;
        }

        try
        {
            var scored = await gpu.ScoreAsync(CancellationToken.None);
            var best = scored.FirstOrDefault()?.Algorithm;
            foreach (var option in scored)
            {
                Benchmarks.Add(new BenchmarkRow(
                    option.Algorithm,
                    Format.Hashrate(option.HashrateHps),
                    option.ExtraWatts is { } w ? Format.Watts(w) : "est.",
                    Format.Usd(option.GrossUsdPerDay),
                    Format.Usd(option.NetUsdPerDay),
                    gpu.SetAsideReason(option.Algorithm) is { } why ? $"paused for now: {Shorten(why)}"
                    : option.Algorithm == best ? "best" : option.Algorithm == gpu.CurrentAlgorithm ? "mining" : ""));
            }
            foreach (var failed in results.Where(r => !r.Succeeded))
                Benchmarks.Add(new BenchmarkRow(failed.Algorithm, "—", "—", "—", "—", $"not working on this card: {Shorten(failed.Failure)}. Tried again later."));

            HasBenchmarks = Benchmarks.Count > 0;
            BenchmarkNote = $"Using today's payouts at your pool and your electricity price. Re-checked every {SrbMinerGpuProvider.ReevaluateEvery.TotalMinutes:0} minutes while mining.";
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
        {
            BenchmarkNote = "Couldn't fetch today's payouts. Otium tries again in a few minutes.";
        }
    }

    [RelayCommand]
    private void RetestGpu()
    {
        _host.RetestGpu();
        Benchmarks.Clear();
        HasBenchmarks = false;
        BenchmarkNote = "Benchmarks cleared. Testing starts the next time the graphics card is earning.";
    }

    private static string Shorten(string? text) =>
        string.IsNullOrEmpty(text) ? "unknown error" : text.Length > 80 ? text[..80] + "…" : text;

    public string DataFolder => _host.Paths.Root;

    public bool LargePagesSupported => LargePages.IsSupported;

    /// <summary>Offered until the right is in effect, and not again while it waits for the next sign-in.</summary>
    public bool CanSpeedUpProcessor => LargePages.IsSupported && !LargePages.InEffect() && LargePagesMessage is null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LargePagesText), nameof(CanSpeedUpProcessor))]
    public partial string? LargePagesMessage { get; set; }

    public string LargePagesText => LargePagesMessage ?? (LargePages.InEffect()
        ? "Processor mining uses large memory pages, its fastest setting."
        : "Processor mining can run noticeably faster with large memory pages (XMRig, the miner, reports up to 50% depending on the processor). Windows needs your permission once.");

    [RelayCommand]
    private async Task SpeedUpProcessor()
    {
        LargePagesMessage = await _host.GrantLargePagesAsync();
    }

    public string EngineInfo =>
        $"CPU: XMRig {XmrigRelease.Version} → unMineable ({PoolEndpoint.Unmineable}, TLS) for your chosen coin, or MoneroOcean " +
        $"({PoolEndpoint.MoneroOcean}) for Monero. GPU: SRBMiner-Multi {SrbMinerRelease.Version} → the pool Otium's GPU policy names " +
        $"for each coin. unMineable logins carry Otium's referral code ({_host.ReferralCode ?? "none"}), which lowers its fee to 0.75%.";

    public string FeeInfo =>
        $"If someone invited you, {_host.Fees.ReferrerShareOfFee * 100:0}% of Otium's fee goes to them for " +
        $"{_host.Fees.ReferrerRewardPeriod.TotalDays / 30:0} months. " +
        $"Fee address: {(_host.FeeWallet is null ? "none for your pool in this build, so the fee slice is mined for you" : _host.FeeWallet)}.";

    /// <summary>Everything that comes off your mining, for the payout address you have now.</summary>
    public string CostText
    {
        get
        {
            var pool = Core.Pools.PayoutRouting.PoolFor(_host.OwnPayoutTarget, AppHost.InUnitedStates) ?? Core.Pools.PayoutPool.Unmineable;
            var gpuPool = _host.GpuEngine is not null && UseGpu ? _host.GpuPayoutPool ?? pool : (Core.Pools.PayoutPool?)null;
            var lines = Core.Fees.CostBreakdown.For(_host.Fees, pool, gpuPool, _host.ReferralCode is not null, _host.QuantusTarget is not null);
            return Core.Fees.CostBreakdown.Text(lines);
        }
    }

    public string WattsHint =>
        $"Leave empty to estimate (about {CpuPowerModel.ForThisMachine().FullLoadWatts:0} W for {Environment.ProcessorCount} threads). " +
        "Check your CPU's spec sheet or a power meter for a better number.";

    public bool IdleUnsupported => OnlyWhenIdle && IdleDetector.ForThisPlatform().GetIdleTime() is null;

    [ObservableProperty] public partial double CpuUsagePercent { get; set; }
    [ObservableProperty] public partial bool OnlyWhenIdle { get; set; }
    [ObservableProperty] public partial bool KeepAwakeWhileEarning { get; set; }

    partial void OnKeepAwakeWhileEarningChanged(bool value)
    {
        if (_loading)
            return;
        _host.Settings.KeepAwakeWhileEarning = value;
        _host.SaveSettings();
        _host.UpdateKeepAwake();
    }

    /// <summary>The opt-in earnings reports for otium.lol's real-earnings table.</summary>
    [ObservableProperty] public partial bool ShareEarningsStats { get; set; }

    partial void OnShareEarningsStatsChanged(bool value)
    {
        if (!_loading)
            _host.SetShareEarningsStats(value);
    }

    public bool GpuEfficiencySupported => _host.GpuEfficiencySupported;

    /// <summary>Windows can tell full-screen apps and games apart from ordinary use, which this needs.</summary>
    public bool BrowsingSupported => OperatingSystem.IsWindows() && _host.GpuEngine is not null;

    [ObservableProperty] public partial bool EarnWhileBrowsing { get; set; }

    [ObservableProperty] public partial bool NotificationsEnabled { get; set; }

    partial void OnNotificationsEnabledChanged(bool value)
    {
        if (_loading)
            return;
        _host.Settings.NotificationsEnabled = value;
        _host.SaveSettings();
    }

    partial void OnEarnWhileBrowsingChanged(bool value)
    {
        if (_loading)
            return;
        _host.Settings.EarnWhileBrowsing = value;
        _host.SaveSettings();
    }

    /// <summary>Efficiency mode: the NVIDIA card mines at a lower power limit (one administrator prompt to set up).</summary>
    [ObservableProperty] public partial bool GpuEfficiencyMode { get; set; }
    [ObservableProperty] public partial bool GpuEfficiencyBusy { get; set; }
    [ObservableProperty] public partial string? GpuEfficiencyMessage { get; set; }

    partial void OnGpuEfficiencyModeChanged(bool value)
    {
        if (_loading || value == _host.Settings.GpuEfficiencyMode)
            return;
        _ = ApplyGpuEfficiencyAsync(value);
    }

    private async Task ApplyGpuEfficiencyAsync(bool on)
    {
        GpuEfficiencyBusy = true;
        GpuEfficiencyMessage = await _host.SetGpuEfficiencyAsync(on);
        GpuEfficiencyBusy = false;
        if (_host.Settings.GpuEfficiencyMode != on)
        {
            _loading = true; // put the switch back without asking again
            GpuEfficiencyMode = _host.Settings.GpuEfficiencyMode;
            _loading = false;
        }
    }

    /// <summary>Pause the processor miner instead of stopping it when the user comes back (opt-in).</summary>
    [ObservableProperty] public partial bool KeepMinerReadyWhenBack { get; set; }

    partial void OnKeepMinerReadyWhenBackChanged(bool value)
    {
        if (_loading)
            return;
        _host.Settings.KeepMinerReadyWhenBack = value;
        _host.SaveSettings(); // rebuilds the engine config: turning it off stops a paused miner on the next tick
    }

    [ObservableProperty] public partial bool InstallUpdatesWhileAway { get; set; }

    partial void OnInstallUpdatesWhileAwayChanged(bool value)
    {
        if (_loading)
            return;
        _host.Settings.InstallUpdatesWhileAway = value;
        _host.SaveSettings();
    }

    [ObservableProperty] public partial decimal? IdleMinutes { get; set; }
    [ObservableProperty] public partial decimal? ElectricityPrice { get; set; }
    [ObservableProperty] public partial decimal? CpuFullLoadWatts { get; set; }
    [ObservableProperty] public partial bool NeverRunAtLoss { get; set; }
    [ObservableProperty] public partial bool StartWithSystem { get; set; }
    /// <summary>The user's own payout coin and address.</summary>
    public PayoutTargetEditor Payout { get; }

    /// <summary>The Otium balance option only exists when the server holds balances.</summary>
    public bool OffersBalance => _host.ServerIsCustodial || UseBalance;
    [ObservableProperty] public partial string? PayoutError { get; set; }
    [ObservableProperty] public partial string? PayoutSaved { get; set; }
    [ObservableProperty] public partial string ServerUrl { get; set; } = "";

    partial void OnCpuUsagePercentChanged(double value) => QueueSave();
    partial void OnIdleMinutesChanged(decimal? value) => QueueSave();
    partial void OnElectricityPriceChanged(decimal? value) => QueueSave();
    partial void OnCpuFullLoadWattsChanged(decimal? value) => QueueSave();
    partial void OnNeverRunAtLossChanged(bool value) => QueueSave();
    partial void OnServerUrlChanged(string value) => QueueSave();

    partial void OnOnlyWhenIdleChanged(bool value)
    {
        OnPropertyChanged(nameof(IdleUnsupported));
        QueueSave();
    }

    partial void OnStartWithSystemChanged(bool value)
    {
        if (_loading)
            return;
        try
        {
            AutoStart.Set(value);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            // Leave the toggle reflecting reality.
            StartWithSystem = AutoStart.IsEnabled();
        }
    }

    private void QueueSave()
    {
        if (_loading)
            return;
        _saveDebounce.Stop();
        _saveDebounce.Start();
    }

    private void Apply()
    {
        var s = _host.Settings;
        s.CpuUsagePercent = (int)Math.Round(Math.Clamp(CpuUsagePercent, 10, 100));
        s.OnlyWhenIdle = OnlyWhenIdle;
        s.IdleMinutes = (int)Math.Clamp(IdleMinutes ?? 5, 1, 120);
        var price = (double)Math.Clamp(ElectricityPrice ?? 0.15m, 0, 5);
        if (Math.Abs(price - s.ElectricityPricePerKwh) > 0.0001)
            s.ElectricityPriceIsEstimate = false;
        s.ElectricityPricePerKwh = price;
        s.CpuFullLoadWatts = CpuFullLoadWatts is > 0 ? (double)CpuFullLoadWatts : null;
        s.NeverRunAtLoss = NeverRunAtLoss;
        s.GpuWatts = GpuWatts is > 0 ? (double)GpuWatts : null;
        if (Uri.TryCreate(ServerUrl, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https")
            s.ServerUrl = ServerUrl.Trim();
        _host.RefreshPowerModel();
        _host.SaveSettings();
    }

    private bool CanSavePayout() => Payout.IsValid;

    [RelayCommand(CanExecute = nameof(CanSavePayout))]
    private async Task SavePayout()
    {
        PayoutSaved = null;
        PayoutError = null;
        if (Payout.Target is not { } target)
            return;
        _host.Settings.PayoutMode = Core.Settings.PayoutMode.OwnWallet;
        _host.Settings.PayoutAddress = target.ToString();
        _host.SaveSettings();
        _host.ApplyDeviceSwitches();
        UseBalance = false;
        PayoutSaved = $"Saved. New earnings go to this {target.Coin.Ticker} address.";
        OnPropertyChanged(nameof(CostText));
        await _host.SyncAccountAsync(CancellationToken.None);
        await _host.RefreshPoolBalanceAsync(force: true, CancellationToken.None);
    }

    [RelayCommand]
    private void OpenDataFolder() => Launch.Open(DataFolder);

    // ---------- help and problem reports ----------

    [ObservableProperty] public partial string ProblemNote { get; set; } = "";
    [ObservableProperty] public partial bool IsSendingReport { get; set; }
    [ObservableProperty] public partial string? ProblemReportResult { get; set; }
    [ObservableProperty] public partial string? ProblemReportReference { get; set; }

    [RelayCommand]
    private void OpenHelp() => Launch.Open(Core.AppIdentity.HelpUrl);

    /// <summary>Otium's Discord server, where users ask each other and the team.</summary>
    [RelayCommand]
    private void OpenDiscord() => Launch.Open(Core.AppIdentity.DiscordUrl);

    public string ContactEmail => Core.AppIdentity.ContactEmail;

    [ObservableProperty] public partial string EmailCopied { get; set; } = "";

    /// <summary>Opens the mail app; the address is also shown (and copyable) for computers without one.</summary>
    [RelayCommand]
    private void EmailUs() => Launch.Open($"mailto:{Core.AppIdentity.ContactEmail}");

    [RelayCommand]
    private async Task CopyEmail()
    {
        await ClipboardService.CopyAsync(Core.AppIdentity.ContactEmail);
        EmailCopied = "Copied";
    }

    [RelayCommand]
    private async Task SendProblemReport()
    {
        IsSendingReport = true;
        ProblemReportResult = null;
        ProblemReportReference = null;
        try
        {
            var reference = await _host.SendProblemReportAsync(ProblemNote);
            ProblemReportReference = reference;
            ProblemReportResult = $"Sent. Your reference is {reference}: mention it when you ask for help.";
            ProblemNote = "";
        }
        catch (Exception e) when (e is AccountRequestException or HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            ProblemReportResult = e is AccountRequestException rejected ? rejected.Message : "Couldn't reach Otium to send it. Check your internet connection and try again.";
        }
        finally
        {
            IsSendingReport = false;
        }
    }

    [RelayCommand]
    private Task CopyProblemReference() => ProblemReportReference is { } reference ? ClipboardService.CopyAsync(reference) : Task.CompletedTask;
}

public sealed record BenchmarkRow(string Algorithm, string Speed, string Power, string Earns, string Net, string Note)
{
    /// <summary>The coin people know rather than the algorithm's name ("Pearl", not "pearlhash").</summary>
    public string Coin => CoinFor(Algorithm);

    public static string CoinFor(string algorithm) => algorithm switch
    {
        "pearlhash" => "Pearl",
        "quantus" => "Quantus",
        "xelishash" => "Xelis",
        "kawpow" => "Ravencoin",
        "autolykos2" => "Ergo",
        "etchash" => "Ethereum Classic",
        "cn/gpu" => "Conceal",
        _ => algorithm,
    };
}
