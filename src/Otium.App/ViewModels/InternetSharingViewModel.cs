using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Otium.App.Services;
using Otium.Core.Bandwidth;

namespace Otium.App.ViewModels;

/// <summary>
/// Opting in to (and out of) sharing unused internet bandwidth. Used by the overview's suggestion card and by Advanced
/// settings, so the explanation a user agrees to is the same everywhere. Sharing never starts without that agreement.
/// </summary>
public partial class InternetSharingViewModel : ViewModelBase
{
    /// <summary>Partners pay little per GB, so estimates assume a modest amount of demand.</summary>
    private const double TypicalGbPerDay = 0.5;

    private readonly AppHost _host;
    private bool _loading = true;

    public InternetSharingViewModel(AppHost host)
    {
        _host = host;
        OnlyWhenAway = host.Settings.BandwidthOnlyWhenIdle;
        DailyLimitGb = (decimal)host.Settings.BandwidthDailyLimitGb;
        PayoutAddress = host.Settings.BandwidthPayoutAddress ?? "";
        _ = CheckServiceAsync();
        _loading = false;
        host.AccountChanged += () => Dispatcher.UIThread.Post(Refresh);
        host.Bandwidth.SnapshotChanged += _ => Dispatcher.UIThread.Post(Refresh);
        Refresh();
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanTurnOn), nameof(ShowInSettings))]
    public partial bool IsOffered { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanTurnOn), nameof(CanRemoveService), nameof(ShowInSettings))]
    public partial bool IsOn { get; set; }

    public bool CanTurnOn => IsOffered && !IsOn;

    /// <summary>
    /// Settings shows internet sharing only when the server offers it, it's on, or its Windows service is still installed:
    /// it's off for launch (it earns little), so most people never see it.
    /// </summary>
    public bool ShowInSettings => IsOffered || IsOn || CanRemoveService;

    [ObservableProperty] public partial bool ShowSuggestion { get; set; }
    [ObservableProperty] public partial bool ShowConsent { get; set; }
    [ObservableProperty] public partial bool IsBusy { get; set; }
    [ObservableProperty] public partial string EstimateText { get; set; } = "";
    [ObservableProperty] public partial string StatusText { get; set; } = "";
    [ObservableProperty] public partial string? Error { get; set; }
    [ObservableProperty] public partial bool OnlyWhenAway { get; set; }
    [ObservableProperty] public partial decimal? DailyLimitGb { get; set; }

    /// <summary>Proxybase pays an 0x address (USDC on Tempo); asked for only when the payout address isn't one.</summary>
    [ObservableProperty] public partial string PayoutAddress { get; set; } = "";

    [ObservableProperty] public partial string? PayoutAddressError { get; set; }

    public bool IsProxybase => _host.UsesProxybase;

    /// <summary>The user's payout address can't receive USDC on Tempo, so the consent screen asks for an 0x address.</summary>
    public bool NeedsPayoutAddress => IsProxybase &&
        Core.Wallets.PayoutTarget.ParseOrNull(_host.Settings.PayoutAddress) is not { Coin.Format: Core.Wallets.AddressFormat.Evm };

    /// <summary>Proxybase reports no byte counts Otium can see, so there's no daily data limit to set.</summary>
    public bool SupportsDailyLimit => !IsProxybase;

    public string TermsUrl => "https://proxybase.xyz/terms";

    /// <summary>Mysterium's network service, installed by Otium, is still there while sharing is off.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanRemoveService), nameof(ShowInSettings))]
    public partial bool ServiceInstalled { get; set; }

    [ObservableProperty] public partial string? ServiceMessage { get; set; }

    public bool CanRemoveService => ServiceInstalled && !IsOn;

    /// <summary>Earlier versions shared through Mysterium, whose Windows service may still be installed.</summary>
    private async Task CheckServiceAsync()
    {
        if (!OperatingSystem.IsWindows())
            return;
        var engines = _host.Paths.EnginesDir;
        ServiceInstalled = await Task.Run(() => Otium.Providers.Mysterium.WindowsMystSupervisor.OurServiceBinary(engines) is not null);
    }

    [RelayCommand]
    private async Task RemoveService()
    {
        IsBusy = true;
        var engines = _host.Paths.EnginesDir;
        var problem = await Task.Run(() => Otium.Providers.Mysterium.WindowsMystSupervisor.RemoveIfOurs(engines, TimeSpan.FromMinutes(2)));
        ServiceMessage = problem ?? "Removed. Turning internet sharing on again would set it up again.";
        await CheckServiceAsync();
        IsBusy = false;
    }

    [RelayCommand]
    private void OpenTerms() => Launch.Open(TermsUrl);

    public string UnavailableText => IsProxybase
        ? Otium.Providers.Proxybase.ProxybaseRelease.ForCurrentPlatform() is null
            ? "Internet sharing through Proxybase isn't available on this computer (it needs Windows, Linux, or a Mac with Apple silicon)."
            : _host.Settings.PayoutMode != Core.Settings.PayoutMode.OwnWallet
                ? "Internet sharing pays your own wallet, so it's off while Otium holds a balance for you."
                : "Internet sharing is switched off on this Otium server for now."
        : _host.Bandwidth.Provider.UnavailableReason
          ?? (_host.Settings.PayoutMode == Core.Settings.PayoutMode.Balance
              ? "Internet sharing isn't available right now."
              : "Internet sharing pays into an Otium balance, so it's off while you're paid to your own wallet.");

    public string PartnerName => _host.Settings.Account.BandwidthPartnerName is { Length: > 0 } name ? name : "Otium's partner";

    /// <summary>The explanation users agree to. Plain words, no small print.</summary>
    public string ConsentText => IsProxybase
        ? "Otium runs Proxybase's open-source client on this computer. Proxybase's customers, mostly AI agents and " +
          "businesses that collect public web pages or check ads, send some of their web traffic through your unused " +
          "internet connection. Websites they visit see your internet address, so sites may sometimes show you more " +
          "CAPTCHAs. Nothing on your computer is accessed: not your files, passwords or browsing.\n\n" +
          "Proxybase pays per GB. Whenever $1 has built up, Otium has it paid straight to your own 0x address, in USDC on " +
          "the Tempo network (add Tempo to your wallet to see it). Otium takes nothing from these earnings. Turning this on " +
          "means you agree to Proxybase's terms."
        : $"{PartnerName} uses a small part of your unused internet connection to load public web pages for its business " +
        "customers, for things like price comparison, ad checks and market research. Websites they visit see your " +
        "internet address. Nothing on your computer is accessed: not your files, passwords or browsing.";

    public string LimitsText => IsProxybase
        ? "There's no daily data cap: your connection is used whenever Proxybase's customers need an address like yours" +
          (_host.Settings.BandwidthOnlyWhenIdle ? ", and only while you're away. " : ". ") +
          "Earnings depend on demand in your country; most home connections see modest traffic, so expect a few dollars a " +
          "month rather than the rates Proxybase advertises. Turn it off any time in Advanced settings."
        : FormattableString.Invariant($"Uses at most {_host.Settings.BandwidthDailyLimitGb:0.#} GB a day (you can change this). ") +
          "Only one computer per internet connection is paid. Turn it off any time in Advanced settings.";

    /// <summary>What sharing could add per day, for the overview's projections.</summary>
    public double ExpectedUsdPerDay
    {
        get
        {
            var account = _host.Settings.Account;
            var hours = _host.Settings.BandwidthOnlyWhenIdle ? 8 : 16;
            var gb = Math.Min(TypicalGbPerDay, _host.Settings.BandwidthDailyLimitGb);
            return account.BandwidthUserUsdPerSharingHour * hours + account.BandwidthUserUsdPerGb * gb;
        }
    }

    private void Refresh()
    {
        var settings = _host.Settings;
        IsOffered = _host.BandwidthOffered;
        IsOn = settings.BandwidthEnabled && settings.BandwidthConsentAt is not null;
        ShowSuggestion = IsOffered && !IsOn && !settings.BandwidthPromptDismissed;
        var month = ExpectedUsdPerDay * 30;
        EstimateText = IsProxybase ? "usually a few dollars a month, paid in USDC" : month > 0 ? $"about {Format.Usd(month)} a month extra" : "";
        OnPropertyChanged(nameof(LimitsText));
        OnPropertyChanged(nameof(ConsentText));
        OnPropertyChanged(nameof(UnavailableText));

        var snap = _host.Bandwidth.Snapshot;
        StatusText = !IsOn ? "Off"
            : snap.State switch
            {
                BandwidthState.Sharing when snap.Earnings is { } earnings => $"Sharing · {FormatBytes(snap.BytesToday)} today · {earnings}",
                BandwidthState.Sharing => $"Sharing · {FormatBytes(snap.BytesToday)} today",
                BandwidthState.WaitingForIdle => "Waiting for you to step away",
                BandwidthState.LimitReached => "Today's limit is reached; sharing continues tomorrow",
                BandwidthState.Starting => snap.StatusText is { Length: > 0 } starting ? starting : "Connecting…",
                BandwidthState.Error => snap.StatusText,
                BandwidthState.Unavailable => snap.StatusText,
                _ => settings.EarningEnabled ? "Starting…" : "Paused with the rest of Otium",
            };
        if (snap.Note is { } note && IsOn)
            StatusText += $". {note}";
    }

    partial void OnOnlyWhenAwayChanged(bool value) => Save();

    partial void OnDailyLimitGbChanged(decimal? value) => Save();

    private void Save()
    {
        if (_loading)
            return;
        _host.Settings.BandwidthOnlyWhenIdle = OnlyWhenAway;
        _host.Settings.BandwidthDailyLimitGb = (double)Math.Clamp(DailyLimitGb ?? 2, 0.1m, 100);
        _host.SaveSettings();
        Refresh();
    }

    [RelayCommand]
    private void LearnMore() => ShowConsent = true;

    [RelayCommand]
    private void NotNow()
    {
        ShowConsent = false;
        _host.Settings.BandwidthPromptDismissed = true;
        _host.SaveSettings();
        Refresh();
    }

    [RelayCommand]
    private Task Agree()
    {
        if (NeedsPayoutAddress)
        {
            if (!Core.Wallets.CryptoAddresses.TryValidate(Core.Wallets.AddressFormat.Evm, PayoutAddress, out var error))
            {
                PayoutAddressError = error ?? "That isn't a valid 0x address. Copy it from your wallet.";
                return Task.CompletedTask;
            }
            PayoutAddressError = null;
            _host.Settings.BandwidthPayoutAddress = PayoutAddress.Trim().ToLowerInvariant();
            _host.SaveSettings();
        }
        return SetAsync(true);
    }

    [RelayCommand]
    private Task TurnOff() => SetAsync(false);

    private async Task SetAsync(bool on)
    {
        IsBusy = true;
        Error = null;
        try
        {
            await _host.SetBandwidthSharingAsync(on);
            ShowConsent = false;
            await CheckServiceAsync();
        }
        catch (InvalidOperationException e)
        {
            Error = e.Message;
        }
        finally
        {
            IsBusy = false;
            Refresh();
        }
    }

    public static string FormatBytes(long bytes) =>
        bytes >= 1024L * 1024 * 1024 ? FormattableString.Invariant($"{bytes / (1024d * 1024 * 1024):0.0} GB")
        : bytes >= 1024 * 1024 ? FormattableString.Invariant($"{bytes / (1024d * 1024):0} MB")
        : FormattableString.Invariant($"{bytes / 1024d:0} KB");
}
