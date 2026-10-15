using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Otium.App.Services;
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
    private readonly DispatcherTimer _saveDebounce;
    private bool _loading = true;

    public SettingsViewModel(AppHost host)
    {
        _host = host;
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
        IdleMinutes = s.IdleMinutes;
        ElectricityPrice = (decimal)s.ElectricityPricePerKwh;
        CpuFullLoadWatts = s.CpuFullLoadWatts is { } w ? (decimal)w : null;
        NeverRunAtLoss = s.NeverRunAtLoss;
        PayoutAddress = s.PayoutAddress;
        ServerUrl = s.ServerUrl;
        StartWithSystem = AutoStart.IsEnabled();
        UseBalance = s.PayoutMode == PayoutMode.Balance;
        UseCpu = s.CpuEnabled;
        UseGpu = s.GpuEnabled;
        GpuWatts = s.GpuWatts is { } gw ? (decimal)gw : null;
        _loading = false;
        _ = RefreshBenchmarksAsync();
        UpdateStatus = host.Updates.Status;
        host.Updates.Changed += () => Dispatcher.UIThread.Post(() => UpdateStatus = _host.Updates.Status);
    }

    public EmailSignInViewModel SignIn => _signIn ??= new EmailSignInViewModel(_host, () => OnPropertyChanged(nameof(AccountText)));

    private EmailSignInViewModel? _signIn;

    public string AccountText => _host.Settings.Account.Email is { } email
        ? $"Signed in as {email}" + (_host.Settings.Account.DeviceCount > 1 ? $" · {_host.Settings.Account.DeviceCount} computers share this balance" : "")
        : "Not signed in. Add your email so your balance can't get lost, or sign in to an account you use on another computer.";

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
    [NotifyPropertyChangedFor(nameof(UseOwnWallet))]
    public partial bool UseBalance { get; set; }

    public bool UseOwnWallet
    {
        get => !UseBalance;
        set => UseBalance = !value;
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
    partial void OnUseGpuChanged(bool value) => ApplyDevices();
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
                    option.Algorithm == best ? "best" : option.Algorithm == gpu.CurrentAlgorithm ? "mining" : ""));
            }
            foreach (var failed in results.Where(r => !r.Succeeded))
                Benchmarks.Add(new BenchmarkRow(failed.Algorithm, "—", "—", "—", "—", $"didn't work: {Shorten(failed.Failure)}"));

            HasBenchmarks = Benchmarks.Count > 0;
            BenchmarkNote = $"Using today's MoneroOcean payouts and your electricity price. Re-checked every {SrbMinerGpuProvider.ReevaluateEvery.TotalMinutes:0} minutes while mining.";
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
        {
            BenchmarkNote = "Couldn't reach MoneroOcean for today's payouts.";
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

    public string EngineInfo =>
        $"CPU: XMRig {XmrigRelease.Version} → MoneroOcean ({PoolEndpoint.MoneroOcean}). " +
        $"GPU: SRBMiner-Multi {SrbMinerRelease.Version} → MoneroOcean ({GpuPoolEndpoint.MoneroOcean}), paid in XMR.";

    public string FeeInfo =>
        $"App fee {FeePolicy.Default.FeePercent:0.#}% of mining time (half of it goes to whoever invited you, if anyone). " +
        "XMRig keeps its own 1% developer donation; SRBMiner charges 0.65–2% depending on the GPU algorithm. " +
        $"Fee wallet:{(_host.FeeWallet is null ? "not configured in this build, so the fee slice is mined for you" : "configured")}.";

    public string WattsHint =>
        $"Leave empty to estimate (about {CpuPowerModel.ForThisMachine().FullLoadWatts:0} W for {Environment.ProcessorCount} threads). " +
        "Check your CPU's spec sheet or a power meter for a better number.";

    public bool IdleUnsupported => OnlyWhenIdle && IdleDetector.ForThisPlatform().GetIdleTime() is null;

    [ObservableProperty] public partial double CpuUsagePercent { get; set; }
    [ObservableProperty] public partial bool OnlyWhenIdle { get; set; }
    [ObservableProperty] public partial decimal? IdleMinutes { get; set; }
    [ObservableProperty] public partial decimal? ElectricityPrice { get; set; }
    [ObservableProperty] public partial decimal? CpuFullLoadWatts { get; set; }
    [ObservableProperty] public partial bool NeverRunAtLoss { get; set; }
    [ObservableProperty] public partial bool StartWithSystem { get; set; }
    [ObservableProperty] public partial string PayoutAddress { get; set; } = "";
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

    [RelayCommand]
    private void SavePayout()
    {
        PayoutSaved = null;
        PayoutError = _host.Provider.ValidatePayoutAddress(PayoutAddress);
        if (PayoutError is not null)
            return;
        _host.Settings.PayoutAddress = PayoutAddress.Trim();
        _host.SaveSettings();
        _ = _host.SyncAccountAsync(CancellationToken.None);
        PayoutSaved = "Saved. New earnings go to this address.";
    }

    [RelayCommand]
    private void OpenDataFolder() => Launch.Open(DataFolder);
}

public sealed record BenchmarkRow(string Algorithm, string Speed, string Power, string Earns, string Net, string Note);
