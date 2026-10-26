using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Otium.App.Services;
using Otium.Core.Bandwidth;
using Otium.Core.Wallets;
using Otium.Providers.Mysterium;

namespace Otium.App.ViewModels;

/// <summary>
/// Opting in to (and out of) internet sharing through Mysterium, next to Proxybase. Its node carries Mysterium's
/// business customers' traffic only and pays MYST straight to the user's own Polygon address; Otium takes nothing and
/// pays the one-time registration. Used by the overview's suggestion card and by Advanced settings, so the explanation
/// a user agrees to is the same everywhere. The node never runs without that agreement.
/// </summary>
public partial class MysteriumSharingViewModel : ViewModelBase
{
    /// <summary>"Usually a dollar or two a month" (Mysterium, October 2026), for projections before the node has a pace.</summary>
    public const double TypicalUsdPerDay = 1.5 / 30;

    public const string TermsUrl = "https://www.mysterium.network/terms-conditions";

    /// <summary>Swaps MYST on Polygon for other coins (SideShift and ChangeNOW don't list MYST, October 2026).</summary>
    public const string SwapUrl = "https://relay.link/swap";

    private readonly AppHost _host;
    private bool _loading = true;

    public MysteriumSharingViewModel(AppHost host)
    {
        _host = host;
        OnlyWhenAway = host.Settings.MysteriumOnlyWhenIdle;
        DailyLimitGb = (decimal)host.Settings.MysteriumDailyLimitGb;
        PayoutAddress = host.Settings.MysteriumPayoutAddress ?? "";
        _ = CheckServiceAsync();
        _loading = false;
        host.AccountChanged += () => Dispatcher.UIThread.Post(Refresh);
        if (host.Mysterium is { } sharer)
            sharer.SnapshotChanged += _ => Dispatcher.UIThread.Post(Refresh);
        Refresh();
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanTurnOn), nameof(ShowInSettings))]
    public partial bool IsOffered { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanTurnOn), nameof(CanRemoveService), nameof(ShowInSettings))]
    public partial bool IsOn { get; set; }

    public bool CanTurnOn => IsOffered && !IsOn;

    /// <summary>Settings shows Mysterium when it's offered here, on, or its network service is still installed.</summary>
    public bool ShowInSettings => IsOffered || IsOn || CanRemoveService;

    [ObservableProperty] public partial bool ShowSuggestion { get; set; }
    [ObservableProperty] public partial bool ShowConsent { get; set; }
    [ObservableProperty] public partial bool IsBusy { get; set; }
    [ObservableProperty] public partial string StatusText { get; set; } = "";
    [ObservableProperty] public partial string ProjectionText { get; set; } = "";
    [ObservableProperty] public partial string? Error { get; set; }
    [ObservableProperty] public partial bool OnlyWhenAway { get; set; }
    [ObservableProperty] public partial decimal? DailyLimitGb { get; set; }

    /// <summary>The user's own Polygon (0x…) address, asked for only when the payout address isn't an 0x address.</summary>
    [ObservableProperty] public partial string PayoutAddress { get; set; } = "";

    [ObservableProperty] public partial string? PayoutAddressError { get; set; }

    /// <summary>The payout address isn't an 0x address, so Mysterium needs a Polygon one.</summary>
    public bool NeedsPayoutAddress =>
        PayoutTarget.ParseOrNull(_host.Settings.PayoutAddress) is not { Coin.Format: AddressFormat.Evm };

    /// <summary>Where the earnings go, once there's an address.</summary>
    public string PaidToText => _host.MysteriumBeneficiary is { } address
        ? $"Paid in MYST to {Shorten(address)} on the Polygon network."
        : "";

    public string EstimateText => "usually a dollar or two a month, paid in MYST";

    /// <summary>The explanation users agree to. Plain words, no small print.</summary>
    public string ConsentText =>
        "Otium runs Mysterium's open-source node on this computer, set to carry traffic for Mysterium's business " +
        "customers only: companies that collect public web pages, move data or run business VPNs. Websites they visit " +
        "see your internet address, so sites may sometimes show you more CAPTCHAs. The node only passes their " +
        "connections on; it doesn't read your files, passwords or browsing. Otium can't see or control this traffic. " +
        "Some internet providers don't allow sharing a connection: check yours if you're not sure.\n\n" +
        (SetupText is { Length: > 0 } setup ? setup + "\n\n" : "") +
        "Mysterium pays in its MYST token, on the Polygon network, straight to your own address: automatically once " +
        "5 MYST has built up, minus Mysterium's 20% network fee. Otium takes nothing from it and pays the one-time " +
        "registration for you. You can swap MYST for another coin on Relay (relay.link). Turning this on means you " +
        "agree to Mysterium's terms.";

    /// <summary>The node needs no administrator rights (it runs in userspace mode); only Windows Security may get in the way.</summary>
    private static string SetupText =>
        OperatingSystem.IsWindows()
            ? "Windows Security may flag Mysterium's node as harmful: it's open source, and Otium checks every download " +
              "against the checksum Mysterium publishes. If it's blocked, Otium offers to allow it in Windows Security."
            : "";

    public string LimitsText =>
        FormattableString.Invariant($"Uses at most {_host.Settings.MysteriumDailyLimitGb:0.#} GB a day (you can change this). ") +
        "Mysterium pays one computer per internet connection. Turn it off any time in Advanced settings.";

    public string UnavailableText =>
        _host.Mysterium?.Provider.UnavailableReason is { } reason && MystRelease.ForCurrentPlatform() is null ? reason
        : _host.Settings.PayoutMode != Core.Settings.PayoutMode.OwnWallet
            ? "Sharing through Mysterium pays your own wallet, so it's off while Otium holds a balance for you."
            : "Sharing through Mysterium is switched off on this Otium server for now.";

    /// <summary>Mysterium's network service, installed by Otium, is still there while this sharing is off.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanRemoveService), nameof(ShowInSettings))]
    public partial bool ServiceInstalled { get; set; }

    [ObservableProperty] public partial string? ServiceMessage { get; set; }

    /// <summary>
    /// A network service a test version installed is still there. The node doesn't use it any more (userspace mode),
    /// so it can go whether or not sharing through Mysterium is on.
    /// </summary>
    public bool CanRemoveService => ServiceInstalled;

    private async Task CheckServiceAsync()
    {
        var engines = _host.Paths.EnginesDir;
        ServiceInstalled = await Task.Run(() => MystSupervisors.OursInstalled(engines));
    }

    [RelayCommand]
    private async Task RemoveService()
    {
        IsBusy = true;
        var engines = _host.Paths.EnginesDir;
        var problem = await Task.Run(() => MystSupervisors.RemoveOurs(engines, TimeSpan.FromMinutes(2)));
        ServiceMessage = problem ?? "Removed. Turning Mysterium sharing on again would set it up again.";
        await CheckServiceAsync();
        IsBusy = false;
    }

    [RelayCommand]
    private static void OpenTerms() => Launch.Open(TermsUrl);

    [RelayCommand]
    private static void OpenSwap() => Launch.Open(SwapUrl);

    /// <summary>What Mysterium could add per day, for the overview's projections: the node's own pace, or the typical amount.</summary>
    public double ExpectedUsdPerDay =>
        _host.Mysterium?.Snapshot is { UserUsdPerDay: > 0 } snap ? snap.UserUsdPerDay : TypicalUsdPerDay;

    private void Refresh()
    {
        var settings = _host.Settings;
        IsOffered = _host.MysteriumOffered;
        IsOn = settings.MysteriumEnabled && settings.MysteriumConsentAt is not null;
        // Suggested only once the user is set up, and not at the same time as Proxybase's own suggestion.
        ShowSuggestion = IsOffered && !IsOn && !settings.MysteriumPromptDismissed && settings.OnboardingComplete
                         && (settings.BandwidthPromptDismissed || settings.BandwidthConsentAt is not null);
        OnPropertyChanged(nameof(LimitsText));
        OnPropertyChanged(nameof(UnavailableText));
        OnPropertyChanged(nameof(NeedsPayoutAddress));
        OnPropertyChanged(nameof(PaidToText));

        if (_host.Mysterium is not { } sharer)
        {
            StatusText = "";
            ProjectionText = "";
            return;
        }
        var snap = sharer.Snapshot;
        StatusText = !IsOn ? "Off"
            : snap.State switch
            {
                BandwidthState.Sharing when snap.Earnings is { } earnings => $"Sharing · {earnings}",
                BandwidthState.Sharing => $"Sharing · {InternetSharingViewModel.FormatBytes(snap.BytesToday)} today",
                BandwidthState.WaitingForIdle => "Waiting for you to step away",
                BandwidthState.LimitReached => "Today's limit is reached; sharing continues tomorrow",
                BandwidthState.Starting => snap.StatusText is { Length: > 0 } starting ? starting : "Connecting…",
                BandwidthState.Error => snap.StatusText,
                BandwidthState.Unavailable => snap.StatusText,
                _ => settings.EarningEnabled ? "Starting…" : "Paused with the rest of Otium",
            };
        ProjectionText = !IsOn ? ""
            : snap.UserUsdPerDay > 0
                ? $"At your recent pace that's about {Format.Usd(snap.UserUsdPerDay * 30)} a month in MYST at today's price, after Mysterium's fee."
                : "Mysterium says a home connection usually earns a dollar or two a month. Your real pace shows here once MYST comes in.";
    }

    partial void OnOnlyWhenAwayChanged(bool value) => Save();

    partial void OnDailyLimitGbChanged(decimal? value) => Save();

    private void Save()
    {
        if (_loading)
            return;
        _host.Settings.MysteriumOnlyWhenIdle = OnlyWhenAway;
        _host.Settings.MysteriumDailyLimitGb = (double)Math.Clamp(DailyLimitGb ?? 5, 0.1m, 100);
        _host.SaveSettings();
        Refresh();
    }

    [RelayCommand]
    private void LearnMore() => ShowConsent = true;

    [RelayCommand]
    private void NotNow()
    {
        ShowConsent = false;
        _host.Settings.MysteriumPromptDismissed = true;
        _host.SaveSettings();
        Refresh();
    }

    [RelayCommand]
    private Task Agree()
    {
        if (NeedsPayoutAddress)
        {
            if (!CryptoAddresses.TryValidate(AddressFormat.Evm, PayoutAddress, out var error))
            {
                PayoutAddressError = error ?? "That isn't a valid 0x address. Copy it from your wallet.";
                return Task.CompletedTask;
            }
            PayoutAddressError = null;
            _host.Settings.MysteriumPayoutAddress = PayoutAddress.Trim().ToLowerInvariant();
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
            await _host.SetMysteriumSharingAsync(on);
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

    private static string Shorten(string address) => address.Length > 12 ? $"{address[..6]}…{address[^4..]}" : address;
}
