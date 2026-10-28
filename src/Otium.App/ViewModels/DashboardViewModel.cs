using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Otium.App.Services;
using Otium.Core.Bandwidth;
using Otium.Core.Engine;
using Otium.Core.Providers;

namespace Otium.App.ViewModels;

/// <summary>Overall status, combined earnings across devices, and a row per device.</summary>
public partial class DashboardViewModel : ViewModelBase
{
    private static readonly TimeSpan BalanceRefresh = TimeSpan.FromMinutes(5);
    private readonly AppHost _host;
    private readonly DispatcherTimer _balanceTimer;
    private readonly DispatcherTimer _statsTimer;

    private void RefreshStats()
    {
        Devices[0].Stats = _host.Hardware.ReadCpu().Describe() ?? "";
        if (Devices.Count > 1 && _host.GpuProvider is { } gpu)
        {
            var cards = _host.Hardware.ReadGpus(gpu.Gpus);
            Devices[1].Stats = cards.Count == 1
                ? cards[0].Describe() ?? ""
                : string.Join(Environment.NewLine, cards.Select(c => $"{c.Name}: {c.Describe() ?? "no readings"}"));
        }
    }

    private Uri? _poolDashboard;

    public DashboardViewModel(AppHost host)
    {
        _host = host;

        Devices.Add(new DeviceViewModel("Processor", $"{Environment.ProcessorCount} threads · Monero (RandomX) via XMRig", host.Engine));
        if (host.GpuEngine is not null)
        {
            var subtitle = host.GpuProvider is { } gpu
                ? $"{string.Join(", ", gpu.Gpus.Select(g => g.Describe()))} · best-paying algorithm via SRBMiner"
                : "Simulated graphics card (dev mode)";
            Devices.Add(new DeviceViewModel("Graphics card", subtitle, host.GpuEngine));
        }

        foreach (var device in Devices)
        {
            var row = device;
            row.Engine.SnapshotChanged += snapshot => Dispatcher.UIThread.Post(() =>
            {
                row.Apply(snapshot);
                Recalculate();
            });
            row.Apply(row.Engine.Snapshot);
        }
        Recalculate();

        host.Bandwidth.SnapshotChanged += _ => Dispatcher.UIThread.Post(Recalculate);
        host.EarningSwitched += () => Dispatcher.UIThread.Post(Recalculate);
        if (host.Mysterium is { } mysterium)
            mysterium.SnapshotChanged += _ => Dispatcher.UIThread.Post(Recalculate);
        if (host.Aioz is { } aioz)
            aioz.SnapshotChanged += _ => Dispatcher.UIThread.Post(Recalculate);
        host.Updates.Changed += () => Dispatcher.UIThread.Post(RefreshUpdate);
        host.AntivirusChanged += () => Dispatcher.UIThread.Post(Recalculate);
        RefreshUpdate();

        // Processor and graphics card stats, every few seconds (cheap: a few driver calls).
        _statsTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _statsTimer.Tick += (_, _) => RefreshStats();
        _statsTimer.Start();
        RefreshStats();

        _balanceTimer = new DispatcherTimer { Interval = BalanceRefresh };
        _balanceTimer.Tick += async (_, _) => await RefreshBalanceAsync();
        _balanceTimer.Start();
        _ = RefreshBalanceAsync();
    }

    public ObservableCollection<DeviceViewModel> Devices { get; } = [];

    public bool IsWindows => OperatingSystem.IsWindows();

    /// <summary>The one-click fix only changes Microsoft Defender, so it's offered only while Defender is the antivirus.</summary>
    public bool CanAllowAutomatically => IsWindows && (_host.AntivirusReport?.DefenderIsActive ?? true);

    /// <summary>What the antivirus removed and when, and how to fix it when it isn't Defender.</summary>
    [ObservableProperty] public partial string AntivirusDetail { get; set; } = "";

    [ObservableProperty] public partial string StateLabel { get; set; } = "Off";
    [ObservableProperty] public partial string StatusText { get; set; } = "";
    [ObservableProperty] public partial bool IsEarning { get; set; }
    [ObservableProperty] public partial bool IsPaused { get; set; }
    [ObservableProperty] public partial bool IsProblem { get; set; }
    [ObservableProperty] public partial string ToggleText { get; set; } = "Start earning";

    [ObservableProperty] public partial bool IsBlocked { get; set; }
    [ObservableProperty] public partial string BlockedPath { get; set; } = "";
    [ObservableProperty] public partial string? AllowResult { get; set; }
    [ObservableProperty] public partial bool IsAllowing { get; set; }

