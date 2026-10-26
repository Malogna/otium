using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Otium.App.Services;
using Otium.Core.Bandwidth;
using Otium.Core.Wallets;
using Otium.Providers.Aioz;

namespace Otium.App.ViewModels;

/// <summary>
/// Opting in to (and out of) sharing storage and upload bandwidth through AIOZ's DePIN node, next to Proxybase and
/// Mysterium. AIOZ pays its AIOZ token, which Otium withdraws to the user's own 0x address; Otium takes nothing. Used by
/// the sharing setup and Advanced settings, so the explanation a user agrees to is the same everywhere. The node never
/// runs without that agreement.
/// </summary>
public partial class AiozSharingViewModel : ViewModelBase
{
    /// <summary>AIOZ publishes no separate terms for its node (October 2026); its source and licence are here.</summary>
    public const string TermsUrl = "https://github.com/AIOZNetwork/aioz-depin-cli";

    private readonly AppHost _host;
    private bool _loading = true;

    public AiozSharingViewModel(AppHost host)
    {
        _host = host;
        OnlyWhenAway = host.Settings.AiozOnlyWhenIdle;
        StorageLimitGb = (decimal)host.Settings.AiozStorageLimitGb;
        PayoutAddress = host.Settings.AiozPayoutAddress ?? "";
        _loading = false;
        host.AccountChanged += () => Dispatcher.UIThread.Post(Refresh);
        if (host.Aioz is { } sharer)
            sharer.SnapshotChanged += _ => Dispatcher.UIThread.Post(Refresh);
        Refresh();
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanTurnOn), nameof(ShowInSettings))]
    public partial bool IsOffered { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanTurnOn), nameof(ShowInSettings))]
    public partial bool IsOn { get; set; }

    public bool CanTurnOn => IsOffered && !IsOn;

    public bool ShowInSettings => IsOffered || IsOn;

    [ObservableProperty] public partial bool ShowConsent { get; set; }
    [ObservableProperty] public partial bool IsBusy { get; set; }
    [ObservableProperty] public partial string StatusText { get; set; } = "";
    [ObservableProperty] public partial string ProjectionText { get; set; } = "";
    [ObservableProperty] public partial string? Error { get; set; }
    [ObservableProperty] public partial bool OnlyWhenAway { get; set; }
    [ObservableProperty] public partial decimal? StorageLimitGb { get; set; }

    /// <summary>The user's own 0x address, asked for only when the payout address isn't an 0x address.</summary>
    [ObservableProperty] public partial string PayoutAddress { get; set; } = "";

    [ObservableProperty] public partial string? PayoutAddressError { get; set; }

    /// <summary>The payout address isn't an 0x address, so AIOZ needs one.</summary>
    public bool NeedsPayoutAddress =>
        PayoutTarget.ParseOrNull(_host.Settings.PayoutAddress) is not { Coin.Format: AddressFormat.Evm };

    /// <summary>Where the earnings go, once there's an address.</summary>
    public string PaidToText => _host.AiozPayoutAddress is { } address
        ? $"Paid in AIOZ to {Shorten(address)} on the AIOZ network."
        : "";

    /// <summary>The explanation users agree to. Plain words, no small print.</summary>
    public string ConsentText =>
        "Otium runs AIOZ's official node on this computer. AIOZ's network stores pieces of files on it and sends them " +
        "to viewers when asked (decentralized storage and video delivery), using some disk space and upload bandwidth. " +
        "The files are AIOZ customers' content, stored encrypted and in pieces; the node doesn't read your own files, " +
        "passwords or browsing, and it needs no administrator rights. Otium can't see or control what it stores. " +
        "Some internet providers don't allow this kind of sharing: check yours if you're not sure.\n\n" +
        (SetupText is { Length: > 0 } setup ? setup + "\n\n" : "") +
        $"AIOZ pays in its AIOZ token. The node collects it, and Otium sends it to your own 0x address on the AIOZ " +
        $"network once {AiozBandwidthProvider.WithdrawAtAioz:0} AIOZ has built up (at most once a day). Otium takes " +
        "nothing. Add the AIOZ network to your wallet to see it, and check your wallet or exchange supports AIOZ on " +
        "that network before you rely on it. AIOZ has many more nodes than paying files, so earnings may be small or " +
        "nothing for a while. AIOZ publishes no separate terms for its node; it's open source (Apache 2.0).";

    /// <summary>The node isn't Authenticode-signed, so Windows Security may stop it.</summary>
    private static string SetupText =>
        OperatingSystem.IsWindows()
            ? "Windows Security may flag AIOZ's node: Otium checks every download against the checksum AIOZ publishes. " +
              "If it's blocked, Otium offers to allow it in Windows Security."
            : "";

    public string LimitsText =>
        FormattableString.Invariant($"Uses at most {_host.Settings.AiozStorageLimitGb:0.#} GB of disk space (you can change this). ") +
        "Turn it off any time in Advanced settings.";

    public string UnavailableText =>
        AiozRelease.ForCurrentPlatform() is null && _host.Aioz?.Provider.UnavailableReason is { } reason ? reason
        : _host.Settings.PayoutMode != Core.Settings.PayoutMode.OwnWallet
            ? "Sharing through AIOZ pays your own wallet, so it's off while Otium holds a balance for you."
            : "Sharing through AIOZ isn't available in this version of Otium.";

    [RelayCommand]
    private static void OpenTerms() => Launch.Open(TermsUrl);

    private void Refresh()
    {
        var settings = _host.Settings;
        IsOffered = _host.AiozOffered;
        IsOn = settings.AiozEnabled && settings.AiozConsentAt is not null;
        OnPropertyChanged(nameof(LimitsText));
        OnPropertyChanged(nameof(UnavailableText));
        OnPropertyChanged(nameof(NeedsPayoutAddress));
        OnPropertyChanged(nameof(PaidToText));

        if (_host.Aioz is not { } sharer)
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
                BandwidthState.Sharing => "Sharing",
                BandwidthState.WaitingForIdle => "Waiting for you to step away",
                BandwidthState.Starting => snap.StatusText is { Length: > 0 } starting ? starting : "Connecting…",
                BandwidthState.Error => snap.StatusText,
                BandwidthState.Unavailable => snap.StatusText,
                _ => settings.EarningEnabled ? "Starting…" : "Paused with the rest of Otium",
            };
        ProjectionText = !IsOn ? ""
            : snap.UserUsdPerDay > 0
                ? $"At your recent pace that's about {Format.Usd(snap.UserUsdPerDay * 30)} a month in AIOZ at today's price."
                : "AIOZ's earnings depend on how many files its network sends this computer. Your real pace shows here once AIOZ comes in.";
    }

    partial void OnOnlyWhenAwayChanged(bool value) => Save();

    partial void OnStorageLimitGbChanged(decimal? value) => Save();

    private void Save()
    {
        if (_loading)
            return;
        _host.Settings.AiozOnlyWhenIdle = OnlyWhenAway;
        _host.Settings.AiozStorageLimitGb = (double)Math.Clamp(StorageLimitGb ?? 20, 1m, 1000);
        _host.SaveSettings();
        Refresh();
    }

    [RelayCommand]
    private void LearnMore() => ShowConsent = true;

    [RelayCommand]
    private void NotNow() => ShowConsent = false;

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
            _host.Settings.AiozPayoutAddress = PayoutAddress.Trim().ToLowerInvariant();
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
            await _host.SetAiozSharingAsync(on);
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

    private static string Shorten(string address) => address.Length > 12 ? $"{address[..6]}…{address[^4..]}" : address;
}
