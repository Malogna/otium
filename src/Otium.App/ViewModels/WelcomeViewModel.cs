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
/// First launch. The first screen offers two ways in: <b>Quick start</b> turns mining on (processor, graphics card,
/// keeping the miner ready, sharing earnings figures) and asks only where to pay, then shows exactly what's on before
/// Start, with internet sharing as its own tick box (it uses the user's internet address, so it's never agreed to in
/// passing); <b>Customize</b> walks through what this PC could earn, where to be paid, and each choice.
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
        UseCpu = host.Settings.CpuEnabled;
        ElectricityPrice = (decimal)host.Settings.ElectricityPricePerKwh;
        PayForElectricity = host.Settings.PayForElectricity;
        _ready = true;
        SignIn = new EmailSignInViewModel(host, OnSignedIn);
        Payout = new PayoutTargetEditor(host.Settings.PayoutAddress, host.Settings.UsPerson, host.SetUsPerson);
        Payout.Changed += () =>
        {
            NextCommand.NotifyCanExecuteChanged();
            StartCommand.NotifyCanExecuteChanged();
            OnPropertyChanged(nameof(PayoutLine));
            OnPropertyChanged(nameof(FeeLine));
        };
        _ = LoadEstimateAsync();
    }

    /// <summary>The payout step: coin and the user's own address.</summary>
    public PayoutTargetEditor Payout { get; }

    public const int PayoutStep = 2;
    public const int LastStep = 3;

    /// <summary>The summary's line about payouts.</summary>
    public string PayoutLine => Payout.Target is { } t
        ? $"{(t.Coin == Core.Wallets.PayoutCoins.Monero ? "MoneroOcean" : "unMineable")} pays your {t.Coin.Ticker} address " +
          $"{Shorten(t.Address)} directly, once you've earned {(t.Coin == Core.Wallets.PayoutCoins.Monero ? "0.003 XMR" : t.Coin.ThresholdText)}. Otium never holds your money."
        : "Add your payout address on the previous step.";

    private static string Shorten(string address) => address.Length > 16 ? $"{address[..8]}…{address[^6..]}" : address;

    public EmailSignInViewModel SignIn { get; }

    public bool IsSupported => _host.Provider.IsSupportedOnThisPlatform;

    public bool HasGpu => _host.GpuEngine is not null;

    public string GpuName => _host.GpuProvider?.Gpus.FirstOrDefault()?.Name ?? (_host.IsSimulated ? "simulated graphics card" : "graphics card");

    public bool IsSimulated => _host.IsSimulated;

    /// <summary>Why Otium can't run in this computer's country (sanctions, mining bans), or null.</summary>
    public string? UnavailableHere => _host.UnavailableHere;

    /// <summary>Quick start and Customize work: a supported computer in a country Otium runs in.</summary>
    public bool CanBegin => IsSupported && UnavailableHere is null;

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

    /// <summary>The full cost stack for the payout chosen on the previous step, so nothing comes as a surprise.</summary>
    public string FeeLine
    {
        get
        {
            var target = Payout.Target?.ToString();
            var pool = Core.Pools.PayoutRouting.PoolFor(target, AppHost.InUnitedStates) ?? Core.Pools.PayoutPool.Unmineable;
            var gpuPool = HasGpu && UseGpu ? pool : (Core.Pools.PayoutPool?)null;
            var lines = Core.Fees.CostBreakdown.For(_host.Fees, pool, gpuPool, _host.ReferralCode is not null);
            return "What it costs you:\n" + Core.Fees.CostBreakdown.Text(lines);
        }
    }

    public ObservableCollection<EstimateRow> EstimateRows { get; } = [];

    // ---------- steps ----------

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsStep0), nameof(IsStep1), nameof(IsStep2), nameof(IsStep3), nameof(ShowNext))]
    [NotifyCanExecuteChangedFor(nameof(NextCommand))]
    public partial int Step { get; set; }

    public bool IsStep0 => Step == 0;

    public bool IsStep1 => Step == 1;

    public bool IsStep2 => Step == 2;

    public bool IsStep3 => Step == LastStep;

    /// <summary>The first screen has its own two buttons (Quick start, Customize) instead of Next.</summary>
    public bool ShowNext => Step != 0 && Step != LastStep;

    private long _lastStepChange;

    private bool CanGoNext() => Step != PayoutStep || Payout.IsValid;

    [RelayCommand(CanExecute = nameof(CanGoNext))]
    private void Next() => GoTo(Step + 1);

    [RelayCommand]
    private void Back() => GoTo(IsQuick && Step == PayoutStep ? 0 : Step - 1);

    // ---------- quick start or customize ----------

    /// <summary>The user chose Quick start: the estimate step is skipped and every option is on.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCustom))]
    public partial bool IsQuick { get; set; }

    public bool IsCustom => !IsQuick;

    /// <summary>Internet sharing can run on this computer (Proxybase has a client for it).</summary>
    public bool CanShareInternet => _host.UsesProxybase ? Otium.Providers.Proxybase.ProxybaseRelease.ForCurrentPlatform() is not null : false;

    /// <summary>Mining on, then straight to where to be paid. Internet sharing waits for its own tick on the summary.</summary>
    [RelayCommand]
    private void QuickStart()
    {
        IsQuick = true;
        UseCpu = true;
        if (HasGpu)
            UseGpu = true;
        ShareInternet = false;
        KeepMinerReady = true;
        ShareEarnings = true;
        GoTo(PayoutStep);
    }

    /// <summary>Step by step, with the cautious defaults: internet sharing and keeping the miner ready stay off.</summary>
    [RelayCommand]
    private void Customize()
    {
        IsQuick = false;
        ShareInternet = false;
        KeepMinerReady = false;
        GoTo(1);
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartCommand))]
    public partial bool UseCpu { get; set; }

    partial void OnUseCpuChanged(bool value)
    {
        _host.Settings.CpuEnabled = value;
        _host.SaveSettings();
    }

    /// <summary>Share unused internet through Proxybase: always the user's own tick, in Quick start and Customize.</summary>
    [ObservableProperty] public partial bool ShareInternet { get; set; }

    /// <summary>What ticking internet sharing means, in both setups.</summary>
    public string SharingChoiceText =>
        "Proxybase's customers, mostly AI agents and businesses collecting public web pages, use some of your unused internet " +
        "connection. Websites they visit see your internet address. Otium can't see or control this traffic, and if someone " +
        "misuses it, it shows your address; some internet providers don't allow sharing a connection. It's paid to your " +
        "address in your coin, like mining. Ticking it means you agree to Proxybase's terms.";

    /// <summary>What Quick start turns on, one line each, so the user sees it all before pressing Start.</summary>
    public IReadOnlyList<string> QuickList
    {
        get
        {
            var list = new List<string> { "Your processor mines while you're away." };
            if (HasGpu)
                list.Add($"Your {GpuName} mines the best-paying coin while you're away.");
            list.Add("The processor miner pauses instead of closing when you come back, so it earns again the moment you leave (it keeps about 2.5 GB of memory for up to 10 minutes).");
            list.Add("This PC's hardware model and earnings (never your address) go into the real-earnings figures on otium.lol.");
            return list;
        }
    }

    [RelayCommand]
    private void OpenProxybaseTerms() => Launch.Open("https://proxybase.xyz/terms");

    // A double-click on Next would otherwise skip the estimate step entirely.
    private void GoTo(int step)
    {
        var now = Environment.TickCount64;
        if (now - _lastStepChange < 450)
            return;
        _lastStepChange = now;
        Step = Math.Clamp(step, 0, LastStep);
    }

    // ---------- estimate ----------

    [ObservableProperty] public partial bool IsEstimating { get; set; } = true;
    [ObservableProperty] public partial string MonthlyEstimate { get; set; } = "";
    [ObservableProperty] public partial string EstimateDetail { get; set; } = "";
    [ObservableProperty] public partial string? EstimateError { get; set; }
    [ObservableProperty] public partial bool IsEditingPrice { get; set; }
    [ObservableProperty] public partial decimal? ElectricityPrice { get; set; }
    [ObservableProperty] public partial bool UseGpu { get; set; }

    private readonly bool _ready;

    /// <summary>Whether electricity is subtracted at all; users whose power is included or free can say no.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DontPayForElectricity), nameof(ElectricityLine))]
    public partial bool PayForElectricity { get; set; }

    public bool DontPayForElectricity
    {
        get => !PayForElectricity;
        set => PayForElectricity = !value;
    }

    public string ElectricityLine
    {
        get
        {
            if (!_host.Settings.PayForElectricity)
                return "Electricity isn't counted, so Otium earns whenever your computer is free.";
            var line = $"Electricity at {Format.Money((decimal)_host.Settings.ElectricityPricePerKwh)}/kWh";
            if (_host.Settings.ElectricityPriceIsEstimate)
                line += $" (typical for {CountryName})";
            if (_host.ElectricityPrices.HasLivePrices && _host.ElectricityPrices.Zone is { } zone)
                line += $", following today's hourly prices in {zone.Code}";
            return line + ".";
        }
    }

    partial void OnPayForElectricityChanged(bool value)
    {
        if (!_ready)
            return;
        _host.Settings.PayForElectricity = value;
        if (!value)
            IsEditingPrice = false;
        _host.SaveSettings();
        _ = LoadEstimateAsync();
    }

    partial void OnUseGpuChanged(bool value)
    {
        OnPropertyChanged(nameof(FeeLine));
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
            if (_host.EffectiveGpuPolicy is null)
                await _host.RefreshGpuPolicyAsync(CancellationToken.None); // so graphics cards are priced at what they'd really mine
            await _host.RefreshElectricityAsync(CancellationToken.None); // live hourly prices where available
            OnPropertyChanged(nameof(ElectricityLine));
            var estimate = await _host.EstimateAsync(CancellationToken.None);
            var projection = Projection.Create(estimate.GrossUsdPerDay, estimate.ElectricityUsdPerDay, null, _host.Settings.OnlyWhenIdle, 0, 1);

            EstimateRows.Clear();
            foreach (var device in estimate.Devices)
            {
                var icon = Icon(device.Device == DeviceKind.Cpu ? "IconCpu" : "IconGpu");
                var what = device.Device == DeviceKind.Cpu ? "Mines Monero" : $"Mines whatever pays best (now {device.Coin})";
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

    /// <summary>"Help show what PCs really earn": ticked, and the user can untick it here or later in Settings.</summary>
    [ObservableProperty] public partial bool ShareEarnings { get; set; } = true;

    /// <summary>Pause the processor miner instead of stopping it when the user comes back: only if they tick it.</summary>
    [ObservableProperty] public partial bool KeepMinerReady { get; set; }

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

    private bool CanStart() => CanBegin && !IsStarting && InviteError is null && Payout.IsValid && (UseCpu || (HasGpu && UseGpu));

    [RelayCommand(CanExecute = nameof(CanStart))]
    private async Task Start()
    {
        IsStarting = true;
        _started(); // show the home screen right away; it reports progress while the account connects
        await _host.StartFirstTimeAsync(InviteCode, Payout.Target?.ToString(), ShareEarnings, KeepMinerReady, ShareInternet && CanShareInternet);
    }
}