    [ObservableProperty] public partial string EarnPerDay { get; set; } = "—";
    [ObservableProperty] public partial string ElectricityPerDay { get; set; } = "—";
    [ObservableProperty] public partial string PowerDetail { get; set; } = "per day";
    [ObservableProperty] public partial string NetPerDay { get; set; } = "—";
    [ObservableProperty] public partial bool IsNetNegative { get; set; }

    // Internet sharing, shown next to the miners when it's on.
    [ObservableProperty] public partial bool ShowSharing { get; set; }
    [ObservableProperty] public partial string SharingLabel { get; set; } = "Off";
    [ObservableProperty] public partial bool SharingIsEarning { get; set; }
    [ObservableProperty] public partial bool SharingIsPaused { get; set; }
    [ObservableProperty] public partial bool SharingIsProblem { get; set; }
    [ObservableProperty] public partial string SharingWhat { get; set; } = "";
    [ObservableProperty] public partial string SharingPerDay { get; set; } = "";
    [ObservableProperty] public partial string SharingDetail { get; set; } = "";
    [ObservableProperty] public partial string SharingEarned { get; set; } = "$0.000";

    [ObservableProperty] public partial bool ShowMysterium { get; set; }
    [ObservableProperty] public partial string MysteriumLabel { get; set; } = "Off";
    [ObservableProperty] public partial bool MysteriumIsEarning { get; set; }
    [ObservableProperty] public partial bool MysteriumIsPaused { get; set; }
    [ObservableProperty] public partial bool MysteriumIsProblem { get; set; }
    [ObservableProperty] public partial string MysteriumWhat { get; set; } = "";
    [ObservableProperty] public partial string MysteriumPerDay { get; set; } = "";
    [ObservableProperty] public partial string MysteriumDetail { get; set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowSharingTotal))]
    public partial bool ShowAioz { get; set; }

    [ObservableProperty] public partial string AiozLabel { get; set; } = "Off";
    [ObservableProperty] public partial bool AiozIsEarning { get; set; }
    [ObservableProperty] public partial bool AiozIsPaused { get; set; }
    [ObservableProperty] public partial bool AiozIsProblem { get; set; }
    [ObservableProperty] public partial string AiozWhat { get; set; } = "";
    [ObservableProperty] public partial string AiozPerDay { get; set; } = "";
    [ObservableProperty] public partial string AiozDetail { get; set; } = "";

    /// <summary>"Internet sharing" in the totals when any kind is on.</summary>
    public bool ShowSharingTotal => ShowSharing || ShowMysterium || ShowAioz;

    [ObservableProperty] public partial string TotalEarned { get; set; } = "$0.000";
    [ObservableProperty] public partial string TotalElectricity { get; set; } = "$0.000";
    [ObservableProperty] public partial string TodayEarned { get; set; } = "$0.000";
    [ObservableProperty] public partial string MiningTime { get; set; } = "0 min";

    [ObservableProperty] public partial string FeeText { get; set; } = "";
    [ObservableProperty] public partial string MiningFor { get; set; } = "";

    [ObservableProperty] public partial bool UpdateReady { get; set; }
    [ObservableProperty] public partial string UpdateText { get; set; } = "";
    public bool IsOwnWallet => _host.Settings.PayoutMode == Core.Settings.PayoutMode.OwnWallet;

    [ObservableProperty] public partial string PoolPending { get; set; } = "—";
    [ObservableProperty] public partial string PoolPaid { get; set; } = "—";
    [ObservableProperty] public partial string PoolNote { get; set; } = "Pool balance appears after your first accepted shares.";

    private void Recalculate()
    {
        var snapshots = Devices.Select(d => d.Snapshot).ToList();
        var active = Devices.Where(d => d.Snapshot.State != EngineState.Off).ToList();

        ToggleText = _host.IsEarning ? "Stop" : "Start earning";

        // Overall state: the "best" thing any device is doing.
        var overall = active.Select(d => d.Snapshot.State).OrderBy(Priority).FirstOrDefault(EngineState.Off);
        StateLabel = DeviceViewModel.Label(overall);
        IsEarning = overall is EngineState.Earning or EngineState.Starting;
        IsPaused = overall is EngineState.WaitingForIdle or EngineState.PausedUnprofitable;
        IsProblem = overall is EngineState.Error;

        var earning = Devices.Where(d => d.Snapshot.State == EngineState.Earning).Select(d => d.Title.ToLowerInvariant()).ToList();
        StatusText = active.Count == 0
            ? "Not earning. Press Start earning to put your spare power to work."
            : earning.Count > 0
                ? $"Earning with your {string.Join(" and ", earning)}"
                : active.OrderBy(d => Priority(d.Snapshot.State)).First().StatusText;

        var blocked = snapshots.FirstOrDefault(s => s.EngineBlocked);
        IsBlocked = blocked is not null || _host.Sharers.Any(s => s.Snapshot.EngineBlocked);
        // One exclusion covers every engine (CPU and GPU), so name their shared folder rather than the blocked one.
        BlockedPath = IsBlocked ? _host.Paths.EnginesDir : "";
        if (IsBlocked)
            _ = _host.RefreshAntivirusAsync();
        AntivirusDetail = IsBlocked ? _host.AntivirusReport?.Describe(_host.Paths.EnginesDir) ?? "" : "";
        OnPropertyChanged(nameof(CanAllowAutomatically));

        RefreshSharing();
        RefreshMysterium();
        RefreshAioz();
        var sharingNow = _host.Sharers.Select(s => s.Snapshot).Where(s => s.State == BandwidthState.Sharing).Sum(s => s.UserUsdPerDay);
        var withNumbers = Devices.Where(d => d.HasNumbers).Select(d => d.Snapshot).ToList();
        var any = withNumbers.Count > 0 || sharingNow > 0;
        var user = withNumbers.Sum(s => s.UserUsdPerDay) + sharingNow;
        var cost = withNumbers.Sum(s => s.ElectricityUsdPerDay);
        var watts = withNumbers.Sum(s => s.ExtraWatts);
        var freePower = !_host.Settings.PayForElectricity;
        EarnPerDay = any ? Format.Usd(user) : "—";
        ElectricityPerDay = !any ? "—" : freePower ? "Free" : Format.Usd(cost);
        PowerDetail = (freePower ? "you don't pay for this power" : "per day") + (watts > 0 ? $" · +{Format.Watts(watts)}" : "");
        NetPerDay = any ? Format.Usd(user - cost) : "—";
        IsNetNegative = any && user - cost < 0;

        TotalEarned = Format.Usd(snapshots.Sum(s => s.TotalUserUsd));
        SharingEarned = Format.Usd(_host.Sharers.Sum(s => s.Snapshot.TotalUserUsd));
        OnPropertyChanged(nameof(ShowSharingTotal));
        var kwh = Format.Kwh(snapshots.Sum(s => s.TotalEnergyKwh));
        TotalElectricity = freePower ? $"free ({kwh})" : $"{Format.Usd(snapshots.Sum(s => s.TotalElectricityUsd))} ({kwh})";
        // Today's profit counts internet sharing too: the history has both, the miners have today's electricity.
        var today = DateTimeOffset.Now.Date;
        TodayEarned = Format.Usd(_host.History.EarnedSince(new DateTimeOffset(today, DateTimeOffset.Now.Offset))
                                 - snapshots.Sum(s => s.TodayElectricityUsd));
        MiningTime = string.Join(" · ", Devices.Select(d => $"{d.Title}: {Format.Duration(d.Snapshot.TotalMiningTime)}"));

        var now = DateTimeOffset.UtcNow;
        var schedule = _host.FeeSchedule;
        var feePercent = schedule.EffectiveFeePercent(now);
        if (!IsOwnWallet)
        {
            var fee = _host.Settings.Account.FeePercent;
            FeeText = _host.Settings.Referral.FeeFreeUntil is { } freeUntil && freeUntil > now
                ? $"Fee-free until {Format.Date(freeUntil)}"
                : $"Otium fee: {fee.ToString("0.#", CultureInfo.InvariantCulture)}% (taken when your balance is credited)";
        }
        else
        FeeText = schedule.IsFeeFree(now) && _host.Settings.Referral.FeeFreeUntil is { } until
            ? $"Fee-free until {Format.Date(until)}"
            : feePercent > 0
                ? $"Fee: {feePercent.ToString("0.#", CultureInfo.InvariantCulture)}% of mining time"
                : "No app fee in this build";

        var slices = Devices
            .Where(d => d.Snapshot.State == EngineState.Earning && d.Snapshot.CurrentRole != PayoutRole.User)
            .Select(d => $"{d.Title} is mining a short slice for {DeviceViewModel.RoleText(d.Snapshot.CurrentRole)}")
            .ToList();
        MiningFor = earning.Count == 0 ? "" : slices.Count > 0 ? string.Join(" · ", slices) : "Mining for: you";
    }

    /// <summary>The internet sharing row: like a device's, with Proxybase's estimate until there's a real pace.</summary>
    private void RefreshSharing()
    {
        var settings = _host.Settings;
        ShowSharing = settings.BandwidthEnabled && settings.BandwidthConsentAt is not null && _host.BandwidthOffered;
        if (!ShowSharing)
            return;
        var b = _host.Bandwidth.Snapshot;
        SharingLabel = b.State switch
        {
            BandwidthState.Sharing => "Sharing",
            BandwidthState.WaitingForIdle => "Waiting",
            BandwidthState.LimitReached => "Limit reached",
            BandwidthState.Starting => "Starting",
            BandwidthState.Error or BandwidthState.Unavailable => "Problem",
            _ => settings.EarningEnabled ? "Starting" : "Off",
        };
        SharingIsEarning = b.State == BandwidthState.Sharing;
        SharingIsPaused = b.State is BandwidthState.WaitingForIdle or BandwidthState.LimitReached;
        SharingIsProblem = b.State is BandwidthState.Error or BandwidthState.Unavailable;
        // A problem in its own words; otherwise, while sharing, what Proxybase says about this connection.
        SharingWhat = b.State is BandwidthState.Error or BandwidthState.Unavailable ? b.StatusText : b.Detail ?? "";
        var estimate = InternetSharingViewModel.EstimateFor(_host);
        SharingPerDay = b.UserUsdPerDay > 0 ? $"{Format.Usd(b.UserUsdPerDay)}/day"
            : estimate > 0 ? $"≈ {Format.Usd(estimate)}/day"
            : "";
        SharingDetail = b.TotalUserUsd > 0 ? $"{Format.Usd(b.TotalUserUsd)} earned so far"
            : estimate > 0 ? "estimate · nothing earned yet"
            : b.BytesToday > 0 ? $"{InternetSharingViewModel.FormatBytes(b.BytesToday)} today" : "";
    }

    /// <summary>The Mysterium row: its state, its pace in dollars (MYST at today's price) and what it earned in MYST.</summary>
    private void RefreshMysterium()
    {
        var settings = _host.Settings;
        ShowMysterium = _host.Mysterium is not null && settings.MysteriumEnabled && settings.MysteriumConsentAt is not null && _host.MysteriumOffered;
        if (!ShowMysterium || _host.Mysterium is not { } sharer)
            return;
        var b = sharer.Snapshot;
        MysteriumLabel = b.State switch
        {
            BandwidthState.Sharing => "Sharing",
            BandwidthState.WaitingForIdle => "Waiting",
            BandwidthState.LimitReached => "Limit reached",
            BandwidthState.Starting => "Starting",
            BandwidthState.Error or BandwidthState.Unavailable => "Problem",
            _ => settings.EarningEnabled ? "Starting" : "Off",
        };
        MysteriumIsEarning = b.State == BandwidthState.Sharing;
        MysteriumIsPaused = b.State is BandwidthState.WaitingForIdle or BandwidthState.LimitReached;
        MysteriumIsProblem = b.State is BandwidthState.Error or BandwidthState.Unavailable;
        MysteriumWhat = b.State is BandwidthState.Error or BandwidthState.Unavailable or BandwidthState.Starting ? b.StatusText : b.Earnings ?? "";
        MysteriumPerDay = b.UserUsdPerDay > 0 ? $"{Format.Usd(b.UserUsdPerDay)}/day" : "";
        MysteriumDetail = b.TotalUserUsd > 0 ? $"{Format.Usd(b.TotalUserUsd)} earned so far"
            : b.BytesToday > 0 ? $"{InternetSharingViewModel.FormatBytes(b.BytesToday)} today" : "nothing earned yet";
    }

    /// <summary>The AIOZ row: its state, its pace in dollars (AIOZ at today's price) and what it earned in AIOZ.</summary>
    private void RefreshAioz()
    {
        var settings = _host.Settings;
        ShowAioz = _host.Aioz is not null && settings.AiozEnabled && settings.AiozConsentAt is not null && _host.AiozOffered;
        if (!ShowAioz || _host.Aioz is not { } sharer)
            return;
        var b = sharer.Snapshot;
        AiozLabel = b.State switch
        {
            BandwidthState.Sharing => "Sharing",
            BandwidthState.WaitingForIdle => "Waiting",
            BandwidthState.Starting => "Starting",
            BandwidthState.Error or BandwidthState.Unavailable => "Problem",
            _ => settings.EarningEnabled ? "Starting" : "Off",
        };
        AiozIsEarning = b.State == BandwidthState.Sharing;
        AiozIsPaused = b.State is BandwidthState.WaitingForIdle;
        AiozIsProblem = b.State is BandwidthState.Error or BandwidthState.Unavailable;
        AiozWhat = b.State is BandwidthState.Error or BandwidthState.Unavailable or BandwidthState.Starting ? b.StatusText
            : string.Join(" · ", new[] { b.Activity, b.Earnings }.Where(x => !string.IsNullOrEmpty(x)));
        AiozPerDay = b.UserUsdPerDay > 0 ? $"{Format.Usd(b.UserUsdPerDay)}/day" : "";
        AiozDetail = b.TotalUserUsd > 0 ? $"{Format.Usd(b.TotalUserUsd)} earned so far" : "nothing earned yet";
    }

    private static int Priority(EngineState state) => state switch
    {
        EngineState.Earning => 0,
        EngineState.Starting => 1,
        EngineState.Installing => 2,
        EngineState.Error => 3,
        EngineState.WaitingForIdle => 4,
        EngineState.PausedUnprofitable => 5,
        _ => 6,
    };

    private async Task RefreshBalanceAsync()
    {
        await _host.RefreshPoolBalanceAsync(force: false, CancellationToken.None);
        if (_host.PoolBalance is not { } balance)
        {
            if (_host.PoolBalanceError is { } error)
                PoolNote = error;
            return;
        }
        PoolPending = Format.Coin(balance.Pending, balance.Coin);
        PoolPaid = Format.Coin(balance.Paid, balance.Coin);
        PoolNote = $"{balance.PoolName} pays your address once pending reaches {Format.Coin(balance.MinimumPayout, balance.Coin)}. CPU and GPU earnings both land here."
                   + (balance.AutoPay ? "" : $" Auto pay is off for this address at {balance.PoolName}: turn it on there or it won't pay you by itself.")
                   + (balance.Problems is { Count: > 0 } problems ? " " + string.Join(" ", problems) : "");
        _poolDashboard = balance.DashboardUrl;
        if (_host.GpuPoolBalance is { } gpu)
            PoolNote += $" Your graphics card mines on {gpu.PoolName}, which pays the same address separately: " +
                        $"{Format.Coin(gpu.Pending, gpu.Coin)} there now, paid out at {Format.Coin(gpu.MinimumPayout, gpu.Coin)}" +
                        (gpu.AutoPay ? "." : $" (auto pay is off at {gpu.PoolName}: turn it on there).");
    }

    private void RefreshUpdate()
    {
        UpdateReady = _host.Updates.ReadyVersion is not null;
        UpdateText = UpdateReady ? $"Otium {_host.Updates.ReadyVersion} is ready to install. Mining pauses for a few seconds while it restarts." : "";
    }

    [RelayCommand]
    private Task RestartToUpdate() => _host.ApplyUpdateAndRestartAsync();

    [RelayCommand]
    private void ToggleEarning()
    {
        _host.SetEarning(!_host.IsEarning);
        Recalculate();
    }

    [RelayCommand]
    private void OpenPoolDashboard()
    {
        if (_poolDashboard is not null)
            Launch.Open(_poolDashboard.ToString());
        else if (Core.Wallets.PayoutTarget.ParseOrNull(_host.Settings.PayoutAddress) is { } target)
            Launch.Open((target.Coin == Core.Wallets.PayoutCoins.Monero
                ? Core.Pools.MoneroOceanClient.DashboardUrl(target.Address)
                : Core.Pools.UnmineableClient.DashboardUrl(target)).ToString());
    }

    [RelayCommand]
    private Task CopyBlockedPath() => ClipboardService.CopyAsync(BlockedPath);

    [RelayCommand]
    private static void OpenWindowsSecurity() => Launch.Open("windowsdefender://threat");

    [RelayCommand]
    private void RetryEngine() => _host.ApplyDeviceSwitchesAfterBlock();

    [RelayCommand]
    private async Task AllowAutomatically()
    {
        IsAllowing = true;
        AllowResult = await _host.AllowEnginesInWindowsSecurityAsync() ?? "Allowed. Downloading the engines again…";
        IsAllowing = false;
    }
}
