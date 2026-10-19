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
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EstimateSentence))]
    public partial string EstimateText { get; set; } = "";

    /// <summary>"Typically …." for the suggestion card, or nothing when there's no estimate.</summary>
    public string EstimateSentence => EstimateText.Length > 0 ? $"Typically {EstimateText}." : "";
    [ObservableProperty] public partial string StatusText { get; set; } = "";

    /// <summary>What sharing looks set to earn: the user's own pace once there is one, before that Proxybase's claim.</summary>
    [ObservableProperty] public partial string ProjectionText { get; set; } = "";
    [ObservableProperty] public partial string? Error { get; set; }
    [ObservableProperty] public partial bool OnlyWhenAway { get; set; }
    [ObservableProperty] public partial decimal? DailyLimitGb { get; set; }

    /// <summary>Proxybase pays an 0x address (USDC on Tempo); asked for only when the payout address isn't one.</summary>
    [ObservableProperty] public partial string PayoutAddress { get; set; } = "";

    [ObservableProperty] public partial string? PayoutAddressError { get; set; }

    public bool IsProxybase => _host.UsesProxybase;

    /// <summary>
    /// Mysterium (earlier versions) needed a Polygon address; Proxybase's earnings are converted to the user's own payout
    /// coin, so no extra address is asked for.
    /// </summary>
    public bool NeedsPayoutAddress => false;

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
          "CAPTCHAs. The client only passes their connections on; it doesn't read your files, passwords or browsing. " +
          "Otium can't see or control this traffic, and Proxybase doesn't check who its customers are, so if someone " +
          "misuses it, it shows your internet address. Some internet providers don't allow sharing a connection: " +
          "check yours if you're not sure.\n\n" +
          "Proxybase pays per GB. When enough has built up, Otium has it converted and paid straight to your payout " +
          "address, in the coin you chose, like your mining earnings. Otium keeps the same share as for mining, paid to " +
          "Otium from your sharing earnings, and the conversion's network costs come off each payout. Turning this on " +
          "means you agree to Proxybase's terms."
        : $"{PartnerName} uses a small part of your unused internet connection to load public web pages for its business " +
        "customers, for things like price comparison, ad checks and market research. Websites they visit see your " +
        "internet address. The client only passes their connections on; it doesn't read your files, passwords or browsing.";

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
        EstimateText = IsProxybase ? $"about {Format.Usd(EstimatedUsdPerDay * 30)} a month by Proxybase's rates, paid in your coin"
            : month > 0 ? $"about {Format.Usd(month)} a month extra" : "";
        OnPropertyChanged(nameof(LimitsText));
        OnPropertyChanged(nameof(ConsentText));
        OnPropertyChanged(nameof(UnavailableText));

        var snap = _host.Bandwidth.Snapshot;
        StatusText = !IsOn ? "Off"
            : snap.State switch
            {
                BandwidthState.Sharing when snap.Earnings is { } earnings => $"Sharing · {earnings}", // reports money, not traffic
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

        // Proxybase pays per GB its customers actually use, so the pace depends on their demand where you are, not on
        // how fast the connection is: a projection from speed would only mislead. Show the real pace once it exists.
        ProjectionText = !IsOn || !IsProxybase ? ""
            : snap.UserUsdPerDay > 0
                ? $"At your recent pace that's about {Format.Usd(snap.UserUsdPerDay * 30)} a month (your share, after Otium's fee)."
                : EstimateExplanation() + " Your real pace replaces this after the first hour of earnings.";
    }

    /// <summary>Proxybase's rate where this computer is (its markets page).</summary>
    public Otium.Providers.Proxybase.ProxybaseMarket Market
    {
        get
        {
            try
            {
                return Otium.Providers.Proxybase.ProxybaseMarkets.For(System.Globalization.RegionInfo.CurrentRegion.TwoLetterISORegionName);
            }
            catch (ArgumentException)
            {
                return Otium.Providers.Proxybase.ProxybaseMarkets.For(null);
            }
        }
    }

    /// <summary>
    /// Before this computer has its own pace: what Proxybase's rate here pays for 1 GB a day (the low end of its
    /// "1–5 GB a day" average for a home connection), after Otium's fee.
    /// </summary>
    public double EstimatedUsdPerDay => IsProxybase
        ? Otium.Providers.Proxybase.ProxybaseMarkets.EstimatedUserUsdPerDay(Market, _host.Fees.FeePercent / 100)
        : 0;

    private string EstimateExplanation()
    {
        var market = Market;
        var rate = Format.Usd(market.SellerUsdPerGb);
        var where = market.Listed
            ? $"Proxybase pays {rate} per GB in {market.CountryName}"
            : $"Proxybase doesn't list {market.CountryName} on its own, so this uses its worldwide rate of {rate} per GB";
        return $"Estimate: about {Format.Usd(EstimatedUsdPerDay * 30)} a month. {where}, and says a home connection carries " +
               "1–5 GB a day on average; this assumes the low end, 1 GB, after Otium's fee. What you really earn depends on how " +
               "much Proxybase's customers use your connection.";
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
