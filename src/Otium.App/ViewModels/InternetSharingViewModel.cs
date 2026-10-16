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
        _loading = false;
        host.AccountChanged += () => Dispatcher.UIThread.Post(Refresh);
        host.Bandwidth.SnapshotChanged += _ => Dispatcher.UIThread.Post(Refresh);
        Refresh();
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanTurnOn))]
    public partial bool IsOffered { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanTurnOn))]
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

    public string UnavailableText => _host.Bandwidth.Provider.UnavailableReason
                                     ?? (_host.Settings.PayoutMode == Core.Settings.PayoutMode.Balance
                                         ? "Internet sharing isn't available right now."
                                         : "Internet sharing pays into an Otium balance, so it's off while you're paid to your own wallet.");

    public string PartnerName => _host.Settings.Account.BandwidthPartnerName is { Length: > 0 } name ? name : "Otium's partner";

    /// <summary>The explanation users agree to. Plain words, no small print.</summary>
    public string ConsentText =>
        $"{PartnerName} uses a small part of your unused internet connection to load public web pages for its business " +
        "customers, for things like price comparison, ad checks and market research. Websites they visit see your " +
        "internet address. Nothing on your computer is accessed: not your files, passwords or browsing.";

    public string LimitsText =>
        FormattableString.Invariant($"Uses at most {_host.Settings.BandwidthDailyLimitGb:0.#} GB a day (you can change this). ") +
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
        EstimateText = month > 0 ? $"about {Format.Usd(month)} a month extra" : "";
        OnPropertyChanged(nameof(LimitsText));
        OnPropertyChanged(nameof(ConsentText));
        OnPropertyChanged(nameof(UnavailableText));

        var snap = _host.Bandwidth.Snapshot;
        StatusText = !IsOn ? "Off"
            : snap.State switch
            {
                BandwidthState.Sharing => $"Sharing · {FormatBytes(snap.BytesToday)} today",
                BandwidthState.WaitingForIdle => "Waiting for you to step away",
                BandwidthState.LimitReached => "Today's limit is reached; sharing continues tomorrow",
                BandwidthState.Starting => "Connecting…",
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
    private Task Agree() => SetAsync(true);

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
