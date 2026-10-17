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
        PolygonAddress = host.Settings.BandwidthPayoutAddress ?? "";
        _ = CheckServiceAsync();
        _loading = false;
        host.AccountChanged += () => Dispatcher.UIThread.Post(Refresh);
        host.Bandwidth.SnapshotChanged += _ => Dispatcher.UIThread.Post(Refresh);
        Refresh();
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanTurnOn))]
    public partial bool IsOffered { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanTurnOn), nameof(CanRemoveService))]
    public partial bool IsOn { get; set; }

    public bool CanTurnOn => IsOffered && !IsOn;

    [ObservableProperty] public partial bool ShowSuggestion { get; set; }
    [ObservableProperty] public partial bool ShowConsent { get; set; }
    [ObservableProperty] public partial bool IsBusy { get; set; }
    [ObservableProperty] public partial string EstimateText { get; set; } = "";
    [ObservableProperty] public partial string StatusText { get; set; } = "";
    [ObservableProperty] public partial string? Error { get; set; }
    [ObservableProperty] public partial bool OnlyWhenAway { get; set; }
    [ObservableProperty] public partial decimal? DailyLimitGb { get; set; }

    /// <summary>Mysterium pays a Polygon (0x…) address; asked for only when the payout address isn't one.</summary>
    [ObservableProperty] public partial string PolygonAddress { get; set; } = "";

    [ObservableProperty] public partial string? PolygonError { get; set; }

    public bool IsMysterium => _host.UsesMysterium;

    /// <summary>The user's payout address can't receive MYST on Polygon, so the consent screen asks for one.</summary>
    public bool NeedsPolygonAddress => IsMysterium &&
        Core.Wallets.PayoutTarget.ParseOrNull(_host.Settings.PayoutAddress) is not { Coin.Format: Core.Wallets.AddressFormat.Evm };

    public string MysteriumTermsUrl => "https://www.mysterium.network/terms-conditions";

    /// <summary>Mysterium's network service, installed by Otium, is still there while sharing is off.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanRemoveService))]
    public partial bool ServiceInstalled { get; set; }

    [ObservableProperty] public partial string? ServiceMessage { get; set; }

    public bool CanRemoveService => ServiceInstalled && !IsOn;

    private async Task CheckServiceAsync()
    {
        if (!IsMysterium || !OperatingSystem.IsWindows())
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
    private void OpenMysteriumTerms() => Launch.Open(MysteriumTermsUrl);

    public string UnavailableText => IsMysterium
        ? Otium.Providers.Mysterium.MystRelease.ForCurrentPlatform() is null
            ? "Internet sharing through Mysterium isn't available on this computer yet (Windows only for now)."
            : "Internet sharing isn't available from this Otium server yet."
        : _host.Bandwidth.Provider.UnavailableReason
          ?? (_host.Settings.PayoutMode == Core.Settings.PayoutMode.Balance
              ? "Internet sharing isn't available right now."
              : "Internet sharing pays into an Otium balance, so it's off while you're paid to your own wallet.");

    public string PartnerName => _host.Settings.Account.BandwidthPartnerName is { Length: > 0 } name ? name : "Otium's partner";

    /// <summary>The explanation users agree to. Plain words, no small print.</summary>
    public string ConsentText => IsMysterium
        ? "Otium runs a Mysterium Network node on this computer. Mysterium's business customers use a small part of your " +
          "unused internet connection to load public web pages and move data, for things like price comparison, ad checks " +
          "and market research; only business services are switched on, not the anonymous public VPN. Websites they visit " +
          "see your internet address. Nothing on your computer is accessed: not your files, passwords or browsing.\n\n" +
          "Mysterium pays in MYST, straight to your own Polygon address, automatically once 5 MYST has built up (it keeps " +
          "20%). Otium pays the node's one-time registration and takes nothing from these earnings. Turning this on means " +
          "you agree to Mysterium's terms for node runners, and Windows asks once for administrator permission to set up " +
          "Mysterium's network service."
        : $"{PartnerName} uses a small part of your unused internet connection to load public web pages for its business " +
        "customers, for things like price comparison, ad checks and market research. Websites they visit see your " +
        "internet address. Nothing on your computer is accessed: not your files, passwords or browsing.";

    public string LimitsText => IsMysterium
        ? FormattableString.Invariant($"Uses at most {_host.Settings.BandwidthDailyLimitGb:0.#} GB a day (you can change this). ") +
          "Earnings vary a lot with demand in your country: independent estimates say a dollar or two a month for an always-on " +
          "computer. Turn it off any time in Advanced settings."
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
        EstimateText = IsMysterium ? "usually a dollar or two a month, paid in MYST" : month > 0 ? $"about {Format.Usd(month)} a month extra" : "";
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
        if (NeedsPolygonAddress)
        {
            if (!Core.Wallets.CryptoAddresses.TryValidate(Core.Wallets.AddressFormat.Evm, PolygonAddress, out var error))
            {
                PolygonError = error ?? "That isn't a valid Polygon address. Copy your 0x… address from your wallet.";
                return Task.CompletedTask;
            }
            PolygonError = null;
            _host.Settings.BandwidthPayoutAddress = PolygonAddress.Trim().ToLowerInvariant();
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
