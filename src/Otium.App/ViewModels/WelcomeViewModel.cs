using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Otium.App.Services;
using Otium.Core.Accounts;
using Otium.Core.Projections;
using Otium.Core.Providers;

namespace Otium.App.ViewModels;

/// <summary>A device line on the "what your PC could earn" step.</summary>
public sealed record EstimateRow(Geometry? Icon, string Name, string What, string PerDay);

/// <summary>
/// First launch in three short steps: what Otium is, what this PC could earn, and a clear summary before Start.
/// Every choice has a sensible default, so a user can just press Next, Next, Start.
/// </summary>
public partial class WelcomeViewModel : ViewModelBase
{
    private readonly AppHost _host;
    private readonly Action _started;

    public WelcomeViewModel(AppHost host, Action started)
    {
        _host = host;
        _started = started;
        InviteCode = host.Settings.Referral.CodeUsed ?? "";
        UseGpu = host.Settings.GpuEnabled;
        ElectricityPrice = (decimal)host.Settings.ElectricityPricePerKwh;
        SignIn = new EmailSignInViewModel(host, OnSignedIn);
        _ = LoadEstimateAsync();
    }

    public EmailSignInViewModel SignIn { get; }

    public bool IsSupported => _host.Provider.IsSupportedOnThisPlatform;

    public bool HasGpu => _host.GpuEngine is not null;

    public string GpuName => _host.GpuProvider?.Gpus.FirstOrDefault()?.Name ?? (_host.IsSimulated ? "simulated graphics card" : "graphics card");

    public bool IsSimulated => _host.IsSimulated;

    public string CountryName
    {
        get
        {
            try
            {
                return RegionInfo.CurrentRegion.EnglishName;
            }
            catch (ArgumentException)
            {
                return "your country";
            }
        }
    }

    public string FeeLine => $"Otium keeps {_host.EffectiveFeePercent:0.#}% of earnings; the mining software adds its own 1–2%.";

    public ObservableCollection<EstimateRow> EstimateRows { get; } = [];

    // ---------- steps ----------

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsStep0), nameof(IsStep1), nameof(IsStep2))]
    public partial int Step { get; set; }

    public bool IsStep0 => Step == 0;

    public bool IsStep1 => Step == 1;

    public bool IsStep2 => Step == 2;

    private long _lastStepChange;

    [RelayCommand]
    private void Next() => GoTo(Step + 1);

    [RelayCommand]
    private void Back() => GoTo(Step - 1);

    // A double-click on Next would otherwise skip the estimate step entirely.
    private void GoTo(int step)
    {
        var now = Environment.TickCount64;
        if (now - _lastStepChange < 450)
            return;
        _lastStepChange = now;
        Step = Math.Clamp(step, 0, 2);
    }

    // ---------- estimate ----------

    [ObservableProperty] public partial bool IsEstimating { get; set; } = true;
    [ObservableProperty] public partial string MonthlyEstimate { get; set; } = "";
    [ObservableProperty] public partial string EstimateDetail { get; set; } = "";
    [ObservableProperty] public partial string? EstimateError { get; set; }
    [ObservableProperty] public partial bool IsEditingPrice { get; set; }
    [ObservableProperty] public partial decimal? ElectricityPrice { get; set; }
    [ObservableProperty] public partial bool UseGpu { get; set; }

    public string ElectricityLine =>
        $"Electricity at {Format.Money((decimal)_host.Settings.ElectricityPricePerKwh)}/kWh" +
        (_host.Settings.ElectricityPriceIsEstimate ? $" (typical for {CountryName})" : "");

    partial void OnUseGpuChanged(bool value)
    {
        _host.Settings.GpuEnabled = value;
        _host.SaveSettings();
        _ = LoadEstimateAsync();
    }

    partial void OnElectricityPriceChanged(decimal? value)
    {
        if (value is not { } price || Math.Abs((double)price - _host.Settings.ElectricityPricePerKwh) < 0.0001)
            return;
        _host.Settings.ElectricityPricePerKwh = (double)Math.Clamp(price, 0, 5);
        _host.Settings.ElectricityPriceIsEstimate = false;
        _host.SaveSettings();
        OnPropertyChanged(nameof(ElectricityLine));
        _ = LoadEstimateAsync();
    }

    [RelayCommand]
    private void EditPrice() => IsEditingPrice = !IsEditingPrice;

    private async Task LoadEstimateAsync()
    {
        IsEstimating = true;
        EstimateError = null;
        try
        {
            var estimate = await _host.EstimateAsync(CancellationToken.None);
            var projection = Projection.Create(estimate.GrossUsdPerDay, estimate.ElectricityUsdPerDay, null, _host.Settings.OnlyWhenIdle, 0, 1);

            EstimateRows.Clear();
            foreach (var device in estimate.Devices)
            {
                var icon = Icon(device.Device == DeviceKind.Cpu ? "IconCpu" : "IconGpu");
                var what = device.Device == DeviceKind.Cpu ? "Mines Monero" : "Mines whatever pays best (e.g. Ravencoin)";
                EstimateRows.Add(new EstimateRow(icon, device.Name, what, $"{Format.Usd(device.GrossUsdPerDay)}/day"));
            }

            var count = new CountUp(v => Dispatcher.UIThread.Post(() => MonthlyEstimate = $"≈ {Format.Money((decimal)v)}"));
            count.AnimateTo(projection.PerMonth);
            EstimateDetail = projection.PerMonth <= 0
                ? "At this electricity price mining wouldn't pay off. Otium only runs when it does, so it will mostly wait."
                : $"a month, earning about {projection.HoursPerDay:0} hours a day{(_host.Settings.OnlyWhenIdle ? " while you're away" : "")}. That's {Format.Usd(estimate.NetUsdPerDay)} per full day of earning, after electricity.";
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            EstimateError = "Couldn't fetch today's payout rates. You'll see real numbers once Otium is running.";
            MonthlyEstimate = "";
            EstimateDetail = "";
        }
        finally
        {
            IsEstimating = false;
        }
    }

    private static Geometry? Icon(string key) =>
        Avalonia.Application.Current?.TryGetResource(key, null, out var value) == true ? value as Geometry : null;

    // ---------- invite & sign-in ----------

    [ObservableProperty] public partial bool ShowInvite { get; set; }
    [ObservableProperty] public partial bool ShowSignIn { get; set; }
    [ObservableProperty] public partial string? SignedInText { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartCommand))]
    public partial string InviteCode { get; set; }

    [ObservableProperty] public partial string? InviteError { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartCommand))]
    public partial bool IsStarting { get; set; }

    partial void OnInviteCodeChanged(string value) =>
        InviteError = string.IsNullOrWhiteSpace(value) || ReferralCodes.IsWellFormed(value)
            ? null
            : $"Invite codes are {ReferralCodes.Length} letters and numbers.";

    [RelayCommand]
    private void ToggleInvite() => ShowInvite = !ShowInvite;

    [RelayCommand]
    private void ToggleSignIn() => ShowSignIn = !ShowSignIn;

    private void OnSignedIn()
    {
        if (_host.Settings.Account.Email is not { } email)
            return;
        SignedInText = $"Signed in as {email}. Your balance comes with you.";
        ShowSignIn = false;
        Step = 1;
    }

    private bool CanStart() => IsSupported && !IsStarting && InviteError is null;

    [RelayCommand(CanExecute = nameof(CanStart))]
    private async Task Start()
    {
        IsStarting = true;
        _started(); // show the home screen right away; it reports progress while the account connects
        await _host.StartFirstTimeAsync(InviteCode);
    }
}
