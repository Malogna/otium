using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Otium.App.Services;
using Otium.Core.Engine;
using Otium.Core.Providers;

namespace Otium.App.ViewModels;

/// <summary>Overall status, combined earnings across devices, and a row per device.</summary>
public partial class DashboardViewModel : ViewModelBase
{
    private static readonly TimeSpan BalanceRefresh = TimeSpan.FromMinutes(5);
    private readonly AppHost _host;
    private readonly DispatcherTimer _balanceTimer;
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

        host.Updates.Changed += () => Dispatcher.UIThread.Post(RefreshUpdate);
        host.AntivirusChanged += () => Dispatcher.UIThread.Post(Recalculate);
        RefreshUpdate();

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
        IsBlocked = blocked is not null;
        // One exclusion covers every engine (CPU and GPU), so name their shared folder rather than the blocked one.
        BlockedPath = blocked is null ? "" : _host.Paths.EnginesDir;
        if (IsBlocked)
            _ = _host.RefreshAntivirusAsync();
        AntivirusDetail = IsBlocked ? _host.AntivirusReport?.Describe(_host.Paths.EnginesDir) ?? "" : "";
        OnPropertyChanged(nameof(CanAllowAutomatically));

        var withNumbers = Devices.Where(d => d.HasNumbers).Select(d => d.Snapshot).ToList();
        var any = withNumbers.Count > 0;
        var user = withNumbers.Sum(s => s.UserUsdPerDay);
        var cost = withNumbers.Sum(s => s.ElectricityUsdPerDay);
        var watts = withNumbers.Sum(s => s.ExtraWatts);
        EarnPerDay = any ? Format.Usd(user) : "—";
        ElectricityPerDay = any ? Format.Usd(cost) : "—";
        PowerDetail = watts > 0 ? $"per day · +{Format.Watts(watts)}" : "per day";
        NetPerDay = any ? Format.Usd(user - cost) : "—";
        IsNetNegative = any && user - cost < 0;

        TotalEarned = Format.Usd(snapshots.Sum(s => s.TotalUserUsd));
        TotalElectricity = $"{Format.Usd(snapshots.Sum(s => s.TotalElectricityUsd))} ({Format.Kwh(snapshots.Sum(s => s.TotalEnergyKwh))})";
        TodayEarned = Format.Usd(snapshots.Sum(s => s.TodayUserUsd - s.TodayElectricityUsd));
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
