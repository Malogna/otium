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
        if (host.GpuEngine is not null && host.GpuProvider is not null)
        {
            var names = string.Join(", ", host.GpuProvider.Gpus.Select(g => g.Describe()));
            Devices.Add(new DeviceViewModel("Graphics card", $"{names} · best-paying algorithm via SRBMiner", host.GpuEngine));
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
        RefreshUpdate();

        _balanceTimer = new DispatcherTimer { Interval = BalanceRefresh };
        _balanceTimer.Tick += async (_, _) => await RefreshBalanceAsync();
        _balanceTimer.Start();
        _ = RefreshBalanceAsync();
    }

    public ObservableCollection<DeviceViewModel> Devices { get; } = [];

    public bool IsWindows => OperatingSystem.IsWindows();

    [ObservableProperty] public partial string StateLabel { get; set; } = "Off";
    [ObservableProperty] public partial string StatusText { get; set; } = "";
    [ObservableProperty] public partial bool IsEarning { get; set; }
    [ObservableProperty] public partial bool IsPaused { get; set; }
    [ObservableProperty] public partial bool IsProblem { get; set; }
    [ObservableProperty] public partial string ToggleText { get; set; } = "Start earning";

    [ObservableProperty] public partial bool IsBlocked { get; set; }
    [ObservableProperty] public partial string BlockedPath { get; set; } = "";

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
        BlockedPath = blocked?.EngineBlockedPath ?? "";

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
        var wallet = _host.Settings.PayoutAddress;
        if (string.IsNullOrWhiteSpace(wallet))
            return;
        try
        {
            var balance = await _host.Provider.GetPayoutBalanceAsync(wallet, CancellationToken.None);
            if (balance is null)
                return;
            PoolPending = Format.Xmr(balance.Pending);
            PoolPaid = Format.Xmr(balance.Paid);
            PoolNote = $"MoneroOcean pays your wallet automatically once pending reaches {Format.Xmr(balance.MinimumPayout)}. CPU and GPU earnings both land here.";
            _poolDashboard = balance.DashboardUrl;
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            PoolNote = "Couldn't reach the pool to check your balance. Retrying in a few minutes.";
        }
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
        else if (!string.IsNullOrWhiteSpace(_host.Settings.PayoutAddress))
            Launch.Open(Core.Pools.MoneroOceanClient.DashboardUrl(_host.Settings.PayoutAddress).ToString());
    }

    [RelayCommand]
    private Task CopyBlockedPath() => ClipboardService.CopyAsync(BlockedPath);

    [RelayCommand]
    private static void OpenWindowsSecurity() => Launch.Open("windowsdefender://threat");

    [RelayCommand]
    private void RetryEngine() => _host.ApplyDeviceSwitchesAfterBlock();
}
